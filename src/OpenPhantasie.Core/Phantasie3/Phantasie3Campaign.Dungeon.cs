namespace OpenPhantasie.Core;

/// <summary>
/// III's dungeons (ovl4): the step and its cell codes (s83 decode of <c>0xe14</c>), exits to the planes, and the event
/// squares and programs (<c>dungeon-events.md</c>; <see cref="Phantasie3DungeonScript"/>).
/// </summary>
public sealed partial class Phantasie3Campaign
{
    // Record offsets (FORMAT.md §9.5).
    public const int LevelOffset = 0x2d0, ForcedFightTable = 0x2d1, ExitTable = 0x2db, EventSquares = 0x2e4;

    /// <summary>Exit destinations that change plane (<c>dungeon-events.md</c> §7).</summary>
    public const int ExitToLight = 29, ExitToDark = 31, ExitToNetherworld = 41;

    /// <summary>The plain square a won fight leaves behind (op 5, forced fights).</summary>
    public const byte SpentFightCell = 0x8a;

    /// <summary>A sprung or disarmed trap becomes this cell (ovl4 <c>0x11d1</c> / <c>0x1224</c>) [C].</summary>
    public const byte SpentTrapCell = 7;

    // Synthetic prompt ids for questions that are not event-square texts.
    private const int YesNoPrompt = -1, WhoPrompt = -2;

    /// <summary>
    /// Entry (ovl4 <c>0x62d</c>): the dungeon's encounter chance <c>FAR:0xdd8c</c> := 10; the party stands on the
    /// entrance exit cell, so `core.prompt.an_exit_take_it` is the first question (live s82). Facing is the shared south.
    /// </summary>
    public void OnDungeonEntered(GameSession session)
    {
        var d = session.CampaignAccess.RequireDungeon();
        d.WanderingChance = DungeonChanceOnEntry;
        var level = session.CampaignAccess.CurrentDungeonLevel;
        if (level.Get(d.X, d.Y) is >= 0xd7 and <= 0xd9)
            session.CampaignAccess.DungeonExitPending = true;
    }

    /// <summary>The dungeon level L (record <c>0x2d0</c>): random and lair monsters come from band 12L.</summary>
    private static int DungeonLevelL(DungeonLevel level) => level.GetBufferByte(LevelOffset);

