using OpenPhantasie.Core;

namespace OpenPhantasie.Data;

/// <summary>Kind of a DOS save import/export note (P1.4 decisions 3, 5, 6, 7).</summary>
public enum DosSaveWarningKind
{
    /// <summary>Import: the DOS save holds a state the remaster rules would not have produced. Loaded as-is.</summary>
    RuleBreak,

    /// <summary>Export: a value did not fit its DOS field and was clamped.</summary>
    Clamp,

    /// <summary>Export: the set can't be written (nothing is written).</summary>
    Blocker,

    /// <summary>Import/export: informational (e.g. a missing file read as empty).</summary>
    Note,
}

/// <summary>One entry of the warnings list shown to the player after a DOS import or export.</summary>
public sealed record DosSaveWarning(DosSaveWarningKind Kind, string File, string Message)
{
    public override string ToString() => $"[{Kind}] {File}: {Message}";
}

internal static class DosFieldWriter
{
    /// <summary>
    /// u16 field write with the P1.4 clamp rule (decision 7). Values -32768..65535 are stored as the 16-bit word (a
    /// negative value is the DOS signed word); anything outside is clamped to that range and listed.
    /// </summary>
    public static void U16(Span<byte> r, int offset, long value, string file, string what,
        ICollection<DosSaveWarning>? warnings)
    {
        if (value is < short.MinValue or > ushort.MaxValue)
        {
            var clamped = Math.Clamp(value, short.MinValue, ushort.MaxValue);
            warnings?.Add(new DosSaveWarning(DosSaveWarningKind.Clamp, file,
                GameText.F("core.dossave.x_x_not_16_bits_x", what, value, clamped)));
            value = clamped;
        }

        r[offset] = (byte)(value & 0xff);
        r[offset + 1] = (byte)((value >> 8) & 0xff);
    }

    public static void U8(Span<byte> r, int offset, long value, string file, string what,
        ICollection<DosSaveWarning>? warnings)
    {
        if (value is < 0 or > byte.MaxValue)
        {
            var clamped = Math.Clamp(value, 0, byte.MaxValue);
            warnings?.Add(new DosSaveWarning(DosSaveWarningKind.Clamp, file,
                GameText.F("core.dossave.x_x_not_a_byte_x", what, value, clamped)));
            value = clamped;
        }

        r[offset] = (byte)value;
    }

    public static int ReadU16(ReadOnlySpan<byte> r, int offset) => r[offset] | (r[offset + 1] << 8);
}
