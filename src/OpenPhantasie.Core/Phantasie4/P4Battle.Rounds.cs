namespace OpenPhantasie.Core;

public sealed partial class P4Battle
{
    // --- Movement (combat.md §7) --------------------------------------------------------------------------------------

    /// <summary>
    /// <c>_calmove</c> at the start of a round: last round's unused MV kept by the action (Aim Blow / Fire Bow ½, Parry 0,
    /// Attack ⅔, Thrust ⅓, a 1–4 MP spell ¾ / ½ / ¼ / 0, else all) + the base move, at least 1, at most the stride; then
    /// the spell modifier (≥ 11 absolute, 0–10 added, Slow ½ / ¼ or ¾ by D4-052) and a flier's wing table; 0 when not able.
    /// </summary>
    private void PrepareMove(P4BattleMember m)
    {
        var left = m.MoveLeft;
        var kept = m.Order.Kind switch
        {
            P4ActionKind.AimBlow or P4ActionKind.FireBow => left / 2,
            P4ActionKind.Parry => 0,
            P4ActionKind.Attack => 2 * left / 3,
            P4ActionKind.Thrust => left / 3,
            P4ActionKind.Cast => P4Combat.MpCost(_o, _t.Combat, m.Order.Spell) switch
            {
                1 => left - left / 4,
                2 => left / 2,
                3 => left / 4,
                _ => 0,
            },
            _ => left,
        };
        var mv = kept + m.BaseMove;
        if (mv < 0)
            mv = 1;
        mv = Math.Min(mv, m.Stride);
        mv = ApplySpellMove(mv, m.SpellMove);
        if (m.Flying && m.Flier)
            mv = WingMove(mv, m.Member.Wounds);
        if (mv < 1)
            mv = 1;
        if (m.Member.Condition != P4Member.Okay || m.State is 0 or 4)
            mv = 0;
        m.Move = m.MoveLeft = mv;
        m.NextDiagonalCostsTwo = false;
        m.Acted = false;
        m.Parry = 0;
        m.Order = new P4Order(P4ActionKind.Parry);
    }

    private int ApplySpellMove(int mv, int mod) => mod switch
    {
        >= 11 => mod,
        >= 0 => mv + mod,
        -1 => (int)(mv * P4Combat.SlowStrideFactor(_o, 1)),
        _ => (int)(mv * P4Combat.SlowStrideFactor(_o, 2)),
    };

    /// <summary><c>_movlost</c> for a flier with a wounded wing: legs unhurt ×¾, one leg light ½, both light ¼, serious 0.</summary>
    private static int WingMove(int mv, int[] w)
    {
        if (w[(int)P4BodyPart.RightArm] == 0 && w[(int)P4BodyPart.LeftArm] == 0)
            return mv;
        var (r, l) = (w[(int)P4BodyPart.RightLeg], w[(int)P4BodyPart.LeftLeg]);
        if (r >= P4Body.Serious || l >= P4Body.Serious)
            return 0;
        return (r, l) switch
        {
            (0, 0) => 3 * mv / 4,
            (0, _) or (_, 0) => mv / 2,
            _ => mv / 4,
        };
    }

    /// <summary><c>_base</c>: the 3 × 3 neighbourhood as square offsets, NW N NE / W centre E / SW S SE.</summary>
    private static readonly int[] Base = [-8, -7, -6, -1, 0, 1, 6, 7, 8];

    /// <summary>
    /// The walk byte of <c>_pos</c> (+1): walkable terrain with nobody on it (placement and <c>_b_mmove</c> zero it under
    /// an occupant).
    /// </summary>
    private bool WalkByte(int sq) => _walkable[sq] && !Occupied(sq);

    /// <summary>
    /// <c>_b_chauto(square, target, mv)</c> after <c>_b_chbtn</c> / <c>_dii(0)</c> (<c>combat.md</c> §7.1) <c>[C]</c>: up to
    /// <paramref name="mv"/> steps, each to the neighbour (inside the box of radius <paramref name="mv"/> around the start;
    /// a walker only onto free walkable squares, a flier with sound wings onto any free square) with the smallest squared
    /// distance to the target — even when that is no closer —, ties to the first in <see cref="Base"/> order. The walk
    /// stops when that neighbour is a square already passed, or the target itself when it is occupied or (for a walker)
    /// blocked, or occupied. Every step counts against the loop; the MV left drops by 1, or 1, 2, 1, 2 … on diagonals
    /// (floor 0). <paramref name="place"/> moves the mover onto each new square. Returns the end square.
    /// </summary>
    private int ChAuto(int from, int target, int mv, bool flying, bool wingsSound, Action<int> place, ref int mvLeft)
    {
        if (target is < 0 or >= Squares || mv <= 0)
            return from;
        var visited = new bool[Squares];
        if (Occupied(target) || (!flying && !_walkable[target]))
            visited[target] = true;
        var flierMode = flying && wingsSound;
        var sq = from;
        var diagonals = 0;
        for (var i = 0; i < mv; i++)
        {
            visited[sq] = true;
            var best = 4;
            var bestD = int.MaxValue;
            for (var k = 0; k < 9; k++)
            {
                if (k == 4)
                    continue;
                var n = sq + Base[k];
                if (n is < 0 or >= Squares || (Col(sq) == 0 && k % 3 == 0) || (Col(sq) == Size - 1 && k % 3 == 2)
                    || Reach(from, n) > mv)
                    continue;
                if (flierMode ? Occupied(n) : !WalkByte(n))
                    continue;
                var d = Range2(n, target);
                if (d < bestD)
                    (best, bestD) = (k, d);
            }
            var next = sq + Base[best];
            if (visited[next] || Occupied(next))
                break;
            var cost = 1;
            if (best is 0 or 2 or 6 or 8)
                cost = ++diagonals % 2 == 1 ? 1 : 2;
            mvLeft = Math.Max(0, mvLeft - cost);
            sq = next;
            place(sq);
        }
        return sq;
    }

    /// <summary>
    /// <c>_b_getnear</c>: the open exit nearest the mover (squared distance, first in edge-table order). The code tests the
    /// walk byte of square <i>i</i> rather than of exit <i>i</i> (original slip, kept) <c>[C]</c>; with none, the mover's square.
    /// </summary>
    private int NearestExit(int from)
    {
        var edges = _data.Tables.EdgeSquares;
        var best = from;
        var bestD = int.MaxValue;
        for (var i = 0; i < edges.Count; i++)
        {
            if (!WalkByte(i) || !_exits.Contains(edges[i]))
                continue;
            var d = Range2(from, edges[i]);
            if (d < bestD)
                (best, bestD) = (edges[i], d);
        }
        return best;
    }

    /// <summary>
    /// <c>_b_friend(0)</c>: the nearest combatant of the other side (squared distance, first in square order), or −1.
    /// </summary>
    private int NearestFoe(int from, bool ofParty)
    {
        var best = -1;
        var bestD = int.MaxValue;
        for (var sq = 0; sq < Squares; sq++)
        {
            if (sq == from || (ofParty ? MonsterAt(sq) is null : MemberAt(sq) is null))
                continue;
            var d = Range2(from, sq);
            if (d < bestD)
                (best, bestD) = (sq, d);
        }
        return best;
    }

    /// <summary>
    /// <c>_f2f</c>'s turn (<c>0x29906</c>) <c>[C]</c>: whoever stands on <paramref name="b"/>, then whoever stands on
    /// <paramref name="a"/>, turns toward the other (<see cref="P4BattleFacing"/>); an empty square turns nobody and the same
    /// square gives delta 0. Used for melee and missile attacks and for the spell casts that call <c>_f2f</c>
    /// (<c>combat.md</c> §9.4: every monster cast toward its aim; party Fire Flash, Sink, Confusion, Weakness, Mind Blast,
    /// Dissolve, Shock Wave, Flameball; monster Fire Flash, Sink, Mind Blast, breath).
    /// </summary>
    private void FaceOff(int a, int b)
    {
        if (a is < 0 or >= Squares || b is < 0 or >= Squares || a == b)
            return;
        TurnOn(b, P4BattleFacing.Toward(b, a));
        TurnOn(a, P4BattleFacing.Toward(a, b));

        void TurnOn(int sq, int delta)
        {
            if (MemberAt(sq) is { } m)
            {
                m.Facing = P4BattleFacing.Turn(m.Facing, delta);
                Footstep(m.Flying);
            }
            else if (MonsterAt(sq) is { } mon)
            {
                mon.Facing = P4BattleFacing.Turn(mon.Facing, delta);
                Footstep(mon.Group.Flying);
            }
        }
    }

