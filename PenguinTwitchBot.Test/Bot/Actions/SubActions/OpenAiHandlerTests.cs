using Microsoft.Extensions.Logging;
using MockQueryable.NSubstitute;
using NSubstitute;
using PenguinTwitchBot.Bot.Actions.SubActions.Handlers;
using PenguinTwitchBot.Bot.Ai;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;
using PenguinTwitchBot.Database.Bot.Models;
using PenguinTwitchBot.Database.Repository;
using System.Collections.Concurrent;
using System.Linq.Expressions;

namespace PenguinTwitchBot.Test.Bot.Actions.SubActions
{
    public class OpenAiHandlerTests
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<OpenAiHandler> _logger;
        private readonly IOpenAiResponseService _openAiResponseService;
        private readonly List<OpenAiResponseCode> _dbResponseCodes;

        public OpenAiHandlerTests()
        {
            _unitOfWork = Substitute.For<IUnitOfWork>();
            _logger = Substitute.For<ILogger<OpenAiHandler>>();
            _openAiResponseService = Substitute.For<IOpenAiResponseService>();
            _dbResponseCodes = new List<OpenAiResponseCode>();

            _openAiResponseService.IsConfigured.Returns(true);

            _unitOfWork.OpenAiResponses.Find(Arg.Any<Expression<Func<OpenAiResponseCode, bool>>>())
                .Returns(callInfo =>
                {
                    var predicate = callInfo.Arg<Expression<Func<OpenAiResponseCode, bool>>>().Compile();
                    return _dbResponseCodes.Where(predicate).ToList().BuildMockDbSet().AsQueryable();
                });

            _unitOfWork.OpenAiResponses.When(x => x.AddAsync(Arg.Any<OpenAiResponseCode>()))
                .Do(callInfo =>
                {
                    _dbResponseCodes.Add(callInfo.Arg<OpenAiResponseCode>());
                });
        }

        [Fact]
        public async Task ExecuteAsync_ReplacesVariablesAndStoresResponse()
        {
            var handler = new OpenAiHandler(_unitOfWork, _logger, _openAiResponseService);
            var subAction = new OpenAiType
            {
                Text = "Hello %user%, tell me about %topic%",
                Instructions = "You are a bot for %streamer%",
                Model = "gpt-5.1",
                MaxOutputTokenCount = 150,
                ResponseVariableName = "AiResponse"
            };

            var variables = new ConcurrentDictionary<string, string>
            {
                ["user"] = "Alice",
                ["topic"] = "twitch bots",
                ["streamer"] = "SuperPenguin"
            };

            _openAiResponseService.GenerateResponseAsync(
                prompt: "Hello Alice, tell me about twitch bots",
                instructions: "You are a bot for SuperPenguin",
                model: "gpt-5.1",
                maxOutputTokenCount: 150,
                serviceTier: "default",
                enableWebSearch: false,
                allowedDomains: null,
                previousResponseId: null)
                .Returns(new OpenAiGenerationResult("This is the AI answer.", "resp-123", true));

            await handler.ExecuteAsync(subAction, variables);

            Assert.True(variables.ContainsKey("AiResponse"));
            Assert.Equal("This is the AI answer.", variables["AiResponse"]);
        }

        [Fact]
        public async Task ExecuteAsync_CleansMarkdownLinksAndNewlines_WhenCleanOutputIsTrue()
        {
            var handler = new OpenAiHandler(_unitOfWork, _logger, _openAiResponseService);
            var subAction = new OpenAiType
            {
                Text = "Explain ships",
                CleanOutput = true,
                ResponseVariableName = "AiResponse"
            };

            var variables = new ConcurrentDictionary<string, string>();

            _openAiResponseService.GenerateResponseAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<IReadOnlyList<string>?>(), Arg.Any<string?>())
                .Returns(new OpenAiGenerationResult("Check this out: ([Carrack](https://starcitizen.tools/Carrack))\nLine 2 of text.", "resp-456", true));

            await handler.ExecuteAsync(subAction, variables);

