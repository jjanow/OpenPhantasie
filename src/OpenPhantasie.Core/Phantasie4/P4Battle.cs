namespace OpenPhantasie.Core;

/// <summary>What a <see cref="P4Battle"/> is waiting for.</summary>
public enum P4BattlePrompt
{
    /// <summary>The encounter options (<c>_enc_opt</c>): <see cref="P4Battle.ChooseOption"/>.</summary>
    Options,

    /// <summary>
    /// One member's orders (manual mode, <c>_b_chmove</c>): <see cref="P4Battle.Step"/> moves him on the field, then
    /// <see cref="P4Battle.Order"/> sets his action.
    /// </summary>
    Order,

    /// <summary>A found item (<c>_findit</c>): <see cref="P4Battle.TakeItem"/>.</summary>
    Treasure,

    /// <summary>The fight is over (<see cref="P4Battle.End"/>); <see cref="P4Battle.Continue"/> closes the report.</summary>
    Over,
}

/// <summary>
/// The encounter options (<c>_enc_opt</c>): G, B, A, F, L, and Q auto combat / R auto round (the party strategy picks one of
/// the five, <see cref="P4Battle.PartyStrategy"/>, and the members act by their combat strategy).
/// </summary>
public enum P4EncounterOption { Greet, Beg, Demand, Fight, Flee, AutoCombat, AutoRound }

/// <summary>A member's action (<c>_epatk</c>, <c>combat.md</c> §2) plus Leave (step off an exit square).</summary>
public enum P4ActionKind { Parry, AimBlow, Lunge, Attack, Thrust, Slash, FireBow, Throw, Cast, Fly, Land, Leave }

/// <summary>
/// A member's order: the action, its target — a field square for weapons, Fire Flash and the code's Flameball, a party slot
/// for Healing and Resurrection — and the spell number for <see cref="P4ActionKind.Cast"/>.
/// </summary>
public sealed record P4Order(P4ActionKind Kind, int Target = -1, int Spell = 0);

/// <summary>How the fight ended: <c>_eexitc</c> (<c>combat.md</c> §11).</summary>
public enum P4BattleEnd
{
    None = 0,
    PartyDead = 1,
    Teleported = 2,
    Begged = 3,
    Fled = 4,
    AllKilled = 101,
    AllLeft = 102,
    Melted = 103,
    DrivenOff = 104,
    Surrendered = 105,
    Greeted = 106,
}

/// <summary>One monster group: its <c>DATM</c> record as the fight changes it (<c>_mns</c> is per type, so spells hit the group).</summary>
public sealed class P4MonsterGroup(P4MonsterType type, int count)
{
    public P4MonsterType Type { get; } = type;

    /// <summary><c>_emcnt</c>: how many the group started with.</summary>
    public int Count { get; } = count;

    /// <summary>+1A, raised by a monster Protection.</summary>
    public int Armour { get; set; } = type.Armour;

    /// <summary>+1C, lowered by the code's Weakness (D4-050 x68000).</summary>
    public int Defence { get; set; } = type.Defence;

    /// <summary>+2E, lowered by Confusion.</summary>
    public double SpellAbility { get; set; } = type.SpellAbility;

    /// <summary>D4-050 manual: Weakness scales the damage value +18.</summary>
    public double DamageFactor { get; set; } = 1.0;

    /// <summary>The group flies (+41, landed by Sink).</summary>
    public bool Flying { get; set; } = type.Flier;
}

/// <summary>One monster on the field (<c>_emhp</c>, <c>_st</c>, <c>_ml</c>, <c>_emmove</c>, <c>_emsplmv</c>, <c>_emstr</c>).</summary>
public sealed class P4BattleMonster
{
    public const int Asleep = 1, Acted = 2, Ready = 3;

    public required P4MonsterGroup Group { get; init; }

    /// <summary>A letter A, B, … for the screen.</summary>
    public required char Letter { get; init; }

    public int Square { get; set; } = -1;

    /// <summary>Record +3 (<see cref="P4BattleFacing"/>): monsters start facing the viewer.</summary>
    public int Facing { get; set; } = P4BattleFacing.Front;

    public int Hp { get; set; }

    public int State { get; set; } = Ready;

    public int[] Wounds { get; } = new int[P4Body.Parts];

    public int SpellMove { get; set; }

    public int Strength { get; set; }

    public bool Dead { get; set; }

    /// <summary>Left the field alive (<c>_b_chkedge</c>).</summary>
    public bool Gone { get; set; }

    /// <summary>A sleeper rolls to wake once a round.</summary>
    public bool WakeRolled { get; set; }

    /// <summary>D4-052 manual Fear: the monster runs for an exit.</summary>
    public bool Afraid { get; set; }

    /// <summary>D4-052 manual Dissolve: damage still to come, one entry per round.</summary>
    public Queue<int> Dissolving { get; } = new();

    public bool OnField => !Dead && !Gone && Square >= 0;

    public string Name => Group.Type.Name;
}

/// <summary>One party member's fight values (<c>_set1</c>, <c>combat.md</c> §1.2).</summary>
public sealed class P4BattleMember
{
    public required int Slot { get; init; }

    public required P4Member Member { get; init; }

    public int Square { get; set; } = -1;

    /// <summary>Record +3 (<see cref="P4BattleFacing"/>): members start facing up the field (<c>_enc_char</c>).</summary>
    public int Facing { get; set; } = P4BattleFacing.Back;

    public bool Gone { get; set; }

