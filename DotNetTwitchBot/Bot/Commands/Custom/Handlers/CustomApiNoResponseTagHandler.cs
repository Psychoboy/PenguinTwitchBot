using DotNetTwitchBot.Bot.Commands.Custom.Tags;
using MediatR;

namespace DotNetTwitchBot.Bot.Commands.Custom.Handlers
{
    public class CustomApiNoResponseTagHandler : IRequestHandler<CustomApiNoResponseTag, CustomCommandResult>
    {
        public async Task<CustomCommandResult> Handle(CustomApiNoResponseTag request, CancellationToken cancellationToken)
        {
            var httpClient = new HttpClient();
            var httpRequest = new HttpRequestMessage()
            {
                RequestUri = new Uri(request.Args),
                Method = HttpMethod.Get
            };
            httpRequest.Headers.Add("Accept", "text/plain");
            _ = await httpClient.SendAsync(httpRequest);
            return new CustomCommandResult();
        }
    }
}
