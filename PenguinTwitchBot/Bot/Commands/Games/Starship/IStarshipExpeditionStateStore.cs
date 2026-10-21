namespace PenguinTwitchBot.Bot.Commands.Games.Starship;

public interface IStarshipExpeditionStateStore
{
    Task<StarshipExpeditionSnapshot?> LoadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(StarshipExpeditionSnapshot snapshot, CancellationToken cancellationToken = default);
}