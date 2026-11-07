using Microsoft.Xna.Framework.Input;

namespace OpenPhantasie.Client;

/// <summary>A dialog that can wait for typed text (a name, a Guild number) and says which characters it takes.</summary>
public interface ITypedEntry
{
    /// <summary>
    /// The characters the entry takes while it waits for typing, in grid order (a space means the space bar is allowed);
    /// null when nothing is being typed.
    /// </summary>
    string? TypingAlphabet { get; }
}

/// <summary>
/// The mouse's way to type (presentation only): a grid of the entry's characters under the panel text, plus SPACE, DEL and
/// OK. Each cell is the key the keyboard would press (letter, digit, Space, Backspace, Return), so the dialog's own key
/// code runs unchanged. The shell draws it only once the mouse has been used, so keyboard play looks as before.
/// </summary>
public sealed class TypingGrid
{
    /// <summary>Characters on a full grid row.</summary>
    public const int PerRow = 13;

    /// <summary>A character cell is this many layout columns wide (the label sits in its first column).</summary>
    public const int CellColumns = 3;

    /// <summary>Width of the panel text the grid fits in, in layout columns.</summary>
    public const int RowColumns = 48;

    /// <summary>One cell: its label, the key it presses, and its left edge and width in layout pixels from the row start.</summary>
    public sealed record Cell(string Label, Keys Key, int X, int Width);

    public IReadOnlyList<IReadOnlyList<Cell>> Rows { get; }

    private TypingGrid(IReadOnlyList<IReadOnlyList<Cell>> rows) => Rows = rows;

    /// <summary>The grid for <paramref name="alphabet"/> (characters without a key are left out).</summary>
    public static TypingGrid For(string alphabet)
    {
        ArgumentNullException.ThrowIfNull(alphabet);
        var chars = alphabet.Where(c => c != ' ' && KeyFor(c) is not null).Distinct().ToList();
        var labels = new List<List<(string, Keys)>>();
        for (var i = 0; i < chars.Count; i += PerRow)
            labels.Add(chars.Skip(i).Take(PerRow).Select(c => (c.ToString(), KeyFor(c)!.Value)).ToList());
        var controls = new List<(string, Keys)>();
        if (alphabet.Contains(' '))
            controls.Add((UiText.S("typing.space"), Keys.Space));
        controls.Add((UiText.S("typing.del"), Keys.Back));
        controls.Add((UiText.S("typing.ok"), Keys.Enter));

        var rows = labels.Select(Place).ToList();
        if (rows.Count > 0 && RowEnd(rows[^1]) + Place(controls).Sum(c => c.Width) <= RowColumns * PixelFont.Advance)
            rows[^1] = Place([.. labels[^1], .. controls]);
        else
            rows.Add(Place(controls));
        return new TypingGrid(rows);
    }

    private static int RowEnd(IReadOnlyList<Cell> row) => row.Count == 0 ? 0 : row[^1].X + row[^1].Width;

    private static List<Cell> Place(IEnumerable<(string Label, Keys Key)> cells)
    {
        var placed = new List<Cell>();
        var x = 0;
        foreach (var (label, key) in cells)
        {
            var width = Math.Max(CellColumns * PixelFont.Advance, PixelFont.Measure(label) + PixelFont.Advance);
            placed.Add(new Cell(label, key, x, width));
            x += width;
        }
        return placed;
    }

    /// <summary>The key that types <paramref name="c"/>: A–Z (either case), 0–9, space; null for anything else.</summary>
    public static Keys? KeyFor(char c)
    {
        var u = char.ToUpperInvariant(c);
        return u switch
        {
            >= 'A' and <= 'Z' => Keys.A + (u - 'A'),
            >= '0' and <= '9' => Keys.D0 + (u - '0'),
            ' ' => Keys.Space,
            _ => null,
        };
    }
}
