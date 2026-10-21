namespace PenguinTwitchBot.Bot.Commands.Games.Starship;

public interface IStarshipExpeditionSettingsStore
{
    Task<StarshipExpeditionSettings> LoadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(StarshipExpeditionSettings settings, CancellationToken cancellationToken = default);
}
