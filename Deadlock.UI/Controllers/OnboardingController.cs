using Deadlock.Core.Domain.Entities;
using Deadlock.Core.Domain.RepositoryContracts;
using Deadlock.Core.Domain.Specifications;
using Deadlock.Core.DTO;
using Deadlock.Core.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Deadlock.UI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class OnboardingController : ControllerBase
    {
        private readonly IUnitOfWork _unitOfWork;

        public OnboardingController(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        #region SiteSelection

        [Authorize]
        [HttpGet("SiteSelection")]
        public async Task<IActionResult> SiteSelection()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if(string.IsNullOrWhiteSpace(userId)) 
                return Unauthorized();

            var spec = new BaseSpecification<MonitoredEntity>(m => m.UserId == userId);
            spec.Includes.Add(t => t.Cameras);
            var yourSites = (await _unitOfWork.Repository<MonitoredEntity>()
                .GetAllWithSpecAsync(spec))
                .Take(3)
                .Select(m => new YourSitesDto
                {
                    Name = m.EntityName,
                    NumberOfCameraes = m.Cameras.Count
                }).ToList();
           
            SiteSelectionDto dto = new SiteSelectionDto()
            {
                EntityTypes = Enum.GetNames(typeof(EntityTypes)).ToList(),
                EntityName = string.Empty, 
                YourSites = yourSites
            };

            return Ok(dto);
        }

        [Authorize]
        [HttpPost("SiteSelection")]
        public async Task<IActionResult> SiteSelectionPost([FromBody] SiteSelectionRequestDto request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var spec = new BaseSpecification<MonitoredEntity>(m => m.EntityName == request.EntityName && m.UserId == userId);
            var monitoredFromDb = await _unitOfWork.Repository<MonitoredEntity>().GetEntityWithSpecAsync(spec);

            if (monitoredFromDb == null) 
            {
                MonitoredEntity obj = new MonitoredEntity()
                {
                    EntityName = request.EntityName,
                    UserId = userId,
                    LastUpdate = DateTime.UtcNow
                };

                if (!Enum.TryParse<EntityTypes>(request.EntityType, true, out var entityType))
                {
                    var validValues = string.Join(", ", Enum.GetNames(typeof(EntityTypes)));
                    return BadRequest($"Invalid EntityType value. Valid values are: {validValues}");
                }

                obj.EntityType = entityType;

                await _unitOfWork.Repository<MonitoredEntity>().AddAsync(obj);        
            }
            else
            {
                monitoredFromDb.LastUpdate = DateTime.UtcNow;
            }
            await _unitOfWork.CompleteAsync();

            return Ok(new { Message = "Data received successfully" });
        }

        #endregion

    }
}
