using MediatR;

namespace DotNetTwitchBot.Bot.Commands.Alias.Requests
{
    public class UpdateAlias(AliasModel alias) : IRequest
    {
        public AliasModel Alias { get; } = alias;
    }
}
