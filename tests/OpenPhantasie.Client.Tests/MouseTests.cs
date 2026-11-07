using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using OpenPhantasie.Client;
using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client.Tests;

/// <summary>
/// The mouse (presentation, always on): window → virtual mapping through the letterbox, the click / drag / wheel state
/// machine, hit-testing, and the keys a click on a dialog's drawn text sends (the same keys a player would type).
/// </summary>
public class MouseTests
{
    // --- window → virtual ---------------------------------------------------------------------------------------------

    [Fact]
    public void PresentRect_IsTheLargestIntegerFit_Centred()
    {
        Assert.Equal(new Rectangle(0, 0, 960, 600), VirtualScreen.PresentRect(320, 200, 960, 600));
        // 1280×976: ×4 = 1280×800, 88 rows of letterbox above and below.
        Assert.Equal(new Rectangle(0, 88, 1280, 800), VirtualScreen.PresentRect(320, 200, 1280, 976));
        // Too small for ×1 still draws ×1 (clipped), from the corner it centres on.
        Assert.Equal(new Rectangle(-10, 0, 320, 200), VirtualScreen.PresentRect(320, 200, 300, 200));
    }

    [Fact]
    public void FractionalPresentRect_FillsTheWindow_AspectKept_Centred()
    {
        // User s152: IV's 506×347 title canvas on a 1920×1032 client area: 1032 / 347 ≈ 2.97× (integer fit gives 2×).
        Assert.Equal(new Rectangle(454, 169, 1012, 694), VirtualScreen.PresentRect(506, 347, 1920, 1032));
        Assert.Equal(new Rectangle(207, 0, 1505, 1032), VirtualScreen.FractionalPresentRect(506, 347, 1920, 1032));
        // Width-bound: letterbox above and below.
        Assert.Equal(new Rectangle(0, 100, 800, 500), VirtualScreen.FractionalPresentRect(320, 200, 800, 700));
        // An exact integer multiple is the same as the integer fit.
        Assert.Equal(VirtualScreen.PresentRect(320, 200, 960, 600), VirtualScreen.FractionalPresentRect(320, 200, 960, 600));
        // Smaller than 1× scales down instead of clipping.
        Assert.Equal(new Rectangle(0, 6, 300, 188), VirtualScreen.FractionalPresentRect(320, 200, 300, 200));
        // Mouse mapping goes through the fractional rectangle like any other.
        var present = VirtualScreen.FractionalPresentRect(506, 347, 1920, 1032);
        Assert.Equal(new Point(0, 0), VirtualScreen.WindowToVirtual(present, 506, 347, new Point(207, 0)));
        Assert.Equal(new Point(505, 346), VirtualScreen.WindowToVirtual(present, 506, 347, new Point(1711, 1031)));
        Assert.Null(VirtualScreen.WindowToVirtual(present, 506, 347, new Point(206, 500)));
    }

    [Fact]
    public void WindowToVirtual_MapsThroughScaleAndLetterbox()
    {
        var present = VirtualScreen.PresentRect(320, 200, 1280, 976);
        Assert.Equal(new Point(0, 0), VirtualScreen.WindowToVirtual(present, 320, 200, new Point(0, 88)));
        Assert.Equal(new Point(160, 100), VirtualScreen.WindowToVirtual(present, 320, 200, new Point(640, 488)));
        Assert.Equal(new Point(319, 199), VirtualScreen.WindowToVirtual(present, 320, 200, new Point(1279, 887)));
        Assert.Equal(new Point(1, 0), VirtualScreen.WindowToVirtual(present, 320, 200, new Point(4, 91)));
        Assert.Equal(new Point(0, 0), VirtualScreen.WindowToVirtual(present, 320, 200, new Point(3, 91)));
        Assert.Null(VirtualScreen.WindowToVirtual(present, 320, 200, new Point(10, 87)));  // top letterbox
        Assert.Null(VirtualScreen.WindowToVirtual(present, 320, 200, new Point(10, 888))); // bottom letterbox
        Assert.Null(VirtualScreen.WindowToVirtual(present, 320, 200, new Point(-1, 200)));
    }

