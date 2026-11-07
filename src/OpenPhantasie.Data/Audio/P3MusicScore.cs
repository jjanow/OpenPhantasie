namespace OpenPhantasie.Data.Audio;

/// <summary>What a Paula register write in <see cref="P3MusicScore"/> sets.</summary>
public enum PaulaWriteKind
{
    /// <summary><c>AUDxLC</c> + <c>AUDxLEN</c>: the waveform (<see cref="PaulaWrite.Value"/> = <see cref="P3MusicScore"/> waveform id).</summary>
    Sample,
    /// <summary><c>AUDxPER</c>.</summary>
    Period,
    /// <summary><c>AUDxVOL</c> (0–64; the driver writes 0–63).</summary>
    Volume,
    /// <summary><c>DMACON</c> set for the channel (only when its DMA was off: a running channel takes a new waveform at its loop).</summary>
    DmaOn,
    /// <summary><c>DMACON</c> clear for the channel.</summary>
    DmaOff,
}

/// <summary>One Paula register write at a VBL of <see cref="P3MusicScore"/>.</summary>
public readonly record struct PaulaWrite(int Frame, int Channel, PaulaWriteKind Kind, int Value);

/// <summary>
/// Phantasie III Amiga's music (<c>D/m0</c> the title tune, <c>D/m1</c> the gods' reward, s108) as its VBL driver plays
/// it: the Paula register writes of every frame. Static read of the driver (flat image of <c>PhantasieIII</c>,
/// <c>reverse-engineering/amiga/phantasie3/FORMAT.md</c> §7.1) `[C]`:
/// <list type="bullet">
/// <item><c>0x1fa5c(name, n)</c> reads n bytes and splits them at the first two <c>0xFF</c> bytes into three voice
/// streams; <c>0x1f52(v1, v2, v3)</c> starts them on Paula channels 0, 1, 2 (waveform 8, tempo factor 1, level
/// ceiling <c>0x3F00</c>, envelope row 15); a "VertB-Timer" interrupt server (<c>AddIntServer(5)</c>, <c>0xd4a</c>)
/// calls <c>0x2192</c> once a VBL.</item>
/// <item>Per voice per frame: while its note counter runs, count down (at 0 the note enters release unless tied), step
/// the envelope and, with vibrato on, write period − p/128, p, p + p/128, p by a frame counter mod 4. At 0, read: a
/// byte below <c>0x80</c> is a note (0 = rest), the next its length (bit 7 = tie); the note holds
/// (length + 1) × tempo frames, its period is table <c>0x21ed0</c>[note − 9]; commands are bytes from <c>0x80</c>
/// (<see cref="Command"/>).</item>
/// <item>Envelope (<c>0x2360</c>), levels in 1/256 of a Paula volume: attack adds to the ceiling, decay subtracts to
/// the sustain level, sustain holds, release subtracts to 0; rates from tables <c>0x21ab0</c> (attack) and
/// <c>0x21cb0</c> (decay, release) in the row <c>0xC7</c> chose, sustain and ceiling from <c>0x21eb0</c>.</item>
/// </list>
/// The VBL rate is the machine's: 60 Hz NTSC or 50 Hz PAL `[~]` (the pack's <c>vbl_hz</c>).
/// </summary>
public sealed class P3MusicScore : TuneScore
{
    public const int Voices = 3;

    /// <summary>Waveform ids: the buffers <c>0x1e6c</c> builds (8 bytes repeated 8 times, or the noise buffer).</summary>
    public const int WaveSine = 0, WaveSquare = 1, WaveTriangle = 2, WaveNoise = 3, WaveClick = 4;

    // Tables of the data hunk, read in place (flat image 0x21ab0 … 0x21f9c).
    private static readonly short[] Periods =
    [
        16256, 15360, 14496, 13792, 12928, 12192, 11520, 10848, 10240, 9664, 9120, 8608, 8128, 7680, 7248, 6896, 6464, 6096,
        5760, 5424, 5120, 4832, 4560, 4304, 4064, 3840, 3624, 3448, 3232, 3048, 2880, 2712, 2560, 2416, 2280, 2152, 2032,
        1920, 1812, 1712, 1616, 1524, 1440, 1356, 1280, 1208, 1140, 1076, 1016, 960, 906, 856, 808, 762, 720, 678, 640, 604,
        570, 538, 508, 480, 453, 428, 404, 381, 360, 339, 320, 302, 285, 269, 254, 240, 226, 214, 202, 190, 180, 170, 160,
        151, 143, 135, 127, 124, 124,
    ];

