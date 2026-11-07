using System.Globalization;
using System.Text.Json;
using OpenPhantasie.Core;

namespace OpenPhantasie.Data;

/// <summary>
/// Phantasie IV's location maps for Core's <see cref="P4Dungeon"/> (T6.3): each level of
/// <c>GameData/phantasie4/dungeons.json</c> as a <see cref="P4DungeonFile"/> — the whole <c>IND&lt;n&gt;.PDT</c>
/// (<c>words_hex</c>, 31 × 31 big-endian words) and the <c>O_0nn</c> trigger entries and bytecode from the code base. The
/// shared loader keeps only the floor bytes, which IV's movement map cannot be built from.
/// </summary>
public static class Phantasie4World
{
    /// <summary>Location maps 1–21 by number, from the game folder <paramref name="gameDataDirectory"/>.</summary>
    public static IReadOnlyDictionary<int, P4DungeonFile> LoadDungeonFiles(string gameDataDirectory)
    {
        var path = Path.Combine(gameDataDirectory, "dungeons.json");
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var files = new Dictionary<int, P4DungeonFile>();
        foreach (var level in doc.RootElement.GetProperty("levels").EnumerateObject())
        {
            var l = level.Value;
            var map = int.Parse(l.GetProperty("id").GetString()!.Split(':')[1], CultureInfo.InvariantCulture);
            var bytes = Convert.FromHexString(l.GetProperty("words_hex").GetString()!);
            var words = new ushort[bytes.Length / 2];
            for (var i = 0; i < words.Length; i++)
                words[i] = (ushort)((bytes[2 * i] << 8) | bytes[2 * i + 1]);
            var events = l.GetProperty("events");
            var triggers = events.GetProperty("triggers").EnumerateArray()
                .Select(t => new P4EventTrigger(t.GetProperty("x").GetInt32(), t.GetProperty("y").GetInt32(),
                    t.GetProperty("dir").GetInt32(), t.GetProperty("address").GetInt32()))
                .ToList();
            var code = Convert.FromHexString(events.GetProperty("code_hex").GetString()!);
            files[map] = new P4DungeonFile(map, l.GetProperty("name").GetString() ?? "", words, triggers, code);
        }
        if (files.Count != 21 || files.Keys.Min() != 1 || files.Keys.Max() != 21)
            throw new InvalidDataException($"{path}: expected location maps 1–21, got {files.Count}.");
        return files;
    }

    /// <summary>The shipped folder's maps (<see cref="GameDataRepository.DefaultPathFor"/>).</summary>
    public static IReadOnlyDictionary<int, P4DungeonFile> LoadDungeonFiles() =>
        LoadDungeonFiles(GameDataRepository.DefaultPathFor(GameId.P4));

