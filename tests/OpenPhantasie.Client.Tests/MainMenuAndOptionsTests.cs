using Microsoft.Xna.Framework.Input;
using OpenPhantasie.Client;
using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client.Tests;

/// <summary>T5.0 (<c>09-startup-options.md</c>): the main menu, the Options screen and the game menu's new entries.</summary>
public class MainMenuAndOptionsTests
{
    private static readonly IReadOnlyList<(string Id, string Name)> TwoPacks = [("amiga", "Amiga"), ("dos-cga", "DOS CGA")];

    private static OptionsScreen Options(ClientSettings settings) =>
        new(settings, g => g == GameId.P1 ? TwoPacks : [], g => g == GameId.P1);

    private static string Line(OptionsScreen o, string startsWith) =>
        o.RowTexts().Select(l => l.TrimStart()).Single(t => t.StartsWith(startsWith, StringComparison.Ordinal));

    [OriginalDataFact]
    public void MainMenu_ListsFourGames_OnlyBuiltOnesStart()
    {
        var menu = new MainMenuScreen(g => g == GameId.P1);
        var lines = menu.Lines();
        Assert.Equal("PHANTASIE", lines[0].Text);
        Assert.Equal(">PHANTASIE I", lines[2].Text);
        Assert.Contains("(NOT BUILT YET)", lines[3].Text);
        Assert.Equal(MenuLineStyle.Dim, lines[3].Style);

        Assert.Null(menu.HandleKeys([Keys.Down, Keys.Enter])); // II can't be picked
        Assert.Equal((MainMenuChoice.Options, GameId.P1), menu.HandleKeys([Keys.Down, Keys.Down, Keys.Down, Keys.Enter]));
        Assert.Equal((MainMenuChoice.Quit, GameId.P1), menu.HandleKeys([Keys.Down, Keys.Enter]));
        Assert.Equal((MainMenuChoice.Play, GameId.P1), menu.HandleKeys([Keys.Down, Keys.Enter])); // wraps to I
    }

    [OriginalDataFact]
    public void ShippedGames_AllFourArePlayable()
    {
        Assert.True(MainMenuScreen.IsPlayable(GameId.P1));
        Assert.True(MainMenuScreen.IsPlayable(GameId.P2)); // T3: II's campaign
        Assert.True(MainMenuScreen.IsPlayable(GameId.P3)); // T4: III's campaign
        Assert.True(MainMenuScreen.IsPlayable(GameId.P4)); // s106: new game in Pendragon, creation at the Guild
        Assert.False(Campaigns.IsBuilt(GameId.P4));         // IV's game is Phantasie4Campaign, not an ICampaign
    }

    [OriginalDataFact]
    public void Options_IIITab_ListsIIIsRules_WithTheShippedGames()
    {
        var o = new OptionsScreen(new ClientSettings(), _ => []); // the default: MainMenuScreen.IsPlayable
        o.Open();
        o.HandleKeys([Keys.Right, Keys.Right, Keys.Right]);
        Assert.Equal(GameId.P3, o.Tab);
        Assert.DoesNotContain(o.Lines(), l => l.Text.Contains("NOT BUILT YET"));
        for (var i = 0; i < 40 && !o.Lines().Any(l => l.Text.Contains("SKILL TRAINING TABLES")); i++)
            o.HandleKeys([Keys.Down]);
        Assert.Contains(o.Lines(), l => l.Text.Contains("SKILL TRAINING TABLES"));
    }

    [Fact]
    public void Options_FromTheMainMenu_HasGeneralAndFourGameTabs()
    {
        var o = Options(new ClientSettings());
        o.Open();
        Assert.Null(o.Tab);
        Assert.Equal(">[GENERAL]  I   II   III   IV ", o.Lines()[1].Text);
        o.HandleKeys([Keys.Right]);
        Assert.Equal(GameId.P1, o.Tab);
        o.HandleKeys([Keys.Right]);
        Assert.Equal(GameId.P2, o.Tab);
        Assert.Contains(o.Lines(), l => l.Text.Contains("NOT BUILT YET"));
        o.HandleKeys([Keys.Left, Keys.Left, Keys.Left]);
        Assert.Equal(GameId.P4, o.Tab); // wraps
    }

