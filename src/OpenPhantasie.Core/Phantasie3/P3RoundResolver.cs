namespace OpenPhantasie.Core;

/// <summary>
/// Phantasie III's combat round, <c>combat_round(mode)</c> (root <c>0xd8c7</c>, <c>combat-round.md</c> §2–5), in place of
/// Phantasie I's three-pass scheduler: 17 actor slots (party 0–6, rank-1 members 7–11, rank-2 members 12–16), bowmen
/// first, the rest mixed by <c>17 − k</c> random swaps; each slot acts once. Sleepers roll the shared wake chance on their
/// own turn; monsters cast a special by cost or melee (root <c>0xd069</c>); the round ends with the wipe, final-fight,
/// give-up and spell checks, the wake chance step, the R1 / R2 swap and compaction. Actions are not cleared: they carry
/// over to the next round, as in DOS. A fight without III's state (<see cref="P3Fight"/>) goes to Phantasie I's resolver.
/// </summary>
public sealed class P3RoundResolver : ICombatResolver
{
    private const int PartySlots = 7;
    private const int GroupSlots = 5;
    private const int ActorSlots = PartySlots + 2 * GroupSlots;

    /// <summary>The front group's chance to give way to a stronger rear group at the end of a round (root <c>0x113e6</c>).</summary>
    public const int RankSwapChance = 30;

    private readonly ICombatResolver _other = new CombatResolver();

    public CombatResult ResolveRound(CombatEncounter encounter, IRandomSource rng)
    {
        ArgumentNullException.ThrowIfNull(encounter);
        ArgumentNullException.ThrowIfNull(rng);
        if (P3Fight.Of(encounter) is not { } fight)
            return _other.ResolveRound(encounter, rng);
        encounter.EnsurePrep();
        var log = new List<string>();
        if (encounter.SpellEnd != SpellCombatEnd.None)
            return CombatSpells.EndBySpell(encounter, log);

        // A monster ambush opens with no menu (OpensWithoutOrders): combat_round(1) before the first party menu.
        var mode = fight.NextRound;
        if (encounter.OpensWithoutOrders)
        {
            mode = P3RoundMode.MonstersOnly;
            encounter.OpensWithoutOrders = false;
        }
        fight.NextRound = P3RoundMode.Normal;
        encounter.Surprise = encounter.Surprise with { FirstRound = false };

        fight.ClearParries();
        if (mode == P3RoundMode.Normal && !encounter.Rules.P3DosParry())
            P3CombatModule.RollParries(encounter, fight, rng, log);

        var partySize = Math.Min(PartySlots, encounter.Party.Members.Count); // an elemental summoned now acts next round
        foreach (var actor in TurnOrder(mode, partySize, i => IsArcher(encounter, i), rng))
        {
            if (actor < PartySlots)
                PartyTurn(encounter, fight, actor, rng, log);
            else
                MonsterTurn(encounter, fight, actor, rng, log);
        }
        return EndOfRound(encounter, fight, rng, log);
    }

    /// <summary>
    /// The 17 slots in acting order (§2); −1 entries (empty slots) are left out. <paramref name="isArcher"/> says whether
    /// party slot i fires a bow this round (Fire Bow at either rank): those go first, in party order, and are never mixed.
    /// </summary>
    public static IReadOnlyList<int> TurnOrder(P3RoundMode mode, int partySize, Func<int, bool> isArcher, IRandomSource rng)
    {
        ArgumentNullException.ThrowIfNull(isArcher);
        var order = new int[ActorSlots];
        Array.Fill(order, -1);
        if (mode != P3RoundMode.PartyOnly)
            for (var a = PartySlots; a < ActorSlots; a++)
                order[a] = a;
        int k;
        if (mode != P3RoundMode.MonstersOnly)
        {
            k = 0;
            var back = partySize - 1;
            for (var i = 0; i < partySize; i++)
            {
                if (isArcher(i))
                    order[k++] = i;
                else
                    order[back--] = i;
            }
        }
        else
            k = PartySlots;
        var span = ActorSlots - k;
        for (var n = 0; n < span; n++)
        {
            var a = P3Dice.Rnd(rng, span) + k - 1;
            var b = P3Dice.Rnd(rng, span) + k - 1;
            (order[a], order[b]) = (order[b], order[a]);
        }
        return order.Where(a => a >= 0).ToArray();
    }

    private static bool IsArcher(CombatEncounter encounter, int i) =>
        i < encounter.Prep.Length && P3CombatModule.TryUnpack(encounter.Prep[i].PackedAction, out var c)
                                  && c.Action == P3Action.FireBow;

