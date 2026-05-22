namespace PenguinTwitchBot.Repository
{
    public interface IRepositoryTools
    {
        Task BackupTable(string backupDirectory);
        Task RestoreTable(string backupDirectory);
    }
}
