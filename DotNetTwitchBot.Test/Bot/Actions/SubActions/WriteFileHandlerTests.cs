using DotNetTwitchBot.Bot.Actions.SubActions.Handlers;
using DotNetTwitchBot.Bot.Actions.SubActions.Types;

namespace DotNetTwitchBot.Test.Bot.Actions.SubActions
{
    public class WriteFileHandlerTests
    {
        [Fact]
        public async Task ValidWriteFileType_WritesToFile()
        {
            // Arrange
            var handler = new WriteFileHandler();

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
            var handler = new WriteFileHandler();

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
        public async Task EmptyFilePath_ThrowsException()
        {
            // Arrange
            var handler = new WriteFileHandler();

            var writeFileType = new WriteFileType
            {
                File = "",
                Text = "Test"
            };

            var variables = new Dictionary<string, string>();

            // Act & Assert
            var exception = await Assert.ThrowsAsync<SubActionHandlerException>(
                () => handler.ExecuteAsync(writeFileType, variables));

            Assert.Contains("File path is empty", exception.Message);
        }

        [Fact]
        public async Task WrongType_ThrowsException()
        {
            // Arrange
            var handler = new WriteFileHandler();

            var wrongType = new SendMessageType();
            var variables = new Dictionary<string, string>();

            // Act & Assert
            var exception = await Assert.ThrowsAsync<SubActionHandlerException>(
                () => handler.ExecuteAsync(wrongType, variables));

            Assert.Contains("is not of WriteFileType class", exception.Message);
        }
    }
}
