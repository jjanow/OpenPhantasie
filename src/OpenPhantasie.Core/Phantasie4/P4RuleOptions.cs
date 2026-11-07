namespace OpenPhantasie.Core;

/// <summary>
/// Phantasie IV's rules options (T6.3, <c>DIFFERENCES-IV.md</c> D4-001, D4-010…020, D4-030…055). Each is the manual's rule
/// by default (Original (manual) and Remaster presets) and the X68000 code's behaviour as the <c>x68000</c> value (Original
/// (X68000) preset), unless noted: D4-034 and D4-055 choose between the two code platforms (<c>msx2</c> / <c>x68000</c>),
/// D4-031's alternative is <c>uncapped</c> (the user's option), and <c>p4.ship-life</c> / <c>p4.teleport</c> (user s84) are
/// choices with the same value in every preset. Listed in <see cref="RuleOptionRegistry.All"/> through
/// <see cref="All"/>; kept in their own class so their fields are initialised before the registry's list reads them.
/// Typed accessors: <see cref="P4RuleOptionsExtensions"/>. The X68000 preset is the "dos" preset slot
/// (<c>options.preset.dos.p4</c> labels it).
/// </summary>
public static class P4RuleOptions
{
    /// <summary>The value naming the X68000 code's behaviour.</summary>
    public const string X68000 = "x68000";

    /// <summary>D4-001: Nikademus may be an ancestor and counts as Human for the race combination (<c>manual</c>) or every path with him ends in "Not possible" (<c>x68000</c>).</summary>
    public static readonly RuleOptionDescriptor Nikademus = ManualOrCode("p4.nikademus", "D4-001");

    /// <summary>D4-010: Elves and Dwarves roll their social class at <c>rand(100) + 10</c>, cap 100 (<c>manual</c>, user s82) or the same roll as the other base races (<c>x68000</c>).</summary>
    public static readonly RuleOptionDescriptor SocialBias = ManualOrCode("p4.social-bias", "D4-010");

    /// <summary>D4-012: base stride Halfling 5 / Orc 8 (<c>manual</c>) or the code's 6 / 7 (<c>x68000</c>).</summary>
    public static readonly RuleOptionDescriptor Strides = ManualOrCode("p4.strides", "D4-012");

    /// <summary>D4-013: a Gargoyle may be a Thief, Fighter or Gargoyle (<c>manual</c>) or a Gargoyle only (<c>x68000</c>).</summary>
    public static readonly RuleOptionDescriptor GargoyleClasses = ManualOrCode("p4.gargoyle-classes", "D4-013");

    /// <summary>D4-015: Priests train to level 35 and gain the 30 missing spells at 31–35 (<c>manual</c>, user s82) or stop at 30 with 35 spells like every class (<c>x68000</c>).</summary>
    public static readonly RuleOptionDescriptor PriestLevels = ManualOrCode("p4.priest-levels", "D4-015");

    /// <summary>D4-016: Fighters learn only their first 4 minor spells (<c>manual</c>) or the code's 7 (<c>x68000</c>).</summary>
    public static readonly RuleOptionDescriptor FighterSpells = ManualOrCode("p4.fighter-spells", "D4-016");

    /// <summary>D4-018: the Long Sword has 14 points (<c>manual</c>) or 13 (<c>x68000</c>, code and catalogue).</summary>
    public static readonly RuleOptionDescriptor LongSword = ManualOrCode("p4.long-sword", "D4-018");

    /// <summary>D4-019: Thief HP growth = the lowest other class − 1 per level, floor 1 (<c>manual</c>, user s82) or the code's table (<c>x68000</c>).</summary>
    public static readonly RuleOptionDescriptor ThiefHp = ManualOrCode("p4.thief-hp", "D4-019");

    /// <summary>D4-020: an item the party sold is resold at <c>trunc(paid × 1.25)</c> (<c>manual</c>, user s82) or at the shop's full town price (<c>x68000</c>).</summary>
    public static readonly RuleOptionDescriptor Resale = ManualOrCode("p4.resale", "D4-020");

    /// <summary>D4-030: entering a town pays the party gold into the accounts by shares and shares out the experience (<c>manual</c>) or shares out the experience only (<c>x68000</c>).</summary>
    public static readonly RuleOptionDescriptor TownEntry = ManualOrCode("p4.town-entry", "D4-030");

