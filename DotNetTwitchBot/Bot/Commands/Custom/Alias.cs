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
        private readonly DataAccess.IAlias _aliasDb;

        public Alias(
            DataAccess.IAlias alias,
            IServiceBackbone serviceBackbone,
            ICommandHandler commandHandler) : base(serviceBackbone, commandHandler)
        {
            _aliasDb = alias;
        }

        public Task<List<AliasModel>> GetAliasesAsync()
        {
            return _aliasDb.GetAliasesAsync();
        }

        public Task<AliasModel?> GetAliasAsync(int id)
        {
            return _aliasDb.GetAliasAsync(id);
        }

        public Task CreateOrUpdateAliasAsync(AliasModel alias)
        {
            return _aliasDb.CreateOrUpdateAliasAsync(alias);
        }

        public Task DeleteAliasAsync(AliasModel alias)
        {
            return _aliasDb.DeleteAliasAsync(alias);
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

        private async Task<bool> IsAlias(CommandEventArgs e)
        {
            if (e.FromAlias) return false; //Prevents endless recursion
            var alias = await _aliasDb.GetAliasAsync(e.Command);
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