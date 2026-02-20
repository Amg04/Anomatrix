using Deadlock.Core.Domain.Entities;
using Deadlock.Core.Domain.RepositoryContracts;
using Deadlock.Core.Domain.Specifications;
using Deadlock.Core.DTO;
using Deadlock.Core.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Deadlock.UI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CamerasController : ControllerBase
    {
        private readonly IDataProtector _protector;
        private readonly IUnitOfWork _unitOfWork;
        public CamerasController(IUnitOfWork unitOfWork, IDataProtectionProvider dp)
        {
            _protector = dp.CreateProtector("cam-secrets");
            _unitOfWork = unitOfWork;
        }

        #region Create

        [Authorize(Roles = SD.ManagerRole)]
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateEditCameraDto dto)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();
            
            var spec = new BaseSpecification<MonitoredEntity>(m => m.UserId == userId);
            spec.AddOrderByDescending(m => m.LastUpdate);
            var latestMonitored = await _unitOfWork.Repository<MonitoredEntity>().GetEntityWithSpecAsync(spec);


            if (latestMonitored == null)
                return NotFound("No monitored entity found for this user.");

            string? pwd = null;
            if (!string.IsNullOrEmpty(dto.PasswordEnc))
                pwd = _protector.Protect(dto.PasswordEnc);
            var cam = new Camera()
            {
                Host = dto.Host,
                Port = dto.Port == 0 ? 554 : dto.Port,
                Username = dto.Username,
                PasswordEnc = pwd,
                RtspPath = dto.RtspPath,
                Enabled = dto.Enabled,
                CameraLocation = dto.CameraLocation,
                MonitoredEntityId = latestMonitored.Id
            };

            await _unitOfWork.Repository<Camera>().AddAsync(cam);
            await _unitOfWork.CompleteAsync();

            // return from GetById => id only
            return CreatedAtAction(nameof(GetById), new { id = cam.Id },new { cam.Id });
        }

        #endregion

        #region GetById => Details

        [Authorize]
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var cam = await _unitOfWork.Repository<Camera>().GetByIdAsync(id);
            if (cam == null)
                return NotFound();

            var url = cam.HlsPublicUrl ?? cam.RtspPath;
            return Ok(new
            {
                cam.Id,
                url,
                cam.Type,
                cam.CameraLocation,
                cam.Status,
            });
        }

        #endregion

        #region Update (Edit)

        [Authorize(Roles = SD.ManagerRole)]
        [HttpPut("{id}")]
        public async Task<IActionResult> Edit(int id, [FromBody] CreateEditCameraDto dto)
        {
            var cam = await _unitOfWork.Repository<Camera>().GetByIdAsync(id);
            if (cam == null)
                return NotFound(new { Message = $"Camera with Id {id} not found." });

            cam.Host = dto.Host;
            cam.Port = dto.Port == 0 ? 554 : dto.Port;
            cam.Username = dto.Username;
            cam.PasswordEnc = !string.IsNullOrEmpty(dto.PasswordEnc) ? _protector.Protect(dto.PasswordEnc) : null;
            cam.RtspPath = dto.RtspPath;
            cam.Enabled = dto.Enabled;
            cam.CameraLocation = dto.CameraLocation;

            await _unitOfWork.CompleteAsync();

            return Ok(new { Message = "Camera updated successfully.", cam.Id });
        }

        #endregion

        #region Delete

        [Authorize(Roles = SD.ManagerRole)]
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var cam = await _unitOfWork.Repository<Camera>().GetByIdAsync(id);
            if (cam == null)
                return NotFound(new { Message = $"Camera with Id {id} not found." });

           _unitOfWork.Repository<Camera>().Delete(cam);
           await _unitOfWork.CompleteAsync();

            return Ok(new { Message = "Camera deleted successfully.", cam.Id });
        }

        #endregion

        #region QuickAction

        [HttpPost("QuickAction")]
        public async Task<IActionResult> QuickAction(int id)
        {
            var cam = await _unitOfWork.Repository<Camera>().GetByIdAsync(id);
            if (cam == null)
                return NotFound(new { Message = $"Camera with Id : {id} not found." });
            cam.Type = "normal";
            cam.CriticalEvent++;
            await _unitOfWork.CompleteAsync();

            return Ok(new { Message = "Camera updated successfully." });
        }

        #endregion
    }
}
