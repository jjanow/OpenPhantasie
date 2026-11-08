namespace OpenPhantasie.Core;

/// <summary>
/// Combat / out-of-combat spell resolution from OV2 cast dispatch
/// (<c>magic-spells.md</c> + recovered coefficient tables).
/// </summary>
public static class SpellCaster
{
    /// <summary>
    /// Resolve a packed cast action for one living party member during combat.
    /// Deducts MP, applies effects, appends to <paramref name="log"/>.
    /// </summary>
    public static MagicResult ResolveCombatCast(
        CombatEncounter encounter,
        int casterIndex,
        int packedAction,
        IRandomSource rng,
        List<string> log)
    {
        ArgumentNullException.ThrowIfNull(encounter);
        ArgumentNullException.ThrowIfNull(rng);
        ArgumentNullException.ThrowIfNull(log);
        encounter.EnsurePrep();

        if ((uint)casterIndex >= (uint)encounter.Party.Members.Count)
            return MagicResult.Fail(GameText.S("core.magic.invalid_caster"));

        var caster = encounter.Party.Members[casterIndex];
        if (!CombatAction.TryDecodeCast(packedAction, out var spellIndex, out var healTarget))
            return MagicResult.Fail(GameText.S("core.magic.not_a_cast_action"));
        if (P3Spells.NotOnTheSharedCaster(encounter.Rules, spellIndex)) // T4: III's own spells are not built yet
        {
            log.Add(GameText.S("core.p3.spell_not_built"));
            return MagicResult.Fail(GameText.S("core.p3.spell_not_built"));
        }

        var cost = Magic.TrySpendMp(caster, spellIndex, out var reason, SpellSlots.For(encounter.Rules));
        if (cost < 0)
        {
            log.Add(GameText.F("core.magic.x_fails_to_cast_x", caster.Name, reason));
            return MagicResult.Fail(reason);
        }

        // [C] OV2 0x0a55 sets @94 on the combat spell-cost path; the Inn consumes it (MAGIC INCREASES).
        caster.CastSinceRest = true;
        // [D✓] s65: the only header DOS I prints; III names the spell (P3Fight.CastLine, root 0xbf9a [C]).
        P3Fight.AddCastLine(encounter, caster, spellIndex, log);

        if (spellIndex <= SpellId.HealingIv)
            return ApplyHealingSpell(encounter, caster, spellIndex, healTarget, cost, rng, log);

        if (spellIndex <= SpellId.FireflashIv)
            return ApplyFireflash(encounter, caster, spellIndex, cost, rng, log);

        if (spellIndex <= SpellId.QuicknessIv)
            return ApplyQuickness(encounter, spellIndex, cost, log);

        if (spellIndex <= SpellId.StrengthIv)
            return ApplyStrength(encounter, spellIndex, cost, log);

        if (spellIndex <= SpellId.ProtectionIv)
            return ApplyProtection(encounter, spellIndex, cost, log);

        if (spellIndex <= SpellId.ConfusionIv)
            return ApplyMonsterScale(encounter, caster, spellIndex,
                encounter.Rules.IsDos(RuleOptionRegistry.ConfusionIv) ? Magic.ConfusionPercentDos : Magic.ConfusionPercent,
                SpellId.ConfusionI, MonsterScaleKind.Confusion, cost, rng, log);

        if (spellIndex <= SpellId.WeaknessIv)
            return ApplyMonsterScale(encounter, caster, spellIndex, Magic.WeaknessPercent,
                SpellId.WeaknessI, MonsterScaleKind.Weakness, cost, rng, log);

        if (spellIndex <= SpellId.BindingIv)
            return ApplyMonsterScale(encounter, caster, spellIndex, Magic.BindingPercent,
                SpellId.BindingI, MonsterScaleKind.Binding, cost, rng, log);

        if (spellIndex <= SpellId.MindblastIv)
            return ApplyAoEDamage(encounter, caster, spellIndex, Magic.MindblastParams,
                SpellId.MindblastI, armorResist: false, cost, rng, log);

        if (spellIndex <= SpellId.FlameboltIv)
            return ApplyAoEDamage(encounter, caster, spellIndex, Magic.FlameboltParams,
                SpellId.FlameboltI, armorResist: true, cost, rng, log);

        return ApplySpecial(encounter, caster, spellIndex, cost, rng, log);
    }

