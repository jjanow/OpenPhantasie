using Microsoft.Xna.Framework.Input;
using OpenPhantasie.Client;
using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client.Tests;

/// <summary>
/// s165: III's Damage Report window (Amiga <c>0x1d4ea</c>) and the out-of-fight caster's magic-first refusals (Amiga
/// <c>0xf8f6</c>) in the Client.
/// </summary>
public class P3DamageReportClientTests
{
    private static readonly GameDataRepository P3Data = GameDataRepository.Load(GameDataRepository.DefaultPathFor(GameId.P3));

    private static GameSession Party(int members)
    {
        var s = GameSessionFactory.Create(P3Data, new SeededDosRandomSource(4));
        s.StartNewGame(GameOptions.ForPreset(GameId.P3));
        s.OpenGuild();
        for (var i = 0; i < members; i++)
            Assert.True(s.TryAddToParty(s.CreateAndEnroll("M" + i, Race.Human,
                i == 1 ? CharacterClass.Priest : CharacterClass.Fighter), out _));
        s.LeaveGuild();
        return s;
    }

    private static P3DamageReport Report(int rows) => new(P3DamageReportKind.Trap,
        [.. Enumerable.Range(0, rows).Select(i => new P3DamageReportRow("NAME" + i, P3DamageOutcome.Died, 99))],
        ["NAME0 takes 99 damage.", "NAME0 dies!"], "A trap!!!");

    [OriginalDataFact]
    public void PackWindow_IsWindow6WithOneOk_SevenRowsClearTheButton()
    {
        var layout = GraphicsPackCatalog.Load(GameDataRepository.DefaultPathFor(GameId.P3)).Find("amiga")?.KeepWindows;
        var w = layout?.DamageReport;
        Assert.NotNull(w);
        Assert.Equal(layout!.Heal!.Box, w!.Box); // struct 0x23360
        Assert.Equal((104, 56), w.Text[0]);       // cell (13, 7)
        Assert.Equal((16, 72), w.Text[1]);        // the name: column 2, row 9
        Assert.Equal((80, 72), w.Text[2]);        // the line: column 10
        Assert.Equal(layout.Heal.Ok, w.Ok);       // slot 1 only (+26 = 1)
        Assert.Equal([9, 15, 9, 10], w.Colours);
        // Six members plus an elemental: the last row still ends above OK, so the box never grows and nothing is cut.
        Assert.True(w.Text[1].Y + 8 * 7 + 2 <= w.Ok!.Value.Y);
    }

    [OriginalDataFact]
    public void Box_OffersEachReportOnce_AnyKeyCloses()
    {
        var s = Party(2);
        var box = new P3DamageReportBox();
        Assert.False(box.Offer(s)); // a session first seen only sets the mark
        Assert.True(s.LeaveTown());
        s.EnterDungeon(5, 23, 7, loadSaved: false); // s163: one step south is the archives' trap 2
        s.Party.Members[0].Skills = s.Party.Members[0].Skills with { SpotTrap = 0 };
        s.Party.Members[1].Skills = s.Party.Members[1].Skills with { SpotTrap = 0 };
        s.MoveDungeon(CardinalDirection.South);
        Assert.True(box.Offer(s));
        Assert.True(box.Shown);
        Assert.Equal(s.Party.Members.Count, box.Report!.Rows.Count);
        Assert.False(box.Offer(s)); // the same report is not offered twice
        Assert.True(box.HandleKeys([]));   // up: owns the frame
        Assert.True(box.Shown);
        Assert.True(box.HandleKeys([Keys.X])); // any key closes it
        Assert.False(box.Shown);
        Assert.False(box.HandleKeys([Keys.X])); // closed: the keys go on
        var lines = P3DamageReportBox.PanelLines(s.DamageReport!);
        Assert.Equal(1 + s.Party.Members.Count, lines.Count);
        Assert.Equal("Damage Report", lines[0]);
    }

    [OriginalDataFact]
    public void BandLine_LosesTheReportLines_KeepsTheTrapLine()
    {
        var report = Report(1);
        Assert.Equal("A trap!!!", P3DamageReportBox.WithoutReportLines("A trap!!! NAME0 takes 99 damage. NAME0 dies!", report));
        Assert.Equal("A trap!!!", P3DamageReportBox.WithoutReportLines("NAME0 dies!", report)); // the step's last line
        Assert.Equal("Other", P3DamageReportBox.WithoutReportLines("Other", report));
        Assert.Equal("", P3DamageReportBox.WithoutReportLines("", report));
        var al = new P3DamageReport(P3DamageReportKind.Trap, [], ["AL takes 3 damage."], "A trap!!!");
        Assert.Equal("SAL takes 3 damage.", P3DamageReportBox.WithoutReportLines("SAL takes 3 damage. AL takes 3 damage.", al));
    }

    [OriginalDataFact]
    public void Cast_III_RefusesOnMagicBeforeTheListAndTheTarget()
    {
        var s = Party(2);
        var priest = s.Party.Members[1];
        priest.SpellsKnown[SpellId.HealingIi] = 1;
        var tooLow = GameText.S("core.magic.not_enough_mp");
        var dialog = new CastDialog();
        priest.MagicCur = 0;
        Assert.Null(dialog.Open(s));
        Assert.Equal(tooLow, dialog.HandleKeys([Keys.D2], s)); // before the list (0xf940)
        Assert.False(dialog.IsOpen);

        var cost = Magic.MagicPointCost(SpellId.HealingIi, SpellSlots.For(GameId.P3));
        priest.MagicCur = cost - 1;
        Assert.Null(dialog.Open(s));
        Assert.Null(dialog.HandleKeys([Keys.D2], s)); // the list
        var lines = dialog.Lines(s, P3Data);
        var row = lines.Skip(1).ToList().FindIndex(l => l.Contains(P3Data.GetSpell(SpellId.HealingIi).Name));
        Assert.True(row >= 0);
        Assert.Equal(tooLow, dialog.HandleKeys([Keys.D1 + row], s)); // before the target (0xf990)
        Assert.False(dialog.IsOpen);

        priest.MagicCur = cost;
        dialog.Open(s);
        dialog.HandleKeys([Keys.D2], s);
        Assert.Null(dialog.HandleKeys([Keys.D1 + row], s)); // enough: the target is asked
        Assert.Equal(UiText.S("cast.heal_whom"), dialog.Lines(s, P3Data)[0]);
    }

    [OriginalDataFact]
    public void Cast_I_KeepsItsOrder_TargetFirst()
    {
        var s = ClientSessions.Overworld();
        var priest = s.Party.Members[1];
        priest.SpellsKnown[SpellId.HealingIi] = 1;
        priest.MagicCur = 0;
        var dialog = new CastDialog();
        dialog.Open(s);
        Assert.Null(dialog.HandleKeys([Keys.D2], s)); // I lists the spells with no magic left
        var lines = dialog.Lines(s, ClientSessions.Data);
        var row = lines.Skip(1).ToList().FindIndex(l => l.Contains("HEALING II"));
        Assert.True(row >= 0);
        Assert.Null(dialog.HandleKeys([Keys.D1 + row], s)); // and asks the target first
        Assert.Equal(UiText.S("cast.heal_whom"), dialog.Lines(s, ClientSessions.Data)[0]);
    }
}
