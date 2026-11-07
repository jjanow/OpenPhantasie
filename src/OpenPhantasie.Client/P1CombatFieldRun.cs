using System.Text.RegularExpressions;
using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client;

/// <summary>
/// One combat round played on Phantasie I Amiga's combat screen (s132, Amiga <c>FORMAT.md</c> "The combat screen"), line by
/// line, in the order the Amiga's round (h35 / h38) draws and prints it. No MonoGame types: tests drive it with
/// milliseconds. A round (not a party option's lines) starts with the set-up (h53 <c>+0x2fc</c>, s135): the field cleared,
/// then the row member by member and the monsters one by one, a WaitTOF each (<see cref="CombatFieldLayout.SetupFrameSeconds"/>,
/// <see cref="State.SetupDrawn"/>). Per line:
/// <list type="number">
/// <item>A swing (<see cref="CombatSwing"/> tag): the attacker's attack frame for <see cref="CombatFieldLayout.AttackTicks"/>
/// dos ticks (h53 <c>+0x59e</c> / <c>+0x622</c>). A cast (<see cref="CombatFieldLineSound.Flash"/>): first the caster's
/// silhouette (<c>+0x1060</c>, <see cref="CombatFieldLayout.FlashTicks"/>, at Speed Fast
/// <see cref="CombatFieldLayout.FastFlashTicks"/>), then the attack frame.</item>
/// <item>A monster's "DIES": the monster is erased first (<c>h38 +0xe3a</c>).</item>
/// <item>The line's sounds (<see cref="CombatFieldLayout.LineSounds"/>), each holding the screen for its length (the
/// player blocks; with the Sound switch off no wait, user s129).</item>
/// <item>The text, centred in pen <see cref="CombatFieldLayout.LinePen"/>; it stays until the next line.</item>
/// <item>The wait: the Speed's polls (<see cref="CombatFieldLayout.LinePolls"/>) unless the line is short or
/// <see cref="CombatFieldLineSound.SkipsWait"/> (a monster's "DIES"; a monster's cast line, D-079). A member's "DIES" instead shows the grave, plays the death sound, waits
/// <see cref="CombatFieldLayout.DeathPolls"/> polls and re-lays the row without the member (<c>+0x85e(m, 0)</c>).</item>
/// </list>
/// Before a monster's damage line from a party damage spell (<see cref="CombatHit.SpellDamage"/>), the spell's sparkles
/// on it (<see cref="CombatFieldLayout.Sparkle"/>, h53 <c>+0xab8</c>, s133): each its sound, then the sprite for its ticks.
/// After the last line the round's end (s136): a round the monsters survive runs the set-up again (h35 <c>+0x16ac</c>: h38
/// <c>+0xe72</c> re-ranks, h71 <c>+0x1050</c>, the row and the monsters as they stand now), then the end sound
/// (<see cref="CombatFieldLayout.RoundEndSound"/>, h53 <c>+0x2da</c>); a won fight plays the end sound first (h35's exits to
/// state 12) and then h44 <c>+0x614</c>'s set-up (the groups emptied: the row only) before its boxes; a lost one only the
/// sound. The set-up's fill clears the line (<see cref="RoundEndSetupMs"/>).
/// A line of <see cref="CombatFieldMessage.Keys"/> (the fight's end, MONSTERS RUN, the Beg / Threaten / Greetings answers) is
/// not printed on the field: it opens requester 23 (h44 <c>+0x466</c> …) for its whole seconds (less when the machine's
/// clock wraps a multiple of 8 s meanwhile, <see cref="CombatFieldMessage.ShownSeconds"/>), no sound, keys swallowed; a
/// <see cref="CombatFieldMessage.Joins"/> pair shares one box. Boxes after the round's last field line follow its end (s132 /
/// s135: "MONSTERS KILLED" after the redraw).
/// </summary>
public sealed class P1CombatFieldRun
{
    /// <summary>One line on the round's timeline (milliseconds from the run's start).</summary>
    public sealed record Line(string Text, Character? Member, Monster? Monster, double StartMs, double FlashMs,
        double AttackMs, double PrintMs, double EndMs, Character? Dying, double RelaidMs, Monster? Slain)
    {
        /// <summary>Requester 23's rows when the line is a message box (shown from <see cref="PrintMs"/> to <see cref="EndMs"/>), else null.</summary>
        public IReadOnlyList<string>? Box { get; init; }

        /// <summary>When the attack frame starts (after the silhouette).</summary>
        public double AttackStartMs => StartMs + FlashMs;
    }

