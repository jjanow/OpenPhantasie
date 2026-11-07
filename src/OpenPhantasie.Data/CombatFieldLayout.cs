using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace OpenPhantasie.Data;

/// <summary>
/// Manifest <c>combat_field</c>: Phantasie I Amiga's combat screen (s132, Amiga <c>FORMAT.md</c> "The combat screen"),
/// read from hunk 53 (drawing), 35 / 38 (the round's lines) and the requesters of hunk 84. Everything is in Amiga screen
/// pixels and pens of <see cref="Palette"/> (the combat table h69 <c>+0x94</c>, ×17).
/// <list type="bullet">
/// <item>The field: <see cref="Field"/> filled with pen 0, the frame (<see cref="Frame"/>, <see cref="FrameWidth"/> px in pen
/// <see cref="FramePen"/>) round it, the menu strip on rows 0 … <see cref="MenuBar"/>.Rows − 1.</item>
/// <item>The party row: every member not DEAD, <see cref="Slot"/>² at y <see cref="PartyY"/>, spread by
/// <see cref="Gap"/>; the monster groups above it, each <see cref="GroupGap"/> rows above the one below.</item>
/// <item>A line: its sounds (<see cref="LineSounds"/>), then the text centred on 320 in pen <see cref="LinePen"/> at
/// TopEdge <see cref="LineY"/>, then a wait of the Speed's polls (<see cref="SpeedPolls"/>, <see cref="PollSeconds"/>
/// each) unless the line is <see cref="ShortLine"/> characters or fewer.</item>
/// </list>
/// </summary>
public sealed record CombatFieldLayout(
    IReadOnlyList<string> Palette, Box Frame, int FrameWidth, int FramePen, Box Field, int PartyY, int Slot, int GroupGap,
    int LineY, int LinePen)
{
    private static readonly Regex HexColour = new("^#[0-9A-Fa-f]{6}$");

    /// <summary>The grave a dying member leaves in its slot (h54 <c>+0xe</c>, 16×19) and its offset from the slot.</summary>
    public string? Grave { get; init; }
    public int GraveX { get; init; }
    public int GraveY { get; init; }

    /// <summary>Seconds of one dos tick (<c>Delay</c>: 1/50 s).</summary>
    public double TickSeconds { get; init; } = 0.02;

    /// <summary>An attack frame's <c>Delay</c> (h53 <c>+0x59e</c> / <c>+0x622</c>: 18), dos ticks.</summary>
    public int AttackTicks { get; init; }

    /// <summary>A caster's silhouette <c>Delay</c> (h53 <c>+0x1060</c>: 18, or <see cref="FastFlashTicks"/> at Speed Fast).</summary>
    public int FlashTicks { get; init; }
    public int FastFlashTicks { get; init; }

    /// <summary>
    /// One WaitTOF of the set-up (h53 <c>+0x2fc</c>, s135): the field's RectFill (h77 <c>+0xa60</c>) and each party / monster
    /// blit (h77 <c>+0xcfe</c>) wait one vertical blank first, so the field is cleared and redrawn one picture a frame
    /// (NTSC 1/60 s; live s132 / s134 / s135: 0.12–0.14 s for 7–8 steps). 0 = drawn at once.
    /// </summary>
    public double SetupFrameSeconds { get; init; }

    /// <summary>The set-up's steps for a row of <paramref name="members"/> and <paramref name="monsters"/> standing: the fill, then one each.</summary>
    public static int SetupSteps(int members, int monsters) => 1 + Math.Max(0, members) + Math.Max(0, monsters);

    /// <summary>One poll of <c>h62 +0x576</c> (<c>Delay(1)</c> + a port check; 21.87 ms live, s130).</summary>
    public double PollSeconds { get; init; }

    /// <summary>The polls a line waits at Speed Fast / Normal / Slow (<c>h65 +0x27e</c>: 1, 30, 80).</summary>
    public IReadOnlyList<int> SpeedPolls { get; init; } = [];

    /// <summary>The polls before the player picks a Speed (h3 <c>+0</c> = 10 at boot, Normal ticked all the same).</summary>
    public int StartPolls { get; init; }

    /// <summary>A line this long or shorter does not wait (<c>+0xbe0</c>: strlen &gt; 3).</summary>
    public int ShortLine { get; init; }

    /// <summary>The polls after a member's grave and sound (<c>h62 +0x576(30)</c>).</summary>
    public int DeathPolls { get; init; }

    /// <summary>Sounds of the fight: its first set-up (17), each round's end (18), a member's death (15).</summary>
    public int? FightStartSound { get; init; }
    public int? RoundEndSound { get; init; }
    public int? MemberDeathSound { get; init; }

    /// <summary>
    /// s154: the monster pictures' disk read after FIGHT (h71 <c>+0x1050</c> → h53 <c>+0xd5e</c>, static `[C]`): every group
    /// whose monster is not in the <see cref="ShapeCacheSlots"/>-slot cache (h54 <c>+2</c>, kept for the program run) is read
    /// from <c>mstr1.pat</c> / <c>mstr2.pat</c> (h53 <c>+0xccc</c>: Open, Seek, Read, Close; no Delay) into the first slot this
    /// fight does not use; the map stays meanwhile. Pure disk time: one live figure for a fight with any read (s153's three
    /// fights 3.62 / 3.77 / 2.77 s, their mean, `[~]`); 0 = not modelled.
    /// </summary>
    public double ShapeLoadSeconds { get; init; }

    /// <summary>The monster-picture cache's slots (h53 <c>+0xd5e</c>: 3).</summary>
    public int ShapeCacheSlots { get; init; }

    /// <summary>Per message template (a string id), the sounds a party / monster line plays before it prints.</summary>
    public IReadOnlyList<CombatFieldLineSound> LineSounds { get; init; } = [];

    /// <summary>The menu strip (titles, the pulled-down box) and its colours.</summary>
    public CombatFieldMenuBar? MenuBar { get; init; }

    /// <summary>The orders requester (requester 21 at Top 23), the order buttons (24), the rank / spell prompt (26).</summary>
    public CombatFieldOrders? Orders { get; init; }

    /// <summary>The Cast requesters: the spell list (requester 12) and its "ON WHOM:" (requester 6), s133.</summary>
    public CombatFieldSpellList? SpellList { get; init; }

    /// <summary>The timed message requester 23 (Beg / Threaten / Greetings, the fight's end).</summary>
    public CombatFieldMessage? Message { get; init; }

    /// <summary>A party damage spell's sparkle on each monster it hits (h53 <c>+0xab8</c>, s133).</summary>
    public CombatFieldSparkle? Sparkle { get; init; }

    /// <summary>
    /// The font the window's text is drawn in (the line, the requesters): I Amiga's <c>z6font</c> 6 (TextAttr h78
    /// <c>+0x36</c>, s134), a font id of the pack's <c>fonts/</c>; null = the pack's own font.
    /// </summary>
    public string? Font { get; init; }

    /// <summary>The <see cref="Font"/>'s file next to the manifest (<c>fonts/&lt;id&gt;.json</c>), or null when there is none.</summary>
    public string? FontFile { get; init; }

    /// <summary><c>fonts/&lt;id&gt;.json</c> next to <paramref name="manifestPath"/> when it exists, else null.</summary>
    internal static string? FontFileFor(string? id, string manifestPath)
    {
        if (string.IsNullOrWhiteSpace(id))
            return null;
        var file = Path.Combine(Path.GetDirectoryName(manifestPath) ?? "", "fonts", id + ".json");
        return File.Exists(file) ? file : null;
    }

    /// <summary>(320 − <paramref name="w"/>·n)/(n + 1) (h53 <c>+0</c>, signed word division): the space before, between and after n things.</summary>
    public static int Gap(int n, int w) => n <= 0 ? 0 : (320 - w * n) / (n + 1);

    /// <summary>Thing <paramref name="i"/> of <paramref name="n"/>, each <paramref name="w"/> wide: its x.</summary>
    public static int SlotX(int i, int n, int w)
    {
        var gap = Gap(n, w);
        return gap + i * (gap + w);
    }

    /// <summary>
    /// The groups' tops (h53 <c>+0x2fc</c>): group 0 ends <see cref="GroupGap"/> rows above the party row, each next group
    /// <see cref="GroupGap"/> rows above the one before. <paramref name="heights"/>: each group's picture height.
    /// </summary>
    public IReadOnlyList<int> GroupTops(IReadOnlyList<int> heights)
    {
        ArgumentNullException.ThrowIfNull(heights);
        var tops = new int[heights.Count];
        var bottom = PartyY - GroupGap;
        for (var g = 0; g < heights.Count; g++)
        {
            tops[g] = bottom - heights[g];
            bottom = tops[g] - GroupGap;
        }
        return tops;
    }

    /// <summary>The polls a line waits at <paramref name="speed"/> (1 Fast, 2 Normal, 3 Slow; 0 = not picked yet).</summary>
    public int LinePolls(int speed) =>
        speed is >= 1 and <= 3 && SpeedPolls.Count == 3 ? SpeedPolls[speed - 1] : StartPolls;

    internal static CombatFieldLayout FromDto(Dto dto, string manifestPath)
    {
        InvalidDataException Bad(string what) => new($"{manifestPath}: combat_field: {what}.");
        if (dto.Palette is not { Count: 16 } palette || palette.Any(c => !HexColour.IsMatch(c ?? "")))
            throw Bad("palette must be 16 #RRGGBB colours");
        static bool Pen(int p) => p is >= 0 and <= 15;
        if (Box.From(dto.Frame) is not { } frame || Box.From(dto.Field) is not { } field || dto.FrameWidth <= 0 ||
            !Pen(dto.FramePen) || !Pen(dto.LinePen) || dto.Slot <= 0 || dto.GroupGap < 0)
            throw Bad("frame / field boxes, frame_width, pens, slot or group_gap");
        if (dto.AttackTicks < 0 || dto.FlashTicks < 0 || dto.FastFlashTicks < 0 || dto.PollSeconds is not > 0 ||
            dto.TickSeconds is <= 0 || dto.SpeedPolls is { Count: not 3 } || dto.SpeedPolls?.Any(p => p < 0) == true ||
            dto.StartPolls < 0 || dto.DeathPolls < 0 || dto.ShortLine < 0)
            throw Bad("bad timing numbers");
        var lines = new List<CombatFieldLineSound>();
        foreach (var l in dto.LineSounds ?? [])
        {
            if (string.IsNullOrWhiteSpace(l.Key) || (l.Party ?? []).Concat(l.Monster ?? []).Any(n => n < 0))
                throw Bad("line_sounds: a key and sound numbers ≥ 0");
            lines.Add(new CombatFieldLineSound(l.Key!, [.. l.Party ?? []], [.. l.Monster ?? []])
            {
                NoWait = l.NoWait,
                MonsterNoWait = l.MonsterNoWait,
                Flash = l.Flash,
                ThenBlank = l.ThenBlank,
            });
        }
        return new CombatFieldLayout([.. palette!], frame, dto.FrameWidth, dto.FramePen, field, dto.PartyY, dto.Slot,
            dto.GroupGap, dto.LineY, dto.LinePen)
        {
            Grave = string.IsNullOrWhiteSpace(dto.Grave) ? null : dto.Grave,
            GraveX = dto.GraveOffset is { Count: 2 } go ? go[0] : 0,
            GraveY = dto.GraveOffset is { Count: 2 } go2 ? go2[1] : 0,
            TickSeconds = dto.TickSeconds ?? 0.02,
            AttackTicks = dto.AttackTicks,
            FlashTicks = dto.FlashTicks,
            FastFlashTicks = dto.FastFlashTicks,
            SetupFrameSeconds = Math.Max(0, dto.SetupFrameSeconds ?? 0),
            PollSeconds = dto.PollSeconds!.Value,
            SpeedPolls = [.. dto.SpeedPolls ?? []],
            StartPolls = dto.StartPolls,
            ShortLine = dto.ShortLine,
            DeathPolls = dto.DeathPolls,
            FightStartSound = dto.Sounds?.FightStart,
            RoundEndSound = dto.Sounds?.RoundEnd,
            MemberDeathSound = dto.Sounds?.MemberDeath,
            ShapeLoadSeconds = dto.ShapeLoadSeconds is > 0 ? dto.ShapeLoadSeconds.Value : 0,
            ShapeCacheSlots = Math.Max(0, dto.ShapeCacheSlots),
            LineSounds = lines,
            MenuBar = dto.MenuBar is { } mb ? CombatFieldMenuBar.FromDto(mb, Bad) with
            {
                Font = string.IsNullOrWhiteSpace(mb.Font) ? null : mb.Font,
                FontFile = FontFileFor(mb.Font, manifestPath),
            } : null,
            Orders = dto.Orders is { } od ? CombatFieldOrders.FromDto(od, Bad) : null,
            SpellList = dto.SpellList?.Build(Bad),
            Message = dto.Message is { } ms ? CombatFieldMessage.FromDto(ms, Bad) : null,
            Sparkle = dto.Sparkle is { } sp ? CombatFieldSparkle.FromDto(sp, Bad) : null,
            Font = string.IsNullOrWhiteSpace(dto.Font) ? null : dto.Font,
            FontFile = FontFileFor(dto.Font, manifestPath),
        };
    }

    internal sealed class Dto
    {
        public List<string?>? Palette { get; set; }
        public List<int>? Frame { get; set; }
        [JsonPropertyName("frame_width")] public int FrameWidth { get; set; }
        [JsonPropertyName("frame_pen")] public int FramePen { get; set; }
        public List<int>? Field { get; set; }
        [JsonPropertyName("party_y")] public int PartyY { get; set; }
        public int Slot { get; set; }
        [JsonPropertyName("group_gap")] public int GroupGap { get; set; }
        [JsonPropertyName("line_y")] public int LineY { get; set; }
        [JsonPropertyName("line_pen")] public int LinePen { get; set; }
        public string? Grave { get; set; }
        [JsonPropertyName("grave_offset")] public List<int>? GraveOffset { get; set; }
        [JsonPropertyName("tick_seconds")] public double? TickSeconds { get; set; }
        [JsonPropertyName("attack_ticks")] public int AttackTicks { get; set; }
        [JsonPropertyName("flash_ticks")] public int FlashTicks { get; set; }
        [JsonPropertyName("fast_flash_ticks")] public int FastFlashTicks { get; set; }
        [JsonPropertyName("setup_frame_seconds")] public double? SetupFrameSeconds { get; set; }
        [JsonPropertyName("poll_seconds")] public double? PollSeconds { get; set; }
        [JsonPropertyName("speed_polls")] public List<int>? SpeedPolls { get; set; }
        [JsonPropertyName("start_polls")] public int StartPolls { get; set; }
        [JsonPropertyName("short_line")] public int ShortLine { get; set; }
        [JsonPropertyName("death_polls")] public int DeathPolls { get; set; }
        [JsonPropertyName("shape_load_seconds")] public double? ShapeLoadSeconds { get; set; }
        [JsonPropertyName("shape_cache_slots")] public int ShapeCacheSlots { get; set; }
        public SoundsDto? Sounds { get; set; }
        [JsonPropertyName("line_sounds")] public List<LineSoundDto>? LineSounds { get; set; }
        [JsonPropertyName("menu_bar")] public CombatFieldMenuBar.Dto? MenuBar { get; set; }
        public CombatFieldOrders.Dto? Orders { get; set; }
        [JsonPropertyName("spell_list")] public CombatFieldSpellListDto? SpellList { get; set; }
        public CombatFieldMessage.Dto? Message { get; set; }
        public CombatFieldSparkle.Dto? Sparkle { get; set; }
        public string? Font { get; set; }
    }

    internal sealed class SoundsDto
    {
        [JsonPropertyName("fight_start")] public int? FightStart { get; set; }
        [JsonPropertyName("round_end")] public int? RoundEnd { get; set; }
        [JsonPropertyName("member_death")] public int? MemberDeath { get; set; }
    }

    internal sealed class LineSoundDto
    {
        public string? Key { get; set; }
        public List<int>? Party { get; set; }
        public List<int>? Monster { get; set; }
        [JsonPropertyName("no_wait")] public bool NoWait { get; set; }
        [JsonPropertyName("monster_no_wait")] public bool MonsterNoWait { get; set; }
        public bool Flash { get; set; }
        [JsonPropertyName("then_blank")] public bool ThenBlank { get; set; }
    }
}

