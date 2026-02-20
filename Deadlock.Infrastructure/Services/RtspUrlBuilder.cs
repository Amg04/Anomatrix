using Deadlock.Core.Domain.Entities;
using Deadlock.Core.ServiceContracts;

namespace Deadlock.Core.Services
{
    public class RtspUrlBuilder : IRtspUrlBuilder
    {
        public string Build(Camera cam, string? pwd)
        {
            var host = cam.Host?.Trim();
            var path = cam.RtspPath.StartsWith("/") ? cam.RtspPath : "/" + cam.RtspPath;

            if (string.IsNullOrEmpty(pwd) && string.IsNullOrEmpty(cam.Username))
            {
                return $"rtsp://{host}:{cam.Port}{path}";
            }
            else
            {
                if(!string.IsNullOrEmpty(cam.Username) && !string.IsNullOrEmpty(pwd))
                {
                    var user = Uri.EscapeDataString(cam.Username);
                    var pass = Uri.EscapeDataString(pwd);
                    return $"rtsp://{user}:{pass}@{host}:{cam.Port}{path}";
                }
                return "User name or Password Not valid";
            }
        }
    }
}
