namespace OpenPhantasie.Core;

/// <summary>What a hard-coded wilderness square is (<c>planes.md</c> §4: type 1 town, 2 dungeon, 3 independent inn).</summary>
public enum P3PlaceKind
{
    Town = 1,
    Dungeon = 2,
    Inn = 3,
}

/// <summary>One hard-coded place square (ovl3 <c>0xa22</c>): its plane map, square, kind, number and name.</summary>
public sealed record P3Place(int RegionId, int X, int Y, P3PlaceKind Kind, int Number, string Name);

/// <summary>A tile-id range and the terrain index it gives (ovl3 <c>0x375</c>).</summary>
public sealed record P3TerrainRange(int First, int Last, int Terrain);

/// <summary>
/// III's days-out counter (<c>FAR:0x9e2</c>, kept in <see cref="GameSession.TravelTime"/>) counts thirds of a day
/// (<c>planes.md</c> §3a) [C] (s88): a wilderness step adds by the terrain of the square being left (root <c>0x14a9</c>:
/// Forest ⅔ day, Hills and Mountains 1 day, anything else ⅓), a blocked step too; a dungeon step that moves adds ⅓ (ovl4
/// <c>0xc02</c>), a bump into a wall nothing. Entering a town ages every member not Dead by one unit (2 weeks) per 14 full
/// days out (ovl3 <c>0x65a</c>); the rest is dropped as the counter starts again.
/// </summary>
public static class P3TravelTime
{
    public const int ThirdsPerDay = 3;

    /// <summary>Full days out per age-clock unit on town entry.</summary>
    public const int DaysPerAgeUnit = 14;

    /// <summary>Terrain numbers with a dearer step (<c>planes.md</c> §3).</summary>
    public const int Forest = 1, Hills = 2, Mountains = 3;

    /// <summary>A dungeon step that moves (the terrain word is 4 inside, ovl4 <c>0x9f0</c>).</summary>
    public const int DungeonStep = 1;

    /// <summary>Thirds of a day one wilderness step costs, by the terrain of the square being left.</summary>
    public static int StepCost(int terrain) => terrain switch
    {
        Forest => 2,
        Hills or Mountains => 3,
        _ => 1,
    };

    /// <summary>The Party Status screen's days out (root <c>0x1c6d</c>: the counter ÷ 3).</summary>
    public static int DaysOut(int thirds) => thirds / ThirdsPerDay;

    /// <summary>Age-clock units town entry adds: <c>⌊⌊thirds / 3⌋ / 14⌋</c>.</summary>
    public static int AgeUnits(int thirds) => DaysOut(thirds) / DaysPerAgeUnit;
}

/// <summary>
/// III's terrain by tile id (<c>planes.md</c> §3): the first matching range wins, the Netherworld's overrides first, any
/// other tile is terrain <see cref="Other"/>; tiles from <see cref="BlockedFrom"/> are blocked (the River Styx / Ocean).
/// </summary>
public sealed record P3Terrain(
    IReadOnlyList<P3TerrainRange> Ranges,
    IReadOnlyList<P3TerrainRange> NetherworldOverrides,
    int Other,
    int BlockedFrom,
    IReadOnlyList<int> NoEncounter,
    IReadOnlyList<string> Names)
{
    /// <summary>Terrain 10: water (a swim test on foot).</summary>
    public const int Water = 10;

    /// <summary>Terrain 8: lava (Netherworld), hurts the party after a move.</summary>
    public const int Lava = 8;

    /// <summary>Terrain 15: the River Styx (blocked).</summary>
    public const int Styx = 15;

    public int TerrainOf(int tile, bool netherworld)
    {
        if (netherworld)
            foreach (var r in NetherworldOverrides)
                if (tile >= r.First && tile <= r.Last)
                    return r.Terrain;
        foreach (var r in Ranges)
            if (tile >= r.First && tile <= r.Last)
                return r.Terrain;
        return Other;
    }

    public bool IsBlocked(int tile) => tile >= BlockedFrom;

    /// <summary>The terrain's name (master string 54 + t); empty for the unnamed ones.</summary>
    public string NameOf(int terrain) => (uint)terrain < (uint)Names.Count ? Names[terrain] : "";
}