    /// <summary>
    /// One dungeon step (ovl4 <c>0xe14</c>, s83): cell 0 is a wall <c>[~]</c> (the maps' corridors are framed by 0); then by
    /// the cell entered: forced fight 0x80–0x89; random square 8–17 (a fight when the chance &gt; <c>rand() % 100</c>, the
    /// chance −1 after it while above 1); trap 2–6; door with monsters 0xba–0xc5 / 0xc9–0xd4; locked door 0xc6 / 0xd5; exit
    /// 0xd7–0xd9; event square ≥ 0xdd; any other byte is floor. <c>rand() % 100</c> is drawn on every step.
    /// </summary>
    public DungeonMoveResult? StepDungeon(GameSession session, DungeonState d, DungeonLevel level, int x, int y)
    {
        DungeonMoveResult Result(DungeonMoveKind kind, byte cell, DungeonFeatureKind fk, string? message = null,
            int? monster = null, SurpriseFlags? surprise = null) => new()
        {
            Kind = kind, LevelId = d.LevelId, X = d.X, Y = d.Y, FeatureByte = cell, FeatureKind = fk, Message = message,
            MonsterId = monster, Surprise = surprise,
        };

        if (!level.IsWalkable(x, y))
        {
            if (level.InBounds(x, y))
                session.PlaySound(P3Sounds.Wall); // cell 0 (DOS ovl4 0xf6c, Amiga 0x1c4be); off the map: no sound
            return Result(DungeonMoveKind.BlockedWall, level.InBounds(x, y) ? level.Get(x, y) : (byte)0,
                DungeonFeatureKind.Wall, GameText.S("core.dungeon.that_is_the_wall"));
        }

        d.PrevX = d.X;
        d.PrevY = d.Y;
        d.X = x;
        d.Y = y;
        session.TravelTime += P3TravelTime.DungeonStep; // ovl4 0xc02: ⅓ day per step that moves
        var cell = level.Get(x, y);
        var rng = session.CampaignAccess.Rng;
        var roll = rng.Next(0, 100); // ovl4 0xea7: rand() % 100, every step
        var L = DungeonLevelL(level);

        switch (cell)
        {
            case >= 0x80 and <= 0x89:
            {
                var monster = level.GetBufferByte(ForcedFightTable + cell - 0x80);
                _nextFight = new FightSetup(false, FightMode.Normal, ClearFlag: 1, FromEvent: false);
                return Result(DungeonMoveKind.TriggerCombat, cell, DungeonFeatureKind.Combat, monster: monster,
                    surprise: SurpriseFlags.Normal);
            }
            case >= 8 and <= 17:
                if (d.WanderingChance <= roll)
                    return Result(DungeonMoveKind.Moved, cell, DungeonFeatureKind.Encounter);
                {
                    var monster = 12 * L + rng.Next(0, 3) + cell - 8;
                    if (d.WanderingChance > 1)
                        d.WanderingChance--;
                    _nextFight = new FightSetup(false, FightMode.Normal, ClearFlag: 0, FromEvent: false);
                    return Result(DungeonMoveKind.TriggerEncounter, cell, DungeonFeatureKind.Encounter, monster: monster,
                        surprise: SurpriseFlags.Normal);
                }
            case >= 2 and <= 6:
                return Trap(session, d, level, cell);
            case (>= 0xba and <= 0xc5) or (>= 0xc9 and <= 0xd4):
            {
                var listener = SkillCheck(session, P3Skill.Listen);
                if (listener is not null)
                {
                    AskYesNo(session, cell, GameText.F("core.p3.x_hears_monsters_continue", listener.Name));
                    session.PlaySound(P3Sounds.MemberLine);
                    return Result(DungeonMoveKind.TriggerLock, cell, DungeonFeatureKind.LockOrAmbush);
                }
                _nextFight = new FightSetup(false, FightMode.MonstersSurprise, ClearFlag: cell, FromEvent: false);
                return Result(DungeonMoveKind.TriggerCombat, cell, DungeonFeatureKind.LockOrAmbush,
                    monster: DoorMonster(cell, L), surprise: SurpriseFor(FightMode.MonstersSurprise));
            }
            case 0xc6 or 0xd5:
                if (SkillCheck(session, P3Skill.PickLock) is { } picker)
                {
                    level.Set(x, y, (byte)(cell - 13));
                    session.PlaySound(P3Sounds.MemberLine);
                    return Result(DungeonMoveKind.Moved, cell, DungeonFeatureKind.LockOrAmbush,
                        GameText.F("core.p3.x_picks_the_lock", picker.Name));
                }
                DungeonTraversal.StepBack(d);
                return Result(DungeonMoveKind.BlockedWall, cell, DungeonFeatureKind.LockOrAmbush,
                    GameText.S("core.p3.the_door_is_locked"));
            case 0xc7 or 0xd6:
                // Special doors (ovl4 0xf77: step result 4, as a lock nobody picks; de58 = 0x2a) [~]: the manual's locks
                // that "cannot be picked" (III PDF 7 / pr.9). The party stays where it was.
                session.PlaySound(P3Sounds.Loss); // DOS ovl4 0xf92 / Amiga 0x1c4e0: event word 42
                DungeonTraversal.StepBack(d);
                return Result(DungeonMoveKind.BlockedWall, cell, DungeonFeatureKind.LockOrAmbush,
                    GameText.S("core.p3.the_door_is_locked"));
            case >= 0xd7 and <= 0xd9:
                session.CampaignAccess.DungeonExitPending = true;
                return Result(DungeonMoveKind.ExitDungeon, cell, DungeonFeatureKind.ExitOrStairs); // the shared prompt asks
            case >= 0xdd:
                return level.GetBufferByte(EventSquares + 5 * (cell - 0xdd)) == 0xff
                    ? Result(DungeonMoveKind.Moved, cell, DungeonFeatureKind.ScriptedEvent)
                    : Result(DungeonMoveKind.ScriptedEvent, cell, DungeonFeatureKind.ScriptedEvent);
            default:
                return Result(DungeonMoveKind.Moved, cell, DungeonFeatureKind.Floor);
        }
    }