            Assert.Equal("Check this out: Line 2 of text.", variables["AiResponse"]);
        }

        [Fact]
        public async Task ExecuteAsync_PreservesRawText_WhenCleanOutputIsFalse()
        {
            var handler = new OpenAiHandler(_unitOfWork, _logger, _openAiResponseService);
            var subAction = new OpenAiType
            {
                Text = "Explain ships",
                CleanOutput = false,
                ResponseVariableName = "AiResponse"
            };

            var variables = new ConcurrentDictionary<string, string>();
            var rawText = "Check this out: ([Carrack](https://starcitizen.tools/Carrack))\nLine 2 of text.";

            _openAiResponseService.GenerateResponseAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<IReadOnlyList<string>?>(), Arg.Any<string?>())
                .Returns(new OpenAiGenerationResult(rawText, "resp-456", true));

            await handler.ExecuteAsync(subAction, variables);

            Assert.Equal(rawText, variables["AiResponse"]);
        }

        [Fact]
        public async Task ExecuteAsync_UsesCustomResponseVariableName()
        {
            var handler = new OpenAiHandler(_unitOfWork, _logger, _openAiResponseService);
            var subAction = new OpenAiType
            {
                Text = "Hello",
                ResponseVariableName = "CustomAiVar"
            };

            var variables = new ConcurrentDictionary<string, string>();

            _openAiResponseService.GenerateResponseAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<IReadOnlyList<string>?>(), Arg.Any<string?>())
                .Returns(new OpenAiGenerationResult("Custom result", "resp-789", true));

            await handler.ExecuteAsync(subAction, variables);

            Assert.True(variables.ContainsKey("CustomAiVar"));
            Assert.Equal("Custom result", variables["CustomAiVar"]);
        }

        [Fact]
        public async Task ExecuteAsync_PassesAndPersistsPreviousResponseId_WhenSavePreviousResponseIsTrue()
        {
            var handler = new OpenAiHandler(_unitOfWork, _logger, _openAiResponseService);
            var subAction = new OpenAiType
            {
                Id = 10,
                Text = "Follow up question",
                SavePreviousResponse = true,
                SessionKey = "%UserId%",
                ResponseVariableName = "AiResponse"
            };

            var variables = new ConcurrentDictionary<string, string>
            {
                ["UserId"] = "user_42"
            };

            _dbResponseCodes.Add(new OpenAiResponseCode
            {
                SessionKey = "10:user_42",
                PreviousResponseId = "prev-resp-111"
            });

            _openAiResponseService.GenerateResponseAsync(
                prompt: "Follow up question",
                instructions: "",
                model: "gpt-5.1",
                maxOutputTokenCount: 200,
                serviceTier: "default",
                enableWebSearch: false,
                allowedDomains: null,
                previousResponseId: "prev-resp-111")
                .Returns(new OpenAiGenerationResult("Second answer", "new-resp-222", true));

            await handler.ExecuteAsync(subAction, variables);

            Assert.Equal("Second answer", variables["AiResponse"]);
            var saved = _dbResponseCodes.FirstOrDefault(x => x.SessionKey == "10:user_42");
            Assert.NotNull(saved);
            Assert.Equal("new-resp-222", saved.PreviousResponseId);
        }

        [Fact]
        public async Task ExecuteAsync_WhenOpenAiNotConfigured_LogsAndSkipsGracefully()
        {
            var handler = new OpenAiHandler(_unitOfWork, _logger, null);
            var subAction = new OpenAiType
            {
                Text = "Hello world",
                ResponseVariableName = "AiResponse"
            };

            var variables = new ConcurrentDictionary<string, string>();

            await handler.ExecuteAsync(subAction, variables);

            Assert.False(variables.ContainsKey("AiResponse"));
        }

        [Fact]
        public async Task ExecuteAsync_WhenPromptEmpty_SkipsGracefully()
        {
            var handler = new OpenAiHandler(_unitOfWork, _logger, _openAiResponseService);
            var subAction = new OpenAiType
            {
                Text = "   ",
                Instructions = "Some instructions",
                ResponseVariableName = "AiResponse"
            };

            var variables = new ConcurrentDictionary<string, string>();

            await handler.ExecuteAsync(subAction, variables);

            await _openAiResponseService.DidNotReceiveWithAnyArgs()
                .GenerateResponseAsync(default!, default!, default!, default, default!, default, default, default);
            Assert.False(variables.ContainsKey("AiResponse"));
        }

        [Fact]
        public void OpenAiType_Validate_Fails_WhenPromptEmpty()
        {
            var type = new OpenAiType();
            var values = new Dictionary<string, object?>
            {
                [nameof(OpenAiType.Text)] = "   ",
                [nameof(OpenAiType.Instructions)] = "System instructions",
                [nameof(OpenAiType.Model)] = "gpt-5.1"
            };

            var error = type.Validate(values);
            Assert.Equal("Prompt is required", error);
        }

        [Fact]
        public void OpenAiType_Validate_Succeeds_WhenPromptProvided()
        {
            var type = new OpenAiType();
            var values = new Dictionary<string, object?>
            {
                [nameof(OpenAiType.Text)] = "Hello",
                [nameof(OpenAiType.Instructions)] = "System instructions",
                [nameof(OpenAiType.Model)] = "gpt-5.1"
            };

            var error = type.Validate(values);
            Assert.Null(error);
        }

        [Fact]
        public void OpenAiType_GetUIFields_PromptAndInstructionsAreResizable()
        {
            var type = new OpenAiType();
            var fields = type.GetUIFields();

            var promptField = fields.FirstOrDefault(f => f.PropertyName == nameof(OpenAiType.Text));
            var instructionsField = fields.FirstOrDefault(f => f.PropertyName == nameof(OpenAiType.Instructions));

            Assert.NotNull(promptField);
            Assert.True(promptField.Resizable);
            Assert.Equal(4, promptField.Lines);

            Assert.NotNull(instructionsField);
            Assert.True(instructionsField.Resizable);
            Assert.Equal(5, instructionsField.Lines);
        }

        [Fact]
        public async Task ExecuteAsync_SerializesConcurrentRequestsForSameSession()
        {
            var handler = new OpenAiHandler(_unitOfWork, _logger, _openAiResponseService);
            var subAction = new OpenAiType
            {
                Id = 99,
                Text = "Concurrent test",
                SavePreviousResponse = true,
                SessionKey = "%UserId%",
                ResponseVariableName = "AiResponse"
            };

            var variables1 = new ConcurrentDictionary<string, string> { ["UserId"] = "user_sync" };
            var variables2 = new ConcurrentDictionary<string, string> { ["UserId"] = "user_sync" };

            var runningCount = 0;
            var maxConcurrent = 0;

            _openAiResponseService.GenerateResponseAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<IReadOnlyList<string>?>(), Arg.Any<string?>())
                .Returns(async _ =>
                {
                    var current = Interlocked.Increment(ref runningCount);
                    lock (_dbResponseCodes)
                    {
                        if (current > maxConcurrent) maxConcurrent = current;
                    }
                    await Task.Delay(50);
                    Interlocked.Decrement(ref runningCount);
                    return new OpenAiGenerationResult("Response", "resp-id", true);
                });

            var task1 = handler.ExecuteAsync(subAction, variables1);
            var task2 = handler.ExecuteAsync(subAction, variables2);

            await Task.WhenAll(task1, task2);

            Assert.Equal(1, maxConcurrent);
        }
    }
}