    /// <summary>
    /// Non-combat CAST SPELL (categories E / N/*). Combat-only spells are rejected, and so are non-combat spells
    /// cast in the wrong place (<see cref="IsCastableAt"/>) — both before any MP is spent (D-035: manual; DOS lists
    /// every known spell in town and charges for ones that do nothing).
    /// <paramref name="healTarget"/> is only used by HEALING; RESURRECTION picks its own target.
    /// <paramref name="field"/> carries what MONSTER EVAL. and VISION read (OV2 <c>0x4790</c>, s41); without it MONSTER EVAL.
    /// reads terrain / tier 0 and VISION reveals nothing (the MP is still spent).
    /// </summary>
    public static MagicResult CastOutOfCombat(
        Party party,
        Character caster,
        int spellIndex,
        Character? healTarget,
        IDosRandomSource rng,
        CastLocation location = CastLocation.Town,
        FieldCastContext? field = null,
        GameOptions? rules = null)
    {
        rules ??= GameOptions.ManualPhantasieI;
        ArgumentNullException.ThrowIfNull(party);
        ArgumentNullException.ThrowIfNull(caster);
        ArgumentNullException.ThrowIfNull(rng);

        if (!party.Members.Contains(caster))
            return MagicResult.Fail(GameText.S("core.magic.caster_not_in_party"));
        // D-035 dos (p1.cast-list): DOS lists every known spell and refuses none here.
        var dosList = rules.IsDos(RuleOptionRegistry.CastList);
        if (!dosList && IsCombatOnly(spellIndex, rules))
            return MagicResult.Fail(GameText.S("core.magic.only_in_combat"));
        if (P3Spells.NotOnTheSharedCaster(rules, spellIndex)) // T4: III's own spells do nothing outside a fight here
            return MagicResult.Fail(GameText.S("core.p3.spell_not_built"));

        if (!dosList && !IsCastableAt(spellIndex, location))
            return MagicResult.Fail(GameText.S("core.magic.cant_cast_here"));

        var cost = Magic.TrySpendMp(caster, spellIndex, out var reason, SpellSlots.For(rules));
        if (cost < 0)
            return MagicResult.Fail(reason);

        // D-070 (manual): any cast counts toward the Inn's MAGIC INCREASES roll. DOS sets @94 only in combat (p1.cast-flag dos).
        // D3-069 (user s137, manual): III counts only casts "while in the wilderness", so a town cast does not.
        if (!rules.IsDos(RuleOptionRegistry.CastFlag) && !(rules.Game == GameId.P3 && location == CastLocation.Town))
            caster.CastSinceRest = true;

        // D-035 dos: the MP is spent before the switch; a spell with no case there does nothing, no message.
        if (dosList && !HasDosOutOfCombatEffect(spellIndex, location))
            return MagicResult.Ok("", mpSpent: cost, log: Array.Empty<string>());

        if (spellIndex <= SpellId.HealingIv)
        {
            var target = healTarget ?? caster;
            var tier = spellIndex + 1;
            // D-010 dos (p1.town-healing): the town routine (OV1 0x261a) draws a DOS word and has no floor.
            var heal = location == CastLocation.Town && rules.IsDos(RuleOptionRegistry.TownHealing)
                ? Magic.TownHealingAmountDos(tier, rng.NextDosWord())
                : Magic.HealingAmount(tier, rng.Next(0, 100));
            var gained = Magic.ApplyHealing(target, heal);
            P3Spells.CureBody(rules, target, spellIndex); // T4: III's healing cures body areas too
            // [D✓] s65: I's DOS prints nothing for a town / field HEALING. III prints "The character's health improves..."
            // once the target is picked (DOS root 0x19be in 0x194d; Amiga 0xfd42 in 0xfcbe, window 7; s163 static).
            return MagicResult.Ok(
                rules.Game == GameId.P3 ? GameText.S("core.p3.character_health_improves") : "",
                mpSpent: cost, hpRestored: gained);
        }

        if (spellIndex == SpellId.Resurrection)
        {
            // [C] Town OV1 0x2693 rolls rand/327 (0..100); wilderness/dungeon OV2 0x113b rolls rand/328 (0..99).
            Func<int> roll = location == CastLocation.Town
                ? () => rng.NextDosWord() / 327
                : () => Roll0To99(rng);
            var raised = TryResurrect(party, caster, roll);
            if (raised is not null)
                P3Spells.AfterResurrection(rules, raised, rng); // III: HP 1, magic full, body whole, CON −1…2
            // DOS prints nothing when no one is raised; the MP is spent either way.
            return raised is null
                ? MagicResult.Ok("", mpSpent: cost, log: Array.Empty<string>())
                : MagicResult.Ok(GameText.F("core.magic.x_is_resurrected", raised.Name), mpSpent: cost);
        }

        if (spellIndex == SpellId.MonsterEval)
        {
            // D-056 (user s42, manual p.15): the level of the monsters that can meet the party here, sometimes wrong.
            // With no monster pool in the context (bare tests), the DOS formula answers.
            var level = field?.NearbyMonsterLevels is { Count: > 0 } pool && !rules.IsDos(RuleOptionRegistry.MonsterEval)
                ? ManualMonsterEvalLevel(pool, rng)
                : MonsterEvalLevel(location, field?.TerrainSubcode ?? 0, field?.DungeonTier ?? 0);
            return MagicResult.Ok(GameText.F("core.magic.monsters_at_level_x", level), mpSpent: cost);
        }

        if (spellIndex == SpellId.Vision)
        {
            // IsCastableAt already limits VISION to the wilderness (D-035); DOS checks [0x64e8] == 2 itself and does
            // nothing in a dungeon. D-057 (user s42, manual p.16: "hundreds of miles"): the whole current region is
            // revealed. DOS reveals a square of VisionRadius (1–3 cells), kept below for reference.
            field?.Region?.RevealAround(field.X, field.Y,
                rules.IsDos(RuleOptionRegistry.Vision) ? VisionRadius(caster.MagicCur) : VisionManualRadius);
            return MagicResult.Ok("", mpSpent: cost, log: Array.Empty<string>());
        }

        return spellIndex switch
        {
            SpellId.Transportation => MagicResult.Ok("", mpSpent: cost), // [D✓] s65: no text; the destination picker follows
            // [C] (s41) DOS slot 57 has no case in the town (OV1 0x254e), field (OV2 0x4790) or combat dispatch:
            // the 2 MP are spent and nothing happens, no message.
            SpellId.DivineI => MagicResult.Ok("", mpSpent: cost, log: Array.Empty<string>()),
            _ => MagicResult.Ok(GameText.F("core.magic.cast_spell_x", spellIndex), mpSpent: cost),
        };
    }

