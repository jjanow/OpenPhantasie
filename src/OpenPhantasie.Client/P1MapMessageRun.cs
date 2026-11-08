using System.Text.RegularExpressions;
using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client;

/// <summary>
/// Requester 23 over Phantasie I Amiga's map screens (Amiga <c>FORMAT.md</c> "The map screen's requester 23", s139, and "The
/// dungeon's requester 23", s140): the boxes a wilderness step's encounter opens (h74 <c>+0</c>: `core.encounter.a_night_encounter`,
/// "%s IS ON WATCH" [/ `core.encounter.and_hears_the_monsters`], "%s HEARS MONSTERS", `p1field.box.you_surprise_them`, `p1field.box.monsters_surprise_you`,
/// `p1field.box.black_knights_flee`) and a dungeon step's events (h41: the wall, stairs, locks, a trap's disarm, the script
/// ops' SMOKE / SPELL / GOLD / MOVED / NOISE / MONSTERS APPEAR), one after the other, before the fight's screen. Each box
/// stays its whole seconds (⌊(polls + 60·rows) / 60⌋, h80 <c>+0x546</c>), less when the machine's clock reaches a multiple
/// of 8 s first (<see cref="CombatFieldMessage.ShownSeconds"/>); keys and clicks are swallowed meanwhile. The boxes of one
/// step follow each other at once (only rolls, a night's prep, and in the dungeon the script's other ops run between them)
/// `[~]`, not filmed back to back. The remaster's D-058 flashes ("A DAY ENCOUNTER", the dungeon's "AN ENCOUNTER") are no
/// box: the Amiga has none, and the user's s140 decision wants them shown without a wait (on the combat screen's line,
/// <see cref="CombatPresenter.P1Banner"/>). s141: a box's sound (<see cref="MapMessageLayout.Sounds"/>) plays first and the
/// box opens when it ends (h89 blocks); a box logged before Core moved the party (op 12, a jammed lock) carries the cell it
/// stood on (<see cref="Box.PartyAt"/>). s144: in the dungeon Core marks the lines the Amiga shows in requester 21
/// (<see cref="GameSession.MapWindow"/>): a MESS text with no menu is a timed window at the top of the map, a step of
/// the run like a box (<see cref="Box.Window"/>, 2(n + 1) s, the clock cut); a damage report ends a part and waits for
/// OK (a key or a click, <see cref="WaitLines"/>, <see cref="Next"/>). The run may hold windows and no box. Other lines
/// stay on the message line. s153: on the overworld too: a location's text pages are one requester-23 box of the pages as
/// stored (<see cref="LogWindowKind.Box"/>), a swim's lines a report; a click on a MESS window's row ends it
/// (<see cref="EndWindowAt"/>). No MonoGame types: tests drive it with milliseconds.
/// </summary>
public sealed class P1MapMessageRun
{
    /// <summary>
    /// One box: its rows, shown from <see cref="StartMs"/> to <see cref="EndMs"/> (ms from the part's start). s141:
    /// <see cref="Sound"/> plays from <see cref="SoundMs"/> to <see cref="StartMs"/> (h89 blocks, the box opens after it);
    /// <see cref="PartyAt"/> is the cell the party stands on under the box when Core has moved it since (null: where Core has it).
    /// </summary>
    public sealed record Box(IReadOnlyList<string> Rows, double StartMs, double EndMs)
    {
        public int? Sound { get; init; }
        public double SoundMs { get; init; }
        public PartyCell? PartyAt { get; init; }

        /// <summary>s144: requester 21's MESS text window (<see cref="MapMessageWindow"/>) rather than a requester-23 box.</summary>
        public bool Window { get; init; }
    }

    /// <summary>s141: a dungeon cell the map draws the party on (level, x, y).</summary>
    public sealed record PartyCell(int LevelId, int X, int Y);

    /// <summary>A box (or s144 a MESS window) before it is timed: rows, sound, party cell.</summary>
    private sealed record Spec(IReadOnlyList<string> Rows, int? Sound, PartyCell? PartyAt, bool Window = false);

    /// <summary>A part: its boxes, then the report shown until a key or a click (empty: none).</summary>
    private sealed record PartSpec(IReadOnlyList<Spec> Boxes, IReadOnlyList<string> WaitLines);

    /// <summary>This part's boxes, timed from its start.</summary>
    public IReadOnlyList<Box> Boxes { get; }