    /// <summary>
    /// s152: <c>_b_face</c> <c>0x22E74</c> plays <c>_noise</c> (effect 0) for a combatant that does not fly
    /// (<c>_tmark</c>+7 = 0), at every call: each step and each <c>_f2f</c> turn.
    /// </summary>
    private void Footstep(bool flying)
    {
        if (!flying)
            Party.Sounds.Note(P4Sounds.CombatStep);
    }

    /// <summary>Walks a party member (<see cref="ChAuto"/>) and keeps his MV left.</summary>
    private void WalkMember(P4BattleMember m, int target)
    {
        var left = m.MoveLeft;
        var w = m.Member.Wounds;
        ChAuto(m.Square, target, m.MoveLeft, m.Flying, w[(int)P4BodyPart.RightArm] == 0 && w[(int)P4BodyPart.LeftArm] == 0,
            to =>
            {
                (m.Facing, m.Square) = (P4BattleFacing.Turn(m.Facing, to - m.Square), to);
                Footstep(m.Flying);
            }, ref left);
        m.MoveLeft = left;
    }

    /// <summary><c>_b_chkedge</c> for a member: on an exit square he leaves the field; the last one out ends the fight (4).</summary>
    private bool TryLeave(P4BattleMember m)
    {
        if (!_exits.Contains(m.Square))
            return false;
        m.Gone = true;
        m.Square = -1;
        Say(P4MessageWindow.Format("p4x.0x4475d", m.Member.Name).TrimStart('\n') + GameText.S("p4x.0x44762"));
        if (!Members.Any(x => x.OnField) && Party.AnyAlive)
        {
            SayId("p4x.0x44021");
            Finish(P4BattleEnd.Fled);
        }
        return true;
    }

    private int MonsterMove(P4BattleMonster mon)
    {
        var mv = Math.Max(1, ApplySpellMove(mon.Group.Type.Movement, mon.SpellMove));
        return mon.Group.Flying ? WingMove(mv, mon.Wounds) : mv;
    }

    // --- The round (combat.md §1.3) ----------------------------------------------------------------------------------

    /// <summary>Phases 1, 3, 5: members, then monsters; then the acted flags are cleared and the options asked again.</summary>
    private void RunRound()
    {
        foreach (var mon in Monsters)
            mon.WakeRolled = false;
        foreach (var p in new[] { 1, 3, 5 })
        {
            if (!(Round == 0 && _monstersSurprise))
            {
                foreach (var m in Members)
                {
                    MemberActs(m, p);
                    if (End != P4BattleEnd.None)
                        break;
                }
            }
            if (End != P4BattleEnd.None)
                break;
            foreach (var mon in Monsters)
            {
                MonsterActs(mon, p);
                if (End != P4BattleEnd.None)
                    break;
            }
            if (End != P4BattleEnd.None)
                break;
        }
        Round++;
        _partySurprises = _monstersSurprise = false;
        foreach (var mon in Monsters)
            if (mon.State == P4BattleMonster.Acted)
                mon.State = P4BattleMonster.Ready;
        if (End == P4BattleEnd.None)
            DissolveTick();
        if (End == P4BattleEnd.None)
        {
            foreach (var m in Members)
                m.Acted = false;
            AskOptions();
        }
    }

    private static bool Early(P4Order o) =>
        o.Kind is P4ActionKind.FireBow or P4ActionKind.Throw || (o.Kind == P4ActionKind.Cast && o.Spell is >= 5 and <= 8);

    /// <summary>
    /// <c>_chrexe(member, p)</c>: a sleeper rolls to wake ((15 + 32 × wounded parts) %) and does nothing; melee and most
    /// spells act when rand(100) − 1 ≤ 5 p² + 3 L (bow, throw and Fire Flash in phase 1 without the roll); a serious right-arm
    /// wound turns any action but bow / throw into Parry.
    /// </summary>
    private void MemberActs(P4BattleMember m, int p)
    {
        if (!m.OnField || m.Acted)
            return;
        if (m.Member.Condition == P4Member.Asleep)
        {
            m.Acted = true;
            var hurt = m.Member.Wounds.Count(w => w > 0);
            if (P4Dice.Rand(_rng, 100) < 16 + 32 * hurt)
            {
                m.Member.Condition = P4Member.Okay;
                Say(m.Member.Name + " " + GameText.S("p4x.0x44846"));
            }
            return;
        }
        if (m.State is 0 or 4)
        {
            m.Acted = true;
            return;
        }
        var order = m.Order;
        if (!Early(order) && P4Dice.Rand(_rng, 100) - 1 > 5 * p * p + 3 * m.Member.Level)
            return;
        m.Acted = true;
        if (order.Kind is not (P4ActionKind.FireBow or P4ActionKind.Throw)
            && m.Member.Wounds[(int)P4BodyPart.RightArm] >= P4Body.Serious)
            order = new P4Order(P4ActionKind.Parry);
        switch (order.Kind)
        {
            case P4ActionKind.Leave:
                if (_exits.Contains(m.Square))
                {
                    m.Gone = true;
                    m.Square = -1;
                    Say(P4MessageWindow.Format("p4x.0x4475d", m.Member.Name).TrimStart('\n') + GameText.S("p4x.0x44762"));
                    if (!Members.Any(x => x.OnField) && Party.AnyAlive)
                    {
                        SayId("p4x.0x44021");
                        Finish(P4BattleEnd.Fled);
                    }
                }
                break;
            case P4ActionKind.AimBlow or P4ActionKind.Lunge or P4ActionKind.Attack or P4ActionKind.Thrust or P4ActionKind.Slash:
                Melee(m, order);
                break;
            case P4ActionKind.FireBow or P4ActionKind.Throw:
                Missile(m, order);
                break;
            case P4ActionKind.Cast:
                Cast(m, order);
                break;
        }
    }

    private static P4WeaponAction WeaponAction(P4ActionKind k) => k switch
    {
        P4ActionKind.AimBlow => P4WeaponAction.AimBlow,
        P4ActionKind.Lunge => P4WeaponAction.Lunge,
        P4ActionKind.Attack => P4WeaponAction.Attack,
        P4ActionKind.Thrust => P4WeaponAction.Thrust,
        _ => P4WeaponAction.Slash,
    };

    private static readonly Dictionary<P4ActionKind, string> ActionShout = new()
    {
        [P4ActionKind.AimBlow] = "p4x.0x44806", [P4ActionKind.Lunge] = "p4x.0x44811", [P4ActionKind.Attack] = "p4x.0x44818",
        [P4ActionKind.Thrust] = "p4x.0x44820", [P4ActionKind.Slash] = "p4x.0x44828", [P4ActionKind.FireBow] = "p4x.0x4484f",
        [P4ActionKind.Throw] = "p4x.0x44871",
    };

    private void Shout(P4BattleMember m, P4ActionKind k) => Say(m.Member.Name + ": " + GameText.S(ActionShout[k]));

