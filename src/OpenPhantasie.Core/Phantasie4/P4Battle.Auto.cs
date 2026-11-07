namespace OpenPhantasie.Core;

/// <summary>
/// Auto mode (<c>combat.md</c> §8.2): Q auto combat / R auto round (<c>_xpopt</c>, <c>_partyact</c>), the members' automatic
/// turn (<c>_eauto1</c>: <c>_try_cast</c>, <c>_b_amove</c>, <c>_protect</c>, <c>_try_attack</c>) and M auto move.
/// </summary>
public sealed partial class P4Battle
{
    /// <summary>Q chosen (<c>_xpopt</c> = 0): every later round is fought by the party strategy without the options menu.</summary>
    public bool AutoCombat { get; private set; }

    /// <summary>Members whose orders are already set this round (<c>_eflag</c>): positioned by hand or given auto orders.</summary>
    private readonly HashSet<int> _set = [];

    /// <summary>M auto move in manual mode (<c>_player</c>): a member on an exit only reports it and does not leave.</summary>
    private bool _player;

    private bool _fleeing;

    /// <summary>Party spells in force this fight (<c>_csf</c>), for <c>_try_cast</c>'s "not already in force".</summary>
    private readonly HashSet<int> _partySpellsInForce = [];

    /// <summary>
    /// <c>_splft</c> merged into <c>_splfc</c>: spells another member's automatic turn already chose this round. Healing and
    /// Resurrection are never "in force" (<c>_pmagic1</c> sets <c>_splfp</c> only for lasting spells), so for them this is
    /// the whole of <c>_csf</c> [C].
    /// </summary>
    private readonly HashSet<int> _chosenThisRound = [];

    /// <summary><c>_par</c>+15E / +15F: the party strategy (attitude 0 friendly … 3 demand; flight 0 … 3 never).</summary>
    public int Attitude => Party.StoryFlags[0x3E];

    public int FlightSetting => Party.StoryFlags[0x3F];

    /// <summary>
    /// <c>_partyact</c> <c>[C]</c>: friendly → Greet while <c>_bturn</c> ≤ 3, then Demand surrender; hostile → Fight; surrender →
    /// Beg; demand → Demand. Then Flee when the members' max HP total × 75 / 60 / 40 / 0 % (flight setting) exceeds their
    /// current HP total (16-bit sums). <c>_bturn</c> = rounds fought + 1 (<c>_encount</c>: 1 at the start, + 1 after each round) <c>[C]</c>.
    /// </summary>
    public P4EncounterOption PartyStrategy()
    {
        var option = Attitude switch
        {
            0 => Round + 1 <= 3 ? P4EncounterOption.Greet : P4EncounterOption.Demand,
            2 => P4EncounterOption.Beg,
            3 => P4EncounterOption.Demand,
            _ => P4EncounterOption.Fight,
        };
        var pct = FlightSetting switch { 0 => 75, 1 => 60, 2 => 40, _ => 0 };
        int max = 0, cur = 0;
        foreach (var m in Members)
        {
            max = (max + m.Member.HpMax) & 0xFFFF;
            cur = (cur + Math.Max(0, m.Member.Hp)) & 0xFFFF;
        }
        if (((max * pct / 100) & 0xFFFF) > cur)
            option = P4EncounterOption.Flee;
        return option;
    }

    /// <summary>
    /// <c>_eautomov</c>: <see cref="AutoTurn"/> for every member (party order) whose orders are not set yet; then the round
    /// (unless <paramref name="thenRun"/> is false).
    /// </summary>
    private void AutoOrders(bool thenRun = true)
    {
        foreach (var m in Members)
        {
            if (End != P4BattleEnd.None)
                return;
            AutoTurn(m);
        }
        if (thenRun && End == P4BattleEnd.None)
            RunRound();
    }

