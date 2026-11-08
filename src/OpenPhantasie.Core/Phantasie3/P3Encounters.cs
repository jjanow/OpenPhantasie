namespace OpenPhantasie.Core;

/// <summary>One monster group (rank) as the encounter options read it: its level and the members' HP words.</summary>
public sealed record P3GroupStrength(int Level, IReadOnlyList<int> MemberHp);

/// <summary>The party's side of a Flee attempt.</summary>
/// <param name="RankOneExists">A rank-1 monster group is present.</param>
/// <param name="RankOneAllDisabled">No rank-1 member is Okay (all asleep / down / out).</param>
/// <param name="AnyMemberAble">A party member (the elemental excluded) is Okay or Down.</param>
/// <param name="FreeFleeFlag"><c>FAR:0xb5c2</c>: the party surprised the monsters (s83, root <c>0xde54</c>: set when it
/// attacks sleeping monsters or `core.p3.you_surprise_the_monsters`; it also makes the fight's mode 2). Flee succeeds outright
/// when set. The III campaign passes its fight's opening (a door's "Continue?" Yes too, [~]: mode 2 there, the flag write
/// not traced).</param>
public sealed record P3FleeContext(bool RankOneExists, bool RankOneAllDisabled, bool AnyMemberAble, bool FreeFleeFlag = false);

/// <summary>
/// Phantasie III's encounter options (<c>encounters.md</c>): monster status letters, monster strength, Greet, Accept
/// Surrender, Beg for Mercy, the party's Flee and monsters fleeing at the end of a round. <c>id</c> is always the rank-1
/// monster id and <c>reaction</c> its reaction byte (26; the group copy, which Charm raises, for Mercy and Flee). All
/// rolls are <c>rnd(100)</c>, 1…100.
/// </summary>
public static class P3Encounters
{
    /// <summary>DOS (<c>p3.encounter-block</c> = <c>dos</c>): rank-1 ids from here on can't be greeted, begged, made to
    /// surrender, flee, or be fled (D3-024).</summary>
    public const int FirstBlockedMonsterId = 44;

    /// <summary>The final bosses 77–79 (Nikademus ×2, Lord Wood) block the party's Flee unless <c>p3.boss-flee</c> = allowed.</summary>
    public const int FirstFinalBossId = 77;

    /// <summary>
    /// The combat list's letter for a monster member (root <c>0x11297</c>): Asleep S, Down D, Unconscious (<see cref="P3Status.Out"/>,
    /// head or torso Broken) U (manual, D3-021) or O (<c>p3.status-letter</c> = <c>dos</c>), anything else K.
    /// </summary>
    public static char StatusLetter(P3Status status, GameOptions rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        return status switch
        {
            P3Status.Asleep => 'S',
            P3Status.Down => 'D',
            P3Status.Out => rules.P3DosStatusLetter() ? 'O' : 'U',
            _ => 'K',
        };
    }

    /// <summary>
    /// Monster party strength (root <c>0xf460</c>): Σ level over the member slots (at most 5 per group) whose HP word has
    /// bit 0 set. That is "odd HP", not "alive" (<c>[~]</c> probably a bug for HP &gt; 0); kept as DOS has it, the manual
    /// is silent (not a conflict).
    /// </summary>
    public static int Strength(IEnumerable<P3GroupStrength> groups)
    {
        ArgumentNullException.ThrowIfNull(groups);
        return groups.Sum(g => g.MemberHp.Take(5).Count(hp => (hp & 1) != 0) * g.Level);
    }

    private static bool Blocked(int id, GameOptions rules) =>
        rules.P3DosEncounterBlock() && id >= FirstBlockedMonsterId;

    /// <summary>
    /// Greet (root <c>0x112e9</c>): <c>rnd(100) &lt; reaction + 40</c>; DOS also needs id &lt; 44. <paramref name="greetBlockedFlag"/>
    /// is <c>FAR:0xb618</c> (s83): set by <c>damage_monster</c> (root <c>0xb191</c>) once the party has hurt a monster, cleared
    /// at every fight start (root <c>0x1055c</c>, ovl4 <c>0x1d82</c>) and by Charm (root <c>0xc758</c>); a set flag refuses.
    /// The III campaign passes <see cref="CombatEncounter.MonsterDamaged"/>. Success ends the fight with no gold and no
    /// treasure.
    /// </summary>
    public static bool Greet(int id, int reaction, IRandomSource rng, GameOptions rules, bool greetBlockedFlag = false)
    {
        ArgumentNullException.ThrowIfNull(rules);
        if (Blocked(id, rules) || greetBlockedFlag) return false;
        return P3Dice.Rnd(rng, 100) < reaction + 40;
    }

