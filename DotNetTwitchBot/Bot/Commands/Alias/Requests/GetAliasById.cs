
namespace DotNetTwitchBot.Bot.Commands.Alias.Requests
{
    public class GetAliasById(int id) : Application.Notifications.IRequest<AliasModel?>
    {
        public int Id { get; } = id;
    }
}