    /// <summary>
    /// What the field shows at a moment: the line in view (the last printed, or null), the actor in its attack frame or
    /// its silhouette, the members under their grave, the members taken off the row, the monsters erased.
    /// </summary>
    public sealed record State(string? Text, Character? AttackingMember, Monster? AttackingMonster, Character? FlashMember,
        Monster? FlashMonster, IReadOnlySet<Character> Graves, IReadOnlySet<Character> Removed, IReadOnlySet<Monster> Gone)
    {
        /// <summary>Requester 23's rows while a message box is up, else null.</summary>
        public IReadOnlyList<string>? Box { get; init; }

        /// <summary>The spell sparkle on screen, or null (s133).</summary>
        public SparkleShown? Sparkle { get; init; }

        /// <summary>
        /// While the round's set-up runs (s135): the steps done so far (0: not yet cleared; 1: the field cleared; 1 + k: k
        /// pictures of the row, then the monsters, blitted); null once it is over. The round-end set-up counts the same way.
        /// </summary>
        public int? SetupDrawn { get; init; }

        /// <summary>
        /// The round's end set-up has begun (s136): the field shows the row and the monsters as they stand after the round
        /// (re-laid, re-ranked), the line cleared.
        /// </summary>
        public bool Relaid { get; init; }

        /// <summary>The whole screen black (the bar too) while a surprise opening's sound 17 holds (s136).</summary>
        public bool Black { get; init; }

        /// <summary>
        /// The menu bar is gone (s138): a surprise opening's run follows h53 <c>+0x274</c>'s BltClear of the whole screen, and
        /// only the Party Options wait (h80 <c>+0xbfe</c>) prints the bar again, after the run's sound 18.
        /// </summary>
        public bool BarCleared { get; init; }

        /// <summary>The frame is not drawn yet: a surprise opening's first set-up draws it last (h77 <c>+0x6dc</c>, s138).</summary>
        public bool FrameHidden { get; init; }
    }

    /// <summary>
    /// The fight's start on the field when it opens with Party Options (s136: state 11, h44 <c>+0x0</c> → <c>+0xca</c> → h71
    /// <c>+0x1050</c> → h53 <c>+0x274</c>): <see cref="Black"/> while sound 17 holds the BltCleared screen (the bar too), then
    /// the set-up (<see cref="SetupDrawn"/> steps; the frame comes last, <see cref="Frame"/> stays false); the bar follows. A
    /// fight that opens with round 1 (the surprise flags) plays its black in that round's run instead
    /// (<see cref="Plan"/>'s <c>leadBlackMs</c>).
    /// </summary>
    public sealed record IntroView(bool Black, int SetupDrawn, bool Frame);

    /// <summary>
    /// The fight's start <paramref name="ms"/> after the field first shows: sound 17 holding the black for
    /// <paramref name="blackMs"/> (0 with the Sound switch off), then one set-up of <paramref name="members"/> +
    /// <paramref name="monsters"/> pictures; null once it is over (the bar then comes back: h80 <c>+0xbfe</c> prints it when
    /// the Party Options wait starts). s135's two set-ups and the bar 0.84 s later were a surprise opening: round 1, its end
    /// set-up and sound 18 (s136).
    /// </summary>
    public static IntroView? Intro(CombatFieldLayout layout, double ms, double blackMs, int members, int monsters)
    {
        ArgumentNullException.ThrowIfNull(layout);
        if (ms < blackMs)
            return new IntroView(true, 0, false);
        var frame = layout.SetupFrameSeconds * 1000;
        if (frame <= 0)
            return null;
        var steps = CombatFieldLayout.SetupSteps(members, monsters);
        var t = ms - blackMs;
        return t < steps * frame ? new IntroView(false, (int)(t / frame), false) : null;
    }

    /// <summary>When the round's lines start (after the set-up), ms from the run's start.</summary>
    public double SetupMs { get; private init; }

    /// <summary>When the round's end set-up starts (s136), ms from the run's start; null when the run has none.</summary>
    public double? RoundEndSetupMs { get; private init; }

    /// <summary>The round's end set-up's steps (<see cref="CombatFieldLayout.SetupSteps"/>).</summary>
    public int RoundEndSetupSteps { get; private init; }