    // ---- Turns ---------------------------------------------------------------------------------------------------------

    /// <summary>
    /// A party slot's turn (root <c>0xd9d7</c>): a sleeper rolls to wake (the turn is used either way); Dead and Out do
    /// nothing; Okay and Down carry out their action through the fight's modules (III: <see cref="P3CombatModule"/>).
    /// </summary>
    private static void PartyTurn(CombatEncounter encounter, P3Fight fight, int i, IRandomSource rng, List<string> log)
    {
        var members = encounter.Party.Members;
        if (i >= members.Count)
            return;
        var member = members[i];
        encounter.TagLine(log, member, null);
        switch (P3Fight.StatusOf(member))
        {
            case P3Status.Asleep:
                if (P3Dice.Rnd(rng, 100) <= fight.WakeChance)
                    Awaken(member, log);
                return;
            case P3Status.Okay or P3Status.Down:
                encounter.Modules.ResolvePartyAction(encounter, i, encounter.Prep[i].PackedAction, rng, log);
                return;
        }
    }

    /// <summary>"&lt;name&gt; awakens": the status follows the body again (root <c>0x107b</c>).</summary>
    private static void Awaken(Character member, List<string> log)
    {
        member.Status = CharacterStatus.Ok;
        Phantasie3Campaign.ApplyBodyStatus(member, P3BodyAreas.Of(member));
        log.Add(GameText.F("core.combat.x_awakens", member.Name));
    }

    /// <summary>
    /// A monster slot's turn (root <c>0xd069</c>): a sleeper rolls to wake; dead and Unconscious do nothing; else
    /// <c>rnd(100) ≤ special chance</c> picks one of its three specials, cast when its magic covers the cost, otherwise a
    /// melee. Rank 2 acts exactly like rank 1.
    /// </summary>
    private static void MonsterTurn(CombatEncounter encounter, P3Fight fight, int actor, IRandomSource rng, List<string> log)
    {
        var rank = actor < PartySlots + GroupSlots ? 1 : 2;
        var m = actor - PartySlots - (rank - 1) * GroupSlots;
        if (Group(encounter, rank) is not { } group || m >= group.Members.Count)
            return;
        var monster = group.Members[m];
        encounter.TagLine(log, null, monster);
        switch (fight.StatusOf(monster))
        {
            case P3Status.Asleep:
                if (P3Dice.Rnd(rng, 100) <= fight.WakeChance)
                {
                    monster.StatusFlags &= ~MonsterStatusFlags.Asleep;
                    fight.RefreshStatus(monster); // status from the body in its slot
                    log.Add(GameText.F("core.combat.x_awakens", monster.Name));
                }
                return;
            case P3Status.Dead or P3Status.Out:
                return;
        }
        if (monster.Specials.Count == 3 && P3Dice.Rnd(rng, 100) <= monster.SpecialChance)
        {
            var special = monster.Specials[P3Dice.Rnd(rng, 3) - 1];
            var cost = SpecialCost(special);
            if (monster.CombatRating >= cost)
            {
                CastSpecial(encounter, fight, group, monster, special, rng, log);
                monster.CombatRating -= cost;
                return;
            }
        }
        Melee(encounter, group, monster, MonsterMeleeRider.None, rng, log);
    }

    /// <summary>
    /// A special's magic cost: <c>code % 4</c>, 2 from code 33 on. So every 4th code (4, 8 … 32) is free, and an empty slot
    /// (0) passes and then does nothing (§3 cost quirk).
    /// </summary>
    public static int SpecialCost(int special) => special >= 33 ? 2 : special % 4;

