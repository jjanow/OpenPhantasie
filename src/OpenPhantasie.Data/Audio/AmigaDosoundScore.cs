namespace OpenPhantasie.Data.Audio;

/// <summary>One Paula channel in one step of <see cref="AmigaDosoundScore"/>: sent this step or not, its period and volume.</summary>
public readonly record struct PaulaNote(bool Sent, int Period, int Volume);

/// <summary>One step of <see cref="AmigaDosoundScore"/>: the four channels it starts, then a wait of <see cref="Ticks"/> dos ticks.</summary>
public sealed record AmigaDosoundStep(IReadOnlyList<PaulaNote> Channels, int Ticks);

/// <summary>
/// Phantasie I Amiga's title tune <c>tamb.dat</c> (2850 B, s108) as the game's own player h89 <c>+0x304</c> plays it: the
/// file is an Atari ST Dosound script (the game came from the ST), but the Amiga build has no PSG and turns each
/// script step into audio.device writes of one 16-byte waveform on the four Paula channels. Static read of
/// <c>game</c> (hunks 89–91), <c>reverse-engineering/amiga/FORMAT.md</c> "Title tune" `[C]`:
/// <list type="bullet">
/// <item>The script is first copied by h89 <c>+0x9bc</c>, which expands each <c>0x81</c> sweep (after <c>0x80</c> set its
/// start) into explicit "reg, value, 0x82, 1" steps; more than 1900 bytes of a sweep fail and nothing plays.</item>
/// <item>Then each command until <c>0x82</c>: regs 0/1, 2/3, 4/5 = the A, B, C periods (low, high byte); reg 7 bit 0 =
/// A off, bit 1 = B off, and any reg 7 write turns channel 1 off for good; regs 8, 9, 10 = volumes (16, the envelope
/// flag, counts as 15), × 4 → 0–60; regs 6 and 11–15 (noise, envelope) and <c>0x80</c> are ignored, <c>0x81</c> skips its
/// three bytes; <c>0x82</c> n = the step's wait, ⌊n·6/5⌋ ticks of <c>Delay()</c> (1/50 s); <c>0xFF</c> or a wait of 0
/// ends the tune.</item>
/// <item>At <c>0x82</c>: Amiga period = ⌊PSG period × 1.7897732⌋ (single-float constant <c>0x3FE5174A</c> =
/// 3 579 545 / 2 000 000, the NTSC Paula clock over the ST's PSG clock; with 16 samples a cycle the pitch is the ST's
/// on an NTSC machine, about 1 % flat on PAL `[~]`). Channel 0 plays B (unless B is off), channel 1 plays B (unless
/// reg 7 was ever written), channels 2 and 3 play A (unless A is off). C is never played.</item>
/// <item>The channels sound for the wait, then each sent channel gets <c>ADCMD_FINISH</c> (it stops at the end of its
/// waveform cycle) and the next step is read. A key or click before a step ends the tune (<c>h80 +0x4e4</c>).</item>
/// </list>
/// </summary>
public sealed class AmigaDosoundScore : TuneScore
{
    /// <summary>dos.library <c>Delay()</c> ticks per second (1/50 s on PAL and NTSC alike).</summary>
    public const int TickHz = 50;

    /// <summary>The PSG → Paula period factor h89 <c>+0x30e</c> loads (IEEE single <c>0x3FE5174A</c>).</summary>
    public static readonly float PeriodFactor = BitConverter.Int32BitsToSingle(0x3FE5174A);

    /// <summary>The longest sweep expansion h89 <c>+0x9bc</c> allows (<c>cmpi.l #1900</c>).</summary>
    public const int ExpansionLimit = 1900;

    private AmigaDosoundScore(IReadOnlyList<AmigaDosoundStep> steps) => Steps = steps;

    /// <summary>
    /// s154: a raw Paula tone (I Amiga's h89 <c>+0x7ca</c>, the defeat door's): the ramp on <paramref name="channel"/> at
    /// Paula <paramref name="period"/> (not scaled from a PSG period) and <paramref name="volume"/> (0–64) for
    /// <paramref name="ticks"/> dos ticks, as one step.
    /// </summary>
    public static AmigaDosoundScore Tone(int channel, int period, int volume, int ticks)
    {
        if (channel is < 0 or > 3 || period <= 0 || ticks <= 0)
            return new AmigaDosoundScore([]);
        var notes = new PaulaNote[4];
        notes[channel] = new PaulaNote(true, period, Math.Clamp(volume, 0, 64));
        return new AmigaDosoundScore([new AmigaDosoundStep(notes, ticks)]);
    }

