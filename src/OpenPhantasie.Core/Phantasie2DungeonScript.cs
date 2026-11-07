namespace OpenPhantasie.Core;

/// <summary>
/// Phantasie II's dungeon event interpreter (ST <c>dungeon_event 0x11ab6</c>, <c>endgame.md</c> §5 [C], re-read s81 from the
/// disassembly). I's record and script layout and ops 1–17 (<see cref="DungeonScript"/>), with II's changes:
/// <list type="bullet">
/// <item>17 ops per run (I: 6), counted again from 0 on GOTO EVENT;</item>
/// <item>op 18 SET field a := b for every non-DEAD member (I: SET-MIN), field 53 writes 0xff into the XP low byte;</item>
/// <item>op 19 TEST: when no member (any status) has field a ≥ b, push back and jump to script c (no message); the run
/// goes on only with the continue bit;</item>
/// <item>op 20 JUMP to script a, op 21 RANDJUMP (4 bytes: r &lt; a → script b), op 22 GOTO EVENT a (restart with event
/// record a: spent check, its message and menu), op 23 ENDING (state 7). JUMP / RANDJUMP keep the continue rule;</item>
/// <item>a one-shot op becomes <c>0x80</c>/<c>0</c>, or <c>0xc0</c>/<c>0</c> for the 4-byte ops (12–15, 18, 19, 21 and a
/// spent <c>0xc0</c>), and <c>0xc0</c> skips 4 bytes (<c>0x12028</c>), so I's stale-byte quirk is gone;</item>
/// <item>II's field-id map <c>0x12078</c> (<see cref="GetField"/>): 8 = quest SCORE @282, 9..13 = @14..@22, 18 = status.</item>
/// </list>
/// A GOTO EVENT whose event shows a menu stops the run with <see cref="DungeonScriptEnd.EventMenu"/> so the player can
/// answer it (the ST's modal menu); a plain-text event runs on at once.
/// </summary>
public static class Phantasie2DungeonScript
{
    /// <summary>At most 17 ops per run (<c>0x1203c</c>).</summary>
    public const int MaxOps = 17;

    /// <summary>A spent 4-byte one-shot op (<c>0x11bf4</c>): a continue no-op that skips 4 bytes.</summary>
    public const byte SpentFourByteOp = 0xc0;

    /// <summary>Most GOTO EVENT restarts one step follows before giving up (Core guard; the data never loops).</summary>
    public const int MaxEventRestarts = 35;

    /// <summary>The 4-byte ops (<c>0x11bc2..0x11bea</c>, plus the handlers that add 2 themselves).</summary>
    public static bool IsFourByte(int op) => op is 12 or 13 or 14 or 15 or 18 or 19 or 21;

    /// <summary>Run the event of the cell the party stands on (feature ≥ 0xdd), with the 1-based menu choice from the host.</summary>
    public static DungeonScriptResult Run(DungeonState state, DungeonLevel level, byte feature, Party party,
        IDungeonScriptHost host, IRandomSource rng, EncounterRules? rules = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(level);
        ArgumentNullException.ThrowIfNull(party);
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(rng);
        if (feature < DungeonScript.FirstEventFeature)
            throw new ArgumentOutOfRangeException(nameof(feature), "Scripted events are feature bytes 0xdd..0xff.");
        var log = new List<string>();
        return Start(state, level, feature, party, host, rng, rules ?? EncounterRules.PhantasieI, log, firstRun: true, restarts: 0);
    }

    /// <summary>Carry on after op 6's "KEEP IT?" was answered.</summary>
    public static DungeonScriptResult Resume(DungeonState state, DungeonLevel level, Party party, IDungeonScriptHost host,
        IRandomSource rng, DungeonScriptResume resume, EncounterRules? rules = null)
    {
        ArgumentNullException.ThrowIfNull(resume);
        return Execute(state, level, party, host, rng, rules ?? EncounterRules.PhantasieI, resume, new List<string>(), 0);
    }

