using DotNetTwitchBot.Bot.Events.Chat;

namespace DotNetTwitchBot.Bot.Commands.Custom
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