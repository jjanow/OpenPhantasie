using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client;

/// <summary>
/// s228: Phantasie III's round on the Apple II field (OUTDOORS3 / DUNGON3 1600–2005, 4000–4160,
/// <c>reverse-engineering/apple2/phantasie3/GRAPHICS.md</c> section 9; the layout's <see cref="Apple2FieldLayout.Plot"/>).
/// Core's III tags drive it, not the lines' wording (the Apple's own wording prints a death as "-DEATH-" after the name,
/// and a group's monsters share a name):
/// <list type="bullet">
/// <item>A member's swing tag: arm 22 (1695), or for an archer (a FIRE order) the loosed bow (shape 3, 1624); an archer's
/// "MISSES" shows the arrow at the miss point for its loops, then the line's pause (1628); its hit's damage line (the
/// next <see cref="DamageSource.PartyMelee"/> hit) the arrow on the monster for 500 loops (4132, SP = 2).</item>
/// <item>A member's cast tag (sound 21, <see cref="P3Fight.CastSound"/>): arm 5 and the sparkle during the tones
/// (1760–1764).</item>
/// <item>A monster's swing or cast tag, or its breath line: pose B to the round's end (2006).</item>
/// <item>A <see cref="DamageSource.PartySpell"/> hit on a monster: the spell hit (24) for 500 loops after its damage
/// line (4132, SP = 1).</item>
/// <item>A death line: the target of the hit before it (a monster or a member that died this round) is erased (4145;
/// a member at once too, 4025 GOSUB 9100); with no such hit, the first one of that name.</item>
/// <item>The message box is HOMEd at every tag but a death's (1610 / 1690 / 1860; the Apple prints ":IT=DIES:" in the
/// same box); a line holds the TIME LAG pause, a "HITS" none (its damage follows at once).</item>
/// </list>
/// </summary>
public sealed partial class Apple2FieldRun
{
    /// <summary>III's round (<see cref="Apple2FieldLayout.Plot"/> must be set); arguments as <see cref="Plan"/>.</summary>
    public static Apple2FieldRun PlanPlot(Apple2FieldLayout layout, IReadOnlyList<string> log, IReadOnlyList<CombatLineTag> tags,
        IReadOnlyList<CombatHit> hits, IReadOnlyList<string> after, Apple2FieldSnapshot snapshot, int timeLag, bool sound)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(log);
        ArgumentNullException.ThrowIfNull(tags);
        ArgumentNullException.ThrowIfNull(hits);
        ArgumentNullException.ThrowIfNull(after);
        ArgumentNullException.ThrowIfNull(snapshot);
        var plot = layout.Plot ?? throw new ArgumentException("not III's field (no monster_plot)", nameof(layout));
        var pause = layout.LinePauseMs(timeLag);
        var lines = new List<Line>();
        var run = new Apple2FieldRun(lines, layout.Columns, layout.TextRows);
        var slain = snapshot.Ranks.SelectMany(r => r).Where(m => !m.IsAlive).ToList();
        var fallen = snapshot.Party.Where(m => !snapshot.DeadBefore.Contains(m) && m.Status == CharacterStatus.Dead).ToList();
        object? lastTarget = null; // the monster or member of the last hit: whose death line comes next
        Character? shooter = null; // an archer whose shot hit, until its damage line
        var at = 0.0;
        void Add(Kind kind, double start, Character? member = null, Monster? monster = null, double end = double.PositiveInfinity) =>
            run._events.Add(new Event(start, end, kind, member, monster));
        var all = log.Select((text, i) => (text, i)).Concat(after.Select(text => (text, -1)));
        foreach (var (text, i) in all)
        {
            if (string.IsNullOrWhiteSpace(text))
                continue;
            var tag = i < 0 ? null : tags.LastOrDefault(t => t.Line == i);
            var home = tag is not null && tag.Sound != P3Sounds.Dies;
            var hold = pause;
            if (home)
                shooter = null;
            if (tag is { Swing: CombatSwing.Hit })
                hold = 0; // its damage line follows at once (1710 -> 4100, 1925 -> 4000)
            if (tag is { Member: { } member, Swing: not CombatSwing.None })
            {
                if (snapshot.Archers.Contains(member))
                {
                    Add(Kind.Fire, at, member); // 1624: XDRAW LD, XDRAW FR
                    if (tag.Swing == CombatSwing.Miss)
                    {
                        // 1628: the arrow at the miss point, 250 loops, XDRAW off, then 750's PA pause.
                        var flight = plot.ArrowMissLoops * layout.LoopMs;
                        Add(Kind.RockMiss, at, end: at + flight);
                        hold = flight + pause;
                    }
                    else
                        shooter = member;
                }
                else
                    Add(Kind.Swing, at, member);
            }
            else if (tag is { Member: { } caster, Sound: P3Fight.CastSound })
            {
                Add(Kind.Cast, at, caster); // 1760: arm 5
                hold = sound ? layout.SparkleMs : 0;
                if (hold > 0)
                    Add(Kind.Sparkle, at, caster, end: at + hold); // 1762-1764
            }
            else if (tag is { Monster: { } mon }
                     && (tag.Swing != CombatSwing.None || tag.Sound == P3Fight.CastSound
                         || text == GameText.F("core.p3.x_breathes", mon.Name)))
                Add(Kind.Pose, at, monster: mon); // 2006 (from 1895 / 1940)
            if (i >= 0)
                foreach (var hit in hits.Where(h => h.Line == i))
                {
                    lastTarget = (object?)hit.Monster ?? hit.Member;
                    if (hit is { Source: DamageSource.PartySpell, Monster: { } spellTarget })
                    {
                        Add(Kind.Hit, at, monster: spellTarget, end: at + layout.HitMs); // 4132, SP = 1: shape 24
                        hold += layout.HitMs;
                    }
                    else if (hit is { Source: DamageSource.PartyMelee, Monster: { } arrowTarget } && shooter is not null)
                    {
                        var ms = plot.ArrowHitLoops * layout.LoopMs; // 4132, SP = 2: shape 1, the arrow
                        Add(Kind.RockHit, at, monster: arrowTarget, end: at + ms);
                        hold += ms;
                        shooter = null;
                    }
                }
            bool Dies(string name) => text == GameText.F("core.combat.x_dies", name);
            var deadMonster = lastTarget is Monster lm && slain.Contains(lm) && Dies(lm.Name) ? lm
                : slain.FirstOrDefault(m => Dies(m.Name));
            var deadMember = deadMonster is not null ? null
                : lastTarget is Character lc && fallen.Contains(lc) && Dies(lc.Name) ? lc
                : fallen.FirstOrDefault(m => Dies(m.Name));
            if (deadMonster is not null)
            {
                slain.Remove(deadMonster);
                Add(Kind.Gone, at, monster: deadMonster); // 4145
                lastTarget = null;
            }
            else if (deadMember is not null)
            {
                fallen.Remove(deadMember);
                Add(Kind.MemberGone, at, deadMember); // 4025: GOSUB 9100 erases the figure
                lastTarget = null;
            }
            lines.Add(new Line(text, home, at, at + hold));
            at += hold;
        }
        return run;
    }
}
