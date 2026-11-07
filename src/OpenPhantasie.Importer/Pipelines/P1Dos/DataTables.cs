using System.Text.Json.Nodes;

namespace OpenPhantasie.Importer.Pipelines.P1Dos;

/// <summary>
/// The I DOS tables read from the data files: towns (<c>extract_towns.py</c>, from TWNS.INT's pristine copy),
/// elementals (<c>extract_elementals.py</c>), scrolls (<c>decrypt_scrolls.py</c>) and messages
/// (<c>decrypt_messages.py</c>).
/// </summary>
internal static class DataTables
{
    // ---- towns (extract_towns.py) -------------------------------------------------------------------------------

    private const int TownStride = 291;
    private const int TownCount = 12;
    private const int StockSlots = 181;
    private const int PriceOff = 0x10F;
    private const int RestockOff = 0xB5;
    private const int RestockCount = 45;
    private const int NameOff = 279;

    /// <summary>
    /// TWNS.INT bytes 2445..5936 equal the pristine TWNS.DAT byte for byte (10-importer.md 3.3); TWNS.DAT itself is
    /// rewritten by the town Save game, so towns are read from TWNS.INT.
    /// </summary>
    public const int TwnsIntTownsOffset = 2445;

    public static JsonObject Towns(byte[] twnsInt)
    {
        if (twnsInt.Length < TwnsIntTownsOffset + TownStride * TownCount)
            throw Py.Bad($"TWNS.INT: expected at least {TwnsIntTownsOffset + TownStride * TownCount} bytes, got {twnsInt.Length}");
        var data = twnsInt.AsSpan(TwnsIntTownsOffset, TownStride * TownCount);
        var towns = new JsonArray();
        for (var index = 0; index < TownCount; index++)
        {
            var rec = data.Slice(index * TownStride, TownStride);
            var nameBytes = rec.Slice(NameOff, 10);
            var nul = nameBytes.IndexOf((byte)0);
            var name = Py.Strip(Py.AsciiReplace(nul < 0 ? nameBytes : nameBytes[..nul]));
            var restock = new int[RestockCount];
            for (var i = 0; i < RestockCount; i++)
                restock[i] = Py.U16(rec, RestockOff + 2 * i);
            towns.Add(new JsonObject
            {
                ["id"] = index + 1, // overworld feature arg is 1-based (Pelnor=1)
                ["index"] = index,
                ["name"] = name,
                ["price_index"] = (int)Py.U16(rec, PriceOff),
                ["stock"] = Py.Bytes(rec[..StockSlots]),
                ["restock_items"] = Py.Ints(restock),
                // P1.4: the whole record, so a DOS export keeps the unmapped bytes.
                ["dos_record"] = Py.Hex(rec),
            });
        }
        return new JsonObject
        {
            ["_source"] = "TWNS.DAT (SSI 1987 IBM/DOS)",
            ["_layout"] = new JsonObject
            {
                ["record_stride"] = TownStride,
                ["town_count"] = TownCount,
                ["stock_slots"] = StockSlots,
                ["price_index_offset"] = Py.HexPlain(PriceOff),
                ["restock_items_offset"] = Py.HexPlain(RestockOff),
                ["name_offset"] = NameOff,
            },
            ["_formulas"] = new JsonObject
            {
                ["buy_price"] = "floor(V*V * town_price_index / 500) + 1 (full charged price; OV1 FUN_0000_1d5b)",
                ["sell_price"] = "V*V / 8",
                ["restock"] = "town entry (OV1 0x8e1): 18x stock[rand/274] = 0 (ids 0..119), then 4x stock[restock_items[rand/(32767/avg_level)]] = 1 (index 0, no draw, when avg_level = 0)",
                ["cha_discount"] = "none [C]: pass-2's full 177-GP shared-purse charge settled as 59 GP from each of 3 persisted @262 carried-gold shares",
            },
            ["towns"] = towns,
        };
    }

    // ---- elementals (extract_elementals.py) ---------------------------------------------------------------------

