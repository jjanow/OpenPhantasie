namespace OpenPhantasie.Core;

/// <summary>
/// The Phantasie I campaign (Phase 09 T1.4): magical pools, dungeon event scripts (runes, MESS messages, script fights and
/// traps), the DNG9 Black Knights, the Black Lord win (Zeus, DIVINE I), party defeat (the Astral Plane and the Great
/// Demon), I's death step (D-009), the Mystic, and I's world facts the shared loop asks for (start town, the
/// TRANSPORTATION towns and OLYMPIA, the River Styx, SCROLL 17's level, the Black Lord's no-drop, the debug monster pool). The code moved here from
/// <c>GameSession</c> unchanged: same order of RNG draws, same text. Stateless; one shared <see cref="Instance"/>. It
/// works only through the public session API and <see cref="GameSession.CampaignAccess"/>, as a campaign outside Core
/// would.
/// </summary>
public sealed class Phantasie1Campaign : ICampaign
{
    /// <summary>D-037: the fight pool's XP / gold are split on town entry (manual p.15, DOS / ST / Amiga).</summary>
    public bool SplitsPoolOnTownEntry => true;

    public static Phantasie1Campaign Instance { get; } = new();

    private Phantasie1Campaign()
    {
    }

    /// <summary><c>[0x64ec]</c> of the Fortress of the Black Knights (DNG9).</summary>
    public const int BlackKnightFortressLevel = 9;

    /// <summary>
    /// A new game, victory and the fully defeated party go to town 1, Pelnor (OV1 <c>0x7dd</c> branch <c>0x8ae</c>,
    /// <c>0x4d5</c>, <c>0x1e15(0)</c>).
    /// </summary>
    public const int HomeTownId = 1;

    /// <summary>Entries in the DOS TRANSPORTATION picker (COM <c>0x16a2</c>): TWNS records 0..11, Core town ids 1..12.</summary>
    public const int TransportTownCount = 12;

    /// <summary>
    /// OLYMPIA, TWNS record 11 (Core id 12): printed blank in the picker (<c>0x1887</c>: index 11 → DS <c>0xd07</c> = " ").
    /// </summary>
    public const int OlympiaTownId = 12;

    /// <summary>OUT18 (10,5)'s town tile carries arg 99, which OV2 <c>0x611a</c> turns into TWNS index 11 = OLYMPIA (s49 [C]).</summary>
    public const int OlympiaFeatureArg = 99;

    /// <summary>SCROLL 17, the win scroll (<c>endgame.md</c>): only dropped inside the Fortress of the Black Knights.</summary>
    public const int WinScrollId = 137;

    /// <summary>BLACK LORD, 0-based monster id 72 (DOS <c>[0x64e6]</c> = 0x49): the win fight; in a dungeon he drops nothing.</summary>
    public const int BlackLordId = 72;

    /// <summary>Overworld regions past this one are the Underworld: a blocked step there is the River Styx.</summary>
    public const int LastSurfaceRegion = 16;

    public GameId Game => GameId.P1;

    public FeatureModuleSet DefaultFeatureModules => FeatureModuleSet.PhantasieI;

    public IMemberDeathRule Death => PhantasieIDeathRule.Instance;

    // --- World facts the shared loop asks for --------------------------------------------

    /// <summary>New game [C] (OV1 <c>0x7dd(1)</c>, branch <c>0x8ae</c>): the town index is 0 = Pelnor (Core town 1).</summary>
    public int StartTownId => HomeTownId;

    private static readonly IReadOnlyList<int> AllTransportTowns = Enumerable.Range(1, TransportTownCount).ToArray();

    /// <summary>The DOS picker lists all 12 TWNS records (s49 [C]); OLYMPIA (12) is the only route to Olympus.</summary>
    public IReadOnlyList<int> TransportTowns => AllTransportTowns;

    public bool IsUnnamedTransportTown(int townId) => townId == OlympiaTownId;

    private static readonly IReadOnlyDictionary<int, int> TownAliases =
        new Dictionary<int, int> { [OlympiaFeatureArg] = OlympiaTownId }.AsReadOnly();

    /// <summary>
    /// [C] (s49) OV2 <c>0x611a</c>: <c>[0x64ee]</c> := arg − 1 (0-based TWNS index), and arg 99 (OUT18 (10,5)) → index 11 =
    /// OLYMPIA. Core town ids are 1-based (arg as-is), so 99 → 12, not 11 (STARVILLE).
    /// </summary>
    public IReadOnlyDictionary<int, int> OverworldTownAliases => TownAliases;