/// <summary>A rectangle in screen pixels: its left, top, width and height.</summary>
public readonly record struct Box(int X, int Y, int W, int H)
{
    public int Right => X + W - 1;
    public int Bottom => Y + H - 1;

    internal static Box? From(IReadOnlyList<int>? xywh) =>
        xywh is { Count: 4 } b && b[2] > 0 && b[3] > 0 ? new Box(b[0], b[1], b[2], b[3]) : null;
}

/// <summary>
/// One message of I Amiga's round (h35 / h38 → h53 <c>+0xb80(text, n)</c>): the string id of Core's template and the
/// sounds played before it prints on the party's / the monsters' side. <see cref="NoWait"/>: the line does not wait (a
/// member's "DIES": n = 15, the death plays it). <see cref="MonsterNoWait"/>: no wait on the monsters' side only (a
/// monster's "%s CASTS A SPELL": the Amiga prints a one-space line with sound 9, h38 <c>+0x480</c>; D-079, user s138: the
/// remaster keeps DOS's text, with the Amiga's sound and no wait). <see cref="Flash"/>: the actor's silhouette and attack
/// frame come first (a cast).
/// </summary>
public sealed record CombatFieldLineSound(string Key, IReadOnlyList<int> Party, IReadOnlyList<int> Monster)
{
    public bool NoWait { get; init; }
    public bool MonsterNoWait { get; init; }
    public bool Flash { get; init; }

