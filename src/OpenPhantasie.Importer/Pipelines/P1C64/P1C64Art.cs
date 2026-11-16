using OpenPhantasie.Importer.Formats;

namespace OpenPhantasie.Importer.Pipelines.P1C64;

/// <summary>
/// The Commodore 64 art of Phantasie I (s218; formats and evidence in <c>reverse-engineering/c64/phantasie1/GRAPHICS.md</c>;
/// the steps in <see cref="C64PhantasieArt"/>, which II shares): I's map routine tables, then (<see cref="Import"/>) the
/// pack <c>GameData/phantasie1/graphics/c64.json</c> (<see cref="P1C64ArtManifest"/>).
/// </summary>
public sealed class P1C64Art(ImportContext? ctx) : C64PhantasieArt(ctx)
{
    public const string GameId = "phantasie1";
    public const string RootPath = "reverse-engineering/c64/phantasie1/extracted/graphics";

    /// <summary>
    /// I's <c>UTIL-3.O</c> map routine (<c>$C5C3</c>): per class (map byte div 10; 12 = fog, bytes 120+) the colour
    /// (<c>$C688</c>) and the PETSCII character (<c>$C695</c>) it prints [C].
    /// </summary>
    public static readonly int[] MapColours = [5, 5, 9, 9, 0, 0, 11, 11, 9, 9, 6, 6, 0];
    public static readonly int[] MapPetscii = [0x60, 0x62, 0x64, 0x66, 0x68, 0x6A, 0x6C, 0x6E, 0x70, 0x72, 0x74, 0x20, 0x20];

    public override string Root => RootPath;
    public override IReadOnlyList<int> ClassColour => MapColours;
    public override IReadOnlyList<int> ClassPetscii => MapPetscii;

    /// <summary>s221: <c>P.END</c>'s Zeus (side 1 <c>ZALL</c>) and <c>CEMETARY</c>'s party wipe (side 2).</summary>
    protected override IEnumerable<(string Rel, RgbaImage Image)> EndScreens(Func<string, byte[]?> read) =>
        C64EndScreens.P1Ending(read).Concat(C64EndScreens.Defeat(read, doorway: false));

    /// <summary>The import step: the art under <see cref="RootPath"/> and the pack <c>graphics/c64.json</c>.</summary>
    public static void Import(ImportContext context)
    {
        var art = new P1C64Art(context);
        art.Run(name => context.Files.Find(name)?.Data);
        context.WriteGameDataBytes(GameId, "graphics/c64.json", PyJson.SerializeUtf8(P1C64ArtManifest.Pack(), 2, ensureAscii: true));
    }
}
