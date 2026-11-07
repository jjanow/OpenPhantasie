using Microsoft.Xna.Framework.Input;
using OpenPhantasie.Core;

namespace OpenPhantasie.Client;

public partial class Game1
{
    private SessionPrompt? _p1CuePrompt;
    private readonly P1MapHolds _p1Holds = new();
    private P1MonsterShapeCache? _p1Shapes;
    private bool _p1FightLoading;
    private readonly P1MoveQueue _p1QueuedMoves = new();
    private (int Dx, int Dy)? _p1MoveNow;

    /// <summary>I Amiga's map cues (pack <c>cue_sounds</c>) apply: I, AMIGA SCREENS on and a pack with them.</summary>
    private bool P1MapCues => _session is { Game: GameId.P1 } && CurrentGameSettings.OriginalScreens
        && _assets?.Pack?.CueSounds is not null && _assets.Pack.DosoundEffects is not null;

    /// <summary>
    /// s153 / s154: I Amiga's question sounds (pack <c>cue_sounds</c>), under AMIGA SCREENS: 19 when "A TOWN. ENTER?" /
    /// "A DUNGEON. ENTER?" opens, 27 when the BLACK KNIGHTS menu opens; the question shows after the sound's hold
    /// (<see cref="P1MapHolds.HoldPrompt"/>; 0 with the Sound switch off). The step tone plays at the step itself
    /// (<see cref="StepAfterTone"/>). Called once a frame and right after a deferred step.
    /// </summary>
    private void UpdateP1CueSounds()
    {
        var prompt = _session is not null && !_combatUi.IsPresenting ? _prompts.Current : null;
        if (!P1MapCues || prompt is null)
        {
            _p1CuePrompt = prompt;
            return;
        }
        if (SamePrompt(prompt, _p1CuePrompt))
            return;
        _p1CuePrompt = prompt;
        var cues = _assets!.Pack!.CueSounds!;
        int? sound = null;
        if (prompt.Kind == SessionPromptKind.OverworldEntry)
            sound = cues.OverworldEntry;
        else if (prompt.Kind == SessionPromptKind.WildernessEncounter
                 && prompt.Lines.Any(l => l.Contains(WildernessEncounter.BlackKnightsTitle, StringComparison.Ordinal)))
            sound = cues.BlackKnights;
        if (sound is not int n)
            return;
        if (_settings.Sound)
            _effects.Play(_assets.Pack.DosoundEffects, n);
        _p1Holds.HoldPrompt(_updateSeconds, P1SoundSeconds(n), prompt);
    }

    private static bool SamePrompt(SessionPrompt a, SessionPrompt? b) =>
        b is not null && (ReferenceEquals(a, b) || (a.Kind == b.Kind && a.Lines.SequenceEqual(b.Lines)));

    /// <summary>The prompt held back by a question sound's hold now (<see cref="CurrentPrompt"/> hides it).</summary>
    private bool P1PromptHeld(SessionPrompt? prompt) =>
        prompt is not null && _p1Holds.PromptHeld(prompt, _updateSeconds, (a, b) => SamePrompt((SessionPrompt)a, b as SessionPrompt));

    /// <summary>
    /// s154: I Amiga's step tone before a map move (h65 <c>+0x158</c>, the tone h89 <c>+0xd2</c> = pack sound 31; the
    /// player blocks): the tone plays and <paramref name="move"/> runs after its hold (at once with the Sound switch off).
    /// The tone comes for every move, a refused one too (the Amiga plays it before h59 / h41 test the cell). False when
    /// the cue does not apply (the caller moves at once).
    /// </summary>
    private bool StepAfterTone(Action move)
    {
        if (!P1MapCues || _assets!.Pack!.CueSounds!.Step is not int tone)
            return false;
        if (_settings.Sound)
            _effects.Play(_assets.Pack.DosoundEffects, tone);
        _p1Holds.Defer(_updateSeconds, P1SoundSeconds(tone), () =>
        {
            move();
            UpdateP1CueSounds(); // 19 / 27 right after the move, so the question never shows before its sound
        });
        return true;
    }

    /// <summary>
    /// s156: I Amiga's tone before an Options pick on the map (h65 <c>+0x158</c> plays it for every code 0–8 its input
    /// routine returns: the moves 5–8 and the Options menu's picks 0–4 = Inspect, Health, Cast Spell, Use potions, Look; the
    /// player blocks, then h47 <c>+0x7a</c> hands 0–4 to h50 / h41 <c>+0x1eac</c>): on the overworld or in the dungeon the
    /// tone plays and <paramref name="open"/> (the remaster's dialog for that pick, by its key or the map bar's item) runs
    /// after its hold; a refusal it returns goes on the message line, an opened dialog's help on the help line. False when
    /// the cue does not apply (the caller opens at once).
    /// </summary>
    private bool MapOptionAfterTone(GameSession s, Func<string?> open)
    {
        if (s.Mode is not (GameMode.Overworld or GameMode.Dungeon))
            return false;
        return StepAfterTone(() =>
        {
            if (!ReferenceEquals(_session, s) || s.Mode is not (GameMode.Overworld or GameMode.Dungeon))
                return;
            if (open() is { } refused)
                SetFeedback(refused, HelpForMode(s.Mode));
            if (OpenModal is { } opened)
                SetFeedback(_message, opened.Help);
        });
    }

