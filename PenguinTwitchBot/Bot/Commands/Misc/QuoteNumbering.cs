using PenguinTwitchBot.Database.Bot.DatabaseTools;

namespace PenguinTwitchBot.Bot.Commands.Misc
{
    /// <summary>
    /// Viewer-facing quote numbers derived from the Guid ids at runtime. Quotes migrated from integer ids
    /// keep their original number; newer quotes continue after the highest legacy number in creation order.
    /// </summary>
    public static class QuoteNumbering
    {
        public static Dictionary<Guid, int> Build(IEnumerable<Guid> allQuoteIds)
        {
            var ids = allQuoteIds.ToList();
            var numbers = new Dictionary<Guid, int>(ids.Count);
            var next = 1;

            foreach (var id in ids)
            {
                if (UuidMigrationHelper.TryGetLegacyInt(id, out var legacy))
                {
                    numbers[id] = (int)legacy;
                    next = Math.Max(next, (int)legacy + 1);
                }
            }

            // UUIDv7 ids sort by creation time.
            foreach (var id in ids.Where(i => !numbers.ContainsKey(i)).OrderBy(i => i))
            {
                numbers[id] = next++;
            }

            return numbers;
        }
    }
}
