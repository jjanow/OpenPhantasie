using Microsoft.Xna.Framework.Input;
using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client.Tests;

public class UnifiedTownMenuTests
{
    [Fact]
    public void EveryCommandIsReachableByPad_WithWrappingAndRememberedRows()
    {
        var menu = new UnifiedTownMenu();
        var seen = new HashSet<UnifiedTownCommand>();
        for (var t = 0; t < UnifiedTownMenu.Tabs.Count; t++)
        {
            foreach (var expected in menu.Commands)
            {
                Assert.Equal(expected, menu.HandleKey(UnifiedTownMenu.PadKeys([Buttons.A]).Single()));
                seen.Add(expected);
                menu.HandleKey(UnifiedTownMenu.PadKeys([Buttons.DPadDown]).Single());
            }
            Assert.Equal(0, menu.Row);
            menu.HandleKey(Keys.Right);
        }
        Assert.Equal(Enum.GetValues<UnifiedTownCommand>().Length, seen.Count);
        menu.HandleKey(Keys.Up);
        Assert.Equal(UnifiedTownCommand.Leave, menu.Selected);
        menu.HandleKey(Keys.Right);
        menu.HandleKey(Keys.Left);
        Assert.Equal(UnifiedTownCommand.Leave, menu.Selected);
        Assert.Null(menu.HandleKey(Keys.Escape));
        Assert.Equal(3, menu.Tab); // never leaves town or opens an irreversible action
    }

    [Theory]
    [InlineData(Keys.A, UnifiedTownCommand.Armory)]
    [InlineData(Keys.B, UnifiedTownCommand.Bank)]
    [InlineData(Keys.G, UnifiedTownCommand.Guild)]
    [InlineData(Keys.M, UnifiedTownCommand.Mystic)]
    [InlineData(Keys.N, UnifiedTownCommand.Inn)]
    [InlineData(Keys.L, UnifiedTownCommand.Leave)]
    [InlineData(Keys.C, UnifiedTownCommand.Cast)]
    [InlineData(Keys.U, UnifiedTownCommand.Use)]
    [InlineData(Keys.D, UnifiedTownCommand.Distribute)]
    [InlineData(Keys.I, UnifiedTownCommand.Inspect)]
    [InlineData(Keys.S, UnifiedTownCommand.Save)]
    [InlineData(Keys.T, UnifiedTownCommand.Trade)]
    public void CommonLettersWorkFromEveryTab(Keys key, UnifiedTownCommand expected)
    {
        var menu = new UnifiedTownMenu();
        for (var tab = 0; tab < 4; tab++)
        {
            menu.AimTab(tab);
            Assert.Equal(expected, menu.HandleKey(key));
        }
    }

    [Fact]
    public void EmptyPartyAndMissingTownServicesExplainTheirAvailability()
    {
        foreach (var game in Enum.GetValues<GameId>())
        {
            var empty = new UnifiedTownContext(game, 0);
            Assert.Null(empty.Refusal(UnifiedTownCommand.Guild));
            Assert.Null(empty.Refusal(UnifiedTownCommand.Save));
            Assert.Null(empty.Refusal(UnifiedTownCommand.Load));
            Assert.Null(empty.Refusal(UnifiedTownCommand.Options));
            Assert.NotNull(empty.Refusal(UnifiedTownCommand.Leave));
            Assert.NotNull(empty.Refusal(UnifiedTownCommand.Inn));
            Assert.NotNull(empty.Refusal(UnifiedTownCommand.Training));
        }
        var inland = new UnifiedTownContext(GameId.P4, 6, Trade: true);
        Assert.NotNull(inland.Refusal(UnifiedTownCommand.Hall));
        Assert.NotNull(inland.Refusal(UnifiedTownCommand.Shipwright));
        Assert.Null((inland with { Hall = true }).Refusal(UnifiedTownCommand.Hall));
        Assert.Null((inland with { Shipwright = true }).Refusal(UnifiedTownCommand.Shipwright));
        Assert.NotNull(inland.Refusal(UnifiedTownCommand.AutoSave));
        Assert.NotNull(inland.Refusal(UnifiedTownCommand.Printout));
        Assert.NotNull(new UnifiedTownContext(GameId.P3, 6).Refusal(UnifiedTownCommand.Trade));
        Assert.NotNull(new UnifiedTownContext(GameId.P3, 6, AutoSaveRequired: true).Refusal(UnifiedTownCommand.AutoSave));
    }

