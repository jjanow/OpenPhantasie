using Microsoft.Xna.Framework.Input;
using OpenPhantasie.Client;
using Xunit;

namespace OpenPhantasie.Client.Tests;

/// <summary>"Pad coverage" (s182): the shared pad → key map for screens that read keys only.</summary>
public sealed class PadCoverageTests
{
    [Fact]
    public void MenuPadKeys_MapsArrowsConfirmBackAndScroll()
    {
        Assert.Equal([Keys.Up, Keys.Down, Keys.Left, Keys.Right, Keys.Enter, Keys.Escape, Keys.PageUp, Keys.PageDown],
            InputMap.MenuPadKeys([Buttons.DPadUp, Buttons.DPadDown, Buttons.DPadLeft, Buttons.DPadRight,
                Buttons.A, Buttons.B, Buttons.LeftShoulder, Buttons.RightShoulder]));
    }

    [Fact]
    public void MenuPadKeys_LeftStickIsTheDPad_OtherButtonsSendNothing()
    {
        Assert.Equal([Keys.Up, Keys.Right],
            InputMap.MenuPadKeys([Buttons.LeftThumbstickUp, Buttons.X, Buttons.Y, Buttons.Start, Buttons.LeftThumbstickRight]));
    }
}
