namespace OpenPhantasie.Core;

/// <summary>
/// QoL <c>shared-aging</c> (<c>docs/plan/13-aging-normalised.md</c>, from D4-033): the one aging model all four games play
/// when the option is on, read from <c>GameData/shared/aging.json</c>. A remaster rule, not an original's: it is I's D-003
/// manual model (the I / II / III manuals' lifespans ±10 %, in 1/26-year age units) stretched over IV.
/// <list type="bullet">
/// <item>Lifespan: the race's band in years, picked by the game's luck roll (I / II seed <c>@10</c>, III LCK: 8–13;
/// IV Luck: 9–14), <c>round(26 · (lo + step · (hi − lo) / 5))</c>, step = luck − the game's lowest roll, clamped 0–5.
/// Fixed once, when the character is made, and carried unchanged by every transfer.</item>
/// <item>Grown older past 70 % of it (abilities × 4/5, once), dead past it (<see cref="Aging.IsPastDecline"/>,
/// <see cref="Aging.IsPastLifespan"/>), checked on town entry.</item>
/// </list>
/// </summary>
/// <param name="BandsByRace">The band in years of races 0–14 (I's numbering, which IV shares).</param>
/// <param name="OtherBand">Every other race (IV's mixed races and any race without a band).</param>
/// <param name="LuckMin">Each game's lowest luck roll: the roll that gives the band's low end.</param>
/// <param name="LuckSteps">Steps from the band's low end to its high end (5: six luck rolls).</param>
public sealed record SharedAgingModel(
    IReadOnlyList<(int Lo, int Hi)> BandsByRace,
    (int Lo, int Hi) OtherBand,
    IReadOnlyDictionary<GameId, int> LuckMin,
    int LuckSteps)
{
    /// <summary>Age units per year (I's age clock <c>@264</c>, IV's record <c>+1E</c>).</summary>
    public const int UnitsPerYear = 26;

    /// <summary>The band in years of <paramref name="race"/> (I's numbering; anything past it is "other").</summary>
    public (int Lo, int Hi) Band(int race) =>
        race >= 0 && race < BandsByRace.Count ? BandsByRace[race] : OtherBand;

    /// <summary>The lifespan in age units of a <paramref name="game"/> character of <paramref name="race"/> with <paramref name="luck"/>.</summary>
    public int Lifespan(GameId game, int race, int luck)
    {
        var (lo, hi) = Band(race);
        var step = Math.Clamp(luck - LuckMin.GetValueOrDefault(game, LuckMin.GetValueOrDefault(GameId.P1)), 0, LuckSteps);
        return (int)Math.Round(UnitsPerYear * (lo + step * (hi - lo) / (double)LuckSteps), MidpointRounding.AwayFromZero);
    }
}
