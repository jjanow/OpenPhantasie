namespace OpenPhantasie.Core;

/// <summary>
/// The party's Fireflash I–IV numbers (D3-075, <c>combat-round.md</c> section 2), III's rule data
/// (<c>GameData/phantasie3/rule-tables.json</c> <c>fireflash</c>), one value per spell #5–8:
/// <list type="bullet">
/// <item><see cref="ManualMax"/>: the manual's top damage (III PDF 11 / pr.14) for <c>p3.fireflash</c> = <c>manual</c>.</item>
/// <item><see cref="DosFactor"/>: DOS's multiplier <c>t</c> (<c>DS:0xc1a[5..8]</c>, root <c>0xb42e</c>) in
/// <c>rnd((INT / 2 + 1) · t)</c>.</item>
/// <item><see cref="DosCap"/>: DOS's caps on that roll (code immediates, root <c>0xb48a…0xb4c8</c>).</item>
/// </list>
/// Live (s212, DOSBox-X): the DOS rule, factors and the cap of 99 seen as decoded.
/// </summary>
public sealed record P3FireflashTable(IReadOnlyList<int> ManualMax, IReadOnlyList<int> DosFactor, IReadOnlyList<int> DosCap)
{
    /// <summary>The values III's data holds (1-10 / 1-30 / 1-60 / 1-100; t 1 / 4 / 8 / 13; caps 10 / 30 / 60 / 99), for
    /// fights built without game data (tests).</summary>
    public static P3FireflashTable Default { get; } = new([10, 30, 60, 100], [1, 4, 8, 13], [10, 30, 60, 99]);

    public P3FireflashTable Validate()
    {
        foreach (var (name, list) in new[] { ("manual_max", ManualMax), ("dos_factor", DosFactor), ("dos_cap", DosCap) })
            if (list is null || list.Count != 4 || list.Any(v => v < 1))
                throw new ArgumentException($"Fireflash {name}: four positive values (Fireflash I–IV) expected.");
        return this;
    }
}
