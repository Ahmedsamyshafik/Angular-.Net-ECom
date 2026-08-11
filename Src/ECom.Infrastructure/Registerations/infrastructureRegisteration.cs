using Ecom.Core.Interfaces;
using Ecom.Core.Services;
using ECom.Infrastructure.Data;
using ECom.Infrastructure.Repositores;
using ECom.Infrastructure.Repositores.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;

namespace ECom.Infrastructure.Registerations
{
    public static class infrastructureRegisteration
    {
        public static IServiceCollection InfrastructureConfigure(this IServiceCollection services, IConfiguration configuration)
        {
            // 1. تسجيل الـ Generic Repo ليعمل مع أي Entity وأي TKey
            services.AddScoped(typeof(IGenericRepo<,>), typeof(GenericRepo<,>));
            //services.AddScoped(typeof(IPhotoRepo), typeof(PhotoRepo));
            services.AddScoped(typeof(IUnitOfWork), typeof(UnitOfWork));


            // Adding Again IImageManagementService and IIFileProvider For HangFire to work properly

            services.AddSingleton<IFileProvider>(
                new PhysicalFileProvider(Directory.GetCurrentDirectory())
            );
            services.AddScoped<IImageManagementService, ImageManagementService>();


            //Apply DbContext Configuration
            services.AddDbContext<AppDbContext>(options =>
            {

                options.UseSqlServer(configuration.GetConnectionString("DefaultConnection"));
            });
            return services;
        }
    }
}
