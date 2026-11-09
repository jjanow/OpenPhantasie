namespace OpenPhantasie.Core;

/// <summary>
/// The Phantasie III campaign (Phase 09 T4; DOS SSI 1988 rules, specs under <c>reverse-engineering/dos/phantasie3/</c>):
/// III's world (the planes, hard-coded places, tile terrain, the wilderness encounter roll), its dungeon cell codes and
/// event programs (<see cref="Phantasie3DungeonScript"/>), the two-group encounter builder and encounter options, Guild
/// training (<see cref="P3Training"/>), creation (social class, class kit, first spell), the Mystic's score, the Astral
/// Plane and the gods' reward. The shared engine runs town, travel, fights and saves; combat itself is still the shared
/// resolver (III's action set, ranks and body-area injury in fights are an open item, <c>09-trilogy.md</c> T4).
/// One instance per session: it carries the game's world data (<see cref="P3WorldData"/>), built by <c>OpenPhantasie.Data</c>,
/// and the current fight's III state.
/// </summary>
public sealed partial class Phantasie3Campaign : ICampaign
{
    /// <summary>The campaign without world data (<see cref="Campaigns.For"/>): enough for menus; a session needs the data one.</summary>
    public static Phantasie3Campaign Unloaded { get; } = new(null);

    private readonly P3WorldData? _world;

    public Phantasie3Campaign(P3WorldData? world) => _world = world;

    /// <summary>The world data; throws on <see cref="Unloaded"/>.</summary>
    public P3WorldData World =>
        _world ?? throw new InvalidOperationException("The Phantasie III campaign has no world data: build it with OpenPhantasie.Data.");

    /// <summary>Town 1, Pendragon: a new game, the gods' reward.</summary>
    public const int HomeTownId = 1;

    /// <summary>Scandor's towns are 1–14; 15 Light, 16 Dark, 17 Death (<c>planes.md</c> §1).</summary>
    public const int ScandorTownCount = 14;

    public const int LightTown = 15, DarkTown = 16, DeathTown = 17;

    /// <summary>Plane numbers = overworld region ids (<c>planes.md</c> §1).</summary>
    public const int Scandor = 0, LightPlane = 1, DarkPlane = 2, Netherworld = 4;

    /// <summary>The final bosses (<c>endgame.md</c> §1): 77 / 78 Nikademus, 79 Lord Wood.</summary>
    public const int NikademusA = 77, NikademusB = 78, LordWood = 79;

    /// <summary>A Darklord's score and a Superhero's (<c>endgame.md</c> §3).</summary>
    public const int DarklordScore = 200, SuperheroScore = 250;

    /// <summary>The dungeon encounter chance <c>FAR:0xdd8c</c> on entry (ovl4 <c>0x679</c>) [C] (s83).</summary>
    public const int DungeonChanceOnEntry = 10;

    public GameId Game => GameId.P3;

    public FeatureModuleSet DefaultFeatureModules => FeatureModuleSet.ForGame(GameId.P3);

    /// <summary>III's death step is not traced; I's (DEAD, D-009 CON loss) is assumed [~], as for II.</summary>
    public IMemberDeathRule Death => Phantasie3DeathRule.Instance;

    // --- World facts -----------------------------------------------------------------------------------------------------

    public int StartTownId => HomeTownId;

    private static readonly IReadOnlyList<int> ScandorTowns = Enumerable.Range(1, ScandorTownCount).ToArray();

    /// <summary>
    /// TRANSPORTATION (#54, root <c>0x8e50</c>, <c>town.md</c> §8) [C]: the 14 Scandor towns, cast from any town (the plane
    /// towns 15–17 included) but never to 15–17. D3-035 (user s89): not a conflict, "interdimensionally" = from any plane's
    /// town back to Scandor; reaching the plane towns by spell would skip the quest route. The party lands on the town's
    /// square (<see cref="OnTownEntered"/>).
    /// </summary>
    public IReadOnlyList<int> TransportTowns => ScandorTowns;

    private static readonly int[] PlaneTowns = [LightTown, DarkTown, DeathTown];

    /// <summary>
    /// D3-035 QoL <c>p3.transport-planes</c> = <c>visited</c>: the plane towns 15–17 a member of the party (the elemental
    /// not counted) has entered (<see cref="Phantasie3CharacterData.PlaneTownsVisited"/>). Off (every preset): none, DOS's 1–14.
    /// </summary>
    public IReadOnlyList<int> ExtraTransportTowns(GameSession session) =>
        session.Options.P3TransportToVisitedPlanes()
            ? PlaneTowns.Where(t => session.Party.Adventurers.Any(m => m.Phantasie3?.HasVisitedPlaneTown(t) == true)).ToArray()
            : [];

