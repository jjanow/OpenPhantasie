using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenPhantasie.Data;

/// <summary>
/// s154: <c>GameData/&lt;game&gt;/dos-screens.json</c>, the DOS presentation the remaster follows with AMIGA SCREENS off,
/// whatever graphics pack is drawn (I: <c>message-and-text.md</c> "Text boxes, key waits and timed lines", <c>endgame.md</c>
/// 4 "Timing"). <see cref="MapBoxes"/>: the lines DOS prints in its centred band, each waiting for a key; <see cref="Defeat"/>:
/// the party defeat's timed lines. Null parts: the file does not say (the front end keeps the remaster's behaviour).
/// </summary>
public sealed record DosScreens
{
    public const string FileName = "dos-screens.json";

    /// <summary>The centred band's string ids (OV2 <c>0xda83</c> / <c>0xdad1</c>), or null.</summary>
    public DosMapBoxes? MapBoxes { get; init; }

    /// <summary>The party defeat's line timing (OV1 <c>0x24c</c>), or null.</summary>
    public DosDefeatTiming? Defeat { get; init; }

    /// <summary>s156: where DOS draws the map's boxes and windows on its 40 × 25 text grid, or null.</summary>
    public DosPagePlaces? Pages { get; init; }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>The file in <paramref name="gameFolder"/>, or null when there is none.</summary>
    public static DosScreens? TryLoad(string gameFolder)
    {
        var path = Path.Combine(gameFolder, FileName);
        return File.Exists(path) ? Parse(File.ReadAllText(path), path) : null;
    }

    /// <summary>Strict: a bad list or a timing ≤ 0 throws <see cref="InvalidDataException"/>.</summary>
    public static DosScreens Parse(string json, string source = FileName)
    {
        Dto dto;
        try
        {
            dto = JsonSerializer.Deserialize<Dto>(json, JsonOptions) ?? throw new InvalidDataException($"{source}: empty.");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"{source}: {ex.Message}", ex);
        }
        InvalidDataException Bad(string what) => new($"{source}: {what}.");
        DosMapBoxes? boxes = null;
        if (dto.MapBoxes is { } m)
        {
            var keys = m.Keys ?? [];
            var dungeonKeys = m.DungeonKeys ?? [];
            if (keys.Concat(dungeonKeys).Any(string.IsNullOrWhiteSpace))
                throw Bad("map_boxes: keys and dungeon_keys must list string ids");
            var splits = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
            foreach (var (key, rows) in m.Splits ?? [])
            {
                if (!(keys.Contains(key) || dungeonKeys.Contains(key)) || rows is not { Count: >= 2 and <= 3 }
                    || rows.Any(string.IsNullOrWhiteSpace))
                    throw Bad($"map_boxes.splits: '{key}' must be one of the keys with 2 or 3 row ids");
                splits[key] = [.. rows];
            }
            boxes = new DosMapBoxes([.. keys], [.. dungeonKeys]) { Splits = splits };
        }
        DosDefeatTiming? defeat = null;
        if (dto.Defeat is { } d)
        {
            if (d.LineHundredths is not > 0)
                throw Bad("defeat.line_hundredths must be > 0");
            defeat = new DosDefeatTiming(d.LineHundredths.Value / 100.0, d.KeyEndsLine ?? false);
        }
        DosPagePlaces? pages = null;
        if (dto.Pages is { } p)
        {
            static bool Cell(List<int>? c) => c is [>= 0 and < 40, >= 0 and < 25];
            static bool Span(List<int>? c, int max) => c is [var a, var b] && a >= 0 && a < b && b < max;
            if (p.BandTopRow is not (>= 0 and < 23) || p.TextTopRow is not (>= 0 and < 23))
                throw Bad("pages: band_top_row and text_top_row must be rows 0..22");
            var reports = new Dictionary<string, DosReportWindow>(StringComparer.Ordinal);
            foreach (var (id, w) in p.Reports ?? [])
            {
                if (string.IsNullOrWhiteSpace(id) || !Span(w.Columns, 40) || !Span(w.Rows, 25) || !Cell(w.Title) || !Cell(w.First))
                    throw Bad($"pages.reports: '{id}' needs columns / rows [first, last] and title / first [column, row]");
                reports[id] = new DosReportWindow(w.Columns![0], w.Columns[1], w.Rows![0], w.Rows[1], (w.Title![0], w.Title[1]),
                    (w.First![0], w.First[1]));
            }
            pages = new DosPagePlaces(p.BandTopRow.Value, p.TextTopRow.Value) { Reports = reports };
        }
        return new DosScreens { MapBoxes = boxes, Defeat = defeat, Pages = pages };
    }

    private sealed class Dto
    {
        [JsonPropertyName("map_boxes")] public BoxesDto? MapBoxes { get; set; }
        [JsonPropertyName("defeat")] public DefeatDto? Defeat { get; set; }
        [JsonPropertyName("pages")] public PagesDto? Pages { get; set; }
    }

    private sealed class PagesDto
    {
        [JsonPropertyName("band_top_row")] public int? BandTopRow { get; set; }
        [JsonPropertyName("text_top_row")] public int? TextTopRow { get; set; }
        [JsonPropertyName("reports")] public Dictionary<string, ReportDto>? Reports { get; set; }
    }

    private sealed class ReportDto
    {
        [JsonPropertyName("columns")] public List<int>? Columns { get; set; }
        [JsonPropertyName("rows")] public List<int>? Rows { get; set; }
        [JsonPropertyName("title")] public List<int>? Title { get; set; }
        [JsonPropertyName("first")] public List<int>? First { get; set; }
    }

    private sealed class BoxesDto
    {
        [JsonPropertyName("keys")] public List<string>? Keys { get; set; }
        [JsonPropertyName("dungeon_keys")] public List<string>? DungeonKeys { get; set; }
        [JsonPropertyName("splits")] public Dictionary<string, List<string>>? Splits { get; set; }
    }

    private sealed class DefeatDto
    {
        [JsonPropertyName("line_hundredths")] public int? LineHundredths { get; set; }
        [JsonPropertyName("key_ends_line")] public bool? KeyEndsLine { get; set; }
    }
}

