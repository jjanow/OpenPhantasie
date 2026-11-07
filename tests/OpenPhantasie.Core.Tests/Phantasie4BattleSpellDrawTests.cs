using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Core.Tests;

/// <summary>
/// Phantasie IV spells whose draws or squares follow the code exactly (<c>combat.md</c> §9.1, §9.3): Mind Blast draws the
/// damage before the resistance roll (<c>0x2CFEC</c>, <c>0x2A20A</c>), breath 67 draws an unused rand(16) first
/// (<c>0x2A488</c>), and the code's Flameball keeps only squares in the <c>_fmark</c> box around the target, in
/// <c>_xflam2</c> order (<c>0x2B418</c>–<c>0x2B570</c>).
/// </summary>
public class Phantasie4BattleSpellDrawTests
{
    private static readonly GameOptions Manual = GameOptions.ForPreset(GameId.P4);
    private static readonly GameOptions Code = GameOptions.ForPreset(GameId.P4, OptionPreset.Dos);
    private static readonly P4Tables T = GameTables.Load(GameId.P4).Phantasie4!.Tables;
    private static readonly P4BattleData B = Phantasie4World.LoadBattleData();

    /// <summary>Returns the lowest or the highest value of every draw and logs each draw's n (rand(n)).</summary>
    private sealed class Log(bool high) : IRandomSource
    {
        public List<int> N { get; } = [];

        public int Next(int minInclusive, int maxExclusive)
        {
            N.Add(maxExclusive - 1);
            return high ? maxExclusive - 1 : minInclusive;
        }
    }

    /// <summary>The indices of the draws of rand(n).</summary>
    private static List<int> At(Log log, int n) =>
        log.N.Select((x, i) => (x, i)).Where(t => t.x == n).Select(t => t.i).ToList();

    /// <summary>
    /// A dungeon fight against one monster of type <paramref name="type"/> on <paramref name="monsterSquare"/>, the caster
    /// (member 0, knowing <paramref name="spell"/>) on <paramref name="casterSquare"/>, member 1 on
    /// <paramref name="otherSquare"/>.
    /// </summary>
    private static P4Battle Fight(int spell, int monsterSquare, int casterSquare, int otherSquare, GameOptions o,
        IRandomSource rng, P4MonsterType type, bool asleep, out P4BattleMonster mon)
    {
        var p = Phantasie4BattleLeftoversTests.Party(members: 2);
        foreach (var m in p.Members)
            m.Experience = 78; // not a multiple of 7: Mind Blast is not void on them
        if (spell > 0)
            p.Members[0].Spells[spell - 1] = 1;
        var data = B with { Monsters = [type, .. B.Monsters.Skip(1)] };
        var b = P4Battle.Dungeon(o, T, data, p, rng, 0);
        foreach (var x in b.Monsters.Skip(1))
            x.Gone = true;
        mon = b.Monsters[0];
        mon.Square = monsterSquare;
        if (asleep)
            mon.State = P4BattleMonster.Asleep;
        b.Members[0].Square = casterSquare;
        b.Members[1].Square = otherSquare;
        b.ChooseOption(P4EncounterOption.Fight);
        return b;
    }

    // --- Mind Blast: damage rand(N) before the resistance rand(100) -------------------------------------------------

    [OriginalDataTheory]
    [InlineData(true)]  // rand(100) = 100 fails: the monster resists, yet rand(8) was drawn first
    [InlineData(false)] // rand(100) = 1: it works
    public void PartyMindBlast_DrawsTheDamageBeforeTheResistanceRoll(bool resists)
    {
        var log = new Log(resists);
        var b = Fight(34, 42, 45, 31, Code, log, B.Monsters[0] with { Experience = 78 }, asleep: true, out var mon);
        var hp = mon.Hp;
        Assert.True(b.Order(new P4Order(P4ActionKind.Cast, Spell: 34)));
        b.Order(new P4Order(P4ActionKind.Parry));
        var eight = Assert.Single(At(log, 8));
        Assert.Equal(100, log.N[eight + 1]);
        if (resists)
            Assert.Equal(hp, mon.Hp);
        else
            Assert.True(mon.Hp < hp);
    }

