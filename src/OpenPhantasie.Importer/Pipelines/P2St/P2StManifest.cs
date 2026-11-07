using System.Text.Json.Nodes;

namespace OpenPhantasie.Importer.Pipelines.P2St;

/// <summary>
/// Port of <c>build_st_p2_asset_manifest.py</c>: the art root's <c>asset-manifest.json</c>, and the graphics pack
/// (<c>pack.json</c> in the art root = <c>GameData/phantasie2/graphics/atari-st.json</c>, the same bytes).
/// <para>The pack is mostly our own data (class a: sound tables, cue maps, timings, layout), which the script holds
/// as constants. The port takes those from the shipped <c>atari-st.json</c> and recomputes every value that comes
/// from the player's disks: the four palettes, the colour-cycle registers, the town-name inks, the ending palettes,
/// the picture paths and the art root.</para>
/// </summary>
internal static class P2StManifest
{
    /// <summary>TOS's VDI pen -> hardware register map for 16 colours (low resolution).</summary>
    private static readonly int[] VdiToRegister = [0, 15, 1, 2, 4, 6, 3, 5, 7, 8, 9, 10, 12, 14, 11, 13];

    public static JsonObject AssetManifest(JsonObject index, JsonArray monsters, JsonObject names)
    {
        var assets = new JsonArray();
        var unmapped = new JsonArray();
        var byIdx = index["monsters"]!.AsArray().ToDictionary(m => (int)m!["index"]!, m => m!);
        foreach (var m in monsters)
        {
            var i = (int)m!["index"]!;
            var name = (string)m["name"]!;
            if (!byIdx.TryGetValue(i, out var sp))
            {
                unmapped.Add(J.O(("kind", "monster"), ("data_id", i), ("name", name), ("reason", "no sprite")));
                continue;
            }
            if ((string)sp["name"]! != name)
                throw new InvalidDataException($"monster {i}: sprite name {(string)sp["name"]!} != table name {name}");
            assets.Add(J.O(("kind", "monster"), ("data_id", i), ("name", name),
                ("sprites", J.O(("idle", J.Clone(sp["idle"])), ("attacking", J.Clone(sp["attacking"])))),
                ("size", J.O(("width", J.Clone(sp["width"])), ("height", J.Clone(sp["height"]))))));
        }
        var races = names["races"]!.AsArray().Select(r => (string)r!).ToList();
        foreach (var r in index["races"]!.AsArray())
        {
            var i = (int)r!["index"]!;
            var entry = J.O(("kind", "race"), ("data_id", i), ("name", J.Clone(r["name"])),
                ("sprites", J.O(("idle", J.Clone(r["idle"])), ("attacking", J.Clone(r["attacking"])))),
                ("size", J.O(("width", 32), ("height", 32))));
            if (i >= races.Count || races[i] != (string)r["name"]!)
                entry["note"] = "portrait slot without a race name in names.json";
            assets.Add(entry);
        }
        const string ex = "reverse-engineering/atari-st/phantasie2/extracted";
        return J.O(
            ("_schema", "phantasie.st-p2.asset-manifest/v1"),
            ("_generated_by", "tools/scripts/build_st_p2_asset_manifest.py"),
            ("_sources", J.Strs([$"{ex}/graphics/extraction-index.json", $"{ex}/monsters.json", $"{ex}/names.json"])),
            ("palette", "combat (monsters and portraits are drawn on the combat screen, palette PHANT.PRG 0x23a82)"),
            ("assets", assets),
            ("unmapped", unmapped),
            ("counts", J.O(("monsters", assets.Count(a => (string)a!["kind"]! == "monster")),
                ("races", assets.Count(a => (string)a!["kind"]! == "race")))));
    }