    /// <summary>D3-035 QoL: every member in the party now (the elemental not counted) has entered town <paramref name="townId"/>.</summary>
    public static void MarkPlaneTownVisited(Party party, int townId)
    {
        if (PlaneOfTown(townId) == Scandor)
            return;
        foreach (var member in party.Adventurers)
            member.P3.MarkPlaneTownVisited(townId);
    }

    public bool IsUnnamedTransportTown(int townId) => false;

    private static readonly IReadOnlyDictionary<int, int> NoAliases = new Dictionary<int, int>().AsReadOnly();

    public IReadOnlyDictionary<int, int> OverworldTownAliases => NoAliases;

    /// <summary>"Ocean" on Scandor, "River Styx" on the other planes (ovl3 <c>0x92c</c>, <c>planes.md</c> §5).</summary>
    public string BlockedMessage(int regionId) =>
        regionId == Scandor ? GameText.S("core.p3.ocean") : GameText.S("core.p3.river_styx");

    private static readonly IReadOnlyList<int> StubMonsters = [0, 1, 2, 3, 4, 5, 6];

    public IReadOnlyList<int> FallbackMonsterPool => StubMonsters;

    public bool MayDropItem(int itemId, int? dungeonLevelId) => true;

    public bool WithholdsDungeonTreasure(int firstMonsterId) => false;

    public bool IsBlackKnightSpot(int dungeonLevelId) => false;

    public void OnSpecialSite(GameSession session, OverworldMoveResult result)
    {
    }

    /// <summary>The plane a town lies on (root <c>0x1e18</c>): 15 Light, 16 Dark, 17 Netherworld, else Scandor.</summary>
    public static int PlaneOfTown(int townId) => townId switch
    {
        LightTown => LightPlane,
        DarkTown => DarkPlane,
        DeathTown => Netherworld,
        _ => Scandor,
    };

    // --- Rules options (D3-020, D3-029) ----------------------------------------------------------------------------------

    public void ApplyRules(GameSession session, GameOptions options)
    {
        session.Sack.Capacity = options.P3FoundItemLimit() ?? int.MaxValue;
        if (options.P3RosterCharacterLimit() is { } limit)
        {
            session.Roster.MaxCharacters = limit;
            session.Roster.Unlimited = false;
        }
        else
            session.Roster.Unlimited = true;
    }

    // --- Creation (ovl2 0x227d, FORMAT.md §5.1) -------------------------------------------------------------------------

    /// <summary>
    /// "Random" in III's race menu: <c>rnd(10) + 5</c> = III races 6–15 (Gnoll, Goblin, Kobold, Lizard Man, Minotaur, Ogre,
    /// Orc, Pixie, Sprite, Troll), with no re-roll for a race already in the roster (I re-rolls).
    /// </summary>
    private static readonly Race[] RandomRaces =
    [
        Race.Gnoll, Race.Goblin, Race.Kobold, Race.LizardMan, Race.Minotaur, Race.Ogre, Race.Orc, Race.Pixie, Race.Sprite,
        Race.Troll,
    ];

    /// <summary>I's start age with no lifespan (<see cref="CharacterCreation.StartingAgeClock"/> of 0): 260 units, 10 years.</summary>
    private const int NoLifespanStartAge = 260;

