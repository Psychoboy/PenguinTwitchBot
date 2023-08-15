using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DotNetTwitchBot.Bot.DataAccess
{
    public interface IAlias
    {
        Task<List<AliasModel>> GetAliasesAsync();
        Task<AliasModel?> GetAliasAsync(int id);
        Task<AliasModel?> GetAliasAsync(string command);
        Task CreateOrUpdateAliasAsync(AliasModel alias);
        Task DeleteAliasAsync(AliasModel alias);
    }
}