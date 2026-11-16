namespace OpenPhantasie.Data;

/// <summary>s224: a member as an Apple II end scene shows it: its name and the body shape it is drawn with.</summary>
public sealed record Apple2SceneMember(string Name, int Body);

/// <summary>s224: a member's fate on the party wipe (the Core's judgement the scene prints).</summary>
public enum Apple2SceneFate
{
    Destroyed,
    Resurrected,
    Undead,
}

/// <summary>
/// s224: an Apple II BASIC end scene (<see cref="Apple2ScenesLayout"/>) running over time: the hi-res page
/// (<see cref="Page"/>), the text page (<see cref="Text"/>), the display switches (<see cref="Graphics"/> on = graphics with
/// the four text rows under it, off = the text page; <see cref="ShowPage2"/>), the key waits and the sounds started. The
/// steps are laid out for the party first (<c>repeat</c>, <c>party</c>, <c>fates</c>, <c>flash</c> become plain
/// statements). A key pressed while none is wanted stays latched for the next GET, as the Apple's keyboard strobe does.
/// No rendering types: the Client draws it, tests run it.
/// </summary>
public sealed class Apple2Scene
{
    private enum Kind
    {
        Clear, Hgr, Text, Graphics, Page1, Page2, Home, Vtab, Print, Return, Draw, XDraw, Hplot, Key, Sound, Wait,
    }

    private sealed record Prim(Kind Kind, double Seconds, Apple2Step? Step = null, string? Text = null, int Row = 0);

    private readonly List<Prim> _program = [];
    private readonly byte[] _shapes;
    private int _next;
    private double _wait;
    private double _afterKey;
    private readonly List<string> _sounds = [];

    public Apple2Scene(IReadOnlyList<Apple2Step> steps, byte[] shapes, IReadOnlyList<Apple2SceneMember> members,
        Func<string, string> text, IReadOnlyList<Apple2SceneFate>? fates = null, byte[]? page2 = null)
    {
        ArgumentNullException.ThrowIfNull(steps);
        ArgumentNullException.ThrowIfNull(members);
        ArgumentNullException.ThrowIfNull(text);
        _shapes = shapes ?? throw new ArgumentNullException(nameof(shapes));
        Page2 = page2;
        foreach (var s in steps)
            Expand(s, members, fates ?? [], text);
        Duration = _program.Sum(p => p.Seconds);
    }

    public Apple2Hires Page { get; } = new();
    public Apple2TextPage Text { get; } = new();

    /// <summary>Graphics shown (mixed: hi-res rows 0–159 over text rows 20–23); false = the text page.</summary>
    public bool Graphics { get; private set; } = true;

    /// <summary>Hi-res page 2 shown (<c>POKE -16299</c>): <see cref="Page2"/>'s bytes, the text rows blank `[~]`.</summary>
    public bool ShowPage2 { get; private set; }

    public byte[]? Page2 { get; }

    /// <summary>A GET is waiting for a key.</summary>
    public bool WaitingForKey { get; private set; }

    public bool Done => _next >= _program.Count && _wait <= 0 && !WaitingForKey;

    /// <summary>The scene's length without its key waits, in seconds.</summary>
    public double Duration { get; }

    /// <summary>The statements run so far (for tests: run to a snapshot point).</summary>
    public int Executed => _next;

    public int Statements => _program.Count;

    /// <summary>The sounds started since the last call (names of <see cref="Apple2ScenesLayout.Sounds"/>).</summary>
    public IReadOnlyList<string> TakeSounds()
    {
        var s = _sounds.ToArray();
        _sounds.Clear();
        return s;
    }

    /// <summary>
    /// Run <paramref name="seconds"/> on. <paramref name="keyLatched"/>: a key has been pressed and not yet taken; a GET
    /// takes it (and sets it false). Time spent waiting for a key does not count.
    /// </summary>
    public void Advance(double seconds, ref bool keyLatched)
    {
        if (WaitingForKey)
        {
            if (!keyLatched)
                return;
            keyLatched = false;
            WaitingForKey = false;
            _wait = _afterKey;
        }
        _wait -= seconds;
        while (_wait <= 0 && _next < _program.Count)
        {
            var p = _program[_next++];
            Execute(p);
            if (p.Kind == Kind.Key)
            {
                if (!keyLatched)
                {
                    WaitingForKey = true;
                    _afterKey = p.Seconds;
                    _wait = 0;
                    return;
                }
                keyLatched = false;
            }
            _wait += p.Seconds;
        }
    }

    /// <summary>Run statements until <paramref name="count"/> have run, taking every key (tests).</summary>
    public void RunStatements(int count)
    {
        WaitingForKey = false;
        _wait = 0;
        while (_next < Math.Min(count, _program.Count))
            Execute(_program[_next++]);
    }

    /// <summary>The index of the <paramref name="n"/>-th (0-based) statement of op <paramref name="op"/> (tests).</summary>
    public int IndexOf(string op, int n = 0)
    {
        for (var i = 0; i < _program.Count; i++)
            if (OpName(_program[i].Kind) == op && n-- == 0)
                return i;
        return -1;
    }

    private static string OpName(Kind k) => k switch
    {
        Kind.Draw => "draw", Kind.XDraw => "xdraw", Kind.Hplot => "hplot", Kind.Key => "key", Kind.Clear => "clear",
        Kind.Hgr => "hgr", Kind.Text => "text", Kind.Graphics => "graphics", Kind.Home => "home", Kind.Print => "print",
        Kind.Sound => "sound", _ => k.ToString().ToLowerInvariant(),
    };

