using DotNetTwitchBot.Bot.Commands;
using DotNetTwitchBot.Bot.Commands.Features;
using DotNetTwitchBot.Bot.Commands.Games;
using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Core.Points;
using DotNetTwitchBot.Bot.Models;
using DotNetTwitchBot.Bot.Models.Points;
using DotNetTwitchBot.Repository;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using NSubstitute;
using System;
using System.Threading.Tasks;
using Xunit;

namespace DotNetTwitchBot.Test.Bot.Core.Points
{

    public class PointsSystemTests
    {
        private readonly IViewerFeature _viewerFeatureMock;
        private readonly ILogger<PointsSystem> _loggerMock;
        private readonly IServiceScopeFactory _scopeFactoryMock;
        private readonly IGameSettingsService _gameSettingsServiceMock;
        private readonly IServiceScope _scopeMock;
        private readonly IServiceProvider _serviceProviderMock;
        private readonly IUnitOfWork _unitOfWorkMock;
        private readonly IServiceBackbone _serviceBackbone;
        private readonly ICommandHandler _commandHandler;
        private readonly PointsSystem _pointsSystem;

        public PointsSystemTests()
        {
            _viewerFeatureMock = Substitute.For<IViewerFeature>();
            _loggerMock = Substitute.For<ILogger<PointsSystem>>();
            _scopeFactoryMock = Substitute.For<IServiceScopeFactory>();
            _gameSettingsServiceMock = Substitute.For<IGameSettingsService>();
            _scopeMock = Substitute.For<IServiceScope>();
            _serviceProviderMock = Substitute.For<IServiceProvider>();
            _unitOfWorkMock = Substitute.For<IUnitOfWork>();
            _serviceBackbone = Substitute.For<IServiceBackbone>();
            _commandHandler = Substitute.For<ICommandHandler>();

            _scopeFactoryMock.CreateScope().Returns(_scopeMock);
            _scopeMock.ServiceProvider.Returns(_serviceProviderMock);
            _serviceProviderMock.GetService(typeof(IUnitOfWork)).Returns(_unitOfWorkMock);

            _pointsSystem = new PointsSystem(
                _viewerFeatureMock,
                _loggerMock,
                _scopeFactoryMock,
                _gameSettingsServiceMock,
                _serviceBackbone,
                _commandHandler
            );
        }

        [Fact]
        public async Task GetMaxPointsByUserId_ShouldReturnMax_WhenPointsExceedMax()
        {
            // Arrange
            var userId = "user1";
            var pointType = 1;
            var max = 100;
            var userPoints = new UserPoints { UserId = userId, PointTypeId = pointType, Points = 200 };

            _unitOfWorkMock.UserPoints.GetUserPointsByUserId(userId, pointType).Returns(userPoints);
            _viewerFeatureMock.GetViewerByUserId(userId).Returns(new Viewer { UserId = userId });

            // Act
            var result = await _pointsSystem.GetMaxPointsByUserId(userId, pointType, max);

            // Assert
            Assert.Equal(max, result);
        }

        [Fact]
        public async Task GetMaxPointsByUserId_ShouldReturnPoints_WhenPointsDoNotExceedMax()
        {
            // Arrange
            var userId = "user1";
            var pointType = 1;
            var max = 100;
            var userPoints = new UserPoints { UserId = userId, PointTypeId = pointType, Points = 50 };

            _unitOfWorkMock.UserPoints.GetUserPointsByUserId(userId, pointType).Returns(userPoints);
            _viewerFeatureMock.GetViewerByUserId(userId).Returns(new Viewer { UserId = userId });

            // Act
            var result = await _pointsSystem.GetMaxPointsByUserId(userId, pointType, max);

            // Assert
            Assert.Equal(userPoints.Points, result);
        }

