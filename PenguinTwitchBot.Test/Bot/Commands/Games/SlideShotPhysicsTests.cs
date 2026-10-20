using PenguinTwitchBot.Bot.Commands.Games;
using PenguinTwitchBot.Bot.Commands.Games.Common;
using Xunit;

namespace PenguinTwitchBot.Test.Bot.Commands.Games
{
    public class SlideShotPhysicsTests
    {
        [Fact]
        public void CalculateEffectivePower_ZeroVariance_ReturnsClampedRaw()
        {
            Assert.Equal(78.5, SlideShotPhysics.CalculateEffectivePower(78.5, 0.0));
            Assert.Equal(100.0, SlideShotPhysics.CalculateEffectivePower(250.0, 0.0));
            Assert.Equal(75.0, SlideShotPhysics.CalculateEffectivePower(double.NaN, 0.0));
        }

        [Fact]
        public void CalculateEffectivePower_StaysWithinVarianceRange()
        {
            var rng = new Random(7);
            for (var i = 0; i < 100; i++)
            {
                Assert.InRange(SlideShotPhysics.CalculateEffectivePower(60.0, 3.0, rng), 57.0, 63.0);
            }
        }

        [Fact]
        public void TravelDistance_MatchesBothEngineWrappers()
        {
            var v0 = SlideShotPhysics.PowerToInitialSpeed(80.0);
            Assert.Equal(SlideShotPhysics.CalculateTravelDistance(v0, 1.28),
                PenguinTwitchBot.Bot.Commands.Games.Curling.CurlingSimulationEngine.CalculateSlideDistance(v0, 1.28));
            Assert.Equal(SlideShotPhysics.CalculateTravelDistance(v0, 0.78),
                PenguinTwitchBot.Bot.Commands.Games.Golf.GolfSimulationEngine.CalculateShotDistance(v0, 0.78));
        }

        [Theory]
        [InlineData(CurlingSlideDirection.LeftToRight, false)]
        [InlineData(CurlingSlideDirection.TopToBottom, false)]
        [InlineData(CurlingSlideDirection.RightToLeft, true)]
        [InlineData(CurlingSlideDirection.BottomToTop, true)]
        public void ResolveIsReversed_HonorsDirection(CurlingSlideDirection dir, bool expected)
        {
            Assert.Equal(expected, SlideShotPhysics.ResolveIsReversed(dir, "tok", null));
            Assert.Equal(!expected, SlideShotPhysics.ResolveIsReversed(dir, "tok", !expected));
        }

        [Fact]
        public void ResolveNormalizedTarget_MirrorsWhenReversedAndCentersOnCenterTarget()
        {
            Assert.Equal(0.84, SlideShotPhysics.ResolveNormalizedTarget(CurlingSlideDirection.LeftToRight, 84.0, false), 6);
            Assert.Equal(0.16, SlideShotPhysics.ResolveNormalizedTarget(CurlingSlideDirection.RightToLeft, 84.0, true), 6);
            Assert.Equal(0.50, SlideShotPhysics.ResolveNormalizedTarget(CurlingSlideDirection.CenterTarget, 84.0, false), 6);
        }

        [Fact]
        public void CreatePoint_MapsAxisByOrientation()
        {
            var h = SlideShotPhysics.CreatePoint(3, 0.2, 0.5, 0.1, 0.4, -0.2, true);
            Assert.Equal((0.2, 0.5, 0.4, 0.0), (h.X, h.Y, h.Vx, h.Vy));
            var v = SlideShotPhysics.CreatePoint(3, 0.2, 0.5, 0.1, 0.4, -0.2, false);
            Assert.Equal((0.5, 0.2, 0.0, 0.4), (v.X, v.Y, v.Vx, v.Vy));
        }

        [Fact]
        public void UserKey_PrefersIdThenNameThenViewer()
        {
            Assert.Equal("id1", DropGameUserKey.Resolve("id1", "name"));
            Assert.Equal("name", DropGameUserKey.Resolve("", "name"));
            Assert.Equal("viewer", DropGameUserKey.Resolve(null, null));
        }
    }
}