    /// <summary>
    /// The fight's screen after a map FIGHT: under AMIGA SCREENS with I's pack, the monster pictures' disk read first
    /// (s154, <see cref="CombatFieldLayout.ShapeLoadSeconds"/>, only when a group is not in the three-slot cache), the map
    /// still up meanwhile (<see cref="ViewMode"/>).
    /// </summary>
    private void StartFightScreen(GameSession s)
    {
        var seconds = P1ShapeLoadSeconds(s);
        if (seconds <= 0)
        {
            _combatUi.Start(s);
            return;
        }
        _p1FightLoading = true;
        _p1Holds.Defer(_updateSeconds, seconds, () =>
        {
            _p1FightLoading = false;
            if (ReferenceEquals(_session, s) && s.Mode == GameMode.Combat && !_combatUi.IsPresenting)
                _combatUi.Start(s);
        });
    }

    private double P1ShapeLoadSeconds(GameSession s)
    {
        if (s.Game != GameId.P1 || !CurrentGameSettings.OriginalScreens || s.Combat is not { } combat
            || _assets?.Pack?.CombatField is not { ShapeLoadSeconds: > 0, ShapeCacheSlots: > 0 } layout)
            return 0;
        _p1Shapes ??= new P1MonsterShapeCache(layout.ShapeCacheSlots); // the program run's cache (h54 +2)
        var groups = combat.Ranks.Where(r => r.Members.Count > 0).Select(r => r.Members[0].Id).ToList();
        return _p1Shapes.Load(groups) > 0 ? layout.ShapeLoadSeconds : 0;
    }

    /// <summary>
    /// Once a frame: a deferred step or fight whose hold is over runs. True while a hold runs: the map takes no input (the
    /// moves pressed meanwhile are kept in order and made one at a time after it, each with its own tone, as the Amiga's
    /// port keeps every event and h65 takes one a call: <see cref="P1MoveQueue"/>).
    /// </summary>
    private bool UpdateP1Holds(GameSession s, KeyboardState keys, GamePadState pad)
    {
        _p1Holds.Update(_updateSeconds);
        _p1MoveNow = null;
        if (_p1Holds.Busy(_updateSeconds))
        {
            if (s.Mode is GameMode.Overworld or GameMode.Dungeon
                && _input.PressedFieldMove(keys, pad, CurrentGameSettings.DiagonalMoves) is { } move)
                _p1QueuedMoves.Press(move);
            return true;
        }
        // Made this frame by the map's input; a question, box or dialog that opened takes the queue's place (dropped).
        _p1MoveNow = _p1QueuedMoves.Next(mapWaits: s.Mode is GameMode.Overworld or GameMode.Dungeon
            && CurrentPrompt is null && OpenModal is null && !DosPagesUp && !MapBoxesUp && MapWaitLines() is null);
        return false;
    }

    /// <summary>
    /// s158: an I Amiga cue that blocks (h89's player, FORMAT.md "Sound effects"): the sound plays with the Sound switch on
    /// and the input waits for its hold (<see cref="P1SoundSeconds"/>; nothing with the switch off, user s129).
    /// </summary>
    private void PlayP1Cue(int sound)
    {
        if (!_settings.Sound || _assets?.Pack?.DosoundEffects is not { } effects)
            return;
        _effects.Play(effects, sound);
        _p1Holds.Defer(_updateSeconds, P1SoundSeconds(sound), static () => { });
    }

    private string? _p1SheetToneState;

    /// <summary>
    /// s158: I Amiga's tone after the character sheet's answers (pack <c>cue_sounds.sheet_tone</c>, h89 <c>+0xd2</c>): h29
    /// <c>+0xcf4</c> after any button of requester 14, <c>+0xe86</c> / <c>+0xf52</c> / <c>+0x934</c> after each view's EXIT,
    /// h23 <c>+0x734</c> after each gadget of the SPELLS view's requester 12, h23 <c>+0xc56</c> after Guild › New's PURGE /
    /// KEEP. Played when the drawn step changes from one of these to anything else (the remaster's step is already
    /// shown; the Amiga acts after the tone `[~]`). Once a frame.
    /// </summary>
    private void UpdateP1SheetTone()
    {
        var state = _session is { } s ? P1SheetToneState(s) : null;
        var before = _p1SheetToneState;
        _p1SheetToneState = state;
        if (before is null || state == before || !P1MapCues || _assets!.Pack!.CueSounds!.SheetTone is not int tone)
            return;
        PlayP1Cue(tone);
    }

    /// <summary>The sheet step drawn as an Amiga requester now (null: none), for <see cref="UpdateP1SheetTone"/>.</summary>
    private string? P1SheetToneState(GameSession s)
    {
        if (P1Requesters is not { Sheet: not null })
            return null;
        if (_inspectDialog.SheetMember is not null)
            return "sheet";
        if (_inspectDialog.PageShown is var (page, _))
            return page == 0 ? "spells:" + _inspectDialog.AmigaSpellFirst : "page:" + page;
        if (P1RolledSheetFor(s) is not null && _guildNewDialog.RolledMember is { } rolled)
            return "rolled:" + System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(rolled);
        return null;
    }

    /// <summary>Drops the holds (a load, the main menu).</summary>
    private void ResetP1Holds()
    {
        _p1Holds.Reset();
        _p1FightLoading = false;
        _p1QueuedMoves.Clear();
        _p1MoveNow = null;
        _p1CuePrompt = null;
        _cycleMapAt = null; // a load redraws the map (h2 +0x328): the water's stored colours
    }
}
