using Microsoft.Xna.Framework.Input;
using OpenPhantasie.Client;
using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client.Tests;

/// <summary>PLAN P3.2 Authentic mode rest, the Client halves: the combat orders reset (D-078) and the combat spell list (D-063).</summary>
public class AuthenticModeClientTests
{
    private static GameSession FightUnder(GameOptions rules, int seed = 11)
    {
        var s = GameSessionFactory.Create(ClientSessions.Data, new SeededDosRandomSource(seed));
        s.StartNewGame(rules);
        s.OpenGuild();
        Assert.True(s.TryAddToParty(s.CreateAndEnroll("Fighto", Race.Human, CharacterClass.Fighter), out _));
        Assert.True(s.TryAddToParty(s.CreateAndEnroll("Priesto", Race.Elf, CharacterClass.Priest), out _));
        s.DepartGuildToWilderness();
        BeginFight(s);
        return s;
    }

    private static void BeginFight(GameSession s) =>
        s.BeginWildernessCombat(ClientSessions.Data.Monsters.First(m => m.HpBase <= 8).Id, SurpriseFlags.Normal);

    private static CombatPresenter AtOrders(GameSession s, CombatPresenter? ui = null)
    {
        ui ??= new CombatPresenter(new ClientSettings());
        ui.Start(s);
        Assert.Equal(CombatPhase.PartyOptions, ui.Phase);
        while (ui.OptionCursor != CombatText.DefaultPartyOptionIndex)
            ui.HandleKey(s, Keys.Down);
        ui.HandleKey(s, Keys.Enter); // FIGHT → orders
        Assert.Equal(0, ui.OrderMember);
        return ui;
    }

    [OriginalDataTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void Orders_ResetToParry_AfterLeavingTown_OnlyUnderDos_D078(bool dos)
    {
        var rules = dos ? GameOptions.ForPreset(GameId.P1).With(RuleOptionRegistry.OrdersReset.Id, "dos") : GameOptions.ForPreset(GameId.P1);
        var s = FightUnder(rules);
        var ui = AtOrders(s);
        var parry = ui.OrderLabelFor(s, 0);
        ui.HandleKey(s, Keys.D1); // the fighter's first menu order instead of PARRY
        if (ui.OrderMember == 0)
            ui.HandleKey(s, Keys.D1); // its rank
        var given = ui.OrderLabelFor(s, 0);
        Assert.NotEqual(parry, given);

        // End the fight, walk into a town and out again, meet the next monsters.
        ui.Cancel();
        foreach (var m in s.Combat!.AllMonsters)
            m.HitPoints = 0;
        s.ResolveCombatRound();
        Assert.Equal(GameMode.Overworld, s.Mode);
        s.EnterTown(1);
        Assert.True(s.LeaveTown());
        BeginFight(s);
        ui.Start(s);
        Assert.Equal(dos ? parry : given, ui.OrderLabelFor(s, 0));
    }

    [OriginalDataTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void CombatSpellPicker_ListsNonCombatSpells_OnlyUnderDos_D063(bool dos)
    {
        var rules = dos ? GameOptions.ForPreset(GameId.P1).With(RuleOptionRegistry.CombatSpellList.Id, "dos") : GameOptions.ForPreset(GameId.P1);
        var s = FightUnder(rules);
        s.Party.Members[1].SpellsKnown[SpellId.Vision] = 1;
        var ui = AtOrders(s);
        ui.HandleKey(s, Keys.Enter); // the fighter keeps PARRY
        Assert.Equal(1, ui.OrderMember);
        ui.HandleKey(s, Keys.D5); // CAST
        Assert.Equal(dos, ui.SpellChoices(s).Contains(SpellId.Vision));
    }
}
