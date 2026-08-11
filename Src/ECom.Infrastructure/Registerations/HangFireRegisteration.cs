using Hangfire;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;

namespace ECom.Infrastructure.Registerations
{
    public static class HangFireRegisteration
    {
        public static IServiceCollection HangFireConfigure(this IServiceCollection services, IConfiguration configuration)
        {
            //1.تسجيل Hangfire
            services.AddHangfire(config => config
                .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
                .UseSimpleAssemblyNameTypeSerializer()
                .UseRecommendedSerializerSettings()
                .UseSqlServerStorage(configuration.GetConnectionString("DefaultConnection")));

            // 2. تشغيل الـ Background Server لـ Hangfire
            services.AddHangfireServer();

            return services;
        }
    }
}
