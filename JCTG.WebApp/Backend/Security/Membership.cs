using Azure.Identity;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.VisualStudio.Web.CodeGenerators.Mvc.Templates.BlazorIdentity.Pages.Manage;


namespace JCTG.WebApp.Backend.Security
{
    public class Membership
    {
        private readonly IHttpContextAccessor _authManager;
        private readonly IConfiguration _configuration;
        private readonly GraphServiceClient _graphClientSecret;
        private readonly IUserService _userService;

        private readonly string clientId = string.Empty;
        private readonly string tenantId = string.Empty;
        private readonly string clientSecret = string.Empty;
        private readonly string[] scopes = [];

        public Membership(IHttpContextAccessor authManager, IConfiguration configuration, IUserService userService)
        {
            _authManager = authManager;
            _configuration = configuration;
            _userService = userService;

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

        public async Task<List<User>> GetAllUsers(Guid accountId)
        {
            // https://learn.microsoft.com/en-us/graph/api/group-list-members?view=graph-rest-1.0&tabs=csharp
            var result = await _graphClientSecret.Groups[$"{accountId}"].Members.GetAsync();
            if (result != null && result.Value != null)
            {
                return result.Value.OfType<User>().ToList();
            }
            return [];
        }

        public static User Empty()
        {
            return new User()
            {
                AccountEnabled = true,
                CreationType = "LocalAccount",
                PasswordPolicies = "DisablePasswordExpiration",
                PasswordProfile = new PasswordProfile()
                {
                    ForceChangePasswordNextSignIn = false
                },
                Identities = new List<ObjectIdentity>()
                {
                    new()
                    {
                        SignInType = "emailAddress",
                        Issuer = "justcalltheguy.onmicrosoft.com"
                    }
                }
            };
        }

        public async Task<User> GetAsync()
        {
            var id = _userService.GetUserId();
            return id == null ? throw new Exception("User is null") : await GetAsync(id);
        }

        public async Task<string> GetEmailAsync()
        {
            await Task.Yield();
            var email = _userService.GetEmail();
            return email ?? throw new Exception("User is null");
        }

        public async Task<string> GetNameAsync()
        {
            await Task.Yield();
            var name = _userService.GetUserName();
            return name ?? throw new Exception("User is null");
        }

        public async Task<bool> IsAdminAsync(Guid accountId)
        {
            var result = await _graphClientSecret.Groups[$"{accountId}"].Members.GetAsync();
            if (result != null && result.Value != null)
            {
                return result.Value.OfType<User>().ToList().Any(f => f.Id == _userService.GetUserId());
            }
            return false;
        }

        public async Task<User> GetAsync(string Id)
        {
            // https://learn.microsoft.com/en-us/graph/api/entity-get?view=graph-rest-1.0&tabs=http
            var response = await _graphClientSecret.Users[Id].GetAsync();

            if (response == null || response.Id == null)
                throw new Exception("Current entity is null");

            return response;
        }

        public async Task<User> CreateAsync(Guid accountId, User entity)
        {
            // https://learn.microsoft.com/en-us/graph/api/entity-post-users?view=graph-rest-1.0&tabs=csharp

            // {
            //    "accountEnabled": true,
            //    "creationType": "LocalAccount",
            //    "displayName": "John Doe",
            //    "passwordProfile": {
            //        "forceChangePasswordNextSignIn": false,
            //        "password": "YourPassword123!"
            //    },
            //    "passwordPolicies": "DisablePasswordExpiration",
            //    "identities": [
            //        {
            //            "signInType": "emailAddress",
            //            "issuer": "your-tenant-name.onmicrosoft.com",
            //            "issuerAssignedId": "john.doe@example.com"
            //        }
            //    ]
            //}


            // For Identity Providers, please see this page : https://learn.microsoft.com/en-gb/azure/active-directory-b2c/identity-provider-google?pivots=b2c-entity-flow


            var user = await _graphClientSecret.Users.PostAsync(entity);

            if (user == null || user.Id == null)
                throw new Exception("User is null");


            // https://learn.microsoft.com/en-us/graph/api/group-post-owners?view=graph-rest-1.0&tabs=csharp
            await _graphClientSecret.Groups[$"{accountId}"].Owners.Ref.PostAsync(new ReferenceCreate
            {
                OdataId = $"https://graph.microsoft.com/v1.0/users/{user.Id}",
            });


            // https://learn.microsoft.com/en-us/graph/api/group-post-members?view=graph-rest-1.0&tabs=csharp
            await _graphClientSecret.Groups[$"{accountId}"].Members.Ref.PostAsync(new ReferenceCreate
            {
                OdataId = $"https://graph.microsoft.com/v1.0/directoryObjects/{user.Id}",
            });


            return user;
        }

        public async Task<User> UpdateAsync(string Id, User user)
        {
            // https://learn.microsoft.com/en-us/graph/api/entity-post-users?view=graph-rest-1.0&tabs=csharp
            var response = await _graphClientSecret.Users[Id].PatchAsync(user);

            if (response == null || response.Id == null)
                throw new Exception("User is null");

            return response;
        }

        public async Task DeleteAsync(string Id)
        {
            // https://learn.microsoft.com/en-us/graph/api/entity-delete?view=graph-rest-1.0&tabs=csharp
            await _graphClientSecret.Users[Id].DeleteAsync();
        }

    }
}
