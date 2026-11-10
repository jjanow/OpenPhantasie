using System.Runtime.CompilerServices;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client;

/// <summary>
/// s183 (Pad leftovers): help lines name the pad's buttons while the pad is the active input. "Active" follows the last
/// input: a newly pressed pad button turns it on, a real key, a click, the wheel or a mouse move turns it off
/// (<see cref="Note"/>, once a frame in <c>Game1.Update</c>). The wording is data: a help text whose id has a twin
/// <c>&lt;id&gt;.pad</c> in the string table shows that twin instead (<see cref="Help"/>, applied where the help is drawn: the
/// HUD's help line, I Amiga's map line, the menu screens), so the screens keep storing and comparing the keyboard text and
/// need no per-screen switch. A help built from a template (the save-slot picker) or with a state of its own (the modern
/// inventory's <see cref="InventoryScreen.UsingPad"/>) picks its pad text itself. Keyboard players see no change.
/// </summary>
public static class PadWording
{
    /// <summary>The id suffix of a help text's pad wording.</summary>
    public const string Suffix = ".pad";

    // Per thread: the game reads and draws on one thread; tests running in parallel never see another test's state.
    [ThreadStatic] private static bool _active;

    private static readonly ConditionalWeakTable<StringTable, Dictionary<string, string>> Twins = new();

    /// <summary>The last input was the pad.</summary>
    public static bool Active
    {
        get => _active;
        set => _active = value;
    }

    /// <summary>
    /// This frame's input: <paramref name="padPressed"/> a pad button went down, <paramref name="keyOrMouse"/> a real key
    /// went down or the mouse was used. The keyboard and mouse win a frame with both.
    /// </summary>
    public static void Note(bool padPressed, bool keyOrMouse)
    {
        if (keyOrMouse)
            Active = false;
        else if (padPressed)
            Active = true;
    }

    /// <summary><paramref name="help"/> as shown: its pad wording while the pad is active and the table has one.</summary>
    public static string Help(string help) => Active ? PadTextFor(UiText.Table, help) ?? help : help;

    /// <summary>
    /// s185: a help built from a template (the text shown never equals its template, so <see cref="Help"/> cannot find its
    /// twin): the template <paramref name="id"/>'s <c>.pad</c> twin while the pad is active and the table has one, else
    /// <paramref name="id"/>, formatted with <paramref name="args"/>.
    /// </summary>
    public static string Format(string id, params object?[] args)
    {
        if (!Active || !UiText.Table.Contains(id + Suffix))
            return UiText.F(id, args);
        return _formatted = UiText.F(id + Suffix, args);
    }

    [ThreadStatic] private static string? _formatted; // the last pad wording Format built (Shows)

    /// <summary>
    /// <paramref name="help"/> shows in the pad's words now: the pad is active and the text has a twin, or is the pad wording
    /// <see cref="Format"/> built last (a screen that draws its help only in the pad's words, III Amiga's town picture).
    /// </summary>
    public static bool Shows(string help) =>
        Active && (!ReferenceEquals(Help(help), help) || help.Length > 0 && help == _formatted);

    /// <summary>
    /// s185: the template <paramref name="id"/> filled with <paramref name="args"/>, from its <c>.pad</c> twin while the pad
    /// is active and the table has one. For a line built from a template (the text shown is no table text, so
    /// <see cref="Help"/> cannot find it) or one shown as the message rather than the help (I DOS's Party Options and order
    /// lines): the caller rebuilds it each frame, so it follows the input as <see cref="Help"/> does.
    /// </summary>
    public static string F(string id, params object?[] args) => UiText.F(TwinId(id), args);

    /// <summary>s185: the text <paramref name="id"/>, from its <c>.pad</c> twin while the pad is active and the table has one.</summary>
    public static string S(string id) => UiText.S(TwinId(id));

    private static string TwinId(string id) => Active && UiText.Table.Contains(id + Suffix) ? id + Suffix : id;

    /// <summary>
    /// The pad wording of the help text <paramref name="text"/> in <paramref name="table"/> (the <c>.pad</c> twin of the id
    /// whose text it is), or null when it has none.
    /// </summary>
    public static string? PadTextFor(StringTable table, string text)
    {
        ArgumentNullException.ThrowIfNull(table);
        if (string.IsNullOrEmpty(text))
            return null;
        Dictionary<string, string> twins;
        lock (Twins)
            twins = Twins.GetValue(table, BuildTwins);
        return twins.TryGetValue(text, out var pad) ? pad : null;
    }

    /// <summary>Each keyboard help text with a pad twin → that twin (the first id wins where two share a text).</summary>
    public static Dictionary<string, string> BuildTwins(StringTable table)
    {
        ArgumentNullException.ThrowIfNull(table);
        var twins = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (id, pad) in table.Strings.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            if (!id.EndsWith(Suffix, StringComparison.Ordinal))
                continue;
            var baseId = id[..^Suffix.Length];
            if (table.Contains(baseId))
                twins.TryAdd(table.Get(baseId), pad);
        }
        return twins;
    }
}
