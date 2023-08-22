using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DotNetTwitchBot.Pages
{
    public class TwitchRedirectModel : PageModel
    {
        private readonly IConfiguration _configuration;

        public TwitchRedirectModel(IConfiguration configuration)
        {
            _configuration = configuration;
        }
        public async Task OnGet()
        {
            var code = Request.HttpContext.Request.Query["code"];
            var api = new TwitchLib.Api.TwitchAPI();
            api.Settings.ClientId = _configuration["twitchClientId"];
            var resp = await api.Auth.GetAccessTokenFromCodeAsync(code, _configuration["twitchClientSecret"], "https://localhost:7293/redirect");
            api.Settings.AccessToken = resp.AccessToken;
            var user = await api.Helix.Users.GetUsersAsync();
        }
    }
}
