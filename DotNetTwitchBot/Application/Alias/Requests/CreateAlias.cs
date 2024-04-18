using MediatR;

namespace DotNetTwitchBot.Application.Alias.Requests
{
    public class CreateAlias(AliasModel alias) : IRequest
    {
        public AliasModel Alias { get; } = alias;
    }
}
