namespace OpenPhantasie.Core;

/// <summary>Party weapon actions (<c>_epatk</c>, <c>combat.md</c> §2).</summary>
public enum P4WeaponAction { AimBlow, Lunge, Attack, Thrust, Slash }

/// <summary>What a Fear spell does (D4-052).</summary>
public enum P4FearOutcome { NoEffect, LevelLowered, MonstersFlee }

/// <summary>
/// Where Teleportation takes the party (D4-052 / D4-070, <c>p4.teleport</c>): the previous square by default;
/// <see cref="Stay"/> is the X68000 code (back to the previous square outdoors, the fight's square in a location map).
/// </summary>
public enum P4TeleportDestination { PreviousSquare, RandomSquare, Entrance, Stay }

/// <summary>
/// Phantasie IV combat rules (<c>reverse-engineering/x68000/phantasie4/logic-specs/combat.md</c>), with D4-036, 037, 042,
/// 044–052 and 055 as <c>p4.*</c> options. The manual's side of a rule reads its amounts from <see cref="P4ManualAmounts"/>
/// (user s84) and throws <see cref="P4AmountUnknownException"/> for one that is still <c>[?]</c>.
/// </summary>
public static class P4Combat
{
    /// <summary>D4-044: a spell's MP cost (spell number 1–65) — the manual's, or the code's table (<c>0x44B7C</c>).</summary>
    public static int MpCost(GameOptions o, P4CombatTables t, int spell) =>
        (o.P4Code(P4RuleOptions.MpCosts) ? t.MpCostCode : t.MpCostManual)[spell - 1];

    /// <summary>
    /// D4-055 (manual silent; user s82): one monster's HP from its base b. MSX2: trunc(b (rand(10) + 4) / 10 + 0.5) = 0.5 …
    /// 1.4 b. X68000: roll 1 → trunc(0.5 b + 0.5), 2 → trunc(1.5 b + 0.5), 3–10 → trunc(1.8 b). Both capped at trunc(1.8 b).
    /// </summary>
    public static int MonsterHp(GameOptions o, P4CombatTables t, IRandomSource rng, int hpBase)
    {
        var roll = P4Dice.Rand(rng, 10);
        double hp = o.P4X68000MonsterHp()
            ? Math.Truncate(hpBase * ((roll - 1) + 5.0 / 10.0) + 0.5)
            : Math.Truncate(hpBase * (roll + 4) / 10.0 + 0.5);
        return (int)Math.Min(hp, Math.Truncate(t.MonsterHpCap * hpBase));
    }

    /// <summary>Slash's n = trunc(Hm / 75) + 1 (3 or 4 once Slash is offered from Hm 150).</summary>
    public static int SlashCount(int hm) => hm / 75 + 1;

    /// <summary>
    /// D4-042: strikes one weapon action makes. Manual (p. 43): Attack 2, Slash n (3–4); each strike is its own hit roll and
    /// damage roll (<see cref="MeleeHitChance"/>, <see cref="MeleeDamage"/>), with the user's s84 per-strike numbers. Code:
    /// one roll per action.
    /// </summary>
    public static int Strikes(GameOptions o, P4CombatTables t, P4WeaponAction action, int hm) =>
        o.P4Code(P4RuleOptions.Strikes) ? 1
        : action switch
        {
            P4WeaponAction.Attack => t.ManualAttackStrikes,
            P4WeaponAction.Slash => SlashCount(hm),
            _ => 1,
        };

    /// <summary>
    /// Melee hit chance (§3.1): Hm (Slash: Hm / n) − the monster's defence (if ≥ 0); Aim Blow − 15, Lunge − 30 (min 0); 200
    /// against a helpless target; × 3/4 when the attacker is Down, ÷ 2 off-hand. Hit if rand(100) ≤ chance. D4-042 manual
    /// (user s84): each of Attack's strikes rolls Hm − 10 and each of Slash's Hm − 20 (not divided by n).
    /// </summary>
    public static int MeleeHitChance(GameOptions o, P4CombatTables t, P4WeaponAction action, int hm, int monsterDefence,
        bool targetHelpless = false, bool attackerDown = false, bool offHand = false)
    {
        var manual = !o.P4Code(P4RuleOptions.Strikes);
        var c = action switch
        {
            P4WeaponAction.Attack when manual => hm - t.ManualAttackHitPenalty,
            P4WeaponAction.Slash when manual => hm - t.ManualSlashHitPenalty,
            P4WeaponAction.Slash => hm / SlashCount(hm),
            _ => hm,
        };
        if (monsterDefence >= 0)
            c -= monsterDefence;
        if (action == P4WeaponAction.AimBlow)
            c = Math.Max(0, c - t.AimPenalty);
        else if (action == P4WeaponAction.Lunge)
            c = Math.Max(0, c - t.LungePenalty);
        if (targetHelpless)
            c = 200;
        return attackerDown ? 3 * c / 4 : offHand ? c / 2 : c;
    }

