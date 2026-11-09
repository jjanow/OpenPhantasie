namespace OpenPhantasie.Core;

/// <summary>
/// Phantasie IV's own tables (T6.3) as Core's IV rules read them: built by <c>OpenPhantasie.Data</c> from
/// <c>GameData/phantasie4/rule-tables.json</c> (or by a test from a fixture). Nothing here is a formula; the specs are named
/// per block (<c>reverse-engineering/x68000/phantasie4/logic-specs/</c>).
/// </summary>
public sealed record P4Tables(
    P4CreationTables Creation,
    P4AgingTables Aging,
    P4TrainingTables Training,
    P4TownTables Towns,
    P4CombatTables Combat,
    P4OutdoorTables Outdoor,
    P4ManualAmounts Amounts,
    P4ServiceTables? Services = null,
    SharedAgingModel? SharedAging = null) // QoL shared-aging (13-aging-normalised.md): GameData/shared/aging.json
{
    /// <summary>The service tables (towns, guild, menus); a table set built without them cannot run those screens.</summary>
    public P4ServiceTables Service => Services ?? throw new InvalidOperationException("IV's service tables are not loaded.");
}

/// <summary>Classes, races and abilities by number (IV numbering, <c>names.json</c>).</summary>
public static class P4Ids
{
    public const int Thief = 0, Monk = 1, Ranger = 2, Fighter = 3, Priest = 4, Wizard = 5, ShadowWarrior = 6,
        GargoyleClass = 7, Sage = 8, Lord = 9;

    public const int Human = 0, Dwarf = 1, Elf = 2, Halfling = 3, Gnome = 4, Pixie = 5, Sprite = 6, Orc = 11,
        HillGiant = 15, GargoyleRace = 16, Spriggan = 17, Unknown = 33, Duck = 34;

    /// <summary>The five races of I–III's creation menu (0–4) — the base races of the social roll.</summary>
    public const int BaseRaces = 5;

    /// <summary>Player classes 0–7 have <c>TABL</c> rows; 8 Sage and 9 Lord are Hall persons only.</summary>
    public const int PlayerClasses = 8;

    public const int Skills = 11;
}

/// <summary>Skill order (record <c>+3C + 2k</c>, <c>skills.md</c>).</summary>
public enum P4Skill
{
    Attack = 0, Parry = 1, SpotItem = 2, SpotTrap = 3, Disarm = 4, Listen = 5, FireBow = 6, PickLock = 7, Swim = 8,
    Navigation = 9, Throwing = 10,
}

/// <summary>Creation and ancestry tables (<c>ancestry.md</c>).</summary>
public sealed record P4CreationTables(
    IReadOnlyList<IReadOnlyList<int>> RaceAbilityMod,
    IReadOnlyList<IReadOnlyList<int>> ClassAbilityMod,
    IReadOnlyDictionary<int, IReadOnlyList<int>> SubBonusPseudoRows,
    IReadOnlyList<IReadOnlyList<int>> Combine5x5,
    IReadOnlyList<(int A, int B, int Result)> CombinePairs,
    IReadOnlyList<double> InheritanceExponent,
    double NewPersonExponent,
    IReadOnlyDictionary<int, IReadOnlyList<int>> RandomRaceLists,
    int RandomRaceRareChance,
    IReadOnlyList<string> ClassMaskByRace,
    IReadOnlyList<int> StrideRace,
    IReadOnlyList<int> StrideClass,
    IReadOnlyDictionary<int, int> StrideManualOverrides,
    int GargoyleRace,
    string GargoyleManualMask,
    IReadOnlyList<int> SocialThresholds,
    int SocialDieBaseRaces,
    int SocialDieOtherRaces,
    IReadOnlyList<int> SocialBiasRaces,
    int SocialBias,
    int SocialBiasCap,
    int HallSuperHeroGives,
    int HallDarkLordGives,
    int GoldPerSocial,
    IReadOnlyDictionary<int, IReadOnlyList<int>> StartingSpells,
    int StartingItem,
    int StartingItemExceptClass,
    IReadOnlyList<int> FlierRaces,
    int DivineAidSpell,
    int NikademusHallSlot,
    int NikademusCountsAsRace,
    int MaxDescendants);

/// <summary>Aging and lifespans (<c>towns.md</c> §2.2, <c>ancestry.md</c> §7, D4-033 / D4-034).</summary>
public sealed record P4AgingTables(
    IReadOnlyDictionary<int, double> FactorByRace,
    double FactorOther,
    int FactorCap,
    double DeclineAt,
    int DaysPerAgeUnit,
    IReadOnlyDictionary<int, int> ManualBaseYears,
    int ManualBaseYearsOther,
    double ManualSpreadMin,
    double ManualSpreadPerLuck,
    int ManualLuckMin,
    int ManualLuckMax);

