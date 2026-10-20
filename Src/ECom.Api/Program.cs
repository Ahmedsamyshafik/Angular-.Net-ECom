using Asp.Versioning;

using Ecom.Core.DTO.StripePayment;
using Ecom.Core.Services;

using ECom.Api.Exceptions;
using ECom.Api.Middleware;
using ECom.Api.Registerations;
using ECom.Infrastructure.Application.Services;
using ECom.Infrastructure.Data;
using ECom.Infrastructure.Registerations;

using FluentValidation;

using Hangfire;

using Scalar.AspNetCore;

using Stripe;

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
//Payment
StripeConfiguration.ApiKey = builder.Configuration["Stripe:SecretKey"];
builder.Services.Configure<StripeSettings>(builder.Configuration.GetSection("Stripe"));

var app = builder.Build();

// Bootstrap Identity (Admin role + optional admin promotion via Seed:AdminEmail) before
// serving requests. Idempotent; if the database is momentarily unavailable the API must
// still start, so failures are logged instead of thrown.
try
{
    await IdentitySeeder.SeedAsync(app.Services, app.Configuration, app.Logger);
}
catch (Exception ex)
{
    app.Logger.LogError(ex, "Identity seeding failed; continuing without it.");
}

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
    service => service.PurgeOldDeletedImagesAsync(CancellationToken.None),
    "0 1 */3 * *"
);
// Safety-net sweep: every stock reservation also has its own one-shot 3-day Hangfire job
// scheduled at creation time (see IReservationJobScheduler). This recurring job catches
// reservations whose job was lost/cancelled before the server restarted; both paths are
// idempotent (Status gate in StockReservationService.ExpireReservationAsync).
RecurringJob.AddOrUpdate<ReservationExpirationJobs>(
    "expire-stock-reservations",
    job => job.ExpireDueReservationsAsync(),
    "*/1 * * * *"
);
// 2. Enable Static Files (علشان يقدر يخدم الصور من wwwroot)
app.UseStaticFiles();

// Routing must run before CORS, rate limiting and authorization so that the selected
// endpoint (and its [EnableRateLimiting]/[Authorize]/CORS metadata) is available to them.
app.UseRouting();

// CORS runs before authentication so preflight (OPTIONS) requests are answered without
// requiring credentials, and before the rate limiter so a 429 still carries CORS headers.
app.UseCors("AllowAngularApp");

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();

// Authentication MUST run before the per-client rate-limiting policy: that policy keys
// authenticated requests by user id, which only exists once authentication has populated
// HttpContext.User.
app.UseAuthentication();

// Rate limiting: same order as before (after CORS) but now also after authentication so
// authenticated requests are partitioned by user id rather than falling back to IP. Runs
// before authorization, so a rejected request never reaches an endpoint.
app.UseRateLimiter();

app.UseAuthorization();

app.MapControllers();

app.Run();
