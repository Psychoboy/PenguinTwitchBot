using PenguinTwitchBot.Helpers;
using Xunit;

namespace PenguinTwitchBot.Test
{
    public class AlertCssHelperTests
    {
        [Fact]
        public void GenerateCss_MatchesUserSampleExactly()
        {
            // The sample CSS from user:
            // color: white;font-size: 50px;font-family: Arial;width: 600px;word-wrap: break-word;-webkit-text-stroke-width: 1px;-webkit-text-stroke-color: black;text-shadow: black 1px 0 5px;
            var options = new AlertCssOptions
            {
                Color = "white",
                FontSize = 50,
                FontFamily = "Arial",
                Width = 600,
                WordWrap = true,
                EnableStroke = true,
                StrokeWidth = 1.0,
                StrokeColor = "black",
                EnableShadow = true,
                ShadowColor = "black",
                ShadowOffsetX = 1,
                ShadowOffsetY = 0,
                ShadowBlur = 5
            };

            var generated = AlertCssHelper.GenerateCss(options);

            const string expected = "color: white;font-size: 50px;font-family: Arial;width: 600px;word-wrap: break-word;-webkit-text-stroke-width: 1px;-webkit-text-stroke-color: black;text-shadow: black 1px 0 5px;";
            Assert.Equal(expected, generated);
        }

        [Fact]
        public void ParseCss_ParsesUserSampleCorrectly()
        {
            const string sampleCss = "color: white;font-size: 50px;font-family: Arial;width: 600px;word-wrap: break-word;-webkit-text-stroke-width: 1px;-webkit-text-stroke-color: black;text-shadow: black 1px 0 5px;";

            var options = AlertCssHelper.ParseCss(sampleCss);

            Assert.Equal("white", options.Color);
            Assert.Equal(50, options.FontSize);
            Assert.Equal("Arial", options.FontFamily);
            Assert.Equal(600, options.Width);
            Assert.True(options.WordWrap);
            Assert.True(options.EnableStroke);
            Assert.Equal(1.0, options.StrokeWidth);
            Assert.Equal("black", options.StrokeColor);
            Assert.True(options.EnableShadow);
            Assert.Equal("black", options.ShadowColor);
            Assert.Equal(1, options.ShadowOffsetX);
            Assert.Equal(0, options.ShadowOffsetY);
            Assert.Equal(5, options.ShadowBlur);
        }

        [Fact]
        public void RoundTrip_GenerateAndParse_YieldsSameCss()
        {
            var initial = AlertCssHelper.Presets["Classic White with Outline"].Clone();
            var css = AlertCssHelper.GenerateCss(initial);
            var parsed = AlertCssHelper.ParseCss(css);
            var regenerated = AlertCssHelper.GenerateCss(parsed);

            Assert.Equal(css, regenerated);
        }

        [Fact]
        public void ParseCss_HandlesRgbaTextShadow()
        {
            const string css = "color: #ffffff;font-size: 40px;text-shadow: rgba(0, 0, 0, 0.75) 2px 3px 10px;";
            var options = AlertCssHelper.ParseCss(css);

            Assert.True(options.EnableShadow);
            Assert.Equal("rgba(0, 0, 0, 0.75)", options.ShadowColor);
            Assert.Equal(2, options.ShadowOffsetX);
            Assert.Equal(3, options.ShadowOffsetY);
            Assert.Equal(10, options.ShadowBlur);
        }

        [Fact]
        public void Presets_AllGenerateNonEmptyCss()
        {
            foreach (var preset in AlertCssHelper.Presets)
            {
                var css = AlertCssHelper.GenerateCss(preset.Value);
                Assert.False(string.IsNullOrWhiteSpace(css), $"Preset '{preset.Key}' produced empty CSS");
            }
        }
    }
}