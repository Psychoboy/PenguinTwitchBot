namespace PenguinTwitchBot.Database.Bot.Models.Credits
{
    /// <summary>
    /// Canonical event type strings stored in <see cref="CreditEvent.EventType"/>
    /// and section source keys used in <see cref="CreditsSectionConfig.SourceTypes"/>.
    /// </summary>
    public static class CreditEventTypes
    {
        public const string Follow = "Follow";
        public const string Subscribe = "Subscribe";
        public const string Resubscribe = "Resubscribe";
        public const string SubGift = "SubGift";
        public const string GiftedSubRecipient = "GiftedSubRecipient";
        public const string Cheer = "Cheer";
        public const string BitsPowerUp = "BitsPowerUp";
        public const string IncomingRaid = "IncomingRaid";
        public const string OutgoingRaid = "OutgoingRaid";
        public const string ChannelPointRedeem = "ChannelPointRedeem";
        public const string WatchStreak = "WatchStreak";
        public const string CharityDonation = "CharityDonation";
        public const string SubUpgrade = "SubUpgrade";
        public const string PayItForward = "PayItForward";
        public const string BitsBadgeTier = "BitsBadgeTier";
        public const string Chatter = "Chatter";
        public const string SongRequest = "SongRequest";
        public const string DropGameWin = "DropGameWin";
        public const string Manual = "Manual";
    }

    public static class CreditSectionSources
    {
        public const string NewSubscribers = "NewSubscribers";
        public const string Resubscribers = "Resubscribers";
        public const string SubGifters = "SubGifters";
        public const string GiftedSubRecipients = "GiftedSubRecipients";
        public const string SubUpgrades = "SubUpgrades";
        public const string NewFollowers = "NewFollowers";
        public const string CheersAndBits = "CheersAndBits";
        public const string BitsPowerUps = "BitsPowerUps";
        public const string IncomingRaids = "IncomingRaids";
        public const string CharityDonations = "CharityDonations";
        public const string BitsBadgeMilestones = "BitsBadgeMilestones";
        public const string ChannelPointRedemptions = "ChannelPointRedemptions";
        public const string WatchStreaks = "WatchStreaks";
        public const string TopSupporters = "TopSupporters";
        public const string ActiveChatters = "ActiveChatters";
        public const string Moderators = "Moderators";
        public const string Vips = "Vips";
        public const string GiveawayWinners = "GiveawayWinners";
        public const string FishingTopAnglers = "FishingTopAnglers";
        public const string DropGameWinners = "DropGameWinners";
        public const string SongRequesters = "SongRequesters";
        public const string CustomStatic = "CustomStatic";
        public const string ManualActionEntries = "ManualActionEntries";

        public static readonly IReadOnlyList<CreditSectionSourceDescriptor> Catalog =
        [
            new(NewSubscribers, "New Subscribers", "Twitch Subscriptions", "First-time paid or Prime subscribers"),
            new(Resubscribers, "Resubscribers", "Twitch Subscriptions", "Recurring subscription renewals & month milestones"),
            new(SubGifters, "Sub Gifters", "Twitch Subscriptions", "Viewers who gifted subscriptions to the community"),
            new(GiftedSubRecipients, "Gifted Sub Recipients", "Twitch Subscriptions", "Viewers who received a gifted subscription"),
            new(SubUpgrades, "Sub Upgrades & Pay-It-Forward", "Twitch Subscriptions", "Prime/Gift paid upgrades and pay-it-forward subs"),

            new(NewFollowers, "New Followers", "Twitch Support & Growth", "Channel followers during the credits window"),
            new(CheersAndBits, "Cheers / Bits", "Twitch Support & Growth", "Bit cheers during the credits window"),
            new(BitsPowerUps, "Bits Power-Ups & Combos", "Twitch Support & Growth", "Twitch Bits power-ups, message effects, and combos"),
            new(IncomingRaids, "Incoming Raids", "Twitch Support & Growth", "Channels that raided the stream"),
            new(CharityDonations, "Charity Donations", "Twitch Support & Growth", "Twitch Charity campaign donations"),
            new(BitsBadgeMilestones, "Bits Badge Milestones", "Twitch Support & Growth", "Bits badge tier unlock celebrations"),

            new(ChannelPointRedemptions, "Channel Point Redemptions", "Twitch Engagement", "Custom channel point rewards redeemed"),
            new(WatchStreaks, "Watch Streaks", "Twitch Engagement", "Viewer watch streak celebrations"),

            new(TopSupporters, "Top Supporters", "Community & Staff", "Combined highlight leaderboard across subs, gifts, bits, and raids"),
            new(ActiveChatters, "Active Chatters", "Community & Staff", "Viewers who chatted during the credits window"),
            new(Moderators, "Moderators", "Community & Staff", "Channel moderators (active in chat or all channel mods)"),
            new(Vips, "VIPs", "Community & Staff", "Channel VIPs (active in chat or all channel VIPs)"),

            new(GiveawayWinners, "Giveaway Winners", "Bot Features", "Viewers who won giveaways during the window"),
            new(FishingTopAnglers, "Fishing Top Anglers", "Bot Features", "Top anglers from fishing catches during the window"),
            new(DropGameWinners, "Drop Game Winners", "Bot Features", "Winners in Parachute, Plinko, Balloon, Curling, Golf, or Racing"),
            new(SongRequesters, "Song Requesters", "Bot Features", "Viewers who requested songs during the window"),

            new(CustomStatic, "Custom / Static Cast", "Custom & Dynamic", "Static manual rows (co-streamers, artists, mods, music credits)"),
            new(ManualActionEntries, "Action-Added Entries", "Custom & Dynamic", "Entries added dynamically by chat commands or Actions")
        ];
    }

    public sealed record CreditSectionSourceDescriptor(
        string Key,
        string DisplayName,
        string Group,
        string Description);
}
