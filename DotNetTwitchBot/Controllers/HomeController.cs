using DotNetTwitchBot.Bot.Commands.Features;
using DotNetTwitchBot.Bot.TwitchServices;
using DotNetTwitchBot.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using System.Diagnostics;
using System.Security.Claims;

namespace DotNetTwitchBot.Controllers
{
    public class HomeController : Controller
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<HomeController> _logger;
        private readonly IViewerFeature _viewerFeature;
        private readonly SettingsFileManager _settingsFileManager;
        private readonly IMemoryCache _stateCache;

        public HomeController(
            IConfiguration configuration,
            ILogger<HomeController> logger,
            SettingsFileManager settingsFileManager,
            IViewerFeature viewerFeature,
            IMemoryCache stateCache)
        {
            _configuration = configuration;
            _logger = logger;
            _viewerFeature = viewerFeature;
            _settingsFileManager = settingsFileManager;
            _stateCache = stateCache;
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

        [HttpGet("/streamersignin")]
        [Authorize(Roles = "Streamer")]
        public IActionResult StreamerSignin()
        {
            _logger.LogInformation("{ipAddress} accessed /streamersign.", HttpContext.Connection?.RemoteIpAddress);
#if DEBUG
            var url = GetBotScopeUrl("https://localhost:7293/streamerredirect", _configuration["twitchClientId"]);
#else
            var url = GetBotScopeUrl("https://bot.superpenguin.tv/streamerredirect", _configuration["twitchClientId"]);
#endif

            return Redirect(url);
        }
        [HttpGet("streamerredirect")]
        public async Task<IActionResult> StreamerRedirect([FromQuery(Name = "code")] string code, [FromQuery(Name = "state")] string state)
        {
            _logger.LogInformation("{ipAddress} accessed /streamerredirect.", HttpContext.Connection?.RemoteIpAddress);
            if (_stateCache.TryGetValue(state, out var val))
            {
                _stateCache.Remove(state);
            }
            else
            {
                return Redirect("/");
            }
            var api = new TwitchLib.Api.TwitchAPI();
            api.Settings.ClientId = _configuration["twitchClientId"];
#if DEBUG
            var resp = await api.Auth.GetAccessTokenFromCodeAsync(code, _configuration["twitchClientSecret"], "https://localhost:7293/streamerredirect");
#else
            var resp = await api.Auth.GetAccessTokenFromCodeAsync(code, _configuration["twitchClientSecret"], "https://localhost:7293/streamerredirect");
#endif

            if (resp == null) { return Redirect("/"); }

            _configuration["twitchAccessToken"] = resp.AccessToken;
            _configuration["expiresIn"] = resp.ExpiresIn.ToString();
            _configuration["twitchRefreshToken"] = resp.RefreshToken;

            await _settingsFileManager.AddOrUpdateAppSetting("twitchAccessToken", resp.AccessToken);
            await _settingsFileManager.AddOrUpdateAppSetting("twitchRefreshToken", resp.RefreshToken);
            await _settingsFileManager.AddOrUpdateAppSetting("expiresIn", resp.ExpiresIn.ToString());

            return Redirect("/botauth");
        }

        [HttpGet("/botsignin")]
        [Authorize(Roles = "Streamer")]
        public IActionResult BotSignin()
        {
            _logger.LogInformation("{ipAddress} accessed /botsignin.", HttpContext.Connection?.RemoteIpAddress);
#if DEBUG
            var url = GetBotScopeUrl("https://localhost:7293/botredirect", _configuration["twitchBotClientId"]);
#else
            var url = GetBotScopeUrl("https://bot.superpenguin.tv/botredirect", _configuration["twitchBotClientId"]);
#endif
            return Redirect(url);
        }
        [HttpGet("botredirect")]
        public async Task<IActionResult> BotRedirect([FromQuery(Name = "code")] string code, [FromQuery(Name = "state")] string state)
        {
            _logger.LogInformation("{ipAddress} accessed /botredirect.", HttpContext.Connection?.RemoteIpAddress);
            if (_stateCache.TryGetValue(state, out var val))
            {
                _stateCache.Remove(state);
            }
            else
            {
                return Redirect("/");
            }

            var api = new TwitchLib.Api.TwitchAPI();
            api.Settings.ClientId = _configuration["twitchBotClientId"];
#if DEBUG
            var resp = await api.Auth.GetAccessTokenFromCodeAsync(code, _configuration["twitchBotClientSecret"], "https://localhost:7293/botredirect");
#else
            var resp = await api.Auth.GetAccessTokenFromCodeAsync(code, _configuration["twitchBotClientSecret"], "https://bot.superpenguin.tv/botredirect");
#endif

            if (resp == null) { return Redirect("/"); }

            _configuration["twitchBotAccessToken"] = resp.AccessToken;
            _configuration["botExpiresIn"] = resp.ExpiresIn.ToString();
            _configuration["twitchBotRefreshToken"] = resp.RefreshToken;

            await _settingsFileManager.AddOrUpdateAppSetting("twitchBotAccessToken", resp.AccessToken);
            await _settingsFileManager.AddOrUpdateAppSetting("twitchBotRefreshToken", resp.RefreshToken);
            await _settingsFileManager.AddOrUpdateAppSetting("botExpiresIn", resp.ExpiresIn.ToString());

            return Redirect("/botauth");
        }

        [HttpGet("/signin")]
        public IActionResult Signin([FromQuery(Name = "r")] string? redirect)
        {
            _logger.LogInformation("{ipAddress} accessed /signin.", HttpContext.Connection?.RemoteIpAddress);
#if DEBUG
            var url = GetAuthorizationCodeUrl("https://localhost:7293/redirect", redirect);
#else
            var url = GetAuthorizationCodeUrl("https://bot.superpenguin.tv/redirect", redirect);
#endif
            return Redirect(url);
        }



        [HttpGet("/redirect")]
        public async Task<IActionResult> RedirectFromTwitch([FromQuery(Name = "code")] string code, [FromQuery(Name = "state")] string state)
        {
            _logger.LogInformation("{ipAddress} accessed /redirect.", HttpContext.Connection?.RemoteIpAddress);
            if (_stateCache.TryGetValue(state, out string? redirect))
            {
                _stateCache.Remove(state);
            }
            else
            {
                return Redirect("/");
            }
            var api = new TwitchLib.Api.TwitchAPI();
            api.Settings.ClientId = _configuration["twitchClientId"];
#if DEBUG
            var resp = await api.Auth.GetAccessTokenFromCodeAsync(code, _configuration["twitchClientSecret"], "https://localhost:7293/redirect");
#else
            var resp = await api.Auth.GetAccessTokenFromCodeAsync(code, _configuration["twitchClientSecret"], "https://bot.superpenguin.tv/redirect");
#endif
            var broadcaster = _configuration["broadcaster"];
            if (broadcaster == null)
            {
                _logger.LogError("Broadcaster is not set.");
                return Redirect("/");
            }

            var botName = _configuration["botName"];
            if (botName == null)
            {
                _logger.LogError("Botname is not set.");
                return Redirect("/");
            }

            api.Settings.AccessToken = resp.AccessToken;
            var users = await api.Helix.Users.GetUsersAsync();
            if (users.Users.Length > 0)
            {
                var user = users.Users[0];
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.Name, user.Login),
                    new Claim(ClaimTypes.Role, broadcaster.Equals(user.Login) || botName.Equals(user.Login) ? "Streamer": "Viewer"),
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
                _logger.LogInformation("{login} logged in to web interface", user.Login);
            }
            if (string.IsNullOrEmpty(redirect))
            {
                return Redirect("/");
            }
            return Redirect(redirect);
        }

