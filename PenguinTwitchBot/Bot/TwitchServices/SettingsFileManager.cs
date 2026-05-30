using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace PenguinTwitchBot.Bot.TwitchServices
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
            var filePath = _configuration["Secrets:SecretsConf"] ?? throw new Exception("Invalid file configuration");
            await AddOrUpdateSettingInFile(sectionPathKey, value, filePath);
        }

        public async Task AddOrUpdateMainAppSetting<T>(string sectionPathKey, T value)
        {
            var filePath = Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json");
            await AddOrUpdateSettingInFile(sectionPathKey, value, filePath);
        }

        private async Task AddOrUpdateSettingInFile<T>(string sectionPathKey, T value, string filePath)
        {
            try
            {
                await _semaphoreSlim.WaitAsync();
                string json = File.ReadAllText(filePath);
                var jsonObj = JsonConvert.DeserializeObject<JObject>(json) ?? throw new InvalidOperationException();

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

        private static bool IsFileLocked(string filePath)
        {
            try
            {
                using var fileStream = File.OpenWrite(filePath);
                return fileStream.Length > 0;
            }
            catch (Exception) { return false; }
        }

        private static void SetValueRecursively<T>(string sectionPathKey, JObject jsonObj, T value)
        {
            var sections = sectionPathKey.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (sections.Length == 0)
            {
                throw new ArgumentException("Invalid section path key", nameof(sectionPathKey));
            }

            var currentObject = jsonObj;
            for (var i = 0; i < sections.Length - 1; i++)
            {
                var section = sections[i];
                if (currentObject[section] is not JObject childObject)
                {
                    childObject = new JObject();
                    currentObject[section] = childObject;
                }

                currentObject = childObject;
            }

            var finalSection = sections[^1];
            currentObject[finalSection] = value is null ? JValue.CreateNull() : JToken.FromObject(value);
        }
    }
}