    /// <summary>
    /// D4-031: party gold and every account hold at most 65,000 on every path, the withdraw wrap fixed (<c>manual</c>) or no
    /// cap at all (<c>uncapped</c>, the user's option; also the Original (X68000) preset, as the code's own caps differ by path
    /// and the user named no option for them).
    /// </summary>
    public static readonly RuleOptionDescriptor GoldCap = new(
        "p4.gold-cap", GameId.P4, ["manual", "uncapped"],
        Manual: "manual", Dos: "uncapped", Remaster: "manual", Difference: "D4-031");

    /// <summary>D4-032: a town inn stay takes two weeks (<c>manual</c>) or no time (<c>x68000</c>).</summary>
    public static readonly RuleOptionDescriptor InnTime = ManualOrCode("p4.inn-time", "D4-032");

    /// <summary>D4-033: the manual's lifespans by race (<c>manual</c>, values set s83) or the code's aging factor (<c>x68000</c>).</summary>
    public static readonly RuleOptionDescriptor Lifespans = ManualOrCode("p4.lifespans", "D4-033");

    /// <summary>D4-034: growing older scales the abilities × 0.8 once (<c>msx2</c>) or sets them to 0 (<c>x68000</c>, 0.8 truncated).</summary>
    public static readonly RuleOptionDescriptor Aging = new(
        "p4.aging", GameId.P4, ["msx2", X68000], Manual: "msx2", Dos: X68000, Remaster: "msx2", Difference: "D4-034");

    /// <summary>D4-035: healing potions heal HP and wounds as the manual's Tables 2–3 (<c>manual</c>) or the code's one step lower (<c>x68000</c>).</summary>
    public static readonly RuleOptionDescriptor PotionWounds = ManualOrCode("p4.potion-wounds", "D4-035");

    /// <summary>D4-036: Spot Item finds treasure after a fight; outdoors and on surrender it is automatic (<c>manual</c>) or the code's flat chance (<c>x68000</c>).</summary>
    public static readonly RuleOptionDescriptor SpotItem = ManualOrCode("p4.spot-item", "D4-036");

    /// <summary>D4-037: thrown weapons hit with the Throwing skill (<c>manual</c>) or with Fire Bow (<c>x68000</c>).</summary>
    public static readonly RuleOptionDescriptor Throwing = ManualOrCode("p4.throwing", "D4-037");

    /// <summary>D4-038: ships can be wrecked in a lost or fled sea fight (5 %) and wear out (<c>manual</c>) or not (<c>x68000</c>).</summary>
    public static readonly RuleOptionDescriptor ShipWear = ManualOrCode("p4.ship-wear", "D4-038");

    /// <summary>
    /// D4-038 (user s84): how many sea steps a ship lasts before it wears out under <see cref="ShipWear"/> = <c>manual</c>;
    /// 4000 in every preset, the others the player's choice. The value is the step count.
    /// </summary>
    public static readonly RuleOptionDescriptor ShipLife = new(
        "p4.ship-life", GameId.P4, ["4000", "2000", "8000"], Manual: "4000", Dos: "4000", Remaster: "4000", Difference: "D4-038");

    /// <summary>D4-039: Levitation outside combat lasts 5 × the slowest stride in steps, wilds and dungeons (<c>manual</c>) or until the next fight, dungeons only (<c>x68000</c>).</summary>
    public static readonly RuleOptionDescriptor Levitation = ManualOrCode("p4.levitation", "D4-039");

    /// <summary>D4-040: below Navigation 50 the ship also drifts on a (50 − skill) % roll (<c>manual</c>, user s84) or only the code's drift (<c>x68000</c>); the best navigator steers either way.</summary>
    public static readonly RuleOptionDescriptor Navigation = ManualOrCode("p4.navigation", "D4-040");

    /// <summary>D4-041: Examine is a party search, +20 to the trap and treasure rolls (<c>manual</c>) or the code's automatic walk (<c>x68000</c>).</summary>
    public static readonly RuleOptionDescriptor Examine = ManualOrCode("p4.examine", "D4-041");

    /// <summary>D4-057 (user s98: the manual): outdoor M only switches the view (<c>manual</c>) or also runs the square's step again (<c>x68000</c>).</summary>
    public static readonly RuleOptionDescriptor MapStep = ManualOrCode("p4.map-step", "D4-057");

    /// <summary>D4-042: Attack strikes twice (−10 to hit) and Slash 3–4 times (−20), each for full damage (<c>manual</c>) or every action is one hit roll (<c>x68000</c>).</summary>
    public static readonly RuleOptionDescriptor Strikes = ManualOrCode("p4.strikes", "D4-042");

