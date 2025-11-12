using Microsoft.AspNetCore.Mvc;

namespace bingooo.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class WelcomeController : ControllerBase
    {
        [HttpGet]
        public IActionResult Index()
        {
            return Ok(new { success = true, message = "Welcome to Bingo API" });
        }
    }
}