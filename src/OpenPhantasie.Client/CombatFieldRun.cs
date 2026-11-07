using OpenPhantasie.Core;

namespace OpenPhantasie.Client;

/// <summary>
/// The Speed menu (III Amiga, word <c>0x240f6</c>: Fast 1, Normal 2, Slow 3; I Amiga; II ST <c>[0x1fefa]</c> 5 / 10 / 1000 ms,
/// <see cref="OpenPhantasie.Data.StCombatLines.SpeedMs"/>).
/// </summary>
public enum AmigaCombatSpeed
{
    Fast = 1,
    Normal = 2,
    Slow = 3,
}

/// <summary>
/// One combat round played on Phantasie III Amiga's combat field (FORMAT.md §8, s117), one line at a time. No MonoGame types:
/// tests drive it with milliseconds.
/// <list type="bullet">
/// <item>A line (<c>0x166c6(text, side)</c>) prints centred on text row 24 in colour 11 (side 1, the party: blue) or 12
/// (side 0, the monsters: red), stays 30 ticks (dos <c>Delay</c>, 1/50 s) × Speed, then its row is cleared.</item>
/// <item>A melee swing (<see cref="CombatSwing"/>) first shows the attacker's attack frame for 21 ticks × Speed
/// (<c>0x1b7c4</c> / <c>0x1b6be</c>: the attack frame, the wait, the idle frame), then the line with sound 15 (hit) or 18
/// (miss) started as it prints.</item>
/// <item>A cast line (<see cref="CombatLineTag.Sound"/>: the party's <c>0x1620a</c>, the monsters' spell lines in
/// <c>0x1673e</c>) starts sound 21 as it prints (s127).</item>
/// <item>Speed (<c>0x1bacc</c>): Slow doubles a wait, Fast takes a quarter (<c>asr #2</c>), Normal keeps it.</item>
/// <item>A party member or monster whose "dies" line has printed is shown dead from then on (the member as the grave).</item>
/// <item>A spell flash (<see cref="CombatFlashRun"/>) plays inside the round, just before the line its hit came before
/// (<see cref="CombatHit.Line"/>): the cast line, then per target its flash, "takes n damage!", "dies!" (seen s120,
/// FORMAT.md §8.6). Its length does not follow Speed (the wait word <c>0x241e8</c>); a flash with no known line, or
/// one after the round's last line, plays after the round's lines.</item>
/// </list>
/// Lines that are not a swing belong to the last tagged actor (<see cref="CombatLineTag"/>); the side of a line printed
/// by the damage and status routines (not a swing) follows that actor <c>[~]</c>.
/// </summary>
public sealed class CombatFieldRun
{
    /// <summary>Ticks a line stays (<c>0x16714</c>: <c>pea 0x1e</c>).</summary>
    public const int LineTicks = 30;

    /// <summary>Ticks the attack frame stays before the idle frame comes back (<c>0x1b7fc</c> / <c>0x1b79e</c>: <c>pea 0x15</c>).</summary>
    public const int AttackTicks = 21;

    /// <summary>Melee sounds (III Amiga sound table, FORMAT.md §7.2).</summary>
    public const int HitSound = 15;
    public const int MissSound = 18;

    /// <summary>The combat palette's party and monster line colours (<c>0x166ca</c>: 11 for side 1, else 12).</summary>
    public const int PartyColour = 11;
    public const int MonsterColour = 12;

    /// <summary>One printed line: its text, whose it is, when it starts and how long each part lasts.</summary>
    public sealed record Line(string Text, bool Party, Character? Member, Monster? Monster, CombatSwing Swing,
        double StartMs, double AttackMs, double ShowMs, int Sound = 0)
    {
        public double EndMs => StartMs + AttackMs + ShowMs;
    }

    /// <summary>
    /// What the field shows at a moment. <see cref="Flash"/>: the flash playing and how many of its frames are drawn, or null.
    /// </summary>
    public sealed record State(Line? Line, bool TextShown, Character? AttackingMember, Monster? AttackingMonster,
        IReadOnlySet<Character> DeadMembers, IReadOnlySet<Monster> DeadMonsters,
        (CombatFlashRun.Flash Flash, int Frames)? Flash = null);

    /// <summary>One flash of the round on the field's timeline.</summary>
    public sealed record FlashSlot(CombatFlashRun.Flash Flash, double StartMs, double LengthMs)
    {
        public double EndMs => StartMs + LengthMs;
    }

    private readonly List<(double Ms, Character? Member, Monster? Monster)> _deaths = [];

