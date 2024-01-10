namespace DotNetTwitchBot.Bot.Commands
{
    public interface ICommandHandler
    {
        void AddCommand(BaseCommandProperties commandProperties, IBaseCommandService commandService);
        void AddCoolDown(string user, string command, DateTime cooldown);
        void AddCoolDown(string user, string command, int cooldown);
        Task<DefaultCommand> AddDefaultCommand(DefaultCommand defaultCommand);
        void AddGlobalCooldown(string command, DateTime cooldown);
        void AddGlobalCooldown(string command, int cooldown);
        bool CommandExists(string command);
        Command? GetCommand(string commandName);
        string GetCommandDefaultName(string commandName);
        Task<DefaultCommand?> GetDefaultCommandById(int id);
        Task<DefaultCommand?> GetDefaultCommandFromDb(string defaultCommandName);
        Task<List<DefaultCommand>> GetDefaultCommandsFromDb();
        bool IsCoolDownExpired(string user, string command);
        Task<bool> IsCoolDownExpiredWithMessage(string user, string displayName, BaseCommandProperties command);
        Task<bool> IsCoolDownExpiredWithMessage(string user, string displayName, string command);
        void RemoveCommand(string commandName);
        void UpdateCommandName(string oldCommandName, string newCommandName);
        Task UpdateDefaultCommand(DefaultCommand defaultCommand);
        Task<IEnumerable<ExternalCommands>> GetExternalCommands();
        Task<ExternalCommands?> GetExternalCommand(int id);
        Task AddOrUpdateExternalCommand(ExternalCommands externalCommand);
        Task DeleteExternalCommand(ExternalCommands externalCommand);
    }
}