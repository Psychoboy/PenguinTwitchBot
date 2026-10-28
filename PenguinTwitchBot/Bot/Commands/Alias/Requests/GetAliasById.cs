
namespace PenguinTwitchBot.Bot.Commands.Alias.Requests
{
    public class GetAliasById(Guid id) : Application.Notifications.IRequest<AliasModel?>
    {
        public Guid Id { get; } = id;
    }
}