    /// <summary>
    /// The outdoor maps, towns and place names (<c>overworld.json</c>, <c>towns.json</c>, the location maps' names) the
    /// outdoor and town loops read.
    /// </summary>
    public static P4Atlas LoadAtlas(string gameDataDirectory, IReadOnlyDictionary<int, P4DungeonFile> dungeons)
    {
        using var ow = JsonDocument.Parse(File.ReadAllText(Path.Combine(gameDataDirectory, "overworld.json")));
        var outdoors = new Dictionary<int, P4OutdoorMap>();
        foreach (var r in ow.RootElement.GetProperty("regions").EnumerateObject())
        {
            var e = r.Value;
            var n = e.GetProperty("region_id").GetInt32();
            var tiles = e.GetProperty("tiles").EnumerateArray()
                .SelectMany(row => row.EnumerateArray().Select(t => (byte)t.GetInt32())).ToArray();
            var h = e.GetProperty("header");
            var dest = h.GetProperty("destination");
            outdoors[n] = new P4OutdoorMap(
                n, tiles,
                h.GetProperty("towns").EnumerateArray().Select(t => t.GetProperty("town").GetInt32()).ToList(),
                h.GetProperty("dungeons").EnumerateArray().Select(d => new P4DungeonSite(
                    d.GetProperty("location").GetInt32(), d.GetProperty("x").GetInt32(), d.GetProperty("y").GetInt32())).ToList(),
                h.GetProperty("inns").EnumerateArray().Select(i => i.GetInt32()).ToList(),
                new P4Transfer(dest.GetProperty("map_kind").GetInt32(), dest.GetProperty("map").GetInt32(),
                    dest.GetProperty("x").GetInt32(), dest.GetProperty("y").GetInt32(), 0));
        }
        var inns = new Dictionary<int, string>();
        foreach (var p in ow.RootElement.GetProperty("places").EnumerateArray())
        {
            if (p.GetProperty("type").GetString() == "inn")
                inns[p.GetProperty("number").GetInt32()] = p.GetProperty("name_id").GetString()!;
        }

        using var tw = JsonDocument.Parse(File.ReadAllText(Path.Combine(gameDataDirectory, "towns.json")));
        var towns = new Dictionary<int, P4TownSite>();
        foreach (var t in tw.RootElement.GetProperty("towns").EnumerateArray())
        {
            var exit = t.GetProperty("exit");
            var ship = t.GetProperty("ship");
            var id = t.GetProperty("id").GetInt32();
            towns[id] = new P4TownSite(id, t.GetProperty("name").GetString() ?? "", t.GetProperty("town_type").GetInt32(),
                exit.GetProperty("map").GetInt32(), exit.GetProperty("x").GetInt32(), exit.GetProperty("y").GetInt32(),
                ship.ValueKind == JsonValueKind.Object ? ship.GetProperty("x").GetInt32() : null,
                ship.ValueKind == JsonValueKind.Object ? ship.GetProperty("y").GetInt32() : null);
        }
        if (outdoors.Count != 24 || towns.Count != 21)
            throw new InvalidDataException($"{gameDataDirectory}: expected outdoor maps 1–24 and towns 1–21.");
        return new P4Atlas(outdoors, towns, dungeons.ToDictionary(d => d.Key, d => d.Value.Name), inns);
    }

