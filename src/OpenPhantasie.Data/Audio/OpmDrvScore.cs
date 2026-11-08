using System.Text;

namespace OpenPhantasie.Data.Audio;

/// <summary>One YM2151 register write.</summary>
public readonly record struct OpmWrite(int Register, int Value);

/// <summary>
/// Phantasie IV X68000's music (s109): MML strings in <c>P4.X</c> compiled and played by Sharp's <b>OPMDRV.X</b> 1.01
/// (the <c>CONFIG.SYS</c> device driver) with the 200 FM voices of <c>P4.SND</c>, on the YM2151 (OPM). This class is
/// the driver's two halves done headless: its MML compiler (<c>_m_trk</c>, OPMDRV image <c>0xa58</c>) into each
/// track's 3-byte event stream, and its sequencer (the OPM timer A interrupt <c>0x1c9e</c>) that turns the streams into
/// OPM register writes. The Client renders the writes on a YM2151 model. Static read of OPMDRV.X (image = file − 0x40)
/// `[C]`; <c>reverse-engineering/x68000/phantasie4/FORMAT.md</c> §6.11.
/// <list type="bullet">
/// <item>Timing: timer A, NA = 1024 − ⌊19531 / tempo⌋ (<c>0x1f36</c>), reloaded with IRQ enabled (register 0x14 =
/// 0x15) at the end of every interrupt; the sequencer runs on every 4th interrupt (<c>0x248f</c> &amp; 3 = 0), all 8
/// channels in order. A timer A period is 64 · (1024 − NA) OPM clocks, so a pass is 256 · ⌊19531 / T⌋ clocks: at the
/// X68000's 4 MHz, 1.25 / T s, 48 passes to a quarter note (the compiler's whole note is 192). Default tempo 120.</item>
/// <item>Track i plays on OPM channel i − 1 (<c>_mus</c>: <c>_m_assign(i, i)</c>).</item>
/// </list>
/// </summary>
public sealed class OpmDrvScore : TuneScore
{
    /// <summary>The X68000's OPM clock, 4 MHz `[C]` (s162): Sharp's service manuals draw the YM2151's φM pin 24 on a net
    /// labelled "4MHz" (CZ-134, CZ-634C/644C, sound block diagram 図7-1 "CLK(4MHz)" and control-board diagram pp. 85–86;
    /// the same in CZ-147), and <i>Inside X68000</i> (Kuwano, 1992, p. 280) says the X68000 gives the OPM 4 MHz, about
    /// 192 cents above the data sheet's 3.579545 MHz notes. The driver's 19531 = 4 000 000 · 60 / (64 · 4 · 48) agrees
    /// (FORMAT.md 6.11.1). No sheet of the first model (CZ-600C) was found.</summary>
    public const int X68000OpmClockHz = 4_000_000;

    public const int Channels = 8;
    public const int VoiceCount = 200;
    public const int VoiceBytes = 55;
    public const int SndRecordBytes = 80;
    public const int DefaultTempo = 120;

    /// <summary>The compiler's ticks in a whole note (<c>192 / n</c> for a length n).</summary>
    public const int WholeNote = 192;

    private OpmDrvScore(IReadOnlyList<byte[]?> tracks, IReadOnlyList<byte[]> voices, double clockHz)
    {
        Tracks = tracks;
        Voices = voices;
        ClockHz = clockHz;
        long cycles = 0, passes = 0;
        var seq = Start();
        seq.InitialWrites(new List<OpmWrite>());
        var sink = new List<OpmWrite>();
        while (!seq.Finished)
        {
            sink.Clear();
            seq.Pass(sink);
            cycles += seq.CyclesToNextPass;
            passes++;
        }
        TotalCycles = cycles;
        Passes = passes;
    }

    /// <summary>Each channel's compiled event stream (ending <c>0xFF</c>), or null when no track plays on it.</summary>
    public IReadOnlyList<byte[]?> Tracks { get; }

    /// <summary>The 200 voices as the driver keeps them (the 55-byte <c>_m_vset</c> arrays, <see cref="VoiceFromSnd"/>).</summary>
    public IReadOnlyList<byte[]> Voices { get; }

    public double ClockHz { get; }

