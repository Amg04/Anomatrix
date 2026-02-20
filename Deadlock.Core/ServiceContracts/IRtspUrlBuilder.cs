using Deadlock.Core.Domain.Entities;

namespace Deadlock.Core.ServiceContracts
{
    public interface IRtspUrlBuilder
    {
        string Build(Camera camera, string? password);
    }
}