    /// <summary>
    /// M auto move (manual mode, <c>_player</c> = 1 → <c>_eautomov</c>): the member giving orders and those after him get
    /// their automatic turn — members already positioned by hand keep theirs — and the round starts (M then F) <c>[~]</c>
    /// (the original shows the new actions first and waits for F).
    /// </summary>
    public void AutoMove()
    {
        Require(P4BattlePrompt.Order);
        _player = true;
        for (var i = _orderIndex; i < Members.Count; i++)
            AutoTurn(Members[i]);
        _player = false;
        if (End == P4BattleEnd.None)
            RunRound();
    }

    /// <summary>
    /// <c>_eauto1(member)</c> <c>[C]</c>: a member on the field whose orders are not set, standing (<c>_pc</c> 3) and Okay, tries a
    /// spell up to three times (<see cref="TryCast"/>); without one he moves by his strategy (<see cref="AutoWalk"/>) and,
    /// unless he covers a companion (<see cref="Protecting"/>), picks a weapon action (<see cref="TryAttack"/>).
    /// </summary>
    private void AutoTurn(P4BattleMember m)
    {
        if (!m.OnField || !_set.Add(m.Slot))
            return;
        if (m.State != 3 || m.Member.Condition != P4Member.Okay)
            return;
        for (var k = 0; k < 3; k++)
            if (TryCast(m))
                return;
        AutoWalk(m);
        if (End != P4BattleEnd.None || !m.OnField)
            return;
        if (!Protecting(m))
            TryAttack(m);
    }

    /// <summary>
    /// <c>_b_amove</c> for a member (§7.2) <c>[C]</c>: the target by movement strategy D, then <c>_b_chkflee</c> (the party's
    /// flight, or HP below 30 / 55 % of max for auto defence A = 1 / 2) and the walk.
    /// </summary>
    private void AutoWalk(P4BattleMember m)
    {
        var target = -1;
        if (!_fleeing)
        {
            var d = m.Member.Strategy[3];
            switch (d & 0x0F)
            {
                case 0: target = Center(party: true); break;
                case 1: target = Faraway(Center(party: true)); break;
                case 2:
                    // _protwho: the code asks "Whom does %s support?" the first time; the remaster takes the protégé set
                    // beforehand (SetSupport) and does not move without one [~].
                    if ((d & 0x80) != 0 && Members.FirstOrDefault(x => x.Slot == ((d >> 4) & 7)) is { OnField: true } p)
                        target = p.Square;
                    break;
                case 3:
                    // Next to a monster and with any square remembered as attacked or hit (_atk_list > 0): stay [C].
                    target = Monsters.Any(x => x.OnField && Reach(x.Square, m.Square) <= 1) && m.AttackMemory.Any(v => v > 0)
                        ? -1 : NearestFoe(m.Square, true);
                    break;
                case 4: target = Faraway(Center(party: false)); break;
                case 5: target = Center(party: false); break;
            }
        }
        var a = m.Member.Strategy[0];
        var limit = (a == 1 ? 30 : 0) + (a == 2 ? 55 : 0);
        if (_fleeing || m.Member.Hp * 100.0 < m.Member.HpMax * limit)
        {
            if (_exits.Contains(m.Square))
            {
                if (!_player)
                    TryLeave(m);
                return;
            }
            target = NearestExit(m.Square);
        }
        if (target is >= 0 and < Squares)
            WalkMember(m, target);
    }

    /// <summary><c>_center(side)</c>: mean row × 7 + mean column of that side's squares (integer division); −1 when none.</summary>
    private int Center(bool party)
    {
        int rows = 0, cols = 0, n = 0;
        for (var sq = 0; sq < Squares; sq++)
        {
            if ((party ? MemberAt(sq) is null : MonsterAt(sq) is null))
                continue;
            rows += Row(sq);
            cols += Col(sq);
            n++;
        }
        return n == 0 ? -1 : rows / n * Size + cols / n;
    }