/// <summary>
/// III's wilderness encounter numbers (s83, root <c>0xde54</c>, <c>encounters.md</c> §7, <c>rule-tables.json</c>
/// <c>wilderness_encounters</c>) [C].
/// </summary>
public sealed record P3WildernessRules(
    int Chance,
    int NightBelow,
    int Kind2Below,
    int SleepingBelow,
    int NoticePerLevel,
    int SurpriseBelow,
    int PartySurpriseBelow,
    int RunAwayBelow,
    IReadOnlyList<int> AreaLevels,
    IReadOnlyList<int> TerrainOffsets)
{
    /// <summary>The area level L of square (x, y) (root <c>0x1198</c>): <c>levels[x / 10 + 5·(y / 15)]</c>.</summary>
    public int AreaLevel(int x, int y)
    {
        var i = x / 10 + 5 * (y / 15);
        return (uint)i < (uint)AreaLevels.Count ? AreaLevels[i] : 0;
    }

    /// <summary>
    /// The monster pick (root <c>0xf143</c>), 0-based: kind 1 (night) <c>12L + {0, 1, 4}</c>, kind 2 <c>12L + rnd(10) + 1</c>,
    /// kind 3 <c>12L + offsets[t + rnd(4)]</c>; then ids &gt; 74 or &lt; 0 become 11 and 61 becomes 60.
    /// </summary>
    public int PickMonster(int kind, int level, int terrain, IRandomSource rng)
    {
        int r;
        switch (kind)
        {
            case 1:
                r = P3Dice.Rnd(rng, 3) - 1;
                if (r == 2) r = 4;
                break;
            case 2:
                r = P3Dice.Rnd(rng, 10) + 1;
                break;
            default:
                var i = P3Dice.Rnd(rng, 4) + terrain;
                r = (uint)i < (uint)TerrainOffsets.Count ? TerrainOffsets[i] : 0;
                break;
        }
        var id = 12 * level + r;
        if (id is > 74 or < 0) id = 11;
        if (id == 61) id = 60;
        return id;
    }
}

/// <summary>
/// The Phantasie III world and rule data the campaign needs beyond the shared tables (built by <c>OpenPhantasie.Data</c> from
/// <c>GameData/phantasie3</c>): places, tile terrain, the planes, the wilderness encounter tables, dungeon entrances, the
/// training and skill tables, the class kit, social classes, score weights and the hit-location table.
/// </summary>
public sealed class P3WorldData
{
    public required IReadOnlyList<P3Place> Places { get; init; }
    public required P3Terrain Terrain { get; init; }
    public required P3WildernessRules Wilderness { get; init; }

    /// <summary>Plane map region id → its plane name (<c>scandor</c>, <c>light</c>, <c>dark</c>, <c>netherworld</c>).</summary>
    public required IReadOnlyDictionary<int, string> PlaneNames { get; init; }

    /// <summary>Plane map region id → the square a party arriving from another plane lands on.</summary>
    public required IReadOnlyDictionary<int, (int X, int Y)> PlaneArrivals { get; init; }

    /// <summary>The square a new game starts on (Scandor (24, 37), Pendragon).</summary>
    public required (int RegionId, int X, int Y) Start { get; init; }

    /// <summary>Entrance x of dungeon n at index n − 1 (y = 0).</summary>
    public required IReadOnlyList<int> DungeonEntranceX { get; init; }

    /// <summary>The training tables with D3-032's five cells as Phantasie I has them (the default).</summary>
    public required P3TrainingTables Training { get; init; }