    /// <summary>
    /// Guild New in III (ovl2 <c>0x227d</c>, <c>FORMAT.md</c> §5.1), every draw in DOS's order: race (Random only), HP dice,
    /// social class (its gold in the bank), STR, INT, DEX, CON, CHA by 3d6 (<see cref="RollAttribute"/>), LCK
    /// <c>rnd(6) + 7</c>, the age roll. Under the manual's HP (D3-022, CON counts) the HP dice follow the attributes,
    /// since they need CON. Then level 1, magic at 75 % of the ceiling, rank Front, origin by the Guild's town, the class
    /// kit worn, the first spell, the nine skills from III's tables (D3-032), all body areas Okay, saved option Thrust.
    /// <para>
    /// The age clock starts at DOS's <c>⌊(rnd(7) + 24) · f + 260⌋</c>, f = LCK times the race factor
    /// (<see cref="P3WorldData.CreationAge"/>, decoded s98). DOS's maximum age (<c>⌊f⌋</c>) is not kept: nothing in DOS reads
    /// it. D3-041 (user s99, manual): the lifespan is I's band for the race picked by LCK (8–13, as I's seed), the age clock
    /// then restarts at I's <see cref="CharacterCreation.StartingAgeClock"/> (one more draw), and
    /// <see cref="Aging.ApplyTownEntry"/> ages it as I; DOS's age and no lifespan under the DOS option.
    /// </para>
    /// </summary>
    public Character? RollNewMember(GameSession session, Race race, CharacterClass cls, bool randomRace,
        IReadOnlyList<int>? abilities)
    {
        var w = World;
        var rng = session.CampaignAccess.Rng;
        var rules = session.Options;
        var tables = w.TrainingFor(rules);
        var creation = session.CreationTables;
        if (randomRace)
            race = RandomRaces[P3Dice.Rnd(rng, RandomRaces.Length) - 1];

        var c = new Character
        {
            Name = "",
            Race = race,
            BirthRace = race,
            Class = cls,
            Status = CharacterStatus.Ok,
            Level = 1,
            Experience = 0,
            Score = 0,
        };
        var hpFirst = rules.P3HpIgnoresCon();
        if (hpFirst)
            c.HpMax = P3Training.CreationHp(cls, 0, tables, rng, rules);

        var p3 = c.P3;
        var die = w.SocialClassBaseRaces.Contains(race) ? w.SocialClassRollDie.BaseRaces : w.SocialClassRollDie.Others;
        var r = P3Dice.Rnd(rng, die);
        p3.SocialClass = w.SocialClassThresholds.Count(t => r >= t);
        c.AddBankGold(P3Training.StartingGold(p3.SocialClass), session.Party.BankGoldCap);

        var a = new int[5];
        for (var i = 0; i < a.Length; i++)
        {
            var (cm, rm) = (creation.ClassAttributeModifiers[(int)cls][i], creation.RaceAttributeModifiers[(int)race][i]);
            a[i] = abilities is null ? RollAttribute(cm, rm, rng) : ClampAttribute(abilities[i], cm, rm); // T5 lineage
        }
        c.Stats = new CharacterStats(a[0], a[1], a[2], a[3], a[4], P3Dice.Rnd(rng, 6) + 7);
        c.SkillBonuses = CharacterCreation.SkillBonusSeeds(c.Stats);

        if (!hpFirst)
            c.HpMax = P3Training.CreationHp(cls, c.Stats.Con, tables, rng, rules);
        c.HpMax = c.HpCur = Math.Max(1, c.HpMax);
        var ageRoll = P3Dice.Rnd(rng, 7); // ovl2 0x2721
        c.AgeClock = w.CreationAge?.AgeClock(race, c.Stats.Luck, ageRoll) ?? NoLifespanStartAge;
        if (!rules.P3NoLifespans() && creation.LifespanBandsByRace.Count > 0)
        {
            // D3-041: I's lifespan and I's start age with it (D-003); DOS III's age factor makes a Halfling start at 29–49,
            // too old for the manual's 40–50 years.
            c.Lifespan = CharacterCreation.LifespanFor(race, c.Stats.Luck, creation);
            c.AgeClock = CharacterCreation.StartingAgeClock(c.Lifespan, rng);
        }

        c.MpCeiling = creation.MpCeilingByClass[(int)cls][0];
        c.MagicMax = c.MagicCur = (3 * c.MpCeiling) >> 2;
        p3.Rank = P3PartyRank.Front;
        p3.Origin = session.GuildTownId switch
        {
            LightTown => Phantasie3Origin.Light,
            DarkTown => Phantasie3Origin.Darkness,
            DeathTown => Phantasie3Origin.Netherworld,
            _ => Phantasie3Origin.Scandor,
        };

        c.SpellsKnown = new int[w.SpellCount];
        if (w.FirstSpell.TryGetValue(cls, out var first) && first > 0)
            c.SpellsKnown[first - 1] = 1;

        GiveKit(session, c);

        SetSkills(c, tables);
        c.BodyCondition = new P3BodyAreas().ToCondition();
        MarkLearnableSpells(session, c);
        return c;
    }

    /// <summary>
    /// One attribute (root <c>0xa719</c>): <c>3d6 + class_mod + race_mod − 17</c>, at most the race maximum and at least 3.
    /// The maximum is <c>race_mod + 8</c>, the manual's table (DOS's own table differs only in Kobold CHA, D3-034).
    /// </summary>
    public static int RollAttribute(int classModifier, int raceModifier, IRandomSource rng) =>
        ClampAttribute(P3Dice.Rnd(rng, 6) + P3Dice.Rnd(rng, 6) + P3Dice.Rnd(rng, 6) + classModifier + raceModifier - 17,
            classModifier, raceModifier, dice: false);

    /// <summary>
    /// <see cref="RollAttribute"/>'s range applied to a value: at most the race maximum (<c>race_mod + 8</c>), at least 3.
    /// T5 lineage also keeps an inherited value inside what 3d6 can give (<c>3 … 18 + class_mod + race_mod − 17</c>).
    /// </summary>
    public static int ClampAttribute(int value, int classModifier, int raceModifier, bool dice = true)
    {
        if (dice)
            value = Math.Clamp(value, 3 + classModifier + raceModifier - 17, 18 + classModifier + raceModifier - 17);
        return Math.Max(3, Math.Min(value, raceModifier + 8));
    }