    /// <summary>The graphics pack: the shipped pack with the disk-derived values recomputed.</summary>
    public static JsonObject Pack(JsonObject index, JsonNode? shipped, P2StSource? src = null)
    {
        if (shipped is not JsonObject pack)
            throw new InvalidOperationException("the shipped GameData/phantasie2/graphics/atari-st.json is missing");
        pack = (JsonObject)pack.DeepClone();
        JsonArray Rgb(string name) => (JsonArray)index["palettes"]![name]!["rgb"]!.DeepClone();
        var screens = index["screens"]!.AsArray().ToDictionary(s => ((string)s!["path"]!).Split('/')[^1], s => (string)s!["path"]!);

        pack["root"] = P2StGraphics.Root;
        pack["palettes"] = J.O(("start", Rgb("start")), ("town", Rgb("town")), ("map", Rgb("map")), ("combat", Rgb("combat")));
        Require(pack, "colour_cycle")["registers"] = index["palettes"]!["map"]!["colour_cycle"]!["registers"]!.DeepClone();

        // Town number -> banner ink: pen = town number except 3 -> 11 and 9 -> 13 (PHANT.PRG 0x4082); 0 = new game.
        var town = Rgb("town");
        var inks = new JsonObject();
        for (var t = 0; t < 13; t++)
        {
            var pen = t switch { 3 => 11, 9 => 13, _ => t };
            inks[t.ToString()] = town[VdiToRegister[pen]]!.DeepClone();
        }
        Require(pack, "town_screen")["name_colours"] = inks;

        var pictures = Require(pack, "pictures");
        pictures["title.cover"] = screens["cover1.png"];
        pictures["title.credits"] = screens["title-credits.png"];
        pictures["town.backdrop"] = screens["pelnor.png"];
        pictures["ui.decor"] = screens["decor.png"];
        pictures["ending.final"] = screens["final2.png"];

        var ending = Require(pack, "ending_scene");
        ending["palette"] = Rgb("combat");
        Require(ending, "intro_page")["palette"] = Rgb("map");

        if (src is not null)
        {
            // s158: each alert line's box = the disks' own form_alert string (PHANT.RSC / OUT.RSC free string n, or a
            // PHANT.PRG data string); rebuilt from the player's disks, not taken from the shipped pack.
            var alerts = Require(Require(pack, "field_sounds"), "alerts");
            foreach (var line in alerts["lines"]!.AsArray())
            {
                var key = (string)line!["key"]!;
                if (AlertBoxes.TryGetValue(key, out var box))
                    line["box"] = AlertText(src, box);
                else
                    line.AsObject().Remove("box");
            }
            var cues = new JsonObject();
            foreach (var (cue, box) in CueBoxes)
                cues[cue] = AlertText(src, box);
            alerts["cue_boxes"] = cues;
        }
        return pack;
    }

    // build_st_p2_asset_manifest.py ST_ALERT_BOXES / ST_CUE_BOXES (s158): line key -> (PHANT | OUT .RSC free string n,
    // or PRG = a PHANT.PRG image address).
    private static readonly (string Key, (string Kind, int N) Src)[] AlertBoxList =
    [
        ("core.guild.party_is_full", ("PHANT", 0)),
        ("core.armory.not_enough_gold", ("PHANT", 1)),
        ("core.guild.learn_cant_afford", ("PHANT", 1)),
        ("core.guild.train_cant_afford", ("PHANT", 1)),
        ("core.p2.you_are_improved", ("PHANT", 2)),
        ("core.town.you_must_assemble_a_group", ("PHANT", 3)),
        ("core.guild.learn_learned", ("PHANT", 4)),
        ("core.guild.no_one_wise_enough", ("PHANT", 5)),
        ("core.guild.learn_no_more", ("PHANT", 7)),
        ("core.magic.x_drinks_healing_x_x", ("PHANT", 9)),
        ("core.magic.x_drinks_magic_x_x", ("PHANT", 9)),
        ("core.p2.unusable", ("PHANT", 10)),
        ("core.guild.learn_failed", ("PHANT", 11)),
        ("core.armory.ninth_item", ("PHANT", 12)),
        ("core.aging.x_has_died", ("PRG", 0x229CA)),
        ("core.aging.x_has_aged", ("PRG", 0x229EA)),
        ("core.guild.you_need_x_more_xp", ("PRG", 0x1F572)),
        ("core.magic.not_enough_mp", ("OUT", 0)),
    ];

    private static readonly Dictionary<string, (string Kind, int N)> AlertBoxes = AlertBoxList.ToDictionary(e => e.Key, e => e.Src);

    private static readonly (string Cue, (string Kind, int N) Src)[] CueBoxes = [("town.cast_done", ("PHANT", 9))];

    private static string AlertText(P2StSource src, (string Kind, int N) box) =>
        box.Kind == "PRG" ? src.Phant.CStr(box.N) : RscFreeStrings(src.Read($"{box.Kind}.RSC"))[box.N];

    /// <summary>A GEM .RSC file's free strings: header word 5 = rs_frstr, word 15 = rs_nstring.</summary>
    internal static List<string> RscFreeStrings(byte[] raw)
    {
        int Word(int i) => System.Buffers.Binary.BinaryPrimitives.ReadUInt16BigEndian(raw.AsSpan(2 * i));
        int frstr = Word(5), count = Word(15);
        var outList = new List<string>(count);
        for (var i = 0; i < count; i++)
        {
            var off = (int)System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(raw.AsSpan(frstr + 4 * i));
            var end = Array.IndexOf(raw, (byte)0, off);
            outList.Add(Py.Latin1(raw.AsSpan(off, end - off)));
        }
        return outList;
    }

    private static JsonObject Require(JsonObject o, string key) =>
        o[key] as JsonObject ?? throw new InvalidOperationException($"atari-st.json: '{key}' missing");
}
