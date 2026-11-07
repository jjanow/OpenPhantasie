using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using OpenPhantasie.Data.Audio;

namespace OpenPhantasie.Data;

/// <summary>
/// What a game's original showed at start-up, before its own first screen (the title splash, s102): a pack's
/// <c>title_sequence</c>. Each <see cref="TitleStep"/> is one screen on a <see cref="Width"/> × <see cref="Height"/>
/// canvas in <see cref="Background"/>: a picture of the pack at its own size (never scaled or cut), text lines from the
/// game's string table, and how long the original kept it. The Client letterboxes the canvas into the window at an integer
/// scale, like every screen. <see cref="Tune"/>: the tune the original played under the pictures (s108), or null.
/// </summary>
public sealed record TitleSequence(int Width, int Height, string Background, IReadOnlyList<TitleStep> Steps)
{
    private static readonly Regex HexColour = new("^#[0-9A-Fa-f]{6}$");

    /// <summary>The original's title tune, or null when it had none (or none is decoded).</summary>
    public TitleTune? Tune { get; init; }

    internal static TitleSequence FromDto(Dto dto, int? packVblHz, string manifestPath, string rootDirectory)
    {
        InvalidDataException Bad(string what) => new($"{manifestPath}: title_sequence: {what}.");
        var (w, h) = dto.Canvas is [var cw, var ch] ? (cw, ch) : (ScreenSpec.Default.Width, ScreenSpec.Default.Height);
        if (w <= 0 || h <= 0)
            throw Bad("bad canvas");
        var background = dto.Background ?? "#000000";
        if (!HexColour.IsMatch(background))
            throw Bad("background is not #RRGGBB");
        var hz = dto.VblHz ?? packVblHz;
        if (dto.Steps is not { Count: > 0 })
            throw Bad("no steps");
        TitleTune? tune = null;
        if (dto.Tune is { } td)
        {
            if (td.Format is null || !TuneFormats.All.Contains(td.Format) || string.IsNullOrEmpty(td.File))
                throw Bad("tune: unknown format or no file");
            if (td.UntilStep is int u && (u < 1 || u > dto.Steps.Count))
                throw Bad("tune: until_step out of range");
            if (td.Format == TuneFormats.X68kOpmDrv && (string.IsNullOrEmpty(td.Voices) || td.Tracks is not { Count: > 0 }))
                throw Bad("tune: an OPMDRV tune needs voices and tracks");
            var tracks = td.Tracks?.Select(t => (IReadOnlyList<int>)t.Select(a => ParseAddress(a, Bad)).ToList()).ToList();
            tune = new TitleTune(td.Format, Path.GetFullPath(Path.Combine(rootDirectory, td.File)), td.Offset ?? 0, td.Length,
                td.UntilStep, td.VblHz ?? hz, td.ClockHz)
            {
                VoicesPath = td.Voices is { Length: > 0 } v ? Path.GetFullPath(Path.Combine(rootDirectory, v)) : null,
                Tracks = tracks,
                PlaysOn = td.PlaysOn,
            };
            if (td.PlaysOn && td.UntilStep is not null)
                throw Bad("tune: plays_on and until_step exclude each other");
        }
        var steps = new List<TitleStep>();
        foreach (var s in dto.Steps!)
        {
            if (s.Seconds is not null && s.Vbl is not null)
                throw Bad("a step has both seconds and vbl");
            if (s.UntilTuneEnds && (tune is null || s.Seconds is not null || s.Vbl is not null))
                throw Bad("until_tune_ends needs a tune and no other time");
            double? seconds = s.Seconds, endsAt = null;
            // The step lasts until the tune ends, counted from the tune's start with the first picture (I's cover1
            // while tamb.dat plays; s156: II's composite while PHANT.PRG waits at 0xb8f2 for START.PRG's Dosound
            // script); the tune's own length even with the music switched off, as that is the original's timing. The
            // steps before it need fixed times. No tune file → until a key, as before.
            if (s.UntilTuneEnds && tune!.TryLoad()?.DurationSeconds is double length)
            {
                var before = steps.Sum(t => t.Seconds ?? throw Bad("until_tune_ends after a step with no fixed time"));
                if (length <= before)
                    throw Bad("until_tune_ends: the tune ends before the step");
                endsAt = length;
                seconds = length - before;
            }
            if (s.Vbl is int vbl)
                seconds = hz is int rate and > 0 ? (double)vbl / rate : throw Bad("vbl without a vbl_hz");
            if (seconds is <= 0)
                throw Bad("a step's time must be positive");
            var (x, y) = s.At is [var ax, var ay] ? (ax, ay) : (0, 0);
            var texts = new List<TitleText>();
            foreach (var t in s.Texts ?? [])
            {
                var colour = t.Colour ?? "#FFFFFF";
                if (string.IsNullOrWhiteSpace(t.String) || !HexColour.IsMatch(colour))
                    throw Bad("bad text");
                texts.Add(new TitleText(t.String!, t.CentreX ?? w / 2, t.Y, colour));
            }
            // s158: a blank step (the background alone) stands for a load the original did on a black screen with the
            // tune already running (II: COVER1 read on the black palette); it needs a fixed time.
            if (s.Blank && (s.Picture is not null || texts.Count > 0 || seconds is null || s.UntilTuneEnds))
                throw Bad("a blank step has no picture or text and a fixed time");
            if (s.Picture is null && texts.Count == 0 && !s.Blank)
                throw Bad("an empty step");
            if (s.Animation is not null && s.Picture is null)
                throw Bad("an animation needs the step's picture");
            if (s.Menu is not null && (seconds is not null || s.MenuAt is not [_, _]))
                throw Bad("a menu step needs menu_at and waits for its pick (no time)");
            var (mx, my) = s.MenuAt is [var px, var py] ? (px, py) : (0, 0);
            steps.Add(new TitleStep(s.Picture, x, y, texts, seconds)
            {
                Animation = s.Animation, Menu = s.Menu, MenuX = mx, MenuY = my, EndsAt = endsAt,
            });
        }
        return new TitleSequence(w, h, background, steps) { Tune = tune };
    }

