using System.Text.Json.Serialization;
using OpenPhantasie.Core;

namespace OpenPhantasie.Data;

/// <summary>
/// Which frame sheet and records a picture arms (one <c>_setanim</c> call site): the sheet picture, the picture origin's
/// offset from the scene's top-left in pack pixels, the records first..last, and the real length of one <c>_anima</c>
/// pass in the input loop that shows it (<see cref="PassMilliseconds"/>; null = the set's default).
/// </summary>
public sealed record PictureAnimationScene(string Sheet, int X, int Y, int First, int Last, double? PassMilliseconds = null);

/// <summary>An open-door picture (IV's <c>_animdoor</c> table <c>0x470FA</c>): its place on the street and on the sheet.</summary>
public sealed record PictureDoor(int X, int Y, int SheetX, int SheetY);

/// <summary>
/// A pack's picture animations (s109, IV's <c>_setanim</c> / <c>_anima</c> tables, <c>FORMAT.md</c> §6.10) [C]:
/// <see cref="Table"/> drives <see cref="PictureAnimator"/>; <see cref="PassMilliseconds"/> is the real length of one
/// <c>_anima</c> pass for the scenes without their own (<see cref="PassMillisecondsFor"/>), null while it is <c>[?]</c>
/// (then those are not played). A pass is CPU-bound, so its length differs per input loop; measured in XEiJ (s110).
/// <see cref="DoorHoldMilliseconds"/>: how long <c>_animdoor</c>'s open door stays before the building (null = <c>[?]</c>).
/// </summary>
public sealed record PictureAnimationSet(
    PictureAnimationTable Table,
    IReadOnlyDictionary<string, PictureAnimationScene> Scenes,
    double? PassMilliseconds,
    int DoorWidth,
    int DoorHeight,
    IReadOnlyList<IReadOnlyList<PictureDoor?>> DoorsByTownType)
{
    /// <summary>The open door's hold (<c>_animdoor</c>'s busy wait), or null when unknown.</summary>
    public double? DoorHoldMilliseconds { get; init; }

    /// <summary>s151: <c>_tload</c>'s "Welcome!" before each street load (null = not in the pack).</summary>
    public PictureWelcome? Welcome { get; init; }

    /// <summary>The pass length scene <paramref name="scene"/> plays at: its own, else the set's default (null = not played).</summary>
    public double? PassMillisecondsFor(PictureAnimationScene scene) => scene.PassMilliseconds ?? PassMilliseconds;

    internal static PictureAnimationSet FromDto(Dto dto, IReadOnlyDictionary<string, string> pictures, string manifestPath)
    {
        InvalidDataException Bad(string what) => new($"{manifestPath}: bad picture_animations ({what}).");

        var frames = new List<PictureAnimationFrame>();
        foreach (var f in dto.Frames ?? [])
        {
            if (f is not [var sx, var sy, var b, var r] || sx < 0 || sy < 0 || b < 0 || r < 0)
                throw Bad($"frame {frames.Count}");
            frames.Add(new PictureAnimationFrame(sx, sy, b, r));
        }
        var records = new List<PictureAnimationRecord>();
        foreach (var r in dto.Records ?? [])
        {
            if (r.At is not [var x, var y] || r.Size is not [var w, var h] || w <= 0 || h <= 0 ||
                r.Frames is not { Count: > 0 } ids || ids.Any(i => i < 0 || i >= frames.Count))
                throw Bad($"record {records.Count}");
            records.Add(new PictureAnimationRecord(x, y, w, h, [.. ids]));
        }
        if (records.Count > PictureAnimator.Slots)
            throw Bad("more records than _anima scans");
        var scenes = new Dictionary<string, PictureAnimationScene>(StringComparer.Ordinal);
        foreach (var (name, s) in dto.Scenes ?? [])
        {
            if (s.Sheet is not { } sheet || !pictures.ContainsKey(sheet) || s.At is not [var x, var y] ||
                s.Records is not [var first, var last] || first < 0 || last < first || last >= records.Count ||
                s.PassMs is <= 0)
                throw Bad($"scene '{name}'");
            scenes[name] = new PictureAnimationScene(sheet, x, y, first, last, s.PassMs);
        }
        if (dto.PassMs is <= 0)
            throw Bad("pass_ms");
        if (dto.Doors?.HoldMs is <= 0)
            throw Bad("doors.hold_ms");
        var doors = new List<IReadOnlyList<PictureDoor?>>();
        foreach (var row in dto.Doors?.ByTownType ?? [])
            doors.Add([.. row.Select(d => d is [var x, var y, var sx, var sy] ? new PictureDoor(x, y, sx, sy) : null)]);
        int dw = dto.Doors?.Size is [var dw0, _] ? dw0 : 0, dh = dto.Doors?.Size is [_, var dh0] ? dh0 : 0;
        PictureWelcome? welcome = null;
        if (dto.Welcome is { } wd)
        {
            if (wd.At is not [var wx, var wy] || wd.HoldMs is not > 0 || wd.CoverMs is < 0 || wd.Back is < 0 or > 15
                || wd.Colour is < 0 or > 15)
                throw Bad("welcome");
            PicturePleaseWait? wait = null;
            if (wd.PleaseWait is { } p)
            {
                if (p.At is not [var px, var py] || p.Box is not [var bx, var by, var bw, var bh] || p.Frame is not [var dark, var light]
                    || p.HoldMs is not > 0 || p.HoldNoSheetMs is not > 0)
                    throw Bad("welcome.please_wait");
                wait = new PicturePleaseWait(px, py, p.Back, p.Colour, bx, by, bw, bh, dark, light, p.HoldMs.Value,
                    p.HoldNoSheetMs.Value);
            }
            welcome = new PictureWelcome(wx, wy, wd.Back, wd.Colour, wd.HoldMs.Value, wd.CoverMs ?? 0) { PleaseWait = wait };
        }
        return new PictureAnimationSet(new PictureAnimationTable(frames, records), scenes, dto.PassMs, dw, dh, doors)
        {
            DoorHoldMilliseconds = dto.Doors?.HoldMs,
            Welcome = welcome,
        };
    }

    internal sealed class Dto
    {
        [JsonPropertyName("pass_ms")] public double? PassMs { get; set; }
        public List<int[]>? Frames { get; set; }
        public List<RecordDto>? Records { get; set; }
        public Dictionary<string, SceneDto>? Scenes { get; set; }
        public DoorsDto? Doors { get; set; }
        public WelcomeDto? Welcome { get; set; }
    }

    internal sealed class WelcomeDto
    {
        public int[]? At { get; set; }
        public int Back { get; set; }
        public int Colour { get; set; }
        [JsonPropertyName("hold_ms")] public double? HoldMs { get; set; }
        [JsonPropertyName("cover_ms")] public double? CoverMs { get; set; }
        [JsonPropertyName("please_wait")] public PleaseWaitDto? PleaseWait { get; set; }
    }

    internal sealed class PleaseWaitDto
    {
        public int[]? At { get; set; }
        public int Back { get; set; }
        public int Colour { get; set; }
        public int[]? Box { get; set; }
        public int[]? Frame { get; set; }
        [JsonPropertyName("hold_ms")] public double? HoldMs { get; set; }
        [JsonPropertyName("hold_no_sheet_ms")] public double? HoldNoSheetMs { get; set; }
    }

    internal sealed class RecordDto
    {
        public int[]? At { get; set; }
        public int[]? Size { get; set; }
        public List<int>? Frames { get; set; }
    }

    internal sealed class SceneDto
    {
        public string? Sheet { get; set; }
        public int[]? At { get; set; }
        public int[]? Records { get; set; }
        [JsonPropertyName("pass_ms")] public double? PassMs { get; set; }
    }

    internal sealed class DoorsDto
    {
        public int[]? Size { get; set; }
        [JsonPropertyName("hold_ms")] public double? HoldMs { get; set; }
        [JsonPropertyName("by_town_type")] public List<List<int[]?>>? ByTownType { get; set; }
    }
}

