namespace OpenPhantasie.Core;

/// <summary>
/// The preset row of a game's options tab (T5.0, <c>09-startup-options.md</c> §4). A preset sets every rules option of the
/// game; <see cref="Custom"/> is any other mix.
/// </summary>
public enum OptionPreset
{
    /// <summary>The manual's side of every option. The default for new games.</summary>
    Manual,

    /// <summary>
    /// The original code's side of each option that has a D-entry (I: DOS; II: the Atari ST, labelled per game through
    /// <see cref="OptionPresetExtensions.LabelId"/>); every other option keeps its original value. The id stays <c>dos</c>.
    /// </summary>
    Dos,

    /// <summary>The recommended set: manual rules plus the QoL options worth having on.</summary>
    Remaster,

    Custom,
}

public static class OptionPresetExtensions
{
    /// <summary>The id written to settings and saves (<c>manual</c>, <c>dos</c>, <c>remaster</c>, <c>custom</c>).</summary>
    public static string Id(this OptionPreset preset) => preset switch
    {
        OptionPreset.Manual => "manual",
        OptionPreset.Dos => "dos",
        OptionPreset.Remaster => "remaster",
        OptionPreset.Custom => "custom",
        _ => throw new ArgumentOutOfRangeException(nameof(preset)),
    };

    /// <summary>
    /// The string id of <paramref name="preset"/>'s label in <paramref name="game"/>'s tab: <c>options.preset.&lt;id&gt;.&lt;game&gt;</c>
    /// when the table has one (<paramref name="hasString"/>), else <c>options.preset.&lt;id&gt;</c>. So a game whose
    /// original is not the DOS version names its own (II: "ORIGINAL (ATARI ST)") while the saved preset id stays <c>dos</c>.
    /// </summary>
    public static string LabelId(this OptionPreset preset, GameId game, Func<string, bool> hasString)
    {
        ArgumentNullException.ThrowIfNull(hasString);
        var generic = "options.preset." + preset.Id();
        var perGame = generic + "." + game.Prefix();
        return hasString(perGame) ? perGame : generic;
    }

    /// <summary>The preset with id <paramref name="id"/>; false for an unknown or empty id.</summary>
    public static bool TryParse(string? id, out OptionPreset preset)
    {
        foreach (var p in Enum.GetValues<OptionPreset>())
            if (string.Equals(p.Id(), id, StringComparison.Ordinal))
            {
                preset = p;
                return true;
            }
        preset = OptionPreset.Manual;
        return false;
    }
}

/// <summary>
/// One rules option of a game (T5.0): a choice the save locks in at new game. <see cref="Values"/> lists the choices in
/// menu order; each preset names the value it gives. <see cref="Difference"/> is the <c>DIFFERENCES.md</c> entry the option
/// comes from, if any. Labels come from the string table (<c>option.&lt;id&gt;</c>, <c>option.&lt;id&gt;.&lt;value&gt;</c>).
/// <see cref="EveryGame"/>: one descriptor shown in every game's tab (its id has no game prefix; <see cref="Game"/> is
/// then only nominal). <see cref="Games"/> narrows an every-game option to the games it means something in (null = all).
/// </summary>
public sealed record RuleOptionDescriptor(
    string Id,
    GameId Game,
    IReadOnlyList<string> Values,
    string Manual,
    string Dos,
    string Remaster,
    string? Difference = null,
    bool EveryGame = false,
    IReadOnlyList<GameId>? Games = null)
{
    /// <summary>True when this option is in <paramref name="game"/>'s tab.</summary>
    public bool AppliesTo(GameId game) =>
        EveryGame ? Games is null || Games.Contains(game) : Game == game;

    /// <summary>The value <paramref name="preset"/> gives this option (<see cref="OptionPreset.Custom"/>: the manual's).</summary>
    public string ValueFor(OptionPreset preset) => preset switch
    {
        OptionPreset.Dos => Dos,
        OptionPreset.Remaster => Remaster,
        _ => Manual,
    };

    public bool Allows(string value) => Values.Contains(value, StringComparer.Ordinal);
}

/// <summary>
/// Every rules option the remaster has built. Kept in code, not JSON, like <see cref="FeatureModuleRegistry"/>: an option
/// only means something with C# behind it. Each later rule (PLAN P3.2: the remaining DOS behaviours) adds one descriptor.
/// </summary>
public static class RuleOptionRegistry
{
    /// <summary>
    /// D-032: a summoned elemental fights with its record's values (<c>prepared</c>, the manual's "to fight for the party")
    /// or with DOS's never-prepped slot 7 (<c>dos</c>: zero attack, damage and armor).
    /// </summary>
    public static readonly RuleOptionDescriptor ElementalPrep = new(
        "p1.elemental-prep", GameId.P1, ["prepared", "dos"],
        Manual: "prepared", Dos: "dos", Remaster: "prepared", Difference: "D-032");

    /// <summary>
    /// 09 T2 QoL (<c>inventory-actions.md</c> §4): where the item stash (the found-item sack) and the other inventory moves
    /// can be used: <c>town</c> (the original: items are handed out in town, manual p.15) or <c>anywhere</c> outside combat.
    /// No D-entry: a QoL rule, off in both Original presets.
    /// </summary>
    public static readonly RuleOptionDescriptor StashAccess = new(
        "p1.stash-access", GameId.P1, ["town", "anywhere"],
        Manual: "town", Dos: "town", Remaster: "anywhere");

    /// <summary>
    /// D-054: which Armory items the town-entry restock can wipe. <c>manual</c> (p.10: only weapons and potions come and
    /// go; armor and shields stay, a sold-out one refills from the restock list) or <c>dos</c> (OV1 <c>0x8e1</c>: armor and
    /// shields are wiped and cut to 1 too). Remaster keeps the manual's rule.
    /// </summary>
    public static readonly RuleOptionDescriptor ArmoryRestock = new(
        "p1.armory-restock", GameId.P1, ["manual", "dos"],
        Manual: "manual", Dos: "dos", Remaster: "manual", Difference: "D-054");

    /// <summary>
    /// D-048: the purse and each bank account hold at most the game's 65,000 GP (<c>manual</c>, p.10), DOS's 65,535
    /// (<c>dos</c>, its 16-bit clamp) or no practical cap (<c>lifted</c>, QoL, <see cref="GoldCaps.Lifted"/>). Under a cap
    /// the excess is lost.
    /// </summary>
    public static readonly RuleOptionDescriptor GoldCap = new(
        "p1.gold-cap", GameId.P1, ["manual", "dos", "lifted"],
        Manual: "manual", Dos: "dos", Remaster: "lifted", Difference: "D-048");

    /// <summary>
    /// D-065: Guild R)ENAME refuses party members (<c>refused</c>, manual p.4: "Characters cannot be renamed while they are
    /// members of a party") or renames them too (<c>allowed</c>, DOS OV1 <c>0x22f6</c>; QoL in the Remaster preset).
    /// </summary>
    public static readonly RuleOptionDescriptor RenamePartyMembers = new(
        "p1.rename-party", GameId.P1, ["refused", "allowed"],
        Manual: "refused", Dos: "allowed", Remaster: "allowed", Difference: "D-065");

    /// <summary>
    /// D-015: the STUNNING BLOW rider (monster special 34) stuns only a member who survived the hit (<c>manual</c>, user
    /// s26: the dead stay dead) or also a member the hit just killed (<c>dos</c>, OV2 <c>0xf85</c>: the corpse becomes SLEEP
    /// at HP 0, counts as alive and can die again). Remaster keeps the decided rule.
    /// </summary>
    public static readonly RuleOptionDescriptor StunningBlow = new(
        "p1.stunning-blow", GameId.P1, ["manual", "dos"],
        Manual: "manual", Dos: "dos", Remaster: "manual", Difference: "D-015");

