namespace OpenPhantasie.Core;

/// <summary>
/// "A MAGICAL POOL" — terrain class 11 (tiles 111/112/113), OV2 <c>0x656b..0x66ff</c> [C] (s39,
/// <c>overworld-movement.md</c> §Magical pool). Each living member who hasn't bathed in this pool kind gains
/// <c>isqrt(luck)·luck/11</c> in one random attribute (STR/INT/DEX/CON/CHA).
/// D-051 (user s40, manual): the gain stops at the race maximum from the manual's "Maximum Value for Attributes by
/// Race" table (= race modifier + 8, D-001); DOS has no cap. The pool is still marked used (<c>@90</c>) and the member
/// still "IS CHANGED" when the stat is already at the maximum, so DNG5's three-pool check stays reachable.
/// </summary>
public static class MagicalPool
{
    public static string Banner => GameText.S("core.pool.a_magical_pool");

    /// <summary>Pool factor <c>k = 2·T + 1</c> (T = tile % 10: 111/112/113 → 3/5/7), multiplied into <c>@90</c>.</summary>
    public static int Factor(int tileSubcode) => 2 * tileSubcode + 1;

    /// <summary>
    /// Attribute gain: <c>isqrt(@10)·@10/11</c> with the DOS Newton isqrt (COM <c>0x3675</c>, isqrt(8) = 3); luck 8..13 → 2..3
    /// ([D✓] s65: luck 8 gained 2).
    /// </summary>
    public static int Gain(int luck) => CombatMath.IntegerSqrt(luck) * luck / 11;

    /// <summary>Manual race maximum for attribute <paramref name="stat"/> (0 STR … 4 CHA). An undead uses its birth race.</summary>
    public static int RaceMaximum(Character member, int stat, CharacterCreationTables tables)
    {
        var race = member.Race == Race.Undead ? member.BirthRace : member.Race;
        return tables.RaceAttributeModifiers[(int)race][stat] + 8;
    }

    /// <summary>
    /// Bathe the party (DOS order: one <c>rand0_99</c> per member who isn't skipped). Returns the lines to show:
    /// the banner, then "%-10.9sIS CHANGED" per changed member.
    /// </summary>
    /// <remarks><paramref name="noCap"/> = DOS (<c>p1.pool-cap</c>, D-051): no race maximum.</remarks>
    public static IReadOnlyList<string> Apply(
        Party party, int tileSubcode, CharacterCreationTables tables, IRandomSource rng, bool noCap = false)
    {
        ArgumentNullException.ThrowIfNull(party);
        ArgumentNullException.ThrowIfNull(tables);
        ArgumentNullException.ThrowIfNull(rng);
        var k = Factor(tileSubcode);
        var lines = new List<string> { Banner };
        foreach (var m in party.Members)
        {
            if (m.Status == CharacterStatus.Dead || m.Phantasie1.Word90 % k == 0)
                continue;
            m.Phantasie1.Word90 = (m.Phantasie1.Word90 * k) & 0xffff;
            var stat = (5 * rng.Next(0, 100) + 100) / 100 - 1; // jump table 0x66a9: 1 STR … 5 CHA
            var cur = Get(m.Stats, stat);
            var max = RaceMaximum(m, stat, tables);
            var next = noCap
                ? cur + Gain(m.Stats.Luck) // DOS OV2 0x656b: no cap
                : Math.Max(cur, Math.Min(cur + Gain(m.Stats.Luck), max)); // D-051; an imported value over max is kept
            m.Stats = Set(m.Stats, stat, next);
            lines.Add(GameText.F("core.pool.x_is_changed", Truncate(m.Name, 9)));
        }
        return lines;
    }

    private static string Truncate(string s, int n) => s.Length <= n ? s : s[..n];

    private static int Get(CharacterStats s, int stat) => stat switch
    {
        0 => s.Str, 1 => s.Int, 2 => s.Dex, 3 => s.Con, _ => s.Cha,
    };

    private static CharacterStats Set(CharacterStats s, int stat, int v) => stat switch
    {
        0 => s with { Str = v }, 1 => s with { Int = v }, 2 => s with { Dex = v }, 3 => s with { Con = v }, _ => s with { Cha = v },
    };
}
