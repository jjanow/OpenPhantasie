namespace OpenPhantasie.Core;

/// <summary>
/// One game's campaign (Phase 09 T1.4): the parts of play that belong to a single game of the series and not to the shared
/// engine. <see cref="GameSession"/> is the shared loop (town, travel, dungeon, combat, saves) and calls these hooks at the
/// points where a game's own content takes over. A session has exactly one campaign, and its <see cref="Game"/> is the
/// session's game: the rules profile, the feature module set, the loaded data and every save are checked against it.
/// <para>
/// Hooks are stateless: they get the session and work through its public API plus <see cref="GameSession.CampaignAccess"/>
/// (the RNG stream, the live dungeon level, the endgame and prompt state), so the state they touch stays on the session
/// where saves and resets already handle it. The hook types are game-neutral; a campaign's own types (I: the DNG event
/// script, <see cref="DungeonScriptResult"/>) stay inside it. Phantasie I is
/// <see cref="Phantasie1Campaign"/>; II–IV add their own once decoded (T3, T4, T6).
/// </para>
/// </summary>
public interface ICampaign
{
    /// <summary>The game this campaign is. The single source of "which game is this session".</summary>
    GameId Game { get; }

    /// <summary>The feature modules a new session of this game starts with (T1.6 defaults).</summary>
    FeatureModuleSet DefaultFeatureModules { get; }

    /// <summary>What happens when a party member's HP reaches 0 (I: DEAD and the D-009 CON loss).</summary>
    IMemberDeathRule Death { get; }

    // --- The game's world: values the shared loop asks for ---------------------------

    /// <summary>The town a new game starts in (I: town 1, Pelnor).</summary>
    int StartTownId { get; }

    /// <summary>
    /// The towns a town TRANSPORTATION cast lists, in picker order, before the loop leaves out the current town
    /// (I: the 12 TWNS records, Core ids 1..12; D-062).
    /// </summary>
    IReadOnlyList<int> TransportTowns { get; }

    /// <summary>True when the TRANSPORTATION picker prints town <paramref name="townId"/> with a blank name (I: OLYMPIA).</summary>
    bool IsUnnamedTransportTown(int townId);

    /// <summary>
    /// Town tile feature args that name another town than their own number (I: OUT18's arg 99 is OLYMPIA, town 12).
    /// <c>GameDataRepository.CreateTraversal</c> hands them to the overworld traversal.
    /// </summary>
    IReadOnlyDictionary<int, int> OverworldTownAliases { get; }

    /// <summary>The line a blocked wilderness step prints in region <paramref name="regionId"/> (I: the ocean or the River Styx).</summary>
    string BlockedMessage(int regionId);

    /// <summary>
    /// Monsters (0-based) the debug fight (<see cref="GameSession.BeginCombat"/> with no monster) picks from when the
    /// session was built without a pool. Game paths always name their monster.
    /// </summary>
    IReadOnlyList<int> FallbackMonsterPool { get; }

    /// <summary>
    /// True when item <paramref name="itemId"/> may drop as treasure on dungeon level <paramref name="dungeonLevelId"/>
    /// (null = the wilderness). I: SCROLL 17 only inside the Fortress of the Black Knights (DNG9).
    /// </summary>
    bool MayDropItem(int itemId, int? dungeonLevelId);

    /// <summary>
    /// True when a dungeon fight whose first monster is <paramref name="firstMonsterId"/> (0-based) drops no item treasure:
    /// the presence roll is skipped. I: the BLACK LORD (<see cref="Phantasie1Campaign.BlackLordId"/>).
    /// </summary>
    bool WithholdsDungeonTreasure(int firstMonsterId);

    // --- Hooks: points where the game's own content takes over -------------------------

    /// <summary>A wilderness step onto a special-site tile (<see cref="OverworldMoveKind.SpecialSite"/>).</summary>
    void OnSpecialSite(GameSession session, OverworldMoveResult result);

    /// <summary>
    /// A dungeon step onto a scripted-event cell (<see cref="DungeonMoveKind.ScriptedEvent"/>). A campaign that asks a
    /// question sets <see cref="CampaignAccess.DungeonEventPrompt"/>; the loop saves it and clears it on the next step.
    /// </summary>
    void BeginDungeonEvent(GameSession session, DungeonMoveResult move);

