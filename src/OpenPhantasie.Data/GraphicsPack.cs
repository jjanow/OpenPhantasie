using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace OpenPhantasie.Data;

/// <summary>
/// Picture names the Client asks a <see cref="GraphicsPack"/> for (T1.5). A name says what is shown, never which file:
/// each pack's manifest maps the names it has to its own images. A name a pack lacks resolves to null and the Client
/// draws what that platform's version showed there (often nothing or text) — never another pack's art (user, s70).
/// </summary>
public static class PictureNames
{
    public const string TitleCover = "title.cover";
    /// <summary>The original's second start-up picture (credits) when it has one (title splash, s102).</summary>
    public const string TitleCredits = "title.credits";
    public const string TownBackdrop = "town.backdrop";
    /// <summary>The town street's open doors at their own places on a full-screen picture (III Amiga <c>D/w3</c>, s102).</summary>
    public const string TownDoors = "town.doors";
    public const string Decor = "ui.decor";

    /// <summary>
    /// The game-won picture (I Amiga <c>final.pic</c>, Zeus: h32 <c>+0x6d0</c>, combat table set pen by pen; II ST
    /// <c>final2.pic</c>). The Client draws I's on its victory screen with the pack's <see cref="EndingSceneLayout"/>
    /// (s127), II's with the ST pack's (s128, Filmon's ceremony).
    /// </summary>
    public const string EndingFinal = "ending.final";

    /// <summary>Phantasie II's defeat sequence parts drawn on <see cref="Decor"/> (ST <c>0x12bea</c>, FORMAT.md §10.4).</summary>
    public const string DecorDoor = "decor.door";
    public const string DecorDemonFace = "decor.demon-face";
    public const string DecorSorrySign = "decor.sorry-sign";

    /// <summary>
    /// Phantasie I DOS's party-defeat screens (OV1 <c>0x2af</c>, endgame.md section 4, s157): the temple with an empty
    /// doorway ("YOU HAVE ARRIVED AT THE ASTRAL PLANE"), the same with the Great Demon's face ("THE GREAT DEMON APPEARS AND
    /// SPEAKS:"), the ghost in every member's slot and the figure a revived member turns back into (by race byte, 15 =
    /// UNDEAD). Whole-screen pictures; <see cref="DefeatPicturesLayout"/> places the row and the line.
    /// </summary>
    public const string DefeatTemple = "defeat.temple";
    public const string DefeatDemon = "defeat.demon";
    public const string DefeatGhost = "defeat.ghost";
    public static string DefeatFigure(int raceId) => Inv($"defeat.figure.{raceId}");

    /// <summary>
    /// Phantasie II's ghost portrait (ST <c>0x22756</c>, FORMAT.md §10.3): every member's portrait turns into it in the
    /// defeat scene (<c>0x12b5e</c>) until the Great Demon's judgement redraws the slot.
    /// </summary>
    public const string PortraitGhost = "portrait.ghost";

    /// <summary>
    /// The mark on a member dying in combat (ST <c>0x23cdc</c>, 16×19, drawn at portrait + (8,13) for 500 ms by
    /// <c>0x188fc</c> mode 0, FORMAT.md §10.3).
    /// </summary>
    public const string PortraitDeathMark = "portrait.death-mark";
    public const string OverworldParty = "overworld.party";
    public const string OverworldFog = "overworld.fog";
    public const string DungeonCompass = "dungeon.compass";

    public static string MonsterIdle(int monsterId) => Inv($"monster.{monsterId}.idle");
    public static string PortraitIdle(int raceId) => Inv($"portrait.{raceId}.idle");

    /// <summary>A monster's attack frame (III Amiga: the picture's second frame, FORMAT.md §6).</summary>
    public static string MonsterAttacking(int monsterId) => Inv($"monster.{monsterId}.attacking");

    /// <summary>A race portrait's attack frame (III Amiga).</summary>
    public static string PortraitAttacking(int raceId) => Inv($"portrait.{raceId}.attacking");

    /// <summary>A summoned elemental's attack frame (III Amiga picture 22's second frame).</summary>
    public const string PortraitElementalAttacking = "portrait.elemental.attacking";
    public static string OverworldRegion(int regionId) => Inv($"overworld.region.{regionId}");
    public static string OverworldIcon(string kind) => $"overworld.icon.{kind}";
    public static string OverworldPattern(int pattern) => Inv($"overworld.pattern.{pattern}");
    /// <summary>Dungeon map glyph by its DOS glyph id (<c>0xe6..0xed</c>, written in decimal: <c>dungeon.glyph.230</c>).</summary>
    public static string DungeonGlyph(int glyph) => Inv($"dungeon.glyph.{glyph}");

    /// <summary>
    /// One overworld tile by map byte (packs that draw the map from tiles, <see cref="TileMapLayout"/>; Phantasie III
    /// Amiga: <c>phantasy.set</c>'s 250 tiles).
    /// </summary>
    public static string OverworldTile(int tile) => Inv($"overworld.tile.{tile}");

    /// <summary>A tile-drawn dungeon map's glyph by the pack's own glyph number (<see cref="TileMapLayout.DungeonGlyphFor"/>).</summary>
    public static string DungeonCell(int glyph) => Inv($"dungeon.cell.{glyph}");

    /// <summary>The picture a scroll item shows instead of text (Phantasie III's Scroll IV, item 126: the map of Scandor).</summary>
    public static string ScrollPicture(int itemId) => Inv($"scroll.picture.{itemId}");

    /// <summary>
    /// The portrait of a summoned elemental (party slot 7). Phantasie III Amiga: race byte 16 → heros.cmp picture 22
    /// (fight set-up <c>0x14fb6</c>, table <c>0x239a0</c>); a pack without it draws no portrait there.
    /// </summary>
    public const string PortraitElemental = "portrait.elemental.idle";

    /// <summary>
    /// The grave Phantasie III's defeat scene draws in a DESTROYED member's slot (Amiga heros.cmp x 128, y 96: sprite 16
    /// of <c>0x1b2b2</c>, race byte 22 at <c>0x1b6be</c>).
    /// </summary>
    public const string PortraitGrave = "portrait.grave";

    /// <summary>
    /// Phantasie III's full-screen scenes (Amiga <c>D/W&lt;n&gt;.cmp</c>): the party's defeat at the gates of the Astral
    /// Plane (w2, root <c>0x1dc16</c>), and the reward scene's god for a good win, Zeus (w1), or an evil one, Pluto (w0)
    /// (<c>0x1ee82</c>: fight outcome 2 → w1, 3 → w0).
    /// </summary>
    public const string SceneAstralPlane = "scene.astral-plane";
    public const string SceneRewardGood = "scene.reward.good";
    public const string SceneRewardEvil = "scene.reward.evil";

    /// <summary>One body-area figure piece by the pack's shape slot (<see cref="BodyFigureLayout"/>).</summary>
    public static string BodyPiece(int slot) => Inv($"body.piece.{slot}");

    /// <summary>One text-scroll frame piece by the pack's shape slot (<see cref="ScrollFrameLayout"/>).</summary>
    public static string ScrollFramePiece(int slot) => Inv($"scroll.frame.{slot}");

    /// <summary>
    /// A dialog button (Phantasie III Amiga <c>dlgbttns.cmp</c>: ok, cancel, more, yes, no); <paramref name="chosen"/> = its
    /// second state.
    /// </summary>
    public static string Button(string name, bool chosen = false) => chosen ? $"button.{name}.chosen" : $"button.{name}";

    private static string Inv(FormattableString s) => s.ToString(CultureInfo.InvariantCulture);
}

/// <summary>
/// A hardware colour cycle (the ST's VBL routine <c>0x1382e</c>, FORMAT.md §10.1): every <see cref="PeriodVbl"/>
/// vertical blanks the colour of register <c>i</c> moves to register <c>i + 1</c> (the last wraps to the first).
/// <see cref="Colours"/> are the registers' stored colours (<c>#RRGGBB</c>), in cycle order. It runs on the pictures whose
/// names start with one of <see cref="Pictures"/> (the ST: the overworld only).
/// </summary>
public sealed record ColourCycle(IReadOnlyList<string> Colours, int PeriodVbl, int VblHz, IReadOnlyList<string> Pictures)
{
    /// <summary>How many steps the cycle has taken after <paramref name="seconds"/>, modulo the register count.</summary>
    public int Phase(double seconds) =>
        (int)((long)Math.Floor(seconds * VblHz / PeriodVbl) % Colours.Count);

    /// <summary>
    /// The colour a pixel stored as <c>Colours[j]</c> shows after <paramref name="phase"/> steps:
    /// <c>Colours[j − phase]</c> (register j now holds what was in the register <paramref name="phase"/> places before).
    /// </summary>
    public string Shown(int j, int phase)
    {
        var n = Colours.Count;
        return Colours[((j - phase) % n + n) % n];
    }

    /// <summary>True when picture <paramref name="name"/> takes part in the cycle.</summary>
    public bool Covers(string name) => Pictures.Any(p => name.StartsWith(p, StringComparison.Ordinal));

    /// <summary>
    /// s153 (<c>"runs_while": "input_wait"</c>): the cycle steps only while the map waits for input, not from a VBL. I Amiga
    /// calls its rotation (h77 <c>+0x178</c>) only from the input wait loop (h80 <c>+0x1ac</c>, when no IDCMP message
    /// waits), so it pauses while anything else runs (a box, a requester, a step). False: it runs all the time (the ST's VBL
    /// routine).
    /// </summary>
    public bool InputWaitOnly { get; init; }

    /// <summary>
    /// s153: the seconds the original's step clock keeps (h80 <c>+0xd2a</c> reads only CurrentTime's seconds mod 8: the
    /// elapsed time comes out negative, compared unsigned, so a step comes early at each multiple); 0 = no wrap.
    /// </summary>
    public double ClockWrapSeconds { get; init; }

    /// <summary>The time between two steps: <see cref="PeriodVbl"/> / <see cref="VblHz"/> seconds.</summary>
    public double StepSeconds => (double)PeriodVbl / VblHz;

    /// <summary>A time whose <see cref="Phase"/> is <paramref name="steps"/> steps (for a cycle counted by steps).</summary>
    public double SecondsAtStep(long steps) => (steps + 0.5) * StepSeconds;
}

