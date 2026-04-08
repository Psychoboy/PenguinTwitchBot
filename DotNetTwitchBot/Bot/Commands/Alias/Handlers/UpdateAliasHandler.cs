using DotNetTwitchBot.Bot.Commands.Alias.Requests;
using DotNetTwitchBot.Repository;

namespace DotNetTwitchBot.Bot.Commands.Alias.Handlers
{
    public class UpdateAliasHandler(IServiceScopeFactory scopeFactory) : Application.Notifications.IRequestHandler<UpdateAlias, Application.Notifications.Unit>
    {
        public async Task<Application.Notifications.Unit> Handle(UpdateAlias request, CancellationToken cancellationToken)
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            db.Aliases.Update(request.Alias);
            await db.SaveChangesAsync();
            return Application.Notifications.Unit.Value;
        }
    }
}
