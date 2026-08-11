using Ecom.Core.Entities.Tbl_Product;
using Ecom.Core.Interfaces;
using ECom.Infrastructure.Data;
using System;
using System.Collections.Generic;
using System.Text;

namespace ECom.Infrastructure.Repositores
{
    public class PhotoRepo : GenericRepo<TblPhoto, int>, IPhotoRepo
    {
        public PhotoRepo(AppDbContext context) : base(context)
        {
        }
    }
}