    /// <summary>
    /// s154: after its wait the line is cleared by a one-space line (no sound, too short to wait): a combat resurrection's
    /// <c>+0xb80(" ", −1)</c> after <c>"%s IS RESURRECTED"</c> (h38 <c>+0x440</c>, static `[C]`).
    /// </summary>
    public bool ThenBlank { get; init; }

    /// <summary>The line waits no polls on this side (<see cref="NoWait"/>, or <see cref="MonsterNoWait"/> for a monster's).</summary>
    public bool SkipsWait(bool party) => NoWait || (!party && MonsterNoWait);
}

/// <summary>
/// The Intuition menu strip on the field (h81): the bar's rows and pens, each title's x and its highlight box when pulled
/// down, and the item box (left, top, width, item row height, item text x) of each menu.
/// </summary>
public sealed record CombatFieldMenuBar(int Rows, int BarPen, int TextPen, int TextY, IReadOnlyList<CombatFieldMenu> Menus)
{
    /// <summary>Pens of a pulled-down menu: its box fill, border and text; the highlighted item's fill and text.</summary>
    public int BoxPen { get; init; }
    public int BorderPen { get; init; }
    public int ItemPen { get; init; }
    public int HighlightPen { get; init; }
    public int HighlightTextPen { get; init; }

    /// <summary>
    /// The pulled-down box has a border (<see cref="BorderPen"/>; Intuition's strip, I Amiga). False: a solid box with no
    /// frame (III Amiga's own bars, <c>0x28de</c>, s156).
    /// </summary>
    public bool Frame { get; init; } = true;

