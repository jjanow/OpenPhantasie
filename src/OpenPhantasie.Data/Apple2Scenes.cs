using System.Text.Json.Serialization;

namespace OpenPhantasie.Data;

/// <summary>
/// s224: one statement of an Apple II BASIC end scene (<c>apple2_scenes</c>; Phantasie I's <c>CEMETARY</c> and
/// <c>FLASH?</c>, <c>reverse-engineering/apple2/phantasie1/GRAPHICS.md</c> 7), then <see cref="Seconds"/> until the next
/// one starts (the interpreter's time, measured with the Apple II+ ROM in a 6502 emulator). <see cref="Op"/>:
/// <list type="bullet">
/// <item><c>clear</c> (HCLR / <c>CALL 62450</c>), <c>hgr</c> (HGR: graphics, mixed, page 1, then the clear), <c>text</c>
/// (TEXT), <c>graphics</c> (<c>POKE -16304</c>);</item>
/// <item><c>home</c>, <c>vtab</c> (<see cref="Row"/>, 1-based), <c>print</c> (string id <see cref="Text"/> after
/// <see cref="Pad"/> spaces, then a return when <see cref="Newline"/>);</item>
/// <item><c>draw</c> / <c>xdraw</c> (<see cref="Shape"/> of <c>SHAPES</c> at <see cref="X"/>, <see cref="Y"/>, with
/// <see cref="Scale"/>, <see cref="Rot"/>, <see cref="Hcolor"/>), <c>hplot</c> (<see cref="Points"/> joined by lines);</item>
/// <item><c>key</c> (GET: waits for a key; its <see cref="Seconds"/> run after the key), <c>sound</c> (<see cref="Sound"/>
/// of <see cref="Apple2ScenesLayout.Sounds"/>), <c>flash</c> (<see cref="Count"/> times page 2 for <see cref="Page2"/> s,
/// page 1 for <see cref="Page1"/> s, after <see cref="First"/> s);</item>
/// <item><c>repeat</c> (<see cref="Steps"/> <see cref="Count"/> times);</item>
/// <item><c>party</c> (a figure per x of <see cref="Xs"/>[members − 1]: <see cref="Shapes"/> in turn, 0 = the member's own
/// body; <see cref="Lead"/> s first, <see cref="Each"/>[k] after shape k, <see cref="Tail"/> after the last);</item>
/// <item><c>fates</c> (per member: its name, <see cref="LeadText"/>, the fate's text, the fate's seconds, a key, HOME,
/// VTAB 24, <see cref="AfterKey"/> s; <see cref="First"/> s before, <see cref="Tail"/> s after the last key).</item>
/// </list>
/// </summary>
public sealed record Apple2Step
{
    public required string Op { get; init; }
    public double Seconds { get; init; }
    public int Shape { get; init; }
    public int X { get; init; }
    public int Y { get; init; }
    public int Scale { get; init; } = 1;
    public int Rot { get; init; }
    public int Hcolor { get; init; }
    public IReadOnlyList<int> Points { get; init; } = [];
    public int Row { get; init; }
    public string? Text { get; init; }
    public int Pad { get; init; }
    public bool Newline { get; init; }
    public string? Sound { get; init; }
    public int Count { get; init; }
    public IReadOnlyList<Apple2Step> Steps { get; init; } = [];
    public IReadOnlyList<IReadOnlyList<int>> Xs { get; init; } = [];
    public IReadOnlyList<int> Shapes { get; init; } = [];
    public double Lead { get; init; }
    public IReadOnlyList<double> Each { get; init; } = [];
    public double Tail { get; init; }
    public double First { get; init; }
    public double Page2 { get; init; }
    public double Page1 { get; init; }
    public string? LeadText { get; init; }
    public double AfterKey { get; init; }
    public Apple2Fate? Destroyed { get; init; }
    public Apple2Fate? Resurrected { get; init; }
    public Apple2Fate? Undead { get; init; }

