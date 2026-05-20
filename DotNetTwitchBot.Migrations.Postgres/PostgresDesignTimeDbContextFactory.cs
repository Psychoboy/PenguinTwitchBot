using DotNetTwitchBot.Bot.Core.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace DotNetTwitchBot.Migrations.Postgres;

public sealed class PostgresDesignTimeDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
	public ApplicationDbContext CreateDbContext(string[] args)
	{
		// First, try to get root directory from environment variable
		var appProjectDir = Environment.GetEnvironmentVariable("DOTNET_TWITCHBOT_ROOT");
		
		if (!string.IsNullOrEmpty(appProjectDir))
		{
			// When running from batch file, config is in DotNetTwitchBot subfolder
			appProjectDir = Path.Combine(appProjectDir, "DotNetTwitchBot");
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
		var connectionString = config.GetConnectionString("PostgresConnection")
			?? Environment.GetEnvironmentVariable("DOTNETTWITCHBOT_POSTGRES_CONNECTION")
			?? "Host=localhost;Database=dotnettwitchbot;Username=postgres;Password=Password;";

		var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
		optionsBuilder.UseNpgsql(connectionString, options =>
		{
			options.MigrationsAssembly(typeof(PostgresDesignTimeDbContextFactory).Assembly.GetName().Name);
		});

		return new ApplicationDbContext(optionsBuilder.Options);
	}
}