    /// <summary><c>0x21eb0</c>: sustain levels and level ceilings, by nibble / row (row r = 1075.2·r, 16128 = 63 × 256).</summary>
    private static readonly short[] Levels =
        [0, 1075, 2150, 3226, 4301, 5376, 6451, 7526, 8602, 9676, 10752, 11827, 12902, 13978, 15053, 16128];

    /// <summary><c>0x21ab0</c>: attack rates by row (<c>0xC7</c>) and nibble.</summary>
    private static readonly short[,] AttackRates = RateTable(0x21ab0);

    /// <summary><c>0x21cb0</c>: decay and release rates by row and nibble.</summary>
    private static readonly short[,] DecayRates = RateTable(0x21cb0);

    private P3MusicScore(IReadOnlyList<PaulaWrite> writes, int frames, IReadOnlyList<int> voiceStarts, int[] notes, bool loops)
    {
        Writes = writes;
        Frames = frames;
        VoiceStarts = voiceStarts;
        NoteCounts = notes;
        Loops = loops;
    }

    public IReadOnlyList<PaulaWrite> Writes { get; }

    /// <summary>
    /// The frame the last voice's DMA goes off on (the frame after it read its <c>0xFF</c>): the tune sounds on frames
    /// 0 … <see cref="Frames"/> − 1.
    /// </summary>
    public int Frames { get; }

    /// <summary>Where each voice stream starts in the loaded bytes.</summary>
    public IReadOnlyList<int> VoiceStarts { get; }

    /// <summary>Notes (including rests) each voice reads.</summary>
    public IReadOnlyList<int> NoteCounts { get; }

    /// <summary>True when a voice reached <c>0xE7</c> (restart all voices): the tune loops and <see cref="Frames"/> is a cap.</summary>
    public bool Loops { get; }

    /// <summary>The VBL rate the duration is reckoned at (set by <see cref="Parse"/>).</summary>
    public double VblHz { get; private init; }

    public override double DurationSeconds => Frames / VblHz;

    /// <summary>
    /// The waveform buffers <c>0x1e6c</c> builds at start-up: three 64-byte tone buffers (an 8-byte table repeated 8
    /// times: <c>0x21f8e</c> sine-like, <c>0x21f7e</c> square, <c>0x21f86</c> triangle), the 4000-byte noise buffer
    /// (1000 longs of the LCG <c>0x1c24</c>: seed = (seed·25173 + 13849) mod 65536 from seed 0, the BSS start value), and
    /// the 4-byte click of an unknown command (<c>0x21f98</c>).
    /// </summary>
    public static IReadOnlyList<sbyte[]> Waveforms()
    {
        static sbyte[] Repeat(sbyte[] eight)
        {
            var b = new sbyte[64];
            for (var i = 0; i < 64; i++)
                b[i] = eight[i % 8];
            return b;
        }
        var noise = new sbyte[4000];
        var seed = 0;
        for (var i = 0; i < 1000; i++)
        {
            seed = (seed * 25173 + 13849) % 65536;
            noise[4 * i + 2] = unchecked((sbyte)(seed >> 8));
            noise[4 * i + 3] = unchecked((sbyte)seed);
        }
        return
        [
            Repeat([0, 53, 127, 73, 0, -53, -127, -73]),
            Repeat([127, 127, 127, 127, -127, -127, -127, -127]),
            Repeat([0, 42, 85, 127, -127, -85, -42, 0]),
            noise,
            [unchecked((sbyte)0x81), 0x7F, 0x00, 0x01],
        ];
    }

