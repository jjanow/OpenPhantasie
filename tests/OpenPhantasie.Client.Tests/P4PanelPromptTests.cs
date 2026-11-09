using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client.Tests;

/// <summary>
/// The panels' smaller pieces (s151; FORMAT.md 6.9 "Dungeon waits, typed prompts, the keypad, the shipwright's window"):
/// the pack's extras from P4.X, the hover look's colours, the inert panels at the location map's waits and the Bank's
/// amount, the keypad's keys, and the shipwright window's rows.
/// </summary>
public partial class P4PanelPromptTests
{
    private static readonly GameOptions Code = GameOptions.ForPreset(GameId.P4, OptionPreset.Dos);

    private static PanelExtras Extras() =>
        GraphicsPackCatalog.Load(GameDataRepository.DefaultPathFor(GameId.P4)).Find("x68000")!.CommandPanels!.Extras;

    [OriginalDataFact]
    public void Pack_HoldsTheTitleHoverWaitKeypadAndShipwright()
    {
        var x = Extras();
        Assert.Equal(new PanelTitleStyle(0, 0, -18), x.Title); // _wttl: font 0, colour 0, y -18
        Assert.Equal(new PanelHover(15, 5), x.Hover);          // _xicon ^ 0xFFFF, _xbox(5, ...)
        var bar = x.EventWait!;
        Assert.Equal((new PanelRect(0, 80, 304, 1), 0), (bar.Line, bar.LineColour));
        Assert.Equal(("p4x.0x43796", 152, 82), (bar.Label, bar.LabelX, bar.LabelY));
        Assert.Equal((new PanelRect(0, 81, 304, 15), "Space"), (bar.Button, bar.Key));
        Assert.Equal((new PanelRect(0, 80, 304, 16), 14, 16), (bar.Band, bar.BandColour, bar.PromptX));
        Assert.Equal(18, P4DungeonScreen.EventSlotRows(bar));
        var pad = x.Keypad!;
        Assert.Equal((34, 40, 224, 20, 16, 5), (pad.Window, pad.X, pad.Y, pad.Step, pad.Size, pad.Digits));
        Assert.Equal("789456123B0R", string.Concat(pad.Keys.Select(k => k.Text)));
        Assert.Equal([7, 8, 9, 4, 5, 6, 1, 2, 3, 11, 0, 10], pad.Keys.Select(k => k.Value));
        Assert.Equal((Keys.Back, Keys.Enter), (P4DungeonScreen.KeypadKey(pad.Keys[9]), P4DungeonScreen.KeypadKey(pad.Keys[11])));
        Assert.Equal((8, 16, 0), (pad.CursorWidth, pad.CursorHeight, pad.CursorColour));
        var ship = x.Shipwright!;
        Assert.Equal([(16, 236), (32, 252), (32, 268)], ship.Lines);
        Assert.Equal((156, 300, 68, 336, 8), (ship.AnswerX, ship.AnswerY, ship.Gold.X, ship.Gold.Y, ship.Gold.Digits));
        Assert.Equal((220, 14, 0), (ship.PictureHeight, ship.BackColour, ship.TextColour));
    }

    [OriginalDataFact]
    public void SkillUpColumns_KeepTheOriginals_PushRightForLongNames()
    {
        var rows = Extras().SkillUp!.Rows;
        Assert.Equal((112, 192), P4DungeonScreen.SkillUpColumns(rows, 60)); // 40 + 60 + 4 = 104: x 112 kept
        Assert.Equal((124, 204), P4DungeonScreen.SkillUpColumns(rows, 80)); // past 112: moved, after keeps its 80
    }

    [OriginalDataFact]
    public void SkillUpHeader_PutsTheShownTokensOnTheOriginalsPlaces()
    {
        var sk = Extras().SkillUp!;
        static int Width(string t) => 8 * t.Length; // a fixed 8-pixel font for the test
        // the header row (#, skill, now, ->, after training): # at 16, 技能 at 16 + 8 + 32 = 56, 現在 at 56 + 32 + 32 = 120, -> at 168, 訓練後 at 200.
        Assert.Equal([(16, "#"), (56, "Skill"), (120, "Now"), (168, "->"), (200, "After")],
            P4DungeonScreen.SkillUpHeader(sk.Header.Text, "#  Skill    Now -> After", 16, 112, 0, Width));
        // The now / after columns moved right by 12: the tokens over them go along; one too close keeps a space.
        Assert.Equal([(16, "#"), (56, "Skill"), (132, "Now"), (180, "->"), (212, "After")],
            P4DungeonScreen.SkillUpHeader(sk.Header.Text, "#  Skill    Now -> After", 16, 112, 12, Width));
        Assert.Equal([(16, "#"), (56, "Abilities!!"), (152, "Now"), (184, "->"), (208, "After")],
            P4DungeonScreen.SkillUpHeader(sk.Header.Text, "# Abilities!! Now -> After", 16, 112, 0, Width));
        Assert.Equal([(16, "Skill Now After")], // another token count: one string, as the original draws it
            P4DungeonScreen.SkillUpHeader(sk.Header.Text, "Skill Now After", 16, 112, 0, Width));
    }