    /// <summary>
    /// A weapon action (§3): one hit roll and one damage roll (code) or Attack's 2 / Slash's n strikes (D4-042 manual);
    /// a bare-handed Shadow Warrior's own damage and clean strike; criticals by weapon; then <c>_mdamage</c>. The target must
    /// still be on its square and in reach, or the action is lost <c>[~]</c>.
    /// </summary>
    private void Melee(P4BattleMember m, P4Order order)
    {
        var target = MonsterAt(order.Target);
        if (target is null && m.State == 3)
            Remember(m, order.Target, -2); // _chrexe: _target finds nobody there
        if (target is null || Reach(m.Square, target.Square) > ReachOf(m, order.Kind) || m.State != 3)
            return;
        Remember(m, target.Square, 1);
        var action = WeaponAction(order.Kind);
        Shout(m, order.Kind);
        FaceOff(m.Square, target.Square);
        var strikes = P4Combat.Strikes(_o, _t.Combat, action, m.Hm);
        for (var s = 0; s < strikes && target.OnField && End == P4BattleEnd.None; s++)
        {
            var c = P4Combat.MeleeHitChance(_o, _t.Combat, action, m.Hm, target.Group.Defence, Helpless(target),
                attackerDown: m.State == 2);
            if (P4Dice.Rand(_rng, 100) > c)
            {
                Remember(m, target.Square, -2);
                Say(GameText.S("p4x.0x44859"));
                continue;
            }
            Remember(m, target.Square, 2);
            int d;
            var level = m.Member.Level;
            if (m.Member.Class == P4Ids.ShadowWarrior && m.WeaponId == 0)
            {
                d = (P4Dice.Rand(_rng, P4Dice.Rand(_rng, 2 * (level + 9)) - 1) - 1)
                    + P4Dice.Rand(_rng, P4Dice.Rand(_rng, 3 * level - 1)) - target.Group.Armour;
                d = Math.Max(0, d);
                if (P4Dice.Rand(_rng, 100) < 25)
                {
                    SayId("p4x.0x44897");
                    d += level + 10;
                }
            }
            else
            {
                d = P4Combat.MeleeDamage(_o, _rng, action, m.Wm, target.Group.Armour, m.Hm);
                if (Critical(m.WeaponId))
                {
                    SayId("p4x.0x4482f");
                    d += m.Wm;
                }
            }
            d = Math.Min(255, d + m.StrengthBonus);
            MonsterDamage(target, d, AttackType(m, target),
                aimed: order.Kind == P4ActionKind.AimBlow && !_o.P4Code(P4RuleOptions.AimBlow));
        }
    }

    /// <summary><c>_chrexe</c>'s <c>_atk_list</c> writes (<see cref="P4BattleMember.AttackMemory"/>).</summary>
    private static void Remember(P4BattleMember m, int square, int value)
    {
        if (square is >= 0 and < Squares)
            m.AttackMemory[square] = value;
    }

    /// <summary><c>_chdtyp</c>: melee type 1, or 5 for a monster with +43 or a flying monster hit from the ground.</summary>
    private static int AttackType(P4BattleMember m, P4BattleMonster target) =>
        target.Group.Type.BigHitter || (target.Group.Flying && !m.Flying) ? 5 : 1;

    /// <summary><c>_critical(item)</c>: rand(100) &lt; t by item (0 for most items).</summary>
    private bool Critical(int item) =>
        _data.Tables.CriticalByItem.TryGetValue(item, out var t) && P4Dice.Rand(_rng, 100) < t;

    /// <summary>
    /// Bow / throw (§3.1–3.2): hit = skill − defence / 4 (D4-037: a throw uses Throwing under the manual); damage from the
    /// bow value with the class bonus; a throw with a serious right arm is off-hand (hit ÷ 2, damage × ⅔). A hit that does
    /// not end the fight clears the Strength bonus.
    /// </summary>
    private void Missile(P4BattleMember m, P4Order order)
    {
        var isThrow = order.Kind == P4ActionKind.Throw;
        var target = MonsterAt(order.Target);
        var w = m.Member.Wounds;
        var right = w[(int)P4BodyPart.RightArm] >= P4Body.Serious;
        var left = w[(int)P4BodyPart.LeftArm] >= P4Body.Serious;
        if (target is null || m.State != 3 || (isThrow ? right && left : right || left))
        {
            if (target is null && m.State == 3 && !(isThrow ? right && left : right || left))
                Remember(m, order.Target, -2);
            return;
        }
        Remember(m, target.Square, 1);
        var offHand = isThrow && right;
        Shout(m, order.Kind);
        FaceOff(m.Square, target.Square);
        if (offHand)
            SayId("p4x.0x44885");
        var skill = P4Combat.MissileSkill(_o, m.Hr, m.Member.Skill(P4Skill.Throwing), isThrow);
        var c = P4Combat.MissileHitChance(skill, target.Group.Defence, Helpless(target));
        c = m.State == 2 ? 3 * c / 4 : offHand ? c / 2 : c;
        if (P4Dice.Rand(_rng, 100) > c)
        {
            Remember(m, target.Square, -2);
            Say(GameText.S("p4x.0x44859"));
            return;
        }
        Remember(m, target.Square, 2);
        var k = (isThrow ? _data.Tables.ThrownClassBonus : _data.Tables.BowClassBonus)[m.Member.Class];
        var d = P4Combat.MissileDamage(_rng, m.Wr, target.Group.Armour, k);
        if (Critical(isThrow ? m.ThrownId : m.BowId))
        {
            SayId("p4x.0x4482f");
            d += m.Wr;
        }
        if (offHand)
            d = 2 * d / 3;
        d = Math.Min(255, d + m.StrengthBonus);
        MonsterDamage(target, d, isThrow ? 6 : 10);
        if (End == P4BattleEnd.None)
            m.StrengthBonus = 0;
    }

    // --- Damage (§3.4, §4.4, §5, §6) ---------------------------------------------------------------------------------

    /// <summary><c>_body</c>: the part names (head, body, right arm, left arm, right leg, left leg).</summary>
    private static readonly string[] PartNames =
        ["p4x.0x3e631", "p4x.0x3e634", "p4x.0x3e637", "p4x.0x3e63c", "p4x.0x3e641", "p4x.0x3e646"];

    /// <summary><c>_hurt</c>: the wound words by level (healed, wounded, badly wounded, lost).</summary>
    private static readonly string[] Hurt = ["p4x.0x3e64b", "p4x.0x3e656", "p4x.0x3e65f", "p4x.0x3e66e"];

    /// <summary><c>_setmc</c>: 0 head / torso lost, 4 head / torso serious, 1 asleep, 2 a leg serious or lost, 3 normal.</summary>
    public static int Condition(P4BattleMonster m)
    {
        var w = m.Wounds;
        if (w[0] >= P4Body.Lost || w[1] >= P4Body.Lost)
            return 0;
        if (w[0] == P4Body.Serious || w[1] == P4Body.Serious)
            return 4;
        if (m.State == P4BattleMonster.Asleep)
            return 1;
        return w[4] >= P4Body.Serious || w[5] >= P4Body.Serious ? 2 : 3;
    }

    /// <summary>Party hits are sure (chance 200) on a monster with condition &lt; 3 or 4.</summary>
    public static bool Helpless(P4BattleMonster m) => Condition(m) is < 3 or 4;

    /// <summary>
    /// <c>_mdamage</c>: d less the monsters' Weakness on the party (%), at least 1; for type &gt; 0 a hit location (body
    /// shapes 2 and 3 turn it into the torso) and a wound; the monster dies when d ≥ HP or its head / torso is lost, its
    /// experience going to the fight; the last one on the field ends it (101). <paramref name="aimed"/> (D4-058, manual):
    /// an Aim Blow lands on the head or torso, except where no blow reaches them (type 5, manual p. 46).
    /// </summary>
    private void MonsterDamage(P4BattleMonster mon, int d, int type, bool aimed = false)
    {
        MonsterHurt = true;
        d -= PartyWeakness * d / 100;
        if (d < 1)
            d = 1;
        Party.Sounds.Note(P4Sounds.MonsterDamaged); // _mdamage 0x21BF4: _sddam once d is at least 1
        if (type > 0)
        {
            var part = (int)P4Body.MonsterHitLocation(_rng, type, aimed);
            var shape = mon.Group.Type.BodyShape;
            if (shape == 2 || (shape == 3 && part != 0))
                part = (int)P4BodyPart.Torso;
            var old = mon.Wounds[part];
            mon.Wounds[part] = P4Body.WoundLevel(_rng, d, mon.Hp, old);
            if (mon.Wounds[part] > old)
            {
                var name = shape switch
                {
                    2 => "p4x.0x44237",
                    3 when part != 0 => "p4x.0x4423a",
                    4 when part == (int)P4BodyPart.RightArm => "p4x.0x4423d",
                    4 when part == (int)P4BodyPart.LeftArm => "p4x.0x44244",
                    _ => PartNames[part],
                };
                SayId("p4x.0x44226", mon.Name, GameText.S(name), GameText.S(Hurt[mon.Wounds[part]]));
            }
        }
        if (d < mon.Hp && Condition(mon) > 0)
        {
            mon.Hp -= d;
            SayId("p4x.0x4424b", mon.Name, d);
            return;
        }
        Party.Sounds.Note(P4Sounds.MonsterKilled); // _mdamage 0x21CEE: the death branch's own _sddam
        mon.Hp = 0;
        mon.Dead = true;
        Experience += mon.Group.Type.Experience;
        SayId("p4x.0x44269", mon.Name);
        if (!Monsters.Any(x => x.OnField))
        {
            SayId("p4x.0x444d6");
            Finish(P4BattleEnd.AllKilled);
        }
    }