    [Fact]
    public void PresetLabel_NamesEachGamesOriginal_IIIsTheAtariSt()
    {
        // II's tab once its game is playable (here: forced on): the original-code preset is the ST's, not DOS's.
        var settings = new ClientSettings();
        var o = new OptionsScreen(settings, _ => TwoPacks, _ => true);
        o.Open();
        o.HandleKeys([Keys.Right, Keys.Right]); // tab II
        Assert.Equal(GameId.P2, o.Tab);
        Assert.EndsWith("ORIGINAL (MANUAL)", Line(o, "PRESET"));
        Assert.EndsWith("WIZARD, PRIEST", Line(o, "TRANSFER: SPELLS"));
        Assert.EndsWith("#55 AND UP", Line(o, "TRANSFER: DIVINE KEPT"));
        Assert.EndsWith("FORMULA", Line(o, "TRANSFER: GOLD"));
        settings.Game(GameId.P2).SetNewGameOptions(GameOptions.ForPreset(GameId.P2, OptionPreset.Dos));
        Assert.EndsWith("ORIGINAL (ATARI ST)", Line(o, "PRESET"));
        Assert.EndsWith("ALSO RANGER, MONK", Line(o, "TRANSFER: SPELLS"));

        settings.Game(GameId.P1).SetNewGameOptions(GameOptions.ForPreset(GameId.P1, OptionPreset.Dos));
        o.HandleKeys([Keys.Left]); // tab I
        Assert.Equal(GameId.P1, o.Tab);
        Assert.EndsWith("ORIGINAL (DOS)", Line(o, "PRESET")); // the fallback label
    }

    [Fact]
    public void GeneralTab_ChangesScaleWithinRange()
    {
        var settings = new ClientSettings { IntegerScale = 6 };
        var o = Options(settings);
        o.Open();
        o.HandleKeys([Keys.Down, Keys.Right]);
        Assert.Equal(6, settings.IntegerScale); // already the largest
        o.HandleKeys([Keys.Left, Keys.Left]);
        Assert.Equal(4, settings.IntegerScale);
        Assert.True(o.Changed);
        Assert.True(o.HandleKeys([Keys.Escape]));
        Assert.False(o.IsOpen);
    }