    /// <summary>
    /// MONSTER EVAL. [C] (OV2 <c>0x4860</c>, s41): no roll and no monster is looked at. Wilderness:
    /// <c>2·(T + 1)</c> capped at 12, <c>T = [0x649c]</c> = tile % 10 of the party's cell. Dungeon:
    /// <c>2·tier + 2</c>, tier = level buffer byte <c>0x4e7</c>. DOS also stores it in the encounter-level word
    /// <c>[0x7508]</c>, which every encounter build resets, so it has no lasting effect.
    /// </summary>
    public static int MonsterEvalLevel(CastLocation location, int terrainSubcode, int dungeonTier)
    {
        if (location == CastLocation.Dungeon)
            return 2 * dungeonTier + 2;
        return Math.Min(2 * (terrainSubcode + 1), 12);
    }

    /// <summary>D-056: chance in 100 that MONSTER EVAL. reads wrong (user s57).</summary>
    public const int MonsterEvalErrorChance = 10;

    /// <summary>
    /// D-056 (manual: "determines the approximate experience level of nearby monsters … occasionally yields inaccurate
    /// results"): the rounded average level of the monsters that can be met at the party's spot. With chance
    /// <see cref="MonsterEvalErrorChance"/> the answer is off by 1 either way (never below 1). Rolls: one 0..99,
    /// then one 0..1 when wrong.
    /// </summary>
    public static int ManualMonsterEvalLevel(IReadOnlyList<int> nearbyLevels, IRandomSource rng)
    {
        ArgumentNullException.ThrowIfNull(nearbyLevels);
        ArgumentNullException.ThrowIfNull(rng);
        var level = (int)Math.Round(nearbyLevels.Average(), MidpointRounding.AwayFromZero);
        if (rng.Next(0, 100) < MonsterEvalErrorChance)
            level += rng.Next(0, 2) == 0 ? -1 : 1;
        return Math.Max(1, level);
    }

    /// <summary>D-057: VISION covers the whole 26×20 region from anywhere in it (no crossing into the next region).</summary>
    public const int VisionManualRadius = OverworldRegion.Width;

    /// <summary>
    /// VISION radius [C] (OV2 <c>0x48c8..0x48d8</c>, s41): <c>(MP·100 + 500) / 400</c> with the caster's MP left after the
    /// 1 MP cost — 0–2 → 1, 3–6 → 2, 7–10 → 3 … The reveal (<c>0x566b</c>) is a square of that radius, clipped to the region.
    /// </summary>
    public static int VisionRadius(int mpLeft) => (mpLeft * 100 + 500) / 400;

    /// <summary>
    /// RESURRECTION [C] (OV1 <c>0x2693</c> town, OV2 <c>0x113b</c> combat/wilderness/dungeon): no target
    /// choice. Walks the party in order; each DEAD member with CON &gt; 0 rolls once and the first roll below
    /// <c>50 + 5·(caster level − target level)</c> is raised: status OK, HP = max, CON untouched.
    /// At most one member per cast. Returns the raised member, or null.
    /// </summary>
    private static Character? TryResurrect(Party party, Character caster, Func<int> roll)
    {
        foreach (var t in party.Members)
        {
            if (t.Status != CharacterStatus.Dead || t.Stats.Con == 0)
                continue;
            if (roll() >= 50 + 5 * (caster.Level - t.Level))
                continue;
            t.Status = CharacterStatus.Ok;
            t.HpCur = t.HpMax;
            return t;
        }

        return null;
    }