    /// <summary>Answer the pending <see cref="GameSession.DungeonEventPrompt"/> (1-based choice).</summary>
    void AnswerDungeonEvent(GameSession session, int choice);

    /// <summary>
    /// A treasure offer that stopped an event (<see cref="GameSession.PendingDungeonScript"/>) was answered: run the rest
    /// of the event.
    /// </summary>
    void ResumeDungeonScript(GameSession session);

    /// <summary>True when a dungeon fight on level <paramref name="dungeonLevelId"/> uses the Black Knight size factor.</summary>
    bool IsBlackKnightSpot(int dungeonLevelId);

    /// <summary>Town M)YSTIC (I: the score and the readiness tiers, OV1 <c>0x20bb</c>).</summary>
    TownTransactionResult ConsultMystic(GameSession session);

    /// <summary>
    /// After any end of combat that did not wipe the party (rewards paid, mode restored, dungeon cell handled): the game's
    /// win condition (I: the Black Lord's death).
    /// </summary>
    void AfterCombatSurvived(GameSession session, CombatEncounter encounter, CombatResult result);

    /// <summary>
    /// Party defeat (state 6). The engine has already cleared fights, dungeon and prompts; the campaign judges the party,
    /// logs it and puts the party where it wakes up.
    /// </summary>
    PartyDefeatResult ResolvePartyDefeat(GameSession session);

    // --- Optional hooks (default: no effect; Phantasie I uses none of them) -------------

    /// <summary>
    /// Right after a fight was built (II: Pluto's minions, <c>encounter_start 0x09bf4</c>). Return a 0-based monster to
    /// rebuild the fight with (the loop starts a fresh encounter, which draws and counts as a new build), or null to keep it.
    /// </summary>
    int? ReplaceEncounter(GameSession session, CombatEncounter encounter) => null;

    /// <summary>
    /// Guild N)EW's roll by the game's own creation (III: ovl2 <c>0x227d</c>, its draw order and 3d6 attributes); null =
    /// the shared <see cref="CharacterCreation.Create(CharacterCreationRequest, CharacterCreationTables, IDosRandomSource, RulesProfile)"/>.
    /// The character is not enrolled yet and has no name. <paramref name="abilities"/> (T5 lineage) replaces the five
    /// attribute rolls when given (clamped to the game's own range); null = the game's own roll.
    /// </summary>
    Character? RollNewMember(GameSession session, Race race, CharacterClass cls, bool randomRace,
        IReadOnlyList<int>? abilities) => null;

    /// <summary>A Guild N)EW character was enrolled in the current town (II: ORIGIN by plane).</summary>
    void OnCharacterEnrolled(GameSession session, Character character)
    {
    }

    /// <summary>
    /// A spell cast outside combat that the game handles itself (II: DIVINE II's summons). Return the result, or null to
    /// let the shared <see cref="SpellCaster"/> cast it.
    /// </summary>
    MagicResult? CastFieldSpell(GameSession session, Character caster, int spellIndex) => null;

    /// <summary>
    /// Town U)SE ITEM on an item the game gives its own use (II: amulets 141–148 EXAMINE / WEAR / LEAVE, the wand of Filmon).
    /// Returns the menu the item opens (empty = used at once with option 0), or null when the item has no such use.
    /// </summary>
    IReadOnlyList<string>? ItemUseOptions(GameSession session, Character user, int itemId) => null;

    /// <summary>Use <paramref name="itemId"/> with the 0-based <paramref name="option"/> of <see cref="ItemUseOptions"/>.</summary>
    TownTransactionResult UseItem(GameSession session, Character user, int itemId, int option) =>
        throw new NotSupportedException($"Item {itemId} has no campaign use.");

