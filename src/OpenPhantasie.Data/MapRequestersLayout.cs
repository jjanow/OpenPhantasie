using System.Text.Json.Serialization;

namespace OpenPhantasie.Data;

/// <summary>
/// Manifest <c>map_requesters</c> (s154, static, Amiga <c>FORMAT.md</c> "Requesters 0, 22 and 14"): Phantasie I Amiga's
/// Intuition requesters the remaster draws behind AMIGA SCREENS. <see cref="Menu"/> is requester 0, the one every
/// <c>h86 +0x32a</c> question uses ("A TOWN.  ENTER?", the HEARS / SLEEPING MONSTERS menus, "THE RIVER STYX" …);
/// <see cref="BlackKnights"/> requester 22; <see cref="Sheet"/> requester 14, the character sheet. Pens are screen pens of
/// the named palette, already put through the game's run-time remap (h77 <c>+0x112e</c>, table h69 <c>+0x19c</c>) where it
/// applies (requesters 0–19, Borders 0–52, ITexts 0–171, off in the town: h77 <c>+0x1230</c>).
/// </summary>
public sealed record MapRequestersLayout(IReadOnlyList<string> Palette)
{
    /// <summary>The rows' and buttons' font (z6font) and its file.</summary>
    public string? Font { get; init; }
    public string? FontFile { get; init; }

    /// <summary>The titles' font (topaz 8: ITexts 123 / 194) and its file.</summary>
    public string? TitleFont { get; init; }
    public string? TitleFontFile { get; init; }

    /// <summary>Requester 0 as <c>h86 +0x32a</c> fills it, or null.</summary>
    public MenuRequester? Menu { get; init; }

    /// <summary>
    /// A remaster prompt's DOS menu id (<c>SessionPrompt.MenuId</c>) → the UI string id of the Amiga's h86 text
    /// (<c>"lines|…[button|…]"</c>, <c>{0}</c> = the listener's name for HEARS).
    /// </summary>
    public IReadOnlyDictionary<int, string> Menus { get; init; } = new Dictionary<int, string>();

    /// <summary>s156: a prompt with no DOS menu id (its <c>SessionPromptKind</c> name, e.g. DungeonTrap) → its h86 text's string id.</summary>
    public IReadOnlyDictionary<string, string> Kinds { get; init; } = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>s156: requester 27, Info › About Phantasie (the logo picture in a frame), or null.</summary>
    public PictureRequester? About { get; init; }

    /// <summary>s156: the town's questions (guild_add_move, training_cost) → their h86 text's string id.</summary>
    public IReadOnlyDictionary<string, string> Town { get; init; } = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>A Core line (string id) that the Amiga shows as an OK requester (requester 0) → its h86 text's string id.</summary>
    public IReadOnlyDictionary<string, string> Oks { get; init; } = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>Requester 22 and the DOS menu id it stands for (Black Knights, menu <c>0xf</c>), or null.</summary>
    public FixedRequester? BlackKnights { get; init; }
    public int BlackKnightsMenu { get; init; }

    /// <summary>Requester 14 (the character sheet in the town / Guild), or null.</summary>
    public SheetRequester? Sheet { get; init; }

    /// <summary>
    /// s156: h69 <c>+0x19c</c>, stored pen → screen pen (h77 <c>+0x112e</c>, run after the town): the sheet's stored pens go
    /// through it over the map (h50 <c>+0x7b2</c>), the menu's screen pens through its inverse in the town. Empty: none.
    /// </summary>
    public IReadOnlyList<int> PenRemap { get; init; } = [];

    /// <summary>s156: the town's palette (h69 <c>+0x54</c>), for the requesters the town shows with their stored pens.</summary>
    public IReadOnlyList<string>? TownPalette { get; init; }

    /// <summary>
    /// s165: requester 19 (h84 <c>+0x3fc6 + 19 · 0x70</c>), a scroll's text in the town's Use item (h29 <c>+0x2c4</c>: the
    /// 20 stored rows into ITexts 0x98–0xab, then <c>h86 +0(0x13)</c>), with its stored pens on <see cref="TownPalette"/>;
    /// its one gadget is O.K. (IText 8 → slot 9). Null: the remaster's text panel.
    /// </summary>
    public SheetPage? ScrollText { get; init; }