    /// <summary>
    /// D-035 (manual p.12–13, spell table p.20): town casting only takes town (N/T) and everywhere (E) spells;
    /// MONSTER EVALUATE is N/WD (wilderness or dungeon), VISION N/W (wilderness), TRANSPORTATION N/T (town).
    /// Combat spells are handled by <see cref="IsCombatOnly"/>; anything else (E, DIVINE I) is allowed everywhere.
    /// </summary>
    public static bool IsCastableAt(int spellIndex, CastLocation location) => spellIndex switch
    {
        SpellId.MonsterEval => location is CastLocation.Field or CastLocation.Dungeon,
        SpellId.Vision => location == CastLocation.Field,
        SpellId.Transportation => location == CastLocation.Town,
        _ => true,
    };

    /// <summary>
    /// D-035 <c>dos</c>: the spells DOS's out-of-combat switch has a case for [C]: town (OV1 <c>0x254e</c>) HEALING I–IV,
    /// RESURRECTION, TRANSPORTATION; wilderness / dungeon (OV2 <c>0x4790</c>) HEALING I–IV, RESURRECTION, MONSTER EVAL.,
    /// and VISION (which does nothing in a dungeon, <c>0x48b3</c>). Every other spell only spends its MP.
    /// </summary>
    public static bool HasDosOutOfCombatEffect(int spellIndex, CastLocation location) =>
        spellIndex <= SpellId.HealingIv || spellIndex == SpellId.Resurrection || (location == CastLocation.Town
            ? spellIndex == SpellId.Transportation
            : spellIndex == SpellId.MonsterEval || (spellIndex == SpellId.Vision && location == CastLocation.Field));

    /// <summary>
    /// The spells out-of-combat Cast offers <paramref name="caster"/> here: the castable known spells (D-035 manual), or
    /// every known spell under <c>p1.cast-list</c> = <c>dos</c> (DOS's 60-slot picker, COM <c>0x1602</c>).
    /// </summary>
    public static IReadOnlyList<int> OutOfCombatChoices(Character caster, CastLocation location, GameOptions? rules = null)
    {
        ArgumentNullException.ThrowIfNull(caster);
        var dos = (rules ?? GameOptions.ManualPhantasieI).IsDos(RuleOptionRegistry.CastList);
        return Enumerable.Range(0, caster.SpellsKnown.Length)
            .Where(id => Magic.KnowsSpell(caster, id) && (dos || (!IsCombatOnly(id, rules) && IsCastableAt(id, location))))
            .ToList();
    }

    /// <summary>
    /// The spells the combat picker offers: combat-only and everywhere spells (D-063 manual: N spells can't be cast in
    /// combat), or every known spell under <c>p1.combat-spell-list</c> = <c>dos</c> (COM <c>0x139c</c>) or, in III,
    /// <c>p3.combat-spell-list</c> = <c>dos</c> (DOS III's picker; the pick is then refused, <see cref="P3CombatModule.CastRefusal"/>).
    /// </summary>
    public static bool IsCombatCastable(int spellIndex, GameOptions? rules = null) =>
        (rules ?? GameOptions.ManualPhantasieI) is var r && (r.IsDos(RuleOptionRegistry.CombatSpellList) || r.IsDos(P3RuleOptions.CombatSpellList))
        || IsCombatOnly(spellIndex, rules) || spellIndex <= SpellId.HealingIv || spellIndex == SpellId.Resurrection;

    /// <summary>
    /// Combat-only (C/A, C/P) spell. In III (<paramref name="rules"/>) #55 / #56 Accuracy / Party Accuracy (0-based 54 / 55,
    /// C/P in III's manual table) are combat-only too; its #13–16 Arrow Flame sit in I's Strength slots, also C/P.
    /// </summary>
    public static bool IsCombatOnly(int spellIndex, GameOptions? rules = null) =>
        (rules?.Game == GameId.P3 && spellIndex is P3Spells.Accuracy or P3Spells.PartyAccuracy)
        || spellIndex is (>= SpellId.FireflashI and <= SpellId.FlameboltIv)
            or SpellId.Charm or SpellId.Sleep or SpellId.Teleportation
            or SpellId.NinjaIi or SpellId.Fear or SpellId.Dissolve
            or SpellId.SummonElemental or SpellId.DispellUndead
            or SpellId.NinjaI or SpellId.Awaken;

    // --- combat helpers ------------------------------------------------------

    private static MagicResult ApplyHealingSpell(
        CombatEncounter encounter, Character caster, int spellIndex, int targetIndex,
        int cost, IRandomSource rng, List<string> log)
    {
        if ((uint)targetIndex >= (uint)encounter.Party.Members.Count)
            targetIndex = encounter.Party.Members.IndexOf(caster);
        if (targetIndex < 0) targetIndex = 0;

        var target = encounter.Party.Members[targetIndex];
        var tier = spellIndex + 1;
        var heal = Magic.HealingAmount(tier, Roll0To99(rng));
        var gained = Magic.ApplyHealing(target, heal);
        P3Spells.CureBody(encounter.Rules, target, spellIndex); // T4: III's healing cures body areas too
        var msg = GameText.F("core.magic.x_is_healed", target.Name); // [D✓] s65: DOS shows no amount
        log.Add(msg);
        return MagicResult.Ok(msg, mpSpent: cost, hpRestored: gained, log: log);
    }

