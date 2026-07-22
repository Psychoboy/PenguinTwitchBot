namespace PenguinTwitchBot.Bot.Commands.Features
{
    public sealed record GiveawayFairnessUserResult(
        string Username,
        int Tickets,
        decimal ExpectedPercent,
        decimal ObservedPercent,
        decimal AbsoluteDeltaPercent);

    public sealed record GiveawayFairnessReport(
        DateTime GeneratedAtUtc,
        int Iterations,
        int TotalTickets,
        string PoolFingerprint,
        decimal MaxAbsoluteDeltaPercent,
        IReadOnlyList<GiveawayFairnessUserResult> Results);
}
