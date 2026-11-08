namespace OpenPhantasie.Core;

/// <summary>
/// The Phantasie II campaign (Phase 09 T3; Atari ST v1.0 rules, specs under
/// <c>reverse-engineering/atari-st/phantasie2/logic-specs/</c>): Pluto's minions and their amulets, the beast runes, the
/// Mystic's quest SCORE, II's dungeon event interpreter (<see cref="Phantasie2DungeonScript"/>), the Filmon ending and the
/// wand of Filmon, DIVINE II's summons, the party defeat (survivors wake at the bloodstained temple, OUT18 (18,14)), ORIGIN
/// at creation, and II's world facts. The shared rules (encounters, terrain, training, towns) come from
/// <c>GameData/phantasie2</c> (<see cref="RulesProfile"/>). Stateless; one shared <see cref="Instance"/>.
/// </summary>
public sealed class Phantasie2Campaign : ICampaign
{
    public static Phantasie2Campaign Instance { get; } = new();

    private Phantasie2Campaign()
    {
    }

    /// <summary>A new game, the ending and a defeat with no survivor go to town 1, PIPPACOTT (<c>towns.md</c> §3).</summary>
    public const int HomeTownId = 1;

    /// <summary>The reachable towns: 1–6 on Ferronrah, 7–10 in the Netherworld (STARVILLE / OLYMPIA are I's leftovers).</summary>
    public const int ReachableTownCount = 10;

    /// <summary>The first Netherworld town (BLACK MOOR); creation there gives ORIGIN NETHERWORLD (<c>endgame.md</c> §9).</summary>
    public const int FirstNetherworldTown = 7;

    /// <summary>Overworld regions past this one get the second wall text (RSC 15 vs 17, <c>terrain.md</c>; I's pair assumed [~]).</summary>
    public const int LastSurfaceRegion = 16;

    /// <summary>Amulet k (1..8) is item 140 + k (AMULET 1 = 141).</summary>
    public const int AmuletBase = 140;

    /// <summary>The wand of Filmon (FLMN WAND), the ending's reward.</summary>
    public const int FilmonWandId = 149;

    /// <summary>The orb (---ORB---): +20 at the Mystic while an OK member carries it.</summary>
    public const int OrbId = 150;

    /// <summary>PLUTO, 0-based 79: killing him shows his speech; it is not the win.</summary>
    public const int PlutoId = 79;

    /// <summary>A first group whose reaction modifier is below this is Pluto's minion <c>k = −10 − modifier</c>.</summary>
    public const int MinionReactionBase = -10;

    /// <summary>The wilderness region where DIVINE II summons a fight (the Astral Plane [~], <c>magic-divine.md</c> §2).</summary>
    public const int DivineIiRegion = 26;

    /// <summary>Core spell index of DIVINE II (ST slot 56, #57; II's <c>spells.json</c> index 55).</summary>
    public const int DivineIiSpell = SpellId.DivineIi;

    /// <summary>Quest SCORE values (<c>endgame.md</c> §1): the dragon's gate, the orb destroyed, the wand used.</summary>
    public const int DragonGateScore = 100, OrbDestroyedScore = 200, WandUsedScore = 250;

    /// <summary>Where survivors of a party defeat wake: OUT18 (18,14), the bloodstained temple (<c>endgame.md</c> §8).</summary>
    public const int WakeRegion = 18, WakeX = 18, WakeY = 14;

    // String ids (literal, so the string-table tests see each one).
    private static readonly string[] RankIds =
    [
        "core.p2.mystic.rank_0", "core.p2.mystic.rank_1", "core.p2.mystic.rank_2", "core.p2.mystic.rank_3",
        "core.p2.mystic.rank_4", "core.p2.mystic.rank_5",
    ];

    private static readonly string[] PlutoVoiceIds =
        ["core.p2.pluto_voice_1", "core.p2.pluto_voice_2", "core.p2.pluto_voice_3", "core.p2.pluto_voice_4", "core.p2.pluto_voice_5"];