    /// <summary>OPM clocks from the start to the pass that ends the last track.</summary>
    public long TotalCycles { get; }

    /// <summary>Sequencer passes up to and including the one that ends the last track.</summary>
    public long Passes { get; }

    public override double DurationSeconds => TotalCycles / ClockHz;

    /// <summary>A fresh sequencer at the start of the tune (<c>_m_play</c>).</summary>
    public OpmDrvSequencer Start() => new(this);

    /// <summary>
    /// Compile the MML of each track (track i = the concatenation of <paramref name="trackMml"/>[i], sent with one
    /// <c>_m_trk</c> each, which appends) with the voices of a <c>P4.SND</c> image.
    /// </summary>
    public static OpmDrvScore Load(IReadOnlyList<IReadOnlyList<string>> trackMml, ReadOnlySpan<byte> snd,
        double clockHz = X68000OpmClockHz)
    {
        if (trackMml.Count is 0 or > Channels)
            throw new InvalidDataException($"OPMDRV: {trackMml.Count} tracks (1–8).");
        var tracks = new byte[]?[Channels];
        for (var i = 0; i < trackMml.Count; i++)
        {
            var c = new OpmDrvCompiler();
            foreach (var s in trackMml[i])
                c.Compile(s);
            tracks[i] = c.Events();
        }
        return new OpmDrvScore(tracks, ReadSnd(snd), clockHz);
    }

    /// <summary>
    /// Load from a program image: <paramref name="image"/> holds the NUL-terminated MML strings at the addresses of
    /// <paramref name="trackAddresses"/> (per track, in sending order).
    /// </summary>
    public static OpmDrvScore Load(ReadOnlySpan<byte> image, IReadOnlyList<IReadOnlyList<int>> trackAddresses,
        ReadOnlySpan<byte> snd, double clockHz = X68000OpmClockHz)
    {
        var mml = new List<IReadOnlyList<string>>();
        foreach (var track in trackAddresses)
        {
            var parts = new List<string>();
            foreach (var a in track)
                parts.Add(CString(image, a));
            mml.Add(parts);
        }
        return Load(mml, snd, clockHz);
    }

    public static string CString(ReadOnlySpan<byte> image, int address)
    {
        if (address < 0 || address >= image.Length)
            throw new InvalidDataException($"OPMDRV: MML address 0x{address:x} outside the image.");
        var rest = image[address..];
        var end = rest.IndexOf((byte)0);
        if (end < 0)
            throw new InvalidDataException($"OPMDRV: MML at 0x{address:x} has no end.");
        return Encoding.ASCII.GetString(rest[..end]);
    }

    /// <summary>The 200 records of <c>P4.SND</c> as the driver's voices (<see cref="VoiceFromSnd"/>).</summary>
    public static IReadOnlyList<byte[]> ReadSnd(ReadOnlySpan<byte> snd)
    {
        if (snd.Length < VoiceCount * SndRecordBytes)
            throw new InvalidDataException($"P4.SND: {snd.Length} bytes, {VoiceCount * SndRecordBytes} expected.");
        var voices = new byte[VoiceCount][];
        for (var n = 0; n < VoiceCount; n++)
            voices[n] = VoiceFromSnd(snd.Slice(n * SndRecordBytes, SndRecordBytes));
        return voices;
    }

    /// <summary>A <c>P4.SND</c> record's 10-character name.</summary>
    public static string VoiceName(ReadOnlySpan<byte> record) =>
        Encoding.ASCII.GetString(record[..10]).TrimEnd('\0', ' ');

    // P4.X _m_inn (0x35a6c…0x35bbe): the header bytes 1–10 from the record offsets of table 0x47c3c, the operator
    // parameters from the 4-byte groups at the offsets of table 0x47c64.
    private static readonly int[] HeaderOffsets = [0x41, 0x38, 0x40, 0x39, 0x3c, 0x3d, 0x3a, 0x3b, 0x42, 0x00];
    private static readonly int[] OperatorOffsets = [0x0a, 0x0e, 0x12, 0x16, 0x1a, 0x1e, 0x22, 0x26, 0x2a, 0x2e, 0x32];