    /// <summary>The monster behind a door (ovl4 <c>0x1056</c>): <c>(cell ≥ 0xc8 ? cell − 15 : cell) − 0xba + 12L</c>.</summary>
    public static int DoorMonster(byte cell, int level) => (cell >= 0xc8 ? cell - 15 : cell) - 0xba + 12 * level;

    /// <summary>
    /// A trap square 2–6 (ovl4 <c>0x10d8…0x1263</c>): the first member passing Find Trap spots it ("… spots a trap. Disarm
    /// it?"); otherwise it springs.
    /// </summary>
    private DungeonMoveResult Trap(GameSession session, DungeonState d, DungeonLevel level, byte cell)
    {
        if (SkillCheck(session, P3Skill.FindTrap) is { } spotter)
        {
            AskYesNo(session, cell, GameText.F("core.p3.x_spots_a_trap", spotter.Name));
            session.PlaySound(P3Sounds.MemberLine);
            return new DungeonMoveResult
            {
                Kind = DungeonMoveKind.TriggerTrap, LevelId = d.LevelId, X = d.X, Y = d.Y, FeatureByte = cell,
                FeatureKind = DungeonFeatureKind.Trap,
            };
        }
        SpringTrap(session, level, cell, d.X, d.Y);
        return new DungeonMoveResult
        {
            Kind = DungeonMoveKind.Moved, LevelId = d.LevelId, X = d.X, Y = d.Y, FeatureByte = cell,
            FeatureKind = DungeonFeatureKind.Trap,
        };
    }

    /// <summary>
    /// "A trap!!!": every member with HP &gt; 0 takes <c>rnd(5t − 25)</c>, 12 when that is below 0 or above 25 (a die of 0
    /// hurts nobody <c>[~]</c>); the square becomes 7. A wiped party goes to the Astral Plane.
    /// </summary>
    private static void SpringTrap(GameSession session, DungeonLevel level, byte cell, int x, int y)
    {
        session.Note(GameText.S("core.p3.a_trap"));
        var die = 5 * cell - 25;
        if (die is > 25 or < 0)
            die = 12;
        if (DamageParty(session, die))
            session.CampaignAccess.PartyDefeatPending = true;
        level.Set(x, y, SpentTrapCell);
    }

    /// <summary>
    /// The answer lit when a III dungeon Yes / No question opens (ovl4 <c>0x1b81</c> → root <c>0x1820</c> → menu
    /// <c>0x75a3</c>, s108 static [C]): "… `core.p3.x_spots_a_trap`" "Yes" (1, ovl4 <c>0x116c</c>), "… hears monsters.
    /// Continue?" "No" (2, <c>0x100d</c>); null for any other event prompt.
    /// </summary>
    /// <summary>
    /// True for `core.p3.who_will_disarm` (ovl4 <c>0x1189</c>: the member picker <c>0x18bd(92)</c>; Amiga window 6,
    /// <c>0x127ce(92)</c>): one choice per party member, and one more (<see cref="SessionPrompt.EscapeChoice"/>) for the
    /// picker's cancel.
    /// </summary>
    public static bool AsksDisarmer(PendingDungeonEvent prompt) => prompt.MessageId == WhoPrompt;

    public static int? YesNoHighlight(PendingDungeonEvent prompt) =>
        prompt.MessageId != YesNoPrompt ? null : prompt.Feature is >= 2 and <= 6 ? 1 : 2;

    private static void AskYesNo(GameSession session, byte cell, string text)
    {
        var msg = new DungeonMessage([text], [SessionPrompts.Yes, SessionPrompts.No]);
        session.CampaignAccess.DungeonEventPrompt = new PendingDungeonEvent(cell, YesNoPrompt, msg);
        session.Note(text);
    }

