using Ecom.Core.DTO.Common;
using Ecom.Core.Interfaces;
using Ecom.Core.Services;

using Hangfire;

using Microsoft.AspNetCore.Hosting;

namespace ECom.Infrastructure.Services
{
    public class ImageManagementService : IImageManagementService
    {
        private readonly IPhotoRepo _photoRepo;
        private readonly IWebHostEnvironment _environment;

        public ImageManagementService(IPhotoRepo photoRepo, IWebHostEnvironment environment)
        {
            _photoRepo = photoRepo;
            _environment = environment;
        }

        public async Task<List<string>> AddImagesAsync(List<FileParameterDto> files, string forWhat, CancellationToken cancellationToken = default)
        {
            var imagesPaths = new List<string>();

            if (files == null || files.Count == 0)
                return imagesPaths;

            var imgDir = Path.Combine(WebRootPath(), "imgs", forWhat);
            if (!Directory.Exists(imgDir))
            {
                Directory.CreateDirectory(imgDir);
            }

            try
            {
                foreach (var file in files)
                {
                    if (file.Data == null || file.Data.Length == 0)
                        continue;

                    var fileExtension = Path.GetExtension(file.FileName);
                    var fileName = $"{Guid.NewGuid():N}{fileExtension}";
                    var allPath = Path.Combine(imgDir, fileName);

                    await System.IO.File.WriteAllBytesAsync(allPath, file.Data, cancellationToken);

                    imagesPaths.Add($"imgs/{forWhat}/{fileName}");
                }

                return imagesPaths;
            }
            catch
            {
                DeleteFilesIfExist(imagesPaths);
                throw new Exception("Failed to add images.");
            }
        }

        public Task RemoveImagesAsync(IEnumerable<string> imagesRelativePath)
        {
            DeleteFilesIfExist(imagesRelativePath);
            return Task.CompletedTask;
        }

        public async Task SoftDeleteImageAsync(int id, string src, CancellationToken cancellationToken = default)
        {
            // Phase 1: Detach the record from the database immediately (soft delete).
            await _photoRepo.DeleteAsync(cancellationToken, id);

            // Phase 2: Schedule a delayed background job to physically purge the file later.
            BackgroundJob.Schedule<IImageManagementService>(
                svc => svc.PurgeImageFileAsync(id, src),
                TimeSpan.FromDays(3)
            );
        }

        public Task PurgeImageFileAsync(int id, string src)
        {
            // Physically remove the file from storage.
            DeleteFileIfExists(src);

            // Hard delete the now-purged database record.
            _photoRepo.HardDeleteImage(id);

            return Task.CompletedTask;
        }

        public async Task PurgeOldDeletedImagesAsync()
        {
            var softDeletedPhotos = await _photoRepo.GetAllWithDeletedAsync(
                predicate: photo => photo.IsDeleted == true,
                trackChanges: false
            );

            if (softDeletedPhotos == null || softDeletedPhotos.Any() == false)
            {
                return;
            }

            foreach (var photo in softDeletedPhotos)
            {
                DeleteFileIfExists(photo.PhotoPath);
                _photoRepo.HardDeleteImage(photo.Id);
            }
        }

        private void DeleteFilesIfExist(IEnumerable<string>? relativePaths)
        {
            if (relativePaths == null)
            {
                return;
            }

            foreach (var path in relativePaths)
            {
                try
                {
                    DeleteFileIfExists(path);
                }
                catch
                {
                }
            }
        }

        private string WebRootPath()
        {
            return _environment.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        }

        private void DeleteFileIfExists(string? photoPath)
        {
            if (string.IsNullOrEmpty(photoPath))
            {
                return;
            }

            var cleanRelativePath = photoPath.TrimStart('/', '\\');
            var fullPath = Path.Combine(WebRootPath(), cleanRelativePath);
            var normalizedPath = Path.GetFullPath(fullPath);

            if (System.IO.File.Exists(normalizedPath))
            {
                System.IO.File.Delete(normalizedPath);
            }
        }
    }
}