    /// <summary>
    /// <c>monster_spell</c> (root <c>0xcad7</c>, table <c>DS:0x9c4</c>): codes 1–4 Fireflash, 5–16 Quickness / Strength /
    /// Protection on both groups, 17–24 the party's armour or attack lowered, 25–32 Mindblast / Fireball, 33 Sleep,
    /// 34 / 35 a stunning / draining melee, 36 / 37 breath. Any other code (0) does nothing.
    /// </summary>
    private static void CastSpecial(
        CombatEncounter encounter, P3Fight fight, MonsterRank group, Monster caster, int special, IRandomSource rng,
        List<string> log)
    {
        switch (special)
        {
            case >= 1 and <= 4:
                encounter.TagLine(log, null, caster, sound: P3Fight.CastSound);
                log.Add(GameText.F("core.p3.x_casts_fireflash", caster.Name));
                if (P3CombatModule.PickMember(encounter.Party, rng) is { } t)
                    SpellHit(encounter, encounter.Party.Members[t], P3Dice.Rnd(rng, SpecialTable[special]), rng, log);
                return;
            case >= 5 and <= 8:
                encounter.TagLine(log, null, caster, sound: P3Fight.CastSound);
                log.Add(GameText.F("core.p3.x_casts_quickness", caster.Name));
                foreach (var m in BothGroups(encounter))
                    m.AttackPower = 10 * SpecialTable[special];
                return;
            case >= 9 and <= 12:
                encounter.TagLine(log, null, caster, sound: P3Fight.CastSound);
                log.Add(GameText.F("core.p3.x_casts_strength", caster.Name));
                foreach (var m in BothGroups(encounter))
                    m.DamageField = SpecialTable[special];
                return;
            case >= 13 and <= 16:
                encounter.TagLine(log, null, caster, sound: P3Fight.CastSound);
                log.Add(GameText.F("core.p3.x_casts_protection", caster.Name));
                foreach (var m in BothGroups(encounter))
                    m.Armor = SpecialTable[special];
                return;
            case >= 17 and <= 20:
                Weaken(encounter, caster, SpecialTable[special], fight.ArmourOf, fight.SetArmour, rng, log);
                encounter.TagLine(log, null, caster, sound: P3Fight.CastSound);
                log.Add(GameText.S("core.p3.party_armour_reduced"));
                return;
            case >= 21 and <= 24:
                Weaken(encounter, caster, SpecialTable[special], fight.AttackOf, fight.SetAttack, rng, log);
                encounter.TagLine(log, null, caster, sound: P3Fight.CastSound);
                log.Add(GameText.S("core.p3.party_slowed"));
                return;
            case >= 25 and <= 32:
                var fireball = special >= 29;
                encounter.TagLine(log, null, caster, sound: P3Fight.CastSound);
                log.Add(GameText.F(fireball ? "core.p3.x_casts_fireball" : "core.p3.x_casts_mindblast", caster.Name));
                Blast(encounter, caster, SpecialTable[special], fireball, rng, log);
                return;
            case 33:
                encounter.TagLine(log, null, caster, sound: P3Fight.CastSound);
                log.Add(GameText.F("core.p3.x_casts_sleep", caster.Name));
                Sleep(encounter, caster, rng, log);
                return;
            case 34:
                Melee(encounter, group, caster, MonsterMeleeRider.StunningBlow, rng, log);
                return;
            case 35:
                Melee(encounter, group, caster, MonsterMeleeRider.MagicDrain, rng, log);
                return;
            case 36 or 37:
                log.Add(GameText.F("core.p3.x_breathes", caster.Name));
                Breath(encounter, fight, caster, special == 36 ? 20 : 36, rng, log);
                return;
        }
    }

    /// <summary>
    /// <c>DS:0x9c4</c> by code: Fireflash dice 12 / 35 / 70 / 120; Quickness attack ÷ 10, Strength damage and Protection
    /// armour; the armour / attack percentages 80 / 60 / 40 / 20; Mindblast dice 13 / 26 / 46 / 86, Fireball 12 / 20 / 36 / 68.
    /// </summary>
    private static readonly int[] SpecialTable =
    [
        0, 12, 35, 70, 120, 7, 10, 16, 28, 11, 14, 20, 32, 8, 12, 16, 20, 80, 60, 40, 20, 80, 60, 40, 20, 13, 26, 46, 86,
        12, 20, 36, 68,
    ];

    private static IEnumerable<Monster> BothGroups(CombatEncounter encounter) =>
        encounter.Ranks.Where(r => r.RankIndex is 0 or 1).SelectMany(r => r.Members);

    /// <summary>
    /// The resist roll of every party-wide special: the member resists when <c>rnd(100) &gt; 50 + 5·(monster magic −
    /// member's current magic)</c>.
    /// </summary>
    private static bool Resists(Monster caster, Character member, IRandomSource rng) =>
        P3Dice.Rnd(rng, 100) > 50 + 5 * (caster.CombatRating - member.MagicCur);

