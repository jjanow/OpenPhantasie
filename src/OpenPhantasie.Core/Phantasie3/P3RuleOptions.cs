namespace OpenPhantasie.Core;

/// <summary>
/// Phantasie III's rules options (T4, <c>DIFFERENCES-III.md</c> D3-010…D3-036 without the transfer entries). Each is the
/// manual's rule by default (Original (manual) and Remaster presets) and DOS III's behaviour as the <c>dos</c> value
/// (Original (DOS) preset), unless noted. Listed in <see cref="RuleOptionRegistry.All"/> through <see cref="All"/>; kept in
/// their own class so their fields are initialised before the registry's list reads them. Typed accessors:
/// <see cref="P3RuleOptionsExtensions"/>.
/// </summary>
public static class P3RuleOptions
{
    /// <summary>D3-010: Slash below Attack % 150 becomes an Attack (<c>manual</c>) or DOS's Thrust (<c>dos</c>).</summary>
    public static readonly RuleOptionDescriptor SlashFallback = ManualOrDos("p3.slash-fallback", "D3-010");

    /// <summary>D3-011: Lunge for Fighters only (<c>manual</c>) or for Thieves too (<c>dos</c>, root <c>0xfc41</c>).</summary>
    public static readonly RuleOptionDescriptor Lunge = ManualOrDos("p3.lunge", "D3-011");

    /// <summary>D3-012: a Thief's Attack picks the rank like Thrust and Slash (<c>manual</c>) or is rank 1 only (<c>dos</c>).</summary>
    public static readonly RuleOptionDescriptor ThiefAttack = ManualOrDos("p3.thief-attack", "D3-012");

    /// <summary>
    /// D3-013: Aim needs Attack % ≥ 150 (else Thrust) and hits at Attack % / 2 (<c>manual</c>, user values s81) or is always
    /// allowed at the full Attack % (<c>dos</c>).
    /// </summary>
    public static readonly RuleOptionDescriptor Aim = ManualOrDos("p3.aim", "D3-013");

    /// <summary>D3-014: Healing IV and Healing Potion 10 spend 6 cure points (<c>manual</c>) or DOS's 4 (<c>dos</c>).</summary>
    public static readonly RuleOptionDescriptor HealingIv = ManualOrDos("p3.healing-iv", "D3-014");

    /// <summary>
    /// D3-031: each cure takes the worst affordable area, the most vital among equals, starting over every time
    /// (<c>manual</c>), or DOS's pass that goes on from the next area after a cure (<c>dos</c>, root <c>0xaafc</c>).
    /// </summary>
    public static readonly RuleOptionDescriptor HealOrder = ManualOrDos("p3.heal-order", "D3-031");

    /// <summary>
    /// D3-015: every inn stay cures 1 Break or 2 Injuries (<c>manual</c>) or a town inn sets every area that is not Gone to
    /// Okay (<c>dos</c>, ovl2 <c>0x347d</c> with the in-town flag).
    /// </summary>
    public static readonly RuleOptionDescriptor InnHealing = ManualOrDos("p3.inn-healing", "D3-015");

    /// <summary>
    /// D3-017: Arrow Flame I / II add 15 / 25 to the bow's points, which feed the normal range <c>2·points + 4</c>
    /// (<c>manual</c>), or replace the range with <c>points + 15 / 25</c> (<c>dos</c>, root <c>0xc3cc</c>).
    /// </summary>
    public static readonly RuleOptionDescriptor ArrowFlame = ManualOrDos("p3.arrow-flame", "D3-017");

    /// <summary>
    /// D3-039 (user s96): a bow spell never lowers the Fire Bow % or damage an earlier one gave (<c>manual</c>: Accuracy
    /// makes it at least 250, the higher Arrow Flame stays) or the last spell sets the % (<c>dos</c>, root <c>0xc3cc</c> /
    /// <c>0xca34</c>).
    /// </summary>
    public static readonly RuleOptionDescriptor BowSpells = ManualOrDos("p3.bow-spells", "D3-039");

