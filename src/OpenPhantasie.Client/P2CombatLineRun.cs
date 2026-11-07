using System.Text.RegularExpressions;
using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client;

/// <summary>
/// One Phantasie II combat round played line by line as the Atari ST does it (s136,
/// <c>reverse-engineering/atari-st/phantasie2/FORMAT.md</c> 7.3), on the remaster's combat screen. No MonoGame types: tests
/// drive it with milliseconds (<see cref="Advance"/>) and keys (<see cref="Key"/>). Per line (<c>0x18cc2(text, n)</c>):
/// <list type="number">
/// <item>The actor's pose first (s156, <c>0x186de</c> / <c>0x188fc</c>; no key): a swing, a rock or a monster's attack shows
/// the attacking picture <see cref="StCombatLines.AttackPoseMs"/> (mode 1); a member's cast or a monster's special first the
/// silhouette (<see cref="StCombatLines.PoseMs"/> / <see cref="StCombatLines.PoseFastMs"/>), then the attack pose (mode 2);
/// a monster's "DIES" erases its picture (mode 0, <see cref="Slain"/>).</item>
/// <item>Before a monster's damage line from a damage spell or a rock (<see cref="CombatHit.SpellDamage"/>), the sparkles
/// (s156, <see cref="StCombatSparkle"/>, <c>0x18b16</c>): each its sound (through the sound wait), the sprite for its hold
/// (no key), the sound silenced (<see cref="Silence"/>).</item>
/// <item>Its sounds (<see cref="StCombatLineSound.Pre"/>, then <see cref="StCombatLineSound.Sound"/>): at Speed ≥
/// <see cref="StCombatLines.SoundWaitSpeed"/> each first waits until the running sound has ended (no key ends that wait;
/// none after <see cref="StCombatLines.NoWaitAfter"/>), then starts (cutting off what still runs). No sound: no wait.</item>
/// <item>The text (<see cref="Printed"/> grows).</item>
/// <item>The hold: Speed ms unless the text is short (<see cref="StCombatLines.ShortText"/>) or the line says no hold; a
/// key ends it. A rock's "MISSES" then shows the rock at its screen spot (<c>0x18c6c</c>).</item>
/// <item>A member's "DIES" (<see cref="StCombatLineSound.Death"/>): no sound, no hold; then the mark (<see cref="Marked"/>),
/// the death sound (waiting for the running one first), its hold (a key ends it), the wait for the death sound, and the
/// row re-laid without the member (<see cref="Removed"/>).</item>
/// </list>
/// A round's run ends with <see cref="StCombatLines.RoundEndSound"/> (18, <c>0x18416</c>) after its last line and before the
/// end-of-fight boxes. An end-of-fight box (s138, <see cref="StEndBoxes"/>, FORMAT.md 7.3a): a one-line box's line (its sound
/// first, if any), or a pair's two lines in a row (either order) printed together, then the box held for Speed + the box's
/// hold (a key ends it) while <see cref="ActiveBox"/> shows it.
/// Lines the table lacks play no sound and take the Speed hold `[?]`. Sound lengths come from
/// <c>soundSeconds</c> (0 with the Sound switch off: no sound waits, as I's run).
/// </summary>
public sealed class P2CombatLineRun
{
    /// <summary>The sound list's marker for <c>0xb9e2</c>: the chip silenced (the front end stops the running effect).</summary>
    public const int Silence = int.MinValue;

    /// <summary>One line of the round, classified: its table entry (null = not in the table), whose, the member named.</summary>
    /// <param name="BoxKey">The string id of an end-of-fight box line (<see cref="StEndBoxes"/>), else null.</param>
    public sealed record Entry(string Text, StCombatLineSound? Sound, CombatLineSide Side, Character? Member,
        string? BoxKey = null)
    {
        /// <summary>The line's index in the lines given to <see cref="Classify"/> (blank lines are dropped), or −1.</summary>
        public int LogLine { get; init; } = -1;

        /// <summary>The table's string id the line matched, or null.</summary>
        public string? Key { get; init; }

        /// <summary>A monster's own line (its swing, special, damage, "DIES"): the monster, when known.</summary>
        public Monster? Monster { get; init; }
    }

