using OpenPhantasie.Data;
using OpenPhantasie.Data.Audio;

namespace OpenPhantasie.Client.Audio;

/// <summary>
/// A decoded tune played on its chip model, streamed as stereo float PCM (s108). <see cref="Render"/> fills the next
/// frames and returns how many it wrote (fewer once the tune is over). No MonoGame types: tests render to WAV.
/// </summary>
public abstract class TuneRenderer
{
    public const int DefaultSampleRate = 44100;

    /// <summary>The NTSC Amiga's Paula clock (the default for the Amiga formats; PAL is 3 546 895).</summary>
    public const int PaulaNtscHz = 3_579_545;

    /// <summary>The Atari ST's YM2149 clock.</summary>
    public const int StPsgHz = 2_000_000;

    protected TuneRenderer(int sampleRate) => SampleRate = sampleRate;

    public int SampleRate { get; }

    /// <summary>Frames rendered so far.</summary>
    public long Position { get; protected set; }

    /// <summary>The tune's length in frames (its score's end).</summary>
    public abstract long Length { get; }

    public bool Finished => Position >= Length;

    /// <summary>Fill up to <paramref name="frames"/> stereo frames (interleaved) and return how many were written.</summary>
    public int Render(Span<float> output, int frames)
    {
        var n = (int)Math.Max(0, Math.Min(frames, Length - Position));
        if (n > 0)
            RenderCore(output[..(2 * n)], n);
        Position += n;
        return n;
    }

    protected abstract void RenderCore(Span<float> output, int frames);

    /// <summary>The renderer for a pack's title tune, or null when its file is missing.</summary>
    /// <param name="a500Filter">s160: the Paula tunes (I, III) through the A500's output filters (the game's AMIGA AUDIO
    /// FILTER switch, <see cref="PaulaMixer"/>); the other chips ignore it.</param>
    public static TuneRenderer? For(TitleTune tune, int sampleRate = DefaultSampleRate, bool a500Filter = true) =>
        tune.TryLoad() switch
        {
            null => null,
            AmigaDosoundScore s => new AmigaDosoundRenderer(s, tune.ClockHz ?? PaulaNtscHz, sampleRate, a500Filter: a500Filter),
            DosoundScore s => new DosoundRenderer(s, tune.ClockHz ?? StPsgHz, sampleRate),
            P3MusicScore s => new P3MusicRenderer(s, tune.ClockHz ?? PaulaNtscHz, sampleRate, a500Filter),
            OpmDrvScore s => new OpmDrvRenderer(s, sampleRate),
            var s => throw new InvalidDataException($"No renderer for {s.GetType().Name}."),
        };

    /// <summary>Render a whole tune (tests, WAV dumps).</summary>
    public float[] RenderAll()
    {
        var buf = new float[2 * Length];
        var at = 0;
        while (!Finished)
        {
            var n = Render(buf.AsSpan(2 * at), (int)Math.Min(4096, Length - Position));
            at += n;
        }
        return buf;
    }

    /// <summary>16-bit PCM WAV of <paramref name="stereo"/> (tests and the verification dumps).</summary>
    public static byte[] Wav(ReadOnlySpan<float> stereo, int sampleRate)
    {
        var pcm = new byte[stereo.Length * 2];
        for (var i = 0; i < stereo.Length; i++)
        {
            var v = (short)Math.Clamp((int)Math.Round(stereo[i] * 32767f), short.MinValue, short.MaxValue);
            pcm[2 * i] = (byte)v;
            pcm[2 * i + 1] = (byte)(v >> 8);
        }
        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);
        w.Write("RIFF"u8);
        w.Write(36 + pcm.Length);
        w.Write("WAVEfmt "u8);
        w.Write(16);
        w.Write((short)1);
        w.Write((short)2);
        w.Write(sampleRate);
        w.Write(sampleRate * 4);
        w.Write((short)4);
        w.Write((short)16);
        w.Write("data"u8);
        w.Write(pcm.Length);
        w.Write(pcm);
        w.Flush();
        return ms.ToArray();
    }
}

/// <summary>
/// Phantasie I's <c>tamb.dat</c> on Paula (<see cref="AmigaDosoundScore"/>): each step queues the 16-byte ramp on the
/// channels it sends (audio.device starts a write when the channel's previous one finishes its cycle), waits its ticks
/// of 1/50 s, and every sent channel is told to finish.
/// </summary>
public sealed class AmigaDosoundRenderer : TuneRenderer
{
    private readonly AmigaDosoundScore _score;
    private readonly PaulaMixer _paula;
    private readonly sbyte[] _wave = AmigaDosoundScore.Waveform();
    private readonly long[] _stepStart;
    private int _next;

