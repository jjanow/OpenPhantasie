namespace OpenPhantasie.Core;

public enum CardinalDirection
{
    North,
    South,
    East,
    West,
}

public enum OverworldMoveKind
{
    Moved,
    Blocked,
    EnterTown,
    EnterDungeon,
    LocationText,
    ScriptedEncounter,
    RandomEncounter,
    /// <summary>Tile 41: an independent inn (OV2 step returns 9 → <c>0x6700</c>, <see cref="WildernessInn"/>) [C] (s36).</summary>
    WildernessInn,
    /// <summary>Terrain class 11: "A MAGICAL POOL" (step returns 8 → OV2 <c>0x656b</c>) [C] (s39); applied by <see cref="MagicalPool"/> (D-051).</summary>
    SpecialSite,

    /// <summary>
    /// Terrain class 7 (tiles 74/75) once per trip (OV2 <c>0x6094</c>, <c>[0x6496]</c>) [C] (s39): a fight against
    /// <see cref="OverworldMoveResult.MonsterId"/> = <c>12·(tile % 10) + 9</c>. Later class-7 steps are plain moves.
    /// </summary>
    TileClassFight,

    /// <summary>
    /// Terrain class 10, water (step returns 10 → OV2 <c>0x685c</c>, <see cref="Swimming"/>) [C] (s39): every member rolls
    /// swim; no random encounter.
    /// </summary>
    Swim,

    /// <summary>
    /// Phantasie II molten lava (<see cref="TerrainKind.Lava"/>, <c>terrain.md</c> §4): every living member is burnt
    /// (<see cref="OpenPhantasie.Core.Lava"/>); no random encounter. A digit-0 lava cell is a plain <see cref="Moved"/>.
    /// </summary>
    Lava,
}

/// <summary>
/// Result of one wilderness step (OV2.OVR FUN_5EA2).
/// </summary>
public sealed class OverworldMoveResult
{
    public required OverworldMoveKind Kind { get; init; }
    public int RegionId { get; init; }
    public int X { get; init; }
    public int Y { get; init; }
    public int Tile { get; init; }
    public int? FeatureArg { get; init; }

    /// <summary>
    /// 0-based first monster of a fixed fight (<see cref="OverworldMoveKind.ScriptedEncounter"/>: feature <c>arg</c>,
    /// DOS <c>[0x64e6] = arg + 1</c>; <see cref="OverworldMoveKind.TileClassFight"/>: <c>12·T + 9</c>). Null otherwise.
    /// </summary>
    public int? MonsterId { get; init; }
    /// <summary>The location text joined with spaces (<see cref="LocationTextLines"/>); null when no line has text.</summary>
    public string? LocationText { get; init; }

    /// <summary>
    /// The 1–3 location-text lines of a <c>tile % 10 == 6</c> step, in display order (<see cref="OverworldTraversal.LocationTextPages"/>).
    /// Empty for every other kind.
    /// </summary>
    public IReadOnlyList<string> LocationTextLines { get; init; } = [];

    /// <summary>
    /// s153: <see cref="LocationTextLines"/> as stored (<see cref="OverworldRegion.RawTextPages"/>: blanks kept, every page
    /// even a blank one). A side channel for I Amiga's requester-23 box (FORMAT.md "The map screen's requester 23"); the
    /// log and <see cref="LocationText"/> keep the trimmed text.
    /// </summary>
    public IReadOnlyList<string> LocationTextRows { get; init; } = [];
    public int? TownId { get; init; }
    public int? DungeonId { get; init; }
    public int? DestX { get; init; }
    public int? DestY { get; init; }

    /// <summary>
    /// Travel time this step adds to <c>[0x63f2]</c> (OV2 <c>0x5f88</c>, <see cref="TravelTimeRules"/>): every accepted
    /// step, whatever it lands on; 0 when blocked [C].
    /// </summary>
    public int TimeCost { get; init; }
}

