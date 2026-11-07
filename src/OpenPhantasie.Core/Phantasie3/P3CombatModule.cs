namespace OpenPhantasie.Core;

/// <summary>
/// Phantasie III's combat in the shared round loop (<see cref="FeatureModuleRegistry.P3CombatActions"/>): the action menu
/// (<see cref="P3Combat.Choose"/>, D3-010…013), party melee, Aim and Fire Bow (<c>character_attack</c> root
/// <c>0xc03b</c>), body-area injury on both sides (<c>body-areas.md</c> §2–3), Arrow Flame / Accuracy (D3-017) and monster
/// melee (root <c>0xb64e</c>, D3-018 / 019). Its rules live in <see cref="P3Combat"/> / <see cref="P3BodyAreas"/>; this
/// class only feeds them the fight. Other casts go to the shared <see cref="SpellCaster"/> with I's packing.
/// <para>
/// Who acts when, monster specials and the round end are III's round loop (<see cref="P3RoundResolver"/>). A group is
/// found by its rank position (<see cref="MonsterRank.RankIndex"/>: 0 = rank 1, 1 = rank 2); the round loop swaps the
/// two groups' members when the ranks reorganise.
/// </para>
/// </summary>
public sealed class P3CombatModule : ICombatActionModule, IMonsterMeleeModule
{
    /// <summary>
    /// First packed value of III's actions: <c>PackBase + 10 · action + rank</c> (rank 1 or 2). Clear of I's melee
    /// (10–99), casts (105+), HEALING (1000–4999) and II's toss (5000–5009); Parry packs as I's <see cref="CombatAction.Parry"/>.
    /// </summary>
    public const int PackBase = 6000;

    private static readonly CombatChoice[] MenuOrder =
    [
        CombatChoice.Attack,
        CombatChoice.Lunge,
        CombatChoice.Slash,
        CombatChoice.Thrust,
        CombatChoice.FireBow,
        CombatChoice.Parry,
        CombatChoice.Aim,
        CombatChoice.Cast,
    ];

    public FeatureModuleDescriptor Descriptor => FeatureModuleRegistry.P3CombatActions;

    /// <summary>
    /// The menu in DOS order (strings 490–497: Attack, Lunge, Slash, Thrust, Fire Bow, Parry, Aim Blow, Cast Spell), then
    /// Change Rank (498), which the front end handles itself.
    /// </summary>
    public IReadOnlyList<CombatChoice> Choices { get; } = [.. MenuOrder, CombatChoice.ChangeRank];

    public static int Pack(P3ActionChoice choice) =>
        choice.Action == P3Action.Parry ? CombatAction.Parry : PackBase + 10 * (int)choice.Action + choice.Rank;

    public static bool TryUnpack(int packed, out P3ActionChoice choice)
    {
        choice = default;
        var v = packed - PackBase;
        if (v < 0 || v / 10 > (int)P3Action.Aim || v % 10 is < 1 or > 2)
            return false;
        choice = new P3ActionChoice((P3Action)(v / 10), v % 10);
        return true;
    }

    private static P3Action ActionOf(CombatChoice choice) => choice switch
    {
        CombatChoice.Attack => P3Action.Attack,
        CombatChoice.Lunge => P3Action.Lunge,
        CombatChoice.Slash => P3Action.Slash,
        CombatChoice.Thrust => P3Action.Thrust,
        CombatChoice.FireBow => P3Action.FireBow,
        CombatChoice.Parry => P3Action.Parry,
        CombatChoice.Aim => P3Action.Aim,
        CombatChoice.Cast => P3Action.CastSpell,
        _ => throw new ArgumentOutOfRangeException(nameof(choice), choice, null),
    };

    /// <summary>The group a rank (1 or 2) names: built in that place and not wiped; null when it is gone.</summary>
    private static MonsterRank? Group(CombatEncounter encounter, int rank) =>
        encounter.Ranks.FirstOrDefault(r => r.RankIndex == rank - 1 && !r.IsWiped);

    /// <summary>
    /// The group an action strikes: the one its rank names. A rank wiped earlier in the round stays empty until the round
    /// end moves rank 2 up (<see cref="P3RoundResolver"/>), so the blow is lost `[~]` (<c>0xc03b</c> finds no target).
    /// </summary>
    private static MonsterRank? Target(CombatEncounter encounter, int rank) => Group(encounter, rank);

