using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client.Tests;

/// <summary>
/// s158, II ST (FORMAT.md 7.4): the alert boxes drawn after the beep (AES <c>form_alert</c>): the disks' own strings in the
/// pack (<see cref="FieldAlertLine.Box"/>, <see cref="FieldAlerts.CueBoxes"/>), their layout as measured on EmuTOS's
/// <c>form_alert</c> (<see cref="StAlertLook"/>), the holes filled from the remaster's line and the modal box.
/// </summary>
public class P2AlertBoxTests
{
    private static GraphicsPack Pack() =>
        GraphicsPackCatalog.Load(GameDataRepository.DefaultPathFor(GameId.P2)).Find("atari-st")!;

    private static FieldAlerts Alerts() => Pack().FieldSounds!.Alerts!;

    [Fact]
    public void Parse_SplitsLinesAndButtons_KeepsThePadding()
    {
        var a = StAlert.Parse("[0][              |You are improved    |              ][ O.K. ]");
        Assert.Equal(0, a.Icon);
        Assert.Equal(["              ", "You are improved    ", "              "], a.Lines);
        Assert.Equal([" O.K. "], a.Buttons);
        Assert.Equal((20, 6), (a.TextColumns, a.ButtonColumns));
        Assert.Throws<FormatException>(() => StAlert.Parse("no alert"));
    }

    [Fact]
    public void Fill_PrintfHoles_InTurn_WidthPadsLeft()
    {
        var died = StAlert.Parse("[0][%8s has died      ][SORRY]").Fill(["KLEPTO"]);
        Assert.Equal(["  KLEPTO has died      "], died.Lines);
        var xp = StAlert.Parse("[0][              |You need %ld more    |experience points   ][GO AND LEARN]").Fill(["250"]);
        Assert.Equal("You need 250 more    ", xp.Lines[1]);
    }

    [OriginalDataTheory]
    // EmuTOS 1.4 in Hatari (parity/shots, 4 host px per ST px): the root box (inside the 3-px outline) and the button's
    // text area (inside its 3-px border).
    [InlineData("[0][              |You are improved    |              ][ O.K. ]", 72, 77, 176, 56, 128, 117, 56)] // wand-improved
    [InlineData("[0][                | UNUSABLE    |              ][SORRY]", 88, 77, 144, 56, 136, 117, 48)] // wand-unusable
    public void Layout_MatchesTheEmuTosShots(string text, int x, int y, int w, int h, int bx, int by, int bw)
    {
        var layout = Pack().FieldSounds!.Alerts!.Look!.Layout(StAlert.Parse(text));
        Assert.Equal((x, y, w, h), layout.Box);
        Assert.Equal((x - 3, y - 3, w + 6, h + 6), layout.Outline);
        Assert.Equal((bx, by, bw, 8), (layout.Buttons[0].X, layout.Buttons[0].Y, layout.Buttons[0].W, layout.Buttons[0].H));
        Assert.Equal((x + 8, y + 16), (layout.Lines[1].X, layout.Lines[1].Y)); // line 2 from cell (1, 2)
    }

    [OriginalDataFact]
    public void Layout_TwoButtons_AsTheTrainingCostShot()
    {
        // p2-train-thief-level0-cost0.png: box 56,81 208×48; buttons' text areas at x 96 and 168 (7 cells, 2 apart).
        var layout = Alerts().Look!.Layout(StAlert.Parse("[0][          |It will cost %d gold     ][AGREE | NOT ]").Fill(["0"]));
        Assert.Equal((56, 81, 208, 48), layout.Box);
        Assert.Equal([96, 168], layout.Buttons.Select(b => b.X));
        Assert.All(layout.Buttons, b => Assert.Equal((113, 56), (b.Y, b.W)));
    }

