using OpenPhantasie.Importer.Formats;

namespace OpenPhantasie.Importer.Pipelines.P2Apple2;

/// <summary>
/// Phantasie II, Apple II: the Apple II art pack (port of <c>extract_a2_p2_graphics.py</c> +
/// <c>build_a2_p2_asset_manifest.py</c>) and II's tables built from the Apple's files (<c>extract_a2_p2_tables.py</c>,
/// then <see cref="A2GameData"/>). Two versions read the same way (<see cref="P2Apple2Pipeline"/> v1.0,
/// <see cref="P2Apple2V21Pipeline"/> v2.1): every file the importer reads holds the same data in both
/// (<c>reverse-engineering/apple2/phantasie2/FORMAT.md</c> section 4); only the program files' byte offsets of the
/// game's own lines differ, which <c>strings/en.sources.json</c> lists per profile.
/// </summary>
public abstract class P2Apple2PipelineBase : IImportPipeline
{
    public const string GameId = "phantasie2";

    public abstract string ProfileId { get; }

    public abstract int RecipeVersion { get; }

    public abstract IReadOnlyList<string> RunsAfter { get; }

    /// <summary>The version's name in the built files' <c>_source</c> lines.</summary>
    internal abstract string SourceName { get; }

    public void Run(ImportContext context)
    {
        var src = new A2Source(context.Files, SourceName);

        context.Progress("art");
        var gfx = new A2Graphics(context);
        gfx.Run(src);
        context.WriteGameDataBytes(GameId, "graphics/apple2.json", PyJson.SerializeUtf8(A2Manifest.Pack(gfx.Index), 2, ensureAscii: true));

        context.Progress("tables");
        var tables = A2GameData.Write(context, src, gfx.Index);

        // D2-050 (user s159): the Apple's own lines (verbatim, or in its own wording for II only while II's tables are
        // the Apple's; strings/en.sources.json), and the remaster's own wording for the lines it lacks.
        context.Progress("text");
        ImportedStrings.Write(context, ownWordingGame: tables ? GameId : null);
        ImportedStrings.WriteFallback(context, ImportedStrings.CommonFallbackGame, GameId);
    }
}

/// <summary>
/// Phantasie II, Apple II v1.0 2/5/86 (profile <c>p2-apple2</c>; the Memorial Set's II, user decision s158). Reads two
/// RDOS <c>.dsk</c> sides: the 4am crack's or the Memorial Set's (DOS 3.3 order; <see cref="Containers.RdosVolume"/>).
/// </summary>
public sealed class P2Apple2Pipeline : P2Apple2PipelineBase
{
    public const string Id = "p2-apple2";

    public override string ProfileId => Id;

    public override int RecipeVersion => 3; // 3 (s163): the common shared / I fallback

    /// <summary>The ST's tables win when both copies are imported in one run: the ST runs first. So do v2.1's (s212).</summary>
    public override IReadOnlyList<string> RunsAfter => ["p2-st-1.0", P2Apple2V21Pipeline.Id];

    internal override string SourceName => A2Tables.Source;
}

/// <summary>
/// Phantasie II, Apple II v2.1 8/19/86 (profile <c>p2-apple2-2.1</c>, s212): the original disk pair under RDOS 3.3
/// (16-sector). Same data files as v1.0; DUNGON2 fixes v1.0's gold-op bug (FORMAT.md section 3.7), which the remaster
/// never had (Core plays the ST's dungeon script). When v1.0 is imported too, v2.1's tables win (v1.0 runs after it).
/// </summary>
public sealed class P2Apple2V21Pipeline : P2Apple2PipelineBase
{
    public const string Id = "p2-apple2-2.1";

    public override string ProfileId => Id;

    public override int RecipeVersion => 1;

    /// <summary>The ST's tables win when both copies are imported in one run: the ST runs first.</summary>
    public override IReadOnlyList<string> RunsAfter => ["p2-st-1.0"];

    internal override string SourceName =>
        "Phantasie II, Apple II v2.1 8/19/86 (RDOS 3.3 original; reverse-engineering/apple2/phantasie2/FORMAT.md)";
}
