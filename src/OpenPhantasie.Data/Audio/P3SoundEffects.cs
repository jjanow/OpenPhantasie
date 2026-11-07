using System.Globalization;
using System.Text.Json.Serialization;

namespace OpenPhantasie.Data.Audio;

/// <summary>
/// One call of the Phantasie III Amiga driver start <c>0x1f52(v1, v2, v3)</c> at VBL <see cref="Frame"/>: where each of
/// the three voice streams starts in the image the driver reads, or null for a 0 pointer (that voice stays silent).
/// </summary>
public sealed record P3DriverStart(int Frame, int? V1, int? V2, int? V3)
{
    public IReadOnlyList<int?> Voices => [V1, V2, V3];
}

/// <summary>
/// Manifest <c>sound_effects</c> (Phantasie III Amiga; <c>reverse-engineering/amiga/phantasie3/FORMAT.md</c> §7.2):
/// the game's sounds are not samples. <c>0xb126(n)</c> (n &gt; 0) calls the music driver's start
/// <c>0x1f52(t[n], t[n + 1], t[n + 2])</c> with the long pointers of table <c>t</c> = <c>0x23e1a</c> (DATA hunk), so every
/// sound is three voice streams in the driver's format (<see cref="P3MusicScore"/>) and cuts whatever the driver was
/// playing, music included. <see cref="Sounds"/>: per sound number the three stream offsets in <see cref="FilePath"/> (the
/// program file <c>PhantasieIII</c>: DATA flat address + <c>0x6038</c>), null for a 0 pointer; a sound whose three
/// pointers are all 0 (33) is the driver's stop.
/// </summary>
public sealed record P3SoundEffects(string Format, string FilePath, IReadOnlyDictionary<int, IReadOnlyList<int?>> Sounds,
    int VblHz, int? ClockHz)
{
    /// <summary>The program's bytes, or null when the file is missing (the extracts are not on this machine).</summary>
    public byte[]? TryLoadImage() => File.Exists(FilePath) ? File.ReadAllBytes(FilePath) : null;

    /// <summary>True when sound <paramref name="sound"/> is the driver's stop (all three pointers 0).</summary>
    public bool IsStop(int sound) => Sounds.TryGetValue(sound, out var v) && v.All(x => x is null);

    /// <summary>
    /// The driver's output when <paramref name="plays"/> (VBL, sound number; in VBL order) are started one after another
    /// on <paramref name="image"/>: each start cuts the one before. Null when no play names a packed sound.
    /// </summary>
    public P3MusicScore? Score(byte[] image, IEnumerable<(int Frame, int Sound)> plays)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(plays);
        var starts = new List<P3DriverStart>();
        foreach (var (frame, sound) in plays)
            if (Sounds.TryGetValue(sound, out var v))
                starts.Add(new P3DriverStart(frame, v[0], v[1], v[2]));
        return starts.Count == 0 ? null : P3MusicScore.Run(image, starts, VblHz);
    }

    internal static P3SoundEffects FromDto(Dto dto, int? packVblHz, string manifestPath, string rootDirectory)
    {
        InvalidDataException Bad(string what) => new($"{manifestPath}: sound_effects: {what}.");
        if (dto.Format != TuneFormats.AmigaP3Music || string.IsNullOrEmpty(dto.File))
            throw Bad("unknown format or no file");
        if ((dto.VblHz ?? packVblHz) is not int hz || hz <= 0)
            throw Bad("no vbl_hz");
        var sounds = new Dictionary<int, IReadOnlyList<int?>>();
        foreach (var (key, voices) in dto.Sounds ?? [])
        {
            if (!int.TryParse(key, NumberStyles.None, CultureInfo.InvariantCulture, out var n) || n <= 0)
                throw Bad($"sound number '{key}'");
            if (voices is not { Count: 3 })
                throw Bad($"sound {n}: three voices wanted");
            sounds[n] = voices.Select(v => v is null ? (int?)null : ParseOffset(v, n)).ToList();
        }
        return new P3SoundEffects(dto.Format, Path.GetFullPath(Path.Combine(rootDirectory, dto.File)), sounds, hz,
            dto.ClockHz);

        int ParseOffset(string text, int n) =>
            text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            && int.TryParse(text.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var o) && o >= 0
                ? o
                : throw Bad($"sound {n}: bad offset '{text}'");
    }

    internal sealed class Dto
    {
        public string? Format { get; set; }
        public string? File { get; set; }
        [JsonPropertyName("vbl_hz")] public int? VblHz { get; set; }
        [JsonPropertyName("clock_hz")] public int? ClockHz { get; set; }
        [JsonPropertyName("step_overhead_seconds")] public double? StepOverheadSeconds { get; set; }
        [JsonPropertyName("call_overhead_seconds")] public double? CallOverheadSeconds { get; set; }
        [JsonPropertyName("channel_overhead_seconds")] public double? ChannelOverheadSeconds { get; set; }
        public Dictionary<string, List<string?>>? Sounds { get; set; }
        /// <summary>II ST's second player (s136, <see cref="StDosoundEffects"/>): sound → body × count + tail.</summary>
        public Dictionary<string, RepeatDto>? Repeats { get; set; }
        /// <summary>IV X68000 (s152, <see cref="X68kAdpcmEffects"/>): the effect lengths, the IOCS rate, moment → effects.</summary>
        public List<int>? Lengths { get; set; }
        public int? Rate { get; set; }
        public Dictionary<string, List<int>>? Moments { get; set; }
    }

    internal sealed class RepeatDto
    {
        public string? Body { get; set; }
        public int Count { get; set; }
        public string? Tail { get; set; }
        [JsonPropertyName("tail_length")] public int TailLength { get; set; }
    }
}
