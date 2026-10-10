using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MockQueryable.NSubstitute;
using NSubstitute;
using PenguinTwitchBot.Bot.Ai;
using PenguinTwitchBot.Bot.Commands.Features;
using PenguinTwitchBot.Bot.Core;
using PenguinTwitchBot.Bot.Core.Points;
using PenguinTwitchBot.Database.Bot.Models;
using PenguinTwitchBot.Database.Bot.Models.Points;
using PenguinTwitchBot.Database.Repository;
using Xunit;

namespace PenguinTwitchBot.Test.Bot.Ai
{
    public class OpenAiViewerContextServiceTests
    {
        private readonly IServiceBackbone _serviceBackbone;
        private readonly IViewerFeature _viewerFeature;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<OpenAiViewerContextService> _logger;
        private readonly IPointsSystem _pointsSystem;
        private readonly List<Viewer> _viewersDb;

        public OpenAiViewerContextServiceTests()
        {
            _serviceBackbone = Substitute.For<IServiceBackbone>();
            _viewerFeature = Substitute.For<IViewerFeature>();
            _unitOfWork = Substitute.For<IUnitOfWork>();
            _logger = Substitute.For<ILogger<OpenAiViewerContextService>>();
            _pointsSystem = Substitute.For<IPointsSystem>();
            _viewersDb = new List<Viewer>();

            _serviceBackbone.BroadcasterName.Returns("StreamerBoss");
            _serviceBackbone.BotName.Returns("PenguinBot");

            _unitOfWork.Viewers.Find(Arg.Any<Expression<Func<Viewer, bool>>>())
                .Returns(callInfo =>
                {
                    var predicate = callInfo.Arg<Expression<Func<Viewer, bool>>>().Compile();
                    return _viewersDb.Where(predicate).ToList().BuildMockDbSet().AsQueryable();
                });
        }

        [Fact]
        public async Task BuildViewerContextAsync_GeneratesCompleteContextXml()
        {
            // Arrange
            var modViewer = new Viewer
            {
                Username = "modsarah",
                DisplayName = "ModSarah",
                isMod = true,
                Title = "Shield Bearer",
                LastSeen = DateTime.UtcNow.AddMinutes(-5)
            };
            var vipViewer = new Viewer
            {
                Username = "vipalex",
                DisplayName = "VipAlex",
                isVip = true,
                LastSeen = DateTime.UtcNow.AddMinutes(-2)
            };
            var regularViewer = new Viewer
            {
                Username = "viewerbob",
                DisplayName = "ViewerBob",
                LastSeen = DateTime.UtcNow.AddMinutes(-1)
            };

            _viewerFeature.GetActiveViewers().Returns(["modsarah", "vipalex", "viewerbob"]);
            _viewerFeature.GetViewerByUserName("modsarah").Returns(modViewer);
            _viewerFeature.GetViewerByUserName("vipalex").Returns(vipViewer);
            _viewerFeature.GetViewerByUserName("viewerbob").Returns(regularViewer);

            var variables = new Dictionary<string, string>
            {
                ["user"] = "vipalex"
            };

            var service = new OpenAiViewerContextService(_serviceBackbone, _viewerFeature, _unitOfWork, _logger, _pointsSystem);

            // Act
            var xml = await service.BuildViewerContextAsync("Hello @modsarah!", "Instructions", variables);

            // Assert
            Assert.Contains("<channel_context>", xml);
            Assert.Contains("<broadcaster username=\"StreamerBoss\" />", xml);
            Assert.Contains("<bot username=\"PenguinBot\" />", xml);
            Assert.Contains("<caller username=\"vipalex\" display_name=\"VipAlex\" roles=\"VIP\"", xml);
            Assert.Contains("<active_chatters total=\"3\">", xml);
            Assert.Contains("<moderators count=\"1\">ModSarah</moderators>", xml);
            Assert.Contains("<vips count=\"1\">VipAlex</vips>", xml);
            Assert.Contains("<viewers count=\"1\">ViewerBob</viewers>", xml);
            Assert.Contains("<mentioned_viewers>", xml);
            Assert.Contains("<viewer username=\"modsarah\" display_name=\"ModSarah\" roles=\"Moderator\" title=\"Shield Bearer\"", xml);
            Assert.Contains("</channel_context>", xml);
        }