    /// <summary>
    /// The battle's data (<see cref="P4BattleData"/>): <c>monsters.json</c> (<c>DATM</c>), the items' combat fields from
    /// <c>items.json</c> and the field tables of <c>rule-tables.json</c> (<c>combat</c>, <c>combat.battle</c>).
    /// </summary>
    public static P4BattleData LoadBattleData(string gameDataDirectory)
    {
        using var md = JsonDocument.Parse(File.ReadAllText(Path.Combine(gameDataDirectory, "monsters.json")));
        static int[] Ints(JsonElement e) => [.. e.EnumerateArray().Select(x => x.GetInt32())];
        var monsters = md.RootElement.GetProperty("monsters").EnumerateArray().Select(m => new P4MonsterType(
            m.GetProperty("index").GetInt32(), m.GetProperty("name").GetString() ?? "", m.GetProperty("group_max").GetInt32(),
            m.GetProperty("attack_power").GetInt32(), m.GetProperty("damage").GetInt32(), m.GetProperty("armor").GetInt32(),
            m.GetProperty("defense").GetInt32(), m.GetProperty("level").GetInt32(), m.GetProperty("xp").GetInt64(),
            m.GetProperty("magic_resistance").GetInt32(), m.GetProperty("hp_base").GetInt32(), m.GetProperty("nice").GetInt32(),
            m.GetProperty("gold").GetInt64(), m.GetProperty("spell_ability").GetInt32(), Ints(m.GetProperty("spell_ids")),
            m.GetProperty("next_group_chance").GetInt32(), Ints(m.GetProperty("companions")), m.GetProperty("movement").GetInt32(),
            Ints(m.GetProperty("treasure_items")), m.GetProperty("flier").GetBoolean(), m.GetProperty("body_shape").GetInt32(),
            m.GetProperty("damage_type_flag").GetBoolean(), m.GetProperty("undead_or_demon").GetBoolean())).ToList();
        if (monsters.Where((m, i) => m.Index != i).Any())
            throw new InvalidDataException($"{gameDataDirectory}: monsters.json must list DATM records 0… in order.");

        using var id = JsonDocument.Parse(File.ReadAllText(Path.Combine(gameDataDirectory, "items.json")));
        var items = new Dictionary<int, P4ItemStats>();
        foreach (var i in id.RootElement.GetProperty("items").EnumerateArray())
        {
            var kind = i.GetProperty("kind").GetString() switch
            {
                "shield" => P4ItemKind.Shield,
                "armor" => P4ItemKind.Armour,
                "weapon" => P4ItemKind.Weapon,
                "bow" => P4ItemKind.Bow,
                "thrown" => P4ItemKind.Thrown,
                _ => P4ItemKind.Other,
            };
            var check = i.TryGetProperty("str_check", out var c) ? c.GetString() switch
            {
                "str2_plus_dex" => P4StrCheck.Str2PlusDex,
                "str_plus_dex" => P4StrCheck.StrPlusDex,
                _ => P4StrCheck.Str,
            } : P4StrCheck.Str;
            var n = i.GetProperty("index").GetInt32();
            items[n] = new P4ItemStats(n, kind, i.TryGetProperty("points", out var p) ? p.GetInt32() : 0,
                i.TryGetProperty("min_str", out var s) ? s.GetInt32() : 0, check,
                i.TryGetProperty("move_pct", out var mv) ? mv.GetInt32() : 0);
        }

        using var rd = JsonDocument.Parse(File.ReadAllText(Path.Combine(gameDataDirectory, Phantasie4RuleTables.FileName)));
        var cb = rd.RootElement.GetProperty("combat");
        var b = cb.GetProperty("battle");
        var fields = b.GetProperty("fields").EnumerateObject().Select(f =>
        {
            var mo = f.Value.GetProperty("monster");
            int Opt(string name) => mo.TryGetProperty(name, out var v) ? v.GetInt32() : 0;
            return new P4Battlefield(f.Name, Ints(f.Value.GetProperty("types")),
                f.Value.GetProperty("obstacles").EnumerateArray().Select(o => new P4ObstacleRoll(o[0].GetInt32(), o[1].GetInt32(),
                    o[2].GetInt32(), o[3].GetBoolean())).ToList(),
                new P4MonsterPick(Opt("die"), Opt("add"), Opt("area_level_times"), Opt("override_below"), Opt("override_die"),
                    Opt("override_add")));
        }).ToList();
        var crit = cb.GetProperty("crit_threshold_by_item").EnumerateObject()
            .ToDictionary(p => int.Parse(p.Name, CultureInfo.InvariantCulture), p => p.Value.GetInt32());
        var missile = cb.GetProperty("missile_class_bonus");
        var treasure = cb.GetProperty("treasure");
        var tables = new P4BattleTables(
            fields, b.GetProperty("obstacle_squares").GetProperty("die").GetInt32(),
            b.GetProperty("obstacle_squares").GetProperty("add").GetInt32(), Ints(b.GetProperty("placement_columns")),
            Ints(b.GetProperty("edge_squares")),
            b.GetProperty("side_exits").EnumerateArray().Select(e => (e[0].GetInt32(), e[1].GetInt32())).ToList(),
            b.GetProperty("groups").GetProperty("max").GetInt32(), b.GetProperty("wake_monster_below").GetInt32(),
            b.GetProperty("monster_flee_below_percent").GetInt32(), crit, Ints(missile.GetProperty("bow")),
            Ints(missile.GetProperty("thrown")), Ints(cb.GetProperty("protection")), Ints(cb.GetProperty("speed")),
            Ints(cb.GetProperty("strength")), Ints(cb.GetProperty("ninja_stride")),
            treasure.GetProperty("dungeon_extra_below").GetInt32(), Ints(treasure.GetProperty("extra_items")),
            b.GetProperty("event_monsters").EnumerateObject().Where(p => !p.Name.StartsWith('_'))
                .ToDictionary(p => int.Parse(p.Name, CultureInfo.InvariantCulture), p => p.Value.GetInt32()));
        using var ed = JsonDocument.Parse(File.ReadAllText(Path.Combine(gameDataDirectory, "elementals.json")));
        var elementals = ed.RootElement.GetProperty("elementals").EnumerateArray().Select(e => new P4ElementalTemplate(
            e.GetProperty("name").GetString() ?? "", e.GetProperty("level").GetInt32(), e.GetProperty("hp_max").GetInt32(),
            e.GetProperty("magic_max").GetInt32(), Ints(e.GetProperty("attributes")), e.GetProperty("attack").GetInt32(),
            e.GetProperty("parry").GetInt32(), e.GetProperty("weapon_damage_b").GetInt32(),
            e.GetProperty("melee_bonus").GetInt32(), e.GetProperty("shield_soak").GetInt32(),
            e.GetProperty("armor_soak").GetInt32(), e.GetProperty("movement_base").GetInt32(),
            Ints(e.GetProperty("known_spells")).Select(s => s + 1).ToList(), Ints(e.GetProperty("strategy")))).ToList();
        return new P4BattleData(monsters, items, tables, elementals);
    }