    /// <summary>An end-of-fight box on screen: its rows, upper first (one row: tree 40, two: tree 41).</summary>
    public sealed record EndBox(IReadOnlyList<string> Rows)
    {
        /// <summary>The rows joined by a line break.</summary>
        public string Text => string.Join("\n", Rows);
    }

    /// <summary>A pose on screen (s156): the member's portrait or the monster's picture as its attacking frame or silhouette.</summary>
    public sealed record PoseShown(Character? Member, Monster? Monster, bool Silhouette);

    /// <summary>
    /// A sprite on screen (s156): on <see cref="Target"/>'s picture at a random spot (<see cref="RollX"/> / <see cref="RollY"/>
    /// are <c>rand()</c> rolls 0..32767, <see cref="StCombatSparkle.Offset"/>), or with no target the rock's miss at the
    /// sparkle's screen spot. <see cref="Rock"/>: the rock sprite, else the spell's.
    /// </summary>
    public sealed record SparkleShown(Monster? Target, bool Rock, int RollX, int RollY);

    private enum StepKind
    {
        Pose,
        Sound,
        Print,
        Hold,
        Mark,
        WaitSound,
        Repack,
        Sparkle,
        Silence,
        Erase,
    }

    private readonly record struct Step(StepKind Kind, double Ms = 0, int Sound = -1, int Line = -1, Character? Member = null,
        EndBox? Box = null, Monster? Monster = null, bool Flag = false, int RollX = 0, int RollY = 0);

    private readonly List<Step> _steps = [];
    private readonly StCombatLines _table;
    private readonly int _speedMs;
    private readonly Func<int, double> _soundSeconds;
    private readonly List<int> _soundPlays = [];
    private readonly List<int> _out = [];
    private readonly HashSet<Character> _marked = new(ReferenceEqualityComparer.Instance);
    private readonly HashSet<Character> _removed = new(ReferenceEqualityComparer.Instance);
    private readonly HashSet<Monster> _slain = new(ReferenceEqualityComparer.Instance);
    private int _step;
    private double _clock;
    private double _stepUntil = double.NaN;
    private double _soundEnd;
    private int _lastSound = -1;
    private int _outTaken;

    /// <summary>The round's lines, classified, in order.</summary>
    public IReadOnlyList<Entry> Entries { get; }

    /// <summary>
    /// The end-of-fight box showing now (FORMAT.md 7.3a: tree 40 <c>0xa124</c> or tree 41 <c>0xa1c2</c> at pixel (12, 75),
    /// each text centred in 35 columns), or null. It stays until its hold (Speed + 1000 / 1500 ms) is over or a key ends it.
    /// </summary>
    public EndBox? ActiveBox => !Done && _steps[_step].Box is { } box ? box : null;

    /// <summary>The pose showing now (s156), or null.</summary>
    public PoseShown? Pose => !Done && _steps[_step] is { Kind: StepKind.Pose } s ? new PoseShown(s.Member, s.Monster, s.Flag) : null;

    /// <summary>The sparkle or the rock's miss showing now (s156), or null.</summary>
    public SparkleShown? Sparkle => !Done && _steps[_step] is { Kind: StepKind.Sparkle } s
        ? new SparkleShown(s.Monster, s.Flag, s.RollX, s.RollY)
        : null;

    /// <summary>How many of <see cref="Entries"/> have printed.</summary>
    public int Printed { get; private set; }

    /// <summary>Members whose death mark is on their portrait now (from the mark until the row is re-laid).</summary>
    public IReadOnlySet<Character> Marked => _marked;

    /// <summary>Members the row has been re-laid without (their "DIES" sequence is over).</summary>
    public IReadOnlySet<Character> Removed => _removed;

    /// <summary>Monsters whose picture has been erased (s156: pose mode 0 before their "DIES").</summary>
    public IReadOnlySet<Monster> Slain => _slain;

