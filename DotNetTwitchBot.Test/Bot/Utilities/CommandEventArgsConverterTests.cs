using DotNetTwitchBot.Bot.Actions.Utilities;
using DotNetTwitchBot.Bot.Events.Chat;
using Xunit;

namespace DotNetTwitchBot.Test.Bot.Utilities
{
    public class CommandEventArgsConverterTests
    {
        [Fact]
        public void ToDictionary_WithNullEventArgs_ReturnsEmptyDictionary()
        {
            // Act
            var result = CommandEventArgsConverter.ToDictionary(null!);

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Fact]
        public void ToDictionary_WithBasicCommandEventArgs_ContainsAllProperties()
        {
            // Arrange
            var eventArgs = new CommandEventArgs
            {
                Command = "test",
                Arg = "arg1 arg2",
                Args = new List<string> { "arg1", "arg2", "arg3" },
                DisplayName = "TestUser",
                Name = "testuser",
                UserId = "12345",
                MessageId = "msg-123",
                IsSub = true,
                IsMod = false,
                IsVip = true,
                IsBroadcaster = false,
                TargetUser = "targetuser",
                IsWhisper = false,
                IsDiscord = false,
                DiscordMention = "",
                FromAlias = false,
                SkipLock = false,
                FromOwnChannel = true
            };

            // Act
            var result = CommandEventArgsConverter.ToDictionary(eventArgs);

            // Assert
            Assert.Equal("test", result["Command"]);
            Assert.Equal("arg1 arg2", result["Arg"]);
            Assert.Equal("TestUser", result["DisplayName"]);
            Assert.Equal("testuser", result["Name"]);
            Assert.Equal("12345", result["UserId"]);
            Assert.Equal("msg-123", result["MessageId"]);
            Assert.Equal("True", result["IsSub"]);
            Assert.Equal("False", result["IsMod"]);
            Assert.Equal("True", result["IsVip"]);
            Assert.Equal("False", result["IsBroadcaster"]);
            Assert.Equal("targetuser", result["TargetUser"]);
            Assert.Equal("False", result["IsWhisper"]);
            Assert.Equal("False", result["IsDiscord"]);
            Assert.Equal("True", result["FromOwnChannel"]);
        }

        [Fact]
        public void ToDictionary_WithArgs_CreatesIndexedKeys()
        {
            // Arrange
            var eventArgs = new CommandEventArgs
            {
                Command = "test",
                Args = new List<string> { "first", "second", "third" }
            };

            // Act
            var result = CommandEventArgsConverter.ToDictionary(eventArgs);

            // Assert
            Assert.Equal("first", result["Args_0"]);
            Assert.Equal("second", result["Args_1"]);
            Assert.Equal("third", result["Args_2"]);
            Assert.False(result.ContainsKey("Args_3"));
        }

        [Fact]
        public void ToDictionary_WithEmptyArgs_DoesNotAddArgKeys()
        {
            // Arrange
            var eventArgs = new CommandEventArgs
            {
                Command = "test",
                Args = new List<string>()
            };

            // Act
            var result = CommandEventArgsConverter.ToDictionary(eventArgs);

            // Assert
            Assert.False(result.ContainsKey("Args_0"));
        }

        [Fact]
        public void ToDictionary_WithNullArgs_DoesNotThrow()
        {
            // Arrange
            var eventArgs = new CommandEventArgs
            {
                Command = "test",
                Args = null!
            };

            // Act
            var result = CommandEventArgsConverter.ToDictionary(eventArgs);

            // Assert
            Assert.NotNull(result);
            Assert.False(result.ContainsKey("Args_0"));
        }

        [Fact]
        public void ToDictionary_WithAdditionalValues_MergesBothDictionaries()
        {
            // Arrange
            var eventArgs = new CommandEventArgs
            {
                Command = "test",
                DisplayName = "TestUser"
            };
            var additionalValues = new Dictionary<string, string>
            {
                ["CustomKey1"] = "CustomValue1",
                ["CustomKey2"] = "CustomValue2"
            };

            // Act
            var result = CommandEventArgsConverter.ToDictionary(eventArgs, additionalValues);

            // Assert
            Assert.Equal("test", result["Command"]);
            Assert.Equal("TestUser", result["DisplayName"]);
            Assert.Equal("CustomValue1", result["CustomKey1"]);
            Assert.Equal("CustomValue2", result["CustomKey2"]);
        }

        [Fact]
        public void ToDictionary_WithAdditionalValues_OverwritesExistingKeys()
        {
            // Arrange
            var eventArgs = new CommandEventArgs
            {
                Command = "test",
                DisplayName = "TestUser"
            };
            var additionalValues = new Dictionary<string, string>
            {
                ["Command"] = "overridden"
            };

            // Act
            var result = CommandEventArgsConverter.ToDictionary(eventArgs, additionalValues);

            // Assert
            Assert.Equal("overridden", result["Command"]);
        }

        [Fact]
        public void ToDictionary_IsCaseInsensitive()
        {
            // Arrange
            var eventArgs = new CommandEventArgs
            {
                Command = "test",
                DisplayName = "TestUser"
            };

            // Act
            var result = CommandEventArgsConverter.ToDictionary(eventArgs);

            // Assert - Test case insensitivity
            Assert.Equal("test", result["command"]);
            Assert.Equal("test", result["COMMAND"]);
            Assert.Equal("TestUser", result["displayname"]);
            Assert.Equal("TestUser", result["DISPLAYNAME"]);
        }

        [Fact]
        public void ToDictionary_WithNullStringProperties_ReturnsEmptyStrings()
        {
            // Arrange
            var eventArgs = new CommandEventArgs
            {
                Command = null!,
                DisplayName = null!,
                TargetUser = null!
            };

            // Act
            var result = CommandEventArgsConverter.ToDictionary(eventArgs);

            // Assert
            Assert.Equal(string.Empty, result["Command"]);
            Assert.Equal(string.Empty, result["DisplayName"]);
            Assert.Equal(string.Empty, result["TargetUser"]);
        }

        [Fact]
        public void ToDictionary_WithMultipleArgs_AllArgsAreIndexedCorrectly()
        {
            // Arrange
            var eventArgs = new CommandEventArgs
            {
                Command = "test",
                Args = new List<string> { "a", "b", "c", "d", "e", "f", "g", "h", "i", "j" }
            };

            // Act
            var result = CommandEventArgsConverter.ToDictionary(eventArgs);

            // Assert
            for (int i = 0; i < 10; i++)
            {
                Assert.Equal(((char)('a' + i)).ToString(), result[$"Args_{i}"]);
            }
        }
    }
}