    /// <summary>
    /// The font of the bar and the menu items: the screen's font, topaz 8 (NewScreen.Font = TextAttr h78 <c>+0x22</c>; the
    /// items' IntuiTexts name it too, s134), a font id of the pack's <c>fonts/</c>; null = the field's font.
    /// </summary>
    public string? Font { get; init; }

    /// <summary>The <see cref="Font"/>'s file next to the manifest, or null when there is none.</summary>
    public string? FontFile { get; init; }

    /// <summary>
    /// s155: Intuition's ghosting of a disabled item (I Amiga's map Options › Look outdoors): rows of a pattern repeated
    /// over the item's text from screen (0, 0), <c>#</c> = the pixel cleared to <see cref="BoxPen"/>; empty = none.
    /// </summary>
    public IReadOnlyList<string> GhostPattern { get; init; } = [];

    /// <summary>s155: the item actions (<see cref="CombatFieldMenu.Actions"/>) ghosted, and inert, on the overworld.</summary>
    public IReadOnlyList<string> GhostedOutdoors { get; init; } = [];

    internal static CombatFieldMenuBar FromDto(Dto dto, Func<string, InvalidDataException> bad)
    {
        if (dto.Menus is not { Count: > 0 } menus || dto.Rows <= 0)
            throw bad("menu_bar: rows and menus");
        var list = new List<CombatFieldMenu>();
        foreach (var m in menus)
        {
            if (string.IsNullOrWhiteSpace(m.Title) || Box.From(m.Highlight) is not { } hl || m.Items is null ||
                (m.Items.Count > 0 && Box.From(m.Box) is null) || m.ItemHeight < 0)
                throw bad("menu_bar: a menu needs title, highlight [x,y,w,h], items and box");
            if (m.Actions is { } acts && acts.Count != m.Items.Count)
                throw bad("menu_bar: actions must name one action per item");
            list.Add(new CombatFieldMenu(m.Title!, m.TitleX, hl, [.. m.Items.Select(i => i ?? "")],
                Box.From(m.Box) ?? default, m.ItemHeight, m.ItemX, m.ItemY) { Actions = [.. (m.Actions ?? []).Select(a => a ?? "")] });
        }
        if (dto.GhostPattern is { } ghost && (ghost.Count == 0 || ghost.Any(string.IsNullOrEmpty)))
            throw bad("menu_bar: ghost_pattern needs non-empty rows");
        return new CombatFieldMenuBar(dto.Rows, dto.BarPen, dto.TextPen, dto.TextY, list)
        {
            BoxPen = dto.BoxPen,
            BorderPen = dto.BorderPen,
            ItemPen = dto.ItemPen,
            HighlightPen = dto.HighlightPen,
            HighlightTextPen = dto.HighlightTextPen,
            Frame = dto.Frame ?? true,
            GhostPattern = [.. dto.GhostPattern ?? []],
            GhostedOutdoors = [.. dto.GhostedOutdoors ?? []],
        };
    }