    /// <summary>
    /// <c>P4.X</c> <c>_m_inn</c>'s conversion of an 80-byte <c>P4.SND</c> record into the driver's 55-byte voice
    /// `[C]`: byte 0 = FL · 8 + CON (record 0x36, 0x37); 1 slot mask (0x41), 2 LFO waveform (0x38), 3 LFO sync (0x40),
    /// 4 LFRQ (0x39), 5 PMD (0x3c), 6 AMD (0x3d), 7 PMS (0x3a), 8 AMS (0x3b), 9 pan (0x42), 10 = the name's first
    /// character (unused by the driver); then 4 operators × 11 bytes (AR, D1R, D2R, RR, D1L, TL, KS, MUL, DT1, DT2, AME),
    /// operator k's from byte k of each 4-byte record group (0x0a AR … 0x32 AME). Operator order M1, C1, M2, C2 (the
    /// algorithm's OP1–OP4: the driver writes them to slot offsets 0, 16, 8, 24).
    /// </summary>
    public static byte[] VoiceFromSnd(ReadOnlySpan<byte> record)
    {
        if (record.Length < SndRecordBytes)
            throw new InvalidDataException("P4.SND: a short record.");
        var v = new byte[VoiceBytes];
        v[0] = (byte)(record[0x36] * 8 + record[0x37]);
        for (var i = 0; i < HeaderOffsets.Length; i++)
            v[1 + i] = record[HeaderOffsets[i]];
        for (var p = 0; p < OperatorOffsets.Length; p++)
            for (var k = 0; k < 4; k++)
                v[11 + 11 * k + p] = record[OperatorOffsets[p] + k];
        return v;
    }
}

/// <summary>
/// OPMDRV.X's MML compiler (<c>0xa58</c>), the subset Phantasie IV's songs use `[C]`. Letters are case-blind (the
/// driver upper-cases each), spaces are skipped. Emits 3-byte events (a 2-byte command gets a 0 pad, <c>0x1104</c>):
/// <list type="table">
/// <item><term>a–g [#|+|-] [n] [.…]</term><description>note: index = semitone (table <c>0x147a</c>: C −3, D −1, E 1,
/// F 2, G 4, A 6, B 8) ± 1 + octave · 12, below 96, → key code (table <c>0x1482</c>: index / 12 = octave, index % 12 →
/// note codes 0, 1, 2, 4, 5, 6, 8, 9, 10, 12, 13, 14 from C#, so index 0 = o0 D# = KC 0). Event (KC, length, gate):
/// length = 192 / n (1 ≤ n ≤ 64) or the default; each of up to three dots adds half the last addition; gate =
/// ⌊length · q / 8⌋ − 1, at least 0 (<c>0x133e</c>).</description></item>
/// <item><term>r [n] [.…]</term><description>rest: (0x7F, length, gate).</description></item>
/// <item><term>&amp;</term><description>tie: the last event must be a note or rest; its gate := its length (no key-off,
/// and the next note keys on over it).</description></item>
/// <item><term>o n / &lt; / &gt;</term><description>octave 0–8 (default 4); <b>&lt; = up</b>, &gt; = down.</description></item>
/// <item><term>l n / q n</term><description>default length 192 / n (default 48); gate q 1–8 (default 8).</description></item>
/// <item><term>v n</term><description>(0x80, table <c>0x146a</c>[n], 0): carrier attenuation for v 0–15 = 42, 40, 37,
/// 34, 32, 29, 26, 24, 21, 18, 16, 13, 10, 8, 5, 2; <c>@v n</c> = (0x80, 127 − n).</description></item>
/// <item><term>t n</term><description>(0x90, n) tempo 32–200.</description></item>
/// <item><term>@ n</term><description>(0xA0, n) voice 1–200. <c>@l n</c> default length in ticks (≤ 192),
/// <c>@w n</c> (0xD0, 192 / n, 192 / n) a wait.</description></item>
/// <item><term>p n</term><description>(0xE0, n) pan 0–3 (bit 0 left, bit 1 right).</description></item>
/// <item><term>y r,v</term><description>(0xB0, r, v) a raw OPM register write.</description></item>
/// <item><term>|: n … :|</term><description>(0xC0 0 n) loop start, n 1–256 (default 2; 256 is stored as 0 = 256
/// rounds); (0xC0 1) loop end. One loop level per channel.</description></item>
/// </list>
/// Not handled (<c>[?]</c>, unused by IV): <c>|n</c> (loop break), <c>[ … ]</c> (segno / coda / D.C. / D.S. / fine),
/// <c>{ … }</c> (tuplets). The driver ends each event stream with 0xFF.
/// </summary>
public sealed class OpmDrvCompiler
{
    /// <summary><c>0x146a</c>: v 0–15 → the attenuation added to a voice's carrier TLs.</summary>
    public static readonly byte[] VolumeTable = [0x2a, 0x28, 0x25, 0x22, 0x20, 0x1d, 0x1a, 0x18, 0x15, 0x12, 0x10, 0x0d, 0x0a, 0x08, 0x05, 0x02];