    /// <summary>
    /// D3-018: armour lowers a monster's melee chance to hit (<c>manual</c>, <c>+ 5·armour</c> in the defence) or raises it,
    /// as DOS's inverted sign (<c>dos</c>, root <c>0xd730</c>, verified live s81).
    /// </summary>
    public static readonly RuleOptionDescriptor ArmourDefence = ManualOrDos("p3.armour-defence", "D3-018");

    /// <summary>
    /// D3-019: Down and Out (monsters also Unconscious) targets are hit at double the normal chance, capped at 95 %
    /// (<c>manual</c>, user values s81), or always (<c>dos</c>). Asleep targets are always hit either way.
    /// </summary>
    public static readonly RuleOptionDescriptor DownTargets = ManualOrDos("p3.down-targets", "D3-019");

    /// <summary>D3-020: the party's found-item list holds 90 (<c>manual</c>), any number (<c>unlimited</c>) or DOS's 60 (<c>dos</c>).</summary>
    public static readonly RuleOptionDescriptor FoundItems = new(
        "p3.found-items", GameId.P3, ["manual", "unlimited", "dos"],
        Manual: "manual", Dos: "dos", Remaster: "manual", Difference: "D3-020");

    /// <summary>D3-021: an unconscious monster shows <c>U</c> (<c>manual</c>) or DOS's <c>O</c> (<c>dos</c>). Display only.</summary>
    public static readonly RuleOptionDescriptor StatusLetter = ManualOrDos("p3.status-letter", "D3-021");

    /// <summary>
    /// D3-022: hit dice scale with Constitution by Phantasie I's rule <c>dice·(CON + 5)/15</c> (<c>manual</c>; CON 10 = the
    /// chart) or ignore it (<c>dos</c>, ovl2 <c>0x227d</c> / <c>0x1031</c>).
    /// </summary>
    public static readonly RuleOptionDescriptor HitPoints = ManualOrDos("p3.hit-points", "D3-022");

    /// <summary>D3-023: each level gained pays social class · 256 gold into the bank account (<c>manual</c>) or nothing (<c>dos</c>).</summary>
    public static readonly RuleOptionDescriptor LevelGold = ManualOrDos("p3.level-gold", "D3-023");

    /// <summary>
    /// D3-024: Greet, Beg for Mercy, Accept Surrender, monsters fleeing and the party's Flee work for every monster
    /// (<c>manual</c>) or are blocked when the rank-1 monster id is 44 or more (<c>dos</c>, root <c>0x112e9</c> …).
    /// </summary>
    public static readonly RuleOptionDescriptor EncounterBlock = ManualOrDos("p3.encounter-block", "D3-024");

    /// <summary>
    /// D3-024: the final bosses (ids 77–79, Nikademus and Lord Wood) block the party's Flee (<c>blocked</c>, every preset,
    /// as I / II and DOS) or can be fled like any monster (<c>allowed</c>, a player choice).
    /// </summary>
    public static readonly RuleOptionDescriptor BossFlee = new(
        "p3.boss-flee", GameId.P3, ["blocked", "allowed"],
        Manual: "blocked", Dos: "blocked", Remaster: "blocked", Difference: "D3-024");

    /// <summary>
    /// D3-024 (user s89): a successful Beg for Mercy takes all the gold and each item found since the party last left town
    /// with a 50 % chance (<c>manual</c>, a remaster rule: no version takes items) or the gold only (<c>dos</c>, DOS /
    /// Amiga / Apple II).
    /// </summary>
    public static readonly RuleOptionDescriptor Mercy = ManualOrDos("p3.mercy", "D3-024");

