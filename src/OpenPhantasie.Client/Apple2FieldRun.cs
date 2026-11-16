using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client;

/// <summary>
/// One combat round played on the Apple II engine's combat field (s224 I, s226 II; OUTDOORS / DUNGON and II's OUTDOORS2 /
/// DUNGON2 1560–2005, 4000–4150, <c>reverse-engineering/apple2/phantasie1/GRAPHICS.md</c> section 7, II's rock
/// <c>../phantasie2/GRAPHICS.md</c> section 7), line by line. No MonoGame types: tests drive it with milliseconds.
/// <list type="bullet">
/// <item>Poses stay until the round ends (the next round's 1525 / 1550 redraw them): a member's swing shows arm 22 from
/// that swing on (1695), a cast arm 21 (1760); a monster that swings, casts or breathes shows pose B from then on (2006
/// via 1895 / 1940).</item>
/// <item>A party cast: the sparkle (shape 23) is up while the cast's tones play (1762–1764, <see cref="Apple2FieldLayout.SparkleMs"/>;
/// none with the sound off: 7570 returns at once).</item>
/// <item>A party damage spell's hit (<see cref="CombatHit.SpellDamage"/>): after its "TAKES n DAMAGE" line, shape 24 on
/// the monster at its SCALE for 500 loops (4132).</item>
/// <item>II's tossed rock (<see cref="Apple2FieldLayout.Rock"/>; none for I): "X THROWS A ROCK" opens the thrower's turn
/// (HOME) with arm 21 and no pause (1623 GOSUB 1760); its "X MISSES" shows the rock at the miss point for its loops, then
/// the line's pause (1628, 4150); a rock's hit (<see cref="DamageSource.PartyRock"/>) shows the rock on the monster at its
/// SCALE after "TAKES n DAMAGE" (1627 SP = 2, 4132).</item>
/// <item>A monster's "DIES" erases it at once (4145); a member's leaves the figure drawn until the round's end (4020 draws
/// nothing; 1530 skips the dead at the next set-up).</item>
/// <item>The text window (rows 20–23) is cleared (HOME) at each member's and monster's turn and at each swing (1610 / 1690
/// / 1860): every tagged line and every party cast line starts a fresh window; the rows scroll past the fourth.</item>
/// <item>Each line then holds the TIME LAG pause (PA = 120 × TIME LAG loops, <see cref="Apple2FieldLayout.LinePauseMs"/>),
/// but a "HITS" swing line, which its damage line follows at once (1710 → 4120, 1925 → 4005) `[~]` (one pause a line:
/// the Apple pauses after a swing's or a turn's lines, not after each).</item>
/// </list>
/// </summary>
public sealed class Apple2FieldRun
{
    /// <summary>One printed line on the round's timeline (ms from the run's start); <see cref="Home"/>: the window cleared first.</summary>
    public sealed record Line(string Text, bool Home, double StartMs, double EndMs);

    /// <summary>What the field shows at a moment.</summary>
    /// <param name="HitRock">The <see cref="Hit"/> is II's rock (shape 59), not the spell hit (24).</param>
    /// <param name="RockMiss">II's missed rock is up at the layout's miss point (1628).</param>
    public sealed record State(IReadOnlyList<string> Window, IReadOnlySet<Character> Swung, IReadOnlySet<Character> Cast,
        Character? Sparkle, IReadOnlySet<Monster> Posed, IReadOnlySet<Monster> Gone, Monster? Hit, bool HitRock = false,
        bool RockMiss = false);

    private enum Kind { Swing, Cast, Sparkle, Pose, Gone, Hit, RockHit, RockMiss }

    private sealed record Event(double StartMs, double EndMs, Kind Kind, Character? Member, Monster? Monster);

    private readonly List<Event> _events = [];
    private readonly int _columns;
    private readonly int _rows;

    private Apple2FieldRun(IReadOnlyList<Line> lines, int columns, int rows)
    {
        Lines = lines;
        _columns = columns;
        _rows = rows;
    }

    public IReadOnlyList<Line> Lines { get; }

    public double LengthMs => Math.Max(Lines.Count == 0 ? 0 : Lines[^1].EndMs, _events.Count == 0 ? 0 : _events.Max(e =>
        double.IsPositiveInfinity(e.EndMs) ? e.StartMs : e.EndMs));

