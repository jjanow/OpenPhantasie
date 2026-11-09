namespace OpenPhantasie.Core;

/// <summary>
/// The remaster's own character record, shared by every game in the series (I, II, III, IV). It is not a file layout:
/// each game's converter maps its own format onto it — for Phantasie I that is <c>GuildCodec</c> in OpenPhantasie.Data,
/// which owns the GUILD.DAT byte offsets and the 282-byte record (Phase 09 T1.1).
/// <para>
/// Fields a game never uses stay empty there: <see cref="Lineage"/> (Phantasie IV parentage; in I–III only a descendant
/// made with the T5 lineage option has it, <see cref="LineageRules"/>) and <see cref="BodyCondition"/> (Phantasie III body
/// areas) are null in Phantasie I. <see cref="Deeds"/> is shared by every
/// game (for IV's Hall of Glory) and stays null until the character takes part in one. Fields only Phantasie I uses (runes,
/// the magical-pool product, DOS round-trip words) live in <see cref="Phantasie1"/>.
/// </para>
/// </summary>
public class Character
{
    public string Name { get; set; } = string.Empty;
    /// <summary>
    /// Race the game shows and uses [C] (sheet PHANTASI.COM 0x2f03; creation
    /// duplicate-race guard OV1 0x0C5F). Becomes <see cref="Race.Undead"/> when the
    /// character turns undead; <see cref="BirthRace"/> keeps the living race.
    /// </summary>
    public Race Race { get; set; }

    /// <summary>
    /// Race chosen at creation. [C] equal to <see cref="Race"/> until the undead conversion (OV1 0x113), which changes
    /// only the displayed race. DOS writes it only at creation (OV1 0x0dca) and never reads it; the remaster uses it
    /// as an undead character's living race.
    /// </summary>
    public Race BirthRace { get; set; }
    public CharacterClass Class { get; set; }
    public CharacterStatus Status { get; set; } = CharacterStatus.Ok;
    public int Level { get; set; } = 1;

    public CharacterStats Stats { get; set; } = new(0, 0, 0, 0, 0, 0);
    public CharacterSkills Skills { get; set; } = new(0, 0, 0, 0, 0, 0, 0, 0);

    public int HpCur { get; set; }
    public int HpMax { get; set; }
    public int MagicCur { get; set; }
    public int MagicMax { get; set; }

    public int GoldInBank { get; set; }

    /// <summary>
    /// Adds to <see cref="GoldInBank"/>, clamped at <paramref name="bankGoldCap"/> (the game's
    /// <see cref="RulesProfile.BankGoldCap"/>; Phantasie I, manual p.10: "A bank account for a single character can hold
    /// a maximum of 65,000 GPs", D-048 — DOS clamps at 65,535, COM <c>0x2e5d</c>; the excess is lost in both).
    /// Returns the gold lost.
    /// </summary>
    public long AddBankGold(long amount, long bankGoldCap)
    {
        var total = GoldInBank + amount;
        var kept = Math.Min(total, Math.Max(bankGoldCap, GoldInBank)); // an imported balance above the cap is not cut
        GoldInBank = (int)kept;
        return total - kept;
    }
    public int Score { get; set; }

    /// <summary>
    /// Phantasie I-only fields (runes, magical-pool product, DOS round-trip words). Always present so Phantasie I code
    /// never null-checks; other games leave it at its empty defaults, except creation's <c>Word90 = 1</c>, which II's ST
    /// writes too (<c>0xca3a</c>, <see cref="CharacterCreation"/>).
    /// </summary>
    public Phantasie1CharacterData Phantasie1 { get; set; } = new();

    /// <summary>Phantasie II's added fields (quest SCORE, origin, minion flags, beast runes); null until the character plays II.</summary>
    public Phantasie2CharacterData? Phantasie2 { get; set; }

    /// <summary><see cref="Phantasie2"/>, created empty on first use.</summary>
    public Phantasie2CharacterData P2 => Phantasie2 ??= new();