    [Fact]
    public void OptionIsOffInEveryPresetAndRestoresWithTheSave()
    {
        var d = RuleOptionRegistry.UnifiedTownMenu;
        Assert.True(d.EveryGame && d.Qol);
        foreach (var game in Enum.GetValues<GameId>())
        {
            foreach (var preset in Enum.GetValues<OptionPreset>())
                Assert.False(GameOptions.ForPreset(game, preset).UnifiedTownMenu);
            var rules = GameOptions.ForPreset(game).With(d.Id, "on");
            var stored = GameOptionsSnapshot.From(rules);
            Assert.True(stored.ToOptions(game).UnifiedTownMenu);
            Assert.False(GameOptions.Restore(game, "custom", null, null).UnifiedTownMenu);
            Assert.Contains(d, RuleOptionRegistry.For(game));
        }
    }

    [OriginalDataFact]
    public void SpellShortcutFiltersChoices_AndNormalCastResetsTheFilter()
    {
        var s = ClientSessions.Overworld();
        s.EnterTown(1);
        var caster = s.Party.Members[1];
        caster.SpellsKnown[SpellId.HealingI] = 1;
        caster.SpellsKnown[SpellId.Transportation] = 1;
        var dialog = new CastDialog();
        dialog.Open(s, TownMenuData.Spells(GameId.P1, "transportation"));
        dialog.HandleKeys([Keys.D2], s);
        var lines = dialog.Lines(s, ClientSessions.Data);
        Assert.Equal(2, lines.Count); // heading plus Transportation
        Assert.Contains(ClientSessions.Data.GetSpell(SpellId.Transportation).Name, lines[1]);
        dialog.HandleKeys([Keys.Escape], s);
        dialog.Open(s);
        dialog.HandleKeys([Keys.D2], s);
        Assert.Contains(dialog.Lines(s, ClientSessions.Data), l => l.Contains(ClientSessions.Data.GetSpell(SpellId.HealingI).Name));
    }

    [OriginalDataFact]
    public void IvServicesAndShortcutReturnToTheSharedStreet_AndMissingDoorDoesNothing()
    {
        var rules = GameOptions.ForPreset(GameId.P4, OptionPreset.Dos).With("unified-town-menu", "on");
        var screen = new P4DungeonScreen(P4Preview.StartInTown(2, rules));
        Assert.True(screen.UnifiedTownReady);
        screen.RunUnifiedTownCommand(UnifiedTownCommand.Hall);
        Assert.True(screen.UnifiedTownReady); // no Hall in this town
        screen.RunUnifiedTownCommand(UnifiedTownCommand.Bank);
        Assert.False(screen.UnifiedTownReady);
        Assert.Equal(P4TownPrompt.Pick, screen.Game.Town.Prompt);
        screen.HandleKeys([Keys.D3]);
        Assert.True(screen.UnifiedTownReady);
        screen.RunUnifiedTownCommand(UnifiedTownCommand.Training);
        Assert.False(screen.UnifiedTownReady);
        Assert.Equal(P4TownPrompt.Who, screen.Game.Town.Prompt);
        screen.HandleKeys([Keys.Space]);
        Assert.True(screen.UnifiedTownReady);
        var caster = screen.Game.Party.Members[0];
        caster.Spells[0] = caster.Spells[46] = caster.Spells[64] = 1;
        caster.Mp = 255;
        screen.RunUnifiedTownCommand(UnifiedTownCommand.Transportation);
        Assert.True(screen.MenuOpen);
        Assert.Equal(P4MenuPrompt.Who, screen.Game.Menus!.Prompt);
        screen.HandleKeys([Keys.D1]);
        Assert.Equal(new[] { screen.Game.Services!.Names.Spells[46], screen.Game.Services.Names.Spells[64] },
            screen.Game.Menus.Choices.Select(c => c.Text));
        screen.HandleKeys([Keys.Space, Keys.Space]);
        Assert.True(screen.UnifiedTownReady);
    }

    [Fact]
    public void IvOptionsStayLockedAndHideImporterAndUpdater()
    {
        var options = new OptionsScreen(new ClientSettings(), _ => [], _ => true);
        options.OpenFor(GameOptions.ForPreset(GameId.P4).With("unified-town-menu", "on"));
        Assert.Equal(GameId.P4, options.Tab);
        Assert.Contains(options.RowTexts(), r => r.Contains(UiText.S("options.rules_locked")));
        options.HandleKeys([Keys.Left]); // General tab
        Assert.DoesNotContain(options.RowTexts(), r => r.Contains(UiText.S("options.import_games")));
    }
}
