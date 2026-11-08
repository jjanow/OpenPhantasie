using System.Text.RegularExpressions;
using Microsoft.Xna.Framework;
using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client;

/// <summary>
/// s140: the sounds of II ST's party-defeat scene (FORMAT.md 7.4, <c>endgame.md</c> 8; the pack's
/// <see cref="FieldSounds.Defeat"/>), from what the remaster's scene shows frame by frame (<see cref="DefeatScene"/>):
/// <list type="bullet">
/// <item>ghosts (23, <c>0x12d5e</c>): <see cref="FieldDefeatSounds.GhostsAfterMs"/> after the defeat page first shows (the
/// ST: the first line's 3000 ms hold, the ghost row, then the sound; traced s141: 3060 ms), or as soon as the page is left
/// earlier (a key ends the ST's hold). s156: the ghost row shows from then on (<see cref="GhostsShown"/>: <c>0x12d4c</c>
/// line 1 and its hold, <c>0x12d54</c> the row, <c>0x12d5e</c> 23), the row empty before;</item>
/// <item>door (22, <c>0x12d86</c>): the door starts to sink, on the sign or the outcome page; it waits for the running sound
/// as every sound of the scene does (s141: on the ST 23 has long ended by then, the astral line's 3000 ms hold between);</item>
/// <item>demon (20, <c>0x12e36</c>): the Great Demon's face first shows (the outcome page);</item>
/// <item>fate (21, <c>0x126d2</c>): each member's fate line, DESTROYED included (s141 trace: the ST prints the line within
/// 15 ms of 21's start, both after the running sound; the remaster shows the line at once and 21 waits `[~]`).</item>
/// </list>
/// The scene runs at Speed 20 (<see cref="FieldDefeatSounds.SpeedMs"/>): every sound but the door's waits for the one
/// before, whatever COMBAT SPEED the player picked. No MonoGame types: tests drive it with the scene's state.
/// </summary>
public sealed class P2DefeatSounds
{
    /// <summary>A sound to start: <see cref="Now"/> = at once (cutting off what runs), else after the running one.</summary>
    public readonly record struct Step(int Sound, bool Now);

    private SessionPromptKind? _kind;
    private bool _face;
    private int _fates;
    private double? _ghostsDue;

    /// <summary>
    /// s156: false on the defeat page until its first line's hold is over (or a key left the page): the ST draws the ghost
    /// row only then (<c>0x12b5e</c>, right before sound 23). True on every other page.
    /// </summary>
    public bool GhostsShown { get; private set; } = true;

    /// <summary>
    /// s165: the defeat page's first line is up and its hold runs (<c>0x12a16</c>: the bottom line, then <c>0x16cbe</c>(3000)):
    /// any key or a click ends it early (<c>0x16cbe</c>'s <c>evnt_multi</c> keyboard | button | timer; <c>0x16d56</c> first
    /// drains what was queued before).
    /// </summary>
    public bool HoldRunning => _kind == SessionPromptKind.PartyDefeat && _ghostsDue is not null;

    /// <summary>
    /// s165: the defeat page's first line has had its hold (the ghosts' moment, <see cref="FieldDefeatSounds.GhostsAfterMs"/>)
    /// and the page is still up: the ST goes on by itself (<c>0x12d54</c> the ghost row, <c>0x12d5e</c> 23, <c>0x12d6a</c> the
    /// astral line), so the front end leaves the page.
    /// </summary>
    public bool HoldOver => _kind == SessionPromptKind.PartyDefeat && _holdOver;

    private bool _holdOver;

    /// <summary>
    /// The sounds due this frame for the defeat page <paramref name="kind"/> showing <paramref name="frame"/> at
    /// <paramref name="now"/> seconds, with <paramref name="fatesShown"/> fate lines printed
    /// (<see cref="DefeatScene.FatesShown"/>). Any other page resets. <paramref name="bannerShown"/> (s165): the defeat page
    /// shows its line "YOUR PARTY HAS BEEN DEFEATED" (a step's report lines paged in front of it hold the line's hold back).
    /// </summary>
    public IReadOnlyList<Step> Observe(FieldDefeatSounds map, SessionPromptKind kind, DefeatSceneFrame frame, int fatesShown,
        double now = 0, bool bannerShown = true)
    {
        ArgumentNullException.ThrowIfNull(map);
        var steps = new List<Step>();
        var previous = _kind;
        if (kind is not (SessionPromptKind.PartyDefeat or SessionPromptKind.DefeatDemonSign or SessionPromptKind.DefeatOutcome))
        {
            Reset();
            return steps;
        }
        _kind = kind;
        if (kind == SessionPromptKind.PartyDefeat)
        {
            if (previous != SessionPromptKind.PartyDefeat)
            {
                _bannerSeen = false;
                _holdOver = false;
                GhostsShown = false;
            }
            if (bannerShown && !_bannerSeen)
            {
                _bannerSeen = true; // s165: the line's hold starts as the line shows
                _ghostsDue = now + map.GhostsAfterMs / 1000.0;
            }
            if (_ghostsDue is { } due && now >= due)
            {
                Ghosts(steps, map);
                _holdOver = true;
            }
            _face = false;
            _fates = 0;
            return steps;
        }
        if (_ghostsDue is not null)
            Ghosts(steps, map); // the page was left before the line's hold ran out: a key ended it
        if (previous == SessionPromptKind.PartyDefeat)
            Add(steps, map.Door, now: false); // DefeatSceneClock starts the door on this change
        if (kind == SessionPromptKind.DefeatOutcome && frame.ShowBehindDoor && previous is not null)
        {
            if (!_face)
            {
                _face = true;
                Add(steps, map.Demon, now: false);
            }
            for (; _fates < fatesShown; _fates++)
                Add(steps, map.Fate, now: false);
        }
        else if (kind == SessionPromptKind.DefeatOutcome && previous is null)
            (_face, _fates) = (true, fatesShown); // met mid-way (a load): nothing replays
        return steps;
    }