    private static MagicResult ApplyFireflash(
        CombatEncounter encounter, Character caster, int spellIndex,
        int cost, IRandomSource rng, List<string> log)
    {
        var tier = Magic.TierIndex(spellIndex, SpellId.FireflashI);
        if (encounter.MonstersWiped)
        {
            log.Add(GameText.S("core.magic.no_target_for_fireflash"));
            return MagicResult.Ok(GameText.S("core.magic.no_target"), mpSpent: cost, log: log);
        }

        // DOS order: damage roll, then the target roll (OV2 0x321 → 0x361).
        var dmgRoll = Roll0To99(rng);
        var dmg = CombatMath.ApplyDamageFloor(encounter.Rules.IsDos(RuleOptionRegistry.FireflashDamage)
            ? Magic.FireflashDamageDos(dmgRoll, tier, caster.Stats.Int)
            : Magic.FireflashDamage(dmgRoll, tier, caster.Stats.Int));
        var target = encounter.Rules.IsDos(RuleOptionRegistry.FireflashTarget)
            ? PickFireflashTargetDos(encounter, rng)!
            : PickFireflashTarget(encounter, rng)!;
        // spellDamage: presentation only (I Amiga sparkles dmg / 32 + 1 times before the damage, h35 +0x4bc).
        CombatResolver.ApplyMonsterDamage(encounter, target, dmg, DamageSource.PartySpell, rng, log, spellIndex, dmg);
        return MagicResult.Ok(GameText.F("core.magic.x_fireflash_hits_x", caster.Name, target.Name), mpSpent: cost, log: log);
    }

    private static MagicResult ApplyQuickness(
        CombatEncounter encounter, int spellIndex, int cost, List<string> log)
    {
        var tier = Magic.TierIndex(spellIndex, SpellId.QuicknessI);
        var pct = Magic.QuicknessPercent[tier];

        for (var i = 0; i < encounter.Party.Members.Count; i++)
        {
            var ch = encounter.Party.Members[i];
            var scaled = (ch.Skills.Attack * (100 + pct)) / 100; // OV2 0x0498: @30 attack
            if (scaled > 255) scaled = 255;
            encounter.Prep[i].WeaponHitEff = scaled;
        }

        return MagicResult.Ok(GameText.S("core.spell.quickness"), mpSpent: cost, log: log);
    }

    private static MagicResult ApplyStrength(
        CombatEncounter encounter, int spellIndex, int cost, List<string> log)
    {
        var tier = Magic.TierIndex(spellIndex, SpellId.StrengthI);
        var flat = Magic.StrengthFlat[tier];

        for (var i = 0; i < encounter.Party.Members.Count; i++)
        {
            var ch = encounter.Party.Members[i];
            var dmg = ch.WeaponDamageA + ch.WeaponDamageB + flat;
            if (dmg > 255) dmg = 255;
            encounter.Prep[i].WeaponDamageEff = dmg;
        }

        return MagicResult.Ok(GameText.S("core.spell.strength"), mpSpent: cost, log: log);
    }

    private static MagicResult ApplyProtection(
        CombatEncounter encounter, int spellIndex, int cost, List<string> log)
    {
        var tier = Magic.TierIndex(spellIndex, SpellId.ProtectionI);
        var flat = Magic.ProtectionFlat[tier];

        for (var i = 0; i < encounter.Party.Members.Count; i++)
        {
            var ch = encounter.Party.Members[i];
            var armor = ch.ArmorSoak + flat;
            if (armor > 255) armor = 255;
            encounter.Prep[i].ArmorEff = armor;
        }

        return MagicResult.Ok(GameText.S("core.spell.protection"), mpSpent: cost, log: log);
    }

    private enum MonsterScaleKind { Confusion, Weakness, Binding }

