using DotNetTwitchBot.Repository;
using OpenAI;
using OpenAI.Responses;
using System.Text.RegularExpressions;

namespace DotNetTwitchBot.Bot.Ai
{
    public partial class StarCitizenAI(OpenAIClient openAIClient, IUnitOfWork unitOfWork) : IStarCitizenAI
    {
        private readonly OpenAIClient client = openAIClient;
        [GeneratedRegex(@"\(\s*\[[^\]]+\]\([^)]+\)\s*\)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase)]
        private static partial Regex LinkPatternRegex();
#pragma warning disable OPENAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
        public async Task<string> GetResponseFromPrompt(string userId, string prompt)
        {
            if (string.IsNullOrWhiteSpace(prompt))
            {
                return "Prompt cannot be empty.";
            }

            var respClient = client.GetResponsesClient("gpt-5.1");

            var webSearchTool = ResponseTool.CreateWebSearchTool(null, null, new WebSearchToolFilters
            {
                AllowedDomains = [
                    "starcitizen.tools",
                    "finder.cstone.space",
                    "uexcorp.space",
                    "robertsspaceindustries.com",
                    "status.robertsspaceindustries.com"
                    ]
            });

            var previousResponseId = await GetPreviousResponseCode(userId);

            var responseOptions = new CreateResponseOptions
            {
                Tools = { webSearchTool },
                MaxOutputTokenCount = 200,
                ServiceTier = "flex",
                Instructions = "You are a helpful assistant that provides information about the game Star Citizen in plain text." +
                "Use the provided tools to gather accurate information about Star Citizen when necessary. " +
                "Avoid all markdown formatting, including asterisks, hashtags, lists, and backticks." +
                "Make as short as possible." +
                "Do not provide any markdown." +
                "Do not use asterisks, hashtags, or backticks." +
                "Do not provide any links unless specifically asked for.",
                PreviousResponseId = previousResponseId

            };
            responseOptions.InputItems.Add(ResponseItem.CreateUserMessageItem(prompt));
            var response = await respClient.CreateResponseAsync(responseOptions);


            foreach (var output in response.Value.OutputItems.Where(x => x is MessageResponseItem))
            {
                if (output is MessageResponseItem messageItem &&
                    messageItem.Content != null && messageItem.Content.Count > 0)
                {
                    var result = messageItem.Content.First().Text;
                    result = LinkPatternRegex().Replace(result, "").Trim();
                    result = result.ReplaceLineEndings(" ");
                    await SavePreviousResponseCode(userId, response.Value.Id);
                    return result;
                }
            }

            return "";
        }

#pragma warning restore OPENAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
        private async Task<string?> GetPreviousResponseCode(string userId)
        {
            var previousResponse = await unitOfWork.ScAiResponses
                .Find(x => x.UserId == userId)
                .FirstOrDefaultAsync();
            return previousResponse?.PreviousResponseId ?? null;

        }

        private async Task SavePreviousResponseCode(string userId, string responseId)
        {
            var previousResponse = await unitOfWork.ScAiResponses
                .Find(x => x.UserId == userId)
                .FirstOrDefaultAsync();
            if (previousResponse == null)
            {
                previousResponse = new Models.ScAiResponseCodes
                {
                    UserId = userId,
                    PreviousResponseId = responseId
                };
                await unitOfWork.ScAiResponses.AddAsync(previousResponse);
            }
            else
            {
                previousResponse.PreviousResponseId = responseId;
                unitOfWork.ScAiResponses.Update(previousResponse);
            }
            await unitOfWork.SaveChangesAsync();
        }
    }
    
}
