namespace OpenPhantasie.Core;

/// <summary>
/// One Phantasie IV party member: the fields of the 190-byte record (<c>FORMAT.md</c> §6.2) that play reads. Only the
/// fields the built loops use are here; the rest is added with the loop that reads it.
/// </summary>
public sealed class P4Member
{
    /// <summary><c>+0A</c> condition: 0 Dead, 1 Okay, 2 Sleep, … (strings at <c>0x409ea</c>).</summary>
    public const int Dead = 0, Okay = 1, Asleep = 2;

    /// <summary><c>+0E</c>, up to 10 characters.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary><c>+19</c>.</summary>
    public int Race { get; set; }

    /// <summary><c>+18</c>.</summary>
    public int Class { get; set; }

    /// <summary><c>+08</c>.</summary>
    public int Level { get; set; } = 1;

    /// <summary><c>+0A</c>; any value but <see cref="Dead"/> counts as alive.</summary>
    public int Condition { get; set; } = Okay;

    public bool Alive => Condition != Dead;

    /// <summary><c>+39</c> / <c>+3A</c>.</summary>
    public int HpMax { get; set; }

    public int Hp { get; set; }

    /// <summary><c>+37</c> / <c>+38</c>.</summary>
    public int MpMax { get; set; }

    public int Mp { get; set; }

    /// <summary><c>+35</c>: combat defence D, also the trap-dodge roll (<c>combat.md</c> §1).</summary>
    public int Defence { get; set; }

    /// <summary><c>+3C + 2k</c>: the eleven skill values in <see cref="P4Skill"/> order.</summary>
    public int[] Skills { get; set; } = new int[P4Ids.Skills];

    /// <summary><c>+52–5A</c>: carried item ids (0 = empty).</summary>
    public int[] Items { get; set; } = new int[CarriedSlots];

    public const int CarriedSlots = 9;

    public int Skill(P4Skill skill) => Skills[(int)skill];

    /// <summary><c>+5B–63</c>: the charges of the carried items (rings 129 / 131 get 20).</summary>
    public int[] ItemCharges { get; set; } = new int[CarriedSlots];

    /// <summary><c>+2F–34</c>: Str, Int, Dex, Con, Cha, Luck (the order of <see cref="P4NewCharacter.Abilities"/>).</summary>
    public int[] Abilities { get; set; } = new int[6];

    public const int Luck = 5;

    /// <summary><c>+0C</c>: the six body parts' wound levels 0–3 in <see cref="P4BodyPart"/> order (<c>_pa</c>).</summary>
    public int[] Wounds { get; set; } = new int[P4Body.Parts];

    /// <summary><c>+73–B3</c>: spell state per spell number 1–65 (index n − 1): 1 known, ≥ 2 may be learned, 0 neither.</summary>
    public int[] Spells { get; set; } = new int[SpellCount];

    public const int SpellCount = 65;

    /// <summary>Whether spell number <paramref name="spell"/> (1–65) is known (state 1).</summary>
    public bool Knows(int spell) => spell is >= 1 and <= SpellCount && Spells[spell - 1] == 1;

    /// <summary><c>+04</c>: experience.</summary>
    public long Experience { get; set; }

    /// <summary><c>+26</c>: the bank account (the sheet's "Gold").</summary>
    public long Account { get; set; }

    /// <summary><c>+20</c>: the aging factor F (<c>ancestry.md</c> §7).</summary>
    public int AgingFactor { get; set; }

    /// <summary><c>+1E</c>: age in 1/26 years.</summary>
    public int AgeUnits { get; set; }

    /// <summary><c>+21</c>: "has grown older" already happened.</summary>
    public bool HasAged { get; set; }

    /// <summary><c>+1B</c>: the town the member is in (set on every town load, <c>_tload</c>).</summary>
    public int Town { get; set; }

    /// <summary><c>+1A</c>: social class (<c>_social</c>; the level-up gold, <c>training.md</c> §4).</summary>
    public int Social { get; set; }

    /// <summary><c>+09</c>: the "Score" — 0 until an ending writes ending score + average level (<c>ending.md</c> §2).</summary>
    public int Score { get; set; }

    /// <summary><c>+1C</c>: origin (<c>_origin</c>).</summary>
    public int Origin { get; set; }

    /// <summary><c>+36</c>: magic level; the inn raises max MP towards it (<c>training.md</c> §4.2).</summary>
    public int MagicLevel { get; set; }

    /// <summary><c>+72</c>: set by every spell cast in combat; the next inn stay may raise max MP (<c>training.md</c> §4.2).</summary>
    public bool MpGrowth { get; set; }

    /// <summary><c>+3B</c>: the melee damage bonus written on leaving a town (<c>towns.md</c> §2 Leaving).</summary>
    public int MeleeBonus { get; set; }