    private static MagicResult ApplyMonsterScale(
        CombatEncounter encounter, Character caster, int spellIndex, int[] table,
        int firstOfLine, MonsterScaleKind kind, int cost, IRandomSource rng, List<string> log)
    {
        var pct = table[Magic.TierIndex(spellIndex, firstOfLine)];
        // OV2 0x1382: one roll per group, in rank order; a hit scales the group's shared record (0x7522 + g·0x39),
        // so every monster in the rank gets the same change. The loop runs over every built group (< [0x6492]), wiped
        // ones too, and draws the roll before any living test. A wiped group uses up its roll with no visible effect (s33).
        foreach (var rank in encounter.Ranks)
        {
            if (!CasterHitsGroup(encounter, caster, rank, rng) || rank.IsWiped)
                continue;

            foreach (var mon in rank.Members)
            {
                switch (kind)
                {
                    case MonsterScaleKind.Confusion: // OV2 0x1403: special-ability gate +0x1e; IV = 0 % per manual (D-014)
                        mon.SpecialChance = mon.SpecialChance * pct / 100;
                        mon.StatusFlags |= MonsterStatusFlags.Confused;
                        break;
                    case MonsterScaleKind.Weakness:
                        mon.AttackPower = mon.AttackPower * pct / 100;
                        mon.StatusFlags |= MonsterStatusFlags.Weakened;
                        break;
                    case MonsterScaleKind.Binding: // OV2 0x1429, made to always help (D-013)
                        mon.Defense = encounter.Rules.IsDos(RuleOptionRegistry.Binding)
                            ? mon.Defense * pct / 100 // DOS OV2 0x1429 (D-013 dos)
                            : Magic.BoundDefense(mon.Defense, pct);
                        mon.StatusFlags |= MonsterStatusFlags.Bound;
                        break;
                }
            }
        }
        // [D✓] s65: DOS prints nothing after the header for CONFUSION / WEAKNESS / BINDING (OV2 has no such strings).

        return MagicResult.Ok(kind.ToString(), mpSpent: cost, log: log);
    }

    /// <summary>
    /// CONFUSION / WEAKNESS / BINDING group roll, one per group: hit if <c>rand ≤ chance</c> with
    /// <c>chance = 5·(caster @16 MP max − group CV +0x25) + 2·(caster level @88 − group level +0x18) + 50</c>.
    /// <b>Remaster design (user, D-028), not DOS</b>: the manual says success depends on "magic power and experience level, as
    /// well as the strength of the target". DOS OV2 <c>0x1382</c> modes 1–3 (<c>0x13b5</c>, [C] s33) has no level term; at
    /// equal levels this is the DOS roll. Also used by the remaster SLEEP (D-020). <c>p1.spell-level</c> = <c>dos</c> drops the
    /// level term (DOS's roll).
    /// </summary>
    private static bool CasterHitsGroup(CombatEncounter encounter, Character caster, MonsterRank rank, IRandomSource rng)
    {
        var group = rank.Members[0];
        var weight = encounter.Rules.IsDos(RuleOptionRegistry.SpellLevel) ? 0 : CasterLevelWeight;
        var chance = CombatMath.CvHitChance(caster.MagicMax, group.CombatRating)
                     + weight * (caster.Level - group.Level);
        return CombatMath.HitsInclusive(Roll0To99(rng), chance);
    }

    /// <summary>D-028: points of hit chance per level the caster is above (or below) the target group.</summary>
    public const int CasterLevelWeight = 2;

    private static MagicResult ApplyAoEDamage(
        CombatEncounter encounter, Character caster, int spellIndex, int[] table,
        int firstOfLine, bool armorResist, int cost, IRandomSource rng, List<string> log)
    {
        var param = table[Magic.TierIndex(spellIndex, firstOfLine)];
        foreach (var mon in encounter.AllMonsters.Where(m => m.IsAlive).ToList())
        {
            var rolled = Magic.ScaledSpellDamage(Roll0To99(rng), param);
            var raw = rolled;
            if (armorResist)
                raw -= mon.Armor;
            var dmg = CombatMath.ApplyDamageFloor(raw);
            // rolled: presentation only (I Amiga sparkles before the armour, h38 +0x1a6 / +0x1f6 -> h53 +0xab8).
            CombatResolver.ApplyMonsterDamage(encounter, mon, dmg, DamageSource.PartySpell, rng, log, spellIndex, rolled);
        }

        return MagicResult.Ok(GameText.F("core.magic.x_aoe_spell", caster.Name), mpSpent: cost, log: log);
    }