/// <summary>
/// Where the town screen's parts sit on the pack's full-screen town picture (s98, user: never crop scenes): the picture is
/// drawn 1:1 over the whole screen, the remaster's menu bar over its top <see cref="BarRows"/> rows (the original's own bar
/// rows, baked into the Amiga and ST pictures, blank in the DOS one), the town name centred on <see cref="NameX"/> at <see cref="NameY"/> in
/// <see cref="NameColour"/> (the DOS name line at the bottom; the ST's name scroll, <c>0x4082</c>). A message shows on a
/// one-line band from row <see cref="MessageY"/> down (its text one row lower) on the picture's own blank rows: the ST's
/// message line (<c>0x4082</c>, text at y 192); the Amiga I name line (y 185 under the picture, <c>h80:0x818</c> prints
/// the name and "LEAVING %s" there); the DOS bottom line (s49 capture: "GOLD CARRIED 295" in place of PELNOR). On the
/// Amiga I and DOS lines the message covers the name while it shows. Null: the picture has no blank rows to spare and the band goes over the picture's bottom (or above a name
/// down there).
/// <see cref="Doors"/>: where each building's open door sits (top-left of a <see cref="DoorSize"/> square, the same place
/// on <see cref="PictureNames.TownDoors"/> and on the street), keyed by place: bank, mystic, armory, inn, guild. Going
/// in flashes it for <see cref="DoorSeconds"/> (III Amiga <c>0xacc8</c>, s102); null: the pack has no door pieces.
/// Manifest <c>town_screen</c>; packs without one get <see cref="Default"/>.
/// </summary>
public sealed record TownScreenLayout(int BarRows, int NameX, int NameY, string NameColour, int? MessageY = null,
    IReadOnlyDictionary<string, (int X, int Y)>? Doors = null, int DoorSize = 48, double DoorSeconds = 0)
{
    /// <summary>The DOS layout: a 9-row bar, the name on the bottom line in white, a message on the same line.</summary>
    public static TownScreenLayout Default { get; } = new(9, 160, 191, "#FFFFFF", 191);

    /// <summary>
    /// Text cells of <see cref="NameCell"/> pixels the name is centred in, or null (exact pixel centring). III Amiga
    /// (<c>0x7e54</c>): 12 cells from cell 14, the name at cell <c>14 + (12 − len) / 2</c>, so an odd gap puts it half a
    /// cell left of the exact centre (<see cref="NameShift"/>).
    /// </summary>
    public int? NameCells { get; init; }

    /// <summary>The text cell width in pixels for <see cref="NameCells"/> (III Amiga: 8).</summary>
    public int NameCell { get; init; } = 8;

    /// <summary>
    /// The width in characters of the original's name field, or null (the name as it is). I Amiga (s120 capture,
    /// <c>h80:0x818</c>): the town record's name (+280) is 10 characters padded with spaces and the whole field is
    /// centred, so the name is padded to this width before it is centred (<see cref="FieldName"/>): PELNOR's left edge
    /// at 130, not 142.
    /// </summary>
    public int? NameField { get; init; }

    /// <summary><paramref name="name"/> padded with spaces to <see cref="NameField"/> characters (as is without one).</summary>
    public string FieldName(string name) => NameField is { } width ? name.PadRight(width) : name;

    /// <summary>
    /// The font of the name and the messages on the town picture's line (a font id of the pack's <c>fonts/</c>), or null
    /// (the pack's own font). I Amiga (s135): <c>h80:0x818</c> prints them with Text() on the window's RastPort, which
    /// carries <c>z6font</c> 6 (FORMAT.md "Fonts"; PELNOR pixel-equal on an exact FS-UAE shot). Drawn behind AMIGA SCREENS.
    /// </summary>
    public string? Font { get; init; }

    /// <summary>The <see cref="Font"/>'s file next to the manifest, or null when there is none.</summary>
    public string? FontFile { get; init; }

    /// <summary>
    /// s154: the colour of "LEAVING %s" on the town line when the party leaves (I Amiga h29 <c>+0x168</c>: h80
    /// <c>+0x818(text, 4)</c>, pen 4 of the town palette <c>0x1947C</c> = <c>EE0</c>), or null when the pack does not print it.
    /// The name inside it keeps its <see cref="NameField"/> padding (x 106 for PELNOR, shot s153).
    /// </summary>
    public string? LeavingColour { get; init; }

    /// <summary>
    /// s154: how long the leaving line holds the town picture before the map (the Amiga has no wait of its own: the line
    /// stays while the overworld loads; a filmed time), or 0.
    /// </summary>
    public double LeavingSeconds { get; init; }

    /// <summary>
    /// s156: how long the whole screen stays black after the leaving line, before the map (I Amiga: 2.14 / 2.35 s in
    /// s154's two leaves, the overworld's load from disk likely `[~]`; the mean), or 0. Keys and clicks wait meanwhile.
    /// </summary>
    public double LeavingBlackSeconds { get; init; }

    /// <summary>What the leave shows <paramref name="elapsed"/> seconds after it began: the line, the black, or nothing (the map).</summary>
    public LeavingPhase LeavingAt(double elapsed) =>
        elapsed < 0 ? LeavingPhase.Map
        : elapsed < LeavingSeconds ? LeavingPhase.Line
        : elapsed < LeavingSeconds + LeavingBlackSeconds ? LeavingPhase.Black
        : LeavingPhase.Map;

    /// <summary>
    /// Pixels the original's cell centring moves a name of <paramref name="length"/> characters from the exact centre
    /// (C's truncating division: −half a cell for an odd gap, +half a cell for an odd overflow); 0 without
    /// <see cref="NameCells"/>.
    /// </summary>
    public int NameShift(int length) =>
        NameCells is not { } cells ? 0 : NameCell * ((cells - length) / 2) - NameCell * (cells - length) / 2;

    /// <summary>
    /// Towns whose street shows in <see cref="DimPalette"/> instead of the picture's own <see cref="DimFrom"/> colours,
    /// with the name in <see cref="DimNameColour"/> (III Amiga towns 16–17: the fade to <c>0x23050</c> instead of
    /// <c>0x24010</c>, name pen 0; <c>0x78ee</c> / <c>0xb228</c>). Empty: none.
    /// </summary>
    public IReadOnlyList<int> DimTowns { get; init; } = [];
    public string? DimFrom { get; init; }
    public string? DimPalette { get; init; }
    public string? DimNameColour { get; init; }

    /// <summary>
    /// The name's colour per town number, overriding <see cref="NameColour"/> (manifest <c>name_colours</c>; empty: one
    /// colour for every town). II ST (s109 live, every town 1–10): <c>0x4082</c> sets vst_color to the town number (town 3
    /// → pen 11, town 9 → pen 13), and TOS's VDI pen → register map turns that into a <c>town</c> palette colour. Key 0 is
    /// a fresh new game (<see cref="NameColourFor"/>).
    /// </summary>
    public IReadOnlyDictionary<int, string> NameColours { get; init; } = System.Collections.Immutable.ImmutableDictionary<int, string>.Empty;

    /// <summary>
    /// The name colour for <paramref name="townId"/>; <paramref name="freshStart"/> (a new game's first town, before
    /// any town entry) looks up town 0 (II ST: the town global is still 0, pen 0 = black).
    /// </summary>
    public string NameColourFor(int? townId, bool freshStart = false) =>
        NameColours.TryGetValue(freshStart ? 0 : townId ?? 0, out var c) ? c : NameColour;

    /// <summary>
    /// The name's centre x on a fresh new game (manifest <c>fresh_name_x</c>; null: <see cref="NameX"/>). II ST: the
    /// banner's per-town nudge (<c>0x4082</c>) reads the same town global, which is 0 on a new turn, so it takes "the
    /// rest" (5) and PIPPACOTT's ink centre sits at 151 instead of 156 (s109 live, s78's reading).
    /// </summary>
    public int? FreshNameX { get; init; }

    /// <summary>The name's centre x: <see cref="FreshNameX"/> on a fresh new game, else <see cref="NameX"/>.</summary>
    public int NameXFor(bool freshStart) => freshStart && FreshNameX is int x ? x : NameX;
}

/// <summary>One picture of a <see cref="GraphicsPack.Scenes"/> entry: a picture name at an offset (pack pixels).</summary>
public sealed record ScenePicture(string Picture, int X, int Y);

/// <summary>A font a pack names for one place on screen (<see cref="GraphicsPack.RoleFonts"/>): its id and its file, or null.</summary>
public sealed record PackFont(string Id, string? File);

/// <summary>
/// How a pack's pictures sit on the screen: the original's resolution, pixel shape (<c>PixelAspectX:PixelAspectY</c>,
/// width : height of one pixel) and the colour behind everything. The remaster's shared layout is authored for
/// <see cref="Default"/>; packs declare their own values and <c>VirtualScreen</c> applies them.
/// </summary>
public sealed record ScreenSpec(int Width, int Height, int PixelAspectX, int PixelAspectY, string Background)
{
    /// <summary>320×200, square pixels, background #10121C — the remaster layout before T1.5.</summary>
    public static ScreenSpec Default { get; } = new(320, 200, 1, 1, "#10121C");
}

