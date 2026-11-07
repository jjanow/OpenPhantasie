namespace OpenPhantasie.Importer.Pipelines.P4X68k;

/// <summary>
/// The O_001–O_021 event bytecode walk (port of <c>disasm</c> in <c>tools/scripts/x68k_p4_events.py</c>; spec
/// <c>logic-specs/events.md</c>): recursive traversal from every trigger entry, in the script's order.
/// </summary>
internal static class P4Events
{
    public sealed record Ins(int Pc, int Op, int[] Args, int Length);

    // opcode -> operand kinds (b byte, w u16 LE, a code address, t text address)
    private static readonly Dictionary<int, string> Ops = new()
    {
        [0x00] = "", [0x01] = "a", [0x02] = "a", [0x03] = "a", [0x04] = "a", [0x05] = "a", [0x06] = "a", [0x07] = "a",
        [0x08] = "", [0x09] = "t", [0x0A] = "t", [0x0B] = "", [0x0C] = "", [0x0D] = "t", [0x0E] = "", [0x0F] = "w",
        [0x10] = "b", [0x11] = "b", [0x12] = "w", [0x13] = "w", [0x14] = "b", [0x15] = "b", [0x16] = "b", [0x17] = "b",
        [0x18] = "b", [0x19] = "b", [0x1A] = "b", [0x1B] = "bbb", [0x1C] = "bbb", [0x1D] = "bb", [0x1E] = "bbbbb",
        [0x1F] = "b", [0x20] = "b", [0x21] = "", [0x22] = "", [0x23] = "b", [0x24] = "b", [0x25] = "b", [0x26] = "b",
        [0x27] = "", [0x28] = "t", [0x29] = "", [0x2A] = "ba", [0x2B] = "b", [0x2C] = "b", [0x2D] = "", [0x2E] = "bbb",
        [0x2F] = "", [0x30] = "b", [0x31] = "", [0x32] = "b", [0x33] = "w", [0x34] = "b", [0x35] = "b", [0x36] = "b",
        [0x37] = "bb", [0x38] = "bb", [0x39] = "", [0x3A] = "bbb",
    };

    public static (int Count, int Base, List<(int X, int Y, int Dir, int Off)> Entries) Parse(byte[] buf)
    {
        var n = buf[0];
        var b = buf[3] | buf[4] << 8;
        var entries = new List<(int, int, int, int)>();
        for (var i = 1; i <= n; i++)
            entries.Add((buf[5 * i], buf[5 * i + 1], buf[5 * i + 2], buf[5 * i + 3] | buf[5 * i + 4] << 8));
        return (n, b, entries);
    }

    /// <summary>Instructions in discovery order (Python dict order) and the text at every text operand.</summary>
    public static (List<Ins> Ins, Dictionary<int, string> Texts) Disasm(byte[] buf)
    {
        var (_, b, entries) = Parse(buf);
        var ins = new Dictionary<int, Ins>();
        var order = new List<Ins>();
        var texts = new Dictionary<int, string>();
        var todo = new List<int>(entries.Select(e => b + e.Off));
        while (todo.Count > 0)
        {
            var pc = todo[^1];
            todo.RemoveAt(todo.Count - 1);
            while (!ins.ContainsKey(pc))
            {
                if (pc >= buf.Length)
                    break;
                int op = buf[pc];
                if (!Ops.TryGetValue(op, out var kinds))
                    break;
                var args = new List<int>();
                var q = pc + 1;
                foreach (var k in kinds)
                {
                    if (k == 'b')
                    {
                        args.Add(buf[q]);
                        q += 1;
                        continue;
                    }
                    var v = buf[q] | buf[q + 1] << 8;
                    if (k is 'a' or 't')
                        v += b;
                    args.Add(v);
                    if (k == 'a')
                        todo.Add(v);
                    else if (k == 't')
                    {
                        var e = Array.IndexOf(buf, (byte)0, v);
                        texts[v] = P4Util.DecodeCp932Replace(buf.AsSpan(v, e - v));
                    }
                    q += 2;
                }
                var x = new Ins(pc, op, args.ToArray(), q - pc);
                ins[pc] = x;
                order.Add(x);
                if (op is 0x00 or 0x01 or 0x08)
                    break;
                pc = q;
            }
        }
        return (order, texts);
    }
}