    /// <summary>
    /// One sparkle of a party damage spell (h53 <c>+0xab8</c>): on <see cref="Monster"/> from <see cref="StartMs"/> to
    /// <see cref="EndMs"/>; <see cref="RandX"/> / <see cref="RandY"/> (0 … 32767) place it inside the monster's picture
    /// (<see cref="CombatFieldSparkle.Offset"/>).
    /// </summary>
    public sealed record SparkleShown(Monster Monster, double StartMs, double EndMs, int RandX, int RandY);

    private P1CombatFieldRun(IReadOnlyList<Line> lines, IReadOnlyList<(double AtMs, int Sound)> sounds, double lengthMs,
        IReadOnlyList<SparkleShown> sparkles)
    {
        Lines = lines;
        SoundPlays = sounds;
        LengthMs = lengthMs;
        Sparkles = sparkles;
    }

    public IReadOnlyList<Line> Lines { get; }

    /// <summary>The round's spell sparkles in time order (s133).</summary>
    public IReadOnlyList<SparkleShown> Sparkles { get; }

    /// <summary>(ms, sound) of every sound the round starts, in time order.</summary>
    public IReadOnlyList<(double AtMs, int Sound)> SoundPlays { get; }

    /// <summary>The whole round, the end sound included.</summary>
    public double LengthMs { get; }

    /// <summary>The last line printed on the field (message boxes left out), or null (also when the round's end set-up cleared it).</summary>
    public string? LastFieldLine => RoundEndSetupMs is null ? Lines.LastOrDefault(l => l.Box is null)?.Text : null;

