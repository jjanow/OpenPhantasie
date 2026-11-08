namespace OpenPhantasie.Core;

/// <summary>
/// Feature-byte class from OV2.OVR FUN_0000_2249 (dungeon-representation.md [C]).
/// </summary>
public enum DungeonFeatureKind
{
    Wall,
    Floor,
    Trap,
    Encounter,
    ScriptLow,
    Combat,
    Message,
    LockOrAmbush,
    ExitOrStairs,
    StairsMessage,
    ScriptedEvent,
    Other,
}

public enum DungeonMoveKind
{
    Moved,
    BlockedWall,
    TriggerTrap,
    TriggerEncounter,
    TriggerCombat,
    TriggerMessage,
    TriggerLock,
    TriggerStairs,
    ExitDungeon,
    ScriptedEvent,
}

public enum DungeonFacing
{
    North = 0,
    East = 1,
    South = 2,
    West = 3,
}

/// <summary>Result of one dungeon step (OV2 FUN_0000_1f05).</summary>
public sealed class DungeonMoveResult
{
    public required DungeonMoveKind Kind { get; init; }
    public int LevelId { get; init; }
    public int X { get; init; }
    public int Y { get; init; }
    public byte FeatureByte { get; init; }
    public DungeonFeatureKind FeatureKind { get; init; }
    public string? Message { get; init; }
    public int? Damage { get; init; }
    public int? MessageId { get; init; }

    /// <summary>
    /// First monster (0-based, <c>[0x64e6] − 1</c>) when this step starts a fight: a wandering encounter, a forced-fight
    /// cell or a lock ambush. The session builds the encounter with <see cref="Surprise"/>.
    /// </summary>
    public int? MonsterId { get; init; }

    /// <summary>Surprise flags the cell set for that fight (<c>[0x649e]</c>, <c>[0x64a0]</c>).</summary>
    public SurpriseFlags? Surprise { get; init; }

    /// <summary>
    /// A trap was spotted by this party member (index into <see cref="Party.Members"/>); "%s SPOTS A TRAP" + D)ISARM OR L)EAVE
    /// waits (<see cref="DungeonTraversal.ResolveSpottedTrap"/>, session <c>AnswerDungeonTrap</c>).
    /// </summary>
    public int? SpotterIndex { get; init; }

    /// <summary>
    /// A lock ambush was heard by this member; "%s `core.dungeon.x_hears_monsters_go_back`" waits
    /// (<see cref="DungeonTraversal.ResolveHeardMonsters"/>, session <c>AnswerDungeonLock</c>).
    /// </summary>
    public int? ListenerIndex { get; init; }

    /// <summary>The party was pushed back to its previous cell (<c>0x29a9</c>).</summary>
    public bool SteppedBack { get; init; }

    /// <summary>s139: a member's disarm roll failed ("%s FAILS", then the spring; II ST <c>0x10e9c</c> sounds 29 first).</summary>
    public bool DisarmFailed { get; init; }

    /// <summary>Every line the step printed, in order (trap report, swim report …); <see cref="Message"/> is the first.</summary>
    public IReadOnlyList<string> Log { get; init; } = Array.Empty<string>();

    /// <summary>
    /// s144: the runs of <see cref="Log"/> the original shows in a window of their own rather than on the message line
    /// (<see cref="LogWindow"/>): a trap's MESS text and its "IN THE TRAP:" report, a description cell's text.
    /// </summary>
    public IReadOnlyList<LogWindow> Windows { get; init; } = Array.Empty<LogWindow>();
}

/// <summary>s144: what a <see cref="LogWindow"/> holds.</summary>
public enum LogWindowKind
{
    /// <summary>
    /// A MESS text with no menu, shown by DOS <c>0x2a95</c> (I Amiga h41 <c>+0x11e4</c>: requester 21 at the top of the map,
    /// timed): a trap's message, script op 11 / 19, an event's own message, a description cell (<c>0x12..0x7f</c>).
    /// </summary>
    Text,

    /// <summary>
    /// A damage report: its title ("IN THE TRAP:", "REPORT:") and the member lines under it (I Amiga: requester 21 with
    /// the title and an OK button, h86 <c>+0(0x15)</c>, waiting for the click).
    /// </summary>
    Report,

    /// <summary>
    /// s153: one timed message box of 1–3 rows (I Amiga's requester 23, h44 <c>+0x466</c> / <c>+0x4d4</c> / <c>+0x556</c>): an
    /// overworld location's text pages (h59 <c>+0x182</c>; DOS prints them on the message line, OV2 <c>0x5fae</c>). Its
    /// <see cref="LogWindow.Rows"/> are the pages as stored, blanks kept, one per page.
    /// </summary>
    Box,
}

