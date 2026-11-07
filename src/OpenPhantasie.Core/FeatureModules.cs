namespace OpenPhantasie.Core;

/// <summary>
/// The four games of the series (Phase 09). <see cref="GameIdExtensions.Prefix"/> gives the <c>p1:</c>…<c>p4:</c> id prefix
/// that T1.2 puts on data ids.
/// </summary>
public enum GameId
{
    P1 = 1,
    P2 = 2,
    P3 = 3,
    P4 = 4,
}

public static class GameIdExtensions
{
    /// <summary>The id prefix without the colon: <c>p1</c>…<c>p4</c>.</summary>
    public static string Prefix(this GameId game) => game switch
    {
        GameId.P1 => "p1",
        GameId.P2 => "p2",
        GameId.P3 => "p3",
        GameId.P4 => "p4",
        _ => throw new ArgumentOutOfRangeException(nameof(game)),
    };
}

/// <summary>
/// What a feature module is (Phase 09 T1.6): its id, its home game (on by default there, an option elsewhere), any other
/// games that have it by default per the manuals, and whether its rules are decoded and built. A module that is not
/// <see cref="Implemented"/> is a placeholder: it has no behaviour and can't be enabled. A module that is not
/// <see cref="CrossGame"/> is a game's own combat core (its order set): an option only in the games that have it by
/// default, never offered elsewhere (the cross-game variants are T5's).
/// </summary>
public sealed record FeatureModuleDescriptor(
    string Id,
    GameId HomeGame,
    bool Implemented,
    string Summary,
    string Source,
    IReadOnlyList<GameId>? AlsoDefaultIn = null,
    bool CrossGame = true,
    bool BuiltIntoHome = false)
{
    /// <summary>
    /// Can be switched on or off in <paramref name="game"/>'s rules. A <see cref="BuiltIntoHome"/> module is no switch in
    /// its home game (the rule is part of that game's own code there).
    /// </summary>
    public bool IsOptionalIn(GameId game) =>
        Implemented && !(BuiltIntoHome && game == HomeGame) && (CrossGame || IsDefaultIn(game));

    /// <summary>
    /// On by default in <paramref name="game"/>: its home game, or a game the manuals say has it unchanged. Never for a
    /// <see cref="BuiltIntoHome"/> module: its home game plays the rule without the module, so the module set stays as it was.
    /// </summary>
    public bool IsDefaultIn(GameId game) =>
        !BuiltIntoHome && (HomeGame == game || (AlsoDefaultIn?.Contains(game) ?? false));
}

/// <summary>A pluggable piece of game rules. Behaviour comes from the hook interfaces it also implements.</summary>
public interface IFeatureModule
{
    FeatureModuleDescriptor Descriptor { get; }
}

/// <summary>One member's order, as chosen from the combat menu.</summary>
public readonly record struct CombatOrderRequest(
    Character Member,
    CombatantPrep Prep,
    CombatChoice Choice,
    int RankIndex,
    int SpellIndex,
    int HealTargetIndex,
    GameOptions? Rules = null,
    CombatEncounter? Encounter = null);

/// <summary>
/// Combat-action hook: a module adds menu orders, packs them at order time and resolves the packed actions it owns.
/// Modules are asked in set order and the first that answers wins, so two modules must not claim the same order or
/// packed value.
/// </summary>
public interface ICombatActionModule : IFeatureModule
{
    /// <summary>The menu orders this module adds, in menu order.</summary>
    IReadOnlyList<CombatChoice> Choices { get; }

    /// <summary>Pack <paramref name="request"/> if this module owns its order (may roll, like PARRY).</summary>
    bool TryChoose(in CombatOrderRequest request, IRandomSource rng, out int packedAction);

    /// <summary>Resolve member <paramref name="memberIndex"/>'s packed action if this module owns it.</summary>
    bool TryResolve(CombatEncounter encounter, int memberIndex, int packedAction, IRandomSource rng, List<string> log);

    /// <summary>
    /// Whether the menu asks "which rank?" for <paramref name="choice"/> by member <paramref name="memberIndex"/>; null =
    /// not this module's order (the front end's own rule then applies: I's Thieves).
    /// </summary>
    bool? AsksRank(CombatEncounter encounter, int memberIndex, CombatChoice choice) => null;
}

