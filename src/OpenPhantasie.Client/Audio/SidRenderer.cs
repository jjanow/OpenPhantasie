using OpenPhantasie.Data.Audio;

namespace OpenPhantasie.Client.Audio;

/// <summary>
/// s226: a C64 screen's SID writes (<see cref="SidScore"/>) on one <see cref="Sid6581"/>: each write lands on its own
/// cycle (<c>round(seconds · clock)</c>), so writes a few cycles apart stay apart; mono to both channels.
/// </summary>
public sealed class SidRenderer : TuneRenderer
{
    private readonly SidScore _score;
    private readonly double _clockHz;
    private long _cycle;
    private int _next;

    public SidRenderer(SidScore score, double clockHz = Sid6581.PalClockHz, int sampleRate = DefaultSampleRate) : base(sampleRate)
    {
        ArgumentNullException.ThrowIfNull(score);
        _score = score;
        _clockHz = clockHz;
        Chip = new Sid6581(clockHz, sampleRate);
        Length = (long)Math.Ceiling(score.Seconds * sampleRate);
    }

    public override long Length { get; }

    /// <summary>The chip (tests).</summary>
    public Sid6581 Chip { get; }

    private long CycleOf(double seconds) => (long)Math.Round(seconds * _clockHz);

    protected override void RenderCore(Span<float> output, int frames)
    {
        for (var i = 0; i < frames; i++)
        {
            var end = (long)Math.Round((double)(Position + i + 1) * _clockHz / SampleRate);
            while (_next < _score.Writes.Count && CycleOf(_score.Writes[_next].Seconds) < end)
            {
                var w = _score.Writes[_next++];
                var at = Math.Max(_cycle, CycleOf(w.Seconds));
                Chip.Clock((int)(at - _cycle));
                _cycle = at;
                Chip.Write(w.Register, w.Value);
            }
            Chip.Clock((int)(end - _cycle));
            _cycle = end;
            var s = Chip.TakeSample();
            output[2 * i] = s;
            output[2 * i + 1] = s;
        }
    }
}