    private static bool RankTwoExists(CombatEncounter? encounter) => encounter is not null && Group(encounter, 2) is not null;

    private static P3Chooser Chooser(Character member, CombatEncounter? encounter) => new(
        member.Class, member.Skills.Attack, P3BodyAreas.Of(member), P3Fight.StatusOf(member), member.WornBow != 0,
        encounter is not null && ReferenceEquals(member, encounter.Party.Elemental), RankTwoExists(encounter));

    public bool TryChoose(in CombatOrderRequest request, IRandomSource rng, out int packedAction)
    {
        packedAction = CombatAction.Cleared;
        if (Array.IndexOf(MenuOrder, request.Choice) < 0)
            return false;
        var rules = request.Rules ?? GameOptions.ForPreset(GameId.P3);
        var choice = P3Combat.Choose(ActionOf(request.Choice), Chooser(request.Member, request.Encounter),
            request.RankIndex + 1, rules);
        if (choice.Action == P3Action.CastSpell)
        {
            var target = request.Encounter is { } enc && (uint)request.HealTargetIndex < (uint)enc.Party.Members.Count
                ? enc.Party.Members[request.HealTargetIndex]
                : null;
            var slots = SpellSlots.For(rules);
            packedAction = !slots.Contains(request.SpellIndex) || !Magic.KnowsSpell(request.Member, request.SpellIndex)
                           || CastRefusal(request.Member, request.SpellIndex, target, rules) is not null
                ? Pack(new P3ActionChoice(P3Action.Thrust, 1)) // root 0xff55: action 3, Thrust (R1)
                : CombatOrders.Choose(request.Member, request.Prep, CombatChoice.Cast, rng, 0, request.SpellIndex,
                    request.HealTargetIndex, rules); // the packing is the shared caster's (I's)
        }
        else
            packedAction = Pack(choice);
        return true;
    }

    /// <summary>
    /// DOS III's refusals of a picked spell (root <c>0xfc41</c>, <c>combat-actions-and-ranks.md</c> §2 "Cast refusals";
    /// the Amiga's <c>0x19430</c> the same): the message's text id, or null when the cast goes ahead. Each refusal makes
    /// the order Thrust (R1) (<see cref="TryChoose"/>). Checked: spells #52, #54 and #57 on (0-based 51, 53, ≥ 56) "cannot
    /// be cast in combat"; a cost above the caster's magic; Healing I–IV on a Dead <paramref name="healTarget"/>.
    /// Resurrection's "not dead" check (<c>0xffc6</c>) is not made here: Core's Resurrection order carries no target `[?]`.
    /// </summary>
    public static string? CastRefusal(Character caster, int spellIndex, Character? healTarget, GameOptions? rules)
    {
        ArgumentNullException.ThrowIfNull(caster);
        if (spellIndex is CombatMonsterEvaluation or CombatTransportation || spellIndex >= CombatFirstPlaneSpell)
            return "core.p3.spell_not_in_combat";
        if (!Magic.CanAfford(caster, spellIndex, SpellSlots.For(rules)))
            return "core.p3.magic_too_low";
        if (spellIndex <= SpellId.HealingIv && healTarget is { Status: CharacterStatus.Dead })
            return "core.p3.dead_or_out";
        return null;
    }

    /// <summary>
    /// DOS III's refusals of Cast itself, before the spell list opens (root <c>0xffef</c> / <c>0xffdf</c>; the Amiga's
    /// <c>0x19430</c> / <c>0x1943a</c> the same, s134 static): "This character knows no spells." when the record's spell
    /// count (<c>+0x56</c>, every known spell, castable in combat or not) is 0; else "This character's magic is too low!"
    /// when the current magic (<c>+0x1e</c>) is 0 or less. The message's text id, or null when the list opens. Each refusal
    /// makes the order Thrust (R1) (<c>0xff55</c>; no rank question, a Thief too), as <see cref="CastRefusal"/>'s do.
    /// </summary>
    public static string? CastRefusalBeforeList(Character caster)
    {
        ArgumentNullException.ThrowIfNull(caster);
        if (Array.IndexOf(caster.SpellsKnown, 1) < 0)
            return "core.p3.knows_no_spells";
        if (caster.MagicCur <= 0)
            return "core.p3.magic_too_low";
        return null;
    }

