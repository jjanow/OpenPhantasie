using OpenPhantasie.Importer.Formats;

namespace OpenPhantasie.Importer.Pipelines.P2Apple2;

/// <summary>
/// Phantasie II, Apple II v1.0 2/5/86 (profile <c>p2-apple2</c>; the Memorial Set's II, user decision s158): the
/// Apple II art pack (port of <c>extract_a2_p2_graphics.py</c> + <c>build_a2_p2_asset_manifest.py</c>) and II's
/// tables built from the Apple's files (<c>extract_a2_p2_tables.py</c>, then <see cref="A2GameData"/>).
/// Reads two RDOS <c>.dsk</c> sides: the 4am crack's or the Memorial Set's (DOS 3.3 order; <see cref="Containers.RdosVolume"/>).
/// </summary>
public sealed class P2Apple2Pipeline : IImportPipeline
{
    public const string GameId = "phantasie2";

    public string ProfileId => "p2-apple2";

    public int RecipeVersion => 3; // 3 (s163): the common shared / I fallback

    /// <summary>The ST's tables win when both copies are imported in one run: the ST runs first.</summary>
    public IReadOnlyList<string> RunsAfter => ["p2-st-1.0"];

    public void Run(ImportContext context)
    {
        var src = new A2Source(context.Files);

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
