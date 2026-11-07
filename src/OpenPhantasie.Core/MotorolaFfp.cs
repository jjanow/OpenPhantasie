namespace OpenPhantasie.Core;

/// <summary>
/// Motorola Fast Floating Point as Phantasie II's <c>PHANT.PRG</c> (Atari ST, DRI C runtime) computes it, bit for bit:
/// a 32-bit value with a 24-bit mantissa in bits 31..8 (top bit set), the sign in bit 7 and an excess-64 exponent in
/// bits 6..0; 0 is all zeros. Ported from the program's own routines (image addresses): <see cref="FromInt"/>
/// <c>0x1dbc6</c>, <see cref="Add"/> <c>0x1dd86</c>, <see cref="Mul"/> <c>0x1e0ca</c>, <see cref="Div"/> <c>0x1de88</c>,
/// <see cref="ToInt"/> <c>0x1dc56</c>, <see cref="Sub"/> <c>0x1dd74</c> [C]. Checked against those routines run in a 68000
/// emulator (s83: 20,000 random operand pairs per operation, and every Toss slot of <c>toss-rock.md</c> §2; s92: signed
/// add / sub on 40,000 pairs, <c>tools/scripts/st_p2_ffp_oracle.py</c>). Add rounds half up on the shifted-out bits of a
/// same-sign sum and truncates a difference, Mul drops the low×low partial product and rounds, Div estimates from the
/// divisor's high word and rounds; ToInt truncates toward zero. <see cref="FromInt"/> takes non-negative integers only.
/// </summary>
public static class MotorolaFfp
{
    /// <summary>0.02 as the ST stores it (data constant <c>0xA3D70A3B</c>, toss slot formula <c>0x77f2</c>).</summary>
    public const uint Two100ths = 0xA3D70A3B;

    /// <summary><c>0x1dbc6</c>: an integer (≥ 0) to FFP.</summary>
    public static uint FromInt(int value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        if (value == 0)
            return 0;
        var x = (uint)value;
        var e = 24;
        while ((x & 0x7F000000) != 0)
        {
            x >>= 1;
            e++;
        }
        while ((x & 0x00800000) == 0)
        {
            x <<= 1;
            e--;
        }
        return (x << 8) | (uint)((e + 64) & 127);
    }

    /// <summary><c>0x1dc56</c>: FFP to an integer, truncating toward zero.</summary>
    public static int ToInt(uint v)
    {
        var e = (int)(v & 127) - 64;
        if (v == 0 || e < 0)
            return 0;
        var negative = (v & 128) != 0;
        if (e > 31)
            return negative ? int.MinValue : int.MaxValue;
        var m = (v >> 8) & 0xFFFFFF;
        e -= 24;
        m = e < 0 ? m >> -e : m << e;
        return negative ? -(int)m : (int)m;
    }

    /// <summary>
    /// <c>0x1dd86</c>: <paramref name="a"/> + <paramref name="b"/>. Same signs add the mantissas (half up on the shifted-out
    /// bits, carry → exponent + 1); opposite signs subtract them with a guard bit and normalise by shifting left, with no
    /// rounding (s92: the signed path checked against the routine on 40,000 pairs).
    /// </summary>
    public static uint Add(uint a, uint b)
    {
        uint ab = a & 0xFF, bb = b & 0xFF;
        if (bb == 0)
            return a;
        if (ab == 0)
            return b;
        if (((ab ^ bb) & 0x80) == 0)
            return AddSameSign(a, b, ab, bb);
        var d = (sbyte)(byte)((ab ^ 0x80) - bb);
        uint m, res;
        if (d == 0)
        {
            m = (a & 0xFFFFFF00) - (b & 0xFFFFFF00);
            if (m == 0)
                return 0;
            return (m & 0x80000000) != 0 ? Normalize(0u - m, bb) : Normalize(m, ab);
        }
        if (d > 0)
        {
            if (d >= 24)
                return a;
            res = ab;
            m = ((a & 0xFFFFFF00) | 0x80) - ((b & 0xFFFFFF00) >> d);
        }
        else
        {
            if (d <= -24)
                return b;
            res = bb;
            m = ((b & 0xFFFFFF00) | 0x80) - ((a & 0xFFFFFF00) >> -d);
        }
        return (m & 0x80000000) != 0 ? (m & 0xFFFFFF00) | res : Normalize(m, res);
    }

    /// <summary><c>0x1dd74</c>: <paramref name="a"/> − <paramref name="b"/> = <c>a + (−b)</c>.</summary>
    public static uint Sub(uint a, uint b) => Add(a, Negate(b));

    /// <summary>The sign flipped (bit 7); 0 stays 0.</summary>
    public static uint Negate(uint v) => (v & 0xFF) == 0 ? v : v ^ 0x80;

