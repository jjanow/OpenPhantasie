using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using OpenPhantasie.Client;

namespace OpenPhantasie.Client.Tests;

/// <summary>s172 (PLAN P3, II terrain key decision): keyboard / pad scrolling of the field sidebar's party cards and terrain key.</summary>
public class SidebarScrollTests
{
    private static (ScrollBox Party, ScrollBox Key) Boxes(int partyRows = 2, int partyShown = 1, int keyRows = 11, int keyShown = 4)
    {
        var party = new ScrollBox();
        party.Set(partyRows, partyShown);
        var key = new ScrollBox();
        key.Set(keyRows, keyShown);
        return (party, key);
    }

    [Fact]
    public void Brackets_ScrollThePartyCards_CommaPeriod_TheKey()
    {
        var (party, key) = Boxes();
        Assert.True(SidebarScroll.Apply([Keys.OemCloseBrackets], [], false, party, key));
        Assert.Equal(1, party.Offset);
        Assert.Equal(0, key.Offset);
        Assert.True(SidebarScroll.Apply([Keys.OemPeriod, Keys.OemPeriod, Keys.OemPeriod], [], false, party, key));
        Assert.Equal(3, key.Offset);
        SidebarScroll.Apply([Keys.OemComma], [], false, party, key);
        Assert.Equal(2, key.Offset);
        SidebarScroll.Apply([Keys.OemOpenBrackets], [], false, party, key);
        Assert.Equal(0, party.Offset);
        Assert.Equal(2, key.Offset);
    }

    [Fact]
    public void Scrolling_StopsAtTheEnds_AndABoxThatFitsStays()
    {
        var (party, key) = Boxes();
        for (var i = 0; i < 20; i++)
            SidebarScroll.Apply([Keys.OemCloseBrackets, Keys.OemPeriod], [], false, party, key);
        Assert.Equal(party.MaxOffset, party.Offset);
        Assert.Equal(7, key.Offset); // 11 rows, 4 shown: the last row at the bottom
        SidebarScroll.Apply([Keys.OemOpenBrackets, Keys.OemOpenBrackets, Keys.OemOpenBrackets], [], false, party, key);
        Assert.Equal(0, party.Offset);

        // I / III without an elemental: every card fits; II with the key off: the sidebar sets the key box empty.
        var (fits, noKey) = Boxes(partyRows: 2, partyShown: 2, keyRows: 0, keyShown: 1);
        SidebarScroll.Apply([Keys.OemCloseBrackets, Keys.OemPeriod], [], false, fits, noKey);
        Assert.Equal(0, fits.Offset);
        Assert.Equal(0, noKey.Offset);
    }

    [Fact]
    public void OtherKeys_AreNotTaken()
    {
        var (party, key) = Boxes();
        Assert.False(SidebarScroll.Apply([Keys.Up, Keys.Down, Keys.PageUp, Keys.PageDown, Keys.Home, Keys.End, Keys.Space],
            [Buttons.DPadDown, Buttons.LeftThumbstickDown, Buttons.RightStick, Buttons.A], false, party, key));
        Assert.Equal(0, party.Offset);
        Assert.Equal(0, key.Offset);
    }

    [Fact]
    public void Pad_RightStick_ThePartyCards_WithTheRightTrigger_TheKey()
    {
        var (party, key) = Boxes();
        Assert.True(SidebarScroll.Apply([], [Buttons.RightThumbstickDown], false, party, key));
        Assert.Equal(1, party.Offset);
        Assert.Equal(0, key.Offset);
        SidebarScroll.Apply([], [Buttons.RightThumbstickDown], true, party, key);
        SidebarScroll.Apply([], [Buttons.RightThumbstickDown], true, party, key);
        Assert.Equal(2, key.Offset);
        SidebarScroll.Apply([], [Buttons.RightThumbstickUp], true, party, key);
        Assert.Equal(1, key.Offset);
        SidebarScroll.Apply([], [Buttons.RightThumbstickUp], false, party, key);
        Assert.Equal(0, party.Offset);
    }

    [Fact]
    public void Pad_TheStickIsEdgeTriggered_TheTriggerReadPastHalfWay()
    {
        var input = new InputMap(new ClientSettings());
        var down = new GamePadState(new GamePadThumbSticks(Vector2.Zero, new Vector2(0, -1f)),
            new GamePadTriggers(0f, 0.8f), new GamePadButtons(), new GamePadDPad());
        Assert.Contains(Buttons.RightThumbstickDown, input.NewButtons(down));
        Assert.True(SidebarScroll.RightTriggerHeld(down));
        input.EndFrame(new KeyboardState(), down);
        Assert.DoesNotContain(Buttons.RightThumbstickDown, input.NewButtons(down)); // held: one row a push
        Assert.False(SidebarScroll.RightTriggerHeld(new GamePadState(new GamePadThumbSticks(),
            new GamePadTriggers(0f, 0.3f), new GamePadButtons(), new GamePadDPad())));
    }

    [Fact]
    public void ScrollKeys_ClashWithNoFieldBinding()
    {
        Keys[] scrollKeys = [Keys.OemOpenBrackets, Keys.OemCloseBrackets, Keys.OemComma, Keys.OemPeriod];
        var bound = ClientSettings.DefaultKeyBindings().Values.ToHashSet();
        foreach (var k in scrollKeys)
        {
            Assert.True(SidebarScroll.IsScrollKey(k));
            Assert.DoesNotContain(k.ToString(), bound);
            Assert.Equal(OutsideBarAct.None, AmigaOutsideBar.ForKey(k)); // III's outside-bar keys
            var input = new InputMap(new ClientSettings());
            input.EndFrame(new KeyboardState(), default);
            Assert.Null(input.PressedFieldMove(new KeyboardState(k), default, diagonals: true));
        }
        // The pad buttons the field already reads (moves, A / B / X / Y, shoulders, stick clicks, Back) scroll nothing.
        foreach (var b in new[] { Buttons.A, Buttons.B, Buttons.X, Buttons.Y, Buttons.LeftShoulder, Buttons.RightShoulder,
                     Buttons.LeftStick, Buttons.RightStick, Buttons.Back, Buttons.DPadUp, Buttons.DPadDown, Buttons.LeftTrigger })
            Assert.Null(SidebarScroll.ForButton(b, rightTriggerHeld: true));
    }

    [Fact]
    public void FieldHelp_NamesTheScrollKeys()
    {
        Assert.Contains("[ ] party", UiText.S("game.3_n_e_s_w_arrows"));
        Assert.Contains("[ ] party", UiText.S("game.3_n_e_s_w_arrows_2"));
        Assert.Contains(", . key", UiText.S("game.3_n_e_s_w_arrows_key"));
        // The help line's scroll keys are not clickable hotkeys (no key would be sent for '[' or ',').
        Assert.Equal([Keys.Space, Keys.F2],
            PanelMouse.Hotkeys(UiText.S("game.3_n_e_s_w_arrows_key"), commandList: true).Select(h => h.Key));
    }
}