    /// <summary>The intro page's ten lines (<c>0x231fc</c>) and the closing page's 21 (<c>0x23244</c>), as the ST prints them.</summary>
    private static readonly string[] FilmonIntroIds =
    [
        "core.p2.filmon_intro_1", "core.p2.filmon_intro_2", "core.p2.filmon_intro_3", "core.p2.filmon_intro_4",
        "core.p2.filmon_intro_5", "core.p2.filmon_intro_6", "core.p2.filmon_intro_7", "core.p2.filmon_intro_8",
        "core.p2.filmon_intro_9", "core.p2.filmon_intro_10",
    ];

    private static readonly string[] FilmonClosingIds =
    [
        "core.p2.filmon_closing_1", "core.p2.filmon_closing_2", "core.p2.filmon_closing_3", "core.p2.filmon_closing_4",
        "core.p2.filmon_closing_5", "core.p2.filmon_closing_6", "core.p2.filmon_closing_7", "core.p2.filmon_closing_8",
        "core.p2.filmon_closing_9", "core.p2.filmon_closing_10", "core.p2.filmon_closing_11",
        "core.p2.filmon_closing_12", "core.p2.filmon_closing_13", "core.p2.filmon_closing_14",
        "core.p2.filmon_closing_15", "core.p2.filmon_closing_16", "core.p2.filmon_closing_17",
        "core.p2.filmon_closing_18", "core.p2.filmon_closing_19", "core.p2.filmon_closing_20",
        "core.p2.filmon_closing_21",
    ];

    /// <summary>The wand lines on the bottom line (<c>0x23238</c>…<c>0x23240</c>; the "greaat" copy is unused).</summary>
    private static readonly string[] WandLineIds = ["core.p2.wand_line_1", "core.p2.wand_line_2", "core.p2.wand_line_3"];

    private static readonly string[] TitleWordIds =
    [
        "core.p2.title_word_0", "core.p2.title_word_1", "core.p2.title_word_2", "core.p2.title_word_3", "core.p2.title_word_4",
        "core.p2.title_word_5", "core.p2.title_word_6", "core.p2.title_word_7", "core.p2.title_word_8", "core.p2.title_word_9",
    ];

    private static string OriginName(Phantasie2Origin o) => o switch
    {
        Phantasie2Origin.Ferronrah => GameText.S("core.p2.origin_ferronrah"),
        Phantasie2Origin.Netherworld => GameText.S("core.p2.origin_netherworld"),
        _ => GameText.S("core.p2.origin_gelnor"),
    };

    public GameId Game => GameId.P2;

    public FeatureModuleSet DefaultFeatureModules => FeatureModuleSet.ForGame(GameId.P2);

    /// <summary>II's "%s DIES" path (<c>0x09894</c>) is listed as changed but not traced; I's death step is assumed [~].</summary>
    public IMemberDeathRule Death => PhantasieIDeathRule.Instance;

    // --- World facts ----------------------------------------------------------------------

    public int StartTownId => HomeTownId;

    private static readonly IReadOnlyList<int> AllTowns = Enumerable.Range(1, ReachableTownCount).ToArray();

    /// <summary>
    /// D2-030 (user s81, manual "from one town to another"): every reachable town, across Ferronrah and the Netherworld.
    /// The ST's same-plane lists are the <c>p2.transport-list</c> = <c>st</c> option (<see cref="TownState.TransportDestinations"/>).
    /// </summary>
    public IReadOnlyList<int> TransportTowns => AllTowns;

    public bool IsUnnamedTransportTown(int townId) => false;

    private static readonly IReadOnlyDictionary<int, int> NoAliases = new Dictionary<int, int>().AsReadOnly();

    /// <summary>II's town tiles carry their own town number (<c>towns.md</c> §3: nothing else sets the town).</summary>
    public IReadOnlyDictionary<int, int> OverworldTownAliases => NoAliases;

    /// <summary>
    /// The ST shows <c>OUT.RSC</c> tree 15 " THE RIVER STYX " past region 16, else tree 17 " `core.overworld.that_is_the_ocean` "
    /// (<c>terrain.md</c> §1; texts read from the resource file s83 [C]): I's two lines.
    /// </summary>
    public string BlockedMessage(int regionId) => regionId > LastSurfaceRegion
        ? GameText.S("core.overworld.the_river_styx")
        : GameText.S("core.overworld.that_is_the_ocean");

    private static readonly IReadOnlyList<int> StubMonsters = [0, 1, 2, 3, 4, 5, 6];

    public IReadOnlyList<int> FallbackMonsterPool => StubMonsters;

