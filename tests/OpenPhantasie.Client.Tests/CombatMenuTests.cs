using Microsoft.Xna.Framework.Input;
using OpenPhantasie.Client;
using OpenPhantasie.Core;

namespace OpenPhantasie.Client.Tests;

/// <summary>PARTY OPTIONS Show names / Party health (OV2 0x3e6f / 0x45a0), menu memory, and the combat spell list (D-035).</summary>
public class CombatMenuTests
{
    private static (GameSession, CombatPresenter) AtPartyOptions()
    {
        var session = ClientSessions.Fight();
        var ui = new CombatPresenter(new ClientSettings());
        ui.Start(session);
        Assert.Equal(CombatPhase.PartyOptions, ui.Phase);
        return (session, ui);
    }

    private static void MoveTo(GameSession s, CombatPresenter ui, int index)
    {
        while (ui.OptionCursor != index)
            ui.HandleKey(s, Keys.Down);
    }

    [OriginalDataFact]
    public void Menu_HasTheSevenDosEntries_InOrder() =>
        Assert.Equal(["BEG MERCY", "THREATEN", "GREETINGS", "FIGHT", "FLEE", "SHOW NAMES", "PARTY HEALTH"],
            CombatText.PartyOptionMenu.Select(e => e.Label));

    [OriginalDataFact]
    public void ShowNames_OpensTheRankList_AndReturnsToTheMenu()
    {
        var (s, ui) = AtPartyOptions();
        MoveTo(s, ui, CombatText.ShowNamesIndex);
        ui.HandleKey(s, Keys.Enter);
        Assert.Equal(CombatText.RankLines(s.Combat), ui.InfoBox);
        Assert.StartsWith("RANK 1 : ", ui.InfoBox[0]);
        Assert.Equal(CombatPhase.PartyOptions, ui.Phase);
        Assert.False(s.CombatOptionChosenThisRound); // no round used

        ui.HandleKey(s, Keys.Space); // any key closes it
        Assert.Empty(ui.InfoBox);
        Assert.Equal(CombatPhase.PartyOptions, ui.Phase);
        Assert.Equal(CombatText.ShowNamesIndex, ui.OptionCursor);
    }

    [OriginalDataFact]
    public void PartyHealth_PrintsTheDosHealthReport()
    {
        var (s, ui) = AtPartyOptions();
        MoveTo(s, ui, CombatText.PartyHealthIndex);
        ui.HandleKey(s, Keys.Enter);
        Assert.Equal(CombatText.HealthReportTitle, ui.InfoBox[0]);
        Assert.Equal(1 + s.Party.Members.Count, ui.InfoBox.Count);
        Assert.False(s.CombatOptionChosenThisRound);
    }

    [Fact]
    public void HealthLine_MatchesTheDosFormat()
    {
        // "%-10.9s%c HP%4d/%-4dA:%d MP%3d/%d"
        var ch = new Character { Name = "ABCDEFGHIJKL", Status = CharacterStatus.Sleep, HpCur = 7, HpMax = 8, MagicCur = 3, MagicMax = 10 };
        Assert.Equal("ABCDEFGHI S HP   7/8   A:3 MP  3/10", CombatText.HealthLine(ch, 150));
        ch.Status = CharacterStatus.Dead;
        Assert.StartsWith("ABCDEFGHI D HP", CombatText.HealthLine(ch, 0));
        ch.Status = CharacterStatus.Ok;
        Assert.Contains("A:4 ", CombatText.HealthLine(ch, 225));
    }

    [OriginalDataFact]
    public void RepeatOrders_SurviveTheNextEncounter()
    {
        // [D✓] s65 DOSBox: orders persist across rounds and encounters (DOS resets them only on leaving town, D-078).
        var (s, ui) = AtPartyOptions();
        Assert.StartsWith("PARRY", ui.OrderLabelFor(s, 0));
        MoveTo(s, ui, CombatText.DefaultPartyOptionIndex);
        ui.HandleKey(s, Keys.Enter); // FIGHT
        Assert.Equal(CombatPhase.Orders, ui.Phase);
        ui.HandleKey(s, Keys.D1); // THRUST
        if (ui.Phase == CombatPhase.Orders && s.Combat!.Ranks.Count > 1 && ui.OrderLabelFor(s, 0).StartsWith("PARRY"))
            ui.HandleKey(s, Keys.D1); // rank 1 when a rank prompt follows
        Assert.StartsWith("THRUST", ui.OrderLabelFor(s, 0));
        ui.Start(s); // the next encounter's build
        Assert.StartsWith("THRUST", ui.OrderLabelFor(s, 0));
    }