    private static int ParseAddress(string a, Func<string, InvalidDataException> bad) =>
        a.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            && int.TryParse(a.AsSpan(2), System.Globalization.NumberStyles.HexNumber, null, out var v) ? v
        : int.TryParse(a, out var d) ? d : throw bad($"tune: bad track address {a}");

    internal sealed class Dto
    {
        public int[]? Canvas { get; set; }
        public string? Background { get; set; }
        [JsonPropertyName("vbl_hz")] public int? VblHz { get; set; }
        public TuneDto? Tune { get; set; }
        public List<StepDto>? Steps { get; set; }
    }

    internal sealed class TuneDto
    {
        public string? Format { get; set; }
        public string? File { get; set; }
        public int? Offset { get; set; }
        public int? Length { get; set; }
        [JsonPropertyName("until_step")] public int? UntilStep { get; set; }
        [JsonPropertyName("vbl_hz")] public int? VblHz { get; set; }
        [JsonPropertyName("clock_hz")] public int? ClockHz { get; set; }
        public string? Voices { get; set; }
        public List<List<string>>? Tracks { get; set; }
        [JsonPropertyName("plays_on")] public bool PlaysOn { get; set; }
    }

    internal sealed class StepDto
    {
        public string? Picture { get; set; }
        public int[]? At { get; set; }
        public double? Seconds { get; set; }
        public int? Vbl { get; set; }
        [JsonPropertyName("until_tune_ends")] public bool UntilTuneEnds { get; set; }
        public bool Blank { get; set; }
        public string? Animation { get; set; }
        public string? Menu { get; set; }
        [JsonPropertyName("menu_at")] public int[]? MenuAt { get; set; }
        public List<TextDto>? Texts { get; set; }
    }

    internal sealed class TextDto
    {
        public string? String { get; set; }
        [JsonPropertyName("centre_x")] public int? CentreX { get; set; }
        public int Y { get; set; }
        public string? Colour { get; set; }
    }
}

/// <summary>
/// One title screen: <see cref="Picture"/> (a pack picture name, or null) drawn 1:1 at (<see cref="X"/>, <see cref="Y"/>),
/// then <see cref="Texts"/>. <see cref="Seconds"/>: how long the original kept it; null = until a key or click (the
/// original waited for one, or how long it stayed is not decoded).
/// </summary>
public sealed record TitleStep(string? Picture, int X, int Y, IReadOnlyList<TitleText> Texts, double? Seconds)
{
    /// <summary>
    /// The pack's <c>picture_animations</c> scene that plays on <see cref="Picture"/> while the step is up (IV's title
    /// torches, s110), its spots relative to the picture's top-left; null = a still picture.
    /// </summary>
    public string? Animation { get; init; }