    /// <param name="stepOverheadSeconds">What each step costs beyond its ticks (I's sound effects, s130:
    /// <see cref="AmigaDosoundEffects.StepOverheadSeconds"/>; the title tune 0).</param>
    /// <param name="channelOverheadSeconds">What each channel a step sends adds to it (s153:
    /// <see cref="AmigaDosoundEffects.ChannelOverheadSeconds"/>; the title tune 0).</param>
    /// <param name="a500Filter">s160: Paula's output through the A500's filters (AMIGA AUDIO FILTER, on by default).</param>
    public AmigaDosoundRenderer(AmigaDosoundScore score, double clockHz, int sampleRate, double stepOverheadSeconds = 0,
        double channelOverheadSeconds = 0, bool a500Filter = true)
        : base(sampleRate)
    {
        _score = score;
        _paula = new PaulaMixer(clockHz, sampleRate, a500Filter);
        _stepStart = new long[score.Steps.Count + 1];
        var seconds = 0.0;
        for (var i = 0; i <= score.Steps.Count; i++)
        {
            _stepStart[i] = (long)Math.Round(seconds * sampleRate);
            if (i < score.Steps.Count)
                seconds += (double)score.Steps[i].Ticks / AmigaDosoundScore.TickHz + stepOverheadSeconds
                    + score.Steps[i].Channels.Count(c => c.Sent) * channelOverheadSeconds;
        }
        Length = _stepStart[^1];
    }

    public override long Length { get; }

    protected override void RenderCore(Span<float> output, int frames)
    {
        var done = 0;
        while (done < frames)
        {
            var pos = Position + done;
            while (_next <= _score.Steps.Count - 1 && _stepStart[_next] <= pos)
                BeginStep(_next++);
            var until = _next < _stepStart.Length ? _stepStart[_next] : long.MaxValue;
            var n = (int)Math.Min(frames - done, Math.Max(1, until - pos));
            _paula.Render(output.Slice(2 * done, 2 * n), n);
            done += n;
        }
    }

    private void BeginStep(int i)
    {
        var step = _score.Steps[i];
        for (var ch = 0; ch < PaulaMixer.Channels; ch++)
        {
            var note = step.Channels[ch];
            if (note.Sent)
                _paula.Queue(ch, _wave, note.Period, note.Volume);
            else
                _paula.FinishAtWrap(ch); // finished after the previous step's wait
        }
    }
}

/// <summary>Phantasie II's Dosound script on the YM2149 (<see cref="DosoundScore"/>), writes at their 50 Hz ticks.</summary>
public sealed class DosoundRenderer : TuneRenderer
{
    private readonly DosoundScore _score;
    private readonly Ym2149 _psg;
    private int _next;

    public DosoundRenderer(DosoundScore score, double clockHz, int sampleRate) : base(sampleRate)
    {
        _score = score;
        _psg = new Ym2149(clockHz, sampleRate);
        Length = FrameOf(score.Ticks);
    }

    public override long Length { get; }

    private long FrameOf(int tick) => (long)Math.Round((double)tick * SampleRate / DosoundScore.TickHz);

    protected override void RenderCore(Span<float> output, int frames)
    {
        var done = 0;
        while (done < frames)
        {
            var pos = Position + done;
            while (_next < _score.Writes.Count && FrameOf(_score.Writes[_next].Tick) <= pos)
            {
                var w = _score.Writes[_next++];
                _psg.Write(w.Register, w.Value);
            }
            var until = _next < _score.Writes.Count ? FrameOf(_score.Writes[_next].Tick) : long.MaxValue;
            var n = (int)Math.Min(frames - done, Math.Max(1, until - pos));
            _psg.Render(output.Slice(2 * done, 2 * n), n);
            done += n;
        }
    }
}

/// <summary>Phantasie III's music driver output on Paula (<see cref="P3MusicScore"/>), writes at their VBLs.</summary>
public sealed class P3MusicRenderer : TuneRenderer
{
    private readonly P3MusicScore _score;
    private readonly PaulaMixer _paula;
    private readonly IReadOnlyList<sbyte[]> _waves = P3MusicScore.Waveforms();
    private int _next;

    /// <param name="a500Filter">s160: Paula's output through the A500's filters (AMIGA AUDIO FILTER, on by default).</param>
    public P3MusicRenderer(P3MusicScore score, double clockHz, int sampleRate, bool a500Filter = true) : base(sampleRate)
    {
        _score = score;
        _paula = new PaulaMixer(clockHz, sampleRate, a500Filter);
        Length = FrameOf(score.Frames);
    }

