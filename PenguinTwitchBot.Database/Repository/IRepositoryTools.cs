using Microsoft.EntityFrameworkCore;

namespace PenguinTwitchBot.Database.Repository
{
    public interface IRepositoryTools
    {
        Task BackupTable(string backupDirectory);
        Task RestoreTable(string backupDirectory);
    }
}
