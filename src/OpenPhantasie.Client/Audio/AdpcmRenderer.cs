using OpenPhantasie.Data.Audio;

namespace OpenPhantasie.Client.Audio;

/// <summary>
/// s152: Phantasie IV X68000's ADPCM effects (<see cref="X68kAdpcmEffects"/>, decoded by <see cref="Msm6258"/>) one after
/// another, at the output rate: each source sample is held until the next (the DAC's hold), the same value on both channels
/// (<c>_voice</c>'s pan 3). MonoGame cannot play the chip's 5.2 kHz directly (its streams start at 8 kHz), hence the
/// resampling. s163: the held samples go through the X68000's analogue path after DAOUT (<see cref="X68000AdpcmFilter"/>,
/// from Sharp's sheet) at the output rate.
/// </summary>
public sealed class AdpcmRenderer : TuneRenderer
{
    /// <summary>Output level of a full-scale 12-bit sample (the remaster's mix level `[~]`).</summary>
    public const float Gain = 0.6f;

    private readonly short[] _samples;
    private readonly double _step;
    private readonly X68000AdpcmFilter? _filter;

    /// <param name="outputFilter">s163: the X68000's analogue path (<see cref="X68000AdpcmFilter"/>); off only for tests
    /// that compare against the bare held samples.</param>
    public AdpcmRenderer(IEnumerable<short[]> effects, double sourceHz, int sampleRate = DefaultSampleRate,
        bool outputFilter = true)
        : base(sampleRate)
    {
        if (outputFilter)
            _filter = new X68000AdpcmFilter(sampleRate);
        ArgumentNullException.ThrowIfNull(effects);
        if (sourceHz <= 0)
            throw new ArgumentOutOfRangeException(nameof(sourceHz));
        _samples = [.. effects.SelectMany(e => e)];
        _step = sourceHz / sampleRate;
        Length = (long)Math.Ceiling(_samples.Length / _step);
    }

    public override long Length { get; }

    protected override void RenderCore(Span<float> output, int frames)
    {
        for (var i = 0; i < frames; i++)
        {
            var src = (long)((Position + i) * _step);
            var v = src < _samples.Length ? _samples[src] / 2048f * Gain : 0f;
            if (_filter is not null)
                v = (float)_filter.Next(v);
            output[2 * i] = v;
            output[2 * i + 1] = v;
        }
    }

    /// <summary>The renderer for <paramref name="effects"/> of <paramref name="pack"/> in turn (unknown numbers skipped), or
    /// null when none decodes.</summary>
    public static AdpcmRenderer? For(X68kAdpcmEffects pack, byte[] image, IEnumerable<int> effects,
        int sampleRate = DefaultSampleRate, bool outputFilter = true)
    {
        ArgumentNullException.ThrowIfNull(pack);
        var decoded = effects.Select(e => pack.Samples(image, e)).OfType<short[]>().ToList();
        return decoded.Count == 0 ? null : new AdpcmRenderer(decoded, pack.SampleRateHz, sampleRate, outputFilter);
    }
}
