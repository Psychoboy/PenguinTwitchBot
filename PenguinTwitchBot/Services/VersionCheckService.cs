using System.Reflection;
using System.Text.Json.Serialization;

namespace PenguinTwitchBot.Services;

public interface IVersionCheckService
{
    string CurrentVersion { get; }
    string? LatestVersion { get; }
    bool IsUpToDate { get; }
}

public class VersionCheckService : IVersionCheckService, IHostedService, IDisposable
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<VersionCheckService> _logger;
    private Timer? _timer;

    public string CurrentVersion { get; } =
        Assembly.GetEntryAssembly()
                ?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion
                ?.Split('+')[0]   // strip build metadata
        ?? "0.0.0";

    public string? LatestVersion { get; private set; }

    public bool IsUpToDate =>
        LatestVersion is null ||
        CurrentVersion == "0.0.0" ||
        string.Equals(CurrentVersion, LatestVersion, StringComparison.OrdinalIgnoreCase);

    public VersionCheckService(IHttpClientFactory httpClientFactory, ILogger<VersionCheckService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        // Check immediately, then every 6 hours
        _timer = new Timer(async _ => await CheckAsync(), null, TimeSpan.Zero, TimeSpan.FromHours(6));
        return Task.CompletedTask;
    }

    private async Task CheckAsync()
    {
        try
        {
            var client = _httpClientFactory.CreateClient("GitHubRelease");
            var response = await client.GetAsync(
                "https://api.github.com/repos/Psychoboy/PenguinTwitchBot/releases?per_page=1");

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                // No releases published yet — not an error
                return;
            }

            response.EnsureSuccessStatusCode();

            var releases = await response.Content.ReadFromJsonAsync<GitHubRelease[]>();
            var release = releases?.FirstOrDefault();
            if (release?.TagName is not null)
            {
                LatestVersion = release.TagName.TrimStart('v');
                _logger.LogInformation("Version check: current={Current}, latest={Latest}, upToDate={UpToDate}",
                    CurrentVersion, LatestVersion, IsUpToDate);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to check latest version from GitHub.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _timer?.Change(Timeout.Infinite, 0);
        return Task.CompletedTask;
    }

    public void Dispose() => _timer?.Dispose();

    private sealed class GitHubRelease
    {
        [JsonPropertyName("tag_name")]
        public string? TagName { get; set; }
    }
}
