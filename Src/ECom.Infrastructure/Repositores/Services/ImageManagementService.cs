using Ecom.Core.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;

namespace ECom.Infrastructure.Repositores.Services
{
    public class ImageManagementService : IImageManagementService
    {
        private readonly IFileProvider _fileProvider;
        private readonly IWebHostEnvironment _environment;
        public IUnitOfWork _UnitOfWork { get; }

        public ImageManagementService(IFileProvider fileProvider, IUnitOfWork _unitOfWork, IWebHostEnvironment environment)
        {
            _fileProvider = fileProvider;
            _UnitOfWork = _unitOfWork;
            _environment = environment;
        }

        public async Task<List<string>> AddImagesAsync(List<IFormFile> files, string ForWhat)//src As product or category name
        {
            var imagespaths = new List<string>();
            var imgDir = Path.Combine("wwwroot", "imgs", ForWhat);
            if (!Directory.Exists(imgDir))
            {
                Directory.CreateDirectory(imgDir);
            }
            ;
            foreach (var file in files)
            {
                if (file.Length > 0)
                {
                    var fileExtension = Path.GetExtension(file.FileName);
                    var fileName = $"{Guid.NewGuid()}{fileExtension}";
                    var allPath = Path.Combine(imgDir, fileName);
                    using (var stream = new FileStream(allPath, FileMode.Create))
                    {
                        await file.CopyToAsync(stream);
                    }
                    //Save 
                    //var imgSrc = Path.Combine("imgs", ForWhat, fileName);
                    var imgSrc = $"imgs/{ForWhat}/{fileName}";
                    imagespaths.Add(imgSrc);
                }
            }

            return imagespaths;
        }

        public async Task DeleteImageAsync(int id, string src)
        {
            // 1. حذف الملف الفعلي من على السيرفر
            DeleteFileIfExists(src);
            // 2. Soft Delete سجل الصورة من الداتابيز
            await _UnitOfWork.PhotoRepo.DeleteAsync(id);
            await _UnitOfWork.Commit();

        }

        public async Task ActualyDeletingUsingHangFire()
        {
            // 1. جلب الصور المعمول لها Soft Delete
            var softDeletedPhotos = await _UnitOfWork.PhotoRepo.GetAllWithDeletedAsync(
                predicate: photo => photo.IsDeleted == true,
                trackChanges: false
            );

            if (softDeletedPhotos == null || !softDeletedPhotos.Any())
            {
                return;
            }

            // 2. حذف الملفات الفعلية (لو لسه موجودة)
            foreach (var photo in softDeletedPhotos)
            {
                DeleteFileIfExists(photo.PhotoPath);
            }

            // 3. مسح السجلات نهائياً من الداتابيز (Hard Delete)
            foreach (var photo in softDeletedPhotos)
            {
                _UnitOfWork.PhotoRepo.DbDeleteImage(photo.Id);
            }
            //await _UnitOfWork.PhotoRepo.DeleteRangeAsync(softDeletedPhotos);
            await _UnitOfWork.Commit();
        }

        // ضابط المسار و تحويله لمسار كامل ثم حذف الملف الفعلي لو موجود
        private void DeleteFileIfExists(string? photoPath)
        {
            if (string.IsNullOrEmpty(photoPath))
            {
                return;
            }

            // 1. تنظيف أي Slash في البداية
            var cleanRelativePath = photoPath.TrimStart('/', '\\');

            // 2. الدمج مع wwwroot
            var webRootPath = _environment.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            var fullPath = Path.Combine(webRootPath, cleanRelativePath);

            // 3. توحيد شكل المسار بالكامل لنظام التشغيل (أمان زيادة)
            var normalizedPath = Path.GetFullPath(fullPath);

            // 4. التحقق والحذف
            if (File.Exists(normalizedPath))
            {
                File.Delete(normalizedPath);
            }
        }
    }
}
