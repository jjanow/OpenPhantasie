using System.Globalization;

namespace OpenPhantasie.Core;

/// <summary>A source of on-screen text by id (T1.5). <c>OpenPhantasie.Data.StringTable</c> is the JSON-backed one.</summary>
public interface ITextSource
{
    /// <summary>The text or composite-format template with id <paramref name="id"/>; throws when the id is missing.</summary>
    string Get(string id);
}

/// <summary>
/// The player-visible text Core builds (log lines, prompts, refusals, town results), looked up by id in the running
/// game's string table instead of written as literals (T1.5), so another game's text or a translation (IV's Japanese
/// text in English) plugs in without touching the rules. Ids are <c>core.&lt;area&gt;.&lt;words&gt;</c>; templates use
/// .NET composite formatting (<c>{0}</c>, <c>{0,-10}</c>), filled in the current culture exactly as <c>$"…"</c> did; a
/// text argument also takes C's precision, <c>{0,-10:.9}</c> = <c>%-10.9s</c> (at most 9 characters, then padded).
/// Layout with no letters (separators, padding, <c>TrimEnd</c>, truncation) stays in code. Debug and exception messages
/// are not player text and stay literal.
/// <para>
/// The table is ambient (one game runs at a time): the Client sets <see cref="Source"/> when it loads a game
/// (<c>UiText.Table</c>), the test projects set it from a module initializer. Core itself never reads files.
/// </para>
/// </summary>
public static class GameText
{
    private static volatile ITextSource? _source;

    /// <summary>The table in use. Unset → every lookup throws (Core has no built-in text).</summary>
    public static ITextSource Source
    {
        get => _source ?? throw new InvalidOperationException(
            "No string table is loaded: set GameText.Source (OpenPhantasie.Data StringTable.ForGame) before running Core.");
        set => _source = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>True once a table has been set.</summary>
    public static bool IsLoaded => _source is not null;

    /// <summary>The text with id <paramref name="id"/>.</summary>
    public static string S(string id) => Source.Get(id);

    /// <summary>The template with id <paramref name="id"/> filled with <paramref name="args"/> (current culture, as <c>$"…"</c>).</summary>
    public static string F(string id, params object?[] args) =>
        string.Format(PrecisionFormat.Current, Source.Get(id), args);
}

/// <summary>
/// s156: the current culture's formatting plus C's string precision for text arguments: <c>{0:.9}</c> keeps at most 9
/// characters (DOS I's <c>%-10.9s</c> name fields: the trap, op report, swim and pool rows). Other arguments and formats
/// are formatted as <see cref="string.Format(IFormatProvider, string, object[])"/> with the current culture does.
/// </summary>
internal sealed class PrecisionFormat : IFormatProvider, ICustomFormatter
{
    private PrecisionFormat()
    {
    }

    /// <summary>The provider for the current culture (read at each call).</summary>
    public static PrecisionFormat Current { get; } = new();

    public object? GetFormat(Type? formatType) => formatType == typeof(ICustomFormatter) ? this : null;

    public string Format(string? format, object? arg, IFormatProvider? formatProvider)
    {
        if (arg is string text && format is ['.', .. var digits]
            && int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var precision))
            return text.Length > precision ? text[..precision] : text;
        return arg is IFormattable formattable
            ? formattable.ToString(format, CultureInfo.CurrentCulture)
            : arg?.ToString() ?? "";
    }
}
