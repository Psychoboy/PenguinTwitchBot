using System.Text.Json;
using PenguinTwitchBot.Bot.Commands.Games.Starship;

namespace PenguinTwitchBot.Test.Bot.Commands.Games;

public class StarshipAppearanceSettingsTests
{
    [Fact]
    public void Defaults_AreValidAndMatchTheOriginalLook()
    {
        var appearance = StarshipAppearanceSettings.NormalizeAndValidate(new StarshipAppearanceSettings());

        Assert.Equal("#0a181d", appearance.PanelColor);
        Assert.Equal("#66d0c2", appearance.AccentColor);
        Assert.Equal("#f4bd68", appearance.HighlightColor);
        Assert.Equal(94, appearance.PanelOpacityPercent);
        Assert.Equal(7, appearance.CornerRadiusPx);
        Assert.Equal("Barlow Condensed", appearance.FontFamily);
        Assert.True(appearance.UseShipAccent && appearance.UseBuiltInShipArt && appearance.UseBuiltInObjectArt);
        Assert.Equal(StarshipAppearanceSettings.ObjectKinds.Order(), appearance.ObjectImages.Keys.Order());
    }

    [Fact]
    public void Normalize_LowercasesColoursAndMatchesFontsCaseInsensitively()
    {
        var appearance = StarshipAppearanceSettings.NormalizeAndValidate(new StarshipAppearanceSettings
        {
            AccentColor = " #66D0C2 ",
            FontFamily = "verdana"
        });

        Assert.Equal("#66d0c2", appearance.AccentColor);
        Assert.Equal("Verdana", appearance.FontFamily);
    }

    [Theory]
    [InlineData("red")]
    [InlineData("#fff")]
    [InlineData("#12345g")]
    [InlineData("url(javascript:alert(1))")]
    [InlineData("")]
    public void Normalize_RejectsInvalidColours(string colour) =>
        Assert.Throws<ArgumentException>(() => StarshipAppearanceSettings.NormalizeAndValidate(
            new StarshipAppearanceSettings { PanelColor = colour }));

    [Fact]
    public void Normalize_RejectsOutOfRangeValuesAndUnknownFonts()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => StarshipAppearanceSettings.NormalizeAndValidate(
            new StarshipAppearanceSettings { PanelOpacityPercent = 101 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => StarshipAppearanceSettings.NormalizeAndValidate(
            new StarshipAppearanceSettings { CornerRadiusPx = 25 }));
        Assert.Throws<ArgumentException>(() => StarshipAppearanceSettings.NormalizeAndValidate(
            new StarshipAppearanceSettings { FontFamily = "Comic Sans'; background:url(x)" }));
    }

    [Fact]
    public void Normalize_AcceptsMediaLibraryAndHttpImagesAndDropsBlanksAndDuplicates()
    {
        var appearance = StarshipAppearanceSettings.NormalizeAndValidate(new StarshipAppearanceSettings
        {
            ShipImages = ["/media/ship%20one.png", " https://cdn.example.com/ship.webp ", "", "/media/ship%20one.png"],
            ObjectImages = new Dictionary<string, List<string>> { ["Wreck"] = ["/media/wreck.png"] }
        });

        Assert.Equal(["/media/ship%20one.png", "https://cdn.example.com/ship.webp"], appearance.ShipImages);
        Assert.Equal(["/media/wreck.png"], appearance.ObjectImages["wreck"]);
        Assert.Empty(appearance.ObjectImages["beacon"]);
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:image/svg+xml;base64,AAAA")]
    [InlineData("//evil.example.com/x.png")]
    [InlineData("media/relative.png")]
    [InlineData("/media/with space.png")]
    [InlineData("/media/quote\".png")]
    [InlineData("/media/angle<.png")]
    [InlineData("sptvShots")]
    public void Normalize_RejectsUnsafeImageAddresses(string url) =>
        Assert.Throws<ArgumentException>(() => StarshipAppearanceSettings.NormalizeAndValidate(
            new StarshipAppearanceSettings { ShipImages = [url] }));

    [Fact]
    public void Normalize_RejectsTooManyImagesAndUnknownObjectTypes()
    {
        var tooMany = Enumerable.Range(0, StarshipAppearanceSettings.MaximumImagesPerList + 1).Select(index => $"/media/{index}.png").ToList();
        Assert.Throws<ArgumentException>(() => StarshipAppearanceSettings.NormalizeAndValidate(
            new StarshipAppearanceSettings { ShipImages = tooMany }));
        Assert.Throws<ArgumentException>(() => StarshipAppearanceSettings.NormalizeAndValidate(
            new StarshipAppearanceSettings { ObjectImages = new Dictionary<string, List<string>> { ["dragon"] = ["/media/a.png"] } }));
    }

    [Fact]
    public void Settings_SerializeWithTheAppearanceAndOldSavedSettingsStillLoad()
    {
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true };
        var settings = StarshipExpeditionSettings.NormalizeAndValidate(new StarshipExpeditionSettings());
        settings.Appearance.AccentColor = "#8fb8ff";
        var json = JsonSerializer.Serialize(settings, options);
        Assert.Contains("\"appearance\"", json, StringComparison.Ordinal);
        Assert.Equal("#8fb8ff", JsonSerializer.Deserialize<StarshipExpeditionSettings>(json, options)!.Appearance.AccentColor);

        using var document = JsonDocument.Parse(json);
        var withoutAppearance = JsonSerializer.Serialize(
            document.RootElement.EnumerateObject().Where(property => property.Name != "appearance").ToDictionary(property => property.Name, property => property.Value));
        var legacy = StarshipExpeditionSettings.NormalizeAndValidate(JsonSerializer.Deserialize<StarshipExpeditionSettings>(withoutAppearance, options)!);
        Assert.Equal("#66d0c2", legacy.Appearance.AccentColor);
    }
}
