using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using PenguinTwitchBot.Bot.Overlay;
using PenguinTwitchBot.Controllers;
using PenguinTwitchBot.Database.Bot.Models;
using PenguinTwitchBot.Database.Bot.Models.Overlay;
using PenguinTwitchBot.Database.Bot.Core.Database;
using PenguinTwitchBot.Database.Repository;

namespace PenguinTwitchBot.Test.Controllers;

public class FireworksQueueControllerTests
{
    [Fact]
    public async Task ReportStatus_RejectsInvalidOverlayKeyBeforeLookingUpWidget()
    {
        var (controller, unitOfWork, security, _) = CreateController();
        security.ValidateKeyAsync("wrong-key").Returns(false);

        var result = await controller.ReportStatus(CreateRequest("wrong-key"));

        Assert.IsType<UnauthorizedResult>(result);
        await unitOfWork.OverlayWidgets.DidNotReceive().GetByIdAsync(Arg.Any<Guid>());
    }

    [Fact]
    public async Task ReportStatus_RejectsAWidgetThatIsNotFireworks()
    {
        var (controller, unitOfWork, security, _) = CreateController();
        security.ValidateKeyAsync("valid-key").Returns(true);
        var widgetId = Guid.NewGuid();
        var widget = new OverlayWidget { Id = widgetId, WidgetType = "alerts" };
        unitOfWork.OverlayWidgets.GetByIdAsync(widgetId).Returns(widget);

        var result = await controller.ReportStatus(CreateRequest("valid-key", widgetId));

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task ReportStatus_StoresValidWidgetQueueReport()
    {
        var (controller, unitOfWork, security, store) = CreateController();
        security.ValidateKeyAsync("valid-key").Returns(true);
        var widgetId = Guid.NewGuid();
        var widget = new OverlayWidget { Id = widgetId, WidgetType = "fireworks" };
        unitOfWork.OverlayWidgets.GetByIdAsync(widgetId).Returns(widget);
        var request = new FireworksQueueStatusRequest(
            widgetId,
            "valid-key",
            3,
            250,
            2,
            [new FireworksQueueJobStatus("job-1", 3)]);

        var result = await controller.ReportStatus(request);

        Assert.IsType<NoContentResult>(result);
        var status = Assert.IsType<FireworksQueueStatus>(store.Get(widgetId));
        Assert.Equal(3, status.PendingBursts);
        Assert.Equal(250, status.MaxQueuedBursts);
        Assert.Equal(240, status.MinLaunchGapMs);
        Assert.Equal(2, status.RejectedBurstCount);
        Assert.Equal("job-1", Assert.Single(status.Jobs).JobId);
    }

    [Fact]
    public async Task ReportStatus_AcceptsAuthenticatedStandaloneWidgetWithoutDatabaseRow()
    {
        var (controller, unitOfWork, security, store) = CreateController();
        security.ValidateKeyAsync("valid-key").Returns(true);
        var standaloneId = Guid.NewGuid();

        var result = await controller.ReportStatus(CreateRequest("valid-key", standaloneId) with
        {
            IsStandalone = true
        });

        Assert.IsType<NoContentResult>(result);
        await unitOfWork.OverlayWidgets.DidNotReceive().GetByIdAsync(standaloneId);
        var status = Assert.IsType<FireworksQueueStatus>(store.Get(standaloneId));
        Assert.True(status.IsStandalone);
    }

    [Fact]
    public async Task ReportStatus_RejectsPendingCountAboveCapacity()
    {
        var (controller, _, security, store) = CreateController();
        security.ValidateKeyAsync("valid-key").Returns(true);

        var result = await controller.ReportStatus(CreateRequest("valid-key") with
        {
            PendingBursts = 251,
            MaxQueuedBursts = 250
        });

        Assert.IsType<BadRequestResult>(result);
        Assert.Null(store.Get(FireworksWidgetId));
    }

    [Fact]
    public void FireworksQueueDefaults_ReadsAndBoundsPerWidgetCapacity()
    {
        Assert.Equal(250, FireworksQueueDefaults.GetMaxQueuedBursts(null));
        Assert.Equal(100, FireworksQueueDefaults.GetMaxQueuedBursts(null, 100));
        Assert.Equal(240, FireworksQueueDefaults.GetMinLaunchGapMs(null));
        Assert.Equal(150, FireworksQueueDefaults.GetMinLaunchGapMs("{\"minLaunchGapMs\":150}"));
        Assert.Equal(3000, FireworksQueueDefaults.GetMinLaunchGapMs("{\"minLaunchGapMs\":5000}"));
        Assert.Equal(42, FireworksQueueDefaults.GetMaxQueuedBursts("{\"maxQueuedBursts\":42}"));
        Assert.Equal(42, FireworksQueueDefaults.GetMaxQueuedBursts("{\"maxQueuedBursts\":42}", 100));
        Assert.Equal(2000, FireworksQueueDefaults.GetMaxQueuedBursts("{\"maxQueuedBursts\":5000}"));
        Assert.Equal(250, FireworksQueueDefaults.GetMaxQueuedBursts("not-json"));
    }

    [Fact]
    public async Task GlobalQueueCapSetting_PersistsAndClampsValues()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var context = new ApplicationDbContext(options);
        await context.Database.EnsureCreatedAsync();
        var unitOfWork = new UnitOfWork(context);

        Assert.Equal(250, await FireworksQueueDefaults.GetGlobalMaxQueuedBurstsAsync(unitOfWork));
        Assert.Equal(2000, await FireworksQueueDefaults.SetGlobalMaxQueuedBurstsAsync(unitOfWork, 5000));
        Assert.Equal(2000, await FireworksQueueDefaults.GetGlobalMaxQueuedBurstsAsync(unitOfWork));
        Assert.Equal(80, await FireworksQueueDefaults.SetGlobalMaxQueuedBurstsAsync(unitOfWork, 80));
        Assert.Equal(80, await FireworksQueueDefaults.GetGlobalMaxQueuedBurstsAsync(unitOfWork));

        var storedSettings = await unitOfWork.Settings.GetAsync(setting => setting.Name == FireworksQueueDefaults.GlobalMaxQueuedBurstsSettingName);
        Assert.Equal(Setting.DataTypeEnum.Int, Assert.Single(storedSettings).DataType);
    }

    private static Guid FireworksWidgetId { get; } = Guid.NewGuid();

    private static (FireworksQueueController Controller, IUnitOfWork UnitOfWork, IOverlaySecurityService Security, FireworksQueueStatusStore Store) CreateController()
    {
        var unitOfWork = Substitute.For<IUnitOfWork>();
        var widgetRepository = Substitute.For<IOverlayWidgetRepository>();
        unitOfWork.OverlayWidgets.Returns(widgetRepository);
        var security = Substitute.For<IOverlaySecurityService>();
        var store = new FireworksQueueStatusStore();
        var controller = new FireworksQueueController(unitOfWork, security, store);
        return (controller, unitOfWork, security, store);
    }

    private static FireworksQueueStatusRequest CreateRequest(string overlayKey, Guid? widgetId = null) => new(
        widgetId ?? FireworksWidgetId,
        overlayKey,
        3,
        250,
        0,
        []);
}