/// <summary>
/// s151: IV's "Welcome!" (<c>_tload</c>, FORMAT.md 6.10): the street window cleared to palette colour <see cref="Back"/>,
/// the text in font 0 italic, colour <see cref="Colour"/>, centred on (<see cref="X"/>, <see cref="Y"/>) of the window;
/// it stays <see cref="HoldMilliseconds"/> while the street picture is read and decoded, then the picture's copy
/// (<see cref="CoverMilliseconds"/>) covers it. Measured in XEiJ `[~]` (its floppy reads are faster than a drive's).
/// </summary>
public sealed record PictureWelcome(int X, int Y, int Back, int Colour, double HoldMilliseconds, double CoverMilliseconds)
{
    /// <summary>s156: the "Please Wait..." before it (null = not in the pack).</summary>
    public PicturePleaseWait? PleaseWait { get; init; }
}

/// <summary>
/// s156: <c>_tload</c>'s "Please Wait..." (FORMAT.md 6.10) before "Welcome!": the street window in colour
/// <see cref="Back"/>, <c>_whole</c>'s sunken box (<see cref="BoxX"/>, <see cref="BoxY"/>, <see cref="BoxWidth"/> ×
/// <see cref="BoxHeight"/>; 2 pixels of <see cref="Dark"/> above / left and <see cref="Light"/> below / right, outside it)
/// and the text in font 0, colour <see cref="Colour"/>, centred on (<see cref="X"/>, <see cref="Y"/>). It stays
/// <see cref="HoldMilliseconds"/> while the town's animation sheet loads, <see cref="HoldNoSheetMilliseconds"/> when no
/// sheet is loaded (the ending) `[~]`. Measured in XEiJ.
/// </summary>
public sealed record PicturePleaseWait(int X, int Y, int Back, int Colour, int BoxX, int BoxY, int BoxWidth, int BoxHeight,
    int Dark, int Light, double HoldMilliseconds, double HoldNoSheetMilliseconds);
