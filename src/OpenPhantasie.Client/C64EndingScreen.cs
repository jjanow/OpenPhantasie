using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client;

/// <summary>
/// s223: Phantasie I C64's ending over time (<c>P.END</c>, GRAPHICS.md 9.4), from the pack's <see cref="C64EndingLayout"/>
/// and Core's <see cref="VictoryScene"/>. No MonoGame types: tests drive it with seconds. It reads no key (the C64 reads
/// none until its closing page).
/// <list type="number">
/// <item>The astral line, a <c>TI</c> wait.</item>
/// <item>A cloud per slot (every slot of the party, its status aside), rising one pixel a step, MC1 flashing.</item>
/// <item>A wait; Zeus wiped in from the bottom, one pixel row per <see cref="C64EndingLayout.WipeFrames"/> frames; a wait.</item>
/// <item>Per slot: the lightning hand on, the thunder, the member's figure in its cloud's place (yellow when OK, light grey
/// when not), a wait, the hand off, a wait.</item>
/// <item>"Zeus smiles and says" with the face talking; a wait; the speech typed a character at a time on the rows from
/// <see cref="C64EndingLayout.SpeechRow"/>, each space a click and 16 + RND · 8 jiffies, each line's end two RND · 8 waits.</item>
/// <item>The grant: the hand cycling its frames, the face at rest; a wait.</item>
/// <item>s224: the closing page (the screen cleared, the layout's lines and its key prompt) until a key (<see cref="Key"/>);
/// then <see cref="Frame.Done"/>. Without closing lines the screen is done after the grant.</item>
/// </list>
/// </summary>
public sealed class C64EndingScreen
{
    /// <summary>A text line on screen: column, row (8 × 8 cells), text, palette colour.</summary>
    public readonly record struct TextRow(int Col, int Row, string Text, int Colour);

    /// <summary>
    /// What shows at a moment. <see cref="CloudY"/>: the clouds' top (screen y), or −1 (not on yet); <see cref="CloudColour"/>:
    /// their MC1 colour now. <see cref="Figures"/>: slots 0…Figures−1 show their figure instead of the cloud.
    /// <see cref="ZeusRows"/>: Zeus's bottom rows drawn. <see cref="Face"/> / <see cref="Hand"/>: the frame shown, or −1.
    /// </summary>
    public readonly record struct Frame(IReadOnlyList<TextRow> Text, int CloudY, int CloudColour, int Figures, int ZeusRows,
        bool Strike, int Face, int Hand, bool Done)
    {
        /// <summary>s224: the closing page is up (black screen, its text only; it waits for a key).</summary>
        public bool Page { get; init; }
    }

    private readonly C64EndingLayout _layout;
    private readonly string _astral;
    private readonly string _smiles;
    private readonly IReadOnlyList<string> _speech;
    private readonly List<(double At, int Line, int Length)> _typed = []; // when each character shows
    private readonly List<(double On, double Off)> _clicks = []; // s226: each GOSUB 0xc43's gate on / off
    private readonly int _slots;
    private readonly IReadOnlyList<string> _closing;
    private readonly string _closingKey;
    private double? _keyAt;

