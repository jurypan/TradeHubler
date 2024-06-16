using Microsoft.Graph;
using Microsoft.Graph.Models;

namespace JCTG.WebApp.Backend.Security
{
    public class Membership(IHttpContextAccessor AuthManager, IConfiguration configuration)
    {
        //public async Task<User?> GetUser() 
        //{
        //    if (AuthManager != null && AuthManager.HttpContext != null)
        //    {
        //        return await graphServiceClient.Me.GetAsync();
        //    }
        //    return null;
        //}

        public bool IsAuthenticated()
        {
            if (AuthManager != null && AuthManager.HttpContext != null && AuthManager.HttpContext.User.Identity != null)
            {
                return AuthManager.HttpContext.User.Identity.IsAuthenticated;
            }
            return false;
        }

        public string SignoutURL()
        {
            if (AuthManager != null && AuthManager.HttpContext != null)
            {
                var instance = configuration.GetSection("AzureAd:Instance").Value;
                var domain = configuration.GetSection("AzureAd:Domain").Value;
                var signUpSignInPolicyId = configuration.GetSection("AzureAd:SignUpSignInPolicyId").Value;
                var logoutUrlOfApp = configuration.GetSection("AzureAd:LogoutUrlOfApp").Value;
                return $"{instance}{domain}/{signUpSignInPolicyId}/oauth2/v2.0/logout?post_logout_redirect_uri={logoutUrlOfApp}";

                //var tenantId = configuration.GetSection("AzureAd:TenantId").Value;
                //var logoutUrlOfApp = configuration.GetSection("AzureAd:LogoutUrlOfApp").Value;
                //return $"https://login.windows.net/{tenantId}/oauth2/logout?post_logout_redirect_uri={logoutUrlOfApp}/logout";
            }
            return string.Empty;
        }
    }
}