        [Fact]
        public async Task BuildMentionedViewersContextAsync_IncludesPointsAndRanks()
        {
            // Arrange
            var viewer = new Viewer
            {
                UserId = "user-123",
                Username = "alice",
                DisplayName = "AliceInWonderland",
                isMod = true,
                isSub = true,
                Title = "Champion",
                LastSeen = DateTime.UtcNow.AddMinutes(-10)
            };

            _viewerFeature.GetViewerByUserName("alice").Returns(viewer);
            _pointsSystem.GetUserPointsByUserId("user-123", 1).Returns(new UserPoints { Points = 5420 });

            _unitOfWork.ViewerMessageCounts.GetUserMessageCountWithRankByUsername("alice")
                .Returns(new ViewerMessageCountWithRank { Username = "alice", Ranking = 2, MessageCount = 1530 });

            _unitOfWork.ViewersTime.GetUserTimeWithRankByUsername("alice")
                .Returns(new ViewerTimeWithRank { Username = "alice", Ranking = 4, Time = 72000 });

            var service = new OpenAiViewerContextService(_serviceBackbone, _viewerFeature, _unitOfWork, _logger, _pointsSystem);

            // Act
            var xml = await service.BuildMentionedViewersContextAsync("Who is @alice?", null);

            // Assert
            Assert.Contains("<mentioned_viewers>", xml);
            Assert.Contains("username=\"alice\"", xml);
            Assert.Contains("display_name=\"AliceInWonderland\"", xml);
            Assert.Contains("roles=\"Moderator, Subscriber\"", xml);
            Assert.Contains("title=\"Champion\"", xml);
            Assert.Contains("points=\"5,420\"", xml);
            Assert.Contains("messages_rank=\"2\"", xml);
            Assert.Contains("messages_count=\"1,530\"", xml);
            Assert.Contains("watch_time_rank=\"4\"", xml);
            Assert.Contains("last_seen=\"10m ago\"", xml);
        }

        [Fact]
        public async Task ProcessViewerTagsAsync_ReplacesAllViewerTags()
        {
            // Arrange
            _viewerFeature.GetActiveViewers().Returns(["user1", "user2"]);
            _viewerFeature.GetViewerByUserName("user1").Returns(new Viewer { Username = "user1", DisplayName = "User1" });
            _viewerFeature.GetViewerByUserName("user2").Returns(new Viewer { Username = "user2", DisplayName = "User2" });

            var service = new OpenAiViewerContextService(_serviceBackbone, _viewerFeature, _unitOfWork, _logger, _pointsSystem);

            var template = "Prompt with %ViewersContext% and active: %ActiveViewers%";
            var variables = new Dictionary<string, string>();

            // Act
            var result = await service.ProcessViewerTagsAsync(template, "Test prompt", "Instructions", variables);

            // Assert
            Assert.DoesNotContain("%ViewersContext%", result);
            Assert.DoesNotContain("%ActiveViewers%", result);
            Assert.Contains("<channel_context>", result);
            Assert.Contains("user1, user2", result);
        }

        [Fact]
        public async Task ProcessViewerTagsAsync_ReplacesMentionedViewersTag()
        {
            // Arrange
            _viewerFeature.GetViewerByUserName("bob").Returns(new Viewer { Username = "bob", DisplayName = "Bob" });

            var service = new OpenAiViewerContextService(_serviceBackbone, _viewerFeature, _unitOfWork, _logger, _pointsSystem);

            var template = "Instructions: %MentionedViewers%";
            var variables = new Dictionary<string, string> { ["Args"] = "@bob" };

            // Act
            var result = await service.ProcessViewerTagsAsync(template, "Check @bob", "Instructions", variables);

            // Assert
            Assert.DoesNotContain("%MentionedViewers%", result);
            Assert.Contains("<mentioned_viewers>", result);
            Assert.Contains("username=\"bob\"", result);
        }

        [Fact]
        public async Task ProcessViewerTagsAsync_LeavesTemplateUnchangedIfNoTagsPresent()
        {
            // Arrange
            var service = new OpenAiViewerContextService(_serviceBackbone, _viewerFeature, _unitOfWork, _logger, _pointsSystem);
            var template = "Regular prompt without viewer tags";
            var variables = new Dictionary<string, string>();

            // Act
            var result = await service.ProcessViewerTagsAsync(template, "Prompt", "Instructions", variables);

            // Assert
            Assert.Equal(template, result);
        }
    }
}