    /// <summary>
    /// Training's skill picks (ovl2 <c>0x16d0</c>, called three times): each pick sets that skill to
    /// <see cref="P3Training.SkillAfter"/> of its current value, so a skill picked again moves half as far again. A null
    /// pick (Esc) changes nothing but is used up <c>[~]</c>.
    /// </summary>
    private static void TrainSkills(Character c, int level, P3TrainingTables tables, IEnumerable<P3Skill?> picks)
    {
        foreach (var k in picks)
            if (k is { } skill)
                SetSkill(c, skill, P3Training.SkillAfter(skill, SkillOf(c, skill), level, c.Stats, c.Class, c.Race, tables));
    }

    private static void SetSkill(Character c, P3Skill skill, int value)
    {
        var s = c.Skills;
        c.Skills = skill switch
        {
            P3Skill.Attack => s with { Attack = value },
            P3Skill.Parry => s with { Parry = value },
            P3Skill.FindItem => s with { FindItem = value },
            P3Skill.FindTrap => s with { SpotTrap = value },
            P3Skill.Disarm => s with { DisarmTrap = value },
            P3Skill.Listen => s with { Listen = value },
            P3Skill.PickLock => s with { PickLock = value },
            P3Skill.Swim => s with { Swim = value },
            _ => s,
        };
        if (skill == P3Skill.FireBow)
            c.P3.FireBow = value;
    }

    /// <summary>
    /// The pick screen's rows (the nine skills in record order, <see cref="P3Skill"/>; the DOS screen's order is not read): each skill's value now and after one
    /// more pick at <paramref name="level"/>, once <paramref name="picksSoFar"/> are applied (on a copy).
    /// </summary>
    public IReadOnlyList<(P3Skill Skill, int Now, int After)> SkillPickRows(
        GameOptions rules, Character member, int level, IReadOnlyList<P3Skill?> picksSoFar)
    {
        var tables = World.TrainingFor(rules);
        var copy = new Character
        {
            Class = member.Class, Race = member.Race, Stats = member.Stats, Skills = member.Skills,
            Phantasie3 = new Phantasie3CharacterData { FireBow = SkillOf(member, P3Skill.FireBow) },
        };
        TrainSkills(copy, level, tables, picksSoFar);
        return P3Training.ScreenOrder // DOS's pick-screen order (ovl2 0x18b0)
            .Select(k => (k, SkillOf(copy, k),
                P3Training.SkillAfter(k, SkillOf(copy, k), level, copy.Stats, copy.Class, copy.Race, tables)))
            .ToArray();
    }

    /// <summary>The nine skills at creation (<see cref="P3Training.SkillAtCreation"/>).</summary>
    private static void SetSkills(Character c, P3TrainingTables tables)
    {
        foreach (var k in Enum.GetValues<P3Skill>())
            SetSkill(c, k, P3Training.SkillAtCreation(k, c.Stats, c.Class, c.Race, tables));
    }

    /// <summary>One skill's current value.</summary>
    public static int SkillOf(Character c, P3Skill skill) => skill switch
    {
        P3Skill.Attack => c.Skills.Attack,
        P3Skill.Parry => c.Skills.Parry,
        P3Skill.FindItem => c.Skills.FindItem,
        P3Skill.FindTrap => c.Skills.SpotTrap,
        P3Skill.Disarm => c.Skills.DisarmTrap,
        P3Skill.Listen => c.Skills.Listen,
        P3Skill.FireBow => c.Phantasie3?.FireBow ?? 0,
        P3Skill.PickLock => c.Skills.PickLock,
        P3Skill.Swim => c.Skills.Swim,
        _ => 0,
    };

    /// <summary>
    /// Spells of the member's class whose level (the creation tables' spell levels = <c>DS:0x1208</c>, the manual's table)
    /// is at most the member's level become learnable (2) [~]: the Learn Spells code (ovl2 <c>0x1a6c</c>) was not read.
    /// </summary>
    private static void MarkLearnableSpells(GameSession session, Character c)
    {
        var levels = session.CreationTables.SpellLevelsByClass[(int)c.Class];
        for (var s = 0; s < levels.Count && s < c.SpellsKnown.Length; s++)
            if (c.SpellsKnown[s] == 0 && levels[s] > 0 && levels[s] <= c.Level)
                c.SpellsKnown[s] = 2;
    }

    // --- Training (training.md) ------------------------------------------------------------------------------------------