    private const int CharRec = 282;
    private const int ElementalCount = 3;
    private const int SpellSlots = 60;
    private const int SpellsOff = 0x60;

    private static readonly (int Off, string Key)[] ElementalWords =
    [
        (2, "intelligence"), (14, "mp_ceiling"), (16, "magic_max"), (18, "magic_cur"), (20, "hp_max"), (22, "hp_cur"),
        (24, "status"), (30, "attack"), (32, "parry"), (88, "level"), (92, "guild_number"), (240, "weapon_damage_b"),
        (242, "shield_soak"), (244, "armor_soak"), (256, "race"),
    ];

    private static readonly (int Off, string Key)[] ElementalBytes = [(216, "birth_race"), (220, "class")];

    private static JsonObject Elemental(ReadOnlySpan<byte> rec)
    {
        var name = Py.Strip(Py.Latin1(Py.UntilNul(rec[270..282])));
        if (name.Any(c => c > 0x7f))
            throw Py.Bad("TWNS.INT: elemental name is not ASCII");
        var output = new JsonObject { ["name"] = name };
        foreach (var (off, key) in ElementalWords)
            output[key] = (int)Py.U16(rec, off);
        foreach (var (off, key) in ElementalBytes)
            output[key] = (int)rec[off];
        var known = new List<int>();
        for (var k = 0; k < SpellSlots; k++)
        {
            var v = Py.U16(rec, SpellsOff + 2 * k);
            if (v is not (1 or 2))
                throw Py.Bad($"TWNS.INT: {name} spell slot {k} = {v}");
            if (v == 1)
                known.Add(k);
        }
        output["known_spells"] = Py.Ints(known);

        var covered = new HashSet<int>();
        foreach (var (off, _) in ElementalWords)
        {
            covered.Add(off);
            covered.Add(off + 1);
        }
        foreach (var (off, _) in ElementalBytes)
            covered.Add(off);
        for (var o = SpellsOff; o < SpellsOff + 2 * SpellSlots; o++)
            covered.Add(o);
        for (var o = 270; o < 282; o++)
            covered.Add(o);
        for (var o = 0; o < CharRec; o++)
            if (!covered.Contains(o) && rec[o] != 0)
                throw Py.Bad($"TWNS.INT: {name}: unexported nonzero byte at {o}");
        return output;
    }

    public static JsonObject Elementals(byte[] twnsInt)
    {
        var start = twnsInt.Length - ElementalCount * CharRec;
        if (start != 12141)
            throw Py.Bad($"TWNS.INT: elemental records expected at 12141, found {start}");
        if (twnsInt.AsSpan(start - 22 * CharRec, 22 * CharRec).ContainsAnyExcept((byte)0))
            throw Py.Bad("TWNS.INT: roster placeholder block is not zero");
        var templates = new JsonArray();
        for (var i = 0; i < ElementalCount; i++)
            templates.Add(Elemental(twnsInt.AsSpan(start + i * CharRec, CharRec)));
        return new JsonObject
        {
            ["_source"] = "TWNS.INT tail, 3 x 282-byte character records at file 12141/12423/12705 " +
                          "(tools/scripts/extract_elementals.py). Roster records 22-24.",
            ["_notes"] = "Every field not listed is 0, including @12 defense and the combat fields @246/@248/@250 " +
                         "(DOS prep never reaches party slot 7, combat-resolution.md section SUMMON ELEMENTAL). " +
                         "known_spells: record @0x60 slots set to 1; every other slot is 2. race 15 = UNDEAD (displayed); " +
                         "birth_race 16 is outside the race table. class 3 = FIGHTER.",
            ["elementals"] = templates,
        };
    }

    // ---- scrolls (decrypt_scrolls.py) ---------------------------------------------------------------------------

    private const int ScrollSize = 800;
    private const int ScrollLine = 40;
    private const int ScrollCount = 20;

    public static byte[] DecryptScroll(ReadOnlySpan<byte> cipher, int scrollIndex)
    {
        var step = 9 + 4 * scrollIndex;
        var output = new byte[cipher.Length];
        for (var i = 0; i < cipher.Length; i++)
            output[i] = (byte)((cipher[i] - ((0x0D + step * i) & 0xFF)) & 0x7F);
        return output;
    }

