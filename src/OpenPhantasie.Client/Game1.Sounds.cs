using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client;

public partial class Game1
{
    private GameSession? _soundsFor;
    private long _soundSerial;
    private long _cueSerial;
    private readonly Queue<int> _fieldSoundQueue = new();

    /// <summary>
    /// s136: the game's sounds outside combat (III: <see cref="P3Sounds"/>, FORMAT.md 7.2) from the pack's
    /// <c>sound_effects</c>, with the Sound switch on; a new session or a load starts from its current count, so nothing
    /// older plays. All started since the last frame play in turn (<see cref="Audio.SoundEffectPlayer.InTurn"/>); a
    /// combat or reward run started later cuts them, as a driver start does on the Amiga.
    /// s139: and the session's moments (<see cref="GameSession.Cue"/>) through the pack's <c>field_sounds</c> (II ST,
    /// FORMAT.md 7.4), paced as <c>0xb8f2</c> (<see cref="FieldSoundsWait"/>).
    /// </summary>
    private void UpdateGameSounds()
    {
        var session = _session;
        if (!ReferenceEquals(session, _soundsFor))
        {
            _soundsFor = session;
            _soundSerial = session?.SoundSequence ?? 0;
            _cueSerial = session?.CueSequence ?? 0;
            _logSerial = session?.LogSequence ?? 0;
            _alertDialogLines.Clear();
            _fieldSoundQueue.Clear();
            _delayedCues.Clear();
            _defeatSounds.Reset();
            _fade = null;
            _fadePending = false;
            _fadeFilter = null;
            _p2Alert.Close();
            _p2FieldBox.Close();
            if (_screen is not null)
            {
                _screen.Fade = null;
                _screen.Unfreeze();
            }
            return;
        }
        if (session is null)
            return;
        UpdateFieldCues(session);
        if (session.SoundSequence == _soundSerial)
            return;
        var sounds = session.SoundsSince(_soundSerial);
        _soundSerial = session.SoundSequence;
        _gameSoundRaised = true; // s157: cuts a carried title tune, with the Sound switch off too
        if (PlaysGameSounds(session.Game, _settings.Sound, _assets?.SoundEffects is not null))
            _effects.PlayInTurn(_assets!.SoundEffects, sounds);
    }

    private P4SoundLog? _p4SoundsFor;
    private long _p4SoundSerial;
    private readonly Audio.P4EffectCues _p4Cues = new();

    /// <summary>
    /// s152: Phantasie IV's sound moments (<see cref="P4Party.Sounds"/>) through the pack's ADPCM effects
    /// (<see cref="Data.Audio.X68kAdpcmEffects"/>, X68000), with the Sound switch on: the moments noted since the last
    /// frame play in turn, after the running effect (<see cref="Audio.P4EffectCues"/>). A new game or a load starts from
    /// its current count, so nothing older plays.
    /// </summary>
    private void UpdateP4Sounds()
    {
        var log = _p4?.Game.Party.Sounds;
        if (!ReferenceEquals(log, _p4SoundsFor))
        {
            _p4SoundsFor = log;
            _p4SoundSerial = log?.Sequence ?? 0;
            _p4Cues.Reset();
            return;
        }
        if (log is null || _assets?.Pack?.AdpcmEffects is not { } pack)
            return;
        if (log.Sequence != _p4SoundSerial)
        {
            var effects = _p4Cues.EffectsFor(pack, log.Since(_p4SoundSerial));
            _p4SoundSerial = log.Sequence;
            if (_settings.Sound)
                _p4Cues.Enqueue(effects);
        }
        if (!_settings.Sound)
            _p4Cues.TakeWaiting();
        else if (_p4Cues.Waiting.Count > 0 && !_effects.Playing)
            _effects.Play(pack, _p4Cues.TakeWaiting());
        _effects.Update();
    }

    /// <summary>Whether the session's own sounds play: III with the Sound switch on and a pack with its sounds.</summary>
    public static bool PlaysGameSounds(GameId game, bool soundOn, bool packHasSounds) =>
        game == GameId.P3 && soundOn && packHasSounds;