/// <summary>
/// One graphics pack: the pictures of one game on one platform (T1.5), from a manifest under
/// <c>GameData/&lt;game&gt;/graphics/</c>. Images live outside GameData (the extracts under <c>reverse-engineering/</c>);
/// <see cref="RootDirectory"/> is where they were found. Pure file lookup — no rendering.
/// </summary>
public sealed class GraphicsPack
{
    public required string Id { get; init; }
    public required string Game { get; init; }
    public required string Platform { get; init; }
    public required string Name { get; init; }
    /// <summary>False for packs only an explicit choice may select (e.g. DOS CGA); never picked as the default.</summary>
    public required bool AutoSelect { get; init; }
    public required string RootDirectory { get; init; }
    public required ScreenSpec Screen { get; init; }
    public required IReadOnlyList<string> Palette { get; init; }
    /// <summary>
    /// The original's named palettes when it switched them per screen (the ST: <c>start</c>, <c>town</c>, <c>map</c>,
    /// <c>combat</c>); documentation and the <see cref="ColourCycle"/>'s source. Unlike <see cref="Palette"/> they are
    /// never forced on the frame: each picture already holds its own screen's colours.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> Palettes { get; init; } =
        new Dictionary<string, IReadOnlyList<string>>();
    /// <summary>The platform's hardware colour cycle (the ST overworld's water), or null.</summary>
    public ColourCycle? ColourCycle { get; init; }
    /// <summary>Font id the pack's text uses (<c>remaster-5x7</c>, built into the Client, is the default).</summary>
    public required string Font { get; init; }
    /// <summary>
    /// The pack's font as data, <c>graphics/fonts/&lt;Font&gt;.json</c> next to the manifest (<see cref="FontDefinition"/>),
    /// or null when there is no such file (a built-in font, or a font this game cannot draw).
    /// </summary>
    public string? FontFile { get; init; }
    /// <summary>
    /// The original's own fonts for single places on screen (<c>"fonts"</c>: role → font id of the pack's <c>fonts/</c>;
    /// IV X68000, s135: <c>p4.combat.exit</c> = P4.X font 0 in its italic style, <c>p4.mystic.bubble</c> = font 0), with
    /// the file each id names (null when there is none). A role the pack leaves out is drawn in <see cref="Font"/>.
    /// </summary>
    public IReadOnlyDictionary<string, PackFont> RoleFonts { get; init; } = new Dictionary<string, PackFont>();
    /// <summary>
    /// The original's icon menus (<c>"command_panels"</c>: a file next to the manifest; IV X68000, s136: <c>_mkicon</c>'s
    /// Command panel), or null when the pack has none.
    /// </summary>
    public CommandPanelSet? CommandPanels { get; init; }
    /// <summary>Amiga map geometry (cell sizes, icon cells) when the pack has one; null otherwise.</summary>
    public AmigaMapManifest? MapLayout { get; init; }
    /// <summary>
    /// s153 (<c>map_layout.party_hidden_under_prompts</c>): the overworld's party sprite is not drawn while a question is
    /// up over the map. I Amiga's party is a hardware sprite; every question there is an h86 requester (<c>+0x32a</c>
    /// menus: A TOWN / A DUNGEON / AN INN. ENTER?, HEARS MONSTERS, SLEEPING MONSTERS, KEEP IT?), and h86 <c>+0</c> and its
    /// wait <c>+0x132</c> MoveSprite the party to y 200 (off the screen) before <c>Request</c> and back after <c>EndRequest</c>.
    /// </summary>
    public bool PartyHiddenUnderPrompts { get; init; }
    /// <summary>Tile-drawn maps (overworld tile per map byte, dungeon glyph per cell) when the pack has them; null otherwise.</summary>
    public TileMapLayout? TileMaps { get; init; }
    /// <summary>The town screen's bar and name on the pack's town picture.</summary>
    public TownScreenLayout TownScreen { get; init; } = TownScreenLayout.Default;
    /// <summary>The body-area figure (Phantasie III's stick figure) when the pack draws one; null otherwise.</summary>
    public BodyFigureLayout? BodyFigure { get; init; }
    /// <summary>The frame text scrolls are read in (Phantasie III Amiga's parchment) when the pack has one; null otherwise.</summary>
    public ScrollFrameLayout? ScrollFrame { get; init; }
    /// <summary>The original's own keep-question windows (Phantasie III Amiga's OK / CANCEL windows) when the pack has them; null otherwise.</summary>
    public KeepWindowsLayout? KeepWindows { get; init; }
    /// <summary>What the original showed at start-up (the title splash, s102), or null when the pack has none.</summary>
    public TitleSequence? TitleSequence { get; init; }
    /// <summary>The gods' reward scene's flashes and tune (Phantasie III Amiga), or null when the pack has none.</summary>
    public RewardSceneLayout? RewardScene { get; init; }
    /// <summary>The sparkle drawn on a fight's spell targets (Phantasie III Amiga), or null when the pack has none.</summary>
    public CombatFlashLayout? CombatFlash { get; init; }
    /// <summary>The victory screen's fade, row and speech (Phantasie I Amiga, II ST), or null when the pack has none.</summary>
    public EndingSceneLayout? EndingScene { get; init; }

    /// <summary>I Amiga's party-defeat screens (<c>defeat_screen</c>, s153), or null.</summary>
    public DefeatScreenLayout? DefeatScreen { get; init; }

    /// <summary>Where I DOS's party-defeat pictures put the row and the line (<c>defeat_pictures</c>, s157), or null.</summary>
    public DefeatPicturesLayout? DefeatPictures { get; init; }

    /// <summary>I Amiga's sounds at moments outside its screens (<c>cue_sounds</c>, s153), or null.</summary>
    public CueSounds? CueSounds { get; init; }
    /// <summary>The original's combat screen (Phantasie I Amiga, s132), or null when the pack has none.</summary>
    public CombatFieldLayout? CombatField { get; init; }
    /// <summary>Requester 23 on the map screen (Phantasie I Amiga's wilderness encounter boxes, s139), or null when the pack has none.</summary>
    public MapMessageLayout? MapMessage { get; init; }
    /// <summary>s154: I Amiga's requesters 0 / 22 / 14 (the map's questions, the Black Knights, the character sheet), or null.</summary>
    public MapRequestersLayout? MapRequesters { get; init; }
    /// <summary>The original's sound effects (Phantasie III Amiga: streams for its music driver), or null when the pack has none.</summary>
    public Audio.P3SoundEffects? SoundEffects { get; init; }
    /// <summary>The original's sound effects when they are Dosound scripts (Phantasie I Amiga, s128), or null.</summary>
    public Audio.AmigaDosoundEffects? DosoundEffects { get; init; }
    /// <summary>The original's sound effects when TOS plays them as Dosound scripts (Phantasie II ST, s135), or null.</summary>
    public Audio.StDosoundEffects? StDosoundEffects { get; init; }
    /// <summary>s152: the original's ADPCM sound effects and the moments that play them (Phantasie IV X68000), or null.</summary>
    public Audio.X68kAdpcmEffects? AdpcmEffects { get; init; }
    /// <summary>The original's combat lines, sound and pacing per line (Phantasie II ST, s136), or null.</summary>
    public StCombatLines? StCombatLines { get; init; }
    /// <summary>s139: the sound each moment outside combat plays (<c>field_sounds</c>; Phantasie II ST), or null.</summary>
    public FieldSounds? FieldSounds { get; init; }
    /// <summary>s140: TRANSPORTATION's palette fade (<c>transport_fade</c>; Phantasie II ST <c>0x260a</c>), or null.</summary>
    public StTransportFade? TransportFade { get; init; }

    /// <summary>
    /// Screens drawn as a stack of the pack's pictures (s107, IV's town and building pictures): scene name → the pictures
    /// in drawing order, each at its offset in pack pixels from the scene's top-left. Empty when the pack has none.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyList<ScenePicture>> Scenes { get; init; } =
        new Dictionary<string, IReadOnlyList<ScenePicture>>();

    /// <summary>The pack's picture animations (s109, IV's <c>_setanim</c> / <c>_anima</c> tables), or null when it has none.</summary>
    public PictureAnimationSet? PictureAnimations { get; private set; }

    /// <summary>IV's location-map view cycling (<c>_ianim</c>, s120: cycles and measured pass costs), or null when the pack has none.</summary>
    public OpenPhantasie.Core.P4ViewAnimationRules? DungeonViewAnimation { get; private set; }

    private readonly Dictionary<string, string> _pictures = new(StringComparer.Ordinal);
    private readonly List<PictureSet> _sets = [];

    /// <summary>Every fixed picture name the pack maps (picture sets resolve on demand and are not listed).</summary>
    public IEnumerable<string> FixedPictureNames => _pictures.Keys;

    /// <summary>The full path of picture <paramref name="name"/>, or null when this pack has no such picture or its file is missing.</summary>
    public string? Resolve(string name)
    {
        if (_pictures.TryGetValue(name, out var rel))
            return Existing(rel);
        foreach (var set in _sets)
            if (set.Match(name) is int n)
                return set.Find(RootDirectory, n);
        return null;
    }

    private string? Existing(string relative)
    {
        if (string.IsNullOrEmpty(relative)) return null;
        var path = Path.Combine(RootDirectory, relative);
        return File.Exists(path) ? path : null;
    }

