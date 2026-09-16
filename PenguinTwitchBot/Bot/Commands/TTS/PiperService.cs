using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using PenguinTwitchBot.Database.Bot.Models;
using PiperSharp;
using PiperSharp.Models;

namespace PenguinTwitchBot.Bot.Commands.TTS;

public class PiperService(ILogger<PiperService> logger) : IPiperService
{
    private readonly SemaphoreSlim _execLock = new(1, 1);
    private readonly SemaphoreSlim _modelLock = new(1, 1);
    private readonly ConcurrentDictionary<string, VoiceModel> _loadedModels = new();

    private const string PinnedPiperReleaseVersion = "2023.11.14-2";
    private static readonly TimeSpan CatalogCacheTtl = TimeSpan.FromDays(7);

    private static readonly Dictionary<string, string> PiperArchiveSha256 = new(StringComparer.OrdinalIgnoreCase)
    {
        ["piper_linux_x86_64.tar.gz"] = "a50cb45f355b7af1f6d758c1b360717877ba0a398cc8cbe6d2a7a3a26e225992",
        ["piper_linux_aarch64.tar.gz"] = "fea0fd2d87c54dbc7078d0f878289f404bd4d6eea6e7444a77835d1537ab88eb",
        ["piper_windows_amd64.zip"] = "f3c58906402b24f3a96d92145f58acba6d86c9b5db896d207f78dc80811efcea",
        ["piper_macos_x64.tar.gz"] = "ced85c0a3df13945b1e623b878a48fdc2854d5c485b4b67f62857cf551deaf8b",
        ["piper_macos_aarch64.tar.gz"] = "6b1eb03b3735946cb35216e063e7eebcc33a6bbf5dd96ec0217959bf1cdcb0cc"
    };

    private static string GetExpectedArchiveName()
    {
        if (OperatingSystem.IsWindows())
        {
            return "piper_windows_amd64.zip";
        }
        if (OperatingSystem.IsLinux())
        {
            return RuntimeInformation.OSArchitecture == Architecture.Arm64
                ? "piper_linux_aarch64.tar.gz"
                : "piper_linux_x86_64.tar.gz";
        }
        if (OperatingSystem.IsMacOS())
        {
            return RuntimeInformation.OSArchitecture == Architecture.Arm64
                ? "piper_macos_aarch64.tar.gz"
                : "piper_macos_x64.tar.gz";
        }
        return string.Empty;
    }

    private static string PiperDirectory => ResolvePiperDirectory();
    private static string PiperModelsDirectory => Path.Combine(PiperDirectory, "models");
    private static string CatalogCachePath => Path.Combine(PiperDirectory, "catalog_cache.json");

    private static string ResolvePiperDirectory()
    {
        // 1. Check direct Data/piper from current directory
        var current = Path.Combine(Directory.GetCurrentDirectory(), "Data", "piper");
        if (Directory.Exists(current)) return current;

        // 2. Check PenguinTwitchBot/Data/piper from current directory (e.g. running from repo root)
        var repoCandidate = Path.Combine(Directory.GetCurrentDirectory(), "PenguinTwitchBot", "Data", "piper");
        if (Directory.Exists(repoCandidate)) return repoCandidate;

        // 3. Search up from AppContext.BaseDirectory
        var search = AppContext.BaseDirectory;
        for (var i = 0; i < 10; i++)
        {
            var candidate = Path.Combine(search, "Data", "piper");
            if (Directory.Exists(candidate)) return candidate;
            var projectCandidate = Path.Combine(search, "PenguinTwitchBot", "Data", "piper");
            if (Directory.Exists(projectCandidate)) return projectCandidate;
            var parent = Directory.GetParent(search);
            if (parent == null) break;
            search = parent.FullName;
        }

        return current;
    }

    private static string PiperExecutablePath
    {
        get
        {
            var exeName = OperatingSystem.IsWindows() ? "piper.exe" : "piper";
            // 1. Check extracted archive subdirectory (e.g. Data/piper/piper/piper)
            var subPath = Path.Combine(PiperDirectory, "piper", exeName);
            if (File.Exists(subPath)) return subPath;

            // 2. Check directly in PiperDirectory (e.g. Data/piper/piper)
            var directPath = Path.Combine(PiperDirectory, exeName);
            if (File.Exists(directPath)) return directPath;

            return subPath;
        }
    }

