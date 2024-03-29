using MySqlConnector;

namespace DotNetTwitchBot.Bot.Core.Database
{
    public class DatabaseTools : IDatabaseTools
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<DatabaseTools> _logger;

        public DatabaseTools(ILogger<DatabaseTools> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
        }

        public async Task Backup()
        {
            _logger.LogInformation("Starting backup");
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
                mb.ExportToFile(string.Format("Data/backup/dbBackup-{0}.sql", DateTime.Now.ToString("yyyy-dd-M--HH-mm-ss")));
                await conn.CloseAsync();
            }
            _logger.LogInformation("Deleting old backups > 30 days");
            var files = Directory.GetFiles("Data/backup");
            foreach (var file in files)
            {
                FileInfo fi = new(file);
                if (fi.CreationTime < DateTime.Now.AddDays(-30))
                {
                    _logger.LogInformation("Deleting backup: {0}", fi.Name);
                    fi.Delete();
                }
            }
            _logger.LogInformation("Completed backup");
        }
    }
}