    /// <summary>
    /// D-016: what monster specials 17–20 (`core.combat.armor_reduced`) scale: the party's armor <c>@246</c>
    /// (<c>armor</c>, user s26: the effect matches the message) or weapon damage <c>@248</c> (<c>weapon</c>, DOS OV2
    /// <c>0x187f</c>, field id <c>0x39</c>). Same formula and rolls either way.
    /// </summary>
    public static readonly RuleOptionDescriptor ArmorReduced = new(
        "p1.armor-reduced", GameId.P1, ["armor", "weapon"],
        Manual: "armor", Dos: "weapon", Remaster: "armor", Difference: "D-016");

    /// <summary>
    /// Quirk Q2 (<c>quirks-and-bugs.md</c>) under D-037's town-entry payout: the XP and gold left over after the equal split
    /// go to the first survivor (<c>first-survivor</c>, the remaster's fix, kept by the user s35; manual silent) or are lost
    /// (<c>dropped</c>, as DOS drops every share remainder, OV1 <c>0x167d</c>).
    /// </summary>
    public static readonly RuleOptionDescriptor PayoutRemainder = new(
        "p1.payout-remainder", GameId.P1, ["first-survivor", "dropped"],
        Manual: "first-survivor", Dos: "dropped", Remaster: "first-survivor", Difference: "D-037");

    // ---- P3.2 Authentic mode: DECIDED manual-side entries whose DOS side is one contained switch ------------------
    // Each is `manual` (the decided rule; Original (manual) and Remaster presets) or `dos` (what the D-entry documents for
    // DOS; Original (DOS) preset). Read them with GameOptions.IsDos.

    /// <summary>D-011: ATTACK / SLASH need no skill (<c>manual</c>) or DOS's <c>@250</c> gate (≥ 75 / ≥ 150; SLASH = <c>@250/75 + 1</c>).</summary>
    public static readonly RuleOptionDescriptor AttackGate = ManualOrDos("p1.attack-gate", "D-011");

    /// <summary>D-012: LUNGE for fighters only (<c>manual</c>) or for thieves too (<c>dos</c>, OV2 <c>0x6d2e</c>).</summary>
    public static readonly RuleOptionDescriptor Lunge = ManualOrDos("p1.lunge", "D-012");

    /// <summary>D-013: BINDING always helps (<c>manual</c>) or DOS's <c>defense · pct / 100</c> (negative defense moves toward 0).</summary>
    public static readonly RuleOptionDescriptor Binding = ManualOrDos("p1.binding", "D-013");

    /// <summary>D-014: CONFUSION IV sets the special gate to 0 % (<c>manual</c>) or 5 % (<c>dos</c>).</summary>
    public static readonly RuleOptionDescriptor ConfusionIv = ManualOrDos("p1.confusion-iv", "D-014");

    /// <summary>D-021: FEAR makes the monsters run (<c>manual</c>) or only lowers their level by 6 (<c>dos</c>, OV2 <c>0x80c</c>).</summary>
    public static readonly RuleOptionDescriptor Fear = ManualOrDos("p1.fear", "D-021");

    /// <summary>D-024: DISPELL UNDEAD hits the data's undead (<c>manual</c>) or any monster whose XP is divisible by 7 (<c>dos</c>).</summary>
    public static readonly RuleOptionDescriptor DispellUndead = ManualOrDos("p1.dispell-undead", "D-024");

    /// <summary>D-028: the CONFUSION / WEAKNESS / BINDING (and SLEEP) roll counts the caster's level (<c>manual</c>) or not (<c>dos</c>).</summary>
    public static readonly RuleOptionDescriptor SpellLevel = ManualOrDos("p1.spell-level", "D-028");

    /// <summary>D-029: FIREFLASH damage = the manual's ranges scaled by INT (<c>manual</c>) or DOS's <c>rand·param·INT/2000 + 1</c>.</summary>
    public static readonly RuleOptionDescriptor FireflashDamage = ManualOrDos("p1.fireflash-damage", "D-029");

    /// <summary>D-059: FIREFLASH hits the front rank (<c>manual</c>) or the rearmost living group, never its last monster (<c>dos</c>).</summary>
    public static readonly RuleOptionDescriptor FireflashTarget = ManualOrDos("p1.fireflash-target", "D-059");

    /// <summary>D-030: a failed monster run leaves sleepers asleep (<c>manual</c>) or wakes them (<c>dos</c>, OV2 <c>0x3e19</c>).</summary>
    public static readonly RuleOptionDescriptor FailedRun = ManualOrDos("p1.failed-run", "D-030");

    /// <summary>D-004: a town Inn stay takes two weeks (<c>manual</c>) or no time (<c>dos</c>, OV1 <c>0x2a0d</c>).</summary>
    public static readonly RuleOptionDescriptor TownInnTime = ManualOrDos("p1.town-inn-time", "D-004");

    /// <summary>D-007: hit dice <c>factor·(CON + 5)/15</c> (<c>manual</c>, the HP chart) or DOS's <c>&gt;&gt; 4</c>.</summary>
    public static readonly RuleOptionDescriptor HitDice = ManualOrDos("p1.hit-dice", "D-007");

    /// <summary>D-008: a wilderness inn stay takes two weeks (<c>manual</c>) or adds 1000 travel-time units (<c>dos</c>, ≈ 3.9 days).</summary>
    public static readonly RuleOptionDescriptor WildernessInnTime = ManualOrDos("p1.wild-inn-time", "D-008");

    /// <summary>D-046: the wilderness inn's MP raise uses <c>&lt;</c> (<c>manual</c>) or DOS's <c>&lt;=</c> (1 % of stays go one past the table).</summary>
    public static readonly RuleOptionDescriptor InnMagicCap = ManualOrDos("p1.inn-magic-cap", "D-046");

    /// <summary>D-047: the wilderness inn refills DEAD members (<c>manual</c>) or skips them (<c>dos</c>, OV2 <c>0x6782</c>).</summary>
    public static readonly RuleOptionDescriptor WildernessInnDead = ManualOrDos("p1.wild-inn-dead", "D-047");

    /// <summary>D-051: a magical pool stops at the race maximum (<c>manual</c>) or has no cap (<c>dos</c>, OV2 <c>0x656b</c>).</summary>
    public static readonly RuleOptionDescriptor PoolCap = ManualOrDos("p1.pool-cap", "D-051");

    /// <summary>D-055: a path step costs the data's 30 (<c>manual</c>, fastest terrain) or DOS's 60 (<c>dos</c>, as water).</summary>
    public static readonly RuleOptionDescriptor PathTime = ManualOrDos("p1.path-time", "D-055");

    /// <summary>D-056: MONSTER EVALUATION reads the nearby monsters (<c>manual</c>) or DOS's terrain / tier formula (<c>dos</c>).</summary>
    public static readonly RuleOptionDescriptor MonsterEval = ManualOrDos("p1.monster-eval", "D-056");

    /// <summary>D-057: VISION reveals the whole region (<c>manual</c>) or DOS's square of <c>(MP·100 + 500)/400</c> (<c>dos</c>).</summary>
    public static readonly RuleOptionDescriptor Vision = ManualOrDos("p1.vision", "D-057");

