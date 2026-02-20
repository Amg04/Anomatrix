using Deadlock.Core.Domain.Entities;
using Deadlock.Core.Domain.RepositoryContracts;
using Deadlock.Core.Domain.Specifications;
using Deadlock.Core.DTO;
using Deadlock.Core.Helpers;
using Deadlock.Core.ServiceContracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Deadlock.UI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class HomeController : ControllerBase
    {
        private readonly IDataProtector _protector;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IRtspUrlBuilder _urlBuilder;
        private readonly UserManager<AppUser> _userManager;

        public HomeController(IUnitOfWork unitOfWork,
            IDataProtectionProvider dp,
            IRtspUrlBuilder urlBuilder,
            UserManager<AppUser> userManager)
        {

            _protector = dp.CreateProtector("cam-secrets");
            _unitOfWork = unitOfWork;
            _urlBuilder = urlBuilder;
            _userManager = userManager;
        }


        #region GetHlsById 

        [Authorize]
        [HttpGet("hls/{id}")]
        public async Task<IActionResult> GetHlsById(int id)
        {
            var cam = await _unitOfWork.Repository<Camera>().GetByIdAsync(id);

            if (cam == null || !cam.Enabled)
                return NotFound();

            string? url;
            if (string.IsNullOrWhiteSpace(cam.Host)) // http not rtsp
            {
                url = cam.RtspPath;
            }
            else // rtsp
            {
                if (string.IsNullOrEmpty(cam.HlsPublicUrl))
                    return Problem(
                        statusCode: 503,
                        title: "Stream not ready",
                        detail: "The requested stream is currently not available."
                    );
                url = cam.HlsPublicUrl;
            }

            return Ok(new { url = url });
        }

        #endregion

        #region hlsByUser

        [Authorize]
        [HttpGet("hlsByUser")]
        public async Task<IActionResult> hlsByUser()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            if (User.IsInRole(SD.ObserverRole))
            {
                var user = await _userManager.FindByIdAsync(userId);
                if (user?.ManagerId == null)
                    return NotFound("Observer has no manager assigned.");
                userId = user?.ManagerId;
            }

            var spec = new BaseSpecification<MonitoredEntity>(m => m.UserId == userId);
            spec.AddOrderByDescending(m => m.LastUpdate);
            var latestMonitored = await _unitOfWork.Repository<MonitoredEntity>().GetEntityWithSpecAsync(spec);

            if (latestMonitored == null)
                return NotFound("No monitored entity found.");


            var cameraSpec = new BaseSpecification<Camera>
              (cam => cam.MonitoredEntityId == latestMonitored.Id && cam.Enabled);

            var cams = (await _unitOfWork.Repository<Camera>().GetAllWithSpecAsync(cameraSpec))
                .Select(cam => new { cam.Id, url = cam.HlsPublicUrl ?? cam.RtspPath })
                .ToList();

            if (!cams.Any())
                return NotFound("No active streams available.");

            return Ok(cams);
        }

        #endregion

        #region hls

        [HttpGet("AllHls")]
        public async Task<IActionResult> AllHls()
        {
            var cams = (await _unitOfWork.Repository<Camera>().GetAllAsync())
                .Select(cam => new { cam.Id, url = cam.HlsPublicUrl ?? cam.RtspPath })
                .ToList();

            if (!cams.Any())
                return NotFound("No Hls available.");

            return Ok(cams);
        }

        #endregion

        #region LiveCamera

        [Authorize]
        [HttpGet("LiveCamera")]
        public async Task<IActionResult> LiveCamera()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            if (User.IsInRole(SD.ObserverRole))
            {
                var user = await _userManager.FindByIdAsync(userId);
                if (user?.ManagerId == null)
                    return NotFound("Observer has no manager assigned.");
                userId = user?.ManagerId;
            }

            var spec = new BaseSpecification<MonitoredEntity>(m => m.UserId == userId);
            spec.AddOrderByDescending(m => m.LastUpdate);
            var latestMonitored = await _unitOfWork.Repository<MonitoredEntity>().GetEntityWithSpecAsync(spec);

            if (latestMonitored == null)
                return NotFound("No monitored entity found.");


            var cameraSpec = new BaseSpecification<Camera>
              (cam => cam.MonitoredEntityId == latestMonitored.Id && cam.Enabled);

            var cams = (await _unitOfWork.Repository<Camera>().GetAllWithSpecAsync(cameraSpec))
                .Select(cam => new { cam.Id, url = cam.HlsPublicUrl ?? cam.RtspPath })
                .ToList();

            if (!cams.Any())
                return NotFound("No active streams available.");

            return Ok(cams);
        }

        #endregion

        #region CameraDetection

        [HttpPost("CameraDetection")]
        public async Task<IActionResult> CameraDetection([FromBody] IEnumerable<CameraDetectionDto> results)
        {
            var dangerResults = results
                .Where(item => (item.Status != null && (item.Status.ToLower() == "abnormal") 
                || item.Crowd_density > 0.4f || item.Weapon_count > 0 || item.Abnormal_count > 0)).ToList();

            foreach (var item in dangerResults)
            {
                var cam = await _unitOfWork.Repository<Camera>().GetByIdAsync(item.CameraId);
                if (cam == null) continue;
                string alertType = DetermineAlertType(item);
                cam.Type = alertType;
            }
            await _unitOfWork.Repository<CameraDetection>()
                .AddRangeAsync(dangerResults.Select(d => (CameraDetection)d));
            await _unitOfWork.CompleteAsync();

            return Ok(new { TotalDangerous = dangerResults.Count });
        }

        #endregion

        #region GetRtsp

        [HttpGet("rtsp/{id}")]
        public async Task<IActionResult> GetRtsp(int id)
        {
            var cam = await _unitOfWork.Repository<Camera>().GetByIdAsync(id);

            if (cam == null)
                return NotFound();

            string? url;
            if (string.IsNullOrWhiteSpace(cam.Host)) // http not rtsp
            {
                url = cam.RtspPath;
            }
            else // rtsp
            {
                string? pwd = null;
                if (!string.IsNullOrEmpty(cam.PasswordEnc))
                    pwd = _protector.Unprotect(cam.PasswordEnc);
                var rtsp = _urlBuilder.Build(cam, pwd);

                url = rtsp;
            }

            return Ok(new { url = url });
        }

        #endregion

        #region GetAllRtsp

        //http://localhost:43979/Home/rtsp
        [HttpGet("rtsp")]
        public async Task<IActionResult> GetAllRtsp()
        {
            var cams = (await _unitOfWork.Repository<Camera>().GetAllAsync())
                .Where(cam => cam.Enabled)
                .Select(cam =>
                {
                    string? url;
                    if (string.IsNullOrWhiteSpace(cam.Host))
                    {
                        url = cam.RtspPath;
                    }
                    else
                    {
                        string? pwd = !string.IsNullOrEmpty(cam.PasswordEnc) ? _protector.Unprotect(cam.PasswordEnc) : null;
                        url = _urlBuilder.Build(cam, pwd);
                    }
                    return new { cam.Id, url = url };
                })
                .ToList();

            if (!cams.Any())
                return NotFound("No active RTSP streams available.");

            return Ok(cams);
        }

        #endregion

        #region Helper methods

        private string DetermineAlertType(CameraDetectionDto item)
        {
            if (item.Status?.ToLower() == "abnormal") return "abnormal";
            if (item.Crowd_density > 0.4f) return "Crowd Density";
            if (item.Weapon_count > 0) return "Weapon Detected";
            if (item.Abnormal_count > 0) return "Abnormal Activity";

            return "Unknown Alert";
        }

        #endregion
    }
}