    // 0x147a: A–G → semitones from D# (the key code table's index 0).
    private static readonly sbyte[] NoteSemitone = [6, 8, -3, -1, 1, 2, 4];

    // 0x1482: index → key code; the note codes skip 3, 7, 11, 15.
    private static readonly byte[] NoteCodes = [0, 1, 2, 4, 5, 6, 8, 9, 10, 12, 13, 14];

    private readonly List<byte> _out = [];
    private int _octave = 4, _length = 48, _q = 8;
    private string _s = "";
    private int _p;

    /// <summary>Key code for compiler note index 0–95 (table <c>0x1482</c>).</summary>
    public static int KeyCode(int index) => (index / 12) << 4 | NoteCodes[index % 12];

    public byte[] Events()
    {
        var e = new byte[_out.Count + 1];
        _out.CopyTo(e);
        e[^1] = 0xFF;
        return e;
    }

    private InvalidDataException Error(string what) => new($"OPMDRV MML: {what} at {_p} in \"{_s}\".");

    private void Emit(int a, int b, int c)
    {
        _out.Add((byte)a);
        _out.Add((byte)b);
        _out.Add((byte)c);
    }

    private void SkipSpaces()
    {
        while (_p < _s.Length && _s[_p] == ' ')
            _p++;
    }

    private bool AtDigit()
    {
        SkipSpaces();
        return _p < _s.Length && _s[_p] is >= '0' and <= '9';
    }

    private int Number()
    {
        SkipSpaces();
        var n = 0;
        while (_p < _s.Length && _s[_p] is >= '0' and <= '9')
            n = n * 10 + (_s[_p++] - '0');
        return n;
    }

    private char Peek() => _p < _s.Length ? char.ToUpperInvariant(_s[_p]) : '\0';

