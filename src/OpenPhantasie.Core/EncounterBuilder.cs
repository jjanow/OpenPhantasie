namespace OpenPhantasie.Core;

/// <summary>
/// OV2 encounter builder <c>0x34aa</c> (<c>combat-resolution.md</c> §Monster record & encounter build): up to three
/// groups, each a copy of one monster record, chained through the companion ids. RNG draws, in DOS order, per
/// group that fits on screen: count, one HP roll per monster, companion pick, next-group roll.
/// </summary>
public static class EncounterBuilder
{
    public const int MaxGroups = 3;

    /// <summary>Groups stop once <c>Σ(sprite width + 3) ≥ 135</c> (<c>0x399b</c>); the group that overflows is dropped.</summary>
    public const int ScreenWidthLimit = 135;

    /// <summary>Most monsters in one group (count roll clamped to 0..5, plus one).</summary>
    public const int MaxGroupSize = 6;

    public const int BlackKnightId = 76;

    /// <summary>The last monster record (80 per game, 0-based); II clamps a first id past it to 0.</summary>
    public const int LastMonsterId = 79;

    /// <param name="firstMonsterId">0-based id of group 0's monster (DOS <c>[0x64e6]</c> − 1).</param>
    /// <param name="record">Fresh record copy for a monster id, HP not rolled.</param>
    /// <param name="blackKnightSpot">
    /// The fight is inside DNG9: <c>[0x64e8] == 3</c> (dungeon) and <c>[0x64ec] == 9</c> (dungeon number, <c>0x1de1</c> →
    /// loader "DNG%d"). Every BLACK KNIGHT group then uses size factor 4 instead of 3 (<c>0x387e</c>).
    /// </param>
    /// <param name="surprise">
    /// The surprise flags the encounter source set (<see cref="Surprise"/>). Null means none are modelled: no opening
    /// effect, which bare-encounter tests use. Otherwise the encounter keeps them. A hold of 2 builds every monster
    /// asleep (<c>0x3a6d</c>), and the fight skips PARTY OPTIONS unless both flags are set (<c>0x33ab</c>).
    /// </param>
    public static CombatEncounter Build(
        Party party,
        int firstMonsterId,
        Func<int, Monster> record,
        IRandomSource rng,
        bool blackKnightSpot = false,
        SurpriseFlags? surprise = null,
        EncounterRules? rules = null)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(rng);
        rules ??= EncounterRules.PhantasieI;
        int Roll() => rules.Roll100(rng);

        // II 0xa742..0xa756: a first id outside the 80 records becomes 0 (SKELETON); DOS has no such check.
        if (rules.ClampFirstMonster && firstMonsterId is < 0 or > LastMonsterId)
            firstMonsterId = 0;

        // L, gold and XP start at 0 (0x3334, 0x3542). XP then accrues per kill (CombatEncounter.RecordKill).
        var encounter = new CombatEncounter(party);
        var flags = surprise ?? SurpriseFlags.None;
        if (surprise is not null)
        {
            encounter.Surprise = flags;
            encounter.OpensWithoutOrders = !flags.OpensWithPartyOptions;
        }
        encounter.FirstMonsterId = firstMonsterId;
        var monsterId = firstMonsterId;
        var width = 0;

        for (var g = 0; g < MaxGroups; g++)
        {
            var template = record(monsterId);
            var factor = template.GroupSizeFactor;
            if (blackKnightSpot && monsterId == BlackKnightId)
                factor = 4;

            // Treasure ids come from group 0 only, read before the width check (0x394c).
            if (g == 0)
                encounter.TreasureItemIds = template.TreasureItems.ToArray();

            width += (rules.GroupFit == EncounterGroupFit.SpriteHeight ? template.SpriteHeight : template.SpriteWidth) + 3;
            if (width >= ScreenWidthLimit)
                break; // no room: group dropped, group count stays g (0x3b64)

            var extra = Math.Clamp(Roll() * factor / 102, 0, MaxGroupSize - 1);
            var members = new Monster[extra + 1];
            for (var i = 0; i < members.Length; i++)
            {
                var mon = i == 0 ? template : record(monsterId);
                mon.HitPoints = CombatMath.MonsterHitPoints(Roll(), mon.HpBase);
                // State := 3 − [0x64a0] (0x3a6d). Hold 1 gives state 2, which the build compaction resets to 3 (ready).
                // Hold 2 gives state 1 = asleep (15 % wake per pass). No RNG.
                if (flags.MonstersStartAsleep)
                    mon.StatusFlags |= MonsterStatusFlags.Asleep;
                encounter.RewardGold += mon.Gold;
                members[i] = mon;
            }

            encounter.AddRank(members);
            encounter.MonsterLevel += template.Level;          // 0x3ac4
            encounter.ReactionSum += template.ReactionModifier; // 0x3acc

            // rand < 50 → +0x28, else +0x27 (0x3adc).
            monsterId = Roll() < 50 ? template.Companions[1] : template.Companions[0];

            // Drawn even for the last group; another follows only if rand < +0x2b (0x3b22).
            if (Roll() >= template.NextGroupChance)
                break;
        }

        // II 0xad68..0xad8a (and Amiga I): L and the reaction sum become the mean over the built groups (signed, truncated).
        if (rules.AverageLevelAndReaction && encounter.Ranks.Count > 0)
        {
            encounter.MonsterLevel /= encounter.Ranks.Count;
            encounter.ReactionSum /= encounter.Ranks.Count;
        }

        return encounter;
    }
}
