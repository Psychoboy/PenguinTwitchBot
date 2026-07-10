using PenguinTwitchBot.Database.Bot.Models;

namespace PenguinTwitchBot.Database.Repository
{
    public interface IGlobalVariablesRepository : IGenericRepository<GlobalVariable>
    {
        Task<List<GlobalVariable>> GetAllOrderedAsync();
        Task<GlobalVariable?> GetByNameAsync(string name);
        Task<GlobalVariable> UpsertAsync(string name, string value);
        Task<bool> DeleteByNameAsync(string name);
    }
}