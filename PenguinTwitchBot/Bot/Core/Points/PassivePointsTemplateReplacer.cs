using PenguinTwitchBot.Database.Bot.Models.Points;

namespace PenguinTwitchBot.Bot.Core.Points
{
    public static class PassivePointsTemplateReplacer
    {
        public static string ReplaceVariables(string? markdown, PointType? pointType, int pointsPerEntry = 1)
        {
            if (string.IsNullOrEmpty(markdown))
            {
                return string.Empty;
            }

            var ptName = pointType?.Name ?? "Tickets";
            var interval = Math.Max(1, pointType?.PassivePointsIntervalMinutes ?? 5);
            var allViewers = pointType?.PassivePointsAllViewers ?? 0;
            var activeViewers = pointType?.PassivePointsActiveViewers ?? 0;
            var subscribers = pointType?.PassivePointsSubscribers ?? 0;

            var activeTotal = allViewers + activeViewers;
            var subTotal = allViewers + activeViewers + subscribers;

            var intervalsPerHour = 60.0 / interval;
            var allViewersPerHour = (long)Math.Round(allViewers * intervalsPerHour);
            var activeTotalPerHour = (long)Math.Round(activeTotal * intervalsPerHour);
            var subTotalPerHour = (long)Math.Round(subTotal * intervalsPerHour);

            var replacements = new (string Key, string Value)[]
            {
                ("{PointType}", ptName),
                ("{IntervalMinutes}", interval.ToString("N0")),
                ("{Interval}", interval.ToString("N0")),
                ("{AllViewersPerHour}", allViewersPerHour.ToString("N0")),
                ("{AllViewers}", allViewers.ToString("N0")),
                ("{EveryonePoints}", allViewers.ToString("N0")),
                ("{ActiveViewers}", activeViewers.ToString("N0")),
                ("{ActivePoints}", activeViewers.ToString("N0")),
                ("{Subscribers}", subscribers.ToString("N0")),
                ("{SubPoints}", subscribers.ToString("N0")),
                ("{ActiveTotalPerHour}", activeTotalPerHour.ToString("N0")),
                ("{ActiveTotal}", activeTotal.ToString("N0")),
                ("{SubscriberTotalPerHour}", subTotalPerHour.ToString("N0")),
                ("{SubscriberTotal}", subTotal.ToString("N0")),
                ("{SubTotalPerHour}", subTotalPerHour.ToString("N0")),
                ("{SubTotal}", subTotal.ToString("N0")),
                ("{PointsPerEntry}", Math.Max(1, pointsPerEntry).ToString("N0")),
            };

            var result = markdown;
            foreach (var (key, value) in replacements)
            {
                result = result.Replace(key, value, StringComparison.OrdinalIgnoreCase);
            }

            return result;
        }
    }
}

