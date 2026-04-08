using DotNetTwitchBot.Bot.Actions.SubActions.Types;
using DotNetTwitchBot.Bot.Queues;
using System.Collections.Concurrent;

namespace DotNetTwitchBot.Bot.Actions.SubActions.Handlers
{
    public class ExternalApiHandler(
        ISubActionExecutionContextAccessor contextAccessor,
        IHttpClientFactory httpClientFactory) : ISubActionHandler
    {
        public SubActionTypes SupportedType => SubActionTypes.ExternalApi;

        public async Task ExecuteAsync(SubActionType subAction, ConcurrentDictionary<string, string> variables)
        {
            if (subAction is not ExternalApiType externalApiType)
            {
                throw new SubActionHandlerException(subAction, "Invalid sub action type for ExternalApiHandler: {SubActionType}", subAction.GetType().Name);
            }

            var context = contextAccessor.ExecutionContext;

            try
            {
                using var httpClient = httpClientFactory.CreateClient();
                using var httpRequest = new HttpRequestMessage()
                {
                    RequestUri = new Uri(externalApiType.Text),
                    Method = new HttpMethod(externalApiType.HttpMethod)
                };

                context?.LogMessage(contextAccessor.CurrentSubActionIndex, $"Sending {externalApiType.HttpMethod} request to {externalApiType.Text}");

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

                using var result = await httpClient.SendAsync(httpRequest);
                var responseContent = await result.Content.ReadAsStringAsync();

                context?.LogMessage(contextAccessor.CurrentSubActionIndex, $"Received response with status {(int)result.StatusCode}");

                variables["ExternalApiResponse"] = responseContent;

            } catch (Exception ex)
            {
                context?.LogMessage(contextAccessor.CurrentSubActionIndex, $"Request failed: {ex.Message}");
                throw new SubActionHandlerException(subAction, ex, "Error executing ExternalApiHandler for URL: {Url}", externalApiType.Text);
            }
        }
    }
}
