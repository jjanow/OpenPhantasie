using System.Text.Json.Serialization;

namespace OpenPhantasie.Data;

/// <summary>
/// Manifest <c>map_message</c> (s139, dungeon s140): requester 23 on the map screens. Phantasie I Amiga's wilderness
/// encounter (h74 <c>+0</c>, called only from the wilderness step h59 <c>+0x3be</c>) and its dungeon events (h41) open the
/// same timed requester as the fight's end (<see cref="CombatFieldMessage"/>: box, rows, centring, whole seconds, clock
/// wrap, keys swallowed) over the map, whose palette (<see cref="Palette"/>, the pack's <c>palettes</c> entry named by
/// <c>palette</c>; the overworld and the dungeon map share it) gives the box's pens. <see cref="Keys"/> are the string ids
/// whose lines open the box on the overworld (one line each), <see cref="DungeonKeys"/> those in the dungeon; <see cref="Splits"/> maps a string id
/// whose text the original shows on more than one row to the string ids of those rows (each formatted with the same
/// arguments). The box geometry and timing are <c>combat_field.message</c>'s; the font is <c>combat_field.font</c>.
/// </summary>
public sealed record MapMessageLayout(IReadOnlyList<string> Palette, CombatFieldMessage Message)
{
    /// <summary>The string ids that open the box on the overworld map, in no particular order.</summary>
    public IReadOnlyList<string> Keys { get; init; } = [];

    /// <summary>The string ids that open the box in the dungeon (s140, h41's events), in no particular order.</summary>
    public IReadOnlyList<string> DungeonKeys { get; init; } = [];

    /// <summary>The keys of the overworld (<paramref name="dungeon"/> false) or of the dungeon.</summary>
    public IReadOnlyList<string> KeysFor(bool dungeon) => dungeon ? DungeonKeys : Keys;

    /// <summary>A string id → the string ids of its rows, when the original's box holds it on several rows.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> Splits { get; init; } =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);

    /// <summary>
    /// s141: a string id → the sound (the pack's <c>sound_effects</c> number) the original plays just before its box (I Amiga
    /// h41: the wall's default beep, 29 before "%s FAILS", 28 / 16 / 26 before ops 3 / 12 / 14's boxes). The player blocks
    /// (h89 <c>+0x304</c>), so the box opens when the sound ends.
    /// </summary>
    public IReadOnlyDictionary<string, int> Sounds { get; init; } = new Dictionary<string, int>(StringComparer.Ordinal);

    /// <summary>
    /// s157: a key → the string id of the text the original's box prints where it differs from Core's (DOS's) text
    /// (<c>texts</c>; I Amiga: `p1field.box.you_surprise_them` with no blank before "!", "THAT IS A WALL"), formatted with the same
    /// arguments. Core's text still finds the key (and stays Core's on the message line and in the log); only the box's
    /// rows change. A key with a <see cref="Splits"/> entry cannot have one.
    /// </summary>
    public IReadOnlyDictionary<string, string> Texts { get; init; } = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>
    /// s144: requester 21 on the dungeon map (<c>window</c>): the MESS text window (timed) and the damage report (OK),
    /// or null when the pack has none (the remaster's text panel and message line stand in).
    /// </summary>
    public MapMessageWindow? Window { get; init; }

    /// <summary>The font of the box's text (the combat screen's z6font), or null for the pack's own.</summary>
    public string? Font { get; init; }

    /// <summary>The <see cref="Font"/>'s file, or null.</summary>
    public string? FontFile { get; init; }

    /// <summary>
    /// s155: the map screen's menu bar (<c>menu_bar</c>, I Amiga h80 <c>+0xb48</c>: " Info" / " Options   Speed" at rest,
    /// the strip h81 <c>+0x270</c> → <c>+0x33c</c> → <c>+0x2c0</c>), over the overworld and the dungeon; null = none.
    /// </summary>
    public CombatFieldMenuBar? MenuBar { get; init; }

    /// <summary>s155: the map screen's message line (<c>line</c>, h41 <c>+0x10c6</c> → h80 <c>+0x84a</c>); null = none.</summary>
    public MapMessageLine? Line { get; init; }

