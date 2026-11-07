namespace OpenPhantasie.Core;

/// <summary>How an event script ended (the OV2 state it returns).</summary>
public enum DungeonScriptEnd
{
    /// <summary>State 3: back to the dungeon step.</summary>
    Done,

    /// <summary>State 11: build a fight (<see cref="DungeonScriptResult.MonsterId"/>).</summary>
    Fight,

    /// <summary>State 6: no non-DEAD member is left (damage op or trap).</summary>
    PartyDefeated,

    /// <summary>Op 16 ran a trap (<c>0x2f8e</c>) and the script returned its result.</summary>
    Trap,

    /// <summary>
    /// Op 6 (<c>0x421d(a, 0)</c>): "YOU FIND %s. KEEP IT?" is asked at this op (<see cref="DungeonScriptResult.ItemId"/>).
    /// DOS waits for the answer, then runs the later ops (s58 DOSBox), so the script stops here and
    /// <see cref="DungeonScript.Resume"/> carries on from <see cref="DungeonScriptResult.Resume"/>.
    /// </summary>
    OfferItem,

    /// <summary>
    /// Phantasie II op 22 (GOTO EVENT) reached an event with a menu: <see cref="DungeonScriptResult.EventFeature"/> /
    /// <see cref="DungeonScriptResult.MessageId"/> wait for the player's answer.
    /// </summary>
    EventMenu,

    /// <summary>Phantasie II op 23: state 7, the ending (Filmon).</summary>
    Ending,
}

/// <summary>
/// Where a script stopped at op 6 (<see cref="DungeonScriptEnd.OfferItem"/>): the interpreter's locals. Plain settable
/// properties so a save taken at the KEEP IT? prompt can hold it (<c>DungeonSnapshot.ScriptResume</c>).
/// </summary>
public sealed class DungeonScriptResume
{
    public byte Feature { get; set; }

    /// <summary>Buffer offset of the next op; <see cref="OpsDone"/> = ops run so far (≥ <see cref="DungeonScript.MaxOps"/>: none left).</summary>
    public int NextOp { get; set; }
    public int OpsDone { get; set; }

    /// <summary><c>[0x64e6]</c> for the end check (op 5/17 with <c>a = 0</c> before the give).</summary>
    public int PendingMonster { get; set; }
    public bool SurpriseFirstRound { get; set; } = SurpriseFlags.Normal.FirstRound;
    public int SurpriseMonsterHold { get; set; } = SurpriseFlags.Normal.MonsterHold;

    /// <summary>The event cell (op 16 traps it), the event message, the menu choice and the script id.</summary>
    public int CellX { get; set; }
    public int CellY { get; set; }
    public int MessageId { get; set; }
    public int Choice { get; set; }
    public int ScriptId { get; set; }

    /// <summary>
    /// III: the program that ran this one as a sub-square (op 22) and carries on once it ends, or null. Chains as deep as
    /// the sub-squares go.
    /// </summary>
    public DungeonScriptResume? Caller { get; set; }

    /// <summary>
    /// III: the program stopped at an event text that waits for a key (<see cref="GameSession.DungeonPage"/>); the
    /// acknowledgement runs on from <see cref="NextOp"/> (none when ≤ 0), then <see cref="Caller"/>.
    /// </summary>
    public bool TextWait { get; set; }
}

/// <summary>Result of one scripted-event cell (OV2 <c>0x24b2</c>).</summary>
public sealed class DungeonScriptResult
{
    public DungeonScriptEnd End { get; init; }

    /// <summary>First monster (0-based, <c>[0x64e6] − 1</c>) when <see cref="End"/> is <see cref="DungeonScriptEnd.Fight"/>.</summary>
    public int? MonsterId { get; init; }

    /// <summary>Surprise flags set by op 5 / op 17 (<c>[0x649e]</c>, <c>[0x64a0]</c>).</summary>
    public SurpriseFlags Surprise { get; init; } = SurpriseFlags.Normal;

    /// <summary>The event record was already spent (<c>r0 == 0xff</c>): nothing happened.</summary>
    public bool WasSpent { get; init; }

    /// <summary>Event message id (<c>r0 &amp; 0x7f</c>) and the 1-based menu choice the host returned.</summary>
    public int MessageId { get; init; }
    public int Choice { get; init; }

    /// <summary>Script id run (<c>r[choice + 1]</c>).</summary>
    public int ScriptId { get; init; }

    public DungeonMoveResult? TrapResult { get; init; }