    private CombatFieldRun(IReadOnlyList<Line> lines, IReadOnlySet<Character> deadAtStart, CombatFlashRun? flashRun,
        IReadOnlyList<FlashSlot> flashes)
    {
        Lines = lines;
        DeadAtStart = deadAtStart;
        FlashRun = flashRun;
        Flashes = flashes;
    }

    public IReadOnlyList<Line> Lines { get; }

    /// <summary>The round's flashes (their layout), or null when none flashes.</summary>
    public CombatFlashRun? FlashRun { get; }

    /// <summary>The round's flashes in the order they play, each at its place among the lines.</summary>
    public IReadOnlyList<FlashSlot> Flashes { get; }

    /// <summary>Members already dead when the round began (their grave shows the whole round).</summary>
    public IReadOnlySet<Character> DeadAtStart { get; }

    public double LengthMs => Math.Max(Lines.Count == 0 ? 0 : Lines[^1].EndMs, Flashes.Count == 0 ? 0 : Flashes[^1].EndMs);

    /// <summary>Milliseconds of <paramref name="ticks"/> 1/50 s ticks at <paramref name="speed"/> (<c>0x1bacc</c>).</summary>
    public static double TicksMs(int ticks, AmigaCombatSpeed speed) => speed switch
    {
        AmigaCombatSpeed.Slow => ticks * 2,
        AmigaCombatSpeed.Fast => ticks >> 2,
        _ => ticks,
    } * 20.0;

    /// <summary>
    /// The run for a round: its log <paramref name="log"/> and tags <paramref name="tags"/> (<see cref="CombatResult"/>),
    /// then <paramref name="after"/> (lines the session noted after the round, e.g. the victory line: the last actor's
    /// side). <paramref name="deadBefore"/>: the members dead when the round began (taken before it was resolved).
    /// <paramref name="party"/> / <paramref name="monsters"/>: who stood on the field when the round began, so a
    /// "dies" line (<c>core.combat.x_dies</c> with the name) can be matched to its member, or to the next monster of that
    /// name that is dead after the round (<paramref name="deadAfter"/>). <paramref name="flashRun"/>: the round's spell
    /// flashes (<see cref="CombatFlashRun.Plan"/>), each played before its hit's line.
    /// </summary>
    public static CombatFieldRun Plan(IReadOnlyList<string> log, IReadOnlyList<CombatLineTag> tags, IReadOnlyList<string> after,
        IReadOnlyList<Character> party, IEnumerable<Character> deadBefore, IReadOnlyList<Monster> monsters,
        Func<Monster, bool> deadAfter, AmigaCombatSpeed speed, CombatFlashRun? flashRun = null)
    {
        ArgumentNullException.ThrowIfNull(deadBefore);
        ArgumentNullException.ThrowIfNull(log);
        ArgumentNullException.ThrowIfNull(tags);
        ArgumentNullException.ThrowIfNull(after);
        ArgumentNullException.ThrowIfNull(party);
        ArgumentNullException.ThrowIfNull(monsters);
        ArgumentNullException.ThrowIfNull(deadAfter);
        var deadAtStart = new HashSet<Character>(deadBefore, ReferenceEqualityComparer.Instance);
        var lines = new List<Line>();
        var deaths = new List<(double, Character?, Monster?)>();
        var unclaimed = monsters.Where(deadAfter).ToList();
        Character? member = null;
        Monster? monster = null;
        var at = 0.0;
        var flashes = new List<FlashSlot>();
        var pending = new Queue<CombatFlashRun.Flash>(flashRun?.Items ?? []);
        void PlayFlashes(Func<CombatFlashRun.Flash, bool> due)
        {
            while (pending.Count > 0 && due(pending.Peek()))
            {
                var slot = new FlashSlot(pending.Dequeue(), at, flashRun!.FlashMs);
                flashes.Add(slot);
                at = slot.EndMs;
            }
        }
        var all = log.Select((text, i) => (text, i)).Concat(after.Select(text => (text, -1)));
        var logDone = false;
        foreach (var (text, i) in all)
        {
            if (i >= 0)
                PlayFlashes(f => f.Line >= 0 && f.Line <= i); // in hit order: a flash never jumps one noted before it
            else if (!logDone)
            {
                logDone = true;
                PlayFlashes(_ => true); // the round's last hits (and any with no known line) before the lines noted after it
            }
            var swing = CombatSwing.None;
            var sound = 0;
            if (i >= 0)
                foreach (var tag in tags.Where(t => t.Line == i))
                {
                    (member, monster) = (tag.Member, tag.Monster);
                    swing = tag.Swing;
                    if (tag.Sound > 0)
                        sound = tag.Sound;
                }
            if (string.IsNullOrWhiteSpace(text))
                continue;
            var attack = swing != CombatSwing.None ? TicksMs(AttackTicks, speed) : 0;
            var line = new Line(text, monster is null, member, monster, swing, at, attack, TicksMs(LineTicks, speed), sound);
            lines.Add(line);
            var printed = line.StartMs + line.AttackMs;
            if (party.FirstOrDefault(m => !deadAtStart.Contains(m) && !deaths.Any(d => ReferenceEquals(d.Item2, m))
                    && text == GameText.F("core.combat.x_dies", m.Name)) is { } dead)
                deaths.Add((printed, dead, null));
            else if (unclaimed.FirstOrDefault(m => text == GameText.F("core.combat.x_dies", m.Name)) is { } slain)
            {
                unclaimed.Remove(slain);
                deaths.Add((printed, null, slain));
            }
            at = line.EndMs;
        }
        PlayFlashes(_ => true);
        var run = new CombatFieldRun(lines, deadAtStart, flashRun, flashes);
        run._deaths.AddRange(deaths);
        return run;
    }

