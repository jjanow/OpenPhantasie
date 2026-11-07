using Microsoft.Xna.Framework.Input;
using OpenPhantasie.Core;

namespace OpenPhantasie.Client.Tests;

/// <summary>
/// s158: III Amiga's window-1 confirm question for the town bar's File › Load / Save (<c>0xaae2</c>, FORMAT.md 8.4): OK
/// runs the item, CANCEL drops it, other keys are read and dropped while it is up.
/// </summary>
public class AmigaConfirmWindowTests
{
    [OriginalDataFact]
    public void Ok_ReturnsTheItem_Cancel_DropsIt_OtherKeysWait()
    {
        var w = new AmigaConfirmWindow();
        Assert.Equal((false, ""), w.HandleKeys([Keys.Enter])); // not up: the keys go on
        w.Open(UiText.For(GameId.P3).Get("p3bar.confirm_load"), "load");
        Assert.Equal(["Are you sure you want to load an old", "game over this current one?"], w.Lines); // S447's two rows
        Assert.Equal((true, ""), w.HandleKeys([Keys.E, Keys.Down])); // dropped
        Assert.True(w.Shown);
        Assert.Equal((true, "load"), w.HandleKeys([Keys.O])); // OK (a click on OK sends O)
        Assert.False(w.Shown);
        w.Open(UiText.For(GameId.P3).Get("p3bar.confirm_save"), "save");
        Assert.Equal((true, ""), w.HandleKeys([Keys.C])); // CANCEL
        Assert.False(w.Shown);
        w.Open("x", "save");
        Assert.Equal((true, ""), w.HandleKeys([Keys.Escape])); // Esc = CANCEL
        Assert.False(w.Shown);
        w.Open("x", "save");
        Assert.Equal((true, "save"), w.HandleKeys([Keys.Enter]));
    }
}
