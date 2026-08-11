using Ecom.Core.Entities.Tbl_Product;
using Microsoft.AspNetCore.Http;

namespace Ecom.Core.Interfaces
{
    public interface IPhotoRepo : IGenericRepo<TblPhoto, int>
    {
        void DbDeleteImage(int id);
        Task HandleUpdatedImgs(List<int>? ExistingPhotoIds, List<IFormFile>? NewPhotos, string EntityId, string EntityType, string UserId);
    }
}