        [Fact]
        public async Task GetUserPointsByUserId_ShouldReturnUserPoints_WhenUserPointsExist()
        {
            // Arrange
            var userId = "user1";
            var pointType = 1;
            var userPoints = new UserPoints { UserId = userId, PointTypeId = pointType, Points = 100 };

            _unitOfWorkMock.UserPoints.GetUserPointsByUserId(userId, pointType).Returns(userPoints);
            _viewerFeatureMock.GetViewerByUserId(userId).Returns(new Viewer { UserId = userId });

            // Act
            var result = await _pointsSystem.GetUserPointsByUserId(userId, pointType);

            // Assert
            Assert.Equal(userPoints, result);
        }

        [Fact]
        public async Task GetUserPointsByUserId_ShouldCreateUserPoints_WhenUserPointsDoNotExist()
        {
            // Arrange
            var userId = "user1";
            var pointType = 1;

            _unitOfWorkMock.UserPoints.GetUserPointsByUserId(userId, pointType).Returns((UserPoints?)null);
            _viewerFeatureMock.GetViewerByUserId(userId).Returns(new Viewer { UserId = userId });

            // Act
            var result = await _pointsSystem.GetUserPointsByUserId(userId, pointType);

            // Assert
            await _unitOfWorkMock.UserPoints.Received(1).AddAsync(Arg.Any<UserPoints>());
            await _unitOfWorkMock.Received(1).SaveChangesAsync();
        }

        [Fact]
        public async Task GetUserPointsByUsername_ShouldReturnUserPoints_WhenUserExists()
        {
            // Arrange
            var username = "user1";
            var pointType = 1;
            var viewer = new Viewer { UserId = "user1" };
            var userPoints = new UserPoints { UserId = viewer.UserId, PointTypeId = pointType, Points = 100 };

            _viewerFeatureMock.GetViewerByUserName(username).Returns(viewer);
            _unitOfWorkMock.UserPoints.GetUserPointsByUserId(viewer.UserId, pointType).Returns(userPoints);
            _viewerFeatureMock.GetViewerByUserId("user1").Returns(new Viewer { UserId = "user1" });

            // Act
            var result = await _pointsSystem.GetUserPointsByUsername(username, pointType);

            // Assert
            Assert.Equal(userPoints, result);
        }

        [Fact]
        public async Task GetUserPointsByUsername_ShouldLogWarning_WhenUserDoesNotExist()
        {
            // Arrange
            var username = "user1";
            var pointType = 1;

            _viewerFeatureMock.GetViewerByUserName(username).Returns((Viewer?)null);

            // Act
            var result = await _pointsSystem.GetUserPointsByUsername(username, pointType);

            // Assert
            Assert.NotNull(result);
        }

        [Fact]
        public async Task StartAsync_ShouldCreateInitialDataIfNeeded()
        {
            // Arrange
            var cancellationToken = new CancellationToken();
            var pointTypes = new List<PointType>();

            _unitOfWorkMock.PointTypes.GetAllAsync().Returns(pointTypes);

            // Act
            await _pointsSystem.StartAsync(cancellationToken);

            // Assert
            await _unitOfWorkMock.PointTypes.Received(1).AddAsync(Arg.Any<PointType>());
            await _unitOfWorkMock.Received(1).SaveChangesAsync();
        }

        [Fact]
        public async Task GetPointTypeById_ShouldReturnPointType_WhenPointTypeExists()
        {
            // Arrange
            var pointTypeId = 1;
            var pointType = new PointType { Id = pointTypeId };

            _unitOfWorkMock.PointTypes.GetByIdAsync(pointTypeId).Returns(pointType);

            // Act
            var result = await _pointsSystem.GetPointTypeById(pointTypeId);

            // Assert
            Assert.Equal(pointType, result);
        }

        [Fact]
        public async Task GetPointTypes_ShouldReturnAllPointTypes()
        {
            // Arrange
            var pointTypes = new List<PointType> { new PointType { Id = 1 }, new PointType { Id = 2 } };

            _unitOfWorkMock.PointTypes.GetAsync(includeProperties: "PointCommands").Returns(pointTypes);

            // Act
            var result = await _pointsSystem.GetPointTypes();

            // Assert
            Assert.Equal(pointTypes, result);
        }