/// <summary>
/// One OUT*.DAT feature record (5 bytes at 0x208+).
/// </summary>
public sealed class OverworldFeature
{
    public int X { get; init; }
    public int Y { get; init; }
    public int Arg { get; init; }
    public int DestX { get; init; }
    public int DestY { get; init; }
}

/// <summary>
/// Mutable overworld region: Phantasie I / II's 26×20 maps, whose fog encodes as <c>visibleTile + 120</c>, or (Phase 09
/// T4) another game's map of its own size with raw tile ids and no fog (III: Scandor 50×75, the planes 9×9 and 31×16,
/// <c>planes.md</c> §2).
/// </summary>
public sealed class OverworldRegion
{
    /// <summary>Phantasie I / II region width (and the default <see cref="GridWidth"/>).</summary>
    public const int Width = 26;

    /// <summary>Phantasie I / II region height (and the default <see cref="GridHeight"/>).</summary>
    public const int Height = 20;
    public const int FogOffset = 120;
    public const int WallTile = 100;

    public int RegionId { get; }
    public string FileName { get; }

    /// <summary>This map's width in cells (<see cref="Width"/> for I / II).</summary>
    public int GridWidth { get; }

    /// <summary>This map's height in cells (<see cref="Height"/> for I / II).</summary>
    public int GridHeight { get; }

    /// <summary>
    /// True when tiles ≥ <see cref="FogOffset"/> are fogged copies (I / II). False for maps whose tile ids use the whole
    /// byte (III): nothing is fogged, <see cref="GetNormalized"/> returns the raw id and revealing does nothing.
    /// </summary>
    public bool HasFog { get; }

    /// <summary>Raw tile bytes (may include fog encoding).</summary>
    public byte[] Tiles { get; }

    public IReadOnlyList<OverworldFeature> Features { get; }
    public IReadOnlyList<string> TextPages { get; }

    /// <summary>
    /// s153: <see cref="TextPages"/> as stored (blanks kept, up to the NUL), same indices; empty when the data has none
    /// (then the trimmed pages stand in). Presentation only: I Amiga's requester-23 box places a page by its blanks.
    /// </summary>
    public IReadOnlyList<string> RawTextPages { get; set; } = [];

    public OverworldRegion(
        int regionId,
        string fileName,
        byte[] tiles,
        IReadOnlyList<OverworldFeature> features,
        IReadOnlyList<string> textPages)
        : this(regionId, fileName, tiles, features, textPages, Width, Height, hasFog: true)
    {
    }

    public OverworldRegion(
        int regionId,
        string fileName,
        byte[] tiles,
        IReadOnlyList<OverworldFeature> features,
        IReadOnlyList<string> textPages,
        int width,
        int height,
        bool hasFog)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(width, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(height, 1);
        if (tiles.Length != width * height)
            throw new ArgumentException($"Expected {width * height} tiles.", nameof(tiles));

        RegionId = regionId;
        FileName = fileName;
        Tiles = tiles;
        Features = features;
        TextPages = textPages;
        GridWidth = width;
        GridHeight = height;
        HasFog = hasFog;
    }

    public bool InBounds(int x, int y) => (uint)x < (uint)GridWidth && (uint)y < (uint)GridHeight;

    public byte GetRaw(int x, int y) => Tiles[y * GridWidth + x];

    public static int Normalize(int raw) => raw > FogOffset - 1 ? raw - FogOffset : raw;

    public int GetNormalized(int x, int y) => HasFog ? Normalize(GetRaw(x, y)) : GetRaw(x, y);

    public void Reveal(int x, int y)
    {
        if (!HasFog)
            return;
        var i = y * GridWidth + x;
        if (Tiles[i] > FogOffset - 1)
            Tiles[i] = (byte)(Tiles[i] - FogOffset);
    }

    public void RevealAround(int x, int y, int radius = 1)
    {
        for (var dy = -radius; dy <= radius; dy++)
        for (var dx = -radius; dx <= radius; dx++)
        {
            var nx = x + dx;
            var ny = y + dy;
            if (InBounds(nx, ny))
                Reveal(nx, ny);
        }
    }

