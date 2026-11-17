using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenPhantasie.Data;

/// <summary>
/// Manifest <c>apple2_field</c> (s224, II s226): the Apple II engine's combat field (I and II), as OUTDOORS / DUNGON
/// (II: OUTDOORS2 / DUNGON2) 1480–2006, 4100–4145 and 4790–4811 draw it (<c>reverse-engineering/apple2/phantasie1/GRAPHICS.md</c>
/// section 7; II's differences <c>../phantasie2/GRAPHICS.md</c> section 7: the tossed rock, <see cref="Rock"/>). The round plays on the
/// mixed hi-res screen: graphics rows 0 … <see cref="GraphicsRows"/> − 1, the field left of the divider x
/// <see cref="Divider"/> (the map stays right of it), four text rows below. Everything is in Apple hi-res pixels; the
/// screen's (0, 0) sits at (<see cref="ScreenX"/>, <see cref="ScreenY"/>) of the remaster's 320 × 200 screen.
/// <list type="bullet">
/// <item>Party: member slot k's figure (body by <see cref="Bodies"/>, arm 20 / 21 / 22) DRAWn at
/// (<see cref="PartyStep"/>·k, <see cref="PartyY"/>) (1525–1534).</item>
/// <item>Monsters: the front rank at y <see cref="RankY"/>, the next at that − the front group's rank height (YS%), the
/// third at that − the second's, not below 0 (1480–1500); monster i of a group of n at x = H% + INT(i·<see cref="Spread"/>
/// / n) (1550).</item>
/// <item>A cast: shape 23 XDRAWn at (<see cref="PartyStep"/>·k + <see cref="SparkleDx"/>, <see cref="SparkleY"/>) during
/// the cast's tones (1762–1764).</item>
/// </list>
/// Per-monster numbers (H%, SCALE, YS%) and each picture's DRAW point come from the import's index
/// (<see cref="Index"/>, <see cref="Apple2FieldArt"/>): they are the Apple's own data, not the remaster's tables.
/// III (s228) draws another field: <see cref="Plot"/> (<c>../phantasie3/GRAPHICS.md</c> section 9).
/// </summary>
public sealed record Apple2FieldLayout
{
    public int ScreenX { get; init; }
    public int ScreenY { get; init; }
    public int Width { get; init; } = 280;
    public int GraphicsRows { get; init; } = 160;
    public int Divider { get; init; } = 138;

    /// <summary>The white HPLOT lines of the screen's set-up (4810), x0, y0, x1, y1 each.</summary>
    public IReadOnlyList<(int X0, int Y0, int X1, int Y1)> Lines { get; init; } = [];

    public int PartyStep { get; init; } = 19;
    public int PartyY { get; init; } = 158;
    public int RankY { get; init; } = 135;
    public int Spread { get; init; } = 120;
    public int SparkleDx { get; init; } = 19;
    public int SparkleY { get; init; } = 136;

    /// <summary>The text window (POKE 34,20: rows 20–23), its columns and the cell size of the Apple's text page.</summary>
    public int TextTop { get; init; } = 20;
    public int TextRows { get; init; } = 4;

    /// <summary>The text window's first column (III: 17, its message box; I / II: 0).</summary>
    public int TextLeft { get; init; }
    public int Columns { get; init; } = 40;
    public int CellWidth { get; init; } = 7;
    public int CellHeight { get; init; } = 8;

    /// <summary>The import's index next to the pictures (pack root), read by <see cref="Apple2FieldArt.Load"/>.</summary>
    public string Index { get; init; } = "extraction-index.json";

    /// <summary>
    /// The pauses: a line's FOR loop of <see cref="LineLoopsPerTimeLag"/> × TIME LAG passes (PA, line 361 / 7560), the
    /// spell hit's 500 (4132), each pass <see cref="LoopMs"/> ms (`[~]`: not timed on an Apple), and the cast tones'
    /// length with the sparkle up (<see cref="SparkleMs"/>, `[?]`: CALL 770 not decoded; 0 with the sound off, 7570).
    /// </summary>
    public int LineLoopsPerTimeLag { get; init; } = 120;
    public int HitLoops { get; init; } = 500;
    public double LoopMs { get; init; } = 1.0;
    public double SparkleMs { get; init; }

    /// <summary>TOWN 2350–2356's body rule (record byte 168).</summary>
    public required Apple2FieldBodies Bodies { get; init; }

