namespace PenguinTwitchBot.Bot.Commands.Games.Golf
{
    public sealed class PlayerGolfRoundState
    {
        public int StrokesUsed { get; set; }
        public double CurrentAxisPos { get; set; } = 0.05;
        public bool IsSunk { get; set; }
        public bool IsInFlight { get; set; }
        public DateTime LastShotUtc { get; set; } = DateTime.UtcNow;
        public bool IsInMotion => IsInFlight && (DateTime.UtcNow - LastShotUtc < TimeSpan.FromSeconds(15));
        public int CurrentScore { get; set; }
        public string LastDropToken { get; set; } = "";

        /// <summary>
        /// Most recent completed stroke that did not end the player's round (not sunk, strokes remaining).
        /// Its win/lose outcome is deferred and fired only if the round ends (session stop) without a further stroke.
        /// </summary>
        public PendingDropRecord? DeferredOutcomeRecord { get; set; }

        /// <summary>Drop tokens whose outcome has already been fired (or discarded); late/duplicate completions for them are ignored.</summary>
        public HashSet<string> FinalizedDropTokens { get; } = new(StringComparer.Ordinal);
    }
}