    /// <summary>
    /// Root <c>0xbb05</c>: every member not Dead who fails to resist has its per-fight value scaled to
    /// <paramref name="percent"/> % (truncated). `[~]` DOS also calls the damage routine with 0 damage for them; not shown
    /// as a line, but noted as a hit (the Amiga's 0-damage call <c>0x17d56</c> flashes the member).
    /// </summary>
    private static void Weaken(
        CombatEncounter encounter, Monster caster, int percent, Func<Character, int> get, Action<Character, int> set,
        IRandomSource rng, List<string> log)
    {
        foreach (var m in encounter.Party.Members)
        {
            if (m.Status == CharacterStatus.Dead || Resists(caster, m, rng))
                continue;
            encounter.NoteHit(new CombatHit(DamageSource.MonsterSpecial, m, null), log);
            set(m, get(m) * percent / 100);
        }
    }

    /// <summary>
    /// Root <c>0xba4e</c>: every member not Dead takes <c>rnd(dice)</c> unless it resists; a resisted Fireball still deals 1,
    /// a resisted Mindblast nothing.
    /// </summary>
    private static void Blast(
        CombatEncounter encounter, Monster caster, int dice, bool fireball, IRandomSource rng, List<string> log)
    {
        foreach (var m in encounter.Party.Members.ToList())
        {
            if (m.Status == CharacterStatus.Dead)
                continue;
            if (!Resists(caster, m, rng))
                SpellHit(encounter, m, P3Dice.Rnd(rng, dice), rng, log);
            else if (fireball)
                SpellHit(encounter, m, 1, rng, log);
        }
    }

    /// <summary>
    /// Root <c>0xb99b</c>: every member rolls to resist; one that fails and is not Dead or Out falls Asleep. `[~]` DOS also
    /// calls the damage routine with 0 damage for them; not shown as a line, but noted as a hit (the Amiga's 0-damage call
    /// in <c>0x17bc2</c> flashes the member).
    /// </summary>
    private static void Sleep(CombatEncounter encounter, Monster caster, IRandomSource rng, List<string> log)
    {
        foreach (var m in encounter.Party.Members)
        {
            if (Resists(caster, m, rng) || m.Status is CharacterStatus.Dead or CharacterStatus.Out)
                continue;
            encounter.NoteHit(new CombatHit(DamageSource.MonsterSpecial, m, null), log);
            m.Status = CharacterStatus.Sleep;
        }
    }

    /// <summary>
    /// Root <c>0xb582</c>: every member not Dead takes <c>rnd(dice) − armour points</c> (at least 0), or 1 when it resists.
    /// </summary>
    private static void Breath(
        CombatEncounter encounter, P3Fight fight, Monster caster, int dice, IRandomSource rng, List<string> log)
    {
        foreach (var m in encounter.Party.Members.ToList())
        {
            if (m.Status == CharacterStatus.Dead)
                continue;
            var damage = Resists(caster, m, rng) ? 1 : P3Dice.Rnd(rng, dice) - fight.ArmourOf(m);
            SpellHit(encounter, m, Math.Max(0, damage), rng, log);
        }
    }

    /// <summary>A monster spell's hit (<c>damage_character</c> with the no-injury flag): HP only, no body area, no status change.</summary>
    private static void SpellHit(CombatEncounter encounter, Character member, int damage, IRandomSource rng, List<string> log) =>
        CombatResolver.ApplyPartyDamage(encounter, member, damage, DamageSource.MonsterSpecial, rng, log);

    private static void Melee(
        CombatEncounter encounter, MonsterRank group, Monster monster, MonsterMeleeRider rider, IRandomSource rng,
        List<string> log) =>
        encounter.Modules.ResolveMonsterMelee(encounter, group, monster, rider, rng, log);

    // ---- End of round --------------------------------------------------------------------------------------------------

