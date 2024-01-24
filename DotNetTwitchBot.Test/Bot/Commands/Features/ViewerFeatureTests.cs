using DotNetTwitchBot.Bot.Commands;
using DotNetTwitchBot.Bot.Commands.Features;
using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Events;
using DotNetTwitchBot.Bot.Events.Chat;
using DotNetTwitchBot.Bot.Models;
using DotNetTwitchBot.Repository;
using DotNetTwitchBot.Bot.TwitchServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MockQueryable.NSubstitute;
using NSubstitute;
using NSubstitute.ReturnsExtensions;

namespace DotNetTwitchBot.Test.Bot.Commands.Features
{
    public class ViewerFeatureTests
    {
        private readonly ILogger<ViewerFeature> logger;
        private readonly ICommandHandler commandHandler;
        private readonly IServiceScope scope;
        private readonly IServiceBackbone serviceBackbone;
        private readonly ITwitchServiceOld twitchService;
        private readonly IServiceScopeFactory scopeFactory;
        private readonly IUnitOfWork dbContext;
        private readonly IServiceProvider serviceProvider;
        private readonly Viewer testViewer;
        private readonly DbSet<Viewer> viewerQueryable;
        private readonly DbSet<Viewer> emptyViewerQueryable;
        private readonly ViewerFeature viewerFeature;

        public ViewerFeatureTests()
        {
            scopeFactory = Substitute.For<IServiceScopeFactory>();
            dbContext = Substitute.For<IUnitOfWork>();
            serviceProvider = Substitute.For<IServiceProvider>();
            scope = Substitute.For<IServiceScope>();
            serviceBackbone = Substitute.For<IServiceBackbone>();
            twitchService = Substitute.For<ITwitchServiceOld>();
            logger = Substitute.For<ILogger<ViewerFeature>>();
            commandHandler = Substitute.For<ICommandHandler>();

            scopeFactory.CreateScope().Returns(scope);
            scope.ServiceProvider.Returns(serviceProvider);
            serviceProvider.GetService(typeof(IUnitOfWork)).Returns(dbContext);

            testViewer = new Viewer { Id = 1, Username = "test", DisplayName = "Test", Title = "Test Title" };
            viewerQueryable = new List<Viewer> { testViewer }.AsQueryable().BuildMockDbSet();
            emptyViewerQueryable = new List<Viewer> { }.AsQueryable().BuildMockDbSet();

            viewerFeature = new ViewerFeature(logger, serviceBackbone, twitchService, scopeFactory, commandHandler);
        }

        [Fact]
        public async Task GetViewer_ShouldReturnViewer()
        {
            //Arrange
            dbContext.Viewers.Find(x => true).ReturnsForAnyArgs(viewerQueryable);

            //Act
            var result = await viewerFeature.GetViewer(1);

            //Assert
            Assert.Equal(testViewer, result);
        }

        [Fact]
        public async Task SaveViewer_ShouldSaveViewer()
        {
            //Arrange

            //Act
            await viewerFeature.SaveViewer(testViewer);

            //Assert
            dbContext.Viewers.Received(1).Update(testViewer);
            await dbContext.Received(1).SaveChangesAsync();
        }

        [Fact]
        public async Task SearchForViewer_ShouldReturnViewer()
        {
            //Arrange
            dbContext.Viewers.Find(x => true).ReturnsForAnyArgs(viewerQueryable);

            //Act
            var result = await viewerFeature.SearchForViewer("test");

            //Assert
            Assert.Equal(testViewer, result.First());
        }

        [Fact]
        public async Task OnUserJoined_ShouldAddUserAndUpdateLastSeen()
        {
            //Arrange
            dbContext.Viewers.Find(x => true).ReturnsForAnyArgs(viewerQueryable);

            //Act
            serviceBackbone.UserJoinedEvent += Raise.Event<ServiceBackbone.AsyncEventHandler<UserJoinedEventArgs>>(this, new UserJoinedEventArgs { Username = "test" });

            //Assert
            dbContext.Viewers.Received(1).Update(testViewer);
            await dbContext.Received(1).SaveChangesAsync();
            Assert.Equal(DateTime.Now, testViewer.LastSeen, new TimeSpan(0, 1, 0));
            Assert.Contains(testViewer.Username, viewerFeature.GetCurrentViewers());
        }

        [Fact]
        public async Task OnUserLeft_ShouldRemoveUserAndUpdateLastSeen()
        {
            //Arrange
            dbContext.Viewers.Find(x => true).ReturnsForAnyArgs(viewerQueryable);

            //Act
            serviceBackbone.UserLeftEvent += Raise.Event<ServiceBackbone.AsyncEventHandler<UserLeftEventArgs>>(this, new UserLeftEventArgs { Username = "test" });

            //Assert
            dbContext.Viewers.Received(1).Update(testViewer);
            await dbContext.Received(1).SaveChangesAsync();
            Assert.Equal(DateTime.Now, testViewer.LastSeen, new TimeSpan(0, 1, 0));
            Assert.DoesNotContain(testViewer.Username, viewerFeature.GetCurrentViewers());
        }

