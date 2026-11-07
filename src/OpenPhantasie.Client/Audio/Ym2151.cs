namespace OpenPhantasie.Client.Audio;

/// <summary>
/// A model of Yamaha's YM2151 (OPM) for Phantasie IV's title tune (s109): 8 channels of 4 operators, the 8 algorithms,
/// operator 1 self-feedback, the 4-stage envelope with key scaling and the chip's rate tables, DT1 / DT2 / MUL, key code
/// + key fraction pitch, the LFO (4 waveforms, AM and PM depth, per-channel sensitivity) and the YM3012 DAC's
/// 10-bit-mantissa output. One output sample per 64 clocks (62.5 kHz at the X68000's 4 MHz).
/// <para>
/// The operator, envelope, algorithm and LFO logic follows the published behaviour of the chip as implemented by
/// Aaron Giles' <b>ymfm</b> (BSD 3-clause, https://github.com/aaronsgiles/ymfm), re-expressed here, not translated
/// line by line; the log-sine and exponent tables are the formulas of the die-shot analyses (Matthew Gambrell &amp;
/// Olli Niemitalo) that ymfm's tables hold (checked equal to ymfm's), the envelope increment, DT1 and phase-step tables
/// are ymfm's. `[~]`: the LFO's waveforms and rate are ymfm's reading of the manual; the noise generator (register 0x0F)
/// and the timers are not modelled (OPMDRV does not use noise, and the timers only pace the driver, which the score
/// does); a key-on write acts at once (ymfm: at its next sample), not at the chip's exact slot time.
/// </para>
/// No MonoGame types: tests render it headless.
/// </summary>
public sealed class Ym2151
{
    public const int ClocksPerSample = 64;

    private const int EnvQuiet = 0x380;