    /// <summary>The single-byte commands (from <c>0x80</c>) the driver knows, with the bytes each one takes.</summary>
    public static readonly IReadOnlyDictionary<int, int> Command = new Dictionary<int, int>
    {
        [0xC2] = 1, // waveform: 0x10 sine, 0x20 triangle, 0x40 square, else noise
        [0xC3] = 2, // skipped
        [0xC4] = 2, // envelope: attack/decay nibbles, sustain/release nibbles
        [0xC7] = 1, // envelope row and level ceiling
        [0xCA] = 0, [0xCC] = 0, [0xE3] = 0, [0xE4] = 0, // no effect
        [0xCD] = 1, [0xCE] = 2, // loop 1: count; back by a 16-bit offset while the count runs
        [0xD1] = 1, [0xD2] = 2, // loop 2
        [0xD3] = 0, [0xD4] = 0, // vibrato on
        [0xDB] = 2, // vibrato on (2 bytes skipped)
        [0xDC] = 0, // vibrato off
        [0xE1] = 0, // waveform 0 (sine)
        [0xE2] = 1, [0xE8] = 1, [0xE9] = 1, // tempo factor of all voices: set, minus, plus
        [0xE5] = 1, // noise
        [0xE6] = 1, // skipped
        [0xE7] = 0, // restart every voice
        [0xFF] = 0, // end of the voice
    };

    private sealed class Voice
    {
        public int? Pc;
        public int? Start;
        public int Counter, Phase, Tie, Vibrato, Period, Level, Loop1, Loop2;
        public int Attack, Decay, Sustain, Release;
        public int Wave = WaveTriangle;
        public bool DmaOffSent;
    }

    /// <summary>
    /// Load <paramref name="length"/> bytes (the game's <c>0x1fa5c</c> count; m0 830, m1 659) and run the driver until every
    /// voice ends (or <paramref name="maxFrames"/>), recording each Paula write.
    /// </summary>
    public static P3MusicScore Parse(ReadOnlySpan<byte> file, int length, double vblHz, int maxFrames = 60 * 60 * 10)
    {
        if (length > file.Length)
            throw new InvalidDataException($"P3 music: wants {length} bytes, the file has {file.Length}.");
        var d = file[..length].ToArray();
        var first = Array.IndexOf(d, (byte)0xFF);
        var second = first < 0 ? -1 : Array.IndexOf(d, (byte)0xFF, first + 1);
        if (second < 0)
            throw new InvalidDataException("P3 music: fewer than three voice streams.");
        return Run(d, [new P3DriverStart(0, 0, first + 1, second + 1)], vblHz, maxFrames);
    }

    /// <summary>
    /// Run the driver over <paramref name="image"/> with <c>0x1f52(v1, v2, v3)</c> called at each of
    /// <paramref name="starts"/> (in frame order; a start applies before that frame's step, as the game calls it between
    /// two VBLs) until every voice has ended after the last start (or <paramref name="maxFrames"/>). A sound effect
    /// (<c>0xb126(n)</c>, <see cref="P3SoundEffects"/>) is such a start: it cuts whatever the driver was playing.
    /// </summary>
    public static P3MusicScore Run(byte[] image, IReadOnlyList<P3DriverStart> starts, double vblHz,
        int maxFrames = 60 * 60 * 10)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(starts);
        if (starts.Count == 0)
            throw new ArgumentException("P3 music: no start.", nameof(starts));
        for (var i = 1; i < starts.Count; i++)
            if (starts[i].Frame < starts[i - 1].Frame)
                throw new ArgumentException("P3 music: starts out of frame order.", nameof(starts));
        foreach (var st in starts)
            foreach (var at in st.Voices)
                if (at is int a && (a < 0 || a >= image.Length))
                    throw new InvalidDataException($"P3 music: a voice starts at {a}, past the image ({image.Length} B).");
        var d = image;
        var writes = new List<PaulaWrite>();
        var voices = new Voice[Voices];
        var notes = new int[Voices];
        var tempo = new[] { 1, 1, 1 };
        var row = 15;          // 0x240a4
        var ceiling = 0x3F00;  // 0x240a6
        var frameCount = 0;    // 0x240aa (BSS 0 at boot; 0x1f52 does not reset it)
        var loops = false;
        for (var v = 0; v < Voices; v++)
            voices[v] = new Voice { DmaOffSent = true };
        var nextStart = 0;

