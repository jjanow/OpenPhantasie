namespace OpenPhantasie.Data.Audio;

/// <summary>One write to a YM2149 register at a 50 Hz tick of the Dosound interrupt.</summary>
public readonly record struct PsgWrite(int Tick, int Register, int Value);

/// <summary>
/// An Atari ST XBIOS 32 <c>Dosound</c> script as TOS plays it (Phantasie II's title sound, <c>START.PRG</c> image
/// <c>0x1b3</c>, s108): the YM2149 register writes in order, each at the 50 Hz tick it happens on.
/// <para>
/// The interpreter is TOS's, not the game's: the sound routine runs on every 4th Timer C interrupt (200 Hz / 4 = 50 Hz,
/// whatever the video sync) and, unless a delay is counting down, reads commands: <c>0x00–0x7F</c> + byte = write that
/// register (only 0–15 exist), <c>0x80</c> + byte = load the sweep value, <c>0x81</c> reg, step, end = add step to the
/// sweep value, write it, and repeat on the next tick until it equals end; any byte above <c>0x81</c> + n = wait n
/// ticks (n = 0 ends the script). A wait of n holds n + 1 ticks: the n ticks that count it down and the tick that reads
/// on. Semantics from EmuTOS <c>bios/sound.c</c> <c>sndirq</c> / <c>bios/vectors.S</c> <c>_int_timerc</c> `[~]` (a
/// reimplementation of TOS, not Atari's own source). FORMAT.md §7.1.
/// </para>
/// </summary>
public sealed class DosoundScore : TuneScore
{
    /// <summary>The Dosound interrupt rate: Timer C (200 Hz) / 4 `[~]`, the same on 50 and 60 Hz machines.</summary>
    public const int TickHz = 50;

    private DosoundScore(IReadOnlyList<PsgWrite> writes, int ticks, int bytes)
    {
        Writes = writes;
        Ticks = ticks;
        ScriptBytes = bytes;
    }

    /// <summary>Every register write, in order (registers 0–15 only).</summary>
    public IReadOnlyList<PsgWrite> Writes { get; }

    /// <summary>
    /// The tick the script reads its end (a wait of 0) on; the writes before that end run on it too. The chip keeps its last
    /// registers after the end (TOS silences nothing), so a script ends by writing its volumes to 0 itself.
    /// </summary>
    public int Ticks { get; }

    /// <summary>The script's length in bytes, through its end command.</summary>
    public int ScriptBytes { get; }

    public override double DurationSeconds => (double)Ticks / TickHz;

    /// <summary>Run the script from <paramref name="start"/> (Dosound's argument) the way the 50 Hz routine does.</summary>
    public static DosoundScore Parse(ReadOnlySpan<byte> data, int start = 0, int maxTicks = 50 * 60 * 30)
    {
        var writes = new List<PsgWrite>();
        int pc = start, delay = 0, sweep = 0;
        for (var tick = 0; tick < maxTicks; tick++)
        {
            if (delay > 0)
            {
                delay--;
                continue;
            }
            int instr;
            while ((instr = Byte(data, pc++)) <= 0x80)
            {
                var value = Byte(data, pc++);
                if (instr == 0x80)
                {
                    sweep = value;
                    continue;
                }
                if (instr < 16)
                    writes.Add(new PsgWrite(tick, instr, value));
            }
            if (instr == 0x81)
            {
                var reg = Byte(data, pc++);
                sweep = (sweep + Byte(data, pc++)) & 0xFF;
                if (reg < 16)
                    writes.Add(new PsgWrite(tick, reg, sweep));
                if (sweep != Byte(data, pc++))
                    pc -= 4; // run the sweep again next tick
            }
            else
            {
                delay = Byte(data, pc++);
                if (delay == 0)
                    return new DosoundScore(writes, tick, pc - start);
            }
        }
        throw new InvalidDataException($"Dosound script at {start} has no end within {maxTicks} ticks.");
    }

    private static int Byte(ReadOnlySpan<byte> data, int at) =>
        at >= 0 && at < data.Length ? data[at] : throw new InvalidDataException($"Dosound script runs past the data at {at}.");
}