/// <summary>
/// Monster-melee hook: a module that resolves a monster's melee turn in place of Phantasie I's (<c>0xd73</c>). The first
/// module that answers true wins; with none, I's melee runs unchanged.
/// </summary>
public interface IMonsterMeleeModule : IFeatureModule
{
    /// <summary>Monster <paramref name="monster"/> (group <paramref name="group"/>) melees; true when handled.</summary>
    bool TryMonsterMelee(
        CombatEncounter encounter, MonsterRank group, Monster monster, MonsterMeleeRider rider, IRandomSource rng,
        List<string> log);
}

/// <summary>Where a hit came from: combat (melee, specials, spells) or the dungeon and wilderness outside combat.</summary>
public enum DamageSource
{
    /// <summary>A party member's melee swing on a monster.</summary>
    PartyMelee,

    /// <summary>A monster's melee swing on a party member.</summary>
    MonsterMelee,

    /// <summary>A monster special (spell or breath) on a party member.</summary>
    MonsterSpecial,

    /// <summary>A party member's combat spell on a monster (FIREFLASH, the area spells; <see cref="DamageHit.SpellIndex"/>).</summary>
    PartySpell,

    /// <summary>A sprung dungeon trap on a party member (outside combat; <see cref="DamageHit.TrapLevel"/>).</summary>
    Trap,

    /// <summary>A dungeon event script's damage ops 1–4 on a party member (outside combat).</summary>
    DungeonScript,

    /// <summary>A failed swim roll on a water tile (outside combat).</summary>
    Swimming,

    /// <summary>A party member's tossed rock on a monster (Phantasie II's Toss Rock, <see cref="P2RockThrowingModule"/>).</summary>
    PartyRock,

    /// <summary>A step onto molten lava (Phantasie II, outside combat; <see cref="Lava"/>).</summary>
    Lava,
}

/// <summary>
/// One hit about to be applied. Exactly one of <see cref="Member"/> / <see cref="Monster"/> is set.
/// <see cref="Amount"/> is already floored (<see cref="CombatMath.ApplyDamageFloor"/>, or the 1-HP floor of the
/// out-of-combat damage step). <see cref="Encounter"/> is null for hits outside combat (traps, scripts, swimming).
/// </summary>
public sealed class DamageHit
{
    public CombatEncounter? Encounter { get; init; }
    public required Party Party { get; init; }
    public required DamageSource Source { get; init; }
    public required IRandomSource Rng { get; init; }
    public Character? Member { get; init; }
    public Monster? Monster { get; init; }

    /// <summary>The spell (<see cref="SpellId"/> index) for <see cref="DamageSource.PartySpell"/>, else null.</summary>
    public int? SpellIndex { get; init; }

    /// <summary>The trap's level <c>a</c> for <see cref="DamageSource.Trap"/>, else null.</summary>
    public int? TrapLevel { get; init; }

    public int Amount { get; set; }
}

/// <summary>
/// Damage hook (the seam for III's body-part injury): runs around the HP subtraction of every hit, in combat (melee,
/// monster specials, party spells) and outside it (traps, dungeon scripts, swimming); <see cref="DamageHit.Source"/>
/// says which. <see cref="BeforeDamage"/> may change <see cref="DamageHit.Amount"/>;
/// <see cref="AfterDamage"/> sees the result. Phantasie I has no damage module: its HP rule is the engine's base step.
/// </summary>
public interface IDamageModule : IFeatureModule
{
    void BeforeDamage(DamageHit hit, List<string> log);

    void AfterDamage(DamageHit hit, bool killed, List<string> log);
}

