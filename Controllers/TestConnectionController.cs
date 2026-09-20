using Microsoft.AspNetCore.Mvc;

namespace EduSathi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TestConnectionController : ControllerBase
    {
        [HttpGet]
        public IActionResult Get()
        {
            return Ok(new
            {
                success = true,
                message = "Connection successful! The React Native app has reached the .NET backend.",
                timestamp = DateTime.UtcNow
            });
        }
    }
}