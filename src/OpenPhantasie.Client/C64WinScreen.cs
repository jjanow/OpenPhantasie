using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client;

/// <summary>
/// s224: Phantasie II C64's ending over time as <c>WIN</c> runs it (II's GRAPHICS.md 8.1, I's 9.5), from the pack's
/// <see cref="C64WinLayout"/> and Core's <see cref="VictoryScene"/>. No MonoGame types: tests drive it with seconds.
/// <list type="number">
/// <item>The intro page (Core's <see cref="VictoryScene.IntroPage"/>), no key, while the hall loads.</item>
/// <item>The hall alone (a <c>FOR 2000</c> and the band file's load).</item>
/// <item>The crowd (its first frame) and the whole party in the row, the header lines in the panel; a <c>FOR 4000</c>;
/// a cheer.</item>
/// <item>Per presented member (Core's titles: records 0…n−1, n = OK members): a <c>FOR 4000</c>; the member in its
/// attacking pose in the present colour, its name / "THE … ONE" / "FROM …" centred on three rows; a <c>FOR 1000</c>;
/// a <c>FOR 4000</c>; a cheer; the member back in its idle pose.</item>
/// <item>A cheer: the panel cleared and the cheer line; the crowd's second frame for the sound; the first again; the panel
/// cleared.</item>
/// <item>The wands prompt (Core's speech lines, the key text after the last) until a key (<see cref="Key"/>).</item>
/// <item>The closing page (Core's <see cref="VictoryScene.ClosingPage"/>) until a key; then <see cref="Frame.Done"/>.</item>
/// </list>
/// </summary>
public sealed class C64WinScreen
{
    public enum PageKind { None, Intro, Closing }

    /// <summary>
    /// What shows at a moment: a <see cref="Page"/> instead of the hall, or the hall (<see cref="Band"/>: the crowd and the
    /// party shown; <see cref="Crowd"/>: the crowd's frame, 0 idle / 1 cheering; <see cref="Presented"/>: the slot in its
    /// attacking pose, or −1) with <see cref="Text"/> in the panel.
    /// </summary>
    public readonly record struct Frame(PageKind Page, bool Band, int Crowd, int Presented,
        IReadOnlyList<C64EndingScreen.TextRow> Text, bool WaitsForKey, bool Done);

    private enum Kind { Intro, Hall, Header, Wait, Title, Cheer, Wands, Closing }

    private readonly record struct Step(Kind Kind, int Index, double Seconds, bool Key);

    private readonly C64WinLayout _layout;
    private readonly IReadOnlyList<(string Text, int Col, int Row)> _header;
    private readonly IReadOnlyList<IReadOnlyList<(string Text, int Col, int Row)>> _titles;
    private readonly IReadOnlyList<(string Text, int Col, int Row)> _wands;
    private readonly string _cheer;
    private readonly List<Step> _steps = [];
    private readonly Dictionary<int, double> _keys = []; // key step → when its key came

    /// <param name="titleWord">The "THE {0} ONE" row's format, resolved (the layout's <see cref="C64WinLayout.TitleWord"/>).</param>
    /// <param name="titleFrom">The "FROM {0}" row's format, resolved.</param>
    /// <param name="cheer">The cheer line, resolved.</param>
    /// <param name="wandsKey">The text after the wands prompt's last line, resolved, or "".</param>
    public C64WinScreen(C64WinLayout layout, VictoryScene scene, string titleWord, string titleFrom, string cheer, string wandsKey)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(scene);
        _layout = layout;
        Scene = scene;
        _cheer = cheer ?? "";
        _header = [.. scene.HeaderLines.Take(layout.HeaderAt.Count).Select((line, i) => (line, layout.HeaderAt[i].Col, layout.HeaderAt[i].Row))];
        // WIN 0x9fb: SYS Q,20 - LEN(A$)/2,20,A$; "THE "V$" ONE" at 16 - LEN(V$)/2 (= 20 - its length / 2); R$ at 20 - LEN/2.
        (string, int, int) Centred(string text, int row) =>
            (text, Math.Max(0, (int)Math.Floor(layout.TitleCentre - text.Length / 2.0)), row);
        _titles = [.. scene.TitleParts.Select(p => (IReadOnlyList<(string, int, int)>)
        [
            Centred(p.Name, layout.TitleRow),
            Centred(string.Format(System.Globalization.CultureInfo.InvariantCulture, titleWord ?? "{0}", p.Word), layout.TitleRow + 1),
            Centred(string.Format(System.Globalization.CultureInfo.InvariantCulture, titleFrom ?? "{0}", p.Origin), layout.TitleRow + 2),
        ])];
        var speech = scene.SpeechLines.Where(l => !string.IsNullOrWhiteSpace(l)).Take(layout.WandsAt.Count).ToList();
        if (speech.Count > 0)
            speech[^1] += wandsKey ?? "";
        _wands = [.. speech.Select((line, i) => (line, layout.WandsAt[i].Col, layout.WandsAt[i].Row))];