    /// <summary>Append one <c>_m_trk</c> string's events.</summary>
    public void Compile(string mml)
    {
        _s = mml;
        _p = 0;
        while (true)
        {
            SkipSpaces();
            if (_p >= _s.Length)
                return;
            var c = char.ToUpperInvariant(_s[_p++]);
            switch (c)
            {
                case 'O':
                    var o = AtDigit() ? Number() : throw Error("o without a number");
                    _octave = o <= 8 ? o : throw Error("octave above 8");
                    break;
                case '<':
                    _octave = _octave < 8 ? _octave + 1 : throw Error("octave above 8");
                    break;
                case '>':
                    _octave = _octave > 0 ? _octave - 1 : throw Error("octave below 0");
                    break;
                case 'L':
                    _length = WholeOver(AtDigit() ? Number() : throw Error("l without a number"));
                    break;
                case 'Q':
                    var q = AtDigit() ? Number() : throw Error("q without a number");
                    _q = q is >= 1 and <= 8 ? q : throw Error("q outside 1–8");
                    break;
                case 'V':
                    var v = AtDigit() ? Number() : throw Error("v without a number");
                    Emit(0x80, v <= 15 ? VolumeTable[v] : throw Error("v above 15"), 0);
                    break;
                case 'T':
                    var t = AtDigit() ? Number() : throw Error("t without a number");
                    Emit(0x90, t is >= 32 and <= 200 ? t : throw Error("tempo outside 32–200"), 0);
                    break;
                case 'P':
                    var pan = AtDigit() ? Number() : throw Error("p without a number");
                    Emit(0xE0, pan <= 3 ? pan : throw Error("pan above 3"), 0);
                    break;
                case 'Y':
                    var reg = AtDigit() ? Number() : throw Error("y without a register");
                    SkipSpaces();
                    if (Peek() != ',')
                        throw Error("y without a comma");
                    _p++;
                    var val = AtDigit() ? Number() : throw Error("y without a value");
                    if (reg > 255 || val > 255 || reg is 1 or 0x10 or 0x11 or 0x12 or 0x14)
                        throw Error("y register refused");
                    Emit(0xB0, reg, val);
                    break;
                case '@':
                    Voice();
                    break;
                case '&':
                    if (_out.Count < 3 || _out[^3] >= 0x80)
                        throw Error("& after no note");
                    _out[^1] = _out[^2];
                    break;
                case '|':
                    if (Peek() != ':')
                        throw Error("| (loop break) is not handled [?]");
                    _p++;
                    var n = AtDigit() ? Number() : 2;
                    Emit(0xC0, 0, n is >= 1 and <= 256 ? n & 0xFF : throw Error("loop count outside 1–256"));
                    break;
                case ':':
                    if (Peek() == '|')
                    {
                        _p++;
                        Emit(0xC0, 1, 0);
                    }
                    break; // a ':' not before '|' is skipped (0x1020)
                case 'R':
                    NoteEvent(0x7F);
                    break;
                case >= 'A' and <= 'G':
                    var index = NoteSemitone[c - 'A'] + Accidental() + _octave * 12;
                    NoteEvent(index is >= 0 and < 96 ? KeyCode(index) : throw Error("note outside the 8 octaves"));
                    break;
                default:
                    throw Error($"'{c}' is not handled");
            }
        }
    }

    private static int WholeOver(int n) =>
        n is >= 1 and <= 64 ? OpmDrvScore.WholeNote / n : throw new InvalidDataException($"OPMDRV MML: length {n} outside 1–64.");

    private int Accidental()
    {
        SkipSpaces();
        switch (Peek())
        {
            case '#' or '+':
                _p++;
                return 1;
            case '-':
                _p++;
                return -1;
            default:
                return 0;
        }
    }

    private void NoteEvent(int code)
    {
        int len, add;
        if (AtDigit())
            len = add = WholeOver(Number());
        else
            len = add = _length;
        for (var dots = 0; dots < 3; dots++)
        {
            SkipSpaces();
            if (Peek() != '.')
                break;
            _p++;
            add >>= 1;
            len += add;
        }
        len &= 0xFF; // stored as a byte
        Emit(code, len, Gate(len, _q));
    }

    /// <summary><c>0x133e</c>: ⌊length · q / 8⌋ − 1, at least 0.</summary>
    public static int Gate(int length, int q)
    {
        var g = length * q / 8 & 0xFF;
        return g == 0 ? 0 : g - 1;
    }

    private void Voice()
    {
        if (AtDigit())
        {
            var n = Number();
            Emit(0xA0, n is >= 1 and <= 200 ? n : throw Error("voice outside 1–200"), 0);
            return;
        }
        var c = Peek();
        _p++;
        switch (c)
        {
            case 'V':
                var v = AtDigit() ? Number() : throw Error("@v without a number");
                Emit(0x80, v <= 127 ? 127 - v : throw Error("@v above 127"), 0);
                break;
            case 'L':
                var l = AtDigit() ? Number() : throw Error("@l without a number");
                _length = l is >= 1 and <= 192 ? l : throw Error("@l outside 1–192");
                break;
            case 'W':
                var w = AtDigit() ? WholeOver(Number()) : _length;
                Emit(0xD0, w, w);
                break;
            default:
                throw Error($"@{c} is not handled");
        }
    }
}