    internal sealed class Dto
    {
        public int Rows { get; set; }
        [JsonPropertyName("bar_pen")] public int BarPen { get; set; }
        [JsonPropertyName("text_pen")] public int TextPen { get; set; }
        [JsonPropertyName("text_y")] public int TextY { get; set; }
        [JsonPropertyName("box_pen")] public int BoxPen { get; set; }
        [JsonPropertyName("border_pen")] public int BorderPen { get; set; }
        [JsonPropertyName("item_pen")] public int ItemPen { get; set; }
        [JsonPropertyName("highlight_pen")] public int HighlightPen { get; set; }
        [JsonPropertyName("highlight_text_pen")] public int HighlightTextPen { get; set; }
        public bool? Frame { get; set; }
        public List<MenuDto>? Menus { get; set; }
        public string? Font { get; set; }
        [JsonPropertyName("ghost_pattern")] public List<string>? GhostPattern { get; set; }
        [JsonPropertyName("ghosted_outdoors")] public List<string>? GhostedOutdoors { get; set; }
    }

    internal sealed class MenuDto
    {
        public string? Title { get; set; }
        [JsonPropertyName("title_x")] public int TitleX { get; set; }
        public List<int>? Highlight { get; set; }
        public List<string?>? Items { get; set; }
        public List<int>? Box { get; set; }
        [JsonPropertyName("item_height")] public int ItemHeight { get; set; }
        [JsonPropertyName("item_x")] public int ItemX { get; set; }
        [JsonPropertyName("item_y")] public int ItemY { get; set; }
        public List<string?>? Actions { get; set; }
    }
}

/// <summary>One menu of <see cref="CombatFieldMenuBar"/>: its title's string id and x, the title's highlight box, the items' string ids, the item box and rows.</summary>
public sealed record CombatFieldMenu(string Title, int TitleX, Box Highlight, IReadOnlyList<string> Items, Box Box, int ItemHeight,
    int ItemX, int ItemY)
{
    /// <summary>
    /// s155: what each item does in the remaster, one name per item (I Amiga's map bar: <c>about</c>, <c>inspect</c>,
    /// <c>health</c>, <c>cast</c>, <c>use_potion</c>, <c>look</c>, <c>fast</c> / <c>normal</c> / <c>slow</c>); an empty list
    /// leaves the mapping to the caller (the fight's bar, <c>CombatPresenter.P1FieldMenus</c>).
    /// </summary>
    public IReadOnlyList<string> Actions { get; init; } = [];
}

/// <summary>
/// I Amiga's orders requesters (h71 <c>+0x90</c>, h62 <c>+0xcc</c> / <c>+0x170</c>): requester 21 (<see cref="Box"/>, fill
/// <see cref="FillPen"/>, <see cref="BorderPen"/> borders), its member rows (text from <see cref="RowX"/>, <see cref="RowY"/>,
/// <see cref="RowStep"/> apart, pen <see cref="RowPen"/>) and the FIGHT button; requester 24's six order buttons; requester
/// 26 for the rank or the spell.
/// </summary>
public sealed record CombatFieldOrders(Box Box, int FillPen, int BorderPen, int RowX, int RowY, int RowStep, int RowPen)
{
    public Box Fight { get; init; }
    public int FightFillPen { get; init; }
    public int FightBorderPen { get; init; }
    public int FightTextX { get; init; }
    public int FightTextY { get; init; }
    public int FightTextPen { get; init; }
    public string FightLabel { get; init; } = "";
    public Box ChoiceBox { get; init; }
    public IReadOnlyList<Box> ChoiceButtons { get; init; } = [];
    public Box PromptBox { get; init; }
    public int PromptTextX { get; init; }
    public int PromptTextY { get; init; }
    public int PromptTextPen { get; init; }
    public IReadOnlyList<Box> RankButtons { get; init; } = [];
    public int RankBorderPen { get; init; }

