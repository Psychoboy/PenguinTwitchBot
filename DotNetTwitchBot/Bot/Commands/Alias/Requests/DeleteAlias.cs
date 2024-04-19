using MediatR;

namespace DotNetTwitchBot.Bot.Commands.Alias.Requests
{
    public class DeleteAlias(AliasModel alias) : IRequest
    {
        public AliasModel Alias { get; } = alias;
    }
}