    internal static GraphicsPack FromDto(PackDto dto, string rootDirectory, string manifestPath)
    {
        var screen = dto.Screen is { } s
            ? new ScreenSpec(s.Width, s.Height, s.PixelAspect is [var ax, var ay] ? ax : 1,
                s.PixelAspect is [_, var ay2] ? ay2 : 1, s.Background ?? ScreenSpec.Default.Background)
            : ScreenSpec.Default;
        if (screen.Width <= 0 || screen.Height <= 0 || screen.PixelAspectX <= 0 || screen.PixelAspectY <= 0)
            throw new InvalidDataException($"{manifestPath}: bad screen spec.");

        foreach (var colour in dto.Palette ?? [])
            if (!HexColour.IsMatch(colour ?? ""))
                throw new InvalidDataException($"{manifestPath}: palette colour '{colour}' is not #RRGGBB.");
        foreach (var colour in (dto.Palettes ?? []).Values.SelectMany(p => p ?? []))
            if (!HexColour.IsMatch(colour ?? ""))
                throw new InvalidDataException($"{manifestPath}: palette colour '{colour}' is not #RRGGBB.");
        ColourCycle? cycle = null;
        if (dto.ColourCycle is { } cc)
        {
            var cyclePalette = cc.Palette is not null && dto.Palettes?.GetValueOrDefault(cc.Palette) is { } named
                ? named : dto.Palette ?? [];
            if (cc.Registers is not { Count: >= 2 } regs || regs.Any(r => r < 0 || r >= cyclePalette.Count) ||
                cc.PeriodVbl <= 0 || cc.VblHz <= 0)
                throw new InvalidDataException($"{manifestPath}: bad colour_cycle.");
            if (cc.RunsWhile is not (null or "always" or "input_wait") || cc.ClockWrapSeconds < 0)
                throw new InvalidDataException($"{manifestPath}: bad colour_cycle runs_while / clock_wrap_seconds.");
            cycle = new ColourCycle([.. regs.Select(r => cyclePalette[r])], cc.PeriodVbl, cc.VblHz, [.. cc.Pictures ?? []])
            {
                InputWaitOnly = cc.RunsWhile == "input_wait",
                ClockWrapSeconds = cc.ClockWrapSeconds,
            };
        }
        var font = dto.Font ?? "remaster-5x7";
        var fontFile = Path.Combine(Path.GetDirectoryName(manifestPath) ?? "", "fonts", font + ".json");

        AmigaMapManifest? layout = null;
        if (dto.MapLayout is { } ml)
        {
            if (ml.Format != "amiga-maps/v1")
                throw new InvalidDataException($"{manifestPath}: unknown map_layout format '{ml.Format}'.");
            layout = AmigaMapManifest.Load(Path.Combine(rootDirectory, ml.File));
        }

        var combatField = dto.CombatField is { } cfl ? CombatFieldLayout.FromDto(cfl, manifestPath) : null;
        var pack = new GraphicsPack
        {
            Id = Require(dto.Id, "id", manifestPath),
            Game = Require(dto.Game, "game", manifestPath),
            Platform = Require(dto.Platform, "platform", manifestPath),
            Name = dto.Name ?? dto.Id!,
            AutoSelect = dto.AutoSelect ?? true,
            RootDirectory = rootDirectory,
            Screen = screen,
            Palette = dto.Palette ?? [],
            Palettes = (dto.Palettes ?? []).ToDictionary(p => p.Key, p => (IReadOnlyList<string>)(p.Value ?? [])),
            ColourCycle = cycle,
            Font = font,
            FontFile = File.Exists(fontFile) ? fontFile : null,
            RoleFonts = (dto.Fonts ?? []).Where(kv => !string.IsNullOrWhiteSpace(kv.Value)).ToDictionary(kv => kv.Key,
                kv => new PackFont(kv.Value, CombatFieldLayout.FontFileFor(kv.Value, manifestPath)), StringComparer.Ordinal),
            CommandPanels = dto.CommandPanels is { Length: > 0 } cp
                ? CommandPanelSet.Load(Path.Combine(Path.GetDirectoryName(manifestPath) ?? "", cp)) : null,
            MapLayout = layout,
            PartyHiddenUnderPrompts = dto.MapLayout?.PartyHiddenUnderPrompts ?? false,
            TileMaps = dto.TileMaps is { } tm ? TileMapLayout.FromDto(tm, manifestPath) : null,
            TownScreen = dto.TownScreen is { } ts ? TownLayout(ts, manifestPath) : TownScreenLayout.Default,
            BodyFigure = dto.BodyFigure is { } bf ? BodyFigureLayout.FromDto(bf, manifestPath) : null,
            ScrollFrame = dto.ScrollFrame is { } sf ? ScrollFrameLayout.FromDto(sf, manifestPath) : null,
            KeepWindows = dto.KeepWindows is { } kw ? KeepWindowsLayout.FromDto(kw, manifestPath) : null,
            TitleSequence = dto.TitleSequence is { } title ? TitleSequence.FromDto(title, cycle?.VblHz, manifestPath, rootDirectory) : null,
            RewardScene = dto.RewardScene is { } reward ? RewardSceneLayout.FromDto(reward, cycle?.VblHz, manifestPath, rootDirectory) : null,
            CombatFlash = dto.CombatFlash is { } cf ? CombatFlashLayout.FromDto(cf, cycle?.VblHz, manifestPath) : null,
            EndingScene = dto.EndingScene is { } es ? EndingSceneLayout.FromDto(es, cycle?.VblHz, manifestPath) : null,
            DefeatScreen = dto.DefeatScreen is { } ds ? DefeatScreenLayout.FromDto(ds, manifestPath) : null,
            DefeatPictures = dto.DefeatPictures is { } dp ? DefeatPicturesLayout.FromDto(dp, manifestPath) : null,
            CueSounds = dto.CueSounds is { } cs ? Data.CueSounds.FromDto(cs, manifestPath) : null,
            CombatField = combatField,
            MapMessage = dto.MapMessage is { } mm ? MapMessageLayout.FromDto(mm, dto.Palettes, combatField, manifestPath) : null,
            MapRequesters = dto.MapRequesters is { } mr ? MapRequestersLayout.FromDto(mr, dto.Palettes, manifestPath) : null,
            SoundEffects = dto.SoundEffects is { } fx
                           && fx.Format is not (Audio.TuneFormats.AmigaDosound or Audio.TuneFormats.AtariStDosound
                               or Audio.X68kAdpcmEffects.Format)
                ? Audio.P3SoundEffects.FromDto(fx, cycle?.VblHz, manifestPath, rootDirectory) : null,
            DosoundEffects = dto.SoundEffects is { Format: Audio.TuneFormats.AmigaDosound } dfx
                ? Audio.AmigaDosoundEffects.FromDto(dfx, manifestPath, rootDirectory) : null,
            StDosoundEffects = dto.SoundEffects is { Format: Audio.TuneFormats.AtariStDosound } sfx
                ? Audio.StDosoundEffects.FromDto(sfx, manifestPath, rootDirectory) : null,
            AdpcmEffects = dto.SoundEffects is { Format: Audio.X68kAdpcmEffects.Format } afx
                ? Audio.X68kAdpcmEffects.FromDto(afx, manifestPath, rootDirectory) : null,
            StCombatLines = dto.CombatLines is { } cl ? StCombatLines.FromDto(cl, manifestPath, dto.Palettes) : null,
            FieldSounds = dto.FieldSounds is { } fs ? FieldSounds.FromDto(fs, manifestPath) : null,
            TransportFade = dto.TransportFade is { } tf ? StTransportFade.FromDto(tf, dto.Palettes, manifestPath) : null,
            Scenes = (dto.Scenes ?? []).ToDictionary(kv => kv.Key,
                kv => (IReadOnlyList<ScenePicture>)[.. (kv.Value ?? []).Select(p => new ScenePicture(p.Picture ?? "",
                    p.At is [var x, _] ? x : -1, p.At is [_, var y] ? y : -1))], StringComparer.Ordinal),
        };
        foreach (var (name, rel) in dto.Pictures ?? [])
            pack._pictures[name] = rel;
        foreach (var (scene, parts) in pack.Scenes)
            if (parts.Count == 0 || parts.Any(p => !pack._pictures.ContainsKey(p.Picture) || p.X < 0 || p.Y < 0))
                throw new InvalidDataException($"{manifestPath}: bad scene '{scene}'.");
        foreach (var set in dto.PictureSets ?? [])
            pack._sets.Add(new PictureSet(set, manifestPath));
        if (dto.PictureAnimations is { } anim)
            pack.PictureAnimations = PictureAnimationSet.FromDto(anim, pack._pictures, manifestPath);
        if (dto.DungeonViewAnimation is { } view)
            pack.DungeonViewAnimation = DungeonViewAnimationLoader.FromDto(view, manifestPath);

        // The map layout's images become named pictures (their paths are relative to the layout file's folder).
        if (layout is not null)
        {
            var dir = Path.GetDirectoryName(dto.MapLayout!.File) ?? "";
            string Rel(string p) => Path.Combine(dir, p);
            foreach (var (id, region) in layout.Regions)
                pack._pictures.TryAdd(PictureNames.OverworldRegion(id), Rel(region.Image));
            foreach (var (kind, image) in layout.IconImages)
                pack._pictures.TryAdd(PictureNames.OverworldIcon(kind), Rel(image));
            pack._pictures.TryAdd(PictureNames.OverworldParty, Rel(layout.PartyImage));
            foreach (var (pattern, image) in layout.PatternImages)
                pack._pictures.TryAdd(PictureNames.OverworldPattern(pattern), Rel(image));
            foreach (var (glyph, image) in layout.GlyphImages)
                pack._pictures.TryAdd(PictureNames.DungeonGlyph(glyph), Rel(image));
            pack._pictures.TryAdd(PictureNames.DungeonCompass, Rel(layout.CompassImage));
        }

        return pack;
    }

    private static readonly Regex HexColour = new("^#[0-9A-Fa-f]{6}$");

    private static string Require(string? value, string field, string path) =>
        string.IsNullOrWhiteSpace(value) ? throw new InvalidDataException($"{path}: '{field}' missing.") : value;

    /// <summary>
    /// A numbered family of pictures in one folder: <c>monster.{n}.idle</c> → the first file (ordinal order) in
    /// <c>dir</c> whose name starts with <c>prefix</c> (e.g. <c>{n:D2}-</c>) and ends with <c>ext</c>.
    /// </summary>
    private sealed class PictureSet
    {
        private readonly Regex _name;
        private readonly string _dir;
        private readonly string _prefix;
        private readonly string _ext;

        public PictureSet(PictureSetDto dto, string manifestPath)
        {
            if (string.IsNullOrEmpty(dto.Name) || !dto.Name.Contains("{n}", StringComparison.Ordinal))
                throw new InvalidDataException($"{manifestPath}: picture set name needs '{{n}}'.");
            _name = new Regex("^" + Regex.Escape(dto.Name).Replace(@"\{n}", "(\\d+)", StringComparison.Ordinal) + "$");
            _dir = dto.Dir ?? "";
            _prefix = dto.Prefix ?? "{n}";
            _ext = dto.Ext ?? ".png";
        }

        public int? Match(string name)
        {
            var m = _name.Match(name);
            return m.Success && int.TryParse(m.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var n)
                ? n : null;
        }

        public string? Find(string root, int n)
        {
            var dir = Path.Combine(root, _dir);
            if (!Directory.Exists(dir)) return null;
            var prefix = string.Format(CultureInfo.InvariantCulture, _prefix.Replace("{n", "{0", StringComparison.Ordinal), n);
            return Directory.EnumerateFiles(dir, "*" + _ext)
                .Where(f => Path.GetFileName(f).StartsWith(prefix, StringComparison.Ordinal))
                .OrderBy(f => f, StringComparer.Ordinal)
                .FirstOrDefault();
        }
    }

    private static TownScreenLayout TownLayout(TownScreenDto dto, string manifestPath)
    {
        var colour = dto.NameColour ?? TownScreenLayout.Default.NameColour;
        if (dto.BarRows is < 0 or > 40 || dto.NameY is < 0 or > 199 || (dto.NameX ?? 160) is < 0 or > 319 || !HexColour.IsMatch(colour)
            || dto.MessageY is < 1 or > 192) // the band's first row; its text one row lower
            throw new InvalidDataException($"{manifestPath}: bad town_screen.");
        Dictionary<string, (int X, int Y)>? doors = null;
        if (dto.Doors is { } d)
        {
            var size = dto.DoorSize ?? 48;
            if (size is < 1 or > 200 || dto.DoorSeconds is null or < 0 or > 10
                || d.Values.Any(p => p.Length != 2 || p[0] < 0 || p[1] < 0 || p[0] + size > 320 || p[1] + size > 200))
                throw new InvalidDataException($"{manifestPath}: bad town_screen.doors.");
            doors = d.ToDictionary(kv => kv.Key, kv => (kv.Value[0], kv.Value[1]), StringComparer.Ordinal);
        }
        if (dto.NameCells is < 1 or > 40 || dto.NameCell is < 1 or > 16 || dto.NameField is < 1 or > 40
            || (dto.DimNameColour is { } dim && !HexColour.IsMatch(dim)) || dto.FreshNameX is < 0 or > 319
            || (dto.DimTowns is { Count: > 0 } && (dto.DimPalette is null || dto.DimFrom is null))
            || (dto.LeavingColour is { } leaving && !HexColour.IsMatch(leaving)) || dto.LeavingSeconds is < 0 or > 30
            || dto.LeavingBlackSeconds is < 0 or > 30)
            throw new InvalidDataException($"{manifestPath}: bad town_screen.");
        var nameColours = new Dictionary<int, string>();
        foreach (var (key, ink) in dto.NameColours ?? [])
        {
            if (!int.TryParse(key, NumberStyles.None, CultureInfo.InvariantCulture, out var town) || town > 255 || !HexColour.IsMatch(ink))
                throw new InvalidDataException($"{manifestPath}: bad town_screen.name_colours.");
            nameColours[town] = ink;
        }
        return new TownScreenLayout(dto.BarRows, dto.NameX ?? 160, dto.NameY, colour, dto.MessageY,
            doors, dto.DoorSize ?? 48, dto.DoorSeconds ?? 0)
        {
            NameCells = dto.NameCells, NameCell = dto.NameCell ?? 8, NameField = dto.NameField, DimTowns = dto.DimTowns ?? [],
            DimFrom = dto.DimFrom, DimPalette = dto.DimPalette, DimNameColour = dto.DimNameColour,
            NameColours = nameColours.Count > 0 ? nameColours : TownScreenLayout.Default.NameColours,
            FreshNameX = dto.FreshNameX,
            Font = dto.Font,
            FontFile = CombatFieldLayout.FontFileFor(dto.Font, manifestPath),
            LeavingColour = dto.LeavingColour,
            LeavingSeconds = dto.LeavingSeconds ?? 0,
            LeavingBlackSeconds = dto.LeavingBlackSeconds ?? 0,
        };
    }

