namespace OpenPhantasie.Core;

/// <summary>
/// The values of the I / II → III transfer (III's <c>rule-tables.json</c> <c>transfer</c>, <c>FORMAT.md</c> section 6).
/// </summary>
/// <param name="SocialClass">The class DOS writes after creation: 3, Noble.</param>
/// <param name="XpPerLevel">XP = this × the old level.</param>
/// <param name="BankGoldDivisor">Bank gold += the old bank gold ÷ this.</param>
/// <param name="DosUndeadLevel">DOS's level for an Undead (I's race 15 → III's 17).</param>
/// <param name="WandItem">The Wand of Nikademus, III item 180.</param>
/// <param name="WandQuestBit">Quest bit 2 of <c>+0x8a</c>: DOS III's "has the wand" (<c>dungeon-events.md</c> section 4).</param>
/// <param name="WandScoreAbove">A source character whose Phantasie I score is above this held the wand (D2-001).</param>
public sealed record P3TransferRule(
    int SocialClass, int XpPerLevel, int BankGoldDivisor, int DosUndeadLevel, int WandItem, int WandQuestBit,
    int WandScoreAbove);

/// <summary>The transfer's rules options (<see cref="P3RuleOptionsExtensions.P3Transfer"/>; D3-001…D3-007).</summary>
public sealed record P3TransferRules(bool KeepWand, bool KeepDivine, bool EraseRoster, bool FromIIAllowed, bool DosLevels)
{
    /// <summary>The manual's transfer (the Original (manual) and Remaster presets).</summary>
    public static P3TransferRules Manual { get; } = new(true, true, false, true, false);

    /// <summary>DOS III's own transfer.</summary>
    public static P3TransferRules Dos { get; } = new(false, false, false, false, true);
}

public sealed partial class Phantasie3Campaign
{
    /// <summary>
    /// D3-001: the transfer keeps the wand for everyone when any character of the source roster held it (Apple III line
    /// 1001 counts the whole roster): a Phantasie I score above <see cref="P3TransferRule.WandScoreAbove"/>, the flag every
    /// I → II converter keeps and II shows as "WAND OF NIK" (D2-001, <see cref="TransferIToII.HasWandOfNikademus"/>).
    /// </summary>
    public bool SourceHasWand(IEnumerable<Character?> sourceRoster)
    {
        ArgumentNullException.ThrowIfNull(sourceRoster);
        var t = World.Transfer ?? throw new InvalidOperationException("III's data has no transfer block.");
        return sourceRoster.OfType<Character>().Any(c => c.Score > t.WandScoreAbove);
    }

    /// <summary>
    /// One character, Phantasie I or II → III (<c>FORMAT.md</c> section 6; DOS ovl1 with the creation routine ovl2
    /// <c>0x227d</c> in transfer mode). Kept from the source: name, the six attributes, race and class (same-named in Core;
    /// DOS's <c>DS:0x4e</c> / <c>DS:0x48</c> maps), status, age (and lifespan, D3-041), deeds. Rolled as at creation, in DOS's
    /// order: HP (before or after the attributes as <c>p3.hit-points</c> says; no attributes are rolled), the social-class
    /// roll whose gold goes into the bank, the age roll (drawn, then overwritten). Fresh: the class kit worn, the first
    /// spell, magic at 75 % of the level-1 ceiling, the nine skills, all body areas Okay, rank Front. Then DOS's ovl1
    /// writes: social class Noble, XP = 100 × old level, bank gold += old ÷ 35, town Pendragon, the I score at
    /// <c>+0x5c</c>, and the origin.
    /// <para>
    /// The manual's rules (<see cref="P3TransferRules"/>): level 0 for all (D3-007; DOS 1, Undead 20); DIVINE I / II become
    /// Divine Aid (D3-002, the manual's "#57 and above"; DOS none); the wand item and quest bit 2 for
    /// <paramref name="wand"/> (D3-001; DOS none). A II character also brings II's quest score to <c>+0x18</c> (the Filmon
    /// and archive tests, <c>FORMAT.md</c> section 5) and the origin Ferronrah unless II's own record says Gelnor (the
    /// character came to II from I); DOS's II path is unreachable, Apple III's not decoded.
    /// </para>
    /// </summary>
    /// <param name="from">The source character; never changed.</param>
    /// <param name="source">The game it comes from: <see cref="GameId.P1"/> or <see cref="GameId.P2"/>.</param>
    /// <param name="wand">The source roster held the wand (<see cref="SourceHasWand"/>); ignored unless the rules keep it.</param>
    public Character AdmitTransferred(GameSession session, Character from, GameId source, P3TransferRules rules, bool wand)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(rules);
        if (source is not (GameId.P1 or GameId.P2))
            throw new ArgumentOutOfRangeException(nameof(source), source, "III takes characters from Phantasie I or II.");
        var w = World;
        var t = w.Transfer ?? throw new InvalidOperationException("III's data has no transfer block.");
        var rng = session.CampaignAccess.Rng;
        var options = session.Options;
        var tables = w.TrainingFor(options);
        var creation = session.CreationTables;
        var cls = from.Class;
        var race = from.Race;