    private static uint AddSameSign(uint a, uint b, uint ab, uint bb)
    {
        var d = (sbyte)(byte)(ab - bb);
        uint exp;
        ulong sum;
        if (d >= 0)
        {
            if (d >= 24)
                return a;
            exp = ab;
            sum = (ulong)((a & 0xFFFFFF00) | 0x80) + ((b & 0xFFFFFF00) >> d);
        }
        else
        {
            if (d <= -24)
                return b;
            exp = bb;
            sum = (ulong)((b & 0xFFFFFF00) | 0x80) + ((a & 0xFFFFFF00) >> -d);
        }
        if (sum > uint.MaxValue)
        {
            // carry out of bit 31: roxr #1 brings it back in, exponent + 1
            sum = 0x80000000UL | ((sum & uint.MaxValue) >> 1);
            var next = (exp + 1) & 0xFF;
            if ((exp & 0x80) == 0 ? next == 0x80 : next == 0)
                throw new OverflowException("FFP add overflow.");
            exp = next;
        }
        return ((uint)sum & 0xFFFFFF00) | exp;
    }

    /// <summary><c>0x1de0c…0x1de32</c>: shift the difference left until its top bit is set; exponent underflow gives 0.</summary>
    private static uint Normalize(uint m, uint res)
    {
        m &= 0xFFFFFF00;
        var e = (res - 1) & 0xFF;
        if (m <= 0x7FFF)
        {
            m <<= 16;
            e = (e - 16) & 0xFF;
        }
        while (true)
        {
            m <<= 1;
            if ((m & 0x80000000) != 0)
                break;
            e = (e - 1) & 0xFF;
        }
        if (((res ^ e) & 0x80) != 0 || e == 0)
            return 0;
        return (m & 0xFFFFFF00) | e;
    }

    /// <summary><c>0x1e0ca</c>: <paramref name="a"/> · <paramref name="b"/>.</summary>
    public static uint Mul(uint a, uint b)
    {
        if ((a & 0xFF) == 0)
            return a;
        if ((b & 0xFF) == 0)
            return 0;
        var s = DoubledExponent(a) + DoubledExponent(b);
        if (s is < -128 or > 127)
            return s >= 0 ? throw new OverflowException("FFP mul overflow.") : 0u;
        var exp = ((((uint)s & 0xFF) ^ 0x80) >> 1) | ((a ^ b) & 0x80);
        uint ahi = a >> 16, alo = a & 0xFF00, bhi = b >> 16, blo = b & 0xFF00;
        var p1 = alo * blo;
        var d4 = (ulong)(((p1 & 0xFFFF) << 16) | (p1 >> 16)) + ahi * blo;
        d4 &= uint.MaxValue;
        var t = d4 + (ulong)bhi * alo;
        var x = t > uint.MaxValue ? 1UL : 0UL;
        var low = (uint)((x << 16) | ((t & uint.MaxValue) >> 16));
        var d7 = ahi * bhi + low;
        if ((d7 & 0x80000000) != 0)
        {
            d7 += 128;
            return exp == 0 ? 0 : (d7 & 0xFFFFFF00) | exp;
        }
        if ((exp & 0x7F) == 0)
            return 0; // subqb #1 underflows (bvs / bcs)
        exp = (exp - 1) & 0xFF;
        d7 += 64;
        var carry = (d7 & 0x80000000) != 0;
        d7 <<= 1;
        if (carry)
        {
            d7 = 0x80000000 | (d7 >> 1);
            exp = (exp + 1) & 0xFF;
        }
        return exp == 0 ? 0 : (d7 & 0xFFFFFF00) | exp;
    }

    /// <summary><c>0x1de88</c>: <paramref name="a"/> / <paramref name="b"/>.</summary>
    public static uint Div(uint a, uint b)
    {
        if ((b & 0xFF) == 0)
            throw new DivideByZeroException();
        if (a == 0)
            return 0;
        var s = DoubledExponent(a) - DoubledExponent(b);
        if (s is < -128 or > 127)
            throw new OverflowException("FFP div overflow.");
        uint ahi = a >> 16, alo = a & 0xFF00, bhi = b >> 16;
        var d7 = (alo << 16) | ahi;
        if (((ahi - bhi) & 0x8000) == 0)
        {
            s += 2;
            if (s > 127)
                throw new OverflowException("FFP div overflow.");
            d7 = (d7 >> 1) | (d7 << 31);
        }
        d7 = (d7 << 16) | (d7 >> 16);
        var exp = ((((uint)s & 0xFF) ^ 0x80) >> 1) | ((a ^ b) & 0x80);
        var q1 = d7 / bhi;
        d7 -= q1 * bhi;
        d7 = (d7 << 16) | (d7 >> 16);
        var t = (long)d7 - (long)(b & 0xFF00) * q1;
        if (t < 0)
        {
            t += b & 0xFFFFFF00;
            q1 = (q1 - 1) & 0xFFFF;
        }
        d7 = (uint)(t & uint.MaxValue) & 0xFFFF0000;
        var q2 = d7 / bhi;
        if (q2 > 0xFFFF)
            q2 = 0; // DIVU overflow leaves d7 alone; its low word (cleared) is read as the quotient
        var d5 = (q1 << 16) | q2;
        if ((q1 & 0x8000) == 0)
        {
            d5 <<= 1;
            exp = (exp - 1) & 0xFF;
        }
        d5 += 128;
        return exp == 0 ? 0 : (d5 & 0xFFFFFF00) | exp;
    }

    /// <summary>2·(e − 64) as the routines hold it in a signed byte.</summary>
    private static int DoubledExponent(uint v)
    {
        var b = (int)(((v << 1) & 0xFF) ^ 0x80);
        return b >= 128 ? b - 256 : b;
    }
}
