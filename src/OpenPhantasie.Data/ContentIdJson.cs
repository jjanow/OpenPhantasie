using System.Text.Json;
using System.Text.Json.Serialization;
using OpenPhantasie.Core;

namespace OpenPhantasie.Data;

/// <summary>
/// Writes a <see cref="ContentId"/> as its string form (<c>"p1:42"</c>) in remaster JSON saves and reads it back
/// strictly: a bare number (a pre-T1.2 save) or a malformed id is an error, never guessed (no old-save migration).
/// </summary>
public sealed class ContentIdJsonConverter : JsonConverter<ContentId>
{
    public override ContentId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
            throw new BareContentIdException($"Expected a game id like \"p1:42\", got {reader.TokenType}.");
        var text = reader.GetString();
        return ContentId.TryParse(text, out var id)
            ? id
            : throw new JsonException($"'{text}' is not a game id like p1:42.");
    }

    public override void Write(Utf8JsonWriter writer, ContentId value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString());
}

/// <summary>Bare local number ↔ game id at the remaster-save boundary (Core keeps the local numbers).</summary>
internal static class ContentIds
{
    public static ContentId[] ToIds(this IEnumerable<int> locals, GameId game) =>
        locals.Select(l => new ContentId(game, l)).ToArray();

    /// <exception cref="InvalidDataException">An id names another game.</exception>
    public static int[] ToLocals(this IEnumerable<ContentId>? ids, GameId game) =>
        ids?.Select(i => i.LocalIn(game)).ToArray() ?? [];
}

/// <summary>A bare number where a game id belongs: the mark of a save written before T1.2 (<see cref="ContentIdJsonConverter"/>).</summary>
public sealed class BareContentIdException(string message) : JsonException(message);

/// <summary>
/// A remaster save written before game-prefixed ids (Phase 09 T1.2: version &lt; 3 or missing, no <c>"game"</c>, or
/// bare-number ids). Refused, not migrated (no old-save compat); the Client shows its own plain message for it.
/// (An <see cref="IOException"/>: <see cref="InvalidDataException"/>, used for every other unreadable save, is sealed.)
/// </summary>
public sealed class OldSaveFormatException(int version, Exception? inner = null)
    : IOException($"This save (version {version}) was written before game-prefixed ids and can't be loaded.", inner)
{
    /// <summary>The file's <c>version</c> field (0 when missing).</summary>
    public int Version { get; } = version;
}