    // --- manifest DTOs (GameData/<game>/graphics/<pack>.json) ---

    internal sealed class ScenePictureDto
    {
        public string? Picture { get; set; }
        public int[]? At { get; set; }
    }

    internal sealed class PackDto
    {
        public string? Id { get; set; }
        public string? Game { get; set; }
        public string? Platform { get; set; }
        public string? Name { get; set; }
        [JsonPropertyName("auto_select")] public bool? AutoSelect { get; set; }
        public string? Root { get; set; }
        public List<string>? Requires { get; set; }
        public ScreenDto? Screen { get; set; }
        public List<string>? Palette { get; set; }
        public Dictionary<string, List<string>>? Palettes { get; set; }
        [JsonPropertyName("colour_cycle")] public ColourCycleDto? ColourCycle { get; set; }
        public string? Font { get; set; }
        public Dictionary<string, string>? Fonts { get; set; }
        [JsonPropertyName("command_panels")] public string? CommandPanels { get; set; }
        [JsonPropertyName("map_layout")] public MapLayoutDto? MapLayout { get; set; }
        [JsonPropertyName("tile_maps")] public TileMapLayout.Dto? TileMaps { get; set; }
        [JsonPropertyName("town_screen")] public TownScreenDto? TownScreen { get; set; }
        [JsonPropertyName("body_figure")] public BodyFigureLayout.Dto? BodyFigure { get; set; }
        [JsonPropertyName("scroll_frame")] public ScrollFrameLayout.Dto? ScrollFrame { get; set; }
        [JsonPropertyName("keep_windows")] public KeepWindowsLayout.Dto? KeepWindows { get; set; }
        [JsonPropertyName("title_sequence")] public TitleSequence.Dto? TitleSequence { get; set; }
        [JsonPropertyName("reward_scene")] public RewardSceneLayout.Dto? RewardScene { get; set; }
        [JsonPropertyName("combat_flash")] public CombatFlashLayout.Dto? CombatFlash { get; set; }
        [JsonPropertyName("ending_scene")] public EndingSceneLayout.Dto? EndingScene { get; set; }
        [JsonPropertyName("defeat_screen")] public DefeatScreenLayout.Dto? DefeatScreen { get; set; }
        [JsonPropertyName("defeat_pictures")] public DefeatPicturesLayout.Dto? DefeatPictures { get; set; }
        [JsonPropertyName("cue_sounds")] public CueSounds.Dto? CueSounds { get; set; }
        [JsonPropertyName("combat_field")] public CombatFieldLayout.Dto? CombatField { get; set; }
        [JsonPropertyName("map_message")] public MapMessageLayout.Dto? MapMessage { get; set; }
        [JsonPropertyName("map_requesters")] public MapRequestersLayout.Dto? MapRequesters { get; set; }
        [JsonPropertyName("sound_effects")] public Audio.P3SoundEffects.Dto? SoundEffects { get; set; }
        [JsonPropertyName("combat_lines")] public StCombatLines.Dto? CombatLines { get; set; }
        [JsonPropertyName("field_sounds")] public FieldSounds.Dto? FieldSounds { get; set; }
        [JsonPropertyName("transport_fade")] public StTransportFade.Dto? TransportFade { get; set; }
        public Dictionary<string, string>? Pictures { get; set; }
        [JsonPropertyName("picture_sets")] public List<PictureSetDto>? PictureSets { get; set; }
        public Dictionary<string, List<ScenePictureDto>>? Scenes { get; set; }
        [JsonPropertyName("picture_animations")] public PictureAnimationSet.Dto? PictureAnimations { get; set; }
        [JsonPropertyName("dungeon_view_animation")] public DungeonViewAnimationLoader.Dto? DungeonViewAnimation { get; set; }
    }

    internal sealed class ScreenDto
    {
        public int Width { get; set; }
        public int Height { get; set; }
        [JsonPropertyName("pixel_aspect")] public int[]? PixelAspect { get; set; }
        public string? Background { get; set; }
    }

    internal sealed class TownScreenDto
    {
        [JsonPropertyName("bar_rows")] public int BarRows { get; set; }
        [JsonPropertyName("name_x")] public int? NameX { get; set; }
        [JsonPropertyName("name_y")] public int NameY { get; set; }
        [JsonPropertyName("name_colour")] public string? NameColour { get; set; }
        [JsonPropertyName("message_y")] public int? MessageY { get; set; }
        public Dictionary<string, int[]>? Doors { get; set; }
        [JsonPropertyName("door_size")] public int? DoorSize { get; set; }
        [JsonPropertyName("door_seconds")] public double? DoorSeconds { get; set; }
        [JsonPropertyName("name_cells")] public int? NameCells { get; set; }
        [JsonPropertyName("name_cell")] public int? NameCell { get; set; }
        [JsonPropertyName("name_field")] public int? NameField { get; set; }
        [JsonPropertyName("dim_towns")] public List<int>? DimTowns { get; set; }
        [JsonPropertyName("dim_from")] public string? DimFrom { get; set; }
        [JsonPropertyName("dim_palette")] public string? DimPalette { get; set; }
        [JsonPropertyName("dim_name_colour")] public string? DimNameColour { get; set; }
        [JsonPropertyName("name_colours")] public Dictionary<string, string>? NameColours { get; set; }
        [JsonPropertyName("fresh_name_x")] public int? FreshNameX { get; set; }
        public string? Font { get; set; }
        [JsonPropertyName("leaving_colour")] public string? LeavingColour { get; set; }
        [JsonPropertyName("leaving_seconds")] public double? LeavingSeconds { get; set; }
        [JsonPropertyName("leaving_black_seconds")] public double? LeavingBlackSeconds { get; set; }
    }

    internal sealed class MapLayoutDto
    {
        public string Format { get; set; } = "";
        public string File { get; set; } = "";
        [JsonPropertyName("party_hidden_under_prompts")] public bool PartyHiddenUnderPrompts { get; set; }
    }

    internal sealed class ColourCycleDto
    {
        public string? Palette { get; set; }
        public List<int>? Registers { get; set; }
        [JsonPropertyName("period_vbl")] public int PeriodVbl { get; set; }
        [JsonPropertyName("vbl_hz")] public int VblHz { get; set; }
        public List<string>? Pictures { get; set; }
        [JsonPropertyName("runs_while")] public string? RunsWhile { get; set; }
        [JsonPropertyName("clock_wrap_seconds")] public double ClockWrapSeconds { get; set; }
    }

    internal sealed class PictureSetDto
    {
        public string? Name { get; set; }
        public string? Dir { get; set; }
        public string? Prefix { get; set; }
        public string? Ext { get; set; }
    }
}