    [OriginalDataFact]
    public void Pack_HoldsTheTrainingHallWindow()
    {
        var hall = Extras().TrainingHall!;
        Assert.Equal((new PanelRect(160, 32, 312, 352), 14, 0, 16, (16, 16)),
            (hall.Window, hall.BackColour, hall.TextColour, hall.LineStep, hall.Name));
        Assert.Equal(["cost", "needs_experience", "nothing_more", "poor_condition"], hall.Notes.Keys.Order());
        var cost = hall.Notes["cost"];
        Assert.Equal(["p4x.0x3fd5b", "", "p4x.0x3fd6e", "p4x.0x3fd8a"], cost.Select(s => s.Label));
        Assert.Equal(new TrainingHallSegment(16, 32, "", "", true, 1, 8), cost[1]); // _font(1, -1, 8, -1), _wdoutl
        Assert.Equal((16, 48), (cost[3].X!.Value, cost[3].Y!.Value));
        Assert.Equal((1, 11), (hall.Notes["needs_experience"][1].Font, hall.Notes["needs_experience"][1].Colour!.Value));
        Assert.Equal((32, 32), (hall.Notes["poor_condition"][1].X!.Value, hall.Notes["poor_condition"][1].Y!.Value));
        Assert.Equal(("p4x.0x3fd97", (int?)null), (hall.ShortOfGold.Label, hall.ShortOfGold.Colour)); // s158: the ink, 0
        var sk = Extras().SkillUp!;
        Assert.Equal(84, P4DungeonScreen.SkillUpTop(hall, sk, 64)); // window 32 at y 136 under the bar: 136 − 32 − 20
        Assert.Equal(96, P4DungeonScreen.SkillUpTop(hall, sk, 96)); // wrapped rows push it down
    }

    [OriginalDataFact]
    public void TitledWindows_HaveTheirTitle_AndKeepTheKeyHelpUnderThem()
    {
        // s158: _trang's _wopen(32, 160, 32, 312, 352, 1, "訓練場") and the shipwright's: the bar and frame round the window.
        var x = Extras();
        Assert.Equal(("p4x.0x3fd0c", "訓練場"), (x.TrainingHall!.Title, x.TrainingHall.TitleText));
        Assert.Equal("p4x.0x400d4", x.Shipwright!.Title);
        var (w, h) = P4DungeonScreen.TitledArea(312, 352);
        Assert.Equal((315, 375), (w, h)); // frame + shadow columns; the 21-row bar, frame and shadow rows
        // The frame's top row at layout row 0, its 187.5 rows counted as 187: the one-row key help still goes under the
        // picture and the strip (s157), not into the column.
        var picture = new Rectangle(4, 0, (int)Math.Ceiling(w / 2f), (int)Math.Floor(h / 2f));
        var layout = P4DungeonScreen.LayOutColumn(picture, picture.Right + 4, 232, 178, _ => 1, _ => 4);
        Assert.Equal((4, 189), (layout.NoteX, layout.NoteTop));
        Assert.True(layout.NoteTop >= h / 2f + 1); // a gap under the shadow row
    }

    [OriginalDataFact]
    public void WipedOutParty_BlacksTheScreen_ThenTheMessageWindow()
    {
        // s162, _deadmsg (0x1E308): _xblack (768 x 512, colour 0), window 32 at (100, 168) 440 x 64 filled black with two
        // white outlines, the two lines centred on 220 at y 16 / 32 in bold white; _xblack again before _acgamend(1).
        var d = Extras().DeadMessage!;
        Assert.Equal((0, new PanelRect(100, 168, 440, 64), 0, 15), (d.ScreenColour, d.Window, d.Fill, d.FrameColour));
        Assert.Equal([new PanelRect(2, 2, 436, 60), new PanelRect(3, 3, 434, 58)], d.Frames);
        Assert.Equal((0, X68kFontStyle.Bold, 15), (d.Font, d.Style, d.TextColour));
        Assert.Equal([("p4x.0x44097", 220, 16), ("p4x.0x440b0", 220, 32)], d.Lines.Select(l => (l.Label, l.X!.Value, l.Y!.Value)));
    }

