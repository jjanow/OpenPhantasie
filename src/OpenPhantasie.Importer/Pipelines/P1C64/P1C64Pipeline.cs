namespace OpenPhantasie.Importer.Pipelines.P1C64;

/// <summary>
/// Phantasie I, Commodore 64 (SSI 1985, v1.4; both disk sides, <c>.d64</c> or C64PP's <c>.g64</c>): I's tables
/// (<see cref="P1C64Tables"/>, s216: a C64-only import is a playable I, with the C64's text and DOS I's numbers; DOS I's
/// or the Amiga's tables win when that copy is imported too) and the remaster's wording for the string-table lines only
/// another copy holds. Then the C64 art pack (<see cref="P1C64Art"/>, s218; <c>graphics/c64.json</c>), written whichever
/// copy's tables win: the Amiga's pack stays I's default, the C64's is drawn when no Amiga copy is imported.
/// </summary>
public sealed class P1C64Pipeline : IImportPipeline
{
    public string ProfileId => "p1-c64";

    public int RecipeVersion => 6; // 1 (s216): I's tables from the C64; 2 (s218): the C64 art pack; 3 (s219): attacking frames for every monster; 4 (s221): item / feature pictures, the ending and party-wipe pictures; 5 (s223): the C64 end-screen animations (ending_scene.c64, c64_defeat, face / hand / door sprites); 6 (s224): the closing page after the grant (ending_scene.c64.closing, its C64-only lines), the party wipe's UNDEAD name row and morph

    /// <summary>DOS I's and the Amiga's tables win when one of them is imported in the same run: they run first.</summary>
    public IReadOnlyList<string> RunsAfter => ["p1-dos-1987", "p1-dos-bonus-1991", "p1-amiga-1.0"];

    public void Run(ImportContext ctx)
    {
        ctx.Progress("tables");
        var tables = P1C64Tables.Write(ctx);

        // The game's own lines of the string tables (10-importer.md 7.6): the C64 has no alternatives in the sources
        // files yet, so with its tables only the remaster's wording for the lines another copy holds (the common shared /
        // I fallback, s163).
        ctx.Progress("text");
        ImportedStrings.Write(ctx, ownWordingGame: tables ? P1DosPipeline.Game : null);
        if (tables)
            ImportedStrings.WriteFallback(ctx, ImportedStrings.CommonFallbackGame);

        ctx.Progress("art");
        ctx.TryOptional("No C64 art", () => P1C64Art.Import(ctx));
    }
}
