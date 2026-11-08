using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using OpenPhantasie.Client;
using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client.Tests;

/// <summary>
/// s136: III Amiga's menu bar outside the towns (list <c>0x23910</c>, FORMAT.md 8.5): its menus, places (<c>0x12d6</c>'s
/// rules, as the fight's bar) and the click-to-open / click-to-take state.
/// </summary>
public partial class AmigaOutsideBarTests
{
    private static IReadOnlyList<string> Titles() => AmigaOutsideBar.Menus.Select(m => UiText.S(m.TitleId)).ToList();

    private static Rectangle Box(int menu) => UiOverlays.FieldMenuBox(UiOverlays.FieldMenuColumn(Titles(), menu),
        AmigaOutsideBar.Menus[menu].Items.Select(i => UiText.S(i.LabelId)).ToList());

    [OriginalDataFact]
    public void Places_FollowTheCodes0x12d6()
    {
        Assert.Equal([0, 6, 15], Enumerable.Range(0, 3).Select(m => UiOverlays.FieldMenuColumn(Titles(), m)));
        Assert.Equal(new Rectangle(0, 12, 80, 16), Box(0));
        // Options: "`p3bar.item.examine` " is 18 cells: (18 + 2) & ~1 = 20 cells; 4 items: (4 + 1) · 8 rows.
        Assert.Equal(new Rectangle(48, 12, 160, 40), Box(1));
        Assert.Equal(new Rectangle(112, 12, 96, 32), Box(2));
    }

    [Fact]
    public void Click_OpensAMenu_SecondClickCloses_ItemTakesAndCloses()
    {
        var bar = new AmigaOutsideBar();
        bar.Toggle(1, AmigaCombatSpeed.Normal);
        Assert.Equal(1, bar.Open);
        Assert.Equal(0, bar.Item);
        bar.Hover(3);
        Assert.Equal(3, bar.Item);
        bar.Hover(4); // past the menu: no change
        Assert.Equal(3, bar.Item);
        Assert.Equal(OutsideBarAct.UseItem, bar.Take(3));
        Assert.Null(bar.Open);
        Assert.Equal(OutsideBarAct.None, bar.Take(0)); // closed: nothing

        bar.Toggle(2, AmigaCombatSpeed.Slow); // Speed opens on the setting in force
        Assert.Equal(2, bar.Item);
        bar.Toggle(2, AmigaCombatSpeed.Slow);
        Assert.Null(bar.Open);
    }

    [Theory]
    [InlineData(Keys.Add, OutsideBarAct.Fast, 1)]
    [InlineData(Keys.OemPlus, OutsideBarAct.Normal, 2)]
    [InlineData(Keys.OemMinus, OutsideBarAct.Slow, 3)]
    [InlineData(Keys.Subtract, OutsideBarAct.Slow, 3)]
    [InlineData(Keys.E, OutsideBarAct.None, 0)] // the Amiga's Examine; E keeps the remaster's east until D3-070 is decided
    [InlineData(Keys.OemQuestion, OutsideBarAct.Info, 0)] // s140: ? and / (0x83e0: 63 and 47 → 0x86e6)
    [InlineData(Keys.Divide, OutsideBarAct.Info, 0)]
    [InlineData(Keys.P, OutsideBarAct.PartyStatus, 0)] // 80 → 0x8832
    [InlineData(Keys.C, OutsideBarAct.None, 0)] // C and U are the remaster's Cast / Use bindings already
    [InlineData(Keys.U, OutsideBarAct.None, 0)]
    public void Keys_AreTheAmigas0x83e0(Keys key, OutsideBarAct act, int speed)
    {
        Assert.Equal(act, AmigaOutsideBar.ForKey(key));
        Assert.Equal(speed == 0 ? null : (AmigaCombatSpeed)speed, AmigaOutsideBar.SpeedOf(act));
    }