    /// <summary>+70 Hm, +71 Hr, +6E Wm, +6D Wr, +6F A, +35 D.</summary>
    public int Hm { get; set; }

    public int Hr { get; set; }

    public int Wm { get; set; }

    public int Wr { get; set; }

    public int Armour { get; set; }

    public int Defence { get; set; }

    /// <summary><c>_equip</c>: best usable item per slot and its value (shield, armour, weapon, bow, thrown).</summary>
    public int ShieldValue { get; set; }

    public int ArmourValue { get; set; }

    public int WeaponId { get; set; }

    public int WeaponValue { get; set; }

    public int BowId { get; set; }

    public int BowValue { get; set; }

    public int ThrownId { get; set; }

    public int ArmourId { get; set; }

    public int ShieldId { get; set; }

    /// <summary>+29: the member's stride (<see cref="P4Member.MoveBase"/>: race + class at creation, then potions).</summary>
    public int Stride { get; set; }

    /// <summary><c>_baserate</c>: the stride after the armour's movement % and the Shoes of Speed.</summary>
    public int BaseMove { get; set; }

    /// <summary>MV this round, and what is left of it.</summary>
    public int Move { get; set; }

    public int MoveLeft { get; set; }

    /// <summary><c>_esplmv</c>: Speed / Slow / Ninja modifier.</summary>
    public int SpellMove { get; set; }

    /// <summary><c>_epstr</c>: the Strength spell's bonus.</summary>
    public int StrengthBonus { get; set; }

    /// <summary><c>_epopt</c>: 1 parrying, −1 parry used, 0 none.</summary>
    public int Parry { get; set; }

    public bool Flier { get; set; }

    public bool Flying { get; set; }

    /// <summary>Party slot 6: a summoned elemental (every hit lands on its "body").</summary>
    public bool Elemental { get; init; }

    public P4Order Order { get; set; } = new(P4ActionKind.Parry);

    public bool Acted { get; set; }

    /// <summary>Diagonal steps this round cost 1, 2, 1, 2 … (<c>_dii</c>).</summary>
    public bool NextDiagonalCostsTwo { get; set; }

    /// <summary>
    /// <c>_atk_list</c> (per member, by square): −1 unknown, 1 attacked, 2 hit, −2 missed / nobody there (<c>_chrexe</c>).
    /// <c>_try_attack</c> clears it when the member stands on another square than at its last call (<see cref="MemorySquare"/>).
    /// </summary>
    public int[] AttackMemory { get; } = Enumerable.Repeat(-1, P4Battle.Squares).ToArray();

    /// <summary>Byte +4 of the member's <c>_tmark</c> record: the square of the last <c>_try_attack</c> (0 after <c>_enc_init</c>).</summary>
    public int MemorySquare { get; set; }

    public bool OnField => !Gone && Square >= 0 && Member.Alive;

    /// <summary><c>_pc</c> (<see cref="P4Member.CombatState"/>).</summary>
    public int State => Member.CombatState;
}

/// <summary>
/// A Phantasie IV fight on the 7 × 7 field (T6.3; <c>_encount</c>, spec
/// <c>reverse-engineering/x68000/phantasie4/logic-specs/combat.md</c>). Headless and resumable like the map loops: the
/// encounter options, each member's orders (moves on the field, then an action) and the found items are
/// <see cref="Prompt"/>s; the three phases of a round run by themselves and write <see cref="Log"/>. Every D4 rule the fight
/// touches goes through <see cref="P4Combat"/> / <see cref="P4Body"/> and their <c>p4.*</c> options.
/// <para>
/// Not built (see the spec's §13 and PLAN): spells 63 and 65 in combat (not on the code's combat pages). Auto mode
/// (<c>P4Battle.Auto.cs</c>) and the walk (§7.1) are built. A fight cannot be saved (the original has no save in combat).
/// </para>
/// </summary>
public sealed partial class P4Battle
{
    public const int Size = 7, Squares = Size * Size;

    private readonly GameOptions _o;
    private readonly P4Tables _t;
    private readonly P4BattleData _data;
    private readonly IRandomSource _rng;
    private readonly List<string> _log = [];
    private readonly bool[] _walkable = new bool[Squares];
    private readonly int[] _obstacle = new int[Squares];
    private readonly HashSet<int> _exits = [];
    private readonly List<int> _foundItems = [];
    private readonly HashSet<int> _monsterSpellsInForce = [];
    private int _orderIndex;
    private bool _partySurprises, _monstersSurprise, _dispelTried;

    /// <summary>
    /// <c>_evmon</c> (<c>_monevf(0)</c> in <c>_enc_mons</c>): the story flag this fight sets when won, or null. Its monsters
    /// never flee [C].
    /// </summary>
    public int? EventFlag { get; private set; }

    private P4Battle(GameOptions o, P4Tables t, P4BattleData data, P4Party party, IRandomSource rng, bool outdoors)
    {
        _o = o;
        _t = t;
        _data = data;
        Party = party;
        _rng = rng;
        Outdoors = outdoors;
    }

    public P4Party Party { get; }

    public bool Outdoors { get; }

    public P4BattlePrompt Prompt { get; private set; }

    /// <summary>The battlefield's name (<c>_e_on_*</c>; "dungeon" on a location map).</summary>
    public string Field { get; private set; } = "dungeon";

    /// <summary>
    /// <c>_etile</c> on a location map (<see cref="P4DungeonMap.BattleTerrain"/> of the party's square: 0 on floor 3 / 31,
    /// else 1); it picks the ground picture (<c>_e_finit</c>). 0 outdoors.
    /// </summary>
    public int Terrain { get; private init; }

