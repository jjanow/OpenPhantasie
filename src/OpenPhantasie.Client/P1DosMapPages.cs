using System.Text.RegularExpressions;
using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client;

/// <summary>
/// s154: Phantasie I's map texts as DOS shows them, for the path with AMIGA SCREENS off (DOS <c>message-and-text.md</c>
/// "Text boxes, key waits and timed lines"): a step's lines are paged, one page per DOS box, and every page waits for a key
/// (COM <c>0x37a0</c>). A page is a centred band line (<see cref="DosMapBoxes"/>: OV2 <c>0xda83</c> / <c>0xdad1</c>: the
/// wall, a lock, a disarm, the stairs, script ops 1 / 3 / 7 / 9 / 12 / 14 / 17, the wilderness encounter lines, STYX /
/// OCEAN), or a run Core marked (<see cref="GameSession.MapWindow"/>: a MESS text <c>0x2a95</c>, a damage report "IN THE
/// TRAP:" / "REPORT:" / "SWIMMING:", a location's text pages). The other lines (message cells, the remaster's own lines)
/// stay on the message line, the last of them shown (<see cref="BarLine"/>). Nothing is dropped: every logged line is on a
/// page or on the bar. No MonoGame types.
/// </summary>
public sealed class P1DosMapPages
{
    /// <summary>One page: its rows as shown and what it is.</summary>
    public sealed record Page(IReadOnlyList<string> Rows, LogWindowKind Kind)
    {
        /// <summary>
        /// s156: the dungeon cell the party stands on while this page waits, when Core has moved it since (DOS draws op 12's
        /// and a jammed lock's band before the move); null: where Core has it.
        /// </summary>
        public P1MapMessageRun.PartyCell? PartyAt { get; init; }

        /// <summary>
        /// s156: a box page's rows as stored (a location sign's padded 39-character rows), or null. DOS centres a row as
        /// stored, its blanks counted (live s45: the Red Pony sign from columns 2 and 9).
        /// </summary>
        public IReadOnlyList<string>? Stored { get; init; }
    }

    /// <summary>The pages in log order.</summary>
    public IReadOnlyList<Page> Pages { get; }

    /// <summary>The logged lines (trimmed) the pages show: the message line leaves them out.</summary>
    public IReadOnlySet<string> PagedLines { get; }

    /// <summary>The last logged line on no page (DOS's row 24 under the boxes), or null when every line is paged.</summary>
    public string? BarLine { get; }

    /// <summary>
    /// s156: what the message line (DOS's row 24) shows while and after the pages: the last line on no page, else the line
    /// that was there before the step (<paramref name="before"/>; DOS clears row 24 only for a new row-24 line, the boxes
    /// restore the screen under them), else blank.
    /// </summary>
    public string MessageLine(string? before) => BarLine ?? before ?? "";

    private P1DosMapPages(IReadOnlyList<Page> pages, IReadOnlySet<string> paged, string? bar)
    {
        Pages = pages;
        PagedLines = paged;
        BarLine = bar;
    }

