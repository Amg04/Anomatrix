using Deadlock.Core.Domain.Entities;
using Deadlock.Core.Domain.RepositoryContracts;
using Deadlock.Core.Domain.Specifications;
using Deadlock.Infrastructure.Data.DbContext;
using Microsoft.EntityFrameworkCore;
namespace Deadlock.Infrastructure.Repositories
{
    public class GenericRepository<T> : IGenericRepository<T> where T : BaseClass
    {
        public readonly DriftersDBContext _dbContext;

        public GenericRepository(DriftersDBContext dbContect)
        {
            _dbContext = dbContect;
        }

        public async Task AddAsync(T entity)
        {
            await _dbContext.Set<T>().AddAsync(entity);
        }

        public void Delete(T entity)
        {
            _dbContext.Remove(entity);
        }
        public void Update(T entity)
        {
            _dbContext.Set<T>().Update(entity);
        }

        public async Task<T?> GetByIdAsync(int id)
        {
            return await _dbContext.Set<T>().FindAsync(id);
        }

        public async Task<IEnumerable<T>> GetAllAsync()
        {
            return await _dbContext.Set<T>().AsNoTracking().ToListAsync();
        }

        public async Task<T?> GetEntityWithSpecAsync(ISpecification<T> spec, CancellationToken cancellationToken = default)
        {
            return await SpecificationEvalutor<T>
                        .GetQuery(_dbContext.Set<T>(), spec)
                        .FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<IEnumerable<T>> GetAllWithSpecAsync(ISpecification<T> spec, CancellationToken cancellationToken = default)
        {
            return await SpecificationEvalutor<T>
                        .GetQuery(_dbContext.Set<T>(), spec)
                        .AsNoTracking()
                        .ToListAsync(cancellationToken);
        }

        public void RemoveRange(IEnumerable<T> entities)
        {
            _dbContext.Set<T>().RemoveRange(entities);
        }

        public async Task AddRangeAsync(IEnumerable<T> entities)
        {
            await _dbContext.Set<T>().AddRangeAsync(entities);
        }
    }
}
