using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using NSubstitute;
using PenguinTwitchBot.Bot.TwitchServices;
using Xunit;

namespace PenguinTwitchBot.Test.Bot.TwitchServices;

public class SettingsFileManagerTests : IDisposable
{
    private readonly string _tempFile;
    private readonly IConfiguration _configuration;
    private readonly ILogger<SettingsFileManager> _logger;

    public SettingsFileManagerTests()
    {
        _tempFile = Path.GetTempFileName();
        File.WriteAllText(_tempFile, "{\n  \"broadcaster\": \"old_broadcaster\"\n}");

        var configValues = new Dictionary<string, string?>
        {
            ["Secrets:SecretsConf"] = _tempFile
        };
        _configuration = new ConfigurationBuilder().AddInMemoryCollection(configValues).Build();
        _logger = Substitute.For<ILogger<SettingsFileManager>>();
    }

    public void Dispose()
    {
        if (File.Exists(_tempFile))
        {
            try { File.Delete(_tempFile); } catch { }
        }

        // Clean up any backup files
        for (int i = 1; i <= 5; i++)
        {
            var bak = $"{_tempFile}.bak.{i}";
            if (File.Exists(bak))
            {
                try { File.Delete(bak); } catch { }
            }
        }
    }

    [Fact]
    public async Task AddOrUpdateAppSettings_BatchUpdatesMultipleKeysAtomically()
    {
        var manager = new SettingsFileManager(_logger, _configuration);

        var batch = new Dictionary<string, object?>
        {
            ["broadcaster"] = "new_broadcaster",
            ["botName"] = "my_bot",
            ["twitchClientId"] = "client_123",
            ["twitchClientSecret"] = "secret_abc",
            ["TwitchExtension:Secret"] = "ZXh0ZW5zaW9uX3NlY3JldF8xMjM0NTY3OA=="
        };

        await manager.AddOrUpdateAppSettings(batch);

        var json = await File.ReadAllTextAsync(_tempFile);
        var obj = JObject.Parse(json);

        Assert.Equal("new_broadcaster", obj["broadcaster"]?.ToString());
        Assert.Equal("my_bot", obj["botName"]?.ToString());
        Assert.Equal("client_123", obj["twitchClientId"]?.ToString());
        Assert.Equal("secret_abc", obj["twitchClientSecret"]?.ToString());
        Assert.Equal("ZXh0ZW5zaW9uX3NlY3JldF8xMjM0NTY3OA==", obj["TwitchExtension"]?["Secret"]?.ToString());
    }

    [Fact]
    public async Task AddOrUpdateAppSetting_SingleKey_UpdatesSuccessfully()
    {
        var manager = new SettingsFileManager(_logger, _configuration);

        await manager.AddOrUpdateAppSetting("weatherApi", "key_999");

        var json = await File.ReadAllTextAsync(_tempFile);
        var obj = JObject.Parse(json);

        Assert.Equal("key_999", obj["weatherApi"]?.ToString());
        Assert.Equal("old_broadcaster", obj["broadcaster"]?.ToString());
    }
}

