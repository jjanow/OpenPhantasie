namespace OpenPhantasie.Core;

/// <summary>When the training level factor F drops to 1.0 (0.2 × the level-1 target), per game and version.</summary>
public enum TrainingFactorReset
{
    /// <summary>DOS Phantasie I: when the character's XP is 0 (OV1 <c>0x1330</c>, <c>town-menus.md</c>).</summary>
    ExperienceZero,

    /// <summary>Phantasie II (ST <c>guild_train 0x0cdde</c>): when the character's level is 0 (D2-012).</summary>
    LevelZero,
}

/// <summary>Which spells a level-up marks "can learn" (flag 2), per game.</summary>
public enum SpellMarkRule
{
    /// <summary>DOS Phantasie I (OV1 <c>0x9ee</c>): the spells of the new level whose flag is still 0.</summary>
    NewLevelOnly,

    /// <summary>
    /// Phantasie II (<c>level_up 0x0c71a</c>, <c>training.md</c> §2): every spell of level 1 … the new level whose flag is
    /// still 0, so a flag cleared by the I → II transfer comes back at the next training [C] live.
    /// </summary>
    UpToNewLevel,
}

/// <summary>What a level-up's HP re-roll equal to the old maximum gives.</summary>
public enum HpRollTie
{
    /// <summary>DOS Phantasie I (<c>town-menus.md</c>: "roll &gt; old"): only a higher roll is taken, an equal one gives +1.</summary>
    PlusOne,

    /// <summary>Phantasie II and Amiga I (<c>training.md</c> §2): a roll ≥ the old maximum becomes the new maximum.</summary>
    NewMaximum,
}

/// <summary>How the training XP target is computed.</summary>
public enum TrainingArithmetic
{
    /// <summary>Doubles (Phantasie I's generic form; I's play uses <see cref="TownMenus.TrainingTarget"/>).</summary>
    Double,

    /// <summary>
    /// Phantasie II (ST <c>guild_train 0x0cf58…0x0d012</c>): Motorola FFP singles, step for step (<see cref="MotorolaFfp"/>),
    /// with F built by repeated multiplication by 1.8 and each class's divisor as the program stores it
    /// (<see cref="ClassTraining.DivisorFfp"/>).
    /// </summary>
    MotorolaFfp,
}

/// <summary>
/// One class's training stat sum and divisor: <c>s = (weights · stats) / Divisor</c>. Phantasie I FIGHTER:
/// (STR + DEX) / 16.5; II FIGHTER: (2·STR + DEX) / 33.0.
/// </summary>
public sealed record ClassTraining(
    CharacterClass Class, int Str, int Int, int Dex, int Con, int Cha, int Luck, double Divisor, uint? DivisorFfp = null)
{
    public int Sum(CharacterStats s) =>
        Str * s.Str + Int * s.Int + Dex * s.Dex + Con * s.Con + Cha * s.Cha + Luck * s.Luck;
}

/// <summary>
/// Guild training and level-up as rules-profile values (Phase 09 T1.3; <c>training</c> in rules.json). The XP target has
/// the same shape in every decoded version, <c>X = (F − 0.8) · 1000 / (sum / divisor + 1e−5)</c> with <c>F = 1.8^level</c>;
/// what differs per game is each class's sum and divisor and when F resets to 1.0. The price is
/// <c>min(15000, trunc(level^2.2) · (20 − CHA)² · price index / 100)</c>, where DOS clamps <c>20 − CHA</c> at 0 and the ST
/// does not (<see cref="ClampCharisma"/>). The level-up deltas are <see cref="SpellMarks"/> and <see cref="HpTie"/>.
/// <para>
/// Computed in doubles. Phantasie I play uses <see cref="TownMenus.TrainingTarget"/>, which reproduces DOS's float
/// rounding bit for bit (<see cref="TownMenus.Train"/> takes that path for I's values); this generic form agrees with it to
/// ~1e−6 relative (pinned by a test). Phantasie II computes in the ST's FFP singles (<see cref="Arithmetic"/>, <c>training.md</c> §1).
/// </para>
/// </summary>
public sealed record TrainingRules(TrainingFactorReset Reset, IReadOnlyList<ClassTraining> Classes)
{
    public const double LevelFactor = 1.8;

