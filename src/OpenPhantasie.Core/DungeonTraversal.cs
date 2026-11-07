namespace OpenPhantasie.Core;

/// <summary>
/// Dungeon crawling — OV2.OVR FUN_0000_1f05 movement + FUN_0000_2249 feature dispatch.
/// </summary>
public sealed class DungeonTraversal
{
    private readonly Dictionary<int, DungeonLevel> _levels;

    /// <summary>The game's encounter rules for wandering cells (<see cref="EncounterRules"/>; default Phantasie I's).</summary>
    public EncounterRules Encounters { get; init; } = EncounterRules.PhantasieI;

    public DungeonTraversal(IEnumerable<DungeonLevel> levels)
    {
        _levels = levels.ToDictionary(l => l.LevelId);
        if (_levels.Count == 0)
            throw new ArgumentException("At least one dungeon level required.", nameof(levels));
    }

    public DungeonLevel GetLevel(int levelId) =>
        _levels.TryGetValue(levelId, out var level)
            ? level
            : throw new KeyNotFoundException($"Dungeon level {levelId} not found.");

    public IReadOnlyCollection<int> LevelIds => _levels.Keys;

    /// <summary>The level the party is in: the state's live buffer, else the loaded original.</summary>
    public DungeonLevel LevelOf(DungeonState state) => state.Level ?? GetLevel(state.LevelId);

    /// <summary>
    /// Enter from overworld using OUT* feature dest_x/dest_y. The party plays on a working copy: <paramref name="restored"/>
    /// (a saved slot's buffer, OV2 <c>0x57ac</c>) or a fresh copy of the original file.
    /// </summary>
    public void Enter(DungeonState state, int levelId, int destX, int destY, DungeonLevel? restored = null)
    {
        var level = restored ?? GetLevel(levelId).CloneAsBuffer();
        if (!level.IsWalkable(destX, destY))
            throw new InvalidOperationException(
                $"Dungeon {levelId} entrance ({destX},{destY}) is not walkable.");

        state.LevelId = levelId;
        state.Level = level;
        state.X = state.PrevX = destX;
        state.Y = state.PrevY = destY;
        state.Facing = DungeonFacing.South; // entrances sit on the north edge
        state.WanderingChance = DungeonEncounters.ChanceOnEntry; // state 13 (0x82): [0x1a5a] := 7
        state.LastMessageId = 0;                                 // 0x1dc6: [0x64f4] := 0
    }

    public void TurnLeft(DungeonState state) =>
        state.Facing = (DungeonFacing)(((int)state.Facing + 3) % 4);

    public void TurnRight(DungeonState state) =>
        state.Facing = (DungeonFacing)(((int)state.Facing + 1) % 4);

    /// <summary>Step one cell forward relative to facing.</summary>
    public DungeonMoveResult TryMoveForward(DungeonState state, Party? party = null, IRandomSource? rng = null)
    {
        var (dx, dy) = FacingDelta(state.Facing);
        return TryMoveAbsolute(state, state.X + dx, state.Y + dy, party, rng);
    }

    /// <summary>Absolute cardinal step (same axes as the 33×38 grid; +Y is south).</summary>
    public DungeonMoveResult TryMove(DungeonState state, CardinalDirection dir, Party? party = null, IRandomSource? rng = null)
    {
        var (dx, dy) = dir switch
        {
            CardinalDirection.North => (0, -1),
            CardinalDirection.South => (0, 1),
            CardinalDirection.East => (1, 0),
            CardinalDirection.West => (-1, 0),
            _ => (0, 0),
        };
        return TryMoveAbsolute(state, state.X + dx, state.Y + dy, party, rng);
    }

    public DungeonMoveResult TryMoveAbsolute(
        DungeonState state,
        int nx,
        int ny,
        Party? party = null,
        IRandomSource? rng = null)
    {
        var level = LevelOf(state);

        if (!level.InBounds(nx, ny) || !level.IsWalkable(nx, ny))
        {
            return new DungeonMoveResult
            {
                Kind = DungeonMoveKind.BlockedWall,
                LevelId = state.LevelId,
                X = state.X,
                Y = state.Y,
                FeatureByte = level.InBounds(nx, ny) ? level.Get(nx, ny) : (byte)0,
                FeatureKind = DungeonFeatureKind.Wall,
                Message = GameText.S("core.dungeon.that_is_the_wall"),
            };
        }

        state.PrevX = state.X; // 0x1f58
        state.PrevY = state.Y;
        state.X = nx;
        state.Y = ny;
        var feature = level.Get(nx, ny);
        var kind = DungeonLevel.Classify(feature);
        return DispatchFeature(state, level, feature, kind, party, rng);
    }