    /// <summary>Op 16's trap kind <c>a</c> (the <c>0x2f8e</c> argument) when <see cref="End"/> is <see cref="DungeonScriptEnd.Trap"/>.</summary>
    public int TrapKind { get; init; }

    /// <summary>The event cell the script runs for (op 16 traps it; a teleport may have moved the party since).</summary>
    public int EventX { get; init; }
    public int EventY { get; init; }

    /// <summary>Op 6's item when <see cref="End"/> is <see cref="DungeonScriptEnd.OfferItem"/>, with the point to resume from.</summary>
    public int? ItemId { get; init; }
    public DungeonScriptResume? Resume { get; init; }

    /// <summary>The event (feature byte) a II GOTO EVENT stopped at (<see cref="DungeonScriptEnd.EventMenu"/>).</summary>
    public byte EventFeature { get; init; }

    public IReadOnlyList<string> Log { get; init; } = Array.Empty<string>();

    /// <summary>
    /// s141: the moves ops 12 / 13 made, in order: each after the first <see cref="ScriptMove.AfterLines"/> lines of
    /// <see cref="Log"/>, from the cell the party stood on (I Amiga h41 <c>+0x1964</c>: sound 16 and "THE PARTY IS MOVED"
    /// come first, the party moves after them, so the boxes before a move show it on the event cell).
    /// </summary>
    public IReadOnlyList<ScriptMove> Moves { get; init; } = Array.Empty<ScriptMove>();

    /// <summary>s144: the damage reports of ops 1–4 in <see cref="Log"/> ("REPORT:" and the member lines, <see cref="LogWindowKind.Report"/>).</summary>
    public IReadOnlyList<LogWindow> Windows { get; init; } = Array.Empty<LogWindow>();
}

/// <summary>s141: a script's move (op 12 / 13), made after <paramref name="AfterLines"/> lines of its log, from (FromX, FromY).</summary>
public readonly record struct ScriptMove(int AfterLines, int FromX, int FromY);

/// <summary>What the event script needs from the session (messages/menus, the found-gold purse). Op 6's KEEP IT? is not a
/// host call: the script stops there (<see cref="DungeonScriptEnd.OfferItem"/>).</summary>
public interface IDungeonScriptHost
{
    /// <summary>
    /// OV2 <c>0x2a95</c>: show MESS message <paramref name="messageId"/> of this level. Return 1 for plain text (or an empty
    /// slot), else the 1-based menu choice.
    /// </summary>
    int ShowMessage(int messageId);

    /// <summary>Found gold <c>[0x63f6]</c> (Core <see cref="LootSack.Gold"/>).</summary>
    long FoundGold { get; set; }

    /// <summary>s139: an op's moment (<see cref="FieldCues"/>), sounded by the platform pack or not; a host without a
    /// session drops it.</summary>
    void Cue(string moment) { }
}

/// <summary>
/// DNG event scripts — OV2 <c>0x24b2</c> (endgame.md §2.1 [C]). The records and scripts live in the level buffer, so the
/// interpreter reads and rewrites the live buffer (<see cref="DungeonState.Level"/>): spent records, one-shot ops and
/// SET-CELL all persist exactly as far as the buffer does. The quest itself is data (DNG1..10 bytes in dungeons.json).
/// </summary>
public static class DungeonScript
{
    /// <summary>Event record <c>k = b − 0xdc</c> (1..35) at buffer <c>0x4f6 + 5k</c>.</summary>
    public const int EventRecordBase = 0x4f6;

    /// <summary>Script <c>s</c> starts at buffer <c>0x5aa + 2s</c>.</summary>
    public const int ScriptBase = 0x5aa;

    public const byte FirstEventFeature = 0xdd;
    public const byte SpentRecord = 0xff;

    /// <summary>At most 6 ops per script (<c>[bp−0x10] &lt; 6</c>).</summary>
    public const int MaxOps = 6;

    /// <summary>DNG9: op 6 takes the jump table there instead of the early special case (same give).</summary>
    public const int FortressLevel = 9;

    public static int EventRecordOffset(byte feature) => EventRecordBase + 5 * (feature - 0xdc);

    /// <summary>True when the cell's event record is spent (<c>r0 == 0xff</c> → return 3, nothing happens).</summary>
    public static bool IsSpent(DungeonLevel level, byte feature) =>
        feature >= FirstEventFeature && level.GetBufferByte(EventRecordOffset(feature)) == SpentRecord;