        var c = new Character
        {
            Name = from.Name,
            Race = race,
            BirthRace = from.BirthRace,
            Class = cls,
            Stats = from.Stats,
            Status = from.Status,
            Level = 1,
            Score = 0,
            Deeds = from.Deeds,
            Lineage = from.Lineage,
        };

        // ovl2 0x227d in transfer mode: HP dice, the social-class roll (its gold), [no attributes], the age roll. Under the
        // manual's HP (D3-022, CON counts) the HP dice come after the attributes' place, as in RollNewMember.
        var hpFirst = options.P3HpIgnoresCon();
        if (hpFirst)
            c.HpMax = P3Training.CreationHp(cls, 0, tables, rng, options);
        var p3 = c.P3;
        var die = w.SocialClassBaseRaces.Contains(race) ? w.SocialClassRollDie.BaseRaces : w.SocialClassRollDie.Others;
        var roll = P3Dice.Rnd(rng, die);
        var rolledClass = w.SocialClassThresholds.Count(th => roll >= th);
        if (!hpFirst)
            c.HpMax = P3Training.CreationHp(cls, c.Stats.Con, tables, rng, options);
        c.HpMax = c.HpCur = Math.Max(1, c.HpMax);
        _ = P3Dice.Rnd(rng, 7); // the age roll (ovl2 0x2721); ovl1 then writes the old age
        c.SkillBonuses = CharacterCreation.SkillBonusSeeds(c.Stats);
        c.MpCeiling = creation.MpCeilingByClass[(int)cls][0];
        c.MagicMax = c.MagicCur = (3 * c.MpCeiling) >> 2;
        p3.Rank = P3PartyRank.Front;
        c.SpellsKnown = new int[w.SpellCount];
        if (w.FirstSpell.TryGetValue(cls, out var first) && first > 0)
            c.SpellsKnown[first - 1] = 1;
        GiveKit(session, c);
        SetSkills(c, tables);
        c.BodyCondition = new P3BodyAreas().ToCondition();

        // ovl1 after the creation routine.
        c.AgeClock = from.AgeClock;
        c.HasAged = from.HasAged;
        c.Lifespan = options.P3NoLifespans() ? 0
            : from.Lifespan > 0 ? from.Lifespan
            // QoL shared-aging: a source with no lifespan gets the shared band of its living race, luck clamped 8–13
            : options.SharedAging ? CharacterCreation.LifespanForImported(from, creation)
            : creation.LifespanBandsByRace.Count > 0 ? CharacterCreation.LifespanFor(race, from.Stats.Luck, creation) : 0;
        c.Experience = (long)t.XpPerLevel * Math.Max(0, from.Level);
        c.Level = !rules.DosLevels ? 0 : race == Race.Undead ? t.DosUndeadLevel : 1;
        c.AddBankGold(P3Training.StartingGold(rolledClass) + Math.Max(0, from.GoldInBank) / t.BankGoldDivisor,
            session.Party.BankGoldCap);
        c.CurrentTown = HomeTownId;
        p3.SocialClass = t.SocialClass;
        p3.Phantasie1Score = from.Score;
        p3.Origin = source == GameId.P2 && from.Phantasie2?.Origin != Phantasie2Origin.Gelnor
            ? Phantasie3Origin.Ferronrah
            : Phantasie3Origin.Gelnor;
        if (source == GameId.P2)
            p3.Phantasie2Score = from.Phantasie2?.QuestScore ?? 0;

        if (rules.KeepDivine && (Magic.KnowsSpell(from, SpellId.DivineI) || Magic.KnowsSpell(from, SpellId.DivineIi)))
            p3.DivineAid = true;
        if (rules.KeepWand && wand)
        {
            c.InventoryItemIds.Add(t.WandItem);
            p3.QuestBits |= 1 << t.WandQuestBit;
        }
        MarkLearnableSpells(session, c);
        return c;
    }

    /// <summary>The class kit (ovl2 <c>0x227d</c>): carried <c>[shield, armour, bow, weapon]</c>, all worn.</summary>
    private void GiveKit(GameSession session, Character c)
    {
        var w = World;
        var bow = w.KitBow.TryGetValue(c.Class, out var b) ? b : 0;
        c.InventoryItemIds.AddRange([w.Kit.Shield, w.Kit.Armor, bow, w.Kit.Weapon]);
        c.WornShield = w.Kit.Shield;
        c.WornArmor = w.Kit.Armor;
        c.WornBow = bow;
        c.WornWeapon = w.Kit.Weapon;
        CombatGear.ApplyWornItemPoints(c, id => ItemPoints(session, id));
    }
}