    /// <summary>
    /// D4-037: the missile skill a throw uses — Throwing (record +50) under the manual, Fire Bow (+48) in the code; bows always
    /// use Fire Bow.
    /// </summary>
    public static int MissileSkill(GameOptions o, int fireBow, int throwing, bool isThrow) =>
        isThrow && !o.P4Code(P4RuleOptions.Throwing) ? throwing : fireBow;

    /// <summary>Bow / thrown hit chance (§3.1): skill − trunc(defence / 4) when the defence is ≥ 0; 200 on a helpless target.</summary>
    public static int MissileHitChance(int skill, int monsterDefence, bool targetHelpless = false) =>
        targetHelpless ? 200 : monsterDefence >= 0 ? skill - monsterDefence / 4 : skill;

    /// <summary>
    /// Melee damage (§3.2): trunc(u^0.5 × Wm − armour) + F (Thrust +1, Attack −1, Slash −n, Aim / Lunge 0), ≤ 0 → 0; then
    /// + the Strength bonus, cap 255. Every hit later does at least 1 (<c>_mdamage</c>). D4-042 manual (user s84/s86): each
    /// Attack strike that lands does full damage (F = 0); Slash does 1–2 less than Attack (p. 43): F = −(n − 2).
    /// </summary>
    public static int MeleeDamage(GameOptions o, IRandomSource rng, P4WeaponAction action, int wm, int armour, int hm,
        int strengthBonus = 0)
    {
        var u = Math.Pow(P4Dice.Frand(rng), 0.5);
        var manual = !o.P4Code(P4RuleOptions.Strikes);
        var f = action switch
        {
            P4WeaponAction.Thrust => 1,
            P4WeaponAction.Attack when !manual => -1,
            P4WeaponAction.Slash => manual ? -(SlashCount(hm) - 2) : -SlashCount(hm),
            _ => 0,
        };
        var d = (int)Math.Truncate(u * wm - armour) + f;
        return Math.Min(255, Math.Max(0, d) + strengthBonus);
    }

    /// <summary>
    /// Bow / thrown damage (§3.2): trunc(u^0.2 × Wr × 1.2 − armour / 2) + K[class]; &lt; 0 → 0 and 0 → 1 (original quirk).
    /// </summary>
    public static int MissileDamage(IRandomSource rng, int wr, int armour, int classBonus, int strengthBonus = 0)
    {
        var u = Math.Pow(P4Dice.Frand(rng), 0.2);
        var d = (int)Math.Truncate(u * wr * 1.2 - armour / 2.0) + classBonus;
        if (d < 0)
            d = 0;
        else if (d == 0)
            d = 1;
        return Math.Min(255, d + strengthBonus);
    }

    /// <summary>
    /// Arrow Flame (spells 24 / 25 one member, 26 / 27 the party): hit bonus and damage bonus. The single forms add 50 / 100
    /// to the hit chance and 15 / 25 damage; the party forms add the same under the manual (D4-045), damage only in the code.
    /// </summary>
    public static (int HitBonus, int DamageBonus) ArrowFlame(GameOptions o, P4CombatTables t, int spell)
    {
        var rank = spell is 24 or 26 ? 0 : spell is 25 or 27 ? 1 : throw new ArgumentOutOfRangeException(nameof(spell));
        var party = spell >= 26;
        var hit = party && o.P4Code(P4RuleOptions.PartyArrowFlame) ? 0 : t.ArrowFlameHit[rank];
        return (hit, t.ArrowFlameDamage[rank]);
    }

    /// <summary>
    /// D4-050: Weakness rank r (1–4) on a monster type: factor 0.8 / 0.6 / 0.4 / 0.2 applied to its damage value (+18) under
    /// the manual (the code's own per-rank factors carried over to the damage <c>[~]</c>) or to its defence against party hits
    /// (+1C) in the code. Either way its Strength bonus is cancelled.
    /// </summary>
    public static (double DefenceFactor, double DamageFactor) Weakness(GameOptions o, P4CombatTables t, int rank)
    {
        var f = t.WeaknessFactor[rank - 1];
        return o.P4Code(P4RuleOptions.Weakness) ? (f, 1.0) : (1.0, f);
    }