    private static readonly ushort[] SinTable = BuildSin();
    private static readonly ushort[] PowerTable = BuildPower();
    /// <summary>
    /// The phase step (20-bit phase per sample, MUL 1) of each 1/64 semitone of block 7, from C# up: the chip's own
    /// values as measured by David Viens, from ymfm's <c>opm_key_code_to_phase_step</c> (BSD 3-clause, Copyright (c)
    /// 2021, Aaron Giles; used in ymfm with Viens' permission). Lower blocks shift right by 7 − block.
    /// </summary>
    private static readonly int[] PhaseStepTable =
    [
        41568, 41600, 41632, 41664, 41696, 41728, 41760, 41792, 41856, 41888, 41920, 41952, 42016, 42048, 42080, 42112,
        42176, 42208, 42240, 42272, 42304, 42336, 42368, 42400, 42464, 42496, 42528, 42560, 42624, 42656, 42688, 42720,
        42784, 42816, 42848, 42880, 42912, 42944, 42976, 43008, 43072, 43104, 43136, 43168, 43232, 43264, 43296, 43328,
        43392, 43424, 43456, 43488, 43552, 43584, 43616, 43648, 43712, 43744, 43776, 43808, 43872, 43904, 43936, 43968,
        44032, 44064, 44096, 44128, 44192, 44224, 44256, 44288, 44352, 44384, 44416, 44448, 44512, 44544, 44576, 44608,
        44672, 44704, 44736, 44768, 44832, 44864, 44896, 44928, 44992, 45024, 45056, 45088, 45152, 45184, 45216, 45248,
        45312, 45344, 45376, 45408, 45472, 45504, 45536, 45568, 45632, 45664, 45728, 45760, 45792, 45824, 45888, 45920,
        45984, 46016, 46048, 46080, 46144, 46176, 46208, 46240, 46304, 46336, 46368, 46400, 46464, 46496, 46528, 46560,
        46656, 46688, 46720, 46752, 46816, 46848, 46880, 46912, 46976, 47008, 47072, 47104, 47136, 47168, 47232, 47264,
        47328, 47360, 47392, 47424, 47488, 47520, 47552, 47584, 47648, 47680, 47744, 47776, 47808, 47840, 47904, 47936,
        48032, 48064, 48096, 48128, 48192, 48224, 48288, 48320, 48384, 48416, 48448, 48480, 48544, 48576, 48640, 48672,
        48736, 48768, 48800, 48832, 48896, 48928, 48992, 49024, 49088, 49120, 49152, 49184, 49248, 49280, 49344, 49376,
        49440, 49472, 49504, 49536, 49600, 49632, 49696, 49728, 49792, 49824, 49856, 49888, 49952, 49984, 50048, 50080,
        50144, 50176, 50208, 50240, 50304, 50336, 50400, 50432, 50496, 50528, 50560, 50592, 50656, 50688, 50752, 50784,
        50880, 50912, 50944, 50976, 51040, 51072, 51136, 51168, 51232, 51264, 51328, 51360, 51424, 51456, 51488, 51520,
        51616, 51648, 51680, 51712, 51776, 51808, 51872, 51904, 51968, 52000, 52064, 52096, 52160, 52192, 52224, 52256,
        52384, 52416, 52448, 52480, 52544, 52576, 52640, 52672, 52736, 52768, 52832, 52864, 52928, 52960, 52992, 53024,
        53120, 53152, 53216, 53248, 53312, 53344, 53408, 53440, 53504, 53536, 53600, 53632, 53696, 53728, 53792, 53824,
        53920, 53952, 54016, 54048, 54112, 54144, 54208, 54240, 54304, 54336, 54400, 54432, 54496, 54528, 54592, 54624,
        54688, 54720, 54784, 54816, 54880, 54912, 54976, 55008, 55072, 55104, 55168, 55200, 55264, 55296, 55360, 55392,
        55488, 55520, 55584, 55616, 55680, 55712, 55776, 55808, 55872, 55936, 55968, 56032, 56064, 56128, 56160, 56224,
        56288, 56320, 56384, 56416, 56480, 56512, 56576, 56608, 56672, 56736, 56768, 56832, 56864, 56928, 56960, 57024,
        57120, 57152, 57216, 57248, 57312, 57376, 57408, 57472, 57536, 57568, 57632, 57664, 57728, 57792, 57824, 57888,
        57952, 57984, 58048, 58080, 58144, 58208, 58240, 58304, 58368, 58400, 58464, 58496, 58560, 58624, 58656, 58720,
        58784, 58816, 58880, 58912, 58976, 59040, 59072, 59136, 59200, 59232, 59296, 59328, 59392, 59456, 59488, 59552,
        59648, 59680, 59744, 59776, 59840, 59904, 59936, 60000, 60064, 60128, 60160, 60224, 60288, 60320, 60384, 60416,
        60512, 60544, 60608, 60640, 60704, 60768, 60800, 60864, 60928, 60992, 61024, 61088, 61152, 61184, 61248, 61280,
        61376, 61408, 61472, 61536, 61600, 61632, 61696, 61760, 61824, 61856, 61920, 61984, 62048, 62080, 62144, 62208,
        62272, 62304, 62368, 62432, 62496, 62528, 62592, 62656, 62720, 62752, 62816, 62880, 62944, 62976, 63040, 63104,
        63200, 63232, 63296, 63360, 63424, 63456, 63520, 63584, 63648, 63680, 63744, 63808, 63872, 63904, 63968, 64032,
        64096, 64128, 64192, 64256, 64320, 64352, 64416, 64480, 64544, 64608, 64672, 64704, 64768, 64832, 64896, 64928,
        65024, 65056, 65120, 65184, 65248, 65312, 65376, 65408, 65504, 65536, 65600, 65664, 65728, 65792, 65856, 65888,
        65984, 66016, 66080, 66144, 66208, 66272, 66336, 66368, 66464, 66496, 66560, 66624, 66688, 66752, 66816, 66848,
        66944, 66976, 67040, 67104, 67168, 67232, 67296, 67328, 67424, 67456, 67520, 67584, 67648, 67712, 67776, 67808,
        67904, 67936, 68000, 68064, 68128, 68192, 68256, 68288, 68384, 68448, 68512, 68544, 68640, 68672, 68736, 68800,
        68896, 68928, 68992, 69056, 69120, 69184, 69248, 69280, 69376, 69440, 69504, 69536, 69632, 69664, 69728, 69792,
        69920, 69952, 70016, 70080, 70144, 70208, 70272, 70304, 70400, 70464, 70528, 70560, 70656, 70688, 70752, 70816,
        70912, 70976, 71040, 71104, 71136, 71232, 71264, 71360, 71424, 71488, 71552, 71616, 71648, 71744, 71776, 71872,
        71968, 72032, 72096, 72160, 72192, 72288, 72320, 72416, 72480, 72544, 72608, 72672, 72704, 72800, 72832, 72928,
        72992, 73056, 73120, 73184, 73216, 73312, 73344, 73440, 73504, 73568, 73632, 73696, 73728, 73824, 73856, 73952,
        74080, 74144, 74208, 74272, 74304, 74400, 74432, 74528, 74592, 74656, 74720, 74784, 74816, 74912, 74944, 75040,
        75136, 75200, 75264, 75328, 75360, 75456, 75488, 75584, 75648, 75712, 75776, 75840, 75872, 75968, 76000, 76096,
        76224, 76288, 76352, 76416, 76448, 76544, 76576, 76672, 76736, 76800, 76864, 76928, 77024, 77120, 77152, 77248,
        77344, 77408, 77472, 77536, 77568, 77664, 77696, 77792, 77856, 77920, 77984, 78048, 78144, 78240, 78272, 78368,
        78464, 78528, 78592, 78656, 78688, 78784, 78816, 78912, 78976, 79040, 79104, 79168, 79264, 79360, 79392, 79488,
        79616, 79680, 79744, 79808, 79840, 79936, 79968, 80064, 80128, 80192, 80256, 80320, 80416, 80512, 80544, 80640,
        80768, 80832, 80896, 80960, 80992, 81088, 81120, 81216, 81280, 81344, 81408, 81472, 81568, 81664, 81696, 81792,
        81952, 82016, 82080, 82144, 82176, 82272, 82304, 82400, 82464, 82528, 82592, 82656, 82752, 82848, 82880, 82976,
    ];

