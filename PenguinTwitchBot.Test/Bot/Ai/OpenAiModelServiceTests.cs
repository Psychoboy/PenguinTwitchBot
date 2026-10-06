using Microsoft.Extensions.Logging;
using NSubstitute;
using PenguinTwitchBot.Bot.Ai;

namespace PenguinTwitchBot.Test.Bot.Ai
{
    public class OpenAiModelServiceTests
    {
        [Theory]
        [InlineData("gpt-5.1", true)]
        [InlineData("gpt-4.1", true)]
        [InlineData("gpt-4.1-mini", true)]
        [InlineData("gpt-4.1-nano", true)]
        [InlineData("gpt-4o", true)]
        [InlineData("gpt-4o-mini", true)]
        [InlineData("o1", true)]
        [InlineData("o1-mini", true)]
        [InlineData("o3-mini", true)]
        [InlineData("chatgpt-4o-latest", true)]
        [InlineData("text-embedding-3-small", false)]
        [InlineData("text-embedding-ada-002", false)]
        [InlineData("dall-e-3", false)]
        [InlineData("tts-1", false)]
        [InlineData("whisper-1", false)]
        [InlineData("omni-moderation-latest", false)]
        [InlineData("gpt-4o-realtime-preview", false)]
        [InlineData("", false)]
        [InlineData(null, false)]
        public void IsTextModel_CorrectlyFiltersModels(string? modelId, bool expected)
        {
            var result = OpenAiModelService.IsTextModel(modelId!);
            Assert.Equal(expected, result);
        }

        [Fact]
        public async Task GetAvailableTextModelsAsync_WhenClientNull_ReturnsDefaultTextModels()
        {
            var logger = Substitute.For<ILogger<OpenAiModelService>>();
            var service = new OpenAiModelService(logger, null);

            Assert.NotNull(service.CachedModels);
            Assert.Contains("gpt-5.1", service.CachedModels);

            var models = await service.GetAvailableTextModelsAsync();

            Assert.NotNull(models);
            Assert.NotEmpty(models);
            Assert.Contains("gpt-5.1", models);
            Assert.Contains("gpt-4o", models);
            Assert.Contains("o3-mini", models);

            Assert.NotNull(service.CachedModels);
            Assert.Equal(models, service.CachedModels);
        }
    }
}

