using DotNetTwitchBot.Bot.Actions.SubActions.Types;
using DotNetTwitchBot.Bot.Queues;

namespace DotNetTwitchBot.Bot.Actions.SubActions.Handlers
{
    public class ExternalApiHandler(ISubActionExecutionContextAccessor contextAccessor) : ISubActionHandler
    {
        public SubActionTypes SupportedType => SubActionTypes.ExternalApi;

        public async Task ExecuteAsync(SubActionType subAction, Dictionary<string, string> variables)
        {
            if (subAction is not ExternalApiType externalApiType)
            {
                throw new SubActionHandlerException(subAction, "Invalid sub action type for ExternalApiHandler: {SubActionType}", subAction.GetType().Name);
            }

            var context = contextAccessor.ExecutionContext;

            try
            {
                var httpClient = new HttpClient();
                var httpRequest = new HttpRequestMessage()
                {
                    RequestUri = new Uri(externalApiType.Text),
                    Method = new HttpMethod(externalApiType.HttpMethod)
                };

                context?.LogMessage($"Sending {externalApiType.HttpMethod} request to {externalApiType.Text}");

                if (!string.IsNullOrEmpty(externalApiType.Headers))
                {
                    var headers = externalApiType.Headers.Split('\n');
                    foreach (var header in headers)
                    {
                        var headerParts = header.Split(':');
                        if (headerParts.Length == 2)
                        {
                            httpRequest.Headers.Add(headerParts[0].Trim(), headerParts[1].Trim());
                        }
                    }
                }
                var result = await httpClient.SendAsync(httpRequest);
                var responseContent = await result.Content.ReadAsStringAsync();

                context?.LogMessage($"Received response with status {(int)result.StatusCode}");

                variables.Add("ExternalApiResponse", responseContent);

            } catch (Exception ex)
            {
                context?.LogMessage($"Request failed: {ex.Message}");
                throw new SubActionHandlerException(subAction, ex, "Error executing ExternalApiHandler for URL: {Url}", externalApiType.Text);
            }
        }
    }
}