    public C64EndingScreen(C64EndingLayout layout, VictoryScene scene, IReadOnlyList<string> speech, string astral,
        string smiles, Func<double> random, IReadOnlyList<string>? closing = null, string? closingKey = null)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(speech);
        ArgumentNullException.ThrowIfNull(random);
        _layout = layout;
        Scene = scene;
        _speech = speech;
        _astral = astral ?? "";
        _smiles = smiles ?? "";
        _closing = closing ?? [];
        _closingKey = closingKey ?? "";
        _slots = scene.Slots.Count;
        RiseAt = layout.Astral.Seconds;
        WipeAt = RiseAt + RiseSteps * (layout.CloudStepSeconds + layout.CloudStepOverhead) + layout.AfterRiseSeconds;
        StrikesAt = WipeAt + layout.WipeHeight * layout.WipeFrames / layout.VblHz + layout.AfterWipeSeconds;
        SmilesAt = StrikesAt + _slots * StrikeSeconds;
        SpeechAt = SmilesAt + layout.Smiles.Seconds;
        // P.END 0x7ff-0x831: PRINT each character; after a space GOSUB 0xc43 (TI to IT = 16, then to IT := RND * 8);
        // after the line the same GOSUB with IT left from the last call.
        var t = SpeechAt;
        var it = 0.0;
        double Click(double first)
        {
            _clicks.Add((t, t + first / layout.JiffyHz));
            var next = random() * layout.RandomJiffies;
            var wait = (first + next) / layout.JiffyHz;
            it = next;
            return wait;
        }
        for (var line = 0; line < speech.Count; line++)
        {
            var text = speech[line] ?? "";
            for (var i = 0; i < text.Length; i++)
            {
                t += layout.LetterSeconds;
                _typed.Add((t, line, i + 1));
                if (text[i] == ' ')
                    t += Click(layout.SpaceJiffies);
            }
            _typed.Add((t, line, text.Length));
            t += Click(it);
        }
        GrantAt = t;
        ClosingAt = GrantAt + layout.GrantSeconds;
    }

    public VictoryScene Scene { get; }

    /// <summary>Steps of the rise (P.END: y = 240 down to 150, 91).</summary>
    public int RiseSteps => _layout.CloudFromY - _layout.CloudToY + 1;

    private double StrikeSeconds => _layout.ThunderSeconds + _layout.FigureSeconds + _layout.AfterStrikeSeconds;

    /// <summary>Seconds from the start: the clouds appear and rise / Zeus's wipe / the first strike / "Zeus smiles" / the
    /// speech / the grant / the end.</summary>
    public double RiseAt { get; }
    public double WipeAt { get; }
    public double StrikesAt { get; }
    public double SmilesAt { get; }
    public double SpeechAt { get; }
    public double GrantAt { get; }

    /// <summary>s224: when the closing page comes up (the grant's wait over).</summary>
    public double ClosingAt { get; }

    /// <summary>When the screen is over: after the grant without a closing page, else at the key that closes it (∞ before).</summary>
    public double DoneAt => _closing.Count == 0 ? ClosingAt : _keyAt ?? double.PositiveInfinity;

    /// <summary>s224: whether the closing page is waiting for its key at <paramref name="t"/>.</summary>
    public bool WaitsForKey(double t) => _closing.Count > 0 && _keyAt is null && t >= ClosingAt;

    /// <summary>s224: a key at <paramref name="t"/>: it closes the closing page (P.END <c>0xae1</c> GET); the C64 reads none
    /// before it.</summary>
    public void Key(double t)
    {
        if (WaitsForKey(t))
            _keyAt = t;
    }

    /// <summary>
    /// The clouds' x (screen) per slot (P.END 0x538-0x5b3): PL = INT(PO / 2 + 1.1) with PO the last slot's index; slot
    /// m &lt; PL at left − gap · m, the others at right + gap · (m − PL).
    /// </summary>
    public static IReadOnlyList<int> CloudXs(int slots, C64EndingLayout layout)
    {
        ArgumentNullException.ThrowIfNull(layout);
        var po = slots - 1;
        var pl = (int)Math.Floor(po / 2.0 + 1.1);
        return [.. Enumerable.Range(0, Math.Max(0, slots)).Select(m => m < pl
            ? layout.CloudLeftX - layout.CloudGap * m
            : layout.CloudRightX + layout.CloudGap * (m - pl))];
    }

    /// <summary>The figure's colour of a slot: OK (granted) members yellow, the others light grey (P.END 0x67b).</summary>
    public int FigureColour(int slot) => Scene.Slots[slot].Granted ? _layout.OkColour : _layout.DeadColour;

    /// <summary>
    /// s226: the screen's SID sounds (the pack's <c>sid_sounds</c>, GRAPHICS.md 9.6) at their moments: P.END's wind
    /// (<c>SYS $4CC6</c> right after the screen is cleared for the rise, stopped by <c>SYS $4CC9</c> after the wipe's TD = 2,
    /// P.END <c>0x644</c>), the thunder at each strike (<c>GOSUB 0xbb6</c> first), and a click per space and line end
    /// (<c>GOSUB 0xc43</c>, its gate off after the caller's wait).
    /// </summary>
    public IReadOnlyList<OpenPhantasie.Data.Audio.SidCue> SoundCues()
    {
        var cues = new List<OpenPhantasie.Data.Audio.SidCue>
        {
            new(RiseAt, "wind", StrikesAt),
            new(StrikesAt, "wind_stop"),
        };
        for (var k = 0; k < _slots; k++)
            cues.Add(new(StrikesAt + k * StrikeSeconds, "thunder"));
        foreach (var (on, off) in _clicks)
        {
            cues.Add(new(on, "click"));
            cues.Add(new(off, "click_off"));
        }
        return cues;
    }

    /// <summary>The screen <paramref name="t"/> seconds from the start.</summary>
    public Frame At(double t)
    {
        var text = new List<TextRow>();
        if (_closing.Count > 0 && t >= ClosingAt)
        {
            // P.END 0x92d-0xabb: CHR$(147), the animator removed, the lines from (col, row) in green, the key prompt.
            for (var i = 0; i < _closing.Count; i++)
                text.Add(new(_layout.ClosingCol, _layout.ClosingRow + i, _closing[i], _layout.ClosingColour));
            if (_layout.ClosingKey is { } key)
                text.Add(new(key.Col, key.Row, _closingKey, key.Colour));
            return new Frame(text, -1, _layout.CloudMc1, 0, 0, false, -1, -1, t >= DoneAt) { Page = true };
        }
        if (t < RiseAt)
            text.Add(new(_layout.Astral.Col, _layout.Astral.Row, _astral, _layout.Astral.Colour));
        int cloudY = -1, colour = _layout.CloudMc1;
        if (t >= RiseAt)
        {
            var step = (int)Math.Floor((t - RiseAt) / (_layout.CloudStepSeconds + _layout.CloudStepOverhead) + 1e-9);
            if (step < RiseSteps)
            {
                cloudY = _layout.CloudFromY - step;
                if (_layout.CloudFlash.Count > 0)
                    colour = _layout.CloudFlash[step / _layout.CloudFlashSteps % _layout.CloudFlash.Count];
            }
            else
                cloudY = _layout.CloudToY;
        }
        var rows = t < WipeAt ? 0
            : Math.Min(_layout.WipeHeight, (int)Math.Floor((t - WipeAt) * _layout.VblHz / _layout.WipeFrames + 1e-9));
        int figures = 0;
        var strike = false;
        if (t >= StrikesAt)
        {
            var k = (int)Math.Floor((t - StrikesAt) / StrikeSeconds + 1e-9);
            var into = t - StrikesAt - k * StrikeSeconds;
            if (k >= _slots)
                figures = _slots;
            else
            {
                figures = k + (into >= _layout.ThunderSeconds - 1e-9 ? 1 : 0);
                strike = into < _layout.ThunderSeconds + _layout.FigureSeconds;
            }
        }
        if (t >= SmilesAt && t < SpeechAt)
            text.Add(new(_layout.Smiles.Col, _layout.Smiles.Row, _smiles, _layout.Smiles.Colour));
        if (t >= SpeechAt)
        {
            var shown = new int[_speech.Count];
            foreach (var (at, line, length) in _typed)
                if (at <= t + 1e-9)
                    shown[line] = Math.Max(shown[line], length);
            for (var line = 0; line < _speech.Count; line++)
                if (shown[line] > 0)
                    text.Add(new(_layout.SpeechCol, _layout.SpeechRow + line, _speech[line][..shown[line]], _layout.SpeechColour));
        }
        var face = t < SmilesAt ? -1
            : t < GrantAt ? AnimationFrame(t - SmilesAt, _layout.Face.Count)
            : 0;
        var hand = t < GrantAt ? -1 : AnimationFrame(t - GrantAt, _layout.Hand.Count);
        return new Frame(text, cloudY, colour, figures, rows, strike, face, hand, t >= DoneAt);
    }

    // UTIL-3.O $C86E: a raster IRQ cycling base + 0 ... count - 1, AnimateVbl frames each.
    private int AnimationFrame(double seconds, int count) =>
        count <= 0 ? -1 : (int)Math.Floor(seconds * _layout.VblHz / _layout.AnimateVbl + 1e-9) % count;
}