        int frame;
        for (frame = 0; frame < maxFrames; frame++)
        {
            while (nextStart < starts.Count && starts[nextStart].Frame <= frame)
                Start(starts[nextStart++]);
            // Every voice has read its 0xFF and had its DMA switched off on the frame after: the run is over.
            if (nextStart >= starts.Count && voices.All(v => v.DmaOffSent))
                break;
            var restarted = false;
            for (var v = 0; v < Voices && !restarted; v++)
                restarted = Step(v);
            if (!restarted)
                frameCount = (frameCount + 1) & 3;
        }
        // The sound stops at the start of the frame the last DMA goes off on (a looping tune is cut at maxFrames).
        var end = voices.All(v => v.DmaOffSent) ? writes.Last(w => w.Kind == PaulaWriteKind.DmaOff).Frame : frame;
        return new P3MusicScore(writes, end, starts[0].Voices.Select(x => x ?? -1).ToList(), notes, loops)
        {
            VblHz = vblHz,
        };

        // 0x1f52(v1, v2, v3) with none −1: per voice a fresh state (waveform 8 = triangle, tempo factor 1, loop and note
        // counters, envelope phase, rates, vibrato and level 0, length 32 words) and its DMA bit cleared; a 0 pointer leaves
        // the voice ended (the frame step keeps its DMA off). Then the level ceiling 0x3F00 and row 15.
        void Start(P3DriverStart st)
        {
            for (var v = 0; v < Voices; v++)
            {
                var at = st.Voices[v];
                voices[v] = new Voice { Pc = at, Start = at };
                tempo[v] = 1;
                writes.Add(new PaulaWrite(frame, v, PaulaWriteKind.DmaOff, 0));
            }
            ceiling = 0x3F00;
            row = 15;
        }

        void Write(int v, PaulaWriteKind kind, int value) => writes.Add(new PaulaWrite(frame, v, kind, value));

        // 0x21e4 for one voice; true when 0xE7 restarted all voices (the rest of the frame is skipped).
        bool Step(int v)
        {
            var s = voices[v];
            if (s.Pc is null)
            {
                // 0x21ec: a voice whose stream ended clears its DMA bit (every frame; recorded once).
                if (!s.DmaOffSent)
                {
                    Write(v, PaulaWriteKind.DmaOff, 0);
                    s.DmaOffSent = true;
                }
                return false;
            }
            if (s.Counter != 0)
            {
                s.Counter--;
                if (s.Counter == 0 && s.Tie == 0)
                    s.Phase = 3;
                Envelope(v);
                if (s.Vibrato == 0)
                    return false;
                var delta = (short)s.Period >> 7;
                Write(v, PaulaWriteKind.Period, frameCount switch
                {
                    1 => s.Period - delta,
                    3 => s.Period + delta,
                    _ => s.Period,
                });
                return false;
            }
            var pc = s.Pc.Value;
            while (true)
            {
                int c = d[pc++];
                if (c < 0x80)
                {
                    int len = d[pc++];
                    s.Tie = 0;
                    if ((len & 0x80) != 0)
                    {
                        len &= 0x7F;
                        s.Tie = 1;
                    }
                    if (tempo[v] != 1)
                        len = (ushort)((len + 1) * (ushort)tempo[v] - 1);
                    s.Counter = len;
                    s.Phase = 0;
                    s.Pc = pc;
                    notes[v]++;
                    if (c == 0)
                    {
                        s.Phase = 3;
                        Envelope(v);
                        return false;
                    }
                    s.Period = Periods[Math.Clamp(c - 9, 0, Periods.Length - 1)];
                    s.Level = 0;
                    Write(v, PaulaWriteKind.Volume, 0);
                    Write(v, PaulaWriteKind.Sample, s.Wave);
                    Write(v, PaulaWriteKind.Period, s.Period);
                    Envelope(v);
                    Write(v, PaulaWriteKind.DmaOn, 0);
                    return false;
                }
                switch (c)
                {
                    case 0xC4:
                    {
                        int a = d[pc], b = d[pc + 1];
                        s.Attack = AttackRates[row, a >> 4];
                        s.Decay = DecayRates[row, a & 15];
                        s.Release = DecayRates[row, b & 15];
                        s.Sustain = Levels[b >> 4];
                        pc += 2;
                        break;
                    }
                    case 0xC7:
                        row = d[pc++] & 15;
                        ceiling = Levels[row];
                        break;
                    case 0xFF:
                        s.Pc = null;
                        return false;
                    case 0xCE or 0xD2:
                    {
                        ref var count = ref c == 0xCE ? ref s.Loop1 : ref s.Loop2;
                        count = (count - 1) & 0xFFFF;
                        if (count != 0)
                            pc -= (d[pc] << 8) | d[pc + 1];
                        else
                            pc += 2;
                        break;
                    }
                    case 0xCD: s.Loop1 = d[pc++]; break;
                    case 0xD1: s.Loop2 = d[pc++]; break;
                    case 0xE2: tempo[0] = tempo[1] = tempo[2] = d[pc++]; break;
                    case 0xE8: { var k = d[pc++]; for (var i = 0; i < Voices; i++) tempo[i] -= k; break; }
                    case 0xE9: { var k = d[pc++]; for (var i = 0; i < Voices; i++) tempo[i] += k; break; }
                    case 0xC2:
                    {
                        var w = d[pc++];
                        s.Wave = w switch { 0x10 => WaveSine, 0x20 => WaveTriangle, 0x40 => WaveSquare, _ => WaveNoise };
                        break;
                    }
                    case 0xDB: pc += 2; s.Vibrato = 1; break;
                    case 0xDC: s.Vibrato = 0; break;
                    case 0xD3 or 0xD4: s.Vibrato = 1; break;
                    case 0xE1: s.Wave = WaveSine; break;
                    case 0xE5: s.Wave = WaveNoise; pc++; break;
                    case 0xC3: pc += 2; break;
                    case 0xE6: pc++; break;
                    case 0xCA or 0xCC or 0xE3 or 0xE4: break;
                    case 0xE7:
                        // Restart: every voice back to its start, counters cleared; the frame ends here.
                        loops = true;
                        foreach (var x in voices)
                        {
                            x.Pc = x.Start;
                            x.Counter = 0;
                            x.DmaOffSent = false;
                        }
                        return true;
                    default:
                        // Any other byte: the driver's fall-through plays a 4-byte click on channel 3 and reads on.
                        writes.Add(new PaulaWrite(frame, 3, PaulaWriteKind.Sample, WaveClick));
                        writes.Add(new PaulaWrite(frame, 3, PaulaWriteKind.Period, 2000));
                        writes.Add(new PaulaWrite(frame, 3, PaulaWriteKind.Volume, 64));
                        writes.Add(new PaulaWrite(frame, 3, PaulaWriteKind.DmaOn, 0));
                        break;
                }
            }
        }