    /// <summary>II's tossed rock (OUTDOORS2 / DUNGON2 1623–1628, 4132), or null (I has no TOSS order).</summary>
    public Apple2FieldRock? Rock { get; init; }

    /// <summary>III's field (s228; OUTDOORS3 / DUNGON3, <c>MONSTER PLOT</c> sprites), or null (I / II).</summary>
    public Apple2FieldPlot? Plot { get; init; }

    /// <summary>A line's pause at the game's TIME LAG <paramref name="timeLag"/> (0–9), ms.</summary>
    public double LinePauseMs(int timeLag) => LineLoopsPerTimeLag * Math.Clamp(timeLag, 0, 9) * LoopMs;

    /// <summary>The spell hit's pause (4132), ms.</summary>
    public double HitMs => HitLoops * LoopMs;

    /// <summary>Monster i's x of a group of <paramref name="n"/> living whose H% is <paramref name="h"/> (1550, integer).</summary>
    public int MonsterX(int h, int i, int n) => h + i * Spread / Math.Max(1, n);

    /// <summary>
    /// Each rank's DRAW y (1480–1500): the front at <see cref="RankY"/>, each next one the rank height
    /// (<paramref name="heights"/>, YS%) of the one before it higher; the third never above 0.
    /// </summary>
    public IReadOnlyList<int> RankYs(IReadOnlyList<int> heights)
    {
        ArgumentNullException.ThrowIfNull(heights);
        var ys = new List<int>();
        for (var r = 0; r < heights.Count; r++)
            ys.Add(r == 0 ? RankY : r == 1 ? RankY - heights[0] : Math.Max(0, ys[r - 1] - heights[r - 1]));
        return ys;
    }

    internal static Apple2FieldLayout FromDto(Dto dto, string manifestPath)
    {
        InvalidDataException Bad(string what) => new($"{manifestPath}: apple2_field: {what}.");
        if (dto.Bodies is not { } b || b.RobedClasses is null || b.HumanElf is null)
            throw Bad("bodies missing");
        if (dto.Width <= 0 || dto.GraphicsRows <= 0 || dto.PartyStep <= 0 || dto.Columns <= 0 || dto.TextRows <= 0
            || dto.CellWidth <= 0 || dto.CellHeight <= 0 || dto.LoopMs < 0 || dto.SparkleMs < 0
            || string.IsNullOrWhiteSpace(dto.Index))
            throw Bad("bad numbers");
        var lines = (dto.Lines ?? []).Select(l => l is [var x0, var y0, var x1, var y1]
            ? (x0, y0, x1, y1) : throw Bad("a line needs x0, y0, x1, y1")).ToList();
        return new Apple2FieldLayout
        {
            ScreenX = dto.ScreenX, ScreenY = dto.ScreenY, Width = dto.Width, GraphicsRows = dto.GraphicsRows,
            Divider = dto.Divider, Lines = lines, PartyStep = dto.PartyStep, PartyY = dto.PartyY, RankY = dto.RankY,
            Spread = dto.Spread, SparkleDx = dto.SparkleDx, SparkleY = dto.SparkleY, TextTop = dto.TextTop,
            TextRows = dto.TextRows, TextLeft = dto.TextLeft, Columns = dto.Columns, CellWidth = dto.CellWidth, CellHeight = dto.CellHeight,
            Index = dto.Index!, LineLoopsPerTimeLag = dto.LineLoopsPerTimeLag, HitLoops = dto.HitLoops,
            LoopMs = dto.LoopMs, SparkleMs = dto.SparkleMs,
            Bodies = new Apple2FieldBodies(b.MonsterRacesFrom, b.MonsterBody, b.HumanElf, b.SmallBody, b.PlainBody,
                b.RobedClasses, b.RobedBody, b.ElementalBody) { NobleClass = b.NobleClass, NobleBody = b.NobleBody },
            Plot = dto.MonsterPlot is { } mp ? Apple2FieldPlot.FromDto(mp, Bad) : null,
            Rock = dto.Rock is { } r
                ? r.MissLoops >= 0 && r.HitLoops >= 0 && r.Miss is [var mx, var my]
                    ? new Apple2FieldRock(mx, my, r.MissLoops, r.HitLoops)
                    : throw Bad("rock: miss needs x, y and loops not below 0")
                : null,
        };
    }

