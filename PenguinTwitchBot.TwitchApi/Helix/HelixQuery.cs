namespace PenguinTwitchBot.TwitchApi.Helix;

internal static class HelixQuery
{
    internal static string Build(string path, IEnumerable<(string Key, string? Value)> parameters)
    {
        var queryParts = new List<string>();

        foreach (var (key, value) in parameters)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            queryParts.Add($"{Uri.EscapeDataString(key)}={Uri.EscapeDataString(value)}");
        }

        return queryParts.Count == 0
            ? path
            : $"{path}?{string.Join("&", queryParts)}";
    }

    internal static IEnumerable<(string Key, string? Value)> Repeat(string key, IEnumerable<string>? values)
    {
        if (values == null)
        {
            yield break;
        }

        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                yield return (key, value);
            }
        }
    }
}
