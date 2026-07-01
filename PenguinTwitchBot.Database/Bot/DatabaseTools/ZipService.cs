using System.IO.Compression;

namespace PenguinTwitchBot.Database.Bot.DatabaseTools
{
    public class ZipService : IZipService
    {
        public void CreateFromDirectory(string sourceDirectoryName, string destinationArchiveFileName)
        {
            ZipFile.CreateFromDirectory(sourceDirectoryName, destinationArchiveFileName);
        }
    }
}