    /// <summary>Members who die in this round (a "DIES" line names them): they keep their slot until <see cref="Removed"/>.</summary>
    public IReadOnlySet<Character> Dying { get; }

    /// <summary>Every sound started so far, in order (also the ones cut off).</summary>
    public IReadOnlyList<int> SoundPlays => _soundPlays;

    /// <summary>Milliseconds since the run began.</summary>
    public double ClockMs => _clock;

    /// <summary>True once every step has run.</summary>
    public bool Done => _step >= _steps.Count;

    /// <param name="table">The pack's <c>combat_lines</c>.</param>
    /// <param name="entries">The lines (<see cref="Classify"/>).</param>
    /// <param name="speed">Speed choice: 1 fast, 2 normal, 3 slow (<see cref="StCombatLines.SpeedMs"/>).</param>
    /// <param name="soundSeconds">How long sound n runs, seconds (0 = Sound off or unknown).</param>
    /// <param name="roundEnds">A round's run: <see cref="StCombatLines.RoundEndSound"/> after its last line (before the
    /// end-of-fight boxes); false for the lines outside a round (the party options' answers).</param>
    /// <param name="hits">The round's hits, their <see cref="CombatHit.Line"/> in the lines given to <see cref="Classify"/>:
    /// the sparkles (s156).</param>
    /// <param name="roll">A <c>rand()</c> roll 0..32767 for a sparkle's spot (default: random).</param>
    public P2CombatLineRun(StCombatLines table, IReadOnlyList<Entry> entries, int speed, Func<int, double> soundSeconds,
        bool roundEnds = false, IReadOnlyList<CombatHit>? hits = null, Func<int>? roll = null)
    {
        _table = table ?? throw new ArgumentNullException(nameof(table));
        Entries = entries ?? throw new ArgumentNullException(nameof(entries));
        _soundSeconds = soundSeconds ?? throw new ArgumentNullException(nameof(soundSeconds));
        _speedMs = table.SpeedMs(speed);
        roll ??= () => Random.Shared.Next(32768);
        var dying = new HashSet<Character>(ReferenceEqualityComparer.Instance);
        var boxes = table.EndBoxes;
        var sparkle = table.Sparkle;
        var pendingHits = (hits ?? []).Where(h => h is { SpellDamage: >= 0, Monster: not null }
                                                  && h.Source is DamageSource.PartySpell or DamageSource.PartyRock).ToList();
        var roundEndPlaced = !roundEnds || table.RoundEndSound < 0;
        void RoundEnd()
        {
            if (roundEndPlaced)
                return;
            roundEndPlaced = true;
            _steps.Add(new Step(StepKind.Sound, Sound: table.RoundEndSound)); // 0x18416: every exit of combat_round 0x7610
        }
        for (var i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            if (sparkle is not null && e.LogLine >= 0)
                foreach (var hit in pendingHits.Where(h => h.Line <= e.LogLine).ToList())
                {
                    pendingHits.Remove(hit);
                    var rock = hit.Source == DamageSource.PartyRock && hit.SpellDamage > 0; // amount −dmg < 0: 0x23c92
                    for (var k = sparkle.Count(hit.SpellDamage); k > 0; k--)
                    {
                        _steps.Add(new Step(StepKind.Sound, Sound: sparkle.Sound)); // 0xb9ce: the wait, then 0x21c48
                        _steps.Add(new Step(StepKind.Sparkle, sparkle.HoldMs, Monster: hit.Monster, Flag: rock,
                            RollX: roll(), RollY: roll()));
                        _steps.Add(new Step(StepKind.Silence)); // 0xb9e2
                    }
                }
            if (boxes is not null && e.BoxKey is { } key)
            {
                RoundEnd(); // the round's 18 comes before combat_victory's boxes
                if (boxes.Pairs.FirstOrDefault(p => p.Contains(key)) is { } pair)
                {
                    // 0xa272(upper, lower): the pair's two lines, in either order, make one box; the rows in the pack's order.
                    if (i + 1 < entries.Count && entries[i + 1].BoxKey is { } other && other != key && pair.Contains(other))
                    {
                        var upper = pair[0] == key ? e.Text : entries[i + 1].Text;
                        var lower = pair[0] == key ? entries[i + 1].Text : e.Text;
                        _steps.Add(new Step(StepKind.Print, Line: i));
                        _steps.Add(new Step(StepKind.Print, Line: i + 1));
                        _steps.Add(new Step(StepKind.Hold, _speedMs + boxes.TwoLine.HoldMs,
                            Box: new EndBox([upper.Trim(), lower.Trim()])));
                        i++;
                        continue;
                    }
                    // s158: a pair's line alone (the manual's totals under p2.combat-shares = manual: "%d XP AND %d GP"
                    // without "EACH SURVIVOR GETS", which would not be true of a total). combat_victory always shows the
                    // two-line box (0xa3da): the line in its own row, the partner's row empty [~].
                    var row = pair.TakeWhile(k => k != key).Count();
                    _steps.Add(new Step(StepKind.Print, Line: i));
                    _steps.Add(new Step(StepKind.Hold, _speedMs + boxes.TwoLine.HoldMs,
                        Box: new EndBox([.. pair.Select((_, k) => k == row ? e.Text.Trim() : "")])));
                    continue;
                }
                else
                {
                    if (boxes.SoundFor(key) is >= 0 and var boxSound)
                        _steps.Add(new Step(StepKind.Sound, Sound: boxSound)); // 0xa05a: sound 2, then "MONSTERS RUN"
                    _steps.Add(new Step(StepKind.Print, Line: i));
                    _steps.Add(new Step(StepKind.Hold, _speedMs + boxes.OneLine.HoldMs, Box: new EndBox([e.Text.Trim()])));
                    continue;
                }
            }
            var s = e.Sound;
            // A rock's own "MISSES" (0x78e4): no pose of its own (0x7748 posed before the throw), the rock's miss after it.
            var rockMiss = e.Key == "core.combat.x_misses" && e.Member is { } thrower && i > 0
                           && entries[i - 1] is { Key: "core.combat.x_throws_a_rock" } prev && ReferenceEquals(prev.Member, thrower);
            if (s is { Pose: true })
            {
                // 0x18f7a: the silhouette (300 ms when Speed > 9, else 10), then 0x186a8's attack pose.
                _steps.Add(new Step(StepKind.Pose, _speedMs > table.PoseSpeed ? table.PoseMs : table.PoseFastMs,
                    Member: e.Member, Monster: e.Monster, Flag: true));
                if (table.AttackPoseMs > 0)
                    _steps.Add(new Step(StepKind.Pose, table.AttackPoseMs, Member: e.Member, Monster: e.Monster));
            }
            else if (s is { Attack: true } && !rockMiss && table.AttackPoseMs > 0)
                _steps.Add(new Step(StepKind.Pose, table.AttackPoseMs, Member: e.Member, Monster: e.Monster));
            if (s is { Erase: true } && e.Monster is { } slain)
                _steps.Add(new Step(StepKind.Erase, Monster: slain)); // 0x9b06: the picture cleared before "DIES"
            foreach (var pre in s?.Pre ?? [])
                _steps.Add(new Step(StepKind.Sound, Sound: pre));
            if (s is { Death: false, Sound: >= 0 })
                _steps.Add(new Step(StepKind.Sound, Sound: s.Sound));
            _steps.Add(new Step(StepKind.Print, Line: i));
            var hold = s?.Death != true && (s?.Hold ?? e.Text.Trim().Length > table.ShortText);
            if (hold)
                _steps.Add(new Step(StepKind.Hold, _speedMs));
            if (rockMiss && sparkle is not null)
                _steps.Add(new Step(StepKind.Sparkle, sparkle.HoldMs, Flag: true)); // 0x18c6c: the rock at its spot
            if (s is { Death: true } && e.Member is { } member)
            {
                dying.Add(member);
                _steps.Add(new Step(StepKind.Mark, Member: member));
                _steps.Add(new Step(StepKind.Sound, Sound: table.Death.Sound));
                _steps.Add(new Step(StepKind.Hold, table.Death.HoldMs));
                _steps.Add(new Step(StepKind.WaitSound));
                _steps.Add(new Step(StepKind.Repack, Member: member));
            }
        }
        RoundEnd();
        Dying = dying;
        Advance(0);
    }

