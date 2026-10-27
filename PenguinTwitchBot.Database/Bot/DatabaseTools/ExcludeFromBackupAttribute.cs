namespace PenguinTwitchBot.Database.Bot.DatabaseTools
{
    /// <summary>
    /// Marks an EF Core entity class as excluded from database backups and restores.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class ExcludeFromBackupAttribute : Attribute
    {
    }
}