    private DungeonMoveResult DispatchFeature(
        DungeonState state,
        DungeonLevel level,
        byte feature,
        DungeonFeatureKind kind,
        Party? party,
        IRandomSource? rng)
    {
        switch (kind)
        {
            case DungeonFeatureKind.Floor:
            case DungeonFeatureKind.Other:
                return Moved(state, feature, kind);

            case DungeonFeatureKind.Trap:
                return ResolveTrapAt(state, level, feature, state.X, state.Y, party, rng, feature, kind, Encounters.Roll);

            case DungeonFeatureKind.Encounter:
            {
                // 0x2259: rand0_99 ≤ [0x1a5a] → fight (F,H = 1,1, 0x227e); else message cell + 3 (0x23c0).
                if (rng is null)
                    return Moved(state, feature, kind);
                var monster = DungeonEncounters.RollWandering(level, state.LevelId, feature, state.WanderingChance, rng, Encounters);
                if (monster is int id)
                    return Fight(state, feature, kind, id, DungeonMoveKind.TriggerEncounter);
                return ShowMessage(state, level, feature, kind, feature + 3);
            }

            case DungeonFeatureKind.Combat:
                // 0x2383: monster buf[0x468 + b], F,H = 1,1 (0x2396). No message.
                return Fight(state, feature, kind, DungeonEncounters.ForcedFightMonster(level, feature),
                    DungeonMoveKind.TriggerCombat);

            case DungeonFeatureKind.Message:
                return ShowMessage(state, level, feature, kind, feature - 0x76); // 0x22c8 → 0x23c0

            case DungeonFeatureKind.ScriptLow:
                return ShowDescription(state, level, feature, kind, feature - 0x11); // 0x298b

            case DungeonFeatureKind.LockOrAmbush:
                return ResolveLock(state, level, feature, kind, party, rng, Encounters.Roll);

            case DungeonFeatureKind.ExitOrStairs:
                return new DungeonMoveResult
                {
                    Kind = DungeonMoveKind.ExitDungeon,
                    LevelId = state.LevelId,
                    X = state.X,
                    Y = state.Y,
                    FeatureByte = feature,
                    FeatureKind = kind,
                    // No text of its own (s58 DOSBox): "AN EXIT. TAKE IT?" is the session's prompt and the old message
                    // line stays under it.
                };

            case DungeonFeatureKind.StairsMessage:
                // FUN_0000_2951: prints (feature-0xd9) → (feature-0xd8)
                return new DungeonMoveResult
                {
                    Kind = DungeonMoveKind.TriggerStairs,
                    LevelId = state.LevelId,
                    X = state.X,
                    Y = state.Y,
                    FeatureByte = feature,
                    FeatureKind = kind,
                    Message = GameText.F("core.dungeon.stairs_from_level_x_to_x", feature - 0xd9, feature - 0xd8).TrimEnd(), // "%-2d"; nothing else happens
                };

            case DungeonFeatureKind.ScriptedEvent:
                // OV2 0x24b2: a spent event record (r0 == 0xff) does nothing (return 3). The record lives in the live
                // buffer, so spending it persists exactly as far as the buffer does (save slot, D-027 policy).
                if (DungeonScript.IsSpent(level, feature))
                    return Moved(state, feature, kind);
                return new DungeonMoveResult
                {
                    Kind = DungeonMoveKind.ScriptedEvent,
                    LevelId = state.LevelId,
                    X = state.X,
                    Y = state.Y,
                    FeatureByte = feature,
                    FeatureKind = kind,
                    // No text of its own: 0x24b2 prints only through 0x2a95(r0 & 0x7f), which the session shows (s49).
                    Message = null,
                };

            default:
                return Moved(state, feature, kind);
        }
    }

    private static DungeonMoveResult Fight(DungeonState state, byte feature, DungeonFeatureKind kind, int monsterId,
        DungeonMoveKind moveKind) => new()
    {
        Kind = moveKind,
        LevelId = state.LevelId,
        X = state.X,
        Y = state.Y,
        FeatureByte = feature,
        FeatureKind = kind,
        MonsterId = monsterId,
        Surprise = SurpriseFlags.Normal,
    };

