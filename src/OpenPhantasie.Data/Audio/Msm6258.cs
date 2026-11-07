namespace OpenPhantasie.Data.Audio;

/// <summary>
/// The OKI MSM6258's 4-bit ADPCM (the X68000's ADPCM chip, s152; <c>reverse-engineering/x68000/phantasie4/FORMAT.md</c>
/// "Sound effects"): the standard OKI / Dialogic decoder. Each nibble n gives a difference from the step size
/// <c>Steps[i]</c> (step / 8, plus step / 4, step / 2, step for bits 0, 1, 2; bit 3 the sign); the 12-bit signal is
/// clamped to −2048 … 2047 and i moves by <c>{−1, −1, −1, −1, 2, 4, 6, 8}</c>[n &amp; 7], clamped to 0 … 48. Each byte holds
/// two samples, the low nibble first (the X68000's order: decoding <c>VDATA</c> high nibble first drifts to the clamp,
/// low first stays centred). The chip's state at the start of an output (signal 0, step index 0) is taken as reset `[~]`.
/// </summary>
public static class Msm6258
{
    /// <summary>The 49 step sizes.</summary>
    public static readonly IReadOnlyList<int> Steps =
    [
        16, 17, 19, 21, 23, 25, 28, 31, 34, 37, 41, 45, 50, 55, 60, 66, 73, 80, 88, 97, 107, 118, 130, 143, 157, 173, 190,
        209, 230, 253, 279, 307, 337, 371, 408, 449, 494, 544, 598, 658, 724, 796, 876, 963, 1060, 1166, 1282, 1411, 1552,
    ];

    private static readonly int[] IndexShift = [-1, -1, -1, -1, 2, 4, 6, 8];

    /// <summary>
    /// The X68000's sample rate for IOCS <c>_ADPCMOUT</c> rate <paramref name="rate"/> (0–4): the MSM6258 at 4 MHz divided by
    /// 1024 / 768 / 512 for rates 0–2, at 8 MHz by 768 / 512 for 3–4 (3.9, 5.2, 7.8, 10.4, 15.6 kHz).
    /// </summary>
    public static double RateHz(int rate) => rate switch
    {
        0 => 4_000_000 / 1024.0,
        1 => 4_000_000 / 768.0,
        2 => 4_000_000 / 512.0,
        3 => 8_000_000 / 768.0,
        4 => 8_000_000 / 512.0,
        _ => throw new ArgumentOutOfRangeException(nameof(rate), rate, "IOCS _ADPCMOUT rates are 0-4."),
    };

    /// <summary>Decodes <paramref name="data"/> (two samples a byte, low nibble first) to 12-bit samples −2048 … 2047.</summary>
    public static short[] Decode(ReadOnlySpan<byte> data)
    {
        var output = new short[data.Length * 2];
        int signal = 0, index = 0, at = 0;
        foreach (var b in data)
        {
            output[at++] = Step(b & 0x0F, ref signal, ref index);
            output[at++] = Step(b >> 4, ref signal, ref index);
        }
        return output;
    }

    private static short Step(int nibble, ref int signal, ref int index)
    {
        var step = Steps[index];
        var diff = step >> 3;
        if ((nibble & 1) != 0)
            diff += step >> 2;
        if ((nibble & 2) != 0)
            diff += step >> 1;
        if ((nibble & 4) != 0)
            diff += step;
        if ((nibble & 8) != 0)
            diff = -diff;
        signal = Math.Clamp(signal + diff, -2048, 2047);
        index = Math.Clamp(index + IndexShift[nibble & 7], 0, Steps.Count - 1);
        return (short)signal;
    }
}
