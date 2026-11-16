using OpenPhantasie.Importer.Formats;
using OpenPhantasie.Importer.Pipelines.P1C64;

namespace OpenPhantasie.Importer.Pipelines.P2C64;

/// <summary>
/// The Commodore 64 art of Phantasie II (s219; formats and evidence in <c>reverse-engineering/c64/phantasie2/GRAPHICS.md</c>):
/// I's engine (<see cref="C64PhantasieArt"/>; II's <c>FONT+HER</c>, <c>HSPRIT</c> and item pictures are I's byte for byte)
/// with II's own monsters, title, terrain characters and map routine tables; then (<see cref="Import"/>) the pack
/// <c>GameData/phantasie2/graphics/c64.json</c> (<see cref="P2C64ArtManifest"/>). Files are read from side 1 first (the
/// town / wilderness side: its <c>MX 30</c>, <c>MX 37</c>, <c>MX 62</c> differ from side 2's in band bytes only), then side 2
/// (the dungeon side's feature pictures).
/// </summary>
public sealed class P2C64Art(ImportContext? ctx) : C64PhantasieArt(ctx)
{
    public const string GameId = P2C64Tables.Game;
    public const string RootPath = "reverse-engineering/c64/phantasie2/extracted/graphics";

    /// <summary>
    /// II's <c>UTIL-3.O</c> map routine (<c>$C5C3</c>): per class (map byte div 10; 12 = fog, bytes 120+) the colour
    /// (<c>$C68A</c>) and the PETSCII character (<c>$C697</c>) it prints [C].
    /// </summary>
    public static readonly int[] MapColours = [5, 5, 9, 9, 12, 7, 11, 0, 9, 9, 6, 6, 0];
    public static readonly int[] MapPetscii = [0x60, 0x62, 0x64, 0x66, 0x68, 0x6A, 0x6C, 0x6E, 0x74, 0x72, 0x74, 0x72, 0x20];

    public override string Root => RootPath;
    public override IReadOnlyList<int> ClassColour => MapColours;
    public override IReadOnlyList<int> ClassPetscii => MapPetscii;

    /// <summary>The import step: the art under <see cref="RootPath"/> and the pack <c>graphics/c64.json</c>.</summary>
    public static void Import(ImportContext context)
    {
        var src = new P2C64Source(context.Files);
        new P2C64Art(context).Run(src.Either);
        context.WriteGameDataBytes(GameId, "graphics/c64.json", PyJson.SerializeUtf8(P2C64ArtManifest.Pack(), 2, ensureAscii: true));
    }
}