    /// <summary>The messages of the current round (cleared when the options are asked again).</summary>
    public IReadOnlyList<string> Log => _log;

    public IReadOnlyList<P4BattleMember> Members { get; private set; } = [];

    public IReadOnlyList<P4MonsterGroup> Groups { get; private set; } = [];

    public IReadOnlyList<P4BattleMonster> Monsters { get; private set; } = [];

    /// <summary><c>_bturn</c>: rounds fought.</summary>
    public int Round { get; private set; }

    /// <summary><c>_emcham</c>: a monster has taken damage this fight (a greeting then fails).</summary>
    public bool MonsterHurt { get; private set; }

    /// <summary><c>_emlevel</c>: the encounter level (mean of the groups' levels, min 1).</summary>
    public int EncounterLevel { get; private set; }

    /// <summary><c>_emnice</c>: the mean of the groups' "nice" values.</summary>
    public int EncounterNice { get; private set; }

    /// <summary><c>_emgold</c> and <c>_epgetxp</c> (experience of the kills so far).</summary>
    public long Gold { get; private set; }

    public long Experience { get; private set; }

    /// <summary><c>_epweak</c>: % off all party damage after a monster Weakness.</summary>
    public int PartyWeakness { get; private set; }

    public P4BattleEnd End { get; private set; }

    /// <summary>True once <see cref="Continue"/> closed the report: the loop may go on (<see cref="Outcome"/>).</summary>
    public bool Done { get; private set; }

    /// <summary>How the loops after the fight see it.</summary>
    public P4FightOutcome Outcome => End switch
    {
        P4BattleEnd.PartyDead => P4FightOutcome.Lost,
        P4BattleEnd.AllKilled or P4BattleEnd.AllLeft or P4BattleEnd.Melted or P4BattleEnd.DrivenOff
            or P4BattleEnd.Surrendered => P4FightOutcome.Won,
        _ => P4FightOutcome.NotWon,
    };

    /// <summary>The member giving orders (<see cref="P4BattlePrompt.Order"/>).</summary>
    public P4BattleMember? Current => Prompt == P4BattlePrompt.Order ? Members[_orderIndex] : null;

    /// <summary>The item offered (<see cref="P4BattlePrompt.Treasure"/>).</summary>
    public int? OfferedItem => Prompt == P4BattlePrompt.Treasure ? _foundItems[0] : null;

    /// <summary>The field's exit squares (<c>_init_edge</c>).</summary>
    public IReadOnlyCollection<int> Exits => _exits;

    /// <summary>Whether a walker may stand on <paramref name="square"/> (no blocking obstacle).</summary>
    public bool Walkable(int square) => _walkable[square];

    /// <summary>The obstacle kind on <paramref name="square"/> (<c>_e_obstacle</c>), or −1.</summary>
    public int Obstacle(int square) => _obstacle[square];

    // --- Setup ---------------------------------------------------------------------------------------------------------

    /// <summary>
    /// An outdoor fight (<c>_oencgo</c> → <c>_encount</c>): the field and the first monster type from the terrain type of the
    /// party's square (<c>_e_terrain</c>), the map's area level (<c>_al</c>) and the surprise flags.
    /// </summary>
    public static P4Battle Outdoor(GameOptions o, P4Tables t, P4BattleData data, P4Party party, IRandomSource rng,
        P4OutdoorFight fight, int terrainType)
    {
        ArgumentNullException.ThrowIfNull(fight);
        var field = data.Tables.FieldFor(terrainType)
            ?? throw new InvalidOperationException($"Outdoor terrain type {terrainType} has no battlefield (_e_terrain).");
        var b = new P4Battle(o, t, data, party, rng, outdoors: true) { Field = field.Name };
        b.InitField(field, fight.AtSea || field.Name == "ship");
        var first = FirstMonster(field.Monster, fight.AreaLevel, rng);
        b.Begin(first, fight.PartySurprises, fight.MonstersSurprise);
        return b;
    }

    /// <summary>A location-map fight (<c>_denc</c>): the <c>DATM</c> type the event or the random roll names; no obstacles.</summary>
    public static P4Battle Dungeon(GameOptions o, P4Tables t, P4BattleData data, P4Party party, IRandomSource rng,
        int monsterType, int terrain = 0)
    {
        var b = new P4Battle(o, t, data, party, rng, outdoors: false) { Terrain = terrain };
        b.InitField(null, walled: true);
        b.Begin(monsterType, false, false);
        return b;
    }

    /// <summary><c>_init_terr</c> / <c>_init_edge</c> / <c>_e_on_*</c>'s obstacles.</summary>
    private void InitField(P4Battlefield? field, bool walled)
    {
        Array.Fill(_walkable, true);
        Array.Fill(_obstacle, -1);
        var bt = _data.Tables;
        if (field is not null)
        {
            foreach (var ob in field.Obstacles)
            {
                var count = P4Dice.Rand(_rng, ob.Die) + ob.Add;
                for (var placed = 0; placed < count;)
                {
                    var sq = P4Dice.Rand(_rng, bt.ObstacleSquareDie) + bt.ObstacleSquareAdd;
                    if (_obstacle[sq] >= 0)
                        continue;
                    _obstacle[sq] = ob.Kind;
                    _walkable[sq] = ob.Passable;
                    placed++;
                }
            }
        }
        // _init_edge: outdoors every edge square is an exit; in a location map or a sea fight one random side exit.
        if (!walled)
        {
            foreach (var e in bt.EdgeSquares)
                _exits.Add(e);
        }
        else
            _exits.Add(bt.SideExits[P4Dice.Rand(_rng, bt.SideExits.Count) - 1].Square);
    }

