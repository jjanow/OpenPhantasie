using System.Text.Json;
using System.Text.Json.Nodes;
using OpenPhantasie.Core;

namespace OpenPhantasie.Data;

/// <summary>
/// <c>starter-guild.json</c> (s218, written by an import of the C64 Phantasie III, never shipped): the pre-generated Guild
/// members of the copy imported (<see cref="StarterGuildMember"/>). Optional. Its <c>tables</c> names the tables it came
/// with: when another copy's tables were imported later (DOS III, the Amiga, the Apple II), <c>monsters.json</c>'s
/// <c>_source</c> no longer starts with it and the file is ignored, so a C64 roster never seeds another version's game.
/// </summary>
public static class StarterGuildLoader
{
    public const string FileName = "starter-guild.json";

    public static IReadOnlyList<StarterGuildMember> Load(string gameDataDirectory)
    {
        var path = Path.Combine(gameDataDirectory, FileName);
        if (!File.Exists(path))
            return [];
        var root = JsonNode.Parse(File.ReadAllBytes(path))?.AsObject()
                   ?? throw new InvalidDataException($"{path}: not a JSON object");
        if ((string?)root["tables"] is { Length: > 0 } tables && !TablesStartWith(gameDataDirectory, tables))
            return [];
        return [.. (root["members"]?.AsArray() ?? []).Select(n => Member(n!.AsObject(), path))];
    }

    private static bool TablesStartWith(string dir, string label)
    {
        var monsters = Path.Combine(dir, "monsters.json");
        if (!File.Exists(monsters))
            return false;
        using var doc = JsonDocument.Parse(File.ReadAllBytes(monsters));
        return doc.RootElement.TryGetProperty("_source", out var s) && s.ValueKind == JsonValueKind.String
               && s.GetString()!.StartsWith(label, StringComparison.Ordinal);
    }

    private static StarterGuildMember Member(JsonObject o, string path)
    {
        int I(JsonNode? n, string what) => n is null ? throw new InvalidDataException($"{path}: {what} missing") : (int)n;
        T E<T>(string key) where T : struct, Enum =>
            Enum.TryParse<T>((string?)o[key], ignoreCase: true, out var v)
                ? v
                : throw new InvalidDataException($"{path}: unknown {key} '{(string?)o[key]}'");
        var a = (o["attributes"] ?? throw new InvalidDataException($"{path}: attributes missing")).AsArray().Select(v => (int)v!).ToArray();
        if (a.Length != 6)
            throw new InvalidDataException($"{path}: attributes are STR INT DEX CON CHA LCK");
        var sk = o["skills"]?.AsObject() ?? throw new InvalidDataException($"{path}: skills missing");
        int S(string k) => I(sk[k], $"skills.{k}");
        var worn = o["worn"]?.AsObject();
        return new StarterGuildMember
        {
            Slot = I(o["slot"], "slot"),
            Name = (string?)o["name"] ?? throw new InvalidDataException($"{path}: name missing"),
            Race = E<Race>("race"),
            Class = E<CharacterClass>("class"),
            Level = I(o["level"], "level"),
            Stats = new CharacterStats(a[0], a[1], a[2], a[3], a[4], a[5]),
            HpMax = I(o["hp_max"], "hp_max"),
            HpCur = I(o["hp_cur"], "hp_cur"),
            MpCeiling = I(o["mp_ceiling"], "mp_ceiling"),
            MagicMax = I(o["mp_max"], "mp_max"),
            MagicCur = I(o["mp_cur"], "mp_cur"),
            Gold = I(o["gold"], "gold"),
            Experience = (long?)o["experience"] ?? 0,
            AgeClock = (int?)o["age_clock"], // null = Guild New's start age (D3-078)
            Status = E<CharacterStatus>("status"),
            Score = (int?)o["score"] ?? 0,
            Skills = new CharacterSkills(S("attack"), S("parry"), S("find_item"), S("find_trap"), S("disarm_trap"),
                S("listen"), S("pick_lock"), S("swim")),
            FireBow = (int?)sk["fire_bow"] ?? 0,
            SocialClass = (int?)o["social_class"] ?? 0,
            Origin = (int?)o["origin"],
            Town = I(o["town"], "town"),
            WornShield = (int?)worn?["shield"] ?? 0,
            WornArmor = (int?)worn?["armor"] ?? 0,
            WornWeapon = (int?)worn?["weapon"] ?? 0,
            WornBow = (int?)worn?["bow"] ?? 0,
            Carried = [.. (o["carried"]?.AsArray() ?? []).Select(v => (int)v!)],
            SpellsKnown = [.. (o["spells_known"]?.AsArray() ?? []).Select(v => (int)v!)],
        };
    }
}