    // The DT1 adjustment by key code (5 bits) and DT1 & 3, in phase-step units (the OPN / OPM data sheets).
    private static readonly byte[,] DetuneTable =
    {
        { 0, 0, 1, 2 }, { 0, 0, 1, 2 }, { 0, 0, 1, 2 }, { 0, 0, 1, 2 }, { 0, 1, 2, 2 }, { 0, 1, 2, 3 }, { 0, 1, 2, 3 },
        { 0, 1, 2, 3 }, { 0, 1, 2, 4 }, { 0, 1, 3, 4 }, { 0, 1, 3, 4 }, { 0, 1, 3, 5 }, { 0, 2, 4, 5 }, { 0, 2, 4, 6 },
        { 0, 2, 4, 6 }, { 0, 2, 5, 7 }, { 0, 2, 5, 8 }, { 0, 3, 6, 8 }, { 0, 3, 6, 9 }, { 0, 3, 7, 10 }, { 0, 4, 8, 11 },
        { 0, 4, 8, 12 }, { 0, 4, 9, 13 }, { 0, 5, 10, 14 }, { 0, 5, 11, 16 }, { 0, 6, 12, 17 }, { 0, 6, 13, 19 },
        { 0, 7, 14, 20 }, { 0, 8, 16, 22 }, { 0, 8, 16, 22 }, { 0, 8, 16, 22 }, { 0, 8, 16, 22 },
    };

    // Envelope increments: rate → 8 increments (4 bits each, low nibble first), the chip's attenuation step pattern.
    private static readonly uint[] IncrementTable =
    [
        0x00000000, 0x00000000, 0x10101010, 0x10101010, 0x10101010, 0x10101010, 0x11101110, 0x11101110,
        0x10101010, 0x10111010, 0x11101110, 0x11111110, 0x10101010, 0x10111010, 0x11101110, 0x11111110,
        0x10101010, 0x10111010, 0x11101110, 0x11111110, 0x10101010, 0x10111010, 0x11101110, 0x11111110,
        0x10101010, 0x10111010, 0x11101110, 0x11111110, 0x10101010, 0x10111010, 0x11101110, 0x11111110,
        0x10101010, 0x10111010, 0x11101110, 0x11111110, 0x10101010, 0x10111010, 0x11101110, 0x11111110,
        0x10101010, 0x10111010, 0x11101110, 0x11111110, 0x10101010, 0x10111010, 0x11101110, 0x11111110,
        0x11111111, 0x21112111, 0x21212121, 0x22212221, 0x22222222, 0x42224222, 0x42424242, 0x44424442,
        0x44444444, 0x84448444, 0x84848484, 0x88848884, 0x88888888, 0x88888888, 0x88888888, 0x88888888,
    ];

    // DT2: +0, +600, +781, +950 cents, in 1/64 semitones.
    private static readonly int[] Detune2 = [0, 384, 500, 608];