    /// <summary>The training tables as DOS reads them (<c>p3.skill-tables</c> = <c>dos</c>).</summary>
    public required P3TrainingTables TrainingAsRead { get; init; }

    public required P3HitLocationTable HitLocations { get; init; }

    /// <summary>The party's Fireflash I–IV numbers (D3-075; <c>rule-tables.json</c> <c>fireflash</c>).</summary>
    public P3FireflashTable Fireflash { get; init; } = P3FireflashTable.Default;

    /// <summary>A first group of one of these sets the "tall monster" flag: every hit on a monster uses row 4 (0-based ids).</summary>
    public IReadOnlySet<int> TallMonsterIds { get; init; } = new HashSet<int>();

    /// <summary>Monsters whose injury lines say "'s body" for every area (snakes, slimes, blobs, moulds; 0-based ids).</summary>
    public IReadOnlySet<int> LimblessMonsterIds { get; init; } = new HashSet<int>();

    public required (int Shield, int Armor, int Weapon) Kit { get; init; }
    public required IReadOnlyDictionary<CharacterClass, int> KitBow { get; init; }

    /// <summary>First spell by class, as a spell number (1-based; 0 = none).</summary>
    public required IReadOnlyDictionary<CharacterClass, int> FirstSpell { get; init; }

    public required IReadOnlyList<string> SocialClassNames { get; init; }
    public required IReadOnlyList<int> SocialClassThresholds { get; init; }
    public required int SocialClassGoldPerClass { get; init; }
    public required IReadOnlyList<Race> SocialClassBaseRaces { get; init; }
    public required (int BaseRaces, int Others) SocialClassRollDie { get; init; }

    public required IReadOnlyList<int> ScoreWeights { get; init; }

    /// <summary>Spell count (III: 57, Divine Aid the last).</summary>
    public required int SpellCount { get; init; }

    /// <summary>The spell names by 0-based index (master strings 331 + n), for the party's "X casts Y" line.</summary>
    public IReadOnlyList<string> SpellNames { get; init; } = [];

    /// <summary>D3-033: Apple II's Armory restock; null = none (the stock changes only by buying and selling).</summary>
    public P3ArmoryRestockRule? ArmoryRestock { get; init; }

    /// <summary>
    /// D3-071: the found item a paying town entry takes out of the found list before the hand-out (DOS ovl2 <c>0x5a9</c>:
    /// 180, the Wand of Nikademus, the last one only); null = none.
    /// </summary>
    public int? TownEntryDroppedItem { get; init; }

    /// <summary>The I / II → III transfer's values (<see cref="Phantasie3Campaign.AdmitTransferred"/>); null = no transfer.</summary>
    public P3TransferRule? Transfer { get; init; }

    /// <summary>Age and maximum age at creation (<see cref="P3CreationAgeRule"/>); null = I's start age, 10 years.</summary>
    public P3CreationAgeRule? CreationAge { get; init; }

    /// <summary>Scroll item id → the scroll text records it shows (<c>planes.md</c> §7).</summary>
    public IReadOnlyDictionary<int, IReadOnlyList<int>> ScrollItemRecords { get; init; } =
        new Dictionary<int, IReadOnlyList<int>>();

    public P3TrainingTables TrainingFor(GameOptions rules) => rules.P3DosSkillTables() ? TrainingAsRead : Training;

    public P3Place? PlaceAt(int regionId, int x, int y) =>
        Places.FirstOrDefault(p => p.RegionId == regionId && p.X == x && p.Y == y);

    public P3Place? Town(int number) => Places.FirstOrDefault(p => p.Kind == P3PlaceKind.Town && p.Number == number);

    public P3Place? Dungeon(int number) => Places.FirstOrDefault(p => p.Kind == P3PlaceKind.Dungeon && p.Number == number);

    public bool IsNetherworld(int regionId) => PlaneNames.TryGetValue(regionId, out var n) && n == "netherworld";
}
