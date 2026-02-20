using Deadlock.Core.Domain.Entities;

namespace Deadlock.Core.Domain.RepositoryContracts
{
    public interface IUnitOfWork : IDisposable
    {
        IGenericRepository<T> Repository<T>() where T : BaseClass;
        Task<int> CompleteAsync(CancellationToken cancellationToken = default);
    }
}