    private void Execute(Prim p)
    {
        var s = p.Step;
        switch (p.Kind)
        {
            case Kind.Clear:
                Page.Clear();
                break;
            case Kind.Hgr:
                Graphics = true;
                ShowPage2 = false;
                Page.Clear();
                break;
            case Kind.Text:
                Graphics = false;
                ShowPage2 = false;
                break;
            case Kind.Graphics:
                Graphics = true;
                break;
            case Kind.Page1:
                ShowPage2 = false;
                break;
            case Kind.Page2:
                ShowPage2 = Page2 is not null;
                break;
            case Kind.Home:
                Text.Home();
                break;
            case Kind.Vtab:
                Text.Vtab(p.Row);
                break;
            case Kind.Print:
                Text.Print(p.Text ?? "");
                break;
            case Kind.Return:
                Text.Return();
                break;
            case Kind.Draw:
            case Kind.XDraw:
                if (p.Kind == Kind.Draw)
                    Page.SetHcolor(s!.Hcolor);
                Page.Draw(_shapes, p.Row, s!.X, s.Y, s.Rot, s.Scale, p.Kind == Kind.XDraw);
                break;
            case Kind.Hplot:
                Page.SetHcolor(s!.Hcolor);
                Page.Lines(s.Points);
                break;
            case Kind.Sound:
                _sounds.Add(s!.Sound!);
                break;
        }
    }

    private void Add(Kind kind, double seconds, Apple2Step? step = null, string? text = null, int row = 0) =>
        _program.Add(new Prim(kind, Math.Max(0, seconds), step, text, row));

    private void Expand(Apple2Step s, IReadOnlyList<Apple2SceneMember> members, IReadOnlyList<Apple2SceneFate> fates,
        Func<string, string> text)
    {
        switch (s.Op)
        {
            case "clear": Add(Kind.Clear, s.Seconds); break;
            case "hgr": Add(Kind.Hgr, s.Seconds); break;
            case "text": Add(Kind.Text, s.Seconds); break;
            case "graphics": Add(Kind.Graphics, s.Seconds); break;
            case "home": Add(Kind.Home, s.Seconds); break;
            case "vtab": Add(Kind.Vtab, s.Seconds, row: s.Row); break;
            case "print":
                Add(Kind.Print, s.Newline ? 0 : s.Seconds, text: new string(' ', s.Pad) + text(s.Text!));
                if (s.Newline)
                    Add(Kind.Return, s.Seconds);
                break;
            case "draw": Add(Kind.Draw, s.Seconds, s, row: s.Shape); break;
            case "xdraw": Add(Kind.XDraw, s.Seconds, s, row: s.Shape); break;
            case "hplot": Add(Kind.Hplot, s.Seconds, s); break;
            case "key": Add(Kind.Key, s.Seconds); break;
            case "sound": Add(Kind.Sound, s.Seconds, s); break;
            case "repeat":
                for (var i = 0; i < s.Count; i++)
                    foreach (var c in s.Steps)
                        Expand(c, members, fates, text);
                if (s.Seconds > 0)
                    Add(Kind.Wait, s.Seconds);
                break;
            case "flash":
                Add(Kind.Wait, s.First);
                for (var i = 0; i < s.Count; i++)
                {
                    Add(Kind.Page2, s.Page2);
                    Add(Kind.Page1, s.Page1);
                }
                Add(Kind.Wait, s.Seconds - s.First - s.Count * (s.Page1 + s.Page2));
                break;
            case "party":
            {
                var n = Math.Clamp(members.Count, 1, s.Xs.Count);
                var xs = s.Xs[n - 1];
                Add(Kind.Wait, s.Lead);
                for (var k = 0; k < xs.Count; k++)
                    for (var j = 0; j < s.Shapes.Count; j++)
                    {
                        var shape = s.Shapes[j] != 0 ? s.Shapes[j]
                            : k < members.Count ? members[k].Body : members.Count > 0 ? members[^1].Body : 19;
                        var last = k == xs.Count - 1 && j == s.Shapes.Count - 1;
                        var fig = s with { Op = "draw", X = xs[k], Shape = shape };
                        Add(Kind.Draw, last ? s.Tail : s.Each[j], fig, row: shape);
                    }
                if (s.Seconds > 0)
                    Add(Kind.Wait, s.Seconds);
                break;
            }
            case "fates":
                Add(Kind.Wait, s.First);
                for (var k = 0; k < members.Count; k++)
                {
                    var fate = k < fates.Count ? fates[k] : Apple2SceneFate.Destroyed;
                    var f = fate switch
                    {
                        Apple2SceneFate.Resurrected => s.Resurrected!,
                        Apple2SceneFate.Undead => s.Undead!,
                        _ => s.Destroyed!,
                    };
                    Add(Kind.Print, 0, text: $"{members[k].Name} {text(s.LeadText!)} ");
                    if (f.Newline)
                        Add(Kind.Return, 0);
                    Add(Kind.Print, 0, text: text(f.Text));
                    Add(Kind.Return, f.Seconds);
                    Add(Kind.Key, 0);
                    Add(Kind.Home, 0);
                    Add(Kind.Vtab, k == members.Count - 1 ? s.Tail : s.AfterKey, row: 24);
                }
                if (s.Seconds > 0)
                    Add(Kind.Wait, s.Seconds);
                break;
        }
    }
}
