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

        [Fact]
        public void HasMultiCodepointPhonemes_ReturnsTrue_WhenJsonContainsMultiCodepointPhonemes()
        {
            var tempFile = Path.GetTempFileName();
            try
            {
                var json = """
                {
                    "phoneme_id_map": {
                        "a": [1],
                        "aɪ": [2]
                    }
                }
                """;
                File.WriteAllText(tempFile, json);

                var result = PiperService.HasMultiCodepointPhonemes(tempFile);
                Assert.True(result);
            }
            finally
            {
                if (File.Exists(tempFile)) File.Delete(tempFile);
            }
        }

        [Fact]
        public void HasMultiCodepointPhonemes_ReturnsFalse_WhenJsonContainsOnlySingleCodepointPhonemes()
        {
            var tempFile = Path.GetTempFileName();
            try
            {
                var json = """
                {
                    "phoneme_id_map": {
                        "a": [1],
                        "b": [2],
                        "^": [3],
                        "$": [4]
                    }
                }
                """;
                File.WriteAllText(tempFile, json);

                var result = PiperService.HasMultiCodepointPhonemes(tempFile);
                Assert.False(result);
            }
            finally
            {
                if (File.Exists(tempFile)) File.Delete(tempFile);
            }
        }

        [Fact]
        public void HasMultiCodepointPhonemes_ReturnsFalse_WhenFileDoesNotExist()
        {
            var result = PiperService.HasMultiCodepointPhonemes("non_existent_file.json");
            Assert.False(result);
        }

        [Fact]
        public void HasMultiCodepointPhonemes_ReturnsFalse_WhenNoPhonemeIdMap()
        {
            var tempFile = Path.GetTempFileName();
            try
            {
                File.WriteAllText(tempFile, """{"audio": {"sample_rate": 22050}}""");

                var result = PiperService.HasMultiCodepointPhonemes(tempFile);
                Assert.False(result);
            }
            finally
            {
                if (File.Exists(tempFile)) File.Delete(tempFile);
            }
        }

        [Fact]
        public void IsModelCompatible_ReturnsFalse_WhenModelHasMultiCodepointPhonemes()
        {
            var logger = Substitute.For<ILogger<PiperService>>();
            var service = new PiperService(logger);

            var testKey = "test_voice-incompatible";
            var modelsDir = GetTestModelsDirectory();
            var targetDir = Path.Combine(modelsDir, testKey);

            Directory.CreateDirectory(targetDir);
            var onnxJsonPath = Path.Combine(targetDir, $"{testKey}.onnx.json");
            File.WriteAllText(onnxJsonPath, """
            {
                "phoneme_id_map": {
                    "aɪ": [161]
                }
            }
            """);

            try
            {
                var result = service.IsModelCompatible(testKey);
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
        public void IsModelCompatible_ReturnsTrue_WhenModelHasSingleCodepointPhonemes()
        {
            var logger = Substitute.For<ILogger<PiperService>>();
            var service = new PiperService(logger);

            var testKey = "test_voice-compatible";
            var modelsDir = GetTestModelsDirectory();
            var targetDir = Path.Combine(modelsDir, testKey);

            Directory.CreateDirectory(targetDir);
            var onnxJsonPath = Path.Combine(targetDir, $"{testKey}.onnx.json");
            File.WriteAllText(onnxJsonPath, """
            {
                "phoneme_id_map": {
                    "a": [1],
                    "b": [2]
                }
            }
            """);

            try
            {
                var result = service.IsModelCompatible(testKey);
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

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public async Task SynthesizeAsync_ThrowsArgumentException_WhenMessageIsNullOrWhiteSpace(string message)
        {
            var logger = Substitute.For<ILogger<PiperService>>();
            var service = new PiperService(logger);

            await Assert.ThrowsAsync<ArgumentException>(() => service.SynthesizeAsync(message, "any-model"));
        }

        [Fact]
        public async Task SynthesizeAsync_ThrowsInvalidOperationException_WhenModelHasMultiCodepointPhonemes()
        {
            var logger = Substitute.For<ILogger<PiperService>>();
            var service = new PiperService(logger);

            var testKey = "test_voice-incompatible-synth";
            var modelsDir = GetTestModelsDirectory();
            var targetDir = Path.Combine(modelsDir, testKey);

            Directory.CreateDirectory(targetDir);
            File.WriteAllText(Path.Combine(targetDir, $"{testKey}.onnx"), "dummy");
            File.WriteAllText(Path.Combine(targetDir, "model.json"), "{}");
            File.WriteAllText(Path.Combine(targetDir, $"{testKey}.onnx.json"), """
            {
                "phoneme_id_map": {
                    "aɪ": [161]
                }
            }
            """);

            try
            {
                var repoPiperDir = Path.Combine(Directory.GetCurrentDirectory(), "PenguinTwitchBot", "Data", "piper", "piper");
                var directPiperDir = Path.Combine(Directory.GetCurrentDirectory(), "Data", "piper", "piper");
                var piperDir = Directory.Exists(Path.Combine(Directory.GetCurrentDirectory(), "PenguinTwitchBot", "Data", "piper"))
                    ? repoPiperDir
                    : directPiperDir;

                Directory.CreateDirectory(piperDir);
                var exeName = OperatingSystem.IsWindows() ? "piper.exe" : "piper";
                var dummyExe = Path.Combine(piperDir, exeName);
                var createdDummyExe = false;
                if (!File.Exists(dummyExe))
                {
                    File.WriteAllText(dummyExe, "dummy");
                    createdDummyExe = true;
                }

                try
                {
                    var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                        service.SynthesizeAsync("Hello world", testKey));
                    Assert.Contains("incompatible with the Piper native engine", ex.Message);
                }
                finally
                {
                    if (createdDummyExe && File.Exists(dummyExe))
                    {
                        File.Delete(dummyExe);
                    }
                }
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