    /// <summary>D-067: Guild Add ages only a member from another town (<c>manual</c>) or every added member (<c>dos</c>, OV1 <c>0x284c</c>).</summary>
    public static readonly RuleOptionDescriptor AddAging = ManualOrDos("p1.add-aging", "D-067");

    /// <summary>D-070: any cast feeds the Inn's MAGIC INCREASES roll (<c>manual</c>) or only combat casts (<c>dos</c>, OV2 <c>0x0a55</c>).</summary>
    public static readonly RuleOptionDescriptor CastFlag = ManualOrDos("p1.cast-flag", "D-070");

    /// <summary>D-027: NO to "SAVE THE DUNGEON MAP?" forgets this dungeon's saved map (<c>manual</c>) or keeps it (<c>dos</c>: NO writes nothing).</summary>
    public static readonly RuleOptionDescriptor DungeonMapNo = ManualOrDos("p1.dungeon-map-no", "D-027");

    // ---- P3.2 Authentic mode rest: DOS sides that are more than one branch (data tables, Guild size, flows, lists) ---

    /// <summary>D-001: Halfling DEX modifier 12, ceiling 20 (<c>manual</c>) or DOS's TWNS.INT 11, ceiling 19 (<c>dos</c>).</summary>
    public static readonly RuleOptionDescriptor HalflingDex = ManualOrDos("p1.halfling-dex", "D-001");

    /// <summary>D-002: Guild N)EW fills 21 characters (<c>manual</c>) or every GUILD.DAT record, 22 (<c>dos</c>).</summary>
    public static readonly RuleOptionDescriptor GuildSize = ManualOrDos("p1.guild-size", "D-002");

    /// <summary>D-003: the manual's lifespan bands (<c>manual</c>) or DOS's <c>@252 = @10 × race factor</c>, checked as <c>@264 &gt;&gt; 8</c> (<c>dos</c>).</summary>
    public static readonly RuleOptionDescriptor Lifespans = ManualOrDos("p1.lifespans", "D-003");

    /// <summary>D-006: Monks never learn SLEEP (<c>manual</c>) or learn it at level 7 (<c>dos</c>, TWNS.INT <c>@0x05a</c>).</summary>
    public static readonly RuleOptionDescriptor MonkSleep = ManualOrDos("p1.monk-sleep", "D-006");

    /// <summary>D-010: town HEALING heals the manual's 1–9… (<c>manual</c>) or DOS's town <c>rand/(32767/c)</c>, 0–10… (<c>dos</c>, OV1 <c>0x261a</c>).</summary>
    public static readonly RuleOptionDescriptor TownHealing = ManualOrDos("p1.town-healing", "D-010");

    /// <summary>D-017: BEG FOR MERCY always works (<c>manual</c>) or needs <c>rand &lt; Σreaction + 90</c> (<c>dos</c>, OV2 <c>0x3bea</c>).</summary>
    public static readonly RuleOptionDescriptor BegMercy = ManualOrDos("p1.beg-mercy", "D-017");

    /// <summary>D-018: the found-item sack holds 90 items (<c>manual</c>) or DOS's 60 slots (<c>dos</c>).</summary>
    public static readonly RuleOptionDescriptor SackSize = ManualOrDos("p1.sack-size", "D-018");

    /// <summary>D-020: SLEEP is the remaster's lasting sleep (<c>manual</c>) or DOS's per-monster <c>rand ≥ L²</c>, rest of the round (<c>dos</c>, OV2 <c>0x1501</c>).</summary>
    public static readonly RuleOptionDescriptor Sleep = ManualOrDos("p1.sleep", "D-020");

    /// <summary>D-022: CHARM ends the fight (<c>manual</c>) or adds 15 reaction and re-allows GREETINGS (<c>dos</c>, OV2 <c>0x767</c>).</summary>
    public static readonly RuleOptionDescriptor Charm = ManualOrDos("p1.charm", "D-022");

    /// <summary>D-023: TELEPORTATION always works at once (<c>manual</c>) or on <c>rand &lt; 3·lvl + 50</c> at the next round top (<c>dos</c>, OV2 <c>0x7b8</c>).</summary>
    public static readonly RuleOptionDescriptor Teleport = ManualOrDos("p1.teleport", "D-023");

    /// <summary>D-031: wilderness treasure always drops (<c>manual</c>) or on <c>rand ≤ 2·L + 20</c>, with DOS's SCROLL 17 guard on the unshifted id (<c>dos</c>, OV2 <c>0x41aa</c>).</summary>
    public static readonly RuleOptionDescriptor WildernessTreasure = ManualOrDos("p1.wild-treasure", "D-031");

    /// <summary>D-033: `core.spell.summon_elemental` needs six non-DEAD members (<c>manual</c>) or DOS's <c>[0x6490] == 5</c> index/count test, its list write and second summon (<c>dos</c>, OV2 <c>0x8cc</c>). Who monster spells hit is <see cref="MonsterSpellTarget"/>.</summary>
    public static readonly RuleOptionDescriptor SummonParty = ManualOrDos("p1.summon-party", "D-033");

    /// <summary>
    /// D-033 (split from <see cref="SummonParty"/>, user s172): a monster spell that hits one member (specials 1–4) picks
    /// over every non-DEAD member (<c>manual</c>) or reads DOS's living list at its word as they stand (<c>dos</c>, OV2
    /// <c>0x1731</c>, <see cref="Party.DosLiving"/>): early in a round with orders the last listed member can't be hit, and
    /// a stale entry (after a <c>dos</c> summon in a count phase) can name a DEAD member, wasting the spell.
    /// </summary>
    public static readonly RuleOptionDescriptor MonsterSpellTarget = ManualOrDos("p1.monster-spell-target", "D-033");

    /// <summary>D-053: the end of combat shows and pools the totals (<c>manual</c>) or DOS's per-survivor shares, remainders lost (<c>dos</c>, OV2 <c>0x3fbe</c>).</summary>
    public static readonly RuleOptionDescriptor CombatShares = ManualOrDos("p1.combat-shares", "D-053");

    /// <summary>D-069: Continue asks for a Guild number (<c>manual</c>) or loads the saved party without asking (<c>dos</c>, OV1 <c>0x866..0x89d</c>).</summary>
    public static readonly RuleOptionDescriptor ContinueParty = ManualOrDos("p1.continue", "D-069");

    /// <summary>D-035: out-of-combat Cast lists only the spells allowed here (<c>manual</c>) or every known spell, the others spending MP for nothing (<c>dos</c>, OV1 <c>0x254e</c> / OV2 <c>0x4790</c>).</summary>
    public static readonly RuleOptionDescriptor CastList = ManualOrDos("p1.cast-list", "D-035");

    /// <summary>D-062: TRANSPORTATION leaves the current town out (<c>manual</c>) or lists all 12 towns (<c>dos</c>, COM <c>0x16a2</c>).</summary>
    public static readonly RuleOptionDescriptor TransportList = ManualOrDos("p1.transport-list", "D-062");

    /// <summary>D-063: the combat picker lists combat and everywhere spells (<c>manual</c>) or every known spell, a non-combat one spending MP for nothing (<c>dos</c>).</summary>
    public static readonly RuleOptionDescriptor CombatSpellList = ManualOrDos("p1.combat-spell-list", "D-063");

    /// <summary>D-078: combat orders stay until changed (<c>manual</c>) or reset to PARRY on leaving town and after a defeat (<c>dos</c>, OV1 <c>0xd0</c> / <c>0x84</c>).</summary>
    public static readonly RuleOptionDescriptor OrdersReset = ManualOrDos("p1.orders-reset", "D-078");