    /// <summary>
    /// <c>_pdamage</c>: at least 1; for type &gt; 0 a hit location and a wound (against max HP). The member dies when d ≥ HP,
    /// or (D4-043 manual) when his head or torso is lost; the code leaves him Out. Everybody dead ends the game (1).
    /// </summary>
    private void MemberDamage(P4BattleMember m, int d, int type)
    {
        if (!m.Member.Alive)
            return;
        if (d < 1)
            d = 1;
        Party.Sounds.Note(P4Sounds.MemberDamaged); // _pdamage 0x1E9C0: _sddam once d is at least 1
        if (type > 0)
        {
            var part = (int)P4Body.HitLocation(_rng, type, partyMember: true);
            if (m.Elemental)
                part = (int)P4BodyPart.Torso;
            var old = m.Member.Wounds[part];
            m.Member.Wounds[part] = P4Body.WoundLevel(_rng, d, m.Member.HpMax, old);
            if (m.Member.Wounds[part] > old)
            {
                var name = m.Elemental ? "p4x.0x440e4"
                    : m.Flier && part == (int)P4BodyPart.RightArm ? "p4x.0x440e7"
                    : m.Flier && part == (int)P4BodyPart.LeftArm ? "p4x.0x440ee"
                    : PartNames[part];
                SayId("p4x.0x440d3", m.Member.Name, GameText.S(name), GameText.S(Hurt[m.Member.Wounds[part]]));
            }
        }
        if (d < m.Member.Hp && !P4Body.WoundsKill(_o, m.Member.Wounds))
        {
            m.Member.Hp -= d;
            SayId("p4x.0x440f5", m.Member.Name, d);
            return;
        }
        Party.Sounds.Note(P4Sounds.MemberKilled); // _pdamage 0x1EB4A: the death branch's own _sddam
        m.Member.Hp = 0;
        m.Member.Condition = P4Member.Dead;
        m.Square = -1;
        SayId("p4x.0x44129", m.Member.Name);
        if (!Party.AnyAlive)
        {
            Say(GameText.S("p4x.0x44097"));
            Finish(P4BattleEnd.PartyDead);
        }
    }

    // --- Monsters (§4) -------------------------------------------------------------------------------------------------

    /// <summary>
    /// <c>_monexe</c> in phase p: a sleeper wakes on rand(100) &lt; 15 (in every phase) and does nothing that phase; a ready monster with a
    /// sound right arm and condition 2 or 3 acts on rand(100) ≤ 5 p² + 3 × encounter level. With rand(100) &lt; its spell
    /// ability it moves (<see cref="MonsterMoves"/>), aims at a random member in the nearest ring that holds one
    /// (<c>_obj_select(0)</c>) and casts one of its three ids (0: nothing; 68 and up: it moves again and makes a melee
    /// attack with that special); otherwise it moves and strikes a random member next to it (<c>_obj_select(1)</c>) <c>[C]</c>.
    /// </summary>
    private void MonsterActs(P4BattleMonster mon, int p)
    {
        if (!mon.OnField)
            return;
        if (mon.State == P4BattleMonster.Asleep)
        {
            // _monexe makes the wake test at every call: once in each phase (s102) [C].
            if (P4Dice.Rand(_rng, 100) < _data.Tables.WakeMonsterBelow)
            {
                Say(mon.Name + " " + GameText.S("p4x.0x44554"));
                mon.State = P4BattleMonster.Acted;
            }
            return;
        }
        var mc = Condition(mon);
        if (mon.State != P4BattleMonster.Ready || mon.Wounds[(int)P4BodyPart.RightArm] >= P4Body.Serious || mc is not (2 or 3))
            return;
        if (P4Dice.Rand(_rng, 100) > 5 * p * p + 3 * EncounterLevel)
            return;
        var special = 0;
        if (P4Dice.Rand(_rng, 100) < mon.Group.SpellAbility)
        {
            MonsterMoves(mon);
            if (!mon.OnField)
                return;
            mon.State = P4BattleMonster.Acted;
            var aim = ObjSelect(mon, melee: false);
            if (aim is not null)
                FaceOff(mon.Square, aim.Square); // _monexe _f2f(monster, aim, 200) 0x25578: before the id is picked [C]
            var ids = mon.Group.Type.SpellIds;
            var id = 0;
            for (var tries = 0; tries < 10; tries++)
            {
                id = ids[P4Dice.Rand(_rng, ids.Count) - 1];
                if (!_monsterSpellsInForce.Contains(id))
                    break;
            }
            if (id is >= 1 and <= 67 && aim is not null)
            {
                if (id <= 65)
                    Party.Sounds.Note(P4Sounds.MonsterCasts); // _monexe 0x256AC: _sdmag after the name; breath 66 / 67 silent
                MonsterCast(mon, id, aim);
            }
            if (id < 68)
                return;
            special = id;
        }
        MonsterMoves(mon);
        if (!mon.OnField)
            return;
        mon.State = P4BattleMonster.Acted;
        if (ObjSelect(mon, melee: true) is { } target)
            MonsterMelee(mon, target, special);
    }

    /// <summary>
    /// <c>_b_amove</c> for a monster <c>[C]</c>: with a member next to it, it stays (and never flees). Otherwise its target is the
    /// nearest member (<c>_b_friend(0)</c>); <c>_b_chkflee</c> turns it into an exit when its HP is below 30 % of its HP base
    /// (never in an event fight, <see cref="EventFlag"/>) (or, D4-052 manual Fear, it is afraid): on an exit it leaves the field at once, else it heads for the nearest open
    /// exit. Then <see cref="ChAuto"/> with its MV (<c>_emmove</c>; the remaster's MV rule, §14).
    /// </summary>
    private void MonsterMoves(P4BattleMonster mon)
    {
        if (Members.Any(m => m.OnField && Reach(m.Square, mon.Square) <= 1))
            return;
        var target = NearestFoe(mon.Square, ofParty: false);
        // _b_chkflee: an event fight's monsters (_evmon >= 0) never flee for low HP [C]; the manual Fear stays [~].
        if (mon.Afraid || (EventFlag is null && mon.Hp * 100 < _data.Tables.MonsterFleeBelowPercent * mon.Group.Type.HpBase))
        {
            if (_exits.Contains(mon.Square))
            {
                mon.Gone = true;
                mon.Square = -1;
                Say(P4MessageWindow.Format("p4x.0x4475d", mon.Name).TrimStart('\n') + GameText.S("p4x.0x44762"));
                if (!Monsters.Any(x => x.OnField))
                    Finish(P4BattleEnd.AllLeft);
                return;
            }
            target = NearestExit(mon.Square);
        }
        var mv = MonsterMove(mon);
        var left = mv;
        ChAuto(mon.Square, target, mv, mon.Group.Flying,
            mon.Wounds[(int)P4BodyPart.RightArm] == 0 && mon.Wounds[(int)P4BodyPart.LeftArm] == 0, to =>
            {
                (mon.Facing, mon.Square) = (P4BattleFacing.Turn(mon.Facing, to - mon.Square), to);
                Footstep(mon.Group.Flying);
            },
            ref left);
    }

    /// <summary>
    /// <c>_obj_select(kind)</c> <c>[C]</c>: for reach r = 1 (melee) or r = 1 … 6 (spells), the members within r squares in
    /// square order; the first r that holds any gives a random one (rand(count) − 1); none → null.
    /// </summary>
    private P4BattleMember? ObjSelect(P4BattleMonster mon, bool melee)
    {
        for (var r = 1; r <= (melee ? 1 : 6); r++)
        {
            var near = Members.Where(m => m.OnField && Reach(m.Square, mon.Square) <= r).OrderBy(m => m.Square).ToList();
            if (near.Count > 0)
                return near[P4Dice.Rand(_rng, near.Count) - 1];
        }
        return null;
    }