/// <summary>
/// Every module the remaster knows. Kept in code, not JSON: a module id only means something with C# behind it, and its
/// home game is a fact from the T0 feature matrix (<c>09-trilogy-matrix.md</c> §2), not a number to tune. The numbers a
/// module uses belong in the per-game rules data (T1.3); the user's on/off choice belongs in settings and saves (T5).
/// </summary>
public static class FeatureModuleRegistry
{
    /// <summary>
    /// The six orders THRUST / ATTACK / SLASH / LUNGE / CAST / PARRY (OV2 <c>0x6bc5</c>, D-011, D-012), party melee and
    /// combat casting (<see cref="SpellCaster"/>).
    /// </summary>
    public static readonly FeatureModuleDescriptor P1CombatOrders = new(
        "p1.combat-orders", GameId.P1, Implemented: true,
        "THRUST, ATTACK, SLASH, LUNGE, CAST and PARRY orders, party melee and combat spells",
        "I manual p.10; combat-resolution.md; II keeps them unchanged (09-trilogy-manual-p2.md: CB pp.9-10). "
        + "III has its own set (p3.combat-actions)",
        AlsoDefaultIn: [GameId.P2], CrossGame: false);

    /// <summary>
    /// III's combat (<see cref="P3CombatModule"/>): the action set Attack / Lunge / Slash / Thrust / Fire Bow / Parry / Aim /
    /// Cast with III's fallbacks (D3-010…013), party ranks, body-area injury both ways (D3-021), bows with Arrow Flame and
    /// Accuracy (D3-017) and monster melee (D3-018 / 019). Needs III's fight state (<see cref="P3Fight"/>), so it is III's
    /// own for now; the separate cross-game parts below stay placeholders until T5.
    /// </summary>
    public static readonly FeatureModuleDescriptor P3CombatActions = new(
        "p3.combat-actions", GameId.P3, Implemented: true,
        "III's actions (Lunge, Slash, Thrust, Fire Bow, Aim), ranks, body injury, bows and monster melee",
        "III pr.10-12, 23-24; reverse-engineering/dos/phantasie3/logic-specs/combat-actions-and-ranks.md, body-areas.md, "
        + "bows.md",
        CrossGame: false);

    /// <summary>
    /// II's "7) Toss Rock" (<see cref="P2RockThrowingModule"/>, <c>toss-rock.md</c>): the TOSS order and the TOSS skill's
    /// growth at creation and level-up (<see cref="TossRockRules"/>).
    /// </summary>
    public static readonly FeatureModuleDescriptor RockThrowing = new(
        "p2.rock-throwing", GameId.P2, Implemented: true,
        "Toss Rock: any rank, unlimited rocks, hit on the TOSS skill",
        "09-trilogy-matrix.md §2; II rulebook CB p.27; reverse-engineering/atari-st/phantasie2/logic-specs/toss-rock.md");

    /// <summary>
    /// IV's ancestry at creation offered in I, II and III (T5, user s72: base races only): Guild New can make a descendant
    /// of two same-race ancestors (a new person or one from the shared Hall list) with IV's inheritance (<see cref="LineageRules"/>,
    /// <c>ancestry.md</c> section 13). IV always has it in its own creation (<see cref="FeatureModuleDescriptor.BuiltIntoHome"/>).
    /// </summary>
    public static readonly FeatureModuleDescriptor Lineage = new(
        "p4.lineage", GameId.P4, Implemented: true,
        "Descendants at creation: two same-race ancestors, abilities inherited by IV's rule",
        "IV manual p.17; reverse-engineering/x68000/phantasie4/logic-specs/ancestry.md sections 3, 5 and 13; "
        + "09-trilogy.md Open user decisions (s72)",
        BuiltIntoHome: true);

    /// <summary>Placeholder: III's body-part injury (damage hook). Not decoded.</summary>
    public static readonly FeatureModuleDescriptor BodyPartInjury = new(
        "p3.body-part-injury", GameId.P3, Implemented: false,
        "Head/Torso/arms/legs, Okay/Injured/Broken/Gone, on adventurers and monsters (not decoded)",
        "09-trilogy-matrix.md §2; 09-trilogy-manual-p3.md");

    /// <summary>Placeholder: III's bows ("8) Fire", Fire Bow skill). Not decoded.</summary>
    public static readonly FeatureModuleDescriptor Bows = new(
        "p3.bows", GameId.P3, Implemented: false,
        "Bow slot, Fire Bow skill, Fire order; replaces II's rocks (not decoded)",
        "09-trilogy-matrix.md §2; III PDF 4, 8");

