using Ecom.Core.Entities.Tbl_Product;
using Ecom.Core.Interfaces;

using ECom.Infrastructure.Data;

namespace ECom.Infrastructure.Repositories
{
    public class PhotoRepo : GenericRepo<TblPhoto, int>, IPhotoRepo
    {
        #region Constructors

        public PhotoRepo(AppDbContext context) : base(context)
        {
        }

        #endregion

        #region Methods

        public void HardDeleteImage(int id)
        {
            var img = _context.Find<TblPhoto>(id);
            if (img != null)
            {
                _context.Remove(img);
            }
        }

        #endregion
    }
}