    /// <summary>
    /// D3-025: Deposit splits the party gold by 1 / 2 / 3 shares per adventurer with Apple II's share maths as the manual's
    /// "small banking fee" (<c>manual</c>, user s89: each account gets <c>⌊S · gold / (ΣS + 0.00001)⌋</c>, the rest is
    /// lost), the same shares with no fee (<c>no-fee</c>, Phantasie I's deposit), or evenly with `p3town.bank_withdraw_all`
    /// (<c>dos</c>, ovl2 <c>0x297a</c>; DOS and Amiga take no fee).
    /// </summary>
    public static readonly RuleOptionDescriptor Bank = new(
        "p3.bank", GameId.P3, ["manual", "no-fee", "dos"],
        Manual: "manual", Dos: "dos", Remaster: "manual", Difference: "D3-025");

    /// <summary>
    /// D3-026: party gold and each account are capped at 65,000 (<c>manual</c>; the number comes from III's rules profile)
    /// or not at all (<c>dos</c>: 32-bit fields, read as <see cref="GoldCapRule.Lifted"/>).
    /// </summary>
    public static readonly RuleOptionDescriptor GoldCap = ManualOrDos("p3.gold-cap", "D3-026");

    /// <summary>
    /// D3-027: Trade Items gives to a member or sells as "adventurer 7" at Distribute's price (<c>manual</c>, user s89),
    /// gives only (<c>give-only</c>, the Amiga program's Trade), or does not exist (<c>dos</c>).
    /// </summary>
    public static readonly RuleOptionDescriptor Trade = new(
        "p3.trade", GameId.P3, ["manual", "give-only", "dos"],
        Manual: "manual", Dos: "dos", Remaster: "manual", Difference: "D3-027");

    /// <summary>
    /// D3-033 (user s89): every town entry re-rolls weapons and potions by Apple II's rule without the Light Key and the
    /// Burnt Gem (<c>manual</c>; <see cref="P3ArmoryRestock"/>) or the stock changes only by buying and selling
    /// (<c>dos</c>, DOS / Amiga).
    /// </summary>
    public static readonly RuleOptionDescriptor ArmoryRestock = ManualOrDos("p3.armory-restock", "D3-033");

    /// <summary>
    /// D3-036 (user s89): this round's Parry rolls the Parry skill; a success takes the shield's points off the monsters'
    /// melee chances that round and off the first damaging melee hit (<c>manual</c>, Apple II plus the hit chance), or
    /// DOS's saved option: the shield points come off the chance all fight when the member's last action of the previous
    /// fight was Parry, with no damage reduction (<c>dos</c>, root <c>0xb848</c> / <c>0xd4ff</c>; Amiga the same).
    /// </summary>
    public static readonly RuleOptionDescriptor Parry = ManualOrDos("p3.parry", "D3-036");

    /// <summary>D3-028: the game saves itself every time the party leaves a town (<c>manual</c>) or only from the menu (<c>dos</c>).</summary>
    public static readonly RuleOptionDescriptor AutoSave = ManualOrDos("p3.autosave", "D3-028");

    /// <summary>
    /// D3-029: the Guild roster has no limit (<c>manual</c>, "dozens") or DOS's 20 records, 3 of them the elementals, so 17
    /// characters (<c>dos</c>). The every-game <c>guild-limit</c> = <c>none</c> lifts the DOS limit too.
    /// </summary>
    public static readonly RuleOptionDescriptor Roster = ManualOrDos("p3.roster", "D3-029");

    /// <summary>
    /// D3-032: skill training uses Phantasie I's values for the five cells of III's tables that look like data errors
    /// (<c>manual</c>, user s82; <c>rule-tables.json</c> <c>skill_table_fixes</c>) or DOS's tables as read (<c>dos</c>).
    /// </summary>
    public static readonly RuleOptionDescriptor SkillTables = ManualOrDos("p3.skill-tables", "D3-032");