    internal static CombatFieldOrders FromDto(Dto dto, Func<string, InvalidDataException> bad)
    {
        if (Box.From(dto.Box) is not { } box || Box.From(dto.Fight) is not { } fight || Box.From(dto.ChoiceBox) is not { } choice
            || Box.From(dto.PromptBox) is not { } prompt || dto.RowStep <= 0)
            throw bad("orders: box, fight, choice_box, prompt_box and row_step");
        var buttons = (dto.ChoiceButtons ?? []).Select(b => Box.From(b) ?? throw bad("orders: choice_buttons")).ToList();
        var ranks = (dto.RankButtons ?? []).Select(b => Box.From(b) ?? throw bad("orders: rank_buttons")).ToList();
        return new CombatFieldOrders(box, dto.FillPen, dto.BorderPen, dto.RowX, dto.RowY, dto.RowStep, dto.RowPen)
        {
            Fight = fight,
            FightFillPen = dto.FightFillPen,
            FightBorderPen = dto.FightBorderPen,
            FightTextX = dto.FightText is { Count: 2 } ft ? ft[0] : fight.X,
            FightTextY = dto.FightText is { Count: 2 } ft2 ? ft2[1] : fight.Y,
            FightTextPen = dto.FightTextPen,
            FightLabel = dto.FightLabel ?? "",
            ChoiceBox = choice,
            ChoiceButtons = buttons,
            PromptBox = prompt,
            PromptTextX = dto.PromptText is { Count: 2 } pt ? pt[0] : prompt.X,
            PromptTextY = dto.PromptText is { Count: 2 } pt2 ? pt2[1] : prompt.Y,
            PromptTextPen = dto.PromptTextPen,
            RankButtons = ranks,
            RankBorderPen = dto.RankBorderPen,
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
        public List<int>? Fight { get; set; }
        [JsonPropertyName("fight_fill_pen")] public int FightFillPen { get; set; }
        [JsonPropertyName("fight_border_pen")] public int FightBorderPen { get; set; }
        [JsonPropertyName("fight_text")] public List<int>? FightText { get; set; }
        [JsonPropertyName("fight_text_pen")] public int FightTextPen { get; set; }
        [JsonPropertyName("fight_label")] public string? FightLabel { get; set; }
        [JsonPropertyName("choice_box")] public List<int>? ChoiceBox { get; set; }
        [JsonPropertyName("choice_buttons")] public List<List<int>>? ChoiceButtons { get; set; }
        [JsonPropertyName("prompt_box")] public List<int>? PromptBox { get; set; }
        [JsonPropertyName("prompt_text")] public List<int>? PromptText { get; set; }
        [JsonPropertyName("prompt_text_pen")] public int PromptTextPen { get; set; }
        [JsonPropertyName("rank_buttons")] public List<List<int>>? RankButtons { get; set; }
        [JsonPropertyName("rank_border_pen")] public int RankBorderPen { get; set; }
    }
}

/// <summary>
/// Requester 23 (h44 <c>+0x466</c> / <c>+0x4d4</c> / <c>+0x556</c> → h80 <c>+0x546</c>): box, pens, the three text rows'
/// x / y. Each line is centred in <see cref="CenterColumns"/> character cells first (h65 <c>+0x1f4(text, buffer, 40)</c>:
/// ⌊(40 − length) / 2⌋ spaces in front, none when the text is longer; s135, seen live), then drawn at <see cref="TextX"/>
/// (the IntuiTexts' LeftEdge). A message of one line takes the middle row (rows 0 and 2 blank), of two the top two, of three all three. Which lines
/// go there: <see cref="Keys"/> (string ids); <see cref="Joins"/> pairs that make one box, rows in the given order. It closes
/// itself after ⌊(polls + <see cref="ExtraTicks"/>[rows − 1]) / <see cref="TicksPerSecond"/>⌋ whole seconds (the wait counts
/// <c>CurrentTime</c> micros against (n / 60)·10⁶, unsigned division); keys and clicks are swallowed, only a GADGETUP
/// would end it and the box has no gadget. The clock quirk (h80 <c>+0xd2a</c>, s136): the elapsed time is
/// ((now s mod <see cref="ClockWrapSeconds"/>)·10⁶ + µs) − (the same at the opening), compared unsigned, so the box closes at
/// once when the clock's seconds wrap to a multiple of <see cref="ClockWrapSeconds"/> (<see cref="ShownSeconds"/>).
/// </summary>
public sealed record CombatFieldMessage(Box Box, int FillPen, int BorderPen, int TextPen, int TextX, IReadOnlyList<int> RowsY)
{
    public IReadOnlyList<int> ExtraTicks { get; init; } = [];
    public double TicksPerSecond { get; init; } = 60;
    public IReadOnlyList<string> Keys { get; init; } = [];

    /// <summary>The field the openers centre each line in (h65 <c>+0x1f4</c>'s width 40); 0 = left at <see cref="TextX"/>.</summary>
    public int CenterColumns { get; init; }