    /// <summary>
    /// §4.2: n = max(1, trunc(+16 / 115 + 0.7)) attacks; each hits on rand(100) &lt; trunc(+16 / n) − D or a target not
    /// standing (<c>_pc</c> &lt; 3); damage trunc(u^0.5 × +18 (× the manual's Weakness)) − A, cut by a parry's shield once,
    /// plus the monster's Strength; special 68 paralyses.
    /// </summary>
    private void MonsterMelee(P4BattleMonster mon, P4BattleMember target, int special)
    {
        var t = mon.Group.Type;
        var n = Math.Max(1, (int)Math.Truncate(t.AttackValue / 115.0 + 0.7));
        Say(mon.Name + ": " + GameText.S("p4x.0x4455d"));
        FaceOff(mon.Square, target.Square);
        for (var i = 0; i < n && target.OnField && End == P4BattleEnd.None; i++)
        {
            if (!(P4Dice.Rand(_rng, 100) < t.AttackValue / n - target.Defence || target.State < 3))
            {
                Say(GameText.S("p4x.0x448ba"));
                continue;
            }
            var u = Math.Pow(P4Dice.Frand(_rng), 0.5);
            var d = (int)Math.Truncate(u * Math.Truncate(t.DamageValue * mon.Group.DamageFactor)) - target.Armour;
            if (target.Parry == 1 && d > 0)
            {
                d -= target.ShieldValue;
                target.Parry = -1;
            }
            MemberDamage(target, mon.Strength + d, 1);
            if (special == 68 && target.Member.Alive)
            {
                target.Member.Condition = P4Member.Asleep;
                SayId("p4x.0x44565", target.Member.Name);
            }
        }
    }

    /// <summary>
    /// The monster spell resistance roll on a member: rand(100) ≤ (monster +24 − member max MP) × 5 + 50 (unsigned under
    /// the code option: <see cref="P4Combat.SuccessRoll"/>; Slow <c>0x29D44</c>, Mind Blast <c>0x2A282</c>).
    /// </summary>
    private bool MonsterSpellWorks(P4BattleMonster mon, P4BattleMember m) =>
        P4Combat.SuccessRoll(_o, _rng, (mon.Group.Type.MagicResistance - m.Member.MpMax) * 5 + 50);

    /// <summary><c>_mmagic</c> (§9.3). Ids it has no combat effect for do nothing <c>[?]</c>.</summary>
    private void MonsterCast(P4BattleMonster mon, int id, P4BattleMember aim)
    {
        var party = Members.Where(m => m.OnField).ToList();
        if (party.Count == 0)
            return;
        var rank = 0;
        switch (id)
        {
            case >= 5 and <= 8:
            {
                rank = id - 4;
                FaceOff(mon.Square, aim.Square); // _f2f 0x29B6C / 0x29B84
                MemberDamage(aim, P4Dice.Rand(_rng, _t.Combat.FireFlashN[rank - 1]), 2);
                break;
            }
            case 9 or 10:
                foreach (var x in Monsters)
                    x.SpellMove = _data.Tables.Speed[id - 9];
                SayId("p4x.0x448c0");
                break;
            case 11 or 12:
                foreach (var m in party)
                    if (MonsterSpellWorks(mon, m))
                        m.SpellMove = -(id - 10);
                SayId("p4x.0x448d7");
                break;
            case 14:
                foreach (var m in party.OrderBy(x => x.Square))
                {
                    if (!m.Flying || !_walkable[m.Square])
                        continue;
                    m.Flying = false;
                    Party.Sounds.Note(P4Sounds.Lands); // _mmagic 0x29E6C: _noise as the flag clears
                    FaceOff(mon.Square, m.Square); // _f2f 0x29E82: each flier that lands, in square order
                }
                SayId("p4x.0x448f7");
                break;
            case >= 16 and <= 19:
            {
                var floor = 5 + 2 * (id - 16);
                foreach (var g in Groups)
                    g.Armour = Math.Max(g.Armour, floor);
                SayId("p4x.0x44919");
                break;
            }
            case >= 30 and <= 33:
                PartyWeakness = 20 * (id - 29);
                SayId("p4x.0x44934");
                break;
            case >= 34 and <= 37:
                rank = id - 33;
                foreach (var m in party.OrderBy(x => x.Square))
                {
                    if (End != P4BattleEnd.None || !m.OnField)
                        continue;
                    // 0x2A20A: an immune member draws nothing; otherwise the damage rand(N) comes before the resistance
                    // rand(100), so a member who resists still uses up both draws.
                    if (P4Combat.MindBlast(_o, _t.Combat, _rng, rank, m.Member.Experience) is not int d
                        || !MonsterSpellWorks(mon, m))
                        continue;
                    FaceOff(mon.Square, m.Square); // _f2f 0x2A2A0, before the damage
                    MemberDamage(m, d, 0);
                }
                break;
            case 38:
                foreach (var m in party)
                {
                    if (P4Dice.Rand(_rng, 100) >= m.Member.Level * m.Member.Level)
                    {
                        m.Member.Condition = P4Member.Asleep;
                        Say(m.Member.Name + ": " + GameText.S("p4x.0x449a8"));
                    }
                }
                break;
            case 66 or 67:
                Breath(mon, id == 66 ? 2 : 4, id == 66);
                break;
        }
        if (id is >= 9 and <= 14 or >= 16 and <= 19 or >= 30 and <= 33 or 38)
            _monsterSpellsInForce.Add(id);
    }

    /// <summary>
    /// Breath 66 / 67 (<c>_mmagic</c> <c>0x2A3D6</c>) <c>[C]</c>: for r = 1 … 6 the squares within r of the monster (the
    /// <c>_fmark</c> box <c>_b_chbtn(monster, r)</c> marks just before the scan) are scanned in square order and every member
    /// found takes rand(rand(16) + 9) / rand(rand(31) + 14) − A (type 2; 67 draws the unused rand(16) first); a member nearer
    /// than r is found again on each wider ring. The scan stops once more than <paramref name="n"/> hits (2 / 4) have been
    /// made, so up to n + 1 hits land (original off-by-one and repeat, kept).
    /// </summary>
    private void Breath(P4BattleMonster mon, int n, bool small)
    {
        var hits = 0;
        for (var r = 1; r <= 6; r++)
        {
            for (var sq = 0; sq < Squares; sq++)
            {
                if (End != P4BattleEnd.None || hits > n)
                    return;
                if (Reach(sq, mon.Square) > r || MemberAt(sq) is not { } m)
                    continue;
                // 0x2A488: rand(16) is drawn for both ids; 67 then overwrites it with rand(31) (3 draws per hit).
                var inner = P4Dice.Rand(_rng, 16) + 9;
                if (!small)
                    inner = P4Dice.Rand(_rng, 31) + 14;
                var d = P4Dice.Rand(_rng, inner);
                FaceOff(mon.Square, sq); // _f2f 0x2A512 / 0x2A52A: once per hit
                MemberDamage(m, d - m.Armour, 2);
                hits++;
            }
        }
    }

    // --- Party spells (§9.1) -------------------------------------------------------------------------------------------

    /// <summary>
    /// The party spell resistance roll on a monster: rand(100) ≤ (caster max MP − monster +24) × 5 + 50 (unsigned under the
    /// code option: <see cref="P4Combat.SuccessRoll"/>; Slow <c>0x2C240</c>, Confusion <c>0x2C76C</c>, Weakness
    /// <c>0x2CDAE</c>, Mind Blast <c>0x2D05E</c>).
    /// </summary>
    private bool SpellWorks(P4BattleMember caster, P4BattleMonster mon) =>
        P4Combat.SuccessRoll(_o, _rng, (caster.Member.MpMax - mon.Group.Type.MagicResistance) * 5 + 50);

    private IEnumerable<P4BattleMonster> FieldMonsters() => Monsters.Where(x => x.OnField).ToList();