    public OverworldFeature? FindFeature(int x, int y) =>
        Features.FirstOrDefault(f => f.X == x && f.Y == y);
}

/// <summary>
/// Party position on the Gelnor overworld mosaic.
/// </summary>
public sealed class OverworldState
{
    public static readonly HashSet<int> ValidRegionIds =
        [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 18, 19];

    /// <summary>
    /// Default position: the party-defeat wake cell OUT1 (20, 5) (OV1 <c>0x046a</c>, <c>endgame.md</c>) [C]. Not the new-game
    /// spawn: a new game opens in Pelnor and its first Exit uses the town's TWNS position (OUT1 (14,15), OV1 <c>0x3305</c>);
    /// the value only matters before that.
    /// </summary>
    public const int NewGameRegionId = 1;
    public const int NewGameX = 20;
    public const int NewGameY = 5;

    public int RegionId { get; set; } = NewGameRegionId;
    public int X { get; set; } = NewGameX;
    public int Y { get; set; } = NewGameY;

    /// <summary>
    /// <c>[0x649a]</c> = tile / 10 of the last tile stepped on (OV2 <c>0x5f7a</c>); FLEE and the monster-run roll read it,
    /// in dungeons too (stale). 0 before the first step: BSS zero-filled at startup (COM <c>0x4f94</c>) [C].
    /// </summary>
    public int TerrainClass { get; set; }

    /// <summary>
    /// <c>[0x6496]</c> [C] (s39): the class-7 tile fight (OV2 <c>0x6094</c>) already happened this trip. Set by that fight;
    /// cleared when the party leaves a town (OV1 <c>0x9b</c>/<c>0xa9</c>/<c>0xbe</c>) or a dungeon (OV2 <c>0x9b</c>).
    /// </summary>
    public bool TileClassFightDone { get; set; }
}

/// <summary>
/// Overworld traversal — OV2.OVR FUN_5EA2 / FUN_61CE.
/// </summary>
public sealed class OverworldTraversal
{
    private readonly Dictionary<int, OverworldRegion> _regions;

    public TravelTimeRules TravelTime { get; }

    /// <summary>
    /// Town tile feature args that name another town (<see cref="ICampaign.OverworldTownAliases"/>; I: 99 → OLYMPIA 12).
    /// Any other arg is the Core town id as-is.
    /// </summary>
    public IReadOnlyDictionary<int, int> TownAliases { get; }

    /// <summary>The game's terrain-class rules (<see cref="TerrainRules"/>; default Phantasie I's).</summary>
    public TerrainRules Terrain { get; }

    public OverworldTraversal(
        IEnumerable<OverworldRegion> regions, TravelTimeRules? travelTime = null,
        IReadOnlyDictionary<int, int>? townAliases = null, TerrainRules? terrain = null)
    {
        TravelTime = travelTime ?? TravelTimeRules.Dos;
        Terrain = terrain ?? TerrainRules.PhantasieI;
        TownAliases = townAliases ?? new Dictionary<int, int>();
        _regions = regions.ToDictionary(r => r.RegionId);
        if (_regions.Count == 0)
            throw new ArgumentException("At least one overworld region is required.");
    }

    /// <summary>Every loaded region (session save of the raw bytes).</summary>
    public IEnumerable<OverworldRegion> Regions => _regions.Values;

    /// <summary>True when region <paramref name="regionId"/> is one of the loaded maps (I: 1..16, 18, 19; II: its 17 files).</summary>
    public bool HasRegion(int regionId) => _regions.ContainsKey(regionId);

    public OverworldRegion GetRegion(int regionId) =>
        _regions.TryGetValue(regionId, out var r)
            ? r
            : throw new KeyNotFoundException($"Unknown overworld region {regionId}.");

