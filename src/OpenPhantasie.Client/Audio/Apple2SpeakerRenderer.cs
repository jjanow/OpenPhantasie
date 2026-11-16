using OpenPhantasie.Data;

namespace OpenPhantasie.Client.Audio;

/// <summary>
/// s224: an Apple II speaker sound (<see cref="Apple2SpeakerSound"/>, the game's <c>CALL 770</c> routine): the speaker cone
/// flips between two levels at each click (<see cref="Apple2Speaker.Clicks"/>); each output sample is the level averaged
/// over its span (a box filter, so clicks faster than the output rate do not alias), then a DC block (the speaker's
/// coupling: a held level dies away) `[~]`. Mono on both channels.
/// </summary>
public sealed class Apple2SpeakerRenderer : TuneRenderer
{
    /// <summary>Output level of the cone's swing (the remaster's mix level `[~]`).</summary>
    public const float Gain = 0.35f;

    private readonly double[] _clicks;
    private int _next;
    private double _level = -1;
    private double _dcIn = -1, _dcOut;

    public Apple2SpeakerRenderer(Apple2SpeakerSound sound, double clockHz, int sampleRate = DefaultSampleRate)
        : base(sampleRate)
    {
        ArgumentNullException.ThrowIfNull(sound);
        _clicks = [.. Apple2Speaker.ClickSeconds(sound, clockHz).Order()];
        var end = _clicks.Length > 0 ? _clicks[^1] : 0;
        Length = (long)Math.Ceiling((end + 0.05) * sampleRate);
    }

    public override long Length { get; }

    protected override void RenderCore(Span<float> output, int frames)
    {
        var dt = 1.0 / SampleRate;
        for (var i = 0; i < frames; i++)
        {
            var t0 = (Position + i) * dt;
            var t1 = t0 + dt;
            var sum = 0.0;
            var at = t0;
            while (_next < _clicks.Length && _clicks[_next] < t1)
            {
                var c = Math.Max(_clicks[_next], t0);
                sum += _level * (c - at);
                at = c;
                _level = -_level;
                _next++;
            }
            sum += _level * (t1 - at);
            var x = sum / dt;
            // DC block: y = x - x1 + R * y1.
            var y = x - _dcIn + 0.995 * _dcOut;
            _dcIn = x;
            _dcOut = y;
            var v = (float)(y * Gain);
            output[2 * i] = v;
            output[2 * i + 1] = v;
        }
    }
}