    private static P3Status StatusOf(Character c) => c.Status switch
    {
        CharacterStatus.Dead => P3Status.Dead,
        CharacterStatus.Sleep => P3Status.Asleep,
        CharacterStatus.Out => P3Status.Out,
        CharacterStatus.Down => P3Status.Down,
        _ => P3Status.Okay,
    };

    private static P3Trainee Trainee(Character c) =>
        new(c.Class, c.Stats, c.Level, c.Experience, c.Score, c.Phantasie3?.SocialClass ?? 0, c.HpMax, StatusOf(c),
            c.Race == Race.Undead);

    /// <summary>
    /// III's own gate (ovl2 <c>0x39b5</c>, <see cref="P3TownEntryPayout.CanUse"/>): the item's <c>str_check</c> sum (STR;
    /// bows 41–60 STR + DEX; melee weapons 2·STR + DEX) ≥ its <c>min_str</c>, both from III's tables. An id with no catalog
    /// entry passes (nothing to gate).
    /// </summary>
    public bool CanUseItem(GameSession session, Character member, int itemId) =>
        session.Items.Get(itemId) is not { } item || P3TownEntryPayout.CanUse(item, member.Stats);

    /// <summary>III's point table (<c>DS:0x113c + id</c>, <c>items.json</c> <c>points</c>; equals the manual's tables).</summary>
    public int ItemPoints(GameSession session, int itemId) =>
        itemId > 0 ? session.Items.Get(itemId)?.Points ?? 0 : 0;

    /// <summary>ovl2 <c>0x37f1</c> (<see cref="P3TownEntryPayout.ReadyBestEquipment"/>): bow slot, ties to the later item.</summary>
    public void ReadyBestEquipment(GameSession session, Character member) =>
        P3TownEntryPayout.ReadyBestEquipment(member, session.Items, session.Equipment);

    /// <summary>
    /// The numbers III's combat reads (root <c>0xd730</c>): the worn weapon's, armour's, shield's and bow's table points (no
    /// damage A, which is I's town-exit formula) and the defence against monster melee (<see cref="P3Combat.Defence"/>,
    /// the save's armour sign, <c>p3.armour-defence</c>).
    /// </summary>
    public GearStats GearStatsOf(GameSession session, Character member)
    {
        int P(int id) => ItemPoints(session, id);
        var shield = P(member.WornShield);
        var armour = P(member.WornArmor);
        return new GearStats(shield, armour, P(member.WornWeapon),
            P3Combat.Defence(shield, armour, member.Skills.Parry, member.Stats.Dex, session.Options), P(member.WornBow));
    }

    /// <summary>
    /// The Armory wears nothing (ovl2 <c>0x2f2a</c>, s173 <c>[C]</c> static, not seen live): it shops on a copy of the record
    /// (<c>FAR:0x9de8</c>); on Exit it calls ready-best (<c>0x37f1</c>, at <c>0x3296</c>) on the record and then copies the
    /// shopping copy back over it, so the bought items arrive carried and the worn slots stay as they were.
    /// </summary>
    public void AfterArmoryBuy(GameSession session, Character shopper, int itemId)
    {
    }

    /// <summary>
    /// The hand-out screen's member columns (ovl2 <c>0x0de6</c>, header "SH AR BW WP", s174 <c>[C]</c> static): the table
    /// points of the worn shield (<c>+0x68</c>), armour (<c>+0x6a</c>), bow (<c>+0x86</c>) and weapon (<c>+0x6c</c>). That is
    /// III's slot order in <c>rules.json</c>, so the columns are the layout's slots.
    /// </summary>
    public IReadOnlyList<string>? HandOutWornColumns(GameSession session) =>
        session.Equipment.Slots.Select(s => s.Id).ToArray();

    /// <summary>D3-073 (user s174: the manual over DOS's "N/A"): III's Armory list shows points or UNUSABLE.</summary>
    public bool ArmoryListsPoints => true;

    public (bool May, string Reason)? CanTrain(GameSession session, Character member) =>
        P3Training.MayTrain(Trainee(member)) ? (true, "") : (false, GameText.S("core.p3.cannot_train"));

    public long? TrainingXpShortfall(GameSession session, Character member) =>
        P3Training.Shortfall(Trainee(member), World.TrainingFor(session.Options));

    public long? TrainingGoldCost(GameSession session, Character member) =>
        P3Training.Cost(member.Level, member.Stats.Cha, session.CurrentTown?.PriceIndex ?? 100,
            World.TrainingFor(session.Options));

    /// <summary>
    /// Train Member (ovl2 <c>0x1031</c>): refused while the member may not train or lacks XP; the party pays the price;
    /// level + 1, HP re-rolled, level gold into the bank (D3-023), three skills trained, new spells learnable. The three
    /// skill picks are the player's (`p3train.skills_to_train_in_x`, <see cref="SkillPickRows"/>, applied in order); a call with
    /// none (scripts, tests) trains the three that gain the most (ties in record order) [I].
    /// </summary>
    public TownTransactionResult? Train(GameSession session, Character member) => Train(session, member, null);

