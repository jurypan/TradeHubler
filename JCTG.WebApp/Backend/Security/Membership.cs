using Azure.Identity;
using Microsoft.Graph;
using Microsoft.Graph.Models;


namespace JCTG.WebApp.Backend.Security
{
    public class Membership
    {
        private readonly IHttpContextAccessor _authManager;
        private readonly IConfiguration _configuration;
        private readonly GraphServiceClient _graphClientSecret;
        private readonly GraphServiceClient _graphDefaultAzure;

        private readonly string clientId = string.Empty;
        private readonly string tenantId = string.Empty;
        private readonly string clientSecret = string.Empty;
        private readonly string[] scopes = [];

        public Membership(IHttpContextAccessor authManager, IConfiguration configuration)
        {
            _authManager = authManager;
            _configuration = configuration;

            tenantId = configuration["AzureAd:TenantId"];
            clientId = configuration["AzureAd:ClientId"];
            clientSecret = configuration["AzureAd:ClientSecret"];
            scopes = configuration.GetSection("Graph:Scopes").Get<string[]>();

            // https://learn.microsoft.com/dotnet/api/azure.identity.clientsecretcredential
            _graphClientSecret = new GraphServiceClient(new ClientSecretCredential(tenantId, clientId, clientSecret, new DeviceCodeCredentialOptions
            {
                AuthorityHost = AzureAuthorityHosts.AzurePublicCloud,
                ClientId = clientId,
                TenantId = tenantId,
                DeviceCodeCallback = (code, cancellation) =>
                {
                    Console.WriteLine(code.Message);
                    return Task.FromResult(0);
                },
            }), scopes);

            // https://learn.microsoft.com/en-us/dotnet/azure/sdk/authentication/create-token-credentials-from-configuration#create-a-defaultazurecredential-type
            _graphDefaultAzure = new GraphServiceClient(new DefaultAzureCredential(new DefaultAzureCredentialOptions()
            {
                AuthorityHost = AzureAuthorityHosts.AzurePublicCloud,
                TenantId = tenantId,
            }));
        }

        public bool IsAuthenticated()
        {
            if (_authManager != null && _authManager.HttpContext != null && _authManager.HttpContext.User.Identity != null)
            {
                return _authManager.HttpContext.User.Identity.IsAuthenticated;
            }
            return false;
        }

        public string SignoutURL()
        {
            if (_authManager != null && _authManager.HttpContext != null)
            {
                var instance = _configuration.GetSection("AzureAd:Instance").Value;
                var domain = _configuration.GetSection("AzureAd:Domain").Value;
                var signUpSignInPolicyId = _configuration.GetSection("AzureAd:SignUpSignInPolicyId").Value;
                var logoutUrlOfApp = _configuration.GetSection("AzureAd:LogoutUrlOfApp").Value;
                return $"{instance}{domain}/{signUpSignInPolicyId}/oauth2/v2.0/logout?post_logout_redirect_uri={logoutUrlOfApp}";

                //var tenantId = _configuration.GetSection("AzureAd:TenantId").Value;
                //var logoutUrlOfApp = _configuration.GetSection("AzureAd:LogoutUrlOfApp").Value;
                //return $"https://login.windows.net/{tenantId}/oauth2/logout?post_logout_redirect_uri={logoutUrlOfApp}/logout";
            }
            return string.Empty;
        }

        public async Task<List<User>> GetAllUsers()
        {
            // https://learn.microsoft.com/en-us/graph/api/user-list?view=graph-rest-1.0&tabs=http
            var result = await _graphClientSecret.Users.GetAsync();
            if (result != null && result.Value != null)
            {
                return result.Value;
            }
            return [];
        }

        public async Task<User> GetAsync()
        {
            // https://learn.microsoft.com/en-us/graph/api/user-get?view=graph-rest-1.0&tabs=http
            var response = await _graphDefaultAzure.Me.GetAsync();
           
            if(response == null || response.Id == null)
                throw new Exception("Current user is null");

            return await GetAsync(response.Id);
        }

        public async Task<User> GetAsync(string Id)
        {
            // https://learn.microsoft.com/en-us/graph/api/user-get?view=graph-rest-1.0&tabs=http
            var response = await _graphClientSecret.Users[Id].GetAsync();

            if (response == null || response.Id == null)
                throw new Exception("Current user is null");

            return response;
        }

        public async Task<User> CreateAsync(User user)
        {
            // https://learn.microsoft.com/en-us/graph/api/user-post-users?view=graph-rest-1.0&tabs=csharp
            var response = await _graphClientSecret.Users.PostAsync(user);

            if (response == null || response.Id == null)
                throw new Exception("User is null");

            return response;
        }

        public async Task<User> UpdateAsync(string Id, User user)
        {
            // https://learn.microsoft.com/en-us/graph/api/user-post-users?view=graph-rest-1.0&tabs=csharp
            var response = await _graphClientSecret.Users[Id].PatchAsync(user);

            if (response == null || response.Id == null)
                throw new Exception("User is null");

            return response;
        }
    }
}
