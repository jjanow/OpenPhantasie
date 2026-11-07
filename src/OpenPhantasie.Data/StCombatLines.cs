using System.Text.Json.Serialization;

namespace OpenPhantasie.Data;

/// <summary>Whose a combat line is (the name in it): a party member, a monster, or nobody (a line without a name).</summary>
public enum CombatLineSide
{
    Any,
    Member,
    Monster,
}

/// <summary>
/// What one kind of II ST combat line does (s136, <c>reverse-engineering/atari-st/phantasie2/FORMAT.md</c> 7.3):
/// <see cref="Sound"/> (−1 = none) through <c>0xb80e</c>; <see cref="Hold"/> null = the Speed hold unless the text is short,
/// false = none (the blank line of a monster's special); <see cref="Pose"/>: the actor's silhouette, then its attack pose
/// first (pose mode 2, <c>0x18f7a</c>: the member's cast <c>0x79fc</c>, the monster's special <c>0x8d8c</c>);
/// <see cref="Pre"/>: sounds played first without a line (a breath's blank line, sound 9); <see cref="Death"/>: a member's
/// "DIES" (n = 15, then the mark sequence <c>0x18a02</c>).
/// </summary>
public sealed record StCombatLineSound(int Sound, bool? Hold = null, bool Pose = false, IReadOnlyList<int>? Pre = null,
    bool Death = false)
{
    /// <summary>s156: the actor's attack pose first (pose mode 1, <c>0x186a8</c>: a swing, a rock, a monster's attack).</summary>
    public bool Attack { get; init; }

    /// <summary>s156: a monster's picture erased before the line (pose mode 0, <c>0x9b06</c>: its "DIES").</summary>
    public bool Erase { get; init; }
}

/// <summary>
/// II ST's spell sparkle (s156, FORMAT.md 7.3, <c>0x18b16(group, slot, amount)</c>): before a damage spell's (or a thrown
/// rock's) damage line on a monster, amount / <see cref="Per"/> + 1 times: <see cref="Sound"/> (through the sound wait),
/// the sprite (<see cref="Spell"/> for amount ≥ 0, <see cref="Rock"/> for a rock's negative amount) at a random
/// <see cref="Size"/>-pixel spot inside the monster's picture, <see cref="HoldMs"/> (no key), then the sound silenced
/// (<c>0xb9e2</c>). <see cref="RockMissX"/> / <see cref="RockMissY"/>: <c>0x18c6c</c>, after a rock's "MISSES" the rock
/// sprite at that screen spot for <see cref="HoldMs"/>, no sound.
/// </summary>
public sealed record StCombatSparkle(int Sound, string Spell, string Rock, int Per, int Size, double HoldMs,
    int RockMissX, int RockMissY)
{
    /// <summary>How many sparkles an amount gives (the loop bound <c>|amount| / per + 1</c>).</summary>
    public int Count(int amount) => Math.Abs(amount) / Math.Max(1, Per) + 1;

    /// <summary>
    /// <c>0x18a8a(n)</c> = <c>rand() / (32767 / n)</c> for a roll 0..32767: 0..n; 0 when n ≤ 0 (a picture no bigger than
    /// the sprite).
    /// </summary>
    public static int Offset(int roll, int n) => n <= 0 ? 0 : roll / (32767 / n);
}

/// <summary>A member's death on the ST's combat row (<c>0x18a02</c>): the mark, <see cref="Sound"/>, <see cref="HoldMs"/>.</summary>
public sealed record StCombatDeath(int Sound, double HoldMs);