    /// <summary><c>_e_on_*</c>: rand(die) − 1 + times × area level + add, then the override roll.</summary>
    public static int FirstMonster(P4MonsterPick p, int areaLevel, IRandomSource rng)
    {
        ArgumentNullException.ThrowIfNull(p);
        var id = P4Dice.Rand(rng, p.Die) - 1 + p.AreaLevelTimes * areaLevel + p.Add;
        if (p.OverrideBelow > 0 && P4Dice.Rand(rng, 100) < p.OverrideBelow)
            id = P4Dice.Rand(rng, p.OverrideDie) - 1 + p.OverrideAdd;
        return id;
    }

    private void Begin(int firstType, bool partySurprises, bool monstersSurprise)
    {
        _partySurprises = partySurprises;
        _monstersSurprise = monstersSurprise;
        EventFlag = _data.Tables.EventFlag(firstType);
        SetUpMembers();
        BuildGroups(firstType);
        Place();
        if (monstersSurprise)
            RunRound();
        else
            AskOptions();
    }

    /// <summary>
    /// <c>_equip</c> + <c>_set1</c> for every member, with the record's +3B (<c>_exittown</c>) and +29. The code equips on
    /// leaving town (and in the Armory, Trade, Distribute); the remaster equips at the fight's start, which differs only
    /// when a Strength change outside town crosses an item's requirement <c>[~]</c>.
    /// </summary>
    private void SetUpMembers()
    {
        var list = new List<P4BattleMember>();
        for (var i = 0; i < Party.Members.Count; i++)
        {
            var m = Party.Members[i];
            var bm = new P4BattleMember { Slot = i, Member = m };
            Equip(bm);
            // _calmove / _baserate read the record's +29 (set at creation, raised by Pixie Dust and the Motion Potion), not the
            // race + class table again (a Duck keeps its old stride) [C].
            bm.Stride = m.MoveBase;
            bm.Flier = P4Creation.IsFlier(_t.Creation, m.Race);
            bm.Flying = bm.Flier;
            var armourPct = _data.Item(bm.ArmourId)?.MovePct ?? 0;
            bm.BaseMove = (int)Math.Truncate(bm.Stride + bm.Stride * armourPct / 100.0 + 0.5)
                + (m.Items.Contains(ShoesOfSpeed) ? 2 : 0);
            // _set1: Wm = +6B + +3B, the bonus _exittown wrote when the party last left a town (a Strength change or a level
            // since then does not count until the next town) [C].
            bm.Wm = Math.Min(255, bm.WeaponValue + m.MeleeBonus);
            bm.Hm = m.Skill(P4Skill.Attack);
            bm.Wr = bm.BowValue;
            bm.Hr = m.Skill(P4Skill.FireBow);
            bm.Armour = bm.ArmourValue;
            var baseMv = m.Condition != P4Member.Okay || m.CombatState is 0 or 4 ? 0
                : Math.Max(1, Math.Min(bm.Stride, bm.BaseMove));
            var d = m.Skill(P4Skill.Parry) / 8 + 10 * (bm.Flier ? 1 : 0) + 3 * baseMv + m.Abilities[2];
            bm.Defence = (d < 1 ? 0 : d) + bm.ShieldValue / 6;
            m.Defence = bm.Defence;
            list.Add(bm);
        }
        Members = list;
    }

    /// <summary>Shoes of Speed (item 130): +2 to the base move (<c>_baserate</c>).</summary>
    public const int ShoesOfSpeed = 130;

    /// <summary><c>_equip</c>: per slot the carried item with the most points the member can use (<c>_cal_user</c>).</summary>
    private void Equip(P4BattleMember bm)
    {
        var m = bm.Member;
        var (str, dex) = (m.Abilities[0], m.Abilities[2]);
        foreach (var id in m.Items)
        {
            if (_data.Item(id) is not { } it || it.Kind == P4ItemKind.Other)
                continue;
            var need = it.Check switch
            {
                P4StrCheck.Str2PlusDex => 2 * str + dex,
                P4StrCheck.StrPlusDex => str + dex,
                _ => str,
            };
            if (it.MinStr > need)
                continue;
            var points = !_o.P4Code(P4RuleOptions.LongSword) && _t.Towns.ItemPointsManual.TryGetValue(id, out var mp)
                ? mp : it.Points;
            switch (it.Kind)
            {
                case P4ItemKind.Shield when points > bm.ShieldValue: (bm.ShieldId, bm.ShieldValue) = (id, points); break;
                case P4ItemKind.Armour when points > bm.ArmourValue: (bm.ArmourId, bm.ArmourValue) = (id, points); break;
                case P4ItemKind.Weapon when points > bm.WeaponValue: (bm.WeaponId, bm.WeaponValue) = (id, points); break;
                case P4ItemKind.Bow when points > bm.BowValue: (bm.BowId, bm.BowValue) = (id, points); break;
                case P4ItemKind.Thrown when bm.ThrownId == 0 || points > (_data.Item(bm.ThrownId)?.Points ?? 0):
                    bm.ThrownId = id;
                    break;
            }
        }
    }

