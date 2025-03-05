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
        [Inject]
        IHttpContextAccessor httpContextAccessor { get; set; } = default!;

        protected override void OnInitialized()
        {
            var authState = AuthenticationStateProvider.GetAuthenticationStateAsync().Result;
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
            var sanitizedUri = NavigationManager.Uri.Replace(Environment.NewLine, "").Replace("\n", "").Replace("\r", "");
            Logger?.LogWarning("{user} tried to access {pageurl} which does not exist.", username, sanitizedUri);
            if (httpContextAccessor is not null)
            {
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                if (httpContextAccessor.HttpContext.Response.HasStarted == false)
                {
                    httpContextAccessor.HttpContext.Response.StatusCode = 404;
                }
#pragma warning restore CS8602 // Dereference of a possibly null reference.
            }
            base.OnInitialized();
        }
    }
}
