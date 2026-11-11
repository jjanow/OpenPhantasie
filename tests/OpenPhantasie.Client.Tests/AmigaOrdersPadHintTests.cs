using Microsoft.Xna.Framework.Input;
using OpenPhantasie.Client;
using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client.Tests;

/// <summary>
/// s194 (pad fix E29, <c>docs/plan/14-pad-fix-list.md</c>): III Amiga's orders screen covers the help line, so the pad's
/// hint goes in the free cells right of Fight! (<see cref="UiOverlays.OrdersPadHintArea"/>), one short text per step.
/// </summary>
public partial class AmigaOrdersScreenTests
{
    private static T Pad<T>(Func<T> read)
    {
        var was = PadWording.Active;
        try
        {
            PadWording.Active = true;
            return read();
        }
        finally
        {
            PadWording.Active = was;
        }
    }

    [OriginalDataFact]
    public void PadHint_FollowsTheStep_OnlyWhileThePadIsTheInput()
    {
        var (s, ui) = AtOrders();
        Teach(s, 0, 4, 5);
        Assert.Null(ui.AmigaOrdersPadHint(s)); // keyboard: the screen stays the original's
        Assert.Contains("Fight!", Pad(() => ui.AmigaOrdersPadHint(s))![0]); // A with no member picked
        ui.HandleKey(s, Keys.D2);
        Assert.Equal(UiText.S("pad.p3orders.order").Split('\n'), Pad(() => ui.AmigaOrdersPadHint(s)));
        ui.HandleKey(s, Keys.C);
        Assert.True(ui.AsksAmigaSpell);
        Assert.Equal([UiText.S("pad.p3orders.spell")], Pad(() => ui.AmigaOrdersPadHint(s)));
        Assert.Contains("RT cancel", UiText.S("pad.p3orders.spell"));
        Assert.Contains("more", UiText.S("pad.p3orders.spell"));
    }

    [OriginalDataFact]
    public void PadHint_EveryStepsText_FitsTheFreeCells_ClearOfEveryWindow()
    {
        foreach (var step in new[] { "member", "order", "rank", "spell", "heal", "message" })
        {
            var rows = UiText.S("pad.p3orders." + step).Split('\n');
            Assert.Equal(rows.Length, UiOverlays.OrdersPadHintRows(rows, spellWindow: step == "spell").Count);
        }
        Assert.Single(UiOverlays.OrdersPadHintRows(["A", "B"], spellWindow: false), r => r.Y == 168);
        Assert.Empty(UiOverlays.OrdersPadHintRows(["A", "B"], spellWindow: true)); // one row only under the spell window
        Assert.Empty(UiOverlays.OrdersPadHintRows([new string('W', 30)], spellWindow: false)); // never cut

        var area = UiOverlays.OrdersPadHintArea;
        Assert.True(area.X >= UiOverlays.OrdersFightBox.Right && area.Right <= 200); // Fight! box … roster (column 25)
        var windows = GraphicsPackCatalog.Load(GameDataRepository.DefaultPathFor(GameId.P3)).Find("amiga")!.KeepWindows!;
        foreach (var box in new[] { windows.Rank!.Box, windows.Heal!.Box, windows.Message!.Box })
            Assert.True(box[3] < 168, string.Join(",", box));
        Assert.True(windows.Spell!.Box[3] < 177);
    }
}
