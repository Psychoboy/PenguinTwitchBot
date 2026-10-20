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
            WsTopics.Curling,
            WsTopics.Golf
        ];

        /// <summary>
        /// Returns all minigame WebSocket topics except the specified current game.
        /// </summary>
        public static IEnumerable<string> GetOtherGameTopics(string currentGameTopic)
        {
            return AllMinigameTopics.Where(t => !string.Equals(t, currentGameTopic, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Returns the lowercased names of all drop games currently running an active timed session according to the registry.
        /// </summary>
        public static List<string> GetActiveGameNames(IDropRegistry registry)
        {
            ArgumentNullException.ThrowIfNull(registry);
            return registry.GetActiveSessionTypes()
                .Select(t => t.ToString().ToLowerInvariant())
                .ToList();
        }

        /// <summary>
        /// Creates the JSON payload alerting other overlays whether another drop game is currently running a timed session.
        /// </summary>
        public static string CreateOtherGameStatusPayload(bool isOtherGameActive, string otherGame, IReadOnlyList<string>? activeGames = null)
        {
            return JsonSerializer.Serialize(new
            {
                type = "other_game_status",
                isOtherGameActive,
                otherGame,
                activeGames
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

        /// <summary>
        /// Asynchronously broadcasts a session status change (active/inactive) to all other minigame topics.
        /// </summary>
        public static async Task BroadcastOtherGameStatusAsync(
            IWebSocketMessenger webSocketMessenger,
            ILogger? logger,
            string currentGameTopic,
            string gameName,
            bool isOtherGameActive,
            IReadOnlyList<string>? activeGames = null)
        {
            ArgumentNullException.ThrowIfNull(webSocketMessenger);
            var payload = CreateOtherGameStatusPayload(isOtherGameActive, gameName, activeGames);
            var description = $"Notify other games {gameName} session {(isOtherGameActive ? "started" : "stopped")}";
            foreach (var topic in GetOtherGameTopics(currentGameTopic))
            {
                try
                {
                    await webSocketMessenger.AddToQueue(topic, payload);
                }
                catch (Exception ex)
                {
                    logger?.LogError(ex, "Failed to queue {Description} message to topic {Topic}", description, topic);
                }
            }
        }

        /// <summary>
        /// Asynchronously alerts all other minigame topics of active drop/slide movement to trigger auto-avoid positioning.
        /// </summary>
        public static async Task BroadcastOtherDropActiveAsync(
            IWebSocketMessenger webSocketMessenger,
            ILogger? logger,
            string currentGameTopic,
            double durationSeconds = 6.0)
        {
            ArgumentNullException.ThrowIfNull(webSocketMessenger);
            var payload = CreateOtherDropActivePayload(durationSeconds);
            foreach (var topic in GetOtherGameTopics(currentGameTopic))
            {
                try
                {
                    await webSocketMessenger.AddToQueue(topic, payload);
                }
                catch (Exception ex)
                {
                    logger?.LogError(ex, "Failed to queue other_drop_active message to topic {Topic}", topic);
                }
            }
        }
    }
}