    /// <summary>
    /// The game's own strength gate: can <paramref name="member"/> use <paramref name="itemId"/> (shields, armour, weapons;
    /// any other item passes)? The inventory screen, the Armory's UNUSABLE and the hand-out's "who can use" column ask this
    /// (<see cref="GameSession.CanWear"/>, <see cref="GameSession.Gear"/>). Default: I's <see cref="ItemRules.IsUsable"/>
    /// (DOS OV1 <c>0x3053</c>, gate computed from the id). II keeps the default: the ST's gate (<c>PHANT.PRG</c>
    /// <c>0xc366</c>, s173 <c>[C]</c> static) computes I's thresholds, requirements and attribute sums for ids 1–100
    /// (<c>atari-st/phantasie2/FORMAT.md</c> section 5). III overrides it with its table rule (<c>P3TownEntryPayout.CanUse</c>).
    /// </summary>
    bool CanUseItem(GameSession session, Character member, int itemId) => ItemRules.IsUsable(itemId, member.Stats);

    /// <summary>
    /// An item's point value (shield / armour protection, weapon / bow damage): what the worn fields hold, the Compare panel,
    /// the hand-out line and the best-gear pick weigh. Default: I's id formulas (<see cref="ItemRules.CombatGate"/>; II's ST
    /// <c>0xc366</c> writes the same). III: its table (<c>items.json</c> <c>points</c>, DOS <c>DS:0x113c</c>).
    /// </summary>
    int ItemPoints(GameSession session, int itemId) => ItemRules.CombatGate(itemId).Points;

    /// <summary>
    /// The best-gear pick after an item reaches a member (the hand-out, III's Trade): default I's selector (DOS OV1
    /// <c>0x2fdb</c>, <see cref="CombatGear.SelectWornItems"/>: most points, first of equals, a kind with none usable worn
    /// empty). III: ovl2 <c>0x37f1</c> (<c>P3TownEntryPayout.ReadyBestEquipment</c>, bow slot, ties to the later item, a
    /// slot never emptied).
    /// </summary>
    void ReadyBestEquipment(GameSession session, Character member) => CombatGear.SelectWornItems(member);

    /// <summary>
    /// The member's gear numbers as this game's combat uses them (the inventory screen's gear line and Compare panel).
    /// Default: I's (<see cref="InventoryActions.Stats"/>: damage A + B, DOS defense rating). III: the table points of the
    /// worn weapon, armour, shield and bow, and its defence (root <c>0xd730</c>, <c>P3Combat.Defence</c>).
    /// </summary>
    GearStats GearStatsOf(GameSession session, Character member) => InventoryActions.Stats(member);

    /// <summary>
    /// The Armory just sold <paramref name="itemId"/> to <paramref name="shopper"/>. Default: I's DOS auto-wear (the bought
    /// item is worn when usable and its points ≥ the worn one's, <see cref="TownMenus.AutoEquipIfBetter"/>). III wears
    /// nothing (ovl2 <c>0x2f2a</c>: its exit ready-best runs on the record before the shopping copy is written back over it).
    /// </summary>
    void AfterArmoryBuy(GameSession session, Character shopper, int itemId) => TownMenus.AutoEquipIfBetter(shopper, itemId);

    /// <summary>
    /// The hand-out screen's worn-item columns, left to right (slot ids), each showing the worn item's points
    /// (<see cref="ItemPoints"/>). Default null: I's row (DOS OV1 <c>0x2bed</c>, WPN ARM SHL = damage B, armour soak,
    /// shield soak). III: ovl2 <c>0x0de6</c>, "SH AR BW WP" = shield, armour, bow, weapon.
    /// </summary>
    IReadOnlyList<string>? HandOutWornColumns(GameSession session) => null;

    // --- Optional hooks a game with its own world rules uses (T4, III; I and II use none) ---------------------------

    /// <summary>
    /// The save's rules were set (<see cref="GameSession.Options"/>): a game whose limits are rules options of its own
    /// applies them here (III: the found-item limit D3-020, the roster D3-029).
    /// </summary>
    void ApplyRules(GameSession session, GameOptions options)
    {
    }

    /// <summary>
    /// A wilderness step in a game whose world is not I's region mosaic (III's planes, places and tile terrain). The
    /// campaign moves the party, notes what happened and starts what follows (prompts, fights, swims); the loop returns its
    /// result as is. Null = the shared <see cref="OverworldTraversal"/>.
    /// </summary>
    OverworldMoveResult? MoveOverworld(GameSession session, CardinalDirection direction) => null;

