using DotNetTwitchBot.Bot.Commands.Features;
using DotNetTwitchBot.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using System.Security.Claims;

namespace DotNetTwitchBot.Controllers
{
    public class HomeController : Controller
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<HomeController> _logger;
        private readonly IViewerFeature _viewerFeature;

        public HomeController(
            IConfiguration configuration,
            ILogger<HomeController> logger,
            IViewerFeature viewerFeature)
        {
            _configuration = configuration;
            _logger = logger;
            _viewerFeature = viewerFeature;
        }
        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        public IActionResult YtPlayer()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        [HttpGet("/signin")]
        public IActionResult Signin()
        {
#if DEBUG
            var url = getAuthorizationCodeUrl("https://localhost:7293/redirect", new());
#else
            var url = getAuthorizationCodeUrl("https://bot.superpenguin.tv/redirect", new());
#endif
            return Redirect(url);
        }

        [HttpGet("/redirect")]
        public async Task<IActionResult> RedirectFromTwitch([FromQuery(Name = "code")] string code)
        {
            var api = new TwitchLib.Api.TwitchAPI();
            api.Settings.ClientId = _configuration["twitchClientId"];
#if DEBUG
            var resp = await api.Auth.GetAccessTokenFromCodeAsync(code, _configuration["twitchClientSecret"], "https://localhost:7293/redirect");
#else
            var resp = await api.Auth.GetAccessTokenFromCodeAsync(code, _configuration["twitchClientSecret"], "https://bot.superpenguin.tv/redirect");
#endif
            api.Settings.AccessToken = resp.AccessToken;
            var users = await api.Helix.Users.GetUsersAsync();
            if (users.Users.Length > 0)
            {
                var user = users.Users[0];
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.Name, user.Login),
                    new Claim(ClaimTypes.Role, _configuration["broadcaster"].Equals(user.Login) ? "Streamer": "Viewer"),
                    new Claim("ProfilePicture", user.ProfileImageUrl),
                    new Claim("DisplayName", user.DisplayName)
                };


                if (await _viewerFeature.IsFollower(user.Login))
                {
                    claims.Add(new Claim(ClaimTypes.Role, "Follower"));
                }
                var viewer = await _viewerFeature.GetViewer(user.Login);
                if (viewer != null)
                {
                    if (viewer.isMod)
                    {
                        claims.Add(new Claim(ClaimTypes.Role, "Moderator"));
                    }

                    if (viewer.isSub)
                    {
                        claims.Add(new Claim(ClaimTypes.Role, "Subscriber"));
                    }

                    if (viewer.isVip)
                    {
                        claims.Add(new Claim(ClaimTypes.Role, "VIP"));
                    }
                }

                var claimsIdentity = new ClaimsIdentity(
                    claims, CookieAuthenticationDefaults.AuthenticationScheme
                );

                var authProperties = new AuthenticationProperties
                {

                };


                await HttpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    new ClaimsPrincipal(claimsIdentity),
                    authProperties
                );
                _logger.LogInformation("{0} logged in to web interface", user.Login);
            }
            return Redirect("/");
        }

        [HttpGet("/signout")]
        public async Task<IActionResult> Signout()
        {
            // Clear the existing external cookie
            await HttpContext.SignOutAsync(
                CookieAuthenticationDefaults.AuthenticationScheme);
            return Redirect("/");
        }

        private string getAuthorizationCodeUrl(string redirectUri, List<string> scopes)
        {
            var scopesStr = String.Join('+', scopes);

            return "https://id.twitch.tv/oauth2/authorize?" +
                   $"client_id={_configuration["twitchClientId"]}&" +
                   $"redirect_uri={System.Web.HttpUtility.UrlEncode(redirectUri)}&" +
                   "response_type=code&" +
                   $"scope=";
        }
    }
}