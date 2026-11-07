using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenPhantasie.Data;

/// <summary>A rectangle in pack pixels, relative to what its owner says.</summary>
public readonly record struct PanelRect(int X, int Y, int Width, int Height);

/// <summary>A sunken frame (<c>_whole</c>): <see cref="Rect"/>, 2 pixels of <see cref="Dark"/> top / left and <see cref="Light"/> bottom / right.</summary>
public sealed record SunkenFrame(PanelRect Rect, int Dark, int Light);

/// <summary>The window title (s151, <c>_wttl</c>): font, colour and the glyph cell's top relative to the window's area.</summary>
public sealed record PanelTitleStyle(int Font, int Colour, int Y);

/// <summary>The hover look (s151, <c>_s_ms_get_cp</c> / <c>_xbtn</c>): an icon's opaque pixels' colour ^ <see cref="IconXor"/>, a plain button's ^ <see cref="BoxXor"/>.</summary>
public sealed record PanelHover(int IconXor, int BoxXor);

/// <summary>
/// <c>_ewait</c>'s SPACE bar (s151) in the location map's message window (<see cref="MessageWidth"/> wide, its top
/// <see cref="MessageHeight"/> rows colour <see cref="MessageColour"/>): a line, the label centred on
/// <see cref="LabelX"/> at <see cref="LabelY"/>, a plain button pressing <see cref="Key"/>; INPUTS / INPUTN's band
/// (<see cref="Band"/>, colour <see cref="BandColour"/>) with their prompt at <see cref="PromptX"/>.
/// </summary>
public sealed record EventWaitBar(int MessageWidth, int MessageHeight, int MessageColour, PanelRect Line, int LineColour,
    string Label, string Text, int LabelX, int LabelY, PanelRect Button, string Key, PanelRect Band, int BandColour,
    int PromptX, string Letters, string LettersText, string Number, string NumberText);

/// <summary>One key of <see cref="Keypad"/>: the character drawn, the key it presses (a digit, <c>Back</c>, <c>Enter</c>), <c>_numin</c>'s value.</summary>
public sealed record KeypadKey(string Text, string Key, int Value);

/// <summary>
/// <c>_numin</c>'s keypad (s151) in window <see cref="Window"/> at (<see cref="X"/>, <see cref="Y"/>): key k at
/// (x + step · (k % 3), y + step · (k / 3)), <see cref="Size"/> square, " c " on <see cref="CellColour"/> cells; the
/// sunken <see cref="Frame"/> and the <see cref="Clear"/> grey relative to (x, y); the steady cursor block.
/// </summary>
public sealed record Keypad(int Window, int X, int Y, int Step, int Size, int Digits, SunkenFrame Frame, PanelRect Clear,
    int CellColour, int TextColour, int BackColour, IReadOnlyList<KeypadKey> Keys, int CursorWidth, int CursorHeight,
    int CursorColour);

/// <summary>A place in the shipwright's window and what is printed there.</summary>
public sealed record ShipwrightGold(int X, int Y, string Label, string LabelText, int Digits, string Unit, string UnitText);

/// <summary>
/// <c>_shipyard</c>'s window (s151): <see cref="Window"/> with SHIP.PKH's <see cref="PictureHeight"/> rows at its origin,
/// the rest colour <see cref="BackColour"/>; the offer's line starts, the answer centred at <see cref="AnswerX"/>, the gold line.
/// </summary>
public sealed record ShipwrightWindow(PanelRect Window, int PictureHeight, int BackColour, int TextColour, string Title,
    string TitleText, IReadOnlyList<(int X, int Y)> Lines, int AnswerX, int AnswerY, string Yes, string No,
    ShipwrightGold Gold);

/// <summary>A line of text at (<see cref="X"/>, <see cref="Y"/>) (centred on x where its owner says), its colour and string id.</summary>
public sealed record SkillUpText(int X, int Y, int Colour, string Label, string Text);