    // Per algorithm: op2 input (0 none / 1 op1), op3 input (0, 1 op1, 2 op2, 5 op1+op2), op4 input (0, 1, 2, 3 op3,
    // 6 op1+op3, 7 op2+op3), and whether op1, op2, op3 also go to the output (op4 always does).
    private static readonly (int In2, int In3, int In4, bool Out1, bool Out2, bool Out3)[] Algorithms =
    [
        (1, 2, 3, false, false, false),
        (0, 5, 3, false, false, false),
        (0, 2, 6, false, false, false),
        (1, 0, 7, false, false, false),
        (1, 0, 3, false, true, false),
        (1, 1, 1, false, true, true),
        (1, 0, 0, false, true, true),
        (0, 0, 0, true, true, true),
    ];

    private enum EnvState { Attack, Decay, Sustain, Release }

    private sealed class Operator
    {
        public uint Phase;
        public int Env = 0x3FF;
        public EnvState State = EnvState.Release;
        public bool KeyOn;
        public uint Step; // cached phase step without LFO PM
    }

    private readonly byte[] _reg = new byte[256];
    private readonly Operator[] _op = new Operator[32];
    private readonly int[] _fb0 = new int[8], _fb1 = new int[8], _fbIn = new int[8];
    private uint _envCounter;
    private uint _lfoCounter;
    private int _lfoAm, _lfoPm;
    private int _pmd, _amd;
    private bool _stepsDirty = true;

    public Ym2151()
    {
        for (var i = 0; i < _op.Length; i++)
            _op[i] = new Operator();
    }

    public int Register(int r) => _reg[r & 0xFF];

    private static ushort[] BuildSin()
    {
        var t = new ushort[256];
        for (var i = 0; i < 256; i++)
            t[i] = (ushort)Math.Round(-Math.Log2(Math.Sin((2 * i + 1) * Math.PI / 1024)) * 256);
        return t;
    }

    private static ushort[] BuildPower()
    {
        var t = new ushort[256];
        for (var i = 0; i < 256; i++)
            t[i] = (ushort)Math.Round((Math.Pow(2, (255 - i) / 256.0) - 1) * 1024);
        return t;
    }

    /// <summary>log-sine attenuation (4.8 fixed) of a 10-bit phase, bit 15 = the negative half.</summary>
    private static int SinAttenuation(int phase)
    {
        var i = phase & 0x3FF;
        var q = (i & 0x100) != 0 ? ~i & 0xFF : i & 0xFF;
        return SinTable[q] | ((i & 0x200) != 0 ? 0x8000 : 0);
    }

    /// <summary>Attenuation (5.8 fixed, 0 = loudest) to a 13-bit linear level.</summary>
    private static int AttenuationToVolume(int a) => a >> 8 >= 32 ? 0 : ((PowerTable[a & 0xFF] | 0x400) << 2) >> (a >> 8);

    public void Write(int register, int value)
    {
        register &= 0xFF;
        value &= 0xFF;
        if (register == 0x19)
        {
            if ((value & 0x80) != 0)
                _pmd = value & 0x7F;
            else
                _amd = value & 0x7F;
        }
        _reg[register] = (byte)value;
        if (register is >= 0x28 and < 0x60 or >= 0xC0 and < 0xE0)
            _stepsDirty = true;
        if (register == 0x08)
        {
            var ch = value & 7;
            for (var k = 0; k < 4; k++)
                KeyOnOff(_op[ch + OpSlot(k)], ch, OpSlot(k), (value >> (3 + k) & 1) != 0);
        }
    }

    /// <summary>Slot offset of channel operator k = OP1 (M1), OP2 (C1), OP3 (M2), OP4 (C2).</summary>
    private static int OpSlot(int k) => k switch { 0 => 0, 1 => 16, 2 => 8, _ => 24 };

    private int KeyCode5(int ch) => _reg[0x28 + ch] >> 2 & 0x1F;

    private int EffectiveRate(int raw, int slot, int ch)
    {
        if (raw == 0)
            return 0;
        var ks = _reg[0x80 + slot] >> 6;
        return Math.Min(63, raw + (KeyCode5(ch) >> (ks ^ 3)));
    }

    private int Rate(EnvState s, int slot, int ch) => s switch
    {
        EnvState.Attack => EffectiveRate((_reg[0x80 + slot] & 0x1F) * 2, slot, ch),
        EnvState.Decay => EffectiveRate((_reg[0xA0 + slot] & 0x1F) * 2, slot, ch),
        EnvState.Sustain => EffectiveRate((_reg[0xC0 + slot] & 0x1F) * 2, slot, ch),
        _ => EffectiveRate((_reg[0xE0 + slot] & 0x0F) * 4 + 2, slot, ch),
    };