    /// <summary>D4-051: Fire Flash rank r damage — 1…N under the manual; trunc(rand(N) × Int / 15) in the code (N = 10 / 30 / 60 / 100).</summary>
    public static int FireFlash(GameOptions o, P4CombatTables t, IRandomSource rng, int rank, int intelligence)
    {
        var n = t.FireFlashN[rank - 1];
        var r = P4Dice.Rand(rng, n);
        return o.P4Code(P4RuleOptions.SpellDamage) ? r * intelligence / 15 : r;
    }

    /// <summary>
    /// D4-051: Mind Blast rank r on one target — 1…N under the manual; in the code rand(N) − 1 (floored to 1 when applied)
    /// and no effect (null) on a target whose experience is a multiple of 7 below 2000.
    /// </summary>
    public static int? MindBlast(GameOptions o, P4CombatTables t, IRandomSource rng, int rank, long targetExperience)
    {
        var n = t.MindBlastN[rank - 1];
        if (!o.P4Code(P4RuleOptions.SpellDamage))
            return P4Dice.Rand(rng, n);
        if (targetExperience % 7 == 0 && targetExperience < 2000)
            return null;
        return Math.Max(1, P4Dice.Rand(rng, n) - 1);
    }

    /// <summary>D4-051: HP a Healing spell rank r restores — 1…9 / 27 / 57 / 99 under the manual; rand(6r² + 4) − 1 (0…) in the code.</summary>
    public static int HealingSpell(GameOptions o, P4CombatTables t, IRandomSource rng, int rank) =>
        o.P4Code(P4RuleOptions.SpellDamage)
            ? P4Dice.Rand(rng, 6 * rank * rank + 4) - 1
            : P4Dice.Rand(rng, t.HealingManualN[rank - 1]);

    /// <summary>
    /// D4-047: the squares a Flameball rank r hits, as offsets. Manual (p. 52): every square within the radius 1 / 2 / 2.5 / 3
    /// of the caster (Euclidean, the caster's own square excluded), friend and foe. Code: around the target square, the first
    /// 5 / 13 / 21 / 21 entries of <c>_xflam2</c> (<c>0x44954</c>, data <c>combat.flameball_code_offsets</c>) in table order —
    /// a + shape, a radius-2 diamond, a 5 × 5 without corners (ranks 3 and 4). The code adds them on the 7-wide grid; the
    /// caller drops what falls off the field (<c>_fmark</c>, <c>P4Battle.Flameball</c>).
    /// </summary>
    public static IReadOnlyList<(int Dx, int Dy)> FlameballSquares(GameOptions o, P4CombatTables t, int rank)
    {
        var list = new List<(int, int)>();
        if (o.P4Code(P4RuleOptions.Flameball))
        {
            var counts = t.FlameballCodeSquares
                ?? throw new InvalidDataException("rule-tables.json: combat.flameball_code_squares missing");
            var offsets = t.FlameballCodeOffsets
                ?? throw new InvalidDataException("rule-tables.json: combat.flameball_code_offsets missing");
            foreach (var off in offsets.Take(counts[rank - 1]))
            {
                var dy = (int)Math.Round(off / 7.0); // |dx| <= 2, so the nearest row is the offset's row
                list.Add((off - 7 * dy, dy));
            }
            return list;
        }
        var r = t.FlameballRadiusManual[rank - 1];
        var span = (int)Math.Floor(r);
        for (var dy = -span; dy <= span; dy++)
        for (var dx = -span; dx <= span; dx++)
        {
            if ((dx, dy) != (0, 0) && dx * dx + dy * dy <= r * r)
                list.Add((dx, dy));
        }
        return list;
    }

    /// <summary>
    /// Flameball damage to one creature: rand(N) − armour, at least 1 (N = 18 / 25 / 35 / 43). Under the manual a creature in
    /// strong armour takes nothing (null): body armour of 10 points or more (user s84: Plate Mail, Elvin Chain Mail, Plate
    /// Mail +1, Mythral Mail). <paramref name="bodyArmour"/> = the worn body armour's points (0 for a monster).
    /// </summary>
    public static int? FlameballDamage(GameOptions o, P4CombatTables t, P4ManualAmounts amounts, IRandomSource rng, int rank,
        int armour, int bodyArmour)
    {
        if (!o.P4Code(P4RuleOptions.Flameball)
            && bodyArmour >= P4Amount.Require(amounts.FlameballStrongArmourAt, "D4-047 strong armour"))
            return null;
        return Math.Max(1, P4Dice.Rand(rng, t.FlameballN[rank - 1]) - armour);
    }

