using AutoMapper;
using Ecom.Core.DTO.Photo;
using Ecom.Core.DTO.Products;
using Ecom.Core.Entities.Tbl_Product;
using ECom.Api.Helper;
using ECom.Infrastructure;
using ECom.Infrastructure.Enum;
using Microsoft.AspNetCore.Mvc;

namespace ECom.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ProductController : MyBaseController
    {
        public ProductController(IUnitOfWork unitOfWork, IMapper mapper) : base(unitOfWork, mapper)
        {
        }





        //Get All
        [HttpGet("GetAll")]
        public async Task<IActionResult> GetAll()
        {
            var products = await _unitOfWork.ProductRepo.GetAllAsync(x => x.Photos, x => x.Category);
            if (products == null)
            {
                return NotFound(new responseApi(400, "No products found."));
            }
            var returnedProducts = _mapper.Map<List<ProductDto>>(products);

            return Ok(new responseApi(200, "Products found.", returnedProducts));
        }
        //Get By id 
        [HttpGet("GetById/{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var product = await _unitOfWork.ProductRepo.GetByIdAsync(new object[] { id }, p => p.Category!, p => p.Photos!);
            if (product == null)
            {
                return NotFound(new responseApi(400, "Product not found."));
            }
            product.Photos = product.Photos?.Where(x => x.IsDeleted != true).ToList();

            var returnedProduct = _mapper.Map<ProductDto>(product);
            return Ok(new responseApi(200, "Product found.", returnedProduct));
        }
        //Add 
        [HttpPost("Add")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> Add([FromForm] AddProductDto productDto)
        {
            var product = _mapper.Map<TblProducts>(productDto);
            product.Id = 0;
            //save imgs
            var paths = await SavingProductImages(productDto.files, product.Id);
            //Add product to database
            product.Photos = paths.Select(p => new TblPhoto { PhotoPath = p, EntityType = StaticEnums.product.ToString(), EntityId = product.Id.ToString(), ProductId = product.Id }).ToList();
            var products = await _unitOfWork.ProductRepo.AddAsync(product);
            await _unitOfWork.Commit();
            //Asing paths in Memory to product.Photos, so that it can be returned in the response
            productDto.Photo = product.Photos.Select(p => new PhotoDto
            {
                PhotoId = p.Id, // 👈 أصبح يحتوي على الـ Primary Key الحقيقي من الداتابيز!
                PhotoPath = p.PhotoPath,
                ProductId = product.Id
            }).ToList();
            productDto.id = product.Id;
            products.Photos.Where(p => p.ProductId == product.Id).ToList().ForEach(p => p.EntityId = product.Id.ToString());
            productDto.files = null;
            await _unitOfWork.Commit();
            return Ok(new responseApi(200, "Product added.", productDto)); //will change the behavior of Adding to return also success of false and msg and data
        }
        //Update 
        [HttpPut("Update")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> Update([FromForm] UpdateProductDto productDto)
        {
            var product = await _unitOfWork.ProductRepo.GetByIdAsync(productDto.id);
            if (product == null)
            {
                return NotFound(new responseApi(404, "Product not found."));
            }
            _mapper.Map(productDto, product);
            //Handle Product imgs..
            await _unitOfWork.PhotoRepo.HandleUpdatedImgs(productDto.ExistingPhotoIds, productDto.NewPhotos, product.Id.ToString(), StaticEnums.product.ToString(), "1");
            await _unitOfWork.Commit();
            _mapper.Map(product, productDto);


            return Ok(new responseApi(200, "Product updated.", productDto));
        }
        //Delete
        [HttpDelete("Delete/{id}")]
        public async Task<IActionResult> Delete(int id)
        {

            bool res = await _unitOfWork.ProductRepo.DeleteAsync(id);
            //delete also product imgs (physical file + db row) من TblPhoto 
            if (res)
            {
                var photos = await _unitOfWork.PhotoRepo.GetAllWithDeletedAsync(
                    predicate: x => x.EntityId == id.ToString() && x.EntityType == StaticEnums.product.ToString());
                foreach (var photo in photos)
                {
                    await _unitOfWork.ImageManagementService.DeleteImageAsync(photo.Id, photo.PhotoPath);
                }
            }
            await _unitOfWork.Commit();
            if (res)
                return Ok(new responseApi(200, "Product deleted."));
            else
                return BadRequest(new responseApi(400, "Product could not be deleted."));
        }
        //Adding a Discound for existing product
        [HttpPost("AddingProductDiscount")]
        public async Task<IActionResult> AddingProductDiscount(AddDiscountDto obj)
        {
            // test after bathroom
            //Adding Discound
            var backupDiscount = await _unitOfWork.DiscountRepo.AddDiscountAsync(obj.From, obj.To, obj.TypeOfDescount, obj.val, obj.name);
            if (backupDiscount == null)
                return BadRequest();
            //Adding Product Discount
            var procuctDis = await _unitOfWork.DiscountProductRepo.AddDiscountProductAsync(backupDiscount, obj.productId);
            if (procuctDis == null)
                return BadRequest();
            await _unitOfWork.Commit();
            return Ok();
        }









        private async Task<List<string>> SavingProductImages(List<IFormFile> files, int productId)
        {
            List<string> imagePaths = new List<string>();
            //image Handling
            //save files to server and add their paths to product.Photos
            if (files != null && files.Count > 0)
            {
                imagePaths = await _unitOfWork.ImageManagementService.AddImagesAsync(files, StaticEnums.product.ToString());
                //List<TblPhoto> photosList = new List<TblPhoto>();
                //foreach (var path in imagePaths)
                //{
                //    photosList.Add(new TblPhoto
                //    {
                //        EntityType = StaticEnums.product.ToString(),
                //        PhotoPath = path,
                //        CreatedAt = DateTime.UtcNow,
                //        CreatedBy = 1,
                //        ProductId = productId
                //    });

                //}
                //await _unitOfWork.PhotoRepo.AddRangeAsync(photosList);
            }
            return imagePaths;
        }
    }
}