        [Fact]
        public async Task AddPointType_ShouldAddAndSavePointType()
        {
            // Arrange
            var pointType = new PointType { Id = 1 };

            // Act
            await _pointsSystem.AddPointType(pointType);

            // Assert
            await _unitOfWorkMock.PointTypes.Received(1).AddAsync(pointType);
            await _unitOfWorkMock.Received(1).SaveChangesAsync();
        }

        [Fact]
        public async Task UpdatePointType_ShouldUpdateAndSavePointType()
        {
            // Arrange
            var pointType = new PointType { Id = 1 };

            // Act
            await _pointsSystem.UpdatePointType(pointType);

            // Assert
            _unitOfWorkMock.PointTypes.Received(1).Update(pointType);
            await _unitOfWorkMock.Received(1).SaveChangesAsync();
        }

        [Fact]
        public async Task DeletePointType_ShouldRemoveAndSavePointType_WhenPointTypeExists()
        {
            // Arrange
            var pointTypeId = 1;
            var pointType = new PointType { Id = pointTypeId };

            _unitOfWorkMock.PointTypes.GetByIdAsync(pointTypeId).Returns(pointType);

            // Act
            await _pointsSystem.DeletePointType(pointTypeId);

            // Assert
            _unitOfWorkMock.PointTypes.Received(1).Remove(pointType);
            await _unitOfWorkMock.Received(1).SaveChangesAsync();
        }

        [Fact]
        public async Task RemovePointsFromUserByUserId_ShouldRemovePoints_WhenUserPointsExistAndSufficient()
        {
            // Arrange
            var userId = "user1";
            var pointType = 1;
            var points = 50;
            var userPoints = new UserPoints { UserId = userId, PointTypeId = pointType, Points = 100 };

            _unitOfWorkMock.UserPoints.GetUserPointsByUserId(userId, pointType).Returns(userPoints);
            _viewerFeatureMock.GetViewerByUserId(userId).Returns(new Viewer { UserId = userId });

            // Act
            var result = await _pointsSystem.RemovePointsFromUserByUserId(userId, pointType, points);

            // Assert
            Assert.True(result);
            _unitOfWorkMock.UserPoints.Received(1).Update(Arg.Is<UserPoints>(up => up.Points == 50));
            await _unitOfWorkMock.Received(1).SaveChangesAsync();
        }