    /// <summary>II has no win scroll and no level-bound drop.</summary>
    public bool MayDropItem(int itemId, int? dungeonLevelId) => true;

    /// <summary>II's combat victory has no Black Lord test (<c>endgame.md</c> §6).</summary>
    public bool WithholdsDungeonTreasure(int firstMonsterId) => false;

    public bool IsBlackKnightSpot(int dungeonLevelId) => false;

    /// <summary>II's terrain has no special-site class (I's pool is II's mist, a terrain fight).</summary>
    public void OnSpecialSite(GameSession session, OverworldMoveResult result)
    {
    }

    // --- Creation --------------------------------------------------------------------------

    /// <summary>ORIGIN (<c>create_character 0x0c90c</c>): NETHERWORLD when the Guild's town number is above 6, else FERRONRAH.</summary>
    public void OnCharacterEnrolled(GameSession session, Character character) =>
        character.P2.Origin = OriginFor(session.GuildTownId);

    public static Phantasie2Origin OriginFor(int townId) =>
        townId >= FirstNetherworldTown ? Phantasie2Origin.Netherworld : Phantasie2Origin.Ferronrah;

    // --- The Mystic (score_check 0xd798) -----------------------------------------------------

    /// <summary>
    /// <c>endgame.md</c> §2: over OK members, s = 20·(an OK member carries the orb) + 10 per beast rune any OK member holds;
    /// rank = 5·(SCORE &gt; 150) + (s &gt; 10) + (s &gt; 60) + (s &gt; 90), 4 when s &gt; 50 without the orb, at most 5. Every
    /// party member's SCORE becomes max(SCORE, s).
    /// </summary>
    public TownTransactionResult ConsultMystic(GameSession session)
    {
        var party = session.Party;
        var ok = party.Members.Where(m => m.Status == CharacterStatus.Ok).ToList();
        var (score, rank, gelnor) = MysticScore(ok);
        foreach (var m in party.Members)
            m.P2.QuestScore = Math.Max(m.P2.QuestScore, score);
        var text = GameText.F("core.p2.mystic.score_x", score) + " " + GameText.S(RankIds[rank]);
        if (gelnor)
            text += " " + GameText.S("core.p2.mystic.heroes_of_gelnor_also");
        return new TownTransactionResult { Kind = TownTransactionKind.Ok, Message = text };
    }

    /// <summary>The Mystic's score, rank (0..5) and the "heroes of Gelnor also" flag for the counted (OK) members.</summary>
    public static (int Score, int Rank, bool HeroesOfGelnor) MysticScore(IReadOnlyList<Character> ok)
    {
        var orb = ok.Any(m => m.InventoryItemIds.Contains(OrbId));
        var hero5 = ok.Any(m => (m.Phantasie2?.QuestScore ?? 0) > 150);
        var gelnor = ok.Any(m => m.Score > 99);
        var runes = Enumerable.Range(0, Phantasie2CharacterData.MinionCount)
            .Count(k => ok.Any(m => m.Phantasie2?.BeastRunes[k] == true));
        var s = 20 * (orb ? 1 : 0) + 10 * runes;
        var rank = 5 * (hero5 ? 1 : 0) + (s > 10 ? 1 : 0) + (s > 60 ? 1 : 0) + (s > 90 ? 1 : 0);
        if (s > 50 && !orb)
            rank = 4;
        return (s, Math.Min(rank, 5), gelnor);
    }

    // --- Encounters: Pluto's minions ------------------------------------------------------------

    /// <summary>The first built group (round-end re-ranking reorders <see cref="CombatEncounter.Ranks"/>).</summary>
    private static MonsterRank? FirstGroup(CombatEncounter encounter) =>
        encounter.Ranks.FirstOrDefault(r => r.RankIndex == 0);

    /// <summary>Minion k (1..8) of a built fight, from group 0's reaction modifier; null when it is no minion.</summary>
    public static int? MinionOf(CombatEncounter encounter)
    {
        if (FirstGroup(encounter) is not { Members.Count: > 0 } group)
            return null;
        var mod = group.Members[0].ReactionModifier;
        var k = MinionReactionBase - mod;
        return mod < MinionReactionBase && k <= Phantasie2CharacterData.MinionCount ? k : null;
    }

