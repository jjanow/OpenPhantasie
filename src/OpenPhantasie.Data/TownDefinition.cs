namespace OpenPhantasie.Data;

/// <summary>
/// Mutable per-town Armory state from TWNS.DAT (12 × 291-byte records).
/// <see cref="Id"/> matches overworld town feature args (Pelnor = 1).
/// </summary>
public sealed class TownDefinition
{
    public required int Id { get; init; }
    public required int Index { get; init; }
    public required string Name { get; init; }
    public required int PriceIndex { get; init; }

    /// <summary>Stock quantity indexed by item id 0..180.</summary>
    public required IReadOnlyList<int> Stock { get; init; }

    /// <summary>TWNS.DAT <c>+0xb5</c>: the 45-entry restock list (OV1 <c>0x8e1</c>, town-menus.md §Armory restock).</summary>
    public IReadOnlyList<int> RestockItems { get; init; } = [];

    /// <summary>The master-disk TWNS.DAT record (291 B), carried into <see cref="OpenPhantasie.Core.TownState.DosRecord"/> (P1.4).</summary>
    public byte[]? DosRecord { get; init; }

    /// <summary>
    /// Phantasie II: the plane the town is on (<c>"ferronrah"</c> / <c>"netherworld"</c>); null when the data has none
    /// (Phantasie I, II's unreachable leftovers). Atari ST <c>logic-specs/towns.md</c> §3.
    /// </summary>
    public string? Plane { get; init; }

    /// <summary>
    /// Phantasie II: the town ids TRANSPORTATION offers from this town (ST <c>transport_menu 0x023ee</c>,
    /// <c>logic-specs/towns.md</c> §4). Null = the data sets no list (Phantasie I's every-other-town rule applies).
    /// </summary>
    public IReadOnlyList<int>? TransportDestinations { get; init; }

    /// <summary>Phantasie II: the town Exit position from <c>towns.json</c> (no DOS record); null for I (read from the record).</summary>
    public (int Region, int X, int Y)? Exit { get; init; }
}