    public static JsonObject Scrolls(byte[] data)
    {
        if (data.Length != ScrollSize * ScrollCount)
            throw Py.Bad($"SCROLLS.DTX: expected {ScrollSize * ScrollCount} bytes, got {data.Length}");
        var scrolls = new JsonArray();
        for (var n = 0; n < ScrollCount; n++)
        {
            var plain = DecryptScroll(data.AsSpan(n * ScrollSize, ScrollSize), n);
            var rows = new List<string>();
            for (var li = 0; li < ScrollSize / ScrollLine; li++)
            {
                var row = plain.AsSpan(li * ScrollLine, ScrollLine);
                var text = new System.Text.StringBuilder();
                foreach (var c in row)
                {
                    if (c is < 32 or >= 127)
                        break;
                    text.Append((char)c);
                }
                rows.Add(text.ToString());
            }
            var body = string.Join("\n", rows.Where(r => Py.Strip(r).Length != 0).Select(Py.RStrip));
            var printable = plain.Count(c => c is >= 32 and < 127 or 0);
            if (printable != plain.Length)
                throw Py.Bad($"SCROLLS.DTX: scroll {n + 1} failed to decrypt cleanly");
            scrolls.Add(new JsonObject
            {
                ["id"] = n + 1,
                ["step"] = 9 + 4 * n,
                ["printable_ratio"] = Py.Round4(printable, plain.Length),
                ["lines"] = Py.Strings(rows.Select(Py.RStrip)),
                ["text"] = body,
            });
        }
        return new JsonObject
        {
            ["_source"] = "SCROLLS.DTX",
            ["_cipher"] = "plain[i] = (cipher[i] - (0x0D + step*i)) & 0x7F; " +
                          "step = 9+4*(id-1); record = 800 bytes",
            ["scrolls"] = scrolls,
        };
    }

    // ---- messages (decrypt_messages.py) -------------------------------------------------------------------------

    private const int Rec = 41; // 1 prefix + 40 text columns
    private const int Line = 40;
    private static readonly byte[] StepCycle = [0x01, 0x21, 0x41, 0x61, 0x81, 0xA1, 0xC1, 0xE1];

    /// <summary>Decrypt one MESS file; <paramref name="n"/> is the file number 1..10.</summary>
    public static byte[] DecryptMess(ReadOnlySpan<byte> cipher, int n)
    {
        var key = (0xFF - 3 * n) & 0xFF;
        var phase = Py.Mod(6 - 3 * n, 8);
        var output = new byte[cipher.Length];
        for (var i = 0; i < cipher.Length; i++)
        {
            output[i] = (byte)((cipher[i] - key) & 0xFF);
            key = (key + StepCycle[(phase + i) % 8]) & 0xFF;
        }
        return output;
    }

    private static List<int> ParseSlotTable(byte[] plain)
    {
        var slots = new List<int>();
        var i = 0;
        while (i + 2 <= plain.Length)
        {
            int w = Py.U16(plain, i);
            if (w != 0 && (w % Rec != 0 || w >= plain.Length))
                break;
            slots.Add(w);
            i += 2;
        }
        return slots;
    }

    private static int FindTextStart(byte[] plain, List<int> slots, int tableEnd)
    {
        var nonzero = slots.Where(s => s != 0).ToList();
        if (nonzero.Count == 0)
            return tableEnd;
        var maxOff = nonzero.Max();

        double Score(int ts)
        {
            var b = ts - Rec;
            if (ts < tableEnd || b < 0)
                return -1e9;
            if (b + maxOff + Rec > plain.Length)
                return -1e9;
            foreach (var s in nonzero)
                if (Py.Mod(b + s - ts, Rec) != 0)
                    return -1e9;
            if (ts + Rec > plain.Length)
                return -1e9;
            var chunk = plain.AsSpan(ts, Rec);
            var printable = 0;
            foreach (var c in chunk[1..])
                if (c is >= 32 and < 127)
                    printable++;
            return printable + (chunk[0] is >= 32 and < 127 or 0x01 or 0xFF ? 0.5 : 0);
        }

        int bestTs = tableEnd;
        var bestSc = Score(tableEnd);
        for (var pad = 0; pad < Rec; pad++)
        {
            var ts = tableEnd + pad;
            var sc = Score(ts);
            if (sc > bestSc)
            {
                bestTs = ts;
                bestSc = sc;
            }
        }
        return bestTs;
    }