    /// <summary>
    /// <c>_enc_mons</c>: up to three groups. Group size rand(n), n = +14 but the party's average level (+0B) when +14 &gt;
    /// +0B + 1; per-monster HP (D4-055), gold and experience; another group follows with rand(100) ≤ +34, of type
    /// companions[rand(2) − 1]. The encounter level and nice value are the groups' means; treasure ids outdoors only.
    /// </summary>
    private void BuildGroups(int firstType)
    {
        var groups = new List<P4MonsterGroup>();
        var monsters = new List<P4BattleMonster>();
        long level = 0, nice = 0;
        var type = firstType;
        while (true)
        {
            var t = _data.Monsters[type];
            var n = t.GroupMax;
            if (Party.AverageLevel + 1 < n)
                n = Party.AverageLevel;
            var count = P4Dice.Rand(_rng, n);
            var g = new P4MonsterGroup(t, count);
            groups.Add(g);
            for (var i = 0; i < count; i++)
            {
                var m = new P4BattleMonster
                {
                    Group = g,
                    Letter = (char)('A' + monsters.Count),
                    Hp = P4Combat.MonsterHp(_o, _t.Combat, _rng, t.HpBase),
                    State = _partySurprises ? P4BattleMonster.Acted : P4BattleMonster.Ready,
                };
                Gold += t.Gold;
                monsters.Add(m);
            }
            level += t.Level;
            nice += t.Nice;
            if (groups.Count == _data.Tables.MaxGroups || P4Dice.Rand(_rng, 100) > t.NextGroupChance)
                break;
            type = t.Companions[P4Dice.Rand(_rng, 2) - 1];
        }
        Groups = groups;
        Monsters = monsters;
        EncounterLevel = Math.Max(1, (int)(level / groups.Count));
        EncounterNice = (int)(nice / groups.Count);
        if (Outdoors)
        {
            foreach (var id in groups[^1].Type.TreasureItems)
                if (id is > 0 and <= MaxItemId)
                    _treasureIds.Add(id);
        }
    }

    /// <summary>Item ids above this are dropped from the treasure list (<c>_findit</c>).</summary>
    public const int MaxItemId = 156;

    private readonly List<int> _treasureIds = [];

    /// <summary>
    /// <c>_echkdisp</c>: members from the bottom-right row upward, monsters from the top row down, each row by the columns
    /// 3, 2, 4, 1, 5, 0, 6 (mirrored for the party); a monster only on a walkable square. Dead members are not placed.
    /// </summary>
    private void Place()
    {
        var cols = _data.Tables.PlacementColumns;
        var queue = new Queue<P4BattleMember>(Members.Where(m => m.Member.Alive));
        for (var rowBase = Squares - 1; rowBase >= Size && queue.Count > 0; rowBase -= Size)
        {
            foreach (var c in cols)
            {
                if (queue.Count == 0)
                    break;
                var sq = rowBase - c;
                if (Occupied(sq))
                    continue;
                queue.Dequeue().Square = sq;
            }
        }
        var mq = new Queue<P4BattleMonster>(Monsters);
        for (var rowBase = 0; rowBase <= 41 && mq.Count > 0; rowBase += Size)
        {
            foreach (var c in cols)
            {
                if (mq.Count == 0)
                    break;
                var sq = rowBase + c;
                if (!_walkable[sq] || Occupied(sq))
                    continue;
                mq.Dequeue().Square = sq;
            }
        }
        // Monsters that found no square stay off the field (18 slots never fill 42 squares in practice).
        foreach (var m in mq)
            m.Gone = true;
    }

    // --- Field geometry ----------------------------------------------------------------------------------------------

    public static int Col(int sq) => sq % Size;

    public static int Row(int sq) => sq / Size;

    /// <summary><c>_b_range</c>: squared distance.</summary>
    public static int Range2(int a, int b)
    {
        var dx = Col(a) - Col(b);
        var dy = Row(a) - Row(b);
        return dx * dx + dy * dy;
    }

    /// <summary>Squares apart counting diagonals as one (reach).</summary>
    public static int Reach(int a, int b) => Math.Max(Math.Abs(Col(a) - Col(b)), Math.Abs(Row(a) - Row(b)));

    /// <summary>The 8 directions as the outdoor pad: 0 SW, 1 S, 2 SE, 3 W, 4 E, 5 NW, 6 N, 7 NE (row 0 at the top).</summary>
    private static readonly int[] DirX = [-1, 0, 1, -1, 1, -1, 0, 1], DirY = [1, 1, 1, 0, 0, -1, -1, -1];

    public static int? Neighbour(int sq, int dir)
    {
        var x = Col(sq) + DirX[dir];
        var y = Row(sq) + DirY[dir];
        return x is >= 0 and < Size && y is >= 0 and < Size ? y * Size + x : null;
    }

    public P4BattleMonster? MonsterAt(int sq) => Monsters.FirstOrDefault(m => m.OnField && m.Square == sq);

    public P4BattleMember? MemberAt(int sq) => Members.FirstOrDefault(m => m.OnField && m.Square == sq);

    public bool Occupied(int sq) => MonsterAt(sq) is not null || MemberAt(sq) is not null;

    private bool Enterable(int sq, bool flying) => (flying || _walkable[sq]) && !Occupied(sq);

    // --- Prompts -------------------------------------------------------------------------------------------------------

    private void Require(P4BattlePrompt p)
    {
        if (Prompt != p)
            throw new InvalidOperationException($"The battle is waiting for {Prompt}, not {p}.");
    }

    private void Say(string text)
    {
        foreach (var part in text.Split('\n'))
            if (part.Length > 0)
                _log.Add(part);
    }

