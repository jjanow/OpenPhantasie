using static OpenPhantasie.Core.Tests.Phantasie3CampaignTests;

namespace OpenPhantasie.Core.Tests;

/// <summary>
/// s165: III's Damage Report (Amiga <c>0x1d4ea</c>, static): every member gets a row in party order, one already at 0 HP
/// "is dead.", a member the damage kills "takes n damage and dies."; and III's out-of-fight caster refuses on the magic
/// before the list and before the target (Amiga <c>0xf8f6</c>).
/// </summary>
public class P3DamageReportTests
{
    [OriginalDataFact]
    public void FailedDisarm_ReportsEveryMember_TheDeadToo()
    {
        var s = WithParty();
        Phantasie3DungeonTests.Enter(s, 5, 23, 8);
        s.CampaignAccess.DungeonEventPrompt = new PendingDungeonEvent(2, -1,
            new DungeonMessage([GameText.F("core.p3.x_spots_a_trap", s.Party.Members[0].Name)],
                [SessionPrompts.Yes, SessionPrompts.No]));
        var prompts = new SessionPrompts(s);
        prompts.Answer(1); // OK: who disarms?
        var thief = s.Party.Members[3];
        thief.Skills = thief.Skills with { DisarmTrap = 0 }; // the roll fails: the trap springs
        s.Party.Members[2].HpCur = 0;
        s.Party.Members[2].Status = CharacterStatus.Dead;
        s.Party.Members[1].HpCur = 1; // rnd(12) >= 1 kills
        var serial = s.DamageReportSerial;
        prompts.Answer(4);

        Assert.Equal(serial + 1, s.DamageReportSerial);
        var report = Assert.IsType<P3DamageReport>(s.DamageReport);
        Assert.Equal(P3DamageReportKind.Trap, report.Kind);
        Assert.Equal(s.Party.Members.Select(m => m.Name), report.Rows.Select(r => r.Name)); // party order, all of them
        Assert.Equal(P3DamageOutcome.AlreadyDead, report.Rows[2].Outcome);
        Assert.Equal("is dead.", report.Rows[2].Text(report.Kind));
        Assert.Equal(P3DamageOutcome.Died, report.Rows[1].Outcome);
        Assert.Equal($"takes {report.Rows[1].Damage} damage and dies.", report.Rows[1].Text(report.Kind));
        Assert.All(new[] { report.Rows[0], report.Rows[3] }, r => Assert.True(r.Outcome is P3DamageOutcome.Damaged
            or P3DamageOutcome.Died));
        Assert.Equal(GameText.S("core.p3.a_trap"), report.Lead);
        Assert.Contains(GameText.F("core.p3.damage_report_x_x", report.Rows[0].Name, report.Rows[0].Damage), report.Notes);
        Assert.Equal("Damage Report", P3DamageReport.Title);
    }

    [OriginalDataFact]
    public void ReportLines_InIIIsWords()
    {
        Assert.Equal("avoids the trap.", new P3DamageReportRow("A", P3DamageOutcome.Unhurt, 0).Text(P3DamageReportKind.Trap));
        Assert.Equal("escapes unscathed.",
            new P3DamageReportRow("A", P3DamageOutcome.Unhurt, 0).Text(P3DamageReportKind.Escape));
        Assert.Equal("takes 7 damage.", new P3DamageReportRow("A", P3DamageOutcome.Damaged, 7).Text(P3DamageReportKind.Trap));
    }

    [OriginalDataFact]
    public void OutOfFightCaster_III_RefusesOnMagicFirst()
    {
        var s = WithParty();
        var priest = s.Party.Members[1];
        priest.MagicCur = 0;
        var tooLow = GameText.S("core.magic.not_enough_mp");
        Assert.Equal(tooLow, SpellCaster.OutOfCombatMagicRefusal(priest, null, GameId.P3)); // before the list (0xf940)
        var cost = Magic.MagicPointCost(SpellId.HealingIi, SpellSlots.For(GameId.P3));
        Assert.True(cost >= 2);
        priest.MagicCur = cost - 1;
        Assert.Null(SpellCaster.OutOfCombatMagicRefusal(priest, null, GameId.P3));
        Assert.Equal(tooLow, SpellCaster.OutOfCombatMagicRefusal(priest, SpellId.HealingIi, GameId.P3)); // before the target
        priest.MagicCur++;
        Assert.Null(SpellCaster.OutOfCombatMagicRefusal(priest, SpellId.HealingIi, GameId.P3));
        priest.MagicCur = 0;
        Assert.Null(SpellCaster.OutOfCombatMagicRefusal(priest, SpellId.HealingI, GameId.P1)); // I / II keep their order
        Assert.Null(SpellCaster.OutOfCombatMagicRefusal(priest, null, GameId.P2));
    }
}