    private void UpdateFieldCues(GameSession session)
    {
        var st = _assets?.Pack?.StDosoundEffects;
        var field = _assets?.Pack?.FieldSounds;
        // s140: the picker's click comes first: the ST clicks at 0x25b4, then plays 22 at 0x262c (one frame here).
        if (_transportDialog.Picks != _transportPicks)
        {
            _transportPicks = _transportDialog.Picks;
            PlayClick(); // 0x25b4: after form_do, the cancel too
            // s162: I Amiga's list window plays the tone after each gadget (h26 +0xdb6, the cancel too); s158: then 22 once a
            // town is picked (h26 +0xe4a, before its fade). Each blocks: they play in turn.
            if (P1MapCues && _assets!.Pack!.CueSounds!.ListTone is int listTone)
                PlayP1CueInTurn(listTone);
            if (P1MapCues && _assets!.Pack!.CueSounds!.Transport is int transport && _transportDialog.LastPickWasTown)
                PlayP1CueInTurn(transport);
        }
        if (_guildAddDialog is { } add && add.MoveQuestions != _moveQuestions)
        {
            _moveQuestions = add.MoveQuestions;
            _alertDialogLines.Add(GuildHall.AddMoveConfirmText[0]); // alert 6 (0xd51a): "Not in the town ..."
        }
        if (session.CueSequence != _cueSerial)
        {
            var cues = session.CuesSince(_cueSerial);
            _cueSerial = session.CueSequence;
            // s156: I Amiga: 12 after a Resurrection cast outside a fight (h38 +0x466); s165: after its requester-23 box
            // "%s IS RESURRECTED" (h44 +0x466) over the map, which plays first (StartP1ResurrectedBox).
            if (P1MapCues && _assets!.Pack!.CueSounds!.Resurrected is int raised && cues.Contains(FieldCues.FieldResurrected)
                && !StartP1ResurrectedBox(session, raised) && _settings.Sound)
                _effects.Play(_assets.Pack.DosoundEffects, raised);
            if (field is not null)
                foreach (var cue in cues)
                {
                    // s158: a cue's box (the town casts' "D O N E !", 0x1628(9)) after its beep.
                    if (field.Alerts is { Look: not null } boxes && boxes.CueBoxes.TryGetValue(cue, out var cueBox))
                        _p2Alert.Offer(cueBox);
                    if (field.SoundFor(cue) is not int sound)
                        continue;
                    if (field.Delays.TryGetValue(cue, out var delay)) // s141: after a timed box (field Resurrection)
                        _delayedCues.Add(new DelayedCue(sound, _updateSeconds + delay.Ms(PlayerSpeedMs) / 1000.0,
                            delay.AfterMs / 1000.0));
                    else
                        PlayFieldSound(sound, FieldSoundsWait(field, PlayerSpeedMs));
                }
            // s162: I Amiga's cues that beep before their alert (pack cue_sounds.cues: town.cast_done, D O N E !).
            if (P1MapCues && _assets!.Pack!.CueSounds!.Cues is { Count: > 0 } beeps)
                foreach (var cue in cues)
                    if (beeps.TryGetValue(cue, out var beep))
                    {
                        PlayP1CueInTurn(beep);
                        if (cue == FieldCues.TownCastDone) // s163: then requester 0 "D O N E !" (h26 +0x38)
                            OpenP1TownHeld("done");
                    }
            if (cues.Contains(FieldCues.Transport) && _assets?.Pack?.TransportFade is { } fade
                && (!fade.OriginalScreensOnly || CurrentGameSettings.OriginalScreens))
            {
                if (fade.AfterSound)
                {
                    // s162: I Amiga's h26 +0xe30 fades only after the list's tone and 22 have ended (h89 blocks).
                    _fadePending = true;
                    _p1Holds.Defer(_updateSeconds, 0, () =>
                    {
                        _fadePending = false;
                        if (_session?.Mode == GameMode.Town)
                            StartTransportFade(fade);
                    });
                }
                else
                    StartTransportFade(fade); // s140: 0x260a's fade follows sound 22
            }
        }
        if (_fade is null && !_fadePending && _screen is { Frozen: true })
            _screen.Unfreeze(); // s158: a pick that moved nobody (no Transport cue): the frame goes on
        // s140: the alert boxes' beep (0x1628 / 0x1668): the session's new lines and the lines dialogs returned.
        var logLines = session.LogSequence != _logSerial ? session.LogSince(_logSerial) : [];
        _logSerial = session.LogSequence;
        if (field?.Alerts is { } alerts && (logLines.Count > 0 || _alertDialogLines.Count > 0))
        {
            if (!ReferenceEquals(_alertBeeps?.Alerts, alerts))
                _alertBeeps = new P2AlertBeeps(alerts);
            for (var n = _alertBeeps.BeepsFor(logLines, _alertDialogLines, session.Mode); n > 0; n--)
                PlayFieldSound(_alertBeeps.Sound, FieldSoundsWait(field, PlayerSpeedMs));
            if (alerts.Look is not null) // s158: then form_alert's box, the disks' text
                foreach (var box in _alertBeeps.BoxesFor(logLines, _alertDialogLines, session.Mode))
                    _p2Alert.Offer(box);
        }
        else if (P1MapCues && _assets!.Pack!.CueSounds!.Alerts is { } p1Alerts && (logLines.Count > 0 || _alertDialogLines.Count > 0))
        {
            // s158: I Amiga's alerts beep first (h26 +0x6e / +0x1a8: h89 +0xe0, then the requester); the beep blocks.
            if (!ReferenceEquals(_alertBeeps?.Alerts, p1Alerts))
                _alertBeeps = new P2AlertBeeps(p1Alerts);
            // s165: in turn after a list tone of the same answer (h26 +0x614 / +0x8f6 play before the pick runs).
            if (_alertBeeps.BeepsFor(logLines, _alertDialogLines, session.Mode) > 0)
                PlayP1CueInTurn(_alertBeeps.Sound);
        }
        _alertDialogLines.Clear();
        if (field is not null)
            PlayDueCues(field);
        UpdateTransportFade();
        if (_fieldSoundQueue.Count > 0 && !_effects.Playing)
        {
            if (st is not null && _settings.Sound)
                _effects.Play(st, _fieldSoundQueue.Dequeue());
            else
                _fieldSoundQueue.Clear();
        }
    }