    /// <summary>D4-043: losing the head or torso kills (<c>manual</c>) or leaves the member Out and healable (<c>x68000</c>).</summary>
    public static readonly RuleOptionDescriptor HeadTorso = ManualOrCode("p4.head-torso", "D4-043");

    /// <summary>
    /// D4-058 (user s99: the manual): an Aim Blow that hits lands on the head (25 %) or the torso (75 %), the split of the
    /// code's own melee roll over those two parts (<c>manual</c>), or where any melee blow lands (<c>x68000</c>; MSX2 and
    /// PC-98 the same).
    /// </summary>
    public static readonly RuleOptionDescriptor AimBlow = ManualOrCode("p4.aim-blow", "D4-058");

    /// <summary>D4-044: the manual's MP costs (<c>manual</c>) or the code's table (<c>x68000</c>).</summary>
    public static readonly RuleOptionDescriptor MpCosts = ManualOrCode("p4.mp-costs", "D4-044");

    /// <summary>D4-045: Party Arrow Flame raises bow hit chance and damage (<c>manual</c>) or damage only (<c>x68000</c>).</summary>
    public static readonly RuleOptionDescriptor PartyArrowFlame = ManualOrCode("p4.party-arrow-flame", "D4-045");

    /// <summary>D4-046: auto combat casts only the manual's p. 32 spell sets (<c>manual</c>) or the code's lists (<c>x68000</c>).</summary>
    public static readonly RuleOptionDescriptor AutoSpells = ManualOrCode("p4.auto-spells", "D4-046");

    /// <summary>D4-047: Flameball burns around the caster by radius, sparing strong armour (<c>manual</c>) or the code's pattern around the target (<c>x68000</c>).</summary>
    public static readonly RuleOptionDescriptor Flameball = ManualOrCode("p4.flameball", "D4-047");

    /// <summary>D4-048: a good Listen on watch wakes the whole party (<c>manual</c>) or only cancels the monsters' free round (<c>x68000</c>).</summary>
    public static readonly RuleOptionDescriptor NightWatch = ManualOrCode("p4.night-watch", "D4-048");

    /// <summary>D4-049: Beg for mercy hands over the party's gold and goods (<c>manual</c>) or leaves gold mod 256 (<c>x68000</c>).</summary>
    public static readonly RuleOptionDescriptor BegMercy = ManualOrCode("p4.beg-mercy", "D4-049");

    /// <summary>D4-050: Weakness lowers the monsters' damage (<c>manual</c>) or their defence against party hits (<c>x68000</c>).</summary>
    public static readonly RuleOptionDescriptor Weakness = ManualOrCode("p4.weakness", "D4-050");

    /// <summary>D4-051: Fire Flash, Mind Blast and Healing do 1–N (<c>manual</c>) or the code's Int-scaled / N − 1 / 0–N rolls with the XP-7 immunity (<c>x68000</c>).</summary>
    public static readonly RuleOptionDescriptor SpellDamage = ManualOrCode("p4.spell-damage", "D4-051");

    /// <summary>D4-052: Slow 2 = ¾ stride, Dissolve over 3 rounds, Fear makes the enemy flee (<c>manual</c>) or the code's ¼, at once, level −6 (<c>x68000</c>). Teleportation: <see cref="Teleport"/>.</summary>
    public static readonly RuleOptionDescriptor SpellWording = ManualOrCode("p4.spell-wording", "D4-052");

    /// <summary>
    /// D4-052 (user s84) / D4-070 (user s103): Teleportation takes the party back to the previous square (<c>previous</c>,
    /// every preset), to a random open square of the same level / outdoor area (<c>random</c>), to the dungeon's entrance
    /// (<c>entrance</c>) or where the X68000 code puts it (<c>stay</c>: previous square outdoors, the fight's square in a
    /// location map).
    /// </summary>
    public static readonly RuleOptionDescriptor Teleport = new(
        "p4.teleport", GameId.P4, ["previous", "random", "entrance", "stay"],
        Manual: "previous", Dos: "previous", Remaster: "previous", Difference: "D4-052");

    /// <summary>D4-053: Identify succeeds on 50 % + 5 % per caster level (<c>manual</c>) or always (<c>x68000</c>).</summary>
    public static readonly RuleOptionDescriptor Identify = ManualOrCode("p4.identify", "D4-053");

    /// <summary>D4-054: Monster Evaluation is off by 1 on (50 − 5 × caster level, min 10) % of casts (<c>manual</c>) or never (<c>x68000</c>).</summary>
    public static readonly RuleOptionDescriptor MonsterEval = ManualOrCode("p4.monster-eval", "D4-054");

