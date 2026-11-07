namespace OpenPhantasie.Core;

/// <summary>Runtime status bits applied by combat spells (M7).</summary>
[Flags]
public enum MonsterStatusFlags
{
    None = 0,
    Confused = 1,
    Weakened = 2,
    Bound = 4,
    Asleep = 16,
}

/// <summary>
/// Runtime combatant spawned from a <c>MonsterDefinition</c> — the OV2 combat copy
/// at <c>0x7522 + slot·0x39</c> of the 0x39-byte monster record (DS:0x1a6c, s22).
/// </summary>
public class Monster
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;

    /// <summary>Monster CV (<c>+0x7547</c>) — spell/breath to-hit.</summary>
    public int CombatRating { get; set; }

    public int? HitPoints { get; set; }

    /// <summary>Monster level (<c>+0x18</c>) — encounter level <c>L</c> sums it once per group.</summary>
    public int Level { get; init; }

    /// <summary>XP awarded when this monster dies (<c>+0x1a</c>, u32).</summary>
    public long ExperienceValue { get; init; }

    /// <summary>Gold this monster adds to the encounter purse at build (<c>+0x2f</c>, u32).</summary>
    public long Gold { get; init; }

    /// <summary>Special-ability gate (<c>+0x1e</c>); 0 = always melee.</summary>
    public int SpecialChance { get; set; }

    /// <summary>Three special-ability ids (<c>+0x20</c>); effects: <see cref="MonsterSpecials"/> (<c>0x16a9</c>).</summary>
    public IReadOnlyList<int> Specials { get; init; } = Array.Empty<int>();

    /// <summary>Monster armor (<c>+0x7536</c>) — party damage subtracts <c>*10</c>.</summary>
    public int Armor { get; set; }

    /// <summary>Damage field (<c>+0x7538</c>) — monster → party damage factor.</summary>
    public int DamageField { get; set; }

    /// <summary>Monster defense (<c>+0x754f</c>) — party melee to-hit subtracts this (often negative).</summary>
    public int Defense { get; set; }

    /// <summary>Monster attack power (<c>+0x7559</c>) — monster melee to-hit.</summary>
    public int AttackPower { get; set; }

    /// <summary>HP base (<c>+0x23</c>) — the build rolls <c>((rand + 50)·base + 50)/100</c> per monster.</summary>
    public int HpBase { get; init; }

    /// <summary>Group-size factor (<c>+0x33</c>) — the build's count roll.</summary>
    public int GroupSizeFactor { get; init; }

    /// <summary>Chance (0–100) that another group follows this one (<c>+0x2b</c>).</summary>
    public int NextGroupChance { get; init; }

    /// <summary>Companion ids (<c>+0x27</c>, <c>+0x28</c>, 0-based) — the next group's monster.</summary>
    public IReadOnlyList<int> Companions { get; init; } = Array.Empty<int>();

    /// <summary>Treasure item ids (<c>+0x35</c>, <c>+0x36</c>); the build keeps only group 0's.</summary>
    public IReadOnlyList<int> TreasureItems { get; init; } = Array.Empty<int>();

    /// <summary>Reaction modifier (<c>+0x29</c>) [~] — summed per group into <c>[0x750c]</c>.</summary>
    public int ReactionModifier { get; init; }

    /// <summary>Sprite width (DS:<c>0x2c3e</c>; the combat copy stores it at <c>+0x34</c>) — screen-width cap.</summary>
    public int SpriteWidth { get; init; }

    /// <summary>Sprite height (II: the size table's word 1); Phantasie II fits groups on it (<see cref="EncounterGroupFit"/>).</summary>
    public int SpriteHeight { get; init; }

    /// <summary>Phantasie III: monster byte 14, the most monsters in a group (count = <c>rnd(GroupMax)</c>); 0 in I / II.</summary>
    public int GroupMax { get; init; }

    /// <summary>
    /// Phantasie III: monster byte 27 (signed); above 41 the group fights alone with at most two monsters
    /// (<c>encounters.md</c> §1, root <c>0xefd8</c>); 0 in I / II.
    /// </summary>
    public int GroupAloneByte { get; init; }

    /// <summary>DISPELL UNDEAD target — from data, the manual's undead (D-024); DOS used XP % 7 == 0.</summary>
    public bool Undead { get; init; }

    /// <summary>A successful FEAR only lowers <c>L</c> and never makes this monster run (D-021, the Black Lord).</summary>
    public bool NeverFlees { get; init; }

    /// <summary>
    /// <see cref="MonsterStatusFlags.Asleep"/> = DOS monster state 1: skips its turns and wakes on <c>rand &lt; 15</c> per pass
    /// (<c>0x1081</c>). Set by the party's SLEEP (D-020); sleepers can still be hit and stay asleep.
    /// </summary>
    public MonsterStatusFlags StatusFlags { get; set; }

    /// <summary>
    /// DOS monster state 2 (<c>[0x64b0 + 16·group + 2·slot]</c>): acted, or lost its turn, this round. Cleared at the
    /// round-start compaction (<c>0x33c9</c> with <c>[0x74ae]</c> = 1); set by acting, waking, a failed monster run, and
    /// and waking from SLEEP.
    /// Round-scoped, not saved (rounds resolve whole).
    /// </summary>
    public bool DoneThisRound { get; set; }

    public bool IsAlive => (HitPoints ?? 0) > 0;
}
