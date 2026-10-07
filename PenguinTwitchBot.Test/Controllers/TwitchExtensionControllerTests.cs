using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using NSubstitute;
using PenguinTwitchBot.Bot.Commands;
using PenguinTwitchBot.Bot.Commands.Alias;
using PenguinTwitchBot.Bot.Commands.Features;
using PenguinTwitchBot.Bot.Commands.Fishing;
using PenguinTwitchBot.Bot.Core;
using PenguinTwitchBot.Bot.Core.Points;
using PenguinTwitchBot.Bot.Features;
using PenguinTwitchBot.Bot.TwitchServices;
using PenguinTwitchBot.Controllers;
using PenguinTwitchBot.Database.Bot.Models;
using PenguinTwitchBot.Database.Bot.Models.Commands;
using PenguinTwitchBot.Database.Bot.Models.Fishing;
using PenguinTwitchBot.Database.Bot.Models.Points;
using Xunit;

using Microsoft.AspNetCore.Mvc.Filters;
using PenguinTwitchBot.Circuit;
using System.Net;

namespace PenguinTwitchBot.Test.Controllers;

public class TwitchExtensionControllerTests
{
    private readonly IFishingService _fishingService = Substitute.For<IFishingService>();
    private readonly IFishingShopService _fishingShopService = Substitute.For<IFishingShopService>();
    private readonly IFishingInventoryService _fishingInventoryService = Substitute.For<IFishingInventoryService>();
    private readonly IGiveawayFeature _giveawayFeature = Substitute.For<IGiveawayFeature>();
    private readonly IPointsSystem _pointsSystem = Substitute.For<IPointsSystem>();
    private readonly ICommandHandler _commandHandler = Substitute.For<ICommandHandler>();
    private readonly IActionCommandService _actionCommandService = Substitute.For<IActionCommandService>();
    private readonly IAlias _aliases = Substitute.For<IAlias>();
    private readonly IFeatureRuntimeCoordinator _featureCoordinator = Substitute.For<IFeatureRuntimeCoordinator>();
    private readonly IViewerFeature _viewerFeature = Substitute.For<IViewerFeature>();
    private readonly ITwitchService _twitchService = Substitute.For<ITwitchService>();
    private readonly IIpLog _ipLog = Substitute.For<IIpLog>();
    private readonly IConfiguration _configuration = Substitute.For<IConfiguration>();
    private readonly ILogger<TwitchExtensionController> _logger = Substitute.For<ILogger<TwitchExtensionController>>();

    private TwitchExtensionController CreateController(string? authHeader = null)
    {
        var scopeFactory = Substitute.For<IServiceScopeFactory>();
        var leaderboards = new Leaderboards(scopeFactory);

        var controller = new TwitchExtensionController(
            _fishingService,
            _fishingShopService,
            _fishingInventoryService,
            _giveawayFeature,
            _pointsSystem,
            leaderboards,
            _commandHandler,
            _actionCommandService,
            _aliases,
            _featureCoordinator,
            _viewerFeature,
            _twitchService,
            _ipLog,
            _configuration,
            _logger);

        var httpContext = new DefaultHttpContext();
        if (!string.IsNullOrWhiteSpace(authHeader))
        {
            httpContext.Request.Headers["Authorization"] = authHeader;
        }

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext,
            RouteData = new Microsoft.AspNetCore.Routing.RouteData(),
            ActionDescriptor = new Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor()
        };

