
using MySqlConnector;

namespace DotNetTwitchBot.Bot.Core.Database
{
    public class DatabaseTools : IDatabaseTools
    {
        private readonly IConfiguration _configuration;
        private readonly Commands.Moderation.Admin _admin;
        private readonly ILogger<DatabaseTools> _logger;

        public DatabaseTools(ILogger<DatabaseTools> logger, Commands.Moderation.Admin admin, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
            _admin = admin;
        }

        public async Task Backup()
        {
            _logger.LogInformation("Starting RAW backup");
            var connectionString = _configuration.GetConnectionString("DefaultConnection");
            using (MySqlConnection conn = new(connectionString))
            {
                using MySqlCommand cmd = new();
                using MySqlBackup mb = new(cmd);
                if (!Directory.Exists("Data/backup"))
                {
                    Directory.CreateDirectory("Data/backup");
                }
                cmd.Connection = conn;
                await conn.OpenAsync();
                mb.ExportToFile(string.Format("Data/backup/dbBackup-{0}.sql", DateTime.Now.ToString("yyyy-MM-dd--HH-mm-ss")));
                await conn.CloseAsync();
            }
            _logger.LogInformation("Deleting old RAW backups > 30 days");
            var files = Directory.GetFiles("Data/backup");
            foreach (var file in files)
            {
                FileInfo fi = new(file);
                if (fi.CreationTime < DateTime.Now.AddDays(-30))
                {
                    _logger.LogInformation("Deleting RAW backup: {0}", fi.Name);
                    fi.Delete();
                }
            }
            _logger.LogInformation("Completed RAW backup");
            await _admin.BackupDatabase();
        }
    }
}