    private void SayId(string id, params object[] args) => Say(P4MessageWindow.Format(id, args));

    private void AskOptions() => Prompt = P4BattlePrompt.Options;

    /// <summary>
    /// The encounter options (<c>_enc_opt</c>, §8) <c>[C]</c>: Greet, Beg for mercy and Demand surrender roll at once; a
    /// refusal, Fight, or a flight that leaves members on the field goes on to the members' orders (<see cref="GiveOrders"/>).
    /// Flee runs every able member's automatic turn with the flight flag (an exit square: he leaves; else he walks toward
    /// the nearest open exit). Q / R let the party strategy pick the option (<see cref="PartyStrategy"/>) and give every
    /// member automatic orders; after Q the client keeps choosing Q while <see cref="AutoCombat"/> holds.
    /// </summary>
    public void ChooseOption(P4EncounterOption option)
    {
        Require(P4BattlePrompt.Options);
        _log.Clear();
        _set.Clear();
        _chosenThisRound.Clear();
        foreach (var m in Members)
            PrepareMove(m);
        var auto = option is P4EncounterOption.AutoCombat or P4EncounterOption.AutoRound;
        AutoCombat = option == P4EncounterOption.AutoCombat;
        if (auto)
            option = PartyStrategy();
        switch (option)
        {
            case P4EncounterOption.Greet:
                SayId("p4x.0x43f55", GameText.S("p4x.0x43ebc"));
                // _enc_opt 0x1CE66: accepted when rand(100) × (1 − _emcham) > (50 outdoors / 90) − nice; _emcham is set by
                // the first damage to a monster, after which only a limit below 0 lets a greeting through [C].
                if (P4Dice.Rand(_rng, 100) * (MonsterHurt ? 0 : 1) > (Outdoors ? 50 : 90) - EncounterNice)
                {
                    SayId("p4x.0x43f89");
                    Finish(P4BattleEnd.Greeted);
                    return;
                }
                SayId("p4x.0x43f71");
                break;
            case P4EncounterOption.Beg:
                SayId("p4x.0x43f55", GameText.S("p4x.0x43ec9"));
                if (P4Dice.Rand(_rng, 100) < (Outdoors ? 90 + EncounterNice : 60 + 3 * EncounterNice))
                {
                    SayId("p4x.0x43fbb");
                    Finish(P4BattleEnd.Begged);
                    return;
                }
                SayId("p4x.0x43fd9");
                break;
            case P4EncounterOption.Demand:
            {
                SayId("p4x.0x43f55", GameText.S("p4x.0x43ed8"));
                var firstXp = Math.Max(1, Groups[0].Type.Experience);
                var c = (Outdoors ? 25 : 15) * (Experience / firstXp) + 2 * Party.AverageLevel - 3 * EncounterLevel;
                if (P4Dice.Rand(_rng, 100) <= c)
                {
                    SayId("p4x.0x44009");
                    Finish(P4BattleEnd.Surrendered);
                    return;
                }
                SayId("p4x.0x43ff1");
                break;
            }
            case P4EncounterOption.Fight:
                SayId("p4x.0x43f55", GameText.S("p4x.0x43ee7"));
                break;
            case P4EncounterOption.Flee:
                // _fleeflag = 1 and _eautomov in either mode: each able member's automatic turn (healing only, the walk
                // to an exit or leaving from one, a weapon choice).
                SayId("p4x.0x43f55", GameText.S("p4x.0x43efe"));
                _fleeing = true;
                foreach (var m in Members)
                {
                    if (End != P4BattleEnd.None)
                        break;
                    AutoTurn(m);
                }
                _fleeing = false;
                if (End != P4BattleEnd.None)
                    return;
                break;
        }
        GiveOrders(auto);
    }

    /// <summary>
    /// <c>_enc_opt</c> <c>0x1D0E2</c> <c>[C]</c>, after Fight, a refused Greet / Beg / Demand or a flight that left members on
    /// the field: in auto mode every member's automatic turn (<c>_eautomov</c>) and the round; in manual mode a summoned
    /// elemental acts automatically (<c>_eauto1(6)</c>) and the others give their orders.
    /// </summary>
    private void GiveOrders(bool auto)
    {
        if (auto)
        {
            AutoOrders();
            return;
        }
        foreach (var m in Members.Where(x => x.Elemental))
            AutoTurn(m);
        _orderIndex = -1;
        NextOrder();
    }

    /// <summary>The next member able to take orders, or the round.</summary>
    private void NextOrder()
    {
        for (_orderIndex++; _orderIndex < Members.Count; _orderIndex++)
        {
            var m = Members[_orderIndex];
            if (m.OnField && m.State is 2 or 3 && !m.Elemental)
            {
                Prompt = P4BattlePrompt.Order;
                return;
            }
        }
        RunRound();
    }

    /// <summary>
    /// Moves the member giving orders one square in direction <paramref name="dir"/> (0–7 as the outdoor pad): an
    /// orthogonal step costs 1 MV, diagonal steps 1, 2, 1, 2 … The square must be free and walkable (or the member flies).
    /// Returns false (nothing changes) when the step is refused.
    /// </summary>
    public bool Step(int dir)
    {
        Require(P4BattlePrompt.Order);
        return TryStep(Members[_orderIndex], dir);
    }

