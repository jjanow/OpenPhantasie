using Microsoft.Xna.Framework.Input;
using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client.Tests;

/// <summary>
/// Phantasie IV's location-map screen (T6.3, <see cref="P4DungeonScreen"/>): keys answer the dungeon loop's prompts, the
/// box shows its text and the prompt's lines, and what is not built yet (fights, towns, the outdoors) is said on screen.
/// </summary>
public class P4DungeonScreenTests
{
    private static readonly GameOptions Manual = GameOptions.ForPreset(GameId.P4);
    private static readonly P4Tables T = GameTables.Load(GameId.P4).Phantasie4!.Tables;

    private static P4Party Party()
    {
        var p = new P4Party();
        foreach (var name in new[] { "ANN", "BOB" })
        {
            var m = new P4Member { Name = name, Level = 3, HpMax = 20, Hp = 20, Defence = 10 };
            Array.Fill(m.Skills, 50);
            p.Members.Add(m);
        }
        return p;
    }

    private static Phantasie4Campaign Shipped(int map, int x, int y)
    {
        var c = Phantasie4World.NewCampaign(Manual, Party(), new SeededRandomSource(1), new P4Location(P4Place.Outdoors, 1, 0, 0));
        c.EnterDungeon(map, x, y);
        return c;
    }

    /// <summary>An open 30 × 30 map whose load event is <paramref name="code"/> (no random rolls).</summary>
    private static Phantasie4Campaign Synthetic(params byte[] code) => Synthetic(false, code);

    private static Phantasie4Campaign Synthetic(bool battle, params byte[] code) => Synthetic(battle, 1, code);

    private static Phantasie4Campaign Synthetic(bool battle, int seed, params byte[] code)
    {
        var words = new ushort[31 * 31];
        Array.Fill(words, (ushort)((31 << 6) | (7 << 11)));
        var file = new P4DungeonFile(1, "test", words, [new P4EventTrigger(0x20, 0, 4, 0)], code);
        var c = new Phantasie4Campaign(Manual, T, Phantasie4World.LoadAtlas(), Party(), new SeededRandomSource(seed), _ => file,
            i => $"item{i}",
            new P4Location(P4Place.Outdoors, 1, 0, 0), battle ? Phantasie4World.LoadBattleData() : null);
        c.EnterDungeon(1, 10, 10);
        return c;
    }

    [Theory]
    [InlineData(Keys.NumPad1, 0)]
    [InlineData(Keys.Down, 1)]
    [InlineData(Keys.PageDown, 2)]
    [InlineData(Keys.Left, 3)]
    [InlineData(Keys.NumPad6, 5)]
    [InlineData(Keys.Home, 6)]
    [InlineData(Keys.Up, 7)]
    [InlineData(Keys.NumPad9, 8)]
    public void MoveKeys_AreTheNumericPadButtons(Keys key, int button) => Assert.Equal(button, P4DungeonScreen.ButtonOf(key));

    [OriginalDataFact]
    public void ArrowKeys_MoveThePartyOnAnOpenMap()
    {
        var s = new P4DungeonScreen(Synthetic(0x22, 0x00));
        Assert.Equal(P4DungeonPrompt.Action, s.Game.Dungeon.Prompt);
        s.HandleKeys([Keys.Right]);
        Assert.Equal((11, 10, 1), (s.Game.Dungeon.X, s.Game.Dungeon.Y, s.Game.Dungeon.Dir));
        s.HandleKeys([Keys.NumPad7]); // NW: west now, north on the next pass (no key needed)
        Assert.Equal((10, 9), (s.Game.Dungeon.X, s.Game.Dungeon.Y));
    }

