using Ecom.Core.Entities;
using Ecom.Core.Interfaces;
using ECom.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;

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

        // 2. Get All (Basic)
        public async Task<IReadOnlyList<T>> GetAllAsync()
        {
            return await _context.Set<T>()
                .AsNoTracking()
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
                .AsNoTracking()
                .ToListAsync();
        }

        // 4. Get By Id (Basic)
        public async Task<T?> GetByIdAsync(params object[] keyValues)
        {
            return await _context.Set<T>().FindAsync(keyValues);
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
                return await query.FirstOrDefaultAsync(x => x.Id!.Equals(keyValue));
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