    /// <summary>D4-052: the stride a Slow rank r leaves an enemy — ½ / ¾ under the manual (as printed), ½ / ¼ in the code.</summary>
    public static double SlowStrideFactor(GameOptions o, int rank) =>
        rank == 1 ? 0.5 : o.P4Code(P4RuleOptions.SpellWording) ? 0.25 : 0.75;

    /// <summary>
    /// D4-052: Dissolve melts the monsters at once (code; success when rand(100) ≤ 110 − 10 × encounter level) or slowly
    /// (manual): over the cast round and the next two (user s84).
    /// </summary>
    public static int DissolveRounds(GameOptions o, P4ManualAmounts amounts) =>
        o.P4Code(P4RuleOptions.SpellWording) ? 1 : P4Amount.Require(amounts.DissolveRounds, "D4-052 Dissolve rounds");

    /// <summary>
    /// D4-052: Dissolve's <paramref name="total"/> damage per round, starting with the cast round: the same total spread
    /// evenly over <see cref="DissolveRounds"/> rounds, any remainder in the earliest rounds (user s84).
    /// </summary>
    public static IReadOnlyList<int> DissolveDamageByRound(GameOptions o, P4ManualAmounts amounts, int total)
    {
        var rounds = DissolveRounds(o, amounts);
        return Enumerable.Range(0, rounds).Select(i => total / rounds + (i < total % rounds ? 1 : 0)).ToArray();
    }

    /// <summary>
    /// The "× 5 + 50" success rolls (resistance R, Fear, Resurrection; <c>combat.md</c> §9): rand(100) ≤ limit. The code
    /// compares unsigned (<c>cmp.l limit, rand; bhi fail</c> at <c>0x29D44</c>, <c>0x2A282</c>, <c>0x2A882</c>,
    /// <c>0x2AA4C</c>, <c>0x2C240</c>, <c>0x2C76C</c>, <c>0x2CDAE</c>, <c>0x2D05E</c>), so under the code option
    /// (D4-051, <c>p4.spell-damage</c>) a limit below 0 always works; under the manual it never does (signed). The draw is
    /// made either way.
    /// </summary>
    public static bool SuccessRoll(GameOptions o, IRandomSource rng, int limit)
    {
        var r = P4Dice.Rand(rng, 100);
        return o.P4Code(P4RuleOptions.SpellDamage) ? (uint)r <= (uint)limit : r <= limit;
    }

    /// <summary>
    /// D4-052: Fear succeeds when rand(100) ≤ (caster level − encounter level) × 5 + 50 (the code's roll). Then the code
    /// lowers the encounter level by 6 (min 1); under the manual the monsters flee (leave the field) <c>[~]</c>.
    /// </summary>
    public static P4FearOutcome Fear(GameOptions o, IRandomSource rng, int casterLevel, int encounterLevel)
    {
        if (!SuccessRoll(o, rng, (casterLevel - encounterLevel) * 5 + 50)) // 0x2AA4C
            return P4FearOutcome.NoEffect;
        return o.P4Code(P4RuleOptions.SpellWording) ? P4FearOutcome.LevelLowered : P4FearOutcome.MonstersFlee;
    }

    /// <summary>
    /// D4-052: where Teleportation (success rand(100) &lt; 3 × level + 20) takes the party (<c>p4.teleport</c>, user s84):
    /// the previous square by default, a random open square of the same level / outdoor area, the dungeon's entrance, or
    /// (D4-070, user s103) the code's <c>stay</c>: previous square outdoors, the fight's square in a location map.
    /// </summary>
    public static P4TeleportDestination Teleport(GameOptions o) =>
        o.Is(P4RuleOptions.Teleport, "random") ? P4TeleportDestination.RandomSquare
        : o.Is(P4RuleOptions.Teleport, "entrance") ? P4TeleportDestination.Entrance
        : o.Is(P4RuleOptions.Teleport, "stay") ? P4TeleportDestination.Stay
        : P4TeleportDestination.PreviousSquare;

    /// <summary>
    /// D4-046: whether auto combat may cast <paramref name="spell"/>. Manual (p. 32): attack 5–8, 11–12, 14, 20, 38, 43, 48,
    /// 59–62 and defence 1–4, 9–10, 13, 16–19, 40. Code: 1–14, 16–23, 26, 27, 29, 34–37, 40, 43, 46, 59–62.
    /// </summary>
    public static bool AutoMayCast(GameOptions o, P4CombatTables t, int spell)
    {
        if (!o.P4Code(P4RuleOptions.AutoSpells))
            return t.AutoAttackManual.Contains(spell) || t.AutoDefenceManual.Contains(spell);
        return spell is >= 1 and <= 14 or >= 16 and <= 23 or 26 or 27 or 29 or >= 34 and <= 37 or 40 or 43 or 46
            or >= 59 and <= 62;
    }