    /// <summary>The part's length: the last box's end.</summary>
    public double LengthMs => Boxes.Count == 0 ? 0 : Boxes[^1].EndMs;

    /// <summary>The lines (as logged) the boxes of every part show: the front end keeps them off its own message line.</summary>
    public IReadOnlySet<string> BoxedLines { get; }

    /// <summary>
    /// s141 / s144: the damage report after this part's boxes (the dungeon only; its title first, then the member lines,
    /// as Core marked them, <see cref="LogWindowKind.Report"/>). I Amiga shows it in requester 21 with an OK button (h86
    /// <c>+0(0x15)</c>, waiting for the click); the remaster waits for a key or a click, then plays <see cref="Next"/>
    /// (or ends the run). Empty: the part ends when its last box closes.
    /// </summary>
    public IReadOnlyList<string> WaitLines { get; }

    /// <summary>True when another part follows <see cref="WaitLines"/>.</summary>
    public bool HasNext => _rest.Count > 0;

    /// <summary>s144: the part ends in a report that waits for a key or a click.</summary>
    public bool Waits => WaitLines.Count > 0;

    private readonly IReadOnlyList<PartSpec> _rest;
    private readonly PartSpec _part;
    private readonly MapMessageLayout _layout;
    private readonly int _polls;
    private readonly Func<int, double>? _soundSeconds;

    private P1MapMessageRun(MapMessageLayout layout, PartSpec part, IReadOnlyList<Box> boxes, IReadOnlyList<PartSpec> rest,
        IReadOnlySet<string> boxed, int polls, Func<int, double>? soundSeconds)
    {
        _layout = layout;
        _part = part;
        Boxes = boxes;
        WaitLines = part.WaitLines;
        _rest = rest;
        BoxedLines = boxed;
        _polls = polls;
        _soundSeconds = soundSeconds;
    }

    /// <summary>The rows of the box up <paramref name="ms"/> into the part, or null between / after the boxes.</summary>
    public IReadOnlyList<string>? At(double ms) => BoxAt(ms)?.Rows;

    /// <summary>s144: the box (or MESS window) up <paramref name="ms"/> into the part, or null.</summary>
    public Box? BoxAt(double ms) => Boxes.FirstOrDefault(b => ms >= b.StartMs && ms < b.EndMs);

    /// <summary>
    /// s143: whether the overworld's party sprite shows <paramref name="ms"/> into <paramref name="run"/> (null: no run).
    /// h80 <c>+0x546</c> (the requester's show) frees the party's hardware sprite (h77 <c>+0x106c</c>: FreeSprite, h78
    /// <c>+0x12</c> := −1) before it opens the box and gets it back (h56 <c>+0x8e2</c>) after it closes; filmed s143 on
    /// FS-UAE: the sprite on its new cell one frame, gone the frame before the box, back one frame after it. Between two
    /// boxes, during a box's sound, and while a dungeon run waits, the sprite shows. The dungeon's party glyph is a blit
    /// and stays.
    /// </summary>
    public static bool PartySpriteShown(P1MapMessageRun? run, double ms) => run?.At(ms) is null;

    /// <summary>
    /// s141: the cell the map draws the party on <paramref name="ms"/> into the part (a box's sound, the box and the gap
    /// before it count): the next box's <see cref="Box.PartyAt"/>, after the last box the next part's first; null when
    /// Core's position stands (the move came before, or there is none).
    /// </summary>
    public PartyCell? PartyAt(double ms) =>
        Boxes.FirstOrDefault(b => ms < b.EndMs) is { } box ? box.PartyAt : _rest.Count > 0 ? _rest[0].Boxes.FirstOrDefault()?.PartyAt : null;

    /// <summary>s141: the sounds whose start falls in [<paramref name="fromMs"/>, <paramref name="toMs"/>), in order.</summary>
    public IEnumerable<int> SoundsBetween(double fromMs, double toMs) =>
        Boxes.Where(b => b.Sound is not null && b.SoundMs >= fromMs && b.SoundMs < toMs).Select(b => b.Sound!.Value);

    /// <summary>s141: the next part (after <see cref="WaitLines"/>), timed from <paramref name="clockMs"/>, or null.</summary>
    public P1MapMessageRun? Next(double? clockMs) =>
        _rest.Count == 0 ? null : Timed(_layout, _rest[0], [.. _rest.Skip(1)], BoxedLines, _polls, clockMs, _soundSeconds);

