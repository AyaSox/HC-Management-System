using Microsoft.AspNetCore.Mvc;

namespace HRManagementSystem.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class HealthController : ControllerBase
    {
        [HttpGet]
        public IActionResult Get()
        {
            return Ok(new { 
                Status = "Healthy", 
                Timestamp = DateTime.UtcNow,
                Application = "HR Management System",
                Version = "1.0.0"
            });
        }

        [HttpGet("ready")]
        public IActionResult Ready()
        {
            return Ok(new { 
                Status = "Ready", 
                Message = "Application is ready to serve requests",
                Timestamp = DateTime.UtcNow
            });
        }
    }
}