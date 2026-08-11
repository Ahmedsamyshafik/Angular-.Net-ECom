using Ecom.Core.Entities;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace Ecom.Core.Interfaces
{
    //public class GenericRepo<T, TKey> : IGenericRepo<T, TKey> where T : BaseEntity<TKey>
    public interface IGenericRepo<T,Tkey> where T : BaseEntity<Tkey>
    {
        Task<IReadOnlyList<T>> GetAllAsync();
        Task<IReadOnlyList<T>> GetAllAsync(params Expression<Func<T, object>>[] include);

        Task<T?> GetByIdAsync(params object[] keyValues);
        Task<T?> GetByIdAsync(object[] keyValues, params Expression<Func<T, object>>[] include);

        Task<T> AddAsync(T entity);

        Task<T> UpdateAsync(T entity);

        Task<bool> DeleteAsync(params object[] keyValues);

        Task SaveChangesAsync();
    }
}
