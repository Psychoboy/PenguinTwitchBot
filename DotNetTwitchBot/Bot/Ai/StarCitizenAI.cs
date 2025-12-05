using OpenAI;
using OpenAI.Responses;

namespace DotNetTwitchBot.Bot.Ai
{
    public class StarCitizenAI(OpenAIClient openAIClient) : IStarCitizenAI
    {
        private readonly OpenAIClient client = openAIClient;
#pragma warning disable OPENAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

        public async Task<string> GetResponseFromPrompt(string prompt)
        {
            if(string.IsNullOrWhiteSpace(prompt))
            {
                return "Prompt cannot be empty.";
            }

            var respClient = client.GetOpenAIResponseClient("gpt-5.1");
            var responseItems = new List<ResponseItem>
            {
                ResponseItem.CreateDeveloperMessageItem("Avoid all markdown formatting, including asterisks, hashtags, lists, and backticks."),
                ResponseItem.CreateUserMessageItem("You are a helpful assistant that provides information about the game Star Citizen in plain text"),
                ResponseItem.CreateUserMessageItem("Make as short as possible."),
                ResponseItem.CreateUserMessageItem("No Markdown"),
                ResponseItem.CreateAssistantMessageItem("Understood. I will keep the responses short and free of markdown formatting."),
                ResponseItem.CreateUserMessageItem(prompt)
            };

            var webSearchTool = ResponseTool.CreateWebSearchTool(null, null, new WebSearchToolFilters
            {
                AllowedDomains = [
                    "starcitizen.tools", 
                    "finder.cstone.space", 
                    "sc-trade.tools", 
                    "robertsspaceindustries.com", 
                    "status.robertsspaceindustries.com"
                    ]
            });

            var response = await respClient.CreateResponseAsync(responseItems, new ResponseCreationOptions
            {
                Tools = { webSearchTool },
                MaxOutputTokenCount = 200,
                ServiceTier = "flex",
            });


            foreach (var output in response.Value.OutputItems.Where(x => x is MessageResponseItem))
            {
                if (output is MessageResponseItem messageItem &&
                    messageItem.Content != null && messageItem.Content.Count > 0)
                {
                    return messageItem.Content.First().Text;
                }
            }
            return "";
        }
#pragma warning restore OPENAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
    }
}
