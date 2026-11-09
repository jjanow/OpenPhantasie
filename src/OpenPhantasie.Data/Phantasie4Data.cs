using System.Text.Json;
using OpenPhantasie.Core;

namespace OpenPhantasie.Data;

/// <summary>
/// Phantasie IV's own tables (<c>GameData/phantasie4/rule-tables.json</c>, T6.3, built by
/// <c>tools/scripts/build_p4_gamedata.py</c>): <see cref="Tables"/> is what Core's IV rules read; <see cref="Hall"/> is the
/// shipped Hall of Glory; the rest stays raw for later (the campaign).
/// </summary>
public sealed class Phantasie4RuleTables
{
    public const string FileName = "rule-tables.json";

    public required P4Tables Tables { get; init; }

    /// <summary>Class names 0–9 and race names 0–34 (IV numbering).</summary>
    public required IReadOnlyList<string> Classes { get; init; }

    public required IReadOnlyList<string> Races { get; init; }

    /// <summary>The Hall of Glory as shipped (<c>FAME</c>, slots 0–2), as Core's ancestors.</summary>
    public required IReadOnlyList<P4Ancestor> Hall { get; init; }

    /// <summary>The Hall persons' names, by slot.</summary>
    public required IReadOnlyList<string> HallNames { get; init; }

