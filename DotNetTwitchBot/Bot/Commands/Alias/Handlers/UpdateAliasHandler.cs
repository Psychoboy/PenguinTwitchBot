using DotNetTwitchBot.Bot.Commands.Alias.Requests;
using DotNetTwitchBot.Repository;
using MediatR;

namespace DotNetTwitchBot.Bot.Commands.Alias.Handlers
{
    public class UpdateAliasHandler(IServiceScopeFactory scopeFactory) : IRequestHandler<UpdateAlias>
    {
        public async Task Handle(UpdateAlias request, CancellationToken cancellationToken)
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            db.Aliases.Update(request.Alias);
            await db.SaveChangesAsync();
        }
    }
}
