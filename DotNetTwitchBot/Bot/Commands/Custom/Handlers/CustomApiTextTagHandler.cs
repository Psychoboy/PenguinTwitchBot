using DotNetTwitchBot.Bot.Commands.Custom.Tags;
using DotNetTwitchBot.Bot.TwitchServices;
using MediatR;

namespace DotNetTwitchBot.Bot.Commands.Custom.Handlers
{
    public class CustomApiTextTagHandler() : IRequestHandler<CustomApiTextTag, CustomCommandResult>
    {
        public async Task<CustomCommandResult> Handle(CustomApiTextTag request, CancellationToken cancellationToken)
        {
            var httpClient = new HttpClient();
            var httpRequest = new HttpRequestMessage()
            {
                RequestUri = new Uri(request.Args),
                Method = HttpMethod.Get
            };
            httpRequest.Headers.Add("Accept", "text/plain");
            var result = await httpClient.SendAsync(httpRequest, cancellationToken);
            return new CustomCommandResult(await result.Content.ReadAsStringAsync(cancellationToken));
        }
    }
}