    /// <summary>s156: the overworld's frame (<c>overworld_frame</c>, h56 <c>+0x8b8</c> → h77 <c>+0x6dc</c>); null = none.</summary>
    public MapFrame? OverworldFrame { get; init; }

    /// <summary>s158: the dungeon screen's frame (<c>dungeon_frame</c>, h56 <c>+0xe7c</c> → h77 <c>+0x6dc</c>); null = none.</summary>
    public MapFrame? DungeonFrame { get; init; }

    internal static MapMessageLayout FromDto(Dto dto, IReadOnlyDictionary<string, List<string>>? palettes,
        CombatFieldLayout? combatField, string manifestPath)
    {
        InvalidDataException Bad(string what) => new($"{manifestPath}: map_message: {what}.");
        if (combatField?.Message is not { } message)
            throw Bad("needs combat_field.message (the requester's box and timing)");
        if (string.IsNullOrWhiteSpace(dto.Palette) || palettes?.GetValueOrDefault(dto.Palette) is not { Count: 16 } palette)
            throw Bad("palette must name a 16-colour entry of palettes");
        if (dto.Keys is not { Count: > 0 } keys || keys.Any(string.IsNullOrWhiteSpace))
            throw Bad("keys must list string ids");
        var dungeonKeys = dto.DungeonKeys ?? [];
        if (dungeonKeys.Any(string.IsNullOrWhiteSpace))
            throw Bad("dungeon_keys must list string ids");
        var splits = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        foreach (var (key, rows) in dto.Splits ?? [])
        {
            if (!(keys.Contains(key) || dungeonKeys.Contains(key)) || rows is not { Count: >= 2 } || rows.Any(string.IsNullOrWhiteSpace) ||
                rows.Count > message.RowsY.Count)
                throw Bad($"splits: '{key}' must be one of keys or dungeon_keys with 2 … {message.RowsY.Count} row ids");
            splits[key] = [.. rows];
        }
        var sounds = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var (key, sound) in dto.Sounds ?? [])
        {
            if (!(keys.Contains(key) || dungeonKeys.Contains(key)) || sound < 0)
                throw Bad($"sounds: '{key}' must be one of keys or dungeon_keys with a sound number ≥ 0");
            sounds[key] = sound;
        }
        var texts = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (key, id) in dto.Texts ?? [])
        {
            if (!(keys.Contains(key) || dungeonKeys.Contains(key)) || string.IsNullOrWhiteSpace(id) || splits.ContainsKey(key))
                throw Bad($"texts: '{key}' must be one of keys or dungeon_keys (not split) with a string id");
            texts[key] = id;
        }
        MapFrame Frame(FrameDto f, string what) => Box.From(f.Box) is { } fb && f.Thickness > 0 && f.Pen is >= 0 and <= 15
            ? new MapFrame(fb, f.Thickness, f.Pen) : throw Bad($"{what}: box [x, y, w, h], thickness > 0, pen 0-15");
        return new MapMessageLayout([.. palette], message)
        {
            Texts = texts,
            Window = dto.Window is { } w ? MapMessageWindow.FromDto(w, Bad) is var window && window.TitleFont is { } titleFont
                ? window with { TitleFontFile = CombatFieldLayout.FontFileFor(titleFont, manifestPath) } : window : null,
            Sounds = sounds,
            Keys = [.. keys],
            DungeonKeys = [.. dungeonKeys],
            Splits = splits,
            Font = combatField.Font,
            FontFile = combatField.FontFile,
            MenuBar = dto.MenuBar is { } mb ? CombatFieldMenuBar.FromDto(mb, Bad) with
            {
                Font = string.IsNullOrWhiteSpace(mb.Font) ? null : mb.Font,
                FontFile = CombatFieldLayout.FontFileFor(mb.Font, manifestPath),
            } : null,
            Line = dto.Line is { } line ? MapMessageLine.FromDto(line, Bad) : null,
            OverworldFrame = dto.OverworldFrame is { } f ? Frame(f, "overworld_frame") : null,
            DungeonFrame = dto.DungeonFrame is { } df ? Frame(df, "dungeon_frame") : null,
        };
    }

    internal sealed class Dto
    {
        public string? Palette { get; set; }
        public List<string>? Keys { get; set; }
        [JsonPropertyName("dungeon_keys")] public List<string>? DungeonKeys { get; set; }
        public Dictionary<string, List<string>>? Splits { get; set; }
        public Dictionary<string, int>? Sounds { get; set; }
        public Dictionary<string, string>? Texts { get; set; }
        public MapMessageWindow.Dto? Window { get; set; }
        [JsonPropertyName("menu_bar")] public CombatFieldMenuBar.Dto? MenuBar { get; set; }
        public MapMessageLine.Dto? Line { get; set; }
        [JsonPropertyName("overworld_frame")] public FrameDto? OverworldFrame { get; set; }
        [JsonPropertyName("dungeon_frame")] public FrameDto? DungeonFrame { get; set; }
    }

    internal sealed class FrameDto
    {
        public List<int>? Box { get; set; }
        public int Thickness { get; set; }
        public int Pen { get; set; }
    }
}

