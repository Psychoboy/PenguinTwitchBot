using Microsoft.Extensions.Logging;
using NSubstitute;
using PenguinTwitchBot.Bot.Ai;
using Xunit;

namespace PenguinTwitchBot.Test.Bot.Ai
{
    public class OpenAiResponseServiceTests
    {
        [Fact]
        public async Task GenerateResponseAsync_WhenClientNull_ReturnsNotConfigured()
        {
            var logger = Substitute.For<ILogger<OpenAiResponseService>>();
            var service = new OpenAiResponseService(logger, null);

            var result = await service.GenerateResponseAsync("hello", "", "gpt-5.1", 200, "default", false, null, null);

            Assert.False(result.Success);
            Assert.Contains("not configured", result.ErrorMessage);
        }

        [Fact]
        public async Task GenerateResponseAsync_WhenPromptEmpty_ReturnsError()
        {
            var logger = Substitute.For<ILogger<OpenAiResponseService>>();
            var service = new OpenAiResponseService(logger, null);

            var result = await service.GenerateResponseAsync("   ", "Some instructions", "gpt-5.1", 200, "default", false, null, null);

            Assert.False(result.Success);
            Assert.Equal("Prompt cannot be empty.", result.ErrorMessage);
        }
    }
}