    /// <summary>
    /// True when the price's <c>20 − CHA</c> is clamped at 0 (DOS I, OV1 <c>0x13a1</c>); false when CHA above 20 makes it grow
    /// again (Amiga I, ST II <c>training.md</c> §1 item 5).
    /// </summary>
    public bool ClampCharisma { get; init; } = true;

    /// <summary>Which spells a level-up marks "can learn".</summary>
    public SpellMarkRule SpellMarks { get; init; } = SpellMarkRule.NewLevelOnly;

    /// <summary>What an HP re-roll equal to the old maximum gives.</summary>
    public HpRollTie HpTie { get; init; } = HpRollTie.PlusOne;

    /// <summary>Doubles, or the ST's FFP singles (<see cref="TrainingArithmetic.MotorolaFfp"/>, II).</summary>
    public TrainingArithmetic Arithmetic { get; init; } = TrainingArithmetic.Double;

    public ClassTraining For(CharacterClass cls) =>
        Classes.FirstOrDefault(c => c.Class == cls)
        ?? throw new ArgumentOutOfRangeException(nameof(cls), cls, "No training entry for this class.");

    /// <summary>The raw XP target X before the comparison (see the type summary).</summary>
    public double Target(CharacterClass cls, CharacterStats stats, int level, long experience)
    {
        ArgumentNullException.ThrowIfNull(stats);
        var c = For(cls);
        var s = c.Sum(stats) / c.Divisor;
        var f = Math.Pow(LevelFactor, level);
        if (Reset == TrainingFactorReset.ExperienceZero ? experience == 0 : level == 0)
            f = 1.0;
        return (f - 0.8) * 1000.0 / (s + 1e-5);
    }

    /// <summary>
    /// XP total that ends the shortfall (the truncated target). Under FFP: the least XP whose <see cref="Shortfall"/> is 0
    /// (searched from 2 below the double target's floor).
    /// </summary>
    public long XpRequired(CharacterClass cls, CharacterStats stats, int level, long experience)
    {
        var x = (long)Math.Truncate(Target(cls, stats, level, experience));
        if (Arithmetic != TrainingArithmetic.MotorolaFfp)
            return x;
        for (var xp = Math.Max(0, x - 8); ; xp++)
            if (Shortfall(cls, stats, level, xp) == 0)
                return xp;
    }

    /// <summary>
    /// XP the member still needs, 0 = may train: <c>n = trunc(XP − X)</c>, "You need −n more" while <c>n &lt; 0</c>
    /// (ST <c>guild_train 0x0cdde</c>, DOS OV1 <c>0x1387</c>). Under FFP every step is the ST's (<see cref="FfpNeed"/>).
    /// </summary>
    public long Shortfall(CharacterClass cls, CharacterStats stats, int level, long experience)
    {
        var n = Arithmetic == TrainingArithmetic.MotorolaFfp
            ? FfpNeed(cls, stats, level, experience)
            : (long)Math.Truncate(experience - Target(cls, stats, level, experience));
        return n < 0 ? -n : 0;
    }

    // ST constants (guild_train): 1.8 at 0xcf58, 1.0 at 0xcf8c, 1e−5 at 0xcf94, −0.8 and 1000 at 0xcfa8.
    private const uint Ffp1Point8 = 0xE6666641;
    private const uint FfpOne = 0x80000041;
    private const uint FfpTenMicro = 0xA7C5AC30;
    private const uint FfpMinus0Point8 = 0xCCCCCCC0;
    private const uint FfpThousand = 0xFA00004A;

