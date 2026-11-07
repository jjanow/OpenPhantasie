using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Core.Tests;

/// <summary>
/// Phantasie IV's 7 × 7 battle: which spell casts turn the caster and the creatures they reach (<c>combat.md</c> §9.4).
/// Every turn is an <c>_f2f(a, b, kind)</c> call inside <c>_pmagic1</c> / <c>_pmagic2</c> / <c>_mmagic</c>; <c>_chrexe</c>'s
/// cast branch has none of its own, so Healing, Slow and the other spells without a call turn nobody.
/// Members start facing Back (up the field), monsters Front.
/// </summary>
public class Phantasie4BattleCastFacingTests
{
    private static readonly GameOptions Manual = GameOptions.ForPreset(GameId.P4);
    private static readonly GameOptions Code = GameOptions.ForPreset(GameId.P4, OptionPreset.Dos);
    private static readonly P4Tables T = GameTables.Load(GameId.P4).Phantasie4!.Tables;
    private static readonly P4BattleData B = Phantasie4World.LoadBattleData();

    /// <summary>Every draw returns the lowest value (rand(n) = 1: every roll succeeds, sleepers wake and do nothing).</summary>
    private sealed class Low : IRandomSource
    {
        public int Next(int minInclusive, int maxExclusive) => minInclusive;
    }

    /// <summary>Every draw returns the highest value (rand(n) = n: every resistance roll fails, sleepers stay asleep).</summary>
    private sealed class High : IRandomSource
    {
        public int Next(int minInclusive, int maxExclusive) => maxExclusive - 1;
    }

    private const int Front = P4BattleFacing.Front, Back = P4BattleFacing.Back,
        Left = P4BattleFacing.Left, Right = P4BattleFacing.Right;

    /// <summary>
    /// A dungeon fight against one Skeleton (asleep, so it does nothing this round) on <paramref name="monsterSquare"/>, the
    /// caster (member 0, knowing <paramref name="spell"/>) on 45 and member 1 on <paramref name="otherSquare"/>.
    /// </summary>
    private static P4Battle Fight(int spell, int monsterSquare, int otherSquare, GameOptions o, IRandomSource rng,
        out P4BattleMonster mon, P4BattleData? data = null, bool asleep = true)
    {
        var p = Phantasie4BattleLeftoversTests.Party(members: 2);
        if (spell > 0)
            p.Members[0].Spells[spell - 1] = 1;
        var b = P4Battle.Dungeon(o, T, data ?? B, p, rng, 0);
        foreach (var x in b.Monsters.Skip(1))
            x.Gone = true;
        mon = b.Monsters[0];
        mon.Square = monsterSquare;
        if (asleep)
            mon.State = P4BattleMonster.Asleep;
        b.Members[0].Square = 45;
        b.Members[1].Square = otherSquare;
        b.ChooseOption(P4EncounterOption.Fight);
        return b;
    }

    [OriginalDataFact]
    public void FireFlash_TurnsTheCasterAndTheMonsterTowardEachOther()
    {
        // _f2f(caster, target, 0) and (…, 304) at 0x2BFDE / 0x2BFF6: 45 → 42 is −1 (Back → Left); 42 → 45 is +1 (Front → Right).
        var b = Fight(5, 42, 31, Manual, new Low(), out var mon);
        Assert.True(b.Order(new P4Order(P4ActionKind.Cast, 42, 5)));
        b.Order(new P4Order(P4ActionKind.Parry));
        Assert.Equal(Left, b.Members[0].Facing);
        Assert.Equal(Right, mon.Facing);
    }

    [OriginalDataFact]
    public void FireFlash_OnAnEmptySquare_StillTurnsTheCaster()
    {
        var b = Fight(5, 3, 31, Manual, new Low(), out var mon);
        Assert.True(b.Order(new P4Order(P4ActionKind.Cast, 42, 5)));
        b.Order(new P4Order(P4ActionKind.Parry));
        Assert.Equal(Left, b.Members[0].Facing);
        Assert.Equal(Front, mon.Facing);
    }

    [OriginalDataFact]
    public void Healing_TurnsNobody()
    {
        // Healing has no _f2f call: the caster keeps facing up the field though the patient stands to its left.
        var b = Fight(2, 3, 44, Manual, new Low(), out _);
        Assert.True(b.Order(new P4Order(P4ActionKind.Cast, 1, 2)));
        b.Order(new P4Order(P4ActionKind.Parry));
        Assert.Equal(Back, b.Members[0].Facing);
        Assert.Equal(Back, b.Members[1].Facing);
    }