    /// <summary>The inverse of <see cref="PenRemap"/> (h69 <c>+0x1ac</c>, h77 <c>+0x1230</c>): screen pen → stored pen.</summary>
    public IReadOnlyList<int> PenUnmap
    {
        get
        {
            var inv = new int[PenRemap.Count];
            for (var i = 0; i < PenRemap.Count; i++)
                inv[PenRemap[i]] = i;
            return inv;
        }
    }

    internal static MapRequestersLayout FromDto(Dto dto, IReadOnlyDictionary<string, List<string>>? palettes, string manifestPath)
    {
        InvalidDataException Bad(string what) => new($"{manifestPath}: map_requesters: {what}.");
        IReadOnlyList<string> PaletteOf(string? name) =>
            !string.IsNullOrWhiteSpace(name) && palettes?.GetValueOrDefault(name) is { Count: 16 } p
                ? [.. p] : throw Bad($"palette '{name}' must name a 16-colour entry of palettes");
        var menus = new Dictionary<int, string>();
        foreach (var (k, v) in dto.Menus ?? [])
        {
            var id = k.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                ? Convert.ToInt32(k[2..], 16) : int.Parse(k, System.Globalization.CultureInfo.InvariantCulture);
            menus[id] = string.IsNullOrWhiteSpace(v) ? throw Bad("menus: a string id per menu id") : v;
        }
        return new MapRequestersLayout(PaletteOf(dto.Palette))
        {
            Font = dto.Font,
            FontFile = CombatFieldLayout.FontFileFor(dto.Font, manifestPath),
            TitleFont = dto.TitleFont,
            TitleFontFile = CombatFieldLayout.FontFileFor(dto.TitleFont, manifestPath),
            Menu = dto.Menu is { } m ? MenuRequester.FromDto(m, Bad) : null,
            Menus = menus,
            Oks = new Dictionary<string, string>(dto.Oks ?? [], StringComparer.Ordinal),
            Kinds = new Dictionary<string, string>(dto.Kinds ?? [], StringComparer.Ordinal),
            Town = new Dictionary<string, string>(dto.Town ?? [], StringComparer.Ordinal),
            About = dto.About is { } about ? PictureRequester.FromDto(about, Bad) : null,
            BlackKnights = dto.BlackKnights is { } bk ? FixedRequester.FromDto(bk, Bad) : null,
            BlackKnightsMenu = dto.BlackKnightsMenu,
            Sheet = dto.Sheet is { } s ? SheetRequester.FromDto(s, PaletteOf(s.Palette), Bad) : null,
            PenRemap = dto.PenRemap is null ? []
                : dto.PenRemap.Count == 16 && dto.PenRemap.Order().SequenceEqual(Enumerable.Range(0, 16)) ? [.. dto.PenRemap]
                : throw Bad("pen_remap must be a permutation of the 16 pens"),
            TownPalette = dto.TownPalette is null ? null : PaletteOf(dto.TownPalette),
            ScrollText = dto.ScrollText is { } scroll ? SheetPage.FromDto(scroll, Bad) : null,
        };
    }

    internal sealed class Dto
    {
        public string? Palette { get; set; }
        public string? Font { get; set; }
        [JsonPropertyName("title_font")] public string? TitleFont { get; set; }
        public MenuRequester.Dto? Menu { get; set; }
        public Dictionary<string, string>? Menus { get; set; }
        public Dictionary<string, string>? Oks { get; set; }
        public Dictionary<string, string>? Kinds { get; set; }
        public Dictionary<string, string>? Town { get; set; }
        public PictureRequester.Dto? About { get; set; }
        [JsonPropertyName("black_knights")] public FixedRequester.Dto? BlackKnights { get; set; }
        [JsonPropertyName("black_knights_menu")] public int BlackKnightsMenu { get; set; }
        public SheetRequester.Dto? Sheet { get; set; }
        [JsonPropertyName("pen_remap")] public List<int>? PenRemap { get; set; }
        [JsonPropertyName("town_palette")] public string? TownPalette { get; set; }
        [JsonPropertyName("scroll_text")] public SheetPage.Dto? ScrollText { get; set; }
    }
}

/// <summary>An outline (a 5-point Border) relative to its requester.</summary>
public readonly record struct RequesterOutline(Box Rect, int Pen);

