using Microsoft.Xna.Framework.Input;
using OpenPhantasie.Client;
using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client.Tests;

/// <summary>
/// D3-035 QoL <c>p3.transport-planes</c> = <c>visited</c>: III's TRANSPORTATION picker (one panel for the DOS-look and
/// Amiga-look towns) lists the entered plane towns after Scandor's, pages past 12 rows with the highlight in view, and
/// remembers a plane town as it remembers a Scandor one.
/// </summary>
public class P3TransportPlanesDialogTests
{
    private static readonly GameDataRepository P3Data = GameDataRepository.Load(GameDataRepository.DefaultPathFor(GameId.P3));

    private static (GameSession Session, Character Caster) PartyInPendragon()
    {
        var s = GameSessionFactory.Create(P3Data, new SeededDosRandomSource(7));
        s.StartNewGame(GameOptions.ForPreset(GameId.P3).With("p3.transport-planes", "visited"));
        s.OpenGuild();
        var caster = s.CreateAndEnroll("MAGE", Race.Human, CharacterClass.Wizard);
        Assert.True(s.TryAddToParty(caster, out var err), err);
        s.LeaveGuild();
        caster.SpellsKnown[SpellId.Transportation] = 1;
        return (s, caster);
    }

    private static void Cast(GameSession s, Character caster)
    {
        caster.MagicCur = caster.MagicMax = 50;
        Assert.True(s.CastSpell(caster, SpellId.Transportation).Success);
    }

    [OriginalDataFact]
    public void VisitedPlaneTowns_AreListed_Paged_AndRemembered()
    {
        var (s, caster) = PartyInPendragon();
        s.EnterTown(Phantasie3Campaign.LightTown);
        s.EnterTown(Phantasie3Campaign.DeathTown);
        s.EnterTown(1);

        var d = new TransportDialog();
        Cast(s, caster);
        Assert.True(d.OpenIfPending(s));
        Assert.Equal(15, s.TransportDestinations.Count); // Scandor 2–14, then Light and Death
        var lines = d.Lines(s, P3Data);
        Assert.Equal(1 + TransportDialog.PageRows, lines.Count); // fits the panel's 13 lines
        Assert.Equal(new ListPage(1, TransportDialog.PageRows, 0, 15), d.Page);
        Assert.StartsWith("> ", lines[1]);

        d.HandleKeys([Keys.Up], s); // wraps to the last entry, Death: the page follows the highlight
        lines = d.Lines(s, P3Data);
        Assert.Equal("> Death", lines[^1]);
        Assert.Equal("  Light", lines[^2]);
        Assert.Equal(3, d.Page!.Offset);
        d.HandleKeys([Keys.Enter], s); // the normal town entry
        Assert.Equal(Phantasie3Campaign.DeathTown, s.CurrentTownId);
        Assert.Equal(Phantasie3Campaign.Netherworld, s.Overworld.RegionId);

        Cast(s, caster); // from Death: 1–14 and Light
        Assert.True(d.OpenIfPending(s));
        d.HandleKeys([Keys.Up, Keys.Escape], s);
        Assert.Equal(Phantasie3Campaign.LightTown, d.TownId);
        Cast(s, caster);
        Assert.True(d.OpenIfPending(s));
        Assert.Equal(Phantasie3Campaign.LightTown, s.TransportDestinations[d.Cursor]); // a plane town stays remembered
        Assert.Equal("> Light", d.Lines(s, P3Data)[^1]);
    }
}
