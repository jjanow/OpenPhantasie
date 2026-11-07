using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client;

/// <summary>
/// The Client's on-screen text (T1.5): every string the player sees comes from the active game's
/// <see cref="StringTable"/> by id, never from a literal, so another game's table, a translation (IV's Japanese text in
/// English, or the original Japanese) or a platform font/layout fits the same layer. Layout characters with no letters
/// (cursor <c>&gt;</c>, padding, <c>/</c>, digits) stay in code. The table is the shared shell table (main menu, options,
/// T5.0) with the game's own table over it; until a game is loaded that is the default game's English one, so headless
/// tests and dialogs built with <c>new</c> work without set-up.
/// </summary>
public static class UiText
{
    private static StringTable? _table;
    private static readonly object Gate = new();

    /// <summary>
    /// The table in use; settable when another game or language is loaded. Core's <see cref="GameText"/> reads the same
    /// table, so setting it here (or the first default read) sets <see cref="GameText.Source"/> too.
    /// </summary>
    public static StringTable Table
    {
        get
        {
            if (_table is { } t) return t;
            lock (Gate)
            {
                if (_table is null)
                {
                    _table = For(GameId.P1);
                    GameText.Source = _table;
                }
                return _table;
            }
        }
        set
        {
            _table = value;
            GameText.Source = value;
        }
    }

    /// <summary>The shared shell table with <paramref name="game"/>'s own table over it (the shared one alone if it has none).</summary>
    public static StringTable For(GameId game, string language = "en") => StringTable.ForGame(game, language);

    /// <summary>The text with id <paramref name="id"/>.</summary>
    public static string S(string id) => Table.Get(id);

    /// <summary>The template with id <paramref name="id"/>, filled like <c>$"…"</c> (current culture).</summary>
    public static string F(string id, params object?[] args) => Table.Format(id, args);

    /// <summary>
    /// The on-screen name of an enum value (race, class, game mode, facing…): id <c>enum.&lt;Type&gt;.&lt;Value&gt;</c>.
    /// A value outside the enum (a raw save byte) shows its number, as <c>ToString()</c> did.
    /// </summary>
    public static string Name<T>(T value) where T : struct, Enum =>
        Enum.IsDefined(value) ? S($"enum.{typeof(T).Name}.{value}") : value.ToString();
}