    public static (int Dx, int Dy) ToDelta(CardinalDirection dir) => dir switch
    {
        CardinalDirection.East => (1, 0),
        CardinalDirection.West => (-1, 0),
        CardinalDirection.South => (0, -1), // ny = y - dy → y+1
        CardinalDirection.North => (0, 1),  // ny = y - dy → y-1
        _ => throw new ArgumentOutOfRangeException(nameof(dir)),
    };

    /// <summary>
    /// Region id after crossing an edge: <c>region - 4*dx - dy</c>.
    /// </summary>
    public static int AdjacentRegion(int regionId, int dx, int dy) =>
        regionId - 4 * dx - dy;

    public OverworldMoveResult TryMove(
        OverworldState state,
        CardinalDirection direction,
        IRandomSource? encounterRng = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        var (dx, dy) = ToDelta(direction);
        var region = GetRegion(state.RegionId);

        var nx = state.X + dx;
        var ny = state.Y - dy;
        var crossed = false;

        if (!region.InBounds(nx, ny))
        {
            var nextId = AdjacentRegion(state.RegionId, dx, dy);
            if (!_regions.ContainsKey(nextId)) // the loaded regions (I: 1..16, 18, 19 = OverworldState.ValidRegionIds; II: its 17 files)
            {
                // [?] (s39) DOS has no range check: 0x61ce saves the region and loads "OUT%-d.DAT" for any id. On the
                // shipped maps only OUT1's north edge (24 water cells) leads to a missing file (OUT0); what the COM file
                // reader 0xc75a does then is undecoded — [D+]. Core treats it as blocked.
                return Blocked(state, region.GetNormalized(state.X, state.Y));
            }

            // Arrive on the opposite edge.
            var next = GetRegion(nextId);
            if (dy == 0)
                nx = dx < 0 ? next.GridWidth - 1 : 0;
            else
                ny = dy > 0 ? next.GridHeight - 1 : 0;

            state.RegionId = nextId;
            region = next;
            crossed = true;
        }

        region.RevealAround(nx, ny);
        var tile = region.GetNormalized(nx, ny);

        if (tile == OverworldRegion.WallTile)
        {
            if (crossed)
            {
                // [C quirk] (s39) OV2 0x61ce has already written the arrival edge coordinate into the party position and
                // loaded the new region; 0x6188 then skips the "blocked" message and the step-back when [bp-6] (crossed)
                // is set, and returns 2. So the party ends up standing on the wall cell of the new region: no time, no
                // dispatch, no message. 13 edge cells do this (e.g. OUT13 (9,0) north → OUT12 (9,19)).
                state.X = nx;
                state.Y = ny;
                return new OverworldMoveResult
                {
                    Kind = OverworldMoveKind.Moved,
                    RegionId = state.RegionId,
                    X = state.X,
                    Y = state.Y,
                    Tile = tile,
                };
            }

            return Blocked(state, tile);
        }

        state.X = nx;
        state.Y = ny;

        var terrainClass = tile / 10;
        var subcode = tile % 10;
        state.TerrainClass = terrainClass;
        // [C] OV2 0x5f88: the time is added before any dispatch. The target was un-fogged before the read (0x53a5), as
        // Core's RevealAround does (overworld-movement.md, Fog note).
        var cost = TravelTime.StepCost(terrainClass);
        var feature = region.FindFeature(nx, ny);

        if (tile == 40)
        {
            var townId = feature?.Arg ?? 0;
            // [C] (s49) OV2 0x611a: [0x64ee] := arg − 1 (0-based TWNS index); Core town ids are 1-based (arg as-is). The
            // campaign's aliases map the rest (I: arg 99 at OUT18 (10,5) → OLYMPIA 12, Phantasie1Campaign).
            if (TownAliases.TryGetValue(townId, out var alias)) townId = alias;
            return new OverworldMoveResult
            {
                Kind = OverworldMoveKind.EnterTown,
                TimeCost = cost,
                RegionId = state.RegionId,
                X = state.X,
                Y = state.Y,
                Tile = tile,
                FeatureArg = feature?.Arg,
                TownId = townId,
            };
        }

        if (tile == 41)
        {
            // [C] s36: tile 41 is the wilderness inn (OV2 0x6149 → state 9 → 0x6700), not a transition.
            return new OverworldMoveResult
            {
                Kind = OverworldMoveKind.WildernessInn,
                TimeCost = cost,
                RegionId = state.RegionId,
                X = state.X,
                Y = state.Y,
                Tile = tile,
                FeatureArg = feature?.Arg,
            };
        }

        if (terrainClass == 5)
        {
            return new OverworldMoveResult
            {
                Kind = OverworldMoveKind.EnterDungeon,
                TimeCost = cost,
                RegionId = state.RegionId,
                X = state.X,
                Y = state.Y,
                Tile = tile,
                FeatureArg = feature?.Arg,
                DungeonId = feature?.Arg,
                DestX = feature?.DestX,
                DestY = feature?.DestY,
            };
        }

        if (subcode == 6)
        {
            // [C] (s41) OV2 0x5fae..0x603c: the feature arg picks 1, 2 or 3 consecutive 40-byte pages, printed, then
            // return 2 — no encounter roll. Temples, cathedrals, signs and inn names all go through here.
            // [~] A missing feature record would read the arg from region +0x255 (0x6214 falls off); no shipped x6
            // tile lacks one (s41 scan), Core uses page 0 then.
            var lines = LocationTextPages(feature?.Arg ?? 0)
                .Select(p => (uint)p < (uint)region.TextPages.Count ? region.TextPages[p] : "")
                .ToArray();
            var text = string.Join(" ", lines.Where(l => l.Length > 0));
            // s153: the same pages as stored (I Amiga's box rows; h59 +0x182 shows every page, blank ones too).
            var rows = LocationTextPages(feature?.Arg ?? 0)
                .Select(p => (uint)p < (uint)region.RawTextPages.Count ? region.RawTextPages[p]
                    : (uint)p < (uint)region.TextPages.Count ? region.TextPages[p] : "")
                .ToArray();
            return new OverworldMoveResult
            {
                Kind = OverworldMoveKind.LocationText,
                TimeCost = cost,
                RegionId = state.RegionId,
                X = state.X,
                Y = state.Y,
                Tile = tile,
                FeatureArg = feature?.Arg,
                LocationText = text.Length > 0 ? text : null,
                LocationTextLines = lines,
                LocationTextRows = rows,
            };
        }

        if (subcode == 7)
        {
            // [C] OV2 0x6045..0x6073: [0x64e6] := (arg + 1) & 0xff (1-based, so Core id = arg), then `inc byte [cell]`
            // before the fight: the tile becomes x8 (spent, plain move) — fixed encounters are one-shot. Flags are not
            // written (stale, see GameSession.SurpriseGlobals). [~] With no feature record DOS reads the byte at region
            // +0x255 (0x6214 falls off its loop); no shipped x7 tile lacks one, so Core makes no fight then.
            region.Tiles[ny * region.GridWidth + nx]++;
            int? monster = feature is null ? null : ((feature.Arg + 1) & 0xff) - 1;
            return new OverworldMoveResult
            {
                Kind = OverworldMoveKind.ScriptedEncounter,
                TimeCost = cost,
                RegionId = state.RegionId,
                X = state.X,
                Y = state.Y,
                Tile = tile,
                FeatureArg = feature is null ? null : feature.Arg + 1,
                MonsterId = monster is >= 0 ? monster : null,
            };
        }

        if (subcode == 8)
        {
            // [C] OV2 0x608a: a spent fixed encounter — plain move, no roll.
            return new OverworldMoveResult
            {
                Kind = OverworldMoveKind.Moved,
                TimeCost = cost,
                RegionId = state.RegionId,
                X = state.X,
                Y = state.Y,
                Tile = tile,
                FeatureArg = feature?.Arg,
            };
        }

        // Terrain classes with a rule of their own (TerrainRules, per game). They never reach the random-encounter roll.
        var rule = Terrain.For(terrainClass);
        switch (rule.Kind)
        {
            case TerrainKind.FixedFightOncePerTrip:
            case TerrainKind.TerrainFight:
            {
                // I: [C] OV2 0x6094..0x60c2, one certain fight per trip. II: [C][L] PHANT.PRG 0x6338, while the flag is
                // clear r ≤ 8 (≈ 9 %) fights 12·T + r′/34 (0-based) and sets the flag; the same three events clear it.
                int? monster = null;
                if (!state.TileClassFightDone)
                {
                    if (rule.Kind == TerrainKind.FixedFightOncePerTrip)
                        monster = rule.MonsterPerDigit * subcode + rule.MonsterOffset;
                    else if (encounterRng is not null && Terrain.RollOnce(encounterRng) <= rule.FightRollMax)
                        monster = rule.MonsterPerDigit * subcode + rule.MonsterOffset
                                  + Terrain.RollOnce(encounterRng) / rule.MonsterSpread;
                }
                if (monster is null)
                    return Plain(state, tile, cost, feature);
                state.TileClassFightDone = true;
                return new OverworldMoveResult
                {
                    Kind = OverworldMoveKind.TileClassFight,
                    TimeCost = cost,
                    RegionId = state.RegionId,
                    X = state.X,
                    Y = state.Y,
                    Tile = tile,
                    FeatureArg = feature?.Arg,
                    MonsterId = monster,
                };
            }
            case TerrainKind.SpecialSite:
                return new OverworldMoveResult
                {
                    Kind = OverworldMoveKind.SpecialSite,
                    TimeCost = cost,
                    RegionId = state.RegionId,
                    X = state.X,
                    Y = state.Y,
                    Tile = tile,
                    FeatureArg = feature?.Arg,
                };
            case TerrainKind.Swim:
                // Water (class 10): OV2 0x6156 returns state 10 = swimming (0x685c) before the random-encounter roll.
                return new OverworldMoveResult
                {
                    Kind = OverworldMoveKind.Swim,
                    TimeCost = cost,
                    RegionId = state.RegionId,
                    X = state.X,
                    Y = state.Y,
                    Tile = tile,
                };
            case TerrainKind.Lava:
                // [C][L] II terrain.md §4: digit 0 (tile 80) is harmless; digits 6 and 8 never get here (text, plain move).
                return subcode == 0
                    ? Plain(state, tile, cost, feature)
                    : new OverworldMoveResult
                    {
                        Kind = OverworldMoveKind.Lava,
                        TimeCost = cost,
                        RegionId = state.RegionId,
                        X = state.X,
                        Y = state.Y,
                        Tile = tile,
                        FeatureArg = feature?.Arg,
                    };
        }

        if (encounterRng is not null && IsOrdinaryTerrain(terrainClass, subcode, tile))
        {
            // I: OV2 FUN_655B, roll 0..99 via raw/0x148; II: rand/327 (0..100). Encounter if roll < 6.
            var roll = Terrain.RollOnce(encounterRng);
            if (roll < Terrain.RandomEncounterBelow)
            {
                return new OverworldMoveResult
                {
                    Kind = OverworldMoveKind.RandomEncounter,
                    TimeCost = cost,
                    RegionId = state.RegionId,
                    X = state.X,
                    Y = state.Y,
                    Tile = tile,
                };
            }
        }

        return new OverworldMoveResult
        {
            Kind = OverworldMoveKind.Moved,
            TimeCost = cost,
            RegionId = state.RegionId,
            X = state.X,
            Y = state.Y,
            Tile = tile,
            FeatureArg = feature?.Arg,
        };
    }

