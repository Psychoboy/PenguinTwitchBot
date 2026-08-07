using SkiaSharp;

namespace PenguinTwitchBot.Services
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

                // Buffer once so we can inspect metadata and decode safely.
                byte[] imageBytes;
                using (var buffer = new MemoryStream())
                {
                    await sourceStream.CopyToAsync(buffer);
                    imageBytes = buffer.ToArray();
                }

                using var codec = SKCodec.Create(new SKMemoryStream(imageBytes));
                if (codec is null)
                {
                    throw new InvalidOperationException("Unsupported or invalid image format.");
                }

                var imageInfo = codec.Info;

                // Validate dimensions to prevent excessive memory usage
                const int maxDimension = 8192;
                if (imageInfo.Width > maxDimension || imageInfo.Height > maxDimension)
                {
                    throw new InvalidOperationException(
                    $"Image dimensions ({imageInfo.Width}x{imageInfo.Height}) exceed maximum allowed size ({maxDimension}x{maxDimension}). " +
                        $"Please resize the image before uploading.");
                }

                // Additional check for total pixel count (prevents wide/tall attack vectors)
                const long maxPixels = 67_108_864; // 8192 * 8192
                long totalPixels = (long)imageInfo.Width * imageInfo.Height;
                if (totalPixels > maxPixels)
                {
                    throw new InvalidOperationException(
                        $"Image total pixel count ({totalPixels:N0}) exceeds maximum allowed ({maxPixels:N0}).");
                }

                using var originalBitmap = SKBitmap.Decode(imageBytes);
                if (originalBitmap is null)
                {
                    throw new InvalidOperationException("Failed to decode image.");
                }
                
                var result = new ImageProcessingResult
                {
                    OriginalWidth = originalBitmap.Width,
                    OriginalHeight = originalBitmap.Height
                };

                // Save full-resolution base image (used as canonical filename).
                var originalFileName = $"{baseFileName}.webp";
                var originalFilePath = Path.Combine(outputDirectory, originalFileName);
                await SaveBitmapAsWebpAsync(originalBitmap, originalFilePath, 100);
                result.ProcessedFiles["original"] = originalFileName;

                _logger.LogInformation($"Created original image: {originalFileName} ({originalBitmap.Width}x{originalBitmap.Height})");

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

                    var (targetWidth, targetHeight) = CalculateFitDimensions(
                        originalBitmap.Width,
                        originalBitmap.Height,
                        config.MaxWidth,
                        config.MaxHeight);

                    var outputInfo = new SKImageInfo(
                        targetWidth,
                        targetHeight,
                        originalBitmap.ColorType,
                        originalBitmap.AlphaType,
                        originalBitmap.ColorSpace);
                    var sampling = new SKSamplingOptions(SKCubicResampler.Mitchell);

                    using var outputImage = originalBitmap.Resize(outputInfo, sampling);
                    if (outputImage is null)
                    {
                        throw new InvalidOperationException($"Failed to resize image to {targetWidth}x{targetHeight}.");
                    }

                    await SaveBitmapAsWebpAsync(outputImage, filePath, config.Quality);
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

        private static async Task SaveBitmapAsWebpAsync(SKBitmap bitmap, string outputPath, int quality)
        {
            using var image = SKImage.FromBitmap(bitmap);
            using var data = image.Encode(SKEncodedImageFormat.Webp, quality);
            if (data is null)
            {
                throw new InvalidOperationException("Failed to encode image as WebP.");
            }

            await using var output = File.Open(outputPath, FileMode.Create, FileAccess.Write, FileShare.None);
            data.SaveTo(output);
        }

        private static (int Width, int Height) CalculateFitDimensions(
            int sourceWidth,
            int sourceHeight,
            int maxWidth,
            int maxHeight)
        {
            if (sourceWidth <= maxWidth && sourceHeight <= maxHeight)
            {
                return (sourceWidth, sourceHeight);
            }

            var widthRatio = (double)maxWidth / sourceWidth;
            var heightRatio = (double)maxHeight / sourceHeight;
            var scale = Math.Min(widthRatio, heightRatio);

            var width = Math.Max(1, (int)Math.Round(sourceWidth * scale));
            var height = Math.Max(1, (int)Math.Round(sourceHeight * scale));
            return (width, height);
        }

        /// <summary>
        /// Process an image from file path
        /// </summary>
        public async Task<ImageProcessingResult> ProcessImageFromFileAsync(
            string sourceFilePath, 
            string outputDirectory, 
            string baseFileName)
        {
            // Read all bytes first so the file handle is released before any writes,
            // preventing IO lock conflicts when source and output directories overlap.
            var bytes = await File.ReadAllBytesAsync(sourceFilePath);
            using var memoryStream = new MemoryStream(bytes);
            return await ProcessImageAsync(memoryStream, outputDirectory, baseFileName);
        }

        /// <summary>
        /// Batch process all images in a directory
        /// </summary>
        public async Task<BatchProcessingResult> BatchProcessImagesAsync(
            string sourceDirectory, 
            string outputDirectory,
            string[]? fileExtensions = null)
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
            var imageExtensions = new[] { ".webp", ".png", ".jpg", ".jpeg", ".gif", ".bmp" };

            var patterns = new[]
            {
                baseFileName,
                $"{baseFileName}_thumbnail",
                $"{baseFileName}_small",
                $"{baseFileName}_medium",
                $"{baseFileName}_large"
            };

            foreach (var pattern in patterns)
            {
                foreach (var ext in imageExtensions)
                {
                    var filePath = Path.Combine(directory, $"{pattern}{ext}");
                    if (File.Exists(filePath))
                    {
                        try
                        {
                            File.Delete(filePath);
                            _logger.LogInformation($"Deleted: {filePath}");
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, $"Failed to delete: {filePath}");
                        }
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