    [OriginalDataFact]
    public void TownGate_AgedMembersSitOnTheirOwnRows_TillTheWaitEnds()
    {
        // s162, _townent: member m's line at (8, 8 + 16 m) in the Town Gate window, the window kept while the wait is up.
        var game = new[] { Code, GameOptions.ForPreset(GameId.P4, OptionPreset.Manual) }
            .SelectMany(o => Enumerable.Range(1, 40).Select(seed => P4Preview.StartInTown(1, o, seed)))
            .First(g => g.Town.GateRows is not null); // the preview party ages on arrival with some rolls
        var rows = game.Town.GateRows!;
        Assert.NotEmpty(rows);
        Assert.Equal(P4TownPrompt.Wait, game.Town.Prompt);
        Assert.All(rows, r => Assert.Equal(game.Town.Party.Members[r.Member].Name, r.Name));
        var gate = Extras().TownGate!;
        Assert.Equal((8, 8, 16, "p4x.0x3fa6f", "p4x.0x3fa89"), (gate.X, gate.Y, gate.Step, gate.GrownOlder.Label, gate.Died.Label));
        static int Width(string t, int font, int style) => 8 * t.Length;
        var text = P4DungeonScreen.TownGateText(gate, [new P4GateRow(1, "ODO", "p4x.0x3fa89"), new P4GateRow(3, "PIP", "p4x.0x3fa6f")],
            304, s => s.Label == "p4x.0x3fa89" ? " died" : " older", Width);
        Assert.Equal([(8, 24, "ODO died"), (8, 56, "PIP older")], text.Select(r => (r.X, r.Y, r.Text)));
        game.Town.Continue();
        Assert.Null(game.Town.GateRows);
    }

    [Fact]
    public void X68kStyles_BoldAndShadow_AsWfnt()
    {
        // FORMAT.md 6.12: bold = row |= row >> 1 (one column wider, advance + 1); shadow = a hollow outline one pixel
        // right and down with a drop-shadow pixel (two rows more, advance unchanged).
        var dot = new GlyphDefinition(1, 4, [1u]);
        var bold = X68kFontStyle.BoldGlyph(dot);
        Assert.Equal((2, 5, 3u), (bold.Width, bold.Advance, bold.Rows[0]));
        var shadow = X68kFontStyle.ShadowGlyph(dot);
        Assert.Equal((4, 4, 3), (shadow.Width, shadow.Advance, shadow.Rows.Count));
        // (x + 1, y) | (x, y + 1), (x + 2, y + 1) | (x + 1, y + 2), (x + 3, y + 2); (x + 1, y + 1) clear.
        Assert.Equal([0b0100u, 0b1010u, 0b0101u], shadow.Rows);
    }

