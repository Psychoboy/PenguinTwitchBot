using DotNetTwitchBot.Bot.Commands.Alias.Requests;
using DotNetTwitchBot.Repository;
using MediatR;

namespace DotNetTwitchBot.Bot.Commands.Alias.Handlers
{
    public class GetAliasByIdHandler(IServiceScopeFactory scopeFactory) : IRequestHandler<GetAliasById, AliasModel?>
    {
        public async Task<AliasModel?> Handle(GetAliasById request, CancellationToken cancellationToken)
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            return await db.Aliases.GetByIdAsync(request.Id);
        }
    }
}
