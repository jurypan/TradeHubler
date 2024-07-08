using JCTG.WebApp.Backend.Security;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Server.Circuits;

namespace JCTG.WebApp.Backend.Middleware
{
    public class CustomCircuitHandler : CircuitHandler
    {
        private readonly NavigationManager _navigationManager;
        private readonly Membership _membership;

        public CustomCircuitHandler(NavigationManager navigationManager, Membership membership)
        {
            _navigationManager = navigationManager;
            _membership = membership;
        }

        public override Task OnConnectionDownAsync(Circuit circuit, CancellationToken cancellationToken)
        {
            // Redirect to login page
            _navigationManager.NavigateTo(_membership.SigninURL());
            return Task.CompletedTask;
        }
    }
}
