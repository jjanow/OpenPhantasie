using Microsoft.Xna.Framework.Input;
using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client.Tests;

/// <summary>
/// s110: a round's spell flashes (III Amiga, <see cref="CombatFlashRun"/>) play out before the next round's menu; the
/// hold releases when the run is over, and fast-forward drops it.
/// </summary>
public class CombatFlashHoldTests
{
    private static readonly CombatFlashLayout Layout = new(["a", "b", "c"], 6, 1, 60, 8, 3, 2, 36, 22, 6, 15, 1, 6, -1,
        140, 48, [143, 138, 133]);

    private static (GameSession, CombatPresenter) AtResolving()
    {
        var s = ClientSessions.Fight();
        var ui = new CombatPresenter(new ClientSettings { TimeLag = 0 });
        var orc = new Monster { Id = 1, Name = "Orc" };
        CombatHit spell = new(DamageSource.PartySpell, null, orc);
        // Any round: three flashes (63 VBLs, 1050 ms at 60 Hz).
        ui.PlanFlashes = _ => CombatFlashRun.Plan([spell, spell, spell], Layout, _ => 0, _ => 0, _ => false, _ => 0);
        ui.Start(s);
        for (var i = 0; i < 20 && ui.Phase != CombatPhase.Resolving; i++)
            ui.HandleKey(s, Keys.Enter); // FIGHT, then every member keeps the order
        Assert.Equal(CombatPhase.Resolving, ui.Phase);
        return (s, ui);
    }

    [OriginalDataFact]
    public void TheNextRound_WaitsForTheFlashes_ThenItsMenuOpens()
    {
        var (s, ui) = AtResolving();
        Assert.False(ui.Update(s, 0, fastForward: false));
        Assert.Equal(GameMode.Combat, s.Mode);
        Assert.NotNull(ui.FlashRun);
        Assert.Equal(1, ui.FlashRunSerial);
        Assert.False(ui.AwaitingInput);
        Assert.NotNull(ui.FlashShown);
        ui.Update(s, 1000, fastForward: false);
        Assert.False(ui.AwaitingInput);
        Assert.Equal(CombatPhase.Resolving, ui.Phase);
        ui.Update(s, 60, fastForward: false);
        Assert.Null(ui.FlashRun);
        Assert.NotEqual(CombatPhase.Resolving, ui.Phase);
    }

    [OriginalDataFact]
    public void FastForward_DuringTheHold_DropsTheFlashes_AndFightsOn()
    {
        var (s, ui) = AtResolving();
        Assert.False(ui.Update(s, 0, fastForward: false));
        Assert.NotNull(ui.FlashRun);
        Assert.True(ui.Update(s, 16, fastForward: true));
        Assert.Null(ui.FlashRun);
        Assert.NotEqual(GameMode.Combat, s.Mode);
    }
}