/// <summary>
/// <c>_sklup</c>'s rows (s152): <see cref="Count"/> rows from <see cref="Top"/> every <see cref="Step"/> pack pixels, each
/// cleared to <see cref="Back"/>; the key letter, the skill, now and after (<see cref="Digits"/> digits by <c>_wdouti</c>)
/// at their x; the row's plain button <see cref="Button"/> (its y = the row's).
/// </summary>
public sealed record SkillUpRows(int Count, int Top, int Step, int KeyX, int SkillX, int NowX, int AfterX, int Digits,
    int Back, PanelRect Button)
{
    /// <summary>The y of row <paramref name="i"/>.</summary>
    public int YOf(int i) => Top + Step * i;
}

/// <summary>
/// <c>_sklup</c>'s level-up skill window (s152, FORMAT.md 6.9 "Level-up skill picks"): window 32 at <see cref="Window"/>
/// (no fill or frame of its own) over <c>_trang</c>'s window <see cref="Under"/> (grey <see cref="BackColour"/>); the
/// <see cref="Bar"/> above it (y &lt; 0, colour <see cref="BarColour"/>) with <see cref="Title"/> centred, at the end the
/// bar in <see cref="DoneBack"/> with <see cref="Done"/>; the <see cref="Header"/>; <c>_whole</c>'s sunken
/// <see cref="Frame"/>; the rows; the prompt and trained lines' places. Text in font 0, <see cref="TextColour"/> on
/// <see cref="CellColour"/> cell boxes.
/// </summary>
public sealed record SkillUpWindow(PanelRect Window, PanelRect Under, int BackColour, PanelRect Bar, int BarColour,
    SkillUpText Title, SkillUpText Done, int DoneBack, SkillUpText Header, SunkenFrame Frame, SkillUpRows Rows,
    (int X, int Y) Prompt, (int X, int Y) Trained, int TextColour, int CellColour);

/// <summary>
/// One piece of <see cref="TrainingHallWindow"/>'s text: at (<see cref="X"/>, <see cref="Y"/>) when given, else at the text
/// cursor after the last piece; a string (<see cref="Label"/> / <see cref="Text"/>) or, with <see cref="Number"/>, the
/// note's amount (left-aligned, no padding); in <see cref="Font"/> and <see cref="Colour"/> (null = the window's ink).
/// </summary>
public sealed record TrainingHallSegment(int? X, int? Y, string Label, string Text, bool Number, int Font, int? Colour);

/// <summary>
/// <c>_trang</c>'s own window 32 (s156, FORMAT.md 6.9 "Level-up skill picks"): <see cref="Window"/> in grey
/// <see cref="BackColour"/>, the member's name at <see cref="Name"/>, then the note's segments (keys
/// <c>poor_condition</c>, <c>nothing_more</c>, <c>needs_experience</c>, <c>cost</c>), and <see cref="ShortOfGold"/> after a
/// YES the purse cannot pay. Text in font 0, <see cref="TextColour"/>; rows <see cref="LineStep"/> apart. s158: the
/// window is titled (<c>_wopen</c> flag 1): <see cref="Title"/> is its bar's string id (<see cref="TitleText"/> the
/// original's text).
/// </summary>
public sealed record TrainingHallWindow(PanelRect Window, int BackColour, int TextColour, int LineStep, (int X, int Y) Name,
    IReadOnlyDictionary<string, IReadOnlyList<TrainingHallSegment>> Notes, TrainingHallSegment ShortOfGold,
    string Title = "", string TitleText = "");

