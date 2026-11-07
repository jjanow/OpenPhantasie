using OpenPhantasie.Importer.Formats;

namespace OpenPhantasie.Importer.Pipelines.P2Apple2;

/// <summary>
/// Phantasie II, Apple II v1.0 2/5/86 (profile <c>p2-apple2</c>; the Memorial Set's II, user decision s158): the
/// Apple II art pack (port of <c>extract_a2_p2_graphics.py</c> + <c>build_a2_p2_asset_manifest.py</c>) and II's
/// tables built from the Apple's files (<c>extract_a2_p2_tables.py</c>, then <see cref="A2GameData"/>).
/// Reads the 4am crack's two RDOS <c>.dsk</c> sides (<see cref="Containers.RdosVolume"/>).
/// </summary>
public sealed class P2Apple2Pipeline : IImportPipeline
{
    public const string GameId = "phantasie2";

    public string ProfileId => "p2-apple2";

    public int RecipeVersion => 1;

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
        A2GameData.Write(context, src, gfx.Index);
    }
}
