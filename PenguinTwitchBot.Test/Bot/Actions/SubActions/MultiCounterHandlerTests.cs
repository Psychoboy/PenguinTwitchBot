using Microsoft.Extensions.DependencyInjection;
using MockQueryable.NSubstitute;
using PenguinTwitchBot.Bot.Actions.SubActions.Handlers;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;
using PenguinTwitchBot.Database.Repository;
using NSubstitute;
using System.Collections.Concurrent;
using System.Linq;

namespace PenguinTwitchBot.Test.Bot.Actions.SubActions
{
    public class MultiCounterHandlerTests
    {
        [Fact]
        public async Task WrongType_ThrowsException()
        {
            var scopeFactory = Substitute.For<IServiceScopeFactory>();
            var handler = new MultiCounterHandler(scopeFactory);

            var wrongType = new SendMessageType();
            var variables = new ConcurrentDictionary<string, string>();

            await Assert.ThrowsAnyAsync<SubActionHandlerException>(() => handler.ExecuteAsync(wrongType, variables));
        }

        [Fact]
        public async Task ValidType_ReturnsExistingCounter()
        {
            var services = new ServiceCollection();
            var unitOfWork = Substitute.For<IUnitOfWork>();
            var repo = Substitute.For<ICountersRepository>();
            unitOfWork.Counters.Returns(repo);
            services.AddSingleton(unitOfWork);
            var provider = services.BuildServiceProvider();
            var realScopeFactory = provider.GetRequiredService<IServiceScopeFactory>();
            var asyncScope = realScopeFactory.CreateAsyncScope();

            var scopeFactory = Substitute.For<IServiceScopeFactory>();
#pragma warning disable NS1000
            scopeFactory.CreateAsyncScope().Returns(asyncScope);
#pragma warning restore NS1000

            var handler = new MultiCounterHandler(scopeFactory);

            var counter = new PenguinTwitchBot.Database.Bot.Models.Counter { CounterName = "test", Amount = 5 };
            var queryable = new List<PenguinTwitchBot.Database.Bot.Models.Counter> { counter }.BuildMockDbSet().AsQueryable();

            repo.Find(Arg.Any<System.Linq.Expressions.Expression<System.Func<PenguinTwitchBot.Database.Bot.Models.Counter, bool>>>()).Returns(queryable);

            var type = new MultiCounterType { Name = "test", Min = 0, Max = 100 };
            var variables = new ConcurrentDictionary<string, string>();

            await handler.ExecuteAsync(type, variables);

            Assert.Equal("5", variables["counter_test"]);
        }

        [Fact]
        public async Task CounterNotFound_CreatesNewCounter()
        {
            var services = new ServiceCollection();
            var unitOfWork = Substitute.For<IUnitOfWork>();
            var repo = Substitute.For<ICountersRepository>();
            unitOfWork.Counters.Returns(repo);
            services.AddSingleton(unitOfWork);
            var provider = services.BuildServiceProvider();
            var realScopeFactory = provider.GetRequiredService<IServiceScopeFactory>();
            var asyncScope = realScopeFactory.CreateAsyncScope();

            var scopeFactory = Substitute.For<IServiceScopeFactory>();
#pragma warning disable NS1000
            scopeFactory.CreateAsyncScope().Returns(asyncScope);
#pragma warning restore NS1000

            var handler = new MultiCounterHandler(scopeFactory);

            var queryable = new List<PenguinTwitchBot.Database.Bot.Models.Counter>().BuildMockDbSet().AsQueryable();
            repo.Find(Arg.Any<System.Linq.Expressions.Expression<System.Func<PenguinTwitchBot.Database.Bot.Models.Counter, bool>>>()).Returns(queryable);

            var type = new MultiCounterType { Name = "newcounter", Min = 0, Max = 100 };
            var variables = new ConcurrentDictionary<string, string>();

            await handler.ExecuteAsync(type, variables);

            Assert.Equal("0", variables["counter_newcounter"]);
        }
    }
}
