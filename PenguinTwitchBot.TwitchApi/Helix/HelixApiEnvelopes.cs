using System.Text.Json.Serialization;

namespace PenguinTwitchBot.TwitchApi.Helix;

internal sealed record HelixDataResponse<T>(
    [property: JsonPropertyName("data")] IReadOnlyList<T> Data);

internal sealed record HelixPaginatedDataResponse<T>(
    [property: JsonPropertyName("data")] IReadOnlyList<T> Data,
    [property: JsonPropertyName("pagination")] HelixPagination? Pagination);

internal sealed record HelixObjectWithPaginationResponse<T>(
    [property: JsonPropertyName("data")] T? Data,
    [property: JsonPropertyName("pagination")] HelixPagination? Pagination);

internal sealed record HelixPagination(
    [property: JsonPropertyName("cursor")] string? Cursor);