    private bool TryStep(P4BattleMember m, int dir)
    {
        if (Neighbour(m.Square, dir) is not int to || !Enterable(to, m.Flying))
            return false;
        var diagonal = dir is 0 or 2 or 5 or 7;
        var cost = diagonal && m.NextDiagonalCostsTwo ? 2 : 1;
        if (m.MoveLeft < cost)
            return false;
        m.MoveLeft -= cost;
        if (diagonal)
            m.NextDiagonalCostsTwo = !m.NextDiagonalCostsTwo;
        m.Facing = P4BattleFacing.Turn(m.Facing, to - m.Square);
        m.Square = to;
        Footstep(m.Flying); // _b_face after the step
        return true;
    }

    /// <summary>
    /// The actions the member giving orders may take now (<c>_opt_set</c>, §2): Throw / Fire Bow with both arms able and
    /// the weapon equipped; melee by reach; Cast Spell; Fly / Land for a flier with sound wings; Parry; Leave on an exit.
    /// </summary>
    public IReadOnlyList<P4ActionKind> Offered()
    {
        Require(P4BattlePrompt.Order);
        return OfferedFor(Members[_orderIndex]);
    }

    private List<P4ActionKind> OfferedFor(P4BattleMember m)
    {
        var list = new List<P4ActionKind>();
        var w = m.Member.Wounds;
        if (m.State == 3 && w[(int)P4BodyPart.RightArm] < P4Body.Serious)
        {
            if (w[(int)P4BodyPart.LeftArm] < P4Body.Serious)
            {
                if (m.ThrownId != 0)
                    list.Add(P4ActionKind.Throw);
                if (m.BowId != 0)
                    list.Add(P4ActionKind.FireBow);
            }
            var cls = m.Member.Class;
            for (var r = 1; r <= 2; r++)
            {
                if (r == 2 && cls != P4Ids.Thief && cls != P4Ids.Fighter)
                    break;
                if (!Monsters.Any(x => x.OnField && Reach(x.Square, m.Square) <= r))
                    continue;
                if (cls == P4Ids.Fighter)
                    Add(P4ActionKind.Lunge);
                if (r == 2 && cls == P4Ids.Fighter)
                    continue;
                Add(P4ActionKind.Thrust);
                Add(P4ActionKind.AimBlow);
                if (m.Hm >= _t.Combat.AttackFrom)
                    Add(P4ActionKind.Attack);
                if (m.Hm >= _t.Combat.SlashFrom)
                    Add(P4ActionKind.Slash);
            }
        }
        list.Add(P4ActionKind.Cast);
        if (m.Flier && w[(int)P4BodyPart.RightArm] == 0 && w[(int)P4BodyPart.LeftArm] == 0)
            list.Add(m.Flying ? P4ActionKind.Land : P4ActionKind.Fly);
        list.Add(P4ActionKind.Parry);
        if (_exits.Contains(m.Square))
            list.Add(P4ActionKind.Leave);
        return list;

        void Add(P4ActionKind k)
        {
            if (!list.Contains(k))
                list.Add(k);
        }
    }

    /// <summary>The spells the member giving orders may cast: known, built for combat, MP ≥ cost (<c>_splist</c>).</summary>
    public IReadOnlyList<int> CastableSpells()
    {
        Require(P4BattlePrompt.Order);
        var m = Members[_orderIndex].Member;
        return Enumerable.Range(1, P4Member.SpellCount)
            .Where(s => m.Knows(s) && CombatSpellBuilt(s) && m.Mp >= P4Combat.MpCost(_o, _t.Combat, s)).ToList();
    }

    /// <summary>
    /// Spells this fight can cast: the code's combat spell pages (<c>_getmagic(member, 0)</c>, lists at <c>0x44AE2</c>), i.e.
    /// every spell but 15, 47, 49, 50, 63 and 65. 64 (Annihilate Enemy) is listed and only empties the caster's MP (D4-071).
    /// </summary>
    public static bool CombatSpellBuilt(int spell) =>
        spell is >= 1 and <= 14 or >= 16 and <= 46 or 48 or >= 51 and <= 62 or 64;

    /// <summary>The list's pack name (FORMAT.md 6.9 "Combat spell list"): <c>_getmagic(member, 0)</c>.</summary>
    public const string SpellListName = "_getmagic.0";

    /// <summary>
    /// <c>_getmagic(member, 0)</c>'s category pages (s152, FORMAT.md 6.9 "Combat spell list"): 1 Non-combat, 2 Combat
    /// (received), 3–5 Combat, each its spells in the table's order (<c>0x44AC8</c> / <c>0x44AE2</c>). The list opens on page
    /// 0 each time Cast is chosen. A table set without them (a fixture) has one page of every combat spell.
    /// </summary>
    public IReadOnlyList<IReadOnlyList<int>> SpellPages =>
        _t.Combat.SpellPagesCode ?? [Enumerable.Range(1, P4Member.SpellCount).Where(CombatSpellBuilt).ToList()];

    /// <summary>
    /// The string ids of the list's head lines (<c>_spelltop(0)</c>, <c>0x44A98</c>[0–5]): one per page, then Quit; empty
    /// without the table (the Client then numbers the pages itself).
    /// </summary>
    public IReadOnlyList<string> SpellPageHeads => _t.Combat.SpellPageHeads ?? [];

    /// <summary>The spells of <paramref name="page"/> the member giving orders may cast now, in the page's order.</summary>
    public IReadOnlyList<int> CastableSpells(int page)
    {
        var castable = CastableSpells();
        return [.. SpellPages[page].Where(castable.Contains)];
    }