    /// <summary>Phantasie III's added fields (social class, rank, quest / deed bits, Divine Aid …); null until the character plays III.</summary>
    public Phantasie3CharacterData? Phantasie3 { get; set; }

    /// <summary><see cref="Phantasie3"/>, created empty on first use.</summary>
    public Phantasie3CharacterData P3 => Phantasie3 ??= new();

    /// <summary>Phantasie IV-style parentage. Null = no lineage (always null in Phantasie I).</summary>
    public Lineage? Lineage { get; set; }

    /// <summary>Phantasie III-style body-area condition. Null = the game tracks no body areas (always null in Phantasie I).</summary>
    public BodyCondition? BodyCondition { get; set; }

    /// <summary>
    /// What the character took part in, oldest first (user s72: the source for IV's Hall of Glory biographies). Null =
    /// nothing recorded. Not part of any original file format.
    /// </summary>
    public IReadOnlyList<Deed>? Deeds { get; set; }

    /// <summary>Appends <paramref name="deed"/> to <see cref="Deeds"/>.</summary>
    public void RecordDeed(Deed deed)
    {
        ArgumentNullException.ThrowIfNull(deed);
        Deeds = [.. Deeds ?? [], deed];
    }

    /// <summary>
    /// Experience points. Sheet prints <c>%ld</c>; remaster stores 64-bit (the DOS converter clamps to its 32-bit word).
    /// </summary>
    public long Experience { get; set; }

    /// <summary>
    /// MP ceiling for this class and level [C] (s11): the manual's "Maximum
    /// Magic Points" table (<c>TWNS.INT @0x1c2</c>). Set at creation and on every level-up;
    /// the Inn raises <see cref="MagicMax"/> toward it. Was labelled "combat rating".
    /// </summary>
    public int MpCeiling { get; set; }

    /// <summary>
    /// Set when the character casts a spell, consumed by the Inn's MAGIC INCREASES roll [C] (s11);
    /// the Inn clears it (OV1 <c>0x2a46</c>, OV2 <c>0x6793</c>). DOS sets it only on the combat cost path
    /// (OV2 <c>0x0a55</c>); the remaster follows the manual and sets it on every cast (DIFFERENCES D-070).
    /// </summary>
    public bool CastSinceRest { get; set; }

    /// <summary>
    /// Per-skill bonus, i = 0..9 in record order (attack, parry, find, spot, disarm, listen, unused, pick, unused,
    /// swim) [C]. Seeded at creation
    /// as <c>2·linkedAttr + 30</c> (0 for the unused slots) and read by every level-up
    /// (OV1 <c>0x9ee</c>); not rewritten when stats later change.
    /// </summary>
    public int[] SkillBonuses { get; set; } = new int[CharacterSkills.RecordSlotCount];

    /// <summary>
    /// Phantasie II's TOSS skill (ST record <c>@42</c>, <c>toss-rock.md</c> §3): the hit chance of the Toss Rock order
    /// (<see cref="FeatureModuleRegistry.RockThrowing"/>). Grows at creation and level-up only while that module is on;
    /// 0 for a character that never trained under it (I's record has no such word: the I converter leaves it out).
    /// </summary>
    public int TossRock { get; set; }

    /// <summary>
    /// The TOSS skill bonus (ST <c>@62</c>), recomputed at every level-up from the current stats while the rock module is
    /// on (<see cref="TossRockRules.Bonus"/>). Kept apart from <see cref="SkillBonuses"/> so I's DOS record never carries it.
    /// </summary>
    public int TossRockBonus { get; set; }

    /// <summary>
    /// Defense rating [C] (s19): combat prep writes it into the record (OV2 <c>0x32f7</c>,
    /// <see cref="CombatMath.ComputeDefenseRating"/>); monster melee to-hit reads it (OV2 <c>0xe68</c>).
    /// Town aging also scales it ×4/5 and zeroes it with the six stats (OV1 <c>0x1954</c>).
    /// </summary>
    public int Word12 { get; set; }