    /// <summary>
    /// The pages of <paramref name="lines"/> (one step's log, in order): <paramref name="windows"/> are Core's marked runs
    /// (starts relative to <paramref name="lines"/>); a line matching one of <paramref name="boxes"/>' keys (the dungeon's
    /// with <paramref name="dungeon"/>) is a band page of its rows (<see cref="DosMapBoxes.Splits"/>); <paramref name="skip"/>
    /// is a line that is a menu's title, not a band (the HEARS MONSTERS question). Null when there is no page.
    /// </summary>
    public static P1DosMapPages? Plan(DosMapBoxes boxes, IReadOnlyList<string> lines, IReadOnlyList<LogWindow>? windows,
        bool dungeon, string? skip = null, Func<int, P1MapMessageRun.PartyCell?>? partyAt = null)
    {
        ArgumentNullException.ThrowIfNull(boxes);
        ArgumentNullException.ThrowIfNull(lines);
        var spans = (windows ?? [])
            .Where(w => w.Count > 0 && w.Start >= 0 && w.Start + w.Count <= lines.Count)
            .GroupBy(w => w.Start).ToDictionary(g => g.Key, g => g.First());
        var pages = new List<Page>();
        var paged = new HashSet<string>(StringComparer.Ordinal);
        string? bar = null;
        for (var i = 0; i < lines.Count; i++)
        {
            if (spans.TryGetValue(i, out var span))
            {
                string[] logged = [.. lines.Skip(i).Take(span.Count).Select(l => l ?? "")];
                foreach (var row in logged.Select(r => r.Trim()).Where(r => r.Length > 0))
                    paged.Add(row);
                if (span.Kind == LogWindowKind.Box)
                    paged.Add(string.Join(" ", logged.Select(r => r.Trim()).Where(r => r.Length > 0)));
                // DOS prints the rows as stored; the remaster's panel takes them trimmed at the end (a page's leading blanks
                // place it in DOS's band, the panel centres nothing).
                var rows = (span.Rows ?? logged).Select(r => (r ?? "").TrimEnd()).ToList();
                IReadOnlyList<string>? stored = null;
                if (span.Kind == LogWindowKind.Box)
                {
                    if (span.Rows is { } asStored)
                        stored = [.. asStored.Select(r => r ?? "").Where(r => r.Trim().Length > 0)];
                    rows = [.. rows.Select(r => r.Trim()).Where(r => r.Length > 0)];
                }
                else
                    while (rows.Count > 0 && rows[^1].Length == 0)
                        rows.RemoveAt(rows.Count - 1);
                if (rows.Count > 0)
                    pages.Add(new Page(rows, span.Kind) { PartyAt = partyAt?.Invoke(i), Stored = stored });
                i += span.Count - 1;
                continue;
            }
            var text = lines[i]?.Trim() ?? "";
            if (text.Length == 0)
                continue;
            if ((skip is null || text != skip.Trim()) && Rows(boxes, text, dungeon) is { } bandRows)
            {
                pages.Add(new Page(bandRows, LogWindowKind.Box) { PartyAt = partyAt?.Invoke(i) });
                paged.Add(text);
                continue;
            }
            bar = text;
        }
        return pages.Count == 0 ? null : new P1DosMapPages(pages, paged, bar);
    }

    /// <summary>
    /// The pages of the step or prompt answer that logged from <paramref name="mark"/> on in I's <paramref name="session"/>,
    /// on the overworld or in the dungeon (or the fight started from there); null when there is none.
    /// </summary>
    public static P1DosMapPages? ForStep(DosMapBoxes boxes, GameSession session, long mark, long? moveMark = null)
    {
        ArgumentNullException.ThrowIfNull(boxes);
        ArgumentNullException.ThrowIfNull(session);
        if (session.Game != GameId.P1)
            return null;
        var mode = session.Mode == GameMode.Combat ? session.CombatReturnMode : session.Mode;
        if (mode is not (GameMode.Overworld or GameMode.Dungeon))
            return null;
        var lines = session.LogSince(mark);
        var first = session.LogSequence - lines.Count;
        var windows = session.MapWindowsSince(mark)
            .Select(w => new LogWindow((int)(w.AfterLine - first), w.Count, w.Kind) { Rows = w.Rows }).ToList();
        P1MapMessageRun.PartyCell? At(int i) => null;
        Func<int, P1MapMessageRun.PartyCell?> partyAt = At;
        if (mode == GameMode.Dungeon)
        {
            // s156: as the Amiga's run (P1MapMessageRun.ForStep): line i was noted as number first + i + 1, before a move
            // when that is at most the move's AfterLine.
            var moves = session.DungeonMovesSince(moveMark ?? 0).Where(m => m.AfterLine >= mark).ToList();
            partyAt = i => moves.FirstOrDefault(m => first + i + 1 <= m.AfterLine) is { } m
                ? new P1MapMessageRun.PartyCell(m.LevelId, m.FromX, m.FromY) : null;
        }
        return Plan(boxes, lines, windows, mode == GameMode.Dungeon,
            mode == GameMode.Overworld ? session.WildernessPrompt?.Title : null, partyAt);
    }

    /// <summary>The band rows for <paramref name="text"/>, or null when it is none of the keys.</summary>
    public static IReadOnlyList<string>? Rows(DosMapBoxes boxes, string text, bool dungeon)
    {
        ArgumentNullException.ThrowIfNull(boxes);
        foreach (var key in boxes.KeysFor(dungeon))
        {
            if (Arguments(GameText.S(key), text.Trim()) is not { } args)
                continue;
            if (!boxes.Splits.TryGetValue(key, out var split))
                return [text.Trim()];
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