/// <summary>
/// How a pack draws maps from tiles, as Phantasie III's Amiga version does (<c>reverse-engineering/amiga/phantasie3/FORMAT.md</c>
/// §4–5): the overworld as a <see cref="ViewColumns"/> × <see cref="ViewRows"/> window of
/// <see cref="PictureNames.OverworldTile"/> pictures (tile = map byte) with <see cref="PictureNames.OverworldParty"/>
/// over the party's cell, and the dungeon as one <see cref="PictureNames.DungeonCell"/> glyph per cell, chosen from the
/// cell's map byte by <see cref="DungeonGlyphFor"/> (a cell not yet seen draws <see cref="UnseenGlyph"/>).
/// </summary>
public sealed record TileMapLayout(
    int TileWidth, int TileHeight, int ViewColumns, int ViewRows,
    int GlyphWidth, int GlyphHeight, int UnseenGlyph, int PartyGlyph, int DefaultGlyph,
    IReadOnlyList<TileMapLayout.GlyphRange> GlyphRanges)
{
    /// <summary>Map bytes <see cref="Min"/>..<see cref="Max"/> (inclusive) draw glyph <see cref="Glyph"/>.</summary>
    public sealed record GlyphRange(int Min, int Max, int Glyph);

    /// <summary>
    /// Bytes the Amiga writes past a map's last cell and shows when its view runs past the map: in region
    /// <see cref="Region"/>, map index <see cref="Index"/> (row · width + column) holds tile <see cref="Tile"/> (III: the
    /// Netherworld's 496 / 497 := 0xF9, <c>0x1ec62</c>).
    /// </summary>
    public sealed record PastMapCell(int Region, int Index, int Tile);

    /// <summary>The pack's <see cref="PastMapCell"/>s (none: every cell past a map is drawn black).</summary>
    public IReadOnlyList<PastMapCell> PastMap { get; init; } = [];

    /// <summary>
    /// The size counters the Amiga's overworld view runs on in region <c>key</c> (III <c>0x24098</c> / <c>0x24096</c>,
    /// set by the plane change <c>0x1eb18</c>, which the boot also runs: Scandor 50 × 74, the Netherworld 31 × 15), as
    /// (columns, rows). The view's centre stops at counter − ⌊view / 2⌋, so its last column / row is the counter's
    /// (FORMAT.md §4); a region not listed keeps the view on its map.
    /// </summary>
    public IReadOnlyDictionary<int, (int Columns, int Rows)> ViewCounters { get; init; } =
        new Dictionary<int, (int Columns, int Rows)>();

    /// <summary>The glyph of a seen dungeon cell holding map byte <paramref name="cell"/>: the first matching range, else the default.</summary>
    public int DungeonGlyphFor(int cell)
    {
        foreach (var r in GlyphRanges)
            if (cell >= r.Min && cell <= r.Max)
                return r.Glyph;
        return DefaultGlyph;
    }

    internal static TileMapLayout FromDto(Dto dto, string manifestPath)
    {
        var tile = dto.OverworldTile ?? [];
        var view = dto.OverworldView ?? [];
        var glyph = dto.DungeonGlyph ?? [];
        if (tile is not [> 0, > 0] || view is not [> 0, > 0] || glyph is not [> 0, > 0])
            throw new InvalidDataException(
                $"{manifestPath}: tile_maps needs overworld_tile, overworld_view and dungeon_glyph as [w, h].");
        var ranges = (dto.DungeonCells ?? []).Select(r => r is [var lo, var hi, var g] && lo <= hi
            ? new GlyphRange(lo, hi, g)
            : throw new InvalidDataException($"{manifestPath}: tile_maps dungeon_cells entries are [min, max, glyph]."))
            .ToList();
        var past = (dto.PastMap ?? []).Select(r => r is [var region, var index, var t]
            ? new PastMapCell(region, index, t)
            : throw new InvalidDataException($"{manifestPath}: tile_maps past_map entries are [region, index, tile]."))
            .ToList();
        var counters = (dto.ViewCounters ?? []).ToDictionary(
            r => r is [var region, > 0, > 0] ? region
                : throw new InvalidDataException($"{manifestPath}: tile_maps view_counters entries are [region, columns, rows]."),
            r => (r[1], r[2]));
        return new TileMapLayout(tile[0], tile[1], view[0], view[1], glyph[0], glyph[1],
            dto.DungeonUnseen, dto.DungeonParty, dto.DungeonDefault, ranges) { PastMap = past, ViewCounters = counters };
    }

    internal sealed class Dto
    {
        [JsonPropertyName("overworld_tile")] public int[]? OverworldTile { get; set; }
        [JsonPropertyName("overworld_view")] public int[]? OverworldView { get; set; }
        [JsonPropertyName("dungeon_glyph")] public int[]? DungeonGlyph { get; set; }
        [JsonPropertyName("dungeon_unseen")] public int DungeonUnseen { get; set; }
        [JsonPropertyName("dungeon_party")] public int DungeonParty { get; set; }
        [JsonPropertyName("dungeon_default")] public int DungeonDefault { get; set; }
        [JsonPropertyName("dungeon_cells")] public List<int[]>? DungeonCells { get; set; }
        [JsonPropertyName("past_map")] public List<int[]>? PastMap { get; set; }
        [JsonPropertyName("view_counters")] public List<int[]>? ViewCounters { get; set; }
    }
}

/// <summary>
/// A body-area figure built from pieces (Phantasie III Amiga, <c>0x9694</c>; <c>reverse-engineering/amiga/phantasie3/FORMAT.md</c>
/// §2): a <see cref="Width"/> × <see cref="Height"/> box filled with <see cref="Background"/>, then every piece in list
/// order. A piece whose area is in state <c>States[s]</c> draws <see cref="PictureNames.BodyPiece"/>
/// (<see cref="SlotFor"/>) at its offset, masked; a state not in <see cref="States"/> (Gone) fills the piece's
/// <see cref="Piece.Clear"/> rectangle with the background instead. A piece with no area (the chest) always draws state 0.
/// </summary>
public sealed record BodyFigureLayout(
    int Width, int Height, string Background, int FirstSlot, int SlotStep, IReadOnlyList<string> States,
    IReadOnlyList<BodyFigureLayout.Piece> Pieces)
{
    /// <summary>One piece: its area id (null = always drawn), offset, and the rectangle a cleared piece fills [x, y, w, h].</summary>
    public sealed record Piece(string? Area, int X, int Y, IReadOnlyList<int>? Clear);

    /// <summary>The picture slot of piece <paramref name="piece"/> drawn in state index <paramref name="state"/>.</summary>
    public int SlotFor(int piece, int state) => FirstSlot + SlotStep * state + piece;

    /// <summary>One drawing step: piece <see cref="Slot"/> at (<see cref="X"/>, <see cref="Y"/>), or (Slot null) a
    /// <see cref="Width"/> × <see cref="Height"/> clear in the background.</summary>
    public readonly record struct Step(int? Slot, int X, int Y, int Width = 0, int Height = 0);

    /// <summary>
    /// The steps that draw a figure whose areas are in <paramref name="areas"/> (area id → state id; a missing area is
    /// state 0), in order, after the box is filled; none for a dead member (<paramref name="dead"/>).
    /// </summary>
    public IReadOnlyList<Step> Plan(IReadOnlyDictionary<string, string>? areas, bool dead)
    {
        var steps = new List<Step>();
        if (dead)
            return steps;
        for (var i = 0; i < Pieces.Count; i++)
        {
            var piece = Pieces[i];
            var state = 0;
            if (piece.Area is { } area && areas is not null && areas.TryGetValue(area, out var id))
                state = States.ToList().IndexOf(id); // a state without pieces (Gone) → −1: cleared
            if (state >= 0)
                steps.Add(new Step(SlotFor(i, state), piece.X, piece.Y));
            else if (piece.Clear is [var cx, var cy, var cw, var ch])
                steps.Add(new Step(null, cx, cy, cw, ch));
        }
        return steps;
    }

    internal static BodyFigureLayout FromDto(Dto dto, string manifestPath)
    {
        if (dto.Size is not [> 0, > 0] size || dto.States is not { Count: > 0 } states || dto.Pieces is not { Count: > 0 } pieces)
            throw new InvalidDataException($"{manifestPath}: body_figure needs size [w, h], states and pieces.");
        var list = pieces.Select(p => p.At is [var x, var y] && (p.Clear is null or [_, _, > 0, > 0])
            ? new Piece(p.Area, x, y, p.Clear)
            : throw new InvalidDataException($"{manifestPath}: body_figure pieces need at [x, y] and clear [x, y, w, h].")).ToList();
        return new BodyFigureLayout(size[0], size[1], dto.Background ?? "#000000", dto.FirstSlot, dto.SlotStep, states, list);
    }

    internal sealed class Dto
    {
        public int[]? Size { get; set; }
        public string? Background { get; set; }
        [JsonPropertyName("first_slot")] public int FirstSlot { get; set; }
        [JsonPropertyName("slot_step")] public int SlotStep { get; set; }
        public List<string>? States { get; set; }
        public List<PieceDto>? Pieces { get; set; }
    }

    internal sealed class PieceDto
    {
        public string? Area { get; set; }
        public int[]? At { get; set; }
        public int[]? Clear { get; set; }
    }
}

/// <summary>
/// The frame text scrolls are read in (Phantasie III Amiga, <c>0x1e2c2</c>; FORMAT.md §2): a <see cref="Columns"/> ×
/// (<see cref="Rows"/> + 2) border of <see cref="Cell"/>-pixel <see cref="PictureNames.ScrollFramePiece"/> pieces round
/// the inside <see cref="Fill"/> [x, y, w, h] in <see cref="FillColour"/>; text in <see cref="TextColour"/>. Corners are
/// fixed; every other edge cell is one of its edge's pieces, picked at random when the scroll is opened.
/// </summary>
public sealed record ScrollFrameLayout(
    int Cell, int Columns, int Rows, IReadOnlyList<int> Fill, string FillColour, string TextColour,
    IReadOnlyList<int> TopCorners, IReadOnlyList<int> BottomCorners,
    IReadOnlyList<int> Top, IReadOnlyList<int> Bottom, IReadOnlyList<int> Left, IReadOnlyList<int> Right)
{
    /// <summary>One edge cell: its column, row (0 = top, <see cref="Rows"/> + 1 = bottom) and piece slot.</summary>
    public readonly record struct Edge(int Column, int Row, int Slot);

    /// <summary>
    /// The border's cells in the Amiga's draw order (column by column: top, bottom, then the two side pieces of row
    /// column + 1), the random pieces drawn from <paramref name="next"/> (<c>next(n)</c> = 0..n − 1).
    /// </summary>
    public IReadOnlyList<Edge> Pick(Func<int, int> next)
    {
        ArgumentNullException.ThrowIfNull(next);
        var edges = new List<Edge>();
        for (var i = 0; i < Columns; i++)
        {
            var corner = i == 0 || i == Columns - 1;
            var side = i == 0 ? 0 : 1;
            edges.Add(new Edge(i, 0, corner ? TopCorners[side] : Top[next(Top.Count)]));
            edges.Add(new Edge(i, Rows + 1, corner ? BottomCorners[side] : Bottom[next(Bottom.Count)]));
            if (i < Rows)
            {
                edges.Add(new Edge(0, i + 1, Left[next(Left.Count)]));
                edges.Add(new Edge(Columns - 1, i + 1, Right[next(Right.Count)]));
            }
        }
        return edges;
    }

    internal static ScrollFrameLayout FromDto(Dto dto, string manifestPath)
    {
        static bool Two(List<int>? l) => l is { Count: 2 };
        static bool Some(List<int>? l) => l is { Count: > 0 };
        if (dto.Cell <= 0 || dto.Columns < 2 || dto.Rows <= 0 || dto.Fill is not [_, _, > 0, > 0] ||
            !Two(dto.TopCorners) || !Two(dto.BottomCorners) || !Some(dto.Top) || !Some(dto.Bottom) || !Some(dto.Left) ||
            !Some(dto.Right))
            throw new InvalidDataException($"{manifestPath}: bad scroll_frame.");
        return new ScrollFrameLayout(dto.Cell, dto.Columns, dto.Rows, dto.Fill, dto.FillColour ?? "#000000",
            dto.TextColour ?? "#FFFFFF", dto.TopCorners!, dto.BottomCorners!, dto.Top!, dto.Bottom!, dto.Left!, dto.Right!);
    }

    internal sealed class Dto
    {
        public int Cell { get; set; }
        public int Columns { get; set; }
        public int Rows { get; set; }
        public int[]? Fill { get; set; }
        [JsonPropertyName("fill_colour")] public string? FillColour { get; set; }
        [JsonPropertyName("text_colour")] public string? TextColour { get; set; }
        [JsonPropertyName("top_corners")] public List<int>? TopCorners { get; set; }
        [JsonPropertyName("bottom_corners")] public List<int>? BottomCorners { get; set; }
        public List<int>? Top { get; set; }
        public List<int>? Bottom { get; set; }
        public List<int>? Left { get; set; }
        public List<int>? Right { get; set; }
    }
}

