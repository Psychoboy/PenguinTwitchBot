using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using PenguinTwitchBot.Bot.Commands.Games.Starship;
using PenguinTwitchBot.Pages.Games;
using PenguinTwitchBot.Shared;

namespace PenguinTwitchBot.Test.Shared;

public class DragReorderStateTests
{
    [Fact]
    public void TryCompleteDrop_MovesItemDownAndResetsState()
    {
        var items = new List<string> { "A", "B", "C", "D" };
        var state = new DragReorderState();

        state.Start(0);
        Assert.Equal("drag-reorder-item drag-reorder-item--dragging", state.GetItemCssClass(0));
        Assert.True(state.Enter(2));
        Assert.Equal("drag-reorder-item drag-reorder-item--drop-after", state.GetItemCssClass(2));

        var moved = state.TryCompleteDrop(items, 2, out var from, out var to);

        Assert.True(moved);
        Assert.Equal(0, from);
        Assert.Equal(2, to);
        Assert.Equal(["B", "C", "A", "D"], items);
        Assert.Null(state.DraggingIndex);
        Assert.Null(state.DragOverIndex);
        Assert.Equal("drag-reorder-item", state.GetItemCssClass(0));
    }

    [Fact]
    public void TryCompleteDrop_MovesItemUpAndShowsDropBeforeIndicator()
    {
        var items = new List<string> { "A", "B", "C", "D" };
        var state = new DragReorderState();

        state.Start(3);
        Assert.True(state.Enter(1));
        Assert.Equal("drag-reorder-item drag-reorder-item--drop-before", state.GetItemCssClass(1));

        var moved = state.TryCompleteDrop(items, 1, out var from, out var to);

        Assert.True(moved);
        Assert.Equal(3, from);
        Assert.Equal(1, to);
        Assert.Equal(["A", "D", "B", "C"], items);
    }

    [Fact]
    public void TryCompleteDrop_RespectsGroupIsolation()
    {
        var trueItems = new List<string> { "T1", "T2" };
        var falseItems = new List<string> { "F1", "F2" };
        var state = new DragReorderState();

        state.Start(0, "true");
        Assert.False(state.Enter(1, "false"));
        Assert.Equal("drag-reorder-item", state.GetItemCssClass(1, "false"));

        var moved = state.TryCompleteDrop(falseItems, 1, out _, out _, "false");

        Assert.False(moved);
        Assert.Equal(["T1", "T2"], trueItems);
        Assert.Equal(["F1", "F2"], falseItems);
        Assert.Null(state.DraggingIndex);
    }

    [Fact]
    public void TryCompleteDrop_ReturnsFalseForSameIndexOrOutOfBounds()
    {
        var items = new List<string> { "A", "B" };
        var state = new DragReorderState();

        state.Start(1);
        Assert.False(state.TryCompleteDrop(items, 1, out _, out _));
        Assert.Equal(["A", "B"], items);

        state.Start(0);
        Assert.False(state.TryCompleteDrop(items, 5, out _, out _));
        Assert.Equal(["A", "B"], items);
    }

    [Fact]
    public void ReassignDescendingZIndex_NormalizesTopToBottomFromCountToOne()
    {
        var widgets = new List<TestWidget>
        {
            new("Top", 1),
            new("Middle", 99),
            new("Bottom", 5)
        };

        DragReorderState.ReassignDescendingZIndex(widgets, (w, z) => w.ZIndex = z);

        Assert.Equal([3, 2, 1], widgets.Select(w => w.ZIndex).ToArray());
    }

    [Fact]
    public async Task StarshipRewardProfileEditor_DragAndDropReordersTiers()
    {
        await using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddMudServices();
        context.Services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());

        var profile = new StarshipRewardProfile
        {
            Name = "DragTest",
            Tiers =
            [
                new() { Id = "t1", Name = "Common", Weight = 3 },
                new() { Id = "t2", Name = "Rare", Weight = 2 },
                new() { Id = "t3", Name = "Legendary", Weight = 1 }
            ]
        };

        var rendered = context.Render<StarshipRewardProfileEditor>(parameters => parameters
            .Add(component => component.Profile, profile));

        Assert.Equal(3, rendered.FindAll("[data-drag-item='true']").Count);

        await rendered.FindAll("[data-drag-item='true']")[2].DragStartAsync(new DragEventArgs());
        await rendered.FindAll("[data-drag-item='true']")[0].DragEnterAsync(new DragEventArgs());
        await rendered.FindAll("[data-drag-item='true']")[0].DropAsync(new DragEventArgs());

        Assert.Equal(["Legendary", "Common", "Rare"], profile.Tiers.Select(t => t.Name).ToArray());
    }

    private sealed class TestWidget(string name, int zIndex)
    {
        public string Name { get; } = name;
        public int ZIndex { get; set; } = zIndex;
    }
}
