using AutoMapper;

using Ecom.Core.Mapping;

using Microsoft.Extensions.DependencyInjection;

namespace ECom.Api.Tests.Mapping
{
    public static class MappingTestHelper
    {
        public static IMapper CreateMapper()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddAutoMapper(_ => { }, typeof(ProductMapping).Assembly);
            return services.BuildServiceProvider().GetRequiredService<IMapper>();
        }
    }
}
