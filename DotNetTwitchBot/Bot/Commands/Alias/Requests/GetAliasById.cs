using DotNetTwitchBot.Application.Notifications;

namespace DotNetTwitchBot.Bot.Commands.Alias.Requests
{
    public class GetAliasById(int id) : IRequest<AliasModel?>
    {
        public int Id { get; } = id;
    }
}
