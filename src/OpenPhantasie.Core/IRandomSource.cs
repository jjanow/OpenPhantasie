namespace OpenPhantasie.Core;

/// <summary>
/// Deterministic RNG seam for combat and skill checks.
/// </summary>
public interface IRandomSource
{
    int Next(int minInclusive, int maxExclusive);
}

/// <summary>
/// Thin wrapper over <see cref="System.Random"/> for production use.
/// </summary>
public sealed class SystemRandomSource : IRandomSource
{
    private readonly Random _random;

    public SystemRandomSource() => _random = new Random();
    public SystemRandomSource(int seed) => _random = new Random(seed);

    public int Next(int minInclusive, int maxExclusive) =>
        _random.Next(minInclusive, maxExclusive);
}

/// <summary>
/// Seeded RNG for unit tests — same seed always yields the same sequence.
/// </summary>
public sealed class SeededRandomSource : IRandomSource
{
    private readonly Random _random;

    public SeededRandomSource(int seed) => _random = new Random(seed);

    public int Next(int minInclusive, int maxExclusive) =>
        _random.Next(minInclusive, maxExclusive);
}