    /// <summary>
    /// s153: a click on a row of the requester-21 MESS window up <paramref name="ms"/> into the part ends it there (its row
    /// gadgets are RELVERIFY: the GADGETUP ends h80 <c>+0x546</c>'s wait, FORMAT.md "Requester 21 on the map"); the boxes
    /// after it follow at once, timed again from <paramref name="ms"/> with the clock <paramref name="clockMs"/> at the part's
    /// start (their 8-s cut moves with them). This run when no MESS window is up then (a requester-23 box has no gadget).
    /// </summary>
    public P1MapMessageRun EndWindowAt(double ms, double? clockMs)
    {
        var index = Boxes.ToList().FindIndex(b => ms >= b.StartMs && ms < b.EndMs);
        if (index < 0 || !Boxes[index].Window)
            return this;
        List<Box> done = [.. Boxes.Take(index), Boxes[index] with { EndMs = ms }];
        return Timed(_layout, _part, _rest, BoxedLines, _polls, clockMs, _soundSeconds, done, ms);
    }

    private static P1MapMessageRun Timed(MapMessageLayout layout, PartSpec part, IReadOnlyList<PartSpec> rest,
        IReadOnlySet<string> boxed, int polls, double? clockMs, Func<int, double>? soundSeconds,
        IReadOnlyList<Box>? done = null, double startMs = 0)
    {
        var boxes = new List<Box>(done ?? []);
        var at = startMs;
        foreach (var spec in part.Boxes.Skip(boxes.Count))
        {
            var soundAt = at;
            if (spec.Sound is int n)
                at += Math.Max(0, soundSeconds?.Invoke(n) ?? 0) * 1000;
            double? clock = clockMs is double c ? (c + at) / 1000 : null;
            var shown = (spec.Window && layout.Window is { } window
                ? layout.Message.ClockCut(window.TextSeconds(spec.Rows.Count, layout.Message.TicksPerSecond), clock)
                : layout.Message.ShownSeconds(spec.Rows.Count, polls, clock)) * 1000;
            boxes.Add(new Box(spec.Rows, at, at + shown)
                { Sound = spec.Sound, SoundMs = soundAt, PartyAt = spec.PartyAt, Window = spec.Window });
            at += shown;
        }
        return new P1MapMessageRun(layout, part, boxes, rest, boxed, polls, soundSeconds);
    }