/// <summary>
/// OPMDRV.X's sequencer (the timer A interrupt <c>0x1c9e</c>, per channel <c>0x1da6</c>) on a compiled
/// <see cref="OpmDrvScore"/> `[C]`. Each <see cref="Pass"/> is one run over the 8 channels:
/// <list type="bullet">
/// <item>An active channel with its length counter at 0 (just started) reads events; otherwise the counter counts
/// down and at 0 reads; else the gate counts down and at 0 (or when already 0) keys the channel off.</item>
/// <item>Reading runs commands until a note, rest or wait: note (KC, len, gate) → register 0x28+ch = KC, key on
/// (0x08 = slot mask · 8 + ch; with LFO sync, register 0x01 = 2 then 0); no key-off first (a tied note slurs). Rest
/// (0x7F) sets only the counters. 0x80 v: attenuation a, carrier TLs := min(127, TL + a). 0x90 tempo. 0xA0 n: voice
/// n − 1 into the channel (<c>0x249e</c>). 0xB0 r, v: register write. 0xC0 0 / 1: loop start / end. 0xD0 n: wait.
/// 0xE0 n: register 0x20+ch = n · 64 + FL/CON. 0xFF: key off, the channel stops.</item>
/// <item>Voice load (<c>0x249e</c>), 55-byte voice v: 0x20+ch = pan · 64 | v0 (FL/CON); 0x38+ch = PMS · 16 + AMS;
/// 0x18 = LFRQ; 0x19 = PMD | 0x80; 0x19 = AMD; 0x1B = waveform &amp; 3 (the LFO is global: the last voice loaded on any
/// channel sets it); per operator at slot offsets 0, 16, 8, 24: 0x40 DT1 · 16 + MUL, 0x60 TL (+ the channel's
/// attenuation on a carrier of the algorithm, table <c>0x280e</c>, at most 127), 0x80 KS · 64 + AR, 0xA0 AME · 128 +
/// D1R, 0xC0 DT2 · 64 + D2R, 0xE0 D1L · 16 + RR. KF is never written by a song (<c>_m_init</c> sets 0x30–0x37 = 0x14,
/// KF 5).</item>
/// </list>
/// <c>_m_play</c> clears each played channel's counters and sets attenuation 21 (v 8). The carrier masks of a channel
/// before its first voice event are the driver's built-in voice 1 (<c>_m_init</c>); here they are empty, so a <c>v</c>
/// before the first <c>@</c> writes no TL, where the X68000 writes voice 1's carrier TL (s117 XEiJ trace: song 0's
/// header <c>v9</c> sets OP4's TL of channels 0–2 to 18). No difference in sound: Phantasie IV's songs load a voice
/// before every first note, which rewrites every TL. The first pass runs at <see cref="OpmDrvScore.Start"/>: in the
/// s117 XEiJ boot it came on the first timer A interrupt after <c>_m_play</c> (the driver's 4-interrupt counter runs
/// free from <c>CONFIG.SYS</c>, so on a real machine 0–3 interrupts later `[~]`). s156: a pass that loads a voice
/// outruns a timer period and loses the interrupt that came meanwhile (<see cref="LostInterrupts"/>: 7 a round in song
/// 0, +0.09 %, <c>FORMAT.md</c> §6.11.3).
/// </summary>
public sealed class OpmDrvSequencer
{
    // 0x280e: algorithm → carrier flags of OP1 (M1), OP2 (C1), OP3 (M2), OP4 (C2).
    private static readonly bool[][] Carriers =
    [
        [false, false, false, true], [false, false, false, true], [false, false, false, true], [false, false, false, true],
        [false, true, false, true], [false, true, true, true], [false, true, true, true], [true, true, true, true],
    ];

    /// <summary>OPM slot offset of voice operator k (M1, C1, M2, C2).</summary>
    public static readonly int[] SlotOffset = [0, 16, 8, 24];

    private readonly OpmDrvScore _score;
    private readonly Channel[] _ch = new Channel[OpmDrvScore.Channels];
    private int _tempo = OpmDrvScore.DefaultTempo;

    private sealed class Channel
    {
        public byte[]? Data;
        public int Ptr, Length, Gate, Attenuation = 0x15;
        public bool Active;
        public int LoopStart, LoopCount;
        public bool InLoop;
        public readonly int[] VoiceTl = new int[4];
        public readonly bool[] Carrier = new bool[4];
        public int SlotMask, Sync, FlCon;
    }

    internal OpmDrvSequencer(OpmDrvScore score)
    {
        _score = score;
        for (var i = 0; i < _ch.Length; i++)
        {
            _ch[i] = new Channel { Data = score.Tracks[i] };
            _ch[i].Active = _ch[i].Data is not null;
        }
        CyclesToNextPass = PassCycles(_tempo, _tempo);
    }