    /// <summary>
    /// `core.prompt.an_exit_take_it` YES (ovl4 <c>0x95b</c>): scores are recomputed; only the exit entry's destination byte
    /// is read: 29 / 31 / 41 put the party on the Light / Dark / Netherworld plane at its arrival square, anything else
    /// leaves it on the dungeon's own square.
    /// </summary>
    public bool TakeDungeonExit(GameSession session, DungeonState state, DungeonLevel level)
    {
        ComputeScores(session.Party);
        var cell = level.Get(state.X, state.Y);
        if (cell is < 0xd7 or > 0xd9)
            return true;
        var plane = level.GetBufferByte(ExitTable + 3 * (cell - 0xd7)) switch
        {
            ExitToLight => LightPlane,
            ExitToDark => DarkPlane,
            ExitToNetherworld => Netherworld,
            _ => (int?)null,
        };
        // An exit to the plane the party is on reloads its map but keeps the position (the loaded-plane marker is
        // plane + 10 while in a dungeon, dungeon-events.md §7): only another plane uses its arrival square.
        if (plane is int p && p != session.Overworld.RegionId && World.PlaneArrivals.TryGetValue(p, out var arrival))
        {
            session.Overworld.RegionId = p;
            session.Overworld.X = arrival.X;
            session.Overworld.Y = arrival.Y;
        }
        return true;
    }

    // --- Event squares and programs ------------------------------------------------------------------------------------

    /// <summary>The event square entry of cell <paramref name="cell"/> (record <c>0x2e4 + 5·(cell − 0xdd)</c>).</summary>
    private static int EntryOffset(int cell) => EventSquares + 5 * (cell - 0xdd);

    /// <summary>
    /// Entering an event square (ovl4 <c>0x1299</c>): an entry of 0xff does nothing; bit 7 = once (the entry becomes 0xff in
    /// the work record); the text <c>(E[0] &amp; 0x7f) − 1</c> shows; with more than one choice the game waits for a key
    /// 1…n, then runs program <c>E[2 + k − 1]</c>. The copy-protection quiz is left out. The texts go on the event page
    /// (<see cref="DungeonTextPage"/>), which this step starts empty (Amiga <c>0x1bf62</c>).
    /// </summary>
    public void BeginDungeonEvent(GameSession session, DungeonMoveResult move)
    {
        session.CampaignAccess.DungeonPage = null;
        RunSquare(session, move.FeatureByte, continuation: null);
    }

    private void RunSquare(GameSession session, int cell, DungeonScriptResume? continuation)
    {
        var level = session.CampaignAccess.CurrentDungeonLevel;
        var at = EntryOffset(cell);
        var e0 = level.GetBufferByte(at);
        if (e0 == 0xff)
        {
            Continue(session, continuation);
            return;
        }
        if ((e0 & 0x80) != 0)
            level.SetBufferByte(at, 0xff);
        var text = (e0 & 0x7f) - 1;
        var n = level.GetBufferByte(at + 1);
        if (n <= 1)
        {
            var program = Phantasie3DungeonScript.ProgramOffset(level.GetBufferByte(at + 2));
            if (ShowText(session, level, text))
            {
                // "Press a key": the program runs once the page is acknowledged.
                session.CampaignAccess.PendingDungeonScript = new DungeonScriptResume
                {
                    Feature = (byte)cell, NextOp = program, Caller = continuation, TextWait = true,
                };
                return;
            }
            new Phantasie3DungeonScript(this, session, cell).RunFrom(program, continuation);
            return;
        }
        // Wait for a key 1..n (the text stays up, "Press a number").
        var msg = level.Messages.TryGetValue(text, out var m) ? m : new DungeonMessage([], []);
        var options = msg.Options.Count >= n ? msg.Options : PadOptions(msg.Options, n);
        session.CampaignAccess.DungeonEventPrompt = new PendingDungeonEvent((byte)cell, text, msg with { Options = options });
        session.CampaignAccess.PendingDungeonScript = continuation is null ? null : MenuContinuation(continuation);
        foreach (var line in msg.Lines)
            session.Note(line);
        if (string.Concat(msg.Lines).Length + string.Concat(msg.Options).Length >= 3)
            AddToPage(session, [.. msg.Lines, .. msg.Options], waits: false);
    }