    /// <summary>
    /// Lifespan in age-clock units (1 unit = 2 weeks, same unit as <see cref="AgeClock"/>).
    /// Remaster unit per DIFFERENCES.md D-003; DOS stores it in coarser steps of 256 age-clock units; DOS imports get
    /// the D-003 band instead (user s17). 0 = unknown.
    /// </summary>
    public int Lifespan { get; set; }

    /// <summary>Town the character was last in, 1-based [C] (written on town entry).</summary>
    public int CurrentTown { get; set; }

    /// <summary>Set once the one-time old-age decline has happened [C] (OV1 <c>0x1954</c>).</summary>
    public bool HasAged { get; set; }

    /// <summary>
    /// Weapon damage component A [C] (s19): combat prep sets effective damage = A + <see cref="WeaponDamageB"/>
    /// (OV2 <c>0x3284</c>). Written on leaving town [C] (OV1 <c>0x3249</c>,
    /// <see cref="TownExit"/>): <c>(3·level·|7 − 2·class| + 11·STR) / 66</c>; fresh roster characters hold 0.
    /// Not the scroll count — that is counted from the inventory (<see cref="ScrollCount"/>).
    /// </summary>
    public int WeaponDamageA { get; set; }

    public int WornShield { get; set; }
    public int WornArmor { get; set; }
    public int WornWeapon { get; set; }

    /// <summary>
    /// Phantasie III's bow slot (record <c>+0x86</c>, item ids 41–60; 0 = none) [C] (09 T4). Always 0 in I and II, whose
    /// rules have no bow slot.
    /// </summary>
    public int WornBow { get; set; }

    /// <summary>
    /// Phantasie IV's thrown-weapon slot (record <c>+0x68</c>, item ids 76–84; 0 = none) [C] (09 T6). Always 0 in I–III,
    /// whose rules have no thrown slot.
    /// </summary>
    public int WornThrown { get; set; }

    /// <summary>Weapon damage component B = worn weapon points (OV1 selector <c>0x2fdb</c>).</summary>
    public int WeaponDamageB { get; set; }

    /// <summary>Shield soak = shield id (PARRY subtracts; also <c>def += soak/3</c>).</summary>
    public int ShieldSoak { get; set; }

    /// <summary>Armor soak base (copied to the effective armor at fight start).</summary>
    public int ArmorSoak { get; set; }

    /// <summary>
    /// The carried item slots, worn items included [C] (OV1 selector <c>0x2fdb</c>, Mystic, equipment screen COM
    /// <c>0x2cf6</c>). Manual: 9 items per character.
    /// </summary>
    public List<int> InventoryItemIds { get; } = new();

    /// <summary>Carried-item capacity (manual: 9); enforced by Armory buy and Inn distribute (s30).</summary>
    public const int InventorySlots = 9;

    /// <summary>
    /// Spell knowledge flags, one per spell index.
    /// 0 = unknown, 1 = known, 2 = available to learn (FUN_09EE). [C]
    /// </summary>
    public int[] SpellsKnown { get; set; } = new int[CharacterCreation.SpellCount];

    /// <summary>
    /// Equipment-screen counters [C] (COM <c>0x2cf6</c>): counted from the carried items, not stored.
    /// Ids 101–110 HEALING potions, 111–120 MAGIC potions, 121–140 SCROLLs, 141–150 RINGs (shown as
    /// letters), 151+ treasures.
    /// </summary>
    public int HealingPotions => InventoryItemIds.Count(id => id is >= 101 and <= 110);
    public int MagicPotions => InventoryItemIds.Count(id => id is >= 111 and <= 120);
    public int ScrollCount => InventoryItemIds.Count(id => id is >= 121 and <= 140);
    public int Treasures => InventoryItemIds.Count(id => id >= 151);

