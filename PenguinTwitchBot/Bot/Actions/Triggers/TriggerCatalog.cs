using MudBlazor;
using PenguinTwitchBot.Bot.Features;
using PenguinTwitchBot.Database.Bot.Actions.Triggers.Configurations;
using PenguinTwitchBot.Database.Bot.Models.Actions.Triggers;

namespace PenguinTwitchBot.Bot.Actions.Triggers
{
    public record TriggerCategoryInfo(
        string Id,
        string Name,
        string Description,
        string Icon,
        Color Color,
        int Order
    );

    public class TriggerItemInfo
    {
        public string Id { get; init; } = string.Empty;
        public TriggerTypes Type { get; init; }
        public string CategoryId { get; init; } = string.Empty;
        public string CategoryName { get; init; } = string.Empty;
        public string DisplayName { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public string Icon { get; init; } = string.Empty;
        public Color Color { get; init; } = Color.Default;
        public string? FeatureKey { get; init; }

        // For DefaultCommand triggers
        public string? DefaultCommandName { get; init; }
        public string? DefaultCommandEventType { get; init; }

        // For TwitchEvent triggers
        public string? TwitchEventName { get; init; }
    }

    public static class TriggerCatalog
    {
        public static readonly List<TriggerCategoryInfo> Categories = new()
        {
            new(
                Id: "twitch",
                Name: "Twitch & Stream",
                Description: "Follows, subs, cheers, raids, channel points, ads, and stream status",
                Icon: Icons.Material.Filled.LiveTv,
                Color: Color.Secondary,
                Order: 1
            ),
            new(
                Id: "minigames",
                Name: "Minigames",
                Description: "Parachute drop, plinko bounce, defuse, wheel spin, and bank heist",
                Icon: Icons.Material.Filled.SportsEsports,
                Color: Color.Success,
                Order: 2
            ),
            new(
                Id: "gambling",
                Name: "Gambling & Points",
                Description: "Gamble, slots, dice rolls, and steal commands",
                Icon: Icons.Material.Filled.Casino,
                Color: Color.Warning,
                Order: 3
            ),
            new(
                Id: "fishing",
                Name: "Fishing",
                Description: "Fish catches, tournaments, gear durability breaks, and accidents",
                Icon: Icons.Material.Filled.Phishing,
                Color: Color.Info,
                Order: 4
            ),
            new(
                Id: "chat-commands",
                Name: "Chat & Commands",
                Description: "Custom chat commands and keyword detection in chat",
                Icon: Icons.Material.Filled.Terminal,
                Color: Color.Primary,
                Order: 5
            ),
            new(
                Id: "counters",
                Name: "Counters & Utilities",
                Description: "Built-in death counter events (custom generic counters trigger via Chat Commands)",
                Icon: Icons.Material.Filled.Numbers,
                Color: Color.Default,
                Order: 6
            ),
            new(
                Id: "timers",
                Name: "Timers & Schedules",
                Description: "Recurring interval and scheduled timer triggers",
                Icon: Icons.Material.Filled.Timer,
                Color: Color.Info,
                Order: 7
            ),
            new(
                Id: "media",
                Name: "Media & Audio",
                Description: "Banned song requests and music events",
                Icon: Icons.Material.Filled.MusicNote,
                Color: Color.Error,
                Order: 8
            ),
            new(
                Id: "general",
                Name: "General & Manual",
                Description: "Manual triggers fired from the UI, stream deck, or API",
                Icon: Icons.Material.Filled.TouchApp,
                Color: Color.Default,
                Order: 9
            )
        };