    /// <summary>The shipped folder's battle data.</summary>
    public static P4BattleData LoadBattleData() => World.Value.Battle;

    /// <summary>The shipped folder's atlas.</summary>
    public static P4Atlas LoadAtlas() => World.Value.Atlas;

    /// <summary>A new <see cref="Phantasie4Campaign"/> on the shipped data, with the party at <paramref name="start"/>.</summary>
    public static Phantasie4Campaign NewCampaign(GameOptions options, P4Party party, IRandomSource rng, P4Location start)
    {
        var w = World.Value;
        return new Phantasie4Campaign(options, w.Tables, w.Atlas, party, rng, n => w.Files[n], w.ItemName, start, w.Battle,
            w.Services);
    }

    /// <summary>The town a new game starts in: <c>_load(0)</c> sets map 1, place 0 (town), Pendragon (<c>FORMAT.md</c> §6.1).</summary>
    public const int NewGameTown = 1;

    /// <summary>
    /// A new IV game (<c>_load(0)</c> → <c>_init</c> → <c>_town</c>): an empty guild and party, the Hall as shipped, in
    /// Pendragon with the town entry run. Characters are made at the Guild (roster N).
    /// </summary>
    public static Phantasie4Campaign NewGame(GameOptions options, IRandomSource rng)
    {
        var game = NewCampaign(options, new P4Party(), rng, new P4Location(P4Place.Town, NewGameTown, 0, 0));
        game.Start();
        return game;
    }

    /// <summary>A saved IV game on the shipped data (<see cref="Phantasie4SaveCodec"/>).</summary>
    public static Phantasie4Campaign RestoreCampaign(P4CampaignSnapshot snapshot, IRandomSource rng)
    {
        var w = World.Value;
        return Phantasie4Campaign.Restore(snapshot, w.Tables, w.Atlas, rng, n => w.Files[n], w.ItemName, w.Battle,
            w.Services);
    }