    [OriginalDataFact]
    public void FinalScreen_AsksToSave_ThenOffersEndGameOrTitle()
    {
        // s158, _endstory: the save question after the final lines' SPACE wait; YES saves (the shell's save), then
        // _acgamend(1) (E / T only), each through _acend's Okay / Cansel.
        var game = P4Preview.StartEnding(0, Code);
        var s = new P4DungeonScreen(game);
        while (game.Town.Prompt == P4TownPrompt.Wait)
            s.HandleKeys([Keys.Space]);
        Assert.Equal((P4TownPrompt.Ending, P4EndingStep.Question, "_confirm"),
            (game.Town.Prompt, game.Town.EndingStep, s.PanelName));
        Assert.True(game.CanSave);
        Assert.Null(s.HandleKeys([Keys.Space])); // only the answer's keys
        Assert.Equal(P4ScreenRequest.Save, s.HandleKeys([Keys.Y]));
        Assert.Equal((P4EndingStep.Answered, "_acgamend.1"), (game.Town.EndingStep, s.PanelName));
        Assert.Equal(2, game.Party.Sounds.Since(0).Count(m => m == P4Sounds.EndingFinal));
        s.HandleKeys([Keys.Space]); // no SPACE in _acgamend(1)
        Assert.Equal("_acgamend.1", s.PanelName);
        Assert.Contains(s.BoxLines(), l => l.Text == GameText.S("p4x.0x4139f")); // the final screen stays up
        s.HandleKeys([Keys.E]);
        Assert.Equal("_acend", s.PanelName);
        s.HandleKeys([Keys.C]);
        Assert.Equal("_acgamend.1", s.PanelName);
        s.HandleKeys([Keys.E]);
        Assert.Equal(P4ScreenRequest.MainMenu, s.HandleKeys([Keys.O]));

        var no = P4Preview.StartEnding(0, Code);
        var t = new P4DungeonScreen(no);
        while (no.Town.Prompt == P4TownPrompt.Wait)
            t.HandleKeys([Keys.Space]);
        Assert.Null(t.HandleKeys([Keys.N])); // NO: no save, the second beep all the same
        Assert.Equal(2, no.Party.Sounds.Since(0).Count(m => m == P4Sounds.EndingFinal));
        t.HandleKeys([Keys.T]);
        Assert.Equal(P4ScreenRequest.Title, t.HandleKeys([Keys.Y]));
    }

    [OriginalDataFact]
    public void MysticSkip_OnlyEndsARunningReading_WithSpaceOrReturn()
    {
        // QoL MYSTIC SKIP: Space / Return (plus the shell's click and pad A); nothing to skip without a reading running
        // (no pack here: P4MysticReading is not started), so the key is not swallowed.
        Assert.True(P4DungeonScreen.IsMysticSkipKey(Keys.Space) && P4DungeonScreen.IsMysticSkipKey(Keys.Enter));
        Assert.False(P4DungeonScreen.IsMysticSkipKey(Keys.Escape));
        var s = new P4DungeonScreen(P4Preview.StartInTown(1, Code)) { MysticSkippable = true };
        Assert.False(s.MysticBusy);
        Assert.False(s.SkipMystic());
        s.MysticSkippable = false;
        Assert.False(s.SkipMystic());
    }

    [OriginalDataFact]
    public void ShareOut_LightsRowZeroOnly()
    {
        // s158, _dvitmlst: _wxbox(win, 15, 16, 32, 280, 32) over row 0 (the item being placed); the queue's rows are plain.
        var game = P4Preview.StartInTown(1, Code);
        var s = new P4DungeonScreen(game);
        s.HandleKeys([Keys.D]);
        Assert.Equal("_distribute", s.PanelName);
        var icons = s.BoxLines().Where(l => l.Icon is not null).ToList();
        Assert.True(icons.Count > 1);
        Assert.True(icons[0].Lit);
        Assert.All(icons.Skip(1), l => Assert.False(l.Lit));
        Assert.Equal(15, P4DungeonScreen.ShareOutXor);
    }

    [OriginalDataFact]
    public void TrainingHallText_FollowsTheOriginalsLines_WrapsLongEnglish()
    {
        var hall = Extras().TrainingHall!;
        static int Width(string t, int font) => 8 * t.Length;
        static string Text(TrainingHallSegment s) => s.Label switch
        {
            "p4x.0x3fd5b" => ": the training will cost", "p4x.0x3fd6e" => " gold.", "p4x.0x3fd8a" => "Is that all right?",
            "p4x.0x3fd97" => " You do not have enough gold.", _ => s.Text,
        };
        var rows = P4DungeonScreen.TrainingHallText(hall, new P4TrainingWindow("Brand", P4TrainingNote.Cost, 51, false),
            296, Text, Width);
        Assert.Equal([(16, 16, "Brand", 0, 0), (56, 16, ": the training will cost", 0, 0), (16, 32, "51", 1, 8),
            (32, 32, " gold.", 0, 0), (16, 48, "Is that all right?", 0, 0)], rows);
        // Short of gold: the line goes on after the question; past 296 it wraps at a space to the line's start.
        rows = P4DungeonScreen.TrainingHallText(hall, new P4TrainingWindow("Brand", P4TrainingNote.Cost, 51, true),
            296, Text, Width);
        Assert.Equal([(160, 48, " You do not have", 0, 0), (16, 64, "enough gold.", 0, 0)], rows[^2..]);
        // Poor condition: the second line at (32, 32).
        rows = P4DungeonScreen.TrainingHallText(hall, new P4TrainingWindow("Odo", P4TrainingNote.PoorCondition, 0, false),
            296, s => s.Label == "p4x.0x3fdae" ? " is in poor health," : "so cannot be trained.", Width);
        Assert.Equal([(16, 16, "Odo", 0, 0), (40, 16, " is in poor health,", 0, 0), (32, 32, "so cannot be trained.", 0, 0)],
            rows);
    }