    /// <summary>
    /// D4-046: the offensive spells auto combat walks (each tried with chance (785 − 30 i) / 10,000, <c>0x44162</c>). Manual:
    /// the code's walk kept for the spells the manual lists, then the manual's attack spells the walk lacks in number order
    /// <c>[~]</c> (order not given); code: the walk as is.
    /// </summary>
    public static IReadOnlyList<int> AutoAttackWalk(GameOptions o, P4CombatTables t)
    {
        if (o.P4Code(P4RuleOptions.AutoSpells))
            return t.AutoAttackWalkCode;
        var kept = t.AutoAttackWalkCode.Where(t.AutoAttackManual.Contains).ToList();
        return kept.Concat(t.AutoAttackManual.Where(s => !kept.Contains(s)).Order()).ToList();
    }

    /// <summary>
    /// D4-049: Beg for mercy accepted — the code leaves gold mod 256 and takes no items; the manual hands over all the
    /// party's gold (0 left) and goods: one unequipped item from each member (user s84).
    /// </summary>
    public static (long Gold, int ItemsPerMember) BegForMercy(GameOptions o, P4ManualAmounts amounts, long partyGold) =>
        o.P4Code(P4RuleOptions.BegMercy)
            ? (partyGold % 256, 0)
            : (0, P4Amount.Require(amounts.BegItemsPerMember, "D4-049 items per member"));

    /// <summary>
    /// D4-049: which unequipped items the monsters take, per member: <paramref name="unequipped"/>[m] lists member m's
    /// unequipped items (pack positions); the result holds up to <see cref="BegForMercy"/>'s count of them, picked at random
    /// (rand per pick; a member with none gives none). Empty lists under the code.
    /// </summary>
    public static IReadOnlyList<IReadOnlyList<int>> BegItemsTaken(GameOptions o, P4ManualAmounts amounts, IRandomSource rng,
        IReadOnlyList<IReadOnlyList<int>> unequipped)
    {
        var (_, per) = BegForMercy(o, amounts, 0);
        return unequipped.Select(items =>
        {
            var left = items.ToList();
            var taken = new List<int>();
            while (taken.Count < per && left.Count > 0)
            {
                var i = P4Dice.Rand(rng, left.Count) - 1;
                taken.Add(left[i]);
                left.RemoveAt(i);
            }
            return (IReadOnlyList<int>)taken;
        }).ToList();
    }

    /// <summary>
    /// D4-048: at a night attack, a watch whose Listen roll succeeds wakes the whole party under the manual; in the code it only
    /// cancels the monsters' free first round (the sleepers stay asleep). The roll: rand(100) &lt; Listen.
    /// </summary>
    public static bool WatchWakesParty(GameOptions o, bool listenSucceeded) =>
        listenSucceeded && !o.P4Code(P4RuleOptions.NightWatch);

    /// <summary>
    /// D4-036: whether the spoils roll finds the encounter's treasure after a won fight. Code: rand(100) ≤ 45 in a dungeon,
    /// ≤ encounter level + 18 outdoors, no skill. Manual (pp. 21, 44): automatic outdoors when the enemy is wiped out and in a
    /// dungeon when it surrenders; otherwise a member must spot it: the party-wide Spot Item roll (rand(100) ≤ skill for each
    /// living member, the game's skill roll, <c>skills.md</c> §4) <c>[~]</c>.
    /// </summary>
    public static bool TreasureFound(GameOptions o, P4CombatTables t, IRandomSource rng, bool outdoors, bool wipedOut,
        bool surrendered, int encounterLevel, IReadOnlyList<int> livingSpotItem)
    {
        if (o.P4Code(P4RuleOptions.SpotItem))
            return P4Dice.Rand(rng, 100) <= (outdoors ? encounterLevel + t.TreasureOutdoorLevelAdd : t.TreasureDungeonChance);
        if ((outdoors && wipedOut) || (!outdoors && surrendered))
            return true;
        var found = false;
        foreach (var s in livingSpotItem)
            found |= P4Dice.Rand(rng, 100) <= s;
        return found;
    }

    /// <summary>
    /// D4-036: under the manual a finder with Spot Item 50 or more also identifies the found items (user s84). The code
    /// never identifies found items.
    /// </summary>
    public static bool SpotIdentifies(GameOptions o, P4ManualAmounts amounts, int bestSpotItem) =>
        !o.P4Code(P4RuleOptions.SpotItem)
        && bestSpotItem >= P4Amount.Require(amounts.SpotItemIdentifyAt, "D4-036 Spot Item identifies");
}
