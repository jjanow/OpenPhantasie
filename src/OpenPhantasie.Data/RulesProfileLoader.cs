using System.Text.Json;
using System.Text.Json.Serialization;
using OpenPhantasie.Core;

namespace OpenPhantasie.Data;

/// <summary>
/// Loads a game's <see cref="RulesProfile"/> from <c>GameData/&lt;game&gt;/rules.json</c> (Phase 09 T1.3). Strict: every
/// field is required (except the optional blocks below), and a missing or out-of-range value or an unknown game throws <see cref="InvalidDataException"/> rather than
/// silently playing with a zero cap. The optional blocks are <c>training</c> (<see cref="TrainingRules"/>), <c>terrain</c>
/// (<see cref="TerrainRules"/>), <c>toss_rock</c> (<see cref="TossRockRules"/>) and <c>guild_by_platform</c> (what each original platform's Guild
/// held: <c>{ "dos": { "slots": 22, "max_characters": 22 }, … }</c>); a platform or value no source states is left out.
/// <c>original_platform</c> (the rules source's platform, <see cref="RulesProfile.OriginalPlatform"/>) defaults to <c>dos</c>.
/// </summary>
public static class RulesProfileLoader
{
    public const string FileName = "rules.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>Load the shipped Phantasie I profile (<c>GameData/phantasie1/rules.json</c>).</summary>
    public static RulesProfile LoadPhantasie1() =>
        Load(Path.Combine(GameDataRepository.DefaultPhantasie1Path, FileName));

    public static RulesProfile Load(string path) => Parse(File.ReadAllText(path), path);

    public static RulesProfile Parse(string json, string source = FileName)
    {
        RulesFileDto dto;
        try
        {
            dto = JsonSerializer.Deserialize<RulesFileDto>(json, JsonOptions)
                  ?? throw new InvalidDataException($"{source}: empty rules file.");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"{source}: {ex.Message}", ex);
        }

