using DotNetTwitchBot.Bot.Actions.SubActions.Handlers;
using DotNetTwitchBot.Bot.Models.Actions.SubActions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace DotNetTwitchBot.Test.Bot.Actions.SubActions
{
    public class WriteFileHandlerTests
    {
        [Fact]
        public async Task ValidWriteFileType_WritesToFile()
        {
            // Arrange
            var logger = Substitute.For<ILogger<WriteFileHandler>>();
            var handler = new WriteFileHandler(logger);

            var tempFile = Path.GetTempFileName();
            var writeFileType = new WriteFileType
            {
                File = tempFile,
                Text = "Hello %user%!",
                Append = false
            };

            var variables = new Dictionary<string, string> { { "user", "TestUser" } };

            try
            {
                // Act
                await handler.ExecuteAsync(writeFileType, variables);

                // Assert
                var content = await File.ReadAllTextAsync(tempFile);
                Assert.Contains("Hello TestUser!", content);
            }
            finally
            {
                // Cleanup
                if (File.Exists(tempFile))
                    File.Delete(tempFile);
            }
        }

        [Fact]
        public async Task AppendMode_AppendsToFile()
        {
            // Arrange
            var logger = Substitute.For<ILogger<WriteFileHandler>>();
            var handler = new WriteFileHandler(logger);

            var tempFile = Path.GetTempFileName();
            await File.WriteAllTextAsync(tempFile, "First Line" + Environment.NewLine);

            var writeFileType = new WriteFileType
            {
                File = tempFile,
                Text = "Second Line",
                Append = true
            };

            var variables = new Dictionary<string, string>();

            try
            {
                // Act
                await handler.ExecuteAsync(writeFileType, variables);

                // Assert
                var content = await File.ReadAllTextAsync(tempFile);
                Assert.Contains("First Line", content);
                Assert.Contains("Second Line", content);
            }
            finally
            {
                // Cleanup
                if (File.Exists(tempFile))
                    File.Delete(tempFile);
            }
        }

        [Fact]
        public async Task EmptyFilePath_LogsWarningAndDoesNotWrite()
        {
            // Arrange
            var logger = Substitute.For<ILogger<WriteFileHandler>>();
            var handler = new WriteFileHandler(logger);

            var writeFileType = new WriteFileType
            {
                File = "",
                Text = "Test"
            };

            var variables = new Dictionary<string, string>();

            // Act
            await handler.ExecuteAsync(writeFileType, variables);

            // Assert
            logger.Received(1).Log(
                LogLevel.Warning,
                Arg.Any<EventId>(),
                Arg.Is<object>(o => o.ToString()!.Contains("File path is empty")),
                null,
                Arg.Any<Func<object, Exception?, string>>());
        }

        [Fact]
        public async Task WrongType_LogsWarning()
        {
            // Arrange
            var logger = Substitute.For<ILogger<WriteFileHandler>>();
            var handler = new WriteFileHandler(logger);

            var wrongType = new SubActionType();
            var variables = new Dictionary<string, string>();

            // Act
            await handler.ExecuteAsync(wrongType, variables);

            // Assert
            logger.Received(1).Log(
                LogLevel.Warning,
                Arg.Any<EventId>(),
                Arg.Is<object>(o => o.ToString()!.Contains("is not of WriteFileType class")),
                null,
                Arg.Any<Func<object, Exception?, string>>());
        }
    }
}