    /// <summary>0-based ids DOS III refuses in combat (<c>0xfeb1</c>: 1-based 0x34 / 0x36 / &gt; 0x38).</summary>
    private const int CombatMonsterEvaluation = 51, CombatTransportation = 53, CombatFirstPlaneSpell = 56;

    /// <summary>
    /// "Target which rank?" (string 483) when a second group exists: Fire Bow; a Thief's Thrust, Slash (at 150 %) and
    /// Attack (manual D3-012; never under <c>p3.thief-attack</c> = <c>dos</c>). Lunge goes to rank 2 without asking.
    /// </summary>
    public bool? AsksRank(CombatEncounter encounter, int memberIndex, CombatChoice choice)
    {
        if (Array.IndexOf(MenuOrder, choice) < 0)
            return null;
        if (!RankTwoExists(encounter) || (uint)memberIndex >= (uint)encounter.Party.Members.Count)
            return false;
        var member = encounter.Party.Members[memberIndex];
        var picked = P3Combat.Choose(ActionOf(choice), Chooser(member, encounter), 2, encounter.Rules);
        return picked.Rank == 2 && choice != CombatChoice.Lunge;
    }

    /// <summary>
    /// The cast gate of DOS III's <c>0xce66</c> [C]: Healing I–IV and Resurrection are always cast; any other spell (bow
    /// spells too) only while a group's living count (<c>FAR:0xe5b2</c> / <c>0xe63c</c>, lowered only by a monster's death
    /// in <c>damage_monster</c> <c>0xb33d</c>) is above 0. A cast after the last monster died earlier in the round prints
    /// nothing and spends no magic.
    /// </summary>
    public static bool CastGoesAhead(CombatEncounter encounter, int spellIndex) =>
        spellIndex <= SpellId.HealingIv || spellIndex == SpellId.Resurrection
        || encounter.Ranks.Exists(r => !r.IsWiped);

    public bool TryResolve(
        CombatEncounter encounter, int memberIndex, int packedAction, IRandomSource rng, List<string> log)
    {
        var fight = P3Fight.Of(encounter);
        if (CombatAction.TryDecodeCast(packedAction, out var spellIndex, out _))
        {
            if (!CastGoesAhead(encounter, spellIndex))
                return true; // no line, no magic spent
            if (fight is not null && BowSpell(spellIndex) is { } bowSpell)
                CastBowSpell(encounter, fight, memberIndex, spellIndex, bowSpell, log);
            else if (SpellCaster.ResolveCombatCast(encounter, memberIndex, packedAction, rng, log).Success && fight is not null)
                SetFightValues(encounter, fight, spellIndex);
            return true;
        }
        if (!TryUnpack(packedAction, out var choice))
            return false;
        if (fight is null)
            return true; // III's orders mean nothing outside a III fight
        var member = encounter.Party.Members[memberIndex];
        var status = P3Fight.StatusOf(member);
        if (status is not (P3Status.Okay or P3Status.Down))
            return true; // Asleep, Out and Dead don't attack (0xc03b)
        choice = P3Combat.Recheck(choice, P3BodyAreas.Of(member), status);
        if (choice.Action == P3Action.Parry || Target(encounter, choice.Rank) is not { } group)
            return true;
        if (choice.Action == P3Action.FireBow)
            FireBow(encounter, fight, member, group, rng, log);
        else
            Melee(encounter, fight, member, status, choice, group, rng, log);
        return true;
    }

    // ---- Character → monster -------------------------------------------------------------------------------------------

    private static void Melee(
        CombatEncounter encounter, P3Fight fight, Character member, P3Status status, P3ActionChoice choice,
        MonsterRank group, IRandomSource rng, List<string> log)
    {
        var attack = new P3MeleeAttack(choice.Action, fight.AttackOf(member), fight.Points(member.WornWeapon),
            group.Members[0].Defense, status == P3Status.Down, RankTerm(member, encounter.Party), choice.Rank,
            fight.TallMonster);
        P3Combat.ResolveMelee(attack,
            r => PickMonster(group, r),
            t => fight.StatusOf(group.Members[t]),
            (t, damage, row) =>
            {
                encounter.TagLine(log, member, null, CombatSwing.Hit);
                log.Add(GameText.F("core.combat.x_hits", member.Name));
                HitMonster(encounter, fight, group.Members[t], damage, row, rng, log);
            },
            rng, encounter.Rules,
            miss: _ =>
            {
                encounter.TagLine(log, member, null, CombatSwing.Miss);
                log.Add(GameText.F("core.combat.x_misses", member.Name));
            });
    }