    private static RuleOptionDescriptor ManualOrDos(string id, string difference) =>
        new(id, GameId.P1, ["manual", "dos"], Manual: "manual", Dos: "dos", Remaster: "manual", Difference: difference);

    /// <summary>
    /// D2-002 (Phantasie II, I → II transfer, <see cref="TransferIToII"/>): which classes arrive with a starting spell.
    /// <c>manual</c> (p.27: Wizards #5, Priests #1; read strictly, Rangers and Monks lose theirs) or <c>st</c> (the Atari ST
    /// also gives Rangers HEALING I and Monks FIREFLASH I). User s76: the ST is fine unless the manual contradicts it, and
    /// it does, so Remaster plays the manual. The Original (Atari ST) preset is this game's <c>Dos</c> slot.
    /// </summary>
    public static readonly RuleOptionDescriptor TransferSpells = new(
        "p2.transfer-spells", GameId.P2, ["manual", "st"],
        Manual: "manual", Dos: "st", Remaster: "manual", Difference: "D2-002");

    /// <summary>
    /// D2-004: the divine spells a transfer keeps: <c>p27</c> (#55 and above, manual p.27 and the ST; user s76) or
    /// <c>p14</c> (#58 and above, manual p.14). Every preset gives <c>p27</c>.
    /// </summary>
    public static readonly RuleOptionDescriptor TransferDivine = new(
        "p2.transfer-divine", GameId.P2, ["p27", "p14"],
        Manual: "p27", Dos: "p27", Remaster: "p27", Difference: "D2-004");

    /// <summary>
    /// D2-003: bank gold on a transfer: <c>st</c> (⌊hi/35⌋·256 + 100, user s76: accepted) or <c>no-gain</c> (user s77:
    /// kept as a player choice, min(old, formula), so nobody gains). Every preset gives <c>st</c>.
    /// </summary>
    public static readonly RuleOptionDescriptor TransferGold = new(
        "p2.transfer-gold", GameId.P2, ["st", "no-gain"],
        Manual: "st", Dos: "st", Remaster: "st", Difference: "D2-003");

    // ---- Phantasie II training, level-up and saving (D2-013, D2-015, D2-016; s79 user decisions) -----------------------

    /// <summary>
    /// D2-013 (I's D-050 again): while XP is short the Guild tells the XP needed and the cost (<c>manual</c>, CB pp.4–5;
    /// user s79: as in I) or the XP only, as the ST's <c>confirm_cost 0x017e4</c> (<c>st</c>).
    /// </summary>
    public static readonly RuleOptionDescriptor P2TrainingCost = new(
        "p2.training-cost", GameId.P2, ["manual", "st"],
        Manual: "manual", Dos: "st", Remaster: "manual", Difference: "D2-013");

    /// <summary>
    /// D2-013 (I's D-007 again): hit dice <c>factor·(CON + 5)/15</c> (<c>manual</c>, the HP chart CB p.11; user s79: as in I)
    /// or the ST's <c>&gt;&gt; 4</c> (<c>st</c>, <c>level_up 0x0c71a</c>).
    /// </summary>
    public static readonly RuleOptionDescriptor P2HitDice = new(
        "p2.hit-dice", GameId.P2, ["manual", "st"],
        Manual: "manual", Dos: "st", Remaster: "manual", Difference: "D2-013");

    /// <summary>
    /// D2-015: wizards learn RESURRECTION (#44) at level 13 (<c>manual</c>, CB p.10 "a high-level wizard"; user s79) or never,
    /// as the ST's class/level table (<c>st</c>, TWNS.INT +0x5a WIZARD slot 43 = 0; <c>dos_tables</c> in creation-tables.json).
    /// </summary>
    public static readonly RuleOptionDescriptor P2WizardResurrection = new(
        "p2.wizard-resurrection", GameId.P2, ["manual", "st"],
        Manual: "manual", Dos: "st", Remaster: "manual", Difference: "D2-015");

    /// <summary>
    /// D2-016 (NOT A CONFLICT): leaving a town saves the game when the Town options "Auto save" item is ticked (<c>toggle</c>,
    /// the ST and its manual section CB p.25, off at start; every preset) or always (<c>always</c>, the Apple section CB p.19;
    /// a player choice in no preset).
    /// </summary>
    public static readonly RuleOptionDescriptor P2AutoSave = new(
        "p2.autosave", GameId.P2, ["toggle", "always"],
        Manual: "toggle", Dos: "toggle", Remaster: "toggle", Difference: "D2-016");

    // ---- Phantasie II: I's manual-vs-DOS decisions again (D2-019…D2-023; user s80: manual, as I, the ST as an option) ----
    // Each is `manual` (Original (manual) and Remaster) or `st` (Original (Atari ST)). Read through the combined accessors
    // below (GoldCap, OriginalGuildSize, OriginalLifespans, OriginalDispellUndead, OriginalFear), which also answer I's ids.

    /// <summary>
    /// D2-019 (I's D-048 again): the purse and each bank account hold at most the manual's 65,000 GP (<c>manual</c>, CB p.6)
    /// or the ST's 65,535 (<c>st</c>, clamp <c>0x0dfa4</c>; <see cref="GoldCapRule.Dos"/>).
    /// </summary>
    public static readonly RuleOptionDescriptor P2GoldCap = ManualOrSt("p2.gold-cap", "D2-019");

    /// <summary>
    /// D2-020 (I's D-002 again): Guild N)EW fills 21 characters (<c>manual</c>, CB p.20, <c>rules.json</c>
    /// <c>guild_max_characters</c>) or all 22 ST slots (<c>st</c>, <c>guild_by_platform.atari-st</c>; seen live s80).
    /// </summary>
    public static readonly RuleOptionDescriptor P2GuildSize = ManualOrSt("p2.guild-size", "D2-020");

    /// <summary>
    /// D2-021 (I's D-003 again): the manual's lifespan bands ±10 % (<c>manual</c>) or the ST's <c>@252 = @10 × race factor</c>
    /// checked as <c>@264 &gt;&gt; 8</c> (<c>st</c>, <c>create_character 0x0cc84</c>, <c>town_aging 0x0bba6</c>; DOS I's factors).
    /// </summary>
    public static readonly RuleOptionDescriptor P2Lifespans = ManualOrSt("p2.lifespans", "D2-021");

    /// <summary>
    /// D2-022 (I's D-024 again): DISPELL UNDEAD hits the data's undead (<c>manual</c>, II's <c>monster-traits.json</c>: I's
    /// list plus PLUTO'S UNDEAD SNAKE) or any monster whose XP is divisible by 7 (<c>st</c>, combat case <c>0x842c</c>).
    /// </summary>
    public static readonly RuleOptionDescriptor P2DispellUndead = ManualOrSt("p2.dispell-undead", "D2-022");

    /// <summary>
    /// D2-023 (I's D-021 again): FEAR makes the monsters run (<c>manual</c>) or only lowers the encounter level by 6
    /// (<c>st</c>, combat case <c>0x8326</c>).
    /// </summary>
    public static readonly RuleOptionDescriptor P2Fear = ManualOrSt("p2.fear", "D2-023");

    /// <summary>
    /// D2-023 follow-up (user s81): under the manual FEAR, Pluto, his Ice Dragon and his eight minions stand fast
    /// (<c>stand</c>, <c>monster-traits.json</c> <c>never_flees</c>, as I's Black Lord) or run like any monster (<c>flee</c>).
    /// The ST never makes monsters run, so both Original presets keep <c>stand</c>.
    /// </summary>
    public static readonly RuleOptionDescriptor P2FearBosses = new(
        "p2.fear-bosses", GameId.P2, ["stand", "flee"],
        Manual: "stand", Dos: "stand", Remaster: "stand", Difference: "D2-023");

