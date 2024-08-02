using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JCTG.WebApp.Backend.Api
{
    [ApiController]
    [Route("api")]
    public class KeepAliveController : Controller
    {
        [AllowAnonymous]
        [HttpGet("ping")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public IActionResult Ping()
        {
            return Ok("Pong");
        }
    }
}