/// <summary>
/// s144: <paramref name="Count"/> lines of a log from index <paramref name="Start"/> that are one <paramref name="Kind"/>
/// window. Core only marks them (DOS prints them as before); a front end may draw them as the original's window.
/// </summary>
public readonly record struct LogWindow(int Start, int Count, LogWindowKind Kind)
{
    /// <summary>
    /// s153: the rows the window shows when they differ from the logged lines (as the original stores them: an overworld
    /// page's leading blanks place it in the box); null: the logged lines.
    /// </summary>
    public IReadOnlyList<string>? Rows { get; init; }
}

/// <summary>Party position inside a dungeon level.</summary>
public sealed class DungeonState
{
    public int LevelId { get; set; } = 1;
    public int X { get; set; }
    public int Y { get; set; }
    public DungeonFacing Facing { get; set; } = DungeonFacing.South;

    /// <summary>
    /// The cell the party came from (<c>[0x751e]/[0x7520]</c>, saved by every step, OV2 <c>0x1f58</c>). Script TEST and
    /// BEG/FLEE push the party back here (<c>0x29a9</c>); teleport sets it to the destination.
    /// </summary>
    public int PrevX { get; set; }
    public int PrevY { get; set; }

    /// <summary>
    /// Wandering-encounter chance <c>[0x1a5a]</c> [C] (s33/s39): 7 on every dungeon entry (state 13), −1 per encounter
    /// build then 1 once below 5, +1 after a BEG. A cell <c>0x08..0x11</c> fights when <c>rand0_99 ≤</c> this.
    /// Not in <c>DNGX.SAV</c>.
    /// </summary>
    public int WanderingChance { get; set; } = DungeonEncounters.ChanceOnEntry;

    /// <summary>
    /// Last MESS message shown by a message cell <c>[0x64f4]</c> (OV2 <c>0x23c0</c>): the same id is not shown twice in a
    /// row. 0 on entry (<c>0x1dc6</c>) and after a script teleport, ops 12/13 (<c>0x27fa</c>; s49 — not SET-CELL).
    /// </summary>
    public int LastMessageId { get; set; }

    /// <summary>
    /// The live level buffer (a 2500-byte working copy, DOS <c>[0x6504]</c>). Every in-play rewrite — spent events,
    /// one-shot ops, SET-CELL, cleared fight cells — lands here, never in the loaded original. It is what the exit
    /// prompt saves to the one-slot <see cref="DungeonSaveSlot"/> (s36; replaces the old <c>ClearedEventKeys</c>).
    /// Null only for states built by hand in tests; <see cref="DungeonTraversal.Enter"/> always sets it.
    /// </summary>
    public DungeonLevel? Level { get; set; }
}

/// <summary>
/// One dungeon level — Phantasie I / II's 33×38 feature grid (OV2 FUN_0000_20c3 stride 0x21) in a 2500-byte buffer, or
/// (Phase 09 T4) another game's grid of its own size inside its own record (III: 24×30 in a 4224-byte <c>DNGS.DAT</c>
/// record, <c>FORMAT.md</c> §9.5). Cell 0 = impassable wall; any non-zero byte is walkable.
/// </summary>
public sealed class DungeonLevel
{
    /// <summary>Phantasie I / II grid width (the default <see cref="GridWidth"/>).</summary>
    public const int Width = 33;

    /// <summary>Phantasie I / II grid height (the default <see cref="GridHeight"/>).</summary>
    public const int Height = 38;
    public const int GridByteLength = Width * Height; // 1254

    public int LevelId { get; }
    public string FileName { get; }
    public byte[] Cells { get; }
    public IReadOnlyList<string> EmbeddedTexts { get; }

    /// <summary>This level's grid width (<see cref="Width"/> for I / II).</summary>
    public int GridWidth { get; }

    /// <summary>This level's grid height (<see cref="Height"/> for I / II).</summary>
    public int GridHeight { get; }

    /// <summary>This level's whole buffer: grid + side data (<see cref="BufferSize"/> for I / II, 4224 for III).</summary>
    public int BufferLength { get; }

    /// <summary>Side-table bytes after the walkable grid (event/stairs metadata).</summary>
    public byte[] SideData { get; }

    /// <summary>
    /// This level's MESS messages by id (OV2 <c>0x2a95</c>): text lines and menu options. Data, shared by every working
    /// copy (never mutated).
    /// </summary>
    public IReadOnlyDictionary<int, DungeonMessage> Messages { get; init; } = new Dictionary<int, DungeonMessage>();