    // ---- Phantasie II towns, inns and travel (D2-030, D2-031, D2-032, D2-040; s81 user decisions) ----------------------

    /// <summary>
    /// D2-030: town TRANSPORTATION lists every other town, across Ferronrah and the Netherworld (<c>manual</c>, "from one
    /// town to another"; user s81) or only the ST's same-plane list (<c>st</c>, <c>towns.json</c> <c>transport_destinations</c>,
    /// <c>PHANT.PRG</c> <c>0x23ee</c>).
    /// </summary>
    public static readonly RuleOptionDescriptor P2TransportList = ManualOrSt("p2.transport-list", "D2-030");

    /// <summary>
    /// D2-031: the town Inn refills every member, DEAD too, status unchanged (<c>manual</c>, as I's town Inn; user s81) or
    /// skips the dead with "IS RESTING IN PEACE" (<c>st</c>, <c>0xe5fc</c>).
    /// </summary>
    public static readonly RuleOptionDescriptor P2InnDead = ManualOrSt("p2.inn-dead", "D2-031");

    /// <summary>D2-032 (I's D-004 again): a town Inn stay takes about two weeks (<c>manual</c>) or no time (<c>st</c>).</summary>
    public static readonly RuleOptionDescriptor P2TownInnTime = ManualOrSt("p2.town-inn-time", "D2-032");

    /// <summary>D2-032 (I's D-046 again): the wilderness inn's MP raise stops at the table (<c>manual</c>) or uses the ST's <c>≤</c> (<c>st</c>).</summary>
    public static readonly RuleOptionDescriptor P2InnMagicCap = ManualOrSt("p2.inn-magic-cap", "D2-032");

    /// <summary>D2-042 (I's D-008 again): a wilderness inn stay takes two weeks (<c>manual</c>) or adds the ST's 1000 travel-time units, no aging (<c>st</c>).</summary>
    public static readonly RuleOptionDescriptor P2WildernessInnTime = ManualOrSt("p2.wild-inn-time", "D2-042");

    /// <summary>D2-043 (I's D-053 again): the end of combat shows and pools the totals (<c>manual</c>) or the ST's `core.combat.each_survivor_gets` shares (<c>st</c>).</summary>
    public static readonly RuleOptionDescriptor P2CombatShares = ManualOrSt("p2.combat-shares", "D2-043");

    /// <summary>D2-040 (I's D-055 again): a path step costs 30 (<c>manual</c>) or the ST's 60, as water (<c>st</c>).</summary>
    public static readonly RuleOptionDescriptor P2PathTime = ManualOrSt("p2.path-time", "D2-040");

    /// <summary>
    /// D2-041 (I's D-027 again, user s82 "as I"): leaving a dungeon with NO to the map save forgets a slot of the same
    /// dungeon (<c>manual</c>, the remaster's D-027 rule) or keeps it, so the older save comes back (<c>st</c>).
    /// </summary>
    public static readonly RuleOptionDescriptor P2DungeonMapNo = ManualOrSt("p2.dungeon-map-no", "D2-041");

    private static RuleOptionDescriptor ManualOrSt(string id, string difference) =>
        new(id, GameId.P2, ["manual", "st"], Manual: "manual", Dos: "st", Remaster: "manual", Difference: difference);

    /// <summary>
    /// QoL (PLAN P3, user s79 from D3-003), every game: Guild N)EW stops at the game's own cap (<c>game</c>: I's 21, or 22
    /// under <c>p1.guild-size</c> = <c>dos</c>; each game's <c>rules.json</c>) or has no roster limit (<c>none</c>: the
    /// roster grows past the game's slots; a DOS export writes the first 22 and lists the rest). No D-entry: a QoL rule, off
    /// in both Original presets.
    /// </summary>
    public static readonly RuleOptionDescriptor GuildLimit = new(
        "guild-limit", GameId.P1, ["game", "none"],
        Manual: "game", Dos: "game", Remaster: "none", EveryGame: true);

    // ---- Elementals across the games (PLAN P3 QoL, user D3-040): one cross-game rule set for Guild Add / Drop / Purge
    // with a loaded elemental. Every value is an original's decoded rule; `game` (every preset) = the game's own. Not in
    // IV's tab: IV picks its elemental when the party is formed and has no Guild Add / Drop / Purge of one. Whatever the
    // values, an elemental is never removed from the game: the records stay in the Guild (GameSession.ElementalTemplates),
    // Purge refuses an elemental (GuildHall.PurgeRefusal), and Drop / Purge only unload the party copy.

    private static readonly GameId[] GuildElementalGames = [GameId.P1, GameId.P2, GameId.P3];

    /// <summary>
    /// When the Guild lets an elemental join and whether a character can join after it: <c>game</c> (the game's own:
    /// I six characters first and nobody after it, OV1 <c>0x2786</c> / <c>0x1fe8</c>; II no count and characters join beside
    /// it, ST <c>0xd442</c> (s172); III one character, later ones go in front of it, ovl2 <c>0x3a0a</c>), <c>phantasie-i</c> or <c>phantasie-iii</c> in every game
    /// (<see cref="ElementalRules.PhantasieI"/>, <see cref="ElementalRules.PhantasieIII"/>). Summoning in combat keeps the
    /// game's own rule (D-033).
    /// </summary>
    public static readonly RuleOptionDescriptor ElementalLoad = new(
        "elemental-load", GameId.P1, ["game", "phantasie-i", "phantasie-iii"],
        Manual: "game", Dos: "game", Remaster: "game", EveryGame: true, Games: GuildElementalGames);

    /// <summary>
    /// Guild ▸ Drop of a character with an elemental loaded: <c>game</c> (I: every Drop unloads it, OV1 <c>0x22d1</c>;
    /// II: it stays, even alone, ST <c>0xd57c</c> (s172); III: it stays while a character is left, ovl2 <c>0x3c92</c>),
    /// <c>phantasie-i</c> or <c>phantasie-iii</c> in every game.
    /// </summary>
    public static readonly RuleOptionDescriptor ElementalDrop = new(
        "elemental-drop", GameId.P1, ["game", "phantasie-i", "phantasie-iii"],
        Manual: "game", Dos: "game", Remaster: "game", EveryGame: true, Games: GuildElementalGames);

    /// <summary>
    /// Guild ▸ Purge of a character in the party with an elemental loaded (D3-040): <c>game</c> (the game's own,
    /// <see cref="ElementalRules.Purge"/>: I purged and the elemental unloaded, OV1 <c>0x2489</c>; II purged and the elemental
    /// stays, ST <c>0xd68a</c> (s172); III by the preset, user s172: <c>as-drop</c> under Manual / Remaster / Custom,
    /// <c>refused</c> under the Original preset), <c>phantasie-i</c> (unloaded), <c>as-drop</c> (III's manual, D3-040: what
    /// this save's Drop does, <see cref="ElementalDrop"/>) or <c>refused</c> (DOS III: a party member is not purged; Drop
    /// first). III's former <c>p3.purge</c> is folded in here (s172).
    /// </summary>
    public static readonly RuleOptionDescriptor ElementalPurge = new(
        "elemental-purge", GameId.P1, ["game", "phantasie-i", "as-drop", "refused"],
        Manual: "game", Dos: "game", Remaster: "game", Difference: "D3-040", EveryGame: true, Games: GuildElementalGames);

