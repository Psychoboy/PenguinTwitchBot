using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;


namespace DotNetTwitchBot.Shared
{
    public partial class NotFoundItem : ComponentBase
    {
        [Inject]
        private ILogger<NotFoundItem>? Logger { get; set; }
        [Inject]
        public NavigationManager NavigationManager { get; set; } = default!;
        [Inject]
        AuthenticationStateProvider AuthenticationStateProvider { get; set; } = default!;

        protected override async Task OnInitializedAsync()
        {
            var authState = await AuthenticationStateProvider
           .GetAuthenticationStateAsync();
            var user = authState.User;
            var username = "UnknownUser";
            if (user.Identity is not null && user.Identity.IsAuthenticated && user.Identity.Name != null)
            {
                username = user.Identity.Name;
                if (user.HasClaim(x => x.Type.Equals("DisplayName")))
                {
                    username = user.Claims.Where(x => x.Type.Equals("DisplayName")).First().Value;
                }
            }
            Logger?.LogWarning("{user} tried to access {pageurl} which does not exist.", username, NavigationManager.Uri);
            await base.OnInitializedAsync();
        }
    }
}
