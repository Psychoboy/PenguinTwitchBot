using PenguinTwitchBot.Bot.Commands.Music;

namespace PenguinTwitchBot.Test.Bot
{
    public class YtPlayerLookupParsingTests
    {
        [Theory]
        [InlineData("{\"items\":[{\"id\":{\"videoId\":\"dQw4w9WgXcQ\"}}]}", "items[0].id.videoId", "dQw4w9WgXcQ")]
        [InlineData("[{\"videoId\":\"dQw4w9WgXcQ\"},{\"videoId\":\"other\"}]", "[0].videoId", "dQw4w9WgXcQ")]
        [InlineData("[{\"videoId\":\"a\"},{\"videoId\":\"dQw4w9WgXcQ\"}]", "[1].videoId", "dQw4w9WgXcQ")]
        [InlineData("{\"items\":[{\"url\":\"/watch?v=dQw4w9WgXcQ\"}]}", "items[0].url", "/watch?v=dQw4w9WgXcQ")]
        [InlineData("{\"a\":{\"b\":{\"c\":\"x\"}}}", "a.b.c", "x")]
        [InlineData("{\"matrix\":[[\"p\",\"q\"]]}", "matrix[0][1]", "q")]
        public void ReadJsonPath_ReturnsValue(string json, string path, string expected)
        {
            Assert.Equal(expected, YtPlayer.ReadJsonPath(json, path));
        }

        [Theory]
        [InlineData("{\"items\":[]}", "items[0].id.videoId")]
        [InlineData("{\"items\":[{}]}", "items[0].id.videoId")]
        [InlineData("{\"items\":{}}", "items[0]")]
        [InlineData("[1]", "[5]")]
        [InlineData("[1]", "[-1]")]
        [InlineData("[1]", "[x]")]
        [InlineData("[1]", "[0")]
        [InlineData("{\"a\":1}", "b")]
        [InlineData("\"text\"", "a")]
        public void ReadJsonPath_ReturnsNull_WhenPathDoesNotResolve(string json, string path)
        {
            Assert.Null(YtPlayer.ReadJsonPath(json, path));
        }

        [Fact]
        public void ReadJsonPath_ConvertsNonStringValuesToText()
        {
            Assert.Equal("42", YtPlayer.ReadJsonPath("{\"n\":42}", "n"));
        }

        [Fact]
        public void ReadJsonPath_Throws_ForInvalidJson()
        {
            Assert.ThrowsAny<System.Text.Json.JsonException>(() => YtPlayer.ReadJsonPath("No videos found", "a"));
        }

        [Theory]
        [InlineData("dQw4w9WgXcQ", "dQw4w9WgXcQ")]
        [InlineData("  dQw4w9WgXcQ\n", "dQw4w9WgXcQ")]
        [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXcQ", "dQw4w9WgXcQ")]
        [InlineData("https://youtu.be/dQw4w9WgXcQ", "dQw4w9WgXcQ")]
        [InlineData("/watch?v=dQw4w9WgXcQ", "dQw4w9WgXcQ")]
        [InlineData("/watch?list=abc&v=dQw4w9WgXcQ", "dQw4w9WgXcQ")]
        public void NormalizeVideoId_ReturnsId(string value, string expected)
        {
            Assert.Equal(expected, YtPlayer.NormalizeVideoId(value));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("No videos found")]
        [InlineData("https://example.com/watch?v=short")]
        public void NormalizeVideoId_ReturnsNull_WhenNotAVideoId(string? value)
        {
            Assert.Null(YtPlayer.NormalizeVideoId(value));
        }
    }
}
