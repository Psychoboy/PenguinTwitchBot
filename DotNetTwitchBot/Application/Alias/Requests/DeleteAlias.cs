using MediatR;

namespace DotNetTwitchBot.Application.Alias.Requests
{
    public class DeleteAlias(AliasModel alias) : IRequest
    {
        public AliasModel Alias { get; } = alias;
    }
}
