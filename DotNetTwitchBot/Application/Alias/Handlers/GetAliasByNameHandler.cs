using DotNetTwitchBot.Application.Alias.Requests;
using DotNetTwitchBot.Repository;
using MediatR;

namespace DotNetTwitchBot.Application.Alias.Handlers
{
    public class GetAliasByNameHandler(IServiceScopeFactory scopeFactory) : IRequestHandler<GetAliasByName, AliasModel?>
    {
        public async Task<AliasModel?> Handle(GetAliasByName request, CancellationToken cancellationToken)
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            return await db.Aliases.Find(x => x.AliasName.Equals(request.Name)).FirstOrDefaultAsync(cancellationToken);
        }
    }
}