    /// <summary>
    /// OV2 <c>0x23c0(id)</c>: show MESS message <paramref name="messageId"/> unless it is the last one shown
    /// (<c>[0x64f4]</c>). A message the level does not hold shows nothing and is not remembered (<c>0x2418</c> failed).
    /// Only the first 40-character record is shown, on the message line (<c>0x2418(id, 0x29)</c> → <c>0x2d03</c>):
    /// no continuation lines and no menu (s49). Every id DNG1..10 sends here is one line long.
    /// </summary>
    private static DungeonMoveResult ShowMessage(DungeonState state, DungeonLevel level, byte feature,
        DungeonFeatureKind kind, int messageId)
    {
        if (!level.Messages.TryGetValue(messageId, out var msg) || messageId == state.LastMessageId)
            return Moved(state, feature, kind); // 0x2418 found nothing, or the same message again: nothing shown
        state.LastMessageId = messageId;
        return new DungeonMoveResult
        {
            Kind = DungeonMoveKind.TriggerMessage,
            LevelId = state.LevelId,
            X = state.X,
            Y = state.Y,
            FeatureByte = feature,
            FeatureKind = kind,
            MessageId = messageId,
            Message = msg.Lines.FirstOrDefault(),
            Log = msg.Lines.Take(1).ToArray(),
        };
    }

    /// <summary>
    /// OV2 <c>0x298b</c> (cells <c>0x12..0x7f</c>) [C] (s41): <c>[0x64f6] := id</c> (write-only), then <c>0x2a95(id)</c>
    /// with <c>id = b − 0x11</c>. Unlike <see cref="ShowMessage"/> there is no last-message check and
    /// <c>[0x64f4]</c> is not written, so the text shows on every step onto the cell. The cell is never rewritten.
    /// An empty slot shows nothing (<c>0x2cfd</c>). A menu's choice would be discarded [~] (no ScriptLow cell in
    /// DNG1..10 points at a menu).
    /// </summary>
    private static DungeonMoveResult ShowDescription(DungeonState state, DungeonLevel level, byte feature,
        DungeonFeatureKind kind, int messageId)
    {
        if (!level.Messages.TryGetValue(messageId, out var msg))
            return Moved(state, feature, kind);
        return new DungeonMoveResult
        {
            Kind = DungeonMoveKind.TriggerMessage,
            LevelId = state.LevelId,
            X = state.X,
            Y = state.Y,
            FeatureByte = feature,
            FeatureKind = kind,
            MessageId = messageId,
            Message = msg.Lines.FirstOrDefault(),
            Log = msg.Lines,
            Windows = msg.Lines.Count > 0 && !msg.IsMenu ? [new LogWindow(0, msg.Lines.Count, LogWindowKind.Text)] : [],
        };
    }

    /// <summary>
    /// <c>0x29a9</c>: back to the previous cell (<c>[0x751e]/[0x7520]</c>) without running its event. Used by a refused
    /// trap, a jammed or unpicked lock, GO BACK, script TEST, and BEG/FLEE after a dungeon fight.
    /// </summary>
    public static void StepBack(DungeonState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        state.X = state.PrevX;
        state.Y = state.PrevY;
    }

    /// <summary>
    /// Trap <c>0x2f8e(a, x, y)</c> on cell (<paramref name="x"/>, <paramref name="y"/>) [C] (s39). The spot roll runs now.
    /// Spotted → a result with <see cref="DungeonMoveResult.SpotterIndex"/>; nothing else happens until
    /// <see cref="ResolveSpottedTrap"/>. Not spotted → the trap springs (<see cref="DungeonTraps.Spring"/>) and the cell
    /// becomes floor 7. Script op 16 passes the event cell, so that cell becomes floor too.
    /// </summary>
    internal static DungeonMoveResult ResolveTrapAt(DungeonState state, DungeonLevel level, int a, int x, int y,
        Party? party, IRandomSource? rng, byte feature, DungeonFeatureKind kind, TerrainRoll roll = TerrainRoll.Percent)
    {
        if (party is null || rng is null)
            return new DungeonMoveResult
            {
                Kind = DungeonMoveKind.TriggerTrap, LevelId = state.LevelId, X = state.X, Y = state.Y,
                FeatureByte = feature, FeatureKind = kind,
            };

        if (DungeonTraps.FindSpotter(party, rng, roll) is int spotter)
        {
            return new DungeonMoveResult
            {
                Kind = DungeonMoveKind.TriggerTrap,
                LevelId = state.LevelId,
                X = state.X,
                Y = state.Y,
                FeatureByte = feature,
                FeatureKind = kind,
                SpotterIndex = spotter,
                Message = GameText.F("core.dungeon.x_spots_a_trap", party.Members[spotter].Name), // manual p.10 (D-052); DOS adds ". DISARM IT ?"
            };
        }
        return Spring(state, level, a, x, y, party, rng, feature, kind, new List<string>(), roll);
    }

