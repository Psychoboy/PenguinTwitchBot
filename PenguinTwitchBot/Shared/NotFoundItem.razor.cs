using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Diagnostics;


namespace PenguinTwitchBot.Shared
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
            var originalRequestTarget = GetOriginalRequestTarget();
            var shouldSuppressWarning = ShouldSuppressNotFoundWarning();
            if (shouldSuppressWarning)
            {
                Logger?.LogDebug("Ignored expected 404 probe for {originalTarget}. NotFound page: {pageurl}.", originalRequestTarget, sanitizedUri);
            }
            else
            {
                Logger?.LogWarning("{user} tried to access {pageurl} which does not exist. Original target: {originalTarget}.", username, sanitizedUri, originalRequestTarget);
            }
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

        private bool ShouldSuppressNotFoundWarning()
        {
            var httpContext = httpContextAccessor?.HttpContext;
            if (httpContext is null)
            {
                return false;
            }

            // StatusCodePagesWithReExecute stores the original path in this feature.
            var reExecuteFeature = httpContext.Features.Get<IStatusCodeReExecuteFeature>();
            var originalPath = reExecuteFeature?.OriginalPath ?? httpContext.Request.Path.Value ?? string.Empty;

            if (!string.Equals(httpContext.Request.Method, "HEAD", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (!(originalPath.StartsWith("/gifs/", StringComparison.OrdinalIgnoreCase)
                || originalPath.StartsWith("/audio/", StringComparison.OrdinalIgnoreCase)
                || originalPath.StartsWith("/clips/", StringComparison.OrdinalIgnoreCase)
                || originalPath.StartsWith("/fishes/", StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }

            var ext = Path.GetExtension(originalPath);
            return !string.IsNullOrWhiteSpace(ext)
                && (ext.Equals(".mp3", StringComparison.OrdinalIgnoreCase)
                    || ext.Equals(".wav", StringComparison.OrdinalIgnoreCase)
                    || ext.Equals(".ogg", StringComparison.OrdinalIgnoreCase)
                    || ext.Equals(".oga", StringComparison.OrdinalIgnoreCase)
                    || ext.Equals(".opus", StringComparison.OrdinalIgnoreCase)
                    || ext.Equals(".aac", StringComparison.OrdinalIgnoreCase)
                    || ext.Equals(".m4a", StringComparison.OrdinalIgnoreCase)
                    || ext.Equals(".mp4", StringComparison.OrdinalIgnoreCase)
                    || ext.Equals(".webm", StringComparison.OrdinalIgnoreCase)
                    || ext.Equals(".ogv", StringComparison.OrdinalIgnoreCase));
        }

        private string GetOriginalRequestTarget()
        {
            var httpContext = httpContextAccessor?.HttpContext;
            if (httpContext is null)
            {
                return "UnknownTarget";
            }

            var reExecuteFeature = httpContext.Features.Get<IStatusCodeReExecuteFeature>();
            var originalPath = reExecuteFeature?.OriginalPath ?? httpContext.Request.Path.Value ?? string.Empty;
            var originalQueryString = reExecuteFeature?.OriginalQueryString ?? httpContext.Request.QueryString.Value ?? string.Empty;

            var target = $"{originalPath}{originalQueryString}";
            return target.Replace(Environment.NewLine, "").Replace("\n", "").Replace("\r", "");
        }
    }
}
