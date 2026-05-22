namespace DotNetTwitchBot.Setup.Models
{
    public enum DatabaseProviderOption
    {
        Sqlite,
        MariaDb,
        Postgres
    }

    public class SetupWizardModel
    {
        // Step 1: Bot Identity
        public string BotName { get; set; } = "";
        public string Broadcaster { get; set; } = "";
        public string BaseUrl { get; set; } = "http://localhost:5000";

        // Step 2: Twitch Streamer App
        public string TwitchClientId { get; set; } = "";
        public string TwitchClientSecret { get; set; } = "";

        // Step 3: Twitch Bot App
        public bool BotUseSameApp { get; set; } = false;
        public string TwitchBotClientId { get; set; } = "";
        public string TwitchBotClientSecret { get; set; } = "";

        // Step 4: Database
        public DatabaseProviderOption DatabaseProvider { get; set; } = DatabaseProviderOption.Sqlite;
        public string SqliteFilePath { get; set; } = "";
        public string MariaDbConnectionString { get; set; } = "";
        public string PostgresConnectionString { get; set; } = "";

        // Step 5: YouTube
        public string YoutubeApiKey { get; set; } = "";

        // Step 6: Discord
        public bool DiscordEnabled { get; set; } = false;
        public string DiscordToken { get; set; } = "";
        public string DiscordServerId { get; set; } = "";
        public string DiscordBroadcastChannel { get; set; } = "";
        public string DiscordPingRoleId { get; set; } = "";
        public string DiscordMemberRoleId { get; set; } = "";

        // Step 7: Weather
        public bool WeatherEnabled { get; set; } = false;
        public string WeatherApiKey { get; set; } = "";
        public string WeatherDefaultLocation { get; set; } = "";

        // Step 8: OpenAI
        public bool OpenAiEnabled { get; set; } = false;
        public string OpenAiApiKey { get; set; } = "";
    }
}