    [OriginalDataFact]
    public void Pack_EveryAlertButTheMoveQuestion_HasTheDisksBox_AndDoneIsTheCastsCue()
    {
        var alerts = Alerts();
        Assert.All(alerts.Lines.Where(l => l.Key != "core.guild.move_not_in_town"), l => Assert.NotNull(l.Box));
        Assert.Null(alerts.Lines.Single(l => l.Key == "core.guild.move_not_in_town").Box); // the remaster's own YES / NO dialog
        Assert.Equal(" D O N E !      ", alerts.CueBoxes["town.cast_done"].Lines[1]);
        Assert.Equal([" O.K. "], alerts.CueBoxes["town.cast_done"].Buttons);
        Assert.Equal("Not enough Magic     ", alerts.Lines.Single(l => l.Key == "core.magic.not_enough_mp").Box!.Lines[0]);
        Assert.Equal(alerts.CueBoxes["town.cast_done"].ToString(),
            alerts.Lines.Single(l => l.Key == "core.magic.x_drinks_healing_x_x").Box!.ToString());
    }

    [OriginalDataFact]
    public void Colours_VdiZeroAndOne_OfTheModesPalette()
    {
        var pack = Pack();
        var look = pack.FieldSounds!.Alerts!.Look!;
        Assert.Equal((Color.Black, Color.White), Game1.StAlertColours(look, GameMode.Town, pack.Palettes)); // as the shots
        Assert.Equal((Color.Black, new Color(255, 0, 0)), Game1.StAlertColours(look, GameMode.Overworld, pack.Palettes));
        Assert.Equal((Color.Black, Color.White), Game1.StAlertColours(look, GameMode.Combat, pack.Palettes)); // none listed
    }

    [OriginalDataFact]
    public void Beeps_GiveTheBox_WithTheLinesValues()
    {
        var beeps = new P2AlertBeeps(Alerts());
        var died = beeps.BoxFor(GameText.F("core.aging.x_has_died", "KLEPTO"), GameMode.Town);
        Assert.Equal("  KLEPTO has died      ", died!.Lines[0]);
        Assert.Null(beeps.BoxFor(GameText.S("core.guild.move_not_in_town"), GameMode.Town)); // beeps, no box
        Assert.True(beeps.Beeps(GameText.S("core.guild.move_not_in_town"), GameMode.Town));
        Assert.Null(beeps.BoxFor(GameText.S("core.p2.unusable"), GameMode.Town, fromDialog: false)); // dialog only
        Assert.Null(beeps.BoxFor(GameText.S("core.magic.not_enough_mp"), GameMode.Town)); // a field alert
        Assert.NotNull(beeps.BoxFor(GameText.S("core.magic.not_enough_mp"), GameMode.Dungeon));
    }

    [OriginalDataFact]
    public void Beeps_ARefusalNotedAndReturned_ShowsOneBox()
    {
        var beeps = new P2AlertBeeps(Alerts());
        var line = GameText.S("core.armory.not_enough_gold");
        var boxes = beeps.BoxesFor([line], [line], GameMode.Town);
        Assert.Single(boxes);
        Assert.Equal("You can not afford it    ", boxes[0].Lines[1]);
    }

    [Fact]
    public void Box_OwnsTheKeys_UntilAnswered_ThenTheNext()
    {
        var box = new P2AlertBox();
        Assert.False(box.HandleKeys([Keys.Enter])); // none up: the keys go on
        var a = StAlert.Parse("[0][A][OK]");
        var b = StAlert.Parse("[0][B][OK]");
        box.Offer(a);
        box.Offer(b);
        Assert.Same(a, box.Current);
        Assert.True(box.HandleKeys([Keys.Left])); // used up, the box stays
        Assert.Same(a, box.Current);
        Assert.True(box.HandleKeys([Keys.Enter]));
        Assert.Same(b, box.Current);
        Assert.True(box.HandleKeys([Keys.Escape]));
        Assert.False(box.Shown);
        box.Offer(a);
        box.Close();
        Assert.Null(box.Current);
    }
}