    [Fact]
    public void GameTab_SwitchesPresentation_AndNewGameRules()
    {
        var settings = new ClientSettings();
        var o = Options(settings);
        o.Open();
        o.HandleKeys([Keys.Right]); // tab I
        Assert.StartsWith("GRAPHICS", Line(o, "GRAPHICS"));
        Assert.EndsWith("Amiga", Line(o, "GRAPHICS"));

        o.HandleKeys([Keys.Down, Keys.Right]); // graphics → DOS CGA
        Assert.Equal("dos-cga", settings.Game(GameId.P1).Graphics);
        o.HandleKeys([Keys.Down]); // → title screen (s102), on by default = as the originals
        Assert.EndsWith("ON", Line(o, "TITLE SCREEN"));
        o.HandleKeys([Keys.Enter]);
        Assert.False(settings.Game(GameId.P1).TitleScreen);
        o.HandleKeys([Keys.Down]); // → title tune (s108), on by default = as the originals
        Assert.EndsWith("ON", Line(o, "TITLE TUNE"));
        o.HandleKeys([Keys.Enter]);
        Assert.False(settings.Game(GameId.P1).TitleTune);
        o.HandleKeys([Keys.Down]); // → AMIGA AUDIO FILTER (s160: the A500's output filters), on by default = as the A500
        Assert.EndsWith("ON", Line(o, "AMIGA AUDIO FILTER"));
        o.HandleKeys([Keys.Enter]);
        Assert.False(settings.Game(GameId.P1).A500Filter);
        Assert.EndsWith("OFF", Line(o, "AMIGA AUDIO FILTER"));
        o.HandleKeys([Keys.Down]); // → AMIGA SCREENS (s132: I's combat screen), on by default = as the original
        Assert.EndsWith("ON", Line(o, "AMIGA SCREENS"));
        o.HandleKeys([Keys.Enter]);
        Assert.False(settings.Game(GameId.P1).OriginalScreens);
        o.HandleKeys([Keys.Down]); // → COMBAT SPEED (the Amiga's Speed menu)
        Assert.EndsWith("NORMAL", Line(o, "COMBAT SPEED"));
        o.HandleKeys([Keys.Down, Keys.Enter]); // diagonal moves off
        Assert.False(settings.Game(GameId.P1).DiagonalMoves);

        o.HandleKeys([Keys.Down, Keys.Down]); // status lines → continue from a list
        Assert.EndsWith("OFF", Line(o, "CONTINUE FROM A LIST")); // off = the manual's Guild-number question
        o.HandleKeys([Keys.Enter]);
        Assert.True(settings.Game(GameId.P1).ContinueFromList);
        Assert.EndsWith("ON", Line(o, "CONTINUE FROM A LIST"));

        o.HandleKeys([Keys.Down]); // → inventory screen (09 T2), off = the original screens
        Assert.EndsWith("OFF", Line(o, "NEW INVENTORY SCREEN"));
        o.HandleKeys([Keys.Enter]);
        Assert.True(settings.Game(GameId.P1).InventoryScreen);

        o.HandleKeys([Keys.Down]); // → time lag and sound (D-073 QoL), off = the manual's T command
        Assert.EndsWith("T COMMAND", Line(o, "TIME LAG AND SOUND"));

        o.HandleKeys([Keys.Down]); // → colorblind palette (P3 QoL), off = the original colours
        Assert.EndsWith("OFF", Line(o, "COLORBLIND PALETTE"));
        o.HandleKeys([Keys.Down]); // → preset (the header is skipped)
        Assert.EndsWith("ORIGINAL (MANUAL)", Line(o, "PRESET"));
        o.HandleKeys([Keys.Right]);
        Assert.EndsWith("ORIGINAL (DOS)", Line(o, "PRESET"));
        Assert.EndsWith("ZERO STATS", Line(o, "SUMMONED ELEMENTAL"));
        Assert.Equal(ElementalPrepRule.Dos, settings.Game(GameId.P1).NewGameOptions(GameId.P1).ElementalPrep);

        o.HandleKeys([Keys.Down, Keys.Right]); // elemental back to full stats: the other DOS values still differ → custom
        Assert.EndsWith("CUSTOM", Line(o, "PRESET"));
        Assert.EndsWith("65,535", Line(o, "GOLD CAP"));

        // Every DOS-side value back to the manual's → matches manual again.
        o.HandleKeys([Keys.Down, Keys.Down, Keys.Left]); // armory restock → weapons, potions
        o.HandleKeys([Keys.Down, Keys.Left]);            // gold cap 65,535 → 65,000
        Assert.EndsWith("CUSTOM", Line(o, "PRESET"));
        o.HandleKeys([Keys.Down, Keys.Left]);            // rename party members → no
        o.HandleKeys([Keys.Down, Keys.Left]);            // stunning blow → survivors only (D-015)
        o.HandleKeys([Keys.Down, Keys.Left]);            // armor reduced → lowers armor (D-016)
        Assert.EndsWith("CUSTOM", Line(o, "PRESET"));
        o.HandleKeys([Keys.Down, Keys.Left]);            // payout remainder → first survivor (Q2)
        var p1Rules = RuleOptionRegistry.For(GameId.P1);
        // Up to the last option whose DOS value differs (the every-game QoL guild-limit after it is the game's cap in both).
        var switches = p1Rules.ToList().FindLastIndex(d => d.Manual != d.Dos) - p1Rules.ToList().IndexOf(RuleOptionRegistry.PayoutRemainder);
        for (var k = 1; k < switches; k++)               // the P3.2 manual-or-DOS switches, D-011 …
            o.HandleKeys([Keys.Down, Keys.Left]);
        Assert.Equal(OptionPreset.Custom, settings.Game(GameId.P1).NewGameOptions(GameId.P1).Preset); // PRESET has scrolled off
        o.HandleKeys([Keys.Down, Keys.Left]);            // … the last one
        Assert.Equal(OptionPreset.Manual, settings.Game(GameId.P1).NewGameOptions(GameId.P1).Preset);
    }

