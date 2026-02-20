using Deadlock.Core.Domain.Entities;
using Deadlock.Core.Domain.RepositoryContracts;
using Deadlock.Core.DTO;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace Deadlock.UI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DashboardController : ControllerBase
    {
        private readonly IUnitOfWork _unitOfWork;
        public DashboardController(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        #region Index

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var allCameras = await _unitOfWork.Repository<Camera>().GetAllAsync();

            DashboardDto dashboardDto = new DashboardDto()
            {
                TotalAlerts = ( await _unitOfWork.Repository<CameraDetection>().GetAllAsync()).Count(),
                ActiveCameras = allCameras.Count(c => c.Enabled),
                CriticalEvents = allCameras.Sum(c => c.CriticalEvent),
                CameraStatus = allCameras.Select(c => new CameraStatusDto
                {
                    Id = c.Id,
                    UserName = c.Username ?? "",
                    Enabled = c.Enabled
                }).ToList()
            };

            return Ok(dashboardDto);
        }

        #endregion

    }
}