    /// <summary>
    /// Answer to "%s SPOTS A TRAP / D)ISARM OR L)EAVE" for the trap of kind <paramref name="a"/> on (<paramref name="x"/>,
    /// <paramref name="y"/>). <paramref name="disarmerIndex"/> null = LEAVE, or "WHO WILL DISARM ?" left with no choice
    /// (−1): step back, the trap stays. Otherwise the chosen member (status OK only, <see cref="DungeonTraps.CanDisarm"/>)
    /// rolls: success → "SUCCEEDS", the cell becomes 7; failure → "FAILS" and the trap springs.
    /// </summary>
    public static DungeonMoveResult ResolveSpottedTrap(DungeonState state, DungeonLevel level, int a, int x, int y,
        int? disarmerIndex, Party party, IRandomSource rng, TerrainRoll roll = TerrainRoll.Percent)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(level);
        ArgumentNullException.ThrowIfNull(party);
        ArgumentNullException.ThrowIfNull(rng);
        var feature = level.InBounds(x, y) ? level.Get(x, y) : (byte)a;
        var kind = DungeonLevel.Classify(feature);
        if (disarmerIndex is not int i)
        {
            StepBack(state);
            return new DungeonMoveResult
            {
                Kind = DungeonMoveKind.Moved, LevelId = state.LevelId, X = state.X, Y = state.Y,
                FeatureByte = feature, FeatureKind = kind, SteppedBack = true,
            };
        }
        if ((uint)i >= (uint)party.Members.Count || !DungeonTraps.CanDisarm(party.Members[i]))
            throw new ArgumentOutOfRangeException(nameof(disarmerIndex), "Only an OK member can disarm.");

