using Microsoft.Extensions.Logging;
using NSubstitute;
using PenguinTwitchBot.Bot.Commands.TTS;
using Xunit;

namespace PenguinTwitchBot.Test.Services
{
    public class PiperServiceTests
    {
        private static string GetTestModelsDirectory()
        {
            var repoDir = Path.Combine(Directory.GetCurrentDirectory(), "PenguinTwitchBot", "Data", "piper", "models");
            var directDir = Path.Combine(Directory.GetCurrentDirectory(), "Data", "piper", "models");
            return Directory.Exists(Path.Combine(Directory.GetCurrentDirectory(), "PenguinTwitchBot", "Data", "piper"))
                ? repoDir
                : directDir;
        }

        [Fact]
        public void IsModelDownloaded_ReturnsFalse_WhenModelDoesNotExist()
        {
            var logger = Substitute.For<ILogger<PiperService>>();
            var service = new PiperService(logger);

            var result = service.IsModelDownloaded("non-existent-voice-xyz");
            Assert.False(result);
        }

        [Fact]
        public void IsModelDownloaded_ReturnsFalse_WhenOnlyOnnxFileExists()
        {
            var logger = Substitute.For<ILogger<PiperService>>();
            var service = new PiperService(logger);

            var testKey = "test_voice-onnx-only";
            var modelsDir = GetTestModelsDirectory();
            var targetDir = Path.Combine(modelsDir, testKey);

            Directory.CreateDirectory(targetDir);
            var onnxFile = Path.Combine(targetDir, $"{testKey}.onnx");
            File.WriteAllText(onnxFile, "dummy");

            try
            {
                // Incomplete bundle (missing .onnx.json and model.json) should return false
                var result = service.IsModelDownloaded(testKey);
                Assert.False(result);
            }
            finally
            {
                if (Directory.Exists(targetDir))
                {
                    Directory.Delete(targetDir, true);
                }
            }
        }

        [Fact]
        public void IsModelDownloaded_ReturnsTrue_WhenCompleteNestedBundleExists()
        {
            var logger = Substitute.For<ILogger<PiperService>>();
            var service = new PiperService(logger);

            var testKey = "test_voice-complete-nested";
            var modelsDir = GetTestModelsDirectory();
            var targetDir = Path.Combine(modelsDir, testKey);

            Directory.CreateDirectory(targetDir);
            File.WriteAllText(Path.Combine(targetDir, $"{testKey}.onnx"), "dummy");
            File.WriteAllText(Path.Combine(targetDir, $"{testKey}.onnx.json"), "{}");
            File.WriteAllText(Path.Combine(targetDir, "model.json"), "{}");

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

        [Fact]
        public void IsModelDownloaded_ReturnsTrue_WhenCompleteDirectBundleExists()
        {
            var logger = Substitute.For<ILogger<PiperService>>();
            var service = new PiperService(logger);

            var testKey = "test_voice-complete-direct";
            var modelsDir = GetTestModelsDirectory();
            Directory.CreateDirectory(modelsDir);

            var onnxPath = Path.Combine(modelsDir, $"{testKey}.onnx");
            var onnxJsonPath = Path.Combine(modelsDir, $"{testKey}.onnx.json");
            var modelJsonPath = Path.Combine(modelsDir, "model.json");

            File.WriteAllText(onnxPath, "dummy");
            File.WriteAllText(onnxJsonPath, "{}");
            var createdModelJson = false;
            if (!File.Exists(modelJsonPath))
            {
                File.WriteAllText(modelJsonPath, "{}");
                createdModelJson = true;
            }

            try
            {
                var result = service.IsModelDownloaded(testKey);
                Assert.True(result);
            }
            finally
            {
                if (File.Exists(onnxPath)) File.Delete(onnxPath);
                if (File.Exists(onnxJsonPath)) File.Delete(onnxJsonPath);
                if (createdModelJson && File.Exists(modelJsonPath)) File.Delete(modelJsonPath);
            }
        }
    }
}
