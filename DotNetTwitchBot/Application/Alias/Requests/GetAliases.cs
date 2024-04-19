using MediatR;

namespace DotNetTwitchBot.Application.Alias.Requests
{
    public class GetAliases : IRequest<IEnumerable<AliasModel>>
    {
    }
}
