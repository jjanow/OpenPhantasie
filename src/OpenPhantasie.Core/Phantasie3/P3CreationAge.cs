namespace OpenPhantasie.Core;

/// <summary>
/// Phantasie III's age and maximum age at creation (<c>rule-tables.json</c> <c>creation_age</c>; DOS ovl2
/// <c>0x2691…0x2748</c>, MSEM87 float code, decoded s98 [C]): a single-precision factor <c>f = LCK · Mul / Div</c> by race,
/// maximum age <c>+0x2a</c> = <c>⌊f⌋</c>, age clock <c>+0x28</c> = <c>⌊(rnd(7) + RollBase) · f + Offset⌋</c> (1/26 year units).
/// </summary>
/// <param name="RollBase">Added to the <c>rnd(7)</c> age roll (24).</param>
/// <param name="Offset">Added after the multiply (260 units, 10 years).</param>
/// <param name="Factors">LCK factor by race as <c>(Mul, Div)</c>; races not listed use <paramref name="OtherFactor"/>.</param>
/// <param name="OtherFactor">Every other race (DOS: 2 / 5).</param>
public sealed record P3CreationAgeRule(
    int RollBase, int Offset, IReadOnlyDictionary<Race, (int Mul, int Div)> Factors, (int Mul, int Div) OtherFactor)
{
    /// <summary>The race factor times LCK, rounded to single precision as DOS stores it (<c>fstp dword [bp−8]</c>).</summary>
    public float Factor(Race race, int lck)
    {
        var (mul, div) = Factors.TryGetValue(race, out var f) ? f : OtherFactor;
        return (float)(lck * (double)mul / div);
    }

    /// <summary>The maximum age word <c>+0x2a</c>: <c>⌊f⌋</c> (MS C <c>_ftol</c>, root <c>0x3b2:0x2d9d</c>).</summary>
    public int MaxAge(Race race, int lck) => (int)Factor(race, lck);

    /// <summary>The starting age clock <c>+0x28</c> for the age roll <paramref name="roll"/> (<c>rnd(7)</c>, 1–7).</summary>
    public int AgeClock(Race race, int lck, int roll) => (int)((roll + RollBase) * (double)Factor(race, lck) + Offset);
}
