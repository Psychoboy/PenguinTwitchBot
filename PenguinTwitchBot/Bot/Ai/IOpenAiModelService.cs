namespace PenguinTwitchBot.Bot.Ai
{
    public interface IOpenAiModelService
    {
        IReadOnlyList<string>? CachedModels { get; }
        Task<IReadOnlyList<string>> GetAvailableTextModelsAsync(CancellationToken cancellationToken = default);
    }
}

