using System.Text.Json.Nodes;

namespace OpenPhantasie.Importer.Pipelines.P1Dos;

/// <summary>
/// The tables read from the unpacked PHANTASI.COM: monsters (<c>extract_monsters.py</c>), items
/// (<c>extract_items.py</c>) and names (<c>extract_names.py</c>, with TWNS.INT's town labels). The notes in the output
/// are ours and live here, as they did in the scripts.
/// </summary>
internal static class ComTables
{
    // ---- monsters (extract_monsters.py) -------------------------------------------------------------------------

    private const int MonsterTableDs = 0x1A6C;
    private const int SpriteDimDs = 0x2C3E; // u16 width, u16 height per monster (OV2 0x38b2)
    private const int MonsterStride = 0x39;
    private const int MonsterCount = 80;

    /// <summary>
    /// The 80 monster records at DS:0x1a6c. The script's cross-check against the DOSBox capture
    /// <c>monster_runtime_table.bin</c> is not done here (10-importer.md 5.2: it stays in the private repo's tests).
    /// </summary>
    public static JsonObject Monsters(ComImage com)
    {
        var table = com.Ds(MonsterTableDs, MonsterStride * MonsterCount);
        var dims = com.Ds(SpriteDimDs, 4 * MonsterCount);
        var monsters = new JsonArray();
        for (var i = 0; i < MonsterCount; i++)
        {
            var r = table.AsSpan(i * MonsterStride, MonsterStride);
            int w = Py.U16(dims, 4 * i), h = Py.U16(dims, 4 * i + 2);
            monsters.Add(new JsonObject
            {
                ["id"] = $"p1:{i}", // game-prefixed id (Phase 09 T1.2)
                ["index"] = i,
                ["name"] = Py.Latin1(Py.UntilNul(r[..0x14])),
                ["armor"] = (int)Py.I16(r, 0x14),
                ["damage"] = (int)Py.I16(r, 0x16),
                ["level"] = (int)Py.I16(r, 0x18),
                ["xp"] = (long)Py.U32(r, 0x1A),
                ["special_chance"] = (int)Py.I16(r, 0x1E),
                ["specials"] = Py.Bytes(r[0x20..0x23]),
                ["hp_base"] = (int)Py.I16(r, 0x23),
                ["combat_rating"] = (int)Py.I16(r, 0x25),
                ["companions"] = Py.Ints([r[0x27], r[0x28]]),
                ["reaction_modifier"] = (int)Py.I16(r, 0x29),
                ["next_group_chance"] = (int)Py.I16(r, 0x2B),
                ["defense"] = (int)Py.I16(r, 0x2D),
                ["gold"] = (long)Py.U32(r, 0x2F),
                ["group_size_factor"] = (int)r[0x33],
                ["byte_34"] = (int)r[0x34],
                ["treasure_items"] = Py.Ints([r[0x35], r[0x36]]),
                ["attack_power"] = (int)Py.I16(r, 0x37),
                ["sprite_width"] = w,
                ["sprite_height"] = h,
                ["raw_hex"] = Py.Hex(r),
            });
        }
        return new JsonObject
        {
            ["_source"] = "PHANTASI.COM unpacked (EXEPACK), DS:0x1a6c, 80 x 0x39; " +
                          "equals extracted/monster_runtime_table.bin (DOSBox capture)",
            ["_fields"] = new JsonObject
            {
                ["armor"] = "+0x14 [C] party damage subtracts armor*10 (OV2 0xbce)",
                ["damage"] = "+0x16 [C] monster damage factor (OV2 0xea9)",
                ["level"] = "+0x18 [C] encounter level L += level once per group (OV2 0x3ac4)",
                ["xp"] = "+0x1a u32 [C] XP per monster killed (OV2 0x1656)",
                ["special_chance"] = "+0x1e [C] special-ability gate (OV2 0xd49)",
                ["specials"] = "+0x20 3 bytes [C] special picked by rand*3/100; effects [C] OV2 0x1004 -> 0x16a9 (combat-resolution.md Monster specials, s26); a pick > 0x21 falls back to melee",
                ["hp_base"] = "+0x23 [C] HP = ((rand0_99 + 50) * hp_base + 50) / 100 (OV2 0x3a27)",
                ["combat_rating"] = "+0x25 [C] monster CV, spell/breath to-hit (OV2 0x13ce)",
                ["companions"] = "+0x27/+0x28 [C] 0-based monster id of the next group (rand<50 -> +0x28 else +0x27) (OV2 0x3adc)",
                ["reaction_modifier"] = "+0x29 [~] summed per group into [0x750c]; reaction roll rand < sum + 90 (OV2 0x3bed)",
                ["next_group_chance"] = "+0x2b [C] another group follows if rand < this (max 3 groups) (OV2 0x3b39)",
                ["defense"] = "+0x2d [C] party melee to-hit subtracts this (OV2 0xb5a); often negative",
                ["gold"] = "+0x2f u32 [C] gold added per monster at encounter build (OV2 0x3a97)",
                ["group_size_factor"] = "+0x33 [C] count = clamp(rand0_99 * f / 102, 0, 5) + 1 (OV2 0x39b1)",
                ["byte_34"] = "+0x34 [~] overwritten in the combat copy by sprite width (OV2 0x38cb)",
                ["treasure_items"] = "+0x35/+0x36 [C] item ids dropped as treasure, first group only (OV2 0x41e4)",
                ["attack_power"] = "+0x37 [C] monster melee to-hit; swings = max(1, ap/82) (OV2 0xd86)",
                ["sprite_width"] = "DS:0x2c3e [C] build stops adding groups once sum(width+3) >= 135 (OV2 0x399b)",
                ["sprite_height"] = "DS:0x2c40 [~]",
            },
            ["monsters"] = monsters,
        };
    }