    /// <summary>
    /// The Pendragon archive altar (<c>dungeon-events.md</c> §6): "grants you the spell of Divine Aid" does what its text
    /// says (<c>fixed</c>: the guard and the write use <c>+0xf0</c>, Divine Aid) or DOS's data fault (<c>dos</c>: field 120
    /// = <c>+0xf8</c>, every living member's Torso Injured, no spell). Manual silent: not a conflict, a DOS bug fixed by
    /// default (s83 brief).
    /// </summary>
    public static readonly RuleOptionDescriptor Altar = new(
        "p3.altar", GameId.P3, ["fixed", "dos"], Manual: "fixed", Dos: "dos", Remaster: "fixed");

    /// <summary>
    /// Dungeon event gold (ops 7–10, <c>dungeon-events.md</c> §3): amounts are <c>a1 · 256</c> unsigned (<c>fixed</c>) or
    /// DOS's sign-extended word (<c>dos</c>: a1 ≥ 128 turns negative, so the Gnome wizard's closing op 9 gives 256 gold
    /// back instead of taking 65,280). Manual silent: a DOS bug fixed by default (s83 brief).
    /// </summary>
    public static readonly RuleOptionDescriptor EventGold = new(
        "p3.event-gold", GameId.P3, ["fixed", "dos"], Manual: "fixed", Dos: "dos", Remaster: "fixed");

    /// <summary>
    /// D3-038 (decided s96, manual default): leaving a dungeon with NO to `core.prompt.save_dungeon_map` forgets that dungeon's saved map
    /// (<c>manual</c>, I's D-027 reading: an old save can't come back) or keeps it, as DOS writes nothing (<c>dos</c>).
    /// </summary>
    public static readonly RuleOptionDescriptor DungeonMapNo = ManualOrDos("p3.dungeon-map-no", "D3-038");

    /// <summary>
    /// D3-041 (user s99): old age follows the manual with I's lifespans (D-003 bands; decline past 70 %, death past the
    /// lifespan, on town entry) (<c>manual</c>), or there is none, as DOS III never reads its maximum age (<c>dos</c>).
    /// </summary>
    public static readonly RuleOptionDescriptor Lifespans = ManualOrDos("p3.lifespans", "D3-041");

    /// <summary>
    /// D-063 for III (user s129): the combat spell list holds only combat and everywhere spells (<c>manual</c>), or every
    /// known spell (<c>dos</c>), as DOS III's picker; either way a non-combat (#52, #54, Divine Aid) or unaffordable pick is
    /// refused with DOS III's message and a Thrust (R1), no magic spent (root <c>0xfc41</c>, <see cref="P3CombatModule.CastRefusal"/>).
    /// </summary>
    public static readonly RuleOptionDescriptor CombatSpellList = ManualOrDos("p3.combat-spell-list", "D-063");

    // ---- The I / II → III transfer (T5; D3-001…D3-007, D3-016, D3-030) ----------------------------------------------

    /// <summary>
    /// D3-001 (user s77): a transfer keeps the Wand of Nikademus by Apple III's rule (<c>manual</c>: when any source character's
    /// Phantasie I score is above 99, every transferred character carries item 180 with quest bit 2) or drops it (<c>dos</c>).
    /// </summary>
    public static readonly RuleOptionDescriptor TransferWand = ManualOrDos("p3.transfer-wand", "D3-001");

    /// <summary>D3-002 (user s79): DIVINE I / II become Divine Aid (<c>manual</c>) or no spell is kept (<c>dos</c>).</summary>
    public static readonly RuleOptionDescriptor TransferDivine = ManualOrDos("p3.transfer-divine", "D3-002");

    /// <summary>
    /// D3-003 (user s79: DOS): transferred characters join the roster (<c>add</c>, every preset) or first erase every III
    /// character (<c>erase</c>, the manual's text).
    /// </summary>
    public static readonly RuleOptionDescriptor TransferRoster = new(
        "p3.transfer-roster", GameId.P3, ["add", "erase"], Manual: "add", Dos: "add", Remaster: "add", Difference: "D3-003");