    private static void FireBow(
        CombatEncounter encounter, P3Fight fight, Character member, MonsterRank group, IRandomSource rng, List<string> log)
    {
        if (PickMonster(group, rng) is not { } t)
            return;
        var target = group.Members[t];
        var bow = fight.BowStatsOf(member, encounter.Rules);
        var damage = P3Combat.FireBow(bow, group.Members[0].Defense, fight.StatusOf(target), rng, encounter.Rules);
        // Presentation only: a shot is a swing on III Amiga's field (s156 [C] static: the member's attack routine 0x16230
        // serves Fire Bow (R1 / R2) too, drawing the attack frame 0x1b6be(slot) before its to-hit, then sound 15 + " hits!"
        // or 18 + " misses", FORMAT.md 8.1).
        if (damage == 0)
        {
            encounter.TagLine(log, member, null, CombatSwing.Miss);
            log.Add(GameText.F("core.combat.x_misses", member.Name));
            return;
        }
        encounter.TagLine(log, member, null, CombatSwing.Hit);
        log.Add(GameText.F("core.combat.x_hits", member.Name));
        HitMonster(encounter, fight, target, damage,
            P3Combat.LocationRow(P3Action.FireBow, fight.TallMonster), rng, log);
    }

    /// <summary>
    /// The target of one swing or shot (root <c>0xb4ec</c>): a random start among the group's five slots, then the first
    /// living member from there `[~]` (wrap-around assumed). Asleep, Down and Unconscious members are targets.
    /// </summary>
    private static int? PickMonster(MonsterRank group, IRandomSource rng)
    {
        const int slots = 5;
        var start = P3Dice.Rnd(rng, slots) - 1;
        for (var k = 0; k < slots; k++)
        {
            var s = (start + k) % slots;
            if (s < group.Members.Count && group.Members[s].IsAlive)
                return s;
        }
        return null;
    }

    /// <summary>
    /// <c>damage_monster</c> (root <c>0xb116</c>): HP down, the Greet block set, the injury step (<c>body-areas.md</c> §2),
    /// then the status from the body; a head or torso removed kills whatever HP is left.
    /// </summary>
    public static void HitMonster(
        CombatEncounter encounter, P3Fight fight, Monster target, int damage, P3LocationRow row, IRandomSource rng,
        List<string> log)
    {
        encounter.NoteHit(new CombatHit(DamageSource.PartyMelee, null, target), log); // a swing or a shot: no flash flag
        var before = target.HitPoints ?? 0;
        target.HitPoints = before - damage;
        encounter.MonsterDamaged = true; // FAR:0xb618
        log.Add(GameText.F("core.combat.x_takes_x_damage", target.Name, damage));
        var body = fight.BodyOf(target);
        fight.RefreshStatus(target); // the hit recomputes the status from the body in its slot
        if (body.TakeHit(before, damage, fight.HitLocations, row, rng) is { Worsened: true } injury)
            log.Add(InjuryLine(target.Name, injury, limbless: fight.LimblessMonsterIds.Contains(target.Id)));
        if (target.HitPoints > 0 && body.BodyStatus() != P3Status.Dead)
            return;
        target.HitPoints = 0;
        encounter.RecordKill(target);
        encounter.TagDies(log);
        log.Add(GameText.F("core.combat.x_dies", target.Name));
    }

    // ---- Parry (D3-036) -------------------------------------------------------------------------------------------------

    /// <summary>
    /// The manual's Parry (D3-036, user s89, Apple II line 1527 plus the hit-chance half): every member whose order this
    /// round is Parry (or forced to it by a broken right arm) and who can act (Okay or Down) rolls <c>rnd(100) ≤ Parry
    /// skill</c>; a success ("&lt;name&gt; parries.") takes the shield's points off the monsters' melee chances this round
    /// and off the first damaging melee hit (<see cref="TryMonsterMelee"/>). Spells and breath are not affected. Rolled
    /// when the round starts, so the parry holds against monsters that act before the member, as Apple II: lines 1520–1531
    /// roll every Parry order once orders are confirmed, before the action loop at line 1600 [C] (s98). Apple II's
    /// hurt-shield-arm success is not copied.
    /// </summary>
    public static void RollParries(CombatEncounter encounter, P3Fight fight, IRandomSource rng, List<string> log)
    {
        var members = encounter.Party.Members;
        for (var i = 0; i < members.Count && i < encounter.Prep.Length; i++)
        {
            var member = members[i];
            var status = P3Fight.StatusOf(member);
            if (status is not (P3Status.Okay or P3Status.Down) || !IsParryOrder(encounter.Prep[i].PackedAction, member, status))
                continue;
            if (P3Dice.Rnd(rng, 100) > member.Skills.Parry)
                continue;
            fight.SetParried(member);
            log.Add(GameText.F("core.p3.x_parries", member.Name));
        }
    }