    /// <summary>
    /// The run for a round's log <paramref name="log"/> with its tags <paramref name="tags"/> (Core's
    /// <see cref="CombatResult.LineTags"/>). <paramref name="party"/>: the row when the round began (members not DEAD, record
    /// order); <paramref name="monsters"/>: the monsters standing then; <paramref name="deadAfter"/>: a monster is dead after the
    /// round. <paramref name="speed"/>: 1 Fast, 2 Normal, 3 Slow, 0 = not picked (the boot value). <paramref name="soundSeconds"/>:
    /// how long sound n holds the screen (0 = Sound off: no wait). <paramref name="hits"/>: the round's hits (Core's
    /// <see cref="CombatResult.Hits"/>) for the spell sparkles; <paramref name="pick"/>(n): a presentation random number
    /// 0 … n − 1 for each sparkle's spot (not Core's combat stream; default <see cref="Random.Shared"/>).
    /// <paramref name="roundEnd"/>: false for the lines of a party option or the morale check (no round, no end sound;
    /// <see cref="CombatFieldMessage.OutsideRoundKeys"/> open the box too). <paramref name="clockMs"/>: the machine's clock when
    /// the run starts (null: not modelled); <paramref name="leadBlackMs"/> / <paramref name="opensFight"/>: a surprise opening's
    /// round 1 (<see cref="OpensFight"/>); a box that would cross a multiple of
    /// <see cref="CombatFieldMessage.ClockWrapSeconds"/> on it closes there (h80 <c>+0xd2a</c>, s136).
    /// </summary>
    public static P1CombatFieldRun Plan(CombatFieldLayout layout, IReadOnlyList<string> log, IReadOnlyList<CombatLineTag> tags,
        IReadOnlyList<Character> party, IReadOnlyList<Monster> monsters, Func<Monster, bool> deadAfter, int speed,
        Func<int, double> soundSeconds, IReadOnlyList<CombatHit>? hits = null, Func<int, int>? pick = null,
        bool roundEnd = true, double? clockMs = null, double leadBlackMs = 0, bool opensFight = false)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(log);
        ArgumentNullException.ThrowIfNull(tags);
        ArgumentNullException.ThrowIfNull(party);
        ArgumentNullException.ThrowIfNull(monsters);
        ArgumentNullException.ThrowIfNull(deadAfter);
        ArgumentNullException.ThrowIfNull(soundSeconds);
        var tick = layout.TickSeconds * 1000;
        var lead = Math.Max(0, leadBlackMs); // a surprise opening's round 1: h53 +0x274's BltClear and sound 17 first (s136)
        // A surprise opening's set-up ends with the frame's PolyDraw (h77 +0x6dc after the BltClear): one more frame with the
        // whole picture (s138 live: the frame 2 video frames after the last blit, the next fill 1 later).
        var setupSteps = CombatFieldLayout.SetupSteps(party.Count, monsters.Count) + (opensFight ? 1 : 0);
        var setupMs = lead + (roundEnd ? setupSteps * layout.SetupFrameSeconds * 1000 : 0);
        var poll = layout.PollSeconds * 1000;
        var waitMs = layout.LinePolls(speed) * poll;
        var flashMs = (speed == 1 ? layout.FastFlashTicks : layout.FlashTicks) * tick;
        var attackMs = layout.AttackTicks * tick;
        var lines = new List<Line>();
        var sounds = new List<(double, int)>();
        var unclaimed = monsters.Where(deadAfter).ToList();
        var dead = new HashSet<Character>(ReferenceEqualityComparer.Instance);
        var at = setupMs; // h53 +0x2fc: the field cleared and redrawn one picture a WaitTOF
        void Play(int n)
        {
            sounds.Add((at, n));
            at += Math.Max(0, soundSeconds(n)) * 1000;
        }
        var message = layout.Message;
        var keys = log.Select(l => string.IsNullOrEmpty(l) ? null : MessageKey(message, l, !roundEnd)).ToList();
        var lastField = Enumerable.Range(0, log.Count).LastOrDefault(i => !string.IsNullOrEmpty(log[i]) && keys[i] is null, -1);
        var ended = !roundEnd;
        double? endSetupMs = null;
        var endSetupSteps = 0;
        var frameMs = layout.SetupFrameSeconds * 1000;
        void EndRound()
        {
            if (ended)
                return;
            ended = true;
            var won = monsters.Count > 0 && monsters.All(deadAfter);
            var lost = party.Count > 0 && party.All(dead.Contains);
            void Setup()
            {
                if (lost || frameMs <= 0)
                    return;
                endSetupSteps = CombatFieldLayout.SetupSteps(party.Count - dead.Count, won ? 0 : monsters.Count(m => !deadAfter(m)));
                endSetupMs = at;
                at += endSetupSteps * frameMs;
            }
            if (!won)
                Setup(); // h35 +0x16ac: h38 +0xe72, h71 +0x1050, then h53 +0x2da
            if (layout.RoundEndSound is int end)
                Play(end);
            if (won)
                Setup(); // h53 +0x2da, state 12: h44 +0x614 -> h53 +0xfc (groups emptied), +0x274
        }
        var joined = new HashSet<int>();
        var sparkles = new List<SparkleShown>();
        pick ??= Random.Shared.Next;
        // h53 +0xab8: before the monster's damage, damage / 32 + 1 times its sound, the sprite, Delay(18), off.
        void Sparkle(int line)
        {
            if (layout.Sparkle is not { } sp || hits is null)
                return;
            foreach (var hit in hits)
            {
                if (hit.Line != line || hit.SpellDamage < 0 || hit.Source != DamageSource.PartySpell || hit.Monster is not { } target)
                    continue;
                for (var k = sp.Count(hit.SpellDamage); k > 0; k--)
                {
                    if (sp.Sound is int n)
                        Play(n);
                    var rx = pick(CombatFieldSparkle.RandMax + 1);
                    var ry = pick(CombatFieldSparkle.RandMax + 1);
                    at += sp.OnFrames * frameMs; // s153: h77 +0xf2a's WaitTOFs before the sprite shows
                    var shownFor = sp.Ticks * tick + Math.Min(1, sp.OffFrames) * frameMs; // off after +0x108e's first WaitTOF
                    sparkles.Add(new SparkleShown(target, at, at + shownFor, rx, ry));
                    at += sp.Ticks * tick + sp.OffFrames * frameMs;
                }
            }
        }
        for (var i = 0; i < log.Count; i++)
        {
            Sparkle(i);
            var text = log[i];
            var tag = tags.LastOrDefault(t => t.Line == i);
            if (string.IsNullOrEmpty(text) || joined.Contains(i))
                continue;
            if (keys[i] is { } key && message is not null)
            {
                if (i > lastField)
                    EndRound();
                var rows = new List<(int Order, string Text)> { (0, text) };
                if (message.Joins.FirstOrDefault(j => j.Contains(key)) is { } join)
                {
                    rows[0] = (IndexIn(join, key), text);
                    for (var k = i + 1; k < log.Count && rows.Count < join.Count; k++)
                    {
                        if (string.IsNullOrEmpty(log[k]))
                            continue;
                        if (keys[k] is not { } other || !join.Contains(other) || rows.Any(r => r.Order == IndexIn(join, other)))
                            break;
                        rows.Add((IndexIn(join, other), log[k]));
                        joined.Add(k);
                    }
                }
                var box = rows.OrderBy(r => r.Order).Select(r => r.Text.Trim()).ToList();
                if (message.SoundsBefore.TryGetValue(key, out var before))
                    Play(before); // s153: h44 +0x398's 2 before the morale box
                var shown = at;
                at += message.ShownSeconds(box.Count, layout.LinePolls(speed), (clockMs + at) / 1000) * 1000;
                lines.Add(new Line(string.Join(" ", box), null, null, shown, 0, 0, shown, at, null, 0, null) { Box = box });
                continue;
            }
            var start = at;
            var kind = Match(layout, text, out var name);
            var member = tag?.Member ?? (name is null ? null : party.FirstOrDefault(m => m.Name == name));
            var monster = member is null ? tag?.Monster : null;
            var isParty = member is not null || (monster is null && name is null);
            var flash = kind?.Flash == true && (member ?? (object?)monster) is not null ? flashMs : 0;
            var attack = tag is { Swing: not CombatSwing.None } || flash > 0 ? attackMs : 0;
            at += flash + attack;
            Monster? slain = null;
            Character? dying = null;
            if (text == GameText.F("core.combat.x_dies", name ?? "\0"))
            {
                dying = party.FirstOrDefault(m => !dead.Contains(m) && m.Name == name);
                if (dying is not null)
                    dead.Add(dying);
                else if (unclaimed.FirstOrDefault(m => m.Name == name) is { } m)
                {
                    unclaimed.Remove(m);
                    slain = m; // erased before its sound and line (h38 +0xe3a)
                }
            }
            if (kind is not null)
                foreach (var n in dying is not null ? [] : isParty ? kind.Party : kind.Monster)
                    Play(n);
            var print = at;
            var relaid = 0.0;
            if (dying is not null)
            {
                if (layout.MemberDeathSound is int death)
                    Play(death);
                at += layout.DeathPolls * poll;
                relaid = at;
            }
            else if (kind?.SkipsWait(isParty) != true && text.Trim().Length > layout.ShortLine)
                at += waitMs;
            lines.Add(new Line(text, member, monster, start, flash, attack, print, at, dying, relaid, slain));
            if (kind?.ThenBlank == true) // s154: h38 +0x440's +0xb80(" ", -1) clears the line after its wait
                lines.Add(new Line(" ", null, null, at, 0, 0, at, at, null, 0, null));
        }
        Sparkle(log.Count);
        EndRound();
        return new P1CombatFieldRun(lines, sounds, at, sparkles)
        {
            SetupMs = setupMs, FrameMs = frameMs, RoundEndSetupMs = endSetupMs, RoundEndSetupSteps = endSetupSteps,
            LeadBlackMs = lead, OpensFight = opensFight,
        };
    }

    private double FrameMs { get; init; }

    /// <summary>The black before the run's set-up (a surprise opening's sound 17, s136); 0 = none.</summary>
    public double LeadBlackMs { get; private init; }

    /// <summary>
    /// The run is a surprise opening's round 1 (s136 / s138): its set-up is the fight's first (h53 <c>+0x274</c>: BltClear,
    /// sound 17 for <see cref="LeadBlackMs"/>, 0 with the Sound switch off), so the frame comes last and the bar stays
    /// cleared for the whole run (<see cref="State.BarCleared"/>).
    /// </summary>
    public bool OpensFight { get; private init; }

    /// <summary>The field <paramref name="ms"/> into the run.</summary>
    public State At(double ms)
    {
        string? text = null;
        Character? attackingMember = null, flashMember = null;
        Monster? attackingMonster = null, flashMonster = null;
        var graves = new HashSet<Character>(ReferenceEqualityComparer.Instance);
        var removed = new HashSet<Character>(ReferenceEqualityComparer.Instance);
        var gone = new HashSet<Monster>(ReferenceEqualityComparer.Instance);
        IReadOnlyList<string>? box = null;
        foreach (var line in Lines)
        {
            if (ms < line.StartMs)
                break;
            if (line.Box is { } rows)
            {
                if (ms < line.EndMs)
                    box = rows;
                continue;
            }
            if (ms < line.AttackStartMs)
                (flashMember, flashMonster) = (line.Member, line.Monster);
            else if (ms < line.AttackStartMs + line.AttackMs)
                (attackingMember, attackingMonster) = (line.Member, line.Monster);
            if (line.Slain is { } slain && ms >= line.AttackStartMs + line.AttackMs)
                gone.Add(slain);
            if (ms >= line.PrintMs)
                text = line.Text;
            if (line.Dying is { } dying && ms >= line.PrintMs)
            {
                if (ms >= line.RelaidMs)
                    removed.Add(dying);
                else
                    graves.Add(dying);
            }
        }
        var relaid = RoundEndSetupMs is { } endSetup && ms >= endSetup;
        int? setupDrawn = ms < SetupMs && FrameMs > 0 ? (int)(Math.Max(0, ms - LeadBlackMs) / FrameMs) : null;
        if (relaid && ms - RoundEndSetupMs!.Value < RoundEndSetupSteps * FrameMs)
            setupDrawn = (int)((ms - RoundEndSetupMs.Value) / FrameMs);
        return new State(relaid ? null : text, attackingMember, attackingMonster, flashMember, flashMonster, graves, removed, gone)
        {
            SetupDrawn = setupDrawn,
            Relaid = relaid,
            Black = ms < LeadBlackMs,
            BarCleared = OpensFight,
            FrameHidden = OpensFight && ms < SetupMs - FrameMs,
            Box = box,
            Sparkle = Sparkles.FirstOrDefault(s => ms >= s.StartMs && ms < s.EndMs),
        };
    }

    private static int IndexIn(IReadOnlyList<string> list, string key)
    {
        for (var i = 0; i < list.Count; i++)
            if (list[i] == key)
                return i;
        return -1;
    }

    /// <summary>The <see cref="CombatFieldMessage.Keys"/> entry whose template is <paramref name="text"/>, or null.</summary>
    public static string? MessageKey(CombatFieldMessage? message, string text, bool outsideRound = false) =>
        message?.Keys.Concat(outsideRound ? message.OutsideRoundKeys : []).FirstOrDefault(k => Template(GameText.S(k)).IsMatch(text.Trim()));

    private static Regex Template(string template) =>
        new("^" + Regex.Replace(Regex.Escape(template.Trim()), @"\\\{(\d+)\}", m => m.Groups[1].Value == "0" ? "(?<n>.+?)" : ".+?") + "$");

    /// <summary>
    /// The pack's entry whose template (a string id with {0}, {1} …) is <paramref name="text"/>, and the name it put in {0}
    /// (null for a template without one); null when none matches.
    /// </summary>
    public static CombatFieldLineSound? Match(CombatFieldLayout layout, string text, out string? name)
    {
        ArgumentNullException.ThrowIfNull(layout);
        foreach (var entry in layout.LineSounds)
        {
            var template = GameText.S(entry.Key);
            var pattern = "^" + Regex.Replace(Regex.Escape(template), @"\\\{(\d+)\}", m => m.Groups[1].Value == "0" ? "(?<n>.+?)" : ".+?") + "$";
            var match = Regex.Match(text, pattern);
            if (!match.Success)
                continue;
            name = match.Groups["n"].Success ? match.Groups["n"].Value : null;
            return entry;
        }
        name = null;
        return null;
    }
}

/// <summary>
/// Who stands on I Amiga's combat field when a round begins: the party row (members not DEAD, in record order: h53
/// <c>+0x30</c>) and each rank's living monsters, front rank first (the round's set-up lays them out afresh, <c>+0x140</c>).
/// </summary>
public sealed record P1CombatFieldSnapshot(IReadOnlyList<Character> Row, IReadOnlyList<IReadOnlyList<Monster>> Groups)
{
    public static P1CombatFieldSnapshot Of(Party party, CombatEncounter combat)
    {
        ArgumentNullException.ThrowIfNull(party);
        ArgumentNullException.ThrowIfNull(combat);
        return new P1CombatFieldSnapshot(party.Members.Where(m => m.Status != CharacterStatus.Dead).ToList(),
            combat.Ranks.Select(r => (IReadOnlyList<Monster>)r.Living.ToList()).Where(g => g.Count > 0).ToList());
    }
}