        // 0x2360: one envelope step, then AUDxVOL = level >> 8.
        void Envelope(int v)
        {
            var s = voices[v];
            var lvl = s.Level;
            var ph = s.Phase;
            if (ph == 0)
            {
                if (s.Attack == 0)
                {
                    lvl = ceiling;
                    ph = 1; // and decay this same frame
                }
                else
                {
                    lvl += s.Attack;
                    if (ceiling <= lvl)
                    {
                        lvl = ceiling;
                        ph = 1;
                    }
                    goto store;
                }
            }
            if (ph == 1)
            {
                if (s.Decay == 0)
                    ph = 2;
                else
                {
                    lvl -= s.Decay;
                    if (s.Sustain >= lvl)
                    {
                        lvl = s.Sustain;
                        ph = 2;
                    }
                    goto store;
                }
            }
            if (ph == 2)
                lvl = s.Sustain;
            else if (ph == 3)
            {
                if (s.Release == 0)
                    lvl = 0;
                else
                {
                    lvl -= s.Release;
                    if (lvl <= 0)
                    {
                        ph = 4;
                        lvl = 0;
                    }
                }
            }
            else if (ph > 3)
                lvl = 0;
            store:
            lvl = (short)lvl;
            s.Level = lvl;
            s.Phase = ph;
            Write(v, PaulaWriteKind.Volume, lvl >> 8);
        }
    }

    private static short[,] RateTable(int address) => address switch
    {
        0x21ab0 => Rows(
        [
            [0, 0, 1075, 1075, 472, 320, 264, 224, 179, 72, 36, 22, 18, 6, 4, 2],
            [0, 0, 2150, 2150, 943, 640, 527, 448, 358, 143, 72, 45, 36, 12, 7, 4],
            [0, 0, 3226, 3226, 1415, 960, 791, 672, 538, 215, 108, 67, 54, 18, 11, 7],
            [0, 0, 4301, 4301, 1886, 1280, 1054, 896, 717, 287, 143, 90, 72, 24, 14, 9],
            [0, 0, 5376, 5376, 2358, 1600, 1318, 1120, 896, 358, 179, 112, 90, 30, 18, 11],
            [0, 0, 6451, 6451, 2829, 1920, 1581, 1344, 1075, 430, 215, 134, 108, 36, 22, 13],
            [0, 0, 7526, 7526, 3301, 2240, 1845, 1568, 1254, 502, 251, 157, 125, 42, 25, 16],
            [0, 0, 8602, 8602, 3773, 2560, 2108, 1792, 1434, 573, 287, 179, 143, 48, 29, 18],
            [0, 0, 9676, 9676, 4244, 2880, 2372, 2016, 1613, 645, 323, 202, 161, 54, 32, 20],
            [0, 0, 10752, 10752, 4716, 3200, 2635, 2240, 1792, 717, 358, 224, 179, 60, 36, 22],
            [0, 0, 11827, 11827, 5187, 3520, 2899, 2464, 1971, 788, 394, 246, 197, 66, 39, 25],
            [0, 0, 12902, 12902, 5659, 3840, 3162, 2688, 2150, 860, 430, 269, 215, 72, 43, 27],
            [0, 0, 13978, 13978, 6131, 4160, 3426, 2912, 2330, 932, 466, 291, 233, 78, 47, 29],
            [0, 0, 15053, 15053, 6602, 4480, 3689, 3136, 2509, 1004, 502, 314, 251, 84, 50, 31],
            [0, 0, 16128, 16128, 7074, 4800, 3953, 3360, 2688, 1075, 538, 336, 269, 90, 54, 34],
        ]),
        _ => Rows(
        [
            [0, 747, 373, 249, 157, 107, 88, 75, 60, 24, 12, 7, 6, 2, 1, 0],
            [0, 1493, 747, 498, 314, 213, 176, 149, 119, 48, 24, 15, 12, 4, 2, 1],
            [0, 2240, 1120, 747, 472, 320, 264, 224, 179, 72, 36, 22, 18, 6, 4, 2],
            [0, 2987, 1493, 995, 629, 427, 351, 299, 239, 95, 48, 30, 24, 8, 5, 3],
            [0, 3733, 1867, 1244, 786, 533, 439, 373, 299, 119, 60, 37, 30, 10, 6, 4],
            [0, 4480, 2240, 1493, 943, 640, 527, 448, 358, 143, 72, 45, 36, 12, 7, 4],
            [0, 5227, 2613, 1742, 1100, 747, 615, 523, 418, 167, 84, 52, 42, 14, 8, 5],
            [0, 5973, 2987, 1991, 1258, 853, 703, 597, 478, 191, 95, 60, 48, 16, 9, 6],
            [0, 6720, 3360, 2240, 1415, 960, 791, 672, 538, 215, 108, 67, 54, 18, 11, 7],
            [0, 7467, 3733, 2489, 1572, 1067, 878, 747, 597, 239, 119, 75, 60, 20, 12, 7],
            [0, 8213, 4107, 2738, 1729, 1173, 966, 821, 657, 263, 131, 82, 66, 22, 13, 8],
            [0, 8960, 4480, 2987, 1886, 1280, 1054, 896, 717, 287, 143, 90, 72, 24, 14, 9],
            [0, 9706, 4853, 3236, 2044, 1387, 1142, 970, 777, 311, 155, 97, 78, 26, 16, 9],
            [0, 10453, 5227, 3484, 2201, 1493, 1230, 1045, 836, 335, 167, 105, 84, 28, 17, 10],
            [0, 11200, 5600, 3733, 2358, 1600, 1318, 1120, 896, 358, 179, 112, 90, 30, 18, 11],
        ]),
    };

    /// <summary>A 16 × 16 table whose row 0 is all zero (as both are in the binary), rows 1–15 as given.</summary>
    private static short[,] Rows(short[][] rows)
    {
        var t = new short[16, 16];
        for (var r = 0; r < rows.Length; r++)
            for (var n = 0; n < 16; n++)
                t[r + 1, n] = rows[r][n];
        return t;
    }
}