        [Fact]
        public async Task OnFollow_ShouldAddFollow()
        {
            //Arrange
            dbContext.Viewers.Find(x => true).ReturnsForAnyArgs(emptyViewerQueryable);
            var followerQueryable = new List<Follower>().AsQueryable().BuildMockDbSet();
            dbContext.Followers.Find(x => true).ReturnsForAnyArgs(followerQueryable);

            //Act
            serviceBackbone.FollowEvent += Raise.Event<ServiceBackbone.AsyncEventHandler<FollowEventArgs>>(this, new FollowEventArgs { Username = "test" });

            //Assert
            await dbContext.Followers.Received(1).AddAsync(Arg.Any<Follower>());
            await dbContext.Received(1).SaveChangesAsync();
            Assert.Contains("test", viewerFeature.GetCurrentViewers());
        }

        [Fact]
        public async Task GetDisplayName_ShouldReturn()
        {
            //Arrange
            dbContext.Viewers.Find(x => true).ReturnsForAnyArgs(viewerQueryable);

            //Act
            var result = await viewerFeature.GetDisplayName("test");

            //Assert
            Assert.Equal("Test", result);
        }

        [Fact]
        public async Task GetNameWithTitle_ShouldReturn()
        {
            //Arrange
            dbContext.Viewers.Find(x => true).ReturnsForAnyArgs(viewerQueryable);

            //Act
            var result = await viewerFeature.GetNameWithTitle("test");

            //Assert
            Assert.Equal("[Test Title] Test", result);
        }

        [Fact]
        public async Task IsSubscriber_UserDoesNotExist_ShouldReturnFalse()
        {
            //Arrange
            dbContext.Viewers.Find(x => true).ReturnsForAnyArgs(emptyViewerQueryable);

            //Act
            var result = await viewerFeature.IsSubscriber("test");

            //Assert
            Assert.False(result);
        }

        [Fact]
        public async Task IsSubscriber_UserDoesExist_ShouldReturnTrue()
        {
            //Arrange
            dbContext.Viewers.Find(x => true).ReturnsForAnyArgs(viewerQueryable);
            testViewer.isSub = true;
            //Act
            var result = await viewerFeature.IsSubscriber("test");

            //Assert
            Assert.True(result);
        }

        [Fact]
        public async Task IsSubscriber_UserDoesExist_ShouldReturnFalse()
        {
            //Arrange
            dbContext.Viewers.Find(x => true).ReturnsForAnyArgs(viewerQueryable);
            testViewer.isSub = false;
            //Act
            var result = await viewerFeature.IsSubscriber("test");

            //Assert
            Assert.False(result);
        }

        [Fact]
        public async Task IsModerator_UserDoesNotExist_ShouldReturnFalse()
        {
            //Arrange
            dbContext.Viewers.Find(x => true).ReturnsForAnyArgs(emptyViewerQueryable);

            //Act
            var result = await viewerFeature.IsModerator("test");

            //Assert
            Assert.False(result);
        }

        [Fact]
        public async Task IsModerator_UserDoesExist_ShouldReturnTrue()
        {
            //Arrange
            dbContext.Viewers.Find(x => true).ReturnsForAnyArgs(viewerQueryable);
            testViewer.isMod = true;
            //Act
            var result = await viewerFeature.IsModerator("test");

            //Assert
            Assert.True(result);
        }

        [Fact]
        public async Task IsModerator_UserDoesExist_ShouldReturnFalse()
        {
            //Arrange
            dbContext.Viewers.Find(x => true).ReturnsForAnyArgs(viewerQueryable);
            testViewer.isMod = false;
            //Act
            var result = await viewerFeature.IsModerator("test");

            //Assert
            Assert.False(result);
        }

        [Fact]
        public async Task OnSubscription_ShouldAddSub()
        {
            //Arrange
            dbContext.Viewers.Find(x => true).ReturnsForAnyArgs(viewerQueryable);

            //Act
            serviceBackbone.SubscriptionEvent += Raise.Event<ServiceBackbone.AsyncEventHandler<SubscriptionEventArgs>>(this, new SubscriptionEventArgs { Name = "test" });

            //Assert
            dbContext.Viewers.Received(1).Update(Arg.Any<Viewer>());
            await dbContext.Received(1).SaveChangesAsync();
            Assert.True(testViewer.isSub);
        }

        [Fact]
        public async Task OnSubscriptionEnd_ShouldAddSub()
        {
            //Arrange
            dbContext.Viewers.Find(x => true).ReturnsForAnyArgs(viewerQueryable);
            testViewer.isSub = false;
            //Act
            serviceBackbone.SubscriptionEndEvent += Raise.Event<ServiceBackbone.AsyncEventHandler<SubscriptionEndEventArgs>>(this, new SubscriptionEndEventArgs { Name = "test" });

            //Assert
            dbContext.Viewers.Received(1).Update(Arg.Any<Viewer>());
            await dbContext.Received(1).SaveChangesAsync();
            Assert.False(testViewer.isSub);
        }