        public static readonly List<TriggerItemInfo> AllTriggers = new()
        {
            // === Twitch & Stream ===
            new()
            {
                Id = "twitch-channelfollow",
                Type = TriggerTypes.TwitchEvent,
                CategoryId = "twitch",
                CategoryName = "Twitch & Stream",
                DisplayName = "Channel Follow",
                Description = "Triggers when someone follows the channel",
                Icon = Icons.Material.Filled.PersonAdd,
                Color = Color.Secondary,
                TwitchEventName = "ChannelFollow"
            },
            new()
            {
                Id = "twitch-channelcheer",
                Type = TriggerTypes.TwitchEvent,
                CategoryId = "twitch",
                CategoryName = "Twitch & Stream",
                DisplayName = "Channel Cheer",
                Description = "Triggers when someone cheers bits in chat",
                Icon = Icons.Material.Filled.Diamond,
                Color = Color.Secondary,
                TwitchEventName = "ChannelCheer"
            },
            new()
            {
                Id = "twitch-channelsubscribe",
                Type = TriggerTypes.TwitchEvent,
                CategoryId = "twitch",
                CategoryName = "Twitch & Stream",
                DisplayName = "Channel Subscribe",
                Description = "Triggers when someone subscribes for the first time",
                Icon = Icons.Material.Filled.Star,
                Color = Color.Secondary,
                TwitchEventName = "ChannelSubscribe"
            },
            new()
            {
                Id = "twitch-channelsubscriptionmessage",
                Type = TriggerTypes.TwitchEvent,
                CategoryId = "twitch",
                CategoryName = "Twitch & Stream",
                DisplayName = "Subscription Renewal (Resub)",
                Description = "Triggers when someone renews their subscription",
                Icon = Icons.Material.Filled.Stars,
                Color = Color.Secondary,
                TwitchEventName = "ChannelSubscriptionMessage"
            },
            new()
            {
                Id = "twitch-channelsubscriptiongift",
                Type = TriggerTypes.TwitchEvent,
                CategoryId = "twitch",
                CategoryName = "Twitch & Stream",
                DisplayName = "Subscription Gift",
                Description = "Triggers when someone gifts subscriptions",
                Icon = Icons.Material.Filled.CardGiftcard,
                Color = Color.Secondary,
                TwitchEventName = "ChannelSubscriptionGift"
            },
            new()
            {
                Id = "twitch-channelsubscriptionend",
                Type = TriggerTypes.TwitchEvent,
                CategoryId = "twitch",
                CategoryName = "Twitch & Stream",
                DisplayName = "Subscription End",
                Description = "Triggers when a subscription expires",
                Icon = Icons.Material.Filled.StarBorder,
                Color = Color.Secondary,
                TwitchEventName = "ChannelSubscriptionEnd"
            },
            new()
            {
                Id = "twitch-channelpointscustomrewardredemptionadd",
                Type = TriggerTypes.TwitchEvent,
                CategoryId = "twitch",
                CategoryName = "Twitch & Stream",
                DisplayName = "Channel Point Redemption",
                Description = "Triggers when someone redeems a channel point reward",
                Icon = Icons.Material.Filled.Loyalty,
                Color = Color.Secondary,
                TwitchEventName = "ChannelPointsCustomRewardRedemptionAdd"
            },
            new()
            {
                Id = "twitch-channelraid",
                Type = TriggerTypes.TwitchEvent,
                CategoryId = "twitch",
                CategoryName = "Twitch & Stream",
                DisplayName = "Channel Raid",
                Description = "Triggers when the channel is raided by another streamer",
                Icon = Icons.Material.Filled.GroupAdd,
                Color = Color.Secondary,
                TwitchEventName = "ChannelRaid"
            },
            new()
            {
                Id = "twitch-streamonline",
                Type = TriggerTypes.TwitchEvent,
                CategoryId = "twitch",
                CategoryName = "Twitch & Stream",
                DisplayName = "Stream Online",
                Description = "Triggers when the stream goes live",
                Icon = Icons.Material.Filled.PlayCircle,
                Color = Color.Secondary,
                TwitchEventName = "StreamOnline"
            },
            new()
            {
                Id = "twitch-streamoffline",
                Type = TriggerTypes.TwitchEvent,
                CategoryId = "twitch",
                CategoryName = "Twitch & Stream",
                DisplayName = "Stream Offline",
                Description = "Triggers when the stream ends",
                Icon = Icons.Material.Filled.StopCircle,
                Color = Color.Secondary,
                TwitchEventName = "StreamOffline"
            },
            new()
            {
                Id = "twitch-channelban",
                Type = TriggerTypes.TwitchEvent,
                CategoryId = "twitch",
                CategoryName = "Twitch & Stream",
                DisplayName = "Channel Ban",
                Description = "Triggers when someone is banned from chat",
                Icon = Icons.Material.Filled.Block,
                Color = Color.Secondary,
                TwitchEventName = "ChannelBan"
            },
            new()
            {
                Id = "twitch-channelunban",
                Type = TriggerTypes.TwitchEvent,
                CategoryId = "twitch",
                CategoryName = "Twitch & Stream",
                DisplayName = "Channel Unban",
                Description = "Triggers when someone is unbanned from chat",
                Icon = Icons.Material.Filled.CheckCircle,
                Color = Color.Secondary,
                TwitchEventName = "ChannelUnban"
            },
            new()
            {
                Id = "twitch-channeladbreakbegin",
                Type = TriggerTypes.TwitchEvent,
                CategoryId = "twitch",
                CategoryName = "Twitch & Stream",
                DisplayName = "Ad Break Begin",
                Description = "Triggers when a commercial ad break starts",
                Icon = Icons.Material.Filled.Campaign,
                Color = Color.Secondary,
                TwitchEventName = "ChannelAdBreakBegin"
            },
            new()
            {
                Id = "twitch-channelbitsuse",
                Type = TriggerTypes.TwitchEvent,
                CategoryId = "twitch",
                CategoryName = "Twitch & Stream",
                DisplayName = "Bits Use (Power-ups)",
                Description = "Triggers when someone uses bits, celebrations, or power-ups",
                Icon = Icons.Material.Filled.Bolt,
                Color = Color.Secondary,
                TwitchEventName = "ChannelBitsUse"
            },
            new()
            {
                Id = "twitch-channelchatnotification",
                Type = TriggerTypes.TwitchEvent,
                CategoryId = "twitch",
                CategoryName = "Twitch & Stream",
                DisplayName = "Chat Notification",
                Description = "Triggers on chat system notices (sub, resub, raid, streak, announcements)",
                Icon = Icons.Material.Filled.Notifications,
                Color = Color.Secondary,
                TwitchEventName = "ChannelChatNotification"
            },

            // === Minigames ===
            new()
            {
                Id = "parachute-bullseye",
                Type = TriggerTypes.DefaultCommand,
                CategoryId = "minigames",
                CategoryName = "Minigames",
                DisplayName = "Parachute Drop - Bullseye",
                Description = "Triggers when a parachute droplet hits the exact center bullseye",
                Icon = Icons.Material.Filled.Adjust,
                Color = Color.Success,
                FeatureKey = FeatureKeys.DropGames,
                DefaultCommandName = "drop",
                DefaultCommandEventType = DefaultCommandEventTypes.ParachuteBullseye
            },
            new()
            {
                Id = "parachute-win",
                Type = TriggerTypes.DefaultCommand,
                CategoryId = "minigames",
                CategoryName = "Minigames",
                DisplayName = "Parachute Drop - Win",
                Description = "Triggers when a parachute droplet lands on a winning zone",
                Icon = Icons.Material.Filled.EmojiEvents,
                Color = Color.Success,
                FeatureKey = FeatureKeys.DropGames,
                DefaultCommandName = "drop",
                DefaultCommandEventType = DefaultCommandEventTypes.ParachuteWin
            },
            new()
            {
                Id = "parachute-lose",
                Type = TriggerTypes.DefaultCommand,
                CategoryId = "minigames",
                CategoryName = "Minigames",
                DisplayName = "Parachute Drop - Missed (Lose)",
                Description = "Triggers when a parachute droplet misses the landing zone",
                Icon = Icons.Material.Filled.Cancel,
                Color = Color.Success,
                FeatureKey = FeatureKeys.DropGames,
                DefaultCommandName = "drop",
                DefaultCommandEventType = DefaultCommandEventTypes.ParachuteLose
            },
            new()
            {
                Id = "parachute-start",
                Type = TriggerTypes.DefaultCommand,
                CategoryId = "minigames",
                CategoryName = "Minigames",
                DisplayName = "Parachute Drop - Round Started",
                Description = "Triggers when a timed parachute session/round actually starts",
                Icon = Icons.Material.Filled.PlayCircle,
                Color = Color.Success,
                FeatureKey = FeatureKeys.DropGames,
                DefaultCommandName = "drop",
                DefaultCommandEventType = DefaultCommandEventTypes.ParachuteStart
            },
            new()
            {
                Id = "parachute-end",
                Type = TriggerTypes.DefaultCommand,
                CategoryId = "minigames",
                CategoryName = "Minigames",
                DisplayName = "Parachute Drop - Round Ended",
                Description = "Triggers when a timed parachute session/round concludes",
                Icon = Icons.Material.Filled.FlagCircle,
                Color = Color.Success,
                FeatureKey = FeatureKeys.DropGames,
                DefaultCommandName = "drop",
                DefaultCommandEventType = DefaultCommandEventTypes.ParachuteEnd
            },
            new()
            {
                Id = "parachute-rocket-kill",
                Type = TriggerTypes.DefaultCommand,
                CategoryId = "minigames",
                CategoryName = "Minigames",
                DisplayName = "Parachute Drop - Rocket Kill",
                Description = "Triggers when a rocket destroys a player's parachute, sending them into a free-fall crash",
                Icon = Icons.Material.Filled.RocketLaunch,
                Color = Color.Error,
                FeatureKey = FeatureKeys.DropGames,
                DefaultCommandName = "drop",
                DefaultCommandEventType = DefaultCommandEventTypes.ParachuteRocketKill
            },
            new()
            {
                Id = "plinko-win",
                Type = TriggerTypes.DefaultCommand,
                CategoryId = "minigames",
                CategoryName = "Minigames",
                DisplayName = "Plinko - Win",
                Description = "Triggers when a plinko puck lands in a winning multiplier bucket",
                Icon = Icons.Material.Filled.Casino,
                Color = Color.Success,
                FeatureKey = FeatureKeys.DropGames,
                DefaultCommandName = "plinko",
                DefaultCommandEventType = DefaultCommandEventTypes.PlinkoWin
            },
            new()
            {
                Id = "plinko-lose",
                Type = TriggerTypes.DefaultCommand,
                CategoryId = "minigames",
                CategoryName = "Minigames",
                DisplayName = "Plinko - Lose",
                Description = "Triggers when a plinko puck lands in a losing bucket",
                Icon = Icons.Material.Filled.Cancel,
                Color = Color.Success,
                FeatureKey = FeatureKeys.DropGames,
                DefaultCommandName = "plinko",
                DefaultCommandEventType = DefaultCommandEventTypes.PlinkoLose
            },
            new()
            {
                Id = "plinko-start",
                Type = TriggerTypes.DefaultCommand,
                CategoryId = "minigames",
                CategoryName = "Minigames",
                DisplayName = "Plinko - Round Started",
                Description = "Triggers when a timed plinko session/round actually starts",
                Icon = Icons.Material.Filled.PlayCircle,
                Color = Color.Success,
                FeatureKey = FeatureKeys.DropGames,
                DefaultCommandName = "plinko",
                DefaultCommandEventType = DefaultCommandEventTypes.PlinkoStart
            },
            new()
            {
                Id = "plinko-end",
                Type = TriggerTypes.DefaultCommand,
                CategoryId = "minigames",
                CategoryName = "Minigames",
                DisplayName = "Plinko - Round Ended",
                Description = "Triggers when a timed plinko session/round concludes",
                Icon = Icons.Material.Filled.FlagCircle,
                Color = Color.Success,
                FeatureKey = FeatureKeys.DropGames,
                DefaultCommandName = "plinko",
                DefaultCommandEventType = DefaultCommandEventTypes.PlinkoEnd
            },
            new()
            {
                Id = "balloon-bullseye",
                Type = TriggerTypes.DefaultCommand,
                CategoryId = "minigames",
                CategoryName = "Minigames",
                DisplayName = "Balloon Ascent - Bullseye",
                Description = "Triggers when a balloon reaches the center bullseye of the sky arch",
                Icon = Icons.Material.Filled.GpsFixed,
                Color = Color.Success,
                FeatureKey = FeatureKeys.DropGames,
                DefaultCommandName = "balloon",
                DefaultCommandEventType = DefaultCommandEventTypes.BalloonBullseye
            },
            new()
            {
                Id = "balloon-win",
                Type = TriggerTypes.DefaultCommand,
                CategoryId = "minigames",
                CategoryName = "Minigames",
                DisplayName = "Balloon Ascent - Win",
                Description = "Triggers when a balloon reaches the sky arch target zone",
                Icon = Icons.Material.Filled.Celebration,
                Color = Color.Success,
                FeatureKey = FeatureKeys.DropGames,
                DefaultCommandName = "balloon",
                DefaultCommandEventType = DefaultCommandEventTypes.BalloonWin
            },
            new()
            {
                Id = "balloon-lose",
                Type = TriggerTypes.DefaultCommand,
                CategoryId = "minigames",
                CategoryName = "Minigames",
                DisplayName = "Balloon Ascent - Miss",
                Description = "Triggers when a balloon misses the sky arch target zone",
                Icon = Icons.Material.Filled.Cancel,
                Color = Color.Success,
                FeatureKey = FeatureKeys.DropGames,
                DefaultCommandName = "balloon",
                DefaultCommandEventType = DefaultCommandEventTypes.BalloonLose
            },
            new()
            {
                Id = "balloon-pop",
                Type = TriggerTypes.DefaultCommand,
                CategoryId = "minigames",
                CategoryName = "Minigames",
                DisplayName = "Balloon Ascent - Popped",
                Description = "Triggers when a balloon is popped mid-air by falling hazards",
                Icon = Icons.Material.Filled.Dangerous,
                Color = Color.Success,
                FeatureKey = FeatureKeys.DropGames,
                DefaultCommandName = "balloon",
                DefaultCommandEventType = DefaultCommandEventTypes.BalloonPop
            },
            new()
            {
                Id = "balloon-start",
                Type = TriggerTypes.DefaultCommand,
                CategoryId = "minigames",
                CategoryName = "Minigames",
                DisplayName = "Balloon Ascent - Round Started",
                Description = "Triggers when a timed balloon ascent session starts",
                Icon = Icons.Material.Filled.PlayCircle,
                Color = Color.Success,
                FeatureKey = FeatureKeys.DropGames,
                DefaultCommandName = "balloon",
                DefaultCommandEventType = DefaultCommandEventTypes.BalloonStart
            },
            new()
            {
                Id = "balloon-end",
                Type = TriggerTypes.DefaultCommand,
                CategoryId = "minigames",
                CategoryName = "Minigames",
                DisplayName = "Balloon Ascent - Round Ended",
                Description = "Triggers when a timed balloon ascent session concludes",
                Icon = Icons.Material.Filled.FlagCircle,
                Color = Color.Success,
                FeatureKey = FeatureKeys.DropGames,
                DefaultCommandName = "balloon",
                DefaultCommandEventType = DefaultCommandEventTypes.BalloonEnd
            },
            new()
            {
                Id = "curling-bullseye",
                Type = TriggerTypes.DefaultCommand,
                CategoryId = "minigames",
                CategoryName = "Minigames",
                DisplayName = "Curling - Bullseye (Button)",
                Description = "Triggers when a curling stone stops dead-center on the button (100 pts)",
                Icon = Icons.Material.Filled.GpsFixed,
                Color = Color.Success,
                FeatureKey = FeatureKeys.DropGames,
                DefaultCommandName = "curl",
                DefaultCommandEventType = DefaultCommandEventTypes.CurlingBullseye
            },
            new()
            {
                Id = "curling-win",
                Type = TriggerTypes.DefaultCommand,
                CategoryId = "minigames",
                CategoryName = "Minigames",
                DisplayName = "Curling - Win (In House)",
                Description = "Triggers when a curling stone stops inside a scoring ring",
                Icon = Icons.Material.Filled.SportsHockey,
                Color = Color.Success,
                FeatureKey = FeatureKeys.DropGames,
                DefaultCommandName = "curl",
                DefaultCommandEventType = DefaultCommandEventTypes.CurlingWin
            },
            new()
            {
                Id = "curling-lose",
                Type = TriggerTypes.DefaultCommand,
                CategoryId = "minigames",
                CategoryName = "Minigames",
                DisplayName = "Curling - Miss / Gutter",
                Description = "Triggers when a curling stone misses the house or slides into the gutter",
                Icon = Icons.Material.Filled.Cancel,
                Color = Color.Success,
                FeatureKey = FeatureKeys.DropGames,
                DefaultCommandName = "curl",
                DefaultCommandEventType = DefaultCommandEventTypes.CurlingLose
            },
            new()
            {
                Id = "curling-start",
                Type = TriggerTypes.DefaultCommand,
                CategoryId = "minigames",
                CategoryName = "Minigames",
                DisplayName = "Curling - Round Started",
                Description = "Triggers when a timed curling session starts",
                Icon = Icons.Material.Filled.PlayCircle,
                Color = Color.Success,
                FeatureKey = FeatureKeys.DropGames,
                DefaultCommandName = "curl",
                DefaultCommandEventType = DefaultCommandEventTypes.CurlingStart
            },
            new()
            {
                Id = "curling-end",
                Type = TriggerTypes.DefaultCommand,
                CategoryId = "minigames",
                CategoryName = "Minigames",
                DisplayName = "Curling - Round Ended",
                Description = "Triggers when a timed curling session concludes",
                Icon = Icons.Material.Filled.FlagCircle,
                Color = Color.Success,
                FeatureKey = FeatureKeys.DropGames,
                DefaultCommandName = "curl",
                DefaultCommandEventType = DefaultCommandEventTypes.CurlingEnd
            },
            new()
            {
                Id = "golf-holeinone",
                Type = TriggerTypes.DefaultCommand,
                CategoryId = "minigames",
                CategoryName = "Minigames",
                DisplayName = "Golf - Sunk in Cup / Hole-in-One",
                Description = "Triggers when a golf shot is sunk directly into the cup (100 base score)",
                Icon = Icons.Material.Filled.GolfCourse,
                Color = Color.Success,
                FeatureKey = FeatureKeys.DropGames,
                DefaultCommandName = "golf",
                DefaultCommandEventType = DefaultCommandEventTypes.GolfBullseye
            },
            new()
            {
                Id = "golf-win",
                Type = TriggerTypes.DefaultCommand,
                CategoryId = "minigames",
                CategoryName = "Minigames",
                DisplayName = "Golf - On the Green (Win)",
                Description = "Triggers when a golf ball rests on the putting green or is sunk in the cup",
                Icon = Icons.Material.Filled.Flag,
                Color = Color.Success,
                FeatureKey = FeatureKeys.DropGames,
                DefaultCommandName = "golf",
                DefaultCommandEventType = DefaultCommandEventTypes.GolfWin
            },
            new()
            {
                Id = "golf-lose",
                Type = TriggerTypes.DefaultCommand,
                CategoryId = "minigames",
                CategoryName = "Minigames",
                DisplayName = "Golf - Off the Green (Miss)",
                Description = "Triggers when a golf ball misses the putting green (short or overshot)",
                Icon = Icons.Material.Filled.Cancel,
                Color = Color.Success,
                FeatureKey = FeatureKeys.DropGames,
                DefaultCommandName = "golf",
                DefaultCommandEventType = DefaultCommandEventTypes.GolfLose
            },
            new()
            {
                Id = "golf-start",
                Type = TriggerTypes.DefaultCommand,
                CategoryId = "minigames",
                CategoryName = "Minigames",
                DisplayName = "Golf - Round Started",
                Description = "Triggers when a timed golf session starts",
                Icon = Icons.Material.Filled.PlayCircle,
                Color = Color.Success,
                FeatureKey = FeatureKeys.DropGames,
                DefaultCommandName = "golf",
                DefaultCommandEventType = DefaultCommandEventTypes.GolfStart
            },
            new()
            {
                Id = "golf-end",
                Type = TriggerTypes.DefaultCommand,
                CategoryId = "minigames",
                CategoryName = "Minigames",
                DisplayName = "Golf - Round Ended",
                Description = "Triggers when a timed golf session concludes",
                Icon = Icons.Material.Filled.FlagCircle,
                Color = Color.Success,
                FeatureKey = FeatureKeys.DropGames,
                DefaultCommandName = "golf",
                DefaultCommandEventType = DefaultCommandEventTypes.GolfEnd
            },
            new()
            {
                Id = "starship-boarding-opened",
                Type = TriggerTypes.DefaultCommand,
                CategoryId = "minigames",
                CategoryName = "Minigames",
                DisplayName = "Starship Expedition - Boarding Opened",
                Description = "Triggers when the crew boarding window opens",
                Icon = Icons.Material.Filled.GroupAdd,
                Color = Color.Success,
                FeatureKey = FeatureKeys.DropGames,
                DefaultCommandName = "starship",
                DefaultCommandEventType = DefaultCommandEventTypes.StarshipBoardingOpened
            },
            new()
            {
                Id = "starship-crew-joined",
                Type = TriggerTypes.DefaultCommand,
                CategoryId = "minigames",
                CategoryName = "Minigames",
                DisplayName = "Starship Expedition - Crew Joined",
                Description = "Triggers when a viewer joins the crew before launch",
                Icon = Icons.Material.Filled.PersonAdd,
                Color = Color.Success,
                FeatureKey = FeatureKeys.DropGames,
                DefaultCommandName = "starship",
                DefaultCommandEventType = DefaultCommandEventTypes.StarshipCrewJoined
            },
            new()
            {
                Id = "starship-event-opened",
                Type = TriggerTypes.DefaultCommand,
                CategoryId = "minigames",
                CategoryName = "Minigames",
                DisplayName = "Starship Expedition - Event Opened",
                Description = "Triggers when a crew event opens for voting",
                Icon = Icons.Material.Filled.HowToVote,
                Color = Color.Success,
                FeatureKey = FeatureKeys.DropGames,
                DefaultCommandName = "starship",
                DefaultCommandEventType = DefaultCommandEventTypes.StarshipEventOpened
            },
            new()
            {
                Id = "starship-event-resolved",
                Type = TriggerTypes.DefaultCommand,
                CategoryId = "minigames",
                CategoryName = "Minigames",
                DisplayName = "Starship Expedition - Event Resolved",
                Description = "Triggers when a crew vote resolves",
                Icon = Icons.Material.Filled.DoneAll,
                Color = Color.Success,
                FeatureKey = FeatureKeys.DropGames,
                DefaultCommandName = "starship",
                DefaultCommandEventType = DefaultCommandEventTypes.StarshipEventResolved
            },
            new()
            {
                Id = "starship-voyage-ended",
                Type = TriggerTypes.DefaultCommand,
                CategoryId = "minigames",
                CategoryName = "Minigames",
                DisplayName = "Starship Expedition - Voyage Ended",
                Description = "Triggers when a voyage returns or is aborted",
                Icon = Icons.Material.Filled.FlagCircle,
                Color = Color.Success,
                FeatureKey = FeatureKeys.DropGames,
                DefaultCommandName = "starship",
                DefaultCommandEventType = DefaultCommandEventTypes.StarshipVoyageEnded
            },
            new()
            {
                Id = "starship-crew-reward-banked",
                Type = TriggerTypes.DefaultCommand,
                CategoryId = "minigames",
                CategoryName = "Minigames",
                DisplayName = "Starship Expedition - Crew Reward Banked",
                Description = "Triggers once when a crew member's station payout is settled",
                Icon = Icons.Material.Filled.AccountBalance,
                Color = Color.Success,
                FeatureKey = FeatureKeys.DropGames,
                DefaultCommandName = "starship",
                DefaultCommandEventType = DefaultCommandEventTypes.StarshipCrewRewardBanked
            },
            new()
            {
                Id = "starship-risk-succeeded",
                Type = TriggerTypes.DefaultCommand,
                CategoryId = "minigames",
                CategoryName = "Minigames",
                DisplayName = "Starship Expedition - Risk Succeeded",
                Description = "Triggers for a crew member who succeeds at a confirmed risky action",
                Icon = Icons.Material.Filled.CheckCircle,
                Color = Color.Success,
                FeatureKey = FeatureKeys.DropGames,
                DefaultCommandName = "starship",
                DefaultCommandEventType = DefaultCommandEventTypes.StarshipRiskSucceeded
            },
            new()
            {
                Id = "starship-risk-failed",
                Type = TriggerTypes.DefaultCommand,
                CategoryId = "minigames",
                CategoryName = "Minigames",
                DisplayName = "Starship Expedition - Risk Failed",
                Description = "Triggers for a crew member who fails a confirmed risky action",
                Icon = Icons.Material.Filled.Warning,
                Color = Color.Warning,
                FeatureKey = FeatureKeys.DropGames,
                DefaultCommandName = "starship",
                DefaultCommandEventType = DefaultCommandEventTypes.StarshipRiskFailed
            },
            new()
            {
                Id = "defuse-success",
                Type = TriggerTypes.DefaultCommand,
                CategoryId = "minigames",
                CategoryName = "Minigames",
                DisplayName = "Defuse - Success",
                Description = "Triggers when a user successfully defuses the bomb",
                Icon = Icons.Material.Filled.CheckCircle,
                Color = Color.Success,
                FeatureKey = FeatureKeys.Defuse,
                DefaultCommandName = "defuse",
                DefaultCommandEventType = DefaultCommandEventTypes.DefuseSuccess
            },
            new()
            {
                Id = "defuse-failure",
                Type = TriggerTypes.DefaultCommand,
                CategoryId = "minigames",
                CategoryName = "Minigames",
                DisplayName = "Defuse - Failure (Boom)",
                Description = "Triggers when the bomb defusal fails and explodes",
                Icon = Icons.Material.Filled.Dangerous,
                Color = Color.Success,
                FeatureKey = FeatureKeys.Defuse,
                DefaultCommandName = "defuse",
                DefaultCommandEventType = DefaultCommandEventTypes.DefuseFailure
            },
            new()
            {
                Id = "wheelspin-result",
                Type = TriggerTypes.DefaultCommand,
                CategoryId = "minigames",
                CategoryName = "Minigames",
                DisplayName = "Wheel Spin - Result",
                Description = "Triggers when the wheel finishes spinning and awards an outcome",
                Icon = Icons.Material.Filled.ChangeCircle,
                Color = Color.Success,
                FeatureKey = FeatureKeys.WheeledGame,
                DefaultCommandName = "spinwheel",
                DefaultCommandEventType = DefaultCommandEventTypes.WheelSpinResult
            },
            new()
            {
                Id = "heist-started",
                Type = TriggerTypes.DefaultCommand,
                CategoryId = "minigames",
                CategoryName = "Minigames",
                DisplayName = "Bank Heist - Started",
                Description = "Triggers when a new bank heist entry starts",
                Icon = Icons.Material.Filled.Security,
                Color = Color.Success,
                FeatureKey = FeatureKeys.Heist,
                DefaultCommandName = "heist",
                DefaultCommandEventType = DefaultCommandEventTypes.HeistStarted
            },
            new()
            {
                Id = "heist-survived",
                Type = TriggerTypes.DefaultCommand,
                CategoryId = "minigames",
                CategoryName = "Minigames",
                DisplayName = "Bank Heist - User Survived",
                Description = "Triggers when a participant survives the heist with loot",
                Icon = Icons.Material.Filled.SentimentVerySatisfied,
                Color = Color.Success,
                FeatureKey = FeatureKeys.Heist,
                DefaultCommandName = "heist",
                DefaultCommandEventType = DefaultCommandEventTypes.HeistUserSurvived
            },
            new()
            {
                Id = "heist-caught",
                Type = TriggerTypes.DefaultCommand,
                CategoryId = "minigames",
                CategoryName = "Minigames",
                DisplayName = "Bank Heist - User Caught",
                Description = "Triggers when a participant gets caught by the police",
                Icon = Icons.Material.Filled.SentimentDissatisfied,
                Color = Color.Success,
                FeatureKey = FeatureKeys.Heist,
                DefaultCommandName = "heist",
                DefaultCommandEventType = DefaultCommandEventTypes.HeistUserCaught
            },
            new()
            {
                Id = "heist-ended",
                Type = TriggerTypes.DefaultCommand,
                CategoryId = "minigames",
                CategoryName = "Minigames",
                DisplayName = "Bank Heist - Ended",
                Description = "Triggers when the bank heist concludes and results are tallied",
                Icon = Icons.Material.Filled.Flag,
                Color = Color.Success,
                FeatureKey = FeatureKeys.Heist,
                DefaultCommandName = "heist",
                DefaultCommandEventType = DefaultCommandEventTypes.HeistEnded
            },

            // === Gambling & Points ===
            new()
            {
                Id = "gamble-jackpot",
                Type = TriggerTypes.DefaultCommand,
                CategoryId = "gambling",
                CategoryName = "Gambling & Points",
                DisplayName = "Gamble - Jackpot Win",
                Description = "Triggers when a user hits the jackpot on !gamble",
                Icon = Icons.Material.Filled.MonetizationOn,
                Color = Color.Warning,
                FeatureKey = FeatureKeys.Gamble,
                DefaultCommandName = "gamble",
                DefaultCommandEventType = DefaultCommandEventTypes.GambleJackpotWin
            },
            new()
            {
                Id = "gamble-win",
                Type = TriggerTypes.DefaultCommand,
                CategoryId = "gambling",
                CategoryName = "Gambling & Points",
                DisplayName = "Gamble - Win",
                Description = "Triggers on a regular winning !gamble roll",
                Icon = Icons.Material.Filled.ThumbUp,
                Color = Color.Warning,
                FeatureKey = FeatureKeys.Gamble,
                DefaultCommandName = "gamble",
                DefaultCommandEventType = DefaultCommandEventTypes.GambleWin
            },
            new()
            {
                Id = "gamble-lose",
                Type = TriggerTypes.DefaultCommand,
                CategoryId = "gambling",
                CategoryName = "Gambling & Points",
                DisplayName = "Gamble - Lose",
                Description = "Triggers on a losing !gamble roll",
                Icon = Icons.Material.Filled.ThumbDown,
                Color = Color.Warning,
                FeatureKey = FeatureKeys.Gamble,
                DefaultCommandName = "gamble",
                DefaultCommandEventType = DefaultCommandEventTypes.GambleLose
            },
            new()
            {
                Id = "slots-3ofakind",
                Type = TriggerTypes.DefaultCommand,
                CategoryId = "gambling",
                CategoryName = "Gambling & Points",
                DisplayName = "Slots - 3 of a Kind",
                Description = "Triggers when 3 matching symbols line up in !slots",
                Icon = Icons.Material.Filled.Filter3,
                Color = Color.Warning,
                FeatureKey = FeatureKeys.Slots,
                DefaultCommandName = "slot",
                DefaultCommandEventType = DefaultCommandEventTypes.SlotsThreeOfAKind
            },
            new()
            {
                Id = "slots-2ofakind",
                Type = TriggerTypes.DefaultCommand,
                CategoryId = "gambling",
                CategoryName = "Gambling & Points",
                DisplayName = "Slots - 2 of a Kind",
                Description = "Triggers when 2 matching symbols line up in !slots",
                Icon = Icons.Material.Filled.Filter2,
                Color = Color.Warning,
                FeatureKey = FeatureKeys.Slots,
                DefaultCommandName = "slot",
                DefaultCommandEventType = DefaultCommandEventTypes.SlotsTwoOfAKind
            },
            new()
            {
                Id = "slots-lose",
                Type = TriggerTypes.DefaultCommand,
                CategoryId = "gambling",
                CategoryName = "Gambling & Points",
                DisplayName = "Slots - Lose",
                Description = "Triggers on a losing spin in !slots",
                Icon = Icons.Material.Filled.Cancel,
                Color = Color.Warning,
                FeatureKey = FeatureKeys.Slots,
                DefaultCommandName = "slot",
                DefaultCommandEventType = DefaultCommandEventTypes.SlotsLose
            },
            new()
            {
                Id = "roll-doubles",
                Type = TriggerTypes.DefaultCommand,
                CategoryId = "gambling",
                CategoryName = "Gambling & Points",
                DisplayName = "Dice Roll - Doubles",
                Description = "Triggers when matching numbers are rolled in !roll",
                Icon = Icons.Material.Filled.Casino,
                Color = Color.Warning,
                FeatureKey = FeatureKeys.Roll,
                DefaultCommandName = "roll",
                DefaultCommandEventType = DefaultCommandEventTypes.RollDoubles
            },
            new()
            {
                Id = "roll-snakeeyes",
                Type = TriggerTypes.DefaultCommand,
                CategoryId = "gambling",
                CategoryName = "Gambling & Points",
                DisplayName = "Dice Roll - Snake Eyes (Double 1s)",
                Description = "Triggers when double 1s are rolled in !roll",
                Icon = Icons.Material.Filled.Filter1,
                Color = Color.Warning,
                FeatureKey = FeatureKeys.Roll,
                DefaultCommandName = "roll",
                DefaultCommandEventType = DefaultCommandEventTypes.RollSnakeEyes
            },
            new()
            {
                Id = "roll-boxcars",
                Type = TriggerTypes.DefaultCommand,
                CategoryId = "gambling",
                CategoryName = "Gambling & Points",
                DisplayName = "Dice Roll - Boxcars (Double 6s)",
                Description = "Triggers when double 6s are rolled in !roll",
                Icon = Icons.Material.Filled.Filter6,
                Color = Color.Warning,
                FeatureKey = FeatureKeys.Roll,
                DefaultCommandName = "roll",
                DefaultCommandEventType = DefaultCommandEventTypes.RollBoxcars
            },
            new()
            {
                Id = "roll-lose",
                Type = TriggerTypes.DefaultCommand,
                CategoryId = "gambling",
                CategoryName = "Gambling & Points",
                DisplayName = "Dice Roll - Lose",
                Description = "Triggers on a losing roll in !roll",
                Icon = Icons.Material.Filled.Cancel,
                Color = Color.Warning,
                FeatureKey = FeatureKeys.Roll,
                DefaultCommandName = "roll",
                DefaultCommandEventType = DefaultCommandEventTypes.RollLose
            },
            new()
            {
                Id = "steal-success",
                Type = TriggerTypes.DefaultCommand,
                CategoryId = "gambling",
                CategoryName = "Gambling & Points",
                DisplayName = "Steal - Success",
                Description = "Triggers when a user successfully steals points from another user",
                Icon = Icons.Material.Filled.Paid,
                Color = Color.Warning,
                FeatureKey = FeatureKeys.Steal,
                DefaultCommandName = "steal",
                DefaultCommandEventType = DefaultCommandEventTypes.StealSuccess
            },
            new()
            {
                Id = "steal-failed",
                Type = TriggerTypes.DefaultCommand,
                CategoryId = "gambling",
                CategoryName = "Gambling & Points",
                DisplayName = "Steal - Failed",
                Description = "Triggers when a steal attempt fails and points are lost",
                Icon = Icons.Material.Filled.HighlightOff,
                Color = Color.Warning,
                FeatureKey = FeatureKeys.Steal,
                DefaultCommandName = "steal",
                DefaultCommandEventType = DefaultCommandEventTypes.StealFailed
            },
            new()
            {
                Id = "steal-topoor",
                Type = TriggerTypes.DefaultCommand,
                CategoryId = "gambling",
                CategoryName = "Gambling & Points",
                DisplayName = "Steal - Target Too Poor",
                Description = "Triggers when the target user doesn't have enough points to steal",
                Icon = Icons.Material.Filled.MoneyOff,
                Color = Color.Warning,
                FeatureKey = FeatureKeys.Steal,
                DefaultCommandName = "steal",
                DefaultCommandEventType = DefaultCommandEventTypes.StealToPoor
            },

            // === Fishing ===
            new()
            {
                Id = "fishing-fishcatch",
                Type = TriggerTypes.FishCatch,
                CategoryId = "fishing",
                CategoryName = "Fishing",
                DisplayName = "Fish Caught",
                Description = "Triggers whenever a fish is caught, with optional filters for fish, category, rarity, stars, weight, or tournament eligibility",
                Icon = Icons.Material.Filled.Phishing,
                Color = Color.Info,
                FeatureKey = FeatureKeys.Fishing
            },
            new()
            {
                Id = "fishing-tournamentcatch",
                Type = TriggerTypes.FishingTournamentCatch,
                CategoryId = "fishing",
                CategoryName = "Fishing",
                DisplayName = "Tournament Eligible Catch",
                Description = "Triggers when a caught fish matches active tournament eligibility or qualifying place",
                Icon = Icons.Material.Filled.EmojiEvents,
                Color = Color.Info,
                FeatureKey = FeatureKeys.Fishing
            },
            new()
            {
                Id = "fishing-tournamentstart",
                Type = TriggerTypes.FishingTournamentStart,
                CategoryId = "fishing",
                CategoryName = "Fishing",
                DisplayName = "Tournament Start",
                Description = "Triggers when a fishing tournament starts",
                Icon = Icons.Material.Filled.PlayCircle,
                Color = Color.Info,
                FeatureKey = FeatureKeys.Fishing
            },
            new()
            {
                Id = "fishing-tournamentend",
                Type = TriggerTypes.FishingTournamentEnd,
                CategoryId = "fishing",
                CategoryName = "Fishing",
                DisplayName = "Tournament End",
                Description = "Triggers when a fishing tournament ends and winners are settled",
                Icon = Icons.Material.Filled.FlagCircle,
                Color = Color.Info,
                FeatureKey = FeatureKeys.Fishing
            },
            new()
            {
                Id = "fishing-itembroken",
                Type = TriggerTypes.FishingItemBroken,
                CategoryId = "fishing",
                CategoryName = "Fishing",
                DisplayName = "Fishing Item Broken",
                Description = "Triggers when an equipped fishing rod, line, reel, tackle box, or net breaks (durability reaches 0)",
                Icon = Icons.Material.Filled.BrokenImage,
                Color = Color.Info,
                FeatureKey = FeatureKeys.Fishing
            },
            new()
            {
                Id = "fishing-accident",
                Type = TriggerTypes.FishingAccident,
                CategoryId = "fishing",
                CategoryName = "Fishing",
                DisplayName = "Fishing Accident",
                Description = "Triggers when an accident occurs while fishing (line snap, rod break, reel jam, tackle box lost, net broken)",
                Icon = Icons.Material.Filled.WarningAmber,
                Color = Color.Info,
                FeatureKey = FeatureKeys.Fishing
            },

            // === Chat & Commands ===
            new()
            {
                Id = "chat-command",
                Type = TriggerTypes.Command,
                CategoryId = "chat-commands",
                CategoryName = "Chat & Commands",
                DisplayName = "Chat Command",
                Description = "Triggers when a custom chat command is used (e.g. !hello)",
                Icon = Icons.Material.Filled.Terminal,
                Color = Color.Primary
            },
            new()
            {
                Id = "chat-keyword",
                Type = TriggerTypes.Keyword,
                CategoryId = "chat-commands",
                CategoryName = "Chat & Commands",
                DisplayName = "Chat Keyword",
                Description = "Triggers when a keyword or pattern is detected in chat messages",
                Icon = Icons.Material.Filled.SearchOff,
                Color = Color.Primary
            },

            // === Counters & Utilities ===
            new()
            {
                Id = "death-incremented",
                Type = TriggerTypes.DefaultCommand,
                CategoryId = "counters",
                CategoryName = "Counters & Utilities",
                DisplayName = "Death Counter (!death) - Added (+)",
                Description = "Triggers when the death counter is incremented for the current game",
                Icon = Icons.Material.Filled.AddCircle,
                Color = Color.Default,
                FeatureKey = FeatureKeys.DeathCounter,
                DefaultCommandName = "death",
                DefaultCommandEventType = DefaultCommandEventTypes.DeathIncremented
            },
            new()
            {
                Id = "death-decremented",
                Type = TriggerTypes.DefaultCommand,
                CategoryId = "counters",
                CategoryName = "Counters & Utilities",
                DisplayName = "Death Counter (!death) - Removed (-)",
                Description = "Triggers when the death counter is decremented for the current game",
                Icon = Icons.Material.Filled.RemoveCircle,
                Color = Color.Default,
                FeatureKey = FeatureKeys.DeathCounter,
                DefaultCommandName = "death",
                DefaultCommandEventType = DefaultCommandEventTypes.DeathDecremented
            },
            new()
            {
                Id = "death-reset",
                Type = TriggerTypes.DefaultCommand,
                CategoryId = "counters",
                CategoryName = "Counters & Utilities",
                DisplayName = "Death Counter (!death) - Reset",
                Description = "Triggers when the death counter is reset to 0 for the current game",
                Icon = Icons.Material.Filled.RestartAlt,
                Color = Color.Default,
                FeatureKey = FeatureKeys.DeathCounter,
                DefaultCommandName = "death",
                DefaultCommandEventType = DefaultCommandEventTypes.DeathReset
            },
            new()
            {
                Id = "death-set",
                Type = TriggerTypes.DefaultCommand,
                CategoryId = "counters",
                CategoryName = "Counters & Utilities",
                DisplayName = "Death Counter (!death) - Set",
                Description = "Triggers when the death counter is set to an exact number for the current game",
                Icon = Icons.Material.Filled.Edit,
                Color = Color.Default,
                FeatureKey = FeatureKeys.DeathCounter,
                DefaultCommandName = "death",
                DefaultCommandEventType = DefaultCommandEventTypes.DeathSet
            },

            // === Timers & Schedules ===
            new()
            {
                Id = "timer-group",
                Type = TriggerTypes.Timer,
                CategoryId = "timers",
                CategoryName = "Timers & Schedules",
                DisplayName = "Timer Group",
                Description = "Triggers actions on a recurring schedule or interval",
                Icon = Icons.Material.Filled.Timer,
                Color = Color.Info,
                FeatureKey = FeatureKeys.AutoTimer
            },

            // === Media & Audio ===
            new()
            {
                Id = "media-bannedsongrequest",
                Type = TriggerTypes.BannedSongRequest,
                CategoryId = "media",
                CategoryName = "Media & Audio",
                DisplayName = "Banned Song Request",
                Description = "Triggers when someone requests a song that has been banned",
                Icon = Icons.Material.Filled.MusicOff,
                Color = Color.Error,
                FeatureKey = FeatureKeys.MusicPlayer
            },

            // === General & Manual ===
            new()
            {
                Id = "general-manual",
                Type = TriggerTypes.Manual,
                CategoryId = "general",
                CategoryName = "General & Manual",
                DisplayName = "Manual",
                Description = "Triggers manually from the dashboard, hotkey, or API",
                Icon = Icons.Material.Filled.TouchApp,
                Color = Color.Default
            }
        };