    internal sealed class Dto
    {
        [JsonPropertyName("screen_x")] public int ScreenX { get; set; }
        [JsonPropertyName("screen_y")] public int ScreenY { get; set; }
        [JsonPropertyName("width")] public int Width { get; set; } = 280;
        [JsonPropertyName("graphics_rows")] public int GraphicsRows { get; set; } = 160;
        [JsonPropertyName("divider")] public int Divider { get; set; } = 138;
        [JsonPropertyName("lines")] public List<int[]>? Lines { get; set; }
        [JsonPropertyName("party_step")] public int PartyStep { get; set; } = 19;
        [JsonPropertyName("party_y")] public int PartyY { get; set; } = 158;
        [JsonPropertyName("rank_y")] public int RankY { get; set; } = 135;
        [JsonPropertyName("spread")] public int Spread { get; set; } = 120;
        [JsonPropertyName("sparkle_dx")] public int SparkleDx { get; set; } = 19;
        [JsonPropertyName("sparkle_y")] public int SparkleY { get; set; } = 136;
        [JsonPropertyName("text_top")] public int TextTop { get; set; } = 20;
        [JsonPropertyName("text_rows")] public int TextRows { get; set; } = 4;
        [JsonPropertyName("text_left")] public int TextLeft { get; set; }
        [JsonPropertyName("columns")] public int Columns { get; set; } = 40;
        [JsonPropertyName("cell_width")] public int CellWidth { get; set; } = 7;
        [JsonPropertyName("cell_height")] public int CellHeight { get; set; } = 8;
        [JsonPropertyName("index")] public string? Index { get; set; } = "extraction-index.json";
        [JsonPropertyName("line_loops_per_time_lag")] public int LineLoopsPerTimeLag { get; set; } = 120;
        [JsonPropertyName("hit_loops")] public int HitLoops { get; set; } = 500;
        [JsonPropertyName("loop_ms")] public double LoopMs { get; set; } = 1.0;
        [JsonPropertyName("sparkle_ms")] public double SparkleMs { get; set; }
        [JsonPropertyName("bodies")] public BodiesDto? Bodies { get; set; }
        [JsonPropertyName("rock")] public RockDto? Rock { get; set; }
        [JsonPropertyName("monster_plot")] public Apple2FieldPlot.Dto? MonsterPlot { get; set; }
    }

    internal sealed class RockDto
    {
        [JsonPropertyName("miss")] public int[]? Miss { get; set; }
        [JsonPropertyName("miss_loops")] public int MissLoops { get; set; }
        [JsonPropertyName("hit_loops")] public int HitLoops { get; set; }
    }

    internal sealed class BodiesDto
    {
        [JsonPropertyName("monster_races_from")] public int MonsterRacesFrom { get; set; }
        [JsonPropertyName("monster_body")] public int MonsterBody { get; set; }
        [JsonPropertyName("human_elf")] public int[]? HumanElf { get; set; }
        [JsonPropertyName("small_body")] public int SmallBody { get; set; }
        [JsonPropertyName("plain_body")] public int PlainBody { get; set; }
        [JsonPropertyName("robed_classes")] public int[]? RobedClasses { get; set; }
        [JsonPropertyName("robed_body")] public int RobedBody { get; set; }
        [JsonPropertyName("elemental_body")] public int ElementalBody { get; set; }
        [JsonPropertyName("noble_class")] public int NobleClass { get; set; } = -1;
        [JsonPropertyName("noble_body")] public int NobleBody { get; set; }
    }
}

/// <summary>
/// TOWN 2350–2356 (s224): a member's body shape (record byte 168), set at creation: races from
/// <see cref="MonsterRacesFrom"/> on → <see cref="MonsterBody"/>; races other than <see cref="HumanElf"/> →
/// <see cref="SmallBody"/>; HUMAN / ELF → <see cref="RobedBody"/> for <see cref="RobedClasses"/>, else
/// <see cref="PlainBody"/>. The elementals (CHAR records 37–39) hold <see cref="ElementalBody"/>. III (TOWN3 2360, s228):
/// social class <see cref="NobleClass"/> turns any body but <see cref="SmallBody"/> into <see cref="NobleBody"/>.
/// </summary>
public sealed record Apple2FieldBodies(int MonsterRacesFrom, int MonsterBody, IReadOnlyList<int> HumanElf, int SmallBody,
    int PlainBody, IReadOnlyList<int> RobedClasses, int RobedBody, int ElementalBody)
{
    /// <summary>III's social class that gets <see cref="NobleBody"/> (TOWN3 2360: 3); −1 = none (I / II).</summary>
    public int NobleClass { get; init; } = -1;

    public int NobleBody { get; init; }

    /// <summary>The body for race id <paramref name="race"/> and class id <paramref name="cls"/> (the Apple's order = DOS's).</summary>
    public int For(int race, int cls) =>
        race >= MonsterRacesFrom ? MonsterBody
        : !HumanElf.Contains(race) ? SmallBody
        : RobedClasses.Contains(cls) ? RobedBody : PlainBody;

    /// <summary>III: <see cref="For(int, int)"/>, then the noble body for <see cref="NobleClass"/> (but not over the small body).</summary>
    public int For(int race, int cls, int socialClass)
    {
        var body = For(race, cls);
        return NobleClass >= 0 && socialClass == NobleClass && body != SmallBody ? NobleBody : body;
    }
}