    [Fact]
    public void WindowToVirtual_NonSquarePixels_StretchedPicture()
    {
        // A 320×200 picture with 5:6 pixels is shown 320×240; at ×2 that is 640×480.
        var present = VirtualScreen.PresentRect(320, 240, 640, 480);
        Assert.Equal(new Rectangle(0, 0, 640, 480), present);
        Assert.Equal(new Point(160, 100), VirtualScreen.WindowToVirtual(present, 320, 200, new Point(320, 240)));
        Assert.Equal(new Point(0, 199), VirtualScreen.WindowToVirtual(present, 320, 200, new Point(0, 479)));
        Assert.Equal(new Point(0, 0), VirtualScreen.WindowToVirtual(present, 320, 200, new Point(0, 2)));
    }

    // --- the state machine ----------------------------------------------------------------------------------------------

    [Fact]
    public void Hover_OnlyWhenThePointerMoves()
    {
        var m = new MouseInput();
        m.Update(new Point(10, 10), false, false, 0);
        Assert.False(m.Moved); // the first position is not a move (a pointer resting on a row does nothing)
        m.Update(new Point(10, 10), false, false, 0);
        Assert.False(m.Moved);
        m.Update(new Point(11, 10), false, false, 0);
        Assert.True(m.Moved);
        m.Update(null, false, false, 0);
        Assert.False(m.Moved);
        Assert.Null(m.Position);
    }

    [Fact]
    public void PressAndRelease_InPlace_IsAClick_AtThePressPoint()
    {
        var m = new MouseInput();
        m.Update(new Point(50, 60), false, false, 0);
        m.Update(new Point(50, 60), true, false, 0);
        Assert.True(m.AnyPressed);
        Assert.Null(m.LeftClick);
        m.Update(new Point(52, 61), false, false, 0); // a small wobble is still a click
        Assert.Equal(new Point(50, 60), m.LeftClick);
        m.Update(new Point(52, 61), false, false, 0);
        Assert.Null(m.LeftClick); // one frame only
    }

    [Fact]
    public void Drag_StartsPastTheThreshold_EndsWithADrop_AndIsNoClick()
    {
        var m = new MouseInput();
        m.Update(new Point(20, 20), false, false, 0);
        m.Update(new Point(20, 20), true, false, 0);
        m.Update(new Point(22, 20), true, false, 0);
        Assert.False(m.Dragging);
        m.Update(new Point(30, 25), true, false, 0);
        Assert.True(m.DragStarted);
        Assert.Equal(new Point(20, 20), m.DragOrigin);
        m.DragPayload = "slot 3";
        m.Update(new Point(80, 40), true, false, 0);
        Assert.False(m.DragStarted);
        Assert.True(m.Dragging);
        m.Update(new Point(81, 40), false, false, 0);
        Assert.Equal((new Point(20, 20), new Point(81, 40)), m.DragEnded);
        Assert.Null(m.LeftClick);
        Assert.Equal("slot 3", m.DragPayload); // still readable on the drop frame
        Assert.False(m.Dragging);
        m.Update(new Point(81, 40), false, false, 0);
        Assert.Null(m.DragEnded);
        Assert.Null(m.DragPayload);
    }

    [Fact]
    public void DropOutsideThePicture_LandsOnTheLastPointInside()
    {
        var m = new MouseInput();
        m.Update(new Point(5, 5), true, false, 0);
        m.Update(new Point(40, 5), true, false, 0);
        m.Update(null, true, false, 0);
        m.Update(null, false, false, 0);
        Assert.Equal((new Point(5, 5), new Point(40, 5)), m.DragEnded);
    }

    [Fact]
    public void RightClick_AndWheelNotches()
    {
        var m = new MouseInput();
        m.Update(new Point(5, 5), false, false, 1000);
        Assert.Equal(0, m.WheelSteps); // the first reading is the baseline
        m.Update(new Point(5, 5), false, true, 1240);
        Assert.Equal(2, m.WheelSteps);
        Assert.False(m.RightClick);
        m.Update(new Point(5, 5), false, false, 1180);
        Assert.True(m.RightClick);
        Assert.Equal(0, m.WheelSteps); // half a notch is kept
        m.Update(new Point(5, 5), false, false, 1060);
        Assert.Equal(-1, m.WheelSteps);
    }

