using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client;

public partial class Game1
{
    private P1DefeatScreen? _p1Defeat;
    private double _p1DefeatStart;
    private int _p1DefeatSoundsStarted;
    private int _p1DefeatTone; // s154: 0 = the raw tone not started, 1 = sounding, 2 = stopped
    // s156: the defeat screens run this program run (h33 +0: DATA, 0 at load, += 1 by every run of h32's screens, never
    // saved or reset by a load), for the 5th defeat's sign. Not the session's PartyDefeatsThisRun (a load reset that).
    private int _p1DefeatScreensThisProgramRun;
    private object? _p1DefeatCounted; // the defeat last counted: a screen rebuilt for it (a splash, a toggle) counts once

    /// <summary>Whether I's party defeat runs as the Amiga's screens (AMIGA SCREENS, a pack with <c>defeat_screen</c> and
    /// <c>decor.pic</c>): they run by themselves, no key needed (s153).</summary>
    private bool P1DefeatScreens => _session is { Game: GameId.P1 } && CurrentGameSettings.OriginalScreens
        && _assets?.Pack?.DefeatScreen is not null && _assets.Decor is not null;

    /// <summary>
    /// s153: I Amiga's party defeat (h32 <c>+0</c>), once a frame. Under <see cref="P1DefeatScreens"/> the defeat box is
    /// answered at once (the Amiga shows no report: black, then <c>decor.pic</c>), and the outcome runs on
    /// <see cref="P1DefeatScreen"/>'s clock with its sounds, answering Core's prompt itself when the last wait is over.
    /// Elsewhere the remaster's prompts wait for keys (DOS runs its screens by itself, ~5 s each: PLAN Watch list).
    /// </summary>
    private void UpdateP1DefeatScreen(double now)
    {
        var prompt = _session is not null && _splash is null ? CurrentPrompt : null;
        if (prompt is null || !P1DefeatScreens || prompt.Kind is not (SessionPromptKind.PartyDefeat or SessionPromptKind.DefeatOutcome))
        {
            if (_p1Defeat is not null)
                StopEffects();
            _p1Defeat = null;
            return;
        }
        if (prompt.Kind == SessionPromptKind.PartyDefeat && (MapBoxesUp || MapWaitLines() is not null))
            return; // a dungeon step's boxes or report (requester 21, OK) close first, as on the Amiga before state 6
        if (prompt.Kind == SessionPromptKind.PartyDefeat)
        {
            AnswerPrompt(prompt, 1); // Core judges the members now; the screens show it in the Amiga's order
            return;
        }
        if (_prompts.ShownDefeat is not { } defeat)
            return;
        var effects = _assets!.Pack!.DosoundEffects;
        if (!ReferenceEquals(_p1Defeat?.Defeat, defeat))
        {
            Func<int, Data.Audio.SoundHold>? hold = null;
            try
            {
                if (_settings.Sound && effects?.TryLoadImage() is { } image)
                    hold = n => effects.Hold(image, n);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
            }
            if (!ReferenceEquals(_p1DefeatCounted, defeat))
            {
                _p1DefeatCounted = defeat;
                _p1DefeatScreensThisProgramRun++;
            }
            _p1Defeat = new P1DefeatScreen(_assets.Pack.DefeatScreen!, defeat, hold, _p1DefeatScreensThisProgramRun);
            _p1DefeatStart = now;
            _p1DefeatSoundsStarted = 0;
            _p1DefeatTone = 0;
        }
        var elapsed = now - _p1DefeatStart;
        if (UpdateP1DefeatTone(effects, elapsed))
        {
            _effects.Update();
            return;
        }
        var starts = _p1Defeat.SoundStarts();
        int? due = null;
        while (_p1DefeatSoundsStarted < starts.Count && starts[_p1DefeatSoundsStarted].At <= elapsed)
            due = starts[_p1DefeatSoundsStarted++].Sound;
        if (due is int sound && _settings.Sound)
            _effects.Play(effects, sound);
        else if (_p1DefeatSoundsStarted > 0 && _effects.Playing
                 && elapsed >= starts[_p1DefeatSoundsStarted - 1].End - (effects?.CallOverheadSeconds ?? 0))
            _effects.Stop(); // a key ended the sound at a step
        _effects.Update();
        if (_p1Defeat.At(elapsed).Done)
        {
            StopEffects();
            _p1Defeat = null;
            AnswerPrompt(prompt, 1);
        }
    }