/// <summary>
/// s156: a frame of <see cref="Thickness"/> nested one-pixel outlines in <see cref="Pen"/>, the outermost covering
/// <see cref="Box"/> (I Amiga h77 <c>+0x6dc(x, y, w, h, pen)</c>: one PolyDraw of three rectangles, (x, y)–(x + w, y + h + 2),
/// then one pixel in, then two; the overworld's (1, 12, 317, 183, 13) covers x 1–318, y 12–197).
/// </summary>
public sealed record MapFrame(Box Box, int Thickness, int Pen);

/// <summary>
/// s155: I Amiga's map message line (h41 <c>+0x10c6</c> → h80 <c>+0x84a(text, 3, 192)</c>, static `[C]`): the JAM2
/// IntuiText h81 <c>+0x5e</c> (BackPen 0, the window's z6font) first prints a blank line of <see cref="ClearCells"/>
/// blanks from <see cref="ClearX"/> at <see cref="TopEdge"/>, then the text centred on <see cref="CentreWidth"/> by its
/// IntuiTextLength (h80 <c>+0x892</c>), in pen <see cref="Pen"/> of the map palette. The text uses the window's font
/// (<see cref="MapMessageLayout.Font"/>).
/// </summary>
public sealed record MapMessageLine(int TopEdge, int Pen, int ClearX, int ClearCells, int CentreWidth)
{
    /// <summary>s156: a MESS record's stored cells (40): h41 <c>+0x10e6</c> copies the record as stored, the line centres all of it.</summary>
    public int RecordCells { get; init; }

    internal static MapMessageLine FromDto(Dto dto, Func<string, InvalidDataException> bad)
    {
        if (dto.TopEdge is not { } top || dto.Pen is not { } pen || pen is < 0 or > 15 || dto.ClearCells <= 0 || dto.CentreWidth <= 0)
            throw bad("line: top_edge, pen 0-15, clear_cells and centre_width");
        return new MapMessageLine(top, pen, dto.ClearX, dto.ClearCells, dto.CentreWidth) { RecordCells = dto.RecordCells };
    }

    internal sealed class Dto
    {
        [JsonPropertyName("top_edge")] public int? TopEdge { get; set; }
        public int? Pen { get; set; }
        [JsonPropertyName("clear_x")] public int ClearX { get; set; }
        [JsonPropertyName("clear_cells")] public int ClearCells { get; set; }
        [JsonPropertyName("centre_width")] public int CentreWidth { get; set; }
        [JsonPropertyName("record_cells")] public int RecordCells { get; set; }
    }
}

