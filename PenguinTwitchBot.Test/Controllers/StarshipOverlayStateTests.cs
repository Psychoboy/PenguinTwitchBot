using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PenguinTwitchBot.Bot.Commands.Fishing;
using PenguinTwitchBot.Bot.Commands.Games.Starship;
using PenguinTwitchBot.Bot.Commands.Misc;
using PenguinTwitchBot.Bot.Services;
using PenguinTwitchBot.Bot.Overlay;
using PenguinTwitchBot.Controllers;
using PenguinTwitchBot.Database.Repository;

namespace PenguinTwitchBot.Test.Controllers;

public class StarshipOverlayStateTests
{
    [Fact]
    public async Task GetStarshipState_PassesRequestCancellationToBothServiceReads()
    {
        using var request = new CancellationTokenSource();
        var service = new Mock<IStarshipExpeditionService>();
        service.Setup(instance => instance.GetSnapshotAsync(request.Token)).ReturnsAsync((StarshipExpeditionSnapshot?)null);
        service.Setup(instance => instance.GetAppearanceAsync(request.Token)).ReturnsAsync(new StarshipAppearanceSettings());
        var controller = new OverlayController(Mock.Of<IUnitOfWork>(), Mock.Of<IFishingService>(), Mock.Of<IStreamTimerService>(),
            Mock.Of<ICounterService>(), Mock.Of<IDeathCounterService>(), service.Object, NullLogger<OverlayController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { RequestAborted = request.Token } }
        };

        Assert.IsType<OkObjectResult>(await controller.GetStarshipState());

        service.Verify(instance => instance.GetSnapshotAsync(request.Token), Times.Once);
        service.Verify(instance => instance.GetAppearanceAsync(request.Token), Times.Once);
    }
}