    [Fact]
    public void Credits_TakeEveryKey_CloseOnOReturnC() // s140: 0x3ba0 with key 'O' (window 0's +46)
    {
        var bar = new AmigaOutsideBar();
        Assert.False(bar.HandleWindowKeys([Keys.O])); // not up: the keys go on
        bar.Toggle(0, AmigaCombatSpeed.Normal);
        bar.ShowCredits();
        Assert.Null(bar.Open); // the menu closed when the item was taken
        Assert.True(bar.HandleWindowKeys([Keys.E])); // read and dropped
        Assert.True(bar.HandleWindowKeys([Keys.Space]));
        Assert.True(bar.CreditsShown);
        foreach (var key in new[] { Keys.O, Keys.Enter, Keys.C, Keys.Escape })
        {
            bar.ShowCredits();
            Assert.True(bar.HandleWindowKeys([key]));
            Assert.False(bar.CreditsShown);
        }
        bar.ShowCredits();
        bar.Close(); // a load / the bar going away drops it
        Assert.False(bar.CreditsShown);
    }

    [Fact]
    public void PartyStatus_WaitsAsTheCreditsDo() // s141: 0x8832 ends with 0x3ba0('O', 0), as window 0
    {
        var bar = new AmigaOutsideBar();
        bar.ShowStatus();
        Assert.True(bar.StatusShown);
        Assert.False(bar.CreditsShown);
        Assert.True(bar.HandleWindowKeys([Keys.E])); // read and dropped (seen: e, Space, Esc on the Amiga)
        Assert.True(bar.StatusShown);
        Assert.True(bar.HandleWindowKeys([Keys.C])); // seen: c closed it
        Assert.False(bar.StatusShown);
        Assert.False(bar.HandleWindowKeys([Keys.P])); // closed: the keys go on
    }

    [OriginalDataFact]
    public void PartyStatusValues_LeftAlignedAtColumn23() // 0xab54 / 0xab1c; seen s141 with poked values
    {
        var w = new KeepWindowsLayout.PartyStatusWindow([72, 28, 248, 180], (80, 40), 5, 184,
            [56, 64, 72, 96, 104, 112, 120, 128, 136, 144], (96, 155), ["p3bar.party_status"],
            [new(1, 20), new(21, 40), new(41, 60), new(61, 100), new(101, 120), new(121, 140), new(141, null)]);
        var values = UiOverlays.StatusValues(w, new P3PartyStatus(3, 12345, 2763347, [1, 2, 3, 4, 5, 6, 7]));
        Assert.Equal(10, values.Count);
        Assert.Equal((184, 56, "3"), values[0]);
        Assert.Equal((184, 64, "12345"), values[1]);
        Assert.Equal((184, 72, "2763347"), values[2]);
        Assert.Equal((184, 96, "1"), values[3]);
        Assert.Equal((184, 144, "7"), values[^1]);
        var labels = UiOverlays.TitleRows(w.Text, UiText.S("p3bar.party_status"));
        Assert.Equal((96, 40, "- Party Status -"), labels[0]); // two leading blanks: 2 cells
        Assert.Equal((88, 56, "Days out:"), labels[1]); // the label rows share the value rows
        Assert.Equal((88, 88, "Items found:"), labels[4]);
        Assert.Equal((104, 96, "Shields:"), labels[5]);
        Assert.Equal((104, 144, "Treasure:"), labels[^1]);
    }

    [OriginalDataFact]
    public void CreditsRows_StartAtTheAmigasCells() // 0x854: "\r" one row down, leading blanks 8-px cells
    {
        var w = new KeepWindowsLayout.MessageWindow([36, 44, 284, 168], (40, 56), "#FF9224", (80, 143));
        var rows = UiOverlays.CreditsRows(w, UiText.S("p3bar.credits"));
        Assert.Equal(7, rows.Count); // ten rows, three blank
        Assert.Equal((40 + 8 * 8, 56, "Phantasie III"), rows[0]);
        Assert.Equal((40 + 4 * 8, 72, "Amiga Version 1.0 by"), rows[1]);
        Assert.Equal((40 + 10 * 8, 80, "Westwood"), rows[2]);
        Assert.Equal((40 + 2 * 8, 128, "Strategic Simulations Inc."), rows[^1]);
        Assert.True(rows[^1].Y + 8 <= w.Ok.Y); // the last row ends above OK
    }
}