    /// <summary><c>_faraway(sq)</c>: the free walkable square farthest from <paramref name="from"/> (first on a tie), else itself.</summary>
    private int Faraway(int from)
    {
        if (from < 0)
            return -1;
        var best = from;
        var bestD = 0;
        for (var sq = 0; sq < Squares; sq++)
        {
            if (sq == from || !WalkByte(sq))
                continue;
            var d = Range2(from, sq);
            if (d > bestD)
                (best, bestD) = (sq, d);
        }
        return best;
    }

    /// <summary>
    /// Sets the companion a member with movement strategy 2 covers (<c>_protwho</c>'s answer, +2D = 0x80 | slot × 16 | 2);
    /// the profile screen sets it before the fight in the remaster.
    /// </summary>
    public static void SetSupport(P4Member member, int slot)
    {
        ArgumentNullException.ThrowIfNull(member);
        member.Strategy[3] = 0x80 | ((slot & 7) << 4) | (member.Strategy[3] & 0x0F);
    }

    /// <summary>
    /// <c>_protect</c> <c>[C]</c>: not while fleeing; only with the "covering" bit (+2D bit 7). A protégé no longer on the field
    /// clears the choice (+2D = 2). Otherwise it returns a target square (a monster next to both, else the nearest monster),
    /// which <c>_eauto1</c> only tests for ≥ 0: a covering member makes no weapon choice and keeps the action he had.
    /// </summary>
    private bool Protecting(P4BattleMember m)
    {
        var d = m.Member.Strategy[3];
        if (_fleeing || (d & 0x80) == 0)
            return false;
        if (Members.FirstOrDefault(x => x.Slot == ((d >> 4) & 7)) is not { OnField: true })
        {
            m.Member.Strategy[3] = 2;
            return false;
        }
        return Monsters.Any(x => x.OnField);
    }

    /// <summary>
    /// <c>_try_attack</c> <c>[C]</c>: the memory <see cref="P4BattleMember.AttackMemory"/> is cleared when the member stands on
    /// another square than at the last call. The nearest ring r = 1 … 6 holding monsters gives the target: <c>_atklist</c>
    /// (<see cref="AtkList"/>), else a random one of them. r ≥ 3: Fire Bow (B = 0, a bow) or Throw (B = 1, a thrown weapon);
    /// r = 2: Lunge for a Fighter; r = 1, or 2 for a Thief: by C — bow, throw, Thrust, Slash, Aim Blow. Slash beyond the
    /// member's skill falls back to Attack, then Thrust (manual p. 43) <c>[~]</c>; anything else not offered leaves the action
    /// as it was. <c>_opt_get(1, …)</c> does not test the reach.
    /// </summary>
    private void TryAttack(P4BattleMember m)
    {
        if (m.MemorySquare != m.Square)
        {
            m.MemorySquare = m.Square;
            Array.Fill(m.AttackMemory, -1);
        }
        var (b, c) = (m.Member.Strategy[1], m.Member.Strategy[2]);
        var cls = m.Member.Class;
        for (var r = 1; r <= 6; r++)
        {
            var ring = Monsters.Where(x => x.OnField && Reach(x.Square, m.Square) <= r).OrderBy(x => x.Square).ToList();
            if (ring.Count == 0)
                continue;
            var remembered = AtkList(m, ring.Count);
            var target = remembered >= 0 ? remembered : ring[P4Dice.Rand(_rng, ring.Count) - 1].Square;
            P4ActionKind? kind = null;
            if (r >= 3)
                kind = b == 0 && m.BowId != 0 ? P4ActionKind.FireBow : b == 1 && m.ThrownId != 0 ? P4ActionKind.Throw : null;
            if (r == 2 && cls == P4Ids.Fighter)
                kind = P4ActionKind.Lunge;
            if (r == 1 || (r == 2 && cls == P4Ids.Thief))
                kind = c switch
                {
                    0 when m.BowId != 0 => P4ActionKind.FireBow,
                    1 when m.ThrownId != 0 => P4ActionKind.Throw,
                    2 => P4ActionKind.Thrust,
                    3 => P4ActionKind.Slash,
                    4 => P4ActionKind.AimBlow,
                    _ => kind,
                };
            if (kind is { } k)
            {
                var offered = OfferedFor(m);
                if (k == P4ActionKind.Slash && !offered.Contains(k))
                    k = offered.Contains(P4ActionKind.Attack) ? P4ActionKind.Attack : P4ActionKind.Thrust;
                if (offered.Contains(k))
                    m.Order = new P4Order(k, target);
            }
            return;
        }
    }

