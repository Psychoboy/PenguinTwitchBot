using Microsoft.EntityFrameworkCore;

namespace PenguinTwitchBot.Repository
{
    public interface IBackupDb
    {
         Task BackupTable(DbContext context, string backupDirectory, ILogger? logger = null);
        Task RestoreTable(DbContext context, string backupDirectory, ILogger? logger = null);
    }
}
