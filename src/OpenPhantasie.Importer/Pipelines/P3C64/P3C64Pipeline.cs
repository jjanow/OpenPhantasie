namespace OpenPhantasie.Importer.Pipelines.P3C64;

/// <summary>
/// Phantasie III, Commodore 64 (SSI 1987; both sides of the one disk, <c>.d64</c> or C64PP's <c>.g64</c>): III's tables
/// (<see cref="P3C64Tables"/>, s218: a C64-only import is a playable III, with DOS III's numbers where the C64's differ
/// or it has none and the C64's own text; DOS III's, the Amiga's or the Apple's tables win when that copy is imported
/// too), the pre-generated party of the disk (<see cref="P3C64StarterGuild"/>), the C64's own lines of the string tables
/// (verbatim, and from s221 the lines it words its own way) and the remaster's wording for the rest. Then the C64 art pack (<see cref="P3C64Art"/>, s220; <c>graphics/c64.json</c>),
/// written whichever copy's tables win: the Amiga's pack stays III's default, then the Apple's, then the C64's.
/// </summary>
public abstract class P3C64PipelineBase : IImportPipeline
{
    public abstract string ProfileId { get; }

    public int RecipeVersion => 3; // 1 (s218): III's tables and the starter party from the C64; 2 (s220): the C64 art pack;
    // 3 (s221): the lines the C64 prints in its own wording (en.own.<profile>.json, tools/scripts/p3_c64_own_wording.json)

    /// <summary>DOS III's, the Amiga's and the Apple's tables win when one of them is imported in the same run: they run first.</summary>
    public IReadOnlyList<string> RunsAfter => ["p3-dos-1988", "p3-amiga-1.0", "p3-apple2"];

    /// <summary>
    /// The shipped profile fallback a C64-only III takes (the remaster's wording for the lines DOS III or the Amiga gives
    /// and the C64 does not): the Apple III's file, which covers every such id (an Apple [cr] import resolves no Apple
    /// line and leaves none on "..."), the same for both builds. The C64's own verbatim lines lie over it.
    /// </summary>
    public static readonly string[] FallbackProfiles = ["p3-apple2"];

    public void Run(ImportContext ctx)
    {
        ctx.Progress("tables");
        var tables = P3C64Tables.Write(ctx);

        // The game's own lines of the string tables (10-importer.md 7.6) and, with the C64's tables, the remaster's
        // wording for the lines only another copy holds (the common shared / I fallback and III's own).
        ctx.Progress("text");
        ImportedStrings.Write(ctx, ownWordingGame: tables ? P3Dos.P3DosPipeline.Game : null);
        if (tables)
            ImportedStrings.WriteFallback(ctx, FallbackProfiles, ImportedStrings.CommonFallbackGame, P3Dos.P3DosPipeline.Game);

        ctx.Progress("art");
        try
        {
            P3C64Art.Import(ctx);
        }
        catch (FileNotFoundException e)
        {
            ctx.Warn($"No C64 art: {e.Message}.");
        }
    }
}

/// <summary>C64PP's v1.0 (!) (and the held v1.1 / cracked pairs, which carry v1.0's programs).</summary>
public sealed class P3C64Pipeline : P3C64PipelineBase
{
    public override string ProfileId => "p3-c64";
}

/// <summary>C64PP's v1.2 (alt): v1.0's data files, its own programs and its own (re-rolled) starter party.</summary>
public sealed class P3C64V12Pipeline : P3C64PipelineBase
{
    public override string ProfileId => "p3-c64-1.2";
}