    /// <summary>
    /// Answer a <see cref="GameSession.WildernessPrompt"/> the campaign asked itself (III's sleeping / heard monsters).
    /// True = handled (the campaign cleared or replaced the prompt); false = the shared day-encounter menus.
    /// </summary>
    bool AnswerWildernessPrompt(GameSession session, WildernessPrompt prompt, WildernessAnswer answer) => false;

    /// <summary>A stay at the wilderness inn the party stands on, by the game's own rules (III's 250-gold inns); null = I's.</summary>
    TownTransactionResult? StayAtWildernessInn(GameSession session) => null;

    /// <summary>
    /// A dungeon step onto (<paramref name="x"/>, <paramref name="y"/>) in a game with its own cell codes (III). The
    /// campaign moves the party and returns the result the loop then handles (fights, exits, events). Null = the shared
    /// <see cref="DungeonTraversal"/> (I's cell classes).
    /// </summary>
    DungeonMoveResult? StepDungeon(GameSession session, DungeonState state, DungeonLevel level, int x, int y) => null;

    /// <summary>
    /// True when town entry churns the Armory by I's restock (OV1 <c>0x8e1</c>; II keeps it). False for a game whose towns
    /// have no I-style restock list (III: its own churn runs in <see cref="OnTownArrival"/>, D3-033).
    /// </summary>
    bool ArmoryRestocks => true;

    /// <summary>
    /// D)ISTRIBUTE's SELL price by item id in <paramref name="town"/>; null = Phantasie I's <c>V²/8</c> (I, II). III: 75 % of
    /// the town's price (<see cref="P3Town.SalePrice"/>).
    /// </summary>
    Func<int, long>? DistributeSalePrice(TownState town, IItemCatalog items) => null;

    /// <summary>
    /// An ordinary arrival in <paramref name="town"/> (<see cref="GameSession.EnterTown"/>, after the aging and I's restock;
    /// not the new-game start): III's Armory restock (D3-033).
    /// </summary>
    void OnTownArrival(GameSession session, TownState town)
    {
    }

    /// <summary>Age-clock units a town entry adds after <paramref name="travelTime"/> (I / II: <c>(time &gt;&gt; 8) / 7</c>).</summary>
    int TownEntryAgeUnits(int travelTime) => TravelClock.AgeUnits(travelTime);

    /// <summary>Town entry ages DEAD members too (I / II: DOS adds to all 7 slots); III ages only the living.</summary>
    bool AgesTheDead => true;

    /// <summary>The DAY OUT number for <see cref="GameSession.TravelTime"/> (I / II: 256 units a day; III: thirds).</summary>
    int DaysOut(int travelTime) => TravelClock.DaysOut(travelTime);

    /// <summary>
    /// D3-071 (manual, user s142): an ordinary town entry shares out the XP pool and opens the found-item hand-out (III:
    /// "the experience will be divided among the adventurers when they reach a town"; Amiga <c>0x7964</c> →
    /// <c>0xbc7c(1)</c>). I / II: only the town's Distribute.
    /// </summary>
    bool PaysOutOnTownEntry => false;

    /// <summary>
    /// D3-071: a paying town entry's first steps, before the found items' hand-out (only when the XP pool or the found
    /// list is not empty). III: every character's best equipment readied and the wand dropped from the found list (DOS
    /// ovl2 <c>0x37f1</c> / <c>0x5a9</c>, <see cref="P3TownEntryPayout"/>). I / II: nothing.
    /// </summary>
    void BeforeTownEntryPayout(GameSession session)
    {
    }

    /// <summary>
    /// D3-071: the town entry's "Shares for NAME :" split (<see cref="GameSession.ShareOutExperience"/>), one answer per
    /// character. Only campaigns that <see cref="PaysOutOnTownEntry"/> get here.
    /// </summary>
    void ShareOutTownEntryExperience(GameSession session, IReadOnlyList<Character> characters, IReadOnlyList<int> shares) =>
        throw new NotSupportedException("This game pays out only through the town's Distribute.");

    /// <summary>The party just entered town <paramref name="townId"/> (III: the party stands on that town's square of its plane).</summary>
    void OnTownEntered(GameSession session, int townId)
    {
    }

    /// <summary>The party just entered a dungeon (III: its own encounter chance, and the entrance is an exit cell).</summary>
    void OnDungeonEntered(GameSession session)
    {
    }

