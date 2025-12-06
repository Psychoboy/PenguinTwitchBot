using DotNetTwitchBot.Bot.TwitchServices;
using OpenAI;
using OpenAI.Responses;
using System.Text.RegularExpressions;

namespace DotNetTwitchBot.Bot.Ai
{
    public partial class ShoutoutAi(
        OpenAIClient openAIClient,
        ILogger<ShoutoutAi> logger
        ) : IShoutoutAi
    {
        [GeneratedRegex(@"\(\s*\[[^\]]+\]\([^)]+\)\s*\)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase)]
        private static partial Regex LinkPatternRegex();
#pragma warning disable OPENAI001
        public async Task<string> GetShoutoutForStreamer(string streamerName, string gameName, string streamTitle, string bio, string additionalInfo = "")
        {
            if (string.IsNullOrWhiteSpace(streamerName))
            {
                logger.LogWarning("Streamer name is empty.");
                return "";
            }

            string prompt = $"Create a short and fun Twitch shoutout message for the streamer '{streamerName}'. " +
                $"They are currently playing '{gameName}' with the stream title '{streamTitle}'. " +
                $"Bio: {bio}. " +
                $"{additionalInfo} " +
                $"Keep it under 300 characters. No Links or Markdown. Do not use asterisks, hashtags, or backticks.";

            var respClient = openAIClient.GetOpenAIResponseClient("gpt-5.1");
            var responseItems = new List<OpenAI.Responses.ResponseItem>
            {
               ResponseItem.CreateDeveloperMessageItem("You are a helpful assistant that provides short and fun Twitch shoutout messages in plain text."),
               ResponseItem.CreateUserMessageItem("Keep it under 300 characters. No Links or Markdown."),
               ResponseItem.CreateUserMessageItem("Do not use asterisks, hashtags, or backticks."),
               ResponseItem.CreateUserMessageItem("Do not provide any markdown."),
               ResponseItem.CreateUserMessageItem(prompt)
            };

            var response = await respClient.CreateResponseAsync(responseItems);
            foreach (var output in response.Value.OutputItems.Where(x => x is MessageResponseItem))
            {
                if (output is MessageResponseItem messageItem &&
                    messageItem.Content != null && messageItem.Content.Count > 0)
                {
                    var result = messageItem.Content.First().Text;
                    result = LinkPatternRegex().Replace(result, "").Trim();
                    result = result.ReplaceLineEndings(" ");
                    return result;
                }
            }
            return "";
        }
#pragma warning restore OPENAI001
    }
}
