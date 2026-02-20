using Deadlock.Core.Domain.Entities;
using Deadlock.Core.Domain.RepositoryContracts;
using Deadlock.Infrastructure.Data.DbContext;
using System.Collections;
using System.Threading.Tasks;
namespace Deadlock.Infrastructure.Repositories
{
    public class UnitOfWork : IUnitOfWork
    {
        private readonly DriftersDBContext _dbContext;
        private readonly Dictionary<string, object> _repositories = new();

        public UnitOfWork(DriftersDBContext dbContext)
        {
            _dbContext = dbContext;
        }

        public IGenericRepository<T> Repository<T>() where T : BaseClass
        {
            var typeName = typeof(T).Name;
            if (!_repositories.ContainsKey(typeName))
            {
                _repositories[typeName] = new GenericRepository<T>(_dbContext);
            }
            return (IGenericRepository<T>)_repositories[typeName];
        }

        public void Dispose()
        {
            _dbContext.Dispose();
        }

        public async Task<int> CompleteAsync(CancellationToken cancellationToken = default)
        {
            return await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