    /// <summary>D3-004 (user s79): characters come from Phantasie I or II (<c>manual</c>) or from I only (<c>dos</c>).</summary>
    public static readonly RuleOptionDescriptor TransferSource = ManualOrDos("p3.transfer-source", "D3-004");

    /// <summary>
    /// D3-007 / D3-016 (user s79, s81): transferred characters start at level 0, Undead too (<c>manual</c>), or at DOS's level
    /// 1, Undead 20 (<c>dos</c>).
    /// </summary>
    public static readonly RuleOptionDescriptor TransferLevel = ManualOrDos("p3.transfer-level", "D3-007");

    /// <summary>Every III option in menu order.</summary>
    public static IReadOnlyList<RuleOptionDescriptor> All { get; } =
    [
        SlashFallback, Lunge, ThiefAttack, Aim, DownTargets, ArmourDefence, ArrowFlame, BowSpells, Parry, StatusLetter,
        CombatSpellList,
        HealingIv, HealOrder, InnHealing, HitPoints, LevelGold, SkillTables, EncounterBlock, BossFlee, Mercy,
        FoundItems, Bank, GoldCap, Trade, ArmoryRestock, AutoSave, Roster, Lifespans, Altar, EventGold, DungeonMapNo,
        TransferSource, TransferRoster, TransferLevel, TransferDivine, TransferWand,
    ];

    private static RuleOptionDescriptor ManualOrDos(string id, string difference) =>
        new(id, GameId.P3, ["manual", "dos"], Manual: "manual", Dos: "dos", Remaster: "manual", Difference: difference);
}

/// <summary>The III rules a save plays by, read from its <see cref="GameOptions"/> (false / manual for a game without them).</summary>
public static class P3RuleOptionsExtensions
{
    public static bool P3SlashToThrust(this GameOptions o) => o.IsDos(P3RuleOptions.SlashFallback);

    public static bool P3ThiefLunge(this GameOptions o) => o.IsDos(P3RuleOptions.Lunge);

    public static bool P3ThiefAttackRankOne(this GameOptions o) => o.IsDos(P3RuleOptions.ThiefAttack);

    public static bool P3DosAim(this GameOptions o) => o.IsDos(P3RuleOptions.Aim);

    public static bool P3DosHealingIv(this GameOptions o) => o.IsDos(P3RuleOptions.HealingIv);

    public static bool P3DosHealOrder(this GameOptions o) => o.IsDos(P3RuleOptions.HealOrder);

    public static bool P3DosInnHealing(this GameOptions o) => o.IsDos(P3RuleOptions.InnHealing);

    public static bool P3DosArrowFlame(this GameOptions o) => o.IsDos(P3RuleOptions.ArrowFlame);

    /// <summary>D3-039: the last bow spell sets the Fire Bow % (true, DOS) or none lowers it.</summary>
    public static bool P3DosBowSpells(this GameOptions o) => o.IsDos(P3RuleOptions.BowSpells);

    public static bool P3DosArmourSign(this GameOptions o) => o.IsDos(P3RuleOptions.ArmourDefence);

    public static bool P3DownAlwaysHit(this GameOptions o) => o.IsDos(P3RuleOptions.DownTargets);

    public static bool P3DosStatusLetter(this GameOptions o) => o.IsDos(P3RuleOptions.StatusLetter);

    public static bool P3HpIgnoresCon(this GameOptions o) => o.IsDos(P3RuleOptions.HitPoints);

    public static bool P3NoLevelGold(this GameOptions o) => o.IsDos(P3RuleOptions.LevelGold);

    public static bool P3DosEncounterBlock(this GameOptions o) => o.IsDos(P3RuleOptions.EncounterBlock);

    public static bool P3BossesFleeable(this GameOptions o) => o.Is(P3RuleOptions.BossFlee, "allowed");

    public static bool P3DosBank(this GameOptions o) => o.IsDos(P3RuleOptions.Bank);