    internal static readonly string[] Ops =
        ["clear", "hgr", "text", "graphics", "home", "vtab", "print", "draw", "xdraw", "hplot", "key", "sound", "flash", "repeat",
         "party", "fates"];

    internal static Apple2Step FromDto(Dto d, string where, Func<string, InvalidDataException> bad)
    {
        if (d.Op is null || !Ops.Contains(d.Op))
            throw bad($"{where}: unknown op '{d.Op}'");
        if (d.S < 0 || d.Scale is < 0 or > 255 || d.Rot is < 0 or > 255 || d.Hcolor is < 0 or > 7
            || d.Op is "draw" or "xdraw" && d.Shape < 1 || d.Op == "hplot" && (d.Points is not { Count: >= 2 } || d.Points.Count % 2 != 0)
            || d.Op == "vtab" && d.Row is < 1 or > 24 || d.Op == "print" && string.IsNullOrWhiteSpace(d.Text)
            || d.Op == "sound" && string.IsNullOrWhiteSpace(d.Sound) || d.Op is "repeat" or "flash" && d.Count < 1
            || d.Op == "party" && (d.Xs is not { Count: > 0 } || d.Shapes is not { Count: > 0 } || d.Each?.Count != d.Shapes.Count)
            || d.Op == "fates" && (d.Destroyed is null || d.Resurrected is null || d.Undead is null || string.IsNullOrWhiteSpace(d.LeadText)))
            throw bad($"{where}: bad {d.Op}");
        Apple2Fate? Fate(Apple2Fate.Dto? f, string name) => f is null ? null
            : string.IsNullOrWhiteSpace(f.Text) || f.S < 0 ? throw bad($"{where}: fates.{name}") : new Apple2Fate(f.Text!, f.S, f.Newline);
        return new Apple2Step
        {
            Op = d.Op, Seconds = d.S, Shape = d.Shape, X = d.X, Y = d.Y, Scale = d.Scale ?? 1, Rot = d.Rot, Hcolor = d.Hcolor,
            Points = d.Points ?? [], Row = d.Row, Text = d.Text, Pad = d.Pad, Newline = d.Newline, Sound = d.Sound,
            Count = d.Count, Xs = d.Xs is { } xs ? [.. xs.Select(x => (IReadOnlyList<int>)(x ?? []))] : [],
            Shapes = d.Shapes ?? [], Each = d.Each ?? [], Tail = d.Tail, First = d.First, Page2 = d.Page2, Page1 = d.Page1,
            Lead = d.Lead, LeadText = d.LeadText, AfterKey = d.AfterKey,
            Destroyed = Fate(d.Destroyed, "destroyed"), Resurrected = Fate(d.Resurrected, "resurrected"),
            Undead = Fate(d.Undead, "undead"),
            Steps = d.Steps is { } st ? [.. st.Select((s, i) => FromDto(s, $"{where}.steps[{i}]", bad))] : [],
        };
    }