    /// <summary>No defeat page shows: the next scene starts afresh.</summary>
    public void Reset()
    {
        _kind = null;
        _face = false;
        _fates = 0;
        _ghostsDue = null;
        _bannerSeen = false;
        _holdOver = false;
        GhostsShown = true;
    }

    private bool _bannerSeen;

    private void Ghosts(List<Step> steps, FieldDefeatSounds map)
    {
        _ghostsDue = null;
        GhostsShown = true;
        Add(steps, map.Ghosts, now: false);
    }

    private static void Add(List<Step> steps, int sound, bool now)
    {
        if (sound >= 0)
            steps.Add(new Step(sound, now));
    }
}

/// <summary>
/// s140: II ST's alert boxes beep first (<c>0x1628</c> / <c>0x1668</c>: <c>0xb7fa</c>, then <c>form_alert</c>; FORMAT.md
/// 7.4). The remaster shows those moments as its own lines, so the pack lists their string ids
/// (<see cref="FieldSounds.Alerts"/>): a shown line that starts as a listed text (its <c>{n}</c> holes matching anything) beeps,
/// in the modes the entry names. s158: <see cref="BoxFor"/> gives the ST's own box for the line (the disks' text, drawn by
/// <see cref="P2AlertBox"/>). A line of another moment with the same text beeps too, unless the pack marks the entry
/// dialog-only (s141: "UNUSABLE" beeps
/// only from the Use item dialog, not after an Armory buy). The full
/// shopper's refusal beeps in the words of the ST's silent after-the-ninth-buy box `[~]`; the Guild asks the move question
/// before it checks for six members (the ST: after), so that pair beeps twice where the ST beeps once `[~]`.
/// </summary>
public sealed class P2AlertBeeps
{
    private readonly FieldAlerts _alerts;
    private readonly List<(Regex Pattern, IReadOnlyList<string> Modes, bool DialogOnly, StAlert? Box)> _lines;

    public P2AlertBeeps(FieldAlerts alerts)
    {
        _alerts = alerts ?? throw new ArgumentNullException(nameof(alerts));
        _lines = [.. alerts.Lines.Select(l => (Prefix(GameText.S(l.Key)), l.Modes, l.DialogOnly, l.Box))];
    }

    /// <summary>The beep (the pack's sound 34).</summary>
    public int Sound => _alerts.Sound;

    /// <summary>The alerts the pack was built from.</summary>
    public FieldAlerts Alerts => _alerts;

    /// <summary>
    /// Whether <paramref name="line"/> stands for an alert that beeps in game mode <paramref name="mode"/>;
    /// <paramref name="fromDialog"/>: a dialog returned it (s141: a <see cref="FieldAlertLine.DialogOnly"/> entry needs it).
    /// </summary>
    public bool Beeps(string line, GameMode mode, bool fromDialog = true)
    {
        if (string.IsNullOrWhiteSpace(line))
            return false;
        var name = mode.ToString().ToLowerInvariant();
        var trimmed = line.Trim();
        return _lines.Any(l => (fromDialog || !l.DialogOnly) && l.Modes.Contains(name) && l.Pattern.IsMatch(trimmed));
    }

    /// <summary>How many of <paramref name="lines"/> beep in <paramref name="mode"/>.</summary>
    public int Count(IEnumerable<string> lines, GameMode mode, bool fromDialog = true) =>
        lines.Count(l => Beeps(l, mode, fromDialog));

    /// <summary>
    /// Beeps for one frame: the session's new log lines and the lines dialogs returned may tell the same result twice
    /// (a refusal is noted and returned), so the larger count plays.
    /// </summary>
    public int BeepsFor(IEnumerable<string> logLines, IEnumerable<string> dialogLines, GameMode mode) =>
        Math.Max(Count(logLines, mode, fromDialog: false), Count(dialogLines, mode));