    public static Phantasie4RuleTables Load(string path)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(path), new JsonDocumentOptions
        {
            CommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        });
        var r = doc.RootElement;
        try
        {
            return Parse(r);
        }
        catch (Exception ex) when (ex is KeyNotFoundException or InvalidOperationException or FormatException)
        {
            throw new InvalidDataException($"{path}: {ex.Message}", ex);
        }
    }

    private static Phantasie4RuleTables Parse(JsonElement r)
    {
        var c = r.GetProperty("creation");
        var social = c.GetProperty("social");
        var bias = c.GetProperty("social_bias_manual");
        var garg = c.GetProperty("gargoyle_classes_manual");
        var creation = new P4CreationTables(
            RaceAbilityMod: Ints2(c.GetProperty("race_ability_mod")),
            ClassAbilityMod: Ints2(c.GetProperty("class_ability_mod")),
            SubBonusPseudoRows: IntKeyed(c.GetProperty("sub_bonus_pseudo_rows"), Ints),
            Combine5x5: Ints2(c.GetProperty("combine_5x5")),
            CombinePairs: c.GetProperty("combine_pairs").EnumerateArray()
                .Select(p => (p.GetProperty("a").GetInt32(), p.GetProperty("b").GetInt32(), p.GetProperty("result").GetInt32()))
                .ToList(),
            InheritanceExponent: c.GetProperty("inheritance_exponent").EnumerateArray().Select(x => x.GetDouble()).ToList(),
            NewPersonExponent: c.GetProperty("new_person_exponent").GetDouble(),
            RandomRaceLists: IntKeyed(c.GetProperty("random_race_lists"), Ints),
            RandomRaceRareChance: c.GetProperty("random_race_rare_chance").GetInt32(),
            ClassMaskByRace: c.GetProperty("class_mask_by_race").EnumerateArray().Select(x => x.GetString()!).ToList(),
            StrideRace: Ints(c.GetProperty("stride_race")),
            StrideClass: Ints(c.GetProperty("stride_class")),
            StrideManualOverrides: IntKeyed(c.GetProperty("stride_manual_overrides"), x => x.GetInt32()),
            GargoyleRace: garg.GetProperty("race").GetInt32(),
            GargoyleManualMask: garg.GetProperty("mask").GetString()!,
            SocialThresholds: Ints(social.GetProperty("thresholds")),
            SocialDieBaseRaces: social.GetProperty("die_base_races").GetInt32(),
            SocialDieOtherRaces: social.GetProperty("die_other_races").GetInt32(),
            SocialBiasRaces: Ints(bias.GetProperty("races")),
            SocialBias: bias.GetProperty("bonus").GetInt32(),
            SocialBiasCap: bias.GetProperty("cap").GetInt32(),
            HallSuperHeroGives: social.GetProperty("hall_super_hero_gives").GetInt32(),
            HallDarkLordGives: social.GetProperty("hall_dark_lord_gives").GetInt32(),
            GoldPerSocial: c.GetProperty("gold_per_social").GetInt32(),
            StartingSpells: StartingSpells(c.GetProperty("starting_spells"), r.GetProperty("classes")),
            StartingItem: c.GetProperty("starting_item").GetProperty("item").GetInt32(),
            StartingItemExceptClass: c.GetProperty("starting_item").GetProperty("except_class").GetInt32(),
            FlierRaces: Ints(c.GetProperty("fliers").GetProperty("races")),
            DivineAidSpell: c.GetProperty("divine_aid_spell").GetInt32(),
            NikademusHallSlot: c.GetProperty("nikademus_hall_slot").GetInt32(),
            NikademusCountsAsRace: c.GetProperty("nikademus_counts_as_race").GetProperty("race").GetInt32(),
            MaxDescendants: c.GetProperty("max_descendants").GetInt32());

        var a = r.GetProperty("aging");
        var life = r.GetProperty("lifespans_manual");
        var factors = a.GetProperty("factor_by_race");
        var baseYears = life.GetProperty("base_years");
        var aging = new P4AgingTables(
            FactorByRace: Numbered(factors, x => x.GetDouble()),
            FactorOther: factors.GetProperty("other").GetDouble(),
            FactorCap: a.GetProperty("factor_cap").GetInt32(),
            DeclineAt: a.GetProperty("decline_at").GetDouble(),
            DaysPerAgeUnit: a.GetProperty("days_per_age_unit").GetInt32(),
            ManualBaseYears: Numbered(baseYears, x => x.GetInt32()),
            ManualBaseYearsOther: baseYears.GetProperty("other").GetInt32(),
            ManualSpreadMin: life.GetProperty("spread_min").GetDouble(),
            ManualSpreadPerLuck: life.GetProperty("spread_per_luck").GetDouble(),
            ManualLuckMin: life.GetProperty("luck_min").GetInt32(),
            ManualLuckMax: life.GetProperty("luck_max").GetInt32());

        var tr = r.GetProperty("training");
        var priest = r.GetProperty("priest_manual");
        var maxLevel = r.GetProperty("max_level");
        var training = new P4TrainingTables(
            HpGrowth: Ints2(r.GetProperty("hp_growth").GetProperty("by_class")),
            MagicLevel: Ints2(r.GetProperty("magic_level").GetProperty("by_class")),
            SkillGrowth: r.GetProperty("skill_growth").GetProperty("by_skill").EnumerateArray().Select(Ints2).ToList(),
            SpellsGranted: r.GetProperty("spells_granted").GetProperty("by_class").EnumerateArray().Select(Ints2).ToList(),
            FighterSpellLimitManual: r.GetProperty("fighter_spell_limit_manual").GetInt32(),
            PriestManualMaxLevel: priest.GetProperty("max_level").GetInt32(),
            PriestManualFirstExtraLevel: priest.GetProperty("first_extra_level").GetInt32(),
            PriestManualSpellsPerLevel: priest.GetProperty("spells_per_level").GetInt32(),
            MaxLevel: maxLevel.GetProperty("level").GetInt32(),
            ScoreGate: maxLevel.GetProperty("score_gate").GetInt32(),
            XpWeights: Ints2(tr.GetProperty("weights")),
            XpDivisors: tr.GetProperty("divisors").EnumerateArray().Select(x => x.GetDouble()).ToList(),
            CostCap: tr.GetProperty("cost_cap").GetInt32(),
            GoldPerLevel: tr.GetProperty("gold_per_level").GetInt32(),
            MagicQuotaClassTerm: Ints(tr.GetProperty("magic_quota_class_term")),
            MagicPriceBase: tr.GetProperty("magic_price_base").GetInt32(),
            SpellCount: r.GetProperty("combat").GetProperty("mp_cost_code").GetArrayLength());

        var tw = r.GetProperty("towns");
        var caps = r.GetProperty("gold_caps");
        var resale = r.GetProperty("resale_manual");
        var potions = r.GetProperty("potions");
        var towns = new P4TownTables(
            PriceIndexByMap: Ints(tw.GetProperty("price_index_by_map")),
            InnHealPoints: tw.GetProperty("inn_heal_points").GetInt32(),
            TownInnDaysManual: tw.GetProperty("town_inn_days_manual").GetInt32(),
            GoldCapManual: caps.GetProperty("manual").GetInt32(),
            SellCapCode: caps.GetProperty("x68000_sell").GetInt32(),
            LootCapCode: caps.GetProperty("x68000_loot").GetInt64(),
            LevelGoldCapCode: caps.GetProperty("x68000_level_gold").GetInt32(),
            ResaleNumerator: resale.GetProperty("numerator").GetInt32(),
            ResaleDenominator: resale.GetProperty("denominator").GetInt32(),
            ItemPointsManual: Numbered(r.GetProperty("item_points_manual"), x => x.GetInt32()),
            PotionHpManual: Ints(potions.GetProperty("manual_hp")),
            PotionHealPointsManual: Ints(potions.GetProperty("manual_heal_points")),
            ShopRandomItems: Ints(r.GetProperty("shop_random_items").GetProperty("items")));

        var cb = r.GetProperty("combat");
        var auto = cb.GetProperty("auto_spells_code");
        var autoManual = cb.GetProperty("auto_spells_manual");
        var arrow = cb.GetProperty("arrow_flame");
        var treasure = cb.GetProperty("treasure");
        var combat = new P4CombatTables(
            MpCostManual: Ints(cb.GetProperty("mp_cost_manual")),
            MpCostCode: Ints(cb.GetProperty("mp_cost_code")),
            AttackFrom: cb.GetProperty("attack_from").GetInt32(),
            SlashFrom: cb.GetProperty("slash_from").GetInt32(),
            AimPenalty: cb.GetProperty("aim_penalty").GetInt32(),
            LungePenalty: cb.GetProperty("lunge_penalty").GetInt32(),
            ManualAttackStrikes: cb.GetProperty("manual_strikes").GetProperty("attack").GetInt32(),
            ManualAttackHitPenalty: cb.GetProperty("manual_strikes").GetProperty("hit_penalty").GetProperty("attack").GetInt32(),
            ManualSlashHitPenalty: cb.GetProperty("manual_strikes").GetProperty("hit_penalty").GetProperty("slash").GetInt32(),
            ArrowFlameHit: Ints(arrow.GetProperty("hit")),
            ArrowFlameDamage: Ints(arrow.GetProperty("damage")),
            WeaknessFactor: cb.GetProperty("weakness_factor").EnumerateArray().Select(x => x.GetDouble()).ToList(),
            FireFlashN: Ints(cb.GetProperty("fire_flash_n")),
            MindBlastN: Ints(cb.GetProperty("mind_blast_n")),
            HealingManualN: Ints(cb.GetProperty("healing_manual_n")),
            FlameballN: Ints(cb.GetProperty("flameball_n")),
            FlameballRadiusManual: cb.GetProperty("flameball_radius_manual").EnumerateArray().Select(x => x.GetDouble()).ToList(),
            AutoDefenceCode: Ints(auto.GetProperty("defence_random")),
            AutoAttackWalkCode: Ints(auto.GetProperty("attack_walk")),
            AutoAttackManual: Ints(autoManual.GetProperty("attack")),
            AutoDefenceManual: Ints(autoManual.GetProperty("defence")),
            MonsterHpCap: cb.GetProperty("monster_hp").GetProperty("cap").GetDouble(),
            TreasureDungeonChance: treasure.GetProperty("dungeon_chance").GetInt32(),
            TreasureOutdoorLevelAdd: treasure.GetProperty("outdoor_level_add").GetInt32(),
            ShockWave: Ints(cb.GetProperty("shock_wave")),
            FlameballCodeSquares: Ints(cb.GetProperty("flameball_code_squares")),
            FlameballCodeOffsets: Ints(cb.GetProperty("flameball_code_offsets")),
            AnnihilateBase: cb.GetProperty("annihilate").GetProperty("base_per_mp").GetInt32(),
            AnnihilateSpread: cb.GetProperty("annihilate").GetProperty("spread_per_mp").GetInt32(),
            SpellPagesCode: Ints2(cb.GetProperty("spell_pages_code").GetProperty("pages")),
            SpellPageHeads: [.. cb.GetProperty("spell_pages_code").GetProperty("heads").EnumerateArray().Select(x => x.GetString()!)]);

        var od = r.GetProperty("outdoor_terrain");
        var outdoor = new P4OutdoorTables(
            LevitationStrideMultiple: od.GetProperty("levitation_manual").GetProperty("stride_multiple").GetInt32(),
            NavigationDriftBelowManual: od.GetProperty("navigation_manual").GetProperty("drift_below").GetInt32(),
            HoursByType: Ints(od.GetProperty("hours")),
            EncounterPerMilleByType: od.GetProperty("encounter_per_mille").EnumerateArray()
                .Select(x => x.ValueKind == JsonValueKind.Number ? x.GetInt32() : (int?)null).ToList(),
            AreaLevelByMap: Ints(od.GetProperty("area_level_by_map")),
            BlockedTypes: Ints(od.GetProperty("blocked")),
            CharonRiverType: od.GetProperty("needs_charon_items").GetProperty("type").GetInt32(),
            CharonItems: Ints(od.GetProperty("needs_charon_items").GetProperty("items")),
            SwimDie: od.GetProperty("swim_damage").GetProperty("die").GetInt32(),
            SwimAdd: od.GetProperty("swim_damage").GetProperty("add").GetInt32(),
            LavaDie: od.GetProperty("lava_damage_die").GetInt32(),
            StormBelow: od.GetProperty("ship_storm_below").GetInt32(),
            StormMoves: od.GetProperty("storm_moves").GetInt32(),
            TerrainNameIds: od.GetProperty("name_ids_by_code_index").EnumerateArray().Select(x => x.GetString()!).ToList(),
            Overview: new P4OverviewTable(
                Blocks: Ints2(od.GetProperty("overview").GetProperty("blocks")),
                BlockByMap: Ints(od.GetProperty("overview").GetProperty("block_by_map")),
                NetherworldFrom: od.GetProperty("overview").GetProperty("netherworld_from").GetInt32(),
                HiddenMap: od.GetProperty("overview").GetProperty("hidden_map").GetInt32()));

        var am = r.GetProperty("manual_amounts");
        int? Amount(string name) => am.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt32() : null;
        var amounts = new P4ManualAmounts(
            ShipWreckChancePerSeaFight: Amount("ship_wreck_chance_per_sea_fight"),
            SpotItemIdentifyAt: Amount("spot_item_identify_at"),
            ExamineSearchBonus: Amount("examine_search_bonus"),
            FlameballStrongArmourAt: Amount("flameball_strong_armour_at"),
            BegItemsPerMember: Amount("beg_items_per_member"),
            DissolveRounds: Amount("dissolve_rounds"),
            IdentifyChanceBase: Amount("identify_chance_base"),
            IdentifyChancePerLevel: Amount("identify_chance_per_level"),
            MonsterEvalWrongBase: Amount("monster_eval_wrong_base"),
            MonsterEvalWrongPerLevel: Amount("monster_eval_wrong_per_level"),
            MonsterEvalWrongMin: Amount("monster_eval_wrong_min"),
            MonsterEvalWrongBy: Amount("monster_eval_wrong_by"));

        var sv = r.GetProperty("services");
        var places = sv.GetProperty("spell_places");
        IReadOnlyDictionary<P4CastPlace, IReadOnlyList<int>> Places(JsonElement e) => new Dictionary<P4CastPlace, IReadOnlyList<int>>
        {
            [P4CastPlace.Outdoors] = Ints(e.GetProperty("outdoors")),
            [P4CastPlace.Dungeon] = Ints(e.GetProperty("dungeon")),
            [P4CastPlace.Town] = Ints(e.GetProperty("town")),
        };
        var end = sv.GetProperty("ending");
        var transport = tw.GetProperty("transport_groups");
        var bag = r.GetProperty("magic_bag");
        var services = new P4ServiceTables(
            RosterSlots: sv.GetProperty("roster_slots").GetInt32(),
            PartyMax: sv.GetProperty("party_max").GetInt32(),
            ElementalChoices: sv.GetProperty("elementals").GetProperty("choices").GetInt32(),
            ElementalDefault: sv.GetProperty("elementals").GetProperty("default").GetInt32(),
            PartyStrategyChoices: Ints(sv.GetProperty("party_strategy").GetProperty("choices")),
            PartyStrategyReset: Ints(sv.GetProperty("party_strategy").GetProperty("reset")),
            MemberStrategyChoices: Ints(sv.GetProperty("member_strategy").GetProperty("choices")),
            MpGrowthPercentPerPoint: sv.GetProperty("mp_growth_percent_per_point").GetProperty("value").GetInt32(),
            LevelRowPeriod: sv.GetProperty("level_row_wrap").GetProperty("period").GetInt32(),
            ThiefRowBeforeTable: Ints(sv.GetProperty("level_row_wrap").GetProperty("thief_row_before_table")),
            SpellPlacesCode: Places(places.GetProperty("x68000")),
            SpellPlacesManual: Places(places.GetProperty("manual")),
            TargetedSpells: Ints(places.GetProperty("targeted")),
            MagicHallPage: sv.GetProperty("magic_hall_page").GetInt32(),
            ShopPage: sv.GetProperty("shop_page").GetInt32(),
            PartyInfoKindFirstIds: Ints(sv.GetProperty("party_info_kinds").GetProperty("first_ids")),
            DivineAidHints: sv.GetProperty("divine_aid_hints").EnumerateArray().Select(x => x.GetString()!).ToList(),
            EndingFirstFlag: end.GetProperty("first_flag").GetInt32(),
            EndingScores: Ints(end.GetProperty("scores")),
            EndingNobleAt: end.GetProperty("noble_at").GetInt32(),
            EndingNoble: end.GetProperty("noble").GetInt32(),
            EndingLordFrom: end.GetProperty("lord_from").GetInt32(),
            EndingLord: end.GetProperty("lord").GetInt32(),
            EndingMinCharisma: end.GetProperty("min_charisma").GetInt32(),
            ShipCost: tw.GetProperty("ship_cost").GetInt32(),
            MagicTrainingAgeUnits: tw.GetProperty("magic_training_age_units").GetInt32(),
            JoinAgeUnits: tw.GetProperty("join_from_other_town_age_units").GetInt32(),
            TransportGroups: Ints2(transport.GetProperty("groups")),
            TownTransportGroup: Numbered(transport.GetProperty("town_group"), x => x.GetInt32()),
            MagicBagCost: bag.GetProperty("cost").GetInt32(),
            MagicBagItems: Ints(bag.GetProperty("items")),
            EndingText: new P4EndingText(
                Messages: end.GetProperty("messages").EnumerateArray().Select(Strs).ToList(),
                Sequences: Ints2(end.GetProperty("sequences")),
                LordTitleBlock: end.GetProperty("lord_title_block").GetInt32(),
                FinalLines: Strs(end.GetProperty("final_lines")),
                ScoreIds: Strs(end.GetProperty("score_ids")),
                CodeIds: Strs(end.GetProperty("code_ids")),
                PostcardLines: Strs(end.GetProperty("postcard_lines")),
                SaveQuestion: end.GetProperty("save_question").GetString()!,
                OfId: end.GetProperty("of_id").GetString()!,
                LordId: end.GetProperty("lord_id").GetString()!,
                BanquetBeforeBlocks: Ints(end.GetProperty("banquet_before_blocks")),
                CellBeforeBlock: end.GetProperty("cell_before_block").GetInt32()),
            IdentifyLines: sv.GetProperty("identify_lines").GetProperty("by_item").EnumerateArray().Select(Strs).ToList(),
            BoxTarget: new P4Transfer(P4Transfer.Outdoors, sv.GetProperty("box").GetProperty("map").GetInt32(),
                sv.GetProperty("box").GetProperty("x").GetInt32(), sv.GetProperty("box").GetProperty("y").GetInt32(), 0),
            BoxLines: Strs(sv.GetProperty("box").GetProperty("lines")),
            TownNameIds: Strs(sv.GetProperty("town_name_ids").GetProperty("ids")),
            DungeonMonsterLevels: Ints(r.GetProperty("dungeon_monster_level_by_map")),
            OutdoorInnCost: tw.GetProperty("outdoor_inn_cost").GetInt32(),
            OutdoorInnHours: tw.GetProperty("outdoor_inn_hours").GetInt32());
        static IReadOnlyList<string> Strs(JsonElement e) => e.EnumerateArray().Select(x => x.GetString()!).ToList();

        var hall = r.GetProperty("hall").GetProperty("persons").EnumerateArray().ToList();
        return new Phantasie4RuleTables
        {
            Tables = new P4Tables(creation, aging, training, towns, combat, outdoor, amounts, services,
                SharedAgingLoader.Shipped),
            Classes = r.GetProperty("classes").EnumerateArray().Select(x => x.GetString()!).ToList(),
            Races = r.GetProperty("races").EnumerateArray().Select(x => x.GetString()!).ToList(),
            Hall = hall.Select(h => new P4Ancestor(
                h.GetProperty("race").GetInt32(), h.GetProperty("cls").GetInt32(),
                Ints(h.GetProperty("stats_str_int_dex_con_cha")), h.GetProperty("slot").GetInt32(),
                h.GetProperty("social").GetInt32(), Ints(h.GetProperty("p1_p2_p3_results")))).ToList(),
            HallNames = hall.Select(h => h.GetProperty("name").GetString()!).ToList(),
        };
    }

    private static IReadOnlyList<int> Ints(JsonElement e) => e.EnumerateArray().Select(x => x.GetInt32()).ToList();

    private static IReadOnlyList<IReadOnlyList<int>> Ints2(JsonElement e) => e.EnumerateArray().Select(Ints).ToList();

    /// <summary>An object keyed by numbers (keys starting with '_' are notes; non-numeric keys are skipped).</summary>
    private static IReadOnlyDictionary<int, T> Numbered<T>(JsonElement e, Func<JsonElement, T> value) =>
        e.EnumerateObject().Where(p => int.TryParse(p.Name, out _))
            .ToDictionary(p => int.Parse(p.Name, System.Globalization.CultureInfo.InvariantCulture), p => value(p.Value));

    private static IReadOnlyDictionary<int, T> IntKeyed<T>(JsonElement e, Func<JsonElement, T> value) => Numbered(e, value);

    private static IReadOnlyDictionary<int, IReadOnlyList<int>> StartingSpells(JsonElement e, JsonElement classes)
    {
        var names = classes.EnumerateArray().Select(x => x.GetString()).ToList();
        return e.EnumerateObject().Where(p => !p.Name.StartsWith('_'))
            .ToDictionary(p => names.IndexOf(p.Name) is var i and >= 0 ? i
                    : throw new InvalidOperationException($"starting_spells: unknown class {p.Name}"),
                p => Ints(p.Value));
    }
}
