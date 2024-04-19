using DotNetTwitchBot.Bot.Commands.Alias.Requests;
using DotNetTwitchBot.Repository;
using MediatR;

namespace DotNetTwitchBot.Bot.Commands.Alias.Handlers
{
    public class CreateAliasHandler(IServiceScopeFactory scopeFactory) : IRequestHandler<CreateAlias>
    {
        public async Task Handle(CreateAlias request, CancellationToken cancellationToken)
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            await db.Aliases.AddAsync(request.Alias);
            await db.SaveChangesAsync();
        }
    }
}