    private void KeyOnOff(Operator op, int ch, int slotBase, bool on)
    {
        var slot = slotBase + ch;
        if (on && !op.KeyOn)
        {
            if (op.State != EnvState.Attack)
            {
                op.State = EnvState.Attack;
                op.Phase = 0;
                if (Rate(EnvState.Attack, slot, ch) >= 62)
                    op.Env = 0;
            }
        }
        else if (!on && op.KeyOn)
            op.State = EnvState.Release;
        op.KeyOn = on;
    }

    private void ClockEnvelope(Operator op, int slot, int ch, uint counter)
    {
        if (op.State == EnvState.Attack && op.Env == 0)
            op.State = EnvState.Decay;
        var d1l = _reg[0xE0 + slot] >> 4;
        var sustain = (d1l | ((d1l + 1) & 0x10)) << 5;
        if (op.State == EnvState.Decay && op.Env >= sustain)
            op.State = EnvState.Sustain;
        var rate = Rate(op.State, slot, ch);
        var shift = rate >> 2;
        var c = counter << shift;
        if ((c & 0x7FF) != 0)
            return;
        var bits = (int)(c >> (shift <= 11 ? 11 : shift) & 7);
        var inc = (int)(IncrementTable[rate] >> (4 * bits) & 0xF);
        if (op.State == EnvState.Attack)
        {
            if (rate < 62)
                op.Env += (~op.Env * inc) >> 4;
        }
        else
        {
            op.Env += inc;
            if (op.Env >= 0x400)
                op.Env = 0x3FF;
        }
    }

    /// <summary>
    /// Block (3 bits), key code (4 bits, "gappy": codes 3, 7, 11 unused) and key fraction (6 bits) plus a delta in
    /// 1/64 semitones → the MUL 1 phase step, over- and underflow moving the block (ymfm's rule).
    /// </summary>
    private static int KeyCodeToPhaseStep(int blockFreq, int delta)
    {
        var block = blockFreq >> 10 & 7;
        var code = (blockFreq >> 6 & 15) - (blockFreq >> 8 & 3);
        var eff = (code << 6 | blockFreq & 63) + delta;
        if ((uint)eff >= 768)
        {
            if (eff < 0)
            {
                eff += 768;
                if (block-- == 0)
                    return PhaseStepTable[0] >> 7;
            }
            else
            {
                eff -= 768;
                if (eff >= 768)
                {
                    block++;
                    eff -= 768;
                }
                if (block++ >= 7)
                    return PhaseStepTable[767];
            }
        }
        return PhaseStepTable[eff] >> (block ^ 7);
    }

    /// <summary>Phase step (20-bit phase per sample) of a slot: key code + fraction + DT2 + LFO PM, DT1, MUL.</summary>
    private uint PhaseStep(int slot, int ch, int pmDelta)
    {
        var step = KeyCodeToPhaseStep((_reg[0x28 + ch] & 0x7F) << 6 | _reg[0x30 + ch] >> 2,
            Detune2[_reg[0xC0 + slot] >> 6] + pmDelta);
        var dt1 = _reg[0x40 + slot] >> 4 & 7;
        var adjust = DetuneTable[KeyCode5(ch), dt1 & 3];
        var s = (uint)(step + ((dt1 & 4) != 0 ? -adjust : adjust));
        var mul = _reg[0x40 + slot] & 15;
        return mul == 0 ? s >> 1 : s * (uint)mul;
    }

    private void ClockLfo()
    {
        var rate = _reg[0x18];
        _lfoCounter += (uint)((0x10 | (rate & 15)) << (rate >> 4));
        if ((_reg[0x01] & 2) != 0)
            _lfoCounter = 0;
        var lfo = (int)(_lfoCounter >> 22 & 0xFF);
        int am, pm;
        switch (_reg[0x1B] & 3)
        {
            case 0: // sawtooth
                am = lfo ^ 0xFF;
                pm = lfo;
                break;
            case 1: // square
                am = (lfo & 0x80) != 0 ? 0 : 0xFF;
                pm = am ^ 0x80;
                break;
            case 2: // triangle
                am = ((lfo & 0x80) != 0 ? lfo << 1 : (lfo ^ 0xFF) << 1) & 0xFF;
                pm = (lfo & 0x40) != 0 ? am : ~am & 0xFF; // as a signed byte: 0 → 127 → 0 → −128 → 0
                break;
            default: // noise: not modelled, held
                am = 0;
                pm = 0x80;
                break;
        }
        _lfoAm = (am & 0xFF) * _amd >> 7;
        _lfoPm = (((pm & 0xFF) ^ 0x80) - 0x80) * _pmd >> 7; // signed −128…127
    }

