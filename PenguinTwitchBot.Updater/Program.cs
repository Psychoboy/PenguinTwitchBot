using System.IO.Compression;
using System.Text.Json;

namespace PenguinTwitchBot.Updater;

internal static class Program
{
    private const string RecoveryManifestName = "recovery-manifest.json";
    private const string OperationApply = "apply";
    private const string OperationRestore = "restore";

    private static int Main(string[] args)
    {
        try
        {
            var options = ParseArgs(args);
            if (options is null)
            {
                PrintUsage();
                return 2;
            }

            var appRoot = Path.GetFullPath(options.AppRoot);

            if (!Directory.Exists(appRoot))
            {
                Console.Error.WriteLine($"App root not found: {appRoot}");
                return 3;
            }

            if (string.Equals(options.Operation, OperationRestore, StringComparison.OrdinalIgnoreCase))
            {
                var recoveryZip = Path.GetFullPath(options.RecoveryZip!);
                if (!File.Exists(recoveryZip))
                {
                    Console.Error.WriteLine($"Recovery zip not found: {recoveryZip}");
                    return 4;
                }

                RestoreFromRecoveryBundle(appRoot, recoveryZip);
                Console.WriteLine("Recovery restore completed successfully.");
                return 0;
            }

            var packagePath = Path.GetFullPath(options.PackagePath!);
            var recoveryRoot = Path.GetFullPath(options.RecoveryRoot!);

            if (!File.Exists(packagePath))
            {
                Console.Error.WriteLine($"Update package not found: {packagePath}");
                return 4;
            }

            Directory.CreateDirectory(recoveryRoot);

            var entries = ReadSafeEntries(packagePath);
            if (entries.Count == 0)
            {
                Console.Error.WriteLine("Update package has no files to apply.");
                return 5;
            }

            var updateTag = options.TargetVersion ?? "unknown";
            var recoveryFile = Path.Combine(recoveryRoot, $"recovery-{DateTime.UtcNow:yyyyMMddHHmmss}-{updateTag}.zip");
            CreateRecoveryBundle(appRoot, entries, recoveryFile, options);

            Console.WriteLine($"Recovery bundle created: {recoveryFile}");

            ApplyUpdate(packagePath, appRoot, entries);
            Console.WriteLine("Update applied successfully.");

            if (!string.IsNullOrWhiteSpace(options.RestartCommand))
            {
                StartProcess(options.RestartCommand!);
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Updater failed: {ex.Message}");
            return 1;
        }
    }

    private static void RestoreFromRecoveryBundle(string appRoot, string recoveryZip)
    {
        var restoreTemp = Path.Combine(Path.GetTempPath(), $"ptb-restore-{Guid.NewGuid():N}");
        Directory.CreateDirectory(restoreTemp);

        try
        {
            ZipFile.ExtractToDirectory(recoveryZip, restoreTemp, overwriteFiles: true);

            var manifestPath = Path.Combine(restoreTemp, RecoveryManifestName);
            if (!File.Exists(manifestPath))
            {
                throw new InvalidOperationException($"Recovery manifest not found in bundle: {RecoveryManifestName}");
            }

            var manifest = JsonSerializer.Deserialize<RecoveryManifest>(File.ReadAllText(manifestPath));
            if (manifest?.Files is null || manifest.Files.Count == 0)
            {
                throw new InvalidOperationException("Recovery manifest does not contain files to restore.");
            }

            foreach (var file in manifest.Files)
            {
                if (string.IsNullOrWhiteSpace(file.Path))
                {
                    continue;
                }

                var normalized = file.Path.Replace('\\', '/');
                if (normalized.StartsWith("/", StringComparison.Ordinal) || normalized.Contains("../", StringComparison.Ordinal) || normalized.Contains("..\\", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException($"Unsafe recovery path: {file.Path}");
                }

                var sourcePath = Path.Combine(restoreTemp, normalized.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(sourcePath))
                {
                    continue;
                }

                var destinationPath = Path.Combine(appRoot, normalized.Replace('/', Path.DirectorySeparatorChar));
                var destinationDir = Path.GetDirectoryName(destinationPath);
                if (!string.IsNullOrWhiteSpace(destinationDir))
                {
                    Directory.CreateDirectory(destinationDir);
                }

                File.Copy(sourcePath, destinationPath, overwrite: true);
            }

            var databaseDir = Path.Combine(restoreTemp, "database");
            if (Directory.Exists(databaseDir))
            {
                var destinationDbBackupDir = Path.Combine(appRoot, "Data", "backups");
                Directory.CreateDirectory(destinationDbBackupDir);

                foreach (var dbBackup in Directory.GetFiles(databaseDir, "*.zip", SearchOption.TopDirectoryOnly))
                {
                    var targetPath = Path.Combine(destinationDbBackupDir, Path.GetFileName(dbBackup));
                    File.Copy(dbBackup, targetPath, overwrite: true);
                }
            }
        }
        finally
        {
            if (Directory.Exists(restoreTemp))
            {
                Directory.Delete(restoreTemp, true);
            }
        }
    }

    private static void ApplyUpdate(string packagePath, string appRoot, IReadOnlyList<string> safeEntries)
    {
        using var archive = ZipFile.OpenRead(packagePath);
        foreach (var relativePath in safeEntries)
        {
            var entry = archive.GetEntry(relativePath.Replace('\\', '/'));
            if (entry is null)
            {
                continue;
            }

            var destinationPath = Path.Combine(appRoot, relativePath);
            var destinationDir = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrWhiteSpace(destinationDir))
            {
                Directory.CreateDirectory(destinationDir);
            }

            entry.ExtractToFile(destinationPath, overwrite: true);
        }
    }

    private static void CreateRecoveryBundle(string appRoot, IReadOnlyList<string> entries, string outputZip, Options options)
    {
        using var recoveryArchive = ZipFile.Open(outputZip, ZipArchiveMode.Create);
        var manifest = new RecoveryManifest
        {
            SourceVersion = options.CurrentVersion ?? "unknown",
            TargetVersion = options.TargetVersion ?? "unknown",
            Rid = options.Rid ?? "unknown",
            CreatedUtc = DateTime.UtcNow,
            Files = []
        };

        foreach (var relativePath in entries)
        {
            var sourceFile = Path.Combine(appRoot, relativePath);
            if (!File.Exists(sourceFile))
            {
                continue;
            }

            recoveryArchive.CreateEntryFromFile(sourceFile, relativePath, CompressionLevel.Optimal);
            var fileInfo = new FileInfo(sourceFile);
            manifest.Files.Add(new RecoveryFile
            {
                Path = relativePath,
                Size = fileInfo.Length,
                LastWriteUtc = fileInfo.LastWriteTimeUtc
            });
        }

        if (!string.IsNullOrWhiteSpace(options.DatabaseBackupPath) && File.Exists(options.DatabaseBackupPath))
        {
            recoveryArchive.CreateEntryFromFile(options.DatabaseBackupPath, Path.Combine("database", Path.GetFileName(options.DatabaseBackupPath)), CompressionLevel.Optimal);
            manifest.DatabaseBackup = options.DatabaseBackupPath;
        }

        var manifestEntry = recoveryArchive.CreateEntry(RecoveryManifestName, CompressionLevel.Optimal);
        using var stream = manifestEntry.Open();
        JsonSerializer.Serialize(stream, manifest, new JsonSerializerOptions { WriteIndented = true });
    }

    private static List<string> ReadSafeEntries(string packagePath)
    {
        var entries = new List<string>();
        using var archive = ZipFile.OpenRead(packagePath);

        foreach (var entry in archive.Entries)
        {
            if (string.IsNullOrWhiteSpace(entry.Name))
            {
                continue;
            }

            var normalized = entry.FullName.Replace('\\', '/');
            if (normalized.StartsWith("/", StringComparison.Ordinal) || normalized.Contains("../", StringComparison.Ordinal) || normalized.Contains("..\\", StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Unsafe entry path: {entry.FullName}");
            }

            if (normalized.StartsWith("Updater/", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (string.Equals(normalized, "update-manifest.json", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            entries.Add(normalized.Replace('/', Path.DirectorySeparatorChar));
        }

        return entries;
    }

    private static void StartProcess(string commandLine)
    {
        var firstSpace = commandLine.IndexOf(' ');
        if (firstSpace < 0)
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(commandLine)
            {
                UseShellExecute = true,
                WorkingDirectory = Directory.GetCurrentDirectory()
            });
            return;
        }

        var file = commandLine[..firstSpace];
        var args = commandLine[(firstSpace + 1)..];
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(file, args)
        {
            UseShellExecute = true,
            WorkingDirectory = Directory.GetCurrentDirectory()
        });
    }

    private static Options? ParseArgs(string[] args)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < args.Length - 1; i += 2)
        {
            if (!args[i].StartsWith("--", StringComparison.Ordinal))
            {
                continue;
            }

            map[args[i][2..]] = args[i + 1];
        }

        if (!map.TryGetValue("app-root", out var appRoot))
        {
            return null;
        }

        map.TryGetValue("operation", out var operation);
        operation = string.IsNullOrWhiteSpace(operation) ? OperationApply : operation;

        map.TryGetValue("package", out var package);
        map.TryGetValue("recovery-root", out var recoveryRoot);
        map.TryGetValue("recovery-zip", out var recoveryZip);

        if (string.Equals(operation, OperationApply, StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(package) || string.IsNullOrWhiteSpace(recoveryRoot))
            {
                return null;
            }
        }
        else if (string.Equals(operation, OperationRestore, StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(recoveryZip))
            {
                return null;
            }
        }
        else
        {
            return null;
        }

        map.TryGetValue("current-version", out var currentVersion);
        map.TryGetValue("target-version", out var targetVersion);
        map.TryGetValue("rid", out var rid);
        map.TryGetValue("database-backup", out var databaseBackupPath);
        map.TryGetValue("restart-command", out var restartCommand);

        return new Options
        {
            Operation = operation,
            AppRoot = appRoot,
            PackagePath = package,
            RecoveryRoot = recoveryRoot,
            RecoveryZip = recoveryZip,
            CurrentVersion = currentVersion,
            TargetVersion = targetVersion,
            Rid = rid,
            DatabaseBackupPath = databaseBackupPath,
            RestartCommand = restartCommand
        };
    }

    private static void PrintUsage()
    {
        Console.WriteLine("Apply usage: PenguinTwitchBot.Updater --operation apply --app-root <path> --package <zip> --recovery-root <path> [--current-version <v>] [--target-version <v>] [--rid <rid>] [--database-backup <path>] [--restart-command <cmd>]");
        Console.WriteLine("Restore usage: PenguinTwitchBot.Updater --operation restore --app-root <path> --recovery-zip <zip>");
    }

    private sealed class Options
    {
        public string Operation { get; init; } = OperationApply;
        public string AppRoot { get; init; } = string.Empty;
        public string? PackagePath { get; init; }
        public string? RecoveryRoot { get; init; }
        public string? RecoveryZip { get; init; }
        public string? CurrentVersion { get; init; }
        public string? TargetVersion { get; init; }
        public string? Rid { get; init; }
        public string? DatabaseBackupPath { get; init; }
        public string? RestartCommand { get; init; }
    }

    private sealed class RecoveryManifest
    {
        public string SourceVersion { get; set; } = string.Empty;
        public string TargetVersion { get; set; } = string.Empty;
        public string Rid { get; set; } = string.Empty;
        public DateTime CreatedUtc { get; set; }
        public string? DatabaseBackup { get; set; }
        public List<RecoveryFile> Files { get; set; } = [];
    }

    private sealed class RecoveryFile
    {
        public string Path { get; set; } = string.Empty;
        public long Size { get; set; }
        public DateTime LastWriteUtc { get; set; }
    }
}