    // ---- items (extract_items.py) -------------------------------------------------------------------------------

    private const int ValueTableDs = 0x0D54; // u16 V[182]; last entry unused
    private const int NamePtrsDs = 0x0EC0;   // u16 name_ptr[181]
    private const int ItemNameCount = 181;   // ids 0..180

    /// <summary>Mirror of FUN_0000_3053 / FUN_0000_17df: kind, combat points, STR gate.</summary>
    private static void KindPointsReq(int item, JsonObject rec)
    {
        (string kind, int points, int? minStr, string? check) = item switch
        {
            < 0x15 => ("shield", item, item, "str"),
            < 0x1f => ("armor", item - 0x14, item * 2 - 0x28, "str"),
            < 0x29 => ("armor", item - 0x1d, item * 2 - 0x3e, "str"),
            < 0x47 => ("weapon", (item >> 1) - 0x14, item * 2 - 0x50, "str2_plus_dex"),
            < 0x65 => ("weapon", (item * 3 >> 2) - 0x32, item * 2 - 0x8c, "str2_plus_dex"),
            _ => ("other", 0, (int?)null, (string?)null),
        };
        rec["kind"] = kind;
        rec["points"] = points;
        rec["min_str"] = minStr is { } m ? JsonValue.Create(m) : null;
        rec["str_check"] = check is null ? null : JsonValue.Create(check);
    }

    public static JsonObject Items(ComImage com)
    {
        var values = com.Ds(ValueTableDs, 2 * ItemNameCount);
        var ptrs = com.Ds(NamePtrsDs, 2 * ItemNameCount);
        var items = new JsonArray();
        for (var i = 0; i < ItemNameCount; i++)
        {
            int namePtr = Py.U16(ptrs, i * 2);
            int v = Py.U16(values, i * 2);
            var rec = new JsonObject
            {
                ["id"] = $"p1:{i}", // game-prefixed id (Phase 09 T1.2)
                ["index"] = i,
                ["name"] = com.CString(namePtr),
                ["name_ptr_ds"] = Py.Hex4(namePtr),
                ["value_factor"] = v,
                ["sell_price_v2_div_8"] = v * v / 8,
            };
            KindPointsReq(i, rec);
            if (i is >= 101 and <= 110)
            {
                rec["potion_tier"] = i - 100;
                rec["heal_hp_n_squared"] = (i - 100) * (i - 100);
            }
            else if (i is >= 111 and <= 120)
            {
                rec["potion_tier"] = i - 110;
                rec["restore_magic_3n"] = 3 * (i - 110);
            }
            items.Add(rec);
        }
        return new JsonObject
        {
            ["_source"] = "PHANTASI.COM unpacked (EXEPACK), SSI 1987 IBM/DOS",
            ["_note"] = "Combat points / min-STR / kind are formula-derived from index " +
                        "(FUN_0000_3053), not a fixed-size stat record. value_factor V is " +
                        "the only parallel numeric table (DS:0xd54).",
            ["_layout"] = new JsonObject
            {
                ["value_table_ds"] = Py.Hex4(ValueTableDs),
                ["name_ptrs_ds"] = Py.Hex4(NamePtrsDs),
                ["equipment_count"] = 101,
                ["extended_id_count"] = ItemNameCount,
            },
            ["_formulas"] = new JsonObject
            {
                ["sell_price"] = "V*V / 8",
                ["buy_price"] = "V*V * town_price_index / 500 + 1",
                ["town_price_index"] = "TWNS.DAT town_record + 0x10f (u16 LE; Pelnor=150)",
                ["town_record_stride"] = 291,
                ["shield_points"] = "index",
                ["armor_plain_points"] = "index - 20",
                ["armor_magic_points"] = "index - 29",
                ["weapon_plain_points"] = "(index >> 1) - 20",
                ["weapon_magic_points"] = "(index * 3 >> 2) - 50",
                ["usable_shield_armor"] = "STR >= min_str",
                ["usable_weapon"] = "STR*2 + DEX >= min_str",
            },
            ["items"] = items,
        };
    }

    // ---- names (extract_names.py) -------------------------------------------------------------------------------

