using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DotNetTwitchBot.Pages
{
    public class ViewerAuthModel : PageModel
    {
        private readonly IConfiguration _configuration;

        public ViewerAuthModel(
            IConfiguration configuration
            )
        {
            _configuration = configuration;
        }
        public void OnGet()
        {
            var url = getAuthorizationCodeUrl("https://localhost:7293/redirect", new());
            Response.Redirect(url);
        }
        public string getAuthorizationCodeUrl(string redirectUri, List<string> scopes)
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
