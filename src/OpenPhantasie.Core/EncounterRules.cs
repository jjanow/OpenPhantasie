namespace OpenPhantasie.Core;

/// <summary>Which sprite dimension the encounter builder's group-fit test sums (<see cref="EncounterRules.GroupFit"/>).</summary>
public enum EncounterGroupFit
{
    /// <summary>DOS I: the sprite table's word 0 (<see cref="Monster.SpriteWidth"/>), <c>combat-resolution.md</c>.</summary>
    SpriteWidth,

    /// <summary>
    /// Phantasie II (ST) and Amiga I: the size table's word 1, the sprite <b>height</b> (<see cref="Monster.SpriteHeight"/>);
    /// groups are stacked vertically on the ST combat screen (<c>encounters.md</c> §3).
    /// </summary>
    SpriteHeight,
}

/// <summary>
/// The per-game rules of the monster-group builder and the encounter rolls (<c>encounters</c> in
/// <c>GameData/&lt;game&gt;/rules.json</c>; null in a profile = Phantasie I's DOS rules, <see cref="PhantasieI"/>).
/// Phantasie II (Atari ST, <c>reverse-engineering/atari-st/phantasie2/logic-specs/encounters.md</c> §1) differs from DOS I
/// in: the roll range (<c>rand / 327</c>, 0..100), L and the reaction sum averaged over the groups, the group fit on the
/// sprite height, the dungeon wandering monster <c>12·T</c>, no Black Knights in the day encounter and no DNG6 TROLKIN
/// override. Everything else (counts, HP, companions, next-group roll, surprise) is I's rule and stays in code.
/// </summary>
public sealed record EncounterRules(
    TerrainRoll Roll,
    bool AverageLevelAndReaction,
    EncounterGroupFit GroupFit,
    int DungeonTierFactor,
    bool DayBlackKnights,
    int? WanderingOverrideLevel,
    int WanderingOverrideMonster,
    bool ClampFirstMonster)
{
    /// <summary>
    /// DOS Phantasie I (OV2 <c>0x34aa</c>, <c>0x70ed</c>, <c>0x2249</c>): 0..99 rolls, Σ L and reaction, width fit, wandering
    /// <c>r/33 + b + T − 8</c>, Black Knights on <c>r &gt; 91</c>, DNG6 TROLKIN (0-based 33) on a second roll &gt; 50.
    /// </summary>
    public static EncounterRules PhantasieI { get; } = new(
        TerrainRoll.Percent,
        AverageLevelAndReaction: false,
        EncounterGroupFit.SpriteWidth,
        DungeonTierFactor: 1,
        DayBlackKnights: true,
        WanderingOverrideLevel: 6,
        WanderingOverrideMonster: 0x22 - 1,
        ClampFirstMonster: false);

    /// <summary>One roll in this game's range: <c>Next(0, 100)</c>, or II's <c>rand() / 327</c> (0..100, 100 at weight 68/32768).</summary>
    public int Roll100(IRandomSource rng) => Roll.Draw(rng);

    /// <summary>Throws <see cref="ArgumentException"/> for an out-of-range value.</summary>
    public EncounterRules Validate()
    {
        if (DungeonTierFactor < 1)
            throw new ArgumentException($"{nameof(DungeonTierFactor)} must be at least 1.");
        if (WanderingOverrideLevel is < 1 || WanderingOverrideMonster < 0)
            throw new ArgumentException("encounters: bad wandering override.");
        return this;
    }
}
