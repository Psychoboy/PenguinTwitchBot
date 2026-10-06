using OpenAI;
using OpenAI.Responses;
using System.Text;

namespace PenguinTwitchBot.Bot.Ai
{
#pragma warning disable OPENAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
    public class OpenAiResponseService : IOpenAiResponseService
    {
        private readonly OpenAIClient? _client;
        private readonly ILogger<OpenAiResponseService> _logger;

        public bool IsConfigured => _client != null;

        public OpenAiResponseService(ILogger<OpenAiResponseService> logger, OpenAIClient? client = null)
        {
            _logger = logger;
            _client = client;
        }

        public async Task<OpenAiGenerationResult> GenerateResponseAsync(
            string prompt,
            string instructions,
            string model,
            int maxOutputTokenCount,
            string serviceTier,
            bool enableWebSearch,
            IReadOnlyList<string>? allowedDomains,
            string? previousResponseId,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(prompt))
            {
                return new OpenAiGenerationResult("", null, false, "Prompt cannot be empty.");
            }

            if (_client == null)
            {
                return new OpenAiGenerationResult("", null, false, "OpenAI client is not configured.");
            }

            var respClient = _client.GetResponsesClient();

            var responseOptions = new CreateResponseOptions
            {
                Model = !string.IsNullOrWhiteSpace(model) ? model : "gpt-5.1",
                MaxOutputTokenCount = maxOutputTokenCount > 0 ? maxOutputTokenCount : 200
            };

            if (!string.IsNullOrWhiteSpace(instructions))
            {
                responseOptions.Instructions = instructions;
            }

            if (!string.IsNullOrWhiteSpace(serviceTier) && !serviceTier.Equals("default", StringComparison.OrdinalIgnoreCase))
            {
                responseOptions.ServiceTier = serviceTier;
            }

            if (!string.IsNullOrWhiteSpace(previousResponseId))
            {
                responseOptions.PreviousResponseId = previousResponseId;
            }

            if (enableWebSearch)
            {
                ResponseTool webSearchTool;
                if (allowedDomains != null && allowedDomains.Count > 0)
                {
                    webSearchTool = ResponseTool.CreateWebSearchTool(null, null, new WebSearchToolFilters
                    {
                        AllowedDomains = allowedDomains.ToList()
                    });
                }
                else
                {
                    webSearchTool = ResponseTool.CreateWebSearchTool();
                }
                responseOptions.Tools.Add(webSearchTool);
            }

            responseOptions.InputItems.Add(ResponseItem.CreateUserMessageItem(prompt));

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(45));

            try
            {
                var response = await respClient.CreateResponseAsync(responseOptions, cts.Token);
                var textBuilder = new StringBuilder();

                foreach (var output in response.Value.OutputItems.OfType<MessageResponseItem>())
                {
                    if (output.Content != null)
                    {
                        foreach (var part in output.Content)
                        {
                            if (!string.IsNullOrEmpty(part.Text))
                            {
                                textBuilder.Append(part.Text);
                            }
                        }
                    }
                }

                var combinedText = textBuilder.ToString();
                if (!string.IsNullOrWhiteSpace(combinedText))
                {
                    return new OpenAiGenerationResult(combinedText, response.Value.Id, true);
                }

                return new OpenAiGenerationResult("", response.Value.Id, false, "No message content in response.");
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning("OpenAI request timed out while generating response.");
                return new OpenAiGenerationResult("", null, false, "OpenAI request timed out.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating response from OpenAI.");
                return new OpenAiGenerationResult("", null, false, ex.Message);
            }
        }
    }
#pragma warning restore OPENAI001
}

