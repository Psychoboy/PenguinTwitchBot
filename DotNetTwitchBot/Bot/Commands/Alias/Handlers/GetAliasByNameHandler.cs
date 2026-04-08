using DotNetTwitchBot.Bot.Commands.Alias.Requests;
using DotNetTwitchBot.Repository;

namespace DotNetTwitchBot.Bot.Commands.Alias.Handlers
{
    public class GetAliasByNameHandler(IServiceScopeFactory scopeFactory) : Application.Notifications.IRequestHandler<GetAliasByName, AliasModel?>
    {
        public async Task<AliasModel?> Handle(GetAliasByName request, CancellationToken cancellationToken)
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            return await db.Aliases.Find(x => x.AliasName.Equals(request.Name)).FirstOrDefaultAsync(cancellationToken);
        }
    }
}
