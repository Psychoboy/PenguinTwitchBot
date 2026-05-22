using PenguinTwitchBot.Bot.Events.Chat;

namespace PenguinTwitchBot.Bot.Commands.Alias
{
    public interface IAlias
    {
        Task<bool> CommandExists(string alias);
        Task CreateOrUpdateAliasAsync(AliasModel alias);
        Task DeleteAliasAsync(AliasModel alias);
        Task<AliasModel?> GetAliasAsync(int id);
        Task<List<AliasModel>> GetAliasesAsync();
        Task<bool> OnCommand(object? sender, CommandEventArgs e);
        Task Register();
        Task<bool> RunCommand(CommandEventArgs e);
    }
}