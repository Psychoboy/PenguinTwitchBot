using System.Security.Claims;
using Bunit;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using MudBlazor.Services;
using NSubstitute;
using PenguinTwitchBot.Database.Bot.Core.Database;
using PenguinTwitchBot.Database.Repository;
using PenguinTwitchBot.Services;
using PenguinTwitchBot.Shared;

namespace PenguinTwitchBot.Test.Services;

public sealed class AnnouncementServiceTests : IDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly ServiceProvider _services;
    private readonly AnnouncementService _service;
    private static readonly ClaimsPrincipal Streamer = User("streamer", "Streamer");
    private static readonly ClaimsPrincipal Viewer = User("viewer", "Viewer");

    public AnnouncementServiceTests()
    {
        _connection.Open();
        _services = new ServiceCollection()
            .AddDbContext<ApplicationDbContext>(options => options.UseSqlite(_connection))
            .AddScoped<IUnitOfWork, UnitOfWork>()
            .BuildServiceProvider();
        using var scope = _services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.EnsureCreated();
        _service = new AnnouncementService(_services.GetRequiredService<IServiceScopeFactory>());
    }

    [Fact]
    public void DateWindowUsesInclusiveLocalCalendarDays()
    {
        var item = new Announcement { StartDate = new DateTime(2026, 11, 7), EndDate = new DateTime(2026, 11, 8) };
        Assert.False(item.IsVisible(new DateTime(2026, 11, 6, 23, 59, 59)));
        Assert.True(item.IsVisible(new DateTime(2026, 11, 7, 0, 0, 0)));
        Assert.True(item.IsVisible(new DateTime(2026, 11, 8, 23, 59, 59)));
        Assert.False(item.IsVisible(new DateTime(2026, 11, 9, 0, 0, 0)));
        Assert.True(new Announcement().IsVisible(DateTime.Now));
    }

    [Fact]
    public async Task OnlyStreamerCanChangeAnnouncements()
    {
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _service.SaveAsync(Viewer, new Announcement { Message = "Hello" }));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _service.DeleteAsync(Viewer, Guid.NewGuid()));
        await Assert.ThrowsAsync<ArgumentException>(() => _service.SaveAsync(Streamer, new Announcement { Message = " " }));
        await Assert.ThrowsAsync<ArgumentException>(() => _service.SaveAsync(Streamer, new Announcement { Message = "Hello", StartDate = new DateTime(2026, 11, 8), EndDate = new DateTime(2026, 11, 7) }));
        Assert.Empty(await _service.GetAnnouncementsAsync());
    }

    [Fact]
    public async Task DismissalsPersistPerAccountAndEditsCreateNewRevision()
    {
        var item = new Announcement { Message = "First" };
        await _service.SaveAsync(Streamer, item);
        var firstRevision = item.Revision;
        await _service.DismissAsync(Viewer, firstRevision);
        var anotherService = new AnnouncementService(_services.GetRequiredService<IServiceScopeFactory>());
        Assert.Contains(firstRevision, await anotherService.GetDismissalsAsync(Viewer));
        Assert.Empty(await anotherService.GetDismissalsAsync(User("other", "Viewer")));
        Assert.Empty(await anotherService.GetDismissalsAsync(new ClaimsPrincipal()));

        item.Message = "Updated";
        await _service.SaveAsync(Streamer, item);
        Assert.NotEqual(firstRevision, item.Revision);
        Assert.DoesNotContain(item.Revision, await anotherService.GetDismissalsAsync(Viewer));
        Assert.Equal("Updated", Assert.Single(await anotherService.GetAnnouncementsAsync()).Message);
        await _service.DeleteAsync(Streamer, item.Id);
        Assert.Empty(await anotherService.GetAnnouncementsAsync());
    }

    [Fact]
    public async Task BannerRendersSafeMarkdownAndDismissesLocally()
    {
        await _service.SaveAsync(Streamer, new Announcement { Message = "**Important** <script>alert(1)</script> [bad](javascript:alert)\n\n| A | B |\n| --- | --- |\n| one | two |\n\n![image](/media/picture.png)" });
        await using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices();
        context.Services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        context.Services.AddSingleton(_service);
        context.AddAuthorization().SetNotAuthorized();
        var banner = context.Render<AnnouncementBanners>();
        await banner.WaitForAssertionAsync(() => Assert.Contains("<strong>Important</strong>", banner.Markup));
        Assert.Single(banner.FindAll(".announcement-message table"));
        Assert.Equal("/media/picture.png", banner.Find(".announcement-message img").GetAttribute("src"));
        Assert.Empty(banner.FindAll("script"));
        Assert.DoesNotContain("href=\"javascript:", banner.Markup, StringComparison.OrdinalIgnoreCase);
        await banner.Find("button[aria-label='Dismiss announcement']").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
        Assert.Empty(banner.FindAll(".announcement-message"));
        Assert.Contains(context.JSInterop.Invocations, invocation => invocation.Identifier == "localStorage.setItem");
    }

    [Fact]
    public async Task BannerRespectsPreviouslyDismissedBrowserState()
    {
        await _service.SaveAsync(Streamer, new Announcement { Message = "Already dismissed" });
        await using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.JSInterop.Setup<string?>("localStorage.getItem", _ => true).SetResult("dismissed");
        context.Services.AddMudServices();
        context.Services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        context.Services.AddSingleton(_service);
        context.AddAuthorization().SetNotAuthorized();
        var banner = context.Render<AnnouncementBanners>();
        await banner.WaitForAssertionAsync(() => Assert.Contains(context.JSInterop.Invocations, invocation => invocation.Identifier == "localStorage.getItem"));
        Assert.Empty(banner.FindAll(".announcement-message"));
    }

    [Fact]
    public async Task BrowserStorageTimeoutIsLoggedAsTimeout()
    {
        await _service.SaveAsync(Streamer, new Announcement { Message = "Hello" });
        await using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.JSInterop.Setup<string?>("localStorage.getItem", _ => true).SetException(new TaskCanceledException());
        context.Services.AddMudServices();
        context.Services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        context.Services.AddSingleton(_service);
        var logger = Substitute.For<ILogger<AnnouncementBanners>>();
        context.Services.AddSingleton(logger);
        context.AddAuthorization().SetNotAuthorized();
        var banner = context.Render<AnnouncementBanners>();
        await banner.WaitForAssertionAsync(() => Assert.True(HasWarning(logger, "browser storage did not respond")));
    }

    [Fact]
    public async Task DisposingBannerCancelsPendingBrowserStorageWithoutWarning()
    {
        await _service.SaveAsync(Streamer, new Announcement { Message = "Hello" });
        var context = new BunitContext();
        context.Services.AddMudServices();
        context.Services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        context.Services.AddSingleton(_service);
        var logger = Substitute.For<ILogger<AnnouncementBanners>>();
        context.Services.AddSingleton(logger);
        var runtime = Substitute.For<IJSRuntime>();
        var pending = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        runtime.InvokeAsync<string?>("localStorage.getItem", Arg.Any<CancellationToken>(), Arg.Any<object?[]>())
            .Returns(call =>
            {
                var token = call.Arg<CancellationToken>();
                token.Register(() => pending.TrySetCanceled(token));
                return new ValueTask<string?>(pending.Task);
            });
        context.Services.AddSingleton(runtime);
        context.AddAuthorization().SetNotAuthorized();
        var banner = context.Render<AnnouncementBanners>();
        await banner.WaitForAssertionAsync(() => Assert.Contains(runtime.ReceivedCalls(), call => call.GetMethodInfo().Name == "InvokeAsync"));
        await context.DisposeAsync();
        Assert.True(pending.Task.IsCanceled);
        Assert.False(HasWarning(logger, "Unable to load announcements"));
        Assert.False(HasWarning(logger, "browser storage did not respond"));
    }

    [Fact]
    public async Task BrowserWriteTimeoutStillSavesAccountDismissal()
    {
        var announcement = new Announcement { Message = "Hello" };
        await _service.SaveAsync(Streamer, announcement);
        await using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.JSInterop.SetupVoid("localStorage.setItem", _ => true).SetException(new TaskCanceledException());
        context.Services.AddMudServices();
        context.Services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        context.Services.AddSingleton(_service);
        var logger = Substitute.For<ILogger<AnnouncementBanners>>();
        context.Services.AddSingleton(logger);
        var auth = context.AddAuthorization();
        auth.SetAuthorized("viewer");
        auth.SetClaims(new Claim("UserId", "viewer"));
        var banner = context.Render<AnnouncementBanners>();
        await banner.WaitForAssertionAsync(() => Assert.NotEmpty(banner.FindAll(".announcement-message")));
        await banner.Find("button[aria-label='Dismiss announcement']").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
        Assert.Contains(announcement.Revision, await _service.GetDismissalsAsync(Viewer));
        Assert.True(HasWarning(logger, "dismissal could not be saved in browser storage"));
    }

    [Fact]
    public async Task AnnouncementEditorPreviewsAndSavesMarkdown()
    {
        await using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices(options => options.PopoverOptions.CheckForPopoverProvider = false);
        context.Services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        context.Services.AddSingleton(_service);
        context.Services.AddSingleton(new MediaUploadService(Path.GetTempPath(), NullLogger<MediaUploadService>.Instance));
        var auth = context.AddAuthorization();
        auth.SetAuthorized("streamer");
        auth.SetRoles("Streamer");
        var page = context.Render<PenguinTwitchBot.Pages.Settings.Announcements>();
        await page.FindAll("button").First(button => button.TextContent.Contains("Add announcement")).ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
        Assert.True(page.FindComponent<GitHubMarkdownEditor>().Instance.SafePreview);
        const string markdown = "**Update**\n\n| A | B |\n| --- | --- |\n| one | two |";
        await page.Find("textarea").ChangeAsync(new Microsoft.AspNetCore.Components.ChangeEventArgs { Value = markdown });
        await page.FindAll("button").First(button => button.TextContent.Trim() == "Preview").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
        Assert.Contains("<strong>Update</strong>", page.Markup);
        Assert.Contains("<table>", page.Markup);
        await page.FindAll("button").First(button => button.TextContent.Trim() == "Save").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
        Assert.Equal(markdown, Assert.Single(await _service.GetAnnouncementsAsync()).Message);
    }

    private static ClaimsPrincipal User(string id, string role) => new(new ClaimsIdentity(
        [new Claim("UserId", id), new Claim(ClaimTypes.Role, role)], "test"));

    private static bool HasWarning(ILogger<AnnouncementBanners> logger, string message) =>
        logger.ReceivedCalls().Any(call => call.GetMethodInfo().Name == "Log" &&
            call.GetArguments()[0] is LogLevel.Warning &&
            call.GetArguments()[2]?.ToString()?.Contains(message, StringComparison.OrdinalIgnoreCase) == true);

    public void Dispose()
    {
        _services.Dispose();
        _connection.Dispose();
    }
}