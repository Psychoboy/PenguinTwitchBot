using PenguinTwitchBot.Helpers;
using Xunit;

namespace PenguinTwitchBot.Test.Services;

public class GitHubMarkdownRendererTests
{
    [Fact]
    public void RenderToHtml_EmptyOrNull_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, GitHubMarkdownRenderer.RenderToHtml(null));
        Assert.Equal(string.Empty, GitHubMarkdownRenderer.RenderToHtml(""));
        Assert.Equal(string.Empty, GitHubMarkdownRenderer.RenderToHtml("   "));
    }

    [Fact]
    public void RenderToHtml_BasicHeadingsAndLists_RendersProperHtml()
    {
        var md = @"## What's Changed
* Feature 1 by @coder
* Feature 2";

        var html = GitHubMarkdownRenderer.RenderToHtml(md);

        Assert.Contains("What's Changed</h2>", html);
        Assert.Contains("<ul>", html);
        Assert.Contains("<li>Feature 1 by @coder", html);
        Assert.DoesNotContain("class=\"gh-mention\"", html);
    }

    [Fact]
    public void RenderToHtml_ExternalLinks_AddsTargetBlankAndNoopener()
    {
        var md = "[Release](https://github.com/Psychoboy/PenguinTwitchBot/releases/v0.2.0)";
        var html = GitHubMarkdownRenderer.RenderToHtml(md);

        Assert.Contains(@"target=""_blank""", html);
        Assert.Contains(@"rel=""noopener noreferrer""", html);
        Assert.Contains(@"href=""https://github.com/Psychoboy/PenguinTwitchBot/releases/v0.2.0""", html);
    }

    [Fact]
    public void RenderToHtml_CodeBlocksAndInlineCode_RendersProperly()
    {
        var md = @"Here is code `@foo` and `#123`:
```csharp
var mention = ""@developer"";
var issue = ""#999"";
```";
        var html = GitHubMarkdownRenderer.RenderToHtml(md);

        Assert.Contains("<code>@foo</code>", html);
        Assert.Contains("<code>#123</code>", html);
        Assert.Contains("<pre><code class=\"language-csharp\">", html);
    }

    [Fact]
    public void RenderToHtml_TaskLists_RendersCheckboxes()
    {
        var md = @"- [x] Completed task
- [ ] Incomplete task";

        var html = GitHubMarkdownRenderer.RenderToHtml(md);

        Assert.Contains(@"type=""checkbox""", html);
        Assert.Contains("disabled", html);
    }

    [Fact]
    public void RenderToHtml_GitHubAlerts_TransformsToCallouts()
    {
        var md = @"> [!NOTE]
> This is a helpful note.";

        var html = GitHubMarkdownRenderer.RenderToHtml(md);

        Assert.Contains("markdown-alert-note", html);
        Assert.Contains("markdown-alert-title", html);
        Assert.Contains("This is a helpful note.", html);
    }

    [Fact]
    public void RenderToHtml_Tables_RendersHtmlTable()
    {
        var md = @"| Header 1 | Header 2 |
| --- | --- |
| Cell 1 | Cell 2 |";

        var html = GitHubMarkdownRenderer.RenderToHtml(md);

        Assert.Contains("<table>", html);
        Assert.Contains("<th>Header 1</th>", html);
        Assert.Contains("<td>Cell 1</td>", html);
    }

    [Fact]
    public void RenderToHtml_Emojis_RendersProperly()
    {
        var md = ":star: :speech_balloon: :bust_in_silhouette: :smile:";
        var html = GitHubMarkdownRenderer.RenderToHtml(md);
        Assert.Contains("⭐ 💬 👤 😄", html);
    }

    [Fact]
    public void RenderToHtml_BracketIcon_RendersSvgWithColorAndSize()
    {
        var md = "[icon:person #3299ff 1.5em] +1 Tickets";
        var html = GitHubMarkdownRenderer.RenderToHtml(md);

        Assert.Contains("<svg", html);
        Assert.Contains("fill: #3299ff", html);
        Assert.Contains(@"width=""1.5em""", html);
        Assert.Contains(@"height=""1.5em""", html);
        Assert.Contains("+1 Tickets", html);
    }

    [Fact]
    public void RenderToHtml_ColonIcon_RendersSvgWithColor()
    {
        var md = ":icon:chat:#0bba83: +5 Tickets";
        var html = GitHubMarkdownRenderer.RenderToHtml(md);

        Assert.Contains("<svg", html);
        Assert.Contains("fill: #0bba83", html);
        Assert.Contains("+5 Tickets", html);
    }

    [Fact]
    public void RenderToHtml_TwitchBrandIcon_RendersBrandSvg()
    {
        var md = "[icon:twitch #9146ff] Follow us";
        var html = GitHubMarkdownRenderer.RenderToHtml(md);

        Assert.Contains("<svg", html);
        Assert.Contains("fill: #9146ff", html);
        Assert.Contains("Follow us", html);
    }

    [Fact]
    public void RenderToHtml_UnknownIcon_LeavesTextIntact()
    {
        var md = "[icon:nonexistent_icon_12345] Hello";
        var html = GitHubMarkdownRenderer.RenderToHtml(md);

        Assert.Contains("[icon:nonexistent_icon_12345]", html);
    }

    [Theory]
    [InlineData("[icon:star style=outlined #ffa800 1.5em]")]
    [InlineData("[icon:star style=rounded #ffa800 1.5em]")]
    [InlineData("[icon:star style=sharp #ffa800 1.5em]")]
    [InlineData("[icon:star style=twotone #ffa800 1.5em]")]
    [InlineData("[icon:outlined:star #ffa800 1.5em]")]
    [InlineData("[icon:star:sharp #ffa800 1.5em]")]
    [InlineData(":icon:star:#ffa800:1.5em:outlined:")]
    public void RenderToHtml_IconStyles_RendersValidSvg(string markdown)
    {
        var html = GitHubMarkdownRenderer.RenderToHtml(markdown);

        Assert.Contains("<svg", html);
        Assert.Contains("fill: #ffa800", html);
        Assert.Contains(@"width=""1.5em""", html);
    }

    [Fact]
    public void MarkdownIconResolver_DifferentStyles_ProduceDifferentSvg()
    {
        var filledSvg = MarkdownIconResolver.RenderSvg("star", style: MaterialIconStyle.Filled);
        var outlinedSvg = MarkdownIconResolver.RenderSvg("star", style: MaterialIconStyle.Outlined);
        var roundedSvg = MarkdownIconResolver.RenderSvg("star", style: MaterialIconStyle.Rounded);
        var sharpSvg = MarkdownIconResolver.RenderSvg("star", style: MaterialIconStyle.Sharp);
        var twoToneSvg = MarkdownIconResolver.RenderSvg("star", style: MaterialIconStyle.TwoTone);

        Assert.NotEmpty(filledSvg);
        Assert.NotEmpty(outlinedSvg);
        Assert.NotEmpty(roundedSvg);
        Assert.NotEmpty(sharpSvg);
        Assert.NotEmpty(twoToneSvg);
        Assert.NotEqual(filledSvg, outlinedSvg);
    }

    [Theory]
    [InlineData("[icon:Icons.Custom.Brands.Steam #3299ff 1.5em]")]
    [InlineData("[icon:Custom.Brands.Steam #3299ff 1.5em]")]
    [InlineData("[icon:Brands:Steam #3299ff 1.5em]")]
    [InlineData("[icon:Brands.Steam #3299ff 1.5em]")]
    [InlineData("[icon:steam #3299ff 1.5em]")]
    [InlineData("[icon:Icons.Custom.FileFormats.FilePdf #0bba83 1.5em]")]
    [InlineData("[icon:FileFormats.FilePdf #0bba83 1.5em]")]
    [InlineData("[icon:file_pdf #0bba83 1.5em]")]
    [InlineData("[icon:Icons.Custom.Uncategorized.BioHazard #ff5722 1.5em]")]
    [InlineData("[icon:Uncategorized.BioHazard #ff5722 1.5em]")]
    [InlineData("[icon:biohazard #ff5722 1.5em]")]
    public void RenderToHtml_CustomIcons_ResolvesAndRendersSvg(string markdown)
    {
        var html = GitHubMarkdownRenderer.RenderToHtml(markdown);

        Assert.Contains("<svg", html);
        Assert.DoesNotContain("[icon:", html);
    }

    [Fact]
    public void MarkdownIconResolver_CheckIcon_ResolvesExactMaterialCheck()
    {
        // 1. Direct name "Check"
        Assert.True(MarkdownIconResolver.TryResolveIcon("Check", out var checkSvg));
        Assert.Equal(MudBlazor.Icons.Material.Filled.Check, checkSvg);

        // 2. Full C# path "Icons.Material.Filled.Check"
        Assert.True(MarkdownIconResolver.TryResolveIcon("Icons.Material.Filled.Check", out var fullPathSvg));
        Assert.Equal(MudBlazor.Icons.Material.Filled.Check, fullPathSvg);

        // 3. Search for "Check" in "All Material Icons"
        var results = MarkdownIconResolver.SearchIcons("Check", "All Material Icons").ToList();
        Assert.Contains(results, i => i.Name.Equals("Check", StringComparison.OrdinalIgnoreCase));

        // 4. Search with C# path "Icons.Material.Filled.Check"
        var pathResults = MarkdownIconResolver.SearchIcons("Icons.Material.Filled.Check", "All Material Icons").ToList();
        Assert.Contains(pathResults, i => i.Name.Equals("Check", StringComparison.OrdinalIgnoreCase));

        // 5. Check differs from CheckCircle
        Assert.NotEqual(MudBlazor.Icons.Material.Filled.CheckCircle, checkSvg);
    }

    [Fact]
    public void RenderToHtml_IconsInsideCodeAndPre_Skipped()
    {
        var md = @"Here is inline code: `[icon:star #ffa800 1.5em]` and `:icon:chat:#0bba83:1.5em:`.
And a fenced code block:
```markdown
[icon:ticket #3299ff]
:icon:bell:#ff5722:
```
Normal icon outside: [icon:heart #f85149]";

        var html = GitHubMarkdownRenderer.RenderToHtml(md);

        // Inside code and pre, raw text is preserved
        Assert.Contains("<code>[icon:star #ffa800 1.5em]</code>", html);
        Assert.Contains("<code>:icon:chat:#0bba83:1.5em:</code>", html);
        Assert.Contains("[icon:ticket #3299ff]", html);
        Assert.Contains(":icon:bell:#ff5722:", html);

        // Outside code, normal icon is rendered to SVG
        Assert.Contains("fill: #f85149", html);
    }

    [Fact]
    public void MarkdownIconResolver_CrownAndMedal_ResolveCorrectSvgs()
    {
        // "crown" should resolve to the 5-pointed jewel crown (ChessQueen), not MilitaryTech
        Assert.True(MarkdownIconResolver.TryResolveIcon("crown", out var crownSvg));
        Assert.Equal(MudBlazor.Icons.Custom.Uncategorized.ChessQueen, crownSvg);
        Assert.NotEqual(MudBlazor.Icons.Material.Filled.MilitaryTech, crownSvg);

        // "queen" should also resolve to ChessQueen
        Assert.True(MarkdownIconResolver.TryResolveIcon("queen", out var queenSvg));
        Assert.Equal(MudBlazor.Icons.Custom.Uncategorized.ChessQueen, queenSvg);

        // "medal" / "ribbon" should resolve to MilitaryTech
        Assert.True(MarkdownIconResolver.TryResolveIcon("medal", out var medalSvg));
        Assert.Equal(MudBlazor.Icons.Material.Filled.MilitaryTech, medalSvg);

        Assert.True(MarkdownIconResolver.TryResolveIcon("ribbon", out var ribbonSvg));
        Assert.Equal(MudBlazor.Icons.Material.Filled.MilitaryTech, ribbonSvg);

        // Render HTML checks
        var crownHtml = GitHubMarkdownRenderer.RenderToHtml("[icon:crown #ffc107 1.5em]");
        Assert.Contains(crownSvg, crownHtml);
        Assert.DoesNotContain(MudBlazor.Icons.Material.Filled.MilitaryTech, crownHtml);

        var medalHtml = GitHubMarkdownRenderer.RenderToHtml("[icon:medal #ffd700 1.5em]");
        Assert.Contains(medalSvg, medalHtml);
    }

    [Theory]
    [InlineData("[icon:person #3299ff 1.5em]", "#3299ff")]
    [InlineData("[icon:chat #0bba83 1.5em]", "#0bba83")]
    [InlineData("[icon:star #ffa800 1.5em]", "#ffa800")]
    [InlineData("[icon:ticket #3299ff 1.5em]", "#3299ff")]
    [InlineData("[icon:gift #e91e63 1.5em]", "#e91e63")]
    [InlineData("[icon:trophy #ffd700 1.5em]", "#ffd700")]
    [InlineData("[icon:diamond #00bcd4 1.5em]", "#00bcd4")]
    [InlineData("[icon:fire #ff5722 1.5em]", "#ff5722")]
    [InlineData("[icon:crown #ffc107 1.5em]", "#ffc107")]
    [InlineData("[icon:medal #ffd700 1.5em]", "#ffd700")]
    [InlineData("[icon:twitch #9146ff 1.5em]", "#9146ff")]
    [InlineData("[icon:discord #5865f2 1.5em]", "#5865f2")]
    public void RenderToHtml_AllEditorSampleChips_RenderMatchingValidSvg(string markdown, string expectedColor)
    {
        var html = GitHubMarkdownRenderer.RenderToHtml(markdown);

        Assert.Contains("<svg", html);
        Assert.True(html.Contains("<path") || html.Contains("<polygon") || html.Contains("<rect") || html.Contains("<circle"), $"HTML does not contain SVG elements: {html}");
        Assert.Contains($"fill: {expectedColor}", html);
        Assert.DoesNotContain("[icon:", html);
    }

    [Fact]
    public void MarkdownIconResolver_AllCuratedCatalogIcons_ResolveNonEmptySvg()
    {
        var catalog = MarkdownIconResolver.GetAllIcons();
        Assert.NotEmpty(catalog);

        foreach (var icon in catalog)
        {
            var svg = icon.GetSvgPath();
            Assert.False(string.IsNullOrWhiteSpace(svg), $"Icon '{icon.Name}' in category '{icon.Category}' failed to resolve an SVG path.");
        }
    }

    [Fact]
    public void MarkdownIconResolver_AllSampleChipsAndAliases_ResolveValidSvg()
    {
        // Test all 12 sample chips from GitHubMarkdownEditor toolbar
        var sampleChips = new[]
        {
            "person", "chat", "star", "ticket", "gift", "trophy",
            "diamond", "fire", "crown", "medal", "twitch", "discord"
        };

        foreach (var chip in sampleChips)
        {
            Assert.True(MarkdownIconResolver.TryResolveIcon(chip, out var svg), $"Sample chip '{chip}' failed to resolve.");
            Assert.False(string.IsNullOrWhiteSpace(svg), $"Sample chip '{chip}' resolved to empty SVG.");
        }

        // Test common aliases
        var aliases = new[]
        {
            "user", "users", "people", "message", "forum", "comment", "sub",
            "gem", "flame", "queen", "king", "coin", "game", "bell", "heart",
            "clock", "check", "warning", "bolt", "shield", "lock", "volume", "music"
        };

        foreach (var alias in aliases)
        {
            Assert.True(MarkdownIconResolver.TryResolveIcon(alias, out var svg), $"Alias '{alias}' failed to resolve.");
            Assert.False(string.IsNullOrWhiteSpace(svg), $"Alias '{alias}' resolved to empty SVG.");
        }
    }

    [Fact]
    public void RenderToHtml_RelativeImage_RendersImgTagWithRelativeSrc()
    {
        var md = "![Giveaway Prize](/media/ps5_console.png)";
        var html = GitHubMarkdownRenderer.RenderToHtml(md);

        Assert.Contains(@"<img src=""/media/ps5_console.png"" alt=""Giveaway Prize""", html);
    }

    [Fact]
    public void RenderToHtml_LinkedRelativeImage_RendersLinkAndImgTag()
    {
        var md = "[![Giveaway Prize](/media/ps5_console.png)](https://twitch.tv)";
        var html = GitHubMarkdownRenderer.RenderToHtml(md);

        Assert.Contains(@"<a href=""https://twitch.tv""", html);
        Assert.Contains(@"target=""_blank""", html);
        Assert.Contains(@"<img src=""/media/ps5_console.png"" alt=""Giveaway Prize""", html);
    }
}