        return controller;
    }

    private static string GenerateTestJwt(string channelId, string userId, string? secretBase64 = null)
    {
        var handler = new JwtSecurityTokenHandler();
        var claims = new List<Claim>
        {
            new("channel_id", channelId),
            new("user_id", userId),
            new("opaque_user_id", "U" + userId),
            new("role", "viewer")
        };

        SigningCredentials? credentials = null;
        if (!string.IsNullOrWhiteSpace(secretBase64))
        {
            var keyBytes = Convert.FromBase64String(secretBase64);
            credentials = new SigningCredentials(new SymmetricSecurityKey(keyBytes), SecurityAlgorithms.HmacSha256);
        }

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddMinutes(30),
            SigningCredentials = credentials
        };

        var token = handler.CreateToken(tokenDescriptor);
        return handler.WriteToken(token);
    }

    [Fact]
    public void GetFeatures_ReturnsLiveCoordinatorStatuses()
    {
        _featureCoordinator.IsEnabled(FeatureKeys.Fishing).Returns(true);
        _featureCoordinator.IsEnabled(FeatureKeys.GiveawayFeature).Returns(false);
        _featureCoordinator.IsEnabled(FeatureKeys.PointsSystem).Returns(true);

        var controller = CreateController();
        var result = controller.GetFeatures();

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ExtensionFeaturesResponse>(ok.Value);

        Assert.True(response.Fishing);
        Assert.False(response.Giveaway);
        Assert.True(response.Points);
        Assert.True(response.Leaderboards);
        Assert.True(response.Commands);
    }

    [Fact]
    public async Task GetGiveaway_ReturnsPublicInformation_WithoutAuthentication()
    {
        _giveawayFeature.IsClosed().Returns(false);
        _giveawayFeature.GetPrize().Returns("Epic Gaming Mouse");
        _giveawayFeature.GetImageUrl().Returns("https://example.com/mouse.png");
        _giveawayFeature.GetPointsPerEntry().Returns(100);
        _giveawayFeature.GetRules().Returns("Must be in chat.");
        _giveawayFeature.GetPrizeAdditionalDetails().Returns("Ships worldwide.");
        _giveawayFeature.GetEntrantsCount().Returns(42);
        _giveawayFeature.GetEntriesCount().Returns(150);

        var controller = CreateController();
        var result = await controller.GetGiveaway();

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ExtensionGiveawayResponse>(ok.Value);

        Assert.False(response.IsClosed);
        Assert.Equal("Epic Gaming Mouse", response.Prize);
        Assert.Equal("https://example.com/mouse.png", response.ImageUrl);
        Assert.Equal(100, response.PointsPerEntry);
        Assert.Equal(42, response.EntrantsCount);
        Assert.Equal(150, response.EntriesCount);
    }

    [Fact]
    public async Task GetGiveawayViewer_ReturnsUnauthorized_WhenAuthHeaderIsMissing()
    {
        var controller = CreateController(authHeader: null);
        var result = await controller.GetGiveawayViewer();

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task GetGiveawayViewer_ReturnsViewerBalance_WhenAuthenticated()
    {
        var jwt = GenerateTestJwt("12345", "67890");
        var controller = CreateController(authHeader: $"Bearer {jwt}");

        var viewer = new Viewer { Id = 1, UserId = "67890", Username = "luckyviewer", DisplayName = "LuckyViewer" };
        _viewerFeature.GetViewerByUserId("67890").Returns(viewer);
        _giveawayFeature.GetPointsPerEntry().Returns(50);
        _pointsSystem.GetUserPointsByUsernameAndGame("luckyviewer", "GiveawayFeature")
            .Returns(new UserPoints { Points = 250 });
        _giveawayFeature.GetEntriesCount("luckyviewer").Returns(2);

        var result = await controller.GetGiveawayViewer();

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ExtensionGiveawayViewerResponse>(ok.Value);

        Assert.Equal("LuckyViewer", response.Username);
        Assert.Equal(250, response.UserTickets);
        Assert.Equal(2, response.UserEntries);
        Assert.Equal(50, response.PointsPerEntry);
        Assert.Equal(5, response.MaxAffordableEntries); // 250 / 50 = 5
    }

    [Fact]
    public async Task GetFishingStore_ReturnsShopItemsList()
    {
        var items = new List<FishingShopItem>
        {
            new()
            {
                Id = 1,
                Name = "Carbon Rod",
                Description = "Light and durable",
                Cost = 150,
                EquipmentSlot = EquipmentSlot.Rod,
                BoostType = FishingBoostType.GeneralRarityBoost,
                BoostAmount = 15.0,
                MaxUses = -1,
                MaxDurability = 100
            }
        };

        _fishingShopService.GetAllShopItems().Returns(items);

        var controller = CreateController();
        var result = await controller.GetFishingStore();

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<List<ExtensionFishingShopItemResponse>>(ok.Value);

        Assert.Single(response);
        Assert.Equal("Carbon Rod", response[0].Name);
        Assert.Equal(150, response[0].Cost);
        Assert.Equal("Rod", response[0].EquipmentSlot);
    }

    [Fact]
    public async Task GetCommands_OnlyReturnsCommandsBelowModeratorRank()
    {
        var defaultCmds = new List<DefaultCommand>
        {
            new() { CustomCommandName = "fish", MinimumRank = Rank.Viewer, Disabled = false, ExcludeFromUi = false, Description = "Go fishing" },
            new() { CustomCommandName = "ban", MinimumRank = Rank.Moderator, Disabled = false, ExcludeFromUi = false, Description = "Ban a user" },
            new() { CustomCommandName = "restart", MinimumRank = Rank.Streamer, Disabled = false, ExcludeFromUi = false, Description = "Restart bot" }
        };

        _commandHandler.GetDefaultCommandsFromDb().Returns(defaultCmds);
        _pointsSystem.GetPointTypes().Returns(new List<PointType>());
        _actionCommandService.GetAllAsync().Returns(new List<ActionCommand>());
        _commandHandler.GetExternalCommands().Returns(new List<ExternalCommands>());

        var controller = CreateController();
        var result = await controller.GetCommands();

        var ok = Assert.IsType<OkObjectResult>(result);
        var list = Assert.IsType<List<ExtensionCommandResponse>>(ok.Value);

        Assert.Single(list);
        Assert.Equal("!fish", list[0].Command);
        Assert.DoesNotContain(list, c => c.Command == "!ban");
        Assert.DoesNotContain(list, c => c.Command == "!restart");
    }

    [Fact]
    public async Task EquipFishingItem_CallsInventoryService_WhenAuthorized()
    {
        var jwt = GenerateTestJwt("12345", "67890");
        var controller = CreateController(authHeader: $"Bearer {jwt}");

        var result = await controller.EquipFishingItem(new ExtensionEquipItemRequest(42));

        var ok = Assert.IsType<OkObjectResult>(result);
        await _fishingInventoryService.Received(1).EquipItem("67890", 42);
    }

    [Fact]
    public async Task UnequipFishingItem_CallsInventoryService_WhenAuthorized()
    {
        var jwt = GenerateTestJwt("12345", "67890");
        var controller = CreateController(authHeader: $"Bearer {jwt}");

        var result = await controller.UnequipFishingItem(new ExtensionEquipItemRequest(42));

        var ok = Assert.IsType<OkObjectResult>(result);
        await _fishingInventoryService.Received(1).UnequipItem("67890", 42);
    }

    [Fact]
    public async Task OnActionExecutionAsync_LogsInteraction_WhenUserIsAuthenticatedAndNotCached()
    {
        var jwt = GenerateTestJwt("12345", "67890");
        var controller = CreateController(authHeader: $"Bearer {jwt}");
        controller.HttpContext.Connection.RemoteIpAddress = IPAddress.Parse("192.168.1.100");

        _ipLog.IsInteractionCached("67890", "192.168.1.100").Returns(false);
        _viewerFeature.GetViewerByUserId("67890").Returns(new Viewer { UserId = "67890", Username = "cooluser" });

        var executed = false;
        var actionExecutingContext = new ActionExecutingContext(
            controller.ControllerContext,
            new List<IFilterMetadata>(),
            new Dictionary<string, object?>(),
            controller);

        await controller.OnActionExecutionAsync(actionExecutingContext, () =>
        {
            executed = true;
            return Task.FromResult(new ActionExecutedContext(controller.ControllerContext, new List<IFilterMetadata>(), controller));
        });

        Assert.True(executed);
        await _ipLog.Received(1).LogInteractionAsync("cooluser", "67890", "192.168.1.100");
    }

    [Fact]
    public async Task OnActionExecutionAsync_SkipsDbLogging_WhenUserInteractionIsAlreadyCached()
    {
        var jwt = GenerateTestJwt("12345", "67890");
        var controller = CreateController(authHeader: $"Bearer {jwt}");
        controller.HttpContext.Connection.RemoteIpAddress = IPAddress.Parse("192.168.1.100");

        _ipLog.IsInteractionCached("67890", "192.168.1.100").Returns(true);

        var executed = false;
        var actionExecutingContext = new ActionExecutingContext(
            controller.ControllerContext,
            new List<IFilterMetadata>(),
            new Dictionary<string, object?>(),
            controller);

        await controller.OnActionExecutionAsync(actionExecutingContext, () =>
        {
            executed = true;
            return Task.FromResult(new ActionExecutedContext(controller.ControllerContext, new List<IFilterMetadata>(), controller));
        });

        Assert.True(executed);
        await _viewerFeature.DidNotReceive().GetViewerByUserId(Arg.Any<string>());
        await _ipLog.DidNotReceive().LogInteractionAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<TimeSpan?>());
    }

    [Fact]
    public async Task OnActionExecutionAsync_DoesNotLog_WhenUserIsAnonymous()
    {
        var controller = CreateController(authHeader: null);
        controller.HttpContext.Connection.RemoteIpAddress = IPAddress.Parse("192.168.1.100");

        var executed = false;
        var actionExecutingContext = new ActionExecutingContext(
            controller.ControllerContext,
            new List<IFilterMetadata>(),
            new Dictionary<string, object?>(),
            controller);

        await controller.OnActionExecutionAsync(actionExecutingContext, () =>
        {
            executed = true;
            return Task.FromResult(new ActionExecutedContext(controller.ControllerContext, new List<IFilterMetadata>(), controller));
        });

        Assert.True(executed);
        await _ipLog.DidNotReceive().LogInteractionAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<TimeSpan?>());
    }

    [Fact]
    public void TwitchExtensionController_ActionModel_DoesNotThrowParameterBindingException()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMvcCore().AddApplicationPart(typeof(TwitchExtensionController).Assembly);
        var provider = services.BuildServiceProvider();

        var descriptorProvider = provider.GetRequiredService<Microsoft.AspNetCore.Mvc.Infrastructure.IActionDescriptorCollectionProvider>();
        var descriptors = descriptorProvider.ActionDescriptors;

        var extensionEndpoints = descriptors.Items
            .OfType<Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor>()
            .Where(x => x.ControllerTypeInfo == typeof(TwitchExtensionController))
            .ToList();

        Assert.NotEmpty(extensionEndpoints);
        Assert.DoesNotContain(extensionEndpoints, x => x.ActionName == nameof(TwitchExtensionController.OnActionExecutionAsync));
    }
}