    internal sealed class Dto
    {
        public string? Op { get; set; }
        public double S { get; set; }
        public int Shape { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public int? Scale { get; set; }
        public int Rot { get; set; }
        public int Hcolor { get; set; }
        public List<int>? Points { get; set; }
        public int Row { get; set; }
        public string? Text { get; set; }
        public int Pad { get; set; }
        public bool Newline { get; set; }
        public string? Sound { get; set; }
        public int Count { get; set; }
        public List<Dto>? Steps { get; set; }
        public List<List<int>?>? Xs { get; set; }
        public List<int>? Shapes { get; set; }

        public double Lead { get; set; }
        [JsonPropertyName("lead_text")] public string? LeadText { get; set; }
        public List<double>? Each { get; set; }
        public double Tail { get; set; }
        public double First { get; set; }
        public double Page2 { get; set; }
        public double Page1 { get; set; }
        [JsonPropertyName("after_key")] public double AfterKey { get; set; }
        public Apple2Fate.Dto? Destroyed { get; set; }
        public Apple2Fate.Dto? Resurrected { get; set; }
        public Apple2Fate.Dto? Undead { get; set; }
    }
}

/// <summary>s224: a fate's text (a string id), the seconds the Apple takes before its key, and whether a return comes first.</summary>
public sealed record Apple2Fate(string Text, double Seconds, bool Newline)
{
    internal sealed class Dto
    {
        public string? Text { get; set; }
        public double S { get; set; }
        public bool Newline { get; set; }
    }
}

/// <summary>
/// s224: a speaker sound as a BASIC subroutine plays it: <c>CALL 770</c> (the game's <c>INFO</c> routine at $302, entered
/// with X = <see cref="EntryX"/>, Y = <see cref="EntryY"/>) once per <see cref="Calls"/> entry: its start (seconds after
/// the subroutine starts), the pitch (POKE 768) and the duration (POKE 769).
/// </summary>
public sealed record Apple2SpeakerSound(int EntryX, int EntryY, IReadOnlyList<(double At, int Pitch, int Duration)> Calls)
{
    internal sealed class Dto
    {
        public int X { get; set; }
        public int Y { get; set; }
        public List<List<double>>? Calls { get; set; }
    }
}

/// <summary>
/// s224: the party figure's body shape (TOWN 2350–2356, record byte 168): <see cref="ByRace"/>[race], but
/// <see cref="Robed"/> for a race of <see cref="RobedRaces"/> with a class of <see cref="RobedClasses"/>.
/// </summary>
public sealed record Apple2Bodies(IReadOnlyList<int> ByRace, int Robed, IReadOnlyList<int> RobedRaces, IReadOnlyList<int> RobedClasses)
{
    public int For(int race, int cls) =>
        RobedRaces.Contains(race) && RobedClasses.Contains(cls) ? Robed
        : race >= 0 && race < ByRace.Count ? ByRace[race] : ByRace[^1];

    internal sealed class Dto
    {
        [JsonPropertyName("by_race")] public List<int>? ByRace { get; set; }
        public int Robed { get; set; }
        [JsonPropertyName("robed_races")] public List<int>? RobedRaces { get; set; }
        [JsonPropertyName("robed_classes")] public List<int>? RobedClasses { get; set; }
    }
}

/// <summary>
/// s224: <c>apple2_scenes</c>: Phantasie I Apple II's party wipe (<see cref="Defeat"/>, <c>CEMETARY</c>) and ending
/// (<see cref="Ending"/>, <c>FLASH?</c>) as their BASIC runs them, replayed on a hi-res page (<see cref="Apple2Hires"/>)
/// with the game's shape table (<see cref="ShapesFile"/>, the import's copy of <c>SHAPES</c>). <see cref="Page2File"/>:
/// what hi-res page 2 shows during <c>flash</c> (memory $4000–$5FFF as the import can rebuild it), or null.
/// </summary>
public sealed record Apple2ScenesLayout
{
    public required string ShapesFile { get; init; }
    public string? Page2File { get; init; }
    public double ClockHz { get; init; } = 1020484;
    public required Apple2Bodies Bodies { get; init; }
    public IReadOnlyDictionary<string, Apple2SpeakerSound> Sounds { get; init; } = new Dictionary<string, Apple2SpeakerSound>();
    public IReadOnlyList<Apple2Step> Defeat { get; init; } = [];
    public IReadOnlyList<Apple2Step> Ending { get; init; } = [];

    /// <summary>The shape table's bytes, or null when the file is missing or unreadable.</summary>
    public byte[]? TryLoadShapes() => TryRead(ShapesFile);

    public byte[]? TryLoadPage2() => Page2File is { } f ? TryRead(f) : null;