    private static bool IsParryOrder(int packedAction, Character member, P3Status status) =>
        CombatAction.IsParry(packedAction)
        || (TryUnpack(packedAction, out var choice)
            && P3Combat.Recheck(choice, P3BodyAreas.Of(member), status).Action == P3Action.Parry);

    // ---- The saved option (record +0x6e / +0x70) ---------------------------------------------------------------------

    /// <summary>The saved option of a new character: Thrust at rank 1 (creation, ovl2 <c>0x255e</c>).</summary>
    public const int NewCharacterAction = (int)P3Action.Thrust;

    private const int CastCode = (int)P3Action.CastSpell;
    private const int RankTwoBase = 11;
    private const int CastRankTwoCode = 16;

    /// <summary>
    /// The DOS action code of an order (<c>combat-actions-and-ranks.md</c> §2): 0 Attack … 6 Aim, 5 Parry, 7 Cast, rank 2
    /// as 11 + action (Attack 11 … Fire Bow 15) and 16 Cast; null for no order.
    /// </summary>
    public static int? ActionCode(int packedAction)
    {
        if (CombatAction.TryDecodeCast(packedAction, out _, out _))
            return CastCode;
        if (CombatAction.IsParry(packedAction))
            return (int)P3Action.Parry;
        if (!TryUnpack(packedAction, out var c))
            return null;
        return c.Rank == 2 ? RankTwoBase + (int)c.Action : (int)c.Action;
    }

    /// <summary>The order a DOS action code stands for; Cast codes have none of their own (the spell is in <c>+0x70</c>).</summary>
    public static P3ActionChoice? ChoiceOf(int code) => code switch
    {
        (int)P3Action.Parry => new P3ActionChoice(P3Action.Parry),
        CastCode or CastRankTwoCode => null,
        >= 0 and <= (int)P3Action.Aim => new P3ActionChoice((P3Action)code, 1),
        >= RankTwoBase and <= RankTwoBase + (int)P3Action.FireBow => new P3ActionChoice((P3Action)(code - RankTwoBase), 2),
        _ => null,
    };

    /// <summary>
    /// Fight end (root <c>0xd4ff</c>): each member's <c>+0x6e</c> / <c>+0x70</c> take this fight's last order (an empty
    /// order keeps the old value, as DOS's array still holds what fight start loaded). Under DOS's Parry
    /// (<c>p3.parry</c> = <c>dos</c>) a member whose last order was Parry keeps the shield bonus all the next fight.
    /// </summary>
    public static void SaveOrders(CombatEncounter encounter)
    {
        var members = encounter.Party.Members;
        for (var i = 0; i < members.Count && i < encounter.Prep.Length; i++)
        {
            if (ReferenceEquals(members[i], encounter.Party.Elemental))
                continue;
            var p3 = members[i].P3;
            var packed = encounter.Prep[i].PackedAction;
            if (ActionCode(packed) is not { } code)
                continue;
            p3.SavedAction = code;
            p3.SavedCast = code == CastCode ? packed : null;
        }
    }

