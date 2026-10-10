using System.Text;

using Ecom.Core.Entities.User;
using Ecom.Core.Interfaces;
using Ecom.Core.Mapping;
using Ecom.Core.Services;

using ECom.Infrastructure.Application.Calculators;
using ECom.Infrastructure.Application.Facades;
using ECom.Infrastructure.Application.Orchestrators;
using ECom.Infrastructure.Application.Services;
using ECom.Infrastructure.Data;
using ECom.Infrastructure.Redis;
using ECom.Infrastructure.Repositories;
using ECom.Infrastructure.Services;

using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

using StackExchange.Redis;

namespace ECom.Infrastructure.Registerations
{
    public static class InfrastructureRegisteration
    {
        public static IServiceCollection InfrastructureConfigure(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddDbContext<AppDbContext>(options =>
            {
                options.UseSqlServer(configuration.GetConnectionString("DefaultConnection"));
            });

            //Repos
            services.AddScoped(typeof(IGenericRepo<,>), typeof(GenericRepo<,>));
            services.AddScoped<IProductRepo, ProductRepo>();
            services.AddScoped<ICategoryRepo, CategoryRepo>();
            services.AddScoped<IPhotoRepo, PhotoRepo>();
            services.AddScoped<IDiscountRepo, DiscountRepo>();
            services.AddScoped<IDiscountProductRepo, DiscountProductRepo>();
            services.AddScoped<ICartRepository, RedisCartRepository>();

            //Services
            services.AddScoped<IProductService, ProductService>();
            services.AddScoped<ICategoryService, CategoryService>();
            services.AddScoped<IEmailService, EmailService>();
            services.AddScoped<IGenerateToken, GenereateToken>();


            //Custom Services
            services.AddScoped<IImageManagementService, ImageManagementService>();
            services.AddScoped<IDiscountPriceCalculator, DiscountPriceCalculator>();

            //Facads
            services.AddScoped<IDiscountFacade, DiscountFacade>();
            services.AddScoped<ICartProductFacade, CartProductFacade>();

            //Orchestrator
            services.AddScoped<IAddProductOrchestrator, AddProductOrchestrator>();

            //Mapper
            services.AddAutoMapper(_ => { }, typeof(ProductMapping).Assembly);

            //Redis
            services.AddSingleton<IConnectionMultiplexer>(i =>
            {

                var redisConn = configuration.GetConnectionString("Redis") ?? "localhost:6379";
                var config = ConfigurationOptions.Parse(redisConn);
                config.AbortOnConnectFail = false; // يمنع إيقاف التطبيق إذا كان Redis غير متاح مؤقتاً
                return ConnectionMultiplexer.Connect(config);

            });
            //Authentication
            services.AddIdentity<AppUser, IdentityRole>().AddEntityFrameworkStores<AppDbContext>().AddDefaultTokenProviders();
            services.AddAuthentication(op =>
            {
                op.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                op.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                op.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;//jwt using cookie
            }).AddCookie(o =>
            {
                o.Cookie.Name = "token";
                o.Events.OnRedirectToLogin = context =>
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return Task.CompletedTask;
                };
            }).AddJwtBearer(op =>
            {
                op.RequireHttpsMetadata = false;
                op.SaveToken = true;
                op.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration["token:Secret"])),
                    ValidateIssuer = true,
                    ValidIssuer = configuration["token:Issure"],
                    ValidateAudience = false,
                    ClockSkew = TimeSpan.Zero


                };
                op.Events = new JwtBearerEvents()
                {
                    OnMessageReceived = context =>
                    {
                        context.Token = context.Request.Cookies["token"];
                        return Task.CompletedTask;
                    }
                };
            });
            //Unit of work
            services.AddScoped<IUnitOfWork, UnitOfWork>();

            return services;
        }
    }
}