    /// <summary>
    /// The run for a round: its <paramref name="log"/>, <paramref name="tags"/> and <paramref name="hits"/>
    /// (<see cref="CombatResult"/>), then <paramref name="after"/> (lines the session noted after it: the victory line);
    /// <paramref name="snapshot"/>: who stood on the field when the round began. <paramref name="timeLag"/>: the game's
    /// TIME LAG; <paramref name="sound"/>: the Sound switch (the sparkle shows only while the tones play).
    /// </summary>
    public static Apple2FieldRun Plan(Apple2FieldLayout layout, IReadOnlyList<string> log, IReadOnlyList<CombatLineTag> tags,
        IReadOnlyList<CombatHit> hits, IReadOnlyList<string> after, Apple2FieldSnapshot snapshot, int timeLag, bool sound)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(log);
        ArgumentNullException.ThrowIfNull(tags);
        ArgumentNullException.ThrowIfNull(hits);
        ArgumentNullException.ThrowIfNull(after);
        ArgumentNullException.ThrowIfNull(snapshot);
        var pause = layout.LinePauseMs(timeLag);
        var lines = new List<Line>();
        var run = new Apple2FieldRun(lines, layout.Columns, layout.TextRows);
        var unclaimed = snapshot.Ranks.SelectMany(r => r).Where(m => !m.IsAlive).ToList();
        var standing = snapshot.Party.Where(m => !snapshot.DeadBefore.Contains(m)).ToList();
        var at = 0.0;
        Character? thrower = null; // II: the member whose rock is in the air (1623 until its MISSES / damage line)
        void Add(Kind kind, double start, Character? member = null, Monster? monster = null, double end = double.PositiveInfinity) =>
            run._events.Add(new Event(start, end, kind, member, monster));
        var all = log.Select((text, i) => (text, i)).Concat(after.Select(text => (text, -1)));
        foreach (var (text, i) in all)
        {
            if (string.IsNullOrWhiteSpace(text))
                continue;
            var tag = i < 0 ? null : tags.LastOrDefault(t => t.Line == i);
            var home = tag is not null;
            var hold = pause;
            var rockMiss = thrower is { } t0 && tag is null && text == GameText.F("core.combat.x_misses", t0.Name);
            if (tag is not null)
                thrower = null;
            if (tag is { Swing: CombatSwing.Hit })
                hold = 0; // its damage line follows at once (1710 -> 4120, 1925 -> 4005)
            if (tag is { Member: { } swinger, Swing: not CombatSwing.None })
                Add(Kind.Swing, at, swinger);
            else if (tag is { Monster: { } mon }
                     && (tag.Swing != CombatSwing.None || text == GameText.F("core.combat.x_casts_a_spell", mon.Name)
                         || text == GameText.F("core.combat.x_breathes", mon.Name)))
                Add(Kind.Pose, at, monster: mon);
            else if ((tag is { Member: { } tagged, Swing: CombatSwing.None } ? [tagged] : tag is null ? standing : [])
                     .FirstOrDefault(m => text == GameText.F("core.combat.x_casts_a_spell", m.Name)) is { } caster)
            {
                // Core tags I's cast line with its caster (P3Fight.AddCastLine); an untagged one is matched by name.
                home = true;
                Add(Kind.Cast, at, caster);
                hold = sound ? layout.SparkleMs : 0;
                if (hold > 0)
                    Add(Kind.Sparkle, at, caster, end: at + hold);
            }
            else if (layout.Rock is not null && tag is null
                     && standing.FirstOrDefault(m => text == GameText.F("core.combat.x_throws_a_rock", m.Name)) is { } tosser)
            {
                // 1610 HOME opens the turn; 1623 GOSUB 1760: arm 21, then straight on to the hit roll (no pause).
                home = true;
                thrower = tosser;
                Add(Kind.Cast, at, tosser);
                hold = 0;
            }
            else if (rockMiss && layout.Rock is { } rock)
            {
                // 1628: XDRAW 59 at the miss point, 250 loops, XDRAW off, then 4150's PA pause.
                var flight = rock.MissLoops * layout.LoopMs;
                Add(Kind.RockMiss, at, end: at + flight);
                hold = flight + pause;
                thrower = null;
            }
            if (unclaimed.FirstOrDefault(m => text == GameText.F("core.combat.x_dies", m.Name)) is { } slain)
            {
                unclaimed.Remove(slain);
                Add(Kind.Gone, at, monster: slain);
            }
            if (i >= 0)
            {
                foreach (var hit in hits.Where(h => h.Line == i && h is { Source: DamageSource.PartySpell, SpellDamage: >= 0, Monster: not null }))
                {
                    Add(Kind.Hit, at, monster: hit.Monster, end: at + layout.HitMs);
                    hold += layout.HitMs;
                }
                if (layout.Rock is { } rock)
                    foreach (var hit in hits.Where(h => h.Line == i && h is { Source: DamageSource.PartyRock, Monster: not null }))
                    {
                        var ms = rock.HitLoops * layout.LoopMs; // 4132 with SP = 2: shape 59 at the monster's SCALE
                        Add(Kind.RockHit, at, monster: hit.Monster, end: at + ms);
                        hold += ms;
                        thrower = null;
                    }
            }
            lines.Add(new Line(text, home, at, at + hold));
            at += hold;
        }
        return run;
    }

    /// <summary>The field <paramref name="ms"/> into the run.</summary>
    public State At(double ms)
    {
        HashSet<Character> swung = new(ReferenceEqualityComparer.Instance), cast = new(ReferenceEqualityComparer.Instance);
        HashSet<Monster> posed = new(ReferenceEqualityComparer.Instance), gone = new(ReferenceEqualityComparer.Instance);
        Character? sparkle = null;
        Monster? hit = null;
        bool hitRock = false, rockMiss = false;
        foreach (var e in _events.Where(e => e.StartMs <= ms))
        {
            var on = ms < e.EndMs;
            switch (e.Kind)
            {
                case Kind.Swing:
                    swung.Add(e.Member!);
                    cast.Remove(e.Member!);
                    break;
                case Kind.Cast:
                    cast.Add(e.Member!);
                    swung.Remove(e.Member!);
                    break;
                case Kind.Sparkle when on:
                    sparkle = e.Member;
                    break;
                case Kind.Pose:
                    posed.Add(e.Monster!);
                    break;
                case Kind.Gone:
                    gone.Add(e.Monster!);
                    break;
                case Kind.Hit when on:
                    hit = e.Monster;
                    hitRock = false;
                    break;
                case Kind.RockHit when on:
                    hit = e.Monster;
                    hitRock = true;
                    break;
                case Kind.RockMiss when on:
                    rockMiss = true;
                    break;
            }
        }
        return new State(Window(ms), swung, cast, sparkle, posed, gone, hit, hitRock, rockMiss);
    }

    /// <summary>The text window's rows <paramref name="ms"/> into the run: the lines since the last HOME, the last rows.</summary>
    private IReadOnlyList<string> Window(double ms)
    {
        var shown = Lines.Where(l => l.StartMs <= ms).ToList();
        if (shown.Count == 0)
            return [];
        var from = shown.FindLastIndex(l => l.Home);
        var rows = shown.Skip(Math.Max(0, from)).SelectMany(l => Wrap(l.Text, _columns)).ToList();
        return rows.Skip(Math.Max(0, rows.Count - _rows)).ToList();
    }

    /// <summary>A line on the Apple's 40-column text page: whole words to a row, a word longer than a row cut.</summary>
    public static IReadOnlyList<string> Wrap(string text, int columns)
    {
        ArgumentNullException.ThrowIfNull(text);
        var rows = new List<string>();
        var row = "";
        foreach (var word in text.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var w = word;
            while (w.Length > columns)
            {
                if (row.Length > 0)
                {
                    rows.Add(row);
                    row = "";
                }
                rows.Add(w[..columns]);
                w = w[columns..];
            }
            if (row.Length == 0)
                row = w;
            else if (row.Length + 1 + w.Length <= columns)
                row += " " + w;
            else
            {
                rows.Add(row);
                row = w;
            }
        }
        if (row.Length > 0)
            rows.Add(row);
        return rows;
    }
}