    public async Task<bool> EnsurePiperExecutableAsync()
    {
        if (File.Exists(PiperExecutablePath))
        {
            EnsureExecutablePermissions();
            return true;
        }

        await _execLock.WaitAsync();
        try
        {
            if (File.Exists(PiperExecutablePath))
            {
                EnsureExecutablePermissions();
                return true;
            }

            Directory.CreateDirectory(PiperDirectory);
            Directory.CreateDirectory(PiperModelsDirectory);

            // Clean up any incomplete extraction to avoid File.Copy IOException
            var subDir = Path.Combine(PiperDirectory, "piper");
            if (Directory.Exists(subDir))
            {
                try { Directory.Delete(subDir, recursive: true); } catch { }
            }

            logger.LogInformation("Downloading Piper executable (version {Version}) to {Directory}...", PinnedPiperReleaseVersion, PiperDirectory);
            using var downloadStream = await PiperDownloader.DownloadPiper(version: PinnedPiperReleaseVersion);
            using var memoryStream = new MemoryStream();
            await downloadStream.CopyToAsync(memoryStream);
            var archiveBytes = memoryStream.ToArray();

            var archiveName = GetExpectedArchiveName();
            if (!PiperArchiveSha256.TryGetValue(archiveName, out var expectedDigest))
            {
                logger.LogError("No trusted Piper checksum configured for platform archive '{ArchiveName}'.", archiveName);
                return false;
            }

            var actualDigest = Convert.ToHexStringLower(SHA256.HashData(archiveBytes));
            if (!string.Equals(actualDigest, expectedDigest, StringComparison.OrdinalIgnoreCase))
            {
                logger.LogError("Piper download checksum verification failed for '{ArchiveName}'. Expected {Expected}, got {Actual}",
                    archiveName, expectedDigest, actualDigest);
                return false;
            }

            memoryStream.Position = 0;
            memoryStream.ExtractPiper(PiperDirectory);
            EnsureExecutablePermissions();

            var success = File.Exists(PiperExecutablePath);
            if (success)
            {
                logger.LogInformation("Piper executable ready at {Path}", PiperExecutablePath);
            }
            else
            {
                logger.LogError("Piper executable not found after extraction at {Path}", PiperExecutablePath);
            }
            return success;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to download and extract Piper executable.");
            return false;
        }
        finally
        {
            _execLock.Release();
        }
    }