    /// <summary>D3-025: the share deposit keeps every gold piece (<c>no-fee</c>) instead of Apple II's truncation.</summary>
    public static bool P3NoDepositFee(this GameOptions o) => o.Is(P3RuleOptions.Bank, "no-fee");

    public static bool P3NoTrade(this GameOptions o) => o.IsDos(P3RuleOptions.Trade);

    /// <summary>D3-027: Trade Items can give but not sell (the Amiga program's Trade).</summary>
    public static bool P3TradeGiveOnly(this GameOptions o) => o.Is(P3RuleOptions.Trade, "give-only");

    /// <summary>D3-024: Beg for Mercy takes the gold only (true) or also each new item at 50 %.</summary>
    public static bool P3MercyGoldOnly(this GameOptions o) => o.IsDos(P3RuleOptions.Mercy);

    /// <summary>D3-033: town entry leaves the Armory stock alone (true, DOS) or re-rolls it by Apple II's rule.</summary>
    public static bool P3NoArmoryRestock(this GameOptions o) => o.IsDos(P3RuleOptions.ArmoryRestock);

    /// <summary>D3-036: Parry is DOS's saved option (true) or this round's skill roll.</summary>
    public static bool P3DosParry(this GameOptions o) => o.IsDos(P3RuleOptions.Parry);

    /// <summary>D3-032: skill training reads DOS's tables as read (true) or with I's values in the five error cells.</summary>
    public static bool P3DosSkillTables(this GameOptions o) => o.IsDos(P3RuleOptions.SkillTables);

    /// <summary>The archive altar keeps DOS's Torso fault (true) or grants Divine Aid as its text says.</summary>
    public static bool P3DosAltar(this GameOptions o) => o.IsDos(P3RuleOptions.Altar);

    /// <summary>Dungeon event gold is DOS's sign-extended amount (true) or unsigned.</summary>
    public static bool P3SignedEventGold(this GameOptions o) => o.IsDos(P3RuleOptions.EventGold);

    /// <summary>D3-028: leaving a town saves the game (manual); false under DOS's menu-only saving.</summary>
    public static bool P3AutoSaveOnLeavingTown(this GameOptions o) => !o.IsDos(P3RuleOptions.AutoSave);

    /// <summary>D3-020: the found-item limit, null = unlimited.</summary>
    public static int? P3FoundItemLimit(this GameOptions o) =>
        o.Is(P3RuleOptions.FoundItems, "unlimited") ? null
        : o.IsDos(P3RuleOptions.FoundItems) ? P3Town.DosFoundItemLimit
        : P3Town.ManualFoundItemLimit;

    /// <summary>D3-041: III characters get no lifespan (true, DOS) or the manual's.</summary>
    public static bool P3NoLifespans(this GameOptions o) => o.IsDos(P3RuleOptions.Lifespans);

    /// <summary>
    /// D3-029: how many characters (elementals not counted) the Guild roster holds, null = no limit: the manual's no limit,
    /// DOS's 17, and the every-game <c>guild-limit</c> = <c>none</c> lifts DOS's too.
    /// </summary>
    public static int? P3RosterCharacterLimit(this GameOptions o) =>
        o.IsDos(P3RuleOptions.Roster) && !o.GuildUnlimited ? P3Town.DosRosterCharacters : null;

    /// <summary>The I / II → III transfer rules a save plays by (D3-001…D3-007).</summary>
    public static P3TransferRules P3Transfer(this GameOptions o) => new(
        KeepWand: !o.IsDos(P3RuleOptions.TransferWand),
        KeepDivine: !o.IsDos(P3RuleOptions.TransferDivine),
        EraseRoster: o.Is(P3RuleOptions.TransferRoster, "erase"),
        FromIIAllowed: !o.IsDos(P3RuleOptions.TransferSource),
        DosLevels: o.IsDos(P3RuleOptions.TransferLevel));
}