    /// <summary>One output sample (left, right) as 16-bit values.</summary>
    public void Clock(out int left, out int right)
    {
        ClockLfo();
        // The envelope runs on every third sample (ymfm: the counter steps first, then the operators see it).
        if (((++_envCounter) & 3) == 3)
            _envCounter++;
        var clockEg = (_envCounter & 3) == 0;
        var egCount = _envCounter >> 2;
        left = right = 0;
        if (_stepsDirty)
        {
            for (var slot = 0; slot < 32; slot++)
                _op[slot].Step = PhaseStep(slot, slot & 7, 0);
            _stepsDirty = false;
        }
        for (var ch = 0; ch < 8; ch++)
        {
            _fb0[ch] = _fb1[ch];
            _fb1[ch] = _fbIn[ch];
            var pms = _reg[0x38 + ch] >> 4 & 7;
            var pmDelta = pms == 0 ? 0 : pms < 6 ? _lfoPm >> (6 - pms) : _lfoPm << (pms - 5);
            for (var k = 0; k < 4; k++)
            {
                var slot = OpSlot(k) + ch;
                var op = _op[slot];
                if (clockEg)
                    ClockEnvelope(op, slot, ch, egCount);
                var step = pmDelta == 0 ? op.Step : PhaseStep(slot, ch, pmDelta);
                op.Phase = (op.Phase + step) & 0xFFFFF;
            }
            var v = ChannelOutput(ch);
            var pan = _reg[0x20 + ch];
            if ((pan & 0x40) != 0)
                left += v;
            if ((pan & 0x80) != 0)
                right += v;
        }
        left = Dac(left);
        right = Dac(right);
    }

    private int OpVolume(int slot, int ch, int phase10)
    {
        var op = _op[slot];
        if (op.Env > EnvQuiet)
            return 0;
        var sin = SinAttenuation(phase10);
        var env = op.Env + ((_reg[0x60 + slot] & 0x7F) << 3);
        var ams = _reg[0x38 + ch] & 3;
        if ((_reg[0xA0 + slot] & 0x80) != 0 && ams != 0)
            env += _lfoAm << (ams - 1);
        env = Math.Min(env, 0x3FF);
        var vol = AttenuationToVolume((sin & 0x7FFF) + (env << 2));
        return (sin & 0x8000) != 0 ? -vol : vol;
    }

    private int ChannelOutput(int ch)
    {
        var alg = Algorithms[_reg[0x20 + ch] & 7];
        var fb = _reg[0x20 + ch] >> 3 & 7;
        var mod1 = fb == 0 ? 0 : (_fb0[ch] + _fb1[ch]) >> (10 - fb);
        Span<int> o = stackalloc int[8];
        o[1] = _fbIn[ch] = OpVolume(ch, ch, (int)(_op[ch].Phase >> 10) + mod1);
        o[2] = OpVolume(16 + ch, ch, (int)(_op[16 + ch].Phase >> 10) + (o[alg.In2] >> 1));
        o[5] = o[1] + o[2];
        o[3] = OpVolume(8 + ch, ch, (int)(_op[8 + ch].Phase >> 10) + (o[alg.In3] >> 1));
        o[6] = o[1] + o[3];
        o[7] = o[2] + o[3];
        var result = OpVolume(24 + ch, ch, (int)(_op[24 + ch].Phase >> 10) + (o[alg.In4] >> 1));
        if (alg.Out1)
            result = Math.Clamp(result + o[1], -32768, 32767);
        if (alg.Out2)
            result = Math.Clamp(result + o[2], -32768, 32767);
        if (alg.Out3)
            result = Math.Clamp(result + o[3], -32768, 32767);
        return result;
    }

    /// <summary>The YM3012 DAC: 16-bit clamp, then the bits below its 10-bit mantissa are lost.</summary>
    private static int Dac(int value)
    {
        value = Math.Clamp(value, -32768, 32767);
        var scan = value ^ (value >> 31);
        var exponent = 7 - System.Numerics.BitOperations.LeadingZeroCount((uint)(scan << 17));
        exponent = Math.Max(exponent, 1) - 1;
        return value & ~((1 << exponent) - 1);
    }
}
