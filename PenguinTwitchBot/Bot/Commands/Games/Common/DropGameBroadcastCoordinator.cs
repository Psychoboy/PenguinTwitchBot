using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using PenguinTwitchBot.Bot.Notifications;

namespace PenguinTwitchBot.Bot.Commands.Games.Common
{
    /// <summary>
    /// Coordinates cross-game status notifications and drop activity alerts between minigames (Parachute, Plinko, Balloon, Curling).
    /// </summary>
    public static class DropGameBroadcastCoordinator
    {
        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

        public static readonly IReadOnlyList<string> AllMinigameTopics =
        [
            WsTopics.Parachute,
            WsTopics.Plinko,
            WsTopics.Balloon,
            WsTopics.Curling
        ];

        /// <summary>
        /// Returns all minigame WebSocket topics except the specified current game.
        /// </summary>
        public static IEnumerable<string> GetOtherGameTopics(string currentGameTopic)
        {
            return AllMinigameTopics.Where(t => !string.Equals(t, currentGameTopic, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Creates the JSON payload alerting other overlays whether another drop game is currently running a timed session.
        /// </summary>
        public static string CreateOtherGameStatusPayload(bool isOtherGameActive, string otherGame)
        {
            return JsonSerializer.Serialize(new
            {
                type = "other_game_status",
                isOtherGameActive,
                otherGame
            }, JsonOptions);
        }

        /// <summary>
        /// Creates the JSON payload alerting other overlays of active drop/slide motion to avoid overlapping banners or visual noise.
        /// </summary>
        public static string CreateOtherDropActivePayload(double durationSeconds = 6.0)
        {
            return JsonSerializer.Serialize(new
            {
                type = "other_drop_active",
                durationSeconds
            }, JsonOptions);
        }
    }
}

