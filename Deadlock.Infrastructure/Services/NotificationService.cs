using Deadlock.Core.ServiceContracts;
using Deadlock.Infrastructure.Services.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace Deadlock.Infrastructure.Services
{
    public class NotificationService : INotificationService
    {
        private readonly IHubContext<CameraHub> _hubContext;
        public NotificationService(IHubContext<CameraHub> hubContext)
        {
            _hubContext = hubContext;
        }

        public async Task NotifyStreamAvailableAsync(int cameraId, string hlsUrl, string? managerUserId)
        {
            var notification = new { Id = cameraId, HlsPublicUrl = hlsUrl };

            if (!string.IsNullOrEmpty(managerUserId))
            {
                var groupName = $"Group_{managerUserId}";
                await _hubContext.Clients.Group(groupName)
                    .SendAsync("NewHlsStreamAvailable", notification);
            }
            else
            {
                await _hubContext.Clients.All
                    .SendAsync("NewHlsStreamAvailable", notification);
            }
        }
    }
}