    // --- hit-testing ----------------------------------------------------------------------------------------------------

    [Fact]
    public void HitMap_TopmostRegionWins()
    {
        var map = new HitMap();
        map.Add(new Rectangle(0, 0, 320, 200), "map");
        map.Add(new Rectangle(8, 40, 100, 20), "panel");
        Assert.Equal("panel", map.At(new Point(10, 45))!.Tag);
        Assert.Equal("map", map.At(new Point(200, 45))!.Tag);
        Assert.Null(map.At(new Point(400, 45)));
        map.Clear();
        Assert.Null(map.At(new Point(10, 45)));
    }

    [Fact]
    public void HitTest_IndexAndSteps()
    {
        var rows = new[] { new Rectangle(0, 0, 10, 10), new Rectangle(0, 10, 10, 10) };
        Assert.Equal(1, HitTest.Index(rows, new Point(5, 15)));
        Assert.Equal(-1, HitTest.Index(rows, new Point(15, 5)));
        Assert.Equal([Keys.Down, Keys.Down], HitTest.Steps(1, 3, Keys.Up, Keys.Down));
        Assert.Equal([Keys.Up], HitTest.Steps(2, 1, Keys.Up, Keys.Down));
        Assert.Empty(HitTest.Steps(2, 2, Keys.Up, Keys.Down));
    }

    [Fact]
    public void TextSpan_CoversTheGlyphCells()
    {
        var span = UiOverlays.TextSpan(14, 30, 2, 5);
        Assert.Equal(new Rectangle(14 + 12 - 1, 29, 19, 10), span);
        Assert.True(span.Contains(new Point(14 + 12, 30)));
        Assert.False(span.Contains(new Point(14 + 30, 30)));
    }

    // --- keys the mouse sends -------------------------------------------------------------------------------------------

    [Fact]
    public void InputMap_InjectedKeys_CountAsNewlyPressed_InOrder_ForOneFrame()
    {
        var input = new InputMap(new ClientSettings());
        var none = new KeyboardState();
        input.Inject([Keys.Down, Keys.Down, Keys.Enter]);
        Assert.Equal([Keys.Down, Keys.Down, Keys.Enter], input.NewKeys(none));
        Assert.True(input.Pressed(GameAction.Confirm, none, default));
        Assert.True(input.PressedMove(CardinalDirection.South, none, default));
        Assert.False(input.AnyRealKeyPressed(none));
        input.EndFrame(none, default);
        Assert.Empty(input.NewKeys(none));
        Assert.False(input.Pressed(GameAction.Confirm, none, default));
    }

    private static PanelMouse.LineHit HitOn(IReadOnlyList<string> lines, int line, int column = 1) =>
        PanelMouse.Analyze(lines).Hits.Single(h => h.Line == line && h.Start <= column && column < h.End);

    [OriginalDataFact]
    public void CursorList_HoverMovesTheMarker_ClickPicks_GameMenu()
    {
        var s = ClientSessions.Overworld();
        var d = new GameMenuDialog();
        d.Open();
        var lines = d.Lines(s, ClientSessions.Data);
        var load = HitOn(lines, 4);
        Assert.Equal([Keys.Down, Keys.Down], load.Hover);
        d.HandleKeys(load.Click, s);
        Assert.False(d.IsOpen);
        Assert.Equal(GameMenuChoice.Load, d.TakeChoice());
        Assert.False(PanelMouse.Analyze(lines).AnyKey);
    }