        if (scene.IntroPage.Count > 0)
            _steps.Add(new(Kind.Intro, 0, layout.IntroSeconds, false));
        _steps.Add(new(Kind.Hall, 0, layout.HallSeconds, false));
        _steps.Add(new(Kind.Header, 0, layout.ForSeconds, false));
        _steps.Add(new(Kind.Cheer, -1, layout.CheerSeconds, false));
        for (var k = 0; k < _titles.Count && k < scene.Slots.Count; k++)
        {
            _steps.Add(new(Kind.Wait, k, layout.ForSeconds, false));
            _steps.Add(new(Kind.Title, k, layout.TitleSeconds + layout.ForSeconds, false));
            _steps.Add(new(Kind.Cheer, k, layout.CheerSeconds, false));
        }
        if (_wands.Count > 0)
            _steps.Add(new(Kind.Wands, 0, double.PositiveInfinity, true));
        if (scene.ClosingPage.Count > 0)
            _steps.Add(new(Kind.Closing, 0, double.PositiveInfinity, true));
    }

    public VictoryScene Scene { get; }

    /// <summary>How many members are presented (Core's titles; the C64 presents every slot `[~]`).</summary>
    public int Presentations => Math.Min(_titles.Count, Scene.Slots.Count);

    private double Start(int i) => i == 0 ? 0 : End(i - 1);

    private double End(int i) => Start(i) + (_keys.TryGetValue(i, out var at) ? at - Start(i) : _steps[i].Seconds);

    private int? Current(double t)
    {
        for (var i = 0; i < _steps.Count; i++)
            if (t < End(i))
                return t >= Start(i) ? i : null;
        return null;
    }

    /// <summary>Seconds from the start when the hall shows / the band and header appear / the wands prompt comes up.</summary>
    public double HallAt => Start(_steps.FindIndex(s => s.Kind == Kind.Hall));
    public double BandAt => Start(_steps.FindIndex(s => s.Kind == Kind.Header));
    public double WandsAt => _steps.FindIndex(s => s.Kind == Kind.Wands) is var w and >= 0 ? Start(w) : DoneAt;

    /// <summary>When the screen is over (∞ until its last key).</summary>
    public double DoneAt => End(_steps.Count - 1);

    /// <summary>A key at <paramref name="t"/>: it ends the wands prompt or the closing page waiting then (WIN's GETs); the
    /// C64 reads no other key.</summary>
    public void Key(double t)
    {
        if (Current(t) is int i && _steps[i].Key && !_keys.ContainsKey(i))
            _keys[i] = t;
    }

    /// <summary>
    /// s226: the screen's SID sounds (the pack's <c>sid_sounds</c>, I's GRAPHICS.md 9.6): WIN's cheer (<c>GOSUB 0x749</c>)
    /// at each cheer, when the crowd's second frame goes up (<see cref="C64WinLayout.CheerFrameAt"/>). The cheers all come
    /// before the first key, so their times are fixed.
    /// </summary>
    public IReadOnlyList<OpenPhantasie.Data.Audio.SidCue> SoundCues() =>
        [.. Enumerable.Range(0, _steps.Count).Where(i => _steps[i].Kind == Kind.Cheer)
            .Select(i => new OpenPhantasie.Data.Audio.SidCue(Start(i) + _layout.CheerFrameAt, "cheer"))];

    /// <summary>Whether a key-waiting step is up at <paramref name="t"/>.</summary>
    public bool WaitsForKey(double t) => Current(t) is int i && _steps[i].Key;

    /// <summary>The screen <paramref name="t"/> seconds from the start.</summary>
    public Frame At(double t)
    {
        var text = new List<C64EndingScreen.TextRow>();
        var colour = _layout.TextColour;
        void Rows(IEnumerable<(string Text, int Col, int Row)> rows)
        {
            foreach (var (s, col, row) in rows)
                text.Add(new(col, row, s, colour));
        }
        if (Current(t) is not int i)
            return new Frame(PageKind.None, false, 0, -1, text, false, t >= DoneAt);
        var step = _steps[i];
        var into = t - Start(i);
        switch (step.Kind)
        {
            case Kind.Intro:
                return new Frame(PageKind.Intro, false, 0, -1, text, false, false);
            case Kind.Closing:
                return new Frame(PageKind.Closing, false, 0, -1, text, true, false);
            case Kind.Hall:
                return new Frame(PageKind.None, false, 0, -1, text, false, false);
            case Kind.Header:
                Rows(_header);
                return new Frame(PageKind.None, true, 0, -1, text, false, false);
            case Kind.Wait:
                return new Frame(PageKind.None, true, 0, -1, text, false, false);
            case Kind.Title:
                Rows(_titles[step.Index]);
                return new Frame(PageKind.None, true, 0, step.Index, text, false, false);
            case Kind.Cheer:
                // WIN 0x6ba: the panel cleared, the line; the pointers + 8; the sound (0x749); the pointers − 8; cleared.
                if (into < _layout.CheerLineSeconds)
                    text.Add(new(_layout.CheerLine.Col, _layout.CheerLine.Row, _cheer, colour));
                var cheering = into >= _layout.CheerFrameAt && into < _layout.CheerFrameAt + _layout.CheerFrameSeconds;
                return new Frame(PageKind.None, true, cheering ? 1 : 0, step.Index, text, false, false);
            case Kind.Wands:
                Rows(_wands);
                return new Frame(PageKind.None, true, 0, -1, text, true, false);
        }
        return new Frame(PageKind.None, false, 0, -1, text, false, false);
    }
}
