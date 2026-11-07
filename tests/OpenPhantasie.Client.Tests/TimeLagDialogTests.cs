using Microsoft.Xna.Framework.Input;
using OpenPhantasie.Core;
using OpenPhantasie.Client;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client.Tests;

public class TimeLagDialogTests
{
    [OriginalDataFact]
    public void DigitThenY_SetsLagAndSoundOn_D073()
    {
        var d = new TimeLagDialog();
        var s = ClientSessions.Overworld();
        d.Open();
        Assert.Equal([TimeLagDialog.LagQuestion], d.Lines(s, ClientSessions.Data));
        d.HandleKeys([Keys.A, Keys.Y], s); // ignored: not a digit (Y means nothing before the lag)
        Assert.True(d.IsOpen);
        d.HandleKeys([Keys.D0], s);
        Assert.Contains(TimeLagDialog.SoundQuestion, d.Lines(s, ClientSessions.Data));
        d.HandleKeys([Keys.D7], s); // ignored: the lag is chosen, Y / N is asked
        d.HandleKeys([Keys.Y], s);
        Assert.False(d.IsOpen);
        Assert.Equal((0, true), d.TakeChosen());
        Assert.Null(d.TakeChosen());
    }

    [OriginalDataFact]
    public void NumPadDigitThenN_SetsSoundOff()
    {
        var d = new TimeLagDialog();
        var s = ClientSessions.Overworld();
        d.Open();
        d.HandleKeys([Keys.NumPad9], s);
        d.HandleKeys([Keys.N], s);
        Assert.Equal((9, false), d.TakeChosen());
    }

    [OriginalDataFact]
    public void Escape_ChangesNothing()
    {
        var d = new TimeLagDialog();
        var s = ClientSessions.Overworld();
        d.Open();
        d.HandleKeys([Keys.D3], s);
        d.HandleKeys([Keys.Escape], s);
        Assert.False(d.IsOpen);
        Assert.Null(d.TakeChosen());
    }
}

public class TimeLagPresenterTests
{
    private static (GameSession, CombatPresenter) AtResolving(int lag)
    {
        var s = ClientSessions.Fight();
        var ui = new CombatPresenter(new ClientSettings { TimeLag = lag });
        ui.Start(s);
        for (var i = 0; i < 20 && ui.Phase != CombatPhase.Resolving; i++)
            ui.HandleKey(s, Keys.Enter); // FIGHT, then every member keeps the order
        Assert.Equal(CombatPhase.Resolving, ui.Phase);
        return (s, ui);
    }

    /// <summary>D-073: lag 0 is as near instant as the melee gets — the round resolves on the next frame, no pause.</summary>
    [OriginalDataFact]
    public void Lag0_ResolvesTheRoundOnTheFirstUpdate_D073()
    {
        var (s, ui) = AtResolving(0);
        var mark = s.LogSequence;
        ui.Update(s, 0, fastForward: false);
        Assert.True(s.LogSequence > mark);
    }

    [OriginalDataFact]
    public void Lag9_WaitsBeforeTheRound()
    {
        var (s, ui) = AtResolving(9);
        var mark = s.LogSequence;
        ui.Update(s, 16, fastForward: false);
        Assert.Equal(mark, s.LogSequence);
        ui.Update(s, ClientSettings.RoundDelayMs(9), fastForward: false);
        Assert.True(s.LogSequence > mark);
    }
}

/// <summary>
/// D3-066 (user s111, manual): III's Time Lag prompt also toggles sound ("Set time lag in combat and toggle sound on/off").
/// III's wilderness and dungeon use the shared T)IME LAG dialog, whose Y / N answer is the Sound switch that gates III's
/// Amiga sound effects (the Options / Esc-menu Sound row is the same flag).
/// </summary>
public class P3TimeLagSoundTests
{
    private static readonly GameDataRepository P3Data = GameDataRepository.Load(GameDataRepository.DefaultPathFor(GameId.P3));

    [OriginalDataFact]
    public void III_TimeLagPrompt_AsksForSound_AndIsACommandInTheFieldByDefault()
    {
        var settings = new ClientSettings();
        Assert.True(TimeLagDialog.IsCommand(GameMode.Overworld, settings.Game(GameId.P3)));
        Assert.True(TimeLagDialog.IsCommand(GameMode.Dungeon, settings.Game(GameId.P3)));

        var s = GameSessionFactory.Create(P3Data, new SeededDosRandomSource(4));
        s.StartNewGame(GameOptions.ForPreset(GameId.P3));
        Assert.Equal(GameId.P3, s.Game);
        var d = new TimeLagDialog();
        d.Open();
        d.HandleKeys([Keys.D4], s);
        Assert.Contains(TimeLagDialog.SoundQuestion, d.Lines(s, P3Data));
        d.HandleKeys([Keys.N], s);
        Assert.Equal((4, false), d.TakeChosen());
    }
}