    /// <summary>The choices per strategy letter A–E (<c>_ds1</c> lists 2–6).</summary>
    public static readonly int[] StrategyChoices = [3, 2, 5, 7, 6];

    /// <summary><c>+B9</c>: the race a Duck Potion saved (the Anti-Duck Potion restores it).</summary>
    public int SavedRace { get; set; }

    /// <summary><c>+29</c>: the combat movement base (creation: the stride; Pixie Dust / Motion Potion raise it).</summary>
    public int MoveBase { get; set; }

    /// <summary><c>+01</c>: the guild slot this record belongs to (0–9; −1 = none, e.g. a preview party).</summary>
    public int GuildSlot { get; set; } = -1;

    /// <summary>
    /// <c>+2A</c>–<c>+2E</c>: the combat strategy (profile page 2, manual p. 31), 0-based: A auto defence (0 none, 1 light,
    /// 2 full), B missile (0 bow, 1 thrown), C melee (0 bow, 1 thrown, 2 thrust, 3 slash, 4 aimed blow), D movement (low
    /// nibble 0 group … 6 stay; bit 7 + bits 4–6: the member D = 2 covers, <c>_protwho</c>), E magic (0 always attack … 5
    /// always defend). <c>_mkdetail</c> sets 2, 0, 2, 3, 3 <c>[C]</c>.
    /// </summary>
    public int[] Strategy { get; set; } = [.. DefaultStrategy];

    public static readonly IReadOnlyList<int> DefaultStrategy = [2, 0, 2, 3, 3];

    /// <summary>
    /// The two ancestors (record <c>+22</c>…<c>+25</c>, with the "new" ancestors' snapshots <c>+B4/B5/BA</c>, <c>+BB/BC/BD</c>):
    /// none for a plain character.
    /// </summary>
    public P4AncestorRef[] Ancestors { get; set; } = [P4AncestorRef.None, P4AncestorRef.None];

    /// <summary>
    /// <c>_pc</c> as <c>_setpc</c> (<c>0x1D3C</c>) leaves it: 4 when dead or the head or torso is lost; 0 when asleep /
    /// paralysed (condition 2) or the head or torso is seriously wounded; 2 when a leg is seriously wounded or lost; else 3.
    /// (<c>_setpc</c> also kills a member at 0 HP; outside combat HP 0 already means Dead.)
    /// </summary>
    public int CombatState =>
        !Alive || Wounds[(int)P4BodyPart.Head] == P4Body.Lost || Wounds[(int)P4BodyPart.Torso] == P4Body.Lost ? 4
        : Condition == Asleep || Wounds[(int)P4BodyPart.Head] == P4Body.Serious
            || Wounds[(int)P4BodyPart.Torso] == P4Body.Serious ? 0
        : Wounds[(int)P4BodyPart.RightLeg] > P4Body.Light || Wounds[(int)P4BodyPart.LeftLeg] > P4Body.Light ? 2
        : 3;

    /// <summary>A member from a freshly created record (<see cref="P4CreationRecord"/>).</summary>
    public static P4Member From(P4NewCharacter c)
    {
        ArgumentNullException.ThrowIfNull(c);
        var m = new P4Member
        {
            Name = c.Name, Race = c.Race, Class = c.Class, Level = c.Level, HpMax = c.HpMax, Hp = c.HpMax,
            MpMax = c.MpMax, Mp = c.MpMax, Skills = [.. c.Skills], Abilities = [.. c.Abilities], Account = c.Account,
            AgingFactor = c.AgingFactor, AgeUnits = c.AgeUnits, Social = c.Social, Origin = c.Origin,
            MagicLevel = c.MagicLevel, MoveBase = c.Stride,
        };
        foreach (var s in c.KnownSpells)
            m.Spells[s - 1] = 1;
        foreach (var s in c.LearnableSpells)
            if (m.Spells[s - 1] == 0)
                m.Spells[s - 1] = 2;
        if (c.Item is int item)
        {
            m.Items[0] = item;
            if (item is P4Party.RingA or P4Party.RingB)
                m.ItemCharges[0] = P4Party.RingCharges;
        }
        return m;
    }

    public P4Member Clone() => new()
    {
        Name = Name, Race = Race, Class = Class, Level = Level, Condition = Condition, HpMax = HpMax, Hp = Hp,
        MpMax = MpMax, Mp = Mp, Defence = Defence, Skills = [.. Skills], Items = [.. Items], ItemCharges = [.. ItemCharges],
        Abilities = [.. Abilities], Wounds = [.. Wounds], Experience = Experience, Account = Account,
        AgingFactor = AgingFactor, AgeUnits = AgeUnits, HasAged = HasAged, Town = Town, Spells = [.. Spells],
        Social = Social, Score = Score, Origin = Origin, MagicLevel = MagicLevel, MpGrowth = MpGrowth,
        MeleeBonus = MeleeBonus, Strategy = [.. Strategy], GuildSlot = GuildSlot, SavedRace = SavedRace, MoveBase = MoveBase,
        Ancestors = [.. Ancestors],
    };
}

