using AutoMapper;
using Ecom.Core.DTO.Categories;
using Ecom.Core.Entities.Tbl_Product;

using ECom.Api.Helper;
using ECom.Infrastructure;
using ECom.Infrastructure.Enum;
using Microsoft.AspNetCore.Mvc;

namespace ECom.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CategoryController : MyBaseController
    {
        public CategoryController(IUnitOfWork unitOfWork, IMapper mapper) : base(unitOfWork, mapper)
        {
        }


        [HttpGet("GetAll")]
        public async Task<IActionResult> GetAll()
        {
            try
            {
                var categories = await _unitOfWork.CategoryRepo.GetAllAsync(x => x.Photos);
                var returnedCategories = _mapper.Map<List<CategoryDto>>(categories);
                return Ok(new responseApi(200, "Categories retrieved successfully", returnedCategories));
            }
            catch (Exception ex)
            {

                return BadRequest(new responseApi(400));
            }
        }
        [HttpGet("GetById/{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            try
            {
                var category = await _unitOfWork.CategoryRepo.GetByIdAsync(new object[] { id }, p => p.Photos);
                if (category == null)
                {
                    return NotFound(new responseApi(404));
                }
                var returnedCategory = _mapper.Map<CategoryDto>(category);
                return Ok(new responseApi(200, "Category found.", returnedCategory));
            }
            catch (Exception ex)
            {
                //loging the error msgg and just display a generic error message to the user
                return BadRequest(new responseApi(400));
            }
        }


        [HttpPost("AddCategory")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> Add([FromForm] AddCategoryDto categoryDto)
        {
            try
            {
                var category = _mapper.Map<TblCategories>(categoryDto);
                category.Id = 0;

                //save imgs if any
                var paths = categoryDto.files != null && categoryDto.files.Count > 0
                    ? await _unitOfWork.ImageManagementService.AddImagesAsync(categoryDto.files, StaticEnums.category.ToString())
                    : new List<string>();

                //Add category to database with its photos
                category.Photos = paths.Select(p => new TblPhoto
                {
                    PhotoPath = p,
                    EntityType = StaticEnums.category.ToString(),
                    EntityId = category.Id.ToString(),
                    CategoryId = category.Id
                }).ToList();

                await _unitOfWork.CategoryRepo.AddAsync(category);
                await _unitOfWork.Commit();

                //Fix EntityId in memory after the real Id is generated, then persist
                category.Photos.Where(p => p.CategoryId == category.Id).ToList().ForEach(p => p.EntityId = category.Id.ToString());
                await _unitOfWork.Commit();

                var returnedCategory = _mapper.Map<CategoryDto>(category);
                return Ok(new responseApi(200, "Category added successfully", returnedCategory));
            }
            catch (Exception ex)
            {
                //loging the error msgg and just display a generic error message to the user
                return BadRequest(new responseApi(400));
            }
        }


        [HttpPut("UpdateCategory")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> Update([FromForm] UpdateCategoryDto categoryDto)
        {
            try
            {
                var category = await _unitOfWork.CategoryRepo.GetByIdAsync(new object[] { categoryDto.Id }, p => p.Photos);


                if (category == null)
                {
                    return NotFound(new responseApi(404));
                }

                //Normal Mapping
                category.Name = categoryDto.Name;
                category.Description = categoryDto.Description;
                category.UpdatedAt = DateTime.UtcNow;
                category.UpdatedBy = 1; // You can replace this with the actual user if you have authentication

                await _unitOfWork.CategoryRepo.UpdateAsync(category);
                //Handle Category imgs.. (soft-delete removed ones, add new ones)
                await _unitOfWork.PhotoRepo.HandleUpdatedImgs(categoryDto.ExistingPhotoIds, categoryDto.NewPhotos, category.Id.ToString(), StaticEnums.category.ToString(), "1");
                await _unitOfWork.Commit();

                var returnedCategory = _mapper.Map<CategoryDto>(category);
                return Ok(new responseApi(200, "Category updated successfully", returnedCategory));
            }
            catch (Exception ex)
            {
                //loging the error msgg and just display a generic error message to the user
                return BadRequest(new responseApi(400));
            }
        }

        [HttpDelete("DeleteCategory/{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {

                bool res = await _unitOfWork.CategoryRepo.DeleteAsync(id);
                //delete also category imgs (file + db row) like products do
                if (res)
                {
                    var photos = await _unitOfWork.PhotoRepo.GetAllWithDeletedAsync(
                        predicate: x => x.EntityId == id.ToString() && x.EntityType == StaticEnums.category.ToString());
                    foreach (var photo in photos)
                    {
                        await _unitOfWork.ImageManagementService.DeleteImageAsync(photo.Id, photo.PhotoPath);
                    }
                }
                await _unitOfWork.Commit();
                if (res)
                    return Ok(new responseApi(200, "Category deleted successfully"));
                else
                    return NotFound(new responseApi(404));
            }
            catch (Exception ex)
            {
                //loging the error msgg and just display a generic error message to the user
                return BadRequest(new responseApi(400));
            }
        }

    }
}