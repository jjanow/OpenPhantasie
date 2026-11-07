using static OpenPhantasie.Core.Tests.Phantasie3CampaignTests;

namespace OpenPhantasie.Core.Tests;

/// <summary>
/// s136: III's inn (ovl2 <c>0x347d</c>, <c>body-areas.md</c> section 4): the town inn is free (manual), every living
/// member ages a stay and gets the ceiling of their level, the dead only rest in peace, and the magic chance is
/// <c>20 · (ceiling − max) &gt; rnd(100)</c> behind the remaster's gate (D3-069: the manual's cast gate).
/// </summary>
public class P3InnTests
{
    private static GameSession InTown()
    {
        var s = WithParty();
        Assert.Equal(GameMode.Town, s.Mode);
        s.Party.GoldCarried = 500;
        return s;
    }

    [OriginalDataFact]
    public void TownInn_IsFree_AndRestoresTheLiving()
    {
        var s = InTown();
        var m = s.Party.Members[2]; // the wizard
        m.HpCur = 1;
        m.MagicCur = 0;
        var gold = s.Party.GoldCarried + s.Sack.Gold;
        Assert.Equal(TownTransactionKind.Ok, s.RestAtInn().Kind);
        Assert.Equal(gold, s.Party.GoldCarried + s.Sack.Gold); // "Inns located in towns are free"
        Assert.Equal((m.HpMax, m.MagicMax), (m.HpCur, m.MagicCur));
    }

    [OriginalDataFact]
    public void Ceiling_FollowsTheLevel_Level15AndAboveRead15()
    {
        var s = InTown();
        var tables = s.CampaignAccess.CreationTables;
        var m = s.Party.Members[2];
        var row = tables.MpCeilingByClass[(int)m.Class];
        m.Level = 6;
        s.RestAtInn();
        Assert.Equal(row[5], m.MpCeiling);
        m.Level = 21;
        s.RestAtInn();
        Assert.Equal(row[14], m.MpCeiling);
    }

    [OriginalDataFact]
    public void DeadMembers_OnlyRestInPeace()
    {
        var s = InTown();
        var dead = s.Party.Members[1];
        dead.Status = CharacterStatus.Dead;
        dead.HpCur = 0;
        var (age, ceiling) = (dead.AgeClock, dead.MpCeiling);
        dead.Level = 10;
        var living = s.Party.Members[0];
        var livingAge = living.AgeClock;
        var r = s.RestAtInn();
        Assert.Equal((age, ceiling, 0), (dead.AgeClock, dead.MpCeiling, dead.HpCur));
        Assert.Equal(CharacterStatus.Dead, dead.Status);
        Assert.Equal(livingAge + TownMenus.InnStayAgeClockUnits, living.AgeClock);
        Assert.Contains(GameText.F("core.inn.x_is_resting_x", dead.Name, GameText.S("core.inn.in_peace")).TrimEnd(), r.Message);
    }

    [OriginalDataFact]
    public void MagicGrows_OnlyBelowTheCeiling_AndOnlyBehindTheGate()
    {
        var s = InTown();
        var m = s.Party.Members[2];
        m.Level = 15;
        var ceiling = P3Inn.Ceiling(s.CampaignAccess.CreationTables, m);
        m.MagicMax = ceiling - 5; // 20 · 5 = 100 > rnd(100) always: a sure point
        m.CastSinceRest = false; // D3-069 (manual): no cast, no gain
        s.RestAtInn();
        Assert.Equal(ceiling - 5, m.MagicMax);
        m.CastSinceRest = true;
        var r = s.RestAtInn();
        Assert.Equal(ceiling - 4, m.MagicMax);
        Assert.Contains(GameText.S("core.inn.magic_increases"), r.Message);
        Assert.False(m.CastSinceRest);
        m.MagicMax = ceiling;
        m.CastSinceRest = true;
        s.RestAtInn();
        Assert.Equal(ceiling, m.MagicMax); // at the ceiling: no roll, no point
    }
    [OriginalDataTheory]
    [InlineData(CastLocation.Town, false)]   // D3-069 (manual): only casts "while in the wilderness" count
    [InlineData(CastLocation.Field, true)]
    [InlineData(CastLocation.Dungeon, true)]
    public void D3069_ATownCastDoesNotOpenTheGate(CastLocation where, bool counts)
    {
        var s = InTown();
        var caster = s.Party.Members[2];
        caster.SpellsKnown[SpellId.HealingI] = 1;
        caster.MagicCur = caster.MagicMax = 20;
        caster.CastSinceRest = false;
        var r = SpellCaster.CastOutOfCombat(s.Party, caster, SpellId.HealingI, caster, new SeededDosRandomSource(1), where,
            rules: s.Options);
        Assert.True(r.Success, r.Message);
        Assert.Equal(counts, caster.CastSinceRest);
    }
}
