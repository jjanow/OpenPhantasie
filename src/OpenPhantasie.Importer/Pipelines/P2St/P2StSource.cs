using OpenPhantasie.Importer.Profiles;

namespace OpenPhantasie.Importer.Pipelines.P2St;

/// <summary>
/// The player's Phantasie II Atari ST files (port of <c>extract_st_p2_common.py</c>). Both ST sets in the repo are
/// played copies, so nothing here reads a file play rewrites (<c>TWNS.DAT</c>, <c>GUILD.DAT</c>, <c>SACK.DAT</c>,
/// <c>*.SAV</c>, the grid bytes 0..519 of <c>OUTn.DAT</c>): a region's starting state is its <c>MAPS.INT</c> block +
/// the static bytes 520..2999 of <c>OUTn.DAT</c> (UTILIT2.PRG 0xb2c does the same on START NEW GAME).
/// </summary>
internal sealed class P2StSource
{
    /// <summary>The 17 overworld regions II ships, in <c>MAPS.INT</c> order (UTILIT2.PRG 0xb2c) [C].</summary>
    public static readonly int[] Regions = [1, 2, 3, 5, 6, 7, 8, 11, 12, 17, 18, 22, 26, 27, 28, 30, 31];
    public static readonly int[] Dungeons = [1, 2, 3, 4, 5, 6, 7, 8];
    public const int Fog = 120;
    public const int Grid = 26 * 20;
    public const int OutSize = 3000;

    private readonly FileSet _files;
    private readonly Dictionary<int, byte[]> _regions = [];

    public P2StSource(FileSet files)
    {
        _files = files;
        Phant = new StPrg(Read("PHANT.PRG"), "PHANT.PRG");
        Start = new StPrg(Read("START.PRG"), "START.PRG");
    }

    public StPrg Phant { get; }
    public StPrg Start { get; }

    public byte[] Read(string name) => _files.Read(name);

    /// <summary>OUTn.DAT in its starting state: MAPS.INT's grid block + the file's static bytes 520..2999.</summary>
    public byte[] Region(int r)
    {
        if (_regions.TryGetValue(r, out var cached))
            return cached;
        var k = Array.IndexOf(Regions, r);
        if (k < 0)
            throw new ArgumentOutOfRangeException(nameof(r));
        var maps = Read("MAPS.INT");
        if (maps.Length != Grid * Regions.Length)
            throw new InvalidDataException($"MAPS.INT: {maps.Length} bytes, expected {Grid * Regions.Length}");
        var o = Read($"OUT{r}.DAT");
        if (o.Length != OutSize)
            throw new InvalidDataException($"OUT{r}.DAT: {o.Length} bytes, expected {OutSize}");
        var data = new byte[OutSize];
        maps.AsSpan(k * Grid, Grid).CopyTo(data);
        o.AsSpan(Grid).CopyTo(data.AsSpan(Grid));
        _regions[r] = data;
        return data;
    }

    public static int Unfog(int b) => b >= Fog ? b - Fog : b;
}