        [Fact]
        public void OnCheer_ShouldUpdate()
        {
            //Arrange
            dbContext.Viewers.Find(x => true).ReturnsForAnyArgs(viewerQueryable);
            //Act
            serviceBackbone.CheerEvent += Raise.Event<ServiceBackbone.AsyncEventHandler<CheerEventArgs>>(this, new CheerEventArgs { Name = "test", IsAnonymous = false });

            //Assert
            Assert.Contains(testViewer.Username, viewerFeature.GetCurrentViewers());
        }

        [Fact]
        public async Task OnChatMessage_ShouldUpdateUser()
        {
            //Arrange
            dbContext.Viewers.Find(x => true).ReturnsForAnyArgs(viewerQueryable);

            //Act
            serviceBackbone.ChatMessageEvent += Raise.Event<ServiceBackbone.AsyncEventHandler<ChatMessageEventArgs>>(this, new ChatMessageEventArgs { Name = "test", IsBroadcaster = true, IsMod = true, IsSub = true, DisplayName = "NewDisplayName", IsVip = true });

            //Assert
            dbContext.Viewers.Received(1).Update(Arg.Any<Viewer>());
            await dbContext.Received(1).SaveChangesAsync();
            Assert.True(testViewer.isSub);
            Assert.True(testViewer.isMod);
            Assert.True(testViewer.isVip);
            Assert.True(testViewer.isBroadcaster);
            Assert.Contains(testViewer.Username, viewerFeature.GetActiveViewers());
            Assert.Equal(DateTime.Now, testViewer.LastSeen, new TimeSpan(0, 1, 0));
        }

        [Fact]
        public async Task OnCommand_Lurk_ShouldAddToActive()
        {
            //Arrange           
            commandHandler.GetCommandDefaultName("lurk").Returns("lurk");

            //Act
            await viewerFeature.OnCommand(new(), new CommandEventArgs { Name = "test", Command = "lurk" });

            //Assert
            Assert.Contains(testViewer.Username, viewerFeature.GetActiveViewers());
        }

        [Fact]
        public async Task GetFollowerAsync_ShouldReturnFollowerFromTwitch()
        {
            //Arrange
            var testFollower = new Follower { Username = "testfollower", DisplayName = "TestFollowerDisplay", FollowDate = DateTime.Now };
            var followerQueryable = new List<Follower> { }.AsQueryable().BuildMockDbSet();
            dbContext.Followers.Find(x => true).ReturnsForAnyArgs(followerQueryable);
            twitchService.GetUserFollow("test").Returns(testFollower);

            //Act
            var result = await viewerFeature.GetFollowerAsync("test");

            //Assert
            await dbContext.Followers.Received(1).AddAsync(Arg.Any<Follower>());
            await dbContext.Received(1).SaveChangesAsync();
            Assert.Equal(testFollower, result);
        }

        [Fact]
        public async Task GetFollowerAsync_ShouldReturnFollowerFromCache()
        {
            //Arrange
            var testFollower = new Follower { Username = "testfollower", DisplayName = "TestFollowerDisplay", FollowDate = DateTime.Now };
            var followerQueryable = new List<Follower> { testFollower }.AsQueryable().BuildMockDbSet();
            dbContext.Followers.Find(x => true).ReturnsForAnyArgs(followerQueryable);
            twitchService.GetUserFollow("test").ReturnsNull();

            //Act
            var result = await viewerFeature.GetFollowerAsync("test");

            //Assert
            Assert.Equal(testFollower, result);
        }

        [Fact]
        public async Task GetFollowerAsync_ShouldReturnNull()
        {
            //Arrange
            var testFollower = new Follower { Username = "testfollower", DisplayName = "TestFollowerDisplay", FollowDate = DateTime.Now };
            var followerQueryable = new List<Follower> { }.AsQueryable().BuildMockDbSet();
            dbContext.Followers.Find(x => true).ReturnsForAnyArgs(followerQueryable);
            twitchService.GetUserFollow("test").ReturnsNull();

            //Act
            var result = await viewerFeature.GetFollowerAsync("test");

            //Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task IsFollower_ShouldReturnFalse()
        {
            //Arrange
            var testFollower = new Follower { Username = "testfollower", DisplayName = "TestFollowerDisplay", FollowDate = DateTime.Now };
            var followerQueryable = new List<Follower> { }.AsQueryable().BuildMockDbSet();
            dbContext.Followers.Find(x => true).ReturnsForAnyArgs(followerQueryable);
            twitchService.GetUserFollow("test").ReturnsNull();

            //Act
            var result = await viewerFeature.IsFollower("test");

            //Assert
            Assert.False(result);
        }

        [Fact]
        public async Task IsFollower_ShouldReturnTrue()
        {
            //Arrange
            var testFollower = new Follower { Username = "testfollower", DisplayName = "TestFollowerDisplay", FollowDate = DateTime.Now };
            var followerQueryable = new List<Follower> { testFollower }.AsQueryable().BuildMockDbSet();
            dbContext.Followers.Find(x => true).ReturnsForAnyArgs(followerQueryable);
            twitchService.GetUserFollow("test").ReturnsNull();

            //Act
            var result = await viewerFeature.IsFollower("test");

            //Assert
            Assert.True(result);
        }

    }
}