    /// <summary>Run the event of the cell the party stands on (feature byte <paramref name="feature"/> ≥ 0xdd).</summary>
    public static DungeonScriptResult Run(
        DungeonState state,
        DungeonLevel level,
        byte feature,
        Party party,
        IDungeonScriptHost host,
        IRandomSource rng)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(level);
        ArgumentNullException.ThrowIfNull(party);
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(rng);
        if (feature < FirstEventFeature)
            throw new ArgumentOutOfRangeException(nameof(feature), "Scripted events are feature bytes 0xdd..0xff.");

        var rec = EventRecordOffset(feature);
        var r0 = level.GetBufferByte(rec);
        if (r0 == SpentRecord)
            return new DungeonScriptResult { End = DungeonScriptEnd.Done, WasSpent = true, Log = new List<string>() };

        var messageId = r0 & 0x7f;
        var choice = host.ShowMessage(messageId);  // 0x2a95: 1, or the 1-based menu choice
        if (r0 > 0x80)
            level.SetBufferByte(rec, SpentRecord); // one-shot, spent whatever the choice (0x2506)

        // The script id is r[choice + 1]; a 4-option menu reads the next record's r0 (endgame.md §2.1).
        var scriptId = level.GetBufferByte(rec + choice + 1);
        return Execute(state, level, party, host, rng, new DungeonScriptResume
        {
            Feature = feature,
            NextOp = ScriptBase + 2 * scriptId,
            CellX = state.X, // 0x24b2(b, x, y): op 16 traps this cell
            CellY = state.Y,
            MessageId = messageId,
            Choice = choice,
            ScriptId = scriptId,
        }); // [0x64e6] := 0, surprise unset
    }

    /// <summary>
    /// Carry on after op 6's "KEEP IT?" was answered: the ops after the give run, then the end check (<c>0x293d</c>).
    /// </summary>
    public static DungeonScriptResult Resume(
        DungeonState state,
        DungeonLevel level,
        Party party,
        IDungeonScriptHost host,
        IRandomSource rng,
        DungeonScriptResume resume)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(level);
        ArgumentNullException.ThrowIfNull(party);
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(rng);
        ArgumentNullException.ThrowIfNull(resume);
        return Execute(state, level, party, host, rng, resume);
    }

    /// <summary>The op loop (<c>0x252c..0x294e</c>) from <paramref name="at"/>, which it does not modify.</summary>
    private static DungeonScriptResult Execute(
        DungeonState state,
        DungeonLevel level,
        Party party,
        IDungeonScriptHost host,
        IRandomSource rng,
        DungeonScriptResume at)
    {
        var log = new List<string>();
        var moves = new List<ScriptMove>();
        var windows = new List<LogWindow>();
        var (cellX, cellY) = (at.CellX, at.CellY);
        var (messageId, choice, scriptId) = (at.MessageId, at.Choice, at.ScriptId);
        var pendingMonster = at.PendingMonster;
        var trapKind = 0;
        var surprise = new SurpriseFlags(at.SurpriseFirstRound, at.SurpriseMonsterHold);
        var p = at.NextOp;

        DungeonScriptResult Finish(DungeonScriptEnd end, int? monster = null, DungeonMoveResult? trap = null,
            int? item = null, DungeonScriptResume? resume = null) => new()
        {
            End = end,
            MonsterId = monster,
            Surprise = surprise,
            MessageId = messageId,
            Choice = choice,
            ScriptId = scriptId,
            TrapResult = trap,
            TrapKind = trap is null ? 0 : trapKind,
            EventX = cellX,
            EventY = cellY,
            ItemId = item,
            Resume = resume,
            Log = log,
            Moves = moves,
            Windows = windows,
        };

        for (var count = at.OpsDone; count < MaxOps; count++)
        {
            var opByte = level.GetBufferByte(p);
            int a = level.GetBufferByte(p + 1), b = level.GetBufferByte(p + 2), c = level.GetBufferByte(p + 3);
            var cont = opByte > 0x7f;
            var op = opByte % 128;
            if (op > 0x3f)
            {
                // One-shot op (0x2571): the byte becomes 0x80 (no-op, continue) or 0 (no-op, end).
                level.SetBufferByte(p, cont ? (byte)0x80 : (byte)0);
                op -= 0x40;
            }

            switch (op)
            {
                case 1: case 2: case 3: case 4:
                    if (op == 1) log.Add(GameText.S("core.script.smoke_fills_the_air"));
                    if (op == 3) log.Add(GameText.S("core.script.a_spell_is_cast"));
                    var report = log.Count;
                    var wiped = DamageParty(party, a, rng, log);
                    windows.Add(new LogWindow(report, log.Count - report, LogWindowKind.Report));
                    if (wiped)
                        return Finish(DungeonScriptEnd.PartyDefeated);
                    break;

                case 5:
                    // 0x26f4: [0x64e6] := a + 1; a fight only if > 1, then return 11 at once.
                    pendingMonster = a + 1;
                    if (pendingMonster > 1)
                    {
                        surprise = Surprise.ScriptFight(rng);
                        return Finish(DungeonScriptEnd.Fight, a);
                    }
                    break; // [~] a = 0 never occurs in DNG1..10; the end check below then fights monster 0

                case 6:
                    // 0x421d (DNG9 takes table entry 0x2725, the same give): the modal KEEP IT? is asked here and the
                    // script goes on after the answer (s58). Stop with the state the loop would carry into the next op.
                    return Finish(DungeonScriptEnd.OfferItem, item: a, resume: new DungeonScriptResume
                    {
                        Feature = at.Feature,
                        NextOp = p + 2,
                        OpsDone = cont ? count + 1 : MaxOps, // no continue bit: only the end check is left
                        PendingMonster = pendingMonster,
                        SurpriseFirstRound = surprise.FirstRound,
                        SurpriseMonsterHold = surprise.MonsterHold,
                        CellX = cellX,
                        CellY = cellY,
                        MessageId = messageId,
                        Choice = choice,
                        ScriptId = scriptId,
                    });

                case 7: case 8:
                    if (op == 7) log.Add(GameText.S("core.script.party_gets_some_gold"));
                    host.FoundGold = Math.Clamp(host.FoundGold + a * 256L, 0, 0xffff) | 0xff;
                    break;

                case 9: case 10:
                    if (op == 9) log.Add(GameText.S("core.script.some_gold_is_taken"));
                    var take = a * 250L;
                    host.FoundGold = host.FoundGold < take ? 0 : host.FoundGold - take;
                    break;

                case 11:
                    host.ShowMessage(a);
                    break;

                case 12: case 13:
                    if (op == 12) log.Add(GameText.S("core.script.the_party_is_moved"));
                    moves.Add(new ScriptMove(log.Count, state.X, state.Y));
                    // 0x27b0: current and previous cell := (a, b); the destination's event is not run. Then the message
                    // line is blanked and the last message shown is forgotten (0x27f4/0x27fa: [0x64f2], [0x64f4] := 0).
                    state.X = state.PrevX = a;
                    state.Y = state.PrevY = b;
                    state.LastMessageId = 0;
                    p += 2;
                    break;

                case 14: case 15:
                    if (op == 14) log.Add(GameText.S("core.script.you_hear_a_noise"));
                    p += 2; // 0x2807/0x2817: no [0x64f4] reset here (that is the teleport's, s49)
                    level.SetBufferByte(b * DungeonLevel.Width + a, (byte)c); // 0x2a02: buf[y·33 + x]
                    break;

                case 16:
                {
                    // 0x2f8e(a, x, y) on the event cell; the script returns the trap's state (s39 [C]). A spotted trap
                    // waits for "D)ISARM OR L)EAVE" (TrapResult.SpotterIndex); a sprung or disarmed one turns the cell to floor 7.
                    trapKind = a;
                    var trap = DungeonTraversal.ResolveTrapAt(state, level, a, cellX, cellY, party, rng, at.Feature,
                        DungeonFeatureKind.ScriptedEvent);
                    return Finish(party.Members.Any(m => m.Status != CharacterStatus.Dead)
                        ? DungeonScriptEnd.Trap
                        : DungeonScriptEnd.PartyDefeated, trap: trap);
                }

                case 17:
                    log.Add(GameText.S("core.script.monsters_appear"));
                    surprise = Surprise.MonstersAppear;
                    pendingMonster = a + 1;
                    if (pendingMonster > 1)
                        return Finish(DungeonScriptEnd.Fight, a);
                    break;

                case 18:
                    p += 2;
                    foreach (var m in party.Members)
                    {
                        if (m.Status == CharacterStatus.Dead)
                            continue;
                        if (GetField(m, a) < b)
                            SetField(m, a, b);
                    }
                    break;

                case 19:
                    p += 2;
                    if (!party.Members.Any(m => GetField(m, a) >= b)) // DEAD members count here
                    {
                        state.X = state.PrevX; // 0x29a9: back to the previous cell, no event
                        state.Y = state.PrevY;
                        host.ShowMessage(c);
                        cont = false;
                    }
                    break;

                default:
                    break; // 0 and ≥ 20 (0x291a: jae → tail) are no-ops
            }

            p += 2;
            if (!cont)
                break;
        }

        return pendingMonster > 0
            ? Finish(DungeonScriptEnd.Fight, pendingMonster - 1)
            : Finish(DungeonScriptEnd.Done);
    }

    /// <summary>
    /// Ops 1–4 (<c>0x25b7</c>): every non-DEAD member takes <c>rand0_99·a / (10·LUCK @10)</c> through <c>0x124a</c> (flag 1),
    /// which floors the hit at 1 even when the report says the member is unhurt [C]. Returns true when nobody is left.
    /// </summary>
    internal static bool DamageParty(Party party, int a, IRandomSource rng, List<string> log)
    {
        log.Add(GameText.S("core.script.report"));
        foreach (var m in party.Members)
        {
            if (m.Status == CharacterStatus.Dead)
                continue;
            var divisor = 10 * m.Stats.Luck;
            // [?] LUCK 0 would divide by zero in DOS (idiv fault); creation never yields LUCK 0.
            var dmg = divisor == 0 ? 0 : rng.Next(0, 100) * a / divisor;
            CombatResolver.ApplyPartyDamage(party, m, Math.Max(1, dmg), DamageSource.DungeonScript, rng, log);
            if (m.Status == CharacterStatus.Dead)
                log.Add(GameText.F("core.script.report_x_dies", m.Name));
            else if (dmg > 0)
                log.Add(GameText.F("core.script.report_x_takes_x_damage", m.Name, dmg));
            else
                log.Add(m.Name); // printed with no text, yet 1 HP was lost (0x124a floor)
        }
        return party.Members.All(m => m.Status == CharacterStatus.Dead);
    }

    /// <summary>
    /// OV2 field-id map <c>0x1ba5</c> (combat-resolution.md, full table [C]) — the ids a script can name. Ids the map does
    /// not cover fall through to a garbage pointer in DOS; here they read 0 and ignore writes <c>[?]</c>.
    /// </summary>
    public static int GetField(Character ch, int id) => id switch
    {
        1 => ch.Stats.Str,
        2 => ch.Stats.Int,
        3 => ch.Stats.Dex,
        4 => ch.Stats.Con,
        5 => ch.Stats.Cha,
        6 => ch.Stats.Luck,
        7 => ch.Word12,
        8 => ch.MpCeiling,
        9 => ch.MagicMax,
        10 => ch.MagicCur,
        11 => ch.HpMax,
        12 => ch.HpCur,
        13 or 18 => (int)ch.Status,
        >= 20 and <= 39 when (id & 1) == 0 => SkillWord(ch, (id - 20) / 2),
        >= 21 and <= 39 => ch.SkillBonuses.ElementAtOrDefault((id - 21) / 2),
        42 => ch.Score,
        48 => ch.WornShield,
        49 => ch.WornArmor,
        50 => ch.WornWeapon,
        >= 152 and <= 160 => ch.InventoryItemIds.ElementAtOrDefault(id - 152),
        161 => (int)ch.Race,
        _ when ch.Phantasie1.TryGetScriptField(id, out var own) => own, // I's own words: 19, runes 43..47, pool product 55
        _ => 0,
    };

    public static void SetField(Character ch, int id, int value)
    {
        switch (id)
        {
            case 42: ch.Score = value; break;
            case 4: ch.Stats = ch.Stats with { Con = value }; break;
            case 6: ch.Stats = ch.Stats with { Luck = value }; break;
            case 11: ch.HpMax = value; break;
            case 12: ch.HpCur = value; break;
            default:
                // I's own words (runes 43..47, pool product 55) live in its character extension.
                // [?] SET-MIN on any other id never occurs in DNG1..10 (s36 scan: ids 43..47 only).
                ch.Phantasie1.TrySetScriptField(id, value);
                break;
        }
    }

    /// <summary>Skill words <c>@30..@48</c> by record slot (2 = find … 7 = pick lock @44, 9 = swim @48).</summary>
    private static int SkillWord(Character ch, int slot) => slot switch
    {
        0 => ch.Skills.Attack,
        1 => ch.Skills.Parry,
        2 => ch.Skills.FindItem,
        3 => ch.Skills.SpotTrap,
        4 => ch.Skills.DisarmTrap,
        5 => ch.Skills.Listen,
        7 => ch.Skills.PickLock,
        9 => ch.Skills.Swim,
        _ => 0, // @42 / @46 unused words
    };
}