    /// <summary>
    /// The boxes for <paramref name="lines"/> (a step's log, in order): each line of <see cref="MapMessageLayout.Keys"/>
    /// opens a box of one row, or of its <see cref="MapMessageLayout.Splits"/> rows; other lines are left alone.
    /// <paramref name="skip"/>: a line that is not a box although its text matches (the HEARS MONSTERS menu's title, which
    /// Core logs with the menu: h86's requester, not requester 23). <paramref name="polls"/>: the Speed's polls (h3, the
    /// combat screen's <see cref="CombatFieldLayout.LinePolls"/>). <paramref name="clockMs"/>: the machine's clock when the
    /// run starts (null: not modelled). <paramref name="dungeon"/>: the dungeon's keys (h41) instead of the overworld's (h74),
    /// and (s141) the lines between two boxes split the run into parts (<see cref="WaitLines"/>).
    /// <paramref name="soundSeconds"/> (s141): how long the pack's sound n holds the screen (0 with the Sound switch off);
    /// null: no sound waits. <paramref name="partyAt"/> (s141): line index → the cell the party stood on when it was
    /// logged, when Core has moved it since. <paramref name="windows"/> (s144): the runs of <paramref name="lines"/> Core
    /// marked as requester-21 windows (a MESS text: a timed window in the run, rows kept as logged less trailing blanks; a
    /// report: it ends the part and waits); used only when the layout has a <see cref="MapMessageLayout.Window"/>. Lines
    /// that are no box and in no window stay on the message line. Null when there is no box and no window.
    /// </summary>
    public static P1MapMessageRun? Plan(MapMessageLayout layout, IReadOnlyList<string> lines, int polls, double? clockMs,
        string? skip = null, bool dungeon = false, Func<int, double>? soundSeconds = null, Func<int, PartyCell?>? partyAt = null,
        IReadOnlyList<LogWindow>? windows = null)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(lines);
        var parts = new List<PartSpec>();
        var specs = new List<Spec>();
        var boxed = new HashSet<string>(StringComparer.Ordinal);
        var spans = windows is null
            ? new Dictionary<int, LogWindow>()
            : windows.Where(w => w.Count > 0 && w.Start >= 0 && w.Start + w.Count <= lines.Count
                    && (w.Kind == LogWindowKind.Box || layout.Window is not null)) // requester 21 needs its layout
                .GroupBy(w => w.Start).ToDictionary(g => g.Key, g => g.First());
        for (var i = 0; i < lines.Count; i++)
        {
            if (spans.TryGetValue(i, out var span))
            {
                string[] logged = [.. lines.Skip(i).Take(span.Count).Select(l => (l ?? "").Trim())];
                foreach (var row in logged.Where(r => r.Length > 0))
                    boxed.Add(row);
                if (span.Kind == LogWindowKind.Box)
                {
                    // s153: requester 23 with the rows as stored (an overworld page's blanks place it: the box centres a
                    // row by its whole length, h65 +0x1f4); the joined text the front end's line got leaves it too.
                    boxed.Add(string.Join(" ", logged.Where(r => r.Length > 0)));
                    specs.Add(new Spec([.. (span.Rows ?? logged).Select(r => r ?? "")], null, partyAt?.Invoke(i)));
                    i += span.Count - 1;
                    continue;
                }
                string[] rows = [.. lines.Skip(i).Take(span.Count).Select(l => (l ?? "").TrimEnd())];
                if (span.Kind == LogWindowKind.Report)
                {
                    // s154: a report logged as one joined line (the wilderness inn's) shows its rows as Core gave them.
                    parts.Add(new PartSpec([.. specs], span.Rows is { } given ? [.. given.Select(r => (r ?? "").TrimEnd())] : rows));
                    specs.Clear();
                }
                else
                {
                    specs.Add(new Spec(rows, null, partyAt?.Invoke(i), Window: true));
                }
                i += span.Count - 1;
                continue;
            }
            var text = lines[i]?.Trim() ?? "";
            if (text.Length == 0)
                continue;
            if ((skip is not null && text == skip.Trim()) || Rows(layout, text, dungeon) is not { } boxRows)
                continue; // the message line's
            specs.Add(new Spec(boxRows, SoundFor(layout, text, dungeon), partyAt?.Invoke(i)));
            boxed.Add(text);
        }
        if (specs.Count > 0)
            parts.Add(new PartSpec([.. specs], []));
        if (parts.Count == 0)
            return null;
        return Timed(layout, parts[0], [.. parts.Skip(1)], boxed, polls, clockMs, soundSeconds);
    }

    /// <summary>
    /// The boxes of the step or prompt answer that logged from <paramref name="mark"/> on in I's <paramref name="session"/>:
    /// on the overworld map (the session still there, or in the fight it started from there; h74's one caller is the
    /// wilderness step h59 <c>+0x3be</c>) by <see cref="MapMessageLayout.Keys"/>, a waiting HEARS MONSTERS menu's title not
    /// being a box; in the dungeon (or the fight it started from there) by <see cref="MapMessageLayout.DungeonKeys"/> (h41's
    /// events, s140), with (s141) the party on the cell it stood on until each <see cref="GameSession.DungeonMove"/> recorded
    /// after <paramref name="moveMark"/> (an earlier <see cref="GameSession.DungeonMoveSequence"/>; null: all kept) and from
    /// <paramref name="mark"/> on. Null when there is none.
    /// </summary>
    public static P1MapMessageRun? ForStep(MapMessageLayout layout, GameSession session, long mark, int polls, double? clockMs,
        Func<int, double>? soundSeconds = null, long? moveMark = null)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(session);
        if (session.Game != GameId.P1)
            return null;
        switch (session.Mode == GameMode.Combat ? session.CombatReturnMode : session.Mode)
        {
            case GameMode.Overworld:
            {
                var lines = session.LogSince(mark);
                var first = session.LogSequence - lines.Count;
                // s153: Core's windows on the overworld: a location's text pages as one requester-23 box (h59 +0x182).
                var windows = session.MapWindowsSince(mark)
                    .Select(w => new LogWindow((int)(w.AfterLine - first), w.Count, w.Kind) { Rows = w.Rows }).ToList();
                return Plan(layout, lines, polls, clockMs, session.WildernessPrompt?.Title,
                    soundSeconds: soundSeconds, windows: windows);
            }
            case GameMode.Dungeon:
            {
                var lines = session.LogSince(mark);
                var first = session.LogSequence - lines.Count; // the count before lines[0] was noted
                var moves = session.DungeonMovesSince(moveMark ?? 0).Where(m => m.AfterLine >= mark).ToList();
                // Line i was noted as number first + i + 1: before a move when that is ≤ the move's AfterLine.
                PartyCell? At(int i) => moves.FirstOrDefault(m => first + i + 1 <= m.AfterLine) is { } m
                    ? new PartyCell(m.LevelId, m.FromX, m.FromY) : null;
                // s144: Core's windows (MESS texts, reports) as runs of these lines.
                var windows = session.MapWindowsSince(mark)
                    .Select(w => new LogWindow((int)(w.AfterLine - first), w.Count, w.Kind) { Rows = w.Rows }).ToList();
                return Plan(layout, lines, polls, clockMs, dungeon: true, soundSeconds: soundSeconds, partyAt: At,
                    windows: windows);
            }
            default:
                return null;
        }
    }

    /// <summary>The string id of the out-of-fight Resurrection's line (h38 <c>+0x2ea</c>'s requester-23 box, s165).</summary>
    public const string ResurrectedKey = "core.magic.x_is_resurrected";

    /// <summary>
    /// s165: <paramref name="layout"/> with only <see cref="ResurrectedKey"/> as a box, on the overworld and in the dungeon
    /// (h38 <c>+0x2ea</c> outside a round shows "%s IS RESURRECTED" in requester 23, h44 <c>+0x466</c>: one row, the same
    /// timing); the step keys, splits, texts and sounds left out, so no step or fight line ever boxes through it.
    /// </summary>
    public static MapMessageLayout ResurrectionLayout(MapMessageLayout layout)
    {
        ArgumentNullException.ThrowIfNull(layout);
        return layout with
        {
            Keys = [ResurrectedKey], DungeonKeys = [ResurrectedKey],
            Splits = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal),
            Texts = new Dictionary<string, string>(StringComparer.Ordinal),
            Sounds = new Dictionary<string, int>(StringComparer.Ordinal),
        };
    }

    /// <summary>s141: the sound the original plays before <paramref name="text"/>'s box (<see cref="MapMessageLayout.Sounds"/>), or null.</summary>
    public static int? SoundFor(MapMessageLayout layout, string text, bool dungeon = false)
    {
        ArgumentNullException.ThrowIfNull(layout);
        foreach (var key in layout.KeysFor(dungeon))
            if (Arguments(GameText.S(key), text.Trim()) is not null)
                return layout.Sounds.TryGetValue(key, out var n) ? n : null;
        return null;
    }

    /// <summary>
    /// The box rows for <paramref name="text"/>, or null when it is none of the layout's keys (the overworld's, or with
    /// <paramref name="dungeon"/> the dungeon's). The rows are the key's <see cref="MapMessageLayout.Splits"/>, else its
    /// <see cref="MapMessageLayout.Texts"/> override (s157), else the text as logged.
    /// </summary>
    public static IReadOnlyList<string>? Rows(MapMessageLayout layout, string text, bool dungeon = false)
    {
        ArgumentNullException.ThrowIfNull(layout);
        foreach (var key in layout.KeysFor(dungeon))
        {
            if (Arguments(GameText.S(key), text.Trim()) is not { } args)
                continue;
            if (!layout.Splits.TryGetValue(key, out var split))
                // s157: the original's own text where it differs from Core's (I Amiga: `p1field.box.you_surprise_them`, "THAT IS A WALL").
                return [layout.Texts.TryGetValue(key, out var own) ? GameText.F(own, args).Trim() : text.Trim()];
            return [.. split.Select(id => GameText.F(id, args).Trim())];
        }
        return null;
    }

    /// <summary>The arguments that make <paramref name="template"/> (with {0}, {1} …) read <paramref name="text"/>, or null.</summary>
    private static object[]? Arguments(string template, string text)
    {
        var pattern = "^" + Regex.Replace(Regex.Escape(template.Trim()), @"\\\{(\d+)[^}]*}", m => $"(?<a{m.Groups[1].Value}>.*?)") + "$";
        var match = Regex.Match(text, pattern);
        if (!match.Success)
            return null;
        var count = Regex.Matches(template, @"\{(\d+)").Select(m => int.Parse(m.Groups[1].Value) + 1).DefaultIfEmpty(0).Max();
        return [.. Enumerable.Range(0, count).Select(i => (object)(match.Groups[$"a{i}"].Success ? match.Groups[$"a{i}"].Value : ""))];
    }
}
