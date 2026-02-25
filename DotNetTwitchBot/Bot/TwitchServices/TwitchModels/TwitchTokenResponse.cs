using System.Text.Json.Serialization;

namespace DotNetTwitchBot.Bot.TwitchServices.TwitchModels
{
    public class TwitchTokenResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; } = string.Empty;
        [JsonPropertyName("token_type")]
        public string TokenType { get; set; } = string.Empty;
        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }
    }
}