    private static MagicResult ApplySpecial(
        CombatEncounter encounter, Character caster, int spellIndex,
        int cost, IRandomSource rng, List<string> log)
    {
        switch (spellIndex)
        {
            case SpellId.Charm:
                CombatSpells.Charm(encounter, caster, rng, log);
                return MagicResult.Ok(GameText.S("core.spell.charm"), mpSpent: cost, log: log);

            case SpellId.Sleep:
            {
                // Remaster (D-020, manual): one caster-vs-CV roll per group, as CONFUSION/WEAKNESS/BINDING; the group's
                // awake monsters fall asleep (DOS state 1: no turns, wake on rand < 15 per pass, still hittable).
                // A second SLEEP doesn't deepen it. DOS 0x1501 instead: per monster resisted if rand < L², and a slept
                // monster only loses the rest of the round (state 2).
                if (encounter.Rules.IsDos(RuleOptionRegistry.Sleep))
                {
                    // D-020 dos (p1.sleep): 0x1382 mode 6 → 0x1501, per living slot in rank order: one roll drawn and
                    // discarded (×0 power), then r; r ≥ L² → "%s IS SLEPT", done this round. L ≥ 10 never works.
                    var l2 = encounter.MonsterLevel * encounter.MonsterLevel;
                    foreach (var rank in encounter.Ranks.Where(r => !r.IsWiped))
                        foreach (var mon in rank.Living.ToList())
                        {
                            _ = Roll0To99(rng);
                            if (Roll0To99(rng) < l2 || mon.HitPoints < 1)
                                continue;
                            mon.DoneThisRound = true;
                            encounter.NoteHit(new CombatHit(DamageSource.PartySpell, null, mon), log);
                            log.Add(GameText.F("core.combat.x_is_slept", mon.Name));
                        }
                    return MagicResult.Ok(GameText.S("core.spell.sleep"), mpSpent: cost, log: log);
                }
                foreach (var rank in encounter.Ranks.Where(r => !r.IsWiped))
                {
                    if (!CasterHitsGroup(encounter, caster, rank, rng))
                        continue;
                    foreach (var mon in rank.Living.Where(m => (m.StatusFlags & MonsterStatusFlags.Asleep) == 0))
                    {
                        mon.StatusFlags |= MonsterStatusFlags.Asleep;
                        // III Amiga 0x181b8 (spell 42 = Sleep): a monster that fails to resist gets status 2 and goes
                        // through damage_monster 0x1833e with 0 damage and the flash flag set: its flash, no line (s125).
                        encounter.NoteHit(new CombatHit(DamageSource.PartySpell, null, mon), log);
                        log.Add(GameText.F("core.combat.x_is_slept", mon.Name));
                    }
                }
                return MagicResult.Ok(GameText.S("core.spell.sleep"), mpSpent: cost, log: log);
            }

            case SpellId.Teleportation:
                if (encounter.Rules.IsDos(RuleOptionRegistry.Teleport))
                    CombatSpells.TeleportDos(encounter, caster, rng); // D-023 dos
                else
                    CombatSpells.Teleport(encounter);
                return MagicResult.Ok(GameText.S("core.spell.teleportation"), mpSpent: cost, log: log);

            case SpellId.Resurrection:
            {
                // [C] OV2 0x113b, roll = rand/328 (0..99). Side writes [C] (s41): order [0x74fa+2i] := 4 (no action — the
                // raised member doesn't act for the rest of the round) and its previous-order mirror [0x5b96+2i] := 4 (next
                // round's default order is PARRY; Core has no default-order model); 0x43ae rebuilds the non-DEAD target
                // list [0x64a2] and [0x6490]++ is the survivor count — both derived from status in Core (Survivors).
                var raised = TryResurrect(encounter.Party, caster, () => Roll0To99(rng));
                if (raised is not null)
                {
                    P3Spells.AfterResurrection(encounter.Rules, raised, rng); // III: HP 1, magic full, body whole, CON −1…2
                    encounter.LivingWordIsCount = true; // 0x11d7 stores the living count [~] (success path)
                    var slot = encounter.Party.Members.IndexOf(raised);
                    if ((uint)slot < (uint)encounter.Prep.Length)
                        encounter.Prep[slot].PackedAction = CombatAction.Cleared;
                    log.Add(GameText.F("core.magic.x_is_resurrected", raised.Name));
                }
                return MagicResult.Ok(GameText.S("core.spell.resurrection"), mpSpent: cost, log: log);
            }

            case SpellId.Fear:
                CombatSpells.Fear(encounter, caster, rng, log);
                return MagicResult.Ok(GameText.S("core.spell.fear"), mpSpent: cost, log: log);

            case SpellId.Dissolve:
                CombatSpells.CastDissolve(encounter, rng, log);
                return MagicResult.Ok(GameText.S("core.spell.dissolve"), mpSpent: cost, log: log);

            case SpellId.DispellUndead:
                CombatSpells.DispellUndead(encounter, caster, rng, log);
                return MagicResult.Ok(GameText.S("core.spell.dispell_undead"), mpSpent: cost, log: log);

            case SpellId.Awaken:
                // OV2 0x9bf → 0x3eff [C]: every non-DEAD member := OK, one "PARTY AWAKENS" line. Manual #51: "rouses
                // all sleeping party members or neutralizes a Sleep spell". Woken members keep action 4 (no action) this round.
                // III (0xca27 sets FAR:0xb5c0): the sleepers wake at the end of the round (P3RoundResolver).
                if (P3Fight.Of(encounter) is { } p3Fight)
                    p3Fight.AwakenAtRoundEnd = true;
                else
                {
                    CombatEncounter.WakeParty(encounter.Party);
                    log.Add(GameText.S("core.combat.party_awakens"));
                }
                return MagicResult.Ok(GameText.S("core.spell.awaken"), mpSpent: cost, log: log);

            case SpellId.NinjaI:
            case SpellId.NinjaIi:
                CombatSpells.Ninja(encounter.Prep[encounter.Party.Members.IndexOf(caster)],
                    spellIndex == SpellId.NinjaI ? CombatSpells.NinjaI : CombatSpells.NinjaIi);
                return MagicResult.Ok(GameText.S("core.spell.ninja"), mpSpent: cost, log: log);

            case SpellId.SummonElemental:
                // [C] OV2 0x8cc: no roll, no message (only the party panel redraws); MP spent either way (0x9f7).
                // III (root 0xc912) prints `core.p3.elemental_begins_to_form` when it comes; I prints nothing.
                if (CombatSpells.SummonElemental(encounter) && encounter.Rules.Game == GameId.P3)
                    log.Add(GameText.S("core.p3.elemental_begins_to_form"));
                return MagicResult.Ok(GameText.S("core.spell.summon_elemental"), mpSpent: cost, log: log);

            case SpellId.DivineI:
                // [C] (s41) cast_id = slot 57 − 3 = 54: no tier range, fails the special bound (0x9e7) → only the MP deduct.
                return MagicResult.Ok("", mpSpent: cost, log: log);

            case SpellId.DivineIi when encounter.Rules.Game == GameId.P2:
                // II's DIVINE II (index 55 in II's spell layout, SpellSlots.PhantasieII): the ST's combat switch 0x8560 has
                // no case for slot 56, so "%s CASTS A SPELL" and the 1 MP only (DIFFERENCES-II D-063 row, s95).
                return MagicResult.Ok("", mpSpent: cost, log: log);

            case SpellId.MonsterEval or SpellId.Vision or SpellId.Transportation
                when encounter.Rules.IsDos(RuleOptionRegistry.CombatSpellList):
                // D-063 dos ([C] OV2 0x6d75): "%s CASTS A SPELL", the MP is spent, nothing happens.
                return MagicResult.Ok("", mpSpent: cost, log: log);

            default:
                log.Add(GameText.F("core.magic.spell_x_no_combat_effect", spellIndex));
                _ = rng;
                _ = caster;
                return MagicResult.Ok(GameText.F("core.magic.spell_x", spellIndex), mpSpent: cost, log: log);
        }
    }

