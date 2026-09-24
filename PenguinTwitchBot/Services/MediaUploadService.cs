using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;

namespace PenguinTwitchBot.Services
{
    public class MediaUploadResult
    {
        public bool Success { get; set; }
        public string? BaseName { get; set; }
        public string? FileName { get; set; }
        public string? ErrorMessage { get; set; }

        public static MediaUploadResult Ok(string baseName, string fileName) =>
            new() { Success = true, BaseName = baseName, FileName = fileName };

        public static MediaUploadResult Fail(string error) =>
            new() { Success = false, ErrorMessage = error };
    }

    public class MediaUploadService
    {
        private readonly string _webRootPath;
        private readonly ILogger<MediaUploadService> _logger;

        public static readonly string[] SupportedAudioExtensions =
            [".mp3", ".wav", ".ogg", ".aac", ".opus", ".webm", ".m4a"];

        public static readonly string[] SupportedImageExtensions =
            [".gif", ".png", ".jpg", ".jpeg", ".webp"];

        public static readonly string[] SupportedVideoExtensions =
            [".webm", ".mp4", ".ogg", ".ogv"];

        public MediaUploadService(IWebHostEnvironment? env, ILogger<MediaUploadService> logger)
        {
            _logger = logger;
            _webRootPath = env?.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        }

        // Constructor for testing with explicit webRootPath
        public MediaUploadService(string webRootPath, ILogger<MediaUploadService> logger)
        {
            _webRootPath = webRootPath;
            _logger = logger;
        }

        public string AudioDirectory => Path.Combine(_webRootPath, "audio");
        public string GifsDirectory => Path.Combine(_webRootPath, "gifs");

        #region Audio Management (Audio Commands & PlaySound)

        public List<string> GetAudioFiles()
        {
            try
            {
                if (!Directory.Exists(AudioDirectory))
                {
                    return [];
                }

                return Directory.GetFiles(AudioDirectory)
                    .Where(f => SupportedAudioExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                    .Select(Path.GetFileNameWithoutExtension)
                    .Where(f => !string.IsNullOrWhiteSpace(f))
                    .Select(f => f!)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(f => f)
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading audio files from {AudioDirectory}", AudioDirectory);
                return [];
            }
        }

        public string? ResolveAudioUrl(string? soundName)
        {
            if (string.IsNullOrWhiteSpace(soundName))
                return null;

            if (!Directory.Exists(AudioDirectory))
                return null;

            // If soundName already has an extension
            var ext = Path.GetExtension(soundName);
            if (!string.IsNullOrEmpty(ext))
            {
                var directPath = Path.Combine(AudioDirectory, soundName);
                if (File.Exists(directPath))
                {
                    return $"/audio/{Uri.EscapeDataString(soundName)}";
                }
            }

            // Look for matching files with supported audio extensions
            var baseName = Path.GetFileNameWithoutExtension(soundName);
            foreach (var audioExt in SupportedAudioExtensions)
            {
                var candidate = Path.Combine(AudioDirectory, $"{baseName}{audioExt}");
                if (File.Exists(candidate))
                {
                    var actualFileName = Path.GetFileName(candidate);
                    return $"/audio/{Uri.EscapeDataString(actualFileName)}";
                }
            }

            return null;
        }

        public async Task<MediaUploadResult> UploadAudioAsync(Stream stream, string originalFileName, long maxSizeBytes = 25 * 1024 * 1024)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(originalFileName))
                    return MediaUploadResult.Fail("File name cannot be empty.");

                var extension = Path.GetExtension(originalFileName).ToLowerInvariant();
                if (!SupportedAudioExtensions.Contains(extension))
                {
                    return MediaUploadResult.Fail($"Unsupported audio format: '{extension}'. Supported formats: {string.Join(", ", SupportedAudioExtensions)}");
                }

                var sanitizedBase = SanitizeFileName(Path.GetFileNameWithoutExtension(originalFileName));
                if (string.IsNullOrWhiteSpace(sanitizedBase))
                    sanitizedBase = $"audio_{DateTime.UtcNow.Ticks}";

                var sanitizedFileName = $"{sanitizedBase}{extension}";

                Directory.CreateDirectory(AudioDirectory);
                var targetPath = Path.Combine(AudioDirectory, sanitizedFileName);