    [OriginalDataFact]
    public void OneLineBar_LeftRightThenReturn_InspectMemberNoYes()
    {
        var s = ClientSessions.Overworld();
        var d = new InspectDialog(new PromptMenu(), ClientSessions.Data);
        d.Open(s);
        Assert.True(PanelMouse.Analyze(d.Lines(s, ClientSessions.Data)).AnyKey); // the party summary: any key
        d.HandleKeys([Keys.Enter], s);
        var lines = d.Lines(s, ClientSessions.Data);
        var bar = lines.Count - 1;
        Assert.Equal(">NO  YES", lines[bar]);
        var yes = HitOn(lines, bar, column: 5);
        Assert.Equal([Keys.Right], yes.Hover);
        Assert.Empty(HitOn(lines, bar, column: 1).Hover);
        d.HandleKeys(yes.Click, s);
        Assert.Equal(InspectDialog.WhoTitle, d.Lines(s, ClientSessions.Data)[0]);

        // "WHOM…": numbered rows type the member's digit.
        var who = d.Lines(s, ClientSessions.Data);
        Assert.Equal([Keys.D2], HitOn(who, 2).Click);
        d.HandleKeys(HitOn(who, 2).Click, s);
        Assert.Equal("Priesto", d.Lines(s, ClientSessions.Data)[0]);
    }

    [Fact]
    public void MidLineMarker_IsABar_Too()
    {
        var hits = PanelMouse.Analyze(["INSPECT MEMBER?", " NO >YES"]).Hits;
        Assert.Equal(2, hits.Count);
        Assert.Equal([Keys.Left, Keys.Enter], hits.Single(h => h.Start == 0).Click);
        Assert.Equal([Keys.Enter], hits.Single(h => h.Start == 4).Click);
    }

    [OriginalDataFact]
    public void AnyKeyPanels_And_TrainingAgreeNot_And_YesNo()
    {
        var s = ClientSessions.Overworld();
        Assert.True(PanelMouse.Analyze(HealthDialog.Report(s)).AnyKey);
        var (hits, anyKey) = PanelMouse.Analyze(["It will cost", "120 gold", "A AGREE", "N NOT"]);
        Assert.False(anyKey);
        Assert.Equal([Keys.A], hits.Single(h => h.Line == 2).Click);
        Assert.Equal([Keys.N], hits.Single(h => h.Line == 3).Click);
        var yn = PanelMouse.Analyze(["SOUND? (Y/N)"]).Hits;
        Assert.Equal([Keys.Y], yn.Single(h => h.Start == 8).Click);
        Assert.Equal([Keys.N], yn.Single(h => h.Start == 10).Click);
    }

    [OriginalDataFact]
    public void CombatInfoBoxes_AreAnyKeyPanels()
    {
        var s = ClientSessions.Fight();
        Assert.True(PanelMouse.Analyze(CombatText.InfoBox(CombatText.ShowNamesIndex, s)).AnyKey);
        Assert.True(PanelMouse.Analyze(CombatText.InfoBox(CombatText.PartyHealthIndex, s)).AnyKey);
        var sheet = InspectDialog.SheetLines(s.Party.Members[0], ClientSessions.Data);
        Assert.True(PanelMouse.Analyze([.. sheet, "", UiText.S("member.press_space")]).AnyKey);
    }

    [OriginalDataFact]
    public void TypedEntry_IsNeverTypedByTheMouse()
    {
        // Continue: the "> 12_" entry line confirms (Return) and types nothing.
        var (hits, _) = PanelMouse.Analyze(["CONTINUE", "GUILD NUMBER?", "> 12_"]);
        Assert.All(hits, h => Assert.All(h.Click, k => Assert.True(k == Keys.Enter, k.ToString())));
        Assert.All(hits, h => Assert.Empty(h.Hover));

        // Guild N)EW: race 1, class 1, Y → INPUT NAME; nothing on that screen sends a letter or digit.
        var session = GameSessionFactory.Create(ClientSessions.Data, new SeededDosRandomSource(3));
        session.OpenGuild();
        var d = new GuildNewDialog();
        Assert.Null(d.Open(session));
        d.HandleKeys([Keys.D1], session);
        d.HandleKeys([Keys.D1], session);
        d.HandleKeys([Keys.Y], session);
        d.HandleKeys([Keys.A, Keys.B], session);
        var name = d.Lines(session, ClientSessions.Data);
        Assert.Contains(name, l => l.Contains("AB_"));
        var (nameHits, nameAnyKey) = PanelMouse.Analyze(name);
        Assert.Empty(nameHits);
        Assert.True(nameAnyKey); // a click is Return = accept the name, as the keyboard's Enter
    }