    /// <summary>The steps in order (empty when the sweep expansion failed or the script ends at once).</summary>
    public IReadOnlyList<AmigaDosoundStep> Steps { get; }

    public int Ticks => Steps.Sum(s => s.Ticks);

    public override double DurationSeconds => (double)Ticks / TickHz;

    /// <summary>
    /// The waveform every channel loops (h89 <c>+0x8ee</c>, written to h91 <c>+0x278</c>, <c>ioa_Length</c> 16): from 127
    /// down by 8 while above 0 = 127, 119, …, 7.
    /// </summary>
    public static sbyte[] Waveform()
    {
        var w = new List<sbyte>();
        for (var v = 127; v > 0; v -= 8)
            w.Add((sbyte)v);
        return [.. w];
    }

    public static AmigaDosoundScore Parse(ReadOnlySpan<byte> file)
    {
        var script = Expand(file);
        var steps = new List<AmigaDosoundStep>();
        if (script is null)
            return new AmigaDosoundScore(steps);

        int periodA = 0, periodB = 0, volA = 0, volB = 0, wait = 0;
        bool aOff = false, bOff = false, ch1Off = false;
        var pc = 0;
        while (true)
        {
            int cmd;
            do
            {
                if (pc >= script.Length)
                    return new AmigaDosoundScore(steps);
                cmd = script[pc++];
                if (cmd == 0xFF)
                    return new AmigaDosoundScore(steps);
                var arg = pc < script.Length ? script[pc] : 0;
                pc++;
                switch (cmd)
                {
                    case 0: periodA = (periodA & 0xFF00) | arg; break;
                    case 1: periodA = (periodA & 0x00FF) | (arg << 8); break;
                    case 2: periodB = (periodB & 0xFF00) | arg; break;
                    case 3: periodB = (periodB & 0x00FF) | (arg << 8); break;
                    case 7:
                        aOff = (arg & 1) != 0;
                        bOff = (arg & 2) != 0;
                        ch1Off = true;
                        break;
                    case 8: volA = Volume(arg); break;
                    case 9: volB = Volume(arg); break;
                    case 0x81: pc += 2; break;
                    case 0x82: wait = arg * 6 / 5; break;
                    // 4/5 (C period), 10 (C volume), 6, 11–15, 0x80: read and dropped.
                }
            } while (cmd != 0x82);
            if (wait == 0)
                return new AmigaDosoundScore(steps);
            var pa = Period(periodA);
            var pb = Period(periodB);
            steps.Add(new AmigaDosoundStep(
            [
                new PaulaNote(!bOff, pb, volB),
                new PaulaNote(!ch1Off, pb, volB),
                new PaulaNote(!aOff, pa, volA),
                new PaulaNote(!aOff, pa, volA),
            ], wait));
        }
    }

    /// <summary>⌊period × 1.7897732⌋ the way the C code does it (float to double, multiply, truncate to a word).</summary>
    public static int Period(int psgPeriod) => (int)((double)psgPeriod * PeriodFactor) & 0xFFFF;

    private static int Volume(int reg) => ((reg == 16 ? 15 : reg) & 15) << 2;

    /// <summary>h89 <c>+0x9bc</c>: copy the script, expanding sweeps; null when a sweep runs past the limit.</summary>
    private static byte[]? Expand(ReadOnlySpan<byte> s)
    {
        var o = new List<byte>();
        int p = 0, sweep = 0; // the sweep start is an uninitialised local until a 0x80 sets it [~]
        while (p < s.Length)
        {
            var b = s[p];
            if (b == 0xFF)
            {
                o.Add(0xFF);
                return [.. o];
            }
            if (b == 0x81)
            {
                if (p + 3 >= s.Length)
                    break;
                int reg = s[p + 1], step = s[p + 2], end = s[p + 3];
                p += 4;
                while (sweep != end)
                {
                    o.AddRange([(byte)reg, (byte)sweep, 0x82, 1]);
                    sweep = (sweep + step) & 0xFF;
                    if (o.Count > ExpansionLimit)
                        return null;
                }
                continue;
            }
            if (b == 0x80)
            {
                sweep = p + 1 < s.Length ? s[p + 1] : 0;
                p += 2;
                continue;
            }
            o.Add(b);
            if (p + 1 < s.Length)
                o.Add(s[p + 1]);
            p += 2;
        }
        return [.. o];
    }
}
