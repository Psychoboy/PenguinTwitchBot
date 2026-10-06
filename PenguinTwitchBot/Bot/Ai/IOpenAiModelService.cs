namespace PenguinTwitchBot.Bot.Ai
{
    public interface IOpenAiModelService
    {
        Task<IReadOnlyList<string>> GetAvailableTextModelsAsync(CancellationToken cancellationToken = default);
    }
}

