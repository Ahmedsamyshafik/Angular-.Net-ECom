using Ecom.Core.Entities;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

namespace Ecom.Core.Interfaces
{
    //public class GenericRepo<T, TKey> : IGenericRepo<T, TKey> where T : BaseEntity<TKey>
    public interface IGenericRepo<T, Tkey> where T : BaseEntity<Tkey>
    {
        Task<IReadOnlyList<T>> GetAllAsync(CancellationToken cancellationToken = default);
        Task<IReadOnlyList<T>> GetAllAsync(params Expression<Func<T, object>>[] includes);
        Task<IReadOnlyList<T>> GetAllAsync(CancellationToken cancellationToken, params Expression<Func<T, object>>[] includes);

        Task<IReadOnlyList<T>> GetAllWithDeletedAsync(Expression<Func<T, bool>> predicate = null, bool trackChanges = false,
           CancellationToken cancellationToken = default,
           params Expression<Func<T, object>>[] includes);

        Task<T?> GetByIdAsync(params object[] keyValues);
        Task<T?> GetByIdAsync(CancellationToken cancellationToken, params object[] keyValues);
        Task<T?> GetByIdAsync(object[] keyValues, params Expression<Func<T, object>>[] includes);
        Task<T?> GetByIdAsync(CancellationToken cancellationToken, object[] keyValues, params Expression<Func<T, object>>[] includes);

        // Existence-guaranteed variants: throw NotFoundException when the record
        // does not exist, so callers never repeat a null check in the API layer.
        Task<T> GetByIdOrThrowAsync(params object[] keyValues);
        Task<T> GetByIdOrThrowAsync(CancellationToken cancellationToken, params object[] keyValues);
        Task<T> GetByIdOrThrowAsync(object[] keyValues, params Expression<Func<T, object>>[] includes);
        Task<T> GetByIdOrThrowAsync(CancellationToken cancellationToken, object[] keyValues, params Expression<Func<T, object>>[] includes);

        Task<T> AddAsync(T entity, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<T>> AddRangeAsync(List<T> entities, CancellationToken cancellationToken = default);
        Task<T> UpdateAsync(T entity, CancellationToken cancellationToken = default);

        Task<bool> DeleteAsync(params object[] keyValues);
        Task<bool> DeleteAsync(CancellationToken cancellationToken, params object[] keyValues);
        Task DeleteOrThrowAsync(params object[] keyValues);
        Task DeleteOrThrowAsync(CancellationToken cancellationToken, params object[] keyValues);
    }
}