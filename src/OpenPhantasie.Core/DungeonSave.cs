namespace OpenPhantasie.Core;

/// <summary>
/// The DOS one-slot dungeon save [C] (s36, dungeon-representation.md §Leaving and re-entering): <c>DNG.SAV</c> = dungeon
/// id word + map-screen image, <c>DNGX.SAV</c> = the 2500-byte live level buffer. Written only by OV2 <c>0x5929</c> when
/// the party leaves by an exit cell and answers YES to `core.prompt.save_dungeon_map`. An entry loads it only
/// when its id matches (no time factor). D-027 (user s37): DOS rule, except that a NO purges a slot holding this same
/// dungeon (DOS keeps it, so an older save would come back).
/// </summary>
public sealed class DungeonSaveSlot
{
    /// <summary>76 rows × 2 CGA banks × 42 bytes [C].</summary>
    public const int ScreenImageSize = 76 * 2 * 42;

    public DungeonSaveSlot(int dungeonId, byte[] levelBuffer, byte[]? screenImage = null)
    {
        // I / II: the 2500-byte DNGX.SAV buffer; other games keep their own record size (III: 4224, the DNP file).
        if (levelBuffer is null || levelBuffer.Length == 0)
            throw new ArgumentException("Level buffer must not be empty.", nameof(levelBuffer));
        if (screenImage is not null && screenImage.Length != ScreenImageSize)
            throw new ArgumentException($"Screen image must be {ScreenImageSize} bytes.", nameof(screenImage));
        DungeonId = dungeonId;
        LevelBuffer = (byte[])levelBuffer.Clone();
        ScreenImage = screenImage is null ? new byte[ScreenImageSize] : (byte[])screenImage.Clone();
    }

    /// <summary><c>DNG.SAV</c> word 0 = <c>[0x64ec]</c>; the slot is restored only on an exact match.</summary>
    public int DungeonId { get; }

    /// <summary>The level buffer <c>[0x6504]</c> as saved (grid, side tables, events, scripts with every rewrite).</summary>
    public byte[] LevelBuffer { get; }

    /// <summary>
    /// The DOS map-screen image, kept opaque for round trips. A slot made by Core has no DOS screen, so it is all
    /// zeros [I] (remaster choice; the remaster draws its own map).
    /// </summary>
    public byte[] ScreenImage { get; }
}