/// <summary>Level-up, skills, spells and training (<c>training.md</c>, <c>skills.md</c>, D4-015 / 016 / 019).</summary>
public sealed record P4TrainingTables(
    IReadOnlyList<IReadOnlyList<int>> HpGrowth,
    IReadOnlyList<IReadOnlyList<int>> MagicLevel,
    IReadOnlyList<IReadOnlyList<IReadOnlyList<int>>> SkillGrowth,
    IReadOnlyList<IReadOnlyList<IReadOnlyList<int>>> SpellsGranted,
    int FighterSpellLimitManual,
    int PriestManualMaxLevel,
    int PriestManualFirstExtraLevel,
    int PriestManualSpellsPerLevel,
    int MaxLevel,
    int ScoreGate,
    IReadOnlyList<IReadOnlyList<int>> XpWeights,
    IReadOnlyList<double> XpDivisors,
    int CostCap,
    int GoldPerLevel,
    IReadOnlyList<int> MagicQuotaClassTerm,
    int MagicPriceBase,
    int SpellCount);

/// <summary>Towns, shops and the bank (<c>towns.md</c>, D4-020 / 030 / 031 / 032 / 035).</summary>
public sealed record P4TownTables(
    IReadOnlyList<int> PriceIndexByMap,
    int InnHealPoints,
    int TownInnDaysManual,
    int GoldCapManual,
    int SellCapCode,
    long LootCapCode,
    int LevelGoldCapCode,
    int ResaleNumerator,
    int ResaleDenominator,
    IReadOnlyDictionary<int, int> ItemPointsManual,
    IReadOnlyList<int> PotionHpManual,
    IReadOnlyList<int> PotionHealPointsManual,
    IReadOnlyList<int> ShopRandomItems);

/// <summary>
/// Combat and spell numbers (<c>combat.md</c>). <c>SpellPagesCode</c>: <c>_getmagic(member, 0)</c>'s five category pages
/// as spell numbers in page order (s152, FORMAT.md 6.9 "Combat spell list"); null in a fixture = one page of every spell.
/// <c>SpellPageHeads</c>: the string ids of its head lines (the pages, then Quit).
/// </summary>
public sealed record P4CombatTables(
    IReadOnlyList<int> MpCostManual,
    IReadOnlyList<int> MpCostCode,
    int AttackFrom,
    int SlashFrom,
    int AimPenalty,
    int LungePenalty,
    int ManualAttackStrikes,
    int ManualAttackHitPenalty,
    int ManualSlashHitPenalty,
    IReadOnlyList<int> ArrowFlameHit,
    IReadOnlyList<int> ArrowFlameDamage,
    IReadOnlyList<double> WeaknessFactor,
    IReadOnlyList<int> FireFlashN,
    IReadOnlyList<int> MindBlastN,
    IReadOnlyList<int> HealingManualN,
    IReadOnlyList<int> FlameballN,
    IReadOnlyList<double> FlameballRadiusManual,
    IReadOnlyList<int> AutoDefenceCode,
    IReadOnlyList<int> AutoAttackWalkCode,
    IReadOnlyList<int> AutoAttackManual,
    IReadOnlyList<int> AutoDefenceManual,
    double MonsterHpCap,
    int TreasureDungeonChance,
    int TreasureOutdoorLevelAdd,
    IReadOnlyList<int>? ShockWave = null,
    IReadOnlyList<int>? FlameballCodeSquares = null,
    IReadOnlyList<int>? FlameballCodeOffsets = null,
    int AnnihilateBase = 2,
    int AnnihilateSpread = 2,
    IReadOnlyList<IReadOnlyList<int>>? SpellPagesCode = null,
    IReadOnlyList<string>? SpellPageHeads = null);

/// <summary>
/// Outdoors: terrain, water, ships, Levitation (<c>outdoor.md</c>, D4-038 / 039 / 040). By terrain type (tile / 10):
/// hours per step and encounter per mille (null = the type's handler makes no roll) [C].
/// </summary>
public sealed record P4OutdoorTables(
    int LevitationStrideMultiple,
    int NavigationDriftBelowManual,
    IReadOnlyList<int> HoursByType,
    IReadOnlyList<int?> EncounterPerMilleByType,
    IReadOnlyList<int> AreaLevelByMap,
    IReadOnlyList<int> BlockedTypes,
    int CharonRiverType,
    IReadOnlyList<int> CharonItems,
    int SwimDie,
    int SwimAdd,
    int LavaDie,
    int StormBelow,
    int StormMoves,
    IReadOnlyList<string> TerrainNameIds,
    P4OverviewTable Overview)
{
    /// <summary>The terrain name <c>_omain</c> prints (table <c>0x42D30</c>: entry type + 1 for types 0–10; else entry 0, empty).</summary>
    public string? TerrainNameId(int type) => type is >= 0 and < 11 ? TerrainNameIds[type + 1] : null;
}

