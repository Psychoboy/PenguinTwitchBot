using PenguinTwitchBot.Bot.Commands.Alias.Requests;
using PenguinTwitchBot.Database.Repository;

namespace PenguinTwitchBot.Bot.Commands.Alias.Handlers
{
    public class GetAliasesHandler(IServiceScopeFactory scopeFactory) : Application.Notifications.IRequestHandler<GetAliases, IEnumerable<AliasModel>>
    {

        public async Task<IEnumerable<AliasModel>> Handle(GetAliases request, CancellationToken cancellationToken)
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            return await db.Aliases.GetAllAsync();
        }
    }
}
