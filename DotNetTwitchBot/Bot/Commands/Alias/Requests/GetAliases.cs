using MediatR;

namespace DotNetTwitchBot.Bot.Commands.Alias.Requests
{
    public class GetAliases : IRequest<IEnumerable<AliasModel>>
    {
    }
}
