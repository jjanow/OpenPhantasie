using Microsoft.Xna.Framework.Input;
using OpenPhantasie.Core;
using OpenPhantasie.Data.Audio;

namespace OpenPhantasie.Client;

public partial class Game1
{
    private readonly Audio.SoundEffectPlayer _c64Play = new(); // s228: the C64's sounds in play (fights, swims, scripts, rings)
    private readonly Queue<string> _c64PlayQueue = new();
    private double _c64PlayBusyUntil;
    private GameSession? _c64PlayFor;
    private object? _c64FightFor;
    private int _c64RoundSerial;
    private long _c64LogSerial;
    private int _c64RingUses;

    /// <summary>
    /// s228: the C64 packs' sounds in play (<see cref="C64PlaySoundCues"/>, I's GRAPHICS.md 9.8), once a frame, with the
    /// Sound switch on and a pack whose <c>sid_sounds</c> hold the fights' sounds: a fight's start, each round shown on the
    /// remaster's combat screen, I / II's swims, dungeon scripts and Resurrections on the map, I's rings and wand used in
    /// town. The C64 waits for each sound, so sounds that come while one plays wait for its last write, then play in turn
    /// on one chip; the end screens' sounds stop them.
    /// </summary>
    private void UpdateC64PlaySounds(KeyboardState keys, GamePadState pad)
    {
        var session = _session;
        var sid = _assets?.Pack?.SidSounds;
        if (!ReferenceEquals(session, _c64PlayFor))
        {
            _c64PlayFor = session;
            _c64FightFor = session?.Combat;
            _c64RoundSerial = _combatUi.ShownRoundSerial;
            _c64LogSerial = session?.LogSequence ?? 0;
            _c64RingUses = _useItemDialog?.RingUses ?? 0;
            _c64PlayQueue.Clear();
        }
        if (session is null || !C64PlaySoundCues.HasFightSounds(sid, session.Game))
        {
            _c64PlayQueue.Clear();
            return;
        }
        var game = session.Game;
        var dungeon = session.CombatReturnMode == GameMode.Dungeon;
        void Add(IEnumerable<string> sounds)
        {
            foreach (var sound in sounds)
                _c64PlayQueue.Enqueue(C64PlaySoundCues.Key(game, dungeon, sound));
        }

        // A fight's start: OUTDOORS 0x0edf / DUNGEON 0x0f0f (AN ENCOUNTER); III has none.
        if (session.Mode == GameMode.Combat && session.Combat is { } fight && !ReferenceEquals(fight, _c64FightFor) && game != GameId.P3)
            Add(["encounter"]);
        _c64FightFor = session.Mode == GameMode.Combat ? session.Combat : null;
        if (_combatUi.ShownRoundSerial != _c64RoundSerial)
        {
            _c64RoundSerial = _combatUi.ShownRoundSerial;
            if (_combatUi.ShownRound is { } shown)
                Add(C64PlaySoundCues.ForRound(game, shown.Round, shown.Before, sid!.MarksFor));
            _c64LogSerial = session.LogSequence; // the round's lines are its own
        }
        if (session.LogSequence != _c64LogSerial)
        {
            var lines = session.LogSince(_c64LogSerial);
            _c64LogSerial = session.LogSequence;
            if (session.Mode != GameMode.Combat)
                Add(C64PlaySoundCues.ForLog(game, session.Mode, lines));
        }
        var rings = _useItemDialog?.RingUses ?? 0;
        if (rings != _c64RingUses)
        {
            if (game == GameId.P1 && session.Mode == GameMode.Town)
                for (var n = rings - _c64RingUses; n > 0; n--)
                    _c64PlayQueue.Enqueue("town.ring"); // TOWNR 0x503f
            _c64RingUses = rings;
        }

        if (!_settings.Sound || _c64SoundFor is not null)
        {
            _c64PlayQueue.Clear(); // the end screens' own sounds run (or the switch is off): none of these
            if (_c64Play.Playing)
                _c64Play.Stop();
            _c64PlayBusyUntil = 0;
            return;
        }
        // s229: the key click (C64PlaySoundCues.ClickKey): each key the game reads, on a chip with no sound running (the C64's
        // input routine empties the key buffer first, so keys pressed during a sound get no click).
        if (_c64PlayQueue.Count == 0 && _updateSeconds >= _c64PlayBusyUntil && ClickKeyNow(session, sid!, keys, pad) is { } click)
            _c64PlayQueue.Enqueue(click);
        if (_c64PlayQueue.Count > 0 && _updateSeconds >= _c64PlayBusyUntil)
        {
            var cues = C64PlaySoundCues.InTurn(sid!, _c64PlayQueue);
            _c64PlayQueue.Clear();
            if (cues.Count > 0)
            {
                _c64Play.Play(sid!.Score(cues, Random.Shared.NextDouble), sid.ClockHz);
                _c64PlayBusyUntil = _updateSeconds + cues[^1].At + sid.Length(cues[^1].Sound);
            }
        }
        _c64Play.Update();
    }

    /// <summary>
    /// s229: the click for this frame's keys, or null: a new key (the mouse's keys too) or pad button while the game's own
    /// screens read keys (no splash, Options, slot, import or reset screen), not while a name is typed (the C64
    /// GETs typed lines with no click); typed amounts still click `[~]`.
    /// </summary>
    private string? ClickKeyNow(GameSession session, SidSounds sid, KeyboardState keys, GamePadState pad)
    {
        if (_splash is not null || _options.IsOpen || _import.IsOpen || _update.IsOpen || _slotScreen.IsOpen || _reset.IsOpen
            || _dosDialog.IsOpen // the remaster's save-path dialog
            || OpenModal is ITypedEntry { TypingAlphabet: not null })
            return null;
        if (!_input.NewKeys(keys).Any(k => !IsModifierKey(k)) && !_input.NewButtons(pad).Any())
            return null;
        return C64PlaySoundCues.ClickKey(sid, session.Game, session.Mode, session.CombatReturnMode);
    }

    /// <summary>Shift, Ctrl, Alt and the system keys alone: the C64's GET returns nothing for them.</summary>
    private static bool IsModifierKey(Keys key) => key is Keys.LeftShift or Keys.RightShift or Keys.LeftControl
        or Keys.RightControl or Keys.LeftAlt or Keys.RightAlt or Keys.LeftWindows or Keys.RightWindows or Keys.CapsLock
        or Keys.NumLock or Keys.Scroll;
}
