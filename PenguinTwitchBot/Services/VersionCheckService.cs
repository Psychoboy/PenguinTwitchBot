using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;
using PenguinTwitchBot.Bot.Core.Database;
using PenguinTwitchBot.Database.Bot.DatabaseTools;

namespace PenguinTwitchBot.Services;

public interface IVersionCheckService
{
    string CurrentVersion { get; }
    string? LatestVersion { get; }
    string? LatestReleaseNotes { get; }
    string? CurrentRid { get; }
    string? LatestUpdateAssetName { get; }
    string? LatestRecoveryBundleName { get; }
    bool HasRecoveryBundle { get; }
    bool IncludePreviewReleases { get; }
    bool CanUserInitiateUpdate { get; }
    string? UpdateBlockedReason { get; }
    bool IsUpToDate { get; }
    Task RefreshNowAsync(CancellationToken cancellationToken = default);
    Task SetIncludePreviewReleasesAsync(bool value, CancellationToken cancellationToken = default);
    Task<UpdateStartResult> StartManualUpdateAsync(CancellationToken cancellationToken = default);
    Task<UpdateStartResult> StartRestoreLatestRecoveryAsync(CancellationToken cancellationToken = default);
    event Action? VersionStatusChanged;
}

public sealed class UpdateStartResult
{
    public bool Started { get; init; }
    public string Message { get; init; } = string.Empty;
}

public class VersionCheckService : BackgroundService, IVersionCheckService
{
    private static readonly Version BootstrapMinVersion = new(0, 2, 0);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<VersionCheckService> _logger;
    private readonly IDatabaseTools _databaseTools;
    private readonly IBackupTools _backupTools;
    private readonly IUpdateChannelSettingsService _updateChannelSettingsService;
    private readonly IHostApplicationLifetime _hostApplicationLifetime;

    private string? _latestUpdateAssetUrl;

    public event Action? VersionStatusChanged;

    public string CurrentVersion { get; } =
        Assembly.GetEntryAssembly()
                ?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion
                ?.Split('+')[0]   // strip build metadata
        ?? "0.0.0";

    public string? LatestVersion { get; private set; }
    public string? LatestReleaseNotes { get; private set; }
    public string? LatestUpdateAssetName { get; private set; }
    public string? CurrentRid { get; } = DetectRid();
    public string? LatestRecoveryBundleName => FindLatestRecoveryZip()?.Name;
    public bool HasRecoveryBundle => FindLatestRecoveryZip() is not null;
    public bool IncludePreviewReleases { get; private set; }

    public bool CanUserInitiateUpdate =>
        !IsUpToDate &&
        !string.IsNullOrWhiteSpace(_latestUpdateAssetUrl) &&
        string.IsNullOrWhiteSpace(UpdateBlockedReason);

    public string? UpdateBlockedReason
    {
        get
        {
            if (CurrentVersion == "0.0.0")
            {
                return "Auto update is disabled for version 0.0.0.";
            }

            var parsedCurrent = ParseLooseVersion(CurrentVersion);
            if (parsedCurrent is null)
            {
                return "Current version format is not supported for auto update.";
            }

            if (parsedCurrent < BootstrapMinVersion)
            {
                return $"A one-time full install is required for versions older than v{BootstrapMinVersion}.";
            }

            if (string.IsNullOrWhiteSpace(CurrentRid))
            {
                return "Current platform is not supported by auto update.";
            }

            if (string.IsNullOrWhiteSpace(LatestVersion))
            {
                return "Unable to determine latest release.";
            }

            if (string.IsNullOrWhiteSpace(LatestUpdateAssetName) || string.IsNullOrWhiteSpace(_latestUpdateAssetUrl))
            {
                return "No compatible update package was found for this platform.";
            }

            return null;
        }
    }

    public bool IsUpToDate =>
        LatestVersion is null ||
        CurrentVersion == "0.0.0" ||
        string.Equals(CurrentVersion, LatestVersion, StringComparison.OrdinalIgnoreCase);

