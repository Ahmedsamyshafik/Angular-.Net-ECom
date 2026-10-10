using Asp.Versioning;
using Ecom.Core.Services;
using ECom.Api.Exceptions;
using ECom.Api.Middleware;
using ECom.Api.Registerations;
using ECom.Infrastructure.Registerations;
using FluentValidation;
using Hangfire;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// 1. Add CORS Service
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngularApp",
        policy =>
        {
            policy.WithOrigins("http://localhost:4200") // Port الـ Angular
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials();
        });
});

builder.Services.AddControllers();
builder.Services.AddOpenApi();
// API Versioning: both readers are registered globally.
// - CategoryController reads the version from the URL segment (api/v1/Category/...).
// - ProductController reads the version from the api-version header.
// Default 1.0 keeps calls that omit a version working against the only version present.
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
    options.ApiVersionReader = ApiVersionReader.Combine(
        new UrlSegmentApiVersionReader(),
        new HeaderApiVersionReader("api-version"));
})
.AddMvc();
// Global error contract: successful responses keep the standard responseApi,
// errors are converted to ProblemDetails by GlobalExceptionHandler.
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
// Rate limiting registration: global sliding window + per-client token bucket (UserId/IP)
builder.Services.RateLimitingConfigure(builder.Configuration);
//infrastructure registration
builder.Services.InfrastructureConfigure(builder.Configuration);
//Hangfire registration
builder.Services.HangFireConfigure(builder.Configuration);
//FluentValidation: registers every IValidator<T> from this assembly
builder.Services.AddValidatorsFromAssembly(typeof(Program).Assembly);

var app = builder.Build();



// Global exception handling (outermost middleware) - maps exceptions to ProblemDetails
app.UseExceptionHandler();

// HSTS: only outside Development (never sent to localhost). Must be added before
// UseHttpsRedirection so HTTPS responses advertise Strict-Transport-Security.
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

// Security headers: X-Content-Type-Options, X-Frame-Options, Referrer-Policy on every
// response (Hangfire dashboard, static files, API, and rate-limit 429 short-circuits).
// No Content-Security-Policy: JSON API + would risk breaking Scalar / Hangfire UI.
app.UseSecurityHeaders();

// HangFire Dashboard configuration
app.UseHangfireDashboard("/hangfire");
// Schedule the recurring job to purge old deleted images every 3 months
RecurringJob.AddOrUpdate<IImageManagementService>(
    "purge-old-deleted-images",
    service => service.PurgeOldDeletedImagesAsync(),
    "0 1 */3 * *"
);
// 2. Enable Static Files (علشان يقدر يخدم الصور من wwwroot)
app.UseStaticFiles();

// 3. Enable CORS Middleware (لازم يتحط قبل app.MapControllers)
app.UseCors("AllowAngularApp"); 

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();

// Rate limiting: applies the global sliding window to every request, then the
// per-client token bucket on endpoints that opt in via [EnableRateLimiting].
// Placed after CORS (so 429 responses still carry CORS headers for the Angular
// app) and before authorization/endpoints (so every mapped endpoint is protected).
app.UseRateLimiter();

app.UseAuthorization();

app.MapControllers();

app.Run();
