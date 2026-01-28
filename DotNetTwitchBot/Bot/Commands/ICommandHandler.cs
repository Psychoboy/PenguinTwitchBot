using DotNetTwitchBot.Bot.Events.Chat;
using DotNetTwitchBot.Bot.Models.Commands;

namespace DotNetTwitchBot.Bot.Commands
{
    public interface ICommandHandler
    {
        void AddCommand(BaseCommandProperties commandProperties, IBaseCommandService commandService);
        Task AddCoolDown(string user, string command, DateTime cooldown);
        Task AddCoolDown(string user, string command, int cooldown);
        Task<DefaultCommand> AddDefaultCommand(DefaultCommand defaultCommand);
        Task AddGlobalCooldown(string command, DateTime cooldown);
        Task AddGlobalCooldown(string command, int cooldown);
        bool CommandExists(string command);
        Command? GetCommand(string commandName);
        string GetCommandDefaultName(string commandName);
        Task<DefaultCommand?> GetDefaultCommandById(int id);
        Task<DefaultCommand?> GetDefaultCommandFromDb(string defaultCommandName);
        Task<List<DefaultCommand>> GetDefaultCommandsFromDb();
        Task<bool> IsCoolDownExpired(string user, PlatformType platform, string command);
        Task<bool> IsCoolDownExpiredWithMessage(string user, PlatformType platform, string displayName, BaseCommandProperties command);
        Task<bool> IsCoolDownExpiredWithMessage(string user, PlatformType platform, string displayName, string command);
        void RemoveCommand(string commandName);
        void UpdateCommandName(string oldCommandName, string newCommandName);
        Task UpdateDefaultCommand(DefaultCommand defaultCommand);
        Task<IEnumerable<ExternalCommands>> GetExternalCommands();
        Task<ExternalCommands?> GetExternalCommand(int id);
        Task AddOrUpdateExternalCommand(ExternalCommands externalCommand);
        Task DeleteExternalCommand(ExternalCommands externalCommand);
        Task<bool> CheckPermission(BaseCommandProperties commandProperties, CommandEventArgs eventArgs);
        Task ResetCooldown(CurrentCooldowns cooldown);
        Task<List<CurrentCooldowns>> GetCurrentCooldowns();
    }
}