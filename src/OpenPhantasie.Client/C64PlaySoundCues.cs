using System.Text.RegularExpressions;
using OpenPhantasie.Core;
using OpenPhantasie.Data;
using OpenPhantasie.Data.Audio;

namespace OpenPhantasie.Client;

/// <summary>
/// s228: which of the C64 packs' play sounds (<c>sid_sounds</c>, I's <c>reverse-engineering/c64/phantasie1/GRAPHICS.md</c>
/// 9.8, III's GRAPHICS.md) a moment of play makes, in the order the C64's programs make them. No MonoGame types: tests
/// feed it rounds and log lines.
/// <list type="bullet">
/// <item>I / II's fights (<c>OUTDOORS</c> / <c>DUNGEON</c>, II's <c>…2</c>): the encounter's start; a member's or
/// monster's MISSES (<c>miss</c>); a member HITS (<c>hit</c>); a monster HITS (<c>monster_hit</c>); a member CASTS
/// (<c>cast</c>); a monster CASTS or BREATHES (<c>monster_cast</c>); a member AWAKENS (<c>awakens</c>); a member
/// damaged by a monster who lives (<c>wound</c>, after its "TAKES n DAMAGE"; one who dies: DIES); a damage spell's
/// marks on a monster (<c>mark</c>, <see cref="SidSounds.MarksFor"/> times; II's tossed rock: once); IS ALIVE
/// (<c>alive</c>); a monster DIES (<c>encounter</c>, the same routine); a member DIES (<c>dies</c> 3 times).</item>
/// <item>I / II outside fights: the swim's damage (<c>hit</c>, and <c>dies</c> once for a swimmer who drowns), the
/// dungeon scripts' SMOKE FILLS THE AIR / A SPELL IS CAST / THE PARTY IS MOVED (<c>monster_cast</c>), a Resurrection
/// on the map (<c>alive</c>).</item>
/// <item>III's fights (<c>PHANT.OBJ</c>): a miss (<c>miss</c>), any damaging hit (<c>hit</c>; FIREFLASH, MINDBLAST and
/// FLAMEBOLT first ring their three pings, <c>ping</c>), a member's death (<c>death</c>).</item>
/// </list>
/// The C64 holds each line while its sound plays; the remaster shows a round's lines at once, so the sounds play in
/// turn on one chip, each from the last write of the one before (<see cref="InTurn"/>) `[~]`.
/// </summary>
public static class C64PlaySoundCues
{
    /// <summary>The sound name for <paramref name="sound"/> on the terrain (I / II) or III's fight.</summary>
    public static string Key(GameId game, bool dungeon, string sound) =>
        game == GameId.P3 ? "fight." + sound : "fight." + (dungeon ? "dungeon." : "outdoors.") + sound;

    /// <summary>Whether <paramref name="sid"/> holds the fights' sounds for <paramref name="game"/>.</summary>
    public static bool HasFightSounds(SidSounds? sid, GameId game) =>
        sid is not null && sid.Has(Key(game, false, game == GameId.P3 ? "miss" : "encounter"));

    private static readonly Dictionary<string, Regex> Templates = new(StringComparer.Ordinal);

    /// <summary>Whether <paramref name="line"/> is string <paramref name="key"/>; its first argument in <paramref name="name"/>.</summary>
    public static bool Is(string line, string key, out string name)
    {
        var template = GameText.S(key);
        if (!Templates.TryGetValue(template, out var regex))
        {
            regex = new Regex("^" + Regex.Replace(Regex.Escape(template.Trim()), @"\\\{(\d+)[^}]*\}",
                m => m.Groups[1].Value == "0" ? "(?<n>.+?)" : ".+?") + "$");
            Templates[template] = regex;
        }
        var match = regex.Match(line.Trim());
        name = match.Success && match.Groups["n"].Success ? match.Groups["n"].Value.Trim() : ""; // I pads names (DOS's columns)
        return match.Success;
    }

    /// <summary>
    /// The sounds of one round shown at once (<paramref name="round"/>'s lines, tags and hits), I / II or III;
    /// <paramref name="members"/>: the party as it stood before the round (who is a member by name);
    /// <paramref name="marks"/>: a monster id's marks (I / II).
    /// </summary>
    public static IReadOnlyList<string> ForRound(GameId game, CombatResult round, IReadOnlyList<Character> members,
        Func<int, int> marks)
    {
        ArgumentNullException.ThrowIfNull(round);
        return ForRound(game, round.Log, round.LineTags, round.Hits, members, marks);
    }