    /// <summary>
    /// Fight start (root <c>0xd55b</c>): each member's saved option is re-checked and written back, then becomes the
    /// first order. Attack below 76 % and Slash below 150 % → Thrust; Fire Bow while Down or with no bow → Thrust; Right
    /// Arm ≥ Broken (<c>0xb550</c>) → Parry. With no second group the order (not the saved option) drops to rank 1:
    /// Lunge and Thrust → Thrust, Attack / Slash / Fire Bow → their rank-1 form; a Cast keeps its spell.
    /// </summary>
    public static void LoadSavedOrders(CombatEncounter encounter)
    {
        encounter.EnsurePrep();
        var rankTwo = RankTwoExists(encounter);
        var members = encounter.Party.Members;
        for (var i = 0; i < members.Count && i < encounter.Prep.Length; i++)
        {
            var member = members[i];
            if (ReferenceEquals(member, encounter.Party.Elemental))
                continue;
            var p3 = member.P3;
            var code = Recheck(p3.SavedAction, member);
            p3.SavedAction = code;
            if (!rankTwo)
                code = RankOneForm(code);
            encounter.Prep[i].PackedAction = code is CastCode or CastRankTwoCode
                ? p3.SavedCast ?? CombatAction.Cleared
                : ChoiceOf(code) is { } choice ? Pack(choice) : CombatAction.Cleared;
        }
    }

    private static int Recheck(int code, Character member)
    {
        const int thrust = (int)P3Action.Thrust;
        const int bow = (int)P3Action.FireBow;
        const int bowRankTwo = RankTwoBase + bow;
        var attack = member.Skills.Attack;
        if (attack < P3Combat.AttackMinimum && code == (int)P3Action.Attack) code = thrust;
        if (attack < P3Combat.SlashMinimum && code == (int)P3Action.Slash) code = thrust;
        if (P3Fight.StatusOf(member) == P3Status.Down && code is bow or bowRankTwo) code = thrust;
        if (P3BodyAreas.Of(member).ForcesParry) code = (int)P3Action.Parry;
        if (member.WornBow == 0 && code is bow or bowRankTwo) code = thrust;
        return code;
    }

    /// <summary>The jump table at root <c>0xd5c4</c>: the order with no second group.</summary>
    private static int RankOneForm(int code) => code switch
    {
        (int)P3Action.Lunge or RankTwoBase + (int)P3Action.Lunge or RankTwoBase + (int)P3Action.Thrust => (int)P3Action.Thrust,
        RankTwoBase + (int)P3Action.Attack => (int)P3Action.Attack,
        RankTwoBase + (int)P3Action.Slash => (int)P3Action.Slash,
        RankTwoBase + (int)P3Action.FireBow => (int)P3Action.FireBow,
        _ => code,
    };

    // ---- Monster → character -------------------------------------------------------------------------------------------

    /// <summary>
    /// <c>monster_melee_attack</c> (root <c>0xb64e</c>): <see cref="P3Combat.MonsterAttacks"/> attacks, each on a random
    /// party member; chance <see cref="P3Combat.MonsterChance"/>; a hit deals <c>rnd(damage)</c> on row 2. The run stops at
    /// the first miss or touch (<c>combat-round.md</c> §4: the swing counter is overwritten; code `[C]`, effect `[~]`). An
    /// Unconscious or sleeping monster makes no attack (the round loop skips them too); a special's rider (I's ids
    /// 34 / 35 = III's 0x22 / 0x23 touches) follows each hit as I's.
    /// </summary>
    public bool TryMonsterMelee(
        CombatEncounter encounter, MonsterRank group, Monster monster, MonsterMeleeRider rider, IRandomSource rng,
        List<string> log)
    {
        if (P3Fight.Of(encounter) is not { } fight)
            return false;
        if (fight.StatusOf(monster) is not (P3Status.Okay or P3Status.Down))
            return true;
        var party = encounter.Party;
        var rules = encounter.Rules;
        var leaderDown = group.Members.Count > 0 && fight.StatusOf(group.Members[0]) == P3Status.Down;
        var attacks = P3Combat.MonsterAttacks(monster.AttackPower);
        for (var a = 0; a < attacks; a++)
        {
            if (PickMember(party, rng) is not { } t)
                return true;
            var member = party.Members[t];
            var shield = fight.Points(member.WornShield);
            var defence = P3Combat.Defence(shield, fight.Points(member.WornArmor), member.Skills.Parry, member.Stats.Dex,
                rules);
            var dosParry = rules.P3DosParry();
            var parrying = dosParry ? member.Phantasie3?.ParrySaved == true : fight.Parried(member);
            var chance = P3Combat.MonsterChance(monster.AttackPower, defence, leaderDown, RankTerm(member, party),
                parrying, shield, P3Fight.StatusOf(member), rules);
            if (P3Dice.Rnd(rng, 100) > chance)
            {
                encounter.TagLine(log, null, monster, CombatSwing.Miss);
                log.Add(GameText.F("core.combat.x_misses", monster.Name));
                return true; // the swing counter is overwritten on a miss (0xb79c): the run stops [~]
            }
            encounter.TagLine(log, null, monster, CombatSwing.Hit);
            log.Add(GameText.F("core.combat.x_hits", monster.Name));
            var damage = P3Dice.Rnd(rng, Math.Max(1, monster.DamageField));
            if (!dosParry && fight.UseParryBlock(member))
            {
                damage -= shield; // D3-036: the first damaging melee hit this round (no melee armour soak in III)
                if (damage <= 0)
                {
                    log.Add(GameText.F("core.p3.x_blocks_the_blow", member.Name));
                    continue;
                }
            }
            HitMember(encounter, fight, member, damage, rng, log);
            if (rider != MonsterMeleeRider.None)
                encounter.MarkTouch(member); // Amiga 0x177c8 types 34 / 35: damage_character with its flash flag
            if (encounter.PartyWiped)
                return true;
            if (rider != MonsterMeleeRider.None && encounter.Prep.Length > t)
            {
                MonsterSpecials.ApplyRider(rider, member, encounter.Prep[t], log, stunTheDead: rules.DosStunningBlow);
                return true; // a stun or drain also ends the run (0xb91d / 0xb726) [~]
            }
        }
        return true;
    }