    [Fact]
    public void PaletteXor_FlipsTheIndexOfOpaquePaletteColours()
    {
        Color[] palette = [.. Enumerable.Range(0, 16).Select(i => new Color(i * 10, i, 255 - i))];
        Color[] pixels = [palette[0], palette[3], Color.Transparent, new Color(1, 2, 3)];
        var lit = P4DungeonScreen.PaletteXor(pixels, palette, 15);
        Assert.Equal([palette[15], palette[12], Color.Transparent, new Color(1, 2, 3)], lit);
    }

    [Fact]
    public void PaddedAmount_PadsEachMissingDigitWithTwoSpaces() =>
        Assert.Equal(new string(' ', 8) + "1500", P4DungeonScreen.PaddedAmount(1500, 8));

    [OriginalDataFact]
    public void ShipwrightRows_FollowTheOriginalsLines_WrapsGrowTheWindow()
    {
        var ship = Extras().Shipwright!;
        // A line that does not fit wraps onto the next row; rows past the original's three push the box and the gold down.
        IEnumerable<string> Fits(string line, int x) => line.Length > 10 ? [line[..10], line[10..]] : [line];
        var (rows, answer) = P4DungeonScreen.ShipwrightText(ship, ["", "offer. If you", "the warning", "buy?", "Thanks"],
            "Thanks", "Come again", Fits);
        Assert.Equal("Thanks", answer);
        Assert.Equal([(16, 236, "offer. If "), (32, 252, "you"), (32, 268, "the warnin"), (32, 284, "g"), (32, 300, "buy?")],
            rows);
        Assert.Equal(32, P4DungeonScreen.ShipwrightExtraRows(ship, rows.Count));
        Assert.Equal(0, P4DungeonScreen.ShipwrightExtraRows(ship, 1));
    }

    [OriginalDataFact]
    public void LocationMapWaits_KeepTheDsetupPanel_WithoutClicks()
    {
        // s151: _ewait and INPUTS / INPUTN leave window 34 up (_dsetup's icons) but read only their own button / keys.
        var game = Phantasie4World.NewCampaign(Code, P4Preview.Start(4, Code).Party, new SeededRandomSource(1),
            new P4Location(P4Place.Outdoors, 1, 0, 0));
        game.EnterDungeon(2, 8, 17); // O_002's lever room: four waits, then a menu
        var map = new P4DungeonScreen(game);
        Assert.Equal(P4DungeonPrompt.Wait, map.Game.Dungeon.Prompt);
        Assert.Equal(("_dsetup", true, false), (map.PanelName, map.PanelInert, map.IconsLight)); // button 14 covers them
        while (map.Game.Dungeon.Prompt == P4DungeonPrompt.Wait)
            map.HandleKeys([Keys.Space]);
        Assert.Equal(P4DungeonPrompt.Menu, map.Game.Dungeon.Prompt); // _xmenu: no panel read
        Assert.Equal(((string?)null, false, true), (map.PanelName, map.PanelInert, map.IconsLight));
    }

    [OriginalDataFact]
    public void BankWithdrawal_ShowsTheKeypadInTheBanksPanel_DigitsType()
    {
        var game = P4Preview.StartInTown(2, Code);
        var s = new P4DungeonScreen(game);
        while (game.Town.Prompt == P4TownPrompt.Wait)
            s.HandleKeys([Keys.Space]);
        game.Party.Members[0].Account = 900;
        game.Town.Enter(P4Building.Bank);
        s.HandleKeys([Keys.D2]); // W: Withdraw
        Assert.Equal(P4TownPrompt.Number, game.Town.Prompt);
        Assert.True(s.KeypadUp);
        Assert.Equal(("_bank", true), (s.PanelName, s.PanelInert));
        Assert.True(s.CursorOn); // _numin's block is steady
        s.Keypad('4');
        s.Keypad('2');
        Assert.Equal("42", s.Typed);
        s.HandleKeys([Keys.Back]); // B
        Assert.Equal("4", s.Typed);
    }
}