        public static bool IsTriggerAvailable(TriggerItemInfo item, IFeatureRuntimeCoordinator? coordinator)
        {
            if (coordinator == null) return true;
            if (!string.IsNullOrEmpty(item.FeatureKey))
            {
                return coordinator.IsEnabled(item.FeatureKey);
            }
            return true;
        }

        public static List<TriggerCategoryInfo> GetAvailableCategories(IFeatureRuntimeCoordinator? coordinator)
        {
            return Categories
                .Where(cat => AllTriggers.Any(t => t.CategoryId == cat.Id && IsTriggerAvailable(t, coordinator)))
                .OrderBy(cat => cat.Order)
                .ToList();
        }

        public static List<TriggerItemInfo> GetTriggersForCategory(string categoryId, IFeatureRuntimeCoordinator? coordinator)
        {
            return AllTriggers
                .Where(t => t.CategoryId.Equals(categoryId, StringComparison.OrdinalIgnoreCase) && IsTriggerAvailable(t, coordinator))
                .OrderBy(t => t.DisplayName)
                .ToList();
        }

        public static List<TriggerItemInfo> SearchTriggers(string searchText, IFeatureRuntimeCoordinator? coordinator)
        {
            if (string.IsNullOrWhiteSpace(searchText))
                return new List<TriggerItemInfo>();

            var query = searchText.Trim();

            return AllTriggers
                .Where(t => IsTriggerAvailable(t, coordinator))
                .Where(t =>
                    t.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    t.Description.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    t.CategoryName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    (t.DefaultCommandName != null && t.DefaultCommandName.Contains(query, StringComparison.OrdinalIgnoreCase)) ||
                    (t.TwitchEventName != null && t.TwitchEventName.Contains(query, StringComparison.OrdinalIgnoreCase)))
                .OrderBy(t => t.DisplayName)
                .ToList();
        }

        public static TriggerItemInfo? FindItem(TriggerTypes type, string? defaultCommandName = null, string? eventType = null, string? twitchEventName = null)
        {
            if (type == TriggerTypes.DefaultCommand && !string.IsNullOrEmpty(defaultCommandName) && !string.IsNullOrEmpty(eventType))
            {
                return AllTriggers.FirstOrDefault(t =>
                    t.Type == TriggerTypes.DefaultCommand &&
                    string.Equals(t.DefaultCommandName, defaultCommandName, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(t.DefaultCommandEventType, eventType, StringComparison.OrdinalIgnoreCase));
            }

            if (type == TriggerTypes.TwitchEvent && !string.IsNullOrEmpty(twitchEventName))
            {
                return AllTriggers.FirstOrDefault(t =>
                    t.Type == TriggerTypes.TwitchEvent &&
                    string.Equals(t.TwitchEventName, twitchEventName, StringComparison.OrdinalIgnoreCase));
            }

            return AllTriggers.FirstOrDefault(t => t.Type == type);
        }
    }
}