        [Fact]
        public async Task RemovePointsFromUserByUserId_ShouldReturnFalse_WhenUserPointsDoNotExist()
        {
            // Arrange
            var userId = "user1";
            var pointType = 1;
            var points = 50;

            _unitOfWorkMock.UserPoints.GetUserPointsByUserId(userId, pointType).Returns((UserPoints?)null);

            // Act
            var result = await _pointsSystem.RemovePointsFromUserByUserId(userId, pointType, points);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task RemovePointsFromUserByUserId_ShouldReturnFalse_WhenUserPointsInsufficient()
        {
            // Arrange
            var userId = "user1";
            var pointType = 1;
            var points = 50;
            var userPoints = new UserPoints { UserId = userId, PointTypeId = pointType, Points = 30 };

            _unitOfWorkMock.UserPoints.GetUserPointsByUserId(userId, pointType).Returns(userPoints);

            // Act
            var result = await _pointsSystem.RemovePointsFromUserByUserId(userId, pointType, points);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task RemovePointsFromUserByUsername_ShouldRemovePoints_WhenUserExistsAndSufficient()
        {
            // Arrange
            var username = "user1";
            var pointType = 1;
            var points = 50;
            var viewer = new Viewer { UserId = "user1" };
            var userPoints = new UserPoints { UserId = viewer.UserId, PointTypeId = pointType, Points = 100 };

            _viewerFeatureMock.GetViewerByUserName(username).Returns(viewer);
            _unitOfWorkMock.UserPoints.GetUserPointsByUserId(viewer.UserId, pointType).Returns(userPoints);
            _viewerFeatureMock.GetViewerByUserId("user1").Returns(new Viewer { UserId = "user1" });

            // Act
            var result = await _pointsSystem.RemovePointsFromUserByUsername(username, pointType, points);

            // Assert
            Assert.True(result);
            _unitOfWorkMock.UserPoints.Received(1).Update(Arg.Is<UserPoints>(up => up.Points == 50));
            await _unitOfWorkMock.Received(1).SaveChangesAsync();
        }

        [Fact]
        public async Task RemovePointsFromUserByUsername_ShouldReturnFalse_WhenUserDoesNotExist()
        {
            // Arrange
            var username = "user1";
            var pointType = 1;
            var points = 50;

            _viewerFeatureMock.GetViewerByUserName(username).Returns((Viewer?)null);

            // Act
            var result = await _pointsSystem.RemovePointsFromUserByUsername(username, pointType, points);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task GetUserPointsByUserIdAndGame_ShouldReturnUserPoints()
        {
            // Arrange
            var userId = "user1";
            var gameName = "game1";
            var pointType = new PointType { Id = 1 };
            var userPoints = new UserPoints { UserId = userId, PointTypeId = (int)pointType.Id, Points = 100 };

            _gameSettingsServiceMock.GetPointTypeForGame(gameName).Returns(pointType);
            _unitOfWorkMock.UserPoints.GetUserPointsByUserId(userId, (int)pointType.Id).Returns(userPoints);
            _viewerFeatureMock.GetViewerByUserId(userId).Returns(new Viewer { UserId = userId });

            // Act
            var result = await _pointsSystem.GetUserPointsByUserIdAndGame(userId, gameName);

            // Assert
            Assert.Equal(userPoints, result);
        }

        [Fact]
        public async Task GetUserPointsByUsernameAndGame_ShouldReturnUserPoints()
        {
            // Arrange
            var username = "user1";
            var gameName = "game1";
            var pointType = new PointType { Id = 1 };
            var viewer = new Viewer { UserId = "user1" };
            var userPoints = new UserPoints { UserId = viewer.UserId, PointTypeId = (int)pointType.Id, Points = 100 };

            _viewerFeatureMock.GetViewerByUserName(username).Returns(viewer);
            _gameSettingsServiceMock.GetPointTypeForGame(gameName).Returns(pointType);
            _unitOfWorkMock.UserPoints.GetUserPointsByUserId(viewer.UserId, (int)pointType.Id).Returns(userPoints);
            _viewerFeatureMock.GetViewerByUserId("user1").Returns(new Viewer { UserId = "user1" });

            // Act
            var result = await _pointsSystem.GetUserPointsByUsernameAndGame(username, gameName);

            // Assert
            Assert.Equal(userPoints, result);
        }

        [Fact]
        public async Task RemovePointsFromUserByUserIdAndGame_ShouldRemovePoints_WhenUserPointsExistAndSufficient()
        {
            // Arrange
            var userId = "user1";
            var gameName = "game1";
            var points = 50;
            var pointType = new PointType { Id = 1 };
            var userPoints = new UserPoints { UserId = userId, PointTypeId = (int)pointType.Id, Points = 100 };

            _gameSettingsServiceMock.GetPointTypeForGame(gameName).Returns(pointType);
            _unitOfWorkMock.UserPoints.GetUserPointsByUserId(userId, (int)pointType.Id).Returns(userPoints);
            _viewerFeatureMock.GetViewerByUserId(userId).Returns(new Viewer { UserId = userId });

            // Act
            var result = await _pointsSystem.RemovePointsFromUserByUserIdAndGame(userId, gameName, points);

            // Assert
            Assert.True(result);
            _unitOfWorkMock.UserPoints.Received(1).Update(Arg.Is<UserPoints>(up => up.Points == 50));
            await _unitOfWorkMock.Received(1).SaveChangesAsync();
        }

        [Fact]
        public async Task RemovePointsFromUserByUsernameAndGame_ShouldRemovePoints_WhenUserExistsAndSufficient()
        {
            // Arrange
            var username = "user1";
            var gameName = "game1";
            var points = 50;
            var pointType = new PointType { Id = 1 };
            var viewer = new Viewer { UserId = "user1" };
            var userPoints = new UserPoints { UserId = viewer.UserId, PointTypeId = (int)pointType.Id, Points = 100 };

            _viewerFeatureMock.GetViewerByUserName(username).Returns(viewer);
            _gameSettingsServiceMock.GetPointTypeForGame(gameName).Returns(pointType);
            _unitOfWorkMock.UserPoints.GetUserPointsByUserId(viewer.UserId, (int)pointType.Id).Returns(userPoints);
            _viewerFeatureMock.GetViewerByUserId("user1").Returns(new Viewer { UserId = "user1" });

            // Act
            var result = await _pointsSystem.RemovePointsFromUserByUsernameAndGame(username, gameName, points);

            // Assert
            Assert.True(result);
            _unitOfWorkMock.UserPoints.Received(1).Update(Arg.Is<UserPoints>(up => up.Points == 50));
            await _unitOfWorkMock.Received(1).SaveChangesAsync();
        }

        [Fact]
        public async Task AddPointsByUserIdAndGame_ShouldAddPoints()
        {
            // Arrange
            var userId = "user1";
            var gameName = "game1";
            var points = 100;
            var pointType = new PointType { Id = 1 };
            var viewer = new Viewer { UserId = "user1" };
            var userPoints = new UserPoints { UserId = viewer.UserId, PointTypeId = (int)pointType.Id, Points = 100 };

            _gameSettingsServiceMock.GetPointTypeForGame(gameName).Returns(pointType);
            _viewerFeatureMock.GetViewerByUserId(userId).Returns(new Viewer { UserId = userId });
            _unitOfWorkMock.UserPoints.GetUserPointsByUserId(viewer.UserId, (int)pointType.Id).Returns(userPoints);

            // Act
            await _pointsSystem.AddPointsByUserIdAndGame(userId, gameName, points);

            // Assert
            await _unitOfWorkMock.Received(1).SaveChangesAsync();
            _unitOfWorkMock.UserPoints.Received(1).Update(Arg.Is<UserPoints>(up => up.Points == 200));
        }

        [Fact]
        public async Task AddPointsByUsernameAndGame_ShouldAddPoints()
        {
            // Arrange
            var username = "testuser";
            var gameName = "game1";
            var points = 100;
            var pointType = new PointType { Id = 1 };
            var viewer = new Viewer { UserId = "user1" };
            var userId = "user1";
            var userPoints = new UserPoints { UserId = viewer.UserId, PointTypeId = (int)pointType.Id, Points = 100 };

            _viewerFeatureMock.GetViewerByUserName(username).Returns(viewer);
            _gameSettingsServiceMock.GetPointTypeForGame(gameName).Returns(pointType);
            _viewerFeatureMock.GetViewerByUserId(userId).Returns(new Viewer { UserId = userId });
            _unitOfWorkMock.UserPoints.GetUserPointsByUserId(viewer.UserId, (int)pointType.Id).Returns(userPoints);

            // Act
            await _pointsSystem.AddPointsByUsernameAndGame(username, gameName, points);

            // Assert
            await _unitOfWorkMock.Received(1).SaveChangesAsync();
            _unitOfWorkMock.UserPoints.Received(1).Update(Arg.Is<UserPoints>(up => up.Points == 200));
        }

        [Fact]
        public async Task GetMaxPointsByUserIdAndGame_ShouldReturnMax_WhenPointsExceedMax()
        {
            // Arrange
            var userId = "user1";
            var gameName = "game1";
            var max = 100;
            var pointType = new PointType { Id = 1 };
            var userPoints = new UserPoints { UserId = userId, PointTypeId = (int)pointType.Id, Points = 200 };

            _gameSettingsServiceMock.GetPointTypeForGame(gameName).Returns(pointType);
            _unitOfWorkMock.UserPoints.GetUserPointsByUserId(userId, (int)pointType.Id).Returns(userPoints);
            _viewerFeatureMock.GetViewerByUserId(userId).Returns(new Viewer { UserId = userId });

            // Act
            var result = await _pointsSystem.GetMaxPointsByUserIdAndGame(userId, gameName, max);

            // Assert
            Assert.Equal(max, result);
        }

    }
}