    public TownTransactionResult Train(GameSession session, Character member, IReadOnlyList<P3Skill?>? picks)
    {
        var tables = World.TrainingFor(session.Options);
        var t = Trainee(member);
        if (!P3Training.MayTrain(t))
            return Failed(GameText.S("core.p3.cannot_train"));
        var shortfall = P3Training.Shortfall(t, tables);
        if (shortfall > 0)
            return Failed(GameText.F("core.p3.need_x_more_xp", member.Name, shortfall));
        var cost = TrainingGoldCost(session, member)!.Value;
        if (!session.Party.Spend(cost, TreasuryEntryKind.Training))
            return Failed(GameText.S("core.p3.cannot_afford"));
        var up = P3Training.LevelUp(t, tables, session.CampaignAccess.Rng, session.Options);
        member.Level = up.Level;
        member.HpMax = member.HpCur = up.HpMax;
        if (up.LevelGold > 0)
            member.AddBankGold(up.LevelGold, session.Party.BankGoldCap);
        TrainSkills(member, up.Level, tables, picks ?? BestPicks(member, up.Level, tables).Cast<P3Skill?>().ToArray());
        MarkLearnableSpells(session, member);
        return new TownTransactionResult
        {
            Kind = TownTransactionKind.Ok,
            Message = GameText.F("core.p3.x_trains_to_level_x", member.Name, member.Level),
            GoldDelta = -cost,
        };
    }

    /// <summary>The three skills (of the eight with class tables and Fire Bow) whose training gains the most.</summary>
    public static IReadOnlyList<P3Skill> BestPicks(Character member, int level, P3TrainingTables tables) =>
        Enum.GetValues<P3Skill>()
            .Select(k => (k, Gain: P3Training.SkillAfter(k, SkillOf(member, k), level, member.Stats, member.Class,
                member.Race, tables) - SkillOf(member, k)))
            .OrderByDescending(x => x.Gain).ThenBy(x => (int)x.k)
            .Take(3).Select(x => x.k).ToArray();

    private static TownTransactionResult Failed(string message) =>
        new() { Kind = TownTransactionKind.Failed, Message = message };

    // --- Inns (body-areas.md §4, D3-015) ---------------------------------------------------------------------------------

    /// <summary>
    /// A town inn stay (ovl2 <c>0x347d</c> with the in-town flag, s136): free (manual: "Inns located in towns are free"),
    /// <see cref="P3Inn.Stay"/> for each member, then the body areas (manual: 1 Break or 2 Injuries; DOS option: every
    /// area not Gone).
    /// </summary>
    public TownTransactionResult? RestAtInn(GameSession session)
    {
        var lines = P3Inn.Stay(session);
        HealBodiesAtInn(session, inTown: true);
        return new TownTransactionResult { Kind = TownTransactionKind.Ok, Message = string.Join("; ", lines) };
    }

    private static void HealBodiesAtInn(GameSession session, bool inTown)
    {
        foreach (var m in session.Party.Members.Where(m => m.Status != CharacterStatus.Dead))
        {
            var body = P3BodyAreas.Of(m);
            if (body.AllOkay)
                continue;
            body.InnStay(inTown, session.Options);
            m.BodyCondition = body.ToCondition();
            ApplyBodyStatus(m, body);
        }
    }

    /// <summary>Status from the body after a change (root <c>0x107b</c>, healing skips the HP test).</summary>
    internal static void ApplyBodyStatus(Character m, P3BodyAreas body)
    {
        if (m.Status == CharacterStatus.Dead)
            return;
        m.Status = body.BodyStatus(m.Status == CharacterStatus.Sleep) switch
        {
            P3Status.Dead => CharacterStatus.Dead,
            P3Status.Out => CharacterStatus.Out,
            P3Status.Down => CharacterStatus.Down,
            P3Status.Asleep => CharacterStatus.Sleep,
            _ => CharacterStatus.Ok,
        };
    }

    // --- The Mystic and the score (town.md §5, endgame.md §2) -----------------------------------------------------------

    /// <summary><c>compute_scores</c> (root <c>0x9884</c>): every member's score from scratch; returns the party's highest.</summary>
    public int ComputeScores(Party party)
    {
        var weights = World.ScoreWeights;
        var best = 0;
        foreach (var m in party.Members)
        {
            var p3 = m.P3;
            var s = 0;
            for (var bit = 0; bit < 8 && bit < weights.Count; bit++)
                if ((p3.QuestBits & (1 << bit)) != 0)
                    s += weights[bit];
            var score = m.Level / 6 + s;
            if (p3.SocialClass == Phantasie3CharacterData.Darklord)
                score = Math.Clamp(score, DarklordScore, SuperheroScore - 1);
            if (p3.SocialClass == Phantasie3CharacterData.Superhero)
                score = Math.Max(score, SuperheroScore);
            m.Score = score;
            best = Math.Max(best, score);
        }
        return best;
    }