/// <summary>
/// III's combat field (s228; OUTDOORS3 / DUNGON3, <c>reverse-engineering/apple2/phantasie3/GRAPHICS.md</c> section 9),
/// in Apple hi-res pixels. The round plays on the game's one hi-res screen (<c>SCREEN</c>, <see cref="Frame"/>): the field
/// box bottom right, the message box (the layout's text window) above it, the party columns on top (<see cref="Boxes"/>).
/// <list type="bullet">
/// <item>Monsters (<c>MONSTER PLOT</c>, 1540–1550): the front rank's sprites with their top-left at y
/// <see cref="RankYs"/>[0] (<see cref="GiantY"/> when its shape is <see cref="GiantShape"/>), the second at
/// <see cref="RankYs"/>[1]; monster i of n at byte column <see cref="Column"/> + <see cref="ColumnStep"/>·INT(<see cref="Slots"/>·(i
/// + .5) / n), <see cref="BytePixels"/> pixels a byte; a sprite covers its box whole (stored bytes).</item>
/// <item>Party (9100–9220): slot k's figure DRAWn at (<see cref="PartyX"/> + step·k, <see cref="PartyBaseY"/> −
/// <see cref="RankStep"/>·(rank + 1)), rank 0 back … 2 front.</item>
/// <item>Effects: the spell hit (24) or a bow hit's arrow (1) at (<see cref="BytePixels"/>·X% + <see cref="HitDx"/>, y +
/// <see cref="HitDy"/>) (4132); the sparkle at the figure's point + (<see cref="SparkleDx"/>, <see cref="SparkleDy"/>)
/// (1762); a missed arrow at <see cref="ArrowMiss"/> (<see cref="ArrowMissDungeon"/> in a dungeon) for
/// <see cref="ArrowMissLoops"/> loops (1628).</item>
/// </list>
/// </summary>
public sealed record Apple2FieldPlot
{
    public int PartyX { get; init; } = 147;
    public int PartyBaseY { get; init; } = 198;
    public int RankStep { get; init; } = 8;
    public IReadOnlyList<int> RankYs { get; init; } = [120, 96];
    public int GiantY { get; init; } = 99;
    public int GiantShape { get; init; } = 21;
    public int Column { get; init; } = 21;
    public int ColumnStep { get; init; } = 4;
    public int Slots { get; init; } = 5;
    public int BytePixels { get; init; } = 7;
    public int HitDx { get; init; } = 3;
    public int HitDy { get; init; } = 20;
    public int SparkleDx { get; init; } = 14;
    public int SparkleDy { get; init; } = -23;
    public (int X, int Y) ArrowMiss { get; init; } = (220, 104);
    public (int X, int Y) ArrowMissDungeon { get; init; } = (220, 105);
    public int ArrowMissLoops { get; init; } = 250;
    public int ArrowHitLoops { get; init; } = 500;

    /// <summary>Draw the game's screen (the index's <c>screens.frame</c>) behind the field.</summary>
    public bool Frame { get; init; } = true;

    public Apple2FieldBoxes? Boxes { get; init; }

    /// <summary>Monster i's byte column of a group of <paramref name="n"/> living (1550: 21 + 4·INT(5·(i + .5) / n)).</summary>
    public int ByteColumn(int i, int n) => Column + ColumnStep * ((2 * Slots * i + Slots) / (2 * Math.Max(1, n)));

    /// <summary>A rank's y (1540): the front rank's is the giant's when its shape is the giant's.</summary>
    public int RankY(int rank, bool giant) =>
        rank == 0 && giant ? GiantY : rank < RankYs.Count ? RankYs[rank] : RankYs[^1];

