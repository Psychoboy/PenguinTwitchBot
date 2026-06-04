namespace PenguinTwitchBot.Bot.Core.Database
{
    public class DatabaseTools(Commands.Moderation.Admin admin) : IDatabaseTools
    {
        public async Task Backup()
        {
            await admin.BackupDatabase();
        }
    }
}