    private static readonly string[] RankIds =
        ["core.p3.mystic.rank_0", "core.p3.mystic.rank_1", "core.p3.mystic.rank_2", "core.p3.mystic.rank_3"];

    /// <summary>
    /// The Mystic (ovl2 <c>0x2ca2</c>): scores recomputed; &lt; 30 novice, &lt; 60 average, &lt; 90 experienced, else expert;
    /// then `core.p3.mystic.heroes_of_gelnor` (a Phantasie I score), "… of Ferronrah" (a II score) and the highest title.
    /// </summary>
    public TownTransactionResult ConsultMystic(GameSession session)
    {
        var party = session.Party;
        var s = ComputeScores(party);
        var rank = s < 30 ? 0 : s < 60 ? 1 : s < 90 ? 2 : 3;
        var parts = new List<string> { GameText.S(RankIds[rank]) };
        if (party.Members.Any(m => (m.Phantasie3?.Phantasie1Score ?? 0) != 0))
            parts.Add(GameText.S("core.p3.mystic.heroes_of_gelnor"));
        if (party.Members.Any(m => (m.Phantasie3?.Phantasie2Score ?? 0) != 0))
            parts.Add(GameText.S("core.p3.mystic.heroes_of_ferronrah"));
        var title = party.Members.Count == 0 ? 0 : party.Members.Max(m => m.Phantasie3?.SocialClass ?? 0);
        if (title == Phantasie3CharacterData.Darklord)
            parts.Add(GameText.S("core.p3.mystic.heroes_of_netherworld"));
        else if (title == Phantasie3CharacterData.Superhero)
            parts.Add(GameText.S("core.p3.mystic.heroes_of_scandor"));
        return new TownTransactionResult { Kind = TownTransactionKind.Ok, Message = string.Join(" ", parts) };
    }

    // --- Party defeat: the Astral Plane (planes.md §6) -------------------------------------------------------------------

    /// <summary>
    /// Root <c>0x1e71</c>: the elemental is dismissed; each member with <c>r = rand()</c>: level &gt; <c>(r &amp; 3) + 2</c> →
    /// resurrected (body healed, Okay, HP ≥ 1, CON − 1); else level &gt; <c>(r' &amp; 3)</c> → Undead at level 20 (the same
    /// healing and CON − 1); else destroyed (out of the roster). Survivors arrive in a random town 1–14 on Scandor, which
    /// empties the found items and the party XP pool; party gold is kept. Nobody left: Pendragon.
    /// </summary>
    public PartyDefeatResult ResolvePartyDefeat(GameSession session)
    {
        var party = session.Party;
        var rng = session.CampaignAccess.Rng;
        var log = new List<string>
        {
            GameText.S("core.p3.astral_1"), GameText.S("core.p3.astral_2"), GameText.S("core.p3.astral_3"),
        };
        party.DismissElemental();
        party.Elemental = null;
        var fates = new List<(Character, DefeatFate)>();
        foreach (var m in party.Members.ToList())
        {
            var r = rng.NextDosWord();
            DefeatFate fate;
            if (m.Level > (r & 3) + 2)
                fate = DefeatFate.Resurrected;
            else if (m.Level > (rng.NextDosWord() & 3))
                fate = DefeatFate.Undead;
            else
                fate = DefeatFate.Destroyed;
            fates.Add((m, fate));
            switch (fate)
            {
                case DefeatFate.Resurrected:
                    Revive(m);
                    log.Add(GameText.F("core.p3.x_resurrected", m.Name));
                    break;
                case DefeatFate.Undead:
                    Revive(m);
                    m.Race = Race.Undead;
                    m.Level = PartyDefeat.UndeadLevel;
                    log.Add(GameText.F("core.p3.x_undead", m.Name));
                    break;
                default:
                    log.Add(GameText.F("core.p3.x_destroyed", m.Name));
                    party.Members.Remove(m);
                    for (var i = 0; i < session.Roster.Slots.Length; i++)
                        if (ReferenceEquals(session.Roster.Slots[i], m))
                            session.Roster.Slots[i] = null;
                    m.InParty = false;
                    break;
            }
        }
        foreach (var line in log)
            session.Note(line);
        var result = new PartyDefeatResult { Fates = fates, Log = log };
        // 0xa911 on arrival: found items and the XP pool are emptied; party gold is kept.
        session.Sack.ItemIds.Clear();
        session.Sack.Experience = 0;
        var town = party.Members.Count > 0 ? rng.NextDosWord() % ScandorTownCount + 1 : HomeTownId;
        MoveToTownSquare(session, town);
        session.EnterTown(town);
        return result;
    }