    public override long Length { get; }

    private long FrameOf(int vbl) => (long)Math.Round(vbl * SampleRate / _score.VblHz);

    protected override void RenderCore(Span<float> output, int frames)
    {
        var done = 0;
        while (done < frames)
        {
            var pos = Position + done;
            while (_next < _score.Writes.Count && FrameOf(_score.Writes[_next].Frame) <= pos)
                Apply(_score.Writes[_next++]);
            var until = _next < _score.Writes.Count ? FrameOf(_score.Writes[_next].Frame) : long.MaxValue;
            var n = (int)Math.Min(frames - done, Math.Max(1, until - pos));
            _paula.Render(output.Slice(2 * done, 2 * n), n);
            done += n;
        }
    }

    private void Apply(PaulaWrite w)
    {
        switch (w.Kind)
        {
            case PaulaWriteKind.Sample: _paula.SetSample(w.Channel, _waves[w.Value]); break;
            case PaulaWriteKind.Period: _paula.SetPeriod(w.Channel, w.Value); break;
            case PaulaWriteKind.Volume: _paula.SetVolume(w.Channel, w.Value); break;
            case PaulaWriteKind.DmaOn: _paula.DmaOn(w.Channel); break;
            case PaulaWriteKind.DmaOff: _paula.DmaOff(w.Channel); break;
        }
    }
}

/// <summary>
/// Phantasie IV's OPMDRV music (<see cref="OpmDrvScore"/>) on the YM2151 (s109): the sequencer runs live, each pass's
/// register writes go to the chip at the pass's OPM clock, the chip makes one sample per 64 clocks, and those samples
/// are interpolated to the output rate. The tune ends with the pass that ends its last track. s162: each chip sample goes
/// through the X68000's analogue stage after the YM3012 (<see cref="X68000AudioFilter"/>; s163: the first model's sheet).
/// </summary>
public sealed class OpmDrvRenderer : TuneRenderer
{
    private readonly OpmDrvSequencer _seq;
    private readonly Ym2151 _opm = new();
    private readonly List<OpmWrite> _writes = [];
    private readonly double _chipPerFrame;
    private long _chipSamples;       // chip samples made
    private long _nextPassSample;    // the chip sample the next pass happens before
    private double _chipPos;         // output position in chip samples
    private float _prevL, _prevR, _curL, _curR;
    private readonly X68000AudioFilter? _leftFilter, _rightFilter;

    /// <param name="outputFilter">s162: the X68000's analogue output stage (<see cref="X68000AudioFilter"/>); off only for
    /// tests that compare against the bare chip.</param>
    public OpmDrvRenderer(OpmDrvScore score, int sampleRate, bool outputFilter = true) : base(sampleRate)
    {
        if (outputFilter)
        {
            _leftFilter = new X68000AudioFilter(score.ClockHz / Ym2151.ClocksPerSample);
            _rightFilter = new X68000AudioFilter(score.ClockHz / Ym2151.ClocksPerSample);
        }
        _seq = score.Start();
        _seq.InitialWrites(_writes);
        Apply();
        _chipPerFrame = score.ClockHz / Ym2151.ClocksPerSample / sampleRate;
        Length = (long)Math.Round(score.DurationSeconds * sampleRate);
    }

    public override long Length { get; }

    private void Apply()
    {
        foreach (var w in _writes)
            _opm.Write(w.Register, w.Value);
        _writes.Clear();
    }

    private void NextChipSample()
    {
        if (_chipSamples == _nextPassSample && !_seq.Finished)
        {
            _seq.Pass(_writes);
            Apply();
            _nextPassSample += _seq.CyclesToNextPass / Ym2151.ClocksPerSample;
        }
        _opm.Clock(out var l, out var r);
        _prevL = _curL;
        _prevR = _curR;
        _curL = l / 32768f;
        _curR = r / 32768f;
        if (_leftFilter is not null)
        {
            _curL = (float)_leftFilter.Next(_curL);
            _curR = (float)_rightFilter!.Next(_curR);
        }
        _chipSamples++;
    }

    protected override void RenderCore(Span<float> output, int frames)
    {
        for (var f = 0; f < frames; f++)
        {
            // Output frame f sits at chip position _chipPos; keep _chipSamples − 1 ≤ _chipPos < _chipSamples.
            while (_chipSamples <= _chipPos)
                NextChipSample();
            var t = (float)(_chipPos - (_chipSamples - 1));
            output[2 * f] = _prevL + (_curL - _prevL) * t;
            output[2 * f + 1] = _prevR + (_curR - _prevR) * t;
            _chipPos += _chipPerFrame;
        }
    }
}