    public static IReadOnlyList<RuleOptionDescriptor> All { get; } =
    [
        ElementalPrep, StashAccess, ArmoryRestock, GoldCap, RenamePartyMembers, StunningBlow, ArmorReduced, PayoutRemainder,
        AttackGate, Lunge, Binding, ConfusionIv, Fear, DispellUndead, SpellLevel, FireflashDamage, FireflashTarget, FailedRun,
        TownInnTime, HitDice, WildernessInnTime, InnMagicCap, WildernessInnDead, PoolCap, PathTime, MonsterEval, Vision,
        AddAging, CastFlag, DungeonMapNo,
        HalflingDex, GuildSize, Lifespans, MonkSleep,
        TownHealing, BegMercy, SackSize, Sleep, Charm, Teleport, WildernessTreasure, SummonParty, MonsterSpellTarget,
        CombatShares, ContinueParty, CastList, TransportList, CombatSpellList, OrdersReset,
        TransferSpells, TransferDivine, TransferGold,
        P2TrainingCost, P2HitDice, P2WizardResurrection, P2AutoSave,
        P2GoldCap, P2GuildSize, P2Lifespans, P2DispellUndead, P2Fear, P2FearBosses,
        P2TransportList, P2InnDead, P2TownInnTime, P2InnMagicCap, P2WildernessInnTime, P2PathTime, P2DungeonMapNo, P2CombatShares,
        .. P3RuleOptions.All, // Phantasie III (T4), its own class so its fields are set before this list reads them
        .. P4RuleOptions.All, // Phantasie IV (T6), the same way
        ElementalLoad, ElementalDrop, ElementalPurge, // I, II and III (EveryGame, Games)
        GuildLimit, // every game (EveryGame), last in each tab
    ];

    /// <summary>The rules options of <paramref name="game"/>, in menu order: its own, then the every-game ones.</summary>
    public static IReadOnlyList<RuleOptionDescriptor> For(GameId game) =>
        All.Where(d => d.Game == game && !d.EveryGame).Concat(All.Where(d => d.EveryGame && d.AppliesTo(game))).ToArray();
}

/// <summary>
/// The rules one save plays by (T5.0 §5): a preset, every rules option's value and the enabled feature module ids.
/// Immutable; a session gets one when it is built for a new game and a new one only from a loaded save. The Client's game
/// tab keeps one as the defaults for new games.
/// </summary>
public sealed class GameOptions
{
    private readonly IReadOnlyList<RuleOptionDescriptor> _options;
    private readonly Dictionary<string, string> _values;

    private GameOptions(
        GameId game, OptionPreset preset, IReadOnlyList<RuleOptionDescriptor> options,
        Dictionary<string, string> values, IReadOnlyList<string> modules)
    {
        Game = game;
        Preset = preset;
        _options = options;
        _values = values;
        Modules = modules;
    }

    public GameId Game { get; }

    /// <summary>Phantasie I's Original (manual) rules: the fallback of code built without a session (a bare encounter, tests).</summary>
    public static GameOptions ManualPhantasieI { get; } = ForPreset(GameId.P1);

    public OptionPreset Preset { get; }

    /// <summary>The game's rules options, in menu order.</summary>
    public IReadOnlyList<RuleOptionDescriptor> Options => _options;

    /// <summary>Option id → value, one entry per option of <see cref="Options"/>.</summary>
    public IReadOnlyDictionary<string, string> Values => _values;

    /// <summary>The enabled feature module ids, in registry order.</summary>
    public IReadOnlyList<string> Modules { get; }

    /// <summary>
    /// The rules <paramref name="preset"/> gives <paramref name="game"/> (<see cref="OptionPreset.Custom"/> starts from the
    /// manual's values). <paramref name="options"/> replaces the registry's list (tests).
    /// </summary>
    public static GameOptions ForPreset(
        GameId game, OptionPreset preset = OptionPreset.Manual, IReadOnlyList<RuleOptionDescriptor>? options = null)
    {
        options ??= RuleOptionRegistry.For(game);
        var values = options.ToDictionary(d => d.Id, d => d.ValueFor(preset), StringComparer.Ordinal);
        return new GameOptions(game, preset, options, values, DefaultModules(game));
    }

    /// <summary>
    /// Rebuild saved or stored rules leniently (T5.0 §5): an unknown or empty preset reads as manual, an unknown option id
    /// or an unbuilt module is dropped, and a missing or bad value takes its preset's value.
    /// </summary>
    public static GameOptions Restore(
        GameId game, string? preset, IReadOnlyDictionary<string, string>? values, IEnumerable<string>? modules,
        IReadOnlyList<RuleOptionDescriptor>? options = null)
    {
        OptionPresetExtensions.TryParse(preset, out var p);
        var restored = ForPreset(game, p, options);
        var merged = new Dictionary<string, string>(restored._values, StringComparer.Ordinal);
        foreach (var d in restored._options)
            if (values is not null && values.TryGetValue(d.Id, out var v) && d.Allows(v))
                merged[d.Id] = v;
        var on = modules is null
            ? restored.Modules
            : FeatureModuleRegistry.All
                .Where(d => d.IsOptionalIn(game) && modules.Contains(d.Id, StringComparer.Ordinal))
                .Select(d => d.Id)
                .ToArray();
        var result = new GameOptions(game, p, restored._options, merged, on);
        // A stored Custom stays Custom (the player picked it); a named preset that no longer fits its values is re-matched.
        return p == OptionPreset.Custom || result.Matches(p) ? result : result.MatchingOrCustom();
    }

    /// <summary>The value of option <paramref name="id"/>; throws for an id this game doesn't have.</summary>
    public string Get(string id) =>
        _values.TryGetValue(id, out var v) ? v : throw new ArgumentException($"No rules option '{id}' in {Game.Prefix()}.", nameof(id));

    /// <summary>These rules with <paramref name="id"/> set to <paramref name="value"/>; the preset follows (§4).</summary>
    public GameOptions With(string id, string value)
    {
        var d = _options.FirstOrDefault(o => o.Id == id)
                ?? throw new ArgumentException($"No rules option '{id}' in {Game.Prefix()}.", nameof(id));
        if (!d.Allows(value))
            throw new ArgumentException($"'{value}' is not a value of '{id}'.", nameof(value));
        var values = new Dictionary<string, string>(_values, StringComparer.Ordinal) { [id] = value };
        return Rebuilt(values, Modules, Preset);
    }

    /// <summary>These rules with feature module <paramref name="id"/> on or off. A placeholder can't be switched on.</summary>
    public GameOptions WithModule(string id, bool on)
    {
        var d = FeatureModuleRegistry.Find(id) ?? throw new ArgumentException($"Unknown feature module '{id}'.", nameof(id));
        if (on && !d.Implemented)
            throw new NotSupportedException($"Feature module '{id}' is not decoded yet and can't be enabled.");
        if (on && !d.IsOptionalIn(Game))
            throw new NotSupportedException($"Feature module '{id}' is {d.HomeGame.Prefix()}'s own and can't be used in {Game.Prefix()}.");
        var set = new HashSet<string>(Modules, StringComparer.Ordinal);
        if (on) set.Add(id);
        else set.Remove(id);
        var modules = FeatureModuleRegistry.All.Where(m => set.Contains(m.Id)).Select(m => m.Id).ToArray();
        return Rebuilt(_values, modules, Preset);
    }