/// <summary>
/// Manifest <c>map_message.window</c> (s144): Phantasie I Amiga's requester 21 (h84 <c>+0x48f6</c>) over the dungeon map
/// (Amiga <c>FORMAT.md</c> "Requester 21 on the map"). As stored it is the damage report's window (h71 <c>+0x112e</c>, shown
/// by h86 <c>+0(0x15)</c>): <see cref="Box"/> with two <see cref="BorderPen"/> borders, the title (ReqText, topaz 8,
/// <see cref="TitlePen"/>) at <see cref="TitleX"/> / <see cref="TitleY"/>, up to <see cref="Rows"/> member rows (gadgets 11–17,
/// z6font) from <see cref="RowY"/>, and the OK button (<see cref="Ok"/>); it waits for a click on a gadget. A MESS text with
/// no menu (h41 <c>+0x11e4</c>) takes the same requester with no border, title or button, moved to <see cref="TextTop"/>
/// and <see cref="TextHeightBase"/> + <see cref="RowStep"/>·n high, for ⌊<see cref="TextTicksPerRow"/>·(n +
/// <see cref="TextExtraRows"/>) / 60⌋ s (h80 <c>+0x546(0x15, (n + 1)·120)</c>, the 8-s clock cut included); keys are swallowed.
/// </summary>
public sealed record MapMessageWindow(Box Box, int FillPen, int BorderPen, int RowX, int RowY, int RowStep, int RowPen, int Rows)
{
    public int TitleX { get; init; }
    public int TitleY { get; init; }
    public int TitlePen { get; init; }

    /// <summary>The title's font id (topaz 8: ITexts 123 / 184 / 194 name TextAttr h78 <c>+0x22</c>), or null for the rows' font.</summary>
    public string? TitleFont { get; init; }

    /// <summary>s154: the <see cref="TitleFont"/>'s file next to the manifest, or null (before s154 the title fell back to the
    /// remaster's 5×7: the font id alone does not load a pack font).</summary>
    public string? TitleFontFile { get; init; }

    public Box Ok { get; init; }
    public int OkFillPen { get; init; }
    public int OkBorderPen { get; init; }
    public int OkTextX { get; init; }
    public int OkTextY { get; init; }
    public int OkTextPen { get; init; }

    /// <summary>The OK button's text (a UI string id: h72 <c>+0x179</c> "   O K").</summary>
    public string OkLabel { get; init; } = "";

    /// <summary>The MESS text window's top (h41 <c>+0x1380</c>: Top := 22).</summary>
    public int TextTop { get; init; }

    /// <summary>The MESS text window's height less a row step per line (h41 <c>+0x1374</c>: 8·n + 33).</summary>
    public int TextHeightBase { get; init; }

    /// <summary>h41 <c>+0x13e8</c>: the wait is (n + <see cref="TextExtraRows"/>)·this ticks.</summary>
    public int TextTicksPerRow { get; init; }

    public int TextExtraRows { get; init; }

    /// <summary>
    /// s153: a row gadget relative to its text (<see cref="TextRowY"/>, <see cref="RowX"/>): gadgets 11–17 at (7, 25 + 8k),
    /// 241 × 7, their IText at (1, 1), so (−1, −1, 241, 7). A click on one is a GADGETUP (RELVERIFY) that ends the timed MESS
    /// window early. Null: not given.
    /// </summary>
    public Box? RowGadget { get; init; }

    /// <summary>s153: row <paramref name="k"/>'s gadget in the MESS window (screen pixels), or null.</summary>
    public Box? TextRowGadget(int k) =>
        RowGadget is { } g ? new Box(RowX + g.X, TextRowY(k) + g.Y, g.W, g.H) : null;

    /// <summary>s153: requester 25, the event menu's buttons under the MESS window (h41 <c>+0x1440</c> / <c>+0x1f6</c>); null: not built.</summary>
    public MapMessageMenu? Menu { get; init; }

