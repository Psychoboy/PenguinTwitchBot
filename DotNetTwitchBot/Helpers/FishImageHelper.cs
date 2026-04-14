namespace DotNetTwitchBot.Helpers
{
    /// <summary>
    /// Helper class for working with processed fish images
    /// </summary>
    public static class FishImageHelper
    {
        /// <summary>
        /// Get the path for a specific size of fish image
        /// </summary>
        /// <param name="baseFileName">The base filename (e.g., "salmon_medium.webp")</param>
        /// <param name="size">The desired size (thumbnail, small, medium, large)</param>
        /// <returns>The path to the image</returns>
        public static string GetImagePath(string? baseFileName, ImageSize size = ImageSize.Medium)
        {
            if (string.IsNullOrEmpty(baseFileName))
                return string.Empty;

            // If the filename already includes a size suffix, extract the base name
            var baseName = GetBaseFileName(baseFileName);
            var extension = GetExtension(baseFileName);

            var sizeSuffix = size switch
            {
                ImageSize.Thumbnail => "_thumbnail",
                ImageSize.Small => "_small",
                ImageSize.Medium => "_medium",
                ImageSize.Large => "_large",
                ImageSize.Original => "",
                _ => "_medium"
            };

            return $"/fishes/{baseName}{sizeSuffix}{extension}";
        }

        /// <summary>
        /// Get the base filename without size suffix
        /// </summary>
        private static string GetBaseFileName(string fileName)
        {
            var nameWithoutExt = System.IO.Path.GetFileNameWithoutExtension(fileName);

            // Remove size suffixes if present
            var suffixes = new[] { "_thumbnail", "_small", "_medium", "_large" };
            foreach (var suffix in suffixes)
            {
                if (nameWithoutExt.EndsWith(suffix))
                {
                    return nameWithoutExt.Substring(0, nameWithoutExt.Length - suffix.Length);
                }
            }

            return nameWithoutExt;
        }

        /// <summary>
        /// Get the base filename without size suffix or extension (public version for use in other classes)
        /// </summary>
        /// <param name="fileName">The filename with or without size suffix</param>
        /// <returns>The base name without size suffix or extension</returns>
        public static string GetBaseName(string? fileName)
        {
            if (string.IsNullOrEmpty(fileName))
                return string.Empty;

            return GetBaseFileName(fileName);
        }

        /// <summary>
        /// Get the file extension, defaulting to .webp for processed images
        /// </summary>
        private static string GetExtension(string fileName)
        {
            var ext = System.IO.Path.GetExtension(fileName);
            
            // If it's a processed image format, use webp
            var processedExts = new[] { ".webp", ".png", ".jpg", ".jpeg", ".gif" };
            if (string.IsNullOrEmpty(ext) || !processedExts.Contains(ext.ToLowerInvariant()))
            {
                return ".webp";
            }
            
            // For processed images, always use webp
            return ".webp";
        }

        /// <summary>
        /// Get srcset attribute for responsive images
        /// </summary>
        /// <param name="baseFileName">The base filename</param>
        /// <returns>A srcset string for responsive images</returns>
        public static string GetSrcSet(string? baseFileName)
        {
            if (string.IsNullOrEmpty(baseFileName))
                return string.Empty;

            var baseName = GetBaseFileName(baseFileName);
            var ext = ".webp";

            return $"/fishes/{baseName}_small{ext} 200w, " +
                   $"/fishes/{baseName}_medium{ext} 400w, " +
                   $"/fishes/{baseName}_large{ext} 800w";
        }
    }

    public enum ImageSize
    {
        Thumbnail,  // 100x100
        Small,      // 200x200
        Medium,     // 400x400
        Large,      // 800x800
        Original    // Original file
    }
}
