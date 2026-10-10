using Ecom.Core.DTO.Common;

namespace Ecom.Core.Services
{
    public interface IImageManagementService
    {
        Task<List<string>> AddImagesAsync(List<FileParameterDto> files, string forWhat, CancellationToken cancellationToken = default);

        Task RemoveImagesAsync(IEnumerable<string> imagesRelativePath);

        Task SoftDeleteImageAsync(int id, string src, CancellationToken cancellationToken = default);

        Task PurgeImageFileAsync(int id, string src);

        Task PurgeOldDeletedImagesAsync();
    }
}