    /// <summary>[C] OV2 <c>0x618e</c> ([D✓] s65): a blocked step prints "THE RIVER STYX" past region 16, else the ocean.</summary>
    public string BlockedMessage(int regionId) => regionId > LastSurfaceRegion ? GameText.S("core.overworld.the_river_styx") : GameText.S("core.overworld.that_is_the_ocean");

    private static readonly IReadOnlyList<int> StubMonsters = [0, 1, 2, 3, 4, 5, 6];

    /// <summary>The first seven monsters: only a session built without a pool (no game data) uses them.</summary>
    public IReadOnlyList<int> FallbackMonsterPool => StubMonsters;

    /// <summary>
    /// SCROLL 17 only inside the Fortress of the Black Knights. DOS tests the <i>unshifted</i> id there, so FIRE ELEMENTAL
    /// (treasure 135) gave SCROLL 17 anywhere 20 % of the time; the remaster tests the shifted item (user, s34, D-031).
    /// </summary>
    public bool MayDropItem(int itemId, int? dungeonLevelId) =>
        itemId != WinScrollId || dungeonLevelId == BlackKnightFortressLevel;

    /// <summary>OV2 <c>0x40e4</c>: a dungeon fight against the BLACK LORD skips the presence roll and drops no item.</summary>
    public bool WithholdsDungeonTreasure(int firstMonsterId) => firstMonsterId == BlackLordId;

    // --- Town: the Mystic ----------------------------------------------------------------

    /// <summary>
    /// The Mystic (<see cref="TownMenus.ConsultMystic(Party, Func{IReadOnlyList{Character}, bool})"/>); the tier-4 text
    /// "face the tests" needs the first four runes (<see cref="HasTheTestRunes"/>).
    /// </summary>
    public TownTransactionResult ConsultMystic(GameSession session) =>
        TownMenus.ConsultMystic(session.Party, HasTheTestRunes);

    /// <summary>
    /// The first four rune words (@72..@78: AIR, EARTH, FIRE, WATER), OR'd across the counted members, are all set
    /// (OV1 <c>0x3327</c>'s third argument, [C]+[D✓] s49).
    /// </summary>
    public static bool HasTheTestRunes(IReadOnlyList<Character> counted) =>
        Enumerable.Range(0, 4).All(r => counted.Any(m => m.Phantasie1.Runes[r] != 0));

    // --- Overworld: magical pools ------------------------------------------------

    /// <summary>"A MAGICAL POOL" (OV2 <c>0x656b</c>) [C]; gain capped at the manual's race maximum (D-051).</summary>
    public void OnSpecialSite(GameSession session, OverworldMoveResult result)
    {
        var access = session.CampaignAccess;
        var lines = MagicalPool.Apply(session.Party, result.Tile % 10, access.CreationTables, access.Rng,
            noCap: session.Options.IsDos(RuleOptionRegistry.PoolCap));
        foreach (var line in lines)
            session.Note(line);
        // s154: the banner and the "IS CHANGED" rows are one report (I Amiga h71 +0xa50…+0xc88: "A MAGICAL POOL" through
        // +0x112e, the rows, requester 21 waiting for OK, h86 +0(0x15)).
        session.NoteMapWindow(LogWindowKind.Report, lines.Count);
    }

    // --- Dungeon event scripts (OV2 0x24b2) -------------------------------------

    /// <summary>Stepping onto a scripted-event cell (feature ≥ 0xdd): show its message; a menu waits for an answer.</summary>
    public void BeginDungeonEvent(GameSession session, DungeonMoveResult move)
    {
        var level = session.CampaignAccess.CurrentDungeonLevel;
        var feature = move.FeatureByte;
        if (DungeonScript.IsSpent(level, feature))
            return;
        var messageId = level.GetBufferByte(DungeonScript.EventRecordOffset(feature)) & 0x7f;
        if (level.Messages.TryGetValue(messageId, out var msg) && msg.IsMenu)
        {
            session.CampaignAccess.DungeonEventPrompt = new PendingDungeonEvent(feature, messageId, msg);
            foreach (var line in msg.Lines)
                session.Note(line);
            return;
        }
        RunDungeonEvent(session, feature, choice: 1);
    }

