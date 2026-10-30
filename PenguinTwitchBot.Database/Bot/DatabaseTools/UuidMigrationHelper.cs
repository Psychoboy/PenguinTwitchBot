using System.Text.Json;
using System.Text.Json.Serialization;

namespace PenguinTwitchBot.Database.Bot.DatabaseTools
{
    /// <summary>
    /// Client-side EF Core value generator that produces RFC 9562 time-ordered UUIDv7 values
    /// whenever an entity with an empty Guid primary key (Guid.Empty) is added to a DbContext.
    /// If an entity already has a non-empty Guid (e.g., during backup restore or explicit assignment),
    /// EF Core preserves the existing Guid.
    /// </summary>
    public sealed class UuidV7ValueGenerator : Microsoft.EntityFrameworkCore.ValueGeneration.ValueGenerator<Guid>
    {
        public override bool GeneratesTemporaryValues => false;

        public override Guid Next(Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry entry)
            => Guid.CreateVersion7();
    }

    /// <summary>
    /// Deterministic mapping from legacy positive integer IDs to RFC 9562 UUIDv7 (epoch 0)
    /// and JSON converters that accept both canonical UUID strings and legacy integer IDs.
    /// </summary>
    public static class UuidMigrationHelper
    {
        public static Guid FromLegacyInt(long legacyId)
        {
            if (legacyId <= 0)
            {
                return Guid.Empty;
            }

            return Guid.Parse($"00000000-0000-7000-8000-{legacyId:x12}");
        }

        public static Guid? FromLegacyNullableInt(long? legacyId)
        {
            if (!legacyId.HasValue || legacyId.Value <= 0)
            {
                return null;
            }

            return FromLegacyInt(legacyId.Value);
        }

        /// <summary>True when the Guid is the deterministic mapping of a legacy positive integer id.</summary>
        public static bool TryGetLegacyInt(Guid id, out long legacyId)
        {
            legacyId = 0;
            var text = id.ToString();
            const string prefix = "00000000-0000-7000-8000-";
            return text.StartsWith(prefix, StringComparison.Ordinal)
                && long.TryParse(text.AsSpan(prefix.Length), System.Globalization.NumberStyles.HexNumber, null, out legacyId)
                && legacyId > 0;
        }

        public static bool TryParseGuidOrLegacyInt(string? raw, out Guid result)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                result = Guid.Empty;
                return false;
            }

            if (Guid.TryParse(raw, out result))
            {
                return true;
            }

            if (long.TryParse(raw, out var legacyInt) && legacyInt > 0)
            {
                result = FromLegacyInt(legacyInt);
                return true;
            }

            result = Guid.Empty;
            return false;
        }
    }

    public sealed class LegacyIntCompatibleGuidJsonConverter : JsonConverter<Guid>
    {
        public override Guid Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String)
            {
                var str = reader.GetString();
                if (UuidMigrationHelper.TryParseGuidOrLegacyInt(str, out var guid))
                {
                    return guid;
                }
                if (string.IsNullOrWhiteSpace(str))
                {
                    return Guid.Empty;
                }
                return reader.GetGuid();
            }

            if (reader.TokenType == JsonTokenType.Number && reader.TryGetInt64(out var legacyId))
            {
                return UuidMigrationHelper.FromLegacyInt(legacyId);
            }

            if (reader.TokenType == JsonTokenType.Null)
            {
                return Guid.Empty;
            }

            throw new JsonException($"Unable to convert JSON token {reader.TokenType} to Guid.");
        }

        public override void Write(Utf8JsonWriter writer, Guid value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value);
        }
    }

    public sealed class LegacyIntCompatibleNullableGuidJsonConverter : JsonConverter<Guid?>
    {
        public override Guid? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
            {
                return null;
            }

            if (reader.TokenType == JsonTokenType.String)
            {
                var str = reader.GetString();
                if (string.IsNullOrWhiteSpace(str))
                {
                    return null;
                }
                if (UuidMigrationHelper.TryParseGuidOrLegacyInt(str, out var guid))
                {
                    return guid;
                }
                return reader.GetGuid();
            }

            if (reader.TokenType == JsonTokenType.Number && reader.TryGetInt64(out var legacyId))
            {
                return UuidMigrationHelper.FromLegacyNullableInt(legacyId);
            }

            throw new JsonException($"Unable to convert JSON token {reader.TokenType} to Nullable<Guid>.");
        }

        public override void Write(Utf8JsonWriter writer, Guid? value, JsonSerializerOptions options)
        {
            if (value.HasValue)
            {
                writer.WriteStringValue(value.Value);
            }
            else
            {
                writer.WriteNullValue();
            }
        }
    }

    public sealed class LegacyIntCompatibleGuidListJsonConverter : JsonConverter<List<Guid>>
    {
        private static readonly LegacyIntCompatibleGuidJsonConverter ElementConverter = new();

        public override List<Guid>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
            {
                return null;
            }

            if (reader.TokenType != JsonTokenType.StartArray)
            {
                throw new JsonException($"Unable to convert JSON token {reader.TokenType} to List<Guid>.");
            }

            var list = new List<Guid>();
            while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
            {
                list.Add(ElementConverter.Read(ref reader, typeof(Guid), options));
            }

            return list;
        }

        public override void Write(Utf8JsonWriter writer, List<Guid> value, JsonSerializerOptions options)
        {
            writer.WriteStartArray();
            foreach (var id in value)
            {
                writer.WriteStringValue(id);
            }
            writer.WriteEndArray();
        }
    }
}
