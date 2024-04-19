using DotNetTwitchBot.Bot.Commands.Alias.Requests;
using DotNetTwitchBot.Repository;
using MediatR;

namespace DotNetTwitchBot.Bot.Commands.Alias.Handlers
{
    public class GetAliasesHandler(IServiceScopeFactory scopeFactory) : IRequestHandler<GetAliases, IEnumerable<AliasModel>>
    {

        public async Task<IEnumerable<AliasModel>> Handle(GetAliases request, CancellationToken cancellationToken)
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            return await db.Aliases.GetAllAsync();
        }
    }
}