    public DungeonLevel(
        int levelId,
        string fileName,
        byte[] cells,
        byte[]? sideData = null,
        IReadOnlyList<string>? embeddedTexts = null)
        : this(levelId, fileName, cells, sideData, embeddedTexts, Width, Height, BufferSize)
    {
    }

    /// <summary>A level of any size (Phase 09 T4): <paramref name="cells"/> is <c>width · height</c> bytes.</summary>
    public DungeonLevel(
        int levelId,
        string fileName,
        byte[] cells,
        byte[]? sideData,
        IReadOnlyList<string>? embeddedTexts,
        int width,
        int height,
        int bufferLength)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(width, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(height, 1);
        if (cells.Length != width * height)
            throw new ArgumentException($"Dungeon grid must be {width * height} bytes.", nameof(cells));
        ArgumentOutOfRangeException.ThrowIfLessThan(bufferLength, width * height);

        LevelId = levelId;
        FileName = fileName;
        Cells = (byte[])cells.Clone();
        SideData = sideData is null ? Array.Empty<byte>() : (byte[])sideData.Clone();
        EmbeddedTexts = embeddedTexts ?? Array.Empty<string>();
        GridWidth = width;
        GridHeight = height;
        BufferLength = bufferLength;
    }

    /// <summary>Size of the DOS level buffer <c>[0x6504]</c> = <c>DNGX.SAV</c> (OV2 <c>0x57d8</c>, 0x9c4 bytes) [C].</summary>
    public const int BufferSize = 2500;

    private int CellCount => GridWidth * GridHeight;

    /// <summary>
    /// The level buffer (I / II: 2500 bytes): grid + side tables, zero-padded. DOS leaves stale bytes from an earlier level
    /// past the file end <c>[~]</c> (save-file-format.md); the remaster zero-fills them (no reader of that tail was found).
    /// </summary>
    public byte[] ToBuffer()
    {
        var buffer = new byte[BufferLength];
        Buffer.BlockCopy(Cells, 0, buffer, 0, CellCount);
        Buffer.BlockCopy(SideData, 0, buffer, CellCount, Math.Min(SideData.Length, BufferLength - CellCount));
        return buffer;
    }

    /// <summary>A level built from a 2500-byte buffer (a <c>DNGX.SAV</c> or a working copy); the side data is the full tail.</summary>
    public static DungeonLevel FromBuffer(int levelId, string fileName, ReadOnlySpan<byte> buffer,
        IReadOnlyList<string>? embeddedTexts = null)
    {
        if (buffer.Length != BufferSize)
            throw new ArgumentException($"Level buffer must be {BufferSize} bytes.", nameof(buffer));
        return new DungeonLevel(levelId, fileName, buffer[..GridByteLength].ToArray(),
            buffer[GridByteLength..].ToArray(), embeddedTexts);
    }

    /// <summary>A working copy in buffer form, so play never mutates the loaded original.</summary>
    public DungeonLevel CloneAsBuffer() => WithBuffer(ToBuffer());

    /// <summary>This level's identity, texts and messages over another buffer of its <see cref="BufferLength"/>.</summary>
    public DungeonLevel WithBuffer(ReadOnlySpan<byte> buffer)
    {
        if (buffer.Length != BufferLength)
            throw new ArgumentException($"Level buffer must be {BufferLength} bytes.", nameof(buffer));
        return new DungeonLevel(LevelId, FileName, buffer[..CellCount].ToArray(),
            buffer[CellCount..].ToArray(), EmbeddedTexts, GridWidth, GridHeight, BufferLength) { Messages = Messages };
    }

    /// <summary>Byte at buffer offset <paramref name="offset"/> (grid, then side tables); 0 past the stored tail.</summary>
    public byte GetBufferByte(int offset) =>
        offset < CellCount ? Cells[offset]
        : offset - CellCount < SideData.Length ? SideData[offset - CellCount] : (byte)0;

    /// <summary>Write buffer offset <paramref name="offset"/>; side offsets past the stored tail are ignored.</summary>
    public void SetBufferByte(int offset, byte value)
    {
        if (offset < CellCount)
            Cells[offset] = value;
        else if (offset - CellCount < SideData.Length)
            SideData[offset - CellCount] = value;
    }

