using Newtonsoft.Json;

namespace DotNetTwitchBot.Bot.TwitchServices
{
    public class SettingsFileManager
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<SettingsFileManager> _logger;
        static readonly SemaphoreSlim _semaphoreSlim = new(1);

        public SettingsFileManager(ILogger<SettingsFileManager> logger, IConfiguration configuration)
        {
            _configuration = configuration;
            _logger = logger;

        }
        public async Task AddOrUpdateAppSetting<T>(string sectionPathKey, T value)
        {

            try
            {
                _semaphoreSlim.Wait();
                var filePath = _configuration["Secrets:SecretsConf"] ?? throw new Exception("Invalid file configuration");
                string json = File.ReadAllText(filePath);
                dynamic jsonObj = JsonConvert.DeserializeObject(json) ?? throw new InvalidOperationException();

                SetValueRecursively(sectionPathKey, jsonObj, value);

                string output = Newtonsoft.Json.JsonConvert.SerializeObject(jsonObj, Newtonsoft.Json.Formatting.Indented);
                while (!IsFileLocked(filePath))
                {
                    _logger.LogWarning("Settings file was locked... Waiting to for it to be unlocked");
                    Thread.Sleep(5000);
                }
                await File.WriteAllTextAsync(filePath, output);

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating settings");
            }
            finally { _semaphoreSlim.Release(); }
        }

        private bool IsFileLocked(string filePath)
        {
            try
            {
                using var fileStream = File.OpenWrite(filePath);
                return fileStream.Length > 0;
            }
            catch (Exception) { return false; }
        }

        private void SetValueRecursively<T>(string sectionPathKey, dynamic jsonObj, T value)
        {
            // split the string at the first ':' character
            var remainingSections = sectionPathKey.Split(":", 2);

            var currentSection = remainingSections[0];
            if (remainingSections.Length > 1)
            {
                // continue with the process, moving down the tree
                var nextSection = remainingSections[1];
                SetValueRecursively(nextSection, jsonObj[currentSection], value);
            }
            else
            {
                // we've got to the end of the tree, set the value
                jsonObj[currentSection] = value;
            }
        }
    }
}