    /// <summary>Placeholder: III's Aim (one blow at head or torso). Not decoded.</summary>
    public static readonly FeatureModuleDescriptor Aim = new(
        "p3.aim", GameId.P3, Implemented: false,
        "Aim: one blow at head or torso, lower hit chance (not decoded)",
        "09-trilogy-matrix.md §2; III PDF 8-9");

    /// <summary>Placeholder: III's party ranks Front / Middle / Back. Not decoded.</summary>
    public static readonly FeatureModuleDescriptor PartyRanks = new(
        "p3.party-ranks", GameId.P3, Implemented: false,
        "Front/Middle/Back ranks with relative hit and defence bonus (not decoded)",
        "09-trilogy-matrix.md §2; III PDF 8");

    /// <summary>All known modules in menu order: built ones first, then the placeholders.</summary>
    public static IReadOnlyList<FeatureModuleDescriptor> All { get; } =
        [P1CombatOrders, P3CombatActions, RockThrowing, Lineage, BodyPartInjury, Bows, Aim, PartyRanks];

    public static FeatureModuleDescriptor? Find(string id) =>
        All.FirstOrDefault(d => string.Equals(d.Id, id, StringComparison.Ordinal));

    /// <summary>A fresh instance of a built module; throws for a placeholder or an unknown id.</summary>
    public static IFeatureModule Create(string id)
    {
        var descriptor = Find(id) ?? throw new ArgumentException($"Unknown feature module '{id}'.", nameof(id));
        if (!descriptor.Implemented)
            throw new NotSupportedException($"Feature module '{id}' is not decoded yet and has no behaviour.");
        return id switch
        {
            "p1.combat-orders" => new P1CombatOrdersModule(),
            "p2.rock-throwing" => new P2RockThrowingModule(),
            "p3.combat-actions" => new P3CombatModule(),
            "p4.lineage" => new LineageModule(),
            _ => throw new NotSupportedException($"Feature module '{id}' has no implementation."),
        };
    }
}

/// <summary>
/// The modules switched on for one game session. Built from a game's defaults (<see cref="ForGame"/>) with optional
/// overrides, or from explicit instances (<see cref="Create"/>, for tests and future options). Immutable.
/// </summary>
public sealed class FeatureModuleSet
{
    private readonly ICombatActionModule[] _actions;
    private readonly IDamageModule[] _damage;
    private readonly IMonsterMeleeModule[] _monsterMelee;

    private FeatureModuleSet(GameId game, IReadOnlyList<IFeatureModule> modules)
    {
        Game = game;
        Modules = modules;
        _actions = modules.OfType<ICombatActionModule>().ToArray();
        _damage = modules.OfType<IDamageModule>().ToArray();
        _monsterMelee = modules.OfType<IMonsterMeleeModule>().ToArray();
        CombatChoices = _actions.SelectMany(a => a.Choices).ToArray();
    }

    /// <summary>The game whose defaults this set started from.</summary>
    public GameId Game { get; }

    public IReadOnlyList<IFeatureModule> Modules { get; }

    /// <summary>The combat menu: every enabled module's orders, in module order.</summary>
    public IReadOnlyList<CombatChoice> CombatChoices { get; }

    /// <summary>Phantasie I's module set (today's behaviour). Combat falls back to it when no set is given.</summary>
    public static FeatureModuleSet PhantasieI { get; } = ForGame(GameId.P1);

    /// <summary>
    /// The built modules on by default in <paramref name="game"/>, plus <paramref name="enable"/>, minus
    /// <paramref name="disable"/>, in registry order. Enabling a placeholder throws <see cref="NotSupportedException"/>.
    /// A game's own placeholders are left out (see <see cref="MissingDefaults"/>).
    /// </summary>
    public static FeatureModuleSet ForGame(
        GameId game, IEnumerable<string>? enable = null, IEnumerable<string>? disable = null)
    {
        var on = new HashSet<string>(StringComparer.Ordinal);
        foreach (var d in FeatureModuleRegistry.All)
            if (d.Implemented && d.IsDefaultIn(game))
                on.Add(d.Id);
        foreach (var id in enable ?? [])
        {
            var d = FeatureModuleRegistry.Find(id) ?? throw new ArgumentException($"Unknown feature module '{id}'.");
            if (!d.Implemented)
                throw new NotSupportedException($"Feature module '{id}' is not decoded yet and can't be enabled.");
            if (!d.IsOptionalIn(game))
                throw new NotSupportedException($"Feature module '{id}' is {d.HomeGame.Prefix()}'s own and can't be used in {game.Prefix()}.");
            on.Add(id);
        }
        foreach (var id in disable ?? [])
            on.Remove(id);
        var modules = FeatureModuleRegistry.All
            .Where(d => on.Contains(d.Id))
            .Select(d => FeatureModuleRegistry.Create(d.Id))
            .ToArray();
        return new FeatureModuleSet(game, modules);
    }

