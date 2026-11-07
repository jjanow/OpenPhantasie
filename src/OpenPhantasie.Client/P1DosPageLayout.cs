using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client;

/// <summary>
/// s156: where one of I DOS's map pages (<see cref="P1DosMapPages.Page"/>) goes on DOS's 40 × 25 text grid (8 × 8 cells,
/// the 320 × 200 screen), from <c>dos-screens.json</c> <c>pages</c> (<c>message-and-text.md</c> "Text boxes, key waits and
/// timed lines"): a band (a box page: full width from the band's top row, each row centred as C's
/// <c>(40 − length) / 2</c>, a ruled row above and below), a MESS text window (the same from the text's top row, the
/// records from column 0), or a framed report window found by its title row. Null when DOS's grid can't hold the page
/// whole (a row past 40 columns or the window past row 24, an unknown report): the caller draws the remaster's scrolling
/// panel instead, so nothing is cut. No MonoGame types.
/// </summary>
public sealed record P1DosPageLayout(bool Framed, int Left, int Right, int Top, int Bottom, IReadOnlyList<P1DosPageLayout.Cell> Texts)
{
    /// <summary>A text at a grid cell (its first character's column and its row).</summary>
    public sealed record Cell(int Column, int Row, string Text);

    private const int Columns = 40;
    private const int Rows = 25;

    /// <summary>The layout of <paramref name="page"/>, or null (see the class).</summary>
    public static P1DosPageLayout? For(DosPagePlaces places, P1DosMapPages.Page page)
    {
        ArgumentNullException.ThrowIfNull(places);
        ArgumentNullException.ThrowIfNull(page);
        var rows = page.Rows.Select(r => (r ?? "").TrimEnd()).ToList();
        if (rows.Count == 0)
            return null;
        switch (page.Kind)
        {
            case LogWindowKind.Box:
            {
                var top = places.BandTopRow;
                var texts = new List<Cell>();
                // A row as DOS printed it: stored (a sign's padded row, its blanks counted) when the page has them.
                var source = page.Stored is { } stored && stored.Count == rows.Count ? stored : rows.Select(r => r.Trim()).ToList();
                for (var i = 0; i < source.Count; i++)
                {
                    var text = source[i].TrimEnd();
                    var width = source[i].Length;
                    if (width > Columns)
                        return null;
                    texts.Add(new Cell((Columns - width) / 2, top + 1 + i, text));
                }
                return Fits(top + rows.Count + 1) ? new P1DosPageLayout(false, 0, Columns - 1, top, top + rows.Count + 1, texts) : null;
            }
            case LogWindowKind.Text:
            {
                var top = places.TextTopRow;
                if (rows.Any(r => r.Length > Columns) || !Fits(top + rows.Count + 1))
                    return null;
                return new P1DosPageLayout(false, 0, Columns - 1, top, top + rows.Count + 1,
                    [.. rows.Select((r, i) => new Cell(0, top + 1 + i, r))]);
            }
            default:
            {
                var title = rows[0].Trim();
                var window = places.Reports.FirstOrDefault(kv => GameText.IsLoaded && GameText.S(kv.Key).Trim() == title).Value;
                if (window is null || window.Title.Column + title.Length > window.Right + 1)
                    return null;
                var texts = new List<Cell> { new(window.Title.Column, window.Title.Row, title) };
                for (var i = 1; i < rows.Count; i++)
                {
                    var row = window.First.Row + i - 1;
                    if (row >= window.Bottom || window.First.Column + rows[i].Length > window.Right + 1)
                        return null; // more rows than the window holds, or a row past its frame
                    texts.Add(new Cell(window.First.Column, row, rows[i]));
                }
                return new P1DosPageLayout(true, window.Left, window.Right, window.Top, window.Bottom, texts);
            }
        }
    }

    private static bool Fits(int bottom) => bottom < Rows;
}
