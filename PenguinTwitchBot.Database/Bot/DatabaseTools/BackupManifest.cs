namespace PenguinTwitchBot.Database.Bot.DatabaseTools
{
    public sealed class BackupManifest
    {
        public const int CurrentFormatVersion = 2;
        public const string ManifestFileName = "manifest.json";

        public int FormatVersion { get; set; } = CurrentFormatVersion;
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public string? DatabaseProvider { get; set; }
        public string? LatestMigrationId { get; set; }
        public Dictionary<string, BackupTableManifestEntry> Tables { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, BackupMediaManifestEntry> MediaFiles { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }

    public sealed class BackupTableManifestEntry
    {
        public string EntityTypeName { get; set; } = string.Empty;
        public string TableName { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public int RowCount { get; set; }
        public string Sha256 { get; set; } = string.Empty;
    }

    public sealed class BackupMediaManifestEntry
    {
        public string RelativePath { get; set; } = string.Empty;
        public long SizeBytes { get; set; }
        public string Sha256 { get; set; } = string.Empty;
    }
}

