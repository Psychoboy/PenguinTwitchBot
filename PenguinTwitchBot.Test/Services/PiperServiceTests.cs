using Microsoft.Extensions.Logging;
using NSubstitute;
using PenguinTwitchBot.Bot.Commands.TTS;
using Xunit;

namespace PenguinTwitchBot.Test.Services
{
    public class PiperServiceTests
    {
        [Fact]
        public void IsModelDownloaded_ReturnsFalse_WhenModelDoesNotExist()
        {
            var logger = Substitute.For<ILogger<PiperService>>();
            var service = new PiperService(logger);

            var result = service.IsModelDownloaded("non-existent-voice-xyz");
            Assert.False(result);
        }

        [Fact]
        public void IsModelDownloaded_ReturnsTrue_WhenNestedOnnxExists()
        {
            var logger = Substitute.For<ILogger<PiperService>>();
            var service = new PiperService(logger);

            var testKey = "test_voice-sample-medium";
            // Check where service resolves PiperDirectory
            var repoDir = Path.Combine(Directory.GetCurrentDirectory(), "PenguinTwitchBot", "Data", "piper", "models", testKey);
            var directDir = Path.Combine(Directory.GetCurrentDirectory(), "Data", "piper", "models", testKey);
            var targetDir = Directory.Exists(Path.Combine(Directory.GetCurrentDirectory(), "PenguinTwitchBot", "Data", "piper"))
                ? repoDir
                : directDir;

            Directory.CreateDirectory(targetDir);
            var onnxFile = Path.Combine(targetDir, $"{testKey}.onnx");
            File.WriteAllText(onnxFile, "dummy");

            try
            {
                var result = service.IsModelDownloaded(testKey);
                Assert.True(result);
            }
            finally
            {
                if (Directory.Exists(targetDir))
                {
                    Directory.Delete(targetDir, true);
                }
            }
        }
    }
}

