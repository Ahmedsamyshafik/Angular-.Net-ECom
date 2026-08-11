using Ecom.Core.Entities.Tbl_Product;
using Ecom.Core.Interfaces;
using ECom.Infrastructure.Data;
using Microsoft.AspNetCore.Http;

namespace ECom.Infrastructure.Repositores
{
    public class PhotoRepo : GenericRepo<TblPhoto, int>, IPhotoRepo
    {
        public PhotoRepo(AppDbContext context) : base(context)
        {

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
                    var imgSrc = Path.Combine("imgs", ForWhat, fileName);
                    imagespaths.Add(imgSrc);
                }
            }

            return imagespaths;
        }

        public Task DeleteImageAsync(int id)
        {

            var img = _context.Find<TblPhoto>(id);
            if (img != null)
            {
                img.IsDeleted = true;
                img.DeletedAt = DateTime.UtcNow;
                img.DeletedBy = 1;
                _context.Update(img);
                _context.SaveChanges();
            }
            return Task.CompletedTask;
        }

        public Task ActualyDeletingUsingHangFire()
        {
            return Task.CompletedTask;
        }

        public async Task HandleUpdatedImgs(List<int>? ExistingPhotoIds, List<IFormFile>? NewPhotos, string EntityId, string EntityType, string UserId)
        {
            if (ExistingPhotoIds != null && ExistingPhotoIds.Count > 0)
            {
                // حذف Soft للصور اللي مش جاية في قائمة اللي هيفضلوا
                var photosToDelete = _context.Set<TblPhoto>()
                    .Where(p => p.EntityId == EntityId && p.EntityType == EntityType &&
                    !ExistingPhotoIds.Contains(p.Id) &&
                    p.IsDeleted == false)
                    .ToList();

                foreach (var photo in photosToDelete)
                {
                    photo.IsDeleted = true;
                    photo.DeletedAt = DateTime.UtcNow;
                    photo.DeletedBy = int.Parse(UserId);
                    _context.Update(photo);
                }
                _context.SaveChanges();
            }
            if (NewPhotos != null && NewPhotos.Count > 0)
            {
                // Handle new photos
                //Adding Physical Images 
                List<string> imgPaths = await AddImagesAsync(NewPhotos, EntityType);
                //Switch adding Any Type of Entity (Product or Category) to Db
                int? productId = null;
                int? categoryId = null;

                // ⚠️ المقارنة هنا Case Insensitive علشان StaticEnums.category.ToString() بترجع "category" مش "Category"
                if (EntityType.Equals("product", StringComparison.OrdinalIgnoreCase))
                {
                    productId = int.Parse(EntityId);
                }
                else if (EntityType.Equals("category", StringComparison.OrdinalIgnoreCase))
                {
                    categoryId = int.Parse(EntityId);
                }

                //adding to Db
                var photoEntities = imgPaths.Select(path => new TblPhoto
                {
                    EntityId = EntityId,
                    EntityType = EntityType,
                    PhotoPath = path, // 👈 بياخد المسار المظبوط اللي اتحفظ بيه الملف فعلياً
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = int.Parse(UserId),
                    ProductId = productId,
                    CategoryId = categoryId,
                }).ToList();
                _context.AddRange(photoEntities);
                await _context.SaveChangesAsync();
            }
        }

        public void DbDeleteImage(int id)
        {
            var img = _context.Find<TblPhoto>(id);
            if (img != null)
            {
                _context.Remove(img);
                _context.SaveChanges();
            }
        }
    }
}
