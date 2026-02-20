using Deadlock.Core.Domain.Entities;
using Deadlock.Core.Domain.RepositoryContracts;
using Deadlock.Core.Domain.Specifications;
using Deadlock.Core.DTO;
using Deadlock.Core.ServiceContracts;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace Deadlock.Infrastructure.Services
{

    public class CameraStreamService : ICameraStreamService, IDisposable
    {
        private readonly IServiceProvider _sp;
        private readonly ICameraProcessManager _processManager;
        private readonly INotificationService _notification;
        private readonly ILogger<CameraStreamService> _logger;
        private readonly ConcurrentDictionary<int, Process> _activeProcesses = new();
        private readonly ConcurrentDictionary<int, DateTime> _startAttempts = new();
        private bool _disposed;

        public CameraStreamService(
             IServiceProvider sp,
            ICameraProcessManager processManager,
            INotificationService notification,
            ILogger<CameraStreamService> logger)
        {
            _sp = sp;
            _processManager = processManager;
            _notification = notification;
            _logger = logger;
        }

        public async Task StartStreamAsync(int cameraId, CancellationToken ct = default)
        {
            if (_activeProcesses.TryRemove(cameraId, out var staleProcess))
            {
                _processManager.KillProcess(cameraId, staleProcess);
                _logger.LogInformation("Cleaned stale process for camera {CameraId}", cameraId);
            }

            if (!_startAttempts.TryAdd(cameraId, DateTime.UtcNow))
                return;

            try
            {
                await StartStreamInternalAsync(cameraId, ct);
            }
            finally
            {
                _startAttempts.TryRemove(cameraId, out _);
            }
        }

        private async Task StartStreamInternalAsync(int cameraId, CancellationToken ct)
        {
            var maxRetries = 3;

            for (int retryCount = 0; retryCount < maxRetries; retryCount++)
            {
                try
                {
                    using var scope = _sp.CreateScope();
                    var urlBuilder = scope.ServiceProvider.GetRequiredService<IRtspUrlBuilder>();
                    var dataProtection = scope.ServiceProvider.GetRequiredService<IDataProtectionProvider>();
                    var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

                    var spec = new BaseSpecification<Camera>(c => c.Id == cameraId);
                    spec.Includes.Add(c => c.MonitoredEntity);
                    var camera = await unitOfWork.Repository<Camera>().GetEntityWithSpecAsync(spec, ct);

                    if (camera == null || !camera.Enabled || string.IsNullOrWhiteSpace(camera.Host))
                        return;

                    try
                    {
                        var webRootPath = GetWebRootPath(scope);
                        var outputDir = Path.Combine(webRootPath, "api", "streams", cameraId.ToString());
                        Directory.CreateDirectory(outputDir);
                        var hlsFile = Path.Combine(outputDir, "index.m3u8");

                        camera.HlsLocalPath = hlsFile;
                        camera.HlsPublicUrl = $"/streams/{camera.Id}/index.m3u8";
                        camera.Status = "Starting";
                        camera.LastHeartbeatUtc = DateTime.UtcNow;

                        await unitOfWork.CompleteAsync(ct);

                        await _notification.NotifyStreamAvailableAsync(camera.Id, camera.HlsPublicUrl, camera.MonitoredEntity?.UserId);

                        string? pwd = null;
                        if (!string.IsNullOrEmpty(camera.PasswordEnc))
                        {
                            var protector = dataProtection.CreateProtector("purpose-string");
                            pwd = protector.Unprotect(camera.PasswordEnc);
                        }
                        var rtspUrl = urlBuilder.Build(camera, pwd);

                        var ffmpegArgs = new FfmpegArgs
                        {
                            InputRtsp = rtspUrl,
                            OutputDir = outputDir,
                            HlsFile = hlsFile
                        };

                        var process = await _processManager.StartFfmpegProcessAsync(ffmpegArgs, outputDir, camera.Id, ct);

                        if (_activeProcesses.TryAdd(cameraId, process))
                        {
                            _ = MonitorProcessAsync(cameraId, process, webRootPath, ct);
                        }
                        break;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to start stream for camera {CameraId}", cameraId);
                        await UpdateStatusAsync(cameraId, "Offline", ct);
                        throw;
                    }
                }
                catch (Exception ex) when (retryCount < maxRetries - 1)
                {
                    _logger.LogWarning(ex, "Retry {RetryCount} for camera {CameraId}", retryCount + 1, cameraId);
                    await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, retryCount)), ct);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to start stream for camera {CameraId} after {MaxRetries} retries",
                        cameraId, maxRetries);
                    await UpdateStatusAsync(cameraId, "Failed", ct);
                    throw;
                }
            }
        }

        public async Task StopStreamAsync(int cameraId)
        {
            if (_activeProcesses.TryRemove(cameraId, out var process))
            {
                _processManager.KillProcess(cameraId, process);
                _ = CleanupHlsFilesDelayedAsync(cameraId, TimeSpan.FromMinutes(5));
            }
            await UpdateStatusAsync(cameraId, "Stopped");
        }

        private async Task CleanupHlsFilesDelayedAsync(int cameraId, TimeSpan delay)
        {
            try
            {
                await Task.Delay(delay);

                if (_activeProcesses.ContainsKey(cameraId))
                    return;

                using var scope = _sp.CreateScope();
                var webRootPath = GetWebRootPath(scope);
                var outputDir = Path.Combine(webRootPath, "api", "streams", cameraId.ToString());
                if (Directory.Exists(outputDir))
                {
                    Directory.Delete(outputDir, recursive: true);
                    _logger.LogInformation("Cleaned up HLS files for camera {CameraId}", cameraId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to cleanup HLS files for camera {CameraId}", cameraId);
            }
        }

        public async Task UpdateStatusAsync(int cameraId, string status, CancellationToken ct = default)
        {
            using var scope = _sp.CreateScope();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            var camera = await unitOfWork.Repository<Camera>()
                .GetEntityWithSpecAsync(new BaseSpecification<Camera>(c => c.Id == cameraId), ct);

            if (camera != null)
            {
                camera.Status = status;
                camera.LastHeartbeatUtc = DateTime.UtcNow;
                await unitOfWork.CompleteAsync(ct);
            }
        }

        public async Task<IEnumerable<Camera>> GetActiveStreamsAsync()
        {
            using var scope = _sp.CreateScope();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            var activeIds = _activeProcesses.Keys;
            var spec = new BaseSpecification<Camera>(c => activeIds.Contains(c.Id) && c.Status == "Online");

            return (await unitOfWork.Repository<Camera>().GetAllWithSpecAsync(spec))
                .Select(c => new Camera
                {
                    Id = c.Id,
                    Status = c.Status,
                    HlsPublicUrl = c.HlsPublicUrl
                });
        }

        private async Task MonitorProcessAsync(int cameraId, Process process, string webRootPath, CancellationToken ct)
        {
            try
            {
                await WaitForHlsFileAsync(cameraId, webRootPath, ct);

                using var scope = _sp.CreateScope();
                var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                var camera = await unitOfWork.Repository<Camera>()
                    .GetEntityWithSpecAsync(new BaseSpecification<Camera>(c => c.Id == cameraId));

                if (camera != null)
                {
                    var lastHlsUpdate = File.GetLastWriteTimeUtc(camera.HlsLocalPath!);
                    camera.Status = IsHlsActive(camera.HlsLocalPath!, lastHlsUpdate) ? "Online" : "Offline";
                    camera.LastHeartbeatUtc = DateTime.UtcNow;
                    await unitOfWork.CompleteAsync(ct);

                    using var timer = new PeriodicTimer(TimeSpan.FromSeconds(60));
                    while (!process.HasExited && !ct.IsCancellationRequested)
                    {
                        try
                        {
                            await timer.WaitForNextTickAsync(ct);

                            using var updateScope = _sp.CreateScope();
                            var updateUnitOfWork = updateScope.ServiceProvider.GetRequiredService<IUnitOfWork>();

                            var cam = await updateUnitOfWork.Repository<Camera>()
                                .GetEntityWithSpecAsync(new BaseSpecification<Camera>(c => c.Id == cameraId), ct);

                            if (cam != null)
                            {
                                var hlsFresh = IsHlsActive(cam.HlsLocalPath!, File.GetLastWriteTimeUtc(cam.HlsLocalPath!));
                                cam.Status = hlsFresh ? "Online" : "Offline";
                                cam.LastHeartbeatUtc = DateTime.UtcNow;
                                await updateUnitOfWork.CompleteAsync(ct);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to update heartbeat for camera {CameraId}", cameraId);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Process monitor failed for camera {CameraId}", cameraId);
            }
            finally
            {
                if (_activeProcesses.TryRemove(cameraId, out var proc))
                {
                    try { _processManager.KillProcess(cameraId, proc); }
                    catch { }
                }
                await UpdateStatusAsync(cameraId, "Offline", ct);
            }
        }

        private static bool IsHlsActive(string hlsPath, DateTime lastWriteTime)
        {
            return (DateTime.UtcNow - lastWriteTime).TotalSeconds < 300;
        }

        private static async Task WaitForHlsFileAsync(int cameraId, string webRootPath, CancellationToken ct)
        {
            var outputDir = Path.Combine(webRootPath, "api", "streams", cameraId.ToString());
            var hlsFile = Path.Combine(outputDir, "index.m3u8");

            var sw = Stopwatch.StartNew();
            while (sw.Elapsed < TimeSpan.FromSeconds(30) && !ct.IsCancellationRequested)
            {
                if (File.Exists(hlsFile)) return;
                await Task.Delay(500, ct);
            }
            throw new TimeoutException($"HLS file not created for camera {cameraId} within 30 seconds");
        }

        private static string GetWebRootPath(IServiceScope scope)
        {
            var env = scope.ServiceProvider.GetService<IWebHostEnvironment>();
            return env?.WebRootPath ?? GetTempWebRootPath();
        }

        private static string GetTempWebRootPath()
            => Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        public void Dispose()
        {
            if (_disposed) return;

            foreach (var process in _activeProcesses.Values)
            {
                try
                {
                    if (!process.HasExited)
                        process.Kill(true);
                }
                catch { }
                process.Dispose();
            }

            _activeProcesses.Clear();
            _startAttempts.Clear();
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }
}