    /// <summary>
    /// <c>encounter_start</c> §3.1: a minion that some party member already killed becomes the 1-based monster ⌊r·36/100⌋
    /// (0-based one less, SKELETON for 0), opened normally and rebuilt; otherwise `core.p2.plutos_minion`.
    /// </summary>
    public int? ReplaceEncounter(GameSession session, CombatEncounter encounter)
    {
        if (MinionOf(encounter) is not int k)
            return null;
        if (session.Party.Members.Any(m => m.Phantasie2?.MinionKilled[k - 1] == true))
        {
            var r = session.Rules.EncounterRulesOrDefault.Roll100(session.CampaignAccess.Rng);
            return Math.Max(r * 36 / 100 - 1, 0);
        }
        session.Note(GameText.S("core.p2.plutos_minion"));
        return null;
    }

    /// <summary>
    /// <c>combat_victory 0xa290</c>: a minion counts as killed when its whole group died. The first non-DEAD member takes the
    /// amulet, which goes into the first free sack slot with no question (lost when the sack is full), and every party
    /// member, dead ones too, gets the minion's flag. Killing PLUTO with every monster dead shows his speech.
    /// </summary>
    public void AfterCombatSurvived(GameSession session, CombatEncounter encounter, CombatResult result)
    {
        if (result.PartyWiped)
            return;
        // combat_victory only: a fight left by FLEE / BEG / TELEPORTATION gives nothing. A DISSOLVE win leaves the group's
        // count word alive, so no amulet and no flag (live s82, §3.1); the ST's FEAR never makes monsters run.
        // The ST box shows `core.p2.plutos_minion_is_killed` above "<name> TAKES AN AMULET" (live s82).
        if (result.PartyWon && MinionOf(encounter) is int k && FirstGroup(encounter)!.IsWiped)
        {
            session.Note(GameText.S("core.p2.plutos_minion_is_killed"));
            var taker = session.Party.Members.FirstOrDefault(m => m.Status != CharacterStatus.Dead);
            if (taker is not null)
                session.Note(GameText.F("core.p2.x_takes_an_amulet", taker.Name));
            session.Sack.TryAdd(AmuletBase + k);
            foreach (var m in session.Party.Members)
                m.P2.MinionKilled[k - 1] = true;
        }
        if (encounter.FirstMonsterId == PlutoId && encounter.AllMonsters.All(m => !m.IsAlive))
            foreach (var id in PlutoVoiceIds)
                session.Note(GameText.S(id));
    }



    // --- Dungeon events ---------------------------------------------------------------------------

    public void BeginDungeonEvent(GameSession session, DungeonMoveResult move)
    {
        var level = session.CampaignAccess.CurrentDungeonLevel;
        var feature = move.FeatureByte;
        if (DungeonScript.IsSpent(level, feature))
            return;
        var messageId = level.GetBufferByte(DungeonScript.EventRecordOffset(feature)) & 0x7f;
        if (level.Messages.TryGetValue(messageId, out var msg) && msg.IsMenu)
        {
            Ask(session, feature, messageId, msg);
            return;
        }
        RunDungeonEvent(session, feature, choice: 1);
    }