    private static void EnsureExecutablePermissions()
    {
        if (!OperatingSystem.IsWindows())
        {
            var dir = Path.GetDirectoryName(PiperExecutablePath);
            if (dir != null && Directory.Exists(dir))
            {
                foreach (var binName in new[] { "piper", "piper_phonemize", "espeak-ng" })
                {
                    var p = Path.Combine(dir, binName);
                    if (File.Exists(p))
                    {
                        try
                        {
                            File.SetUnixFileMode(p,
                                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                                UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                                UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
                        }
                        catch
                        {
                            // Ignore permission error if not supported
                        }
                    }
                }
            }
        }
    }

    private static string GetModelDirectory(string modelKey)
    {
        var nestedDir = Path.Combine(PiperModelsDirectory, modelKey);
        if (IsValidModelBundle(nestedDir, modelKey))
        {
            return nestedDir;
        }
        return PiperModelsDirectory;
    }

    public bool IsModelDownloaded(string modelKey)
    {
        var nestedDir = Path.Combine(PiperModelsDirectory, modelKey);
        if (IsValidModelBundle(nestedDir, modelKey)) return true;

        if (IsValidModelBundle(PiperModelsDirectory, modelKey)) return true;

        return false;
    }

    private static bool IsValidModelBundle(string directory, string modelKey)
    {
        if (!Directory.Exists(directory)) return false;

        var onnxPath = Path.Combine(directory, $"{modelKey}.onnx");
        var onnxJsonPath = Path.Combine(directory, $"{modelKey}.onnx.json");
        var modelJsonPath = Path.Combine(directory, "model.json");

        return File.Exists(onnxPath) && File.Exists(onnxJsonPath) && File.Exists(modelJsonPath);
    }

    public async Task<bool> DownloadVoiceAsync(string modelKey)
    {
        if (IsModelDownloaded(modelKey)) return true;

        await _modelLock.WaitAsync();
        try
        {
            if (IsModelDownloaded(modelKey)) return true;

            Directory.CreateDirectory(PiperModelsDirectory);
            logger.LogInformation("Downloading Piper voice model '{ModelKey}' to {Directory}...", modelKey, PiperModelsDirectory);
            var model = await PiperDownloader.GetModelByKey(modelKey);
            if (model == null)
            {
                logger.LogError("Piper voice model '{ModelKey}' not found.", modelKey);
                return false;
            }
            model = await model.DownloadModel(PiperModelsDirectory);
            _loadedModels[modelKey] = model;
            logger.LogInformation("Successfully downloaded Piper voice model '{ModelKey}'.", modelKey);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to download Piper voice model '{ModelKey}'.", modelKey);
            return false;
        }
        finally
        {
            _modelLock.Release();
        }
    }

    public async Task<IReadOnlyList<PiperVoiceInfo>> GetVoicesAsync()
    {
        List<PiperVoiceInfo>? staleCache = null;

        // Try reading cached catalog
        if (File.Exists(CatalogCachePath))
        {
            try
            {
                var fileInfo = new FileInfo(CatalogCachePath);
                var isFresh = DateTime.UtcNow - fileInfo.LastWriteTimeUtc < CatalogCacheTtl;
                var json = await File.ReadAllTextAsync(CatalogCachePath);
                var cached = JsonSerializer.Deserialize<List<PiperVoiceInfo>>(json);
                if (cached is { Count: > 0 })
                {
                    if (isFresh)
                    {
                        foreach (var v in cached)
                        {
                            v.IsDownloaded = IsModelDownloaded(v.Key);
                        }
                        return cached;
                    }

                    // Keep expired cache as fallback if refresh fails
                    staleCache = cached;
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to read cached Piper voices catalog.");
            }
        }

        // Fetch from Hugging Face
        try
        {
            logger.LogInformation("Fetching fresh Piper voice catalogue from Hugging Face...");
            var hfModels = await PiperDownloader.GetHuggingFaceModelList();
            if (hfModels != null && hfModels.Count > 0)
            {
                var voices = new List<PiperVoiceInfo>();
                foreach (var (key, model) in hfModels)
                {
                    voices.Add(ParseModelKey(key, model));
                }

                // Save to cache
                Directory.CreateDirectory(PiperDirectory);
                var serialized = JsonSerializer.Serialize(voices, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(CatalogCachePath, serialized);
                return voices;
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to fetch Piper voices from Hugging Face. Falling back to cached/built-in list.");
        }

        // If refreshing failed, fall back to stale cache if available
        if (staleCache is { Count: > 0 })
        {
            foreach (var v in staleCache)
            {
                v.IsDownloaded = IsModelDownloaded(v.Key);
            }
            return staleCache;
        }

        // Built-in fallback list of popular Piper models if Hugging Face cannot be reached and no cache exists
        var fallbackVoices = new List<PiperVoiceInfo>();
        var fallbackKeys = new[]
        {
            "en_US-lessac-medium", "en_US-lessac-low", "en_US-lessac-high",
            "en_US-amy-medium", "en_US-amy-low",
            "en_US-danny-low", "en_US-ryan-medium", "en_US-ryan-low",
            "en_GB-alan-medium", "en_GB-alan-low",
            "en_GB-alba-medium", "en_GB-cori-medium",
            "fr_FR-siwis-medium", "de_DE-thorsten-medium",
            "es_ES-carlfm-medium", "it_IT-riccardo-medium"
        };

        foreach (var key in fallbackKeys)
        {
            fallbackVoices.Add(ParseModelKey(key, null));
        }

        return fallbackVoices;
    }

    private PiperVoiceInfo ParseModelKey(string key, VoiceModel? model)
    {
        // Key format: {language}_{region}-{name}-{quality}
        // e.g. en_US-lessac-medium
        var parts = key.Split('-');
        var langPart = parts.Length > 0 ? parts[0].Replace('_', '-') : "en-US";
        var namePart = parts.Length > 1 ? parts[1] : key;
        var qualityPart = parts.Length > 2 ? parts[2] : "medium";

        // Format friendly name: e.g. "lessac (medium)"
        var friendlyName = $"{namePart} ({qualityPart})";

        return new PiperVoiceInfo
        {
            Key = key,
            Name = friendlyName,
            LanguageCode = langPart,
            Quality = qualityPart,
            Sex = BaseVoice.SexType.None,
            IsDownloaded = IsModelDownloaded(key)
        };
    }

    public async Task<byte[]> SynthesizeAsync(string message, string modelKey)
    {
        var execReady = await EnsurePiperExecutableAsync();
        if (!execReady)
        {
            throw new InvalidOperationException("Piper executable could not be initialized.");
        }

        var downloaded = await DownloadVoiceAsync(modelKey);
        if (!downloaded)
        {
            throw new InvalidOperationException($"Piper voice model '{modelKey}' is not available.");
        }

        if (!_loadedModels.TryGetValue(modelKey, out var model))
        {
            var modelDir = GetModelDirectory(modelKey);
            model = await VoiceModel.LoadModel(modelDir);
            _loadedModels[modelKey] = model;
        }

        var workingDir = Path.GetDirectoryName(PiperExecutablePath) ?? PiperDirectory;
        var provider = new PiperProvider(new PiperConfiguration
        {
            ExecutableLocation = PiperExecutablePath,
            WorkingDirectory = workingDir,
            Model = model
        });

        logger.LogInformation("Inferring Piper speech for voice '{ModelKey}'...", modelKey);
        return await provider.InferAsync(message, AudioOutputType.Wav);
    }
}
