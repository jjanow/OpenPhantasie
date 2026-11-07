namespace OpenPhantasie.Data;

/// <summary>
/// One dungeon cell. Feature-byte classes are decoded in
/// <see cref="OpenPhantasie.Core.DungeonLevel.Classify"/> (OV2 FUN_0000_2249 [C]).
/// </summary>
public record DungeonCell(byte FeatureId);

/// <summary>
/// Simple rectangular grid used by dungeon levels.
/// </summary>
public sealed class Grid<T>
{
    public int Width { get; }
    public int Height { get; }
    private readonly T[] _cells;

    public Grid(int width, int height, T fill)
    {
        if (width <= 0 || height <= 0)
            throw new ArgumentOutOfRangeException(nameof(width), "Grid dimensions must be positive.");

        Width = width;
        Height = height;
        _cells = new T[width * height];
        Array.Fill(_cells, fill);
    }

    public T this[int x, int y]
    {
        get
        {
            ThrowIfOutOfRange(x, y);
            return _cells[y * Width + x];
        }
        set
        {
            ThrowIfOutOfRange(x, y);
            _cells[y * Width + x] = value;
        }
    }

    private void ThrowIfOutOfRange(int x, int y)
    {
        if ((uint)x >= (uint)Width || (uint)y >= (uint)Height)
            throw new ArgumentOutOfRangeException($"Cell ({x},{y}) outside {Width}x{Height} grid.");
    }
}

/// <summary>
/// Dungeon level definition. Walkable grid is 33×38 (1254 B) [C] in Phantasie I and II; Phantasie III's DNGS record has
/// a 24×30 grid (<see cref="GridWidth"/> × <see cref="GridHeight"/> come from the data). Remaining bytes are side tables + text.
/// </summary>
public sealed class DungeonLevelDefinition
{
    public required string Id { get; init; }

    /// <summary>Game-scoped id: <c>p1:</c> + the level number (DNG1 = <c>p1:1</c>), T1.2.</summary>
    public OpenPhantasie.Core.ContentId ContentId { get; init; }
    public int ByteSize { get; init; }

    /// <summary>Full raw DNG* file bytes.</summary>
    public byte[] RawFeatureBytes { get; init; } = Array.Empty<byte>();

    /// <summary>Walkable grid length (33×38 = 1254).</summary>
    public int GridByteLength { get; init; } = GameDataRepository.DungeonGridByteLength;

    public int GridWidth { get; init; } = GameDataRepository.DungeonGridWidth;
    public int GridHeight { get; init; } = GameDataRepository.DungeonGridHeight;

    public byte[] GetGridBytes()
    {
        var len = Math.Min(GridByteLength, RawFeatureBytes.Length);
        var copy = new byte[len];
        Buffer.BlockCopy(RawFeatureBytes, 0, copy, 0, len);
        return copy;
    }

    /// <summary>Decoded 33×38 cell grid.</summary>
    public Grid<DungeonCell>? Cells { get; init; }

    public IReadOnlyList<DungeonEmbeddedText> EmbeddedTexts { get; init; } =
        Array.Empty<DungeonEmbeddedText>();

    /// <summary>MESSn messages by id (1-based) with their menu options (OV2 0x2a95, s36).</summary>
    public IReadOnlyDictionary<int, OpenPhantasie.Core.DungeonMessage> Messages { get; init; } =
        new Dictionary<int, OpenPhantasie.Core.DungeonMessage>();

    /// <summary>Phantasie III: the dungeon's place name (master strings 34–44); null for I / II.</summary>
    public string? Name { get; init; }

    /// <summary>Phantasie III: the DNGS record's side tables (<c>FORMAT.md</c> §9.5); null for I / II.</summary>
    public DungeonRecordTables? Tables { get; init; }

    // DNGX.SAV / DNG.SAV are fully mapped in save-file-format.md (s36) [C]. Party position and
    // facing are NOT saved: re-entering a dungeon always starts at its OUT entrance.
}

public record DungeonEmbeddedText(string OffsetHex, string Text);
