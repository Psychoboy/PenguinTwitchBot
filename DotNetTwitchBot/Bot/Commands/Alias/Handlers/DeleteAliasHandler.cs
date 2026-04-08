using DotNetTwitchBot.Bot.Commands.Alias.Requests;
using DotNetTwitchBot.Repository;

namespace DotNetTwitchBot.Bot.Commands.Alias.Handlers
{
    public class DeleteAliasHandler(IServiceScopeFactory scopeFactory) : Application.Notifications.IRequestHandler<DeleteAlias, Application.Notifications.Unit>
    {
        public async Task<Application.Notifications.Unit> Handle(DeleteAlias request, CancellationToken cancellationToken)
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            db.Aliases.Remove(request.Alias);
            await db.SaveChangesAsync();
            return Application.Notifications.Unit.Value;
        }
    }
}