/// <summary>
/// Manifest <c>keep_windows</c> (Phantasie III Amiga, <c>FORMAT.md</c> §2.3): the keep questions as the original's own
/// windows — the dungeon "Keep …?" (window 1) and the fight find (window 23) — each a box filled in
/// <see cref="FillColour"/> with a two-line frame (<see cref="FrameColours"/>: outer, inner), its text lines at
/// <see cref="Window.Text"/> (one origin per line, the last taking any further lines 10 px apart) and the
/// <see cref="PictureNames.Button"/> OK / CANCEL buttons at their places.
/// </summary>
public sealed record KeepWindowsLayout(
    string FillColour, IReadOnlyList<string> FrameColours, string TextColour, int ButtonWidth, int ButtonHeight,
    KeepWindowsLayout.Window Dungeon, KeepWindowsLayout.Window Fight)
{
    /// <summary>The town's Bank window (window 9, s117), or null.</summary>
    public Window? Bank { get; init; }

    /// <summary>Trade's member windows, "Whose item shall be traded?" / "Who gets the item?" (window 6, s117), or null.</summary>
    public Window? TradeWho { get; init; }

    /// <summary>Trade's "Trade which item?" window (window 10, s117), or null.</summary>
    public Window? TradeItem { get; init; }

    /// <summary>The combat orders screen's "Target rank 1? (or 2)" (window 20, <c>0x19806</c>, s118): OK = rank 1, CANCEL = 2; or null.</summary>
    public Window? Rank { get; init; }

    /// <summary>
    /// The combat orders screen's spell window (window 11, <c>0xf770</c>, s119): the title "Select &lt;name&gt;'s spell:", the
    /// first spell row's name and cost origins (rows 8 px apart, 12 to a page), OK / CANCEL and MORE; or null.
    /// </summary>
    public Window? Spell { get; init; }

    /// <summary>
    /// The heal target window (window 6, <c>0x127ce(33)</c>, s119): the title "Who shall be healed in combat?", then the
    /// first member row's name, race, level, class and status origins (rows 8 px apart); or null.
    /// </summary>
    public Window? Heal { get; init; }

    /// <summary>
    /// The fight's "Monster Identification:" (M on the field, <c>0x1a264(1)</c>, s120): window 23 moved to (16, 128)–(300, 191);
    /// <see cref="InfoWindow.Text"/> = the title, then rank 1's row ("Rank", its digit, ":", the count, the name); rank 2
    /// one row above (<c>0x1a44c</c>: row 22 − rank); or null.
    /// </summary>
    public InfoWindow? MonsterNames { get; init; }

    /// <summary>
    /// The party's health (H on the field, <c>0xadac</c>, window 24, s120): <see cref="InfoWindow.Text"/> = the row labels
    /// (the window's title cell), the second label set (shown with a 4th member), then one origin a member slot (0–5); or null.
    /// </summary>
    public InfoWindow? PartyHealth { get; init; }

    /// <summary>
    /// The message box (window 7, <c>0xaaac</c>, struct <c>0x23394</c>, s134): the box, the text's origin (the title cell;
    /// a "\r" in the text starts a row 8 px down at the same column), its colour and the one OK button; or null.
    /// </summary>
    public MessageWindow? Message { get; init; }

    /// <summary>
    /// File › Info's credits (window 0, struct <c>0x23228</c>, drawn by <c>0x86e6</c> through <c>0x3904</c>, s140): the box,
    /// the title cell's origin (a "\r" goes 8 px down; each row's leading blanks are 8-px cells), its colour and the one
    /// OK button; <see cref="MessageWindow.TextIds"/>[0] = the credits text (pointer <c>0x2308c</c>, set at <c>0x7450</c>).
    /// Null: no credits window.
    /// </summary>
    public MessageWindow? Credits { get; init; }

    /// <summary>
    /// s141: Options › Party Status (P outside the towns, <c>0x8832</c>): window 22 (struct <c>0x236a0</c>), the trip summary;
    /// or null (the remaster's health dialog stands in).
    /// </summary>
    public PartyStatusWindow? PartyStatus { get; init; }

    /// <summary>A list window's text colours: [0] labels and rows, [1] values and the picked row (s117).</summary>
    public IReadOnlyList<string> ListColours { get; init; } = ["#B6926D", "#FFDB49"];

    /// <summary>The pack palette the <see cref="PictureNames.Button"/> art is cut in (III Amiga: "map"), or null.</summary>
    public string? ButtonPalette { get; init; }

    /// <summary>
    /// The pack palette in force while the party is in a dungeon (III Amiga: "dungeon", s125 seen): the keep windows'
    /// buttons show their <see cref="ButtonPalette"/> indices in it (the label yellow #FFFF00 there). Null: as cut.
    /// </summary>
    public string? DungeonPalette { get; init; }

    /// <summary>
    /// s156: III Amiga's own menu bars by name ("fight" <c>0x2397c</c>, "outside" <c>0x23910</c>, "town" <c>0x238a4</c>): the
    /// places <c>0x12d6</c> computes at start-up written out (titles, inverted spans, boxes, item rows), pens as indices of
    /// the palette in force, each item's remaster action. Empty: none.
    /// </summary>
    public IReadOnlyDictionary<string, CombatFieldMenuBar> MenuBars { get; init; } = new Dictionary<string, CombatFieldMenuBar>();

    /// <summary>One window: the box (x0, y0, x1, y1 inclusive), the text origins and the OK / CANCEL top-left corners.</summary>
    public sealed record Window(IReadOnlyList<int> Box, IReadOnlyList<(int X, int Y)> Text, (int X, int Y) Ok, (int X, int Y) Cancel)
    {
        /// <summary>The MORE button's top-left corner (window 11), or null.</summary>
        public (int X, int Y)? More { get; init; }
    }

    /// <summary>A window with one OK button (window 7): the box (x0, y0, x1, y1 inclusive), the text origin and colour, OK's corner.</summary>
    public sealed record MessageWindow(IReadOnlyList<int> Box, (int X, int Y) Text, string TextColour, (int X, int Y) Ok)
    {
        /// <summary>
        /// The text ids (string tables) whose lines the original shows in this window outside the orders screen (s135: the
        /// town / guild callers of <c>0xaaac</c> that have a remaster equivalent); empty = none.
        /// </summary>
        public IReadOnlyList<string> TextIds { get; init; } = [];

        /// <summary>s141: the text's font id (III Amiga's credits: topaz 8, the screen font <c>0x854</c> draws with), or null
        /// = the pack's font.</summary>
        public string? Font { get; init; }

        /// <summary>The <see cref="Font"/>'s file next to the manifest (<c>fonts/&lt;id&gt;.json</c>), or null.</summary>
        public string? FontFile { get; init; }
    }

    /// <summary>
    /// s141: window 22, Party Status (<c>0x8832</c>, FORMAT.md 8.9): the box (x0, y0, x1, y1 inclusive), the labels' origin
    /// (the text of <see cref="TextIds"/>[0] laid out as a window title: a "\r" goes 8 px down, leading blanks are 8-px
    /// cells), <see cref="ColourIndex"/> = the labels' and values' colour as an index of the palette in force (the map's
    /// outside, the dungeon's below), <see cref="ValueX"/> and <see cref="ValueRows"/> = the values' left edge and their
    /// rows (days out, experience, gold, then one row per <see cref="Kinds"/> entry), the OK's corner, and the kinds the
    /// found items are counted by.
    /// </summary>
    public sealed record PartyStatusWindow(IReadOnlyList<int> Box, (int X, int Y) Text, int ColourIndex, int ValueX,
        IReadOnlyList<int> ValueRows, (int X, int Y) Ok, IReadOnlyList<string> TextIds,
        IReadOnlyList<OpenPhantasie.Core.P3FoundItemKind> Kinds)
    {
        /// <summary>The labels' and values' font id (topaz 8 on the Amiga), or null = the pack's font.</summary>
        public string? Font { get; init; }

        /// <summary>The <see cref="Font"/>'s file next to the manifest (<c>fonts/&lt;id&gt;.json</c>), or null.</summary>
        public string? FontFile { get; init; }
    }

    /// <summary>
    /// A window with no buttons that waits for a key (s120: M and H on the field): the box (x0, y0, x1, y1 inclusive),
    /// the text origins, and its colours as indices into the pack palette <see cref="Palette"/> (the one in force when
    /// the original draws it): <see cref="Frame"/> = outer, inner frame (the fill is index 0), <see cref="Colours"/> per window.
    /// </summary>
    public sealed record InfoWindow(IReadOnlyList<int> Box, IReadOnlyList<(int X, int Y)> Text, string Palette,
        IReadOnlyList<int> Frame, IReadOnlyList<int> Colours);

    internal static KeepWindowsLayout FromDto(Dto dto, string manifestPath)
    {
        InfoWindow? Info(InfoWindowDto? w, int texts, int colours) => w is null ? null
            : w is { Box: [_, _, _, _] box, Text: { } text, Palette: { Length: > 0 } palette, Frame: [_, _] frame,
                Colours: { } c } && text.Count == texts && text.All(t => t is [_, _]) && c.Length == colours
                ? new InfoWindow(box, [.. text.Select(t => (t[0], t[1]))], palette, frame, c)
                : throw new InvalidDataException($"{manifestPath}: bad keep_windows info window.");
        Window W(WindowDto? w) =>
            w is { Box: [_, _, _, _] box, Text: { Count: > 0 } text, Ok: [var ox, var oy], Cancel: [var cx, var cy] }
            && text.All(t => t is [_, _])
                ? new Window(box, [.. text.Select(t => (t[0], t[1]))], (ox, oy), (cx, cy))
                {
                    More = w.More is [var mx, var my] ? (mx, my) : null,
                }
                : throw new InvalidDataException($"{manifestPath}: bad keep_windows window.");
        if (dto.FrameColours is not { Count: 2 } frame || dto.ButtonSize is not [> 0 and var bw, > 0 and var bh])
            throw new InvalidDataException($"{manifestPath}: bad keep_windows.");
        Window? Optional(WindowDto? w) => w is null ? null : W(w);
        MessageWindow? Message(MessageWindowDto? m) => m is null ? null
            : m is { Box: [_, _, _, _] mbox, Text: [var mtx, var mty], Ok: [var mox, var moy], TextColour: { Length: > 0 } mink }
                ? new MessageWindow(mbox, (mtx, mty), mink, (mox, moy))
                {
                    TextIds = m.TextIds ?? [], Font = m.Font,
                    FontFile = CombatFieldLayout.FontFileFor(m.Font, manifestPath),
                }
                : throw new InvalidDataException($"{manifestPath}: bad keep_windows message window.");
        PartyStatusWindow? Status(PartyStatusDto? d) => d is null ? null
            : d is { Box: [_, _, _, _] sbox, Text: [var stx, var sty], ColourIndex: >= 0 and var sci, ValueX: var svx,
                ValueRows: { } rows, Ok: [var sox, var soy], TextIds: { Count: > 0 } sids, Kinds: { Count: > 0 } kinds }
              && kinds.All(k => k is [_] or [_, _]) && rows.Length == 3 + kinds.Count
                ? new PartyStatusWindow(sbox, (stx, sty), sci, svx, rows, (sox, soy), sids,
                    [.. kinds.Select(k => new OpenPhantasie.Core.P3FoundItemKind(k[0], k.Length > 1 ? k[1] : null))])
                {
                    Font = d.Font, FontFile = CombatFieldLayout.FontFileFor(d.Font, manifestPath),
                }
                : throw new InvalidDataException($"{manifestPath}: bad keep_windows party_status window.");
        return new KeepWindowsLayout(dto.FillColour ?? "#000000", frame, dto.TextColour ?? "#FFFFFF", bw, bh,
            W(dto.Dungeon), W(dto.Fight))
        {
            Bank = Optional(dto.Bank),
            TradeWho = Optional(dto.TradeWho),
            TradeItem = Optional(dto.TradeItem),
            Rank = Optional(dto.Rank),
            Spell = Optional(dto.Spell),
            Heal = Optional(dto.Heal),
            MonsterNames = Info(dto.MonsterNames, 6, 1),
            PartyHealth = Info(dto.PartyHealth, 8, 7),
            Message = Message(dto.Message),
            Credits = Message(dto.Credits) is var credits && credits is null or { TextIds.Count: > 0 } ? credits
                : throw new InvalidDataException($"{manifestPath}: keep_windows credits window without its text id."),
            PartyStatus = Status(dto.PartyStatus),
            ListColours = dto.ListColours is { Count: 2 } colours ? colours : ["#B6926D", "#FFDB49"],
            ButtonPalette = dto.ButtonPalette,
            DungeonPalette = dto.DungeonPalette,
            MenuBars = (dto.MenuBars ?? []).ToDictionary(kv => kv.Key, kv => CombatFieldMenuBar.FromDto(kv.Value,
                m => new InvalidDataException($"{manifestPath}: keep_windows.menu_bars.{kv.Key}: {m}")) with
            {
                Font = string.IsNullOrWhiteSpace(kv.Value.Font) ? null : kv.Value.Font,
                FontFile = CombatFieldLayout.FontFileFor(kv.Value.Font, manifestPath),
            }),
        };
    }

    internal sealed class Dto
    {
        [JsonPropertyName("fill_colour")] public string? FillColour { get; set; }
        [JsonPropertyName("frame_colours")] public List<string>? FrameColours { get; set; }
        [JsonPropertyName("text_colour")] public string? TextColour { get; set; }
        [JsonPropertyName("button_size")] public int[]? ButtonSize { get; set; }
        [JsonPropertyName("button_palette")] public string? ButtonPalette { get; set; }
        [JsonPropertyName("dungeon_palette")] public string? DungeonPalette { get; set; }
        public WindowDto? Dungeon { get; set; }
        public WindowDto? Fight { get; set; }
        public WindowDto? Bank { get; set; }
        [JsonPropertyName("trade_who")] public WindowDto? TradeWho { get; set; }
        [JsonPropertyName("trade_item")] public WindowDto? TradeItem { get; set; }
        public WindowDto? Rank { get; set; }
        public WindowDto? Spell { get; set; }
        public WindowDto? Heal { get; set; }
        [JsonPropertyName("list_colours")] public List<string>? ListColours { get; set; }
        [JsonPropertyName("monster_names")] public InfoWindowDto? MonsterNames { get; set; }
        [JsonPropertyName("party_health")] public InfoWindowDto? PartyHealth { get; set; }
        public MessageWindowDto? Message { get; set; }
        public MessageWindowDto? Credits { get; set; }
        [JsonPropertyName("party_status")] public PartyStatusDto? PartyStatus { get; set; }
        [JsonPropertyName("menu_bars")] public Dictionary<string, CombatFieldMenuBar.Dto>? MenuBars { get; set; }
    }

    internal sealed class PartyStatusDto
    {
        public int[]? Box { get; set; }
        public int[]? Text { get; set; }
        [JsonPropertyName("colour_index")] public int ColourIndex { get; set; } = -1;
        [JsonPropertyName("value_x")] public int ValueX { get; set; }
        [JsonPropertyName("value_rows")] public int[]? ValueRows { get; set; }
        public int[]? Ok { get; set; }
        [JsonPropertyName("text_ids")] public List<string>? TextIds { get; set; }
        public List<int[]>? Kinds { get; set; }
        public string? Font { get; set; }
    }

    internal sealed class InfoWindowDto
    {
        public int[]? Box { get; set; }
        public List<int[]>? Text { get; set; }
        public string? Palette { get; set; }
        public int[]? Frame { get; set; }
        public int[]? Colours { get; set; }
    }

    internal sealed class MessageWindowDto
    {
        public int[]? Box { get; set; }
        public int[]? Text { get; set; }
        [JsonPropertyName("text_colour")] public string? TextColour { get; set; }
        public int[]? Ok { get; set; }
        [JsonPropertyName("text_ids")] public List<string>? TextIds { get; set; }
        public string? Font { get; set; }
    }

    internal sealed class WindowDto
    {
        public int[]? Box { get; set; }
        public List<int[]>? Text { get; set; }
        public int[]? Ok { get; set; }
        public int[]? Cancel { get; set; }
        public int[]? More { get; set; }
    }
}