    private static readonly (int Ds, int Count) ClassPtrs = (0x167E, 6);
    private static readonly (int Ds, int Count) RacePtrs = (0x16B2, 15);
    private static readonly (int Ds, int Count) SpellPtrs = (0x174D, 60);
    private static readonly (int Ds, int Count) ItemPtrs = (0x0EC0, 181);
    private const int EquipmentCount = 101;
    private const int DivineSlot = 57;

    private static int[] Pointers(ComImage com, (int Ds, int Count) table)
    {
        var b = com.Ds(table.Ds, 2 * table.Count);
        return Enumerable.Range(0, table.Count).Select(i => (int)Py.U16(b, 2 * i)).ToArray();
    }

    private static JsonArray Named(ComImage com, IReadOnlyList<int> ptrs, string label)
    {
        var a = new JsonArray();
        for (var i = 0; i < ptrs.Count; i++)
        {
            var name = com.CString(ptrs[i]);
            if (name.Length == 0)
                throw Py.Bad($"PHANTASI.COM {label}: empty name at {i}");
            a.Add(new JsonObject { ["index"] = i, ["offset"] = Py.Hex4(ptrs[i]), ["name"] = name });
        }
        return a;
    }

    private static JsonArray Spells(ComImage com)
    {
        var ptrs = Pointers(com, SpellPtrs);
        var used = Enumerable.Range(0, 54).Append(DivineSlot).ToList();
        for (var i = 0; i < 60; i++)
            if (!used.Contains(i) && com.CString(ptrs[i]).Length != 0)
                throw Py.Bad("PHANTASI.COM: spell slot layout moved");
        // Remaster index 54 = DOS slot 57 (spells.json uses 0..54).
        var output = Named(com, used.Select(i => ptrs[i]).ToList(), "spells");
        ((JsonObject)output[^1]!)["dos_slot"] = DivineSlot;
        return output;
    }

    /// <summary>The spell names in remaster order (0..54), for spells.json.</summary>
    public static IReadOnlyList<string> SpellNames(JsonObject names) =>
        ((JsonArray)names["spells"]!).Select(s => (string)s!["name"]!).ToList();

    private static JsonArray MonsterNames(ComImage com)
    {
        var table = com.Ds(MonsterTableDs, MonsterStride * MonsterCount);
        var a = new JsonArray();
        for (var i = 0; i < MonsterCount; i++)
            a.Add(new JsonObject
            {
                ["index"] = i,
                ["offset"] = Py.Hex4(MonsterTableDs + i * MonsterStride),
                ["name"] = Py.Latin1(Py.UntilNul(table.AsSpan(i * MonsterStride, 0x14))),
            });
        return a;
    }

    private static bool IsLabelChar(byte b) => b is >= (byte)'A' and <= (byte)'Z' or >= (byte)'0' and <= (byte)'9' or (byte)' ' or (byte)'.';

    /// <summary>
    /// TWNS.INT's 10-character space-padded name headers: <c>re.finditer(rb"[A-Z][A-Z0-9 .]{2,}\x00")</c>, kept when
    /// the match starts the file or follows a NUL / blank.
    /// </summary>
    private static JsonArray TownLabels(byte[] data)
    {
        var a = new JsonArray();
        var i = 0;
        while (i < data.Length)
        {
            if (data[i] is >= (byte)'A' and <= (byte)'Z')
            {
                var j = i + 1;
                while (j < data.Length && IsLabelChar(data[j]))
                    j++;
                if (j - (i + 1) >= 2 && j < data.Length && data[j] == 0)
                {
                    if (i == 0 || data[i - 1] is 0x00 or 0x20)
                    {
                        var s = Py.Strip(Py.Latin1(data.AsSpan(i, j - i)));
                        if (s.Length != 0)
                            a.Add(new JsonObject { ["offset"] = Py.Hex4(i), ["name"] = s });
                    }
                    i = j + 1;
                    continue;
                }
            }
            i++;
        }
        return a;
    }

    public static JsonObject Names(ComImage com, byte[] twnsInt)
    {
        var items = Pointers(com, ItemPtrs);
        return new JsonObject
        {
            ["_source"] = "PHANTASI.COM unpacked (EXEPACK) via DS pointer tables + TWNS.INT",
            ["_note"] = "offset = DS offset (TWNS.INT labels: file offset). spell_level_names are really " +
                        "the potion item names, ids 101..110 (only referenced from the item pointer table). " +
                        "Monster stats: monsters.json.",
            ["classes"] = Named(com, Pointers(com, ClassPtrs), "classes"),
            ["races"] = Named(com, Pointers(com, RacePtrs), "races"),
            ["spells"] = Spells(com),
            ["spell_level_names"] = Named(com, items[101..111], "spell_level_names"),
            ["items"] = Named(com, items[..EquipmentCount], "items"),
            ["monsters"] = MonsterNames(com),
            ["town_labels"] = TownLabels(twnsInt),
        };
    }
}
