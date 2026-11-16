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
            TextRows = dto.TextRows, Columns = dto.Columns, CellWidth = dto.CellWidth, CellHeight = dto.CellHeight,
            Index = dto.Index!, LineLoopsPerTimeLag = dto.LineLoopsPerTimeLag, HitLoops = dto.HitLoops,
            LoopMs = dto.LoopMs, SparkleMs = dto.SparkleMs,
            Bodies = new Apple2FieldBodies(b.MonsterRacesFrom, b.MonsterBody, b.HumanElf, b.SmallBody, b.PlainBody,
                b.RobedClasses, b.RobedBody, b.ElementalBody),
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
    }
}

/// <summary>
/// TOWN 2350–2356 (s224): a member's body shape (record byte 168), set at creation: races from
/// <see cref="MonsterRacesFrom"/> on → <see cref="MonsterBody"/>; races other than <see cref="HumanElf"/> →
/// <see cref="SmallBody"/>; HUMAN / ELF → <see cref="RobedBody"/> for <see cref="RobedClasses"/>, else
/// <see cref="PlainBody"/>. The elementals (CHAR records 37–39) hold <see cref="ElementalBody"/>.
/// </summary>
public sealed record Apple2FieldBodies(int MonsterRacesFrom, int MonsterBody, IReadOnlyList<int> HumanElf, int SmallBody,
    int PlainBody, IReadOnlyList<int> RobedClasses, int RobedBody, int ElementalBody)
{
    /// <summary>The body for race id <paramref name="race"/> and class id <paramref name="cls"/> (the Apple's order = DOS's).</summary>
    public int For(int race, int cls) =>
        race >= MonsterRacesFrom ? MonsterBody
        : !HumanElf.Contains(race) ? SmallBody
        : RobedClasses.Contains(cls) ? RobedBody : PlainBody;
}

/// <summary>
/// II's tossed rock on the Apple field (s226; OUTDOORS2 / DUNGON2): "X THROWS A ROCK" draws the thrower's arm 21 (1623
/// GOSUB 1760, no sparkle); a miss (1628) XDRAWs shape 59 at SCALE 1 at (<see cref="MissX"/>, <see cref="MissY"/>) for
/// <see cref="MissLoops"/> empty loops, then the line's PA pause (4150); a hit (1627, SP = 2) XDRAWs 59 on the monster at
/// its SCALE for <see cref="HitLoops"/> loops after "TAKES n DAMAGE" (4132: shape SP·35 − 11).
/// </summary>
public sealed record Apple2FieldRock(int MissX, int MissY, int MissLoops, int HitLoops);

/// <summary>A picture of the field: its file under the pack root and where the DRAW point sits in it.</summary>
public sealed record Apple2FieldPicture(string File, int OriginX, int OriginY);

/// <summary>One monster's drawing numbers from its MN record: H% (x and HCOLOR), SCALE, YS% (rank height), both poses.</summary>
public sealed record Apple2FieldMonster(int X, int Scale, int RankHeight, Apple2FieldPicture Idle, Apple2FieldPicture Attacking);

/// <summary>
/// The Apple's own numbers the field needs (s224), from the import's <c>extraction-index.json</c> (written by
/// <c>P1Apple2Art</c> / II's <c>A2Graphics</c>): each monster's H% / SCALE / YS% and DRAW points, the figures by body and
/// pose, the sparkle and the spell hit at each SCALE the monsters use; II (s226) also the rock (shape 59) at each SCALE
/// (<see cref="RockHits"/>; SCALE 1 is the miss's).
/// </summary>
public sealed record Apple2FieldArt(IReadOnlyDictionary<int, Apple2FieldMonster> Monsters,
    IReadOnlyDictionary<(int Body, string Pose), Apple2FieldPicture> Figures, Apple2FieldPicture? Sparkle,
    IReadOnlyDictionary<int, Apple2FieldPicture> Hits)
{
    /// <summary>The rock (II) by SCALE; empty when the index has none (I).</summary>
    public IReadOnlyDictionary<int, Apple2FieldPicture> RockHits { get; init; } = new Dictionary<int, Apple2FieldPicture>();

    public const string Idle = "idle", Attacking = "attacking", Casting = "casting";

    /// <summary>Reads the index at <paramref name="path"/>; null when it is missing or has no monsters.</summary>
    public static Apple2FieldArt? Load(string path)
    {
        if (!System.IO.File.Exists(path))
            return null;
        using var doc = JsonDocument.Parse(System.IO.File.ReadAllBytes(path));
        var root = doc.RootElement;
        static Apple2FieldPicture? Pic(JsonElement e) =>
            e.ValueKind == JsonValueKind.Object && e.TryGetProperty("file", out var f) && e.TryGetProperty("origin", out var o)
                && o.GetArrayLength() == 2
                ? new Apple2FieldPicture(f.GetString() ?? "", o[0].GetInt32(), o[1].GetInt32()) : null;
        var monsters = new Dictionary<int, Apple2FieldMonster>();
        if (root.TryGetProperty("monsters", out var ms))
            foreach (var m in ms.EnumerateObject())
                if (int.TryParse(m.Name, out var id) && m.Value.TryGetProperty("idle", out var i) && Pic(i) is { } idle
                    && m.Value.TryGetProperty("attacking", out var a) && Pic(a) is { } act)
                    monsters[id] = new Apple2FieldMonster(m.Value.GetProperty("hcolor").GetInt32(),
                        m.Value.GetProperty("scale").GetInt32(), m.Value.GetProperty("group_height").GetInt32(), idle, act);
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
        if (root.TryGetProperty("effects", out var fx))
            foreach (var e in fx.EnumerateObject())
            {
                var use = e.Value.TryGetProperty("use", out var u) ? u.GetString() : null;
                var scale = e.Value.TryGetProperty("scale", out var s) ? s.GetInt32() : 1;
                if (use == "spell-sparkle")
                    sparkle = Pic(e.Value);
                else if (use == "spell-hit" && Pic(e.Value) is { } hit)
                    hits[scale] = hit;
                else if (use == "rock" && Pic(e.Value) is { } rock)
                    rocks[scale] = rock;
            }
        return new Apple2FieldArt(monsters, figures, sparkle, hits) { RockHits = rocks };
    }
}
