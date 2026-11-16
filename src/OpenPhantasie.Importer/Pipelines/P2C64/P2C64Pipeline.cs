using OpenPhantasie.Importer.Containers;
using OpenPhantasie.Importer.Profiles;

namespace OpenPhantasie.Importer.Pipelines.P2C64;

/// <summary>
/// Phantasie II, Commodore 64 v1.2 (SSI 1986; both disk sides, <c>.d64</c> or C64PP's <c>.g64</c>): II's tables
/// (<see cref="P2C64Tables"/>, s218: a C64-only import is a playable II, with the C64's text and the Atari ST's
/// numbers, as the Apple import; the ST's and the Apple's tables win when that copy is imported too) and the C64's own
/// lines of the string tables. Then the C64 art pack (<see cref="P2C64Art"/>, s219; <c>graphics/c64.json</c>), written
/// whichever copy's tables win: the ST's pack stays II's default, then the Apple's, then the C64's. v1.0 is recognised
/// and not imported (user s217).
/// </summary>
public sealed class P2C64Pipeline : IImportPipeline
{
    public const string Id = "p2-c64";

    public string ProfileId => Id;

    public int RecipeVersion => 2; // 1 (s218): II's tables from the C64; 2 (s219): the C64 art pack

    /// <summary>The ST's and the Apple's tables win when one of them is imported in the same run: they run first.</summary>
    public IReadOnlyList<string> RunsAfter => ["p2-st-1.0", "p2-apple2", "p2-apple2-2.1"];

    public void Run(ImportContext ctx)
    {
        ctx.Progress("tables");
        var tables = P2C64Tables.Write(ctx, new P2C64Source(ctx.Files));

        // The C64's lines found verbatim (strings/en.sources.json, p2-c64) and the remaster's wording for the lines it
        // lacks (the Apple import's rule, D2-050: the common shared / I file and II's).
        ctx.Progress("text");
        ImportedStrings.Write(ctx, ownWordingGame: tables ? P2C64Tables.Game : null);
        ImportedStrings.WriteFallback(ctx, ImportedStrings.CommonFallbackGame, P2C64Tables.Game);

        ctx.Progress("art");
        try
        {
            P2C64Art.Import(ctx);
        }
        catch (FileNotFoundException e)
        {
            ctx.Warn($"No C64 art: {e.Message}.");
        }
    }
}

/// <summary>
/// The two sides of the C64 disk, each read on its own: both carry files of the same name and other bytes (<c>MN</c>,
/// <c>B</c>, the pictures; FORMAT.md section 2). Side 1 holds <c>TOWN2</c>, side 2 <c>DUNGEON2</c>.
/// </summary>
internal sealed class P2C64Source
{
    private readonly Container _s1, _s2;

    public P2C64Source(FileSet files)
    {
        _s1 = files.Containers.FirstOrDefault(c => Has(c, "TOWN2")) ?? throw new FileNotFoundException("side 1 (TOWN2) not found");
        _s2 = files.Containers.FirstOrDefault(c => Has(c, "DUNGEON2")) ?? throw new FileNotFoundException("side 2 (DUNGEON2) not found");
    }

    private static bool Has(Container c, string name) => c.Files.Any(f => f.Path == name);

    /// <summary>A side 1 file as stored (a PRG with its load address).</summary>
    public byte[] Side1(string name) => Read(_s1, name);

    /// <summary>A side 2 file as stored (a PRG with its load address).</summary>
    public byte[] Side2(string name) => Read(_s2, name);

    /// <summary>A file as stored, side 1's when both sides hold one, else side 2's; null when neither does.</summary>
    public byte[]? Either(string name) => Find(_s1, name) ?? Find(_s2, name);

    private static byte[]? Find(Container c, string name) => c.Files.FirstOrDefault(f => f.Path == name)?.Data;

    private static byte[] Read(Container c, string name) =>
        (c.Files.FirstOrDefault(f => f.Path == name) ?? throw new FileNotFoundException($"{name} not found on {c.Description}", name)).Data;
}