/// <summary>
/// Amounts behind rules the user decided for the manual (s82) where the manual gives no number and no platform has one;
/// the user set them in s84 (<c>DIFFERENCES-IV.md</c>). Null = <c>[?]</c>, a question for the user: the manual path of such
/// a rule refuses to guess (<see cref="P4Amount.Require"/>). The ship's life in steps is the option <c>p4.ship-life</c>.
/// </summary>
public sealed record P4ManualAmounts(
    int? ShipWreckChancePerSeaFight = null,
    int? SpotItemIdentifyAt = null,
    int? ExamineSearchBonus = null,
    int? FlameballStrongArmourAt = null,
    int? BegItemsPerMember = null,
    int? DissolveRounds = null,
    int? IdentifyChanceBase = null,
    int? IdentifyChancePerLevel = null,
    int? MonsterEvalWrongBase = null,
    int? MonsterEvalWrongPerLevel = null,
    int? MonsterEvalWrongMin = null,
    int? MonsterEvalWrongBy = null);

/// <summary>An amount the manual's rule needs but nobody has given yet (<see cref="P4ManualAmounts"/>).</summary>
public sealed class P4AmountUnknownException(string what)
    : InvalidOperationException($"[?] {what}: the manual gives no amount and none is decided yet (DIFFERENCES-IV.md).");

public static class P4Amount
{
    /// <summary>The amount, or a <see cref="P4AmountUnknownException"/> naming the D-entry.</summary>
    public static int Require(int? amount, string what) => amount ?? throw new P4AmountUnknownException(what);
}

/// <summary>The game's generator as the specs use it: <c>rand(n)</c> returns 1..n, <c>rand(0)</c> = 1 (<c>_rand</c>).</summary>
public static class P4Dice
{
    public static int Rand(IRandomSource rng, int n)
    {
        ArgumentNullException.ThrowIfNull(rng);
        return n <= 0 ? 1 : rng.Next(1, n + 1);
    }

    /// <summary><c>_frand</c>: rand(100) / 100 (0.01 … 1.00).</summary>
    public static double Frand(IRandomSource rng) => Rand(rng, 100) / 100.0;
}

/// <summary>A IV place that offers spells outside combat (<c>_getmagic</c> places 5, 6, 7).</summary>
public enum P4CastPlace { Outdoors = 5, Dungeon = 6, Town = 7 }

/// <summary>
/// The town buildings, the guild and the out-of-combat menus (s102; <c>rule-tables.json</c> <c>services</c>, <c>towns</c>,
/// <c>magic_bag</c>; specs <c>towns.md</c> §3, §6, <c>training.md</c> §4.1–§5, <c>spells-noncombat.md</c>).
/// </summary>
public sealed record P4ServiceTables(
    int RosterSlots,
    int PartyMax,
    int ElementalChoices,
    int ElementalDefault,
    IReadOnlyList<int> PartyStrategyChoices,
    IReadOnlyList<int> PartyStrategyReset,
    IReadOnlyList<int> MemberStrategyChoices,
    int MpGrowthPercentPerPoint,
    int LevelRowPeriod,
    IReadOnlyList<int> ThiefRowBeforeTable,
    IReadOnlyDictionary<P4CastPlace, IReadOnlyList<int>> SpellPlacesCode,
    IReadOnlyDictionary<P4CastPlace, IReadOnlyList<int>> SpellPlacesManual,
    IReadOnlyList<int> TargetedSpells,
    int MagicHallPage,
    int ShopPage,
    IReadOnlyList<int> PartyInfoKindFirstIds,
    IReadOnlyList<string> DivineAidHints,
    int EndingFirstFlag,
    IReadOnlyList<int> EndingScores,
    int EndingNobleAt,
    int EndingNoble,
    int EndingLordFrom,
    int EndingLord,
    int EndingMinCharisma,
    int ShipCost,
    int MagicTrainingAgeUnits,
    int JoinAgeUnits,
    IReadOnlyList<IReadOnlyList<int>> TransportGroups,
    IReadOnlyDictionary<int, int> TownTransportGroup,
    int MagicBagCost,
    IReadOnlyList<int> MagicBagItems,
    P4EndingText EndingText,
    IReadOnlyList<IReadOnlyList<string>> IdentifyLines,
    P4Transfer BoxTarget,
    IReadOnlyList<string> BoxLines,
    IReadOnlyList<string> TownNameIds,
    IReadOnlyList<int> DungeonMonsterLevels,
    int OutdoorInnCost,
    int OutdoorInnHours);

