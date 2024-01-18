using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Events.Chat;
using DotNetTwitchBot.Repository;

namespace DotNetTwitchBot.Bot.Commands.Custom
{
    public class Alias(
        IServiceScopeFactory scopeFactory,
        IServiceBackbone serviceBackbone,
        ICommandHandler commandHandler) : BaseCommandService(serviceBackbone, commandHandler), IAlias
    {
        public async Task<List<AliasModel>> GetAliasesAsync()
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            return (await db.Aliases.GetAllAsync()).ToList();
        }

        public async Task<AliasModel?> GetAliasAsync(int id)
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            return await db.Aliases.Find(x => x.Id == id).FirstOrDefaultAsync();
        }

        public async Task CreateOrUpdateAliasAsync(AliasModel alias)
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            if (alias.Id == null)
            {
                await db.Aliases.AddAsync(alias);
            }
            else
            {
                db.Aliases.Update(alias);
            }
            await db.SaveChangesAsync();
        }

        public async Task DeleteAliasAsync(AliasModel alias)
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            db.Aliases.Remove(alias);
            await db.SaveChangesAsync();
        }

        public async Task<bool> RunCommand(CommandEventArgs e)
        {
            if (await IsAlias(e))
            {
                e.FromAlias = true;
                await ServiceBackbone.RunCommand(e);
                return true;
            }
            return false;
        }

        public override Task<bool> OnCommand(object? sender, CommandEventArgs e)
        {
            return Task.FromResult(true);
        }

        public async Task<bool> CommandExists(string alias)
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            return await db.Aliases.Find(x => x.AliasName == alias).AnyAsync();
        }

        private async Task<bool> IsAlias(CommandEventArgs e)
        {
            if (e.FromAlias) return false; //Prevents endless recursion
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var alias = await db.Aliases.Find(x => x.AliasName.Equals(e.Command)).FirstOrDefaultAsync();
            if (alias != null)
            {
                e.Command = alias.CommandName;
                return true;
            }
            return false;
        }

        public override Task Register()
        {
            return Task.CompletedTask;
        }
    }
}