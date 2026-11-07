using Microsoft.Xna.Framework.Input;
using OpenPhantasie.Client;
using OpenPhantasie.Core;

namespace OpenPhantasie.Client.Tests;

/// <summary>Option → Cast spell lists only spells castable here (D-035, manual p.12–13).</summary>
public class CastDialogTests
{
    [OriginalDataFact]
    public void Wilderness_ListsEverywhereAndWildernessSpellsOnly()
    {
        var s = ClientSessions.Overworld();
        var priest = s.Party.Members[1];
        foreach (var id in new[] { SpellId.HealingI, SpellId.FireflashI, SpellId.MonsterEval, SpellId.Vision, SpellId.Transportation })
            priest.SpellsKnown[id] = 1;
        var dialog = new CastDialog();
        Assert.Null(dialog.Open(s));
        dialog.HandleKeys([Keys.D2], s);
        var lines = dialog.Lines(s, ClientSessions.Data);
        Assert.Contains(lines, l => l.Contains("HEALING I "));
        Assert.Contains(lines, l => l.Contains("MONSTER EVAL."));
        Assert.Contains(lines, l => l.Contains("VISION"));
        Assert.DoesNotContain(lines, l => l.Contains("FIREFLASH"));
        Assert.DoesNotContain(lines, l => l.Contains("TRANSPORTATION"));
    }

    /// <summary>
    /// s158: a caster who knows no spell at all gets the no-spells line (III: S443, DOS root 0x8d8a, Amiga 0x84e0); one whose
    /// known spells are none castable here keeps the castable-here line.
    /// </summary>
    [OriginalDataFact]
    public void NoSpellKnown_HasItsOwnLine()
    {
        var s = ClientSessions.Overworld();
        var caster = s.Party.Members[1];
        Array.Clear(caster.SpellsKnown);
        var dialog = new CastDialog();
        dialog.Open(s);
        Assert.Equal(UiText.F("cast.x_knows_no_spells", caster.Name), dialog.HandleKeys([Keys.D2], s));
        caster.SpellsKnown[SpellId.FireflashI] = 1; // combat only
        dialog.Open(s);
        Assert.Equal(UiText.F("cast.x_knows_no_spell_that_can", caster.Name), dialog.HandleKeys([Keys.D2], s));
    }
}