    /// <summary>The party row's DRAW y for <paramref name="rank"/> (0 back, 1 middle, 2 front; 9100).</summary>
    public int PartyY(int rank) => PartyBaseY - RankStep * (Math.Clamp(rank, 0, 2) + 1);

    internal static Apple2FieldPlot FromDto(Dto d, Func<string, InvalidDataException> bad)
    {
        (int, int) Point(int[]? p, string what) => p is [var x, var y] ? (x, y) : throw bad($"monster_plot: {what} needs x, y");
        if (d.RankYs is not { Length: > 0 } || d.Slots <= 0 || d.BytePixels <= 0 || d.ArrowMissLoops < 0 || d.ArrowHitLoops < 0)
            throw bad("monster_plot: bad numbers");
        var hit = Point(d.Hit, "hit");
        var sparkle = Point(d.Sparkle, "sparkle");
        var miss = Point(d.ArrowMiss, "arrow_miss");
        return new Apple2FieldPlot
        {
            PartyX = d.PartyX, PartyBaseY = d.PartyBaseY, RankStep = d.RankStep, RankYs = d.RankYs, GiantY = d.GiantY,
            GiantShape = d.GiantShape, Column = d.Column, ColumnStep = d.ColumnStep, Slots = d.Slots, BytePixels = d.BytePixels,
            HitDx = hit.Item1, HitDy = hit.Item2, SparkleDx = sparkle.Item1, SparkleDy = sparkle.Item2, ArrowMiss = miss,
            ArrowMissDungeon = d.ArrowMissDungeon is null ? miss : Point(d.ArrowMissDungeon, "arrow_miss_dungeon"),
            ArrowMissLoops = d.ArrowMissLoops, ArrowHitLoops = d.ArrowHitLoops, Frame = d.Frame,
            Boxes = d.Boxes is { } b
                ? new Apple2FieldBoxes(b.Column, b.Step, b.Width, b.NameRow, b.ClassRow, b.MagicRow, b.HitsRow, b.BodyRow,
                    b.StatusDx, b.Marks is { Length: 4 } marks ? marks : throw bad("monster_plot: boxes need 4 marks"))
                : null,
        };
    }

    internal sealed class Dto
    {
        [JsonPropertyName("party_x")] public int PartyX { get; set; } = 147;
        [JsonPropertyName("party_base_y")] public int PartyBaseY { get; set; } = 198;
        [JsonPropertyName("rank_step")] public int RankStep { get; set; } = 8;
        [JsonPropertyName("rank_ys")] public int[]? RankYs { get; set; }
        [JsonPropertyName("giant_y")] public int GiantY { get; set; } = 99;
        [JsonPropertyName("giant_shape")] public int GiantShape { get; set; } = 21;
        [JsonPropertyName("column")] public int Column { get; set; } = 21;
        [JsonPropertyName("column_step")] public int ColumnStep { get; set; } = 4;
        [JsonPropertyName("slots")] public int Slots { get; set; } = 5;
        [JsonPropertyName("byte_pixels")] public int BytePixels { get; set; } = 7;
        [JsonPropertyName("hit")] public int[]? Hit { get; set; }
        [JsonPropertyName("sparkle")] public int[]? Sparkle { get; set; }
        [JsonPropertyName("arrow_miss")] public int[]? ArrowMiss { get; set; }
        [JsonPropertyName("arrow_miss_dungeon")] public int[]? ArrowMissDungeon { get; set; }
        [JsonPropertyName("arrow_miss_loops")] public int ArrowMissLoops { get; set; } = 250;
        [JsonPropertyName("arrow_hit_loops")] public int ArrowHitLoops { get; set; } = 500;
        [JsonPropertyName("frame")] public bool Frame { get; set; } = true;
        [JsonPropertyName("boxes")] public BoxesDto? Boxes { get; set; }
    }

    internal sealed class BoxesDto
    {
        [JsonPropertyName("column")] public int Column { get; set; }
        [JsonPropertyName("step")] public int Step { get; set; }
        [JsonPropertyName("width")] public int Width { get; set; }
        [JsonPropertyName("name_row")] public int NameRow { get; set; }
        [JsonPropertyName("class_row")] public int ClassRow { get; set; }
        [JsonPropertyName("magic_row")] public int MagicRow { get; set; }
        [JsonPropertyName("hits_row")] public int HitsRow { get; set; }
        [JsonPropertyName("body_row")] public int BodyRow { get; set; }
        [JsonPropertyName("status_dx")] public int StatusDx { get; set; }
        [JsonPropertyName("marks")] public string? Marks { get; set; }
    }
}