    /// <summary>
    /// The x of a line of <paramref name="length"/> characters in a font of <paramref name="advance"/> px a cell: the
    /// spaces h65 <c>+0x1f4</c> puts in front (<c>(width − length) / 2</c>, signed and truncating) count as cells.
    /// </summary>
    public int TextLeft(int length, int advance)
    {
        var pad = CenterColumns > 0 ? (CenterColumns - length) / 2 : 0;
        return TextX + Math.Max(0, pad) * advance;
    }

    /// <summary>Lines that open the box only outside a round (h44 <c>+0x3a6</c>: the morale check's "MONSTERS RUN"; in a round h35 prints it on the field).</summary>
    public IReadOnlyList<string> OutsideRoundKeys { get; init; } = [];

    /// <summary>A sound played before the box of a key (s153: h44 <c>+0x398</c> plays 2 before the morale check's
    /// "MONSTERS RUN"; it blocks), by string key.</summary>
    public IReadOnlyDictionary<string, int> SoundsBefore { get; init; } = new Dictionary<string, int>();
    public IReadOnlyList<IReadOnlyList<string>> Joins { get; init; } = [];

    /// <summary>
    /// s154: Party Options › Show names (h44 <c>+0x138c</c>): the rank list's wait in ticks (<c>h80 +0x546(0x17, h3 + 360)</c>,
    /// h44 <c>+0x1468</c>), whatever the row count; 0 = not given. Its rows go straight into slots 101… (h23 <c>+0xd28</c>,
    /// after the three are blanked): from the top row, not centred.
    /// </summary>
    public int ListTicks { get; init; }

    /// <summary>s154: seconds the Show names list stays up after a Speed of <paramref name="polls"/> polls (whole seconds).</summary>
    public double ListSeconds(int polls) => ListTicks <= 0 ? 0 : Math.Floor((polls + ListTicks) / TicksPerSecond);

    /// <summary>Seconds a box of <paramref name="rows"/> rows stays up after a Speed of <paramref name="polls"/> polls.</summary>
    public double Seconds(int rows, int polls)
    {
        if (ExtraTicks.Count == 0)
            return 0;
        var extra = ExtraTicks[Math.Clamp(rows, 1, ExtraTicks.Count) - 1];
        return Math.Floor((polls + extra) / TicksPerSecond);
    }

    /// <summary>
    /// h80 <c>+0xd2a</c> keeps only the clock's seconds mod this (<c>andi.l #7</c>: 8), 0 = no wrap (s136). A box closes at
    /// the first clock reading past such a multiple of seconds, even before its <see cref="Seconds"/>.
    /// </summary>
    public double ClockWrapSeconds { get; init; }

    /// <summary>
    /// How long a box of <paramref name="rows"/> rows after a Speed of <paramref name="polls"/> polls really stays up when
    /// it opens at <paramref name="clockSeconds"/> on the machine's clock (null: the clock not modelled, the full
    /// <see cref="Seconds"/>): the full time, or less when the clock's seconds reach a multiple of
    /// <see cref="ClockWrapSeconds"/> first (h80 <c>+0xd2a</c>: the elapsed time comes out negative, compared unsigned).
    /// </summary>
    public double ShownSeconds(int rows, int polls, double? clockSeconds) => ClockCut(Seconds(rows, polls), clockSeconds);

    /// <summary>
    /// s144: how long a wait of <paramref name="full"/> seconds through h80 <c>+0x546</c> really lasts when it starts at
    /// <paramref name="clockSeconds"/> (null: not modelled): cut at the clock's next multiple of <see cref="ClockWrapSeconds"/>.
    /// </summary>
    public double ClockCut(double full, double? clockSeconds)
    {
        if (clockSeconds is not { } clock || ClockWrapSeconds <= 0 || full <= 0)
            return full;
        var phase = clock % ClockWrapSeconds;
        if (phase < 0)
            phase += ClockWrapSeconds;
        return Math.Min(full, ClockWrapSeconds - phase);
    }

    /// <summary>The rows' indices for a box of <paramref name="rows"/> lines (one line: the middle row).</summary>
    public IReadOnlyList<int> RowIndices(int rows) =>
        rows == 1 && RowsY.Count >= 3 ? [1] : [.. Enumerable.Range(0, Math.Min(rows, RowsY.Count))];

    internal static CombatFieldMessage FromDto(Dto dto, Func<string, InvalidDataException> bad)
    {
        if (Box.From(dto.Box) is not { } box || dto.RowsY is not { Count: > 0 } rows || dto.TicksPerSecond is <= 0 ||
            dto.ExtraTicks?.Any(t => t < 0) == true || dto.Joins?.Any(j => j is not { Count: >= 2 }) == true ||
            dto.ClockWrapSeconds < 0)
            throw bad("message: box, rows_y, extra_ticks, joins or clock_wrap_seconds");
        return new CombatFieldMessage(box, dto.FillPen, dto.BorderPen, dto.TextPen, dto.TextX, [.. rows])
        {
            ExtraTicks = [.. dto.ExtraTicks ?? []],
            TicksPerSecond = dto.TicksPerSecond ?? 60,
            Keys = [.. dto.Keys ?? []],
            CenterColumns = Math.Max(0, dto.CenterColumns),
            OutsideRoundKeys = [.. dto.OutsideRoundKeys ?? []],
            SoundsBefore = dto.SoundsBefore ?? new Dictionary<string, int>(),
            Joins = [.. (dto.Joins ?? []).Select(j => (IReadOnlyList<string>)[.. j])],
            ClockWrapSeconds = dto.ClockWrapSeconds,
            ListTicks = Math.Max(0, dto.ListTicks),
        };
    }