    /// <summary>s141: a cue sound waiting for its box's hold (<see cref="FieldCueDelay"/>); a key ends the hold, and only
    /// <see cref="After"/> seconds (the box's redraw) remain.</summary>
    private readonly record struct DelayedCue(int Sound, double Due, double After);

    private readonly List<DelayedCue> _delayedCues = new();

    /// <summary>s141: a key or a click ends a timed box's hold (<c>0x16cbe</c>'s <c>evnt_multi</c>): what remains is the
    /// redraw after it.</summary>
    private void CutDelayedCues()
    {
        for (var i = 0; i < _delayedCues.Count; i++)
            _delayedCues[i] = _delayedCues[i] with { Due = Math.Min(_delayedCues[i].Due, _updateSeconds + _delayedCues[i].After) };
    }

    private void PlayDueCues(FieldSounds field)
    {
        for (var i = 0; i < _delayedCues.Count;)
        {
            if (_delayedCues[i].Due > _updateSeconds)
            {
                i++;
                continue;
            }
            var sound = _delayedCues[i].Sound;
            _delayedCues.RemoveAt(i);
            PlayFieldSound(sound, FieldSoundsWait(field, PlayerSpeedMs));
        }
    }

    private long _logSerial;
    private int _transportPicks, _moveQuestions;
    private readonly List<string> _alertDialogLines = new();
    private P2AlertBeeps? _alertBeeps;
    private readonly P2DefeatSounds _defeatSounds = new();
    private StTransportFade? _fade;
    private bool _fadePending; // s162: I Amiga's fade waits for the sounds before it (frame kept frozen meanwhile)
    private double _fadeStart;
    private TransportFadeFilter? _fadeFilter;

    /// <summary>The player's COMBAT SPEED as the pack's Speed word (ms), or null without the ST's table.</summary>
    private int? PlayerSpeedMs => _assets?.Pack?.StCombatLines?.SpeedMs((int)CurrentGameSettings.CombatSpeed);

    /// <summary>
    /// s140: start II ST's sound <paramref name="sound"/> as <c>0xb8b0</c> does: after the running one when
    /// <paramref name="waits"/> (<c>0xb8f2</c> at Speed ≥ 9), else at once, cutting it off. Nothing with the Sound switch off.
    /// </summary>
    private void PlayFieldSound(int sound, bool waits)
    {
        if (!_settings.Sound || _assets?.Pack?.StDosoundEffects is not { } st || sound < 0)
            return;
        if (waits)
            _fieldSoundQueue.Enqueue(sound);
        else
        {
            _fieldSoundQueue.Clear(); // 0xb8f2 at fast: the new script cuts the running one off
            _effects.Play(st, sound);
        }
    }

    /// <summary>s140: II ST's click after a GEM pick (<c>0xb7e6</c>, the pack's 33; Sound switch on).</summary>
    private void PlayClick()
    {
        if (_assets?.Pack?.FieldSounds is { Click: int click } field)
            PlayFieldSound(click, FieldSoundsWait(field, PlayerSpeedMs));
    }

