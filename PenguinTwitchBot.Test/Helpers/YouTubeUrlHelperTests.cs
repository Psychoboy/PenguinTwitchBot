using PenguinTwitchBot.Helpers;

namespace PenguinTwitchBot.Test.Helpers
{
    public class YouTubeUrlHelperTests
    {
        [Theory]
        [InlineData("dQw4w9WgXcQ")]
        [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXcQ")]
        [InlineData("https://youtube.com/watch?v=dQw4w9WgXcQ&t=30s")]
        [InlineData("http://www.youtube.com/watch?v=dQw4w9WgXcQ")]
        [InlineData("https://youtu.be/dQw4w9WgXcQ")]
        [InlineData("https://youtu.be/dQw4w9WgXcQ?t=42")]
        [InlineData("https://www.youtube.com/shorts/dQw4w9WgXcQ")]
        [InlineData("https://www.youtube.com/embed/dQw4w9WgXcQ")]
        [InlineData("https://www.youtube.com/live/dQw4w9WgXcQ")]
        [InlineData("www.youtube.com/watch?v=dQw4w9WgXcQ")]
        [InlineData("  https://www.youtube.com/watch?v=dQw4w9WgXcQ  ")]
        public void ExtractVideoId_ReturnsId_ForSupportedFormats(string input)
        {
            Assert.Equal("dQw4w9WgXcQ", YouTubeUrlHelper.ExtractVideoId(input));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("not a video")]
        [InlineData("https://www.youtube.com/watch?list=PL123456")]
        [InlineData("https://example.com/watch?v=dQw4w9WgXcQextra")]
        [InlineData("ftp://youtu.be/dQw4w9WgXcQ")]
        public void ExtractVideoId_ReturnsNull_ForUnsupportedInput(string? input)
        {
            Assert.Null(YouTubeUrlHelper.ExtractVideoId(input));
        }

        [Theory]
        [InlineData("https://music.youtube.com/watch?v=dQw4w9WgXcQ")]
        [InlineData("https://m.youtube.com/watch?v=dQw4w9WgXcQ")]
        public void ExtractVideoId_ReturnsId_ForYouTubeSubdomains(string input)
        {
            Assert.Equal("dQw4w9WgXcQ", YouTubeUrlHelper.ExtractVideoId(input));
        }

        [Theory]
        [InlineData("https://piped.video/watch?v=dQw4w9WgXcQ")]
        [InlineData("https://youtube.com.evil.test/watch?v=dQw4w9WgXcQ")]
        public void ExtractVideoId_ReturnsNull_ForNonYouTubeHosts(string input)
        {
            Assert.Null(YouTubeUrlHelper.ExtractVideoId(input));
        }
    }
}