/// <summary>
/// III's party columns (OUTDOORS3 600–635, STARTER 620–640), in text cells: member k's column starts at <see cref="Column"/>
/// + <see cref="Step"/>·k and is <see cref="Width"/> wide; the name (inverse) on <see cref="NameRow"/>, the class, magic and
/// hits "cur:max" (the Apple's '@' glyph shows ':'; a maximum hits of 100 or more puts ":max" on the next row), the body
/// letters (<see cref="Marks"/>: one per area state, OK / injured / broken / gone) from <see cref="BodyRow"/> and the
/// status word down column + <see cref="StatusDx"/>.
/// </summary>
public sealed record Apple2FieldBoxes(int Column, int Step, int Width, int NameRow, int ClassRow, int MagicRow, int HitsRow,
    int BodyRow, int StatusDx, string Marks);

/// <summary>
/// II's tossed rock on the Apple field (s226; OUTDOORS2 / DUNGON2): "X THROWS A ROCK" draws the thrower's arm 21 (1623
/// GOSUB 1760, no sparkle); a miss (1628) XDRAWs shape 59 at SCALE 1 at (<see cref="MissX"/>, <see cref="MissY"/>) for
/// <see cref="MissLoops"/> empty loops, then the line's PA pause (4150); a hit (1627, SP = 2) XDRAWs 59 on the monster at
/// its SCALE for <see cref="HitLoops"/> loops after "TAKES n DAMAGE" (4132: shape SP·35 − 11).
/// </summary>
public sealed record Apple2FieldRock(int MissX, int MissY, int MissLoops, int HitLoops);

/// <summary>A picture of the field: its file under the pack root and where the DRAW point sits in it.</summary>
public sealed record Apple2FieldPicture(string File, int OriginX, int OriginY);

/// <summary>
/// One monster's drawing numbers from its MN record: H% (x and HCOLOR), SCALE, YS% (rank height), both poses. III's
/// <c>MONSTER PLOT</c> sprites (s228) have no H% / SCALE / YS%: X 0, SCALE 1, the sprite's rows, and its S% in
/// <see cref="Shape"/>.
/// </summary>
public sealed record Apple2FieldMonster(int X, int Scale, int RankHeight, Apple2FieldPicture Idle, Apple2FieldPicture Attacking)
{
    /// <summary>III: the record's S% (pose A's sprite; 21 = a 42-row giant); 0 for I / II.</summary>
    public int Shape { get; init; }
}

