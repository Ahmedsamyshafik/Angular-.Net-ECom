using Ecom.Core.Interfaces;
using ECom.Infrastructure.Data;
using ECom.Infrastructure.Repositores;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;

namespace ECom.Infrastructure
{
    public static class infrastructureRegisteration
    {
        public static IServiceCollection InfrastructureConfigure(this IServiceCollection services, IConfiguration configuration)
        {
            // 1. تسجيل الـ Generic Repo ليعمل مع أي Entity وأي TKey
            services.AddScoped(typeof(IGenericRepo<,>), typeof(GenericRepo<,>));
           //services.AddScoped(typeof(IPhotoRepo), typeof(PhotoRepo));
           services.AddScoped(typeof(IUnitOfWork), typeof(UnitOfWork));
            //Apply DbContext Configuration
            services.AddDbContext<AppDbContext>(options =>
            {
                
                options.UseSqlServer(configuration.GetConnectionString("DefaultConnection"));
            });
            return services;    
        }
    }
}