    /// <summary>True once every channel has read its end.</summary>
    public bool Finished => _ch.All(c => !c.Active);

    public int Tempo => _tempo;

    /// <summary>OPM clocks from the last <see cref="Pass"/> to the next.</summary>
    public long CyclesToNextPass { get; private set; }

    /// <summary>
    /// Timer A clocks per interrupt at tempo t: 64 · ⌊19531 / t⌋. A pass waits for 4 interrupts; the interval running
    /// when the tempo changes was loaded at the previous overflow (old tempo), the next three use the new value `[~]`.
    /// </summary>
    public static long PassCycles(int oldTempo, int newTempo) => 64L * (19531 / oldTempo) + 3 * 64L * (19531 / newTempo);

    /// <summary><c>_m_init</c>'s writes that a song keeps: KF = 0x14 on all channels, all keys off.</summary>
    public void InitialWrites(List<OpmWrite> writes)
    {
        for (var ch = 0; ch < OpmDrvScore.Channels; ch++)
            writes.Add(new OpmWrite(0x30 + ch, 0x14));
        for (var ch = 0; ch < OpmDrvScore.Channels; ch++)
            writes.Add(new OpmWrite(0x08, ch));
    }

    /// <summary>One sequencer pass over the 8 channels; the register writes it makes are added in order.</summary>
    public void Pass(List<OpmWrite> writes)
    {
        var before = _tempo;
        _loadedVoice = false;
        for (var i = 0; i < _ch.Length; i++)
        {
            var c = _ch[i];
            if (!c.Active)
                continue;
            if (c.Length == 0 || --c.Length == 0)
                Read(i, c, writes);
            else if (c.Gate == 0 || --c.Gate == 0)
                writes.Add(new OpmWrite(0x08, i));
        }
        var lost = LostInterrupts(_firstPass, _loadedVoice);
        _firstPass = false;
        CyclesToNextPass = PassCycles(before, _tempo) + lost * 64L * (19531 / _tempo);
    }

    /// <summary>
    /// s156: timer A interrupts a pass loses (FORMAT.md section 6.11.3, the s117 XEiJ trace): a pass that loads a voice
    /// takes longer than a timer period (each chip write waits on the busy flag, ~61 µs) and the handler's closing
    /// 0x14 = 0x15 clears the overflow that came meanwhile, so the next pass comes one period later; the song's first
    /// pass (its header writes and two voice loads, 5.9 ms) loses 2. Measured for song 0 (7 a round + 1); other songs `[~]`
    /// (not needed: the remaster plays no IV song but song 0, the title).
    /// </summary>
    public static int LostInterrupts(bool firstPass, bool loadedVoice) => !loadedVoice ? 0 : firstPass ? 2 : 1;

    private bool _loadedVoice, _firstPass = true;