    /// <summary>
    /// Roster ordinal / occupied marker. [C] Set at creation to the 1-based creation order (fixtures: 1..6); nonzero =
    /// occupied. The creation duplicate-strong-race guard (OV1 0x0C59) uses 0 as the empty-slot test.
    /// </summary>
    public int RosterOrdinal { get; set; }

    /// <summary>
    /// "In the active party" flag. [C] Guild→Add member (OV1 0x2786) sets it on the roster record; Guild→Drop member
    /// (OV1 0x225c) clears it before copying the record back. Not a status (see <see cref="CharacterStatus"/>).
    /// </summary>
    public bool InParty { get; set; }

    /// <summary>
    /// Age clock, 1 unit = 1/26 year (2 weeks) [C] (s11). Displayed AGE = <c>AgeClock / 26</c> (PHANTASI.COM
    /// <c>0x2f35</c>). Core uses <c>int</c>; DOS stores a u16.
    /// </summary>
    public int AgeClock { get; set; }

    /// <summary>Displayed AGE in years [C] (sheet: age clock / 26).</summary>
    public int AgeYears => AgeClock / 26;

    /// <summary>Deep copy for save snapshots (Phase 7).</summary>
    public Character Clone()
    {
        var copy = new Character
        {
            Name = Name,
            Race = Race,
            BirthRace = BirthRace,
            Class = Class,
            Status = Status,
            Level = Level,
            Stats = Stats,
            Skills = Skills,
            HpCur = HpCur,
            HpMax = HpMax,
            MagicCur = MagicCur,
            MagicMax = MagicMax,
            GoldInBank = GoldInBank,
            Score = Score,
            Phantasie1 = Phantasie1.Clone(),
            Phantasie2 = Phantasie2?.Clone(),
            Phantasie3 = Phantasie3?.Clone(),
            Lineage = Lineage,
            BodyCondition = BodyCondition,
            Deeds = Deeds,
            Experience = Experience,
            MpCeiling = MpCeiling,
            CastSinceRest = CastSinceRest,
            SkillBonuses = (int[])SkillBonuses.Clone(),
            TossRock = TossRock,
            TossRockBonus = TossRockBonus,
            Word12 = Word12,
            Lifespan = Lifespan,
            CurrentTown = CurrentTown,
            HasAged = HasAged,
            WeaponDamageA = WeaponDamageA,
            WornShield = WornShield,
            WornArmor = WornArmor,
            WornWeapon = WornWeapon,
            WornBow = WornBow,
            WornThrown = WornThrown,
            WeaponDamageB = WeaponDamageB,
            ShieldSoak = ShieldSoak,
            ArmorSoak = ArmorSoak,
            SpellsKnown = (int[])SpellsKnown.Clone(),
            RosterOrdinal = RosterOrdinal,
            InParty = InParty,
            AgeClock = AgeClock,
        };
        copy.InventoryItemIds.AddRange(InventoryItemIds);
        return copy;
    }
}

/// <summary>
/// Active adventuring party (subset of Guild roster).
/// Gold carried is the shared purse used for shopping/training, capped at <see cref="GoldCap"/> (D-048).
/// </summary>
public class Party
{
    public const int MaxMembers = 6;

    /// <summary>A party under Phantasie I's rules (<see cref="RulesProfile.PhantasieI"/>).</summary>
    public Party() : this(RulesProfile.PhantasieI) { }

    /// <summary>A party under <paramref name="rules"/> (its gold caps).</summary>
    public Party(RulesProfile rules)
    {
        Rules = rules ?? throw new ArgumentNullException(nameof(rules));
    }

    /// <summary>The game rules this party plays by (gold caps).</summary>
    public RulesProfile Rules { get; }

    /// <summary>
    /// What happens to a member whose HP reaches 0 (Phase 09 T1.4: the campaign's <see cref="ICampaign.Death"/>, set by
    /// <see cref="GameSession"/>). Defaults to Phantasie I's (DEAD and the D-009 CON loss).
    /// </summary>
    public IMemberDeathRule Death
    {
        get => _death;
        init => _death = value ?? throw new ArgumentNullException(nameof(value));
    }

