namespace OpenPhantasie.Core;

/// <summary>A dungeon place on an outdoor map (<c>MOUT</c> header): location map (<c>O_0xx</c>) and its entry square.</summary>
public sealed record P4DungeonSite(int Location, int X, int Y);

/// <summary>
/// One outdoor map 1–24 as shipped (<c>MOUT</c> record, <c>outdoor.md</c> §1): 26 × 26 tile bytes (type = byte / 10,
/// sub-index = byte % 10) and the header the place tiles read by sub-index: towns, dungeons, inns and the destination of
/// the map's pool or Charon (kind 0 when the map has none).
/// </summary>
public sealed record P4OutdoorMap(
    int Map, IReadOnlyList<byte> Tiles, IReadOnlyList<int> Towns, IReadOnlyList<P4DungeonSite> Dungeons,
    IReadOnlyList<int> Inns, P4Transfer Destination)
{
    public const int Size = 26;

    /// <summary><c>_out[x + 26 y]</c>.</summary>
    public int Tile(int x, int y) => Tiles[x + Size * y];

    public static int TypeOf(int tile) => tile / 10;

    public static int SubOf(int tile) => tile % 10;
}

/// <summary>
/// A town as the loops need it (<c>towns.json</c>): name, type (0 Pendragon, 1 with a shipwright, 2 neither), price
/// factor, where leaving puts the party (<c>_townexit</c>) and the shipwright's mooring square (<c>_shipexit</c>; null =
/// none) on the exit map [C].
/// </summary>
public sealed record P4TownSite(int Id, string Name, int Type, int ExitMap, int ExitX, int ExitY, int? ShipX, int? ShipY);

/// <summary>
/// Everything the outdoor and town loops read from the data: the outdoor maps by number, the towns by map number, the
/// location maps' names and the outdoor inns' name string ids by <c>_inns</c> index.
/// </summary>
public sealed record P4Atlas(
    IReadOnlyDictionary<int, P4OutdoorMap> Outdoors,
    IReadOnlyDictionary<int, P4TownSite> Towns,
    IReadOnlyDictionary<int, string> DungeonNames,
    IReadOnlyDictionary<int, string> InnNameIds)
{
    /// <summary>The outdoor map grid (<c>_omove</c>): maps 1–15 sit 5 wide, maps 16–24 (the Netherworld) 3 wide [C].</summary>
    public static int GridWidth(int map) => map < 16 ? 5 : 3;
}
