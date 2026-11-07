using System.Globalization;

namespace OpenPhantasie.Data.Audio;

/// <summary>
/// Manifest <c>sound_effects</c> with format <c>atari-st-dosound</c> (Phantasie II Atari ST, s135;
/// <c>reverse-engineering/atari-st/phantasie2/FORMAT.md</c> 7.2) `[C]`: <c>0xb80e(n)</c> silences the YM2149 and passes
/// entry n of the table <c>0x24a8e</c> to XBIOS 32 Dosound, which TOS plays from its 50 Hz interrupt
/// (<see cref="DosoundScore"/>, as the title tune). <see cref="Sounds"/>: per sound number the script's offset in
/// <see cref="FilePath"/> (<c>PHANT.PRG</c>; file offset = image + <c>0x1c</c>). Dosound returns at once: a sound does not
/// hold the game (at Speed normal / slow the next sound waits for it, <c>0xb8f2</c>).
/// </summary>
public sealed record StDosoundEffects(string FilePath, IReadOnlyDictionary<int, int> Sounds, int? ClockHz)
{
    /// <summary>
    /// s136: sounds the second player <c>0xb92a(count, body)</c> builds (1, 9, 27): the body's bytes up to (not including)
    /// its <c>0xff</c>, <see cref="StDosoundRepeat.Count"/> times, then <see cref="StDosoundRepeat.TailLength"/> bytes at
    /// <see cref="StDosoundRepeat.Tail"/> (<c>0x21a32</c>: registers 0–13 := 0, end), all file offsets.
    /// </summary>
    public IReadOnlyDictionary<int, StDosoundRepeat> Repeats { get; init; } = new Dictionary<int, StDosoundRepeat>();

    /// <summary>Whether the pack has sound <paramref name="sound"/> (a table script or a repeat).</summary>
    public bool Has(int sound) => Sounds.ContainsKey(sound) || Repeats.ContainsKey(sound);

    /// <summary>The script <c>0xb92a</c> passes to Dosound for <paramref name="repeat"/> (null when out of the image).</summary>
    public static byte[]? BuildRepeat(byte[] image, StDosoundRepeat repeat)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(repeat);
        var end = Array.IndexOf(image, (byte)0xff, repeat.Body);
        if (repeat.Body >= image.Length || end < 0 || repeat.Tail + repeat.TailLength > image.Length)
            return null;
        var body = image.AsSpan(repeat.Body, end - repeat.Body);
        var script = new byte[body.Length * repeat.Count + repeat.TailLength];
        for (var k = 0; k < repeat.Count; k++)
            body.CopyTo(script.AsSpan(k * body.Length));
        image.AsSpan(repeat.Tail, repeat.TailLength).CopyTo(script.AsSpan(body.Length * repeat.Count));
        return script;
    }

    /// <summary>The program's bytes, or null when the file is missing.</summary>
    public byte[]? TryLoadImage() => File.Exists(FilePath) ? File.ReadAllBytes(FilePath) : null;

    /// <summary>Sound <paramref name="sound"/> as TOS plays it, or null for a number the pack lacks.</summary>
    public DosoundScore? Score(byte[] image, int sound)
    {
        ArgumentNullException.ThrowIfNull(image);
        if (Repeats.TryGetValue(sound, out var repeat))
            return BuildRepeat(image, repeat) is { } script ? DosoundScore.Parse(script) : null;
        return Sounds.TryGetValue(sound, out var offset) && offset < image.Length
            ? DosoundScore.Parse(image, offset)
            : null;
    }

    /// <summary>How long sound <paramref name="sound"/> sounds (its ticks / 50); 0 when unknown.</summary>
    public double Seconds(byte[] image, int sound) => Score(image, sound)?.DurationSeconds ?? 0;

    internal static StDosoundEffects FromDto(P3SoundEffects.Dto dto, string manifestPath, string rootDirectory)
    {
        InvalidDataException Bad(string what) => new($"{manifestPath}: sound_effects: {what}.");
        if (dto.Format != TuneFormats.AtariStDosound || string.IsNullOrEmpty(dto.File))
            throw Bad("unknown format or no file");
        var sounds = new Dictionary<int, int>();
        foreach (var (key, offsets) in dto.Sounds ?? [])
        {
            if (!int.TryParse(key, NumberStyles.None, CultureInfo.InvariantCulture, out var n) || n < 0)
                throw Bad($"sound number '{key}'");
            if (offsets is not [{ } v])
                throw Bad($"sound {n}: one script offset");
            sounds[n] = v.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                        && int.TryParse(v.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var o) && o >= 0
                ? o
                : throw Bad($"sound {n}: bad offset '{v}'");
        }
        var repeats = new Dictionary<int, StDosoundRepeat>();
        foreach (var (key, r) in dto.Repeats ?? [])
        {
            if (!int.TryParse(key, NumberStyles.None, CultureInfo.InvariantCulture, out var n) || n < 0)
                throw Bad($"repeat number '{key}'");
            if (r.Count <= 0 || r.TailLength < 0)
                throw Bad($"repeat {n}: count / tail_length");
            repeats[n] = new StDosoundRepeat(Offset(r.Body, n), r.Count, Offset(r.Tail, n), r.TailLength);
        }
        return new StDosoundEffects(Path.GetFullPath(Path.Combine(rootDirectory, dto.File)), sounds, dto.ClockHz)
        {
            Repeats = repeats,
        };

        int Offset(string? v, int n) =>
            v is not null && v.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            && int.TryParse(v.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var o) && o >= 0
                ? o
                : throw Bad($"repeat {n}: bad offset '{v}'");
    }
}

/// <summary>One sound of II ST's second player <c>0xb92a</c> (s136): file offsets of the body and the tail.</summary>
public sealed record StDosoundRepeat(int Body, int Count, int Tail, int TailLength);