    /// <summary>
    /// The combat spell list as the original draws it on page <paramref name="page"/> (s152, <c>_splist(member, page, 0)</c>):
    /// every spell of the page in its order, lettered A…; the name (from <paramref name="spellNames"/>, by number − 1) and a
    /// choice only for a castable one (choice = its index in <see cref="CastableSpells(int)"/>), the letter alone otherwise.
    /// The head lines stand for the choices after the spells: the pages 0–4, then Quit (<see cref="SpellListChoice"/>).
    /// </summary>
    public P4ListWindow SpellList(int page, IReadOnlyList<string> spellNames)
    {
        ArgumentNullException.ThrowIfNull(spellNames);
        var offered = CastableSpells(page).ToList();
        var rows = SpellPages[page].Select(n => offered.IndexOf(n) is >= 0 and var i
            ? new P4ListRow(P4Names.At(spellNames, n - 1), i) : new P4ListRow(null, null));
        return new P4ListWindow(SpellListName, [.. rows], [.. Enumerable.Range(offered.Count, SpellPages.Count + 1)]);
    }

    /// <summary>
    /// What choice <paramref name="choice"/> of <see cref="SpellList"/> on <paramref name="page"/> stands for: a spell
    /// (its number), another page, or Quit (<c>_getmagic</c> returns 100: no order, the member's orders go on).
    /// </summary>
    public (int? Spell, int? Page, bool Quit) SpellListChoice(int page, int choice)
    {
        var offered = CastableSpells(page);
        if (choice >= 0 && choice < offered.Count)
            return (offered[choice], null, false);
        var head = choice - offered.Count;
        return head >= 0 && head < SpellPages.Count ? (null, head, false) : (null, null, true);
    }

    /// <summary>Whether a spell needs a party slot (Healing, Resurrection) or a field square (Fire Flash, code Flameball).</summary>
    public SpellTarget TargetOf(int spell) => spell switch
    {
        >= 1 and <= 4 or 40 => SpellTarget.Member,
        >= 5 and <= 8 or 42 or >= 51 and <= 54 => SpellTarget.Square,
        >= 55 and <= 58 when _o.P4Code(P4RuleOptions.Flameball) => SpellTarget.Square,
        _ => SpellTarget.None,
    };

    public enum SpellTarget { None, Member, Square }

    /// <summary>
    /// Sets the action of the member giving orders (§2). A weapon needs a monster's square within its reach (1, Lunge and a
    /// Thief's melee 2, bow / throw / spells 6); a spell must be castable and get the target it needs. Parry is rolled
    /// here: rand(100) &lt; Parry, or the left leg seriously hurt. Returns false (no change) for an order the menu refuses.
    /// </summary>
    public bool Order(P4Order order)
    {
        Require(P4BattlePrompt.Order);
        ArgumentNullException.ThrowIfNull(order);
        var m = Members[_orderIndex];
        if (!OfferedFor(m).Contains(order.Kind))
            return false;
        switch (order.Kind)
        {
            case P4ActionKind.AimBlow or P4ActionKind.Lunge or P4ActionKind.Attack or P4ActionKind.Thrust
                or P4ActionKind.Slash or P4ActionKind.FireBow or P4ActionKind.Throw:
                if (order.Target is < 0 or >= Squares || MonsterAt(order.Target) is null
                    || Reach(m.Square, order.Target) > ReachOf(m, order.Kind))
                    return false;
                break;
            case P4ActionKind.Cast:
                if (!CastableSpells().Contains(order.Spell))
                    return false;
                switch (TargetOf(order.Spell))
                {
                    case SpellTarget.Member when order.Target < 0 || order.Target >= Members.Count:
                    case SpellTarget.Square when order.Target is < 0 or >= Squares:
                        return false;
                }
                break;
            case P4ActionKind.Fly:
                m.Flying = true;
                return true; // takes no MV and no action: the member still gives an order
            case P4ActionKind.Land:
                if (!_walkable[m.Square])
                    return false;
                m.Flying = false;
                return true;
        }
        m.Order = order;
        _set.Add(m.Slot);
        if (order.Kind == P4ActionKind.Parry)
            m.Parry = P4Dice.Rand(_rng, 100) < m.Member.Skill(P4Skill.Parry)
                || m.Member.Wounds[(int)P4BodyPart.LeftLeg] >= P4Body.Serious ? 1 : 0;
        NextOrder();
        return true;
    }

    /// <summary>Reach in <c>_opt_get</c>: 1 melee, 2 Lunge and a Thief's melee, 6 bow / throw / spells.</summary>
    private static int ReachOf(P4BattleMember m, P4ActionKind k) => k switch
    {
        P4ActionKind.FireBow or P4ActionKind.Throw or P4ActionKind.Cast => 6,
        P4ActionKind.Lunge => 2,
        _ => m.Member.Class == P4Ids.Thief ? 2 : 1,
    };

    /// <summary>A found item: <paramref name="take"/> puts it in the pack (if a slot is free) or leaves it.</summary>
    public void TakeItem(bool take)
    {
        Require(P4BattlePrompt.Treasure);
        var id = _foundItems[0];
        _foundItems.RemoveAt(0);
        if (take && Array.IndexOf(Party.Pack, 0) >= 0)
        {
            Party.AddItem(id, _rng);
            SayId("p4x.0x44477");
        }
        NextTreasure();
    }

    /// <summary>Closes the report at <see cref="P4BattlePrompt.Over"/>: <see cref="Done"/>.</summary>
    public void Continue()
    {
        Require(P4BattlePrompt.Over);
        Done = true;
    }
}
