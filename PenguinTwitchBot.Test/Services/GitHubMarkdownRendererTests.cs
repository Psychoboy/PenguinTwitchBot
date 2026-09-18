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
}
