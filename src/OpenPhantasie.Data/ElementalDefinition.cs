using System.Text.Json.Serialization;
using OpenPhantasie.Core;

namespace OpenPhantasie.Data;

/// <summary>
/// One `core.spell.summon_elemental` template from <c>elementals.json</c> (TWNS.INT tail, roster records 22–24,
/// <c>tools/scripts/extract_elementals.py</c>). Fields not listed are 0 in the DOS record.
/// </summary>
public sealed class ElementalDefinition
{
    public string Name { get; set; } = "";
    public int Intelligence { get; set; }

    [JsonPropertyName("mp_ceiling")] public int MpCeiling { get; set; }
    [JsonPropertyName("magic_max")] public int MagicMax { get; set; }
    [JsonPropertyName("magic_cur")] public int MagicCur { get; set; }
    [JsonPropertyName("hp_max")] public int HpMax { get; set; }
    [JsonPropertyName("hp_cur")] public int HpCur { get; set; }
    public int Status { get; set; }
    public int Attack { get; set; }
    public int Parry { get; set; }
    public int Level { get; set; }
    [JsonPropertyName("guild_number")] public int GuildNumber { get; set; }
    [JsonPropertyName("weapon_damage_b")] public int WeaponDamageB { get; set; }
    [JsonPropertyName("shield_soak")] public int ShieldSoak { get; set; }
    [JsonPropertyName("armor_soak")] public int ArmorSoak { get; set; }

    /// <summary>Displayed race <c>@256</c> (15 = UNDEAD).</summary>
    public int Race { get; set; }

    /// <summary>Birth race <c>@216</c> — 16, outside the race table; kept raw.</summary>
    [JsonPropertyName("birth_race")] public int BirthRace { get; set; }

    public int Class { get; set; }

    /// <summary>Spell slots holding 1 (known); every other slot holds 2 in the record.</summary>
    [JsonPropertyName("known_spells")] public List<int> KnownSpells { get; set; } = [];

    /// <summary>Phantasie III: all six attributes (STR, INT, DEX, CON, CHA, LCK); null = I's record (INT only).</summary>
    public List<int>? Attributes { get; set; }

    /// <summary>Phantasie III: worn item ids by equipment slot id (<c>shield</c>, <c>armor</c>, <c>bow</c>, <c>weapon</c>); null in I / II.</summary>
    public Dictionary<string, int>? Worn { get; set; }

    /// <summary>The template as a character record (a fresh copy each call).</summary>
    public Character ToCharacter()
    {
        var ch = new Character
        {
            Name = Name,
            Race = (Race)Race,
            BirthRace = (Race)BirthRace,
            Class = (CharacterClass)Class,
            Status = (CharacterStatus)Status,
            Level = Level,
            Stats = Attributes is [var str, var inte, var dex, var con, var cha, var lck]
                ? new CharacterStats(str, inte, dex, con, cha, lck)
                : new CharacterStats(0, Intelligence, 0, 0, 0, 0),
            Skills = new CharacterSkills(Attack, Parry, 0, 0, 0, 0, 0, 0),
            HpMax = HpMax,
            HpCur = HpCur,
            MpCeiling = MpCeiling,
            MagicMax = MagicMax,
            MagicCur = MagicCur,
            WeaponDamageB = WeaponDamageB,
            ShieldSoak = ShieldSoak,
            ArmorSoak = ArmorSoak,
            RosterOrdinal = GuildNumber,
        };
        // III knows spells past I's 55 slots (#55 / #56 = slots 54 / 55): grow the flag array to fit.
        if (KnownSpells.Count > 0 && KnownSpells.Max() >= ch.SpellsKnown.Length)
            ch.SpellsKnown = new int[KnownSpells.Max() + 1];
        Array.Fill(ch.SpellsKnown, 2);
        foreach (var k in KnownSpells)
            ch.SpellsKnown[k] = 1;
        foreach (var (slot, item) in Worn ?? [])
            WornItems.Set(ch, slot, item);
        return ch;
    }
}