/// <summary>
/// A boolean gadget with the game's button render (Borders 0 / 1 / 4: two outlines at (−2, −2) / (−1, −1) in
/// <see cref="BorderPen"/>, the face's 18-point fill in <see cref="FacePen"/>) and its IText at <see cref="TextX"/> /
/// <see cref="TextY"/> (JAM1, <see cref="TextPen"/>), relative to the requester; <see cref="Label"/> is a UI string id.
/// </summary>
public sealed record RequesterButton(Box Rect, string Label, int TextX, int TextY)
{
    public int FacePen { get; init; }
    public int BorderPen { get; init; }
    public int TextPen { get; init; }

    internal static RequesterButton FromDto(ButtonDto dto, Func<string, InvalidDataException> bad) =>
        Box.From(dto.Box) is { } b && dto.Text is { Count: 2 } t
            ? new RequesterButton(b, dto.Label ?? "", t[0], t[1])
            {
                FacePen = dto.FacePen, BorderPen = dto.BorderPen, TextPen = dto.TextPen,
            }
            : throw bad("a button needs box [x, y, w, h] and text [x, y]");

    internal sealed class ButtonDto
    {
        public List<int>? Box { get; set; }
        public string? Label { get; set; }
        public List<int>? Text { get; set; }
        [JsonPropertyName("face_pen")] public int FacePen { get; set; }
        [JsonPropertyName("border_pen")] public int BorderPen { get; set; }
        [JsonPropertyName("text_pen")] public int TextPen { get; set; }
    }

    internal static IReadOnlyList<RequesterOutline> Outlines(List<OutlineDto>? dtos, Func<string, InvalidDataException> bad) =>
        [.. (dtos ?? []).Select(o => Box.From(o.Rect) is { } r ? new RequesterOutline(r, o.Pen) : throw bad("an outline needs rect [x, y, w, h]"))];

    internal sealed class OutlineDto
    {
        public List<int>? Rect { get; set; }
        public int Pen { get; set; }
    }
}

/// <summary>
/// Requester 0 (h84 <c>+0x3fc6</c>: Left 81, Top 63, 158 × 73) as h86 <c>+0x32a</c> fills it from <c>"a|b[c|d]"</c>: the text
/// lines into ITexts 0–4 (rows <see cref="RowsY"/>, the first used row by the line count, <see cref="FirstRow"/>), each at
/// x <see cref="RowLeft"/> + <see cref="HalfCell"/>·(<see cref="RowCells"/> − length); one button at
/// <see cref="OneButton"/>, two at <see cref="TwoButtons"/>, each label at x <see cref="LabelLeft"/> +
/// <see cref="HalfCell"/>·(<see cref="LabelCells"/> − length).
/// </summary>
public sealed record MenuRequester(Box Box, int FillPen)
{
    public IReadOnlyList<RequesterOutline> Borders { get; init; } = [];
    public IReadOnlyList<int> RowsY { get; init; } = [];
    public int RowPen { get; init; }
    public int RowLeft { get; init; }
    public int RowCells { get; init; }
    public int HalfCell { get; init; }

    /// <summary>Index = text lines − 1: the row the first line takes (past the list: row 0).</summary>
    public IReadOnlyList<int> FirstRow { get; init; } = [];

    public (int X, int Y) OneButton { get; init; }
    public IReadOnlyList<(int X, int Y)> TwoButtons { get; init; } = [];
    public (int W, int H) ButtonSize { get; init; }
    public int ButtonFacePen { get; init; }
    public int ButtonBorderPen { get; init; }
    public int LabelPen { get; init; }
    public int LabelLeft { get; init; }
    public int LabelY { get; init; }
    public int LabelCells { get; init; }

    /// <summary>The row of line <paramref name="i"/> of <paramref name="lines"/> (the remaster never drops one: rows past the five go on down).</summary>
    public int RowY(int lines, int i)
    {
        var first = lines >= 1 && lines - 1 < FirstRow.Count ? FirstRow[lines - 1] : 0;
        var k = first + i;
        return k < RowsY.Count ? RowsY[k] : RowsY[^1] + (k - RowsY.Count + 1) * (RowsY.Count > 1 ? RowsY[1] - RowsY[0] : 7);
    }

    /// <summary>A text line's x in the requester (h86 <c>+0x49e</c>: (23 − strlen)·3 + 10).</summary>
    public int RowX(int length) => RowLeft + HalfCell * (RowCells - length);

    /// <summary>A label's x in its button (h86 <c>+0x456</c>: (10 − strlen)·3 + 2).</summary>
    public int LabelX(int length) => LabelLeft + HalfCell * (LabelCells - length);

