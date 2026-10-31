using System;
using System.Collections.Generic;
using System.Linq;
using PenguinTwitchBot.Extensions;
using Xunit;

namespace PenguinTwitchBot.Test.Extensions
{
    public class ShuffleBagTests
    {
        [Fact]
        public void StandaloneShuffleBag_Empty_ReturnsDefault()
        {
            var bag = new ShuffleBag<string>();
            Assert.Null(bag.Next());
            Assert.Equal(0, bag.TotalCount);
            Assert.Equal(0, bag.RemainingCount);
        }

        [Fact]
        public void StandaloneShuffleBag_SingleItem_AlwaysReturnsThatItem()
        {
            var bag = new ShuffleBag<string>(["only_item"]);
            Assert.Equal(1, bag.TotalCount);

            for (int i = 0; i < 10; i++)
            {
                Assert.Equal("only_item", bag.Next());
            }
        }

        [Fact]
        public void StandaloneShuffleBag_MultipleItems_DrawsEveryItemOncePerCycle()
        {
            var items = new List<string> { "A", "B", "C", "D" };
            var bag = new ShuffleBag<string>(items);

            // In each cycle of 4 draws, all 4 items should appear exactly once
            for (int cycle = 0; cycle < 5; cycle++)
            {
                var cycleDraws = new List<string>();
                for (int i = 0; i < items.Count; i++)
                {
                    var drawn = bag.Next();
                    Assert.NotNull(drawn);
                    cycleDraws.Add(drawn);
                }

                Assert.Equal(items.OrderBy(x => x), cycleDraws.OrderBy(x => x));
            }
        }

        [Fact]
        public void StandaloneShuffleBag_TwoItems_NeverRepeatsConsecutivelyAcrossRefills()
        {
            var items = new List<string> { "X", "Y" };
            var bag = new ShuffleBag<string>(items);

            string? last = null;
            for (int i = 0; i < 50; i++)
            {
                var current = bag.Next();
                Assert.NotNull(current);
                if (last != null)
                {
                    Assert.NotEqual(last, current);
                }
                last = current;
            }
        }

        [Fact]
        public void StandaloneShuffleBag_ThreeItems_NeverRepeatsConsecutively()
        {
            var items = new List<string> { "1", "2", "3" };
            var bag = new ShuffleBag<string>(items);

            string? last = null;
            for (int i = 0; i < 60; i++)
            {
                var current = bag.Next();
                Assert.NotNull(current);
                if (last != null)
                {
                    Assert.NotEqual(last, current);
                }
                last = current;
            }
        }

        [Fact]
        public void KeyedShuffleBag_MaintainsIndependentBagsPerKey()
        {
            var key1 = "test_bag_1_" + Guid.NewGuid();
            var key2 = "test_bag_2_" + Guid.NewGuid();

            var pool1 = new List<string> { "Apple", "Banana" };
            var pool2 = new List<string> { "Cat", "Dog" };

            var result1 = pool1.NextFromShuffleBag(key1);
            var result2 = pool2.NextFromShuffleBag(key2);

            Assert.Contains(result1, pool1);
            Assert.Contains(result2, pool2);
        }

        [Fact]
        public void KeyedShuffleBag_WithKeySelector_ResolvesDistinctObjectInstancesById()
        {
            var bagKey = "object_test_" + Guid.NewGuid();
            var id1 = Guid.NewGuid();
            var id2 = Guid.NewGuid();

            var drawHistory = new List<Guid>();
            for (int i = 0; i < 10; i++)
            {
                // Simulate new object instances created per request with the same IDs
                var candidates = new List<TestEntity>
                {
                    new() { Id = id1, Name = "Item 1 (run " + i + ")" },
                    new() { Id = id2, Name = "Item 2 (run " + i + ")" }
                };

                var picked = candidates.NextFromShuffleBag(bagKey, x => x.Id);
                Assert.NotNull(picked);
                drawHistory.Add(picked.Id);
            }

            // With 2 items, it must alternate and never pick the same ID consecutively
            for (int i = 1; i < drawHistory.Count; i++)
            {
                Assert.NotEqual(drawHistory[i - 1], drawHistory[i]);
            }
        }

        [Fact]
        public void KeyedShuffleBag_DynamicCandidatesChange_AdaptsGracefully()
        {
            var bagKey = "dynamic_test_" + Guid.NewGuid();

            // First run with A, B, C
            var initial = new List<string> { "A", "B", "C" };
            var pick1 = initial.NextFromShuffleBag(bagKey);
            Assert.Contains(pick1, initial);

            // Change candidates: remove A (simulate disabling), add D (simulate adding new)
            var updated = new List<string> { "B", "C", "D" };
            var picks = new List<string>();
            for (int i = 0; i < 15; i++)
            {
                var pick = updated.NextFromShuffleBag(bagKey);
                Assert.NotNull(pick);
                Assert.NotEqual("A", pick); // Disabled item "A" is NEVER picked
                Assert.Contains(pick, updated);
                picks.Add(pick);
            }

            // Both existing items and newly added item D must be picked
            Assert.Contains("D", picks);
            Assert.Contains("B", picks);
            Assert.Contains("C", picks);
        }

        [Fact]
        public void KeyedShuffleBag_Reset_ClearsStoredState()
        {
            var bagKey = "reset_test_" + Guid.NewGuid();
            var items = new List<string> { "A", "B" };

            var first = items.NextFromShuffleBag(bagKey);
            ShuffleBag.Reset(bagKey);

            // After reset, calling NextFromShuffleBag recreates the bag cleanly
            var afterReset = items.NextFromShuffleBag(bagKey);
            Assert.Contains(afterReset, items);
        }

        private sealed class TestEntity
        {
            public Guid Id { get; set; }
            public string Name { get; set; } = "";
        }
    }
}