    /// <summary>
    /// s154: a report title as Core logs it (a Core string id, DOS's text) → the UI string id of the title the original
    /// prints in its place (I Amiga: h72 <c>+0x131</c> "  SWIMMING:", h42 <c>+0x1a2</c> "   REPORT:", h72 <c>+0xdf</c>
    /// "  IN THE INN", their leading blanks kept: the title IText's LeftEdge is fixed). A title not listed is drawn as logged.
    /// </summary>
    public IReadOnlyDictionary<string, string> Titles { get; init; } = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>
    /// s156: a report row as Core logs it (a Core string id whose text is <c>"{0,-N}…"</c>: a name padded to N, then the
    /// rest) → the UI string id of the row the original prints (I Amiga's pool: h72 <c>+0xb4</c> "%-10.9s IS CHANGED", a
    /// blank more than DOS's). A row not listed is drawn as logged.
    /// </summary>
    public IReadOnlyDictionary<string, string> RowTexts { get; init; } = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>
    /// s154: the map's Health report (h50 <c>+0x420…+0x54a</c>): its title and row format (UI string ids), or null when the
    /// pack does not draw it as this window.
    /// </summary>
    public string? HealthTitle { get; init; }

    /// <summary>s154: the map Health report's row (a UI string id: name, status, HP, HP max, MP, MP max).</summary>
    public string? HealthRow { get; init; }

    /// <summary>
    /// s154: requester 21's Top for Party Options › Party health in a fight (h44 <c>+0x1478</c>). s158 live: 45, the map's
    /// (h71 <c>+0x106</c>'s Top 23 holds only while the orders requester is up, when the menu's picks do nothing; <c>+0x2a4</c>
    /// puts 45 back). Null: not given.
    /// </summary>
    public int? FightTop { get; init; }

    /// <summary>s154: this window moved to <paramref name="top"/> (the title, rows and OK keep their offsets).</summary>
    public MapMessageWindow MovedTo(int top)
    {
        var dy = top - Box.Y;
        return this with
        {
            Box = Box with { Y = top }, RowY = RowY + dy, TitleY = TitleY + dy, Ok = Ok with { Y = Ok.Y + dy },
            OkTextY = OkTextY + dy,
        };
    }

    /// <summary>The MESS text window of <paramref name="rows"/> lines (no border).</summary>
    public Box TextBox(int rows) => new(Box.X, TextTop, Box.W, TextHeightBase + RowStep * Math.Max(1, rows));

    /// <summary>Row <paramref name="k"/>'s text y in the MESS window (the gadgets keep their offset from the top).</summary>
    public int TextRowY(int k) => TextTop + (RowY - Box.Y) + RowStep * k;

    /// <summary>
    /// The MESS window's full wait in seconds for <paramref name="rows"/> lines: ⌊ticks / <paramref name="ticksPerSecond"/>⌋
    /// (h80 <c>+0x546</c> waits whole seconds; no Speed polls are added here).
    /// </summary>
    public double TextSeconds(int rows, double ticksPerSecond) =>
        ticksPerSecond <= 0 ? 0 : Math.Floor(TextTicksPerRow * (double)(rows + TextExtraRows) / ticksPerSecond);

    /// <summary>
    /// The report's window for <paramref name="rows"/> member lines: <see cref="Box"/>, grown by a row step per line past
    /// <see cref="Rows"/> (the Amiga has seven rows; the remaster never drops a line).
    /// </summary>
    public Box ReportBox(int rows) => Box with { H = Box.H + RowStep * Math.Max(0, rows - Rows) };

    /// <summary>The OK button under <paramref name="rows"/> member lines (it keeps its place from the window's bottom).</summary>
    public Box OkFor(int rows) => Ok with { Y = Ok.Y + RowStep * Math.Max(0, rows - Rows) };