    [Fact]
    public void GameTab_ListsOtherGamesModules_PlaceholdersFixed()
    {
        var o = Options(new ClientSettings());
        o.Open();
        o.HandleKeys([Keys.Right, Keys.Up]); // tab I, then Up wraps to the last row (the list is longer than the screen)
        Assert.Contains(o.Lines(), l => l.Text.Contains("FROM OTHER GAMES") && l.Style == MenuLineStyle.Header);
        var injury = o.Lines().Single(l => l.Text.Contains("BODY-PART INJURY (III)"));
        Assert.EndsWith("NOT BUILT", injury.Text);
        Assert.Equal(MenuLineStyle.Dim, injury.Style);
        var rock = o.Lines().Single(l => l.Text.Contains("TOSS ROCK (II)")); // built (s79): a switch, off in I's presets
        Assert.DoesNotContain("NOT BUILT", rock.Text);
        Assert.NotEqual(MenuLineStyle.Dim, rock.Style);
        Assert.DoesNotContain(o.Lines(), l => l.Text.Contains("COMBAT ORDERS")); // I's own module
    }

    /// <summary>s160: AMIGA AUDIO FILTER is a row of the Paula games' tabs only (I, III), right after TITLE TUNE.</summary>
    [OriginalDataFact]
    public void A500FilterRow_OnlyOnTheAmigaSoundTabs()
    {
        var o = new OptionsScreen(new ClientSettings(), _ => [], _ => true);
        o.Open();
        foreach (var game in new[] { GameId.P1, GameId.P2, GameId.P3, GameId.P4 })
        {
            o.HandleKeys([Keys.Right]);
            Assert.Equal(game, o.Tab);
            var rows = o.RowTexts().Select(t => t.TrimStart()).ToList();
            var at = rows.FindIndex(t => t.StartsWith("AMIGA AUDIO FILTER", StringComparison.Ordinal));
            if (game is GameId.P1 or GameId.P3)
            {
                Assert.True(rows[at - 1].StartsWith("TITLE TUNE", StringComparison.Ordinal));
                Assert.EndsWith("ON", rows[at]);
            }
            else
                Assert.Equal(-1, at);
        }
    }

    [OriginalDataFact]
    public void InGame_ShowsOnlyGeneralAndTheRunningGame_RulesLocked()
    {
        var session = ClientSessions.Overworld();
        var settings = new ClientSettings();
        var o = Options(settings);
        o.Open(session);
        Assert.Equal(GameId.P1, o.Tab);
        Assert.Equal("> GENERAL  [I]", o.Lines()[1].Text);

        // The locked rows can be scrolled to and read but not changed: Down past the twelve presentation rows (the header is
        // skipped) lands on PRESET, and Right / Return there change nothing.
        o.HandleKeys([Keys.Down, Keys.Down, Keys.Down, Keys.Down, Keys.Down, Keys.Down, Keys.Down, Keys.Down, Keys.Down, Keys.Down,
            Keys.Down, Keys.Down, Keys.Down]);
        Assert.Equal(MenuLineStyle.Selected, o.Lines().Single(l => l.Text.Contains("PRESET")).Style);
        Assert.Contains(o.Lines(), l => l.Text.Contains("THIS SAVE'S RULES (LOCKED)")); // the header above it
        o.HandleKeys([Keys.Right, Keys.Enter]);
        Assert.EndsWith("ORIGINAL (MANUAL)", Line(o, "PRESET"));
        Assert.Null(settings.Game(GameId.P1).NewGameRules);
        Assert.Equal(OptionPreset.Manual, session.Options.Preset);
        Assert.Equal(UiText.S("options.detail.locked"), o.Detail()[^1].Text); // the description box says why
        o.HandleKeys([Keys.Down]);
        Assert.Equal(MenuLineStyle.Dim, o.Lines().Single(l => l.Text.Contains("PRESET")).Style);

        // The last locked rows scroll into view.
        for (var i = 0; i < 20 && !o.Lines().Any(l => l.Text.Contains("RENAME PARTY MEMBERS")); i++)
            o.HandleKeys([Keys.Down]);
        Assert.Contains(o.Lines(), l => l.Text.Contains("RENAME PARTY MEMBERS"));
    }

    [OriginalDataFact]
    public void GameMenu_OffersOptionsAndMainMenu_MainMenuAsksFirst()
    {
        var s = ClientSessions.Overworld();
        var d = new GameMenuDialog();
        d.Open();
        var lines = d.Lines(s, ClientSessions.Data);
        Assert.Contains(" Options", lines);
        Assert.DoesNotContain(lines, l => l.Contains("Diagonal moves"));

        d.HandleKeys([Keys.Up, Keys.Up, Keys.Enter], s); // Main menu
        Assert.True(d.IsOpen);
        Assert.Contains(GameMenuDialog.MainMenuQuestion, d.Lines(s, ClientSessions.Data));
        d.HandleKeys([Keys.Y], s);
        Assert.Equal(GameMenuChoice.MainMenu, d.TakeChoice());
    }

