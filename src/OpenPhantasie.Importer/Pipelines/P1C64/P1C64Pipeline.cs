namespace OpenPhantasie.Importer.Pipelines.P1C64;

/// <summary>
/// Phantasie I, Commodore 64 (SSI 1985, v1.4; both disk sides, <c>.d64</c> or C64PP's <c>.g64</c>): I's tables
/// (<see cref="P1C64Tables"/>, s216: a C64-only import is a playable I, with the C64's text and DOS I's numbers; DOS I's
/// or the Amiga's tables win when that copy is imported too) and the remaster's wording for the string-table lines only
/// another copy holds. No art yet: the C64 pictures (FORMAT.md section 6) are not a pack, so the Client draws I's
/// default art when another copy gave it.
/// </summary>
public sealed class P1C64Pipeline : IImportPipeline
{
    public string ProfileId => "p1-c64";

    public int RecipeVersion => 1; // 1 (s216): I's tables from the C64

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
    }
}
