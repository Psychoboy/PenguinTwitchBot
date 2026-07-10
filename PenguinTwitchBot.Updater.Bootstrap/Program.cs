using System.IO.Compression;

namespace PenguinTwitchBot.Updater.Bootstrap;

internal static class Program
{
    private static int Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("Bootstrap requires updater executable path as the first argument.");
            return 2;
        }

        var updaterPath = Path.GetFullPath(args[0]);
        if (!File.Exists(updaterPath))
        {
            Console.Error.WriteLine($"Updater executable not found: {updaterPath}");
            return 3;
        }

        StageUpdaterFilesFromPackage(args.Skip(1).ToArray());

        var startInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = updaterPath,
            UseShellExecute = false,
            WorkingDirectory = Path.GetDirectoryName(updaterPath) ?? Directory.GetCurrentDirectory()
        };

        foreach (var arg in args.Skip(1))
        {
            startInfo.ArgumentList.Add(arg);
        }

        using var process = System.Diagnostics.Process.Start(startInfo);
        if (process is null)
        {
            Console.Error.WriteLine("Failed to launch updater process.");
            return 4;
        }

        process.WaitForExit();
        return process.ExitCode;
    }

    private static void StageUpdaterFilesFromPackage(string[] args)
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

        if (!map.TryGetValue("package", out var packagePath) || !map.TryGetValue("app-root", out var appRoot))
        {
            return;
        }

        var absolutePackage = Path.GetFullPath(packagePath);
        var absoluteAppRoot = Path.GetFullPath(appRoot);
        if (!File.Exists(absolutePackage) || !Directory.Exists(absoluteAppRoot))
        {
            return;
        }

        var bootstrapNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "PenguinTwitchBot.Updater.Bootstrap",
            "PenguinTwitchBot.Updater.Bootstrap.exe"
        };

        using var archive = System.IO.Compression.ZipFile.OpenRead(absolutePackage);
        foreach (var entry in archive.Entries)
        {
            if (string.IsNullOrWhiteSpace(entry.Name))
            {
                continue;
            }

            var normalized = GetSafeArchiveEntryPath(entry.FullName);
            if (!normalized.StartsWith("Updater/", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (bootstrapNames.Contains(entry.Name))
            {
                continue;
            }

            var destinationPath = GetSafeDestinationPath(absoluteAppRoot, normalized.Replace('/', Path.DirectorySeparatorChar));
            var destinationDir = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrWhiteSpace(destinationDir))
            {
                Directory.CreateDirectory(destinationDir);
            }

            entry.ExtractToFile(destinationPath, overwrite: true);
        }
    }

    private static string GetSafeArchiveEntryPath(string entryPath)
    {
        var normalized = entryPath.Replace('\\', '/');
        if (normalized.StartsWith("/", StringComparison.Ordinal) || normalized.Contains("../", StringComparison.Ordinal) || normalized.Contains("..\\", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Unsafe entry path: {entryPath}");
        }

        return normalized;
    }

    private static string GetSafeDestinationPath(string rootPath, string relativePath)
    {
        var fullRoot = Path.GetFullPath(rootPath);
        var fullPath = Path.GetFullPath(Path.Combine(fullRoot, relativePath));
        if (!fullPath.StartsWith(fullRoot + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Resolved path escapes root: {relativePath}");
        }

        return fullPath;
    }
}