    private static byte[]? TryRead(string path)
    {
        try
        {
            return File.Exists(path) ? File.ReadAllBytes(path) : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    internal static Apple2ScenesLayout FromDto(Dto dto, string manifestPath, string rootDirectory)
    {
        InvalidDataException Bad(string what) => new($"{manifestPath}: apple2_scenes: {what}.");
        if (string.IsNullOrWhiteSpace(dto.Shapes) || dto.Bodies?.ByRace is not { Count: > 0 } byRace
            || dto.ClockHz is <= 0)
            throw Bad("shapes, bodies.by_race and clock_hz are required");
        var sounds = new Dictionary<string, Apple2SpeakerSound>(StringComparer.Ordinal);
        foreach (var (name, s) in dto.Sounds ?? [])
        {
            if (s?.Calls is not { } calls || calls.Any(c => c is not { Count: 3 } || c[0] < 0 || c[1] is < 0 or > 255 || c[2] is < 0 or > 255))
                throw Bad($"sounds.{name}: calls are [at, pitch, duration]");
            sounds[name] = new Apple2SpeakerSound(s.X, s.Y, [.. calls.Select(c => (c[0], (int)c[1], (int)c[2]))]);
        }
        IReadOnlyList<Apple2Step> Steps(List<Apple2Step.Dto>? list, string what) =>
            list is null ? [] : [.. list.Select((s, i) => Apple2Step.FromDto(s, $"{what}[{i}]", w => Bad(w)))];
        var layout = new Apple2ScenesLayout
        {
            ShapesFile = Path.Combine(rootDirectory, dto.Shapes!),
            Page2File = string.IsNullOrWhiteSpace(dto.Page2) ? null : Path.Combine(rootDirectory, dto.Page2),
            ClockHz = dto.ClockHz ?? 1020484,
            Bodies = new Apple2Bodies(byRace, dto.Bodies.Robed, dto.Bodies.RobedRaces ?? [], dto.Bodies.RobedClasses ?? []),
            Sounds = sounds,
            Defeat = Steps(dto.Defeat, "defeat"),
            Ending = Steps(dto.Ending, "ending"),
        };
        foreach (var s in layout.Defeat.Concat(layout.Ending).SelectMany(Flatten))
            if (s.Op == "sound" && !sounds.ContainsKey(s.Sound!))
                throw Bad($"sound '{s.Sound}' not in sounds");
        return layout;
    }

    private static IEnumerable<Apple2Step> Flatten(Apple2Step s) => [s, .. s.Steps.SelectMany(Flatten)];

    /// <summary>Every string id the scenes print (their PRINTs, the fates' lead and words).</summary>
    public IEnumerable<string> StringKeys => Defeat.Concat(Ending).SelectMany(Flatten)
        .SelectMany(s => new[] { s.Text, s.LeadText, s.Destroyed?.Text, s.Resurrected?.Text, s.Undead?.Text })
        .OfType<string>().Distinct();

    private static readonly System.Text.Json.JsonSerializerOptions ParseOptions = new() { PropertyNameCaseInsensitive = true };

    /// <summary>An <c>apple2_scenes</c> object read on its own (tests, tools); files resolve under <paramref name="rootDirectory"/>.</summary>
    public static Apple2ScenesLayout Parse(string json, string rootDirectory) =>
        FromDto(System.Text.Json.JsonSerializer.Deserialize<Dto>(json, ParseOptions)
                ?? throw new InvalidDataException("apple2_scenes: empty"), "apple2_scenes", rootDirectory);

    internal sealed class Dto
    {
        public string? Shapes { get; set; }
        public string? Page2 { get; set; }
        [JsonPropertyName("clock_hz")] public double? ClockHz { get; set; }
        public Apple2Bodies.Dto? Bodies { get; set; }
        public Dictionary<string, Apple2SpeakerSound.Dto?>? Sounds { get; set; }
        public List<Apple2Step.Dto>? Defeat { get; set; }
        public List<Apple2Step.Dto>? Ending { get; set; }
    }
}

/// <summary>
/// s224: the Apple II's 40 × 24 text page as Applesoft prints on it (the full-screen window): HOME clears it, VTAB moves
/// the cursor's row, a character goes at the cursor and moves it on (wrapping at column 40), a return goes to the next
/// row's start and scrolls the page up from the bottom row.
/// </summary>
public sealed class Apple2TextPage
{
    public const int Cols = 40;
    public const int Rows = 24;
    private readonly char[] _cells = new char[Cols * Rows];

    public Apple2TextPage() => Home();

    public int Column { get; private set; }
    public int Line { get; private set; }

    public char this[int col, int row] => _cells[row * Cols + col];

    /// <summary>Row <paramref name="row"/> as a string of 40 characters.</summary>
    public string RowText(int row) => new(_cells, row * Cols, Cols);

    public void Home()
    {
        Array.Fill(_cells, ' ');
        Column = 0;
        Line = 0;
    }

    public void Vtab(int row) => Line = Math.Clamp(row - 1, 0, Rows - 1);

    public void Print(string text)
    {
        foreach (var ch in text)
        {
            _cells[Line * Cols + Column] = char.ToUpperInvariant(ch);
            if (++Column >= Cols)
                Return();
        }
    }

    public void Return()
    {
        Column = 0;
        if (++Line < Rows)
            return;
        Array.Copy(_cells, Cols, _cells, 0, Cols * (Rows - 1));
        Array.Fill(_cells, ' ', Cols * (Rows - 1), Cols);
        Line = Rows - 1;
    }
}

/// <summary>
/// s224: the game's speaker routine (<c>INFO</c> at $302, <c>CALL 770</c>; GRAPHICS.md 8.3): <c>LDA $C030</c> (a click),
/// then <c>DEY / BNE</c> (when Y wraps: <c>DEC $301</c>, done at 0) and <c>DEX / BNE</c> back to the DEY; when X runs
/// out, <c>LDX $300</c> and back to the click. The clicks' cycle offsets from the call, counted per instruction.
/// </summary>
public static class Apple2Speaker
{
    /// <summary>The cycle of each click (the speaker toggles) of one call, from its first instruction.</summary>
    public static IReadOnlyList<long> Clicks(int pitch, int duration, int entryX, int entryY)
    {
        var clicks = new List<long>();
        long t = 0;
        int x = entryX & 0xFF, y = entryY & 0xFF, d = duration & 0xFF;
        while (true)
        {
            clicks.Add(t + 3); // LDA $C030: the read is the instruction's 4th cycle
            t += 4;
            while (true)
            {
                y = (y - 1) & 0xFF; // DEY
                t += 2;
                if (y == 0)
                {
                    t += 2; // BNE not taken
                    d = (d - 1) & 0xFF; // DEC $301
                    t += 6;
                    if (d == 0)
                        return clicks; // BEQ taken → RTS
                    t += 2; // BEQ not taken
                }
                else
                    t += 3; // BNE taken
                x = (x - 1) & 0xFF; // DEX
                t += 2;
                if (x != 0)
                {
                    t += 3; // BNE taken → DEY
                    continue;
                }
                t += 2; // BNE not taken
                t += 4 + 3; // LDX $300, JMP $302
                x = pitch & 0xFF;
                break;
            }
            if (clicks.Count > 2_000_000)
                return clicks;
        }
    }

    /// <summary>Every click of <paramref name="sound"/>, in seconds from its start.</summary>
    public static IReadOnlyList<double> ClickSeconds(Apple2SpeakerSound sound, double clockHz)
    {
        var all = new List<double>();
        foreach (var (at, pitch, duration) in sound.Calls)
            all.AddRange(Clicks(pitch, duration, sound.EntryX, sound.EntryY).Select(c => at + c / clockHz));
        return all;
    }
}