    /// <summary>s140: a line a dialog returned this frame, for the alerts' beep (<see cref="P2AlertBeeps"/>).</summary>
    private void NoteAlertLine(string? line)
    {
        if (!string.IsNullOrWhiteSpace(line)
            && (_assets?.Pack?.FieldSounds?.Alerts is not null || P1MapCues && _assets?.Pack?.CueSounds?.Alerts is not null))
            _alertDialogLines.Add(line);
    }

    /// <summary>
    /// s140: II ST's defeat scene sounds (<see cref="P2DefeatSounds"/>) for the frame the scene draws: the door's at once,
    /// the others after the running sound (the scene's Speed 20 waits whatever the player picked).
    /// </summary>
    private void UpdateDefeatSounds(SessionPromptKind kind, DefeatSceneFrame frame, int fatesShown, bool bannerShown = true)
    {
        if (_assets?.Pack?.FieldSounds?.Defeat is not { } defeat)
            return;
        foreach (var step in _defeatSounds.Observe(defeat, kind, frame, fatesShown, _updateSeconds, bannerShown))
            PlayFieldSound(step.Sound, waits: !step.Now && FieldSoundsWait(_assets.Pack.FieldSounds, defeat.SpeedMs));
    }

    /// <summary>
    /// s165: II ST's defeat page holds "YOUR PARTY HAS BEEN DEFEATED" 3000 ms and goes on by itself (<c>0x12a16</c> →
    /// <c>0x16cbe</c>(3000), then the ghost row, 23 and the astral line): with the ST pack's defeat scene the page is
    /// left as its hold ends (<see cref="P2DefeatSounds.HoldOver"/>, the ghosts' moment). Elsewhere it waits for a key.
    /// </summary>
    private bool P2DefeatHolds => _session is { Game: GameId.P2 } && _assets?.Pack?.FieldSounds?.Defeat is not null
                                 && _assets.Decor is not null;

    private void UpdateP2DefeatHold()
    {
        if (P2DefeatHolds && _splash is null && OpenModal is null && CurrentPrompt is { Kind: SessionPromptKind.PartyDefeat } prompt
            && _defeatSounds.HoldOver)
            AnswerPrompt(prompt, 1);
    }

    /// <summary>
    /// s165: while the DEFEATED line's hold runs, any key or a click ends it (<c>0x16cbe</c>: <c>evnt_multi</c> keyboard |
    /// button | timer), not only the keys that answer the prompt. True when it answered.
    /// </summary>
    private bool HandleP2DefeatHoldKeys(KeyboardState keys, SessionPrompt prompt)
    {
        if (!P2DefeatHolds || prompt.Kind != SessionPromptKind.PartyDefeat || !_defeatSounds.HoldRunning
            || !_input.NewKeys(keys).Any() && !_mouse.LeftPressed)
            return false;
        AnswerPrompt(prompt, 1);
        return true;
    }

    /// <summary>
    /// s165: a dialog's <paramref name="line"/> that II ST shows in its timed field box (a new cue whose
    /// <see cref="FieldCueDelay.BoxPalettes"/> names the mode: field Resurrection, <c>0x92c8</c> → <c>0xa18c</c>) opens
    /// <see cref="P2FieldBox"/>, held Speed + tree 40's hold (<c>0xa16a</c>). True when it did (the line then stays off the
    /// message line, as the ST writes none there).
    /// </summary>
    private bool OfferP2FieldBox(GameSession session, string line)
    {
        if (_assets?.Pack?.FieldSounds is not { } field || _assets.Pack.StCombatLines?.EndBoxes is not { } boxes
            || session.CueSequence == _cueSerial)
            return false;
        var mode = session.Mode.ToString().ToLowerInvariant();
        if (!session.CuesSince(_cueSerial).Any(c => field.Delays.TryGetValue(c, out var d) && d.BoxPalettes?.ContainsKey(mode) == true))
            return false;
        _p2FieldBox.Open(line, mode, _updateSeconds, (PlayerSpeedMs ?? 0) + boxes.OneLine.HoldMs);
        return true;
    }

    /// <summary>s165: the palette <see cref="P2FieldBox"/> takes its pens from in <paramref name="mode"/>, or null.</summary>
    private IReadOnlyList<string>? P2FieldBoxPalette(string mode) =>
        _assets?.Pack?.FieldSounds?.Delays.Values.Select(d => d.BoxPalettes?.GetValueOrDefault(mode)).FirstOrDefault(n => n is not null)
            is { } name ? _assets.Pack.Palettes?.GetValueOrDefault(name) : null;