    [OriginalDataFact]
    public void LeverRoom_WaitsThenAMenu_PickedWithTheCursor()
    {
        // O_002 (8,17): four boxes, then a four-line menu; line 2 opens the cells (story flag 11).
        var s = new P4DungeonScreen(Shipped(2, 8, 17));
        Assert.Equal(P4DungeonPrompt.Wait, s.Game.Dungeon.Prompt);
        Assert.NotEmpty(s.BoxLines());
        for (var i = 0; i < 4; i++)
            s.HandleKeys([Keys.Space]);
        Assert.Equal(P4DungeonPrompt.Menu, s.Game.Dungeon.Prompt);
        var menu = s.BoxLines().Where(l => l.Text.StartsWith('>') || l.Text.StartsWith(' ')).ToList();
        Assert.Equal(4, menu.Count);
        Assert.StartsWith(">1 ", menu[0].Text);
        s.HandleKeys([Keys.Down]);
        Assert.StartsWith(">2 ", s.BoxLines().Single(l => l.Style == MenuLineStyle.Selected).Text);
        s.HandleKeys([Keys.Enter]);
        Assert.Equal(1, s.Game.Party.StoryFlags[11]);
        Assert.Equal(UiText.S("p4.dungeon.help_wait"), s.Help);
    }

    [OriginalDataFact]
    public void Typing_FillsLetterAndNumber_ThenAFightWaitsForTheCombatField()
    {
        // NOENC; INPUTS; INPUTN; FIGHT 3; END
        var s = new P4DungeonScreen(Synthetic(0x22, 0x0C, 0x0E, 0x23, 0x03, 0x00));
        Assert.Equal(P4DungeonPrompt.Letters, s.Game.Dungeon.Prompt);
        foreach (var ch in "JILLIAN-THE-GREAT")
            s.OnTextInput(ch);
        Assert.Equal("JILLIAN-TH", s.Typed); // up to 10 characters
        s.HandleKeys([Keys.Back]);
        Assert.Contains(s.BoxLines(), l => l.Text == UiText.F("p4.dungeon.letters_x", "JILLIAN-T"));
        s.HandleKeys([Keys.Enter]);

        Assert.Equal(P4DungeonPrompt.Number, s.Game.Dungeon.Prompt);
        foreach (var ch in "12a3456")
            s.OnTextInput(ch);
        Assert.Equal("12345", s.Typed);
        s.HandleKeys([Keys.Enter]);

        Assert.Equal(P4DungeonPrompt.Fight, s.Game.Dungeon.Prompt);
        Assert.Contains(s.BoxLines(), l => l.Text == UiText.S("p4.dungeon.combat_not_built"));
        s.HandleKeys([Keys.Enter]); // preview: the fight is left unresolved
        Assert.Equal(P4DungeonPrompt.Action, s.Game.Dungeon.Prompt);
    }

    [OriginalDataFact]
    public void AWipedOutParty_AKeyOpensSelectGame_EndGameOrTitle()
    {
        // s158, _deadmsg: "The party has been wiped out.", any key (_mgetc), then _acgamend(1): E / T, no SPACE back.
        var s = new P4DungeonScreen(Synthetic(0x22, 0x1A, 255, 0x00)); // DAMAGE 255 on arrival
        Assert.True(s.Game.PartyLost);
        Assert.Equal([GameText.S("p4x.0x44097"), GameText.S("p4x.0x440b0")], s.BoxLines().Select(l => l.Text).TakeLast(2));
        Assert.Equal(P4ScreenRequest.Load, s.HandleKeys([Keys.F9])); // F9 still loads
        Assert.Null(s.SystemPage);
        Assert.Null(s.HandleKeys([Keys.A]));
        Assert.Equal(P4SystemPage.SelectGameFinal, s.SystemPage);
        s.HandleKeys([Keys.Space]);
        Assert.Equal(P4SystemPage.SelectGameFinal, s.SystemPage);
        s.HandleKeys([Keys.E]);
        Assert.Equal(P4SystemPage.EndGame, s.SystemPage);
        s.HandleKeys([Keys.C]);
        Assert.Equal(P4SystemPage.SelectGameFinal, s.SystemPage);
        s.HandleKeys([Keys.E]);
        Assert.Equal(P4ScreenRequest.MainMenu, s.HandleKeys([Keys.O]));

        var t = new P4DungeonScreen(Synthetic(0x22, 0x1A, 255, 0x00));
        t.HandleKeys([Keys.Space, Keys.T]);
        Assert.Equal(P4ScreenRequest.Title, t.HandleKeys([Keys.Y]));
    }