    /// <summary>The field <paramref name="ms"/> into the run (past the end: the last moment, no line).</summary>
    public State At(double ms)
    {
        var line = Lines.FirstOrDefault(l => ms >= l.StartMs && ms < l.EndMs);
        var attacking = line is not null && ms < line.StartMs + line.AttackMs;
        var deadMembers = new HashSet<Character>(DeadAtStart, ReferenceEqualityComparer.Instance);
        var deadMonsters = new HashSet<Monster>(ReferenceEqualityComparer.Instance);
        foreach (var (when, m, mon) in _deaths)
        {
            if (when > ms)
                continue;
            if (m is not null)
                deadMembers.Add(m);
            if (mon is not null)
                deadMonsters.Add(mon);
        }
        (CombatFlashRun.Flash, int)? flash = null;
        if (FlashRun is { } flashRun && Flashes.FirstOrDefault(f => ms >= f.StartMs && ms < f.EndMs) is { } slot)
            flash = (slot.Flash, flashRun.FramesAt((ms - slot.StartMs) / 1000.0 * flashRun.Layout.VblHz + 1e-6));
        return new State(line, line is not null && !attacking, attacking ? line!.Member : null,
            attacking ? line!.Monster : null, deadMembers, deadMonsters, flash);
    }

    /// <summary>
    /// (VBL at <paramref name="vblHz"/>, sound) of each swing's or cast line's sound, started as its line prints, and each flash's sound
    /// (<see cref="CombatFlashRun.SoundPlays"/>: before its frame <see cref="OpenPhantasie.Data.CombatFlashLayout.SoundFrame"/>), in time order.
    /// </summary>
    public IReadOnlyList<(int Frame, int Sound)> SoundPlays(double vblHz)
    {
        var plays = Lines.Where(l => l.Swing != CombatSwing.None || l.Sound > 0)
            .Select(l => ((int)Math.Round((l.StartMs + l.AttackMs) / 1000.0 * vblHz),
                l.Sound > 0 ? l.Sound : l.Swing == CombatSwing.Hit ? HitSound : MissSound)).ToList();
        if (FlashRun is { Layout: { Sound: int sound } layout })
            plays.AddRange(Flashes.Select(f => ((int)Math.Round(f.StartMs / 1000.0 * vblHz
                + (double)layout.SoundFrame * layout.FrameVbl * vblHz / layout.VblHz), sound)));
        return [.. plays.OrderBy(p => p.Item1)];
    }
}

/// <summary>
/// Who stands on III Amiga's combat field when a round begins (s117): the party in slot order (<see cref="DeadBefore"/>
/// show as the grave), rank 1's and rank 2's living monsters (the Amiga draws each group's members in a row,
/// <c>0x1b150</c>), each row taken before the round's swap and compaction.
/// </summary>
public sealed record CombatFieldSnapshot(IReadOnlyList<Character> Party, IReadOnlyList<Character> DeadBefore,
    IReadOnlyList<IReadOnlyList<Monster>> Groups)
{
    /// <summary>The field of <paramref name="combat"/> now: rank 1 (rank index 0) first, then rank 2.</summary>
    public static CombatFieldSnapshot Of(Party party, CombatEncounter combat)
    {
        ArgumentNullException.ThrowIfNull(party);
        ArgumentNullException.ThrowIfNull(combat);
        var members = party.Members.Take(7).ToList();
        var groups = combat.Ranks.Where(r => r.RankIndex is 0 or 1).OrderBy(r => r.RankIndex)
            .Select(r => (IReadOnlyList<Monster>)r.Members.Where(m => m.IsAlive).ToList()).ToList();
        return new CombatFieldSnapshot(members, members.Where(m => m.Status == CharacterStatus.Dead).ToList(), groups);
    }
}
