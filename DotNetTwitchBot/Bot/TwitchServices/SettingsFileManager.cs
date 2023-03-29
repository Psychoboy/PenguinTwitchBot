using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace DotNetTwitchBot.Bot.TwitchServices
{
    public class SettingsFileManager
    {
        private IConfiguration _configuration;
        private ILogger<SettingsFileManager> _logger;

        public SettingsFileManager(ILogger<SettingsFileManager> logger, IConfiguration configuration)
        {
            _configuration = configuration;
            _logger = logger;

        }
        public void AddOrUpdateAppSetting<T>(string sectionPathKey, T value)
        {
            lock (this)
            {
                try
                {
                    var filePath = _configuration["Secrets:SecretsConf"]; //Path.Combine(AppContext.BaseDirectory, "appsettings.secrets.json");
                    if (filePath == null) throw new Exception("Invalid file configuration");
                    string json = File.ReadAllText(filePath);
                    dynamic jsonObj = JsonConvert.DeserializeObject(json) ?? throw new InvalidOperationException();

                    SetValueRecursively(sectionPathKey, jsonObj, value);

                    string output = Newtonsoft.Json.JsonConvert.SerializeObject(jsonObj, Newtonsoft.Json.Formatting.Indented);
                    File.WriteAllText(filePath, output);

                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error updating settings");
                }
            }
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