    private readonly IMemberDeathRule _death = PhantasieIDeathRule.Instance;

    /// <summary>
    /// The feature modules whose damage hooks see hits outside combat (traps, dungeon scripts, swimming; Phase 09 T1.6).
    /// <see cref="GameSession"/> keeps it equal to its <see cref="GameSession.FeatureModules"/>. Defaults to
    /// <see cref="FeatureModuleSet.PhantasieI"/>.
    /// </summary>
    public FeatureModuleSet Modules
    {
        get => _modules;
        set => _modules = value ?? throw new ArgumentNullException(nameof(value));
    }

    private FeatureModuleSet _modules = FeatureModuleSet.PhantasieI;

    public List<Character> Members { get; } = new();

    /// <summary>Shared party gold purse (withdrawn from bank before shopping).</summary>
    public long GoldCarried { get; set; }

    /// <summary>
    /// DOS's non-DEAD member list <c>[0x64a2]</c> and word <c>[0x6490]</c>, kept for the session (D-033 <c>dos</c> reads
    /// them; <see cref="DosLivingList"/>).
    /// </summary>
    public DosLivingList DosLiving { get; } = new();

    /// <summary>
    /// Most gold the purse holds (<see cref="RulesProfile.PartyGoldCap"/>). Phantasie I, manual p.10: "A party can carry
    /// up to 65,000 GPs" (D-048, user s40). DOS clamps the purse at 65,535 (COM <c>0x2e5d</c>) and the excess is lost;
    /// the manual doesn't say what happens to it, so Core loses it too. The save's <see cref="GoldCapRule"/> can replace
    /// the game's cap with DOS's 65,535 or lift it (<c>p1.gold-cap</c>).
    /// </summary>
    public long GoldCap => GoldCapRule.Apply(Rules.PartyGoldCap);

    /// <summary>Most gold one member's bank account holds (<see cref="RulesProfile.BankGoldCap"/>, D-048; <see cref="GoldCapRule"/>).</summary>
    public long BankGoldCap => GoldCapRule.Apply(Rules.BankGoldCap);

    /// <summary>
    /// The save's gold cap rule (<see cref="GameOptions.GoldCap"/>, D-048), set by <see cref="GameSession"/> from its
    /// options. <see cref="GoldCapRule.Manual"/> = the game's own caps.
    /// </summary>
    public GoldCapRule GoldCapRule { get; set; }

    /// <summary>Adds to <see cref="GoldCarried"/>, clamped at <see cref="GoldCap"/> (D-048). Returns the gold lost.</summary>
    public long AddGold(long amount)
    {
        var total = GoldCarried + amount;
        var kept = Math.Min(total, Math.Max(GoldCap, GoldCarried));
        GoldCarried = kept;
        return total - kept;
    }

    public IEnumerable<Character> LivingMembers =>
        Members.Where(m => m.Status == CharacterStatus.Ok);

    /// <summary>
    /// Party slot 7 (DOS record <c>0x624c</c>; <c>[0x62a8]</c> = its <c>@92</c>): the elemental loaded at the Guild for
    /// `core.spell.summon_elemental` (<c>combat-resolution.md</c> §`core.spell.summon_elemental`). Not in <see cref="Members"/> except between a
    /// successful summon and the end of that fight. Cleared by Guild Drop / Purge, town entry and party defeat.
    /// </summary>
    public Character? Elemental { get; set; }

    /// <summary>The loaded elemental is fighting in the current combat (DOS party size <c>[0x648e]</c> = 7).</summary>
    public bool ElementalSummoned => Elemental is not null && Members.Contains(Elemental);

    /// <summary>The members without a summoned elemental — DOS end of combat counts these only (<c>0x3f2e</c>).</summary>
    public IEnumerable<Character> Adventurers => Members.Where(m => !ReferenceEquals(m, Elemental));

