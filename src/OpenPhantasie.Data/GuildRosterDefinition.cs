namespace OpenPhantasie.Data;

/// <summary>
/// Decoded GUILD.DAT roster snapshot (Phase 3 fixture). Used as a seed /
/// importer reference for M2 character save work — not the live save format
/// writer. Open fields (@28 dual-use, spells_known packing, XP width) are
/// noted in <see cref="CharacterSchemaNotes"/>.
/// </summary>
public sealed class GuildRosterDefinition
{
    public int RecordSize { get; init; }
    public int SlotCount { get; init; }
    public IReadOnlyList<GuildCharacterSnapshot> Characters { get; init; } =
        Array.Empty<GuildCharacterSnapshot>();
}

public sealed class GuildCharacterSnapshot
{
    public required string Name { get; init; }
    /// <summary>Displayed race (GUILD @256); falls back to <see cref="BirthRace"/>.</summary>
    public string? Race { get; init; }
    /// <summary>Birth race (GUILD @216).</summary>
    public string? BirthRace { get; init; }
    public string? Class { get; init; }
    public string? Status { get; init; }
    public int Level { get; init; }
    public int Strength { get; init; }
    public int Intelligence { get; init; }
    public int Dexterity { get; init; }
    public int Constitution { get; init; }
    public int Charisma { get; init; }
    public int Luck { get; init; }
    public int MagicCur { get; init; }
    public int MagicMax { get; init; }
    /// <summary>GUILD @14 MP ceiling.</summary>
    public int MpCeiling { get; init; }
    /// <summary>GUILD @12 defense rating [C]: written by combat prep (OV2 0x32f7), read by monster to-hit (OV2 0xe68).</summary>
    public int Word12 { get; init; }
    /// <summary>GUILD @50..@68 skill bonus words (record order); null = derive from stats.</summary>
    public int[]? SkillBonuses { get; init; }
    public bool CastSinceRest { get; init; }
    /// <summary>GUILD @252 in DOS steps — informational; Core replaces it with the D-003 band (user s17).</summary>
    public int LifespanDos { get; init; }
    public int CurrentTown { get; init; }
    public bool HasAged { get; init; }
    /// <summary>GUILD @264 age clock (2-week units).</summary>
    public int AgeClock { get; init; }
    public int HpMax { get; init; }
    public int HpCur { get; init; }
    public int GoldInBank { get; init; }
    public long Experience { get; init; }

    /// <summary>GUILD @28 — weapon damage A (s19; not a scroll count).</summary>
    public int? WeaponDamageA { get; init; }
    /// <summary>Pre-s19 JSON name of @28 — read only.</summary>
    public int? ScrollOrWeaponHit { get; init; }
    /// <summary>GUILD @222..@238 carried items (non-zero, worn included); null in pre-s19 extracts.</summary>
    public int[]? Inventory { get; init; }

    public int WornShield { get; init; }
    public int WornArmor { get; init; }
    public int WornWeapon { get; init; }
    public GuildSkillsSnapshot? Skills { get; init; }
    public int Score { get; init; }
    public int Slot { get; init; }
}

public sealed class GuildSkillsSnapshot
{
    public int Attack { get; init; }
    public int Parry { get; init; }
    public int FindItem { get; init; }
    public int SpotTrap { get; init; }
    public int DisarmTrap { get; init; }
    public int Listen { get; init; }
    public int PickLock { get; init; }
    public int Swim { get; init; }
}