    /// <summary>The buttons' places in the requester for <paramref name="count"/> buttons (1 or 2; more take the two places).</summary>
    public IReadOnlyList<(int X, int Y)> ButtonsFor(int count) => count <= 1 ? [OneButton] : TwoButtons;

    internal static MenuRequester FromDto(Dto dto, Func<string, InvalidDataException> bad)
    {
        if (Box.From(dto.Box) is not { } box || dto.RowsY is not { Count: > 0 } rows || dto.OneButton is not { Count: 2 } one
            || dto.TwoButtons is not { Count: 2 } two || two.Any(b => b is not { Count: 2 }) || dto.ButtonSize is not { Count: 2 } size
            || dto.RowCells <= 0 || dto.LabelCells <= 0 || dto.HalfCell <= 0)
            throw bad("menu: box, rows_y, row_cells, half_cell, one_button, two_buttons ([x, y] each), button_size, label_cells");
        return new MenuRequester(box, dto.FillPen)
        {
            Borders = RequesterButton.Outlines(dto.Borders, bad),
            RowsY = [.. rows],
            RowPen = dto.RowPen,
            RowLeft = dto.RowLeft,
            RowCells = dto.RowCells,
            HalfCell = dto.HalfCell,
            FirstRow = [.. dto.FirstRow ?? []],
            OneButton = (one[0], one[1]),
            TwoButtons = [.. two.Select(b => (b[0], b[1]))],
            ButtonSize = (size[0], size[1]),
            ButtonFacePen = dto.ButtonFacePen,
            ButtonBorderPen = dto.ButtonBorderPen,
            LabelPen = dto.LabelPen,
            LabelLeft = dto.LabelLeft,
            LabelY = dto.LabelY,
            LabelCells = dto.LabelCells,
        };
    }

    internal sealed class Dto
    {
        public List<int>? Box { get; set; }
        [JsonPropertyName("fill_pen")] public int FillPen { get; set; }
        public List<RequesterButton.OutlineDto>? Borders { get; set; }
        [JsonPropertyName("rows_y")] public List<int>? RowsY { get; set; }
        [JsonPropertyName("row_pen")] public int RowPen { get; set; }
        [JsonPropertyName("row_left")] public int RowLeft { get; set; }
        [JsonPropertyName("row_cells")] public int RowCells { get; set; }
        [JsonPropertyName("half_cell")] public int HalfCell { get; set; }
        [JsonPropertyName("first_row")] public List<int>? FirstRow { get; set; }
        [JsonPropertyName("one_button")] public List<int>? OneButton { get; set; }
        [JsonPropertyName("two_buttons")] public List<List<int>>? TwoButtons { get; set; }
        [JsonPropertyName("button_size")] public List<int>? ButtonSize { get; set; }
        [JsonPropertyName("button_face_pen")] public int ButtonFacePen { get; set; }
        [JsonPropertyName("button_border_pen")] public int ButtonBorderPen { get; set; }
        [JsonPropertyName("label_pen")] public int LabelPen { get; set; }
        [JsonPropertyName("label_left")] public int LabelLeft { get; set; }
        [JsonPropertyName("label_y")] public int LabelY { get; set; }
        [JsonPropertyName("label_cells")] public int LabelCells { get; set; }
    }
}

/// <summary>
/// s156: a requester whose one gadget is a picture (requester 27: the logo, a click on it closes): box, backfill, outlines and
/// the picture (a pack picture name) at <see cref="PictureBox"/> relative to the box.
/// </summary>
public sealed record PictureRequester(Box Box, int FillPen, string Picture, Box PictureBox)
{
    public IReadOnlyList<RequesterOutline> Borders { get; init; } = [];

    internal static PictureRequester FromDto(Dto dto, Func<string, InvalidDataException> bad) =>
        Box.From(dto.Box) is { } box && Box.From(dto.PictureBox) is { } pic && !string.IsNullOrWhiteSpace(dto.Picture)
            ? new PictureRequester(box, dto.FillPen, dto.Picture, pic) { Borders = RequesterButton.Outlines(dto.Borders, bad) }
            : throw bad("about: box, picture and picture_box");

    internal sealed class Dto
    {
        public List<int>? Box { get; set; }
        [JsonPropertyName("fill_pen")] public int FillPen { get; set; }
        public List<RequesterButton.OutlineDto>? Borders { get; set; }
        public string? Picture { get; set; }
        [JsonPropertyName("picture_box")] public List<int>? PictureBox { get; set; }
    }
}