    /// <summary>
    /// `core.prompt.an_exit_take_it` was answered YES: true when the campaign put the party where the exit leads (III's
    /// planes); false = I's exit record (<see cref="DungeonLevel.ExitTarget"/>).
    /// </summary>
    bool TakeDungeonExit(GameSession session, DungeonState state, DungeonLevel level) => false;

    /// <summary>The fight of first monster <paramref name="firstMonsterId"/> by the game's own builder; null = I's <see cref="EncounterBuilder"/>.</summary>
    CombatEncounter? BuildEncounter(GameSession session, int firstMonsterId, SurpriseFlags surprise) => null;

    /// <summary>The game's own combat round (III: <see cref="P3RoundResolver"/>); null = Phantasie I's <see cref="Core.CombatResolver"/>.</summary>
    ICombatResolver? CombatResolver => null;

    /// <summary>
    /// A new fight is built and prepared (<see cref="CombatEncounter.Prepare"/>), before the first round (III: every
    /// member's first order from the saved option, root <c>0xd55b</c>).
    /// </summary>
    void OnCombatStarted(GameSession session, CombatEncounter encounter) { }

    /// <summary>A fight is saved: the game adds its own fight state to <paramref name="snapshot"/> (III: <see cref="P3Fight"/>).</summary>
    void OnCombatCaptured(GameSession session, CombatEncounter encounter, CombatSnapshot snapshot) { }

    /// <summary>A fight restored from a save (III: its <see cref="P3Fight"/> from <see cref="CombatSnapshot.Phantasie3"/>).</summary>
    void OnCombatRestored(GameSession session, CombatEncounter encounter, CombatSnapshot snapshot) { }

    /// <summary>The top-of-round monster check by the game's own rules (III: none, its give-up test ends the round); null = I's.</summary>
    MoraleOutcome? MoraleCheck(GameSession session, CombatEncounter encounter, List<string> log) => null;

    /// <summary>
    /// The item treasure after a fight the party survived, by the game's own rules (III: one Find Item roll per item of the
    /// lead monster, root <c>0xee93</c>). Returns the offers in order, each with its finder when the game names one (III);
    /// null = I's <see cref="TreasureDrop"/>.
    /// </summary>
    IReadOnlyList<TreasureOffer>? RollTreasure(GameSession session, CombatEncounter encounter, List<string> log) => null;

    /// <summary>A PARTY OPTIONS choice by the game's own rules (III's Greet / Surrender / Mercy / Flee); null = I's.</summary>
    PartyOptionResult? ChoosePartyOption(GameSession session, CombatEncounter encounter, PartyOption option) => null;

    /// <summary>Guild training by the game's own rules (III's <see cref="P3Training"/>); null = the shared training.</summary>
    TownTransactionResult? Train(GameSession session, Character member) => null;

    /// <summary>Why <paramref name="member"/> may not train (null reason = may), by the game's rules; null = shared.</summary>
    (bool May, string Reason)? CanTrain(GameSession session, Character member) => null;

    /// <summary>XP <paramref name="member"/> still needs to train, by the game's rules; null = shared.</summary>
    long? TrainingXpShortfall(GameSession session, Character member) => null;

    /// <summary>What training costs <paramref name="member"/> in the current town, by the game's rules; null = shared.</summary>
    long? TrainingGoldCost(GameSession session, Character member) => null;

    /// <summary>After a town inn stay (the shared stay; a game with its own <see cref="RestAtInn"/> does its own).</summary>
    void AfterInnStay(GameSession session)
    {
    }

    /// <summary>A town inn stay by the game's own rules (III: ovl2 <c>0x347d</c>, s136); null = the shared stay.</summary>
    TownTransactionResult? RestAtInn(GameSession session) => null;
}

/// <summary>
/// The step when a party member's HP reaches 0 (OV2 <c>0x124a</c> → <c>0x12cf</c> in I). The engine has already clamped HP
/// to 0; the rule sets the status and anything else the game does on death. It must not draw random numbers.
/// </summary>
public interface IMemberDeathRule
{
    void ApplyDeath(Character member);
}

