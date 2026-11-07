using System.Globalization;

namespace OpenPhantasie.Data.Audio;

/// <summary>
/// Manifest <c>sound_effects</c> with format <c>x68k-adpcm</c> (Phantasie IV X68000, s152;
/// <c>reverse-engineering/x68000/phantasie4/FORMAT.md</c> "Sound effects") `[C]`: <c>_vinit</c> reads <c>VDATA</c> whole
/// and <c>_voice(n)</c> plays effect n through IOCS <c>_ADPCMOUT</c> (the MSM6258, <see cref="Msm6258"/>): from the sum of
/// <see cref="Lengths"/> 0 … n − 1 (table <c>0x4A338</c>), <see cref="Lengths"/>[n] − 1 bytes, at IOCS rate
/// <see cref="Rate"/>, both channels. <see cref="Moments"/>: per moment Core notes (<c>P4Sounds</c>) the effects it plays,
/// taken in turn on each note (<c>_sdwlk</c> alternates 4 and 5 by <c>_wlkflg</c>); a moment not listed is silent.
/// The file is the player's own copy, read at run time (as the title tune's <c>P4.X</c>).
/// </summary>
public sealed record X68kAdpcmEffects(string FilePath, IReadOnlyList<int> Lengths, int Rate,
    IReadOnlyDictionary<string, IReadOnlyList<int>> Moments)
{
    public const string Format = "x68k-adpcm";

    /// <summary>The output rate in Hz (rate 1: 4 MHz / 768 = 5208.3 Hz).</summary>
    public double SampleRateHz => Msm6258.RateHz(Rate);

    /// <summary>The file's bytes, or null when the file is missing.</summary>
    public byte[]? TryLoadImage() => File.Exists(FilePath) ? File.ReadAllBytes(FilePath) : null;

    /// <summary>Where effect <paramref name="effect"/> starts in the file: the lengths of the effects before it.</summary>
    public int Start(int effect) => Lengths.Take(effect).Sum();

    /// <summary>The bytes <c>_voice(effect)</c> hands to <c>_ADPCMOUT</c> (length − 1 of them), or null when out of range.</summary>
    public ReadOnlyMemory<byte>? Bytes(byte[] image, int effect)
    {
        ArgumentNullException.ThrowIfNull(image);
        if (effect < 0 || effect >= Lengths.Count)
            return null;
        var start = Start(effect);
        var count = Lengths[effect] - 1;
        return count <= 0 || start + count > image.Length ? null : image.AsMemory(start, count);
    }

    /// <summary>Effect <paramref name="effect"/> decoded (12-bit samples at <see cref="SampleRateHz"/>), or null.</summary>
    public short[]? Samples(byte[] image, int effect) =>
        Bytes(image, effect) is { } bytes ? Msm6258.Decode(bytes.Span) : null;

    /// <summary>The effects listed for <paramref name="moment"/> (empty when the moment is silent).</summary>
    public IReadOnlyList<int> EffectsFor(string moment) =>
        Moments.TryGetValue(moment, out var effects) ? effects : [];

    internal static X68kAdpcmEffects FromDto(P3SoundEffects.Dto dto, string manifestPath, string rootDirectory)
    {
        InvalidDataException Bad(string what) => new($"{manifestPath}: sound_effects: {what}.");
        if (dto.Format != Format || string.IsNullOrEmpty(dto.File))
            throw Bad("unknown format or no file");
        if (dto.Lengths is not { Count: > 0 } lengths || lengths.Any(l => l < 1))
            throw Bad("lengths: one positive length per effect");
        var rate = dto.Rate ?? 1;
        if (rate is < 0 or > 4)
            throw Bad($"rate {rate} (0-4)");
        var moments = new Dictionary<string, IReadOnlyList<int>>(StringComparer.Ordinal);
        foreach (var (moment, effects) in dto.Moments ?? [])
        {
            if (string.IsNullOrWhiteSpace(moment) || effects is not { Count: > 0 }
                || effects.Any(e => e < 0 || e >= lengths.Count))
                throw Bad($"moment '{moment}': effects 0-{(lengths.Count - 1).ToString(CultureInfo.InvariantCulture)}");
            moments[moment] = [.. effects];
        }
        return new X68kAdpcmEffects(Path.GetFullPath(Path.Combine(rootDirectory, dto.File)), [.. lengths], rate, moments);
    }
}
