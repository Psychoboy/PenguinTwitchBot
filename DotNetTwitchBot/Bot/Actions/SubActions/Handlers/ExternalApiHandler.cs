using DotNetTwitchBot.Bot.Actions.SubActions.Types;

namespace DotNetTwitchBot.Bot.Actions.SubActions.Handlers
{
    public class ExternalApiHandler(ILogger<ExternalApiHandler> logger) : ISubActionHandler
    {
        public SubActionTypes SupportedType => SubActionTypes.ExternalApi;

        public async Task ExecuteAsync(SubActionType subAction, Dictionary<string, string> variables)
        {
            if (subAction is not ExternalApiType externalApiType)
            {
                logger.LogError("Invalid sub action type for ExternalApiHandler: {SubActionType}", subAction.GetType().Name);
                return;
            }
            try
            {
                var httpClient = new HttpClient();
                var httpRequest = new HttpRequestMessage()
                {
                    RequestUri = new Uri(externalApiType.Text),
                    Method = new HttpMethod(externalApiType.HttpMethod)
                };

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
                variables.Add("ExternalApiResponse", await result.Content.ReadAsStringAsync());

            } catch (Exception ex)
            {
                logger.LogError(ex, "Error executing ExternalApiHandler for URL: {Url}", externalApiType.Text);
            }
        }
    }
}
