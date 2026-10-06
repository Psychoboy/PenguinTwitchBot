namespace PenguinTwitchBot.Bot.Ai
{
    public sealed record OpenAiGenerationResult(
        string Text,
        string? ResponseId,
        bool Success,
        string? ErrorMessage = null);

    public interface IOpenAiResponseService
    {
        bool IsConfigured { get; }
        Task<OpenAiGenerationResult> GenerateResponseAsync(
            string prompt,
            string instructions,
            string model,
            int maxOutputTokenCount,
            string serviceTier,
            bool enableWebSearch,
            IReadOnlyList<string>? allowedDomains,
            string? previousResponseId,
            CancellationToken cancellationToken = default);
    }
}

