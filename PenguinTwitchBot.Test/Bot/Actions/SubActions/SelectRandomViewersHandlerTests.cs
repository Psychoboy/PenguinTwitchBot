using NSubstitute;
using Microsoft.Extensions.DependencyInjection;
using PenguinTwitchBot.Bot.Actions.SubActions;
using PenguinTwitchBot.Bot.Actions.SubActions.Handlers;
using PenguinTwitchBot.Bot.Commands.Features;
using PenguinTwitchBot.Bot.Core;
using PenguinTwitchBot.Database.Bot.Actions.SubActions;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.UI;
using System.Collections.Concurrent;

namespace PenguinTwitchBot.Test.Bot.Actions.SubActions
{
    public class SelectRandomViewersHandlerTests
    {
        [Fact]
        public async Task ExecuteAsync_SelectsUniqueEligibleViewersAndClearsStaleVariables()
        {
            var viewerFeature = Substitute.For<IViewerFeature>();
            var serviceBackbone = Substitute.For<IServiceBackbone>();
            viewerFeature.GetCurrentViewers().Returns(["Alice", "alice", "Bob", "Bot", "Carol"]);
            serviceBackbone.IsKnownBot("Bot").Returns(true);
            var handler = new SelectRandomViewersHandler(viewerFeature, serviceBackbone);
            var variables = new ConcurrentDictionary<string, string>
            {
                ["user"] = "Bob",
                ["selected_viewer_3"] = "stale"
            };

            await handler.ExecuteAsync(new SelectRandomViewersType
            {
                ViewerCount = 3,
                ExcludedViewers = "%user%\nother"
            }, variables);

            Assert.Equal("2", variables["selected_viewer_count"]);
            Assert.Equal(2, variables.Keys.Count(key => key.StartsWith("selected_viewer_") && key != "selected_viewer_count"));
            Assert.Contains(variables["selected_viewer_1"], new[] { "Alice", "alice", "Carol" });
            Assert.Contains(variables["selected_viewer_2"], new[] { "Alice", "alice", "Carol" });
            Assert.False(string.Equals(variables["selected_viewer_1"], variables["selected_viewer_2"], StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain("selected_viewer_3", variables.Keys);
            viewerFeature.Received(1).GetCurrentViewers();
            viewerFeature.DidNotReceive().GetActiveViewers();
        }

        [Fact]
        public async Task ExecuteAsync_WithActiveOnly_SelectsFromActiveViewers()
        {
            var viewerFeature = Substitute.For<IViewerFeature>();
            var serviceBackbone = Substitute.For<IServiceBackbone>();
            viewerFeature.GetActiveViewers().Returns(["Dave", "dave", "Eve", "Bot", "Frank"]);
            viewerFeature.GetCurrentViewers().Returns(["Alice", "Bob", "Carol"]);
            serviceBackbone.IsKnownBot("Bot").Returns(true);
            var handler = new SelectRandomViewersHandler(viewerFeature, serviceBackbone);
            var variables = new ConcurrentDictionary<string, string>
            {
                ["user"] = "Frank"
            };

            await handler.ExecuteAsync(new SelectRandomViewersType
            {
                ViewerCount = 2,
                ActiveOnly = true,
                ExcludedViewers = "%user%"
            }, variables);

            Assert.Equal("2", variables["selected_viewer_count"]);
            Assert.Contains(variables["selected_viewer_1"], new[] { "Dave", "dave", "Eve" });
            Assert.Contains(variables["selected_viewer_2"], new[] { "Dave", "dave", "Eve" });
            Assert.False(string.Equals(variables["selected_viewer_1"], variables["selected_viewer_2"], StringComparison.OrdinalIgnoreCase));
            viewerFeature.Received(1).GetActiveViewers();
            viewerFeature.DidNotReceive().GetCurrentViewers();
        }

        [Fact]
        public async Task ExecuteAsync_WrongTypeThrowsException()
        {
            var handler = new SelectRandomViewersHandler(
                Substitute.For<IViewerFeature>(),
                Substitute.For<IServiceBackbone>());

            await Assert.ThrowsAnyAsync<SubActionHandlerException>(() =>
                handler.ExecuteAsync(new SendMessageType(), new ConcurrentDictionary<string, string>()));
        }

        [Fact]
        public void Configuration_RoundTripsAndIsDiscovered()
        {
            var type = new SelectRandomViewersType();
            Assert.False(type.ActiveOnly);

            var values = type.GetValues();
            values[nameof(SelectRandomViewersType.ViewerCount)] = 4;
            values[nameof(SelectRandomViewersType.ActiveOnly)] = true;
            values[nameof(SelectRandomViewersType.ExcludedViewers)] = "alice, bob";

            type.SetValues(values);

            Assert.Equal(4, type.ViewerCount);
            Assert.True(type.ActiveOnly);
            Assert.Equal("alice, bob", type.ExcludedViewers);
            Assert.Null(type.Validate(values));
            Assert.Contains(type.GetUIFields(), field => field.PropertyName == nameof(SelectRandomViewersType.ViewerCount) && field.FieldType == UIFieldType.Number);
            Assert.Contains(type.GetUIFields(), field => field.PropertyName == nameof(SelectRandomViewersType.ActiveOnly) && field.FieldType == UIFieldType.Switch);
            Assert.Contains(type.GetUIFields(), field => field.PropertyName == nameof(SelectRandomViewersType.ExcludedViewers) && field.FieldType == UIFieldType.TextArea);
            Assert.Equal(typeof(SelectRandomViewersType), SubActionRegistry.GetSubActionType(SubActionTypes.SelectRandomViewers));

            var services = new ServiceCollection();
            services.AddSubActionHandlers();
            Assert.Contains(services, descriptor =>
                descriptor.ServiceType == typeof(ISubActionHandler) &&
                descriptor.ImplementationType == typeof(SelectRandomViewersHandler));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(101)]
        public void Validate_RejectsViewerCountOutsideUiRange(int viewerCount)
        {
            var type = new SelectRandomViewersType();

            var error = type.Validate(new Dictionary<string, object?>
            {
                [nameof(SelectRandomViewersType.ViewerCount)] = viewerCount
            });

            Assert.NotNull(error);
        }
    }
}