    private static void Ask(GameSession session, byte feature, int messageId, DungeonMessage msg)
    {
        session.CampaignAccess.DungeonEventPrompt = new PendingDungeonEvent(feature, messageId, msg);
        foreach (var line in msg.Lines)
            session.Note(line);
    }

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
        var level = access.CurrentDungeonLevel;
        var host = new ScriptHost(session, level, choice, alreadyShown);
        AfterDungeonScript(session, Phantasie2DungeonScript.Run(access.RequireDungeon(), level, feature, session.Party,
            host, access.Rng, session.Rules.EncounterRulesOrDefault));
    }

    public void ResumeDungeonScript(GameSession session)
    {
        var access = session.CampaignAccess;
        var resume = access.PendingDungeonScript!;
        access.PendingDungeonScript = null;
        var level = access.CurrentDungeonLevel;
        var host = new ScriptHost(session, level, resume.Choice, firstShown: true) { Resumed = true };
        AfterDungeonScript(session, Phantasie2DungeonScript.Resume(access.RequireDungeon(), level, session.Party, host,
            access.Rng, resume, session.Rules.EncounterRulesOrDefault));
    }

    private static void AfterDungeonScript(GameSession session, DungeonScriptResult result)
    {
        foreach (var line in result.Log)
            session.Note(line);
        var access = session.CampaignAccess;
        switch (result.End)
        {
            case DungeonScriptEnd.OfferItem:
                access.AddPendingTreasure(result.ItemId!.Value);
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
                if (result.TrapResult is { } trap)
                    access.OfferEventTrap(result.TrapKind, result.EventX, result.EventY, trap);
                break;
            case DungeonScriptEnd.EventMenu:
                var level = access.CurrentDungeonLevel;
                if (level.Messages.TryGetValue(result.MessageId, out var menu))
                    Ask(session, result.EventFeature, result.MessageId, menu);
                break;
            case DungeonScriptEnd.Ending:
                ResolveEnding(session);
                break;
        }
    }

    private sealed class ScriptHost(GameSession session, DungeonLevel level, int firstChoice, bool firstShown)
        : IDungeonScriptHost
    {
        private bool _first = true;

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
            NoteMessage(messageId);
            return 1;
        }

        public long FoundGold
        {
            get => session.Sack.Gold;
            set => session.Sack.Gold = value;
        }

        public void Cue(string moment) => session.Cue(moment);

        private void NoteMessage(int messageId)
        {
            if (level.Messages.TryGetValue(messageId, out var msg))
                foreach (var line in msg.Lines)
                    session.Note(line);
        }
    }

    // --- The ending: Filmon (endgame_filmon 0x12f2a) ---------------------------------------------------

    /// <summary>The ten title words (<c>rand() / 0xce4</c>), "avesome" as the ST spells it.</summary>
    private const int TitleWords = 10;

    /// <summary>The ending's sack scan stops at slot 54 (<c>endgame.md</c> §7 step 5).</summary>
    public const int EndingSackSlotCap = 54;

    /// <summary>
    /// State 7 (<c>endgame.md</c> §7): Filmon's ceremony. Titles for party records 0..n−1 (n = OK members, the ST's quirk),
    /// a wand of Filmon into the sack for every OK member, the roster written, then the game goes on in PIPPACOTT. The
    /// screen (intro page, <c>final2.pic</c>, the titles, the wand lines, the closing page; §7.1) waits in
    /// <see cref="GameSession.PendingVictoryScene"/> for the front end.
    /// </summary>
    public static void ResolveEnding(GameSession session)
    {
        var access = session.CampaignAccess;
        var members = session.Party.Members;
        var n = members.Count(m => m.Status == CharacterStatus.Ok);
        var titles = new List<string>();
        for (var i = 0; i < n && i < members.Count; i++)
        {
            var word = Math.Min(access.Rng.NextDosWord() / 0xce4, TitleWords - 1);
            var origin = OriginName(members[i].Phantasie2?.Origin ?? Phantasie2Origin.Gelnor);
            titles.Add(GameText.F("core.p2.title_x_x_x", members[i].Name, GameText.S(TitleWordIds[word]), origin));
        }
        var speaks = GameText.S("core.p2.filmon_speaks");
        access.PendingVictoryScene = new VictoryScene(
            [.. members.Select(m => new VictorySlot(m.Race, m.Status == CharacterStatus.Ok))],
            [speaks, .. WandLineIds.Select(GameText.S), " "]) // 0x133ba: the bottom line blanked (3 s or a key)
        {
            IntroPage = [.. FilmonIntroIds.Select(GameText.S)],
            HeaderLines = [speaks, GameText.S("core.p2.presenting_heroes")],
            Titles = titles,
            // The party size as {0}: the ST's lines take none; the Apple's "THESE "PO + 1" BRAVE ADVEN-" (WIN line 3116,
            // PO = the last party index, PEEK(SA + 1203)) prints it, so its own-wording line holds {0} (D2-050). The Apple
            // has no elementals; the remaster's summoned one is not an adventurer and is left out of the count (s170).
            ClosingPage = [.. FilmonClosingIds.Select(id => GameText.F(id, session.Party.Adventurers.Count()))],
        };
        // Remaster record (user s72, not ST data; s149): every member at Filmon's ceremony destroyed the orb, the deed IV's
        // Hall reads as "completed II" (ancestry.md section 8.4). Recorded before the roster write so the save keeps it.
        var orb = new Deed(Deed.Destroyed, new ContentId(GameId.P2, OrbId));
        foreach (var m in members)
            if (m.Deeds?.Contains(orb) != true)
                m.RecordDeed(orb);
        foreach (var m in members)
            if (m.Status == CharacterStatus.Ok && session.Sack.ItemCount < Math.Min(session.Sack.Capacity, LootSack.DosSlotCount))
                session.Sack.TryAdd(FilmonWandId);
        access.VictoryGuildWrite = session.Roster.Slots.Select(c => c?.Clone()).ToArray();
        access.LastEndgame = EndgameOutcome.Victory;
        access.ClearDungeonState();
        session.Overworld.RegionId = 1; // 0x25b92 := 1
        session.EnterTown(HomeTownId);
    }

    // --- Items: amulets and the wand of Filmon (use_item 0xdfcc) ----------------------------------------

    public IReadOnlyList<string>? ItemUseOptions(GameSession session, Character user, int itemId) => itemId switch
    {
        > AmuletBase and <= AmuletBase + Phantasie2CharacterData.MinionCount =>
            [GameText.S("core.p2.amulet_examine"), GameText.S("core.p2.amulet_wear"), GameText.S("core.p2.amulet_leave")],
        FilmonWandId => [],
        _ => null,
    };

    /// <summary>
    /// Amulet (dialog 18): EXAMINE gives beast rune j to every party member who has minion j's flag ("YOU ARE CHANGED");
    /// WEAR "LOOKS NICE !"; LEAVE nothing; the amulet is kept. Wand of Filmon: only at SCORE exactly 200 — removed, SCORE
    /// 250, each attribute bumped ("You are improved"); else "UNUSABLE".
    /// </summary>
    public TownTransactionResult UseItem(GameSession session, Character user, int itemId, int option)
    {
        if (itemId is > AmuletBase and <= AmuletBase + Phantasie2CharacterData.MinionCount)
        {
            var j = itemId - AmuletBase - 1;
            switch (option)
            {
                case 0:
                    foreach (var m in session.Party.Members)
                        if (m.Phantasie2?.MinionKilled[j] == true)
                            m.P2.BeastRunes[j] = true;
                    return Ok(GameText.S("core.p2.you_are_changed"));
                case 1:
                    return Ok(GameText.S("core.p2.looks_nice"));
                default:
                    return Ok("");
            }
        }
        if (itemId == FilmonWandId)
        {
            if ((user.Phantasie2?.QuestScore ?? 0) != OrbDestroyedScore || !user.InventoryItemIds.Remove(FilmonWandId))
                return new TownTransactionResult
                {
                    Kind = TownTransactionKind.Failed, Message = GameText.S("core.p2.unusable"), Unusable = true,
                };
            user.P2.QuestScore = WandUsedScore;
            user.Stats = BumpAll(user.Stats);
            return Ok(GameText.S("core.p2.you_are_improved"));
        }
        throw new NotSupportedException($"Item {itemId} has no Phantasie II use.");
    }

    /// <summary>The wand's <c>bump 0x2bc8</c> on STR, INT, DEX, CON, CHA: +1 if &lt; 9, +1 if LUCK &gt; 11 and &lt; 18, +1 if &lt; 20.</summary>
    public static CharacterStats BumpAll(CharacterStats s)
    {
        int Bump(int v)
        {
            if (v < 9) v++;
            if (s.Luck > 11 && v < 18) v++;
            if (v < 20) v++;
            return v;
        }
        return s with { Str = Bump(s.Str), Int = Bump(s.Int), Dex = Bump(s.Dex), Con = Bump(s.Con), Cha = Bump(s.Cha) };
    }

    private static TownTransactionResult Ok(string message) =>
        new() { Kind = TownTransactionKind.Ok, Message = message };

    // --- DIVINE II (cast_spell_field 0x6ecc, branch 0x71bc) ---------------------------------------------

    /// <summary>
    /// <c>magic-divine.md</c> §2: the MP (1) is spent first; in the wilderness of region 26 it summons the 1-based monster
    /// ⌊r₁·r₂·0.0072⌋, overwritten 25 % of the time by PLUTO'S WYVERN / SNAKE-DRAGON (75 / 76; 77 at r₄ = 100), 0 → 1.
    /// Anywhere else nothing
    /// happens. Other spells go to the shared caster.
    /// </summary>
    public MagicResult? CastFieldSpell(GameSession session, Character caster, int spellIndex)
    {
        if (spellIndex == DivineISpell)
            return CastDivineI(session, caster);
        if (spellIndex != DivineIiSpell)
            return null;
        if (caster.SpellsKnown.ElementAtOrDefault(DivineIiSpell) == 0)
            return MagicResult.Fail(GameText.S("core.magic.spell_not_known"));
        if (caster.MagicCur < 1)
            return MagicResult.Fail(GameText.S("core.magic.not_enough_mp"));
        caster.MagicCur -= 1;
        caster.CastSinceRest = true;
        var cast = GameText.F("core.p2.x_casts_divine_ii", caster.Name);
        if (session.Mode != GameMode.Overworld || session.Overworld.RegionId != DivineIiRegion)
            return MagicResult.Ok(cast, mpSpent: 1);
        var rules = session.Rules.EncounterRulesOrDefault;
        var rng = session.CampaignAccess.Rng;
        int r1 = rules.Roll100(rng), r2 = rules.Roll100(rng);
        var id = DivineIiBaseMonster(r1, r2);
        if (rules.Roll100(rng) < 25)
            id = DivineIiBeastMonster(rules.Roll100(rng));
        if (id == 0)
            id = 1;

        session.Note(cast);
        session.BeginWildernessCombat(id - 1, session.SurpriseGlobals);
        return MagicResult.Ok("", mpSpent: 1);
    }

    /// <summary>
    /// DIVINE II's base summon, 1-based: <c>ftoi(fmul(itof(r₁·r₂), 0.0072))</c> in the ST's FFP (<c>0x71fc..0x7232</c>,
    /// constant <c>0xEBEDFA39</c>) [C]. Equals ⌊r₁·r₂·0.0072⌋ for every pair of 0…100 draws (s83, checked in the program's
    /// own routines).
    /// </summary>
    public static int DivineIiBaseMonster(int r1, int r2) =>
        MotorolaFfp.ToInt(MotorolaFfp.Mul(MotorolaFfp.FromInt(r1 * r2), DivineIiFactor));

    /// <summary>
    /// DIVINE II's 25 % branch, 1-based: <c>ftoi(fadd(fmul(itof(r₄), 0.02), 75.0))</c> (<c>0x7246..0x7282</c>) [C]. In FFP
    /// 50·0.02 rounds to exactly 1 and 100·0.02 to 2, so r₄ 0–49 gives 75 PLUTO'S WYVERN, 50–99 gives 76 PLUTO'S
    /// SNAKE-DRAGON and r₄ = 100 gives 77 PLUTO'S GIANT WYVERN (minion 7) (s83, checked in the program's own routines).
    /// </summary>
    public static int DivineIiBeastMonster(int r4) =>
        MotorolaFfp.ToInt(MotorolaFfp.Add(MotorolaFfp.Mul(MotorolaFfp.FromInt(r4), MotorolaFfp.Two100ths), DivineIiBeastBase));

    private const uint DivineIiFactor = 0xEBEDFA39; // 0.0072

    // --- DIVINE I (cast_spell_field 0x6ecc, branch 0x72a4) ----------------------------------------------

    /// <summary>Core spell index of DIVINE I (ST slot 57, #58; II's <c>spells.json</c> index 54).</summary>
    public const int DivineISpell = SpellId.DivineI;

    /// <summary>DIVINE I's cost: spell 58, <c>58 mod 4</c> = 2 MP (<c>magic-divine.md</c> §1).</summary>
    public const int DivineICost = 2;

    /// <summary>The dungeon message DIVINE I shows: the level's advice record 105 (<c>0x116aa(105)</c>).</summary>
    public const int DivineIAdviceMessage = 105;

    /// <summary>The deity table <c>0x1fd12</c>: four names; index 4 (r₁ = r₂ = 100) reads past it.</summary>
    private static readonly string[] DeityKeys = ["core.p2.deity_0", "core.p2.deity_1", "core.p2.deity_2", "core.p2.deity_3"];

    public static int DeityCount => DeityKeys.Length;

    /// <summary>
    /// <c>magic-divine.md</c> §3: the MP (2) is spent first; in a dungeon a deity ⌊r₁·r₂·0.0004⌋ (DEMIGOD, ATHENA, APOLLO,
    /// ZEUS) `core.p2.x_appears_and_gives_you_advice`, then the level's message 105. Anywhere else nothing happens. Index 4 (both
    /// draws 100) makes the ST read a pointer past the table (a bus error on the ST <c>[~]</c>, D2-044): the remaster shows
    /// ZEUS, the last name (user s92).
    /// </summary>
    private static MagicResult CastDivineI(GameSession session, Character caster)
    {
        if (caster.SpellsKnown.ElementAtOrDefault(DivineISpell) == 0)
            return MagicResult.Fail(GameText.S("core.magic.spell_not_known"));
        if (caster.MagicCur < DivineICost)
            return MagicResult.Fail(GameText.S("core.magic.not_enough_mp"));
        caster.MagicCur -= DivineICost;
        caster.CastSinceRest = true;
        var cast = GameText.F("core.p2.x_casts_divine_i", caster.Name);
        if (session.Mode != GameMode.Dungeon || session.Dungeon is null)
            return MagicResult.Ok(cast, mpSpent: DivineICost);
        var rules = session.Rules.EncounterRulesOrDefault;
        var rng = session.CampaignAccess.Rng;
        int r1 = rules.Roll100(rng), r2 = rules.Roll100(rng);
        var deity = Math.Min(DivineIDeity(r1, r2), DeityCount - 1);
        var log = new List<string>
        {
            GameText.F("core.p2.x_appears_and_gives_you_advice", GameText.S(DeityKeys[deity])),
        };
        var level = session.CampaignAccess.CurrentDungeonLevel;
        if (level.Messages.TryGetValue(DivineIAdviceMessage, out var advice))
            log.AddRange(advice.Lines);
        return MagicResult.Ok(cast, mpSpent: DivineICost, log: log);
    }

    /// <summary>
    /// DIVINE I's deity index: <c>ftoi(fmul(itof(r₁·r₂), 0.0004))</c> in the ST's FFP (<c>0x72b8..0x72ee</c>, constant
    /// <c>0xD1B71735</c>) [C]; equals ⌊r₁·r₂·0.0004⌋ for every pair (s83), so 0…3, and 4 at r₁ = r₂ = 100.
    /// </summary>
    public static int DivineIDeity(int r1, int r2) =>
        MotorolaFfp.ToInt(MotorolaFfp.Mul(MotorolaFfp.FromInt(r1 * r2), DivineIFactor));

    private const uint DivineIFactor = 0xD1B71735; // 0.0004
    private const uint DivineIiBeastBase = 0x96000047; // 75.0

    // --- Party defeat ----------------------------------------------------------------------------------

    /// <summary>
    /// As I (the Great Demon's judgement, <see cref="PartyDefeat"/>; II's UNDEAD line is the string table's "GRANTED UNDEAD
    /// STATUS"), but survivors wake at OUT18 (18,14), the bloodstained temple; with nobody left the game goes to PIPPACOTT.
    /// </summary>
    /// <summary>The defeat of the run that shows the "Sorry, we've run out of Demons" sign (<c>0x12de4</c>: exactly the 5th) [C].</summary>
    public const int DemonSignDefeat = 5;

    public PartyDefeatResult ResolvePartyDefeat(GameSession session)
    {
        // 0x12678 clears the XP pool, the found gold and the clock but not the found items 0x25eb8 (live s82: an amulet in
        // the sack survived the defeat), unlike I.
        var result = PartyDefeat.Resolve(session.Party, session.Roster, session.Sack, session.CampaignAccess.Rng,
            keepFoundItems: true);
        result = new PartyDefeatResult
        {
            Fates = result.Fates, Log = result.Log, DemonsRanOut = session.PartyDefeatsThisRun == DemonSignDefeat,
        };
        foreach (var line in result.Log)
            session.Note(line);
        session.Overworld.RegionId = WakeRegion;
        session.Overworld.X = WakeX;
        session.Overworld.Y = WakeY;
        if (result.AnyRevived)
            session.CampaignAccess.ReturnToWilderness();
        else
            session.EnterTown(HomeTownId);
        return result;
    }
}
