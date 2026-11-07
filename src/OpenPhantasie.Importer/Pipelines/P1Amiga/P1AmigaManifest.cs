using System.Text.Json.Nodes;

namespace OpenPhantasie.Importer.Pipelines.P1Amiga;

/// <summary>
/// Port of <c>tools/scripts/build_amiga_asset_manifest.py</c>: cross-references the Amiga sprites with I's monster and
/// race tables. The script read the DOS extracts (<c>reverse-engineering/dos/extracted/monsters.json</c> /
/// <c>names.json</c>); their index / name fields equal GameData's <c>monsters.json</c> / <c>names.json</c>, which the
/// importer reads instead (this run's DOS output, an earlier import, or shipped).
/// <para>Writes the full manifest into the art root and <c>GameData/phantasie1/asset-manifest.json</c> = the shipped
/// file's own keys (<c>map_tiles</c> …) with <c>assets</c> set to the monster entries (the file the Client loads holds
/// only those, as committed).</para>
/// </summary>
internal static class P1AmigaManifest
{
    public static void Run(ImportContext ctx, JsonObject index)
    {
        var dosMonsters = ctx.ReadGameData("phantasie1", "monsters.json")?["monsters"] as JsonArray;
        var dosRaces = ctx.ReadGameData("phantasie1", "names.json")?["races"] as JsonArray;
        if (dosMonsters is null || dosRaces is null)
        {
            ctx.Warn("Phantasie I's monsters.json / names.json (from the DOS version) are missing, so the Amiga " +
                     "sprites are not linked to the monster table (asset-manifest.json not written); import the DOS " +
                     "copy of Phantasie I and import the Amiga copy again.");
            return;
        }

        static string Norm(string s) => s.ToUpperInvariant().TrimEnd('.');
        static int Int(JsonNode? n) => (int)n!;
        static string Str(JsonNode? n) => (string)n!;

        var assets = new JsonArray();
        var unmapped = new JsonArray();
        var byAmiga = ((JsonArray)index["monsters"]!).ToDictionary(m => Int(m!["index"]), m => (JsonObject)m!);
        foreach (var dm in dosMonsters)
        {
            var idx = Int(dm!["index"]);
            if (!byAmiga.TryGetValue(idx, out var am))
            {
                unmapped.Add(new JsonObject
                {
                    ["kind"] = "monster", ["data_id"] = idx, ["name"] = Str(dm["name"]),
                    ["reason"] = "no Amiga sprite at this index",
                });
                continue;
            }
            var entry = new JsonObject
            {
                ["kind"] = "monster", ["data_id"] = idx, ["name"] = Str(dm["name"]), ["amiga_name"] = Str(am["name"]),
                ["sprites"] = new JsonObject { ["idle"] = Str(am["idle"]), ["attacking"] = Str(am["attacking"]) },
                ["size"] = new JsonObject { ["width"] = Int(am["width"]), ["height"] = Int(am["height"]) },
            };
            if (Norm(Str(am["name"])) != Norm(Str(dm["name"])))
                entry["note"] = $"name drift: Amiga={PyRepr(Str(am["name"]))} DOS={PyRepr(Str(dm["name"]))}";
            assets.Add(entry);
        }
        var dosMonsterIds = dosMonsters.Select(m => Int(m!["index"])).ToHashSet();
        foreach (var idx in byAmiga.Keys.Where(k => !dosMonsterIds.Contains(k)).Order())
        {
            var am = byAmiga[idx];
            unmapped.Add(new JsonObject
            {
                ["kind"] = "monster_sprite", ["amiga_index"] = idx, ["name"] = Str(am["name"]), ["path"] = Str(am["idle"]),
                ["reason"] = "Amiga sprite index not present in Phase 3 monsters.json",
            });
        }

        var byRace = ((JsonArray)index["races"]!).ToDictionary(r => Int(r!["index"]), r => (JsonObject)r!);
        var dosRaceIds = new HashSet<int>();
        foreach (var dr in dosRaces)
        {
            var idx = Int(dr!["index"]);
            dosRaceIds.Add(idx);
            if (!byRace.TryGetValue(idx, out var ar))
            {
                unmapped.Add(new JsonObject
                {
                    ["kind"] = "race", ["data_id"] = idx, ["name"] = Str(dr["name"]),
                    ["reason"] = "no Amiga portrait at this index",
                });
                continue;
            }
            assets.Add(new JsonObject
            {
                ["kind"] = "race", ["data_id"] = idx, ["name"] = Str(dr["name"]), ["amiga_name"] = Str(ar["name"]),
                ["sprites"] = new JsonObject { ["idle"] = Str(ar["idle"]), ["attacking"] = Str(ar["attacking"]) },
                ["size"] = new JsonObject { ["width"] = 32, ["height"] = 32 },
            });
        }
        foreach (var idx in byRace.Keys.Where(k => !dosRaceIds.Contains(k)).Order())
        {
            var ar = byRace[idx];
            unmapped.Add(new JsonObject
            {
                ["kind"] = "race_portrait", ["amiga_index"] = idx, ["name"] = Str(ar["name"]), ["path"] = Str(ar["idle"]),
                ["reason"] = "Amiga portrait present but no matching race in Phase 3 names.json (playable-race table is 0..14 on DOS)",
            });
        }

        foreach (var scr in ((JsonArray)index["screens"]!).Cast<JsonObject>())
        {
            var source = scr.ContainsKey("source") ? Str(scr["source"])
                : scr.ContainsKey("hunk69_offset") ? $"game hunk 69 +{Str(scr["hunk69_offset"])}"
                : scr.ContainsKey("file_offset") ? $"game @ {Str(scr["file_offset"])}"
                : throw new InvalidDataException($"screen {scr["path"]} has no source / hunk69_offset / file_offset");
            unmapped.Add(new JsonObject
            {
                ["kind"] = "ui_screen", ["path"] = Str(scr["path"]), ["source"] = source,
                ["reason"] = "UI/full-screen art — no monster/item/spell data ID",
            });
        }
        unmapped.Add(new JsonObject
        {
            ["kind"] = "tiles",
            ["reason"] = "Overworld/dungeon tile bitmaps are not separate ADF files; map cells in OUT*.DAT / dng* index " +
                         "graphics embedded in the `game` binary (or drawn at runtime). Needs FS-UAE memory dump + " +
                         "Maptapper / blit-routine RE — see FORMAT.md.",
        });

        var monsterAssets = assets.Where(a => Str(a!["kind"]) == "monster").ToList();
        var manifest = new JsonObject
        {
            ["_schema"] = "phantasie.amiga.asset-manifest/v1",
            ["_generated_by"] = "tools/scripts/build_amiga_asset_manifest.py",
            ["_phase3_sources"] = new JsonArray("reverse-engineering/dos/extracted/monsters.json",
                "reverse-engineering/dos/extracted/names.json"),
            ["_amiga_source"] = "reverse-engineering/amiga/extracted/extraction-index.json",
            ["assets"] = assets,
            ["unmapped"] = unmapped,
            ["counts"] = new JsonObject
            {
                ["mapped_assets"] = assets.Count,
                ["unmapped_notes"] = unmapped.Count,
                ["monsters"] = monsterAssets.Count,
                ["races"] = assets.Count(a => Str(a!["kind"]) == "race"),
            },
        };
        ctx.WriteJsonFile(P1AmigaPipeline.Root + "asset-manifest.json", manifest);

        // GameData: our shipped keys (map_tiles, notes) + the monster sprite entries.
        if (ctx.ReadShipped("phantasie1", "asset-manifest.json") is not JsonObject shipped)
        {
            ctx.Warn("the shipped phantasie1/asset-manifest.json is missing; its Amiga sprite list was not written.");
            return;
        }
        shipped["assets"] = new JsonArray(monsterAssets.Select(a => a!.DeepClone()).ToArray());
        ctx.WriteGameData("phantasie1", "asset-manifest.json", shipped);
    }

    /// <summary>Python's <c>repr</c> of a plain string (single quotes unless it holds one).</summary>
    private static string PyRepr(string s)
    {
        var q = s.Contains('\'') && !s.Contains('"') ? '"' : '\'';
        var body = s.Replace("\\", "\\\\");
        if (q == '\'')
            body = body.Replace("'", "\\'");
        return q + body + q;
    }
}
