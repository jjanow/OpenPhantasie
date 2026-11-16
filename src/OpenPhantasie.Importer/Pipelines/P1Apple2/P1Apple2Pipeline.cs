namespace OpenPhantasie.Importer.Pipelines.P1Apple2;

/// <summary>
/// Phantasie I, Apple II (SSI 1984, VERSION 1.0; the boot and dungeon sides, the 4am crack's <c>.dsk</c> pair or the
/// original <c>.woz</c> pair): I's tables (<see cref="P1Apple2Tables"/>, s222: an Apple-only import is a playable I, with
/// the Apple's text and DOS I's numbers; DOS I's, the Amiga's or the C64's tables win when that copy is imported too) and
/// the string-table lines: the Apple's own where its programs hold them verbatim, the remaster's wording for the lines
/// only another copy holds. Then the Apple II art pack (<see cref="P1Apple2Art"/>, s223; <c>graphics/apple2.json</c>),
/// written whichever copy's tables win: the Amiga's pack stays I's default, then the C64's, then the Apple's.
/// </summary>
public sealed class P1Apple2Pipeline : IImportPipeline
{
    public string ProfileId => "p1-apple2";

    // 1 (s222): I's tables from the Apple II; 2 (s223): the Apple II art pack; 3 (s224): the combat field (spell hit per
    // SCALE, apple2_field); 4 (s224): the end scenes (shapes.bin, page2.bin, apple2_scenes) and the scenes' lines
    public int RecipeVersion => 4;

    /// <summary>DOS I's, the Amiga's and the C64's tables win when one of them is imported in the same run: they run first.</summary>
    public IReadOnlyList<string> RunsAfter => ["p1-dos-1987", "p1-dos-bonus-1991", "p1-amiga-1.0", "p1-c64"];

    public void Run(ImportContext ctx)
    {
        ctx.Progress("tables");
        var tables = P1Apple2Tables.Write(ctx);

        // The game's own lines of the string tables (10-importer.md 7.6): the p1-apple2 alternatives of the sources files
        // (found verbatim in TOWN / OUTDOORS / DUNGON / CEMETARY), and with the Apple's tables the remaster's wording for
        // the lines only another copy holds (the common shared / I fallback).
        ctx.Progress("text");
        ImportedStrings.Write(ctx, ownWordingGame: tables ? P1DosPipeline.Game : null);
        if (tables)
            ImportedStrings.WriteFallback(ctx, ImportedStrings.CommonFallbackGame);

        ctx.Progress("art");
        ctx.TryOptional("No Apple II art", () => P1Apple2Art.Import(ctx));
    }
}
