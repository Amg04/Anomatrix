using Deadlock.Core.Domain.Entities;

namespace Deadlock.Core.ServiceContracts
{
    public interface ICameraStreamService
    {
        Task StartStreamAsync(int cameraId, CancellationToken ct = default);
        Task StopStreamAsync(int cameraId);
        Task UpdateStatusAsync(int cameraId, string status, CancellationToken ct = default);
        Task<IEnumerable<Camera>> GetActiveStreamsAsync();
    }
}