    [OriginalDataFact]
    public void AFight_RunsOnTheBattleField_AndKeysPlayIt()
    {
        // NOENC; FIGHT 1 (Undead Goblin); END — with the shipped battle data the 7 x 7 battle starts.
        var s = new P4DungeonScreen(Synthetic(true, 0x22, 0x23, 0x01, 0x00));
        var b = Assert.IsType<P4Battle>(s.Game.Battle);
        Assert.Equal(P4BattlePrompt.Options, b.Prompt);
        Assert.Contains(s.BoxLines(), l => l.Text.Contains(UiText.S("p4.battle.option.fight"), StringComparison.Ordinal));
        Assert.Null(s.HandleKeys([Keys.F5])); // no saving in a fight
        Assert.Equal(UiText.S("p4.battle.no_save"), s.Note);
        s.HandleKeys([Keys.F]);
        Assert.Equal(P4BattlePrompt.Order, b.Prompt);
        var first = b.Current!;
        var at = first.Square;
        s.HandleKeys([Keys.Up]);
        Assert.Equal(at - P4Battle.Size, first.Square);
        s.HandleKeys([Keys.P]); // parry
        for (var i = 0; i < 400 && s.Game.Battle is not null; i++)
        {
            var key = s.Game.Battle.Prompt switch
            {
                P4BattlePrompt.Options => Keys.F,
                P4BattlePrompt.Order => Keys.P,
                P4BattlePrompt.Treasure => Keys.N,
                _ => Keys.Enter,
            };
            s.HandleKeys([key]);
        }
        Assert.Null(s.Game.Battle);
        Assert.NotEqual(P4DungeonPrompt.Fight, s.Game.Dungeon.Prompt);
    }

    [OriginalDataFact]
    public void CombatSpellList_OpensOnPage0_TurnsPages_PicksAndQuits()
    {
        // s152 (_getmagic(member, 0)): C opens the list on page 0; after the page's spells come the five pages and Quit.
        var s = new P4DungeonScreen(Synthetic(true, 0x22, 0x23, 0x01, 0x00));
        var b = Assert.IsType<P4Battle>(s.Game.Battle);
        s.HandleKeys([Keys.F]);
        var m = b.Current!;
        m.Member.Spells[1] = 1; // Healing 2 (page 0)
        m.Member.Spells[25] = 1; // *Arrow Flame 1 (26, page 4)
        m.Member.Mp = 10;
        s.HandleKeys([Keys.C]);
        var heads = b.SpellPageHeads.Select(h => P4DungeonScreen.HeadText(GameText.S(h))).ToList();
        Assert.Equal("Non-combat", heads[0]); // the original's "1 :" dropped: the column has its own digits
        Assert.Contains(s.BoxLines(), l => l.Text.EndsWith(heads[0], StringComparison.Ordinal));
        Assert.Contains(s.BoxLines(), l => l.Text.TrimStart('>', ' ').StartsWith("7 ", StringComparison.Ordinal) && l.Text.EndsWith(heads[5],
            StringComparison.Ordinal)); // one spell, five pages, then Quit on 7
        s.HandleKeys([Keys.D7]); // Quit: back to the orders
        Assert.Same(m, b.Current);
        Assert.DoesNotContain(s.BoxLines(), l => l.Text.EndsWith(heads[5], StringComparison.Ordinal));
        s.HandleKeys([Keys.C]);
        s.HandleKeys([Keys.D6]); // head 5 = page 4
        Assert.Contains(s.BoxLines(), l => l.Text.TrimStart('>', ' ').StartsWith("2 ", StringComparison.Ordinal)
            && l.Text.EndsWith(heads[0], StringComparison.Ordinal)); // page 4 holds one castable spell too
        s.HandleKeys([Keys.D1]); // *Arrow Flame 1: no target
        Assert.Equal((P4ActionKind.Cast, 26), (m.Order.Kind, m.Order.Spell));
    }