    /// <summary>
    /// The surrender test, used by Accept Surrender (root <c>0xd171</c>) and by monsters fleeing at the end of a round:
    /// <c>strength &lt; start strength / 2</c>, then <c>rnd(100) ≤ 50</c>; DOS also needs id &lt; 44. Success ends the
    /// fight as a win (gold, XP, treasure).
    /// </summary>
    public static bool Surrenders(int strength, int startStrength, int id, IRandomSource rng, GameOptions rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        if (strength >= startStrength / 2) return false;
        if (P3Dice.Rnd(rng, 100) > 50) return false;
        return !Blocked(id, rules);
    }

    /// <summary>
    /// Beg for Mercy (root <c>0x10e14</c>): <c>rnd(100) &lt; reaction + 60</c>; DOS also needs id &lt; 44. On success the
    /// monsters take all the party's gold and the fight ends with no gold or treasure (<see cref="MercyTakeItems"/>).
    /// </summary>
    public static bool BegForMercy(int id, int reaction, IRandomSource rng, GameOptions rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        if (P3Dice.Rnd(rng, 100) >= reaction + 60) return false;
        return !Blocked(id, rules);
    }

    /// <summary>Each new item is taken with this chance in % (D3-024, user s89).</summary>
    public const int MercyItemChance = 50;

    /// <summary>
    /// The manual's "all of the party's gold, some of its new items" (D3-024, user s89; a remaster rule, no version takes
    /// items): each item found since the party last left town (<see cref="LootSack.IsNewSinceTownExit"/>; items kept in
    /// the found-item list from an earlier trip are safe) is taken with a <see cref="MercyItemChance"/> % roll of its own,
    /// in slot order. A taken item leaves a hole, as I's Mercy (<see cref="LootSack.ForfeitToBeggars"/>). Nothing under
    /// <c>p3.mercy</c> = <c>dos</c> (DOS / Amiga / Apple II). Returns how many were taken.
    /// </summary>
    public static int MercyTakeItems(LootSack found, IRandomSource rng, GameOptions rules)
    {
        ArgumentNullException.ThrowIfNull(found);
        ArgumentNullException.ThrowIfNull(rules);
        if (rules.P3MercyGoldOnly()) return 0;
        var taken = 0;
        for (var i = 0; i < found.ItemIds.Count; i++)
        {
            if (!found.IsNewSinceTownExit(i) || P3Dice.Rnd(rng, 100) > MercyItemChance)
                continue;
            found.ItemIds[i] = 0;
            taken++;
        }
        while (found.ItemIds.Count > 0 && found.ItemIds[^1] == 0)
            found.ItemIds.RemoveAt(found.ItemIds.Count - 1);
        return taken;
    }

    /// <summary>
    /// The party's Flee (root <c>0x10f30</c>), in DOS's order: rank 1 present and all disabled → away; the free-flee flag
    /// → away; nobody able → caught; DOS only: id ≥ 44 → caught; else <c>rnd(100) &lt; 70 + reaction − strength</c>. Last, a
    /// final boss (id ≥ 77) catches the party whatever came before, unless <c>p3.boss-flee</c> = <c>allowed</c> under the
    /// manual rule (DOS's 77+ block stays under <c>p3.encounter-block</c> = <c>dos</c>).
    /// </summary>
    public static bool Flee(P3FleeContext party, int id, int reaction, int strength, IRandomSource rng, GameOptions rules)
    {
        ArgumentNullException.ThrowIfNull(party);
        ArgumentNullException.ThrowIfNull(rules);
        bool ok;
        if (party.RankOneExists && party.RankOneAllDisabled) ok = true;
        else if (party.FreeFleeFlag) ok = true;
        else if (!party.AnyMemberAble) ok = false;
        else if (Blocked(id, rules)) ok = false;
        else ok = P3Dice.Rnd(rng, 100) < 70 + reaction - strength;
        if (id >= FirstFinalBossId && (rules.P3DosEncounterBlock() || !rules.P3BossesFleeable())) ok = false;
        return ok;
    }
}