    internal static MapMessageWindow FromDto(Dto dto, Func<string, InvalidDataException> bad)
    {
        if (Box.From(dto.Box) is not { } box || Box.From(dto.Ok) is not { } ok || dto.RowStep <= 0 || dto.Rows <= 0
            || dto.Title is not { Count: 2 } title || dto.OkText is not { Count: 2 } okText || string.IsNullOrWhiteSpace(dto.OkLabel)
            || dto.TextHeightBase <= 0 || dto.TextTicksPerRow <= 0 || dto.TextExtraRows < 0)
            throw bad("window: box, ok, ok_text, ok_label, title, row_step, rows, text_height_base, text_ticks_per_row and text_extra_rows");
        return new MapMessageWindow(box, dto.FillPen, dto.BorderPen, dto.RowX, dto.RowY, dto.RowStep, dto.RowPen, dto.Rows)
        {
            TitleX = title[0],
            TitleY = title[1],
            TitlePen = dto.TitlePen,
            TitleFont = dto.TitleFont,
            Ok = ok,
            OkFillPen = dto.OkFillPen,
            OkBorderPen = dto.OkBorderPen,
            OkTextX = okText[0],
            OkTextY = okText[1],
            OkTextPen = dto.OkTextPen,
            OkLabel = dto.OkLabel!,
            TextTop = dto.TextTop,
            TextHeightBase = dto.TextHeightBase,
            TextTicksPerRow = dto.TextTicksPerRow,
            TextExtraRows = dto.TextExtraRows,
            RowGadget = dto.RowGadget is null ? null
                : Box.From(dto.RowGadget) ?? throw bad("window: row_gadget is [x, y, w, h]"),
            Menu = dto.Menu is null ? null : MapMessageMenu.FromDto(dto.Menu, bad),
            Titles = (dto.Titles ?? []).Any(t => string.IsNullOrWhiteSpace(t.Key) || string.IsNullOrWhiteSpace(t.Value))
                ? throw bad("window: titles maps Core string ids to UI string ids")
                : new Dictionary<string, string>(dto.Titles ?? [], StringComparer.Ordinal),
            RowTexts = new Dictionary<string, string>(dto.RowTexts ?? [], StringComparer.Ordinal),
            HealthTitle = dto.HealthTitle,
            HealthRow = dto.HealthRow,
            FightTop = dto.FightTop,
        };
    }

    internal sealed class Dto
    {
        public List<int>? Box { get; set; }
        [JsonPropertyName("fill_pen")] public int FillPen { get; set; }
        [JsonPropertyName("border_pen")] public int BorderPen { get; set; }
        [JsonPropertyName("row_x")] public int RowX { get; set; }
        [JsonPropertyName("row_y")] public int RowY { get; set; }
        [JsonPropertyName("row_step")] public int RowStep { get; set; }
        [JsonPropertyName("row_pen")] public int RowPen { get; set; }
        public int Rows { get; set; }
        public List<int>? Title { get; set; }
        [JsonPropertyName("title_pen")] public int TitlePen { get; set; }
        [JsonPropertyName("title_font")] public string? TitleFont { get; set; }
        public List<int>? Ok { get; set; }
        [JsonPropertyName("ok_fill_pen")] public int OkFillPen { get; set; }
        [JsonPropertyName("ok_border_pen")] public int OkBorderPen { get; set; }
        [JsonPropertyName("ok_text")] public List<int>? OkText { get; set; }
        [JsonPropertyName("ok_text_pen")] public int OkTextPen { get; set; }
        [JsonPropertyName("ok_label")] public string? OkLabel { get; set; }
        [JsonPropertyName("text_top")] public int TextTop { get; set; }
        [JsonPropertyName("text_height_base")] public int TextHeightBase { get; set; }
        [JsonPropertyName("text_ticks_per_row")] public int TextTicksPerRow { get; set; }
        [JsonPropertyName("text_extra_rows")] public int TextExtraRows { get; set; }
        [JsonPropertyName("row_gadget")] public List<int>? RowGadget { get; set; }
        public MapMessageMenu.Dto? Menu { get; set; }
        public Dictionary<string, string>? Titles { get; set; }
        [JsonPropertyName("row_texts")] public Dictionary<string, string>? RowTexts { get; set; }
        [JsonPropertyName("health_title")] public string? HealthTitle { get; set; }
        [JsonPropertyName("health_row")] public string? HealthRow { get; set; }
        [JsonPropertyName("fight_top")] public int? FightTop { get; set; }
    }
}

