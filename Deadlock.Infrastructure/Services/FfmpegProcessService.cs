using Deadlock.Core.Domain.Entities;
using Deadlock.Core.DTO;
using Deadlock.Core.ServiceContracts;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace Deadlock.Core.Services
{
    public class FfmpegProcessService : ICameraProcessManager
    {
        private readonly ILogger<FfmpegProcessService> _logger;
        private readonly ConcurrentDictionary<int, Process> _processes = new();
        public FfmpegProcessService(ILogger<FfmpegProcessService> logger)
        {
            _logger = logger;
        }
        public async Task<Process> StartFfmpegProcessAsync(FfmpegArgs args, string outputDir, int cameraId, CancellationToken ct)
        {
            Directory.CreateDirectory(outputDir);
            
           
            var ffmpegPath = Path.Combine(
                AppContext.BaseDirectory,
                "tools", "ffmpeg", "ffmpeg.exe"
            );

            var ffmpegArgs = $"-rtsp_transport tcp -i \"{args.InputRtsp}\" " +
                           "-c:v copy -an " +
                           "-f hls " +
                           "-hls_time 2 -hls_list_size 6 -hls_flags delete_segments+append_list+discont_start " +
                           $"-hls_segment_filename \"{outputDir}/segment_%03d.ts\" " +
                           $"\"{args.HlsFile}\"";

            var psi = new ProcessStartInfo
            {
                FileName = ffmpegPath,
                Arguments = ffmpegArgs,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
            process.Start();
            _processes.TryAdd(cameraId, process);

            process.Exited += (sender, e) =>
            {
                _processes.TryRemove(cameraId, out _);
            };

            _ = Task.Run(async () =>
            {
                using StreamWriter logFile = new(Path.Combine(outputDir, "ffmpeg_error.log"), append: true);
                while (!process.StandardError.EndOfStream && !ct.IsCancellationRequested)
                {
                    var line = await process.StandardError.ReadLineAsync();
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    await logFile.WriteLineAsync($"{DateTime.UtcNow}: {line}");
                }
            }, ct);

            return process;
        }

        public void KillProcess(int cameraId, Process process)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(true);
                    _processes.TryRemove(cameraId, out _);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to kill process for camera {CameraId}", cameraId);
            }
        }

        public bool IsProcessRunning(int cameraId) => _processes.ContainsKey(cameraId);
    }
}