/// <summary>s156: one of the sheet's views (requesters 9–11): box, backfill, outlines, text rows (place and pen) and EXIT.</summary>
public sealed record SheetPage(Box Box, int FillPen, RequesterButton Exit)
{
    public IReadOnlyList<RequesterOutline> Borders { get; init; } = [];
    public IReadOnlyList<(int X, int Y, int Pen)> Rows { get; init; } = [];

    internal static SheetPage FromDto(Dto dto, Func<string, InvalidDataException> bad) =>
        Box.From(dto.Box) is { } box && dto.Exit is { } exit && dto.Rows is { Count: > 0 } rows && rows.All(r => r.At is { Count: 2 })
            ? new SheetPage(box, dto.FillPen, RequesterButton.FromDto(exit, bad))
            {
                Borders = RequesterButton.Outlines(dto.Borders, bad),
                Rows = [.. rows.Select(r => (r.At![0], r.At[1], r.Pen))],
            }
            : throw bad("sheet pages: box, rows ({at, pen}) and exit");

    internal sealed class Dto
    {
        public List<int>? Box { get; set; }
        [JsonPropertyName("fill_pen")] public int FillPen { get; set; }
        public List<RequesterButton.OutlineDto>? Borders { get; set; }
        public List<RowDto>? Rows { get; set; }
        public RequesterButton.ButtonDto? Exit { get; set; }
    }

    internal sealed class RowDto
    {
        public List<int>? At { get; set; }
        public int Pen { get; set; }
    }
}

/// <summary>A requester drawn as stored: box, backfill, outlines, a title (topaz) and its buttons (requester 22).</summary>
public sealed record FixedRequester(Box Box, int FillPen)
{
    public IReadOnlyList<RequesterOutline> Borders { get; init; } = [];
    public string Title { get; init; } = "";
    public (int X, int Y) TitleAt { get; init; }
    public int TitlePen { get; init; }
    public IReadOnlyList<RequesterButton> Buttons { get; init; } = [];

    internal static FixedRequester FromDto(Dto dto, Func<string, InvalidDataException> bad) =>
        Box.From(dto.Box) is { } box && dto.TitleAt is { Count: 2 } at && dto.Buttons is { Count: > 0 } buttons
            ? new FixedRequester(box, dto.FillPen)
            {
                Borders = RequesterButton.Outlines(dto.Borders, bad),
                Title = dto.Title ?? "",
                TitleAt = (at[0], at[1]),
                TitlePen = dto.TitlePen,
                Buttons = [.. buttons.Select(b => RequesterButton.FromDto(b, bad))],
            }
            : throw bad("a requester needs box, title_at and buttons");

    internal sealed class Dto
    {
        public List<int>? Box { get; set; }
        [JsonPropertyName("fill_pen")] public int FillPen { get; set; }
        public List<RequesterButton.OutlineDto>? Borders { get; set; }
        public string? Title { get; set; }
        [JsonPropertyName("title_at")] public List<int>? TitleAt { get; set; }
        [JsonPropertyName("title_pen")] public int TitlePen { get; set; }
        public List<RequesterButton.ButtonDto>? Buttons { get; set; }
    }
}

/// <summary>
/// Requester 14, the character sheet (h29 <c>+0x93e</c> fills it, <c>+0xcca</c> shows it), in its own <see cref="Palette"/>
/// (the town's: h5 runs with the stored pens). The name (IText 123, topaz) at <see cref="NameAt"/>; the LEVEL line (IText
/// 124) at <see cref="LevelAt"/>; the stat rows (ITexts 125–136, slots 0x7e–0x89: a label template with the value written
/// at its column) at <see cref="Rows"/>, each a UI string id formatted with its values; the runes row (ITexts 137–142) at
/// <see cref="RunesY"/>; the buttons (gadgets 41–44 and 3, GRELRIGHT / GRELBOTTOM, here as screen-relative places).
/// </summary>
public sealed record SheetRequester(Box Box, int FillPen, IReadOnlyList<string> Palette)
{
    public IReadOnlyList<RequesterOutline> Borders { get; init; } = [];
    public (int X, int Y) NameAt { get; init; }
    public int NamePen { get; init; }
    public (int X, int Y) LevelAt { get; init; }
    public int LevelPen { get; init; }

    /// <summary>The twelve stat rows: place and template string id, in slot order (STRENGTH … SCORE).</summary>
    public IReadOnlyList<(int X, int Y, string Text)> Rows { get; init; } = [];
    public int RowPen { get; init; }