/// <summary>
/// The Apple's own numbers the field needs (s224), from the import's <c>extraction-index.json</c> (written by
/// <c>P1Apple2Art</c> / II's <c>A2Graphics</c>): each monster's H% / SCALE / YS% and DRAW points, the figures by body and
/// pose, the sparkle and the spell hit at each SCALE the monsters use; II (s226) also the rock (shape 59) at each SCALE
/// (<see cref="RockHits"/>; SCALE 1 is the miss's). III (s228, <c>P3Apple2Art</c>'s index): the sprites (top-left at the
/// DRAW point), the figures by body and pose (idle, attacking, parrying, casting), every effect by its use
/// (<see cref="Effects"/>), every shape (<see cref="Shapes"/>) and the game's screen (<see cref="Frame"/>).
/// </summary>
public sealed record Apple2FieldArt(IReadOnlyDictionary<int, Apple2FieldMonster> Monsters,
    IReadOnlyDictionary<(int Body, string Pose), Apple2FieldPicture> Figures, Apple2FieldPicture? Sparkle,
    IReadOnlyDictionary<int, Apple2FieldPicture> Hits)
{
    /// <summary>The rock (II) by SCALE; empty when the index has none (I).</summary>
    public IReadOnlyDictionary<int, Apple2FieldPicture> RockHits { get; init; } = new Dictionary<int, Apple2FieldPicture>();

    /// <summary>The index's effects at SCALE 1 by their use ("arrow", "bow", "bow-fired", "body-down", …).</summary>
    public IReadOnlyDictionary<string, Apple2FieldPicture> Effects { get; init; } = new Dictionary<string, Apple2FieldPicture>();

    /// <summary>The index's shapes by number (III: every <c>SHAPES</c> shape alone, white).</summary>
    public IReadOnlyDictionary<int, Apple2FieldPicture> Shapes { get; init; } = new Dictionary<int, Apple2FieldPicture>();

    /// <summary>III: the game's screen (<c>SCREEN</c>, 280 × 192), or null.</summary>
    public Apple2FieldPicture? Frame { get; init; }

    public const string Idle = "idle", Attacking = "attacking", Casting = "casting", Parrying = "parrying";

    /// <summary>Reads the index at <paramref name="path"/>; null when it is missing or has no monsters.</summary>
    public static Apple2FieldArt? Load(string path)
    {
        if (!System.IO.File.Exists(path))
            return null;
        using var doc = JsonDocument.Parse(System.IO.File.ReadAllBytes(path));
        var root = doc.RootElement;
        static Apple2FieldPicture? Pic(JsonElement e) =>
            e.ValueKind != JsonValueKind.Object || !e.TryGetProperty("file", out var f) ? null
            : e.TryGetProperty("origin", out var o) && o.GetArrayLength() == 2
                ? new Apple2FieldPicture(f.GetString() ?? "", o[0].GetInt32(), o[1].GetInt32())
            : e.TryGetProperty("byte_column", out _) || e.TryGetProperty("size", out _)
                ? new Apple2FieldPicture(f.GetString() ?? "", 0, 0) // III's sprites and screens: drawn from their top-left
            : null;
        static int Int(JsonElement e, string name, int fallback) =>
            e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt32() : fallback;
        var monsters = new Dictionary<int, Apple2FieldMonster>();
        if (root.TryGetProperty("monsters", out var ms))
            foreach (var m in ms.EnumerateObject())
                if (int.TryParse(m.Name, out var id) && m.Value.TryGetProperty("idle", out var i) && Pic(i) is { } idle
                    && m.Value.TryGetProperty("attacking", out var a) && Pic(a) is { } act)
                    monsters[id] = new Apple2FieldMonster(Int(m.Value, "hcolor", 0), Int(m.Value, "scale", 1),
                        Int(m.Value, "group_height", Int(i, "rows", 0)), idle, act) { Shape = Int(m.Value, "shape", 0) };
        if (monsters.Count == 0)
            return null;
        var figures = new Dictionary<(int, string), Apple2FieldPicture>();
        if (root.TryGetProperty("figures", out var fs))
            foreach (var f in fs.EnumerateObject())
                if (f.Name.Split('.') is [var body, var pose] && int.TryParse(body, out var b) && Pic(f.Value) is { } p)
                    figures[(b, pose)] = p;
        Apple2FieldPicture? sparkle = null;
        var hits = new Dictionary<int, Apple2FieldPicture>();
        var rocks = new Dictionary<int, Apple2FieldPicture>();
        var effects = new Dictionary<string, Apple2FieldPicture>(StringComparer.Ordinal);
        if (root.TryGetProperty("effects", out var fx))
            foreach (var e in fx.EnumerateObject())
            {
                var use = e.Value.TryGetProperty("use", out var u) ? u.GetString() : null;
                var scale = e.Value.TryGetProperty("scale", out var s) ? s.GetInt32() : 1;
                if (use is not null && scale == 1 && Pic(e.Value) is { } effect)
                    effects.TryAdd(use, effect);
                if (use == "spell-sparkle")
                    sparkle = Pic(e.Value);
                else if (use == "spell-hit" && Pic(e.Value) is { } hit)
                    hits[scale] = hit;
                else if (use == "rock" && Pic(e.Value) is { } rock)
                    rocks[scale] = rock;
            }
        var shapes = new Dictionary<int, Apple2FieldPicture>();
        if (root.TryGetProperty("shapes", out var ss) && ss.ValueKind == JsonValueKind.Object)
            foreach (var e in ss.EnumerateObject())
                if (int.TryParse(e.Name, out var n) && Pic(e.Value) is { } shape)
                    shapes[n] = shape;
        var frame = root.TryGetProperty("screens", out var sc) && sc.ValueKind == JsonValueKind.Object
                    && sc.TryGetProperty("frame", out var fr) ? Pic(fr) : null;
        return new Apple2FieldArt(monsters, figures, sparkle, hits)
        {
            RockHits = rocks, Effects = effects, Shapes = shapes, Frame = frame,
        };
    }
}
