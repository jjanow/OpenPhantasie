using OpenPhantasie.Core;

namespace OpenPhantasie.Data;

/// <summary>
/// Static monster record loaded from GameData/phantasie1/monsters.json — the 80 × 0x39
/// table at DS:0x1a6c of the unpacked PHANTASI.COM (<c>extract_monsters.py</c>, s22).
/// Field roles are pinned by OV2 use-sites (<c>combat-resolution.md</c> §Monster record).
/// Sprite paths come from the Phase 4 asset-manifest; <c>Undead</c>/<c>NeverFlees</c> from monster-traits.json.
/// <see cref="SpriteHeight"/> (T3): Phantasie II's builder fits groups on the sprite height, not the width
/// (<c>reverse-engineering/atari-st/phantasie2/logic-specs/encounters.md</c> §3); I's DOS data carries it as [~] only.
/// </summary>
public record MonsterDefinition(
    int Id,
    string Name,
    int Armor,
    int Damage,
    int Level,
    long Experience,
    int SpecialChance,
    IReadOnlyList<int> Specials,
    int HpBase,
    int CombatRating,
    IReadOnlyList<int> Companions,
    int ReactionModifier,
    int NextGroupChance,
    int Defense,
    long Gold,
    int GroupSizeFactor,
    IReadOnlyList<int> TreasureItems,
    int AttackPower,
    int SpriteWidth = 0,
    bool Undead = false,
    bool NeverFlees = false,
    string? SpriteIdlePath = null,
    string? SpriteAttackingPath = null,
    int SpriteHeight = 0)
{
    /// <summary>Game-scoped id (<c>p1:</c><see cref="Id"/>), T1.2.</summary>
    public ContentId ContentId { get; init; }

    /// <summary>Phantasie III: byte 14, the group count is <c>rnd(GroupMax)</c> (<c>encounters.md</c> §1); null in I / II.</summary>
    public int? GroupMax { get; init; }

    /// <summary>Phantasie III: byte 27 raw (s8 &gt; 41: the group fights alone, at most 2 monsters) [C]; null in I / II.</summary>
    public int? Byte27 { get; init; }

    /// <summary>Phantasie III: byte 33 raw (1 on undead / demons and Constrictor; purpose [?]); null in I / II.</summary>
    public int? Byte33 { get; init; }
}

public static class MonsterDefinitionExtensions
{
    /// <summary>
    /// The record copy the OV2 encounter builder (<c>0x34aa</c>) makes for a group, HP not yet rolled.
    /// <see cref="EncounterBuilder"/> rolls HP per monster.
    /// </summary>
    public static Monster ToRecord(this MonsterDefinition def) => new()
    {
        Id = def.Id,
        Name = def.Name,
        CombatRating = def.CombatRating,
        Level = def.Level,
        ExperienceValue = def.Experience,
        Gold = def.Gold,
        SpecialChance = def.SpecialChance,
        Specials = def.Specials,
        DamageField = def.Damage,
        Defense = def.Defense,
        AttackPower = def.AttackPower,
        Armor = def.Armor,
        HpBase = def.HpBase,
        GroupSizeFactor = def.GroupSizeFactor,
        NextGroupChance = def.NextGroupChance,
        Companions = def.Companions,
        TreasureItems = def.TreasureItems,
        ReactionModifier = def.ReactionModifier,
        SpriteWidth = def.SpriteWidth,
        SpriteHeight = def.SpriteHeight,
        Undead = def.Undead,
        NeverFlees = def.NeverFlees,
        GroupMax = def.GroupMax ?? 0,
        GroupAloneByte = def.Byte27 ?? 0,
    };

    /// <summary>One runtime combatant: <see cref="ToRecord"/> plus the build's HP roll (<c>0x3a27</c>).</summary>
    public static Monster ToCombatant(this MonsterDefinition def, IRandomSource rng)
    {
        ArgumentNullException.ThrowIfNull(rng);
        var mon = def.ToRecord();
        mon.HitPoints = CombatMath.MonsterHitPoints(rng.Next(0, 100), def.HpBase);
        return mon;
    }
}