    private void StartTransportFade(StTransportFade fade)
    {
        _fade = fade;
        _fadeStart = _updateSeconds;
        _fadeFilter = new TransportFadeFilter(fade.Palette);
        if (_screen is { Frozen: false })
            _screen.Freeze(); // s158: frozen at the pick already (FreezeForTransport); this frame otherwise
    }

    /// <summary>
    /// s158: II ST's TRANSPORTATION fades the old town's screen with the picker still drawn (<c>0x19158</c> only clears the
    /// button's SELECTED bit; s162: I Amiga's list requester too, ended by h8 <c>+0x12ea</c> after the fade): as a town is picked (before Core moves the party and before this frame is drawn) the last
    /// frame is kept on screen. Only with the pack's fade; the cue starts the fade next frame, or the frame is let go.
    /// </summary>
    private void FreezeForTransport()
    {
        if (_assets?.Pack?.TransportFade is not null)
            _screen?.Freeze();
    }

    /// <summary>
    /// s158: an alert box's fill and ink: registers <see cref="StAlertLook.FillPen"/> / <see cref="StAlertLook.InkPen"/>
    /// (VDI 0 / 1) of the palette <paramref name="mode"/> shows (<see cref="StAlertLook.Palettes"/>); black / white
    /// without one.
    /// </summary>
    private (Color Fill, Color Ink) StAlertColours(StAlertLook look, GameMode mode) =>
        StAlertColours(look, mode, _assets?.Pack?.Palettes);

    public static (Color Fill, Color Ink) StAlertColours(StAlertLook look, GameMode mode,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? palettes)
    {
        ArgumentNullException.ThrowIfNull(look);
        if (look.Palettes.TryGetValue(mode.ToString().ToLowerInvariant(), out var name)
            && palettes?.GetValueOrDefault(name) is { } registers
            && registers.Count > Math.Max(look.FillPen, look.InkPen))
            return (VirtualScreen.ParseColor(registers[look.FillPen]), VirtualScreen.ParseColor(registers[look.InkPen]));
        return (Color.Black, Color.White);
    }

    /// <summary>True while II ST's (s162: and I Amiga's, h26 <c>+0xe30</c>) TRANSPORTATION fade runs: no key is read then
    /// (<c>0x260a</c>: Setcolor and Vsync only; the Amiga: SetRGB4 and WaitTOF only).</summary>
    private bool TransportFading => _fade is not null;

    /// <summary>
    /// s140: the fade's frame (<see cref="TransportFadeFilter"/> on the virtual screen); at its end <c>0xb9e2</c> silences
    /// the running sound (sound 22 runs 7.7 s, the fade 6.2 s at 60 Hz).
    /// </summary>
    private void UpdateTransportFade()
    {
        if (_fade is not { } fade || _fadeFilter is not { } filter)
            return;
        var t = _updateSeconds - _fadeStart;
        if (t >= fade.TotalSeconds && _screen is { Frozen: true } && _session?.Mode == GameMode.Town)
            _screen.Unfreeze(); // s163: the new town is drawn under the DisplayBeeps' flash (filmed)
        if (t >= fade.EndSeconds || _session?.Mode != GameMode.Town)
        {
            _fade = null;
            _fadeFilter = null;
            if (_screen is not null)
            {
                _screen.Fade = null;
                _screen.Unfreeze();
            }
            if (fade.SilencesSound && t >= fade.TotalSeconds)
                _effects.Stop();
            return;
        }
        filter.At(fade, t);
        if (_screen is not null)
            _screen.Fade = filter;
    }

    /// <summary>s139: the pack's sounds for <paramref name="cues"/> in order; a moment the pack lacks is skipped.</summary>
    public static IReadOnlyList<int> FieldSoundsFor(FieldSounds map, IEnumerable<string> cues)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(cues);
        return [.. cues.Select(map.SoundFor).OfType<int>()];
    }

    /// <summary>
    /// s139: whether a field sound waits for the running one (II ST <c>0xb8f2</c>: Speed <c>[0x1fefa]</c> ≥ 9, the game's
    /// COMBAT SPEED normal 10 / slow 1000 ms) or cuts it off (fast, 5 ms). No Speed table: in turn. Not modelled `[~]`:
    /// the ST skips the wait after sound 18 (<c>[0x21a30]</c>), and the queue here waits for whatever the effect player
    /// runs (a combat or reward run too), not only the field sound before.
    /// </summary>
    public static bool FieldSoundsWait(FieldSounds map, int? speedMs) =>
        map.WaitSpeedMs is not int wait || speedMs is not int speed || speed >= wait;
}