    private void Cast(P4BattleMember m, P4Order order)
    {
        var spell = order.Spell;
        var cost = P4Combat.MpCost(_o, _t.Combat, spell);
        var me = m.Member;
        if (me.Mp < cost)
            return;
        Say(me.Name + ": " + GameText.S("p4x.0x43eb7") + spell);
        Party.Sounds.Note(P4Sounds.MemberCasts); // _chrexe 0x28EA4: _sdmag after the spell's name
        var level = me.Level;
        switch (spell)
        {
            case >= 1 and <= 4:
            {
                var t = Members[order.Target].Member;
                if (t.Alive)
                {
                    t.Hp = Math.Min(t.HpMax, t.Hp + P4Combat.HealingSpell(_o, _t.Combat, _rng, spell));
                    P4Body.Heal(t.Wounds, spell);
                }
                break;
            }
            case >= 5 and <= 8:
                FaceOff(m.Square, order.Target); // _f2f 0x2BFDE / 0x2BFF6: always, whoever is on the square
                if (MonsterAt(order.Target) is { } ff)
                    MonsterDamage(ff, P4Combat.FireFlash(_o, _t.Combat, _rng, spell - 4, me.Abilities[1]), 2);
                break;
            case 9 or 10:
                foreach (var x in Members)
                    x.SpellMove = Math.Min(99, x.SpellMove + _data.Tables.Speed[spell - 9]);
                break;
            case 11 or 12:
                foreach (var x in FieldMonsters())
                    if (SpellWorks(m, x))
                        x.SpellMove = -(spell - 10);
                break;
            case 13:
                foreach (var x in Members)
                    x.Flying = true;
                break;
            case 14:
                foreach (var x in FieldMonsters().Where(x => x.Group.Flying).OrderBy(x => x.Square))
                {
                    Party.Sounds.Note(P4Sounds.Lands); // _pmagic1 0x2C490: _noise as the flag clears
                    FaceOff(m.Square, x.Square); // _f2f 0x2C4A6: each flier that lands, in square order
                }
                foreach (var g in Groups)
                    g.Flying = false;
                SayId("p4x.0x448f7");
                break;
            case >= 16 and <= 19:
                foreach (var x in Members)
                    x.Armour = Math.Min(255, x.ArmourValue + _data.Tables.Protection[spell - 16]);
                break;
            case >= 20 and <= 23:
                foreach (var x in FieldMonsters().OrderBy(x => x.Square))
                {
                    if (!SpellWorks(m, x))
                        continue;
                    FaceOff(m.Square, x.Square); // _f2f 0x2C786
                    x.Group.SpellAbility *= _t.Combat.WeaknessFactor[spell - 20];
                }
                break;
            case 24 or 25:
            {
                var (hit, dmg) = P4Combat.ArrowFlame(_o, _t.Combat, spell);
                m.Hr = me.Skill(P4Skill.FireBow) + hit;
                m.Wr = Math.Min(255, m.BowValue + dmg);
                break;
            }
            case 26 or 27:
            {
                var (hit, dmg) = P4Combat.ArrowFlame(_o, _t.Combat, spell);
                foreach (var x in Members)
                {
                    x.Wr = Math.Min(255, x.BowValue + dmg);
                    if (hit > 0)
                        x.Hr = x.Member.Skill(P4Skill.FireBow) + hit;
                }
                break;
            }
            case 28:
                m.Hr = 250;
                break;
            case 29:
                foreach (var x in Members)
                    x.Hr = 250;
                break;
            case >= 30 and <= 33:
            {
                var (df, dmgF) = P4Combat.Weakness(_o, _t.Combat, spell - 29);
                foreach (var x in FieldMonsters().OrderBy(x => x.Square))
                {
                    if (!SpellWorks(m, x))
                        continue;
                    FaceOff(m.Square, x.Square); // _f2f 0x2CDCE
                    x.Group.Defence = (int)Math.Truncate(x.Group.Defence * df);
                    x.Group.DamageFactor *= dmgF;
                    x.Strength = 0;
                }
                break;
            }
            case >= 34 and <= 37:
                foreach (var x in FieldMonsters().OrderBy(x => x.Square))
                {
                    if (End != P4BattleEnd.None || !x.OnField)
                        continue;
                    // 0x2CFEC: an immune monster draws nothing; otherwise the damage rand(N) comes before the resistance
                    // rand(100), so a monster that resists still uses up both draws.
                    if (P4Combat.MindBlast(_o, _t.Combat, _rng, spell - 33, x.Group.Type.Experience) is not int d
                        || !SpellWorks(m, x))
                        continue;
                    FaceOff(m.Square, x.Square); // _f2f 0x2D07C, before the damage
                    MonsterDamage(x, d, 0);
                }
                break;
            case 38:
                foreach (var x in FieldMonsters())
                    if (P4Dice.Rand(_rng, 100) >= EncounterLevel * EncounterLevel)
                        x.State = P4BattleMonster.Asleep;
                break;
            case 39:
                if (P4Dice.Rand(_rng, 100) < 3 * level + 20)
                    Finish(P4BattleEnd.Teleported);
                break;
            case 40:
                Resurrect(m, Members[order.Target]);
                break;
            case 41:
                switch (P4Combat.Fear(_o, _rng, level, EncounterLevel))
                {
                    case P4FearOutcome.LevelLowered:
                        EncounterLevel = Math.Max(1, EncounterLevel - 6);
                        SayId("p4x.0x449bf");
                        break;
                    case P4FearOutcome.MonstersFlee:
                        foreach (var x in Monsters)
                            x.Afraid = true;
                        SayId("p4x.0x449bf");
                        break;
                }
                break;
            case 42:
                Summon(order.Target);
                break;
            case 43:
                DispelUndead(level);
                break;
            case 44 or 45:
                (m.Hm, m.Wm, m.Armour, m.SpellMove) = spell == 44 ? (200, 20, 8, _data.Tables.NinjaStride[0])
                    : (255, 25, 10, _data.Tables.NinjaStride[1]);
                break;
            case 46:
                foreach (var x in Members)
                    if (x.Member.Condition == P4Member.Asleep)
                        x.Member.Condition = P4Member.Okay;
                Say(GameText.S("p4x.0x44a55"));
                break;
            case 48:
                Dissolve(m);
                break;
            case >= 51 and <= 54:
                ShockWave(m, order.Target, spell - 50);
                break;
            case >= 55 and <= 58:
                Flameball(m, order, spell - 54);
                break;
            case >= 59 and <= 62:
                foreach (var x in Members)
                    x.StrengthBonus = _data.Tables.Strength[spell - 59];
                SayId("p4x.0x44a7b");
                break;
            case 64 when _o.Is(P4RuleOptions.Annihilate, "divine"):
                Annihilate(Math.Max(cost, me.Mp));
                break;
        }
        me.Mp = spell == 64 ? 0 : Math.Max(0, me.Mp - cost);
        me.MpGrowth = true; // _psetcast: +72, the next inn stay may raise max MP (training.md §4.2)
    }

    /// <summary>
    /// Annihilate Enemy (64) under <c>p4.annihilate</c> = <c>divine</c> (QoL, user s117; the code has no effect, D4-071):
    /// every monster on the field, in square order, takes M × base + rand(M × spread) − 1; no roll, no immunity, no armour,
    /// no wound; the party is never hit.
    /// </summary>
    private void Annihilate(int mp)
    {
        foreach (var x in FieldMonsters().OrderBy(x => x.Square).ToList())
        {
            if (End != P4BattleEnd.None || !x.OnField)
                continue;
            var d = mp * _t.Combat.AnnihilateBase + P4Dice.Rand(_rng, mp * _t.Combat.AnnihilateSpread) - 1;
            MonsterDamage(x, d, 0);
        }
    }

    /// <summary>Resurrection (40): a dead member with Con ≠ 0, rand(100) ≤ (caster L − target L) × 5 + 50, on a free square.</summary>
    private void Resurrect(P4BattleMember caster, P4BattleMember target)
    {
        var t = target.Member;
        if (t.Alive || t.Abilities[3] == 0
            || !P4Combat.SuccessRoll(_o, _rng, (caster.Member.Level - t.Level) * 5 + 50)) // 0x2A882
        {
            SayId("p4x.0x44a23");
            return;
        }
        for (var tries = 0; tries < 10; tries++)
        {
            var sq = P4Dice.Rand(_rng, Squares) - 1;
            if (!_walkable[sq] || Occupied(sq))
                continue;
            t.Condition = P4Member.Okay;
            t.Hp = t.HpMax;
            P4Body.Heal(t.Wounds, 10);
            target.Square = sq;
            target.Gone = false;
            target.Acted = true;
            SayId("p4x.0x449af", t.Name);
            return;
        }
    }

