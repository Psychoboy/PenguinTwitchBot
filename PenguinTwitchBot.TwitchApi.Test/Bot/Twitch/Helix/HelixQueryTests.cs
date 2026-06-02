using PenguinTwitchBot.TwitchApi.Helix;

namespace PenguinTwitchBot.Test.Bot.Twitch.Helix;

public class HelixQueryTests
{
    [Fact]
    public void Build_ShouldReturnPath_WhenNoParameters()
    {
        var result = HelixQuery.Build("users", []);

        Assert.Equal("users", result);
    }

    [Fact]
    public void Build_ShouldSkipNullOrWhitespaceValues()
    {
        var result = HelixQuery.Build("users", new (string Key, string? Value)[]
        {
            ("id", null),
            ("login", "  "),
            ("first", "100")
        });

        Assert.Equal("users?first=100", result);
    }

    [Fact]
    public void Repeat_ShouldReturnNothing_ForEmptyList()
    {
        var result = HelixQuery.Repeat("id", []).ToList();

        Assert.Empty(result);
    }

    [Fact]
    public void Repeat_ShouldFilterWhitespace_AndKeepValues()
    {
        var result = HelixQuery.Repeat("id", ["1", " ", "2"]).ToList();

        Assert.Equal(2, result.Count);
        Assert.Equal(("id", "1"), result[0]);
        Assert.Equal(("id", "2"), result[1]);
    }

    [Fact]
    public void Build_ShouldEscapeKeysAndValues()
    {
        var result = HelixQuery.Build("search", new (string Key, string? Value)[]
        {
            ("q value", "a/b c")
        });

        Assert.Equal("search?q%20value=a%2Fb%20c", result);
    }
}
