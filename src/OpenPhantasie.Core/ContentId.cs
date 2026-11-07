using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace OpenPhantasie.Core;

/// <summary>
/// A game-scoped content id (Phase 09 T1.2): the game (<see cref="GameId"/>) plus that game's own local number, written
/// <c>p1:42</c>. Items, spells, monsters and dungeon levels carry one in the game data and in remaster saves, so
/// Phantasie II–IV content can sit beside I's without colliding.
/// <para>
/// The local number is the original game's own id (for Phantasie I the DOS table index, or the DNG level number), so
/// Core rules that do arithmetic on it (item ranges, spell slots) keep working on <see cref="Local"/> unchanged, and
/// DOS save import/export maps straight to and from the bare number. The kind (item / spell / monster / dungeon) is not
/// part of the id: it comes from the field that holds it, as in the original data.
/// </para>
/// </summary>
public readonly record struct ContentId(GameId Game, int Local)
{
    /// <summary><c>p1:42</c> (<c>?:42</c> for an unset id, so diagnostics never throw).</summary>
    public override string ToString() =>
        (Enum.IsDefined(Game) ? Game.Prefix() : "?") + ":" + Local.ToString(CultureInfo.InvariantCulture);

    /// <summary>Strict parse of <c>pN:local</c> (N = 1..4, local = a non-negative integer).</summary>
    public static bool TryParse([NotNullWhen(true)] string? text, out ContentId id)
    {
        id = default;
        if (string.IsNullOrEmpty(text)) return false;
        var colon = text.IndexOf(':');
        if (colon <= 0 || colon == text.Length - 1) return false;
        if (!GameIds.TryParsePrefix(text.AsSpan(0, colon), out var game)) return false;
        var digits = text.AsSpan(colon + 1);
        foreach (var c in digits)
            if (c is < '0' or > '9') return false;
        if (!int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var local)) return false;
        id = new ContentId(game, local);
        return true;
    }

    /// <inheritdoc cref="TryParse"/>
    /// <exception cref="FormatException">Not a <c>pN:local</c> id.</exception>
    public static ContentId Parse(string text) =>
        TryParse(text, out var id) ? id : throw new FormatException($"'{text}' is not a game id like p1:42.");

    /// <summary>The local number, after checking the id belongs to <paramref name="expected"/>.</summary>
    /// <exception cref="InvalidDataException">The id names another game.</exception>
    public int LocalIn(GameId expected) =>
        Game == expected
            ? Local
            : throw new InvalidDataException($"Id {this} belongs to {Game.Prefix()}, not {expected.Prefix()}.");
}

/// <summary>Prefix → <see cref="GameId"/> (the inverse of <see cref="GameIdExtensions.Prefix"/>).</summary>
public static class GameIds
{
    public static bool TryParsePrefix(ReadOnlySpan<char> prefix, out GameId game)
    {
        foreach (var g in Enum.GetValues<GameId>())
        {
            if (prefix.SequenceEqual(g.Prefix()))
            {
                game = g;
                return true;
            }
        }

        game = default;
        return false;
    }

    /// <exception cref="FormatException">Not <c>p1</c>…<c>p4</c>.</exception>
    public static GameId ParsePrefix(string prefix) =>
        TryParsePrefix(prefix, out var g) ? g : throw new FormatException($"'{prefix}' is not a game id (p1…p4).");
}