    private sealed record MessRecord(int Offset, int Prefix, string Text);

    public static JsonObject Messages(Func<string, byte[]> read)
    {
        var areas = new JsonArray();
        for (var n = 1; n <= 10; n++)
        {
            var name = $"MESS{n}";
            var cipher = read(name);
            var plain = DecryptMess(cipher, n);
            var slots = ParseSlotTable(plain);
            var tableEnd = slots.Count * 2;
            var textStart = FindTextStart(plain, slots, tableEnd);
            var b = slots.Any(s => s != 0) ? textStart - Rec : textStart;

            var recs = new List<MessRecord>();
            var recArray = new JsonArray();
            for (var pos = textStart; pos + Rec <= plain.Length; pos += Rec)
            {
                var prefix = plain[pos];
                var raw = Py.AsciiReplace(plain.AsSpan(pos + 1, Line));
                var text = raw.TrimEnd(' ');
                recs.Add(new MessRecord(pos, prefix, text));
                recArray.Add(new JsonObject
                {
                    ["offset"] = pos,
                    ["record_index"] = (pos - textStart) / Rec,
                    ["prefix"] = (int)prefix,
                    ["text"] = text,
                    ["raw_line"] = raw,
                });
            }
            var printableCount = plain.Count(c => c == 0 || c is >= 32 and < 127);

            var slotMsgs = new JsonArray();
            for (var si = 0; si < slots.Count; si++)
            {
                var off = slots[si];
                if (off == 0)
                {
                    slotMsgs.Add(new JsonObject { ["slot"] = si, ["offset"] = 0, ["text"] = null });
                    continue;
                }
                var addr = b + off;
                var rel = addr - textStart;
                if (rel >= 0 && rel % Rec == 0 && rel / Rec < recs.Count)
                {
                    var r = recs[rel / Rec];
                    slotMsgs.Add(new JsonObject
                    {
                        ["slot"] = si, ["offset"] = off, ["file_offset"] = addr, ["prefix"] = r.Prefix, ["text"] = r.Text,
                    });
                }
                else
                {
                    slotMsgs.Add(new JsonObject
                    {
                        ["slot"] = si, ["offset"] = off, ["file_offset"] = addr, ["text"] = null,
                        ["_note"] = "offset did not land on a record",
                    });
                }
            }

            var pr = Py.Round4(printableCount, Math.Max(1, plain.Length));
            if (printableCount * 100L <= 90L * plain.Length)
                throw Py.Bad($"{name} decrypt looks wrong (printable ratio {pr})");
            areas.Add(new JsonObject
            {
                ["id"] = n,
                ["source"] = name,
                ["size"] = cipher.Length,
                ["initial_key"] = (0xFF - 3 * n) & 0xFF,
                ["phase"] = Py.Mod(6 - 3 * n, 8),
                ["printable_ratio"] = pr,
                ["text_start"] = textStart,
                ["base"] = b,
                ["slot_count"] = slots.Count,
                ["record_count"] = recs.Count,
                ["slots"] = slotMsgs,
                ["records"] = recArray,
            });
        }
        return new JsonObject
        {
            ["_source"] = "MESS1..MESS10",
            ["_cipher"] = "key=(0xFF-3*n); phase=(6-3*n)%8; " +
                          "plain[i]=(cipher[i]-key)&0xFF; " +
                          "key=(key+1+0x20*((phase+i)%8))&0xFF; " +
                          "records=41 bytes (1 prefix + 40 text)",
            ["areas"] = areas,
        };
    }
}