    internal sealed class Dto
    {
        [JsonPropertyName("list_ticks")] public int ListTicks { get; set; }
        public List<int>? Box { get; set; }
        [JsonPropertyName("fill_pen")] public int FillPen { get; set; }
        [JsonPropertyName("border_pen")] public int BorderPen { get; set; }
        [JsonPropertyName("text_pen")] public int TextPen { get; set; }
        [JsonPropertyName("text_x")] public int TextX { get; set; }
        [JsonPropertyName("center_columns")] public int CenterColumns { get; set; }
        [JsonPropertyName("rows_y")] public List<int>? RowsY { get; set; }
        [JsonPropertyName("extra_ticks")] public List<int>? ExtraTicks { get; set; }
        public List<string>? Keys { get; set; }
        [JsonPropertyName("outside_round_keys")] public List<string>? OutsideRoundKeys { get; set; }
        [JsonPropertyName("sounds_before")] public Dictionary<string, int>? SoundsBefore { get; set; }
        public List<List<string>>? Joins { get; set; }
        [JsonPropertyName("ticks_per_second")] public double? TicksPerSecond { get; set; }
        [JsonPropertyName("clock_wrap_seconds")] public double ClockWrapSeconds { get; set; }
    }
}

/// <summary>
/// The sparkle of a party damage spell (s133; h53 <c>+0xab8</c>, called by h35 <c>+0x4bc</c> for FIREFLASH and h38
/// <c>+0x1a6</c> / <c>+0x1f6</c> for MINDBLAST / FLAMEBOLT, before each monster's damage): <see cref="Count"/> times, the
/// <see cref="Sound"/> (it blocks), then the hardware sprite <see cref="Picture"/> (<see cref="Size"/>² px, h77
/// <c>+0xf2a</c>) at a random spot inside the monster for <see cref="Ticks"/> dos ticks (<c>Delay(18)</c>, Speed does not
/// change it), then off (<c>+0x108e</c>). The spot (<c>+0xa54</c>): the monster's x + <see cref="Offset"/>(r, w − size),
/// its y + <see cref="Offset"/>(r', h − size).
/// </summary>
public sealed record CombatFieldSparkle(string Picture, int Size, int Ticks, int DamageStep)
{
    /// <summary>The sound before each sparkle (h89 <c>+0x1cc</c>: the script h90 <c>+0x299</c>), or null.</summary>
    public int? Sound { get; init; }

    /// <summary>VBLs h77 <c>+0xf2a</c> waits (<c>WaitTOF</c>) before the sprite shows (s153 static: four; live, the gap
    /// between two sparkles 0.18 s = sound 30's hold 0.10 + these).</summary>
    public int OnFrames { get; init; }

    /// <summary>VBLs h77 <c>+0x108e</c> waits taking the sprite off after its <c>Delay</c> (s153 static: three); the sprite
    /// stays up for the first.</summary>
    public int OffFrames { get; init; }

    /// <summary>Sparkles for a spell's damage (before armour): damage / <see cref="DamageStep"/> + 1 (<c>divs.w #32</c>).</summary>
    public int Count(int damage) => damage / DamageStep + 1;

    /// <summary>The largest random number <see cref="Offset"/> takes (the game's <c>rand()</c>: 0 … 32767).</summary>
    public const int RandMax = 32767;

    /// <summary>
    /// h53 <c>+0xa0e(span)</c>: 0 for span 0, else r / (32767 / span) in C's truncating division, r = 0 … <see cref="RandMax"/>;
    /// it reaches span itself for the top few r.
    /// </summary>
    public static int Offset(int r, int span) => span == 0 || RandMax / span == 0 ? 0 : r / (RandMax / span);

    internal static CombatFieldSparkle FromDto(Dto dto, Func<string, InvalidDataException> bad)
    {
        if (string.IsNullOrWhiteSpace(dto.Picture) || dto.Size <= 0 || dto.Ticks < 0 || dto.DamageStep <= 0 || dto.Sound < 0
            || dto.OnFrames < 0 || dto.OffFrames < 0)
            throw bad("sparkle: picture, size, ticks, damage_step, sound, on_frames and off_frames");
        return new CombatFieldSparkle(dto.Picture!, dto.Size, dto.Ticks, dto.DamageStep)
        {
            Sound = dto.Sound, OnFrames = dto.OnFrames ?? 0, OffFrames = dto.OffFrames ?? 0,
        };
    }

    internal sealed class Dto
    {
        public string? Picture { get; set; }
        public int Size { get; set; }
        public int Ticks { get; set; }
        [JsonPropertyName("damage_step")] public int DamageStep { get; set; }
        public int? Sound { get; set; }
        [JsonPropertyName("on_frames")] public int? OnFrames { get; set; }
        [JsonPropertyName("off_frames")] public int? OffFrames { get; set; }
    }
}