/// <summary>The smaller pieces of IV X68000's panels (s151, FORMAT.md 6.9), each null when the pack's file lacks it.</summary>
public sealed record PanelExtras(PanelTitleStyle? Title, PanelHover? Hover, EventWaitBar? EventWait, Keypad? Keypad,
    ShipwrightWindow? Shipwright, SkillUpWindow? SkillUp = null, TrainingHallWindow? TrainingHall = null)
{
    public static PanelExtras Load(string path)
    {
        var d = JsonSerializer.Deserialize<Dto>(File.ReadAllText(path)) ?? new Dto();
        static PanelRect R(RectDto? r) => r is null ? default : new(r.X, r.Y, r.Width, r.Height);
        return new(
            d.Title is { } t ? new(t.Font, t.Colour, t.Y) : null,
            d.Hover is { } h ? new(h.IconXor, h.BoxXor) : null,
            d.EWait is { Message: { } m, Line: { } l, Button: { } b, Band: { } band } e
                ? new(m.Width, m.Height, m.Colour, R(l), l.Colour, e.Label ?? "", e.Text ?? "", e.LabelX, e.LabelY, R(b),
                    e.Key ?? "Space", R(band), band.Colour, e.PromptX, e.Letters ?? "", e.LettersText ?? "", e.Number ?? "",
                    e.NumberText ?? "")
                : null,
            d.NumIn is { Frame: { } f, Clear: { } c, Keys: { } keys, Cursor: { } cur } n
                ? new(n.Window, n.X, n.Y, n.Step, n.Size, n.Digits, new(R(f), f.Dark, f.Light), R(c), n.CellColour,
                    n.TextColour, n.BackColour, [.. keys.Select(k => new KeypadKey(k.Text ?? "", k.Key ?? "", k.Value))],
                    cur.Width, cur.Height, cur.Colour)
                : null,
            d.Ship is { Window: { } w, Lines: { } lines, Answer: { } a, Gold: { } g } s
                ? new(R(w), s.PictureHeight, s.BackColour, s.TextColour, s.Title ?? "", s.TitleText ?? "",
                    [.. lines.Select(p => (p.X, p.Y))], a.X, a.Y, a.Yes ?? "", a.No ?? "",
                    new(g.X, g.Y, g.Label ?? "", g.LabelText ?? "", g.Digits, g.Unit ?? "", g.UnitText ?? ""))
                : null,
            d.Sklup is { Window: { } sw, Under: { } su, Bar: { } sb, Title: { } st, Done: { } sd, Header: { } sh,
                Frame: { } sf, Rows: { Button: { } btn } sr, Prompt: { } sp, Trained: { } stt } k
                ? new(R(sw), R(su), k.BackColour, R(sb), sb.Colour, Line(st), Line(sd), sd.Back, Line(sh),
                    new(R(sf), sf.Dark, sf.Light),
                    new(sr.Count, sr.Top, sr.Step, sr.KeyX, sr.SkillX, sr.NowX, sr.AfterX, sr.Digits, sr.Back, R(btn)),
                    (sp.X, sp.Y), (stt.X, stt.Y), k.TextColour, k.CellColour)
                : null,
            d.Trang is { Window: { } tw, Name: { } tn, Notes: { } notes, ShortOfGold: { } shortOf } tr
                ? new(R(tw), tr.BackColour, tr.TextColour, tr.LineStep, (tn.X, tn.Y),
                    notes.ToDictionary(p => p.Key, p => (IReadOnlyList<TrainingHallSegment>)[.. p.Value.Select(Seg)]),
                    Seg(shortOf), tr.Title ?? "", tr.TitleText ?? "")
                : null);
        static SkillUpText Line(LineDto l) => new(l.X, l.Y, l.Colour, l.Label ?? "", l.Text ?? "");
        static TrainingHallSegment Seg(SegmentDto s) =>
            new(s.X, s.Y, s.Label ?? "", s.Text ?? "", s.Number, s.Font, s.Colour);
    }

    private sealed class Dto
    {
        [JsonPropertyName("title")] public TitleDto? Title { get; set; }
        [JsonPropertyName("hover")] public HoverDto? Hover { get; set; }
        [JsonPropertyName("ewait")] public EWaitDto? EWait { get; set; }
        [JsonPropertyName("numin")] public NumInDto? NumIn { get; set; }
        [JsonPropertyName("shipwright")] public ShipDto? Ship { get; set; }
        [JsonPropertyName("sklup")] public SklupDto? Sklup { get; set; }
        [JsonPropertyName("trang")] public TrangDto? Trang { get; set; }
    }

    private sealed class SegmentDto
    {
        [JsonPropertyName("x")] public int? X { get; set; }
        [JsonPropertyName("y")] public int? Y { get; set; }
        [JsonPropertyName("label")] public string? Label { get; set; }
        [JsonPropertyName("text")] public string? Text { get; set; }
        [JsonPropertyName("number")] public bool Number { get; set; }
        [JsonPropertyName("font")] public int Font { get; set; }
        [JsonPropertyName("colour")] public int? Colour { get; set; }
    }

    private sealed class TrangDto
    {
        [JsonPropertyName("window")] public RectDto? Window { get; set; }
        [JsonPropertyName("back_colour")] public int BackColour { get; set; }
        [JsonPropertyName("text_colour")] public int TextColour { get; set; }
        [JsonPropertyName("line_step")] public int LineStep { get; set; }
        [JsonPropertyName("name")] public RectDto? Name { get; set; }
        [JsonPropertyName("notes")] public Dictionary<string, List<SegmentDto>>? Notes { get; set; }
        [JsonPropertyName("short_of_gold")] public SegmentDto? ShortOfGold { get; set; }
        [JsonPropertyName("title")] public string? Title { get; set; }
        [JsonPropertyName("title_text")] public string? TitleText { get; set; }
    }

    private sealed class LineDto
    {
        [JsonPropertyName("x")] public int X { get; set; }
        [JsonPropertyName("y")] public int Y { get; set; }
        [JsonPropertyName("colour")] public int Colour { get; set; }
        [JsonPropertyName("back")] public int Back { get; set; }
        [JsonPropertyName("label")] public string? Label { get; set; }
        [JsonPropertyName("text")] public string? Text { get; set; }
    }

    private sealed class SklupRowsDto
    {
        [JsonPropertyName("count")] public int Count { get; set; }
        [JsonPropertyName("top")] public int Top { get; set; }
        [JsonPropertyName("step")] public int Step { get; set; }
        [JsonPropertyName("key_x")] public int KeyX { get; set; }
        [JsonPropertyName("skill_x")] public int SkillX { get; set; }
        [JsonPropertyName("now_x")] public int NowX { get; set; }
        [JsonPropertyName("after_x")] public int AfterX { get; set; }
        [JsonPropertyName("digits")] public int Digits { get; set; }
        [JsonPropertyName("back")] public int Back { get; set; }
        [JsonPropertyName("button")] public RectDto? Button { get; set; }
    }

    private sealed class SklupDto
    {
        [JsonPropertyName("window")] public RectDto? Window { get; set; }
        [JsonPropertyName("under")] public RectDto? Under { get; set; }
        [JsonPropertyName("back_colour")] public int BackColour { get; set; }
        [JsonPropertyName("bar")] public RectDto? Bar { get; set; }
        [JsonPropertyName("title")] public LineDto? Title { get; set; }
        [JsonPropertyName("done")] public LineDto? Done { get; set; }
        [JsonPropertyName("header")] public LineDto? Header { get; set; }
        [JsonPropertyName("frame")] public RectDto? Frame { get; set; }
        [JsonPropertyName("rows")] public SklupRowsDto? Rows { get; set; }
        [JsonPropertyName("prompt")] public RectDto? Prompt { get; set; }
        [JsonPropertyName("trained")] public RectDto? Trained { get; set; }
        [JsonPropertyName("text_colour")] public int TextColour { get; set; }
        [JsonPropertyName("cell_colour")] public int CellColour { get; set; }
    }

    private sealed class RectDto
    {
        [JsonPropertyName("x")] public int X { get; set; }
        [JsonPropertyName("y")] public int Y { get; set; }
        [JsonPropertyName("width")] public int Width { get; set; }
        [JsonPropertyName("height")] public int Height { get; set; }
        [JsonPropertyName("colour")] public int Colour { get; set; }
        [JsonPropertyName("dark")] public int Dark { get; set; }
        [JsonPropertyName("light")] public int Light { get; set; }
    }

    private sealed class TitleDto
    {
        [JsonPropertyName("font")] public int Font { get; set; }
        [JsonPropertyName("colour")] public int Colour { get; set; }
        [JsonPropertyName("y")] public int Y { get; set; }
    }

    private sealed class HoverDto
    {
        [JsonPropertyName("icon_xor")] public int IconXor { get; set; }
        [JsonPropertyName("box_xor")] public int BoxXor { get; set; }
    }

    private sealed class EWaitDto
    {
        [JsonPropertyName("message")] public RectDto? Message { get; set; }
        [JsonPropertyName("line")] public RectDto? Line { get; set; }
        [JsonPropertyName("label")] public string? Label { get; set; }
        [JsonPropertyName("text")] public string? Text { get; set; }
        [JsonPropertyName("label_x")] public int LabelX { get; set; }
        [JsonPropertyName("label_y")] public int LabelY { get; set; }
        [JsonPropertyName("button")] public RectDto? Button { get; set; }
        [JsonPropertyName("key")] public string? Key { get; set; }
        [JsonPropertyName("band")] public RectDto? Band { get; set; }
        [JsonPropertyName("prompt_x")] public int PromptX { get; set; }
        [JsonPropertyName("letters")] public string? Letters { get; set; }
        [JsonPropertyName("letters_text")] public string? LettersText { get; set; }
        [JsonPropertyName("number")] public string? Number { get; set; }
        [JsonPropertyName("number_text")] public string? NumberText { get; set; }
    }

    private sealed class KeyDto
    {
        [JsonPropertyName("text")] public string? Text { get; set; }
        [JsonPropertyName("key")] public string? Key { get; set; }
        [JsonPropertyName("value")] public int Value { get; set; }
    }

    private sealed class NumInDto
    {
        [JsonPropertyName("window")] public int Window { get; set; }
        [JsonPropertyName("x")] public int X { get; set; }
        [JsonPropertyName("y")] public int Y { get; set; }
        [JsonPropertyName("step")] public int Step { get; set; }
        [JsonPropertyName("size")] public int Size { get; set; }
        [JsonPropertyName("digits")] public int Digits { get; set; }
        [JsonPropertyName("frame")] public RectDto? Frame { get; set; }
        [JsonPropertyName("clear")] public RectDto? Clear { get; set; }
        [JsonPropertyName("cell_colour")] public int CellColour { get; set; }
        [JsonPropertyName("text_colour")] public int TextColour { get; set; }
        [JsonPropertyName("back_colour")] public int BackColour { get; set; }
        [JsonPropertyName("keys")] public List<KeyDto>? Keys { get; set; }
        [JsonPropertyName("cursor")] public RectDto? Cursor { get; set; }
    }

    private sealed class AnswerDto
    {
        [JsonPropertyName("x")] public int X { get; set; }
        [JsonPropertyName("y")] public int Y { get; set; }
        [JsonPropertyName("yes")] public string? Yes { get; set; }
        [JsonPropertyName("no")] public string? No { get; set; }
    }

    private sealed class GoldDto
    {
        [JsonPropertyName("x")] public int X { get; set; }
        [JsonPropertyName("y")] public int Y { get; set; }
        [JsonPropertyName("label")] public string? Label { get; set; }
        [JsonPropertyName("label_text")] public string? LabelText { get; set; }
        [JsonPropertyName("digits")] public int Digits { get; set; }
        [JsonPropertyName("unit")] public string? Unit { get; set; }
        [JsonPropertyName("unit_text")] public string? UnitText { get; set; }
    }

    private sealed class ShipDto
    {
        [JsonPropertyName("window")] public RectDto? Window { get; set; }
        [JsonPropertyName("picture_height")] public int PictureHeight { get; set; }
        [JsonPropertyName("back_colour")] public int BackColour { get; set; }
        [JsonPropertyName("text_colour")] public int TextColour { get; set; }
        [JsonPropertyName("title")] public string? Title { get; set; }
        [JsonPropertyName("title_text")] public string? TitleText { get; set; }
        [JsonPropertyName("lines")] public List<RectDto>? Lines { get; set; }
        [JsonPropertyName("answer")] public AnswerDto? Answer { get; set; }
        [JsonPropertyName("gold")] public GoldDto? Gold { get; set; }
    }
}

public sealed partial class CommandPanelSet
{
    /// <summary>The title, hover look, dungeon wait bar, keypad, shipwright window (s151) and level-up skill window (s152); never null after <see cref="Load"/>.</summary>
    public PanelExtras Extras { get; init; } = new(null, null, null, null, null);
}