    /// <summary>
    /// s151: the original's start menu over this step (IV: <c>"disk"</c>, <c>_diskmenu(0)</c>'s New Game / Saved Game panel,
    /// the pack's command panels), its window's top-left (title bar included) at (<see cref="MenuX"/>, <see cref="MenuY"/>)
    /// of the canvas; null = none. A menu step waits for a pick, not for any key.
    /// </summary>
    public string? Menu { get; init; }

    public int MenuX { get; init; }
    public int MenuY { get; init; }

    /// <summary>
    /// s156: an <c>until_tune_ends</c> step ends when this many seconds have passed since the splash began (the tune's
    /// end), however long the steps before it lasted (a key may cut those short); null = <see cref="Seconds"/> from the
    /// step's own start.
    /// </summary>
    public double? EndsAt { get; init; }
}

/// <summary>A line of the game's string table (<see cref="StringId"/>), centred on <see cref="CentreX"/>, top at <see cref="Y"/>.</summary>
public sealed record TitleText(string StringId, int CentreX, int Y, string Colour);

/// <summary>
/// The tune a title splash plays (s108): <see cref="Format"/> (<see cref="TuneFormats"/>) read from <see cref="FilePath"/> (an
/// original file under the pack's root; <see cref="Offset"/> / <see cref="Length"/> cut it out of a program when the tune
/// sits inside one). It starts with the first picture and stops when the splash ends, or when it leaves step
/// <see cref="UntilStep"/> − 1 (I: the tune ends with cover1); a key ends a tune with the step it belongs to.
/// <see cref="VblHz"/>: the frame rate a VBL-driven player runs at (`[~]` per machine); <see cref="ClockHz"/>: the sound
/// chip's clock (Paula 3 579 545 NTSC / 3 546 895 PAL, the ST's PSG 2 000 000), null = the format's default.
/// </summary>
public sealed record TitleTune(string Format, string FilePath, int Offset, int? Length, int? UntilStep, int? VblHz, int? ClockHz)
{
    /// <summary>
    /// <see cref="TuneFormats.X68kOpmDrv"/>: the voice file (<c>P4.SND</c>), or null for the other formats.
    /// </summary>
    public string? VoicesPath { get; init; }

    /// <summary>
    /// <see cref="TuneFormats.X68kOpmDrv"/>: per track (track i plays on OPM channel i − 1), the addresses of its MML
    /// strings in the file after <see cref="Offset"/> (P4.X: image addresses, offset 0x40), in the order the game sends
    /// them. Pack JSON: <c>"tracks": [["0x47ca4", "0x47cef"], …]</c>.
    /// </summary>
    public IReadOnlyList<IReadOnlyList<int>>? Tracks { get; init; }

    /// <summary>
    /// s157 (<c>plays_on</c>): the tune is not stopped when the splash ends but plays on into the game until its own end or
    /// until the game's first sound effect (III Amiga: <c>D/M0</c> runs on its VBL interrupt over the Initial Menu, and
    /// every effect's driver start <c>0x1f52</c> resets all three voices, FORMAT.md 7.1 / 7.2). False: it stops with the
    /// splash (or with step <see cref="UntilStep"/>).
    /// </summary>
    public bool PlaysOn { get; init; }

    /// <summary>Decode the tune; null when its file is missing (the extracts are not on this machine).</summary>
    public TuneScore? TryLoad()
    {
        if (!File.Exists(FilePath))
            return null;
        var all = File.ReadAllBytes(FilePath);
        if (Offset < 0 || Offset > all.Length)
            throw new InvalidDataException($"{FilePath}: tune offset {Offset} past the end.");
        var bytes = all.AsSpan(Offset);
        if (Length is int n)
            bytes = bytes[..Math.Min(n, bytes.Length)];
        return Format switch
        {
            TuneFormats.AmigaDosound => AmigaDosoundScore.Parse(bytes),
            TuneFormats.AtariStDosound => DosoundScore.Parse(bytes),
            TuneFormats.AmigaP3Music => P3MusicScore.Parse(bytes, bytes.Length, VblHz ?? throw new InvalidDataException(
                $"{FilePath}: a VBL-driven tune needs vbl_hz.")),
            TuneFormats.X68kOpmDrv => VoicesPath is { } vp && File.Exists(vp)
                ? OpmDrvScore.Load(bytes, Tracks!, File.ReadAllBytes(vp), ClockHz ?? OpmDrvScore.X68000OpmClockHz)
                : null,
            _ => throw new InvalidDataException($"{FilePath}: unknown tune format {Format}."),
        };
    }
}