    [OriginalDataFact]
    public void Slow_TurnsNobody()
    {
        var b = Fight(11, 42, 31, Manual, new Low(), out var mon);
        Assert.True(b.Order(new P4Order(P4ActionKind.Cast, Spell: 11)));
        b.Order(new P4Order(P4ActionKind.Parry));
        Assert.Equal(Back, b.Members[0].Facing);
        Assert.Equal(Front, mon.Facing);
        Assert.Equal(-1, mon.SpellMove); // it did work
    }

    [OriginalDataTheory]
    [InlineData(true, Left, Right)]  // the monster passes R: _f2f 0x2C786
    [InlineData(false, Back, Front)] // it resists: no call
    public void Confusion_TurnsOnlyTowardTheMonstersItReaches(bool works, int caster, int monster)
    {
        var b = Fight(20, 42, 31, Manual, works ? new Low() : new High(), out var mon);
        Assert.True(b.Order(new P4Order(P4ActionKind.Cast, Spell: 20)));
        b.Order(new P4Order(P4ActionKind.Parry));
        Assert.Equal(caster, b.Members[0].Facing);
        Assert.Equal(monster, mon.Facing);
    }

    [OriginalDataFact]
    public void ShockWave_AMemberOnThePathTurnsTowardTheCaster()
    {
        // _f2f(caster, square, 302…304) 0x2B2FC before each hit: member 1 on 38, just above the caster, turns round (+7: Front).
        var b = Fight(51, 3, 38, Manual, new Low(), out _);
        Assert.True(b.Order(new P4Order(P4ActionKind.Cast, 3, 51)));
        b.Order(new P4Order(P4ActionKind.Parry));
        Assert.Equal(Front, b.Members[1].Facing);
        Assert.Equal(Back, b.Members[0].Facing);
    }

    [OriginalDataFact]
    public void Flameball_Code_ThePatternFacesTheCentre_NotTheCaster()
    {
        // Code option (D4-047): the + pattern around square 17; the Skeleton on 16 turns toward 17 (+1: Right), not toward
        // the caster on 45 (+8 from Front: no turn). _f2f(centre, square, 2 / 306) at 0x2B48C / 0x2B4A4.
        var b = Fight(55, 16, 31, Code, new Low(), out var mon);
        Assert.True(b.Order(new P4Order(P4ActionKind.Cast, 17, 55)));
        b.Order(new P4Order(P4ActionKind.Parry));
        Assert.Equal(Right, mon.Facing);
        Assert.Equal(Back, b.Members[0].Facing); // 45 → 17 is straight up (−7): already facing it
    }

    [OriginalDataTheory]
    [InlineData(5)]  // Fire Flash 1: _f2f 0x29B6C / 0x29B84
    [InlineData(66)] // breath: _f2f 0x2A512 / 0x2A52A per hit
    public void MonsterCast_TurnsTheMonsterAndTheMemberItHits(int id)
    {
        // A Skeleton that always casts id; member 1 stands to its left (2), the caster far away. 3 → 2 is −1 (Front →
        // Left); 2 → 3 is +1 (Back → Right).
        var caster = B.Monsters[0] with { SpellAbility = 100, SpellIds = [id, id, id] };
        var data = B with { Monsters = [caster, .. B.Monsters.Skip(1)] };
        var b = Fight(0, 3, 2, Manual, new Low(), out var mon, data, asleep: false);
        b.Order(new P4Order(P4ActionKind.Parry));
        b.Order(new P4Order(P4ActionKind.Parry));
        Assert.True(b.Members[1].Member.Hp < 60); // it was hit
        Assert.Equal(Left, mon.Facing);
        Assert.Equal(Right, b.Members[1].Facing);
    }

    [OriginalDataTheory]
    [InlineData(9)]  // Speed 1: no _f2f in _mmagic
    [InlineData(38)] // Sleep
    public void MonsterCast_EveryCastFacesTheAim_EvenWithoutItsOwnCall(int id)
    {
        // _monexe 0x25578: _f2f(monster, aim, 200) right after _obj_select(0), before the id is picked.
        var caster = B.Monsters[0] with { SpellAbility = 100, SpellIds = [id, id, id] };
        var data = B with { Monsters = [caster, .. B.Monsters.Skip(1)] };
        var b = Fight(0, 3, 2, Manual, new Low(), out var mon, data, asleep: false);
        b.Order(new P4Order(P4ActionKind.Parry));
        b.Order(new P4Order(P4ActionKind.Parry));
        Assert.Equal(60, b.Members[1].Member.Hp); // a spell, not a blow
        Assert.Equal(Left, mon.Facing);
        Assert.Equal(Right, b.Members[1].Facing);
    }
}
