using DotNetTwitchBot.Bot.Core;

namespace DotNetTwitchBot.Test.Bot.Core
{
    public class LanguageTests
    {

        [Fact]
        public async Task LoadAndGetString_ShouldReturnString()
        {
            // Arrange
            var language = new Language();
            await language.LoadLanguage();
            // Act
            var result = language.Get("game.jackpot.response");

            // Assert
            Assert.Equal("The current jackpot is (jackpot)", result);

        }

        [Fact]
        public async Task LoadAndGetString_ShouldNotReturnString()
        {
            // Arrange
            var language = new Language();
            await language.LoadLanguage();
            // Act
            var result = language.Get("Some.Test.String");

            // Assert
            Assert.Equal("", result);

        }

        [Fact]
        public async Task LoadedCustomStrings()
        {
            // Arrange
            var language = new Language();
            await language.LoadLanguage();
            // Act
            var result = language.Get("test.custom");

            // Assert
            Assert.Equal("This is a test custom string", result);

        }
    }
}
