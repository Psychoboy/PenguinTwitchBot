using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Diagnostics;


namespace PenguinTwitchBot.Shared
{
    public partial class NotFoundItem : ComponentBase
    {
        private static readonly string[] ExpectedProbePrefixes = ["/gifs/", "/audio/", "/clips/", "/fishes/"];
        private static readonly HashSet<string> ExpectedMediaExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".mp3", ".wav", ".ogg", ".oga", ".opus", ".aac", ".m4a", ".mp4", ".webm", ".ogv"
        };

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

            if (!IsHeadRequest(httpContext.Request.Method))
            {
                return false;
            }

            // StatusCodePagesWithReExecute stores the original path in this feature.
            var reExecuteFeature = httpContext.Features.Get<IStatusCodeReExecuteFeature>();
            var originalPath = reExecuteFeature?.OriginalPath ?? httpContext.Request.Path.Value ?? string.Empty;

            return IsExpectedProbePath(originalPath) && IsExpectedMediaExtension(originalPath);
        }

        private static bool IsHeadRequest(string? method)
            => string.Equals(method, "HEAD", StringComparison.OrdinalIgnoreCase);

        private static bool IsExpectedProbePath(string originalPath)
            => ExpectedProbePrefixes.Any(prefix => originalPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

        private static bool IsExpectedMediaExtension(string originalPath)
        {
            var ext = Path.GetExtension(originalPath);
            return !string.IsNullOrWhiteSpace(ext) && ExpectedMediaExtensions.Contains(ext);
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