                using var memoryStream = new MemoryStream();
                await stream.CopyToAsync(memoryStream);
                var data = memoryStream.ToArray();

                if (data.Length == 0)
                    return MediaUploadResult.Fail("The uploaded file is empty.");

                if (data.Length > maxSizeBytes)
                    return MediaUploadResult.Fail($"File size ({data.Length / (1024 * 1024.0):F1} MB) exceeds maximum allowed size ({maxSizeBytes / (1024 * 1024)} MB).");

                if (!ValidateAudioSignature(data, extension))
                {
                    return MediaUploadResult.Fail("File header verification failed. The file is corrupted or not a valid audio file.");
                }

                await File.WriteAllBytesAsync(targetPath, data);
                _logger.LogInformation("Saved audio file: {TargetPath}", targetPath);

                return MediaUploadResult.Ok(sanitizedBase, sanitizedFileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to upload audio file {OriginalFileName}", originalFileName);
                return MediaUploadResult.Fail($"Upload failed: {ex.Message}");
            }
        }

        #endregion

        #region Alert Media Management (Images, Videos, Companion Sounds)

        public List<string> GetAlertMediaFiles()
        {
            try
            {
                if (!Directory.Exists(GifsDirectory))
                {
                    return [];
                }

                var allowedExtensions = SupportedImageExtensions.Concat(SupportedVideoExtensions).ToHashSet(StringComparer.OrdinalIgnoreCase);

                return Directory.GetFiles(GifsDirectory)
                    .Where(f => allowedExtensions.Contains(Path.GetExtension(f)))
                    .Select(Path.GetFileName)
                    .Where(f => !string.IsNullOrWhiteSpace(f))
                    .Select(f => f!)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(f => f)
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading alert media files from {GifsDirectory}", GifsDirectory);
                return [];
            }
        }

        public string? GetCompanionAudioFileName(string? mediaFileName)
        {
            if (string.IsNullOrWhiteSpace(mediaFileName) || !Directory.Exists(GifsDirectory))
                return null;

            var baseName = Path.GetFileNameWithoutExtension(mediaFileName);

            foreach (var ext in SupportedAudioExtensions)
            {
                var candidate = Path.Combine(GifsDirectory, $"{baseName}{ext}");
                if (File.Exists(candidate))
                {
                    return Path.GetFileName(candidate);
                }
            }

            return null;
        }

        public string? ResolveAlertMediaUrl(string? mediaFileName)
        {
            if (string.IsNullOrWhiteSpace(mediaFileName) || !Directory.Exists(GifsDirectory))
                return null;

            var path = Path.Combine(GifsDirectory, mediaFileName);
            if (File.Exists(path))
            {
                return $"/gifs/{Uri.EscapeDataString(mediaFileName)}";
            }

            return null;
        }

        public string? ResolveAlertSoundUrl(string? soundFileName)
        {
            if (string.IsNullOrWhiteSpace(soundFileName) || !Directory.Exists(GifsDirectory))
                return null;

            var path = Path.Combine(GifsDirectory, soundFileName);
            if (File.Exists(path))
            {
                return $"/gifs/{Uri.EscapeDataString(soundFileName)}";
            }

            return null;
        }

