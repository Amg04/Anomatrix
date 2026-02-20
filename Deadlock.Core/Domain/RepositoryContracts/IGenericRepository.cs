using Deadlock.Core.Domain.Entities;
using Deadlock.Core.Domain.Specifications;
namespace Deadlock.Core.Domain.RepositoryContracts
{
    public interface IGenericRepository<T> where T : BaseClass
    {
        Task AddAsync(T entity);
        void Delete(T entity);
        Task<T?> GetByIdAsync(int id);
        Task<IEnumerable<T>> GetAllAsync();
        void Update(T entity);
        Task<T?> GetEntityWithSpecAsync(ISpecification<T> spec, CancellationToken cancellationToken = default);
        Task<IEnumerable<T>> GetAllWithSpecAsync(ISpecification<T> spec, CancellationToken cancellationToken = default);
        void RemoveRange(IEnumerable<T> entities);
        Task AddRangeAsync(IEnumerable<T> entities);
    }
}