    [OriginalDataFact]
    public void CombatSpellList_TakesTheStripOnlyWhileOpen()
    {
        // s160 (user s159): the list opens right of the field at the panel's place and the column gives way to it while it
        // is open; Quit, Backspace and a picked spell close it and the column comes back. No panel strip in a fight.
        var s = new P4DungeonScreen(Synthetic(true, 0x22, 0x23, 0x01, 0x00));
        var b = Assert.IsType<P4Battle>(s.Game.Battle);
        s.HandleKeys([Keys.F]);
        var m = b.Current!;
        m.Member.Spells[25] = 1; // *Arrow Flame 1 (page 4, no target)
        m.Member.Mp = 10;
        Assert.False(s.FightSpellListOpen);
        Assert.False(s.PanelPlace);
        s.HandleKeys([Keys.C]);
        Assert.True(s.FightSpellListOpen);
        Assert.Null(s.PanelName);
        s.HandleKeys([Keys.Back]);
        Assert.False(s.FightSpellListOpen);
        s.HandleKeys([Keys.C]);
        s.HandleKeys([Keys.D5]); // head 5 = page 4: the list stays open on its new page
        Assert.True(s.FightSpellListOpen);
        s.HandleKeys([Keys.D1]); // *Arrow Flame 1: the order is given, the list closes
        Assert.Equal((P4ActionKind.Cast, 26), (m.Order.Kind, m.Order.Spell));
        Assert.False(s.FightSpellListOpen);
    }

    [OriginalDataFact]
    public void CombatSpellList_OpensWithNothingCastable_AndQuits()
    {
        // _getmagic(member, 0) opens even when nothing can be cast: the letters only, the six head lines; 6 quits.
        var s = new P4DungeonScreen(Synthetic(true, 0x22, 0x23, 0x01, 0x00));
        var b = Assert.IsType<P4Battle>(s.Game.Battle);
        s.HandleKeys([Keys.F]);
        var m = b.Current!;
        Array.Fill(m.Member.Spells, 0);
        Assert.Empty(b.CastableSpells());
        s.HandleKeys([Keys.C]);
        var quit = P4DungeonScreen.HeadText(GameText.S(b.SpellPageHeads[5]));
        Assert.Contains(s.BoxLines(), l => l.Text.TrimStart('>', ' ').StartsWith("6 ", StringComparison.Ordinal)
            && l.Text.EndsWith(quit, StringComparison.Ordinal));
        s.HandleKeys([Keys.D6]); // Quit
        Assert.Same(m, b.Current);
        Assert.DoesNotContain(s.BoxLines(), l => l.Text.EndsWith(quit, StringComparison.Ordinal));
    }

    [OriginalDataFact]
    public void FoundItem_ShowsTheFinditPanel_OnlyAtThatQuestion()
    {
        // s140: in a fight the Command panel shows only at _findit's question ("Found Item(s)", O pick up / C leave =
        // Y / N); the other battle prompts have none. s142 (user, option a): it lies over the field, no strip.
        var seen = false;
        for (var seed = 1; seed <= 30 && !seen; seed++)
        {
            var s = new P4DungeonScreen(Synthetic(true, seed, 0x22, 0x23, 0x01, 0x00));
            Assert.Equal(P4BattlePrompt.Options, s.Game.Battle!.Prompt);
            Assert.Null(s.PanelName);
            Assert.False(s.PanelPlace);
            Assert.False(s.PanelOverField);
            for (var i = 0; i < 400 && s.Game.Battle is { } b; i++)
            {
                if (b.Prompt == P4BattlePrompt.Treasure)
                {
                    Assert.Equal(("_findit", false, true), (s.PanelName, s.PanelPlace, s.PanelOverField));
                    // s141: the question's lines (shown in _msg's window over the field) start with the item found.
                    Assert.NotEmpty(b.TreasureLines);
                    Assert.Equal(b.TreasureLines, b.Log.TakeLast(b.TreasureLines.Count));
                    Assert.Equal(b.TreasureLines, s.BoxLines().Where(l => l.Style == MenuLineStyle.Dim && l.Text.Length > 0).Select(l => l.Text));
                    seen = true;
                }
                else
                {
                    Assert.False(s.PanelPlace);
                    Assert.Empty(b.TreasureLines);
                }
                s.HandleKeys([b.Prompt switch
                {
                    P4BattlePrompt.Options => Keys.F,
                    P4BattlePrompt.Order => Keys.Q,
                    P4BattlePrompt.Treasure => Keys.N,
                    _ => Keys.Enter,
                }]);
            }
        }
        Assert.True(seen);
    }