    private static DungeonScriptResult Start(DungeonState state, DungeonLevel level, byte feature, Party party,
        IDungeonScriptHost host, IRandomSource rng, EncounterRules rules, List<string> log, bool firstRun, int restarts)
    {
        var rec = DungeonScript.EventRecordOffset(feature);
        var r0 = level.GetBufferByte(rec);
        if (r0 == DungeonScript.SpentRecord)
            return new DungeonScriptResult { End = DungeonScriptEnd.Done, WasSpent = firstRun, Log = log };

        var messageId = r0 & 0x7f;
        // GOTO EVENT onto a menu: hand the menu to the player (the campaign re-enters through Run with the answer).
        if (!firstRun && level.Messages.TryGetValue(messageId, out var msg) && msg.IsMenu)
            return new DungeonScriptResult
            {
                End = DungeonScriptEnd.EventMenu, EventFeature = feature, MessageId = messageId, Log = log,
                EventX = state.X, EventY = state.Y,
            };

        var choice = host.ShowMessage(messageId);
        if (r0 > 0x80)
            level.SetBufferByte(rec, DungeonScript.SpentRecord); // 0x11b1c: one-shot, spent whatever the choice
        var scriptId = level.GetBufferByte(rec + choice + 1);
        return Execute(state, level, party, host, rng, rules, new DungeonScriptResume
        {
            Feature = feature,
            NextOp = DungeonScript.ScriptBase + 2 * scriptId,
            CellX = state.X,
            CellY = state.Y,
            MessageId = messageId,
            Choice = choice,
            ScriptId = scriptId,
        }, log, restarts); // [0x2832c] := 0
    }