    private void Read(int ch, Channel c, List<OpmWrite> writes)
    {
        var d = c.Data!;
        for (var guard = 0; guard < 1 << 20; guard++)
        {
            int b = d[c.Ptr], b1 = c.Ptr + 1 < d.Length ? d[c.Ptr + 1] : 0, b2 = c.Ptr + 2 < d.Length ? d[c.Ptr + 2] : 0;
            if (b < 0x80)
            {
                c.Length = b1;
                c.Gate = b2;
                if (b != 0x7F)
                {
                    writes.Add(new OpmWrite(0x28 + ch, b));
                    writes.Add(new OpmWrite(0x08, c.SlotMask << 3 | ch));
                    if (c.Sync != 0)
                    {
                        writes.Add(new OpmWrite(0x01, 2));
                        writes.Add(new OpmWrite(0x01, 0));
                    }
                }
                c.Ptr += 3;
                return;
            }
            if (b == 0xFF)
            {
                writes.Add(new OpmWrite(0x08, ch));
                c.Active = false;
                return;
            }
            switch (b & 0xF0)
            {
                case 0x80:
                    c.Attenuation = b1;
                    for (var k = 0; k < 4; k++)
                        if (c.Carrier[k])
                            writes.Add(new OpmWrite(0x60 + SlotOffset[k] + ch, Math.Min(127, (c.VoiceTl[k] + c.Attenuation) & 0xFF)));
                    break;
                case 0x90:
                    _tempo = b1;
                    break;
                case 0xA0:
                    LoadVoice(ch, c, _score.Voices[(b1 - 1) & 0xFF], writes);
                    _loadedVoice = true;
                    break;
                case 0xB0:
                    writes.Add(new OpmWrite(b1, b2));
                    break;
                case 0xC0:
                    if (b1 == 0)
                    {
                        c.LoopStart = c.Ptr + 3;
                        c.LoopCount = b2;
                        c.InLoop = true;
                    }
                    else if (b1 == 1)
                    {
                        if (c.InLoop)
                        {
                            c.LoopCount = (c.LoopCount - 1) & 0xFF;
                            if (c.LoopCount != 0)
                            {
                                c.Ptr = c.LoopStart;
                                continue;
                            }
                            c.InLoop = false;
                        }
                    }
                    else
                        throw new InvalidDataException($"OPMDRV: loop command C0 {b1:x2} is not handled [?].");
                    break;
                case 0xD0:
                    c.Length = c.Gate = b1;
                    c.Ptr += 3;
                    return;
                case 0xE0:
                    writes.Add(new OpmWrite(0x20 + ch, (b1 << 6 | c.FlCon) & 0xFF));
                    break;
            }
            c.Ptr += 3;
        }
        throw new InvalidDataException("OPMDRV: a loop with no note.");
    }

    private static void LoadVoice(int ch, Channel c, byte[] v, List<OpmWrite> writes)
    {
        // 0x2752: the channel keeps the TLs, carrier flags, slot mask, sync flag and FL/CON.
        for (var k = 0; k < 4; k++)
        {
            c.VoiceTl[k] = v[11 + 11 * k + 5];
            c.Carrier[k] = Carriers[v[0] & 7][k];
        }
        c.SlotMask = v[1] & 0x0F;
        c.Sync = v[3];
        c.FlCon = v[0];
        writes.Add(new OpmWrite(0x20 + ch, (v[9] << 6 | v[0]) & 0xFF));
        writes.Add(new OpmWrite(0x38 + ch, (v[7] << 4 | v[8]) & 0xFF));
        writes.Add(new OpmWrite(0x18, v[4]));
        writes.Add(new OpmWrite(0x19, v[5] | 0x80));
        writes.Add(new OpmWrite(0x19, v[6] & 0x7F));
        writes.Add(new OpmWrite(0x1B, v[2] & 3));
        // The driver's order: 0x40 for the four operators, then 0x60, 0x80, 0xA0, 0xC0, 0xE0.
        for (var k = 0; k < 4; k++)
            writes.Add(new OpmWrite(0x40 + SlotOffset[k] + ch, (Op(v, k, 8) << 4 | Op(v, k, 7)) & 0xFF));
        for (var k = 0; k < 4; k++)
        {
            var tl = Op(v, k, 5);
            if (c.Carrier[k])
                tl = Math.Min(127, (tl + c.Attenuation) & 0xFF);
            writes.Add(new OpmWrite(0x60 + SlotOffset[k] + ch, tl));
        }
        for (var k = 0; k < 4; k++)
            writes.Add(new OpmWrite(0x80 + SlotOffset[k] + ch, (Op(v, k, 6) << 6 | Op(v, k, 0)) & 0xFF));
        for (var k = 0; k < 4; k++)
            writes.Add(new OpmWrite(0xA0 + SlotOffset[k] + ch, (Op(v, k, 10) << 7 | Op(v, k, 1)) & 0xFF));
        for (var k = 0; k < 4; k++)
            writes.Add(new OpmWrite(0xC0 + SlotOffset[k] + ch, (Op(v, k, 9) << 6 | Op(v, k, 2)) & 0xFF));
        for (var k = 0; k < 4; k++)
            writes.Add(new OpmWrite(0xE0 + SlotOffset[k] + ch, (Op(v, k, 4) << 4 | Op(v, k, 3)) & 0xFF));
    }

    private static int Op(byte[] v, int k, int param) => v[11 + 11 * k + param];
}