/// <summary>Kind of an ancestor reference (record <c>+22</c> / <c>+24</c>): 0 none ("Unknown"), 1 Hall person, 2 new person.</summary>
public enum P4AncestorKind { None = 0, Hall = 1, New = 2 }

/// <summary>
/// One ancestor of a character (<c>ancestry.md</c> §7 item 2): a Hall person by slot, or a "new" person by index (guild
/// slot × 2 + n) with the social class, race and class the record keeps for it.
/// </summary>
public sealed record P4AncestorRef(P4AncestorKind Kind, int Index = 0, int Social = 0, int Race = 0, int Class = 0)
{
    public static P4AncestorRef None { get; } = new(P4AncestorKind.None);
}

/// <summary>The party's ship (<c>_par</c>+06–09): none, moored on a square, or the party aboard with a navigator.</summary>
public enum P4ShipState { None = 0, Moored = 1, Aboard = 2 }

/// <summary>
/// The Phantasie IV party record <c>party</c> (<c>_par</c>, <c>FORMAT.md</c> §6.7) with its members (<c>_pmb</c>): party
/// gold, the shared 128-slot pack, the 64 story flags (byte 0 is the per-location reset counter, <c>events.md</c> §3) and
/// the per-location flags, 20 for each of 22 map numbers.
/// </summary>
public sealed class P4Party
{
    public const int PackSlots = 128, StoryFlagCount = 64, LocationFlagsPerMap = 20, LocationMaps = 22;

    /// <summary>The two rings whose new copies get 20 charges (<c>_setitm</c>): 129 and 131.</summary>
    public const int RingA = 0x81, RingB = 0x83, RingCharges = 20;

    public List<P4Member> Members { get; } = [];

    /// <summary>
    /// s152: the sound moments this party's game noted (<see cref="P4Sounds"/>), for the front end. Every loop and fight
    /// shares the party, so they all note here. Not part of a save.
    /// </summary>
    public P4SoundLog Sounds { get; } = new();

    /// <summary>
    /// <c>_par</c>+14: the elemental (0 Mud, 1 Mist, 2 Air) added at the guild for Summon Elemental (manual p. 26); null
    /// when none (the code's value above 2 makes the spell do nothing).
    /// </summary>
    public int? Elemental { get; set; }

    /// <summary><c>+1C</c>.</summary>
    public long Gold { get; set; }

    /// <summary><c>+0A</c>: the highest Score (+09) in the party, written on leaving a town (<c>_exittown</c>).</summary>
    public int MaxScore { get; set; }

    /// <summary>
    /// <c>_emlevel</c>: the last fight's encounter level (the groups' mean level), which outdoor Monster Evaluation reports
    /// (clamped 1–12). 0 until a fight sets it (the battle does not write it yet).
    /// </summary>
    public int EncounterLevel { get; set; }


    /// <summary>The guild roster (<c>_gmb</c>, 10 records; null = empty slot). Party members link to it by <see cref="P4Member.GuildSlot"/>.</summary>
    public P4Member?[] Roster { get; } = new P4Member?[RosterSlots];

    /// <summary>
    /// The Hall of Glory's descendant counts (<c>fames</c> +18) where this game changed them (each use as an ancestor adds
    /// one, up to 99); null = the count as shipped.
    /// </summary>
    public int?[] HallDescendants { get; } = new int?[HallSlots];

    /// <summary>
    /// The Hall persons this game took in from Phantasie III (<c>_mcopy</c>), by slot: 3–10 (<see cref="P4Hall"/>); 0–2 stay
    /// null (the shipped three).
    /// </summary>
    public P4HallPerson?[] HallImports { get; } = new P4HallPerson?[HallSlots];

    public const int HallSlots = 11;

    public const int RosterSlots = 10, ItemIds = 157;

    /// <summary><c>goods</c> (<c>_item</c>): per item id the count the party sold and the town it is shelved in.</summary>
    public int[] GoodsCount { get; } = new int[ItemIds];

    public int[] GoodsTown { get; } = new int[ItemIds];

    /// <summary><c>_itemp</c>: the shop's live stock per item id — never cleared, so it carries from town to town (<c>towns.md</c> §6.3).</summary>
    public int[] ShopStock { get; } = new int[ItemIds];

    /// <summary>Story flags 0x3E / 0x3F are the party strategy (<c>_par</c>+15E / +15F): A attitude, B flight (<c>_party_info</c>).</summary>
    public const int AttitudeFlag = 0x3E, FleeFlag = 0x3F;