    /// <summary>
    /// s158: the ST's boxes for <paramref name="line"/> in <paramref name="mode"/> (the first listed line it matches that
    /// has a box), its <c>printf</c> holes filled from the line's own values in turn ("%8s has died": the name); null when
    /// it beeps without a box (the Guild's YES / NO move question) or does not match.
    /// </summary>
    public StAlert? BoxFor(string line, GameMode mode, bool fromDialog = true)
    {
        if (string.IsNullOrWhiteSpace(line))
            return null;
        var name = mode.ToString().ToLowerInvariant();
        var trimmed = line.Trim();
        foreach (var (pattern, modes, dialogOnly, box) in _lines)
        {
            if ((!fromDialog && dialogOnly) || !modes.Contains(name) || pattern.Match(trimmed) is not { Success: true } m)
                continue;
            return box?.Fill([.. m.Groups.Cast<Group>().Skip(1).Select(g => g.Value)]);
        }
        return null;
    }

    /// <summary>
    /// s158: the boxes for one frame, in order, from the list (the session's new lines or the dialogs' lines) that beeps
    /// more, as <see cref="BeepsFor"/> counts (a refusal noted and returned shows once).
    /// </summary>
    public IReadOnlyList<StAlert> BoxesFor(IReadOnlyList<string> logLines, IReadOnlyList<string> dialogLines, GameMode mode)
    {
        var fromLog = Count(logLines, mode, fromDialog: false) > Count(dialogLines, mode);
        return [.. (fromLog ? logLines : dialogLines).Select(l => BoxFor(l, mode, fromDialog: !fromLog)).OfType<StAlert>()];
    }

    private static Regex Prefix(string template) =>
        new("^" + Regex.Replace(Regex.Escape(template.Trim()), @"\\\{\d+(,[^}]*)?}", "(.+?)"));
}

/// <summary>
/// s140: II ST's TRANSPORTATION fade (<see cref="StTransportFade"/>, <c>0x260a</c>) on the finished frame: a pixel whose
/// nearest register of the faded palette is black now shows black. On the ST the hardware registers fade, so every pixel
/// of a register goes at once; the remaster's frame holds colours of its own (texts, panels), which follow the register
/// nearest them `[~]`. The ST fades the old town's screen with the picker still drawn (the new town is entered after
/// <c>0x260a</c>); s158: the remaster fades the frame from before the pick (<see cref="VirtualScreen.Freeze"/>), Core has
/// moved the party already.
/// </summary>
public sealed class TransportFadeFilter
{
    private readonly Color[] _registers;
    private readonly Dictionary<uint, int> _nearest = new();
    private readonly bool[] _black;
    private Color? _flash;

    public TransportFadeFilter(IReadOnlyList<string> palette)
    {
        ArgumentNullException.ThrowIfNull(palette);
        _registers = [.. palette.Select(VirtualScreen.ParseColor)];
        _black = new bool[_registers.Length];
    }

    /// <summary>Set which registers are black for <paramref name="seconds"/> into <paramref name="fade"/>.</summary>
    public void At(StTransportFade fade, double seconds)
    {
        ArgumentNullException.ThrowIfNull(fade);
        for (var i = 0; i < _black.Length; i++)
            _black[i] = fade.IsBlack(i, seconds);
        _flash = fade.IsFlash(seconds) ? VirtualScreen.ParseColor(fade.FlashColour) : null; // s163: the DisplayBeeps
    }

    /// <summary>True when some register is black or the flash is up (the frame changes).</summary>
    public bool Active => _flash is not null || _black.Any(b => b);

    /// <summary>The register nearest <paramref name="c"/> (squared RGB distance, the first wins a tie).</summary>
    public int NearestRegister(Color c)
    {
        var key = c.PackedValue | 0xff000000u;
        if (_nearest.TryGetValue(key, out var hit))
            return hit;
        var best = 0;
        var bestD = int.MaxValue;
        for (var i = 0; i < _registers.Length; i++)
        {
            var p = _registers[i];
            int dr = c.R - p.R, dg = c.G - p.G, db = c.B - p.B;
            var d = dr * dr + dg * dg + db * db;
            if (d < bestD)
                (bestD, best) = (d, i);
        }
        if (_nearest.Count < 65536)
            _nearest[key] = best;
        return best;
    }

    /// <summary>Black every pixel whose nearest register is black.</summary>
    public void Apply(Span<Color> pixels)
    {
        if (!Active)
            return;
        if (_flash is { } flash)
        {
            for (var i = 0; i < pixels.Length; i++)
                if (NearestRegister(pixels[i]) == 0)
                    pixels[i] = flash;
            return;
        }
        for (var i = 0; i < pixels.Length; i++)
            if (_black[NearestRegister(pixels[i])])
                pixels[i] = Color.Black;
    }
}