    /// <summary>The dungeon places of the outdoor maps (<c>MOUT</c>, <c>_odungeon</c>): location map → its entry square.</summary>
    public static IReadOnlyDictionary<int, (string Name, int X, int Y)> DungeonEntries(string gameDataDirectory)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(gameDataDirectory, "overworld.json")));
        var entries = new SortedDictionary<int, (string, int, int)>();
        foreach (var p in doc.RootElement.GetProperty("places").EnumerateArray())
        {
            if (p.GetProperty("type").GetString() != "dungeon" || !p.TryGetProperty("entry", out var e))
                continue;
            entries[p.GetProperty("number").GetInt32()] =
                (p.GetProperty("name").GetString() ?? "", e.GetProperty("x").GetInt32(), e.GetProperty("y").GetInt32());
        }
        return entries;
    }

    public static IReadOnlyDictionary<int, (string Name, int X, int Y)> DungeonEntries() =>
        DungeonEntries(GameDataRepository.DefaultPathFor(GameId.P4));

    /// <summary>
    /// The outdoor square of each location map's place (top-left of its 2 × 2 block, <c>overworld.json</c> places): outdoor
    /// map, x, y. Only the maps reached from the outdoors have one.
    /// </summary>
    public static IReadOnlyDictionary<int, (int Map, int X, int Y)> DungeonPlaces()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(GameDataRepository.DefaultPathFor(GameId.P4),
            "overworld.json")));
        var places = new SortedDictionary<int, (int, int, int)>();
        foreach (var p in doc.RootElement.GetProperty("places").EnumerateArray())
        {
            if (p.GetProperty("type").GetString() == "dungeon")
                places[p.GetProperty("number").GetInt32()] = (int.Parse(p.GetProperty("plane").GetString()![3..],
                    CultureInfo.InvariantCulture), p.GetProperty("x").GetInt32(), p.GetProperty("y").GetInt32());
        }
        return places;
    }

    private sealed record WorldData(
        P4Tables Tables, IReadOnlyDictionary<int, P4DungeonFile> Files, P4Atlas Atlas, Func<int, string> ItemName,
        P4BattleData Battle, P4ServiceData Services);

    /// <summary>The shipped folder's items, names and Hall (<see cref="LoadServiceData(string)"/>).</summary>
    public static P4ServiceData LoadServiceData() => World.Value.Services;

    /// <summary>
    /// What the town buildings and the party menus read (s102): the item catalogue (<c>items.json</c>: name, price, kind,
    /// combat fields), IV's names (<c>names.json</c>) and the Hall of Glory as shipped (<c>rule-tables.json</c> <c>hall</c>).
    /// </summary>
    public static P4ServiceData LoadServiceData(string gameDataDirectory)
    {
        var battle = LoadBattleData(gameDataDirectory);
        using var id = JsonDocument.Parse(File.ReadAllText(Path.Combine(gameDataDirectory, "items.json")));
        var items = id.RootElement.GetProperty("items").EnumerateArray().Select(i =>
        {
            var n = i.GetProperty("index").GetInt32();
            var s = battle.Item(n);
            return new P4ItemInfo(n, i.GetProperty("name").GetString() ?? "", i.GetProperty("value").GetInt32(),
                i.GetProperty("kind").GetString() ?? "", s?.Kind ?? P4ItemKind.Other, s?.Points ?? 0, s?.MinStr ?? 0,
                s?.Check ?? P4StrCheck.Str, i.TryGetProperty("icon", out var icon) ? icon.GetInt32() : 0);
        }).ToList();
        using var nd = JsonDocument.Parse(File.ReadAllText(Path.Combine(gameDataDirectory, "names.json")));
        IReadOnlyList<string> Names(string key) => nd.RootElement.GetProperty(key).EnumerateArray()
            .OrderBy(e => e.GetProperty("index").GetInt32()).Select(e => e.GetProperty("name").GetString() ?? "").ToList();
        var names = new P4Names(Names("races"), Names("classes"), Names("social_classes"), Names("origins"), Names("spells"),
            Names("skills"));
        using var rd = JsonDocument.Parse(File.ReadAllText(Path.Combine(gameDataDirectory, Phantasie4RuleTables.FileName)));
        static int[] Ints(JsonElement e) => [.. e.EnumerateArray().Select(x => x.GetInt32())];
        var hall = rd.RootElement.GetProperty("hall").GetProperty("persons").EnumerateArray().Select(h => new P4HallPerson(
            h.GetProperty("slot").GetInt32(), h.GetProperty("name").GetString() ?? "", h.GetProperty("race").GetInt32(),
            h.GetProperty("cls").GetInt32(), h.GetProperty("level").GetInt32(), h.GetProperty("social").GetInt32(),
            h.GetProperty("origin").GetInt32(), Ints(h.GetProperty("stats_str_int_dex_con_cha")),
            Ints(h.GetProperty("p1_p2_p3_results")), h.GetProperty("descendants").GetInt32(),
            h.GetProperty("deeds_line").GetInt32(), h.GetProperty("alive").GetInt32() != 0)).ToList();
        return new P4ServiceData(new P4ItemCatalog(items), names, hall);
    }

    /// <summary>The shipped data, read once: the files never change while the program runs.</summary>
    private static readonly Lazy<WorldData> World = new(ReadWorld);

    private static WorldData ReadWorld()
    {
        var dir = GameDataRepository.DefaultPathFor(GameId.P4);
        var tables = GameTables.Load(dir).Phantasie4?.Tables
            ?? throw new InvalidDataException($"{dir}: IV's {Phantasie4RuleTables.FileName} is missing.");
        var names = GameDataRepository.Load(dir).Items.ToDictionary(i => i.Id, i => i.Name);
        var files = LoadDungeonFiles(dir);
        return new WorldData(tables, files, LoadAtlas(dir, files), id => names.TryGetValue(id, out var n) ? n : $"#{id}",
            LoadBattleData(dir), LoadServiceData(dir));
    }
}
