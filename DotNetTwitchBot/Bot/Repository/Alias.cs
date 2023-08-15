using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DotNetTwitchBot.Bot.DataAccess
{
    public class Alias : IAlias
    {
        private readonly IServiceScopeFactory _scopeFactory;

        public Alias(IServiceScopeFactory scopeFactory)
        {
            _scopeFactory = scopeFactory;
        }
        public async Task CreateOrUpdateAliasAsync(AliasModel alias)
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
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

        public async Task DeleteAliasAsync(AliasModel alias)
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Aliases.Remove(alias);
            await db.SaveChangesAsync();
        }

        public async Task<AliasModel?> GetAliasAsync(int id)
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            return await db.Aliases.Where(x => x.Id == id).FirstOrDefaultAsync();
        }

        public async Task<AliasModel?> GetAliasAsync(string command)
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            return await db.Aliases.Where(x => x.AliasName.Equals(command)).FirstOrDefaultAsync();
        }

        public async Task<List<AliasModel>> GetAliasesAsync()
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            return await db.Aliases.ToListAsync();
        }
    }
}