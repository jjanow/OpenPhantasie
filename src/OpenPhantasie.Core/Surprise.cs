namespace OpenPhantasie.Core;

/// <summary>
/// The two OV2 surprise globals, set before an encounter is built (<c>combat-resolution.md</c> §Surprise):
/// <see cref="FirstRound"/> = <c>[0x649e]</c>, <see cref="MonsterHold"/> = <c>[0x64a0]</c> (0, 1 or 2).
/// </summary>
/// <remarks>
/// What they do: PARTY OPTIONS open the fight only when both are set (<c>0x33ab</c>). Otherwise round 1 runs at once
/// with no orders. A hold of 2 builds every monster asleep (<c>0x3a6d</c>). While the hold is set, ready monsters
/// don't act (<c>0xce7</c>). The scheduler clears it after pass 1 of a first round, and at round end it goes 2 → 1 → 0.
/// </remarks>
public readonly record struct SurpriseFlags(bool FirstRound, int MonsterHold)
{
    /// <summary>Every random encounter (1, 1): options first; in round 1 the monsters sit out pass 1.</summary>
    public static SurpriseFlags Normal => new(true, 1);

    /// <summary>(1, 0): no options or orders; in round 1 only the monsters act ("MONSTERS APPEAR", "MONSTERS SURPRISE YOU !").</summary>
    public static SurpriseFlags PartySurprised => new(true, 0);

    /// <summary>(0, 2): monsters built asleep; round 1 has no orders, and the monsters are held in rounds 1 and 2.</summary>
    public static SurpriseFlags MonstersSurprised => new(false, 2);

    /// <summary>(0, 1): an empty first round, no orders and monsters held (Black Knights → FIGHT).</summary>
    public static SurpriseFlags Stalled => new(false, 1);

    /// <summary>No surprise state at all (0, 0). Same opening as <see cref="PartySurprised"/>; the default for a bare encounter.</summary>
    public static SurpriseFlags None => default;

    /// <summary>OV2 <c>0x33ab</c>: the fight opens with PARTY OPTIONS (state 4) only when both flags are non-zero.</summary>
    public bool OpensWithPartyOptions => FirstRound && MonsterHold != 0;

    /// <summary>Builder state <c>3 − [0x64a0]</c> (<c>0x3a6d</c>): only a hold of 2 gives state 1 (asleep).</summary>
    public bool MonstersStartAsleep => MonsterHold == 2;
}

/// <summary>The surprise rolls of each encounter source, as decoded (<c>combat-resolution.md</c> §Surprise, writers table).</summary>
public static class Surprise
{
    /// <summary>DNG script op 5 (<c>0x26f4</c>): <c>rand0_99 &gt; 90</c> → party surprised, else normal.</summary>
    public static SurpriseFlags ScriptFight(IRandomSource rng) =>
        Roll(rng) > 90 ? SurpriseFlags.PartySurprised : SurpriseFlags.Normal;

    /// <summary>DNG script op 17, "MONSTERS APPEAR" (<c>0x283f</c>): always party surprised.</summary>
    public static SurpriseFlags MonstersAppear => SurpriseFlags.PartySurprised;

    /// <summary>
    /// Dungeon lock ambush (<c>0x2f28..0x2f56</c>), after the party chose to stay: hold := <c>rand0_99 ≤ 50</c>,
    /// then first-round := 0 only if <c>rand0_99 &gt; 50</c> and a member heard the monsters. Both rolls are always drawn.
    /// II (ST <c>0x11354…0x113b2</c> [C] s92) draws them on <c>rand / 327</c> (<paramref name="roll"/>).
    /// </summary>
    public static SurpriseFlags LockAmbush(IRandomSource rng, bool someoneHeard, TerrainRoll roll = TerrainRoll.Percent)
    {
        var hold = roll.Draw(rng) <= 50 ? 1 : 0;
        var firstRound = !(roll.Draw(rng) > 50 && someoneHeard);
        return new SurpriseFlags(firstRound, hold);
    }

    /// <summary>Overworld "%s HEARS MONSTERS" → FIGHT (<c>0x746b</c>): <c>rand0_99 &lt; 30</c> → normal, else "YOU SURPRISE THEM !".</summary>
    public static SurpriseFlags HeardThenFight(IRandomSource rng) =>
        Roll(rng) < 30 ? SurpriseFlags.Normal : SurpriseFlags.MonstersSurprised;

    /// <summary>Overworld "%s HEARS MONSTERS" → RUN (<c>0x74a1</c>): <c>rand0_99 &lt; 80</c> → away (null), else a normal fight.</summary>
    public static SurpriseFlags? HeardThenRun(IRandomSource rng) =>
        Roll(rng) < 80 ? null : SurpriseFlags.Normal;

    /// <summary>Black Knights menu → RUN (<c>0x728e</c>): <c>rand0_99 &lt; 44</c> → away (null), else a normal fight.</summary>
    public static SurpriseFlags? BlackKnightsRun(IRandomSource rng) =>
        Roll(rng) < 44 ? null : SurpriseFlags.Normal;

    /// <summary>Black Knights menu → FIGHT (<c>0x72a1</c>): a stalled opening.</summary>
    public static SurpriseFlags BlackKnightsFight => SurpriseFlags.Stalled;

    /// <summary>"YOU SPOT SLEEPING MONSTERS!" → ATTACK (<c>0x7376</c>): monsters surprised.</summary>
    public static SurpriseFlags SleepingMonstersAttacked => SurpriseFlags.MonstersSurprised;

    private static int Roll(IRandomSource rng)
    {
        ArgumentNullException.ThrowIfNull(rng);
        return rng.Next(0, 100);
    }
}