/// <summary>The campaigns the remaster has built, by game.</summary>
public static class Campaigns
{
    /// <summary>True when <paramref name="game"/>'s campaign is built (the main menu greys out the others).</summary>
    public static bool IsBuilt(GameId game) => game is GameId.P1 or GameId.P2 or GameId.P3;

    /// <summary>
    /// True when <paramref name="game"/> can be started from the main menu: the built campaigns, and IV, whose game is
    /// <see cref="Phantasie4Campaign"/> rather than an <see cref="ICampaign"/> (new game in Pendragon, creation at the Guild).
    /// </summary>
    public static bool IsPlayable(GameId game) => IsBuilt(game) || game == GameId.P4;

    /// <summary>The campaign of <paramref name="game"/>; throws for a game that is not built yet.</summary>
    public static ICampaign For(GameId game) => game switch
    {
        GameId.P1 => Phantasie1Campaign.Instance,
        GameId.P2 => Phantasie2Campaign.Instance,
        GameId.P3 => Phantasie3Campaign.Unloaded, // a session gets its data-bound campaign from OpenPhantasie.Data
        _ => throw new NotSupportedException($"The {game.Prefix()} campaign is not built yet."),
    };
}

/// <summary>
/// An event menu on a dungeon cell waiting for an answer (<see cref="GameSession.DungeonEventPrompt"/>): the cell's event
/// key (I: the feature byte ≥ 0xdd), the level message shown and its options. The loop saves the key and message id and
/// rebuilds the message from the level on load.
/// </summary>
public sealed record PendingDungeonEvent(byte Feature, int MessageId, DungeonMessage Message);

/// <summary>
/// III's dungeon event-text page (Amiga <c>0x1d982</c>, DOS ovl4 <c>0x1c4d</c>; <c>dungeon-events.md</c> §2.1): the texts
/// one step's event program has shown so far, one row each, piled up in order (a menu's choices included). While
/// <see cref="WaitsForKey"/> the program is stopped at a text with no menu ("Press a key") until
/// <see cref="GameSession.AcknowledgeDungeonPage"/>; a menu's text shows with "Press a number" and is answered through
/// <see cref="GameSession.AnswerDungeonEvent"/>. The page starts empty at each step and when an event fight starts.
/// </summary>
public sealed record DungeonTextPage(IReadOnlyList<string> Lines, bool WaitsForKey);

/// <summary>
/// What a campaign hook may use beyond the session's public API (Phase 09 T1.4): the session's one RNG stream, the race
/// and class tables, the live dungeon level, the treasure queue, leaving the dungeon, and the prompt and endgame state
/// that only the campaign sets. Reached through <see cref="GameSession.CampaignAccess"/>, so a campaign outside Core (or
/// a test's) works the same as Phantasie I's. Front ends do not need it.
/// </summary>
public sealed class CampaignAccess
{
    private readonly GameSession _session;

    internal CampaignAccess(GameSession session) => _session = session;

    /// <summary>The session's DOS random source (one stream: campaign hooks draw from it in DOS order).</summary>
    public IDosRandomSource Rng => _session.Rng;

    /// <summary>The game's item catalog (III: the items' point values for combat).</summary>
    public IItemCatalog Items => _session.Items;

    /// <summary>The race / class tables (I: race maxima for the magical pool).</summary>
    public CharacterCreationTables CreationTables => _session.CreationTables;

    /// <summary>The dungeon the party is in; throws outside a dungeon.</summary>
    public DungeonState RequireDungeon() => _session.RequireDungeon();

    /// <summary>The live level the party is in (throws outside a dungeon).</summary>
    public DungeonLevel CurrentDungeonLevel => _session.CurrentDungeonLevel;

    /// <summary>Queue a "YOU FIND %s. KEEP IT?" offer (<see cref="GameSession.PendingTreasure"/>).</summary>
    public void AddPendingTreasure(int itemId) => _session.AddPendingTreasure(itemId);

    /// <summary>Leave the dungeon with no exit prompts: the level, its pending prompts and any stopped event are gone.</summary>
    public void ClearDungeonState() => _session.ClearDungeonState();

    /// <summary>Put the party on the overworld at its current position, out of any town.</summary>
    public void ReturnToWilderness() => _session.ReturnToWilderness();

