using System.Linq.Expressions;

using Ecom.Core.Entities;
using Ecom.Core.Exceptions;
using Ecom.Core.Interfaces;

using ECom.Infrastructure.Data;

using Microsoft.EntityFrameworkCore;

namespace ECom.Infrastructure.Repositories
{
    public class GenericRepo<T, TKey> : IGenericRepo<T, TKey> where T : BaseEntity<TKey>
    {
        protected readonly AppDbContext _context;

        public GenericRepo(AppDbContext context)
        {
            _context = context;
        }

        // 1. Add
        public async Task<T> AddAsync(T entity, CancellationToken cancellationToken = default)
        {
            await _context.Set<T>().AddAsync(entity, cancellationToken);
            return entity;
        }

        public async Task<IReadOnlyList<T>> AddRangeAsync(List<T> entities, CancellationToken cancellationToken = default)
        {
            await _context.Set<T>().AddRangeAsync(entities, cancellationToken);
            return entities;
        }

        // 2. Get All (Basic)
        public async Task<IReadOnlyList<T>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            return await _context.Set<T>()
                .AsNoTracking().Where(x => x.IsDeleted == false)
                .ToListAsync(cancellationToken);
        }

        // 3. Get All (With Includes)
        public async Task<IReadOnlyList<T>> GetAllAsync(params Expression<Func<T, object>>[] includes)
        {
            return await GetAllAsync(CancellationToken.None, includes);
        }

        public async Task<IReadOnlyList<T>> GetAllAsync(CancellationToken cancellationToken, params Expression<Func<T, object>>[] includes)
        {
            IQueryable<T> query = _context.Set<T>();

            if (includes != null)
            {
                query = includes.Aggregate(query, (current, include) => current.Include(include));
            }

            return await query
                .AsNoTracking().Where(x => x.IsDeleted == false)
                .ToListAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<T>> GetAllWithDeletedAsync(Expression<Func<T, bool>> predicate = null, bool trackChanges = false,
            CancellationToken cancellationToken = default,
            params Expression<Func<T, object>>[] includes)
        {

            IQueryable<T> query = _context.Set<T>();

            if (!trackChanges)
            {
                query = query.AsNoTracking();
            }

            if (predicate != null)
            {
                query = query.Where(predicate);
            }

            if (includes != null && includes.Length > 0)
            {
                query = includes.Aggregate(query, (current, include) => current.Include(include));
            }

            return await query.ToListAsync(cancellationToken);
        }

        // 4. Get By Id (Basic)
        public async Task<T?> GetByIdAsync(params object[] keyValues)
        {
            return await GetByIdAsync(CancellationToken.None, keyValues);
        }

        public async Task<T?> GetByIdAsync(CancellationToken cancellationToken, params object[] keyValues)
        {
            var entity = await _context.Set<T>().FindAsync(keyValues, cancellationToken);

            // 2. ���� ���� ��� IsDeleted ��� �� �����
            if (entity != null && entity.IsDeleted == false)
            {
                return entity;
            }

            return null;
        }

        // 5. Get By Id (With Includes)
        public async Task<T?> GetByIdAsync(object[] keyValues, params Expression<Func<T, object>>[] includes)
        {
            return await GetByIdAsync(CancellationToken.None, keyValues, includes);
        }

        public async Task<T?> GetByIdAsync(CancellationToken cancellationToken, object[] keyValues, params Expression<Func<T, object>>[] includes)
        {
            IQueryable<T> query = _context.Set<T>();

            if (includes != null)
            {
                query = includes.Aggregate(query, (current, include) => current.Include(include));
            }


            if (keyValues != null && keyValues.Length > 0)
            {
                var keyValue = keyValues[0];
                var entity = await query.FirstOrDefaultAsync(x => x.Id!.Equals(keyValue), cancellationToken);
                if (entity != null && entity.IsDeleted == false)
                {
                    return entity;
                }
            }

            return null;
        }

        // 6. Get By Id (Throwing) - independent find
        public async Task<T> GetByIdOrThrowAsync(params object[] keyValues)
        {
            return await GetByIdOrThrowAsync(CancellationToken.None, keyValues);
        }

        public async Task<T> GetByIdOrThrowAsync(CancellationToken cancellationToken, params object[] keyValues)
        {
            var entity = await GetByIdAsync(cancellationToken, keyValues);
            return entity ?? throw new NotFoundException($"The requested {typeof(T).Name} was not found.");
        }

        public async Task<T> GetByIdOrThrowAsync(object[] keyValues, params Expression<Func<T, object>>[] includes)
        {
            return await GetByIdOrThrowAsync(CancellationToken.None, keyValues, includes);
        }

        public async Task<T> GetByIdOrThrowAsync(CancellationToken cancellationToken, object[] keyValues, params Expression<Func<T, object>>[] includes)
        {
            var entity = await GetByIdAsync(cancellationToken, keyValues, includes);
            return entity ?? throw new NotFoundException($"The requested {typeof(T).Name} was not found.");
        }

        // 7. Update
        public async Task<T> UpdateAsync(T entity, CancellationToken cancellationToken = default)
        {
            _context.Set<T>().Update(entity);
            return await Task.FromResult(entity);
        }

        // 8. Soft Delete
        public async Task<bool> DeleteAsync(params object[] keyValues)
        {
            return await DeleteAsync(CancellationToken.None, keyValues);
        }

        public async Task<bool> DeleteAsync(CancellationToken cancellationToken, params object[] keyValues)
        {
            var entity = await GetByIdAsync(cancellationToken, keyValues);

            if (entity != null)
            {
                entity.IsDeleted = true;
                entity.DeletedAt = DateTime.UtcNow;

                _context.Set<T>().Update(entity);
                return true;
            }

            return false;
        }

        // 9. Soft Delete (Throwing)
        public async Task DeleteOrThrowAsync(params object[] keyValues)
        {
            await DeleteOrThrowAsync(CancellationToken.None, keyValues);
        }

        public async Task DeleteOrThrowAsync(CancellationToken cancellationToken, params object[] keyValues)
        {
            if (!await DeleteAsync(cancellationToken, keyValues))
            {
                throw new NotFoundException($"The requested {typeof(T).Name} was not found.");
            }
        }
    }
}