    /// <summary>The same from a round's lines, line tags and hits.</summary>
    public static IReadOnlyList<string> ForRound(GameId game, IReadOnlyList<string> log, IReadOnlyList<CombatLineTag> tags,
        IReadOnlyList<CombatHit> hits, IReadOnlyList<Character> members, Func<int, int> marks)
    {
        ArgumentNullException.ThrowIfNull(log);
        ArgumentNullException.ThrowIfNull(tags);
        ArgumentNullException.ThrowIfNull(hits);
        ArgumentNullException.ThrowIfNull(members);
        ArgumentNullException.ThrowIfNull(marks);
        var names = members.Select(m => m.Name).ToHashSet(StringComparer.Ordinal);
        var events = new List<(int Line, int Order, string Sound)>();
        var seq = 0;
        foreach (var tag in tags)
        {
            if (tag.Line < 0 || tag.Line >= log.Count)
                continue;
            var line = log[tag.Line];
            if (tag.Swing == CombatSwing.Miss)
                events.Add((tag.Line, seq++, "miss"));
            else if (tag.Swing == CombatSwing.Hit && game != GameId.P3) // III: the hit's noise comes with its damage
                events.Add((tag.Line, seq++, tag.Member is not null ? "hit" : "monster_hit"));
            else if (game != GameId.P3 && Is(line, "core.combat.x_awakens", out _) && tag.Member is not null)
                events.Add((tag.Line, seq++, "awakens"));
            else if (game != GameId.P3 && (Is(line, "core.combat.x_casts_a_spell", out _) || Is(line, "core.combat.x_breathes", out _)))
                events.Add((tag.Line, seq++, tag.Member is not null ? "cast" : "monster_cast"));
        }
        for (var i = 0; i < log.Count; i++)
        {
            var line = log[i];
            if (game == GameId.P3)
            {
                // PHANT.OBJ's --DEATH-- for a member (target slots 1-7); a monster's death has no sound.
                if (Is(line, "core.combat.x_dies", out var dead) && names.Contains(dead))
                    events.Add((i, seq++, "death"));
                continue;
            }
            if (Is(line, "core.combat.x_dies", out var who))
            {
                if (names.Contains(who))
                    events.AddRange(Enumerable.Repeat((i, seq++, "dies"), 3));
                else
                    events.Add((i, seq++, "encounter"));
            }
            else if (Is(line, "core.magic.x_is_resurrected", out _))
                events.Add((i, seq++, "alive"));
        }
        foreach (var hit in hits)
        {
            var at = hit.Line < 0 ? log.Count : hit.Line;
            if (game == GameId.P3)
            {
                // PHANT.OBJ: the three pings before each monster struck by FIREFLASH, MINDBLAST or FLAMEBOLT; the noise
                // for every damaging hit (Core notes each: party melee and spells, monster melee and specials).
                if (hit is { Source: DamageSource.PartySpell, Monster: not null } && P3PingSpell(hit))
                    events.Add((at, -2, "ping"));
                events.Add((at, -1, "hit"));
                continue;
            }
            if (hit.Member is { } member)
            {
                // OUTDOORS 0x2e11: "TAKES n DAMAGE", then DIES (its line) or the wound's sound.
                if (!DiesAt(log, member.Name, at))
                    events.Add((at, seq++, "wound"));
            }
            else if (hit.Monster is { } monster && (hit.Source == DamageSource.PartyRock
                         || hit is { Source: DamageSource.PartySpell, SpellDamage: >= 0 }))
            {
                // 0x2fd6 with SP set: a mark per C% (II's rock, SP = 2: one).
                var n = hit.Source == DamageSource.PartyRock ? 1 : marks(monster.Id);
                for (var k = 0; k < n; k++)
                    events.Add((at, seq++, "mark"));
            }
        }
        return [.. events.OrderBy(e => e.Line).ThenBy(e => e.Order).Select(e => e.Sound)];
    }

    /// <summary>The line after a hit's "takes" line (or that line) is "<paramref name="name"/> DIES".</summary>
    private static bool DiesAt(IReadOnlyList<string> log, string name, int at)
    {
        for (var i = at; i < Math.Min(log.Count, at + 2); i++)
            if (Is(log[i], "core.combat.x_dies", out var who) && who == name)
                return true;
        return false;
    }

    /// <summary>III's FIREFLASH 1–4, MINDBLAST 1–4, FLAMEBOLT 1–4 (spells 5–8, 33–40; <c>SpellIndex</c> 0-based).</summary>
    private static bool P3PingSpell(CombatHit hit) =>
        hit.Spell is int s && (s is >= SpellId.FireflashI and < SpellId.FireflashI + 4 or >= SpellId.MindblastI and < SpellId.FlameboltI + 4);

    /// <summary>
    /// The sounds the session's lines outside a fight make (I / II): <paramref name="lines"/> noted since the last call;
    /// <paramref name="mode"/> the session's mode (none in town: <c>TOWNR</c> plays none of these).
    /// </summary>
    public static IReadOnlyList<string> ForLog(GameId game, GameMode mode, IReadOnlyList<string> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);
        var sounds = new List<string>();
        if (game is not (GameId.P1 or GameId.P2) || mode is not (GameMode.Overworld or GameMode.Dungeon))
            return sounds;
        foreach (var line in lines)
        {
            if (Is(line, "core.dungeon.x_sinks_and_takes_x_damage", out _))
                sounds.Add("hit"); // OUTDOORS 0x6033: the TAKES DAMAGE line's sound
            else if (Is(line, "core.dungeon.swim_x_dies", out _))
                sounds.AddRange(["hit", "dies"]); // 0x60dd: DIES after its TAKES DAMAGE, the death's sound once
            else if (mode == GameMode.Dungeon && (Is(line, "core.script.smoke_fills_the_air", out _)
                         || Is(line, "core.script.a_spell_is_cast", out _) || Is(line, "core.script.the_party_is_moved", out _)))
                sounds.Add("monster_cast"); // DUNGEON 0x4d5d / 0x4d86 / 0x4ee6
            else if (Is(line, "core.magic.x_is_resurrected", out _))
                sounds.Add("alive"); // OUTDOORS 0x2ae1 / DUNGEON: IS ALIVE
        }
        return sounds;
    }

    /// <summary>
    /// <paramref name="sounds"/> as cues in turn from <paramref name="at"/>: each starts at the last write of the one
    /// before (<see cref="SidSounds.Length"/>), as the C64's next statement runs when a sound routine returns.
    /// </summary>
    public static IReadOnlyList<SidCue> InTurn(SidSounds sid, IEnumerable<string> sounds, double at = 0)
    {
        ArgumentNullException.ThrowIfNull(sid);
        ArgumentNullException.ThrowIfNull(sounds);
        var cues = new List<SidCue>();
        foreach (var sound in sounds)
        {
            if (!sid.Has(sound))
                continue;
            cues.Add(new SidCue(at, sound));
            at += sid.Length(sound);
        }
        return cues;
    }
}