    /// <summary>The runes row: "RUNES:" then AIR / EARTH / FIRE / WATER / GOD, each at its x (string ids).</summary>
    public int RunesY { get; init; }
    public IReadOnlyList<(int X, string Text)> Runes { get; init; } = [];
    public int RunesPen { get; init; }

    /// <summary>SPELLS, EQUIPMENT, SKILLS, POTIONS, EXIT (the last: gadget 3, id 0).</summary>
    public IReadOnlyList<RequesterButton> Buttons { get; init; } = [];

    /// <summary>
    /// s156: the sheet's views by name (equipment, skills, potions: requesters 10, 9, 11), each a box with its text rows and
    /// EXIT, stored pens; empty: none.
    /// </summary>
    public IReadOnlyDictionary<string, SheetPage> Pages { get; init; } = new Dictionary<string, SheetPage>(StringComparer.Ordinal);

    /// <summary>s156: Guild › New's rolled sheet (h23 <c>+0xc08</c>): PURGE (gadget 41) and KEEP (gadget 43); empty: none.</summary>
    public IReadOnlyList<RequesterButton> RolledButtons { get; init; } = [];

    internal static SheetRequester FromDto(Dto dto, IReadOnlyList<string> palette, Func<string, InvalidDataException> bad)
    {
        if (Box.From(dto.Box) is not { } box || dto.NameAt is not { Count: 2 } name || dto.LevelAt is not { Count: 2 } level
            || dto.Rows is not { Count: > 0 } rows || rows.Any(r => r.At is not { Count: 2 } || string.IsNullOrWhiteSpace(r.Text))
            || dto.Runes is not { } runes || runes.Any(r => string.IsNullOrWhiteSpace(r.Text)) || dto.Buttons is not { Count: > 0 } buttons)
            throw bad("sheet: box, name_at, level_at, rows ({at, text}), runes ({x, text}) and buttons");
        return new SheetRequester(box, dto.FillPen, palette)
        {
            Borders = RequesterButton.Outlines(dto.Borders, bad),
            NameAt = (name[0], name[1]),
            NamePen = dto.NamePen,
            LevelAt = (level[0], level[1]),
            LevelPen = dto.LevelPen,
            Rows = [.. rows.Select(r => (r.At![0], r.At[1], r.Text!))],
            RowPen = dto.RowPen,
            RunesY = dto.RunesY,
            Runes = [.. runes.Select(r => (r.X, r.Text!))],
            RunesPen = dto.RunesPen,
            Buttons = [.. buttons.Select(b => RequesterButton.FromDto(b, bad))],
            RolledButtons = [.. (dto.RolledButtons ?? []).Select(b => RequesterButton.FromDto(b, bad))],
            Pages = (dto.Pages ?? []).ToDictionary(kv => kv.Key, kv => SheetPage.FromDto(kv.Value, bad), StringComparer.Ordinal),
        };
    }

    internal sealed class Dto
    {
        public string? Palette { get; set; }
        public List<int>? Box { get; set; }
        [JsonPropertyName("fill_pen")] public int FillPen { get; set; }
        public List<RequesterButton.OutlineDto>? Borders { get; set; }
        [JsonPropertyName("name_at")] public List<int>? NameAt { get; set; }
        [JsonPropertyName("name_pen")] public int NamePen { get; set; }
        [JsonPropertyName("level_at")] public List<int>? LevelAt { get; set; }
        [JsonPropertyName("level_pen")] public int LevelPen { get; set; }
        public List<RowDto>? Rows { get; set; }
        [JsonPropertyName("row_pen")] public int RowPen { get; set; }
        [JsonPropertyName("runes_y")] public int RunesY { get; set; }
        public List<RuneDto>? Runes { get; set; }
        [JsonPropertyName("runes_pen")] public int RunesPen { get; set; }
        public List<RequesterButton.ButtonDto>? Buttons { get; set; }
        [JsonPropertyName("rolled_buttons")] public List<RequesterButton.ButtonDto>? RolledButtons { get; set; }
        public Dictionary<string, SheetPage.Dto>? Pages { get; set; }
    }

    internal sealed class RowDto
    {
        public List<int>? At { get; set; }
        public string? Text { get; set; }
    }

    internal sealed class RuneDto
    {
        public int X { get; set; }
        public string? Text { get; set; }
    }
}