    /// <summary>
    /// An event ran a trap on cell (<paramref name="x"/>, <paramref name="y"/>): a spotted trap becomes the D)ISARM OR
    /// L)EAVE prompt; a sprung one logs its lines (its message is already noted).
    /// </summary>
    public void OfferEventTrap(int trapKind, int x, int y, DungeonMoveResult trap) =>
        _session.OfferEventTrap(trapKind, x, y, trap);

    /// <summary>The event menu waiting for <see cref="GameSession.AnswerDungeonEvent"/>, or null.</summary>
    public PendingDungeonEvent? DungeonEventPrompt
    {
        get => _session.DungeonEventPrompt;
        set => _session.DungeonEventPrompt = value;
    }

    /// <summary>III's event-text page (<see cref="GameSession.DungeonPage"/>), or null.</summary>
    public DungeonTextPage? DungeonPage
    {
        get => _session.DungeonPage;
        set => _session.DungeonPage = value;
    }

    /// <summary>An event stopped at a treasure offer (saved with the session), or null.</summary>
    public DungeonScriptResume? PendingDungeonScript
    {
        get => _session.PendingDungeonScript;
        set => _session.PendingDungeonScript = value;
    }

    /// <summary>Set when no member is left standing (state 6); the loop then waits for <see cref="GameSession.ResolvePartyDefeat"/>.</summary>
    public bool PartyDefeatPending
    {
        get => _session.PartyDefeatPending;
        set => _session.PartyDefeatPending = value;
    }

    /// <summary>A reward scene for the front end to show (<see cref="GameSession.PendingRewardScene"/>).</summary>
    public RewardScene? PendingRewardScene
    {
        get => _session.PendingRewardScene;
        set => _session.PendingRewardScene = value;
    }

    /// <summary>I's victory screen for the front end to show (<see cref="GameSession.PendingVictoryScene"/>).</summary>
    public VictoryScene? PendingVictoryScene
    {
        get => _session.PendingVictoryScene;
        set => _session.PendingVictoryScene = value;
    }

    /// <summary>The endgame moment the front end shows next.</summary>
    public EndgameOutcome? LastEndgame
    {
        get => _session.LastEndgame;
        set => _session.LastEndgame = value;
    }

    /// <summary>"A TOWN. ENTER?" / `core.overworld.a_dungeon_enter` (or a campaign's own place question) waiting for an answer.</summary>
    public OverworldEntryPrompt? EntryPrompt
    {
        get => _session.EntryPrompt;
        set => _session.EntryPrompt = value;
    }

    /// <summary>A wilderness encounter menu waiting for <see cref="GameSession.AnswerWildernessPrompt"/>.</summary>
    public WildernessPrompt? WildernessPrompt
    {
        get => _session.WildernessPrompt;
        set => _session.WildernessPrompt = value;
    }

    /// <summary>`core.prompt.an_exit_take_it` waiting (a campaign's own dungeon step found an exit cell).</summary>
    public bool DungeonExitPending
    {
        get => _session.DungeonExitPending;
        set => _session.DungeonExitPending = value;
    }

    /// <summary>"%s HEARS MONSTERS" behind a door, waiting for <see cref="GameSession.AnswerDungeonLock"/>.</summary>
    public GameSession.PendingDungeonLock? DungeonLockPrompt
    {
        get => _session.DungeonLockPrompt;
        set => _session.DungeonLockPrompt = value;
    }

    /// <summary>A fresh record copy of 0-based monster <paramref name="id"/> (HP not rolled).</summary>
    public Monster MonsterRecord(int id) => _session.MonsterRecordOf(id);

    /// <summary>The shared swim step (every member rolls; nobody left → party defeat).</summary>
    public void Swim() => _session.Swim();

    /// <summary>The Guild image written on the victory screen, or null.</summary>
    public IReadOnlyList<Character?>? VictoryGuildWrite
    {
        get => _session.VictoryGuildWrite;
        set => _session.VictoryGuildWrite = value;
    }
}

/// <summary>
/// One item offered after a fight (<see cref="ICampaign.RollTreasure"/>): its id and the member who found it, or null when
/// the game's question names nobody (I's "YOU FIND %s. KEEP IT?").
/// </summary>
public sealed record TreasureOffer(int ItemId, string? Finder);