    /// <summary>
    /// The sounds started since the last call (to play them), in order; <see cref="Silence"/> where the chip is silenced
    /// (stop the running effect).
    /// </summary>
    public IReadOnlyList<int> TakeSounds()
    {
        var due = _out.Skip(_outTaken).ToList();
        _outTaken = _out.Count;
        return due;
    }

    /// <summary>A key or a click: ends the hold running now (AES <c>evnt_multi</c>); the sound waits and the poses go on.</summary>
    public void Key()
    {
        if (!Done && _steps[_step].Kind == StepKind.Hold)
        {
            _stepUntil = double.NaN;
            _step++;
            Advance(0);
        }
    }

    /// <summary>Run everything left at once (fast forward): the lines print, the marks clear, the row is re-laid.</summary>
    public void Finish()
    {
        while (!Done)
            Advance(1e9);
    }

    /// <summary>Let <paramref name="ms"/> milliseconds pass.</summary>
    public void Advance(double ms)
    {
        var target = _clock + Math.Max(0, ms);
        while (!Done)
        {
            var step = _steps[_step];
            if (double.IsNaN(_stepUntil))
                _stepUntil = step.Kind switch
                {
                    StepKind.Pose or StepKind.Hold or StepKind.Sparkle => _clock + step.Ms,
                    StepKind.Sound or StepKind.WaitSound => WaitsForSound ? Math.Max(_clock, _soundEnd) : _clock,
                    _ => _clock,
                };
            if (_stepUntil > target)
            {
                _clock = target;
                return;
            }
            _clock = Math.Max(_clock, _stepUntil);
            _stepUntil = double.NaN;
            switch (step.Kind)
            {
                case StepKind.Sound:
                    _soundPlays.Add(step.Sound);
                    _out.Add(step.Sound);
                    _soundEnd = _clock + Math.Max(0, _soundSeconds(step.Sound)) * 1000;
                    _lastSound = step.Sound;
                    break;
                case StepKind.Silence:
                    _out.Add(Silence);
                    _soundEnd = _clock; // the silence script ends at once: the next sound has nothing to wait for
                    break;
                case StepKind.Print:
                    Printed = step.Line + 1;
                    break;
                case StepKind.Mark:
                    _marked.Add(step.Member!);
                    break;
                case StepKind.Repack:
                    _marked.Remove(step.Member!);
                    _removed.Add(step.Member!);
                    break;
                case StepKind.Erase:
                    _slain.Add(step.Monster!);
                    break;
            }
            _step++;
        }
        _clock = Math.Max(_clock, target);
    }

