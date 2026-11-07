namespace OpenPhantasie.Core;

/// <summary>
/// A Phantasie III fight's own state in a save (<see cref="CombatSnapshot.Phantasie3"/>; <see cref="P3Fight.Capture"/> /
/// <see cref="P3Fight.Restore"/>): DOS keeps all of it in the fight's far data until the fight ends, so a fight loaded
/// mid-way goes on as it was. Monsters are in the save's monster order (<see cref="CombatSnapshot.Monsters"/>), members
/// in party order (the summoned elemental last).
/// </summary>
public sealed class P3FightSnapshot
{
    /// <summary>Each monster's six body areas (<see cref="P3AreaState"/> numbers, area order).</summary>
    public List<int[]> MonsterBodies { get; set; } = new();

    /// <summary>
    /// The body that still decides a moved monster's status (<see cref="P3Fight.KeepBodiesBySlot"/>), per monster; null
    /// where none is kept.
    /// </summary>
    public List<int[]?>? MonsterStatusBodies { get; set; }

    /// <summary>Each member's Arrow Flame in effect (<see cref="P3BowSpell"/>; None or an Arrow Flame).</summary>
    public List<P3BowSpell> BowSpells { get; set; } = new();

    /// <summary>Members whose bow % is Accuracy's 250.</summary>
    public List<int> Accuracy { get; set; } = new();

    /// <summary>Each member's attack % this fight (<c>FAR:0xfd10 + 2i</c>); null = from the record.</summary>
    public List<int?> Attack { get; set; } = new();

    /// <summary>Each member's armour points this fight (<c>FAR:0xb59a + 2i</c>); null = from the worn armour.</summary>
    public List<int?> Armour { get; set; } = new();

    /// <summary>Members who parried this round, and those whose one blocked hit is still unused.</summary>
    public List<int> Parried { get; set; } = new();
    public List<int> ParryBlock { get; set; } = new();

    public int WakeChance { get; set; } = P3Fight.WakeChanceStart;
    public P3RoundMode NextRound { get; set; }
    public bool FreeFlee { get; set; }
    public bool AwakenAtRoundEnd { get; set; }
    public int StartStrength { get; set; }

    /// <summary>The fight came from an event program's op 5: a win turns the square into 0x8a.</summary>
    public bool FromEvent { get; set; }
}
