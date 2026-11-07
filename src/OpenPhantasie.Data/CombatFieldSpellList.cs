using System.Text.Json.Serialization;

namespace OpenPhantasie.Data;

/// <summary>
/// Manifest <c>combat_field.spell_list</c>: I Amiga's Cast requesters (s133, Amiga <c>FORMAT.md</c> "The spell list"):
/// requester 12 "SPELLS AVAILABLE:" (h71 <c>+0x73a</c> → h23 <c>+0x570</c>) and requester 6 "ON WHOM:" (h62
/// <c>+0x508</c>) for HEALING I–IV. Screen pixels and the pens on screen (the struct pens through h77 <c>+0x112e</c>'s
/// table h69 <c>+0x19c</c>).
/// </summary>
public sealed record CombatFieldSpellList(CombatFieldList Spells, CombatFieldList Target);

/// <summary>
/// One list requester: its box (<see cref="FillPen"/>, two <see cref="BorderPen"/> borders at (0, 0) and (1, 1)), the title
/// (JAM1 in <see cref="TitlePen"/>), <see cref="Rows"/> row gadgets (<see cref="RowW"/> × <see cref="RowH"/>, from
/// (<see cref="RowX"/>, <see cref="RowY"/>), <see cref="RowStep"/> apart, text at +1, +1 in <see cref="RowPen"/> on
/// <see cref="RowBackPen"/>), the EXIT button; requester 12 also has the list's frame and fill and two scroll buttons.
/// </summary>
public sealed record CombatFieldList(Box Box, int FillPen, int BorderPen, int TitleX, int TitleY, int TitlePen, int RowX,
    int RowY, int RowStep, int RowW, int RowH, int Rows, int RowPen, int RowBackPen, Box Exit)
{
    /// <summary>The 1-px frame round the list (pen <see cref="ListFramePen"/>) and the list's fill (pen <see cref="ListFillPen"/>).</summary>
    public Box? ListFrame { get; init; }
    public int ListFramePen { get; init; }
    public Box? ListFill { get; init; }
    public int ListFillPen { get; init; }

    /// <summary>The EXIT button's two borders (one and two px outside it), fill, label and its place.</summary>
    public int ExitBorderPen { get; init; }
    public int ButtonFillPen { get; init; }
    public int ButtonTextPen { get; init; }
    public string ExitLabel { get; init; } = "";
    public int ExitTextDx { get; init; }
    public int ExitTextDy { get; init; }

    /// <summary>The scroll buttons (requester 12, gadgets 2 / 3), their borders' pen and glyphs (rows of '#', at +GlyphDx, +GlyphDy).</summary>
    public Box? ScrollUp { get; init; }
    public Box? ScrollDown { get; init; }
    public int ScrollBorderPen { get; init; }
    public IReadOnlyList<string> UpGlyph { get; init; } = [];
    public IReadOnlyList<string> DownGlyph { get; init; } = [];
    public int GlyphDx { get; init; }
    public int GlyphDy { get; init; }

    /// <summary>A disabled gadget's ghost (Intuition): pen-<see cref="GhostPen"/> dots every <see cref="GhostStep"/> px, the odd rows shifted half a step.</summary>
    public int GhostPen { get; init; }
    public int GhostStep { get; init; } = 4;

    /// <summary>
    /// The sound after each gadget event of this requester, or null (requester 12, s134: h23 <c>+0x734</c> calls h89
    /// <c>+0xd2</c> = the script h90 <c>+5</c> right after h86 <c>+0x132</c> returns: a live row, a scroll gadget that can
    /// move, EXIT; it blocks. "ON WHOM:" plays none).
    /// </summary>
    public int? Tone { get; init; }

    /// <summary>Row <paramref name="k"/>'s gadget box.</summary>
    public Box Row(int k) => new(RowX, RowY + k * RowStep, RowW, RowH);

    internal static CombatFieldList FromDto(Dto dto, Func<string, InvalidDataException> bad, string what)
    {
        static bool Pen(int p) => p is >= 0 and <= 15;
        if (Box.From(dto.Box) is not { } box || Box.From(dto.Exit) is not { } exit || dto.Title is not { Count: 2 } title
            || dto.Row is not { Count: 4 } row || dto.RowStep <= 0 || dto.Rows <= 0
            || !new[] { dto.FillPen, dto.BorderPen, dto.TitlePen, dto.RowPen, dto.RowBackPen, dto.ExitBorderPen,
                dto.ButtonFillPen, dto.ButtonTextPen, dto.ListFramePen, dto.ListFillPen, dto.ScrollBorderPen, dto.GhostPen }.All(Pen)
            || dto.GhostStep is <= 0 || dto.Tone is < 0)
            throw bad($"spell_list.{what}: box, title [x,y], row [x,y,w,h], row_step, rows, exit, pens 0-15 and tone >= 0");
        static IReadOnlyList<string> Glyph(List<string?>? g) => [.. (g ?? []).Select(r => r ?? "")];
        return new CombatFieldList(box, dto.FillPen, dto.BorderPen, title[0], title[1], dto.TitlePen, row[0], row[1],
            dto.RowStep, row[2], row[3], dto.Rows, dto.RowPen, dto.RowBackPen, exit)
        {
            ListFrame = Box.From(dto.ListFrame),
            ListFramePen = dto.ListFramePen,
            ListFill = Box.From(dto.ListFill),
            ListFillPen = dto.ListFillPen,
            ExitBorderPen = dto.ExitBorderPen,
            ButtonFillPen = dto.ButtonFillPen,
            ButtonTextPen = dto.ButtonTextPen,
            ExitLabel = dto.ExitLabel ?? "",
            ExitTextDx = dto.ExitText is { Count: 2 } et ? et[0] : 0,
            ExitTextDy = dto.ExitText is { Count: 2 } et2 ? et2[1] : 0,
            ScrollUp = Box.From(dto.ScrollUp),
            ScrollDown = Box.From(dto.ScrollDown),
            ScrollBorderPen = dto.ScrollBorderPen,
            UpGlyph = Glyph(dto.UpGlyph),
            DownGlyph = Glyph(dto.DownGlyph),
            GlyphDx = dto.GlyphAt is { Count: 2 } ga ? ga[0] : 0,
            GlyphDy = dto.GlyphAt is { Count: 2 } ga2 ? ga2[1] : 0,
            GhostPen = dto.GhostPen,
            GhostStep = dto.GhostStep ?? 4,
            Tone = dto.Tone,
        };
    }

    internal sealed class Dto
    {
        public List<int>? Box { get; set; }
        [JsonPropertyName("fill_pen")] public int FillPen { get; set; }
        [JsonPropertyName("border_pen")] public int BorderPen { get; set; }
        public List<int>? Title { get; set; }
        [JsonPropertyName("title_pen")] public int TitlePen { get; set; }
        public List<int>? Row { get; set; }
        [JsonPropertyName("row_step")] public int RowStep { get; set; }
        public int Rows { get; set; }
        [JsonPropertyName("row_pen")] public int RowPen { get; set; }
        [JsonPropertyName("row_back_pen")] public int RowBackPen { get; set; }
        [JsonPropertyName("list_frame")] public List<int>? ListFrame { get; set; }
        [JsonPropertyName("list_frame_pen")] public int ListFramePen { get; set; }
        [JsonPropertyName("list_fill")] public List<int>? ListFill { get; set; }
        [JsonPropertyName("list_fill_pen")] public int ListFillPen { get; set; }
        public List<int>? Exit { get; set; }
        [JsonPropertyName("exit_border_pen")] public int ExitBorderPen { get; set; }
        [JsonPropertyName("button_fill_pen")] public int ButtonFillPen { get; set; }
        [JsonPropertyName("button_text_pen")] public int ButtonTextPen { get; set; }
        [JsonPropertyName("exit_label")] public string? ExitLabel { get; set; }
        [JsonPropertyName("exit_text")] public List<int>? ExitText { get; set; }
        [JsonPropertyName("scroll_up")] public List<int>? ScrollUp { get; set; }
        [JsonPropertyName("scroll_down")] public List<int>? ScrollDown { get; set; }
        [JsonPropertyName("scroll_border_pen")] public int ScrollBorderPen { get; set; }
        [JsonPropertyName("up_glyph")] public List<string?>? UpGlyph { get; set; }
        [JsonPropertyName("down_glyph")] public List<string?>? DownGlyph { get; set; }
        [JsonPropertyName("glyph_at")] public List<int>? GlyphAt { get; set; }
        [JsonPropertyName("ghost_pen")] public int GhostPen { get; set; }
        [JsonPropertyName("ghost_step")] public int? GhostStep { get; set; }
        public int? Tone { get; set; }
    }
}

/// <summary>DTO of <see cref="CombatFieldSpellList"/>: the two requesters.</summary>
internal sealed class CombatFieldSpellListDto
{
    public CombatFieldList.Dto? Spells { get; set; }
    public CombatFieldList.Dto? Target { get; set; }

    internal CombatFieldSpellList Build(Func<string, InvalidDataException> bad) =>
        Spells is null || Target is null
            ? throw bad("spell_list: spells and target")
            : new CombatFieldSpellList(CombatFieldList.FromDto(Spells, bad, "spells"), CombatFieldList.FromDto(Target, bad, "target"));
}