        public async Task<MediaUploadResult> UploadAlertMediaAsync(Stream stream, string originalFileName, long maxSizeBytes = 50 * 1024 * 1024)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(originalFileName))
                    return MediaUploadResult.Fail("File name cannot be empty.");

                var extension = Path.GetExtension(originalFileName).ToLowerInvariant();
                var isImage = SupportedImageExtensions.Contains(extension);
                var isVideo = SupportedVideoExtensions.Contains(extension);

                if (!isImage && !isVideo)
                {
                    var allAllowed = SupportedImageExtensions.Concat(SupportedVideoExtensions);
                    return MediaUploadResult.Fail($"Unsupported media format: '{extension}'. Supported formats: {string.Join(", ", allAllowed)}");
                }

                var sanitizedBase = SanitizeFileName(Path.GetFileNameWithoutExtension(originalFileName));
                if (string.IsNullOrWhiteSpace(sanitizedBase))
                    sanitizedBase = $"alert_{DateTime.UtcNow.Ticks}";

                var sanitizedFileName = $"{sanitizedBase}{extension}";

                Directory.CreateDirectory(GifsDirectory);
                var targetPath = Path.Combine(GifsDirectory, sanitizedFileName);

                using var memoryStream = new MemoryStream();
                await stream.CopyToAsync(memoryStream);
                var data = memoryStream.ToArray();

                if (data.Length == 0)
                    return MediaUploadResult.Fail("The uploaded file is empty.");

                if (data.Length > maxSizeBytes)
                    return MediaUploadResult.Fail($"File size ({data.Length / (1024 * 1024.0):F1} MB) exceeds maximum allowed size ({maxSizeBytes / (1024 * 1024)} MB).");

                var valid = isImage
                    ? ValidateImageSignature(data, extension)
                    : ValidateVideoSignature(data, extension);

                if (!valid)
                {
                    return MediaUploadResult.Fail("File header verification failed. The file is corrupted or not a valid media file.");
                }

                await File.WriteAllBytesAsync(targetPath, data);
                _logger.LogInformation("Saved alert media file: {TargetPath}", targetPath);

                return MediaUploadResult.Ok(sanitizedBase, sanitizedFileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to upload alert media {OriginalFileName}", originalFileName);
                return MediaUploadResult.Fail($"Upload failed: {ex.Message}");
            }
        }

        public async Task<MediaUploadResult> UploadAlertCompanionAudioAsync(Stream stream, string baseName, string originalFileName, long maxSizeBytes = 25 * 1024 * 1024)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(baseName))
                    return MediaUploadResult.Fail("A base media file must be specified to upload companion audio.");

                var sanitizedBase = SanitizeFileName(Path.GetFileNameWithoutExtension(baseName));
                if (string.IsNullOrWhiteSpace(sanitizedBase))
                    return MediaUploadResult.Fail("Invalid base media filename.");

                var audioExtension = Path.GetExtension(originalFileName).ToLowerInvariant();
                if (!SupportedAudioExtensions.Contains(audioExtension))
                {
                    return MediaUploadResult.Fail($"Unsupported companion audio format: '{audioExtension}'. Supported: {string.Join(", ", SupportedAudioExtensions)}");
                }

                Directory.CreateDirectory(GifsDirectory);

                using var memoryStream = new MemoryStream();
                await stream.CopyToAsync(memoryStream);
                var data = memoryStream.ToArray();

                if (data.Length == 0)
                    return MediaUploadResult.Fail("The companion audio file is empty.");

                if (data.Length > maxSizeBytes)
                    return MediaUploadResult.Fail($"Audio size ({data.Length / (1024 * 1024.0):F1} MB) exceeds limit ({maxSizeBytes / (1024 * 1024)} MB).");

                if (!ValidateAudioSignature(data, audioExtension))
                {
                    return MediaUploadResult.Fail("Audio file header verification failed. The file is corrupted or not a valid audio file.");
                }

                // Delete any existing companion audio files with different extensions for this baseName
                foreach (var ext in SupportedAudioExtensions)
                {
                    var existingFile = Path.Combine(GifsDirectory, $"{sanitizedBase}{ext}");
                    if (File.Exists(existingFile))
                    {
                        try { File.Delete(existingFile); } catch { /* Ignore cleanup issues */ }
                    }
                }

                var targetFileName = $"{sanitizedBase}{audioExtension}";
                var targetPath = Path.Combine(GifsDirectory, targetFileName);

                await File.WriteAllBytesAsync(targetPath, data);
                _logger.LogInformation("Saved alert companion audio file: {TargetPath}", targetPath);

                return MediaUploadResult.Ok(sanitizedBase, targetFileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to upload alert companion audio for base {BaseName}", baseName);
                return MediaUploadResult.Fail($"Upload failed: {ex.Message}");
            }
        }

        #endregion

        #region Verification & Helpers

        private static readonly HashSet<char> UnsafeFileNameChars = new([
            '/', '\\', ':', '*', '?', '"', '<', '>', '|', '\0', '\r', '\n', '\t'
        ]);

        public static string SanitizeFileName(string fileName)
        {
            var invalidChars = Path.GetInvalidFileNameChars().Concat(UnsafeFileNameChars).ToHashSet();
            var cleaned = new string(fileName
                .Trim()
                .Select(c => invalidChars.Contains(c) ? '_' : c)
                .ToArray());

            // Remove path separators and traversal attempts
            cleaned = cleaned.Replace("/", "_").Replace("\\", "_").Replace("..", "_");
            return cleaned;
        }

        public static bool ValidateAudioSignature(byte[] data, string extension)
        {
            if (data.Length < 4) return false;

            return extension.ToLowerInvariant() switch
            {
                ".mp3" => IsMp3(data),
                ".wav" => IsWav(data),
                ".ogg" or ".opus" or ".oga" => IsOgg(data),
                ".aac" => IsAac(data),
                ".webm" => IsEbml(data),
                ".m4a" or ".mp4" => IsM4a(data),
                _ => false
            };
        }

        public static bool ValidateImageSignature(byte[] data, string extension)
        {
            if (data.Length < 4) return false;

            return extension.ToLowerInvariant() switch
            {
                ".gif" => data.Length >= 6 &&
                          data[0] == 'G' && data[1] == 'I' && data[2] == 'F' && data[3] == '8' &&
                          (data[4] == '7' || data[4] == '9') && data[5] == 'a',
                ".png" => data.Length >= 8 &&
                          data[0] == 0x89 && data[1] == 0x50 && data[2] == 0x4E && data[3] == 0x47 &&
                          data[4] == 0x0D && data[5] == 0x0A && data[6] == 0x1A && data[7] == 0x0A,
                ".jpg" or ".jpeg" => data.Length >= 3 &&
                                     data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF,
                ".webp" => data.Length >= 12 &&
                           data[0] == 'R' && data[1] == 'I' && data[2] == 'F' && data[3] == 'F' &&
                           data[8] == 'W' && data[9] == 'E' && data[10] == 'B' && data[11] == 'P',
                _ => false
            };
        }

        public static bool ValidateVideoSignature(byte[] data, string extension)
        {
            if (data.Length < 4) return false;

            return extension.ToLowerInvariant() switch
            {
                ".webm" => IsEbml(data),
                ".ogg" or ".ogv" => IsOgg(data),
                ".mp4" => IsM4a(data),
                _ => false
            };
        }

        private static bool IsMp3(byte[] data)
        {
            // ID3v2 tag
            if (data.Length >= 3 && data[0] == 0x49 && data[1] == 0x44 && data[2] == 0x33)
                return true;

            // Raw MPEG sync frame (0xFF followed by 0b111xxxxx)
            for (int i = 0; i < Math.Min(data.Length - 1, 1024); i++)
            {
                if (data[i] == 0xFF && (data[i + 1] & 0xE0) == 0xE0)
                    return true;
            }

            return false;
        }

        private static bool IsWav(byte[] data)
        {
            return data.Length >= 12 &&
                   data[0] == 'R' && data[1] == 'I' && data[2] == 'F' && data[3] == 'F' &&
                   data[8] == 'W' && data[9] == 'A' && data[10] == 'V' && data[11] == 'E';
        }

        private static bool IsOgg(byte[] data)
        {
            return data.Length >= 4 &&
                   data[0] == 'O' && data[1] == 'g' && data[2] == 'g' && data[3] == 'S';
        }

        private static bool IsEbml(byte[] data)
        {
            // EBML header (used by WebM and MKV)
            return data.Length >= 4 &&
                   data[0] == 0x1A && data[1] == 0x45 && data[2] == 0xDF && data[3] == 0xA3;
        }

        private static bool IsAac(byte[] data)
        {
            // ADTS frame header (0xFF 0xF1 or 0xFF 0xF9) or ADIF header
            if (data.Length >= 2 && data[0] == 0xFF && (data[1] == 0xF1 || data[1] == 0xF9 || (data[1] & 0xF6) == 0xF0))
                return true;

            if (data.Length >= 4 && data[0] == 'A' && data[1] == 'D' && data[2] == 'I' && data[3] == 'F')
                return true;

            return false;
        }

        private static bool IsM4a(byte[] data)
        {
            // ISO Base Media File Format (MP4/M4A): bytes 4-7 are 'ftyp'
            if (data.Length >= 8 &&
                data[4] == 'f' && data[5] == 't' && data[6] == 'y' && data[7] == 'p')
                return true;

            return false;
        }

        #endregion
    }
}