        [HttpGet("/signout")]
        public async Task<IActionResult> Signout()
        {
            _logger.LogInformation("{ipAddress} accessed /signout.", HttpContext.Connection?.RemoteIpAddress);
            // Clear the existing external cookie
            await HttpContext.SignOutAsync(
                CookieAuthenticationDefaults.AuthenticationScheme);
            return Redirect("/");
        }

        private string GetAuthorizationCodeUrl(string redirectUri, string? redirect)
        {
            var stateString = Guid.NewGuid().ToString();
            _stateCache.Set(stateString, redirect, DateTimeOffset.Now.AddMinutes(60));
            return "https://id.twitch.tv/oauth2/authorize?" +
                   $"client_id={_configuration["twitchClientId"]}&" +
                   $"redirect_uri={System.Web.HttpUtility.UrlEncode(redirectUri)}&" +
                   $"state={stateString}&" +
                   "response_type=code&" +
                   $"scope=";
        }

        private string GetBotScopeUrl(string redirectUri, string? clientId)
        {
            var scopes = new List<string>()
            {
                "analytics:read:extensions",
                "user:edit",
                "user:read:email",
                "clips:edit",
                "bits:read",
                "analytics:read:games",
                "user:edit:broadcast",
                "user:read:broadcast",
                "chat:read",
                "chat:edit",
                "channel:moderate",
                "channel:read:subscriptions",
                "whispers:read",
                "whispers:edit",
                "moderation:read",
                "channel:read:redemptions",
                "channel:edit:commercial",
                "channel:read:hype_train",
                "channel:read:stream_key",
                "channel:manage:extensions",
                "channel:manage:broadcast",
                "user:edit:follows",
                "channel:manage:redemptions",
                "channel:read:editors",
                "channel:manage:videos",
                "user:read:blocked_users",
                "user:manage:blocked_users",
                "user:read:subscriptions",
                "user:read:follows",
                "channel:manage:polls",
                "channel:manage:predictions",
                "channel:read:polls",
                "channel:read:predictions",
                "moderator:manage:automod",
                "channel:manage:schedule",
                "channel:read:goals",
                "moderator:read:automod_settings",
                "moderator:manage:automod_settings",
                "moderator:manage:banned_users",
                "moderator:read:blocked_terms",
                "moderator:manage:blocked_terms",
                "moderator:read:chat_settings",
                "moderator:manage:chat_settings",
                "channel:manage:raids",
                "moderator:manage:announcements",
                "moderator:manage:chat_messages",
                "user:manage:chat_color",
                "channel:manage:moderators",
                "channel:read:vips",
                "channel:manage:vips",
                "user:manage:whispers",
                "channel:read:charity",
                "moderator:read:chatters",
                "moderator:read:shield_mode",
                "moderator:manage:shield_mode",
                "moderator:read:shoutouts",
                "moderator:manage:shoutouts",
                "moderator:read:followers",
                "channel:read:guest_star",
                "channel:manage:guest_star",
                "moderator:read:guest_star",
                "moderator:manage:guest_star",
                "channel:bot",
                "user:bot",
                "user:read:chat",
                "channel:manage:ads",
                "channel:read:ads",
                "user:write:chat"
            };
            var scopeStr = String.Join("+", scopes);
            var stateString = Guid.NewGuid().ToString();
            _stateCache.Set(stateString, stateString, DateTimeOffset.Now.AddMinutes(60));
            return "https://id.twitch.tv/oauth2/authorize?" +
                   $"client_id={clientId}&" +
                   $"redirect_uri={System.Web.HttpUtility.UrlEncode(redirectUri)}&" +
                   $"state={stateString}&" +
                   "response_type=code&" +
                   $"scope={scopeStr}&" + "force_verify=true";
        }
    }
}