    /// <summary>
    /// Summon Elemental (42, <c>_pmagic2</c> <c>0x2AAB4</c>) <c>[C]</c>: only while the party has no seventh member, the target
    /// square is free walkable ground and an elemental was added at the guild (<c>_par</c>+14 ≤ 2); then the ELMS record
    /// becomes party slot 6 (HP and MP full, Okay, unwounded, <c>_pc</c> 3, <c>_set1</c>) on that square. Nothing is said
    /// either way. It gets its orders from the next options on (<c>_eauto1(6)</c>, automatic in either mode), so it joins
    /// from the next round as the manual says (p. 53) <c>[~]</c> (its action in the cast round is a stale <c>_epatk</c>);
    /// <c>_enc_end</c> removes it after the fight.
    /// </summary>
    private void Summon(int target)
    {
        if (Members.Any(x => x.Elemental) || Party.Elemental is not (>= 0 and <= 2) || target is < 0 or >= Squares
            || !WalkByte(target) || _data.Elementals is not { } list || Party.Elemental.Value >= list.Count)
            return;
        var e = list[Party.Elemental.Value];
        var m = new P4Member
        {
            Name = e.Name, Race = 33, Class = P4Ids.Fighter, Level = e.Level, HpMax = e.HpMax, Hp = e.HpMax,
            MpMax = e.MpMax, Mp = e.MpMax, Abilities = [.. e.Abilities], Strategy = [.. e.Strategy],
        };
        m.Skills[(int)P4Skill.Attack] = e.Attack;
        m.Skills[(int)P4Skill.Parry] = e.Parry;
        foreach (var s in e.KnownSpells)
            m.Spells[s - 1] = 1;
        var bm = new P4BattleMember
        {
            Slot = 6, Member = m, Elemental = true, Square = target, Stride = e.Movement, BaseMove = e.Movement,
            WeaponValue = e.WeaponValue, Wm = Math.Min(255, e.WeaponValue + e.MeleeBonus), Hm = e.Attack, Wr = 0, Hr = 0,
            ShieldValue = e.ShieldValue, ArmourValue = e.ArmourValue, Armour = e.ArmourValue, Acted = true,
        };
        var d = e.Parry / 8 + 3 * Math.Max(1, e.Movement) + e.Abilities[2];
        bm.Defence = (d < 1 ? 0 : d) + e.ShieldValue / 6;
        Members = [.. Members, bm];
        _set.Add(bm.Slot);
    }

    /// <summary>Dispel Undead (43): once a fight, first type undead; x = (L − level) × 5 − rand(100): &gt; −5 melt, &gt; −45 driven off.</summary>
    private void DispelUndead(int level)
    {
        if (_dispelTried || !Groups[0].Type.Undead)
        {
            SayId("p4x.0x44a23");
            return;
        }
        _dispelTried = true;
        var x = (level - EncounterLevel) * 5 - P4Dice.Rand(_rng, 100);
        if (x > -5)
        {
            SayId("p4x.0x449db");
            Finish(P4BattleEnd.Melted);
        }
        else if (x > -45)
        {
            SayId("p4x.0x449fb");
            Finish(P4BattleEnd.DrivenOff);
        }
        else
            SayId("p4x.0x44a23");
    }

    /// <summary>
    /// Dissolve (48): rand(100) ≤ 110 − 10 × encounter level. The code melts every monster at once (103); D4-052 manual: each
    /// monster's HP as damage spread over this round and the next two (user s84), deaths paid as kills.
    /// </summary>
    private void Dissolve(P4BattleMember caster)
    {
        if (P4Dice.Rand(_rng, 100) > 110 - 10 * EncounterLevel)
        {
            SayId("p4x.0x44a23");
            return;
        }
        // _f2f 0x2B12A: on success the caster and each monster, in square order, face each other (the manual's spread
        // over three rounds keeps the monsters on the field; they turn at the cast all the same [~]).
        foreach (var x in FieldMonsters().OrderBy(x => x.Square))
            FaceOff(caster.Square, x.Square);
        SayId("p4x.0x44a5b");
        if (P4Combat.DissolveRounds(_o, _t.Amounts) == 1)
        {
            Finish(P4BattleEnd.Melted);
            return;
        }
        foreach (var x in FieldMonsters())
        {
            x.Dissolving.Clear();
            foreach (var d in P4Combat.DissolveDamageByRound(_o, _t.Amounts, x.Hp))
                x.Dissolving.Enqueue(d);
        }
        DissolveTick();
    }

    private void DissolveTick()
    {
        foreach (var x in Monsters)
        {
            if (End != P4BattleEnd.None)
                return;
            if (x.OnField && x.Dissolving.Count > 0)
                MonsterDamage(x, x.Dissolving.Dequeue(), 0);
        }
    }

    /// <summary>
    /// <c>_shoot(from, to, 20)</c> <c>[C]</c> (s102): the squares the Shock Wave crosses. The field is a 6 × 5 grid of 8-pixel
    /// cells per square; a Bresenham line runs from the caster's cell (column × 6, row × 5) toward the target's, one point
    /// per step of the longer axis (the target's own point not plotted), and each point (8 x + 24, 8 y + 24) is mapped
    /// back to a square (<c>_b_cmouse</c>: x / 48, y / 40). The list keeps repeats, in order (<c>_xwave</c>).
    /// </summary>
    public static IReadOnlyList<int> WavePath(int from, int to)
    {
        var (x, y) = (Col(from) * 6, Row(from) * 5);
        var dx = Col(to) * 6 - x;
        var dy = Row(to) * 5 - y;
        var (sx, sy) = (Math.Sign(dx), Math.Sign(dy));
        var (ax, ay) = (Math.Abs(dx), Math.Abs(dy));
        var path = new List<int>();
        var xMajor = ax > ay;
        var n = xMajor ? ax : ay;
        var err = n;
        for (var i = 0; i < n; i++)
        {
            path.Add((8 * y + 24) / 40 * Size + (8 * x + 24) / 48);
            if (xMajor)
            {
                err -= ay;
                if (err < 0)
                {
                    err += ax;
                    y += sy;
                }
                x += sx;
            }
            else
            {
                err -= ax;
                if (err < 0)
                {
                    err += ay;
                    x += sx;
                }
                y += sy;
            }
        }
        return path;
    }

    /// <summary>
    /// Shock Wave rank r (51–54, <c>_pmagic2</c> <c>0x2B192</c>) <c>[C]</c>: along <see cref="WavePath"/> toward the target square,
    /// every member or monster met takes 7 / 15 / 24 / 35 (type 0: no roll, no wound), friend and foe alike; the caster's
    /// own square and a square just hit (the same square again in a row) are skipped.
    /// </summary>
    private void ShockWave(P4BattleMember caster, int target, int rank)
    {
        var d = (_t.Combat.ShockWave ?? throw new InvalidDataException("rule-tables.json: combat.shock_wave missing"))[rank - 1];
        var last = -1;
        FaceOff(caster.Square, target); // _f2f(caster, target, 20) 0x2B20E, before the path
        foreach (var sq in WavePath(caster.Square, target))
        {
            if (End != P4BattleEnd.None)
                return;
            if (sq == last || sq == caster.Square)
                continue;
            if (MemberAt(sq) is { } p)
            {
                last = sq;
                FaceOff(caster.Square, sq); // _f2f 0x2B2FC, before the damage
                MemberDamage(p, d, 0);
            }
            else if (MonsterAt(sq) is { } x)
            {
                last = sq;
                FaceOff(caster.Square, sq);
                MonsterDamage(x, d, 0);
            }
        }
    }