    /// <summary>OV2 <c>0x60a0</c>: <c>[0x64e6] = 12·T + 10</c> (1-based) → 0-based <c>12·T + 9</c> (tile 75 GREAT TROLL, 74 WARRIOR).</summary>
    /// <summary>
    /// The text pages a location-text feature shows [C] (s41, OV2 <c>0x5fba..0x6039</c>): <c>arg &lt; 10</c> → page
    /// <c>arg</c> (one line, <c>0xda83</c>); <c>10..19</c> → pages <c>arg−10</c>, <c>arg−9</c> (<c>0xdad1</c>);
    /// <c>≥ 20</c> → pages <c>arg−20 .. arg−18</c> (<c>0xdb41</c>). Page <c>p</c> = region bytes <c>0x253 + 40·p</c>.
    /// </summary>
    public static IReadOnlyList<int> LocationTextPages(int arg) => arg switch
    {
        < 10 => [arg],
        < 20 => [arg - 10, arg - 9],
        _ => [arg - 20, arg - 19, arg - 18],
    };

    public static int TileClassFightMonster(int terrainSubcode) => 12 * terrainSubcode + 9;

    /// <remarks>
    /// <c>tile % 10 == 8</c> is a plain move with no roll [C] (OV2 <c>0x608a</c>, s36). Class 7 never rolls either (s39).
    /// [~] DOS checks the sub-code cases (6/7/8) before the class cases (5/7/11) and the tiles 40/41; Core's order differs
    /// only for tiles no map uses.
    /// </remarks>
    private bool IsOrdinaryTerrain(int terrainClass, int subcode, int tile) =>
        tile != 40 && tile != 41
        && terrainClass != 5 && Terrain.For(terrainClass).Kind == TerrainKind.Ordinary
        && subcode != 6 && subcode != 7 && subcode != 8;