    public VersionCheckService(
        IHttpClientFactory httpClientFactory,
        ILogger<VersionCheckService> logger,
        IDatabaseTools databaseTools,
        IBackupTools backupTools,
        IUpdateChannelSettingsService updateChannelSettingsService,
        IHostApplicationLifetime hostApplicationLifetime)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _databaseTools = databaseTools;
        _backupTools = backupTools;
        _updateChannelSettingsService = updateChannelSettingsService;
        _hostApplicationLifetime = hostApplicationLifetime;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Check immediately on startup, then every 6 hours
        await RefreshNowAsync(stoppingToken);
        using var timer = new PeriodicTimer(TimeSpan.FromHours(6));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RefreshNowAsync(stoppingToken);
        }
    }

    public async Task RefreshNowAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            IncludePreviewReleases = await _updateChannelSettingsService.GetIncludePreviewReleasesAsync();

            var client = _httpClientFactory.CreateClient("GitHubRelease");
            var response = await client.GetAsync(
                "https://api.github.com/repos/Psychoboy/PenguinTwitchBot/releases?per_page=10",
                cancellationToken);

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                // No releases published yet — not an error
                return;
            }

            response.EnsureSuccessStatusCode();

            var releases = await response.Content.ReadFromJsonAsync<GitHubRelease[]>();
            var release = releases?.FirstOrDefault(r => !r.Draft && (IncludePreviewReleases || !r.PreRelease));
            if (release?.TagName is not null)
            {
                LatestVersion = release.TagName.TrimStart('v');
                LatestReleaseNotes = release.Body;
                ResolveUpdateAsset(release);
                _logger.LogInformation("Version check: current={Current}, latest={Latest}, upToDate={UpToDate}",
                    CurrentVersion, LatestVersion, IsUpToDate);
                VersionStatusChanged?.Invoke();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to check latest version from GitHub.");
        }
    }

    public async Task SetIncludePreviewReleasesAsync(bool value, CancellationToken cancellationToken = default)
    {
        await _updateChannelSettingsService.SetIncludePreviewReleasesAsync(value);
        IncludePreviewReleases = value;
        await RefreshNowAsync(cancellationToken);
    }

    public async Task<UpdateStartResult> StartManualUpdateAsync(CancellationToken cancellationToken = default)
    {
        await RefreshNowAsync(cancellationToken);

        if (!CanUserInitiateUpdate)
        {
            return new UpdateStartResult
            {
                Started = false,
                Message = UpdateBlockedReason ?? "Update cannot be started right now."
            };
        }

        try
        {
            var appRoot = AppContext.BaseDirectory;
            var updatesDir = Path.Combine(appRoot, "Data", "updates", "downloads");
            var recoveryDir = Path.Combine(appRoot, "Data", "updates", "recovery");
            Directory.CreateDirectory(updatesDir);
            Directory.CreateDirectory(recoveryDir);

            var packageFileName = LatestUpdateAssetName!;
            var packagePath = Path.Combine(updatesDir, packageFileName);

            var client = _httpClientFactory.CreateClient("GitHubRelease");
            await using (var downloadStream = await client.GetStreamAsync(_latestUpdateAssetUrl!, cancellationToken))
            await using (var fileStream = File.Create(packagePath))
            {
                await downloadStream.CopyToAsync(fileStream, cancellationToken);
            }

            await _databaseTools.Backup();

            string? latestDatabaseBackup = null;
            if (Directory.Exists(_backupTools.BackupDirectory))
            {
                latestDatabaseBackup = _backupTools
                    .GetBackupFiles(_backupTools.BackupDirectory)
                    .OrderByDescending(f => f.CreationTimeUtc)
                    .FirstOrDefault()
                    ?.FullName;
            }

            var updaterRoot = Path.Combine(appRoot, "Updater");
            var updaterName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? "PenguinTwitchBot.Updater.exe"
                : "PenguinTwitchBot.Updater";
            var bootstrapName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? "PenguinTwitchBot.Updater.Bootstrap.exe"
                : "PenguinTwitchBot.Updater.Bootstrap";

            var updaterPath = Path.Combine(updaterRoot, updaterName);
            var bootstrapPath = Path.Combine(updaterRoot, bootstrapName);

            if (!File.Exists(updaterPath) || !File.Exists(bootstrapPath))
            {
                return new UpdateStartResult
                {
                    Started = false,
                    Message = "Updater binaries are missing. Install the latest full package first."
                };
            }

            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = bootstrapPath,
                UseShellExecute = false,
                WorkingDirectory = updaterRoot
            };

            startInfo.ArgumentList.Add(updaterPath);
            startInfo.ArgumentList.Add("--app-root");
            startInfo.ArgumentList.Add(appRoot);
            startInfo.ArgumentList.Add("--package");
            startInfo.ArgumentList.Add(packagePath);
            startInfo.ArgumentList.Add("--recovery-root");
            startInfo.ArgumentList.Add(recoveryDir);
            startInfo.ArgumentList.Add("--current-version");
            startInfo.ArgumentList.Add(CurrentVersion);
            startInfo.ArgumentList.Add("--target-version");
            startInfo.ArgumentList.Add(LatestVersion!);

            if (!string.IsNullOrWhiteSpace(CurrentRid))
            {
                startInfo.ArgumentList.Add("--rid");
                startInfo.ArgumentList.Add(CurrentRid!);
            }

            if (!string.IsNullOrWhiteSpace(latestDatabaseBackup) && File.Exists(latestDatabaseBackup))
            {
                startInfo.ArgumentList.Add("--database-backup");
                startInfo.ArgumentList.Add(latestDatabaseBackup);
            }

            var process = System.Diagnostics.Process.Start(startInfo);
            if (process is null)
            {
                return new UpdateStartResult
                {
                    Started = false,
                    Message = "Failed to launch updater process."
                };
            }

            _hostApplicationLifetime.StopApplication();

            return new UpdateStartResult
            {
                Started = true,
                Message = "Update started. The application is shutting down so the updater can apply changes."
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start manual update.");
            return new UpdateStartResult
            {
                Started = false,
                Message = $"Failed to start update: {ex.Message}"
            };
        }
    }

    public Task<UpdateStartResult> StartRestoreLatestRecoveryAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var appRoot = AppContext.BaseDirectory;
            var updaterRoot = Path.Combine(appRoot, "Updater");
            var updaterName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? "PenguinTwitchBot.Updater.exe"
                : "PenguinTwitchBot.Updater";
            var bootstrapName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? "PenguinTwitchBot.Updater.Bootstrap.exe"
                : "PenguinTwitchBot.Updater.Bootstrap";

            var updaterPath = Path.Combine(updaterRoot, updaterName);
            var bootstrapPath = Path.Combine(updaterRoot, bootstrapName);
            if (!File.Exists(updaterPath) || !File.Exists(bootstrapPath))
            {
                return Task.FromResult(new UpdateStartResult
                {
                    Started = false,
                    Message = "Updater binaries are missing. Install the latest full package first."
                });
            }

            var latestRecovery = FindLatestRecoveryZip();
            if (latestRecovery is null)
            {
                return Task.FromResult(new UpdateStartResult
                {
                    Started = false,
                    Message = "No recovery bundle was found."
                });
            }

            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = bootstrapPath,
                UseShellExecute = false,
                WorkingDirectory = updaterRoot
            };

            startInfo.ArgumentList.Add(updaterPath);
            startInfo.ArgumentList.Add("--operation");
            startInfo.ArgumentList.Add("restore");
            startInfo.ArgumentList.Add("--app-root");
            startInfo.ArgumentList.Add(appRoot);
            startInfo.ArgumentList.Add("--recovery-zip");
            startInfo.ArgumentList.Add(latestRecovery.FullName);

            var process = System.Diagnostics.Process.Start(startInfo);
            if (process is null)
            {
                return Task.FromResult(new UpdateStartResult
                {
                    Started = false,
                    Message = "Failed to launch updater process."
                });
            }

            _hostApplicationLifetime.StopApplication();

            return Task.FromResult(new UpdateStartResult
            {
                Started = true,
                Message = "Recovery restore started. The application is shutting down so files can be restored."
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start recovery restore.");
            return Task.FromResult(new UpdateStartResult
            {
                Started = false,
                Message = $"Failed to start recovery restore: {ex.Message}"
            });
        }
    }

    private void ResolveUpdateAsset(GitHubRelease release)
    {
        LatestUpdateAssetName = null;
        _latestUpdateAssetUrl = null;

        if (string.IsNullOrWhiteSpace(CurrentRid) || string.IsNullOrWhiteSpace(release.TagName))
        {
            return;
        }

        var expectedPrefix = $"PenguinTwitchBot-{release.TagName}-";
        var expectedSuffix = $"-{CurrentRid}-update.zip";

        var asset = release.Assets.FirstOrDefault(a =>
            !string.IsNullOrWhiteSpace(a.Name) &&
            !string.IsNullOrWhiteSpace(a.BrowserDownloadUrl) &&
            a.Name.StartsWith(expectedPrefix, StringComparison.OrdinalIgnoreCase) &&
            a.Name.EndsWith(expectedSuffix, StringComparison.OrdinalIgnoreCase));

        if (asset is null)
        {
            return;
        }

        LatestUpdateAssetName = asset.Name;
        _latestUpdateAssetUrl = asset.BrowserDownloadUrl;
    }

    private static string? DetectRid()
    {
        string os;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            os = "win";
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            os = "linux";
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            os = "osx";
        }
        else
        {
            return null;
        }

        var arch = RuntimeInformation.OSArchitecture switch
        {
            Architecture.X64 => "x64",
            Architecture.Arm64 => "arm64",
            _ => null
        };

        if (arch is null)
        {
            return null;
        }

        return $"{os}-{arch}";
    }

    private static Version? ParseLooseVersion(string version)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            return null;
        }

        var core = version.Trim().TrimStart('v');
        var separatorIndex = core.IndexOfAny(['-', '+']);
        if (separatorIndex >= 0)
        {
            core = core[..separatorIndex];
        }

        return Version.TryParse(core, out var parsed) ? parsed : null;
    }

    private FileInfo? FindLatestRecoveryZip()
    {
        var recoveryDir = Path.Combine(AppContext.BaseDirectory, "Data", "updates", "recovery");
        if (!Directory.Exists(recoveryDir))
        {
            return null;
        }

        var latestPath = Directory
            .GetFiles(recoveryDir, "recovery-*.zip", SearchOption.TopDirectoryOnly)
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault();

        return string.IsNullOrWhiteSpace(latestPath) ? null : new FileInfo(latestPath);
    }

    private sealed class GitHubRelease
    {
        [JsonPropertyName("tag_name")]
        public string? TagName { get; set; }

        [JsonPropertyName("draft")]
        public bool Draft { get; set; }

        [JsonPropertyName("prerelease")]
        public bool PreRelease { get; set; }

        [JsonPropertyName("body")]
        public string? Body { get; set; }

        [JsonPropertyName("assets")]
        public List<GitHubReleaseAsset> Assets { get; set; } = [];
    }

    private sealed class GitHubReleaseAsset
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("browser_download_url")]
        public string? BrowserDownloadUrl { get; set; }
    }
}