    /// <summary>
    /// Flameball rank r (D4-047): the code burns its pattern around the target square (<c>_xflam2</c> order) with offsets on
    /// the 7-wide grid, keeping a square only if it is 0–48 and marked in <c>_fmark</c>, which <c>_b_chbtn(target, r)</c>
    /// (<c>0x2B418</c>) sets to the box of Chebyshev radius r around the target just before the loop; so a square that would
    /// wrap to another row (5+ columns away) is dropped, as at the field's edge. The manual burns every square within the
    /// radius of the caster, friend and foe, sparing members in strong armour. rand(N) − armour, at least 1, type 2.
    /// </summary>
    private void Flameball(P4BattleMember m, P4Order order, int rank)
    {
        var code = _o.P4Code(P4RuleOptions.Flameball);
        var centre = code ? order.Target : m.Square;
        var squares = new List<int>();
        foreach (var (dx, dy) in P4Combat.FlameballSquares(_o, _t.Combat, rank))
        {
            if (code)
            {
                var sq = centre + dx + Size * dy;
                if (sq is >= 0 and < Squares && Reach(sq, centre) <= rank) // 0x2B54E–0x2B570: 0–48 and _fmark set
                    squares.Add(sq);
            }
            else
            {
                var (x, y) = (Col(centre) + dx, Row(centre) + dy);
                if (x is >= 0 and < Size && y is >= 0 and < Size)
                    squares.Add(y * Size + x);
            }
        }
        if (code)
            FaceOff(m.Square, order.Target); // _f2f(caster, target, 1) 0x2B406
        foreach (var sq in squares)
        {
            if (End != P4BattleEnd.None)
                return;
            // _f2f(centre, square, 2 / 306) 0x2B48C / 0x2B4A4: each creature in the pattern and whoever is on the centre
            // face each other, before the damage roll (manual option: the centre is the caster [~]).
            if (MonsterAt(sq) is not null || MemberAt(sq) is not null)
                FaceOff(centre, sq);
            if (MonsterAt(sq) is { } x)
            {
                if (P4Combat.FlameballDamage(_o, _t.Combat, _t.Amounts, _rng, rank, x.Group.Armour, 0) is int d)
                    MonsterDamage(x, d, 2);
            }
            else if (MemberAt(sq) is { } p)
            {
                if (P4Combat.FlameballDamage(_o, _t.Combat, _t.Amounts, _rng, rank, p.Armour, p.ArmourValue) is int d)
                    MemberDamage(p, d, 2);
            }
        }
    }

    // --- End of the fight (§11) ----------------------------------------------------------------------------------------

    /// <summary>
    /// <c>_enc_end</c>: the spoils by how it ended — gold for 101–105 (Beg: D4-049), the kills' experience for every end the
    /// party survived (+ the first group's count × its experience on a melt), the pool and gold (<c>_esub01</c>), then the
    /// treasure (<c>_esub02</c>, D4-036) for 101–105.
    /// </summary>
    private void Finish(P4BattleEnd end)
    {
        if (End != P4BattleEnd.None)
            return;
        End = end;
        Prompt = P4BattlePrompt.Over;
        if (end == P4BattleEnd.PartyDead)
            return;
        if (end == P4BattleEnd.Melted)
            Experience += Groups[0].Count * Groups[0].Type.Experience;
        // _monevf(1): only ends 101 and 103 call it; an event fight then sets its story flag [C].
        if (EventFlag is int flag && end is P4BattleEnd.AllKilled or P4BattleEnd.Melted)
            Party.StoryFlags[flag] = 1;
        var won = Outcome == P4FightOutcome.Won;
        long gold = won ? Gold : 0;
        if (end == P4BattleEnd.Begged)
            Beg();
        Party.ExperiencePool = Math.Min(999_999_999, Party.ExperiencePool + Experience / 10);
        var before = Party.Gold;
        Party.Gold = P4Town.Cap(_o, _t.Towns, Party.Gold + gold, P4Town.GoldPath.Loot);
        Party.GoldSinceTown += Math.Max(0, Party.Gold - before);
        if (_o.TreasurySafe) // safe: the gain is banked at once
            Party.BankPurse(TreasuryEntryKind.Loot);
        Say($"{GameText.S("p4x.0x44494")} {Experience} {GameText.S("p4x.0x444a9").Trim()}"
            + (gold > 0 ? $", {gold} {GameText.S("p4x.0x444b4").Trim()}" : "") + GameText.S("p4x.0x444bf"));
        if (won)
            Treasure(end);
        NextTreasure();
    }

    /// <summary>D4-049: gold mod 256 and no items (code), or all gold and one unequipped item per member (manual).</summary>
    private void Beg()
    {
        var (gold, _) = P4Combat.BegForMercy(_o, _t.Amounts, Party.Gold);
        Party.Gold = gold;
        var unequipped = Members.Select(bm => (IReadOnlyList<int>)Enumerable.Range(0, P4Member.CarriedSlots)
            .Where(i => bm.Member.Items[i] != 0 && bm.Member.Items[i] is var id && id != bm.ShieldId && id != bm.ArmourId
                && id != bm.WeaponId && id != bm.BowId && id != bm.ThrownId).ToList()).ToList();
        var taken = P4Combat.BegItemsTaken(_o, _t.Amounts, _rng, unequipped);
        for (var i = 0; i < taken.Count; i++)
            foreach (var slot in taken[i])
                Members[i].Member.Items[slot] = 0;
    }

    /// <summary>
    /// <c>_esub02</c>: the treasure roll (D4-036: the code's flat chance, or the manual's automatic / Spot Item rule), then
    /// the last group's three treasure ids (outdoors only) and one item of the extra table, index rand(2 × level + 6)
    /// (outdoors always, in a location map when rand(100) is below 65; an index past the table gives none).
    /// </summary>
    private void Treasure(P4BattleEnd end)
    {
        var spot = Members.Where(m => m.Member.Alive).Select(m => m.Member.Skill(P4Skill.SpotItem)).ToList();
        if (!P4Combat.TreasureFound(_o, _t.Combat, _rng, Outdoors, end == P4BattleEnd.AllKilled,
                end == P4BattleEnd.Surrendered, EncounterLevel, spot))
            return;
        // D4-036 manual: a finder with a high Spot Item also sees what the items are. The remaster shows the game's own
        // appraisal text (_ident_mi) under each found item when the best living Spot Item reaches the amount [~].
        _appraiseFound = P4Combat.SpotIdentifies(_o, _t.Amounts, spot.DefaultIfEmpty(0).Max());
        _foundItems.AddRange(_treasureIds);
        var bt = _data.Tables;
        if (Outdoors || P4Dice.Rand(_rng, 100) < bt.TreasureDungeonExtraBelow)
        {
            var i = P4Dice.Rand(_rng, 2 * EncounterLevel + 6);
            if (i < bt.TreasureExtraItems.Count && bt.TreasureExtraItems[i] != 0)
                _foundItems.Add(bt.TreasureExtraItems[i]);
        }
    }

    private void NextTreasure()
    {
        // _findit (s140, static [C]): with no free pack slot the item is passed over without the question or a message.
        if (Array.IndexOf(Party.Pack, 0) < 0)
            _foundItems.Clear();
        if (_foundItems.Count > 0)
        {
            Prompt = P4BattlePrompt.Treasure;
            _treasureLogStart = _log.Count;
            Say(P4MessageWindow.Format("p4x.0x4444a", _itemName(_foundItems[0])) + GameText.S("p4x.0x4445b"));
            if (_appraiseFound && _t.Services is { } sv && _foundItems[0] < sv.IdentifyLines.Count)
            {
                // s156: the appraisal is _ident_mi's text, and _ident_mi sounds _sdmag on entry (0x2DBBC, unconditional),
                // so its sound comes with it. P4.X itself calls _ident_mi only from Identify: the moment is the
                // remaster's (D4-036) [~].
                Party.Sounds.Note(P4Sounds.Identify);
                Say(string.Concat(sv.IdentifyLines[_foundItems[0]].Select(GameText.S)));
            }
            return;
        }
        Prompt = P4BattlePrompt.Over;
    }

    private Func<int, string> _itemName = id => $"#{id}";

    private int _treasureLogStart;

    /// <summary>
    /// The lines said at the found-item question now up (s141): the item and "pick it up?", then the D4-036 appraisal;
    /// <c>_findit</c> prints them into <c>_msg</c>'s window over the field. Empty at any other prompt.
    /// </summary>
    public IReadOnlyList<string> TreasureLines =>
        Prompt == P4BattlePrompt.Treasure ? [.. _log.Skip(_treasureLogStart)] : [];

    /// <summary>D4-036 manual: the found items come with their appraisal text (<see cref="Treasure"/>).</summary>
    private bool _appraiseFound;

    /// <summary>Item names for the found-item line (the campaign's <c>itemName</c>).</summary>
    public Func<int, string> ItemName
    {
        get => _itemName;
        set => _itemName = value ?? throw new ArgumentNullException(nameof(value));
    }
}