    [Fact]
    public void SharedTable_HoldsTheShellText_WithoutAGameTable()
    {
        var shared = StringTable.Load(GameDataRepository.SharedDataPath);
        Assert.Equal("shared", shared.Game);
        foreach (var id in new[] { "menu.title", "menu.game.p4", "options.help", "options.preset.custom", "module.p2.rock-throwing" })
            Assert.True(shared.Contains(id), id);
        Assert.Equal("phantasie3", UiText.For(GameId.P3).Game); // III extends I's table (T4)
        Assert.Equal("phantasie4", UiText.For(GameId.P4).Game); // IV has its own table (T6.3)
        Assert.Equal("phantasie2", UiText.For(GameId.P2).Game); // II extends I's table
    }

    [OriginalDataFact]
    public void Options_PageKeys_MoveAPageAtATime_AndWrapAtTheEnds()
    {
        var o = new OptionsScreen(new ClientSettings(), _ => []);
        o.Open();
        o.HandleKeys([Keys.Right]); // I's tab: far more rows than a page
        o.HandleKeys([Keys.PageDown]);
        var first = o.Cursor;
        Assert.InRange(first, 2, OptionsScreen.VisibleRows);
        o.HandleKeys([Keys.PageDown]);
        Assert.InRange(o.Cursor, first + 1, first + OptionsScreen.VisibleRows);
        o.HandleKeys([Keys.PageUp]);
        Assert.Equal(first, o.Cursor); // no header in I's first two pages: back where it was
        o.HandleKeys([Keys.PageUp]);
        Assert.Equal(0, o.Cursor); // the tab row

        o.HandleKeys([Keys.End]);
        var last = o.Cursor;
        o.HandleKeys([Keys.Down]);
        Assert.Equal(0, o.Cursor); // Down wraps from the last row...
        o.HandleKeys([Keys.PageUp]);
        Assert.Equal(last, o.Cursor); // ...and so does PgUp from the tab row
        o.HandleKeys([Keys.PageDown]);
        Assert.Equal(0, o.Cursor); // PgDn from the last row: the top
        o.HandleKeys([Keys.End, Keys.Home]);
        Assert.Equal(0, o.Cursor);
    }

    [OriginalDataFact]
    public void Options_DetailBox_DescribesTheRow_AndARulesPresets()
    {
        var o = new OptionsScreen(new ClientSettings(), _ => []);
        o.Open();
        Assert.Equal(UiText.S("options.tab.desc"), o.Detail().Single().Text);

        o.HandleKeys([Keys.Right]);
        for (var i = 0; i < 80 && !o.Lines().Any(l => l.Style == MenuLineStyle.Selected && l.Text.Contains("GOLD CAP")); i++)
            o.HandleKeys([Keys.Down]);
        var detail = o.Detail().Select(l => l.Text).ToList();
        Assert.Equal("GOLD CAP", detail[0]);
        Assert.Equal(UiText.S("option.p1.gold-cap.desc"), detail[1]);
        Assert.Contains("ORIGINAL (MANUAL): 65,000", detail);
        Assert.Contains("ORIGINAL (DOS): 65,535", detail);
        Assert.Contains("REMASTER: NO CAP", detail);
        Assert.Contains("Recorded as D-048 in DIFFERENCES.md.", detail);
    }

    [Fact]
    public void EveryOptionsRow_HasADescription()
    {
        var shared = StringTable.Load(GameDataRepository.SharedDataPath);
        var ids = new List<string>
        {
            "options.tab", "options.window_scale", "options.start_maximized", "options.graphics", "options.diagonal_moves",
            "options.status_lines", "options.continue_from_list", "options.inventory_screen", "options.time_lag_where",
            "options.time_lag", "options.sound", "options.colorblind", "options.preset", "options.title_screen",
        };
        foreach (var game in MainMenuScreen.Games)
            ids.AddRange(RuleOptionRegistry.For(game).Select(d => "option." + d.Id));
        ids.AddRange(FeatureModuleRegistry.All.Select(m => "module." + m.Id));
        foreach (var id in ids.Distinct())
            Assert.True(shared.Contains(id + ".desc"), id);
    }
}