    [Fact]
    public void Distribute_Rows_AreACursorList_ThePoolIsNot()
    {
        var lines = new[]
        {
            "NAME       CLASS WPN ARM SHL  #",
            " SELL                         10",
            " SACK                         2",
            ">1 Fighto     FIG    4   2   0   3",
            " 2 Priesto    PRI    2   1   0   1",
            "---------------------------------",
            "SWORD          1  2 <====",
        };
        var (hits, _) = PanelMouse.Analyze(lines);
        Assert.Equal([Keys.Up, Keys.Up, Keys.Enter], hits.Single(h => h.Line == 1).Click);
        Assert.Equal([Keys.Down, Keys.Enter], hits.Single(h => h.Line == 4).Click);
        Assert.DoesNotContain(hits, h => h.Line >= 5);
    }

    [Theory]
    [InlineData("L list P purge N new R rename A add S spells T train D drop E exit", "LPNRASTDE")]
    [InlineData("A add to party, N new member, G leave.", "ANG")]
    [InlineData("C)AST     H)EALTH", "CH")]
    [InlineData("1 THRUST 2 ATTACK 3 SLASH 4 LUNGE", "1234")]
    [InlineData("001 WEAPON(S)", "")]
    public void Hotkeys_FindTheKeysOfACommandLine(string text, string keys)
    {
        var found = PanelMouse.Hotkeys(text, commandList: true).Select(h => h.Key).ToList();
        Assert.Equal(keys.Select(c => PanelMouse.KeyOf(c.ToString())!.Value), found);
    }

    [Fact]
    public void Hotkeys_FieldHelp_OnlyItsCommands_NotTheMoveLetters()
    {
        var found = PanelMouse.Hotkeys("3/N E S W arrows numpad move  Space fight  F2 map", commandList: true)
            .Select(h => h.Key).ToList();
        Assert.Equal([Keys.Space, Keys.F2], found);
        Assert.Equal([Keys.Enter, Keys.Escape],
            PanelMouse.Hotkeys("Type name  Enter accept  Esc cancel", commandList: true).Select(h => h.Key));
    }

    // --- the remaster menus ---------------------------------------------------------------------------------------------

    [Fact]
    public void MainMenu_LinesToRows()
    {
        var menu = new MainMenuScreen(g => g == GameId.P1);
        var lines = menu.Lines();
        Assert.Null(menu.RowOfLine(0));
        Assert.Null(menu.RowOfLine(1));
        Assert.Equal(0, menu.RowOfLine(2));
        Assert.Equal(3, menu.RowOfLine(5));
        Assert.Null(menu.RowOfLine(6));
        Assert.Equal(4, menu.RowOfLine(7));
        Assert.EndsWith(lines[7].Text.TrimStart('>', ' '), UiText.S("menu.options"));
        Assert.Equal(5, menu.RowOfLine(8));
        Assert.Null(menu.RowOfLine(9));
    }

    [Fact]
    public void Options_PointSkipsHeaders_ClickChangesAndSwitchesTabs()
    {
        var settings = new ClientSettings { IntegerScale = 3 };
        var o = new OptionsScreen(settings, _ => [], g => g == GameId.P1);
        o.Open();
        o.Lines();
        Assert.True(o.Point(2)); // window scale
        Assert.Equal(1, o.Cursor);
        o.Click(2, 3);
        Assert.Equal(4, settings.IntegerScale);
        Assert.True(o.Changed);

        // Tab row: ">[GENERAL]  I   II   III   IV " — column 12 is "I".
        o.Click(1, 12);
        Assert.Equal(GameId.P1, o.Tab);
        Assert.Equal(0, o.Cursor);
        var lines = o.Lines();
        var header = lines.ToList().FindIndex(l => l.Style == MenuLineStyle.Header);
        Assert.False(o.Point(header)); // headers are not rows the cursor stops on
        Assert.Equal(0, o.Cursor);
        o.Click(1, 1);
        Assert.Null(o.Tab);
    }
}