    /// <summary>
    /// D-059 (user s53, manual "one monster close to the spellcaster"): a living monster of the front rank — the first
    /// rank in rank order with one alive — picked evenly. DOS walks from the back rank instead (OV2 <c>0x361</c>).
    /// </summary>
    public static Monster? PickFireflashTargetDos(CombatEncounter encounter, IRandomSource rng)
    {
        // DOS OV2 0x361 (combat-resolution.md §FIREFLASH target; D-059 dos): the rearmost group with a living monster;
        // k = rand0_99 · L / 100 over its compact living list (L = last index), so in a group of 2+ the last monster is
        // never hit. One roll, a group of one included.
        var back = encounter.Ranks.LastOrDefault(r => !r.IsWiped);
        if (back is null) return null;
        var living = back.Living.ToList();
        return living[Roll0To99(rng) * (living.Count - 1) / 100];
    }

    public static Monster? PickFireflashTarget(CombatEncounter encounter, IRandomSource rng)
    {
        var front = encounter.Ranks.FirstOrDefault(r => !r.IsWiped);
        if (front is null) return null;
        var living = front.Living.ToList();
        if (living.Count == 1) return living[0];
        return living[rng.Next(0, living.Count)];
    }

    private static int Roll0To99(IRandomSource rng) => rng.Next(0, 100);

    private static string Roman(int tier) => tier switch
    {
        1 => "I",
        2 => "II",
        3 => "III",
        4 => "IV",
        _ => tier.ToString(),
    };
}

/// <summary>
/// What the wilderness/dungeon cast routine (OV2 <c>0x4790</c>) reads besides the party: the tile class
/// <c>T = [0x649c]</c> (tile % 10 of the party's cell) and the dungeon level tier (byte <c>0x4e7</c>) for MONSTER EVAL.,
/// and the current region + party cell (<c>[0x751a]</c>/<c>[0x751c]</c>) for VISION.
/// </summary>
public sealed record FieldCastContext(
    int TerrainSubcode = 0,
    int DungeonTier = 0,
    OverworldRegion? Region = null,
    int X = 0,
    int Y = 0,
    IReadOnlyList<int>? NearbyMonsterLevels = null);

/// <summary>Where an out-of-combat spell is cast — DOS uses different code (and RNG scaling) per site.</summary>
public enum CastLocation
{
    /// <summary>Town Option → Cast spell (OV1 <c>0x254e</c>).</summary>
    Town,
    /// <summary>Wilderness cast (OV2 <c>0x4790</c>).</summary>
    Field,
    /// <summary>Dungeon cast (OV2 <c>0x4790</c>, same routine; split out for the manual's N/W vs N/WD rule, D-035).</summary>
    Dungeon,
}