    /// <summary>Answer the event menu with a 1-based <paramref name="choice"/> and run the chosen script.</summary>
    public void AnswerDungeonEvent(GameSession session, int choice)
    {
        var prompt = session.DungeonEventPrompt
                     ?? throw new InvalidOperationException("No dungeon event is waiting for an answer.");
        if (choice < 1 || choice > prompt.Message.Options.Count)
            throw new ArgumentOutOfRangeException(nameof(choice));
        session.CampaignAccess.DungeonEventPrompt = null;
        RunDungeonEvent(session, prompt.Feature, choice, alreadyShown: true);
    }

    private static void RunDungeonEvent(GameSession session, byte feature, int choice, bool alreadyShown = false)
    {
        var access = session.CampaignAccess;
        var dungeon = access.RequireDungeon();
        var level = access.CurrentDungeonLevel;
        var host = new ScriptHost(session, level, choice, alreadyShown);
        AfterDungeonScript(session, DungeonScript.Run(dungeon, level, feature, session.Party, host, access.Rng));
    }

    /// <summary>The KEEP IT? of a stopped script was answered: run its remaining ops (no event message is shown again).</summary>
    public void ResumeDungeonScript(GameSession session)
    {
        var access = session.CampaignAccess;
        var resume = access.PendingDungeonScript!;
        access.PendingDungeonScript = null;
        var dungeon = access.RequireDungeon();
        var level = access.CurrentDungeonLevel;
        var host = new ScriptHost(session, level, resume.Choice, firstShown: true) { Resumed = true };
        AfterDungeonScript(session, DungeonScript.Resume(dungeon, level, session.Party, host, access.Rng, resume));
    }

    private static void AfterDungeonScript(GameSession session, DungeonScriptResult result)
    {
        // s141: the moves go in at their places among the lines (I Amiga: op 12's box before the move).
        for (var i = 0; i <= result.Log.Count; i++)
        {
            foreach (var move in result.Moves.Where(m => m.AfterLines == i))
                session.NoteDungeonMove(move.FromX, move.FromY);
            if (i < result.Log.Count)
                session.Note(result.Log[i]);
        }
        session.NoteMapWindows(result.Log, result.Windows); // s144: the damage reports

        var access = session.CampaignAccess;
        switch (result.End)
        {
            case DungeonScriptEnd.OfferItem:
                access.AddPendingTreasure(result.ItemId!.Value); // 0x421d: "YOU FIND %s. KEEP IT?" now
                access.PendingDungeonScript = result.Resume;
                break;
            case DungeonScriptEnd.Fight:
                session.BeginDungeonCombat(result.MonsterId, result.Surprise);
                break;
            case DungeonScriptEnd.PartyDefeated:
                access.PartyDefeatPending = true;
                break;
            case DungeonScriptEnd.Trap:
                if (result.TrapResult?.Message is { } m)
                    session.Note(m);
                if (result.TrapResult is { } trap) // op 16 spotted → D)ISARM OR L)EAVE on the event cell
                    access.OfferEventTrap(result.TrapKind, result.EventX, result.EventY, trap);
                break;
        }
    }

    /// <summary>The script's view of the session: MESS messages and found gold (op 6 stops the script instead).</summary>
    private sealed class ScriptHost(GameSession session, DungeonLevel level, int firstChoice, bool firstShown)
        : IDungeonScriptHost
    {
        private bool _first = true;

        /// <summary>A resumed script (after op 6): its event message was already asked, so every message is a later one.</summary>
        public bool Resumed { get; init; }

        public int ShowMessage(int messageId)
        {
            if (_first && !Resumed)
            {
                _first = false;
                if (!firstShown)
                    NoteMessage(messageId);
                return firstChoice;
            }
            NoteMessage(messageId); // later messages: shown; a menu's answer is not used by ops 11/19
            return 1;
        }

        public long FoundGold
        {
            get => session.Sack.Gold;
            set
            {
                session.Sack.Gold = value;
                session.BankFieldGoldIfSafe(); // s175 treasury "safe": event gold is banked at once
            }
        }

        private void NoteMessage(int messageId)
        {
            if (level.Messages.TryGetValue(messageId, out var msg))
            {
                foreach (var line in msg.Lines)
                    session.Note(line);
                if (!msg.IsMenu)
                    session.NoteMapWindow(LogWindowKind.Text, msg.Lines.Count); // s144: 0x2a95's text
            }
        }
    }

