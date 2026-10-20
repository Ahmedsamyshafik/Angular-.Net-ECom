using AutoMapper;

using Ecom.Core.DTO.Categories;
using Ecom.Core.Entities.Tbl_Product;
using Ecom.Core.Interfaces;
using Ecom.Core.Pagination;
using Ecom.Core.Services;

using ECom.Infrastructure.Enum;

namespace ECom.Infrastructure.Application.Services
{
    public class CategoryService : ICategoryService
    {
        #region Private Fields

        private readonly ICategoryRepo _categoryRepo;
        private readonly IPhotoRepo _photoRepo;
        private readonly IImageManagementService _imageManagementService;
        private readonly IMapper _mapper;

        #endregion

        #region Constructors

        public CategoryService(
            ICategoryRepo categoryRepo,
            IPhotoRepo photoRepo,
            IImageManagementService imageManagementService,
            IMapper mapper)
        {
            _categoryRepo = categoryRepo;
            _photoRepo = photoRepo;
            _imageManagementService = imageManagementService;
            _mapper = mapper;
        }

        #endregion

        #region Methods

        #region Queries

        public async Task<Pagination<CategoryDto>> GetPageAsync(CategoryQueryParams query, CancellationToken cancellationToken = default)
        {
            var page = await _categoryRepo.GetPageAsync(query, cancellationToken);

            return new Pagination<CategoryDto>(
                page.PageNumber,
                page.PageSize,
                page.TotalCount,
                page.Data.Select(category => _mapper.Map<CategoryDto>(source: category)).ToList());
        }

        public async Task<CategoryDto> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            var category = await _categoryRepo.GetByIdOrThrowAsync(
                cancellationToken,
                new object[] { id },
                c => c.Photos!);

            return _mapper.Map<CategoryDto>(category);
        }

        #endregion

        #region Commands

        public async Task<AddCategoryResult> AddAsync(AddCategoryDto category, string systemUserId, CancellationToken cancellationToken = default)
        {
            var storedPaths = await _imageManagementService
                .AddImagesAsync(category.Files, StaticEnums.category.ToString(), cancellationToken);

            try
            {
                var entity = _mapper.Map<TblCategories>(category);
                entity.Photos = storedPaths
                    .Select(path => new TblPhoto { PhotoPath = path, Category = entity, CreatedBy = systemUserId })
                    .ToList();

                var added = await _categoryRepo.AddAsync(entity, cancellationToken);

                return new AddCategoryResult(_mapper.Map<CategoryDto>(added), storedPaths);
            }
            catch
            {
                await RemoveStoredImagesAsync(storedPaths, cancellationToken);
                throw;
            }
        }

        public async Task CompensateAsync(AddCategoryResult result, CancellationToken cancellationToken = default)
        {//Roll Back
            await RemoveStoredImagesAsync(result?.StoredPhotoPaths, cancellationToken);
        }

        public async Task<CategoryDto> UpdateAsync(UpdateCategoryDto category, string systemUserId, CancellationToken cancellationToken = default)
        {
            var entity = await _categoryRepo.GetByIdOrThrowAsync(
                cancellationToken,
                new object[] { category.Id },
                c => c.Photos!);

            _mapper.Map(category, entity);
            entity.UpdatedAt = DateTime.UtcNow;
            entity.UpdatedBy = systemUserId;

            var storedPaths = await _imageManagementService
                .AddImagesAsync(category.NewPhotos, StaticEnums.category.ToString(), cancellationToken);

            await ReconcilePhotosAsync(category.ExistingPhotoIds, storedPaths, category.Id, systemUserId, cancellationToken);

            return _mapper.Map<CategoryDto>(entity);
        }

        public async Task DeleteAsync(int id, string systemUserId, CancellationToken cancellationToken = default)
        {
            await _categoryRepo.DeleteOrThrowAsync(cancellationToken, id);

            var photos = await _photoRepo.GetAllWithDeletedAsync(
                predicate: photo => photo.CategoryId == id && photo.IsDeleted == false,
                cancellationToken: cancellationToken);

            foreach (var photo in photos)
            {
                await _imageManagementService.SoftDeleteImageAsync(photo.Id, photo.PhotoPath, cancellationToken);
                photo.DeletedBy = systemUserId;
            }
        }

        #endregion

        #region Private Helpers

        //Handeled updated photos, removed photos and new photos
        private async Task ReconcilePhotosAsync(List<int>? keptPhotoIds, IReadOnlyList<string> newPhotoPaths, int categoryId, string systemUserId, CancellationToken cancellationToken)
        {
            var kept = keptPhotoIds ?? new List<int>();

            var removed = await _photoRepo.GetAllWithDeletedAsync(
                predicate: photo => photo.CategoryId == categoryId
                                    && photo.IsDeleted == false
                                    && !kept.Contains(photo.Id),
                trackChanges: true,
                cancellationToken: cancellationToken);

            foreach (var photo in removed)
            {
                photo.IsDeleted = true;
                photo.DeletedAt = DateTime.UtcNow;
                photo.DeletedBy = systemUserId;
            }

            if (newPhotoPaths.Count == 0)
            {
                return;
            }

            var newPhotos = newPhotoPaths
                .Select(path => new TblPhoto
                {
                    PhotoPath = path,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = systemUserId,
                    ProductId = null,
                    CategoryId = categoryId
                })
                .ToList();

            await _photoRepo.AddRangeAsync(newPhotos, cancellationToken);
        }

        private async Task RemoveStoredImagesAsync(IReadOnlyList<string>? storedPaths, CancellationToken cancellationToken = default)
        {
            if (storedPaths is null || storedPaths.Count == 0)
            {
                return;
            }

            await _imageManagementService.RemoveImagesAsync(storedPaths, cancellationToken);
        }

        #endregion

        #endregion
    }
}
