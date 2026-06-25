using PenguinTwitchBot.Database.Bot.Core.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace PenguinTwitchBot.Migrations.Sqlite;

public sealed class SqliteDesignTimeDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
	public ApplicationDbContext CreateDbContext(string[] args)
	{
		// First, try to get root directory from environment variable
		var appProjectDir = Environment.GetEnvironmentVariable("DOTNET_TWITCHBOT_ROOT");
		
		if (!string.IsNullOrEmpty(appProjectDir))
		{
			// When running from batch file, config is in PenguinTwitchBot subfolder
			appProjectDir = Path.Combine(appProjectDir, "PenguinTwitchBot");
		}
		else
		{
			// Fall back to searching upward from current directory
			appProjectDir = Directory.GetCurrentDirectory();
			
			if (!File.Exists(Path.Combine(appProjectDir, "appsettings.json")))
			{
				var searchDir = appProjectDir;
				while (searchDir != null && !File.Exists(Path.Combine(searchDir, "appsettings.json")))
				{
					var parent = Path.GetDirectoryName(searchDir);
					if (parent == searchDir) break; // Reached filesystem root
					searchDir = parent;
				}
				
				if (searchDir != null && File.Exists(Path.Combine(searchDir, "appsettings.json")))
				{
					appProjectDir = searchDir;
				}
			}
		}

		var appSettingsPath = Path.Combine(appProjectDir, "appsettings.json");
		var config = new ConfigurationBuilder()
			.AddJsonFile(appSettingsPath, optional: false, reloadOnChange: true)
			.Build();

		// Load secrets file
		var secretsSection = config.GetSection("Secrets");
		var secretsFileLocation = secretsSection.GetValue<string>("SecretsConf");
		if (!string.IsNullOrEmpty(secretsFileLocation))
		{
			var secretsPath = Path.Combine(appProjectDir, secretsFileLocation);
			if (File.Exists(secretsPath))
			{
				config = new ConfigurationBuilder()
					.AddJsonFile(appSettingsPath, optional: false, reloadOnChange: true)
					.AddJsonFile(secretsPath, optional: true, reloadOnChange: true)
					.Build();
			}
		}

		// Get connection string from configuration
		var connectionString = config.GetConnectionString("SqliteConnection")
			?? Environment.GetEnvironmentVariable("PenguinTwitchBot_SQLITE_CONNECTION")
			?? "Data Source=Data/PenguinTwitchBot.sqlite";

		// Convert relative paths to absolute paths for SQLite connection string
		if (connectionString.Contains("Data Source=") && !Path.IsPathRooted(connectionString.Split("=")[1].Trim(';')))
		{
			var relativePath = connectionString.Split("=")[1].Trim(';');
			var absolutePath = Path.Combine(appProjectDir, relativePath);
			connectionString = $"Data Source={absolutePath}";
		}

		var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
		optionsBuilder.UseSqlite(connectionString, options =>
		{
			options.MigrationsAssembly(typeof(SqliteDesignTimeDbContextFactory).Assembly.GetName().Name);
		});

		return new ApplicationDbContext(optionsBuilder.Options);
	}
}