    /// <summary>End of combat (OV2 <c>0x3f2e</c>, party size 7 → 6): a summoned elemental goes back to slot 7.</summary>
    public void DismissElemental()
    {
        if (Elemental is not null)
            Members.Remove(Elemental);
    }
}

/// <summary>
/// Adventurer Guild roster — <see cref="RulesProfile.GuildSlotCount"/> record slots (Phantasie I: 22), at most
/// <see cref="MaxCharacters"/> (Phantasie I: the manual's 21, D-002) filled by new characters.
/// </summary>
public sealed class GuildRoster
{
    /// <summary>A roster under Phantasie I's rules (<see cref="RulesProfile.PhantasieI"/>).</summary>
    public GuildRoster() : this(RulesProfile.PhantasieI) { }

    public GuildRoster(RulesProfile rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        _baseSlotCount = rules.GuildSlotCount;
        Slots = new Character?[rules.GuildSlotCount];
        MaxCharacters = rules.GuildMaxCharacters;
    }

    private readonly int _baseSlotCount;

    /// <summary>
    /// The record slots: the game's <see cref="RulesProfile.GuildSlotCount"/>, or more under the QoL rules option
    /// <c>guild-limit</c> = <c>none</c> (<see cref="Unlimited"/>; the array is replaced, one slot at a time, when N)EW finds
    /// no hole). Index + 1 is the Guild number.
    /// </summary>
    public Character?[] Slots { get; private set; }

    /// <summary>
    /// QoL rules option <c>guild-limit</c> (PLAN P3, user s79 from D3-003): no Guild roster limit. N)EW always has room and
    /// grows <see cref="Slots"/> past the game's slot count when every slot is taken. Set by the session from the save's
    /// rules; off (the default) keeps <see cref="MaxCharacters"/>.
    /// </summary>
    public bool Unlimited { get; internal set; }

    /// <summary>
    /// Empty the roster with room for <paramref name="slotCount"/> records (never fewer than the game's own slot count): a
    /// loaded save brings its own roster length.
    /// </summary>
    internal void Reset(int slotCount) => Slots = new Character?[Math.Max(_baseSlotCount, slotCount)];

    /// <summary>The first empty slot, or (under <see cref="Unlimited"/>) a new one at the end; −1 when there is none.</summary>
    internal int FreeSlot()
    {
        var free = Array.IndexOf(Slots, null);
        if (free >= 0 || !Unlimited)
            return free;
        var grown = new Character?[Slots.Length + 1];
        Array.Copy(Slots, grown, Slots.Length);
        Slots = grown;
        return grown.Length - 1;
    }

    /// <summary>
    /// Most characters Guild N)EW fills (<see cref="RulesProfile.GuildMaxCharacters"/>; I: 21, D-002). The session sets it
    /// from the save's rules (<c>p1.guild-size</c> = <c>dos</c>: DOS's 22); 1..<see cref="Slots"/> length.
    /// </summary>
    public int MaxCharacters
    {
        get => _maxCharacters;
        internal set => _maxCharacters = value >= 1 && value <= Slots.Length
            ? value
            : throw new ArgumentOutOfRangeException(nameof(value), value, $"1..{Slots.Length}");
    }

    private int _maxCharacters;

    /// <summary>Guild N)EW may add a character (fewer than <see cref="MaxCharacters"/> occupied, or <see cref="Unlimited"/>).</summary>
    public bool HasRoom => Unlimited || Occupied.Count() < MaxCharacters;

    public IEnumerable<Character> Occupied =>
        Slots.Where(c => c is not null).Cast<Character>();
}

/// <summary>Rune index into <see cref="Phantasie1CharacterData.Runes"/> (script field id <c>43 + index</c>).</summary>
public enum Rune
{
    Air = 0,
    Earth = 1,
    Fire = 2,
    Water = 3,
    God = 4,
}