/// <summary>s154: the string ids DOS prints in its centred band (a key closes it), on the overworld and in the dungeon.</summary>
public sealed record DosMapBoxes(IReadOnlyList<string> Keys, IReadOnlyList<string> DungeonKeys)
{
    /// <summary>A string id → the ids of its rows when DOS shows it on several rows (<c>0xdad1</c>).</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> Splits { get; init; } =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);

    /// <summary>The overworld's keys, or (<paramref name="dungeon"/>) the dungeon's.</summary>
    public IReadOnlyList<string> KeysFor(bool dungeon) => dungeon ? DungeonKeys : Keys;
}

/// <summary>s154: each defeat line holds <paramref name="LineSeconds"/>; <paramref name="KeyEndsLine"/>: a key ends that wait.</summary>
public sealed record DosDefeatTiming(double LineSeconds, bool KeyEndsLine);

/// <summary>
/// s156: DOS's places for the map's boxes on its 40 × 25 text grid (8 × 8 cells). A band (COM <c>0x2e5f</c> / <c>0x2ead</c>
/// / <c>0x2f1d</c>) is full width from <see cref="BandTopRow"/>: its rows centred on the rows under it, a ruled row above
/// and below. A MESS text window (<c>0x2a95</c>) is the same from <see cref="TextTopRow"/>, its 40-character records from
/// column 0. A damage or pool / inn report is a framed window (<see cref="Reports"/>, by its title's string id).
/// </summary>
public sealed record DosPagePlaces(int BandTopRow, int TextTopRow)
{
    /// <summary>Report windows by their title's string id.</summary>
    public IReadOnlyDictionary<string, DosReportWindow> Reports { get; init; } =
        new Dictionary<string, DosReportWindow>(StringComparer.Ordinal);
}

/// <summary>
/// s156: a framed DOS window (OV2 <c>0xb9d1</c>(column, row, last column, last row)): its title at <paramref name="Title"/>,
/// the member rows from <paramref name="First"/> down, one a row.
/// </summary>
public sealed record DosReportWindow(int Left, int Right, int Top, int Bottom, (int Column, int Row) Title, (int Column, int Row) First);