/// <summary>
/// Phantasie II ST's combat round line by line (s136, FORMAT.md 7.3), from the pack's <c>combat_lines</c>: every line goes
/// through <c>0x18cc2(text, n)</c>: sound n (at Speed ≥ <see cref="SoundWaitSpeed"/> after the running sound has ended,
/// unless the last sound was <see cref="NoWaitAfter"/>), the text, then Speed ms (<see cref="SpeedMs"/>) unless the text
/// has <see cref="ShortText"/> characters or fewer. A cast first shows the caster's pose (<see cref="PoseMs"/> when Speed
/// &gt; <see cref="PoseSpeed"/>, else <see cref="PoseFastMs"/>), then its attack pose (<see cref="AttackPoseMs"/>).
/// <see cref="FightStartSound"/> (17, <c>0x1838e</c>) starts the combat screen, <see cref="RoundEndSound"/> (18,
/// <c>0x18416</c>) ends every round (−1 = none). <see cref="Lines"/>: string id → per side what it does.
/// <see cref="EndBoxes"/> (s138, FORMAT.md 7.3a): the end-of-fight message boxes, or null.
/// </summary>
public sealed record StCombatLines(
    IReadOnlyDictionary<string, IReadOnlyDictionary<CombatLineSide, StCombatLineSound>> Lines,
    int FastMs, int NormalMs, int SlowMs, int SoundWaitSpeed, int NoWaitAfter, int ShortText,
    double PoseMs, double PoseFastMs, int PoseSpeed, StCombatDeath Death, int FightStartSound = -1, int RoundEndSound = -1, StEndBoxes? EndBoxes = null)
{
    /// <summary>s156: the attack pose (<c>0x186a8</c>: the attacking picture, a fixed <c>evnt_timer(300)</c>, the idle one).</summary>
    public double AttackPoseMs { get; init; }

    /// <summary>s156: the silhouette's two registers of the combat palette (the shape, the rest of the rectangle), or null.</summary>
    public IReadOnlyList<int>? SilhouettePens { get; init; }

    /// <summary>s156: the spell sparkle and the rock's miss (<c>0x18b16</c> / <c>0x18c6c</c>), or null.</summary>
    public StCombatSparkle? Sparkle { get; init; }

    /// <summary>The Speed word <c>[0x1fefa]</c> for a Speed choice: 1 fast, 2 normal, 3 slow (the menu's ms).</summary>
    public int SpeedMs(int speed) => speed switch
    {
        1 => FastMs,
        3 => SlowMs,
        _ => NormalMs,
    };

    internal static StCombatLines FromDto(Dto dto, string manifestPath,
        IReadOnlyDictionary<string, List<string>>? palettes = null)
    {
        InvalidDataException Bad(string what) => new($"{manifestPath}: combat_lines: {what}.");
        if (dto.SpeedMs is not { } speed || !speed.TryGetValue("fast", out var fast) || !speed.TryGetValue("normal", out var normal)
            || !speed.TryGetValue("slow", out var slow))
            throw Bad("speed_ms needs fast / normal / slow");
        if (dto.Death is not { } death)
            throw Bad("no death");
        var lines = new Dictionary<string, IReadOnlyDictionary<CombatLineSide, StCombatLineSound>>(StringComparer.Ordinal);
        foreach (var (key, sides) in dto.Lines ?? [])
        {
            var map = new Dictionary<CombatLineSide, StCombatLineSound>();
            foreach (var (name, s) in sides)
            {
                var side = name switch
                {
                    "member" => CombatLineSide.Member,
                    "monster" => CombatLineSide.Monster,
                    "any" => CombatLineSide.Any,
                    _ => throw Bad($"{key}: side '{name}'"),
                };
                map[side] = new StCombatLineSound(s.Sound, s.Hold, s.Pose, s.Pre, s.Death) { Attack = s.Attack, Erase = s.Erase };
            }
            lines[key] = map;
        }
        return new StCombatLines(lines, fast, normal, slow, dto.SoundWaitSpeed, dto.NoWaitAfter, dto.ShortText,
            dto.PoseMs, dto.PoseFastMs, dto.PoseSpeed, new StCombatDeath(death.Sound, death.HoldMs), dto.FightStartSound, dto.RoundEndSound,
            dto.EndBoxes is { } boxes ? StEndBoxes.FromDto(boxes, palettes, Bad) : null)
        {
            AttackPoseMs = dto.AttackPoseMs,
            SilhouettePens = dto.SilhouettePens is { Count: 2 } pens ? pens : null,
            Sparkle = dto.Sparkle is not { } sp ? null
                : sp.RockMiss is not { Count: 2 } miss || string.IsNullOrEmpty(sp.Spell) || string.IsNullOrEmpty(sp.Rock)
                    ? throw Bad("sparkle needs spell, rock and rock_miss [x, y]")
                    : new StCombatSparkle(sp.Sound, sp.Spell, sp.Rock, sp.Per, sp.Size, sp.HoldMs, miss[0], miss[1]),
        };
    }

    internal sealed class Dto
    {
        [JsonPropertyName("speed_ms")] public Dictionary<string, int>? SpeedMs { get; set; }
        [JsonPropertyName("sound_wait_speed")] public int SoundWaitSpeed { get; set; }
        [JsonPropertyName("no_wait_after")] public int NoWaitAfter { get; set; } = -1;
        [JsonPropertyName("short_text")] public int ShortText { get; set; }
        [JsonPropertyName("pose_ms")] public double PoseMs { get; set; }
        [JsonPropertyName("pose_fast_ms")] public double PoseFastMs { get; set; }
        [JsonPropertyName("pose_speed")] public int PoseSpeed { get; set; }
        [JsonPropertyName("fight_start_sound")] public int FightStartSound { get; set; } = -1;
        [JsonPropertyName("round_end_sound")] public int RoundEndSound { get; set; } = -1;
        [JsonPropertyName("attack_pose_ms")] public double AttackPoseMs { get; set; }
        [JsonPropertyName("silhouette_pens")] public List<int>? SilhouettePens { get; set; }
        public SparkleDto? Sparkle { get; set; }
        public DeathDto? Death { get; set; }
        public Dictionary<string, Dictionary<string, SideDto>>? Lines { get; set; }
        [JsonPropertyName("end_boxes")] public StEndBoxes.Dto? EndBoxes { get; set; }
    }

    internal sealed class DeathDto
    {
        public int Sound { get; set; }
        [JsonPropertyName("hold_ms")] public double HoldMs { get; set; }
    }

    internal sealed class SparkleDto
    {
        public int Sound { get; set; } = -1;
        public string? Spell { get; set; }
        public string? Rock { get; set; }
        public int Per { get; set; } = 32;
        public int Size { get; set; } = 16;
        [JsonPropertyName("hold_ms")] public double HoldMs { get; set; }
        [JsonPropertyName("rock_miss")] public List<int>? RockMiss { get; set; }
    }

    internal sealed class SideDto
    {
        public bool Attack { get; set; }
        public bool Erase { get; set; }
        public int Sound { get; set; } = -1;
        public bool? Hold { get; set; }
        public bool Pose { get; set; }
        public List<int>? Pre { get; set; }
        public bool Death { get; set; }
    }
}
