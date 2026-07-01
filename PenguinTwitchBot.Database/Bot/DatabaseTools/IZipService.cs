using System.IO.Compression;

namespace PenguinTwitchBot.Database.Bot.DatabaseTools
{
    public interface IZipService
    {
        void CreateFromDirectory(string sourceDirectoryName, string destinationArchiveFileName);
    }
}