    private static IReadOnlyList<string> PadOptions(IReadOnlyList<string> given, int n) =>
        Enumerable.Range(1, n).Select(k => k <= given.Count ? given[k - 1] : k.ToString(System.Globalization.CultureInfo.InvariantCulture)).ToArray();

    /// <summary>A continuation kept while a menu waits: <see cref="DungeonScriptResume.Choice"/> = −1 marks it.</summary>
    private static DungeonScriptResume MenuContinuation(DungeonScriptResume c) => new()
    {
        Feature = c.Feature, NextOp = c.NextOp, CellX = c.CellX, CellY = c.CellY, Choice = -1, Caller = c.Caller,
    };

    public void AnswerDungeonEvent(GameSession session, int choice)
    {
        var access = session.CampaignAccess;
        var prompt = access.DungeonEventPrompt ?? throw new InvalidOperationException("No dungeon event is waiting for an answer.");
        if (choice < 1 || choice > prompt.Message.Options.Count + (prompt.MessageId == WhoPrompt ? 1 : 0))
            throw new ArgumentOutOfRangeException(nameof(choice));
        access.DungeonEventPrompt = null;
        var d = access.RequireDungeon();
        var level = access.CurrentDungeonLevel;
        var cell = prompt.Feature;
        if (prompt.MessageId == YesNoPrompt)
        {
            AnswerYesNo(session, d, level, cell, yes: choice == 1);
            return;
        }
        if (prompt.MessageId == WhoPrompt)
        {
            // ovl4 0x1189…0x11bc (Amiga 0x1c6d6…0x1c71a, s163 static): the picker asks again while the pick is Dead or Out;
            // its cancel (the choice after the members) leaves the trap as No does (step result −1 at 0x1265).
            if (choice > session.Party.Members.Count)
            {
                DungeonTraversal.StepBack(d);
                return;
            }
            if (session.Party.Members[choice - 1].Status is CharacterStatus.Dead or CharacterStatus.Out)
            {
                access.DungeonEventPrompt = prompt;
                return;
            }
            Disarm(session, d, level, cell, choice - 1);
            return;
        }
        var continuation = access.PendingDungeonScript is { Choice: -1 } c ? c : null;
        access.PendingDungeonScript = null;
        RunProgram(session, level.GetBufferByte(EntryOffset(cell) + 2 + choice - 1), cell, continuation);
    }

    private void AnswerYesNo(GameSession session, DungeonState d, DungeonLevel level, byte cell, bool yes)
    {
        if (cell is >= 2 and <= 6)
        {
            if (!yes)
            {
                DungeonTraversal.StepBack(d); // [~] ovl4 0x1265: no cell change, the step returns 0
                return;
            }
            var names = session.Party.Members.Select(m => m.Name).ToList();
            session.CampaignAccess.DungeonEventPrompt =
                new PendingDungeonEvent(cell, WhoPrompt, new DungeonMessage([GameText.S("core.p3.who_will_disarm")], names));
            session.Note(GameText.S("core.p3.who_will_disarm"));
            return;
        }
        // A door with monsters: Continue = the party surprises them; else back where it came from.
        if (!yes)
        {
            DungeonTraversal.StepBack(d);
            return;
        }
        _nextFight = new FightSetup(false, FightMode.PartySurprise, ClearFlag: cell, FromEvent: false);
        session.BeginDungeonCombat(DoorMonster(cell, DungeonLevelL(level)), SurpriseFor(FightMode.PartySurprise));
    }

    /// <summary>
    /// The chosen member (not Dead or Out) rolls Disarm: success clears the trap, failure springs it. s163: success leaves
    /// the party where it came from, as No does (ovl4 <c>0x120d</c> → <c>0x1265</c>, step result −1; Amiga <c>0x1c762</c> →
    /// <c>0x1c7c2</c>; seen live: the next step went onto the disarmed cell).
    /// </summary>
    private static void Disarm(GameSession session, DungeonState d, DungeonLevel level, byte cell, int memberIndex)
    {
        var m = (uint)memberIndex < (uint)session.Party.Members.Count ? session.Party.Members[memberIndex] : null;
        if (m is not null && m.Status is not (CharacterStatus.Dead or CharacterStatus.Out)
            && P3Dice.Rnd(session.CampaignAccess.Rng, 100) <= m.Skills.DisarmTrap)
        {
            level.Set(d.X, d.Y, SpentTrapCell);
            DungeonTraversal.StepBack(d);
            session.Note(GameText.F("core.p3.x_disarms_the_trap", m.Name));
            session.PlaySound(P3Sounds.MemberLine);
            return;
        }
        SpringTrap(session, level, cell, d.X, d.Y);
    }

