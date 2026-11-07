namespace OpenPhantasie.Core;

/// <summary>
/// How much travel time each wilderness step costs (data: <c>travel-time.json</c>). DOS OV2 <c>0x5f88..0x5fa7</c> [C]:
/// terrain class (<c>tile / 10</c>) 0..3 costs <c>(class + 1)·75</c>, every other class 60
/// (<c>overworld-movement.md</c> §Travel time). P0.7 tiles (s41, <c>map-tile-graphics.md</c>) [~]: 0 dotted open land,
/// 1 forest, 2 hills, 3 mountains; class 6 (black, the roads joining towns/inns) = path, costing 60 like water and
/// classes 8/9. The shipped JSON follows D-055 (manual: path is the fastest terrain) and charges class 6 less;
/// <see cref="Dos"/> keeps the traced DOS table.
/// </summary>
public sealed record TravelTimeRules(IReadOnlyList<int> StepCostByTerrainClass, int OtherStepCost, int WildernessInnDos)
{
    /// <summary>The traced DOS values; the shipped JSON holds the same numbers.</summary>
    public static TravelTimeRules Dos { get; } = new([75, 150, 225, 300], 60, 1000);

    /// <summary>Time added by one accepted (not blocked) wilderness step onto a tile of this terrain class.</summary>
    public int StepCost(int terrainClass) =>
        terrainClass >= 0 && terrainClass < StepCostByTerrainClass.Count
            ? StepCostByTerrainClass[terrainClass]
            : OtherStepCost;
}

/// <summary>
/// The DOS travel-time word <c>[0x63f2]</c> (<c>overworld-movement.md</c> §Travel time, <c>town-menus.md</c> §Aging) [C].
/// Only wilderness steps (and, in DOS, the wilderness inn) add to it; dungeon steps, combat and rest add nothing. Town
/// entry turns it into age and resets it (<see cref="Aging.ApplyTownEntry"/>).
/// </summary>
public static class TravelClock
{
    /// <summary>The counter is a u16.</summary>
    public const int Modulus = 0x10000;

    /// <summary><c>DAY OUT %u</c> prints <c>time &gt;&gt; 8</c> (OV2 <c>0x46c0</c>): 256 units per displayed day.</summary>
    public const int UnitsPerDay = 256;

    /// <summary>Town entry divides the days by 7 (OV1 <c>0x199c</c>); each result is one age-clock unit (two weeks).</summary>
    public const int DaysPerAgeUnit = 7;

    /// <summary>
    /// OV2 <c>0x6528</c>: adds in 16 bits. Its test against <c>0xffff</c> compares a value whose high word was zeroed,
    /// so it never fires — the counter wraps mod 65536 instead of saturating [C quirk, kept for 1:1].
    /// </summary>
    public static int Add(int time, int amount) => (time + amount) & (Modulus - 1);

    /// <summary>The party summary's <c>DAY OUT</c> value.</summary>
    public static int DaysOut(int time) => (time & (Modulus - 1)) / UnitsPerDay;

    /// <summary>Age-clock units added to every party slot on town entry: <c>(time &gt;&gt; 8) / 7</c>, unsigned (OV1 <c>0x1995</c>).</summary>
    public static int AgeUnits(int time) => DaysOut(time) / DaysPerAgeUnit;
}