    [OriginalDataFact]
    public void PartyMindBlast_ImmuneMonster_DrawsNothing()
    {
        // 0x2CFE8: experience 77 (7 × 11, below 2000) jumps past both draws.
        var log = new Log(false);
        var b = Fight(34, 42, 45, 31, Code, log, B.Monsters[0] with { Experience = 77 }, asleep: true, out var mon);
        var hp = mon.Hp;
        Assert.True(b.Order(new P4Order(P4ActionKind.Cast, Spell: 34)));
        b.Order(new P4Order(P4ActionKind.Parry));
        Assert.Empty(At(log, 8));
        Assert.Equal(hp, mon.Hp);
    }

    [OriginalDataFact]
    public void PartyMindBlast_Manual_SameDrawOrder()
    {
        var log = new Log(false);
        var b = Fight(34, 42, 45, 31, Manual, log, B.Monsters[0] with { Experience = 77 }, asleep: true, out var mon);
        var hp = mon.Hp;
        Assert.True(b.Order(new P4Order(P4ActionKind.Cast, Spell: 34)));
        b.Order(new P4Order(P4ActionKind.Parry));
        var eight = Assert.Single(At(log, 8)); // no immunity under the manual
        Assert.Equal(100, log.N[eight + 1]);
        Assert.True(mon.Hp < hp);
    }

    [OriginalDataTheory]
    [InlineData(78, true)]
    [InlineData(77, false)] // an immune member: no draw at all (0x2A206)
    public void MonsterMindBlast_DrawsTheDamageBeforeTheResistanceRoll(long memberExperience, bool drawn)
    {
        // A monster that always casts Mind Blast 1 (34); member 1 stands next to it, the caster far away.
        var log = new Log(false);
        var caster = B.Monsters[0] with { SpellAbility = 100, SpellIds = [34, 34, 34] };
        var b = Fight(0, 3, 45, 2, Code, log, caster, asleep: false, out _);
        b.Members[1].Member.Experience = memberExperience;
        b.Members[0].Member.Experience = 77; // the caster far away is immune: only member 1 can draw
        b.Order(new P4Order(P4ActionKind.Parry));
        b.Order(new P4Order(P4ActionKind.Parry));
        if (!drawn)
        {
            Assert.Empty(At(log, 8));
            Assert.Equal(60, b.Members[1].Member.Hp);
            return;
        }
        var eight = Assert.Single(At(log, 8));
        Assert.Equal(100, log.N[eight + 1]);
        Assert.True(b.Members[1].Member.Hp < 60);
    }

    // --- Breath 67: rand(16), then rand(31), then the damage -----------------------------------------------------

    [OriginalDataFact]
    public void Breath67_DrawsTheUnusedRand16First()
    {
        var log = new Log(false);
        var caster = B.Monsters[0] with { SpellAbility = 100, SpellIds = [67, 67, 67] };
        var b = Fight(0, 3, 45, 2, Code, log, caster, asleep: false, out _);
        b.Order(new P4Order(P4ActionKind.Parry));
        b.Order(new P4Order(P4ActionKind.Parry));
        var hits = At(log, 31);
        Assert.NotEmpty(hits);
        foreach (var i in hits)
        {
            Assert.Equal(16, log.N[i - 1]);
            Assert.Equal(15, log.N[i + 1]); // rand(rand(31) − 1 + 15) with rand(31) = 1
        }
    }

    // --- Flameball (code): the _fmark box and the _xflam2 order -------------------------------------------------

    [OriginalDataFact]
    public void Flameball_Code_DoesNotWrapToTheRowAbove()
    {
        // Target 14 (row 2, column 0), rank 1: offset −1 is square 13 (row 1, column 6), outside the radius-1 box
        // _b_chbtn(14, 1) marks, so the monster there is not burnt. Square 15 (+1) is.
        var b = Fight(55, 13, 45, 15, Code, new Log(false), B.Monsters[0], asleep: true, out var mon);
        var hp = mon.Hp;
        Assert.True(b.Order(new P4Order(P4ActionKind.Cast, 14, 55)));
        b.Order(new P4Order(P4ActionKind.Parry));
        Assert.Equal(hp, mon.Hp);
        Assert.True(b.Members[1].Member.Hp < 60);
    }