    public void ResumeDungeonScript(GameSession session)
    {
        var access = session.CampaignAccess;
        var resume = access.PendingDungeonScript!;
        if (resume.Choice == -1)
            return; // a menu's caller continuation: only AnswerDungeonEvent carries on from it
        access.PendingDungeonScript = null;
        Continue(session, resume);
    }

    /// <summary>Run a stopped program on from its next op, then the programs that ran it as a sub-square.</summary>
    private void Continue(GameSession session, DungeonScriptResume? continuation)
    {
        if (continuation is null)
            return;
        if (continuation.NextOp <= 0)
        {
            Continue(session, continuation.Caller);
            return;
        }
        var script = new Phantasie3DungeonScript(this, session, continuation.Feature);
        script.RunFrom(continuation.NextOp, continuation.Caller);
    }

    private void RunProgram(GameSession session, int programIndex, int cell, DungeonScriptResume? continuation) =>
        new Phantasie3DungeonScript(this, session, cell).RunFrom(Phantasie3DungeonScript.ProgramOffset(programIndex), continuation);

    /// <summary>
    /// An event text with no menu (ovl4 <c>0x1c4d</c>, Amiga <c>0x1d982(text, 0)</c>): a text shorter than 3 bytes is not
    /// shown; any other goes on the event page and waits for a key. Returns true when it waits (the caller stops the
    /// program there).
    /// </summary>
    internal bool ShowText(GameSession session, DungeonLevel level, int text)
    {
        if (!level.Messages.TryGetValue(text, out var msg) || string.Concat(msg.Lines).Length < 3)
            return false;
        foreach (var line in msg.Lines)
            session.Note(line);
        AddToPage(session, [.. msg.Lines, .. msg.Options], waits: true);
        return true;
    }

    /// <summary>Pile <paramref name="lines"/> under the page's earlier rows (Amiga <c>0x24074</c>).</summary>
    private static void AddToPage(GameSession session, IReadOnlyList<string> lines, bool waits)
    {
        var access = session.CampaignAccess;
        access.DungeonPage = new DungeonTextPage([.. access.DungeonPage?.Lines ?? [], .. lines], waits);
    }

    /// <summary>A fight an event program starts (op 5: the square becomes 0x8a after a win; op 17: an ambush, no change).</summary>
    internal void StartEventFight(GameSession session, int monster, bool ambush)
    {
        session.CampaignAccess.DungeonPage = null; // the fight starts a new page (Amiga 0x1da86 clears 0x24074)
        _nextFight = new FightSetup(false, ambush ? FightMode.MonstersSurprise : FightMode.Normal,
            ClearFlag: ambush ? 0 : 1, FromEvent: !ambush);
        session.BeginDungeonCombat(monster, SurpriseFor(ambush ? FightMode.MonstersSurprise : FightMode.Normal));
    }

    /// <summary>A program stopped at "Keep … ?" (op 6) or at a sub-square's menu (op 22) and carries on afterwards.</summary>
    /// <remarks>
    /// A program that ends at op 6 still resumes through a fresh record (<c>NextOp</c> 0, then its caller): the caller may be
    /// a menu's continuation (<see cref="DungeonScriptResume.Choice"/> −1), which <see cref="ResumeDungeonScript"/> skips.
    /// </remarks>
    internal static void Suspend(GameSession session, int cell, int nextOp, DungeonScriptResume? caller) =>
        session.CampaignAccess.PendingDungeonScript =
            new DungeonScriptResume { Feature = (byte)cell, NextOp = Math.Max(0, nextOp), Caller = caller };

    internal void RunSubSquare(GameSession session, int cell, DungeonScriptResume? continuation) =>
        RunSquare(session, cell, continuation);
}