/// <summary>
/// Who stands on the Apple II field (I and II) when a round begins (s224): the party in slot order (slot k at x 19·k, 1525; the
/// members dead then are not drawn, their slot stays empty), the parrying members (arm 21 from the set-up, 1532: Core's
/// order holds PARRY only when the parry roll at the orders succeeded, as 1430–1435), the elemental (its body), and each
/// rank's living monsters, front rank first (1480–1550; Core's ranks stand for the Apple's size order, 1047).
/// </summary>
public sealed record Apple2FieldSnapshot(IReadOnlyList<Character> Party, IReadOnlySet<Character> DeadBefore,
    IReadOnlySet<Character> Parrying, Character? Elemental, IReadOnlyList<IReadOnlyList<Monster>> Ranks)
{
    public static Apple2FieldSnapshot Of(Party party, CombatEncounter combat)
    {
        ArgumentNullException.ThrowIfNull(party);
        ArgumentNullException.ThrowIfNull(combat);
        var members = party.Members.ToList();
        var dead = new HashSet<Character>(members.Where(m => m.Status == CharacterStatus.Dead), ReferenceEqualityComparer.Instance);
        var parrying = new HashSet<Character>(ReferenceEqualityComparer.Instance);
        for (var i = 0; i < members.Count && i < combat.Prep.Length; i++)
            if (CombatAction.IsParry(combat.Prep[i].PackedAction))
                parrying.Add(members[i]);
        var ranks = combat.Ranks.Where(r => !r.IsWiped)
            .Select(r => (IReadOnlyList<Monster>)r.Members.Where(m => m.IsAlive).ToList()).ToList();
        return new Apple2FieldSnapshot(members, dead, parrying, party.Elemental, ranks);
    }
}