    /// <summary>
    /// Where exit cell <paramref name="exitCell"/> (<c>0xd7..0xd9</c>) puts the party (OV2 <c>0x2d46</c> [C]): record
    /// <c>k = b − 0xd7</c> at <c>buf + 0x4f2 + 3k</c> = region, y, 25 − x. Every level's record 0 is the OUT tile that
    /// enters it; DNG7's <c>0xd8</c> uses record 1 = a one-way exit to OUT8 (12,5) (D-038, user s37: DOS). Null for any
    /// other byte. Phantasie I / II's layout only.
    /// </summary>
    public (int RegionId, int X, int Y)? ExitTarget(byte exitCell)
    {
        if (exitCell is < 0xd7 or > 0xd9)
            return null;
        var at = 0x4f2 + 3 * (exitCell - 0xd7);
        return (GetBufferByte(at), 25 - GetBufferByte(at + 2), GetBufferByte(at + 1));
    }

    public byte Get(int x, int y)
    {
        ThrowIfOutOfRange(x, y);
        return Cells[y * GridWidth + x];
    }

    public void Set(int x, int y, byte value)
    {
        ThrowIfOutOfRange(x, y);
        Cells[y * GridWidth + x] = value;
    }

    public bool InBounds(int x, int y) =>
        (uint)x < (uint)GridWidth && (uint)y < (uint)GridHeight;

    public bool IsWalkable(int x, int y) =>
        InBounds(x, y) && Get(x, y) != 0;

    /// <summary>
    /// The cell's kind by I's byte ranges (OV2 <c>FUN_2249</c>). Phantasie II uses the same ranges [C] (ST step dispatch
    /// <c>0x10b60</c>: 8..0x11 wandering, 0x12..0x7f message b − 0x11, 0x80..0x89 fixed fight, 0x8b..0xb8 message
    /// b − 0x76, 0xb9..0xd6 lock (<c>0x111d6</c>: kind b − 0xb8, banks of 15), 0xd7..0xd9 exit, 0xda..0xdc stairs, ≥ 0xdd
    /// event, 2..6 trap; 0, 1, 7 and 0x8a do nothing), so the ranges stay code, shared by both games.
    /// </summary>
    public static DungeonFeatureKind Classify(byte feature) => feature switch
    {
        0 => DungeonFeatureKind.Wall,
        1 or 7 => DungeonFeatureKind.Floor,
        >= 2 and <= 6 => DungeonFeatureKind.Trap,
        >= 8 and <= 0x11 => DungeonFeatureKind.Encounter,
        >= 0x12 and <= 0x7f => DungeonFeatureKind.ScriptLow,
        >= 0x80 and <= 0x89 => DungeonFeatureKind.Combat,
        >= 0x8b and <= 0xb8 => DungeonFeatureKind.Message,
        >= 0xb9 and <= 0xd6 => DungeonFeatureKind.LockOrAmbush,
        >= 0xd7 and <= 0xd9 => DungeonFeatureKind.ExitOrStairs,
        >= 0xda and <= 0xdc => DungeonFeatureKind.StairsMessage,
        >= 0xdd => DungeonFeatureKind.ScriptedEvent,
        _ => DungeonFeatureKind.Other, // 0x8a etc.
    };

    /// <summary>MESS / script id for message tiles: <c>feature - 0x76</c> (FUN_2249).</summary>
    public static int? MessageScriptId(byte feature)
    {
        if (feature is >= 0x8b and <= 0xb8)
            return feature - 0x76;
        if (feature is >= 8 and <= 0x11)
            return feature + 3; // FUN_2249 alternate path
        return null;
    }

    private void ThrowIfOutOfRange(int x, int y)
    {
        if (!InBounds(x, y))
            throw new ArgumentOutOfRangeException($"Cell ({x},{y}) outside {GridWidth}x{GridHeight}.");
    }
}

/// <summary>A MESS message as OV2 <c>0x2a95</c> shows it: its text lines, then a menu when it has options.</summary>
public sealed record DungeonMessage(IReadOnlyList<string> Lines, IReadOnlyList<string> Options)
{
    public bool IsMenu => Options.Count > 0;

    /// <summary>
    /// s153: <see cref="Options"/> as stored (the 11- or 3-character cells, blanks kept); null when the data has none.
    /// Presentation only: I Amiga's buttons print the cell as it is (h41 <c>+0x1440</c> / <c>+0x1f6</c>), so its blanks
    /// place the label.
    /// </summary>
    public IReadOnlyList<string>? OptionCells { get; init; }

    /// <summary>
    /// s41: this slot is not a message start but a later record of message N (its prefix is N's option byte). OV2
    /// <c>0x2a95</c> would still show it as below if asked; no cell, event or script in DNG1..10 asks for one
    /// (<c>extract_dungeon_events.py</c> checks). Example: DNG1 7 → 6.
    /// </summary>
    public int? ContinuationOf { get; init; }
}