    /// <summary>
    /// <c>n</c> as the ST computes it (<c>0x0cf58…0x0d012</c>): <c>s = itof(sum) / divisor</c>; F = 1.8, multiplied by 1.8
    /// once per level above 1 (1.0 at level 0); <c>X = (F + −0.8) · 1000 / (s + 1e−5)</c>; <c>n = ftoi(itof(XP) − X)</c>.
    /// </summary>
    private long FfpNeed(CharacterClass cls, CharacterStats stats, int level, long experience)
    {
        ArgumentNullException.ThrowIfNull(stats);
        var c = For(cls);
        var divisor = c.DivisorFfp ?? throw new InvalidOperationException($"Training {cls} has no FFP divisor.");
        var s = MotorolaFfp.Add(MotorolaFfp.Div(MotorolaFfp.FromInt(c.Sum(stats)), divisor), FfpTenMicro);
        var f = Ffp1Point8;
        for (var i = 1; i < level; i++)
            f = MotorolaFfp.Mul(f, Ffp1Point8);
        if (level == 0)
            f = FfpOne;
        var x = MotorolaFfp.Div(MotorolaFfp.Mul(MotorolaFfp.Add(f, FfpMinus0Point8), FfpThousand), s);
        var xp = MotorolaFfp.FromInt((int)Math.Clamp(experience, 0, int.MaxValue));
        return MotorolaFfp.ToInt(MotorolaFfp.Sub(xp, x));
    }

    /// <summary>
    /// The training price at <paramref name="priceIndex"/>. Level 0 costs 0 (<c>trunc(0^2.2)</c>; on the ST the FFP log of 0
    /// underflows to 0, seen live).
    /// </summary>
    public long GoldCost(int level, int charisma, int priceIndex)
    {
        var k = 20 - charisma;
        if (ClampCharisma) k = Math.Max(k, 0);
        var g = level <= 0 ? 0 : (long)Math.Pow(level, 2.2);
        return Math.Min(15000, g * k * k * priceIndex / 100);
    }

    /// <summary>Throws <see cref="ArgumentException"/> unless every class appears once with a positive divisor.</summary>
    public TrainingRules Validate()
    {
        ArgumentNullException.ThrowIfNull(Classes);
        foreach (var cls in Enum.GetValues<CharacterClass>())
        {
            var n = Classes.Count(c => c.Class == cls);
            if (n != 1) throw new ArgumentException($"Training: class {cls} appears {n} times (expected once).");
        }
        if (Classes.Any(c => !(c.Divisor > 0)))
            throw new ArgumentException("Training: every divisor must be positive.");
        if (Arithmetic == TrainingArithmetic.MotorolaFfp && Classes.Any(c => c.DivisorFfp is null))
            throw new ArgumentException("Training: FFP arithmetic needs every class's divisor_ffp.");
        return this;
    }

    public bool Equals(TrainingRules? other) =>
        other is not null && Reset == other.Reset && Classes.SequenceEqual(other.Classes)
        && ClampCharisma == other.ClampCharisma && SpellMarks == other.SpellMarks && HpTie == other.HpTie;

    public override int GetHashCode() => HashCode.Combine(Reset, Classes.Count, ClampCharisma, SpellMarks, HpTie);

    /// <summary>Phantasie I (DOS OV1 <c>0x11ea…0x12ab</c>, <c>town-menus.md</c> §Training) as profile values.</summary>
    public static TrainingRules PhantasieI { get; } = new(TrainingFactorReset.ExperienceZero,
    [
        new(CharacterClass.Thief, Str: 1, Int: 0, Dex: 2, Con: 0, Cha: 0, Luck: 0, Divisor: 19.8),
        new(CharacterClass.Monk, Str: 1, Int: 1, Dex: 1, Con: 0, Cha: 0, Luck: 0, Divisor: 49.5),
        new(CharacterClass.Ranger, Str: 1, Int: 1, Dex: 0, Con: 0, Cha: 1, Luck: 0, Divisor: 52.8),
        new(CharacterClass.Fighter, Str: 1, Int: 0, Dex: 1, Con: 0, Cha: 0, Luck: 0, Divisor: 16.5),
        new(CharacterClass.Priest, Str: 1, Int: 1, Dex: 0, Con: 0, Cha: 0, Luck: 0, Divisor: 18.1),
        new(CharacterClass.Wizard, Str: 0, Int: 2, Dex: 0, Con: 0, Cha: 0, Luck: 1, Divisor: 42.9),
    ])
    {
        ClampCharisma = true,
        SpellMarks = SpellMarkRule.NewLevelOnly,
        HpTie = HpRollTie.PlusOne,
    };
}
