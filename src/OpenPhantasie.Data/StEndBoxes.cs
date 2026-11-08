using System.Text.Json.Serialization;

namespace OpenPhantasie.Data;

/// <summary>
/// One end-of-fight box shape (an OUT.RSC tree): <see cref="Columns"/> × <see cref="Rows"/> cells, the text objects' cells
/// (<see cref="TextCells"/>, one per row of text) and the hold after Speed (<see cref="HoldMs"/>).
/// </summary>
public sealed record StEndBoxShape(int Columns, int Rows, IReadOnlyList<(int Column, int Row)> TextCells, double HoldMs);

/// <summary>Where a box and its text rows land on the 320×200 screen (<see cref="StEndBoxes.Layout"/>).</summary>
public sealed record StEndBoxLayout(int X, int Y, int Width, int Height, IReadOnlyList<(int X, int Y, string Text)> Rows);

/// <summary>
/// Phantasie II ST's end-of-fight boxes (s137 / s138, <c>reverse-engineering/atari-st/phantasie2/FORMAT.md</c> 7.3a), from
/// the pack's <c>combat_lines.end_boxes</c>: OUT.RSC tree 40 (<see cref="OneLine"/>, <c>0xa124</c>) for the lines in
/// <see cref="Keys"/> (and <see cref="OutsideRoundKeys"/> outside a round), tree 41 (<see cref="TwoLine"/>, <c>0xa1c2</c>)
/// for each of <see cref="Pairs"/> ([upper, lower] string ids). Placed at (<see cref="X"/>, <see cref="Y"/>) in
/// <see cref="Cell"/>-px cells, each text centred in <see cref="CenterColumns"/> columns (<c>0x17266</c>); held for Speed +
/// the shape's hold (a key ends it). <see cref="Sounds"/>: a sound played before the box (<c>0xb80e</c>).
/// Colours are "#RRGGBB" from the pack palette the box names.
/// </summary>
public sealed record StEndBoxes(
    int X, int Y, int Cell, int CenterColumns, int Border, string BorderColour, string FillColour, string TextColour,
    StEndBoxShape OneLine, StEndBoxShape TwoLine, IReadOnlyList<string> Keys, IReadOnlyList<string> OutsideRoundKeys,
    IReadOnlyList<IReadOnlyList<string>> Pairs, IReadOnlyDictionary<string, int> Sounds)
{
    /// <summary>s165: the registers of <see cref="BorderColour"/>, <see cref="FillColour"/> and <see cref="TextColour"/> (VDI 0 /
    /// 6 / 0 of the root G_BOX and its G_TEXT), for a box shown over another palette (<see cref="Colours"/>).</summary>
    public (int Border, int Fill, int Text) Pens { get; init; } = (-1, -1, -1);

    /// <summary>
    /// s165: the box's border, fill and text colours ("#RRGGBB") in <paramref name="palette"/> (the palette the screen shows:
    /// the box's G_BOX draws in VDI pens, so the field's <c>map</c> registers colour it there); the pack's own colours when
    /// the palette lacks a pen or no pens are known.
    /// </summary>
    public (string Border, string Fill, string Text) Colours(IReadOnlyList<string>? palette)
    {
        string Pen(int pen, string fallback) => palette is not null && pen >= 0 && pen < palette.Count ? palette[pen] : fallback;
        return (Pen(Pens.Border, BorderColour), Pen(Pens.Fill, FillColour), Pen(Pens.Text, TextColour));
    }

    /// <summary><c>0x17266(str, buf, 35)</c>: the spaces put before a text of <paramref name="length"/> characters.</summary>
    public int Pad(int length) => Math.Max(0, (CenterColumns - length) / 2);

    /// <summary>The sound played before the box for string id <paramref name="key"/> (−1 = none).</summary>
    public int SoundFor(string key) => Sounds.TryGetValue(key, out var n) ? n : -1;

    /// <summary>
    /// The box for <paramref name="rows"/> (one row: tree 40, two: tree 41): its rectangle and each row's first cell, in
    /// screen pixels; row i starts <see cref="Pad"/> cells right of its text object. A text that would run past the box
    /// (a long translation or name) widens it to the right, keeping the left margin, so nothing is cut.
    /// </summary>
    public StEndBoxLayout Layout(IReadOnlyList<string> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        var shape = rows.Count >= 2 ? TwoLine : OneLine;
        var columns = shape.Columns;
        var placed = new List<(int, int, string)>();
        for (var i = 0; i < rows.Count && i < shape.TextCells.Count; i++)
        {
            var (col, row) = shape.TextCells[i];
            var text = rows[i];
            var start = col + Pad(text.Length);
            columns = Math.Max(columns, start + text.Length + col);
            placed.Add((X + Cell * start, Y + Cell * row, text));
        }
        return new StEndBoxLayout(X, Y, Cell * columns, Cell * shape.Rows, placed);
    }

    internal static StEndBoxes FromDto(Dto dto, IReadOnlyDictionary<string, List<string>>? palettes,
        Func<string, InvalidDataException> bad)
    {
        if (dto.Palette is null || palettes?.GetValueOrDefault(dto.Palette) is not { } palette)
            throw bad($"end_boxes: palette '{dto.Palette}' not in palettes");
        string Pen(int pen) => pen >= 0 && pen < palette.Count ? palette[pen] : throw bad($"end_boxes: pen {pen}");
        StEndBoxShape Shape(ShapeDto? s, string name) => s is { Columns: > 0, Rows: > 0, TextCells.Count: > 0 }
            && s.TextCells.All(c => c is { Count: 2 })
            ? new StEndBoxShape(s.Columns, s.Rows, [.. s.TextCells.Select(c => (c[0], c[1]))], s.HoldMs)
            : throw bad($"end_boxes: {name} needs columns, rows and text_cells");
        var pairs = dto.Pairs ?? [];
        if (pairs.Any(p => p is not { Count: 2 }))
            throw bad("end_boxes: each pair names two string ids");
        return new StEndBoxes(dto.X, dto.Y, dto.Cell, dto.CenterColumns, dto.Border, Pen(dto.BorderPen), Pen(dto.FillPen),
            Pen(dto.TextPen), Shape(dto.OneLine, "one_line"), Shape(dto.TwoLine, "two_line"), [.. dto.Keys ?? []],
            [.. dto.OutsideRoundKeys ?? []], [.. pairs.Select(p => (IReadOnlyList<string>)[.. p])],
            new Dictionary<string, int>(dto.Sounds ?? [], StringComparer.Ordinal))
        {
            Pens = (dto.BorderPen, dto.FillPen, dto.TextPen),
        };
    }

    internal sealed class Dto
    {
        public string? Palette { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public int Cell { get; set; } = 8;
        [JsonPropertyName("center_columns")] public int CenterColumns { get; set; }
        public int Border { get; set; }
        [JsonPropertyName("border_pen")] public int BorderPen { get; set; }
        [JsonPropertyName("fill_pen")] public int FillPen { get; set; }
        [JsonPropertyName("text_pen")] public int TextPen { get; set; }
        [JsonPropertyName("one_line")] public ShapeDto? OneLine { get; set; }
        [JsonPropertyName("two_line")] public ShapeDto? TwoLine { get; set; }
        public List<string>? Keys { get; set; }
        [JsonPropertyName("outside_round_keys")] public List<string>? OutsideRoundKeys { get; set; }
        public List<List<string>>? Pairs { get; set; }
        public Dictionary<string, int>? Sounds { get; set; }
    }

    internal sealed class ShapeDto
    {
        public int Columns { get; set; }
        public int Rows { get; set; }
        [JsonPropertyName("text_cells")] public List<List<int>>? TextCells { get; set; }
        [JsonPropertyName("hold_ms")] public double HoldMs { get; set; }
    }
}