    // --- Fights ------------------------------------------------------------------

    /// <summary>Inside DNG9 every BLACK KNIGHT group uses size factor 4 (OV2 <c>0x387e</c>).</summary>
    public bool IsBlackKnightSpot(int dungeonLevelId) => dungeonLevelId == BlackKnightFortressLevel;

    /// <summary>The Black Lord killed → state 7, Zeus' victory (endgame.md §1, §3).</summary>
    public void AfterCombatSurvived(GameSession session, CombatEncounter encounter, CombatResult result)
    {
        if (Endgame.IsBlackLordKilled(encounter, result))
            ResolveVictory(session);
    }

    /// <summary>
    /// OV1 <c>0x4d5</c>: GUILD.DAT written, DIVINE I granted, Zeus' speech waiting for a key (<see cref="VictoryScene"/>,
    /// shown by the front end in town), the party goes to town 1.
    /// </summary>
    private static void ResolveVictory(GameSession session)
    {
        var access = session.CampaignAccess;
        session.Note(GameText.S("core.endgame.black_lord_is_dead"));
        // The slots before GrantVictory: who stands OK is who gets DIVINE I (DOS 0x4d5, Amiga h32 +0x83e: status 1).
        var slots = session.Party.Adventurers.Select(m => new VictorySlot(m.Race, m.Status == CharacterStatus.Ok)).ToList();
        access.VictoryGuildWrite = Endgame.GrantVictory(session.Party, session.Roster);
        access.PendingVictoryScene = new VictoryScene(slots, Endgame.ZeusSpeech);
        access.LastEndgame = EndgameOutcome.Victory;
        access.ClearDungeonState();
        session.Overworld.RegionId = 1; // [0x64ea] := 1
        session.EnterTown(HomeTownId);  // current town [0x63e2] := 1, town loop (OV1 0xb4 → 0x1e15(0))
    }

    // --- Party defeat: the Astral Plane ----------------------------------------------

    /// <summary>
    /// Party defeat (state 6 → OV1 <c>0x3bc</c>, D-025: DOS rule): the Great Demon judges every member
    /// (<see cref="PartyDefeat"/>). Survivors wake on the overworld at OUT1 (20,5); with nobody left the game returns to
    /// town 1 with an empty party. There is no game over.
    /// </summary>
    public PartyDefeatResult ResolvePartyDefeat(GameSession session)
    {
        var result = PartyDefeat.Resolve(session.Party, session.Roster, session.Sack, session.CampaignAccess.Rng);
        foreach (var line in result.Log)
            session.Note(line);
        session.Overworld.RegionId = PartyDefeat.WakeRegion;
        session.Overworld.X = PartyDefeat.WakeX;
        session.Overworld.Y = PartyDefeat.WakeY;
        if (result.AnyRevived)
        {
            session.CampaignAccess.ReturnToWilderness();
        }
        else
        {
            // [C] (s41) nobody left: state 1 → 0x1e15(0) → the full town entry 0x1954 (aging no-op on an empty party,
            // Armory restock of town 1).
            session.EnterTown(HomeTownId);
        }
        return result;
    }
}

/// <summary>
/// Phantasie I's death step (death routine OV2 <c>0x12cf</c>): status DEAD and the CON loss of DIFFERENCES.md D-009 (DOS
/// rule, user s14).
/// </summary>
public sealed class PhantasieIDeathRule : IMemberDeathRule
{
    public static PhantasieIDeathRule Instance { get; } = new();

    private PhantasieIDeathRule()
    {
    }

    public void ApplyDeath(Character member)
    {
        member.Status = CharacterStatus.Dead;
        member.Stats = member.Stats with { Con = member.Stats.Con - ConLoss(member.Stats) };
    }

    /// <summary>
    /// CON lost on dying [C] (OV2 <c>0x12cf</c>, D-009): <c>(CON &gt; 0 ? 1 : 0) + (CON &gt; 1 &amp;&amp; @10 &lt; 11 ? 1 : 0)</c>,
    /// @10 = Luck. No floor below 0; a CON-0 corpse can't be resurrected.
    /// </summary>
    public static int ConLoss(CharacterStats stats) =>
        (stats.Con > 0 ? 1 : 0) + (stats.Con > 1 && stats.Luck < 11 ? 1 : 0);
}