    /// <summary>
    /// s154: the raw tone under the sinking door (h89 <c>+0x7ca</c> after line 2's wait, <c>+0x7f4</c> after the door's last
    /// step; pack <c>defeat_screen.raw_tone</c>), with the Sound switch on. True while it sounds (no other sound then).
    /// </summary>
    private bool UpdateP1DefeatTone(Data.Audio.AmigaDosoundEffects? effects, double elapsed)
    {
        var layout = _assets!.Pack!.DefeatScreen!;
        if (_p1Defeat is not { } screen || layout.TonePeriod is not int period || !_settings.Sound || _p1DefeatTone == 2)
            return false;
        var (start, end) = screen.Tone;
        if (elapsed < start)
            return false;
        if (elapsed >= end)
        {
            if (_p1DefeatTone == 1)
                _effects.Stop();
            _p1DefeatTone = 2;
            return false;
        }
        if (_p1DefeatTone == 0)
        {
            _effects.PlayTone(effects, layout.ToneChannel, period, layout.ToneVolume, end - start);
            _p1DefeatTone = 1;
        }
        return true;
    }

    /// <summary>Keys and the left button go to the defeat screens' input port as presses and releases (as the victory).</summary>
    private bool HandleP1DefeatKeys(KeyboardState keys, GamePadState pad, SessionPrompt prompt)
    {
        if (_p1Defeat is null || prompt.Kind != SessionPromptKind.DefeatOutcome)
            return false;
        var t = _updateSeconds - _p1DefeatStart;
        foreach (var _ in _input.NewKeys(keys))
            _p1Defeat.Key(t);
        foreach (var _ in _input.ReleasedKeys(keys))
            _p1Defeat.Release(t);
        if (_mouse.LeftPressed && _mouse.Position is not null)
            _p1Defeat.Key(t);
        if (_mouse.LeftReleased)
            _p1Defeat.Release(t);
        var (padPresses, padReleases) = ScenePadEvents(pad); // s182: A / the D-pad as a key's press and release
        for (var i = 0; i < padPresses; i++)
            _p1Defeat.Key(t);
        for (var i = 0; i < padReleases; i++)
            _p1Defeat.Release(t);
        return true;
    }

    /// <summary>Draws the defeat screens' frame (black while <c>decor.pic</c> loads); true when drawn.</summary>
    private bool DrawP1DefeatScreen(SessionPrompt prompt)
    {
        if (_p1Defeat is not { } screen || prompt.Kind != SessionPromptKind.DefeatOutcome || _assets is null
            || _spriteBatch is null || _pixel is null)
            return false;
        var frame = screen.At(_updateSeconds - _p1DefeatStart);
        if (frame.Black)
        {
            _spriteBatch.Draw(_pixel, new Rectangle(0, 0, 320, UiOverlays.ScreenBottom), Color.Black);
            return true;
        }
        var layout = _assets.Pack!.DefeatScreen!;
        Color? ink = _assets.Pack.EndingScene?.Palette is { Count: 16 } pens
            ? VirtualScreen.ParseColor(pens[layout.TextPen]!) : null;
        // s154: the lines in the window's z6font (h80 +0x84a, as the victory speech), centred in it.
        using var font = PixelFont.With(FontSources.Named(layout.Font, layout.FontFile));
        var drawn = UiOverlays.DrawDefeatScene(_spriteBatch, _pixel, _assets, SessionPromptKind.DefeatOutcome,
            new DefeatSceneFrame(frame.DoorHeight, frame.Face), screen.Row(frame), frame.Text, awaitsReturn: false,
            lineColour: ink, centredTop: layout.TextY, clickIsReturn: false);
        if (drawn && frame.Sign && _assets.DecorSorrySign is { } sign) // s154: the 5th defeat's sign (h32 +0x64c)
            _spriteBatch.Draw(sign, new Rectangle(layout.SignX, layout.SignY, sign.Width, sign.Height), Color.White);
        return drawn;
    }
}