    private static DungeonScriptResult Execute(DungeonState state, DungeonLevel level, Party party, IDungeonScriptHost host,
        IRandomSource rng, EncounterRules rules, DungeonScriptResume at, List<string> log, int restarts)
    {
        var (cellX, cellY) = (at.CellX, at.CellY);
        var (messageId, choice, scriptId) = (at.MessageId, at.Choice, at.ScriptId);
        var pendingMonster = at.PendingMonster;
        var surprise = new SurpriseFlags(at.SurpriseFirstRound, at.SurpriseMonsterHold);
        var trapKind = 0;
        var p = at.NextOp;
        int Script(int s) => DungeonScript.ScriptBase + 2 * s;

        DungeonScriptResult Finish(DungeonScriptEnd end, int? monster = null, DungeonMoveResult? trap = null,
            int? item = null, DungeonScriptResume? resume = null) => new()
        {
            End = end, MonsterId = monster, Surprise = surprise, MessageId = messageId, Choice = choice,
            ScriptId = scriptId, TrapResult = trap, TrapKind = trap is null ? 0 : trapKind, EventX = cellX, EventY = cellY,
            ItemId = item, Resume = resume, Log = log,
        };

        for (var count = at.OpsDone; count < MaxOps; count++)
        {
            var opByte = level.GetBufferByte(p);
            int a = level.GetBufferByte(p + 1), b = level.GetBufferByte(p + 2), c = level.GetBufferByte(p + 3);
            var cont = opByte > 0x7f;
            var op = opByte % 128;
            var next = p + 2;                         // 0x12026; the 4-byte handlers add 2 more
            if (opByte == SpentFourByteOp)
                next += 2;                            // 0x12028
            if (op > 0x3f)
            {
                op -= 0x40;
                level.SetBufferByte(p, op == 0 || IsFourByte(op)
                    ? (cont ? SpentFourByteOp : (byte)0)
                    : (cont ? (byte)0x80 : (byte)0));
            }
            if (IsFourByte(op))
                next += 2;

            switch (op)
            {
                case 1: case 2: case 3: case 4:
                    if (op == 1) log.Add(GameText.S("core.script.smoke_fills_the_air"));
                    if (op == 3)
                    {
                        host.Cue(FieldCues.EventSpellCast); // 0x11d02: sound 28, then the box
                        log.Add(GameText.S("core.script.a_spell_is_cast"));
                    }
                    if (DungeonScript.DamageParty(party, a, rng, log))
                        return Finish(DungeonScriptEnd.PartyDefeated);
                    break;

                case 5:
                    pendingMonster = a + 1; // 0x11d1c: H := r <= 90, F := 1
                    if (pendingMonster > 1)
                    {
                        surprise = rules.Roll100(rng) > 90 ? SurpriseFlags.PartySurprised : SurpriseFlags.Normal;
                        return Finish(DungeonScriptEnd.Fight, a);
                    }
                    break;

                case 6:
                    return Finish(DungeonScriptEnd.OfferItem, item: a, resume: new DungeonScriptResume
                    {
                        Feature = at.Feature,
                        NextOp = next,
                        OpsDone = cont ? count + 1 : MaxOps,
                        PendingMonster = pendingMonster,
                        SurpriseFirstRound = surprise.FirstRound,
                        SurpriseMonsterHold = surprise.MonsterHold,
                        CellX = cellX, CellY = cellY, MessageId = messageId, Choice = choice, ScriptId = scriptId,
                    });

                case 7: case 8:
                    if (op == 7) log.Add(GameText.S("core.script.party_gets_some_gold"));
                    host.FoundGold = Math.Clamp(host.FoundGold + a * 256L, 0, 0xffff) | 0xff; // 0x11d98
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
                    if (op == 12)
                    {
                        host.Cue(FieldCues.EventPartyMoved); // 0x11dfc: sound 16 (op 13 silent)
                        log.Add(GameText.S("core.script.the_party_is_moved"));
                    }
                    state.X = state.PrevX = a;
                    state.Y = state.PrevY = b;
                    state.LastMessageId = 0;
                    break;

                case 14: case 15:
                    if (op == 14)
                    {
                        host.Cue(FieldCues.EventNoise); // 0x11e88: sound 26 (op 15 silent)
                        log.Add(GameText.S("core.script.you_hear_a_noise"));
                    }
                    level.SetBufferByte(b * DungeonLevel.Width + a, (byte)c);
                    break;

                case 16:
                {
                    trapKind = a;
                    var trap = DungeonTraversal.ResolveTrapAt(state, level, a, cellX, cellY, party, rng, at.Feature,
                        DungeonFeatureKind.ScriptedEvent, rules.Roll);
                    return Finish(party.Members.Any(m => m.Status != CharacterStatus.Dead)
                        ? DungeonScriptEnd.Trap
                        : DungeonScriptEnd.PartyDefeated, trap: trap);
                }

                case 17:
                    log.Add(GameText.S("core.script.monsters_appear")); // 0x11ecc: F, H := 1, 0
                    surprise = Surprise.MonstersAppear;
                    pendingMonster = a + 1;
                    if (pendingMonster > 1)
                        return Finish(DungeonScriptEnd.Fight, a);
                    break;

                case 18: // 0x11f0c: SET for members whose status @24 is not 0 (DEAD)
                    foreach (var m in party.Members)
                        if (m.Status != CharacterStatus.Dead)
                            SetField(m, a, b);
                    break;

                case 19: // 0x11f54: TEST over every member, DEAD included
                    if (!party.Members.Any(m => GetField(m, a) >= b))
                    {
                        state.X = state.PrevX; // 0x110f0: back to the previous cell
                        state.Y = state.PrevY;
                        next = Script(c);
                    }
                    break;

                case 20: // 0x11fb4
                    next = Script(a);
                    break;

                case 21: // 0x11fcc
                    if (rules.Roll100(rng) < a)
                        next = Script(b);
                    break;

                case 22: // 0x11ff4: restart with event record a (feature a + 0xdc), op count from 0
                    if (restarts >= MaxEventRestarts)
                        return Finish(DungeonScriptEnd.Done);
                    var restart = Start(state, level, (byte)(a + 0xdc), party, host, rng, rules, log, firstRun: false,
                        restarts + 1);
                    return restart;

                case 23: // 0x12004: return 7, the ending
                    return Finish(DungeonScriptEnd.Ending);
            }

            p = next;
            if (!cont)
                break;
        }

        return pendingMonster > 0
            ? Finish(DungeonScriptEnd.Fight, pendingMonster - 1)
            : Finish(DungeonScriptEnd.Done);
    }

