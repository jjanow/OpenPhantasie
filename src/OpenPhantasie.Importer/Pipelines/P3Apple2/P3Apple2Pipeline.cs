namespace OpenPhantasie.Importer.Pipelines.P3Apple2;

/// <summary>
/// Phantasie III, Apple II (SSI 1987; the v3.0 5/26/87 originals or the v1.0 [cr] sides): III's tables
/// (<see cref="P3Apple2Tables.Write"/>, s212: an Apple-only import is a playable III, with DOS III's numbers where the
/// Apple's differ or it has none and the Apple's own text; DOS III's or the Amiga's tables win when that copy is
/// imported too) and the Apple's own lines of the string tables, with the remaster's wording for the rest.
/// Then the Apple II art pack (<see cref="P3Apple2Art"/>, s212).
/// </summary>
public sealed class P3Apple2Pipeline : IImportPipeline
{
    public string ProfileId => "p3-apple2";

    public int RecipeVersion => 2; // 1 (s212): III's tables and the Apple II art pack; 2 (s212): the remaster's wording for the lines it lacks

    /// <summary>DOS III's and the Amiga's tables win when both copies are imported in one run: they run first.</summary>
    public IReadOnlyList<string> RunsAfter => ["p3-dos-1988", "p3-amiga-1.0"];

    public void Run(ImportContext ctx)
    {
        ctx.Progress("tables");
        var tables = P3Apple2Tables.Write(ctx);

        // The game's own lines of the string tables (10-importer.md 7.6); with the Apple's tables, the remaster's wording
        // for the lines it lacks (the common shared / I fallback and III's own).
        // The p3-apple2 alternatives point into v3.0's programs; v1.0's are laid out differently, so they give none.
        ctx.Progress("text");
        if (IsV30(ctx.Files))
            ImportedStrings.Write(ctx, ownWordingGame: tables ? P3Dos.P3DosPipeline.Game : null);
        if (tables)
            ImportedStrings.WriteFallback(ctx, ImportedStrings.CommonFallbackGame, P3Dos.P3DosPipeline.Game);

        ctx.Progress("art");
        try
        {
            P3Apple2Art.Import(ctx);
        }
        catch (FileNotFoundException e)
        {
            ctx.Warn($"No Apple II art: {e.Message}.");
        }
    }

    /// <summary>The programs the string sources point into are v3.0's (5/26/87; <c>build_string_sources.py</c>
    /// <c>A3_PROGRAMS</c>).</summary>
    private static readonly Dictionary<string, string> V30Programs = new(StringComparer.Ordinal)
    {
        ["TOWN3"] = "844c62b9d04eed88a601720adb729bc3f02ec820",
        ["DUNGON3"] = "ab39dfe8ac0ade19b67879074c0a8b2c64120525",
    };

    public static bool IsV30(Profiles.FileSet files) =>
        V30Programs.All(kv => files.Find(kv.Key)?.Sha1 == kv.Value);
}