    [OriginalDataFact]
    public void AutoCombat_Q_FightsOnWithAnyKey_AndQInOrdersIsAutoMove()
    {
        var s = new P4DungeonScreen(Synthetic(true, 0x22, 0x23, 0x01, 0x00));
        var b = Assert.IsType<P4Battle>(s.Game.Battle);
        Assert.Contains(s.BoxLines(), l => l.Text.Contains(UiText.S("p4.battle.option.autocombat"), StringComparison.Ordinal));
        s.HandleKeys([Keys.F]);
        Assert.Equal(P4BattlePrompt.Order, b.Prompt);
        s.HandleKeys([Keys.Q]); // auto move: every member gets automatic orders and the round runs
        Assert.NotEqual(P4BattlePrompt.Order, b.Prompt);
        if (b.Prompt == P4BattlePrompt.Options)
        {
            s.HandleKeys([Keys.Q]);
            Assert.True(b.AutoCombat || b.Done || b.Prompt != P4BattlePrompt.Options);
        }
        for (var i = 0; i < 400 && s.Game.Battle is not null; i++)
            s.HandleKeys([s.Game.Battle.Prompt == P4BattlePrompt.Treasure ? Keys.N : Keys.Enter]);
        Assert.True(s.Game.Battle is null || s.Game.Battle.Round >= 100);
    }

    [OriginalDataFact]
    public void LeavingTheMap_GoesOnOutdoors_WithTheOutdoorBox()
    {
        // O_001's map edge: TELEPORT 1, 9, 10, 10 (the Town Hall's south wall is open below x = 15).
        var s = new P4DungeonScreen(Shipped(1, 16, 27));
        var keys = new Queue<Keys>([Keys.Left]);
        for (var i = 0; i < 40 && s.Game.Place == P4Place.Dungeon; i++)
            s.HandleKeys([s.Game.Dungeon.Prompt == P4DungeonPrompt.Action ? keys.Count > 0 ? keys.Dequeue() : Keys.Down
                : s.Game.Dungeon.Prompt == P4DungeonPrompt.Wait ? Keys.Space : Keys.Enter]);
        Assert.Equal(P4Place.Outdoors, s.Game.Place);
        Assert.Equal((9, 10, 10), (s.Game.Outdoor.Map, s.Game.Outdoor.X, s.Game.Outdoor.Y));
        var lines = s.BoxLines().Select(l => l.Text).ToList();
        Assert.StartsWith(UiText.F("p4.outdoor.title_x_y_z_w", 9, 10, 10, s.Game.Party.DaysOut), lines[0]);
        Assert.Equal(P4ScreenRequest.Save, s.HandleKeys([Keys.F5]));
        Assert.Null(s.HandleKeys([Keys.Escape])); // s180: asked first
        Assert.Equal(P4ScreenRequest.MainMenu, s.HandleKeys([Keys.Y]));
    }

    [Theory]
    [InlineData(Keys.NumPad1, 0)]
    [InlineData(Keys.Down, 1)]
    [InlineData(Keys.NumPad3, 2)]
    [InlineData(Keys.Left, 3)]
    [InlineData(Keys.Right, 4)]
    [InlineData(Keys.Home, 5)]
    [InlineData(Keys.Up, 6)]
    [InlineData(Keys.PageUp, 7)]
    public void OutdoorDirections_AreThePadWithoutTheMiddle(Keys key, int dir) =>
        Assert.Equal(dir, P4DungeonScreen.DirectionOf(key));

    [OriginalDataFact]
    public void TownPreviewSwitch_TakesATown()
    {
        Assert.Equal(5, P4Preview.ParseTownArgs([P4Preview.TownSwitch, "5"], out var none));
        Assert.Null(none);
        Assert.Null(P4Preview.ParseTownArgs([P4Preview.TownSwitch, "22"], out var error));
        Assert.NotNull(error);
        var game = P4Preview.StartInTown(1, Manual);
        Assert.Equal(P4Place.Town, game.Place);
        Assert.Equal(1, game.Town.Town);
    }

