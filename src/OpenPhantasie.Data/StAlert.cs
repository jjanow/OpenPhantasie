using System.Globalization;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace OpenPhantasie.Data;

/// <summary>
/// s158: one GEM alert as the ST program hands it to AES <c>form_alert</c> (II ST: PHANT.RSC / OUT.RSC free strings and
/// the strings of <c>0x1668</c>; FORMAT.md 7.4): <c>[icon][line|line…][button|button…]</c>. The text is the disks' own,
/// padding spaces included (they set the box's width).
/// </summary>
public sealed record StAlert(int Icon, IReadOnlyList<string> Lines, IReadOnlyList<string> Buttons)
{
    private static readonly Regex Shape = new(@"^\[(\d)\]\[(.*)\]\[(.*)\]$", RegexOptions.Singleline);
    private static readonly Regex Hole = new(@"%(-?)(\d*)l?([sd])");

    /// <summary>Parse <c>[icon][text][buttons]</c>; '|' splits the lines and the buttons.</summary>
    public static StAlert Parse(string alert)
    {
        ArgumentNullException.ThrowIfNull(alert);
        var m = Shape.Match(alert);
        if (!m.Success)
            throw new FormatException($"Not a GEM alert string: '{alert}'.");
        return new StAlert(m.Groups[1].Value[0] - '0', m.Groups[2].Value.Split('|'), m.Groups[3].Value.Split('|'));
    }

    /// <summary>
    /// The alert with its C <c>printf</c> holes (<c>%s</c>, <c>%8s</c>, <c>%d</c>, <c>%ld</c>) filled in turn from
    /// <paramref name="values"/> (a width pads on the left, as <c>sprintf</c>); a hole without a value stays empty.
    /// </summary>
    public StAlert Fill(IReadOnlyList<string> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        var next = 0;
        string One(string text) => Hole.Replace(text, h =>
        {
            var v = next < values.Count ? values[next] : "";
            next++;
            var width = h.Groups[2].Value.Length > 0 ? int.Parse(h.Groups[2].Value, CultureInfo.InvariantCulture) : 0;
            return h.Groups[1].Value == "-" ? v.PadRight(width) : v.PadLeft(width);
        });
        return this with { Lines = [.. Lines.Select(One)], Buttons = [.. Buttons.Select(One)] };
    }

    /// <summary>The widest line, in characters.</summary>
    public int TextColumns => Lines.Count == 0 ? 0 : Lines.Max(l => l.Length);

    /// <summary>The widest button label, in characters.</summary>
    public int ButtonColumns => Buttons.Count == 0 ? 0 : Buttons.Max(b => b.Length);

    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.Append('[').Append(Icon).Append("][").AppendJoin('|', Lines).Append("][").AppendJoin('|', Buttons).Append(']');
        return sb.ToString();
    }
}

/// <summary>
/// s158: where an <see cref="StAlert"/> box lands on the 320×200 screen (<see cref="StAlertLook.Layout"/>): the root box,
/// its outline, each line's first cell and each button's text area (the border is drawn outside it).
/// </summary>
public sealed record StAlertLayout(
    (int X, int Y, int W, int H) Box, (int X, int Y, int W, int H) Outline,
    IReadOnlyList<(int X, int Y, string Text)> Lines, IReadOnlyList<(int X, int Y, int W, int H, string Text)> Buttons);

/// <summary>
/// s158: the look of II ST's alert boxes (TOS AES <c>form_alert</c>; FORMAT.md 7.4). TOS's AES and its 8×8 font are not
/// on the disks: the geometry is measured on EmuTOS 1.4's alerts in Hatari (<c>parity/shots/p2-endgame-wand-*.png</c>,
/// <c>p2-train-*.png</c>; EmuTOS's <c>form_alert</c> comes from DRI's GEM AES, as TOS's does) `[~]`: a root box of
/// (widest line + 2) columns × (lines + 4) rows of <see cref="Cell"/> px, a one-pixel border inside it and an outline
/// <see cref="Outline"/> px outside, centred in <see cref="Area"/>; the lines left-aligned from column 1, row 1; one button
/// row after a blank row, each button (widest label + 1) columns wide with a <see cref="ButtonBorder"/>-px border outside,
/// the row centred on whole columns. The colours are VDI 0 (fill, register <see cref="FillPen"/>) and VDI 1 (lines and
/// text, register <see cref="InkPen"/>) of the palette the game mode shows (<see cref="Palettes"/>).
/// </summary>
public sealed record StAlertLook(
    int Cell, int Outline, int ButtonBorder, int ButtonGap, (int X, int Y, int W, int H) Area, int FillPen, int InkPen,
    IReadOnlyDictionary<string, string> Palettes)
{
    /// <summary>The box for <paramref name="alert"/>.</summary>
    public StAlertLayout Layout(StAlert alert)
    {
        ArgumentNullException.ThrowIfNull(alert);
        var buttonCols = alert.ButtonColumns + 1;
        var rowCols = alert.Buttons.Count * buttonCols + Math.Max(0, alert.Buttons.Count - 1) * ButtonGap;
        var cols = Math.Max(alert.TextColumns, rowCols) + 2;
        var rows = alert.Lines.Count + 4;
        var (w, h) = (cols * Cell, rows * Cell);
        var x = Area.X + Ceil(Area.W - w);
        var y = Area.Y + Ceil(Area.H - h);
        var lines = alert.Lines.Select((l, i) => (x + Cell, y + Cell * (1 + i), l)).ToList();
        var bx = x + (cols - rowCols) / 2 * Cell;
        var by = y + Cell * (alert.Lines.Count + 2);
        var buttons = alert.Buttons.Select((b, i) => (bx + i * (buttonCols + ButtonGap) * Cell, by, buttonCols * Cell, Cell, b))
            .ToList();
        return new StAlertLayout((x, y, w, h), (x - Outline, y - Outline, w + 2 * Outline, h + 2 * Outline), lines, buttons);
    }

    private static int Ceil(int twice) => (twice + 1) / 2;

    internal static StAlertLook FromDto(Dto dto, string manifestPath)
    {
        if (dto.Area is not { Count: 4 } area || dto.Cell <= 0 || dto.Palettes is not { Count: > 0 } palettes)
            throw new InvalidDataException($"{manifestPath}: field_sounds.alerts.look: needs cell, area [x, y, w, h], palettes.");
        return new StAlertLook(dto.Cell, dto.Outline, dto.ButtonBorder, dto.ButtonGap, (area[0], area[1], area[2], area[3]),
            dto.FillPen, dto.InkPen, palettes.ToDictionary(p => p.Key.ToLowerInvariant(), p => p.Value));
    }

    public sealed class Dto
    {
        [JsonPropertyName("_note")] public string? Note { get; set; }
        public int Cell { get; set; }
        public int Outline { get; set; }
        [JsonPropertyName("button_border")] public int ButtonBorder { get; set; }
        [JsonPropertyName("button_gap")] public int ButtonGap { get; set; }
        public List<int>? Area { get; set; }
        [JsonPropertyName("fill_pen")] public int FillPen { get; set; }
        [JsonPropertyName("ink_pen")] public int InkPen { get; set; }
        public Dictionary<string, string>? Palettes { get; set; }
    }
}