    private static void Revive(Character m)
    {
        m.BodyCondition = new P3BodyAreas().ToCondition();
        m.Status = CharacterStatus.Ok;
        m.HpCur = Math.Max(1, m.HpCur);
        m.Stats = m.Stats with { Con = Math.Max(0, m.Stats.Con - 1) };
    }

    public void OnTownEntered(GameSession session, int townId)
    {
        MoveToTownSquare(session, townId);
        MarkPlaneTownVisited(session.Party, townId); // D3-035 QoL, recorded whatever p3.transport-planes says
    }

    /// <summary>D3-071 (manual, user s142): the pool and found items are shared out on town entry.</summary>
    public bool PaysOutOnTownEntry => true;

    /// <summary>
    /// D3-071, DOS ovl2 <c>0x0000</c>(1) (s144 <c>[C]</c> static): each character's best equipment readied (ovl2
    /// <c>0x37f1</c>), then the last wand taken out of the found list (ovl2 <c>0x5a9</c>, <c>town_entry_payout</c>).
    /// </summary>
    public void BeforeTownEntryPayout(GameSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        foreach (var member in session.Party.Adventurers)
            P3TownEntryPayout.ReadyBestEquipment(member, session.Items, session.Equipment);
        if (World.TownEntryDroppedItem is { } wand)
            P3TownEntryPayout.DropFoundItem(session.Sack, wand);
    }

    /// <summary>D3-071: DOS's split, the remainder drawn point by point (<see cref="P3TownEntryPayout.ShareOut"/>).</summary>
    public void ShareOutTownEntryExperience(GameSession session, IReadOnlyList<Character> characters, IReadOnlyList<int> shares)
    {
        ArgumentNullException.ThrowIfNull(session);
        P3TownEntryPayout.ShareOut(characters, shares, session.Sack, session.Rng);
    }

    /// <summary>III's towns have no I-style restock list (TWNS <c>+0xb5</c>): I's churn never runs; see <see cref="OnTownArrival"/>.</summary>
    public bool ArmoryRestocks => false;

    /// <summary>D3-033: every ordinary town entry re-rolls weapons and potions by Apple II's rule, unless the save plays DOS's.</summary>
    public void OnTownArrival(GameSession session, TownState town)
    {
        if (session.Options.P3NoArmoryRestock() || World.ArmoryRestock is not { } rule)
            return;
        P3ArmoryRestock.Apply(town, rule, P3ArmoryRestock.AverageLevel(session.Party), session.CampaignAccess.Rng);
    }

    /// <summary>Town entry ages by one unit per 14 full days out (<see cref="P3TravelTime"/>).</summary>
    public int TownEntryAgeUnits(int travelTime) => P3TravelTime.AgeUnits(travelTime);

    /// <summary>ovl3 <c>0x65a</c>: only members not Dead age.</summary>
    public bool AgesTheDead => false;

    public int DaysOut(int travelTime) => P3TravelTime.DaysOut(travelTime);

    /// <summary>D3-027: Distribute's SELL pays <see cref="P3Town.SalePrice"/> (75 % of the town's price), ovl2 <c>0xa8f</c>.</summary>
    public Func<int, long>? DistributeSalePrice(TownState town, IItemCatalog items) =>
        id => P3Town.SalePrice(items.Get(id)?.Value ?? 0, town.PriceIndex);

    /// <summary>Put the party on town <paramref name="townId"/>'s square of its plane (so leaving the town starts there).</summary>
    public void MoveToTownSquare(GameSession session, int townId)
    {
        session.Overworld.RegionId = PlaneOfTown(townId);
        if (World.Town(townId) is { } place)
        {
            session.Overworld.RegionId = place.RegionId;
            session.Overworld.X = place.X;
            session.Overworld.Y = place.Y;
        }
    }
}

/// <summary>
/// III's death step (<c>damage_character</c> root <c>0xbbb6</c>): HP 0 → Dead ("dies!"), and nothing else: no CON loss at
/// death (Resurrection takes 1–2 CON instead, root <c>0x18b6</c>; manual agrees). Out / Down come only from the body.
/// </summary>
public sealed class Phantasie3DeathRule : IMemberDeathRule
{
    public static Phantasie3DeathRule Instance { get; } = new();

    private Phantasie3DeathRule()
    {
    }

    public void ApplyDeath(Character member) => member.Status = CharacterStatus.Dead;
}