    [OriginalDataFact]
    public void Town_MenuListsTheDoors_AndLeaveGoesOutdoors()
    {
        var c = Phantasie4World.NewCampaign(Manual, Party(), new SeededRandomSource(1), new P4Location(P4Place.Town, 2, 0, 0));
        c.Start();
        var s = new P4DungeonScreen(c);
        Assert.Equal(P4TownPrompt.Menu, c.Town.Prompt);
        var lines = s.BoxLines().Select(l => l.Text).ToList();
        Assert.Equal("Pendleton", lines[0]);
        Assert.Contains(lines, l => l.Contains(P4DungeonScreen.BuildingName(P4Building.Shipwright)));
        s.HandleKeys([Keys.D7]); // 1 Guild … 6 Shipwright, 7 Leave
        Assert.Equal(P4Place.Outdoors, c.Place);
        Assert.Equal((8, 18, 6), (c.Outdoor.Map, c.Outdoor.X, c.Outdoor.Y));
    }

    [OriginalDataFact]
    public void PartyLines_ShowHpMpAndCondition()
    {
        var s = new P4DungeonScreen(Synthetic(0x22, 0x00));
        s.Game.Party.Members[1].Condition = P4Member.Dead;
        var lines = s.PartyLines();
        Assert.Equal(2, lines.Count);
        Assert.Contains("ANN", lines[0]);
        Assert.Contains("20/20", lines[0]);
        Assert.EndsWith(UiText.S("p4.dungeon.dead"), lines[1]);
    }

    [OriginalDataFact]
    public void Preview_StartsIvsCreatedPartyAtAnEntrance()
    {
        Assert.Null(P4Preview.ParseArgs(["--test-party"], out var none));
        Assert.Null(none);
        Assert.Equal(4, P4Preview.ParseArgs([P4Preview.Switch, "4"], out _));
        Assert.Null(P4Preview.ParseArgs([P4Preview.Switch, "2"], out var error)); // reached from inside, not the outdoors
        Assert.NotNull(error);

        var game = P4Preview.Start(4, Manual);
        Assert.Equal(P4Preview.Members.Count, game.Party.Members.Count);
        Assert.All(game.Party.Members, m => Assert.True(m.HpMax > 0 && m.Hp == m.HpMax));
        Assert.Equal(P4Place.Dungeon, game.Location.Place);
        Assert.Equal(4, game.Location.Map);
    }

    [OriginalDataFact]
    public void Preview_TakesASquareAfterTheMap()
    {
        Assert.Equal((6, 6), P4Preview.ParseSquare([P4Preview.Switch, "20", "6,6"]));
        Assert.Null(P4Preview.ParseSquare([P4Preview.Switch, "20"]));
        Assert.Null(P4Preview.ParseSquare([P4Preview.Switch, "20", "30,1"]));
        Assert.Null(P4Preview.ParseSquare([P4Preview.Switch, "20", "--test-party"]));
        Assert.Equal(20, P4Preview.ParseArgs([P4Preview.Switch, "20", "6,6"], out _));

        var game = P4Preview.Start(20, Manual, at: (6, 6));
        Assert.Equal((P4Place.Dungeon, 20, 6, 6), (game.Location.Place, game.Location.Map, game.Location.X, game.Location.Y));
    }

    [Fact]
    public void FindItMessageSpan_ClearsThePanelOverTheField()
    {
        // s142: no panel = the original's (32, 272); a panel from x 176 (a 160-wide window on the 336 field) pushes the
        // window left to x 8, ending 4 short of the panel.
        Assert.Equal((P4DungeonScreen.MessageX, P4DungeonScreen.MessageWidth), P4DungeonScreen.FindItMessageSpan(null));
        Assert.Equal((P4DungeonScreen.MessageX, P4DungeonScreen.MessageWidth), P4DungeonScreen.FindItMessageSpan(330));
        Assert.Equal((8, 164), P4DungeonScreen.FindItMessageSpan(176));
    }
}