/// <summary>
/// The graphics packs of one game (T1.5): <c>GameData/&lt;game&gt;/graphics/index.json</c> lists the pack manifests and
/// the order in which packs may become the default. Only packs whose original files are present are
/// <see cref="Available"/>; a pack with <c>auto_select: false</c> (DOS CGA for Phantasie I) is never the default and is
/// drawn only when chosen explicitly (<see cref="Find"/>).
/// </summary>
public sealed class GraphicsPackCatalog
{
    public required string Game { get; init; }
    /// <summary>Packs whose image root and required files were found, in index order.</summary>
    public required IReadOnlyList<GraphicsPack> Available { get; init; }
    /// <summary>Pack ids the index lists whose original files are not present (not offered).</summary>
    public required IReadOnlyList<string> Missing { get; init; }
    /// <summary>The first available pack in the index's <c>default_order</c>, or null (draw no art, as without extracts).</summary>
    public required GraphicsPack? Default { get; init; }

    public GraphicsPack? Find(string id) =>
        Available.FirstOrDefault(p => string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase));

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>
    /// Read <c>graphics/index.json</c> under <paramref name="gameDataDirectory"/>. Each pack's <c>root</c> is a
    /// repo-relative folder looked up in the import's <c>files/</c> and from <paramref name="searchFrom"/> (default: the
    /// app folder, dev builds only) and its parents; the
    /// pack is available when the folder holds every <c>requires</c> path. A game without an index has no packs.
    /// </summary>
    public static GraphicsPackCatalog Load(string gameDataDirectory, string? searchFrom = null)
    {
        var game = Path.GetFileName(Path.TrimEndingDirectorySeparator(gameDataDirectory));
        var indexPath = Path.Combine(gameDataDirectory, "graphics", "index.json");
        if (!File.Exists(indexPath))
            return new GraphicsPackCatalog { Game = game, Available = [], Missing = [], Default = null };

        var index = JsonSerializer.Deserialize<IndexDto>(File.ReadAllText(indexPath), Options)
            ?? throw new InvalidDataException($"{indexPath}: empty.");
        var available = new List<GraphicsPack>();
        var missing = new List<string>();
        foreach (var file in index.Packs ?? [])
        {
            var path = Path.Combine(gameDataDirectory, "graphics", file);
            var dto = JsonSerializer.Deserialize<GraphicsPack.PackDto>(File.ReadAllText(path), Options)
                ?? throw new InvalidDataException($"{path}: empty.");
            var root = dto.Root is null ? null : FindRoot(dto.Root, dto.Requires ?? [], searchFrom);
            if (root is null)
                missing.Add(dto.Id ?? file);
            else
                available.Add(GraphicsPack.FromDto(dto, root, path));
        }

        var auto = available.Where(p => p.AutoSelect).ToList();
        var fallback = (index.DefaultOrder ?? [])
            .Select(id => auto.FirstOrDefault(p => string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase)))
            .FirstOrDefault(p => p is not null)
            // Art follows what was imported (user, s158): with no default pack present (a DOS-only copy), the first
            // available pack of fallback_order (I: DOS CGA) is drawn.
            ?? (index.FallbackOrder ?? [])
                .Select(id => available.FirstOrDefault(p => string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase)))
                .FirstOrDefault(p => p is not null);
        return new GraphicsPackCatalog
        {
            Game = index.Game ?? game,
            Available = available,
            Missing = missing,
            Default = fallback,
        };
    }

    private static string? FindRoot(string relativeRoot, IReadOnlyList<string> requires, string? searchFrom)
    {
        if (Path.IsPathRooted(relativeRoot))
            return Has(relativeRoot) ? relativeRoot : null;
        // The player's import (files/<repo-relative root>) first, then (dev build) the app folder and its parents.
        foreach (var dir in GameDataLocator.ArtSearchRoots(searchFrom))
        {
            var candidate = Path.Combine(dir, relativeRoot);
            if (Has(candidate))
                return candidate;
        }

        return null;

        bool Has(string root) => Directory.Exists(root) &&
            requires.All(r => File.Exists(Path.Combine(root, r)) || Directory.Exists(Path.Combine(root, r)));
    }

    private sealed class IndexDto
    {
        public string? Game { get; set; }
        [JsonPropertyName("default_order")] public List<string>? DefaultOrder { get; set; }
        [JsonPropertyName("fallback_order")] public List<string>? FallbackOrder { get; set; }
        public List<string>? Packs { get; set; }
    }
}

/// <summary>s156: the steps of leaving a town on a pack that prints "LEAVING %s" (<see cref="TownScreenLayout.LeavingAt"/>).</summary>
public enum LeavingPhase
{
    /// <summary>The town picture with the leaving line.</summary>
    Line,

    /// <summary>The whole screen black (the map loading).</summary>
    Black,

    /// <summary>Done: the map shows.</summary>
    Map,
}