    /// <summary>D4-055 (manual silent): monster HP 0.5–1.4 × base (<c>msx2</c>, user s82) or the X68000's 1.8 × for 80 % (<c>x68000</c>).</summary>
    public static readonly RuleOptionDescriptor MonsterHp = new(
        "p4.monster-hp", GameId.P4, ["msx2", X68000], Manual: "msx2", Dos: X68000, Remaster: "msx2", Difference: "D4-055");

    /// <summary>
    /// D4-056 (manual silent; user s84: a bug): ancestors whose bonus-free average is below 5.0 inherit with the table's last
    /// exponent, k = 5.635, weak dice (<c>manual</c>) or the code's run-off denormal, every die a 6 (<c>x68000</c>).
    /// </summary>
    public static readonly RuleOptionDescriptor WeakAncestors = ManualOrCode("p4.weak-ancestors", "D4-056");

    /// <summary>
    /// D4-060 (PENDING, s102): training past level 15 goes on to 16, 17, … (<c>msx2</c>, as MSX2 and the manual's higher
    /// levels) or wraps the level to (L + 1) mod 15, so level 15 trains back to level 1 (<c>x68000</c>).
    /// </summary>
    public static readonly RuleOptionDescriptor LevelWrap = new(
        "p4.level-wrap", GameId.P4, ["msx2", X68000], Manual: "msx2", Dos: X68000, Remaster: "msx2", Difference: "D4-060");

    /// <summary>
    /// D4-061 (PENDING, s102): which spells can be cast outside combat in town / outdoors / dungeons: the manual's pp. 54–55
    /// lists (<c>manual</c>: no Levitation in town, Divine Aid also outdoors) or the code's (<c>x68000</c>).
    /// </summary>
    public static readonly RuleOptionDescriptor CastPlaces = ManualOrCode("p4.cast-places", "D4-061");

    /// <summary>
    /// D4-062 (user s103): P Party info shows the full experience gained (pool × 10) and the gold gained since leaving town
    /// (<c>manual</c>) or the code's pool as stored (a tenth) and the whole purse (<c>x68000</c>).
    /// </summary>
    public static readonly RuleOptionDescriptor PartyInfo = ManualOrCode("p4.party-info", "D4-062");

    /// <summary>
    /// D4-072 (user s109: option C): the outdoor M overview shows the code's fixed 3 × 3 block that holds the party's map
    /// (<c>x68000</c>, every preset) or the 3 × 3 maps centred on it, the manual's "around the current area" (<c>centred</c>;
    /// cells past the world's edge are black, the Netherworld still shows only the party's own map). Display only.
    /// </summary>
    public static readonly RuleOptionDescriptor Overview = new(
        "p4.overview", GameId.P4, [X68000, "centred"], Manual: X68000, Dos: X68000, Remaster: X68000, Difference: "D4-072");

    /// <summary>
    /// Loading a save made in a location map (user s111; the manual is silent). <c>x68000</c> (every preset): the original's
    /// reload, since its save holds no map: the map is read fresh from <c>O_0xx</c> (event changes to walls and floors are
    /// lost unless the map-load trigger redoes them from flags), <c>_dreset</c> rotates the location flags once more, the
    /// doorway marks are recomputed, the map-load event runs, and saving there works only at the location map's command
    /// prompt (<c>_syscon</c> is reached only from <c>_dmain</c>). <c>saved-map</c>: the remaster's save keeps the live map
    /// and the loop, mid-event included, and goes on as if never stopped.
    /// </summary>
    public static readonly RuleOptionDescriptor DungeonReload = new(
        "p4.dungeon-reload", GameId.P4, [X68000, "saved-map"], Manual: X68000, Dos: X68000, Remaster: X68000);

    /// <summary>
    /// Spell 64, Annihilate Enemy (QoL, user s117 from D4-071). <c>x68000</c> (every preset; off = the original): the code, the
    /// cast only empties the caster's MP. <c>divine</c>: a remaster effect, since no original or manual has
    /// one: with M = the caster's MP at the cast (at least the cost), every monster on the field takes 2M + rand(2M) − 1
    /// (2M … 4M − 1), no resistance roll, no immunity, no armour, never the party; MP then 0 as in the code.
    /// </summary>
    public static readonly RuleOptionDescriptor Annihilate = new(
        "p4.annihilate", GameId.P4, [X68000, "divine"], Manual: X68000, Dos: X68000, Remaster: X68000, Difference: "D4-071");

