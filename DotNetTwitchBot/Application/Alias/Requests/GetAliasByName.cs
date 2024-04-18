using MediatR;

namespace DotNetTwitchBot.Application.Alias.Requests
{
    public class GetAliasByName(string name) : IRequest<AliasModel?>
    {
        public string Name { get; } = name;
    }
}
