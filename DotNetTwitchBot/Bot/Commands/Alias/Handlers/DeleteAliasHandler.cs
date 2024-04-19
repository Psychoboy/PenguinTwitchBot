using DotNetTwitchBot.Bot.Commands.Alias.Requests;
using DotNetTwitchBot.Repository;
using MediatR;

namespace DotNetTwitchBot.Bot.Commands.Alias.Handlers
{
    public class DeleteAliasHandler(IServiceScopeFactory scopeFactory) : IRequestHandler<DeleteAlias>
    {
        public async Task Handle(DeleteAlias request, CancellationToken cancellationToken)
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            db.Aliases.Remove(request.Alias);
            await db.SaveChangesAsync();
        }
    }
}
