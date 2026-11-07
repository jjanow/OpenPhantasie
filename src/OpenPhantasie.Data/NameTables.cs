namespace OpenPhantasie.Data;

/// <summary>
/// Named enum tables from names.json (classes, races, towns, etc.).
/// </summary>
public record NamedIndex(int Id, string Name);

/// <summary>
/// Master name tables extracted from PHANTASI.COM / TWNS.INT.
/// </summary>
public sealed class NameTables
{
    public IReadOnlyList<NamedIndex> Classes { get; init; } = Array.Empty<NamedIndex>();
    public IReadOnlyList<NamedIndex> Races { get; init; } = Array.Empty<NamedIndex>();
    public IReadOnlyList<NamedIndex> Spells { get; init; } = Array.Empty<NamedIndex>();
    public IReadOnlyList<NamedIndex> SpellLevelNames { get; init; } = Array.Empty<NamedIndex>();
    public IReadOnlyList<NamedIndex> Items { get; init; } = Array.Empty<NamedIndex>();
    public IReadOnlyList<NamedIndex> Monsters { get; init; } = Array.Empty<NamedIndex>();
    public IReadOnlyList<NamedIndex> TownLabels { get; init; } = Array.Empty<NamedIndex>();

    /// <summary>Phantasie III: the nine skills in the master string order (Attack … Fire Bow); empty for I / II.</summary>
    public IReadOnlyList<NamedIndex> Skills { get; init; } = Array.Empty<NamedIndex>();

    /// <summary>Phantasie III: social classes (Peasant 0 … Noble 3, endgame titles Darklord 4, Superhero 5).</summary>
    public IReadOnlyList<NamedIndex> SocialClasses { get; init; } = Array.Empty<NamedIndex>();

    /// <summary>Phantasie III: origins as record <c>+0x58</c> numbers them (Gelnor 0 … Darkness 5).</summary>
    public IReadOnlyList<NamedIndex> Origins { get; init; } = Array.Empty<NamedIndex>();

    /// <summary>Phantasie III: the six body areas in record order (Head, Left Arm, Right Arm, Torso, Left Leg, Right Leg).</summary>
    public IReadOnlyList<NamedIndex> BodyAreas { get; init; } = Array.Empty<NamedIndex>();

    /// <summary>Phantasie III: body-area states (Okay 0, Injured 1, Broken 2, Gone 3).</summary>
    public IReadOnlyList<NamedIndex> BodyStates { get; init; } = Array.Empty<NamedIndex>();
}
