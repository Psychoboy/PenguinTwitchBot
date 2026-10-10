using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PenguinTwitchBot.Bot.Commands.Features;
using PenguinTwitchBot.Bot.Core;
using PenguinTwitchBot.Bot.Core.Points;
using PenguinTwitchBot.Database.Bot.Core;
using PenguinTwitchBot.Database.Bot.Models;
using PenguinTwitchBot.Database.Repository;

namespace PenguinTwitchBot.Bot.Ai
{
    public partial class OpenAiViewerContextService(
        IServiceBackbone serviceBackbone,
        IViewerFeature viewerFeature,
        IUnitOfWork unitOfWork,
        ILogger<OpenAiViewerContextService> logger,
        IPointsSystem? pointsSystem = null) : IOpenAiViewerContextService
    {
        [GeneratedRegex(@"@(?<name>[a-zA-Z0-9_]{3,25})", RegexOptions.Compiled)]
        private static partial Regex MentionRegex();

        private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
        {
            "the", "and", "for", "are", "but", "not", "you", "all", "any", "can", "had", "her", "was",
            "one", "our", "out", "day", "get", "has", "him", "his", "how", "man", "new", "now", "old",
            "see", "two", "way", "who", "boy", "did", "its", "let", "put", "say", "she", "too", "use",
            "chat", "bot", "ping", "test", "help", "this", "that", "with", "from", "they", "them", "what",
            "when", "where", "which", "will", "would", "about", "there", "their"
        };

        public async Task<string> ProcessViewerTagsAsync(
            string template,
            string? promptText,
            string? instructionsText,
            IReadOnlyDictionary<string, string> variables)
        {
            if (string.IsNullOrWhiteSpace(template))
            {
                return template;
            }

            var hasFullContextTag = template.Contains("%ViewersContext%", StringComparison.OrdinalIgnoreCase) ||
                                   template.Contains("%ChannelContext%", StringComparison.OrdinalIgnoreCase) ||
                                   template.Contains("<viewers_context>", StringComparison.OrdinalIgnoreCase) ||
                                   template.Contains("<channel_context>", StringComparison.OrdinalIgnoreCase);

            var hasMentionedViewersTag = template.Contains("%MentionedViewers%", StringComparison.OrdinalIgnoreCase) ||
                                         template.Contains("<mentioned_viewers>", StringComparison.OrdinalIgnoreCase);

            var hasActiveViewersTag = template.Contains("%ActiveViewers%", StringComparison.OrdinalIgnoreCase);

            if (!hasFullContextTag && !hasMentionedViewersTag && !hasActiveViewersTag)
            {
                return template;
            }

            var result = template;

            if (hasFullContextTag)
            {
                var fullContext = await BuildViewerContextAsync(promptText, instructionsText, variables);
                result = ReplaceIgnoreCase(result, "%ViewersContext%", fullContext);
                result = ReplaceIgnoreCase(result, "%ChannelContext%", fullContext);
                result = ReplaceIgnoreCase(result, "<viewers_context>", fullContext);
                result = ReplaceIgnoreCase(result, "<channel_context>", fullContext);
            }

            if (hasMentionedViewersTag)
            {
                var combinedText = $"{promptText} {instructionsText}".Trim();
                var mentionedContext = await BuildMentionedViewersContextAsync(combinedText, variables);
                result = ReplaceIgnoreCase(result, "%MentionedViewers%", mentionedContext);
                result = ReplaceIgnoreCase(result, "<mentioned_viewers>", mentionedContext);
            }

            if (hasActiveViewersTag)
            {
                var activeList = await GetActiveViewersListAsync();
                result = ReplaceIgnoreCase(result, "%ActiveViewers%", activeList);
            }

            return result;
        }

        public async Task<string> BuildViewerContextAsync(
            string? promptText,
            string? instructionsText,
            IReadOnlyDictionary<string, string>? variables,
            int maxActiveChatters = 30)
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("<channel_context>");

                // 1. Broadcaster
                var broadcasterName = serviceBackbone.BroadcasterName;
                if (string.IsNullOrWhiteSpace(broadcasterName))
                {
                    broadcasterName = "Streamer";
                }
                sb.AppendLine($"  <broadcaster username=\"{EscapeXml(broadcasterName)}\" />");

                // 2. Bot
                var botName = serviceBackbone.BotName;
                if (string.IsNullOrWhiteSpace(botName))
                {
                    botName = "PenguinBot";
                }
                sb.AppendLine($"  <bot username=\"{EscapeXml(botName)}\" />");

                // 3. Caller / Trigger User
                var caller = await ResolveCallerAsync(variables);
                if (caller != null)
                {
                    sb.AppendLine($"  <caller {caller} />");
                }

                // 4. Active Chatters categorized by role
                var activeChattersXml = await BuildActiveChattersBlockAsync(maxActiveChatters);
                sb.AppendLine(activeChattersXml);

                // 5. Mentioned Viewers
                var combinedText = $"{promptText} {instructionsText}".Trim();
                var mentionedViewersXml = await BuildMentionedViewersContextAsync(combinedText, variables);
                if (!string.IsNullOrWhiteSpace(mentionedViewersXml))
                {
                    sb.AppendLine(mentionedViewersXml);
                }

                sb.Append("</channel_context>");
                return sb.ToString();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to build channel viewer context");
                return "<channel_context />";
            }
        }

        public async Task<string> BuildMentionedViewersContextAsync(
            string? text,
            IReadOnlyDictionary<string, string>? variables = null)
        {
            try
            {
                var mentions = ExtractMentions(text, variables);
                if (mentions.Count == 0)
                {
                    return "  <mentioned_viewers />";
                }

                var sb = new StringBuilder();
                sb.AppendLine("  <mentioned_viewers>");

                var addedUsernames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var mention in mentions)
                {
                    if (addedUsernames.Contains(mention)) continue;
                    addedUsernames.Add(mention);

                    var viewerXml = await BuildSingleViewerXmlAsync(mention);
                    if (!string.IsNullOrWhiteSpace(viewerXml))
                    {
                        sb.AppendLine($"    {viewerXml}");
                    }
                }

                sb.Append("  </mentioned_viewers>");
                return sb.ToString();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to build mentioned viewers context");
                return "  <mentioned_viewers />";
            }
        }

        public async Task<string> GetActiveViewersListAsync(int maxActiveChatters = 50)
        {
            try
            {
                var active = viewerFeature.GetActiveViewers() ?? [];
                var topActive = active.Take(maxActiveChatters).ToList();
                return string.Join(", ", topActive);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to retrieve active viewers list");
                return string.Empty;
            }
        }

        private async Task<string> BuildActiveChattersBlockAsync(int maxActiveChatters)
        {
            var rawActive = viewerFeature.GetActiveViewers() ?? [];
            var normalizedActive = rawActive
                .Select(UsernameNormalizer.Normalize)
                .Where(u => !string.IsNullOrWhiteSpace(u))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(maxActiveChatters)
                .ToList();

            var mods = new List<string>();
            var vips = new List<string>();
            var subs = new List<string>();
            var regulars = new List<string>();

            foreach (var username in normalizedActive)
            {
                Viewer? viewer = null;
                try
                {
                    viewer = await viewerFeature.GetViewerByUserName(username);
                }
                catch (Exception ex)
                {
                    logger.LogDebug(ex, "Failed to fetch viewer {Username} for active list", username);
                }

                var displayName = viewer != null && !string.IsNullOrWhiteSpace(viewer.DisplayName)
                    ? viewer.DisplayName
                    : username;

                if (viewer?.isMod == true)
                {
                    mods.Add(displayName);
                }
                else if (viewer?.isVip == true)
                {
                    vips.Add(displayName);
                }
                else if (viewer?.isSub == true)
                {
                    subs.Add(displayName);
                }
                else
                {
                    regulars.Add(displayName);
                }
            }

            var sb = new StringBuilder();
            sb.AppendLine($"  <active_chatters total=\"{normalizedActive.Count}\">");
            sb.AppendLine($"    <moderators count=\"{mods.Count}\">{(mods.Count > 0 ? EscapeXml(string.Join(", ", mods)) : "")}</moderators>");
            sb.AppendLine($"    <vips count=\"{vips.Count}\">{(vips.Count > 0 ? EscapeXml(string.Join(", ", vips)) : "")}</vips>");
            sb.AppendLine($"    <subscribers count=\"{subs.Count}\">{(subs.Count > 0 ? EscapeXml(string.Join(", ", subs)) : "")}</subscribers>");
            sb.AppendLine($"    <viewers count=\"{regulars.Count}\">{(regulars.Count > 0 ? EscapeXml(string.Join(", ", regulars)) : "")}</viewers>");
            sb.Append("  </active_chatters>");

            return sb.ToString();
        }

        private async Task<string?> ResolveCallerAsync(IReadOnlyDictionary<string, string>? variables)
        {
            if (variables == null) return null;

            string? callerUsername = null;
            if (variables.TryGetValue("user", out var u) && !string.IsNullOrWhiteSpace(u)) callerUsername = u;
            else if (variables.TryGetValue("User", out var u2) && !string.IsNullOrWhiteSpace(u2)) callerUsername = u2;
            else if (variables.TryGetValue("Name", out var u3) && !string.IsNullOrWhiteSpace(u3)) callerUsername = u3;

            if (string.IsNullOrWhiteSpace(callerUsername)) return null;

            var normalizedCaller = UsernameNormalizer.Normalize(callerUsername);
            Viewer? viewer = null;
            try
            {
                viewer = await viewerFeature.GetViewerByUserName(normalizedCaller);
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Failed to resolve caller {Username}", callerUsername);
            }

            var displayName = viewer?.DisplayName ?? callerUsername;
            var roles = GetViewerRolesString(viewer);
            var title = viewer?.Title ?? "";

            var attrs = $"username=\"{EscapeXml(normalizedCaller)}\" display_name=\"{EscapeXml(displayName)}\" roles=\"{EscapeXml(roles)}\"";
            if (!string.IsNullOrWhiteSpace(title))
            {
                attrs += $" title=\"{EscapeXml(title)}\"";
            }

            return attrs;
        }

        private async Task<string> BuildSingleViewerXmlAsync(string username)
        {
            var normalized = UsernameNormalizer.Normalize(username);
            Viewer? viewer = null;
            try
            {
                viewer = await viewerFeature.GetViewerByUserName(normalized);
                if (viewer == null)
                {
                    viewer = await unitOfWork.Viewers.Find(x => x.Username == normalized).FirstOrDefaultAsync();
                }
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Error fetching viewer profile for {Username}", normalized);
            }

            if (viewer == null)
            {
                return $"<viewer username=\"{EscapeXml(normalized)}\" status=\"unseen\" />";
            }

            var displayName = !string.IsNullOrWhiteSpace(viewer.DisplayName) ? viewer.DisplayName : viewer.Username;
            var roles = GetViewerRolesString(viewer);
            var title = viewer.Title ?? "";
            var lastSeen = FormatRelativeTime(viewer.LastSeen);

            var attrs = new StringBuilder();
            attrs.Append($"username=\"{EscapeXml(viewer.Username)}\" ");
            attrs.Append($"display_name=\"{EscapeXml(displayName)}\" ");
            attrs.Append($"roles=\"{EscapeXml(roles)}\"");

            if (!string.IsNullOrWhiteSpace(title))
            {
                attrs.Append($" title=\"{EscapeXml(title)}\"");
            }

            attrs.Append($" last_seen=\"{EscapeXml(lastSeen)}\"");

            // Points
            if (pointsSystem != null)
            {
                try
                {
                    var points = await pointsSystem.GetUserPointsByUserId(viewer.UserId, 1);
                    attrs.Append($" points=\"{points.Points:N0}\"");
                }
                catch
                {
                    // Ignore points lookup failures
                }
            }

            // Message Rank
            try
            {
                var msgRank = await unitOfWork.ViewerMessageCounts.GetUserMessageCountWithRankByUsername(viewer.Username);
                if (msgRank != null)
                {
                    attrs.Append($" messages_rank=\"{msgRank.Ranking}\"");
                    attrs.Append($" messages_count=\"{msgRank.MessageCount:N0}\"");
                }
            }
            catch
            {
                // Ignore message rank lookup failures
            }

            // Watch Time Rank
            try
            {
                var timeRank = await unitOfWork.ViewersTime.GetUserTimeWithRankByUsername(viewer.Username);
                if (timeRank != null)
                {
                    attrs.Append($" watch_time_rank=\"{timeRank.Ranking}\"");
                }
            }
            catch
            {
                // Ignore watch time lookup failures
            }

            return $"<viewer {attrs} />";
        }

        private HashSet<string> ExtractMentions(
            string? text,
            IReadOnlyDictionary<string, string>? variables)
        {
            var mentions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (!string.IsNullOrWhiteSpace(text))
            {
                var matches = MentionRegex().Matches(text);
                foreach (Match match in matches)
                {
                    var name = match.Groups["name"].Value;
                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        mentions.Add(UsernameNormalizer.Normalize(name));
                    }
                }
            }

            if (variables != null)
            {
                var checkKeys = new[] { "Args", "rawinput", "TargetUser", "target_user", "targetUser" };
                foreach (var key in checkKeys)
                {
                    if (variables.TryGetValue(key, out var val) && !string.IsNullOrWhiteSpace(val))
                    {
                        var matches = MentionRegex().Matches(val);
                        foreach (Match match in matches)
                        {
                            var name = match.Groups["name"].Value;
                            if (!string.IsNullOrWhiteSpace(name))
                            {
                                mentions.Add(UsernameNormalizer.Normalize(name));
                            }
                        }

                        // If the argument itself is a single username without '@'
                        var trimmed = val.Trim();
                        if (trimmed.Length >= 3 && trimmed.Length <= 25 &&
                            !trimmed.Contains(' ') &&
                            !StopWords.Contains(trimmed))
                        {
                            mentions.Add(UsernameNormalizer.Normalize(trimmed));
                        }
                    }
                }
            }

            // Check if any active chatters are named in the text directly
            if (!string.IsNullOrWhiteSpace(text))
            {
                var active = viewerFeature.GetActiveViewers() ?? [];
                foreach (var chatter in active)
                {
                    if (chatter.Length >= 3 &&
                        !StopWords.Contains(chatter) &&
                        Regex.IsMatch(text, $@"\b{Regex.Escape(chatter)}\b", RegexOptions.IgnoreCase))
                    {
                        mentions.Add(UsernameNormalizer.Normalize(chatter));
                    }
                }
            }

            return mentions;
        }

        private static string GetViewerRolesString(Viewer? viewer)
        {
            if (viewer == null) return "Viewer";

            var roles = new List<string>();
            if (viewer.isBroadcaster) roles.Add("Broadcaster");
            if (viewer.isMod) roles.Add("Moderator");
            if (viewer.isVip) roles.Add("VIP");
            if (viewer.isSub) roles.Add("Subscriber");
            if (viewer.isEditor) roles.Add("Editor");

            return roles.Count > 0 ? string.Join(", ", roles) : "Viewer";
        }

        private static string FormatRelativeTime(DateTime dateTime)
        {
            if (dateTime == DateTime.MinValue) return "never";
            var diff = DateTime.UtcNow - dateTime.ToUniversalTime();
            if (diff.TotalMinutes < 1) return "just now";
            if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes}m ago";
            if (diff.TotalHours < 24) return $"{(int)diff.TotalHours}h ago";
            return $"{(int)diff.TotalDays}d ago";
        }

        private static string EscapeXml(string? value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            return value
                .Replace("&", "&amp;")
                .Replace("\"", "&quot;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("'", "&apos;");
        }

        private static string ReplaceIgnoreCase(string input, string search, string replacement)
        {
            return input.Replace(search, replacement, StringComparison.OrdinalIgnoreCase);
        }
    }
}