    /// <summary>
    /// II's field-id map (<c>0x12078</c>, read from the disassembly s81). Ids the map does not cover raise the "bad field"
    /// alert in the ST and read a dummy word; here they read 0 and ignore writes.
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
        8 => ch.Phantasie2?.QuestScore ?? 0,
        9 => ch.MpCeiling,
        10 => ch.MagicMax,
        11 => ch.MagicCur,
        12 => ch.HpMax,
        13 => ch.HpCur,
        18 => (int)ch.Status,
        19 => ch.Phantasie1.Word26,
        >= 20 and <= 39 when (id & 1) == 0 => SkillWord(ch, (id - 20) / 2),
        >= 21 and <= 39 => ch.SkillBonuses.ElementAtOrDefault((id - 21) / 2),
        42 => ch.Score,
        >= 43 and <= 47 => ch.Phantasie1.Runes[id - 43],
        48 => ch.WornShield,
        49 => ch.WornArmor,
        50 => ch.WornWeapon,
        53 => (int)((ch.Experience >> 16) & 0xffff), // the word at @266
        55 => ch.Phantasie1.Word90,
        60 => ch.CastSinceRest ? 1 : 0,               // @94
        >= 61 and <= 120 => SpellIndexOfSlot(id - 61) is int si ? ch.SpellsKnown.ElementAtOrDefault(si) : 0,
        151 => (int)ch.Class,
        >= 152 and <= 160 => ch.InventoryItemIds.ElementAtOrDefault(id - 152),
        161 => (int)ch.Race,
        162 => ch.WeaponDamageB,
        163 => ch.ShieldSoak,
        164 => ch.ArmorSoak,
        _ => 0, // 56..58 (@246..@250) and the rest: no remaster field [?]
    };

    /// <summary>Op 18's write (<c>0x1241c</c>): field 53 sets the XP low byte to 0xff; every other id is a word write.</summary>
    public static void SetField(Character ch, int id, int value)
    {
        switch (id)
        {
            case 1: ch.Stats = ch.Stats with { Str = value }; break;
            case 2: ch.Stats = ch.Stats with { Int = value }; break;
            case 3: ch.Stats = ch.Stats with { Dex = value }; break;
            case 4: ch.Stats = ch.Stats with { Con = value }; break;
            case 5: ch.Stats = ch.Stats with { Cha = value }; break;
            case 6: ch.Stats = ch.Stats with { Luck = value }; break;
            case 7: ch.Word12 = value; break;
            case 8: ch.P2.QuestScore = value; break;
            case 9: ch.MpCeiling = value; break;
            case 10: ch.MagicMax = value; break;
            case 11: ch.MagicCur = value; break;
            case 12: ch.HpMax = value; break;
            case 13: ch.HpCur = value; break;
            case 18: ch.Status = (CharacterStatus)Math.Clamp(value, 0, 2); break;
            case 19: ch.Phantasie1.Word26 = value; break;
            case 42: ch.Score = value; break;
            case >= 43 and <= 47: ch.Phantasie1.Runes[id - 43] = value; break;
            case 48: ch.WornShield = value; break;
            case 49: ch.WornArmor = value; break;
            case 50: ch.WornWeapon = value; break;
            case 53: ch.Experience = (ch.Experience & ~0xffL) | 0xff; break;
            case 55: ch.Phantasie1.Word90 = value; break;
            case 60: ch.CastSinceRest = value != 0; break;
            case >= 61 and <= 120 when SpellIndexOfSlot(id - 61) is int si:
                if (si >= ch.SpellsKnown.Length)
                {
                    var grown = ch.SpellsKnown;
                    Array.Resize(ref grown, si + 1); // II's 56th spell (DIVINE II) on a record sized for I's 55
                    ch.SpellsKnown = grown;
                }
                ch.SpellsKnown[si] = value;
                break;
            case 151: ch.Class = (CharacterClass)value; break;
            case >= 152 and <= 160:
                var slot = id - 152;
                while (ch.InventoryItemIds.Count <= slot)
                    ch.InventoryItemIds.Add(0);
                ch.InventoryItemIds[slot] = value;
                break;
            case 161: ch.Race = (Race)value; break;
            case 162: ch.WeaponDamageB = value; break;
            case 163: ch.ShieldSoak = value; break;
            case 164: ch.ArmorSoak = value; break;
        }
    }

    /// <summary>
    /// II's spell flag slot (<c>@96 + 2·slot</c>) → the Core spell index of II's <c>spells.json</c>: slots 0–53 as-is,
    /// 56 DIVINE II → 55, 57 DIVINE I → 54; the empty slots 54, 55, 58, 59 have none.
    /// </summary>
    public static int? SpellIndexOfSlot(int slot) => slot switch
    {
        >= 0 and < 54 => slot,
        56 => Phantasie2Campaign.DivineIiSpell,
        57 => SpellId.DivineI,
        _ => null,
    };

    private static int SkillWord(Character ch, int slot) => slot switch
    {
        0 => ch.Skills.Attack,
        1 => ch.Skills.Parry,
        2 => ch.Skills.FindItem,
        3 => ch.Skills.SpotTrap,
        4 => ch.Skills.DisarmTrap,
        5 => ch.Skills.Listen,
        6 => ch.TossRock, // II's TOSS skill @42
        7 => ch.Skills.PickLock,
        9 => ch.Skills.Swim,
        _ => 0,
    };
}
