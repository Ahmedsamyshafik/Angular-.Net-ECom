using Ecom.Core.Entities;
using Ecom.Core.Interfaces;
using ECom.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace ECom.Infrastructure.Repositores
{
    public class GenericRepo<T, TKey> : IGenericRepo<T, TKey> where T : BaseEntity<TKey>
    {
        protected readonly AppDbContext _context;

        public GenericRepo(AppDbContext context)
        {
            _context = context;
        }

        // 1. Add
        public async Task<T> AddAsync(T entity)
        {
            await _context.Set<T>().AddAsync(entity);
            return entity;
        }

        public async Task<T> AddRangeAsync(List<T> entities)
        {
            await _context.Set<T>().AddRangeAsync(entities);
            return entities.First();
        }

        // 2. Get All (Basic)
        public async Task<IReadOnlyList<T>> GetAllAsync()
        {
            return await _context.Set<T>()
                .AsNoTracking().Where(x => x.IsDeleted == false)
                .ToListAsync();
        }

        // 3. Get All (With Includes)
        public async Task<IReadOnlyList<T>> GetAllAsync(params Expression<Func<T, object>>[] includes)
        {
            IQueryable<T> query = _context.Set<T>();

            if (includes != null)
            {
                query = includes.Aggregate(query, (current, include) => current.Include(include));
            }

            return await query
                .AsNoTracking().Where(x => x.IsDeleted == false)
                .ToListAsync();
        }

        public async Task<IReadOnlyList<T>> GetAllWithDeletedAsync(Expression<Func<T, bool>> predicate = null,bool trackChanges = false,
            params Expression<Func<T, object>>[] includes)
        {
            // 1. البدء بـ Queryable أساسي للـ Entity
            IQueryable<T> query = _context.Set<T>();

            // 2. تطبيق الـ Tracking حسب الحاجة (أداء أسرع لو False)
            if (!trackChanges)
            {
                query = query.AsNoTracking();
            }

            // 3. تطبيق شرط الفلترة (لو المطور باعت شرط زي IsDeleted == true)
            if (predicate != null)
            {
                query = query.Where(predicate);
            }

            // 4. دمج الـ Includes بأسلوب مرن وواضح
            if (includes != null && includes.Length > 0)
            {
                query = includes.Aggregate(query, (current, include) => current.Include(include));
            }

            // 5. التنفيذ وإرجاع القائمة
            return await query.ToListAsync();
        }

        // 4. Get By Id (Basic)
        public async Task<T?> GetByIdAsync(params object[] keyValues)
        {
            var entity = await _context.Set<T>().FindAsync(keyValues);

            // 2. نفحص حالة الـ IsDeleted قبل ما نرجعه

            if (entity != null && entity.IsDeleted == false)
            {
                return entity;
            }

            return null;
        }

        // 5. Get By Id (With Includes)
        public async Task<T?> GetByIdAsync(object[] keyValues, params Expression<Func<T, object>>[] includes)
        {
            IQueryable<T> query = _context.Set<T>();

            if (includes != null)
            {
                query = includes.Aggregate(query, (current, include) => current.Include(include));
            }

            // افترضنا إن اسم الـ Key بيكتب كـ "Id" من الـ BaseEntity
            // إذا كان مفتاح واحد فقط
            if (keyValues != null && keyValues.Length > 0)
            {
                var keyValue = keyValues[0];
                var entity = await query.FirstOrDefaultAsync(x => x.Id!.Equals(keyValue));
                if (entity != null && entity.IsDeleted == false)
                {
                    return entity;
                }
            }

            return null;
        }

        // 6. Update
        public async Task<T> UpdateAsync(T entity)
        {
            _context.Set<T>().Update(entity);
            return await Task.FromResult(entity);
        }

        // 7. Soft Delete
        public async Task<bool> DeleteAsync(params object[] keyValues)
        {
            var entity = await GetByIdAsync(keyValues);

            if (entity != null)
            {
                entity.IsDeleted = true;
                entity.DeletedAt = DateTime.UtcNow;

                _context.Set<T>().Update(entity);
                return true;
            }

            return false;
        }

        // 8. Save Changes
        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }
    }
}