    /// <summary>
    /// A party member for a monster's swing or Fireflash (root <c>0x1124</c>): a random start, then the first one not Dead
    /// (wrapping); Asleep, Out and Down are targets, the elemental too.
    /// </summary>
    internal static int? PickMember(Party party, IRandomSource rng)
    {
        var n = party.Members.Count;
        if (n == 0)
            return null;
        var start = P3Dice.Rnd(rng, n) - 1;
        for (var k = 0; k < n; k++)
        {
            var i = (start + k) % n;
            if (party.Members[i].Status != CharacterStatus.Dead)
                return i;
        }
        return null;
    }

    /// <summary>
    /// <c>damage_character</c> (root <c>0xbbb6</c>): HP down (the campaign's death step at 0), the injury step on row 2,
    /// then the status from the body (root <c>0x107b</c>); a head or torso removed is death at any HP `[~]` (the death
    /// step for it is the campaign's, as at 0 HP).
    /// </summary>
    public static void HitMember(
        CombatEncounter encounter, P3Fight fight, Character member, int damage, IRandomSource rng, List<string> log)
    {
        var before = member.HpCur;
        CombatResolver.ApplyPartyDamage(encounter, member, damage, DamageSource.MonsterMelee, rng, log);
        var body = P3BodyAreas.Of(member);
        var injury = body.TakeHit(before, damage, fight.HitLocations, P3LocationRow.Melee, rng);
        if (injury is null)
            return;
        member.BodyCondition = body.ToCondition();
        if (injury.Worsened)
            log.Add(InjuryLine(member.Name, injury, limbless: false));
        if (member.Status == CharacterStatus.Dead)
            return;
        Phantasie3Campaign.ApplyBodyStatus(member, body);
        if (member.Status != CharacterStatus.Dead)
            return;
        encounter.Party.Death.ApplyDeath(member);
        log.Add(GameText.F("core.combat.x_dies", member.Name));
    }

    /// <summary>"&lt;name&gt;'s &lt;area&gt; is injured! / breaks! / is removed!" (root <c>0xe6c6</c>); limbless: "'s body".</summary>
    private static string InjuryLine(string name, P3Injury injury, bool limbless) =>
        GameText.F("core.p3.injury_x_x_x", name,
            GameText.S(limbless ? "core.p3.area.body" : injury.Area switch
            {
                P3BodyArea.Head => "core.p3.area.head",
                P3BodyArea.LeftArm => "core.p3.area.left_arm",
                P3BodyArea.RightArm => "core.p3.area.right_arm",
                P3BodyArea.Torso => "core.p3.area.torso",
                P3BodyArea.LeftLeg => "core.p3.area.left_leg",
                _ => "core.p3.area.right_leg",
            }),
            GameText.S(injury.Severity switch
            {
                P3AreaState.Gone => "core.p3.injury.gone",
                P3AreaState.Broken => "core.p3.injury.broken",
                _ => "core.p3.injury.injured",
            }));