    [OriginalDataFact]
    public void PartyOptionsHighlight_IsNotResetWhenTheMenuReopens()
    {
        var (s, ui) = AtPartyOptions();
        Assert.Equal(CombatText.DefaultPartyOptionIndex, ui.OptionCursor); // first-ever highlight: FIGHT [?]
        MoveTo(s, ui, CombatText.PartyHealthIndex);
        ui.Start(s); // the menu is built again (next round / next fight)
        Assert.Equal(CombatPhase.PartyOptions, ui.Phase);
        Assert.Equal(CombatText.PartyHealthIndex, ui.OptionCursor);
    }

    [Fact]
    public void CombatSpellChoices_ListOnlyCombatAndEverywhereSpells_D035()
    {
        var ch = new Character { SpellsKnown = new int[SpellId.Count] };
        foreach (var id in new[] { SpellId.HealingI, SpellId.FireflashI, SpellId.Resurrection, SpellId.MonsterEval, SpellId.Vision,
                     SpellId.Transportation, SpellId.DivineI, SpellId.Awaken })
            ch.SpellsKnown[id] = 1;
        Assert.Equal([SpellId.HealingI, SpellId.FireflashI, SpellId.Resurrection, SpellId.Awaken],
            CombatText.CombatSpellChoices(ch));
    }

    [Fact]
    public void CombatSpellChoices_III_OffersAccuracyPastIs55()
    {
        var ch = new Character { SpellsKnown = new int[57] };
        foreach (var id in new[] { SpellId.Transportation, P3Spells.Accuracy, P3Spells.PartyAccuracy, 56 })
            ch.SpellsKnown[id] = 1;
        Assert.Equal([P3Spells.Accuracy, P3Spells.PartyAccuracy],
            CombatText.CombatSpellChoices(ch, GameOptions.ForPreset(GameId.P3)));
    }

    [Fact]
    public void CombatSpellChoices_III_DosListsEverySpell_ThePickIsStillRefused_D063()
    {
        var ch = new Character { SpellsKnown = new int[57], MagicCur = 50 };
        foreach (var id in new[] { SpellId.FireflashI, SpellId.MonsterEval, SpellId.Transportation, 56 })
            ch.SpellsKnown[id] = 1;
        var manual = GameOptions.ForPreset(GameId.P3);
        var dos = manual.With("p3.combat-spell-list", "dos");
        Assert.Equal([SpellId.FireflashI], CombatText.CombatSpellChoices(ch, manual));
        Assert.Equal([SpellId.FireflashI, SpellId.MonsterEval, SpellId.Transportation, 56], CombatText.CombatSpellChoices(ch, dos));
        Assert.Equal("dos", GameOptions.ForPreset(GameId.P3, OptionPreset.Dos).Get("p3.combat-spell-list"));
        // Either way DOS III's refusal (root 0xfc41) turns the pick into a Thrust with its message.
        Assert.Equal("core.p3.spell_not_in_combat", P3CombatModule.CastRefusal(ch, SpellId.Transportation, null, dos));
        Assert.Null(P3CombatModule.CastRefusal(ch, SpellId.FireflashI, null, dos));
        // I's option stays I's: III's value doesn't open I's list.
        Assert.False(SpellCaster.IsCombatCastable(SpellId.Vision, GameOptions.ForPreset(GameId.P1)));
    }

    [OriginalDataFact]
    public void SpellPicker_ShowsTheListAndANonCombatNumberTakesTheParryPath()
    {
        var (s, ui) = AtPartyOptions();
        var priest = s.Party.Members[1];
        priest.SpellsKnown[SpellId.Vision] = 1;
        MoveTo(s, ui, CombatText.DefaultPartyOptionIndex);
        ui.HandleKey(s, Keys.Enter); // FIGHT → orders
        ui.HandleKey(s, Keys.Enter); // fighter keeps PARRY
        Assert.Equal(1, ui.OrderMember);
        ui.HandleKey(s, Keys.D5); // CAST
        Assert.All(ui.SpellChoices(s), id => Assert.True(CombatText.IsCombatCastable(id)));
        Assert.DoesNotContain(SpellId.Vision, ui.SpellChoices(s));

        var mp = priest.MagicCur;
        foreach (var k in (SpellId.Vision + 1).ToString())
            ui.HandleKey(s, Keys.D0 + (k - '0'));
        ui.HandleKey(s, Keys.Enter);
        Assert.Equal(CombatPhase.Resolving, ui.Phase);
        Assert.Equal(mp, priest.MagicCur);
        Assert.Equal("PARRY", ui.OrderLabelFor(s, 1));
    }
}
