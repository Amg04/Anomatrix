using Deadlock.Core.DTO;
using System.Diagnostics;

namespace Deadlock.Core.ServiceContracts
{
    public interface ICameraProcessManager
    {
        Task<Process> StartFfmpegProcessAsync(FfmpegArgs args, string outputDir,int cameraId, CancellationToken ct);
        void KillProcess(int cameraId, Process process);
        bool IsProcessRunning(int cameraId);
    }
}
