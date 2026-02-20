using Deadlock.Core.Domain.Entities;
using Deadlock.Core.Domain.RepositoryContracts;
using Deadlock.Core.Domain.Specifications;
using Deadlock.Core.ServiceContracts;

namespace Deadlock.UI.Services
{
    public class RtspPumpService : BackgroundService
    {
        private readonly IServiceProvider _sp;
        private readonly ICameraStreamService _streamService;
        public RtspPumpService(
            IServiceProvider sp,
            ICameraStreamService streamService)
        {
            _sp = sp;
            _streamService = streamService;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                using var scope = _sp.CreateScope();
                var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                var spec = new BaseSpecification<Camera>(c => c.Enabled);
                var enabledCameras = (await unitOfWork.Repository<Camera>()
                    .GetAllWithSpecAsync(spec, stoppingToken)).Select(c => c.Id);
                var enabledIds = enabledCameras.ToHashSet();

                var activeIds = (await _streamService.GetActiveStreamsAsync())
                    .Select(s => s.Id) .ToHashSet();

                // Start cameras that are enabled but not running
                foreach (var cameraId in enabledCameras)
                {
                    if (activeIds.Contains(cameraId)) continue;
                    _ = _streamService.StartStreamAsync(cameraId, stoppingToken);
                }

                // Stop cameras that are no longer enabled
                foreach (var cameraId in activeIds)
                {
                    if (!enabledIds.Contains(cameraId))
                        await _streamService.StopStreamAsync(cameraId);
                }

                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
        }
    }
}
