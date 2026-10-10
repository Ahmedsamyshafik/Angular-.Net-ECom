using Ecom.Core.Entities.Tbl_Product;

namespace Ecom.Core.Interfaces
{
    public interface IPhotoRepo : IGenericRepo<TblPhoto, int>
    {
        void HardDeleteImage(int id);
    }
}
