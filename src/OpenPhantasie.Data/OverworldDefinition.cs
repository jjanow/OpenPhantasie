namespace OpenPhantasie.Data;

/// <summary>
/// One overworld region (OUT*.DAT). Geometry is 26×20 [C] (OV2 FUN_538B) in Phantasie I and II; Phantasie III has one
/// map per plane in this shape (Scandor 50×75, Light / Dark 9×9, Netherworld 31×16, <see cref="Plane"/> set).
/// </summary>
public sealed class OverworldRegionDefinition
{
    public required string Id { get; init; }
    public int RegionId { get; init; }
    public int ByteSize { get; init; }
    public int GridWidth { get; init; }
    public int GridHeight { get; init; }
    public IReadOnlyList<IReadOnlyList<int>> Tiles { get; init; } = Array.Empty<IReadOnlyList<int>>();
    public IReadOnlyList<OverworldFeatureDefinition> Features { get; init; } =
        Array.Empty<OverworldFeatureDefinition>();
    public IReadOnlyList<OverworldTextPage> TextPages { get; init; } =
        Array.Empty<OverworldTextPage>();
    public IReadOnlyList<DungeonEmbeddedText> EmbeddedTexts { get; init; } =
        Array.Empty<DungeonEmbeddedText>();

    /// <summary>Phantasie III: the plane id this map is (<c>scandor</c>, <c>light</c>, <c>dark</c>, <c>netherworld</c>); null for I / II.</summary>
    public string? Plane { get; init; }

    /// <summary>Phantasie III: the square a party coming from another plane lands on; null when none (Scandor, I / II).</summary>
    public (int X, int Y)? Arrival { get; init; }
}

public record OverworldFeatureDefinition(int Index, int X, int Y, int Arg, int DestX, int DestY);

public record OverworldTextPage(int Index, string Text)
{
    /// <summary>
    /// s153: the page as stored, up to its NUL, leading and trailing blanks kept (I: 39 characters); null when the data
    /// has none. I Amiga's requester-23 box centres a row by its full length (h65 <c>+0x1f4</c>), so the blanks place it.
    /// </summary>
    public string? Raw { get; init; }
}