        var game = Enum.GetValues<GameId>().Where(g => g.Prefix() == dto.Game).Cast<GameId?>().FirstOrDefault()
                   ?? throw new InvalidDataException($"{source}: unknown game \"{dto.Game}\" (expected p1..p4).");
        try
        {
            return new RulesProfile
            {
                Game = game,
                PartyGoldCap = Require(dto.PartyGoldCap, "party_gold_cap", source),
                BankGoldCap = Require(dto.BankGoldCap, "bank_gold_cap", source),
                GuildSlotCount = (int)Require(dto.GuildSlotCount, "guild_slot_count", source),
                GuildMaxCharacters = (int)Require(dto.GuildMaxCharacters, "guild_max_characters", source),
                StartingGold = (int)Require(dto.StartingGold, "starting_gold", source),
                Equipment = new EquipmentLayout(
                    (dto.EquipmentSlots ?? throw new InvalidDataException($"{source}: \"equipment_slots\" is missing."))
                    .Select(e => new EquipmentSlot(
                        e.Id ?? throw new InvalidDataException($"{source}: an equipment slot has no \"id\"."),
                        (int)Require(e.FirstItem, "first_item", source),
                        (int)Require(e.LastItem, "last_item", source)))),
                GuildByPlatform = new PlatformGuildTable((dto.GuildByPlatform ?? []).Select(kv =>
                    KeyValuePair.Create(kv.Key, new PlatformGuildSize(kv.Value?.Slots, kv.Value?.MaxCharacters)))),
                OriginalPlatform = string.IsNullOrWhiteSpace(dto.OriginalPlatform) ? "dos" : dto.OriginalPlatform,
                Training = dto.Training is null ? null : ParseTraining(dto.Training, source),
                Terrain = dto.Terrain is null ? null : ParseTerrain(dto.Terrain, source),
                TossRock = dto.TossRock is null ? null : ParseTossRock(dto.TossRock, source),
                Encounters = dto.Encounters is null ? null : ParseEncounters(dto.Encounters, source),
                DungeonSavePerDungeon = dto.DungeonSavePerDungeon ?? false,
                DropGoldShare = dto.DropGoldShare ?? false,
                Elementals = dto.Elementals is null
                    ? null
                    : new ElementalRules(
                        (int)Require(dto.Elementals.MinMembers, "elementals.min_members", source),
                        dto.Elementals.BlocksAdd ?? throw new InvalidDataException($"{source}: elementals.blocks_add is missing."),
                        dto.Elementals.DropKeeps ?? false,
                        dto.Elementals.KeepsAlone ?? false,
                        dto.Elementals.DropsElemental ?? false,
                        ParsePurge(dto.Elementals.Purge, "elementals.purge", source) ?? ElementalPurge.Clears,
                        ParsePurge(dto.Elementals.PurgeOriginal, "elementals.purge_original", source),
                        dto.Elementals.SummonMinMembers),
            }.Validate();
        }
        catch (ArgumentException ex)
        {
            throw new InvalidDataException($"{source}: {ex.Message}", ex);
        }
    }

    private static readonly string[] StatNames = ["STR", "INT", "DEX", "CON", "CHA", "LUCK"];

    private static TrainingRules ParseTraining(TrainingDto dto, string source)
    {
        var reset = dto.LevelFactorOneWhen switch
        {
            "experience_zero" => TrainingFactorReset.ExperienceZero,
            "level_zero" => TrainingFactorReset.LevelZero,
            var v => throw new InvalidDataException(
                $"{source}: training.level_factor_one_when \"{v}\" (expected experience_zero or level_zero)."),
        };
        var classes = (dto.Classes ?? throw new InvalidDataException($"{source}: training.classes is missing."))
            .Select(c =>
            {
                var cls = Enum.GetValues<CharacterClass>()
                              .Where(k => string.Equals(k.ToString(), c.Class, StringComparison.OrdinalIgnoreCase))
                              .Cast<CharacterClass?>().FirstOrDefault()
                          ?? throw new InvalidDataException($"{source}: training class \"{c.Class}\" is unknown.");
                var w = c.Weights ?? throw new InvalidDataException($"{source}: training {c.Class} has no weights.");
                if (w.Keys.FirstOrDefault(k => !StatNames.Contains(k)) is { } bad)
                    throw new InvalidDataException($"{source}: training {c.Class} weight \"{bad}\" is not a stat.");
                int W(string k) => w.TryGetValue(k, out var v) ? v : 0;
                return new ClassTraining(cls, W("STR"), W("INT"), W("DEX"), W("CON"), W("CHA"), W("LUCK"),
                    c.Divisor ?? throw new InvalidDataException($"{source}: training {c.Class} has no divisor."),
                    c.DivisorFfp is null ? null : Convert.ToUInt32(c.DivisorFfp, 16));
            }).ToList();
        return new TrainingRules(reset, classes)
        {
            ClampCharisma = dto.CharismaClamp
                            ?? throw new InvalidDataException($"{source}: training.charisma_clamp is missing."),
            SpellMarks = dto.SpellMarks switch
            {
                "new_level" => SpellMarkRule.NewLevelOnly,
                "up_to_new_level" => SpellMarkRule.UpToNewLevel,
                var v => throw new InvalidDataException(
                    $"{source}: training.spell_marks \"{v}\" (expected new_level or up_to_new_level)."),
            },
            HpTie = dto.HpTie switch
            {
                "plus_one" => HpRollTie.PlusOne,
                "new_maximum" => HpRollTie.NewMaximum,
                var v => throw new InvalidDataException($"{source}: training.hp_tie \"{v}\" (expected plus_one or new_maximum)."),
            },
            Arithmetic = dto.Arithmetic switch
            {
                null or "double" => TrainingArithmetic.Double,
                "motorola_ffp" => TrainingArithmetic.MotorolaFfp,
                var v => throw new InvalidDataException(
                    $"{source}: training.arithmetic \"{v}\" (expected double or motorola_ffp)."),
            },
        };
    }

    private static TerrainRules ParseTerrain(TerrainDto dto, string source)
    {
        var roll = dto.Roll switch
        {
            "percent" => TerrainRoll.Percent,
            "rand_327" => TerrainRoll.Rand327,
            var v => throw new InvalidDataException($"{source}: terrain.roll \"{v}\" (expected percent or rand_327)."),
        };
        var classes = new Dictionary<int, TerrainClassRule>();
        foreach (var (key, c) in dto.Classes ?? throw new InvalidDataException($"{source}: terrain.classes is missing."))
        {
            if (key.StartsWith('_')) continue;
            if (!int.TryParse(key, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var cls))
                throw new InvalidDataException($"{source}: terrain class \"{key}\" is not a number.");
            var kind = c.Kind switch
            {
                "fixed_fight_once_per_trip" => TerrainKind.FixedFightOncePerTrip,
                "special_site" => TerrainKind.SpecialSite,
                "swim" => TerrainKind.Swim,
                "terrain_fight" => TerrainKind.TerrainFight,
                "lava" => TerrainKind.Lava,
                var v => throw new InvalidDataException($"{source}: terrain class {cls} kind \"{v}\" is unknown."),
            };
            classes[cls] = new TerrainClassRule(kind, c.MonsterPerDigit ?? 0, c.MonsterOffset ?? 0, c.FightRollMax ?? 0,
                c.MonsterSpread ?? 0, c.DamageDivisor ?? 0);
        }
        return new TerrainRules(classes, roll,
            dto.RandomEncounterBelow ?? throw new InvalidDataException($"{source}: terrain.random_encounter_below is missing."));
    }

    private static TossRockRules ParseTossRock(TossRockDto dto, string source)
    {
        int R(int? v, string name) => v ?? throw new InvalidDataException($"{source}: toss_rock.{name} is missing.");
        var races = new Dictionary<Race, int>();
        foreach (var (name, bonus) in dto.RaceBonus ?? [])
        {
            if (name.StartsWith('_')) continue;
            var race = Enum.GetValues<Race>()
                           .Where(r => string.Equals(r.ToString(), name, StringComparison.OrdinalIgnoreCase))
                           .Cast<Race?>().FirstOrDefault()
                       ?? throw new InvalidDataException($"{source}: toss_rock race \"{name}\" is unknown.");
            races[race] = bonus;
        }
        return new TossRockRules(
            R(dto.BonusBase, "bonus_base"), R(dto.BonusPerLevel, "bonus_per_level"), R(dto.BonusPerDex, "bonus_per_dex"),
            R(dto.BonusPerStr, "bonus_per_str"), races, R(dto.CoefficientColumn, "coefficient_column"),
            R(dto.RockDamage, "rock_damage"), R(dto.TargetTries, "target_tries"));
    }

    /// <summary><c>clears</c> / <c>as-drop</c> / <c>keeps</c> / <c>refused</c> (<see cref="ElementalPurge"/>); null when absent.</summary>
    private static ElementalPurge? ParsePurge(string? value, string name, string source) => value switch
    {
        null => null,
        "clears" => ElementalPurge.Clears,
        "as-drop" => ElementalPurge.AsDrop,
        "keeps" => ElementalPurge.Keeps,
        "refused" => ElementalPurge.Refused,
        _ => throw new InvalidDataException($"{source}: \"{name}\" must be clears, as-drop, keeps or refused, not \"{value}\"."),
    };

    private static long Require(long? value, string name, string source) =>
        value ?? throw new InvalidDataException($"{source}: \"{name}\" is missing.");

    private sealed class ElementalsDto
    {
        [JsonPropertyName("min_members")]
        public int? MinMembers { get; set; }

        [JsonPropertyName("blocks_add")]
        public bool? BlocksAdd { get; set; }

        [JsonPropertyName("drop_keeps")]
        public bool? DropKeeps { get; set; }

        [JsonPropertyName("keeps_alone")]
        public bool? KeepsAlone { get; set; }

        [JsonPropertyName("drops_elemental")]
        public bool? DropsElemental { get; set; }

        [JsonPropertyName("purge")]
        public string? Purge { get; set; }

        [JsonPropertyName("purge_original")]
        public string? PurgeOriginal { get; set; }

        [JsonPropertyName("summon_min_members")]
        public int? SummonMinMembers { get; set; }
    }

    private sealed class RulesFileDto
    {
        [JsonPropertyName("game")]
        public string? Game { get; set; }

        [JsonPropertyName("party_gold_cap")]
        public long? PartyGoldCap { get; set; }

        [JsonPropertyName("bank_gold_cap")]
        public long? BankGoldCap { get; set; }

        [JsonPropertyName("guild_slot_count")]
        public int? GuildSlotCount { get; set; }

        [JsonPropertyName("guild_max_characters")]
        public int? GuildMaxCharacters { get; set; }

        [JsonPropertyName("starting_gold")]
        public int? StartingGold { get; set; }

        [JsonPropertyName("equipment_slots")]
        public List<EquipmentSlotDto>? EquipmentSlots { get; set; }

        [JsonPropertyName("guild_by_platform")]
        public Dictionary<string, PlatformGuildDto?>? GuildByPlatform { get; set; }

        [JsonPropertyName("elementals")]
        public ElementalsDto? Elementals { get; set; }

        [JsonPropertyName("dungeon_save_per_dungeon")]
        public bool? DungeonSavePerDungeon { get; set; }

        [JsonPropertyName("drop_gold_share")]
        public bool? DropGoldShare { get; set; }

        [JsonPropertyName("original_platform")]
        public string? OriginalPlatform { get; set; }

        [JsonPropertyName("training")]
        public TrainingDto? Training { get; set; }

        [JsonPropertyName("terrain")]
        public TerrainDto? Terrain { get; set; }

        [JsonPropertyName("toss_rock")]
        public TossRockDto? TossRock { get; set; }

        [JsonPropertyName("encounters")]
        public EncountersDto? Encounters { get; set; }
    }

    private sealed class EncountersDto
    {
        [JsonPropertyName("roll")]
        public string? Roll { get; set; }

        [JsonPropertyName("level_and_reaction")]
        public string? LevelAndReaction { get; set; }

        [JsonPropertyName("group_fit")]
        public string? GroupFit { get; set; }

        [JsonPropertyName("dungeon_tier_factor")]
        public int? DungeonTierFactor { get; set; }

        [JsonPropertyName("day_black_knights")]
        public bool? DayBlackKnights { get; set; }

        [JsonPropertyName("wandering_override_level")]
        public int? WanderingOverrideLevel { get; set; }

        [JsonPropertyName("wandering_override_monster")]
        public int? WanderingOverrideMonster { get; set; }

        [JsonPropertyName("clamp_first_monster")]
        public bool? ClampFirstMonster { get; set; }
    }

    private static EncounterRules ParseEncounters(EncountersDto dto, string source)
    {
        T R<T>(T? v, string name) where T : struct =>
            v ?? throw new InvalidDataException($"{source}: encounters.{name} is missing.");
        return new EncounterRules(
            dto.Roll switch
            {
                "percent" => TerrainRoll.Percent,
                "rand_327" => TerrainRoll.Rand327,
                var v => throw new InvalidDataException($"{source}: encounters.roll \"{v}\" (expected percent or rand_327)."),
            },
            dto.LevelAndReaction switch
            {
                "sum" => false,
                "average" => true,
                var v => throw new InvalidDataException($"{source}: encounters.level_and_reaction \"{v}\" (expected sum or average)."),
            },
            dto.GroupFit switch
            {
                "sprite_width" => EncounterGroupFit.SpriteWidth,
                "sprite_height" => EncounterGroupFit.SpriteHeight,
                var v => throw new InvalidDataException($"{source}: encounters.group_fit \"{v}\" (expected sprite_width or sprite_height)."),
            },
            R(dto.DungeonTierFactor, "dungeon_tier_factor"),
            R(dto.DayBlackKnights, "day_black_knights"),
            dto.WanderingOverrideLevel,
            dto.WanderingOverrideMonster ?? 0,
            R(dto.ClampFirstMonster, "clamp_first_monster"));
    }

    private sealed class TerrainDto
    {
        [JsonPropertyName("roll")]
        public string? Roll { get; set; }

        [JsonPropertyName("random_encounter_below")]
        public int? RandomEncounterBelow { get; set; }

        [JsonPropertyName("classes")]
        public Dictionary<string, TerrainClassDto>? Classes { get; set; }
    }

    private sealed class TerrainClassDto
    {
        [JsonPropertyName("kind")]
        public string? Kind { get; set; }

        [JsonPropertyName("monster_per_digit")]
        public int? MonsterPerDigit { get; set; }

        [JsonPropertyName("monster_offset")]
        public int? MonsterOffset { get; set; }

        [JsonPropertyName("fight_roll_max")]
        public int? FightRollMax { get; set; }

        [JsonPropertyName("monster_spread")]
        public int? MonsterSpread { get; set; }

        [JsonPropertyName("damage_divisor")]
        public int? DamageDivisor { get; set; }
    }

    private sealed class TossRockDto
    {
        [JsonPropertyName("bonus_base")]
        public int? BonusBase { get; set; }

        [JsonPropertyName("bonus_per_level")]
        public int? BonusPerLevel { get; set; }

        [JsonPropertyName("bonus_per_dex")]
        public int? BonusPerDex { get; set; }

        [JsonPropertyName("bonus_per_str")]
        public int? BonusPerStr { get; set; }

        [JsonPropertyName("race_bonus")]
        public Dictionary<string, int>? RaceBonus { get; set; }

        [JsonPropertyName("coefficient_column")]
        public int? CoefficientColumn { get; set; }

        [JsonPropertyName("rock_damage")]
        public int? RockDamage { get; set; }

        [JsonPropertyName("target_tries")]
        public int? TargetTries { get; set; }
    }

    private sealed class TrainingDto
    {
        [JsonPropertyName("level_factor_one_when")]
        public string? LevelFactorOneWhen { get; set; }

        [JsonPropertyName("classes")]
        public List<ClassTrainingDto>? Classes { get; set; }

        [JsonPropertyName("charisma_clamp")]
        public bool? CharismaClamp { get; set; }

        [JsonPropertyName("spell_marks")]
        public string? SpellMarks { get; set; }

        [JsonPropertyName("hp_tie")]
        public string? HpTie { get; set; }

        [JsonPropertyName("arithmetic")]
        public string? Arithmetic { get; set; }
    }

    private sealed class ClassTrainingDto
    {
        [JsonPropertyName("class")]
        public string? Class { get; set; }

        [JsonPropertyName("weights")]
        public Dictionary<string, int>? Weights { get; set; }

        [JsonPropertyName("divisor")]
        public double? Divisor { get; set; }

        [JsonPropertyName("divisor_ffp")]
        public string? DivisorFfp { get; set; }
    }

    private sealed class EquipmentSlotDto
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("first_item")]
        public int? FirstItem { get; set; }

        [JsonPropertyName("last_item")]
        public int? LastItem { get; set; }
    }

    private sealed class PlatformGuildDto
    {
        [JsonPropertyName("slots")]
        public int? Slots { get; set; }

        [JsonPropertyName("max_characters")]
        public int? MaxCharacters { get; set; }
    }
}