    private static OverworldMoveResult Plain(OverworldState state, int tile, int cost, OverworldFeature? feature) => new()
    {
        Kind = OverworldMoveKind.Moved,
        TimeCost = cost,
        RegionId = state.RegionId,
        X = state.X,
        Y = state.Y,
        Tile = tile,
        FeatureArg = feature?.Arg,
    };

    private static OverworldMoveResult Blocked(OverworldState state, int tile) => new()
    {
        Kind = OverworldMoveKind.Blocked,
        RegionId = state.RegionId,
        X = state.X,
        Y = state.Y,
        Tile = tile,
    };
}

public enum OverworldEntryKind
{
    Town,
    Dungeon,
}

/// <summary>
/// "A TOWN. ENTER?" / "A DUNGEON. ENTER?" (manual p.12; DOS OV2 <c>0x6115</c> / menu <c>0x1c</c>; the town prompt has its own menu, <c>0xe</c> <c>[~]</c>): a town id (1-based) or
/// a dungeon level id with its arrival cell.
/// </summary>
public sealed record OverworldEntryPrompt(OverworldEntryKind Kind, int Id, int DestX, int DestY)
{
    public static string TownText => GameText.S("core.overworld.a_town_enter");
    public static string DungeonText => GameText.S("core.overworld.a_dungeon_enter");

    /// <summary>
    /// The place's own name (Phantasie III asks "&lt;name&gt; Will you enter?", ovl3 <c>0x6c8</c>); null = I's generic
    /// question. Saved with the prompt (<see cref="WildernessSnapshot.EntryName"/>, s95).
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// The dungeon was entered and DOS III's next question waits: "Load previously saved dungeon?" (ovl4 <c>0x6fe</c>, DS
    /// <c>0x62ec</c>), asked on every entry under <c>p3.dungeon-map-no</c> = <c>dos</c> (D3-038). Yes loads the saved map,
    /// No the original.
    /// </summary>
    public bool AsksLoad { get; init; }

    public string Text => AsksLoad
        ? GameText.S("core.p3.load_saved_dungeon")
        : Name is { } name
            ? GameText.F("core.p3.will_you_enter", name)
            : Kind == OverworldEntryKind.Town ? TownText : DungeonText;
}