    [OriginalDataFact]
    public void Flameball_Code_BurnsInTableOrder()
    {
        // Rank 1 order 0, −7, −1, +7, +1: the monster on the centre (24) turns toward each creature hit (_f2f 0x2B48C),
        // the caster on 31 (+7) and then member 1 on 25 (+1), so it ends facing right. In square order it would end
        // facing the caster (+7: front).
        var b = Fight(55, 24, 31, 25, Code, new Log(false), B.Monsters[0], asleep: true, out var mon);
        Assert.True(b.Order(new P4Order(P4ActionKind.Cast, 24, 55)));
        b.Order(new P4Order(P4ActionKind.Parry));
        Assert.Equal(P4BattleFacing.Right, mon.Facing);
    }

    [OriginalDataFact]
    public void Flameball_Code_PatternIsTheXflam2Table()
    {
        Assert.Equal([(0, 0), (0, -1), (-1, 0), (0, 1), (1, 0)], P4Combat.FlameballSquares(Code, T.Combat, 1));
        var all = P4Combat.FlameballSquares(Code, T.Combat, 4);
        Assert.Equal(21, all.Count);
        Assert.Equal((-1, -2), all[13]); // −15
        Assert.Equal((2, -1), all[19]);  // −5
    }

    // --- The "× 5 + 50" success rolls compare unsigned in the code (bhi) ---------------------------------------------

    private sealed class Fixed(int value) : IRandomSource
    {
        public int Next(int minInclusive, int maxExclusive) => Math.Clamp(value, minInclusive, maxExclusive - 1);
    }

    [OriginalDataTheory]
    [InlineData(-1, 100, true, false)]  // below 0: unsigned huge, always works in the code; never under the manual
    [InlineData(-450, 1, true, false)]
    [InlineData(0, 1, false, false)]    // 0: rand(100) ≥ 1 always fails, either way
    [InlineData(50, 50, true, true)]
    [InlineData(50, 51, false, false)]
    [InlineData(150, 100, true, true)]
    public void SuccessRoll_CodeComparesUnsigned(int limit, int roll, bool code, bool manual)
    {
        Assert.Equal(code, P4Combat.SuccessRoll(Code, new Fixed(roll), limit));
        Assert.Equal(manual, P4Combat.SuccessRoll(Manual, new Fixed(roll), limit));
    }

    [OriginalDataFact]
    public void Fear_FarAboveTheCaster_AlwaysWorksInTheCode()
    {
        // 0x2AA4C: (1 − 20) × 5 + 50 = −45 → unsigned, rand(100) = 100 still passes.
        Assert.Equal(P4FearOutcome.LevelLowered, P4Combat.Fear(Code, new Fixed(100), 1, 20));
        Assert.Equal(P4FearOutcome.NoEffect, P4Combat.Fear(Manual, new Fixed(1), 1, 20));
    }

    [OriginalDataTheory]
    [InlineData(true, -1)] // code: (10 − 30) × 5 + 50 = −50 → always works (0x2C240)
    [InlineData(false, 0)] // manual: never works
    public void PartySlow_OnAFarStrongerMonster_FollowsTheUnsignedCompare(bool code, int spellMove)
    {
        var log = new Log(true); // rand(100) = 100
        var strong = B.Monsters[0] with { MagicResistance = 30 };
        var b = Fight(11, 42, 45, 31, code ? Code : Manual, log, strong, asleep: true, out var mon);
        Assert.True(b.Order(new P4Order(P4ActionKind.Cast, Spell: 11)));
        b.Order(new P4Order(P4ActionKind.Parry));
        Assert.Equal(spellMove, mon.SpellMove);
    }
}
