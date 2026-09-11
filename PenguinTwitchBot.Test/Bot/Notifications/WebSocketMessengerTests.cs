using PenguinTwitchBot.Bot.Notifications;
using Xunit;

namespace PenguinTwitchBot.Test.Bot.Notifications
{
    public class WsTopicsTests
    {
        [Fact]
        public void Parse_KeepsKnownTopicsAndNormalizesCase()
        {
            var parsed = WsTopics.Parse(["CHAT", "Alerts"]);

            Assert.Equal(new[] { WsTopics.Alerts, WsTopics.Chat }, parsed.OrderBy(topic => topic));
        }

        [Fact]
        public void Parse_SplitsCommaSeparatedValues()
        {
            var parsed = WsTopics.Parse(["chat, fishing"]);

            Assert.Equal(new[] { WsTopics.Chat, WsTopics.Fishing }, parsed.OrderBy(topic => topic));
        }

        [Theory]
        [InlineData("not-a-topic")]
        [InlineData("chat\r\nINFO: forged log entry")]
        [InlineData("<script>alert(1)</script>")]
        public void Parse_DiscardsUnknownTopics(string topic)
        {
            Assert.Empty(WsTopics.Parse([topic]));
        }

        [Fact]
        public void Parse_HandlesNullAndEmptyInput()
        {
            Assert.Empty(WsTopics.Parse(null));
            Assert.Empty(WsTopics.Parse([null, "", "   "]));
        }
    }

    public class WebSocketMessengerSanitizationTests
    {
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("\r\n")]
        public void SanitizeDisplayName_FallsBackToUnknown(string? displayName)
        {
            Assert.Equal("Unknown", WebSocketMessenger.SanitizeDisplayName(displayName));
        }

        [Fact]
        public void SanitizeDisplayName_ReplacesControlCharacters()
        {
            var sanitized = WebSocketMessenger.SanitizeDisplayName("Chat\r\nINFO: forged entry");

            Assert.DoesNotContain('\r', sanitized);
            Assert.DoesNotContain('\n', sanitized);
            Assert.Equal("Chat__INFO: forged entry", sanitized);
        }

        [Fact]
        public void SanitizeDisplayName_CapsLength()
        {
            var sanitized = WebSocketMessenger.SanitizeDisplayName(new string('a', 500));

            Assert.Equal(64, sanitized.Length);
        }

        [Fact]
        public void SanitizeDisplayName_KeepsOrdinaryNames()
        {
            Assert.Equal("Overlay: main", WebSocketMessenger.SanitizeDisplayName("  Overlay: main  "));
        }
    }
}
