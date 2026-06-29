using PenguinTwitchBot.Application.Clips;
using PenguinTwitchBot.TwitchApi.Models.Clips;
using PenguinTwitchBot.TwitchApi.Models.Users;
using Xunit;

namespace PenguinTwitchBot.Test.Application.Clips
{
    public class ClipHandlerTests
    {
        [Fact]
        public void Handle_MethodExists()
        {
            Assert.NotNull(typeof(ClipHandler).GetMethod("Handle"));
        }
    }

    public class ClipNotificationTests
    {
        [Fact]
        public void Constructor_SetsProperties()
        {
            var clip = new Clip(
                "clip123", "https://example.com/clip", "https://embed.com", 
                "Title", 100, DateTime.UtcNow, "en", 
                "https://thumb.com", "Broadcaster", "bid", "Creator", "cid", 30.0f);
            var user = new User(
                "user1", "TestUser", "testuser", "https://avatar.com", 
                DateTime.UtcNow, "desc", null, null, null);
            var gameUrl = "https://example.com/game.jpg";
            
            var notification = new ClipNotification(clip, user, gameUrl);
            
            Assert.Equal(clip, notification.Clip);
            Assert.Equal(user, notification.User);
            Assert.Equal(gameUrl, notification.GameUrl);
        }

        [Fact]
        public void ClipProperty_GetterWorks()
        {
            var clip = new Clip("", "", "", "", 0, DateTime.UtcNow, "", "", "", "", "", "", 0.0f);
            var user = new User("", "", "", "", DateTime.UtcNow, null, null, null, null);
            var notification = new ClipNotification(clip, user, "");
            Assert.Equal(clip, notification.Clip);
        }

        [Fact]
        public void UserProperty_GetterWorks()
        {
            var clip = new Clip("", "", "", "", 0, DateTime.UtcNow, "", "", "", "", "", "", 0.0f);
            var user = new User("user", "", "", "", DateTime.UtcNow, null, null, null, null);
            var notification = new ClipNotification(clip, user, "");
            Assert.Equal(user, notification.User);
        }

        [Fact]
        public void GameUrlProperty_GetterWorks()
        {
            var clip = new Clip("", "", "", "", 0, DateTime.UtcNow, "", "", "", "", "", "", 0.0f);
            var user = new User("", "", "", "", DateTime.UtcNow, null, null, null, null);
            var notification = new ClipNotification(clip, user, "gameUrlValue");
            Assert.Equal("gameUrlValue", notification.GameUrl);
        }
    }
}