/// <summary>
/// The ending's text (<c>_endmsg</c> blocks, <c>_ending</c>'s order per ending, <c>_common_proc</c>'s Lord titles,
/// <c>_endstory</c>'s final screen): string ids.
/// </summary>
/// <summary>
/// The M overview's 3 × 3 blocks of maps (display only; <c>_omaps</c> <c>0x42ECC</c>, <c>_mvb</c> <c>0x43732</c>, <c>FORMAT.md</c> §6.9).
/// </summary>
/// <param name="Blocks">Each block's nine maps, row-major.</param>
/// <param name="BlockByMap">The block a jump to map n shows (index = map).</param>
/// <param name="NetherworldFrom">From this map on only the party's own map is shown; the other cells show <paramref name="HiddenMap"/>.</param>
public sealed record P4OverviewTable(
    IReadOnlyList<IReadOnlyList<int>> Blocks, IReadOnlyList<int> BlockByMap, int NetherworldFrom, int HiddenMap)
{
    /// <summary>The block shown after arriving on <paramref name="map"/>: <paramref name="current"/> while it holds the map (a walk), else the map's own.</summary>
    public int BlockFor(int map, int current, bool jump) =>
        !jump && current >= 0 && current < Blocks.Count && Blocks[current].Contains(map) ? current
        : map >= 0 && map < BlockByMap.Count ? BlockByMap[map] : 0;

    /// <summary>
    /// D4-072 <c>centred</c> (the remaster's reading of the manual): the nine maps around <paramref name="map"/>, row-major,
    /// the map in the middle; −1 for a cell past the edge of its world. The map grid comes from <see cref="Blocks"/>:
    /// overlapping blocks share one grid (Scandor), a block sharing no map with another is a world of its own (the
    /// Netherworld). An unknown map gives nine −1 except the middle.
    /// </summary>
    public IReadOnlyList<int> CentredOn(int map)
    {
        var at = Grid();
        var cells = Enumerable.Repeat(-1, 9).ToArray();
        cells[4] = map;
        if (!at.TryGetValue(map, out var p))
            return cells;
        var byPos = at.Where(kv => kv.Value.World == p.World).ToDictionary(kv => (kv.Value.X, kv.Value.Y), kv => kv.Key);
        for (var k = 0; k < 9; k++)
            if (byPos.TryGetValue((p.X + k % 3 - 1, p.Y + k / 3 - 1), out var m))
                cells[k] = m;
        return cells;
    }

    private Dictionary<int, (int World, int X, int Y)>? _grid;

    /// <summary>Each map's world and grid square, laid out from the blocks (a block joins the grid of a block it overlaps).</summary>
    private Dictionary<int, (int World, int X, int Y)> Grid()
    {
        if (_grid is not null)
            return _grid;
        var at = new Dictionary<int, (int World, int X, int Y)>();
        var placed = new bool[Blocks.Count];
        var world = 0;
        for (var start = 0; start < Blocks.Count; start++)
        {
            if (placed[start])
                continue;
            Place(start, world, 0, 0);
            for (var grew = true; grew;)
            {
                grew = false;
                for (var b = 0; b < Blocks.Count; b++)
                {
                    if (placed[b])
                        continue;
                    var k = Enumerable.Range(0, Blocks[b].Count)
                        .FirstOrDefault(i => at.TryGetValue(Blocks[b][i], out var q) && q.World == world, -1);
                    if (k < 0)
                        continue;
                    var q2 = at[Blocks[b][k]];
                    Place(b, world, q2.X - k % 3, q2.Y - k / 3);
                    grew = true;
                }
            }
            world++;
        }
        return _grid = at;

        void Place(int b, int w, int x0, int y0)
        {
            placed[b] = true;
            for (var i = 0; i < Blocks[b].Count; i++)
                at.TryAdd(Blocks[b][i], (w, x0 + i % 3, y0 + i / 3));
        }
    }
}

public sealed record P4EndingText(
    IReadOnlyList<IReadOnlyList<string>> Messages,
    IReadOnlyList<IReadOnlyList<int>> Sequences,
    int LordTitleBlock,
    IReadOnlyList<string> FinalLines,
    IReadOnlyList<string> ScoreIds,
    IReadOnlyList<string> CodeIds,
    IReadOnlyList<string> PostcardLines,
    string SaveQuestion,
    string OfId,
    string LordId,
    IReadOnlyList<int> BanquetBeforeBlocks,
    int CellBeforeBlock);