    /// <summary><c>+0B</c>: the rounded average level, written on leaving a town and by the Mystic (<c>events.md</c> §3.1).</summary>
    public int AverageLevel { get; set; }

    /// <summary><c>+15</c>: Levitation cast outside combat; cleared at the end of a fight. No dungeon water damage while set.</summary>
    public bool Levitating { get; set; }

    /// <summary>D4-039 manual: steps of Levitation left (5 × the slowest stride when cast); null = until the next fight (code).</summary>
    public int? LevitationSteps { get; set; }

    /// <summary><c>+20</c> / <c>+A0</c>: the pack's item ids (0 = empty) and charges.</summary>
    public int[] Pack { get; } = new int[PackSlots];

    public int[] PackCharges { get; } = new int[PackSlots];

    /// <summary><c>+120</c>: story flags 0–63 (byte values; flag 0 is the reset counter).</summary>
    public byte[] StoryFlags { get; } = new byte[StoryFlagCount];

    /// <summary><c>+160</c>: per-location flags, map × 20 + n.</summary>
    public byte[] LocationFlags { get; } = new byte[LocationMaps * LocationFlagsPerMap];

    /// <summary><c>+16</c>: days out (u16, capped at 65,535): <c>_dayout</c> adds whole days; a town entry turns them into age.</summary>
    public int DaysOut { get; set; }

    /// <summary><c>__daytime</c>: the hours left over from <c>_dayout</c> (0–23; a global, not in <c>_par</c>, cleared on leaving a town).</summary>
    public int Daytime { get; set; }

    /// <summary><c>+18</c>: the party experience pool, stored ÷ 10 (shared out at a town gate).</summary>
    public long ExperiencePool { get; set; }

    /// <summary>
    /// D4-062 manual: the gold the party has gained since it last left a town (fights, gold events), for P Party info.
    /// Remaster-only (the code shows the whole purse); cleared by <see cref="P4TownRules.Leave"/>.
    /// </summary>
    public long GoldSinceTown { get; set; }

    /// <summary><c>+06</c> low nibble.</summary>
    public P4ShipState Ship { get; set; }

    /// <summary><c>+06</c> high nibble: the navigator's party slot while aboard.</summary>
    public int ShipNavigator { get; set; }

    /// <summary><c>+07–09</c>: the ship's outdoor map and square.</summary>
    public int ShipMap { get; set; }

    public int ShipX { get; set; }

    public int ShipY { get; set; }

    /// <summary>D4-038 manual: sea steps the current ship has sailed (its life is <c>p4.ship-life</c>).</summary>
    public int ShipSeaSteps { get; set; }

    /// <summary><c>_dayout(hours)</c>: hours + <c>__daytime</c>; whole days into <see cref="DaysOut"/> (cap 65,535), the rest kept.</summary>
    public void PassHours(int hours)
    {
        var total = hours + Daytime;
        Daytime = total % 24;
        DaysOut = Math.Min(0xFFFF, DaysOut + total / 24);
    }

    /// <summary>Clears the ship (<c>_par</c>+06–09 = 0).</summary>
    public void LoseShip()
    {
        Ship = P4ShipState.None;
        ShipNavigator = ShipMap = ShipX = ShipY = 0;
        ShipSeaSteps = 0;
    }

    public bool AnyAlive => Members.Any(m => m.Alive);

    public ref byte LocationFlag(int map, int n) => ref LocationFlags[map * LocationFlagsPerMap + n];

    /// <summary><c>_hasitm(item, 0)</c>: in the pack or any member's carried slots.</summary>
    public bool HasItem(int item) =>
        Pack.Contains(item) || Members.Any(m => m.Items.Contains(item));

    /// <summary><c>_remitm</c>: removes the first copy, pack first, then the members' slots in party order.</summary>
    public void RemoveItem(int item)
    {
        var i = Array.IndexOf(Pack, item);
        if (i >= 0)
        {
            Pack[i] = 0;
            return;
        }
        foreach (var m in Members)
        {
            var j = Array.IndexOf(m.Items, item);
            if (j >= 0)
            {
                m.Items[j] = 0;
                return;
            }
        }
    }

    /// <summary>
    /// <c>_setitm</c>: the first empty pack slot, or slot rand(127) (1–127) when the pack is full. Only the rings 129 / 131
    /// get their charges set (20); any other item keeps the slot's old charge byte.
    /// </summary>
    public void AddItem(int item, IRandomSource rng)
    {
        var i = Array.IndexOf(Pack, 0);
        if (i < 0)
            i = P4Dice.Rand(rng, PackSlots - 1);
        Pack[i] = item;
        if (item is RingA or RingB)
            PackCharges[i] = RingCharges;
    }
}