    /// <summary><c>0xb8f2</c>: at Speed ≥ 9, unless the last sound was 18, wait for the running sound.</summary>
    private bool WaitsForSound => _speedMs >= _table.SoundWaitSpeed && _lastSound != _table.NoWaitAfter;

    /// <summary>
    /// Classify a round's lines: each line's string id by its text (the pack's ids, matched against the string table), whose
    /// it is (the tag Core noted for the line, else the name in it: a member of <paramref name="party"/> or else a monster),
    /// and for a member's line the member, for a monster's line the monster (the tag's, else the last of
    /// <paramref name="hits"/> on a monster of that name up to the line: its damage and "DIES"). Blank lines are dropped (the
    /// remaster's log shows none); <see cref="Entry.LogLine"/> keeps each line's index. End-of-fight box lines
    /// (<see cref="StCombatLines.EndBoxes"/>) get their <see cref="Entry.BoxKey"/>; <paramref name="outsideRound"/>: the
    /// lines come from outside a round (the morale check, a party option's answer), where the outside-round keys are boxes
    /// too ("MONSTERS RUN": in a round the spell's line).
    /// </summary>
    public static IReadOnlyList<Entry> Classify(StCombatLines table, IReadOnlyList<string> lines,
        IReadOnlyList<CombatLineTag> tags, IReadOnlyList<Character> party, bool outsideRound = false,
        IReadOnlyList<CombatHit>? hits = null)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(lines);
        ArgumentNullException.ThrowIfNull(tags);
        ArgumentNullException.ThrowIfNull(party);
        var patterns = table.Lines.Keys.Select(k => (Key: k, Regex: Template(GameText.S(k)))).ToList();
        var boxPatterns = table.EndBoxes is { } boxes
            ? boxes.Keys.Concat(outsideRound ? boxes.OutsideRoundKeys : []).Concat(boxes.Pairs.SelectMany(p => p))
                .Distinct().Select(k => (Key: k, Regex: Template(GameText.S(k)))).ToList()
            : [];
        var result = new List<Entry>();
        for (var i = 0; i < lines.Count; i++)
        {
            var text = lines[i];
            if (string.IsNullOrWhiteSpace(text))
                continue;
            var trimmed = text.Trim();
            if (boxPatterns.FirstOrDefault(b => b.Regex.IsMatch(trimmed)) is { Key: { } boxKey })
            {
                result.Add(new Entry(text, null, CombatLineSide.Any, null, boxKey) { LogLine = i });
                continue;
            }
            StCombatLineSound? sound = null;
            var side = CombatLineSide.Any;
            Character? member = null;
            Monster? monster = null;
            string? matched = null;
            foreach (var (key, regex) in patterns)
            {
                var match = regex.Match(trimmed);
                if (!match.Success)
                    continue;
                matched = key;
                var sides = table.Lines[key];
                if (match.Groups["n"] is { Success: true } named)
                {
                    var tag = tags.FirstOrDefault(t => t.Line == i);
                    member = tag?.Member is { } tagged && tagged.Name == named.Value
                        ? tagged
                        : tag?.Monster is { } m && m.Name == named.Value
                            ? null
                            : party.FirstOrDefault(p => p.Name == named.Value);
                    side = member is not null ? CombatLineSide.Member : CombatLineSide.Monster;
                    if (member is null)
                        monster = tag?.Monster is { } own && own.Name == named.Value
                            ? own
                            : hits?.LastOrDefault(h => h.Line <= i && h.Monster?.Name == named.Value)?.Monster;
                }
                sound = sides.TryGetValue(side, out var s) ? s : sides.GetValueOrDefault(CombatLineSide.Any);
                break;
            }
            result.Add(new Entry(text, sound, side, member) { LogLine = i, Key = matched, Monster = monster });
        }
        if (table.EndBoxes is { } endBoxes)
            KeepPairsTogether(result, endBoxes.Pairs);
        return result;
    }

    /// <summary>
    /// s156 (FORMAT.md 7.3a): <c>combat_victory</c> shows its two-line boxes one right after the other (the XP box
    /// <c>0xa3da</c>, then the minion box <c>0xa4fa</c>; the pack lists the pairs in that order), where Core notes the
    /// treasure lines between them: a later pair found further down moves up to follow the one before it.
    /// </summary>
    private static void KeepPairsTogether(List<Entry> entries, IReadOnlyList<IReadOnlyList<string>> pairs)
    {
        int Find(IReadOnlyList<string> pair) => Enumerable.Range(0, Math.Max(0, entries.Count - 1)).FirstOrDefault(i =>
            entries[i].BoxKey is { } a && entries[i + 1].BoxKey is { } b && a != b && pair.Contains(a) && pair.Contains(b), -1);
        for (var p = 1; p < pairs.Count; p++)
        {
            var before = Find(pairs[p - 1]);
            var at = Find(pairs[p]);
            if (before < 0 || at <= before + 2)
                continue;
            var moved = entries.GetRange(at, 2);
            entries.RemoveRange(at, 2);
            entries.InsertRange(before + 2, moved);
        }
    }

    private static Regex Template(string template) =>
        new("^" + Regex.Replace(Regex.Escape(template.Trim()), @"\\\{(\d+)(,[^}]*)?}",
            m => m.Groups[1].Value == "0" ? "(?<n>.+?)" : ".+?") + "$");
}
