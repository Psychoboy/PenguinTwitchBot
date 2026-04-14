using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Jpeg;

namespace DotNetTwitchBot.Services
{
    public class ImageProcessingService
    {
        private readonly ILogger<ImageProcessingService> _logger;

        public ImageProcessingService(ILogger<ImageProcessingService> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Process an uploaded image and create optimized versions
        /// </summary>
        /// <param name="sourceStream">The source image stream</param>
        /// <param name="outputDirectory">Directory to save processed images</param>
        /// <param name="baseFileName">Base filename without extension</param>
        /// <returns>Dictionary with size names and their corresponding filenames</returns>
        public async Task<ImageProcessingResult> ProcessImageAsync(
            Stream sourceStream, 
            string outputDirectory, 
            string baseFileName)
        {
            try
            {
                Directory.CreateDirectory(outputDirectory);

                using var image = await Image.LoadAsync(sourceStream);
                
                var result = new ImageProcessingResult
                {
                    OriginalWidth = image.Width,
                    OriginalHeight = image.Height
                };

                // Define size configurations
                var sizes = new Dictionary<string, ImageSizeConfig>
                {
                    { "thumbnail", new ImageSizeConfig(100, 100, 80) },
                    { "small", new ImageSizeConfig(200, 200, 85) },
                    { "medium", new ImageSizeConfig(400, 400, 90) },
                    { "large", new ImageSizeConfig(800, 800, 95) }
                };

                foreach (var (sizeName, config) in sizes)
                {
                    var fileName = $"{baseFileName}_{sizeName}.webp";
                    var filePath = Path.Combine(outputDirectory, fileName);

                    using var outputImage = image.Clone(ctx =>
                    {
                        // Resize while maintaining aspect ratio
                        var resizeOptions = new ResizeOptions
                        {
                            Size = new Size(config.MaxWidth, config.MaxHeight),
                            Mode = ResizeMode.Max, // Maintains aspect ratio, fits within bounds
                            Sampler = KnownResamplers.Lanczos3 // High quality resampling
                        };
                        ctx.Resize(resizeOptions);
                    });

                    var encoder = new WebpEncoder
                    {
                        Quality = config.Quality,
                        Method = WebpEncodingMethod.BestQuality
                    };

                    await outputImage.SaveAsync(filePath, encoder);
                    result.ProcessedFiles[sizeName] = fileName;

                    _logger.LogInformation($"Created {sizeName} image: {fileName} ({outputImage.Width}x{outputImage.Height})");
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing image");
                throw;
            }
        }

        /// <summary>
        /// Process an image from file path
        /// </summary>
        public async Task<ImageProcessingResult> ProcessImageFromFileAsync(
            string sourceFilePath, 
            string outputDirectory, 
            string baseFileName)
        {
            using var fileStream = File.OpenRead(sourceFilePath);
            return await ProcessImageAsync(fileStream, outputDirectory, baseFileName);
        }

        /// <summary>
        /// Batch process all images in a directory
        /// </summary>
        public async Task<BatchProcessingResult> BatchProcessImagesAsync(
            string sourceDirectory, 
            string outputDirectory,
            string[] fileExtensions = null)
        {
            fileExtensions ??= new[] { ".png", ".jpg", ".jpeg", ".gif", ".webp" };
            
            var result = new BatchProcessingResult();

            if (!Directory.Exists(sourceDirectory))
            {
                _logger.LogWarning($"Source directory does not exist: {sourceDirectory}");
                return result;
            }

            var files = Directory.GetFiles(sourceDirectory)
                .Where(f => fileExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                .Where(f => !Path.GetFileName(f).Contains("_thumbnail") 
                         && !Path.GetFileName(f).Contains("_small")
                         && !Path.GetFileName(f).Contains("_medium")
                         && !Path.GetFileName(f).Contains("_large"))
                .ToList();

            _logger.LogInformation($"Found {files.Count} images to process");

            foreach (var file in files)
            {
                try
                {
                    var fileName = Path.GetFileNameWithoutExtension(file);
                    var processResult = await ProcessImageFromFileAsync(file, outputDirectory, fileName);
                    
                    result.ProcessedCount++;
                    result.ProcessedImages.Add(new ProcessedImageInfo
                    {
                        SourceFile = file,
                        BaseFileName = fileName,
                        Result = processResult
                    });

                    _logger.LogInformation($"Successfully processed: {file}");
                }
                catch (Exception ex)
                {
                    result.FailedCount++;
                    result.FailedImages.Add((file, ex.Message));
                    _logger.LogError(ex, $"Failed to process: {file}");
                }
            }

            return result;
        }

        /// <summary>
        /// Delete old image files (useful when replacing images)
        /// </summary>
        public void DeleteImageAndVariants(string directory, string baseFileName)
        {
            var patterns = new[]
            {
                $"{baseFileName}.*",
                $"{baseFileName}_thumbnail.*",
                $"{baseFileName}_small.*",
                $"{baseFileName}_medium.*",
                $"{baseFileName}_large.*"
            };

            foreach (var pattern in patterns)
            {
                var files = Directory.GetFiles(directory, pattern);
                foreach (var file in files)
                {
                    try
                    {
                        File.Delete(file);
                        _logger.LogInformation($"Deleted: {file}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, $"Failed to delete: {file}");
                    }
                }
            }
        }

        /// <summary>
        /// Get file size in KB
        /// </summary>
        public long GetFileSizeKB(string filePath)
        {
            if (!File.Exists(filePath))
                return 0;

            return new FileInfo(filePath).Length / 1024;
        }
    }

    public class ImageSizeConfig
    {
        public int MaxWidth { get; set; }
        public int MaxHeight { get; set; }
        public int Quality { get; set; }

        public ImageSizeConfig(int maxWidth, int maxHeight, int quality)
        {
            MaxWidth = maxWidth;
            MaxHeight = maxHeight;
            Quality = quality;
        }
    }

    public class ImageProcessingResult
    {
        public int OriginalWidth { get; set; }
        public int OriginalHeight { get; set; }
        public Dictionary<string, string> ProcessedFiles { get; set; } = new();
    }

    public class BatchProcessingResult
    {
        public int ProcessedCount { get; set; }
        public int FailedCount { get; set; }
        public List<ProcessedImageInfo> ProcessedImages { get; set; } = new();
        public List<(string FilePath, string Error)> FailedImages { get; set; } = new();
    }

    public class ProcessedImageInfo
    {
        public string SourceFile { get; set; } = string.Empty;
        public string BaseFileName { get; set; } = string.Empty;
        public ImageProcessingResult Result { get; set; } = new();
    }
}