    /// <summary>The end of the round (root <c>0xdb22…0xde4e</c>, §5), in DOS order.</summary>
    private static CombatResult EndOfRound(CombatEncounter encounter, P3Fight fight, IRandomSource rng, List<string> log)
    {
        var party = encounter.Party;
        // 1. Wiped: nobody left who is not Dead and not the elemental (Asleep / Out / Down count as alive).
        if (!party.Members.Any(m => m.Status != CharacterStatus.Dead && !ReferenceEquals(m, party.Elemental)))
            return new CombatResult { PartyWiped = true, EncounterOver = true, Log = log };

        // 2. The final fights: Nikademus or Lord Wood in rank 1 with its first member dead (endgame.md; the reward scene
        // runs after the fight). `[~]` no kill line or gold: DOS returns outcome 2 / 3, not a win.
        if (Group(encounter, 1) is { Members.Count: > 0 } r1 && !r1.Members[0].IsAlive
            && r1.Members[0].Id is Phantasie3Campaign.NikademusA or Phantasie3Campaign.NikademusB
                or Phantasie3Campaign.LordWood)
            return new CombatResult { EncounterOver = true, RewardXp = encounter.RewardXp, Log = log };

        // 3–4. The give-up test runs every round; a wiped side or a give-up ends the fight as a win.
        var givesUp = P3Encounters.Surrenders(Strength(encounter.Ranks), fight.StartStrength, RankOneId(encounter), rng,
            encounter.Rules);
        if (encounter.MonstersWiped)
            return CombatResolver.Victory(encounter, log);
        if (givesUp)
        {
            encounter.TagSound(log, P3Sounds.Stop); // 0x15990: the driver's stop before the line (s158)
            log.Add(GameText.S("core.p3.monsters_flee"));
            var ran = PartyOptions.MonstersRan(encounter);
            return new CombatResult
            {
                PartyWon = true, EncounterOver = true, RewardXp = ran.RewardXp, RewardGold = ran.RewardGold, Log = log,
            };
        }

        // 5. Teleportation and the other fight-ending spells (the shared caster's SpellEnd).
        if (encounter.SpellEnd != SpellCombatEnd.None)
            return CombatSpells.EndBySpell(encounter, log);

        // 7. The fight goes on; an Awaken cast this round wakes the sleepers.
        fight.FreeFlee = false;
        fight.WakeChance = Math.Min(100, fight.WakeChance + 15);
        if (fight.AwakenAtRoundEnd)
        {
            // 0xdd85: each party slot still Asleep wakes through the same "awakens" routine (0xd301) as a wake roll.
            foreach (var member in party.Members.Take(PartySlots).Where(m => P3Fight.StatusOf(m) == P3Status.Asleep))
                Awaken(member, log);
            fight.AwakenAtRoundEnd = false;
        }

        // 8. "The monsters will reorganize": an empty rank 1 lets rank 2 move up; else a stronger rank 2 comes forward 30 %.
        if (Group(encounter, 1) is { } front && Group(encounter, 2) is { } rear && rear.Living.Any())
        {
            if (!front.Living.Any()
                || (Strength([front]) < Strength([rear]) && P3Dice.Rnd(rng, 100) <= RankSwapChance))
                SwapGroups(front, rear);
        }

        // 9. Compaction: the living move to the front slots; the body areas stay by slot.
        foreach (var g in encounter.Ranks)
            Compact(fight, g);
        return new CombatResult { EncounterOver = false, Log = log };
    }

    /// <summary>The group in rank 1 or 2 (<see cref="MonsterRank.RankIndex"/> 0 / 1 = the rank position), wiped or not.</summary>
    private static MonsterRank? Group(CombatEncounter encounter, int rank) =>
        encounter.Ranks.FirstOrDefault(r => r.RankIndex == rank - 1);

    private static int RankOneId(CombatEncounter encounter) =>
        Group(encounter, 1)?.Members.FirstOrDefault()?.Id ?? encounter.FirstMonsterId;

    /// <summary>Monster strength (root <c>0xf460</c> / <c>0xf48c</c>): Σ level over members with odd HP.</summary>
    public static int Strength(IEnumerable<MonsterRank> groups) =>
        P3Encounters.Strength(groups.Select(r => new P3GroupStrength(
            r.Members.Count == 0 ? 0 : r.Members[0].Level, r.Members.Select(m => Math.Max(0, m.HitPoints ?? 0)).ToList())));

    /// <summary>Root <c>0x113e6</c>: the two group records change places (members, bodies with them).</summary>
    private static void SwapGroups(MonsterRank front, MonsterRank rear)
    {
        var members = front.Members.ToList();
        front.Members.Clear();
        front.Members.AddRange(rear.Members);
        rear.Members.Clear();
        rear.Members.AddRange(members);
    }

    /// <summary>
    /// Root <c>0xd23e</c>: living members (status &gt; 0 and HP &gt; 0) to the front, in order. A mover keeps its status
    /// until its next hit or waking, then takes it from its new slot's body (<see cref="P3Fight.KeepBodiesBySlot"/>).
    /// </summary>
    private static void Compact(P3Fight fight, MonsterRank group)
    {
        var before = group.Members.ToList();
        var compacted = before.Where(m => m.IsAlive).Concat(before.Where(m => !m.IsAlive)).ToList();
        if (compacted.SequenceEqual(before))
            return;
        fight.KeepBodiesBySlot(before, compacted);
        group.Members.Clear();
        group.Members.AddRange(compacted);
    }
}
