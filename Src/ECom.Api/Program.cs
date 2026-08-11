using Ecom.Core.Services;
using ECom.Infrastructure.Registerations;
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
                  .AllowAnyMethod();
        });
});

builder.Services.AddControllers();
builder.Services.AddOpenApi();
//infrastructure registration
builder.Services.InfrastructureConfigure(builder.Configuration);
//Hangfire registration
builder.Services.HangFireConfigure(builder.Configuration);
//Mapper registration
builder.Services.AddAutoMapper(cfg => { }, AppDomain.CurrentDomain.GetAssemblies());
// بيسجل كل الـ Profiles اللي بتورث من Profile في الـ Assembly ده 

var app = builder.Build();

// HangFire Dashboard configuration
app.UseHangfireDashboard("/hangfire");
// Schedule the recurring job to purge old deleted images every 3 months
RecurringJob.AddOrUpdate<IImageManagementService>(
    "purge-old-deleted-images",
    service => service.ActualyDeletingUsingHangFire(),
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

app.UseAuthorization();

app.MapControllers();

app.Run();
