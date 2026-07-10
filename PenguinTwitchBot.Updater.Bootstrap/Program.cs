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

        var updaterArgs = args.Length > 1 ? string.Join(' ', args.Skip(1).Select(QuoteArg)) : string.Empty;
        var startInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = updaterPath,
            Arguments = updaterArgs,
            UseShellExecute = false,
            WorkingDirectory = Path.GetDirectoryName(updaterPath) ?? Directory.GetCurrentDirectory()
        };

        using var process = System.Diagnostics.Process.Start(startInfo);
        if (process is null)
        {
            Console.Error.WriteLine("Failed to launch updater process.");
            return 4;
        }

        process.WaitForExit();
        return process.ExitCode;
    }

    private static string QuoteArg(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "\"\"";
        }

        return value.Contains(' ') || value.Contains('"')
            ? $"\"{value.Replace("\"", "\\\"")}\""
            : value;
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

            var normalized = entry.FullName.Replace('\\', '/');
            if (!normalized.StartsWith("Updater/", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (normalized.Contains("../", StringComparison.Ordinal))
            {
                continue;
            }

            if (bootstrapNames.Contains(entry.Name))
            {
                continue;
            }

            var relativePath = normalized.Replace('/', Path.DirectorySeparatorChar);
            var destinationPath = Path.Combine(absoluteAppRoot, relativePath);
            var destinationDir = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrWhiteSpace(destinationDir))
            {
                Directory.CreateDirectory(destinationDir);
            }

            entry.ExtractToFile(destinationPath, overwrite: true);
        }
    }
}