    /// <summary>
    /// T5 (user s72; the manual p. 8 and the code: Hall only, D4-017): the Hall's T can also bring a Phantasie III character
    /// into IV's Guild as a playable character (<c>guild</c>, <c>ancestry.md</c> section 8.3) or only into the Hall
    /// (<c>hall</c>, the original, every preset).
    /// </summary>
    public static readonly RuleOptionDescriptor TransferJoin = new(
        "p4.transfer-join", GameId.P4, ["hall", "guild"], Manual: "hall", Dos: "hall", Remaster: "hall", Difference: "D4-017");

    /// <summary>
    /// T5 (user s72): the Hall's T takes characters from the remaster's Phantasie I and II saves too (<c>all</c>,
    /// <c>ancestry.md</c> section 8.4) or from Phantasie III only (<c>iii</c>, the original, every preset).
    /// </summary>
    public static readonly RuleOptionDescriptor TransferGames = new(
        "p4.transfer-games", GameId.P4, ["iii", "all"], Manual: "iii", Dos: "iii", Remaster: "iii");

    /// <summary>Every IV option in menu order (creation, training, towns, combat, spells, outdoors).</summary>
    public static IReadOnlyList<RuleOptionDescriptor> All { get; } =
    [
        Nikademus, SocialBias, Strides, GargoyleClasses, WeakAncestors, PriestLevels, FighterSpells, ThiefHp, LevelWrap,
        LongSword, TownEntry, GoldCap, InnTime, Resale, Lifespans, Aging, PotionWounds,
        Strikes, HeadTorso, AimBlow, Throwing, PartyArrowFlame, Weakness, Flameball, SpellDamage, SpellWording, Teleport, MpCosts,
        Annihilate, AutoSpells,
        MonsterHp, NightWatch, BegMercy, SpotItem, Identify, MonsterEval, CastPlaces, PartyInfo,
        Levitation, Navigation, ShipWear, ShipLife, Examine, MapStep, Overview, DungeonReload,
        TransferJoin, TransferGames,
    ];

    private static RuleOptionDescriptor ManualOrCode(string id, string difference) =>
        new(id, GameId.P4, ["manual", X68000], Manual: "manual", Dos: X68000, Remaster: "manual", Difference: difference);
}

/// <summary>The IV rules a save plays by, read from its <see cref="GameOptions"/> (the manual's for a game without them).</summary>
public static class P4RuleOptionsExtensions
{
    /// <summary>True when <paramref name="option"/> plays the X68000 code (or <c>uncapped</c> for D4-031).</summary>
    public static bool P4Code(this GameOptions o, RuleOptionDescriptor option) =>
        o.Is(option, P4RuleOptions.X68000);

    /// <summary>T5: the Hall's T may put a III character into the Guild (<c>p4.transfer-join</c> = <c>guild</c>).</summary>
    public static bool P4TransferJoinsGuild(this GameOptions o) => o.Is(P4RuleOptions.TransferJoin, "guild");

    /// <summary>T5: the Hall's T also reads Phantasie I and II (<c>p4.transfer-games</c> = <c>all</c>).</summary>
    public static bool P4TransferFromAllGames(this GameOptions o) => o.Is(P4RuleOptions.TransferGames, "all");

    public static bool P4GoldUncapped(this GameOptions o) => o.Is(P4RuleOptions.GoldCap, "uncapped");

    public static bool P4X68000Aging(this GameOptions o) => o.Is(P4RuleOptions.Aging, P4RuleOptions.X68000);

    public static bool P4X68000MonsterHp(this GameOptions o) => o.Is(P4RuleOptions.MonsterHp, P4RuleOptions.X68000);

    /// <summary>D4-060: the X68000's level wrap at 16.</summary>
    public static bool P4X68000LevelWrap(this GameOptions o) => o.Is(P4RuleOptions.LevelWrap, P4RuleOptions.X68000);

    /// <summary>D4-038: the sea steps a ship lasts (<c>p4.ship-life</c>; 4000 for a game without the option).</summary>
    public static int P4ShipLifeSteps(this GameOptions o) =>
        o.Values.TryGetValue(P4RuleOptions.ShipLife.Id, out var v)
            ? int.Parse(v, System.Globalization.CultureInfo.InvariantCulture)
            : int.Parse(P4RuleOptions.ShipLife.Manual, System.Globalization.CultureInfo.InvariantCulture);
}
