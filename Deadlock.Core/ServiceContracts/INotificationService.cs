namespace Deadlock.Core.ServiceContracts
{
    public interface INotificationService
    {
        Task NotifyStreamAvailableAsync(int cameraId, string hlsUrl, string? managerUserId);
    }
}