    /// <summary>
    /// <paramref name="preset"/>'s rules; <see cref="OptionPreset.Custom"/> keeps the current values and only renames the
    /// preset.
    /// </summary>
    public GameOptions WithPreset(OptionPreset preset) =>
        preset == OptionPreset.Custom
            ? new GameOptions(Game, preset, _options, _values, Modules)
            : ForPreset(Game, preset, _options);

    /// <summary>True when these values and modules are exactly what <paramref name="preset"/> gives.</summary>
    public bool Matches(OptionPreset preset) =>
        preset != OptionPreset.Custom
        && _options.All(d => _values[d.Id] == d.ValueFor(preset))
        && Modules.SequenceEqual(DefaultModules(Game));

    /// <summary>The feature module set these rules switch on.</summary>
    public FeatureModuleSet CreateModules()
    {
        var defaults = DefaultModules(Game);
        return FeatureModuleSet.ForGame(Game, Modules, defaults.Except(Modules));
    }

    /// <summary>D-032 (<see cref="RuleOptionRegistry.ElementalPrep"/>); I's rule, the manual's value elsewhere.</summary>
    public ElementalPrepRule ElementalPrep =>
        _values.TryGetValue(RuleOptionRegistry.ElementalPrep.Id, out var v) && v == "dos"
            ? ElementalPrepRule.Dos
            : ElementalPrepRule.Prepared;

    /// <summary>
    /// <see cref="RuleOptionRegistry.StashAccess"/>: <see cref="StashAccessRule.Anywhere"/> only when the save's value says
    /// so; the original's town-only rule otherwise (also for a game without the option).
    /// </summary>
    public StashAccessRule StashAccess =>
        _values.TryGetValue(RuleOptionRegistry.StashAccess.Id, out var v) && v == "anywhere"
            ? StashAccessRule.Anywhere
            : StashAccessRule.Town;

    /// <summary>
    /// <see cref="RuleOptionRegistry.ArmoryRestock"/> (D-054): true only when the save plays DOS's restock (armor and
    /// shields wiped too); the manual's rule otherwise, also for a game without the option.
    /// </summary>
    public bool DosArmoryRestock =>
        _values.TryGetValue(RuleOptionRegistry.ArmoryRestock.Id, out var v) && v == "dos";

    /// <summary>
    /// <see cref="RuleOptionRegistry.GoldCap"/> (D-048): the game's own caps (<see cref="GoldCapRule.Manual"/>) unless the
    /// save says <c>dos</c> or <c>lifted</c>; <see cref="RuleOptionRegistry.P2GoldCap"/> (D2-019) = <c>st</c> is the same
    /// 16-bit clamp (<see cref="GoldCapRule.Dos"/>). Also the game's own for a game without either option.
    /// </summary>
    public GoldCapRule GoldCap =>
        _values.TryGetValue(RuleOptionRegistry.GoldCap.Id, out var v)
            ? v switch { "dos" => GoldCapRule.Dos, "lifted" => GoldCapRule.Lifted, _ => GoldCapRule.Manual }
            : Is(RuleOptionRegistry.P2GoldCap, "st") ? GoldCapRule.Dos
            : Is(P3RuleOptions.GoldCap, "dos") ? GoldCapRule.Lifted // D3-026: DOS III has no cap
            : GoldCapRule.Manual;

    /// <summary>
    /// True when Guild N)EW fills the original platform's Guild (<see cref="RulesProfile.OriginalPlatform"/>) instead of the
    /// manual's 21: <see cref="RuleOptionRegistry.GuildSize"/> = <c>dos</c> (I, D-002) or
    /// <see cref="RuleOptionRegistry.P2GuildSize"/> = <c>st</c> (II, D2-020).
    /// </summary>
    public bool OriginalGuildSize => IsDos(RuleOptionRegistry.GuildSize) || Is(RuleOptionRegistry.P2GuildSize, "st");

    /// <summary>
    /// True when lifespans follow the original's race factors (<c>dos_tables.lifespan_factors</c>, checked as
    /// <c>@264 &gt;&gt; 8</c>): <see cref="RuleOptionRegistry.Lifespans"/> = <c>dos</c> (I, D-003) or
    /// <see cref="RuleOptionRegistry.P2Lifespans"/> = <c>st</c> (II, D2-021). False: the manual's bands.
    /// </summary>
    public bool OriginalLifespans => IsDos(RuleOptionRegistry.Lifespans) || Is(RuleOptionRegistry.P2Lifespans, "st");

    /// <summary>
    /// True when DISPELL UNDEAD takes any monster whose XP is divisible by 7: <see cref="RuleOptionRegistry.DispellUndead"/>
    /// = <c>dos</c> (I, D-024) or <see cref="RuleOptionRegistry.P2DispellUndead"/> = <c>st</c> (II, D2-022). False: the
    /// data's undead flag (<c>monster-traits.json</c>).
    /// </summary>
    public bool OriginalDispellUndead =>
        IsDos(RuleOptionRegistry.DispellUndead) || Is(RuleOptionRegistry.P2DispellUndead, "st");

    /// <summary>
    /// True when a successful FEAR only lowers the encounter level: <see cref="RuleOptionRegistry.Fear"/> = <c>dos</c>
    /// (I, D-021) or <see cref="RuleOptionRegistry.P2Fear"/> = <c>st</c> (II, D2-023). False: the monsters run.
    /// </summary>
    public bool OriginalFear => IsDos(RuleOptionRegistry.Fear) || Is(RuleOptionRegistry.P2Fear, "st");

    /// <summary>
    /// <see cref="RuleOptionRegistry.P2FearBosses"/> = <c>flee</c>: FEAR also makes <see cref="Monster.NeverFlees"/>
    /// monsters run. False (default, and every game without the option): they stand fast.
    /// </summary>
    public bool FearBossesFlee => Is(RuleOptionRegistry.P2FearBosses, "flee");

    /// <summary>
    /// <see cref="RuleOptionRegistry.RenamePartyMembers"/> (D-065): true when Guild R)ENAME may rename party members; the
    /// manual's refusal otherwise, also for a game without the option.
    /// </summary>
    public bool RenamePartyMembers =>
        _values.TryGetValue(RuleOptionRegistry.RenamePartyMembers.Id, out var v) && v == "allowed";

    /// <summary><see cref="RuleOptionRegistry.StunningBlow"/> (D-015): true when a STUNNING BLOW also puts a member it killed to SLEEP (DOS).</summary>
    public bool DosStunningBlow => Is(RuleOptionRegistry.StunningBlow, "dos");

    /// <summary><see cref="RuleOptionRegistry.ArmorReduced"/> (D-016): true when specials 17–20 lower weapon damage (DOS), not armor.</summary>
    public bool DosArmorReduced => Is(RuleOptionRegistry.ArmorReduced, "weapon");

    /// <summary><see cref="RuleOptionRegistry.PayoutRemainder"/> (Q2): true when the town-entry payout drops its remainders (DOS).</summary>
    public bool PayoutRemainderDropped => Is(RuleOptionRegistry.PayoutRemainder, "dropped");

    /// <summary>
    /// The I → II transfer rules (<see cref="TransferIToII"/>; D2-002, D2-003, D2-004). A game without the options gets
    /// the first value of each (the manual's spells, #55+, the ST gold formula).
    /// </summary>
    public TransferIToIIRules TransferIToII => new(
        Is(RuleOptionRegistry.TransferSpells, "st") ? TransferSpellRule.St : TransferSpellRule.Manual,
        Is(RuleOptionRegistry.TransferDivine, "p14") ? TransferDivineRule.FromP14 : TransferDivineRule.FromP27,
        Is(RuleOptionRegistry.TransferGold, "no-gain") ? TransferGoldRule.NoGain : TransferGoldRule.St);

