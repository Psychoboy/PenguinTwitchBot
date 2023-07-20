using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Events.Chat;

namespace DotNetTwitchBot.Bot.Commands.Custom
{
    public class Alias : BaseCommandService
    {
        private IServiceScopeFactory _scopeFactory;

        public Alias(IServiceScopeFactory scopeFactory, ServiceBackbone serviceBackbone, CommandHandler commandHandler) : base(serviceBackbone, scopeFactory, commandHandler)
        {
            _scopeFactory = scopeFactory;
        }

        public async Task<List<AliasModel>> GetAliasesAsync()
        {
            await using (var scope = _scopeFactory.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                return await db.Aliases.ToListAsync();
            }
        }

        public async Task<AliasModel?> GetAliasAsync(int id)
        {
            await using (var scope = _scopeFactory.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                return await db.Aliases.Where(x => x.Id == id).FirstOrDefaultAsync();
            }
        }

        public async Task CreateOrUpdateAliasAsync(AliasModel alias)
        {
            await using (var scope = _scopeFactory.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
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
        }

        public async Task DeleteAliasAsync(AliasModel alias)
        {
            await using (var scope = _scopeFactory.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                db.Aliases.Remove(alias);
                await db.SaveChangesAsync();
            }
        }

        public override async Task OnCommand(object? sender, CommandEventArgs e)
        {
            if (await IsAlias(e))
            {
                e.FromAlias = true;
                await _serviceBackbone.RunCommand(e);
            }
        }

        private async Task<bool> IsAlias(CommandEventArgs e)
        {
            if (e.FromAlias) return false; //Prevents endless recursion
            await using (var scope = _scopeFactory.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var alias = await db.Aliases.Where(x => x.AliasName.Equals(e.Command)).FirstOrDefaultAsync();
                if (alias != null)
                {
                    e.Command = alias.CommandName;
                    return true;
                }
                return false;
            }
        }

        public override void RegisterDefaultCommands()
        {
            //Do nothing here
        }
    }
}