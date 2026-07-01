using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace PenguinTwitchBot.Database.Bot.DatabaseTools
{
    public interface IBackupTools
    {
        string BackupDirectory { get; }
        List<FileInfo> GetBackupFiles(string backupDirectory);
        Task BackupTable<T>(DbContext context, string backupDirectory, ILogger? logger = null) where T : class;
        Task WriteData<T>(string backupDirectory, List<T> records, ILogger? logger = null);
        Task RestoreTable<T>(DbContext context, string backupDirectory, ILogger? logger = null) where T : class;
        Task BackupDatabase(DbContext context, string backupDirectory, ILogger logger);
        Task RestoreDatabase(DbContext context, string backupDirectory, ILogger? logger = null);
    }
}