    /// <summary>
    /// <c>_atklist(list)</c> <c>[C]</c>: over the list's <paramref name="count"/> positions i, the first with the highest
    /// memory value above −1 — but the memory is read at <b>square i</b>, not at the square listed there (original slip),
    /// and that position is then used as the target square. −1 (no favourite) when the best value is below 0.
    /// </summary>
    private static int AtkList(P4BattleMember m, int count)
    {
        var (best, at) = (-1, 0);
        for (var i = 0; i < count; i++)
            if (m.AttackMemory[i] > best)
                (best, at) = (m.AttackMemory[i], i);
        return m.AttackMemory[at] < 0 ? -1 : at;
    }

    /// <summary>
    /// <c>_try_cast</c> (§9.2) by magic setting E, with the spell sets of D4-046 (<see cref="P4Combat.AutoMayCast"/>,
    /// <see cref="P4Combat.AutoAttackWalk"/>). A spell needs MP left ≥ max MP / 2 (healing and counters) or / 3 (the
    /// random defence) (<c>_psetcast</c>). s110 <c>[C]</c>: the members are visited in party order — a dead one gets
    /// Resurrection, an Okay one below half HP (HP &lt; HP max &gt;&gt; 1) the highest Healing not chosen this round —; monster
    /// Slow 1 / Slow 2 in force bring Speed 1 / Speed 2 (spells 9 / 10); the chance is (30 / 50 / 80) − 5 × <c>_bturn</c>.
    /// E = 2 never casts. Which dead member Resurrection may target (the code tests his square's walk byte) stays
    /// <c>[~]</c>.
    /// </summary>
    private bool TryCast(P4BattleMember m)
    {
        var e = m.Member.Strategy[4];
        var me = m.Member;
        bool Can(int s, int div) => me.Knows(s) && CombatSpellBuilt(s) && P4Combat.AutoMayCast(_o, _t.Combat, s)
            && P4Combat.MpCost(_o, _t.Combat, s) is var cost && me.Mp >= cost && me.Mp - cost >= me.MpMax / div;
        bool Cast(int s, int target = -1)
        {
            m.Order = new P4Order(P4ActionKind.Cast, target, s);
            _partySpellsInForce.Add(s);
            _chosenThisRound.Add(s);
            return true;
        }
        bool Chosen(int s) => _chosenThisRound.Contains(s);
        bool Hurting(P4BattleMember x) => x.Member.Condition == P4Member.Okay && x.Member.Hp < (x.Member.HpMax >> 1);
        int IndexOf(P4BattleMember x)
        {
            for (var i = 0; i < Members.Count; i++)
                if (ReferenceEquals(Members[i], x))
                    return i;
            return -1;
        }
        if (_fleeing || e >= 3)
        {
            foreach (var x in Members)
            {
                if (!_fleeing && !x.Member.Alive)
                {
                    if (x.Member.Abilities[3] != 0 && !Chosen(40) && Can(40, 2))
                        return Cast(40, IndexOf(x));
                    continue;
                }
                if (x.Member.Alive && Hurting(x))
                    foreach (var s in new[] { 4, 3, 2, 1 })
                        if (!Chosen(s) && Can(s, 2))
                            return Cast(s, IndexOf(x));
            }
            if (_fleeing)
                return false;
            // _msf(11 + i) → _psetcast(9 + i): Slow 1 is answered with Speed 1, Slow 2 with Speed 2.
            for (var i = 0; i < 2; i++)
                if (_monsterSpellsInForce.Contains(11 + i) && Can(9 + i, 2))
                    return Cast(9 + i);
            if (_monsterSpellsInForce.Contains(14) && Can(13, 2))
                return Cast(13);
            if (Members.Count(x => x.Member.Condition == P4Member.Asleep) >= 2 && !Chosen(46) && Can(46, 2))
                return Cast(46);
            var chance = (e switch { 3 => 30, 4 => 50, _ => 80 }) - 5 * (Round + 1);
            if (P4Dice.Rand(_rng, 100) < chance)
            {
                // One entry of the 7 (rand(7)), refused when it or its kind is already in force / chosen: Speed 1 / 2
                // when either is, Strength 1–4 when any is; then _psetcast with MP left >= max MP / 3. No second pick [C].
                var table = _t.Combat.AutoDefenceCode;
                var pick = table[P4Dice.Rand(_rng, table.Count) - 1];
                bool InForce(int s) => _partySpellsInForce.Contains(s) || Chosen(s);
                var ok = pick switch
                {
                    9 or 10 => !InForce(table[0]) && !InForce(table[1]),
                    13 => !InForce(13),
                    >= 16 and <= 19 => !table.Skip(3).Take(4).Any(InForce),
                    _ => true,
                };
                if (ok && Can(pick, 3))
                    return Cast(pick);
            }
        }
        if (e is 0 or 1 or 3)
        {
            // _msf(9 + i) → 11 + i: monster Speed 1 / 2 answered with Slow 1 / 2; _msf(13) → 14; _msf(30 + i) → 59 + i [C].
            for (var i = 0; i < 2; i++)
                if (_monsterSpellsInForce.Contains(9 + i) && Can(11 + i, 2))
                    return Cast(11 + i);
            if (_monsterSpellsInForce.Contains(13) && Can(14, 2))
                return Cast(14);
            for (var i = 0; i < 4; i++)
                if (_monsterSpellsInForce.Contains(30 + i) && Can(59 + i, 2))
                    return Cast(59 + i);
            // Dispel Undead: the rand(100) is drawn first for an undead fight; then < 40, not chosen, not yet tried [C].
            if (Groups[0].Type.Undead && P4Dice.Rand(_rng, 100) <= 39 && !Chosen(43) && !_dispelTried && Can(43, 2))
                return Cast(43);
            if (Monsters.Any(x => x.OnField && x.Group.Flying) && !Chosen(14) && !_partySpellsInForce.Contains(14) && Can(14, 2))
                return Cast(14);
            var chance = e switch { 0 => 75, 1 => 35, _ => 15 };
            if (P4Dice.Rand(_rng, 100) < chance)
            {
                // The roll rand(10000) < 785 − 30 i comes first, _psetcast only after it [C].
                var walk = P4Combat.AutoAttackWalk(_o, _t.Combat);
                for (var i = 0; i < walk.Count; i++)
                {
                    var s = walk[i];
                    if (P4Dice.Rand(_rng, 10_000) >= 785 - 30 * i)
                        continue;
                    if (TargetOf(s) == SpellTarget.Square)
                    {
                        // _faropp: the farthest monster (squared distance, first on a tie); none there skips the entry.
                        var far = Monsters.Where(x => x.OnField).OrderByDescending(x => Range2(m.Square, x.Square))
                            .ThenBy(x => x.Square).FirstOrDefault();
                        if (far is null)
                            continue;
                        if (Can(s, 2))
                            return Cast(s, far.Square);
                        continue;
                    }
                    if (Can(s, 2))
                        return Cast(s);
                }
            }
        }
        return false;
    }
}