    /// <summary>Change Rank (root <c>0xfb5f</c>): Front / Middle / Back into record <c>+0x08</c>.</summary>
    public static void ChangeRank(Character member, P3PartyRank rank)
    {
        ArgumentNullException.ThrowIfNull(member);
        member.P3.Rank = rank;
    }

    /// <summary>A member's rank term (<see cref="P3Combat.RankTerm"/>) in <paramref name="party"/>.</summary>
    private static int RankTerm(Character member, Party party) =>
        P3Combat.RankTerm(RankOf(member), party.Members.Select(RankOf).ToList());

    private static P3PartyRank RankOf(Character c) => c.Phantasie3?.Rank ?? P3PartyRank.Front;

    // ---- Party buffs ---------------------------------------------------------------------------------------------------

    /// <summary>
    /// <c>combat_spell_5_28</c> (root <c>0xc3cc</c>, table <c>DS:0xc1a</c>): Quickness I–IV (#9–12) sets every member's
    /// attack % this fight to Attack + 10 / 25 / 45 / 70, Protection I–IV (#17–20) its armour points to the armour's points
    /// + 1 / 3 / 6 / 10. Both are rebuilt from the base, so they undo the monsters' slow and armour spells (manual: "an
    /// enemy Slow is overridden by Quickness").
    /// </summary>
    private static void SetFightValues(CombatEncounter encounter, P3Fight fight, int spellIndex)
    {
        foreach (var m in encounter.Party.Members)
        {
            if (spellIndex is >= SpellId.QuicknessI and <= SpellId.QuicknessIv)
                fight.SetAttack(m, m.Skills.Attack + Magic.QuicknessPercent[spellIndex - SpellId.QuicknessI]);
            else if (spellIndex is >= SpellId.ProtectionI and <= SpellId.ProtectionIv)
                fight.SetArmour(m, fight.Points(m.WornArmor) + Magic.ProtectionFlat[spellIndex - SpellId.ProtectionI]);
        }
    }

    // ---- Bow spells ----------------------------------------------------------------------------------------------------

    /// <summary>
    /// #13 / #14 Arrow Flame I, #15 / #16 Arrow Flame II (0-based 12–15), #55 / #56 Accuracy / Party Accuracy (54 / 55,
    /// <c>combat_spell_29_56</c> cases <c>0xca34</c> / <c>0xca46</c>: bow % word = 250; III's spell layout,
    /// <see cref="SpellSlots.PhantasieIII"/>, costs them 3 / 4 MP).
    /// </summary>
    public static P3BowSpell? BowSpell(int spellIndex) => spellIndex switch
    {
        12 or 13 => P3BowSpell.ArrowFlameI,
        14 or 15 => P3BowSpell.ArrowFlameII,
        P3Spells.Accuracy or P3Spells.PartyAccuracy => P3BowSpell.Accuracy,
        _ => null,
    };

    /// <summary>The party forms: P. Arrow Flame I / II, Party Accuracy.</summary>
    private static bool ForParty(int spellIndex) => spellIndex is 13 or 15 or P3Spells.PartyAccuracy;

    /// <summary>
    /// <c>combat_spell_5_28</c> / <c>combat_spell_29_56</c> (roots <c>0xc3cc</c> / <c>0xc687</c>): the magic is spent as
    /// for any cast, then the caster's (or every living member's) bow fire uses the spell for the rest of the fight.
    /// </summary>
    private static void CastBowSpell(
        CombatEncounter encounter, P3Fight fight, int casterIndex, int spellIndex, P3BowSpell spell, List<string> log)
    {
        var caster = encounter.Party.Members[casterIndex];
        if (Magic.TrySpendMp(caster, spellIndex, out var reason, SpellSlots.PhantasieIII) < 0)
        {
            log.Add(GameText.F("core.magic.x_fails_to_cast_x", caster.Name, reason));
            return;
        }
        caster.CastSinceRest = true;
        P3Fight.AddCastLine(encounter, caster, spellIndex, log);
        // Party Accuracy (0xca46) loops over every party slot, whatever its status; the party Arrow Flames skip the dead.
        IEnumerable<Character> targets = !ForParty(spellIndex) ? [caster]
            : spellIndex == P3Spells.PartyAccuracy ? encounter.Party.Members
            : encounter.Party.Members.Where(m => m.Status != CharacterStatus.Dead);
        foreach (var m in targets)
            fight.SetBowSpell(m, spell, encounter.Rules);
    }
}