/// <summary>
/// Manifest <c>map_message.window.menu</c> (s153, static): Phantasie I Amiga's requester 25 (h84 <c>+0x4ab6</c>), the event
/// menu's buttons. h41 <c>+0x11e4</c> with a button count in 2…20 (a MESS menu) <c>Request</c>s requester 21 as the MESS text
/// window (no wait) and then <c>+0x1f6</c> sets requester 25's Top to the text window's bottom (h43 <c>+0</c> = 8n + 55),
/// its Height to <see cref="RowHeight"/>·⌈k / <see cref="PerRow"/>⌉ + <see cref="HeightBase"/>, clears its ReqBorder, cuts
/// its gadget list after button k and shows it through h86 <c>+0(0x19)</c>, which waits for a GADGETUP and returns the
/// gadget id (the answer = id − 10). Left <see cref="Left"/>, Width <see cref="Width"/>, backfill <see cref="FillPen"/>; the
/// buttons are gadgets 93–102 at <see cref="Buttons"/> (relative, stored, the tenth alone in the middle of a fourth row),
/// <see cref="ButtonSize"/> each, a <see cref="ButtonFillPen"/> face (Border 70), two <see cref="ButtonBorderPen"/> borders
/// (68 / 69 at (−2, −2) and (−1, −1)), the label (IText 202 + i, <see cref="LabelPen"/>, z6font) at <see cref="Label"/>.
/// </summary>
public sealed record MapMessageMenu(int Left, int Width, int FillPen, int RowHeight, int HeightBase, int PerRow)
{
    public IReadOnlyList<(int X, int Y)> Buttons { get; init; } = [];
    public (int W, int H) ButtonSize { get; init; }
    public int ButtonFillPen { get; init; }
    public int ButtonBorderPen { get; init; }
    public (int X, int Y) Label { get; init; }
    public int LabelPen { get; init; }

    /// <summary>The requester for <paramref name="buttons"/> buttons under a MESS window whose bottom is <paramref name="top"/>.</summary>
    public Box BoxFor(int top, int buttons) =>
        new(Left, top, Width, RowHeight * ((Math.Max(1, buttons) + PerRow - 1) / PerRow) + HeightBase);

    /// <summary>Button <paramref name="i"/> on the screen in a requester at <paramref name="top"/>; null past the stored gadgets.</summary>
    public Box? Button(int top, int i) =>
        (uint)i < (uint)Buttons.Count ? new Box(Left + Buttons[i].X, top + Buttons[i].Y, ButtonSize.W, ButtonSize.H) : null;

    internal static MapMessageMenu FromDto(Dto dto, Func<string, InvalidDataException> bad)
    {
        if (dto.Buttons is not { Count: > 0 } buttons || buttons.Any(b => b is not { Count: 2 })
            || dto.ButtonSize is not { Count: 2 } size || dto.Label is not { Count: 2 } label
            || dto.Width <= 0 || dto.RowHeight <= 0 || dto.PerRow <= 0 || dto.HeightBase < 0)
            throw bad("window.menu: left, width, row_height, height_base, per_row, buttons ([x, y] each), button_size and label");
        return new MapMessageMenu(dto.Left, dto.Width, dto.FillPen, dto.RowHeight, dto.HeightBase, dto.PerRow)
        {
            Buttons = [.. buttons.Select(b => (b[0], b[1]))],
            ButtonSize = (size[0], size[1]),
            ButtonFillPen = dto.ButtonFillPen,
            ButtonBorderPen = dto.ButtonBorderPen,
            Label = (label[0], label[1]),
            LabelPen = dto.LabelPen,
        };
    }

    internal sealed class Dto
    {
        public int Left { get; set; }
        public int Width { get; set; }
        [JsonPropertyName("fill_pen")] public int FillPen { get; set; }
        [JsonPropertyName("row_height")] public int RowHeight { get; set; }
        [JsonPropertyName("height_base")] public int HeightBase { get; set; }
        [JsonPropertyName("per_row")] public int PerRow { get; set; }
        public List<List<int>>? Buttons { get; set; }
        [JsonPropertyName("button_size")] public List<int>? ButtonSize { get; set; }
        [JsonPropertyName("button_fill_pen")] public int ButtonFillPen { get; set; }
        [JsonPropertyName("button_border_pen")] public int ButtonBorderPen { get; set; }
        public List<int>? Label { get; set; }
        [JsonPropertyName("label_pen")] public int LabelPen { get; set; }
    }
}
