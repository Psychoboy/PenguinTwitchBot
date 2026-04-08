using DotNetTwitchBot.Bot.Commands.Alias.Requests;
using DotNetTwitchBot.Repository;

namespace DotNetTwitchBot.Bot.Commands.Alias.Handlers
{
    public class CreateAliasHandler(IServiceScopeFactory scopeFactory) : Application.Notifications.IRequestHandler<CreateAlias, Application.Notifications.Unit>
    {
        public async Task<Application.Notifications.Unit> Handle(CreateAlias request, CancellationToken cancellationToken)
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            await db.Aliases.AddAsync(request.Alias);
            await db.SaveChangesAsync();
            return Application.Notifications.Unit.Value;
        }
    }
}
