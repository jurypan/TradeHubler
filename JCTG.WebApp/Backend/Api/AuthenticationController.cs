using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication;
using JCTG.WebApp.Backend.Security;
using Microsoft.AspNetCore.Authentication.MicrosoftAccount;


namespace JCTG.WebApp.Backend.Api
{
    [ApiController]
    [Route("Authentication")]
    public class AuthenticationController(Membership memberShip) : ControllerBase
    {
        private readonly Serilog.ILogger _logger = Serilog.Log.ForContext<AuthenticationController>();

        [HttpGet("Login")]
        public async Task<IActionResult> Login(string redirectUrl)
        {
            var props = new AuthenticationProperties()
            {
                RedirectUri = redirectUrl == null ? "/" : redirectUrl,
            };
            return await Task.Run(() => Challenge(props, MicrosoftAccountDefaults.AuthenticationScheme));
        }

        [HttpGet("Logout")]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync();
            return Redirect(memberShip.SignoutURL());
        }
    }
}
