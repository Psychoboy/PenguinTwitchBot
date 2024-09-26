using DotNetTwitchBot.Bot.Commands;
using DotNetTwitchBot.Bot.Commands.Metrics;
using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Models.Metrics;
using DotNetTwitchBot.Repository;
using Microsoft.Extensions.DependencyInjection;
using MockQueryable.NSubstitute;
using NSubstitute;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DotNetTwitchBot.Test.Bot.Commands.Metrics
{
    public class SongRequestsTests
    {
        //[Fact]
        //public async Task IncrementSongCount_NewMetric()
        //{
        //    //Arrange
        //    var scopeFactory = Substitute.For<IServiceScopeFactory>();
        //    var dbContext = Substitute.For<IUnitOfWork>();
        //    var serviceProvider = Substitute.For<IServiceProvider>();
        //    var scope = Substitute.For<IServiceScope>();

        //    scopeFactory.CreateScope().Returns(scope);
        //    scope.ServiceProvider.Returns(serviceProvider);

        //    serviceProvider.GetService(typeof(IUnitOfWork)).Returns(dbContext);

        //    var queryable = new List<SongRequestMetric> { }.AsQueryable().BuildMockDbSet();
        //    dbContext.SongRequestMetrics.Find(x => true).ReturnsForAnyArgs(queryable);

        //    var songRequests = new SongRequests(scopeFactory, Substitute.For<IServiceBackbone>(), Substitute.For<ICommandHandler>());
            
        //    //Act
        //    await songRequests.IncrementSongCount(new DotNetTwitchBot.Bot.Models.Song());

        //    //Assert
        //    await dbContext.SongRequestMetrics.Received(1).AddAsync(Arg.Any<SongRequestMetric>());
        //    await dbContext.Received(1).SaveChangesAsync();
        //}

        //[Fact]
        //public async Task IncrementSongCount_ExistingMetric()
        //{
        //    //Arrange
        //    var scopeFactory = Substitute.For<IServiceScopeFactory>();
        //    var dbContext = Substitute.For<IUnitOfWork>();
        //    var serviceProvider = Substitute.For<IServiceProvider>();
        //    var scope = Substitute.For<IServiceScope>();

        //    scopeFactory.CreateScope().Returns(scope);
        //    scope.ServiceProvider.Returns(serviceProvider);

        //    serviceProvider.GetService(typeof(IUnitOfWork)).Returns(dbContext);

        //    var testMetric = new SongRequestMetric { RequestedCount = 1};
        //    var queryable = new List<SongRequestMetric> { testMetric }.AsQueryable().BuildMockDbSet();
        //    dbContext.SongRequestMetrics.Find(x => true).ReturnsForAnyArgs(queryable);

        //    var songRequests = new SongRequests(scopeFactory, Substitute.For<IServiceBackbone>(), Substitute.For<ICommandHandler>());

        //    //Act
        //    await songRequests.IncrementSongCount(new DotNetTwitchBot.Bot.Models.Song());

        //    //Assert
        //    dbContext.SongRequestMetrics.Received(1).Update(Arg.Any<SongRequestMetric>());
        //    await dbContext.Received(1).SaveChangesAsync();
        //    Assert.Equal(2, testMetric.RequestedCount);
        //}

        //[Fact]
        //public async Task DecrementSongCount_ExistingMetric()
        //{
        //    //Arrange
        //    var scopeFactory = Substitute.For<IServiceScopeFactory>();
        //    var dbContext = Substitute.For<IUnitOfWork>();
        //    var serviceProvider = Substitute.For<IServiceProvider>();
        //    var scope = Substitute.For<IServiceScope>();

        //    scopeFactory.CreateScope().Returns(scope);
        //    scope.ServiceProvider.Returns(serviceProvider);

        //    serviceProvider.GetService(typeof(IUnitOfWork)).Returns(dbContext);

        //    var testMetric = new SongRequestMetric { RequestedCount = 1 };
        //    var queryable = new List<SongRequestMetric> { testMetric }.AsQueryable().BuildMockDbSet();
        //    dbContext.SongRequestMetrics.Find(x => true).ReturnsForAnyArgs(queryable);

        //    var songRequests = new SongRequests(scopeFactory, Substitute.For<IServiceBackbone>(), Substitute.For<ICommandHandler>());

        //    //Act
        //    await songRequests.DecrementSongCount(new DotNetTwitchBot.Bot.Models.Song());

        //    //Assert
        //    dbContext.SongRequestMetrics.Received(1).Update(Arg.Any<SongRequestMetric>());
        //    await dbContext.Received(1).SaveChangesAsync();
        //    Assert.Equal(0, testMetric.RequestedCount);
        //}

        //[Fact]
        //public async Task GetRequestedCount_NewMetric()
        //{
        //    //Arrange
        //    var scopeFactory = Substitute.For<IServiceScopeFactory>();
        //    var dbContext = Substitute.For<IUnitOfWork>();
        //    var serviceProvider = Substitute.For<IServiceProvider>();
        //    var scope = Substitute.For<IServiceScope>();

        //    scopeFactory.CreateScope().Returns(scope);
        //    scope.ServiceProvider.Returns(serviceProvider);

        //    serviceProvider.GetService(typeof(IUnitOfWork)).Returns(dbContext);

        //    var queryable = new List<SongRequestMetric> {}.AsQueryable().BuildMockDbSet();
        //    dbContext.SongRequestMetrics.Find(x => true).ReturnsForAnyArgs(queryable);

        //    var songRequests = new SongRequests(scopeFactory, Substitute.For<IServiceBackbone>(), Substitute.For<ICommandHandler>());

        //    //Act
        //    var result = await songRequests.GetRequestedCount(new DotNetTwitchBot.Bot.Models.Song());

        //    //Assert
        //    Assert.Equal(0, result);
        //}

        //[Fact]
        //public async Task GetRequestedCount_ExistingMetric()
        //{
        //    //Arrange
        //    var scopeFactory = Substitute.For<IServiceScopeFactory>();
        //    var dbContext = Substitute.For<IUnitOfWork>();
        //    var serviceProvider = Substitute.For<IServiceProvider>();
        //    var scope = Substitute.For<IServiceScope>();

        //    scopeFactory.CreateScope().Returns(scope);
        //    scope.ServiceProvider.Returns(serviceProvider);

        //    serviceProvider.GetService(typeof(IUnitOfWork)).Returns(dbContext);

        //    var testMetric = new SongRequestMetric { RequestedCount = 1 };
        //    var queryable = new List<SongRequestMetric> { testMetric }.AsQueryable().BuildMockDbSet();
        //    dbContext.SongRequestMetrics.Find(x => true).ReturnsForAnyArgs(queryable);

        //    var songRequests = new SongRequests(scopeFactory, Substitute.For<IServiceBackbone>(), Substitute.For<ICommandHandler>());

        //    //Act
        //    var result = await songRequests.GetRequestedCount(new DotNetTwitchBot.Bot.Models.Song());

        //    //Assert
        //    Assert.Equal(1, result);
        //}
    }
}
