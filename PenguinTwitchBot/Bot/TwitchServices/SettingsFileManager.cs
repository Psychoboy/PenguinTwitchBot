using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace PenguinTwitchBot.Bot.TwitchServices
{
    public class SettingsFileManager
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<SettingsFileManager> _logger;
        static readonly SemaphoreSlim _semaphoreSlim = new(1);
        private const int MaxBackups = 3;

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
                await WriteSettingsFileAtomically(filePath, output);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating settings");
            }
            finally { _semaphoreSlim.Release(); }
        }

        private async Task WriteSettingsFileAtomically(string filePath, string content)
        {
            string? tempFilePath = null;
            bool enteredReplacementPhase = false;
            try
            {
                await RotateBackups(filePath);

                tempFilePath = filePath + ".tmp";
                await File.WriteAllTextAsync(tempFilePath, content);

                if (!File.Exists(filePath))
                {
                    enteredReplacementPhase = true;
                    File.Move(tempFilePath, filePath);
                    tempFilePath = null;
                    return;
                }

                var original = new FileInfo(filePath);
                var replacement = new FileInfo(tempFilePath);

                original.IsReadOnly = false;
                replacement.IsReadOnly = false;

                enteredReplacementPhase = true;
                File.Replace(tempFilePath, filePath, null, true);
                tempFilePath = null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error writing settings file atomically.");

                if (!string.IsNullOrEmpty(tempFilePath) && File.Exists(tempFilePath))
                {
                    try { File.Delete(tempFilePath); }
                    catch
                    {
                        // ignore any exceptions during cleanup
                    }
                }

                if (enteredReplacementPhase)
                {
                    _logger.LogError("Replacement phase failed. Attempting to restore from backup.");
                    await RestoreFromBackup(filePath);
                }

                throw;
            }
        }

        private static async Task RotateBackups(string filePath)
        {
            string oldestBackup = GetBackupPath(filePath, MaxBackups);
            if (File.Exists(oldestBackup))
            {
                File.Delete(oldestBackup);
            }

            for (int i = MaxBackups - 1; i >= 1; i--)
            {
                string src = GetBackupPath(filePath, i);
                string dst = GetBackupPath(filePath, i + 1);
                if (File.Exists(src))
                {
                    File.Move(src, dst);
                }
            }

            if (File.Exists(filePath))
            {
                string firstBackup = GetBackupPath(filePath, 1);
                File.Copy(filePath, firstBackup, overwrite: true);
                await Task.CompletedTask;
            }
        }

        private async Task RestoreFromBackup(string filePath)
        {
            for (int i = 1; i <= MaxBackups; i++)
            {
                string backupPath = GetBackupPath(filePath, i);
                if (File.Exists(backupPath))
                {
                    try
                    {
                        File.Copy(backupPath, filePath, overwrite: true);
                        _logger.LogInformation("Successfully restored settings file from backup: {BackupPath}", backupPath);
                        return;
                    }
                    catch (Exception restoreEx)
                    {
                        _logger.LogError(restoreEx, "Failed to restore from backup: {BackupPath}", backupPath);
                    }
                }
            }

            _logger.LogCritical("All backups failed to restore. Settings file may be corrupted at: {FilePath}", filePath);
            await Task.CompletedTask;
        }

        private static string GetBackupPath(string filePath, int backupIndex)
        {
            return $"{filePath}.bak.{backupIndex}";
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