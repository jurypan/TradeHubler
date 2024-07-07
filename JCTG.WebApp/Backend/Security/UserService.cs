using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace JCTG.WebApp.Backend.Security
{
    public interface IUserService
    {
        string? GetUserId();
        string? GetUserName();
        string? GetEmail();
    }

    public class UserService(IHttpContextAccessor httpContextAccessor) : IUserService
    {
        private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;

        public string? GetUserId()
        {
            return _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        }

        public string? GetUserName()
        {
            return _httpContextAccessor.HttpContext?.User?.FindFirstValue("name");
        }

        public string? GetEmail()
        {
            return _httpContextAccessor.HttpContext?.User?.FindFirstValue("emails");
        }
    }

}