    /// <summary>
    /// True when level-ups and creation roll the original's hit-dice count (<c>&gt;&gt; 4</c>): <see cref="RuleOptionRegistry.HitDice"/>
    /// = <c>dos</c> (I, D-007) or <see cref="RuleOptionRegistry.P2HitDice"/> = <c>st</c> (II, D2-013).
    /// </summary>
    public bool OriginalHitDice => IsDos(RuleOptionRegistry.HitDice) || Is(RuleOptionRegistry.P2HitDice, "st");

    /// <summary><see cref="RuleOptionRegistry.P2TrainingCost"/> = <c>st</c>: the Guild shows no cost while XP is short (D2-013).</summary>
    public bool TrainingCostHidden => Is(RuleOptionRegistry.P2TrainingCost, "st");

    /// <summary>
    /// True when the creation tables take the original's spell-level cells (<c>dos_tables.spell_levels</c>):
    /// <see cref="RuleOptionRegistry.MonkSleep"/> = <c>dos</c> (I, D-006) or <see cref="RuleOptionRegistry.P2WizardResurrection"/>
    /// = <c>st</c> (II, D2-015).
    /// </summary>
    public bool OriginalSpellLevels => IsDos(RuleOptionRegistry.MonkSleep) || Is(RuleOptionRegistry.P2WizardResurrection, "st");

    /// <summary>
    /// <see cref="RuleOptionRegistry.P2AutoSave"/> = <c>always</c>: leaving a town saves the game whatever the Auto save
    /// toggle says (D2-016, the Apple text). False elsewhere: the toggle decides.
    /// </summary>
    public bool AutoSaveAlways =>
        Is(RuleOptionRegistry.P2AutoSave, "always") || Is(P3RuleOptions.AutoSave, "manual"); // D3-028: III manual

    /// <summary>The town Inn takes no time: <c>p1.town-inn-time</c> = <c>dos</c> (D-004) or <c>p2.town-inn-time</c> = <c>st</c> (D2-032).</summary>
    public bool TownInnNoTime => IsDos(RuleOptionRegistry.TownInnTime) || Is(RuleOptionRegistry.P2TownInnTime, "st");

    /// <summary>A wilderness inn stay adds the original 1000 travel-time units and ages nobody (D-008 <c>dos</c> / D2-042 <c>st</c>).</summary>
    public bool WildernessInnTravelTime =>
        IsDos(RuleOptionRegistry.WildernessInnTime) || Is(RuleOptionRegistry.P2WildernessInnTime, "st");

    /// <summary>The end of combat pays the original per-survivor shares (D-053 <c>dos</c> / D2-043 <c>st</c>), not the manual's totals.</summary>
    public bool SurvivorShares => IsDos(RuleOptionRegistry.CombatShares) || Is(RuleOptionRegistry.P2CombatShares, "st");

    /// <summary>The wilderness inn's MP raise uses the original <c>≤</c> (D-046 / D2-032), which can pass the table.</summary>
    public bool InnMagicPastTable => IsDos(RuleOptionRegistry.InnMagicCap) || Is(RuleOptionRegistry.P2InnMagicCap, "st");

    /// <summary>A path step costs the original 60 (DOS I's D-055 / the ST's D2-040), not the manual's 30.</summary>
    public bool OriginalPathTime => IsDos(RuleOptionRegistry.PathTime) || Is(RuleOptionRegistry.P2PathTime, "st");

    /// <summary>D2-031 <c>st</c>: the town Inn leaves DEAD members' HP and MP as they were ("IS RESTING IN PEACE").</summary>
    public bool TownInnSkipsDead => Is(RuleOptionRegistry.P2InnDead, "st");

    /// <summary>D2-030 <c>st</c>: TRANSPORTATION lists only the ST's same-plane destinations of the current town.</summary>
    public bool SamePlaneTransport => Is(RuleOptionRegistry.P2TransportList, "st");

    /// <summary>
    /// <see cref="RuleOptionRegistry.GuildLimit"/> (QoL, every game): true when the Guild roster has no limit; the game's
    /// own cap otherwise, also for options built without it.
    /// </summary>
    public bool GuildUnlimited => Is(RuleOptionRegistry.GuildLimit, "none");

    /// <summary>
    /// True when this save plays the <c>dos</c> value of a manual-or-DOS option (<see cref="RuleOptionRegistry.AttackGate"/>
    /// and the other P3.2 switches); false for its manual value and for a game without it.
    /// </summary>
    public bool IsDos(RuleOptionDescriptor option) => Is(option, "dos");

    /// <summary>
    /// True when this save gives <paramref name="option"/> the value <paramref name="value"/>; false for a game without the
    /// option (the typed accessors ask for the non-manual value, so such a game plays the manual rule).
    /// </summary>
    public bool Is(RuleOptionDescriptor option, string value) =>
        _values.TryGetValue(option.Id, out var v) && v == value;

    /// <summary>The current preset while the values still match it, else the first preset that matches, else Custom.</summary>
    private GameOptions Rebuilt(Dictionary<string, string> values, IReadOnlyList<string> modules, OptionPreset keepPreset)
    {
        var next = new GameOptions(Game, keepPreset, _options, values, modules);
        return keepPreset != OptionPreset.Custom && next.Matches(keepPreset) ? next : next.MatchingOrCustom();
    }

    private GameOptions MatchingOrCustom()
    {
        foreach (var p in new[] { OptionPreset.Manual, OptionPreset.Dos, OptionPreset.Remaster })
            if (Matches(p))
                return new GameOptions(Game, p, _options, _values, Modules);
        return new GameOptions(Game, OptionPreset.Custom, _options, _values, Modules);
    }

    private static string[] DefaultModules(GameId game) =>
        FeatureModuleRegistry.All.Where(d => d.Implemented && d.IsDefaultIn(game)).Select(d => d.Id).ToArray();
}

/// <summary>Which gold cap a save plays by (<see cref="RuleOptionRegistry.GoldCap"/>, D-048).</summary>
public enum GoldCapRule
{
    /// <summary>The game's own caps (<see cref="RulesProfile.PartyGoldCap"/> / <see cref="RulesProfile.BankGoldCap"/>).</summary>
    Manual,

    /// <summary>DOS's 16-bit clamp: <see cref="GoldCaps.Dos"/> (65,535) for the purse and every account.</summary>
    Dos,

    /// <summary>No practical cap: <see cref="GoldCaps.Lifted"/> (QoL).</summary>
    Lifted,
}

/// <summary>The gold caps behind <see cref="GoldCapRule"/>.</summary>
public static class GoldCaps
{
    /// <summary>DOS clamps the purse and the bank words at 65,535 (COM <c>0x2e5d</c>, D-048).</summary>
    public const long Dos = ushort.MaxValue;

    /// <summary>
    /// The lifted cap: <see cref="int.MaxValue"/>, because a bank balance (<see cref="Character.GoldInBank"/>) is an int.
    /// No play reaches it.
    /// </summary>
    public const long Lifted = int.MaxValue;

    /// <summary>The cap <paramref name="rule"/> gives in place of the game's own <paramref name="gameCap"/>.</summary>
    public static long Apply(this GoldCapRule rule, long gameCap) => rule switch
    {
        GoldCapRule.Dos => Dos,
        GoldCapRule.Lifted => Lifted,
        _ => gameCap,
    };
}