    /// <summary>A set of explicit module instances, in the given order (tests, future custom modules).</summary>
    public static FeatureModuleSet Create(GameId game, params IFeatureModule[] modules)
    {
        ArgumentNullException.ThrowIfNull(modules);
        foreach (var m in modules)
            ArgumentNullException.ThrowIfNull(m);
        return new FeatureModuleSet(game, modules.ToArray());
    }

    /// <summary>Modules that should be on by default in <paramref name="game"/> but are not built yet.</summary>
    public static IReadOnlyList<FeatureModuleDescriptor> MissingDefaults(GameId game) =>
        FeatureModuleRegistry.All.Where(d => !d.Implemented && d.IsDefaultIn(game)).ToArray();

    public bool IsEnabled(string id) => Modules.Any(m => m.Descriptor.Id == id);

    /// <summary>
    /// The TOSS skill rules while Toss Rock is on (<see cref="P2RockThrowingModule"/>), else null: creation and level-up grow
    /// TOSS only when this is set (II by default, any other game that switches the module on).
    /// </summary>
    public TossRockRules? TossRock => Modules.OfType<P2RockThrowingModule>().FirstOrDefault()?.Rules;

    /// <summary>Pack a menu order through the module that owns it.</summary>
    public int Choose(in CombatOrderRequest request, IRandomSource rng)
    {
        ArgumentNullException.ThrowIfNull(request.Member);
        ArgumentNullException.ThrowIfNull(request.Prep);
        ArgumentNullException.ThrowIfNull(rng);
        foreach (var a in _actions)
            if (a.TryChoose(request, rng, out var packed))
                return packed;
        throw new ArgumentOutOfRangeException(nameof(request), request.Choice, "No enabled module offers this order.");
    }

    /// <summary>Resolve a member's packed action; an action no module owns does nothing.</summary>
    internal void ResolvePartyAction(
        CombatEncounter encounter, int memberIndex, int packedAction, IRandomSource rng, List<string> log)
    {
        foreach (var a in _actions)
            if (a.TryResolve(encounter, memberIndex, packedAction, rng, log))
                return;
    }

    /// <summary>A monster's melee turn through the first module that takes it; false = Phantasie I's melee.</summary>
    internal bool ResolveMonsterMelee(
        CombatEncounter encounter, MonsterRank group, Monster monster, MonsterMeleeRider rider, IRandomSource rng,
        List<string> log)
    {
        foreach (var m in _monsterMelee)
            if (m.TryMonsterMelee(encounter, group, monster, rider, rng, log))
                return true;
        return false;
    }

    /// <summary>The menu's rank question for an order (<see cref="ICombatActionModule.AsksRank"/>); null = no module rule.</summary>
    public bool? AsksRank(CombatEncounter encounter, int memberIndex, CombatChoice choice)
    {
        foreach (var a in _actions)
            if (a.AsksRank(encounter, memberIndex, choice) is { } asks)
                return asks;
        return null;
    }

    /// <summary>Run every damage module's <see cref="IDamageModule.BeforeDamage"/>; returns the (possibly changed) amount.</summary>
    internal int BeforeDamage(DamageHit hit, List<string> log)
    {
        foreach (var d in _damage)
            d.BeforeDamage(hit, log);
        return hit.Amount;
    }

    internal void AfterDamage(DamageHit hit, bool killed, List<string> log)
    {
        foreach (var d in _damage)
            d.AfterDamage(hit, killed, log);
    }

    internal bool HasDamageModules => _damage.Length > 0;
}