        var disarmer = party.Members[i];
        var log = new List<string>();
        if (DungeonTraps.DisarmSucceeds(disarmer, rng, roll))
        {
            log.Add(GameText.F("core.dungeon.x_succeeds", disarmer.Name));
            if (level.InBounds(x, y))
                level.Set(x, y, DungeonTraps.SpentCell);
            return new DungeonMoveResult
            {
                Kind = DungeonMoveKind.TriggerTrap, LevelId = state.LevelId, X = state.X, Y = state.Y,
                FeatureByte = feature, FeatureKind = kind, Message = log[0], Log = log,
            };
        }
        log.Add(GameText.F("core.dungeon.x_fails", disarmer.Name)); // sound 0x1d, then the spring path 0x306f
        return Spring(state, level, a, x, y, party, rng, feature, kind, log, roll, disarmFailed: true);
    }

    private static DungeonMoveResult Spring(DungeonState state, DungeonLevel level, int a, int x, int y, Party party,
        IRandomSource rng, byte feature, DungeonFeatureKind kind, List<string> log, TerrainRoll roll, bool disarmFailed = false)
    {
        // 0x306f: MESS message a + 4 first (0x2a95), then the "IN THE TRAP:" report.
        var windows = new List<LogWindow>();
        if (level.Messages.TryGetValue(a + 4, out var msg))
        {
            if (msg.Lines.Count > 0 && !msg.IsMenu)
                windows.Add(new LogWindow(log.Count, msg.Lines.Count, LogWindowKind.Text));
            log.AddRange(msg.Lines);
        }
        var report = log.Count;
        DungeonTraps.Spring(party, a, rng, log, out var damage, roll);
        windows.Add(new LogWindow(report, log.Count - report, LogWindowKind.Report));
        if (level.InBounds(x, y))
            level.Set(x, y, DungeonTraps.SpentCell);
        return new DungeonMoveResult
        {
            Kind = DungeonMoveKind.TriggerTrap,
            LevelId = state.LevelId,
            X = state.X,
            Y = state.Y,
            FeatureByte = feature,
            FeatureKind = kind,
            MessageId = a + 4,
            Message = log.FirstOrDefault(),
            Damage = damage,
            Log = log,
            Windows = windows,
            DisarmFailed = disarmFailed,
        };
    }

    /// <summary>
    /// Lock / ambush cell <c>0x2e03(b, x, y)</c> [C] (s39). Open door (kind 1): nothing. Kind 14: pick roll — picked →
    /// the cell opens (<c>0xb9 + si</c>) and the party stays; not picked → "NO ONE CAN PICK THE LOCK", the cell jams
    /// (kind 15) and the party steps back. Kind 15: same message and step back. Kinds 2..13: listen roll; heard →
    /// <see cref="DungeonMoveResult.ListenerIndex"/> and "GO BACK ?" waits (<see cref="ResolveHeardMonsters"/>); not
    /// heard → the ambush at once with <see cref="Surprise.LockAmbush"/>.
    /// </summary>
    private static DungeonMoveResult ResolveLock(DungeonState state, DungeonLevel level, byte feature,
        DungeonFeatureKind kind, Party? party, IRandomSource? rng, TerrainRoll roll)
    {
        var door = DungeonLocks.Classify(feature);
        if (door == DungeonDoorKind.Open)
            return Moved(state, feature, kind);
        if (party is null || rng is null)
            return new DungeonMoveResult
            {
                Kind = DungeonMoveKind.TriggerLock, LevelId = state.LevelId, X = state.X, Y = state.Y,
                FeatureByte = feature, FeatureKind = kind,
            };

        DungeonMoveResult Jam()
        {
            level.Set(state.X, state.Y, DungeonLocks.JammedCell(feature));
            StepBack(state);
            return new DungeonMoveResult
            {
                Kind = DungeonMoveKind.TriggerLock, LevelId = state.LevelId, X = state.X, Y = state.Y,
                FeatureByte = feature, FeatureKind = kind, Message = GameText.S("core.dungeon.no_one_can_pick_the_lock"), SteppedBack = true,
            };
        }

        switch (door)
        {
            case DungeonDoorKind.Locked:
                if (DungeonLocks.FindPicker(party, rng, roll) is not int picker)
                    return Jam();
                level.Set(state.X, state.Y, DungeonLocks.OpenedCell(feature));
                return new DungeonMoveResult
                {
                    Kind = DungeonMoveKind.TriggerLock, LevelId = state.LevelId, X = state.X, Y = state.Y,
                    FeatureByte = feature, FeatureKind = kind, Message = GameText.F("core.dungeon.x_picks_the_lock", party.Members[picker].Name),
                };

            case DungeonDoorKind.Jammed:
                return Jam();

            default:
                if (DungeonLocks.FindListener(party, rng, roll) is int listener)
                {
                    return new DungeonMoveResult
                    {
                        Kind = DungeonMoveKind.TriggerLock, LevelId = state.LevelId, X = state.X, Y = state.Y,
                        FeatureByte = feature, FeatureKind = kind, ListenerIndex = listener,
                        Message = GameText.F("core.dungeon.x_hears_monsters_go_back", party.Members[listener].Name),
                    };
                }
                return Ambush(state, level, feature, kind, rng, someoneHeard: false, roll);
        }
    }

    /// <summary>
    /// Answer to "%s HEARS MONSTERS. GO BACK ?" on the lock cell the party stands on: YES → step back, no fight; NO →
    /// the ambush (monster <c>12·T + di − 2</c>, <see cref="Surprise.LockAmbush"/> with someone heard).
    /// </summary>
    public static DungeonMoveResult ResolveHeardMonsters(DungeonState state, DungeonLevel level, bool goBack,
        IRandomSource rng, TerrainRoll roll = TerrainRoll.Percent)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(level);
        ArgumentNullException.ThrowIfNull(rng);
        var feature = level.Get(state.X, state.Y);
        var kind = DungeonLevel.Classify(feature);
        if (goBack)
        {
            StepBack(state);
            return new DungeonMoveResult
            {
                Kind = DungeonMoveKind.Moved, LevelId = state.LevelId, X = state.X, Y = state.Y,
                FeatureByte = feature, FeatureKind = kind, SteppedBack = true,
            };
        }
        return Ambush(state, level, feature, kind, rng, someoneHeard: true, roll);
    }

    private static DungeonMoveResult Ambush(DungeonState state, DungeonLevel level, byte feature,
        DungeonFeatureKind kind, IRandomSource rng, bool someoneHeard, TerrainRoll roll) => new()
    {
        Kind = DungeonMoveKind.TriggerLock,
        LevelId = state.LevelId,
        X = state.X,
        Y = state.Y,
        FeatureByte = feature,
        FeatureKind = kind,
        MonsterId = DungeonLocks.AmbushMonster(level, feature),
        Surprise = Surprise.LockAmbush(rng, someoneHeard, roll),
    };

    private static DungeonMoveResult Moved(DungeonState state, byte feature, DungeonFeatureKind kind) =>
        new()
        {
            Kind = DungeonMoveKind.Moved,
            LevelId = state.LevelId,
            X = state.X,
            Y = state.Y,
            FeatureByte = feature,
            FeatureKind = kind,
        };

    private static (int Dx, int Dy) FacingDelta(DungeonFacing facing) => facing switch
    {
        DungeonFacing.North => (0, -1),
        DungeonFacing.East => (1, 0),
        DungeonFacing.South => (0, 1),
        DungeonFacing.West => (-1, 0),
        _ => (0, 0),
    };
}
