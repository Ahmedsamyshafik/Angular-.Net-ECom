using Microsoft.AspNetCore.Http;
namespace Ecom.Core.Services
{
    public interface IImageManagementService
    {
        Task<List<string>> AddImagesAsync(List<IFormFile> files, string ForWhat);

        Task DeleteImageAsync(int id, string src);

        Task ActualyDeletingUsingHangFire();
    }
}
