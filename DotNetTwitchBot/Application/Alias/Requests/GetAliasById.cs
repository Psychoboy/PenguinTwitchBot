using MediatR;

namespace DotNetTwitchBot.Application.Alias.Requests
{
    public class GetAliasById(int id) : IRequest<AliasModel?>
    {
        public int Id { get; } = id;
    }
}
