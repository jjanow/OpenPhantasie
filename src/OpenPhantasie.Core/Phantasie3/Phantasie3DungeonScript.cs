namespace OpenPhantasie.Core;

/// <summary>
/// Phantasie III's dungeon event programs (ovl4 <c>0x14b0</c>, jump table <c>0x1aea</c>; <c>dungeon-events.md</c> §3):
/// 2- or 4-byte ops at record <c>0x393 + 2·index</c>. Op byte bit 7 = continue with the next op, bit 6 = clear the op
/// byte after it runs, bits 0–5 = type; type 0 or above 27 ends the program, 16 and 17 end it even with bit 7. Gotos
/// carry on at their target. The program runs over the live work record, so cleared ops and rewritten cells persist as far
/// as the record does (the dungeon save).
/// <para>
/// Field ops address a word of each party record at <c>+8 + 2f</c> (<c>f = a − 61</c> when <c>a &gt; 120</c>); the remaster
/// maps the words the data uses onto the character: 1–6 the attributes, 8 the Phantasie II score, 11 magic, 42 the
/// Phantasie I score, 61 the III score, 65 the quest bits, 66 the deed bits, 117 Divine Aid and 120 the Torso area (DOS's
/// archive altar; <c>p3.altar</c> = <c>fixed</c> reads 120 as Divine Aid, the text's meaning). Any other word reads 0 and
/// takes no write <c>[?]</c>.
/// </para>
/// </summary>
public sealed class Phantasie3DungeonScript
{
    /// <summary>Programs start at record <c>0x393</c>; index p is the 2-byte unit <c>0x393 + 2p</c>.</summary>
    public const int ProgramBase = 0x393;

    /// <summary>The program area ends at the text pointers (<c>0x593</c>).</summary>
    public const int ProgramEnd = 0x593;

    /// <summary>A guard against loops (DOS has none; the data has no endless program).</summary>
    public const int MaxOps = 400;

    /// <summary>Ops that are 4 bytes long; the rest are 2.</summary>
    private static readonly HashSet<int> FourByteOps = [12, 13, 14, 15, 18, 19, 21, 23, 24, 25];

    /// <summary>Field ids (after the <c>a − 61</c> step) with their own meaning.</summary>
    public const int FieldP2Score = 8, FieldMagic = 11, FieldP1Score = 42, FieldScore = 61, FieldQuestBits = 65,
        FieldDeedBits = 66, FieldDivineAid = 117, FieldTorso = 120, FieldGoldA = 14, FieldGoldB = 15, FieldXp = 52;

    /// <summary>Op 18 f = 14 / 15: gold taken per living member (<c>min(gold, 65,280)</c>).</summary>
    public const long GoldTakeCap = 65280;

    /// <summary>Op 18 f = 52: XP taken per living member above it.</summary>
    public const long XpTaken = 12000;

    /// <summary>Op 27: a stat is raised while below this.</summary>
    public const int StatRaiseCap = 30;

    private readonly Phantasie3Campaign _campaign;
    private readonly GameSession _session;
    private readonly int _cell;

    internal Phantasie3DungeonScript(Phantasie3Campaign campaign, GameSession session, int cell)
    {
        _campaign = campaign;
        _session = session;
        _cell = cell;
    }

    public static int ProgramOffset(int index) => ProgramBase + 2 * index;

    private DungeonLevel Level => _session.CampaignAccess.CurrentDungeonLevel;

    private byte B(int offset) => Level.GetBufferByte(offset);

    /// <summary>
    /// Run from record offset <paramref name="offset"/> until the program ends or stops for a question or a fight. When it
    /// ends normally and <paramref name="caller"/> is set (a sub-square run by op 22), the caller's later ops run.
    /// </summary>
    internal void RunFrom(int offset, DungeonScriptResume? caller)
    {
        var level = Level;
        var word = 0; // the event sound word (DOS FAR:0xde58, Amiga 0x240fa): cleared as each op starts
        for (var ops = 0; ops < MaxOps; ops++)
        {
            if (offset < ProgramBase || offset >= ProgramEnd)
                break;
            word = 0;
            var op = B(offset);
            var type = op & 0x3f;
            if (type == 0 || type > 27)
                break;
            var length = FourByteOps.Contains(type) ? 4 : 2;
            int a1 = B(offset + 1), a2 = length == 4 ? B(offset + 2) : 0, a3 = length == 4 ? B(offset + 3) : 0;
            if ((op & 0x40) != 0)
                level.SetBufferByte(offset, 0); // bit 6: the step happens once per saved map
            var next = (op & 0x80) != 0 ? offset + length : -1;

            switch (type)
            {
                case 1 or 2 or 3 or 4:
                    // s165: the report's `core.p3.report_escapes_unscathed` pair (Amiga 0x1cb00: 0x1d24a(a1, 1, 2)).
                    if (Phantasie3Campaign.DamageParty(_session, a1, P3DamageReportKind.Escape))
                    {
                        _session.CampaignAccess.PartyDefeatPending = true;
                        return;
                    }
                    word = P3Sounds.Loss; // no message: sounds at the program's end if this op is its last
                    break;
                case 5:
                    _campaign.StartEventFight(_session, a1, ambush: false);
                    return;
                case 6:
                    if (next > 0 || caller is not null)
                        Phantasie3Campaign.Suspend(_session, _cell, next, caller);
                    _session.CampaignAccess.AddPendingTreasure(a1);
                    return; // the rest runs after "Keep …?" (GameSession.AnswerTreasure → ResumeDungeonScript)
                case 7 or 8:
                    _session.Sack.Gold = Math.Max(0, _session.Sack.Gold + Gold(a1));
                    if (type == 7)
                    {
                        _session.Note(GameText.S("core.p3.party_finds_gold"));
                        _session.PlaySound(P3Sounds.GoldFound); // the op tail plays the word with the message
                    }
                    else
                        word = P3Sounds.GoldFound;
                    break;
                case 9 or 10:
                {
                    var amount = Gold(a1);
                    if (amount >= 0)
                        Phantasie3Campaign.TakeGold(_session, amount);
                    else
                        _session.Sack.Gold -= amount; // DOS: a negative amount gives gold (the wizard's +256)
                    if (type == 9)
                    {
                        _session.Note(GameText.S("core.p3.some_gold_was_taken"));
                        _session.PlaySound(P3Sounds.Loss);
                    }
                    else
                        word = P3Sounds.Loss;
                    break;
                }
                case 11:
                    if (_campaign.ShowText(_session, level, a1 - 1))
                    {
                        // "Press a key": the rest (and any caller's rest) runs once the page is acknowledged.
                        _session.CampaignAccess.PendingDungeonScript = new DungeonScriptResume
                        {
                            Feature = (byte)_cell, NextOp = next, Caller = caller, TextWait = true,
                        };
                        return;
                    }
                    break;
                case 12 or 13:
                    Teleport(a1, a2);
                    if (type == 12)
                    {
                        _session.Note(GameText.S("core.p3.party_is_teleported"));
                        _session.PlaySound(P3Sounds.Teleported);
                    }
                    break; // 13 writes no word
                case 14 or 15:
                    if (level.InBounds(a1, a2))
                        level.Set(a1, a2, (byte)a3);
                    if (type == 14)
                    {
                        _session.Note(GameText.S("core.p3.noise_in_the_distance"));
                        _session.PlaySound(P3Sounds.Noise);
                    }
                    break; // 15 writes no word
                case 16:
                    if (Phantasie3Campaign.DamageParty(_session, a1))
                        _session.CampaignAccess.PartyDefeatPending = true;
                    return;
                case 17:
                    _campaign.StartEventFight(_session, a1, ambush: true);
                    return;
                case 18:
                    SetField(Field(a1), a2);
                    break;
                case 19:
                    if (!_session.Party.Members.Any(m => (short)GetField(m, Field(a1)) >= a2))
                    {
                        StepBack();
                        next = ProgramOffset(a3);
                    }
                    break;
                case 20:
                    next = ProgramOffset(a1);
                    break;
                case 21:
                    if (_session.CampaignAccess.Rng.Next(0, 100) < a1)
                        next = ProgramOffset(a2);
                    break;
                case 22:
                {
                    var after = next > 0
                        ? new DungeonScriptResume { Feature = (byte)_cell, NextOp = next, Caller = caller }
                        : caller;
                    _campaign.RunSubSquare(_session, 0xdc + a1, after);
                    return;
                }
                case 23:
                    if (_session.Sack.ItemIds.Contains(a1) || _session.Party.Members.Any(m => m.InventoryItemIds.Contains(a1)))
                        next = ProgramOffset(a2);
                    break;
                case 24:
                    SetBit(a1 - 1, Field(a2), a3 != 0);
                    break;
                case 25:
                    if (Field(a2) == FieldDivineAid
                        || _session.Party.Members.Any(m => (GetField(m, Field(a2)) & (1 << (a1 - 1))) != 0))
                        next = ProgramOffset(a3);
                    break;
                case 26:
                    if (_session.Party.Members.Any(m => m.Race is Race.Dwarf or Race.Gnome))
                        next = ProgramOffset(a1);
                    break;
                case 27:
                    foreach (var m in Living)
                        RaiseStat(m, a1);
                    break;
            }

            if (_session.PartyDefeatPending)
                return;
            if (next < 0)
                break;
            offset = next;
        }

        if (caller?.NextOp > 0)
            new Phantasie3DungeonScript(_campaign, _session, caller.Feature).RunFrom(caller.NextOp, caller.Caller);
        else if (caller?.Caller is { } outer)
            new Phantasie3DungeonScript(_campaign, _session, outer.Feature).RunFrom(outer.NextOp, outer.Caller);
        else
            _session.PlaySound(word); // the program is over: the step's end plays a word no message played (0: none)
    }

    private IEnumerable<Character> Living => _session.Party.Members.Where(m => m.Status != CharacterStatus.Dead);

    /// <summary>
    /// Gold ops: <c>a1 · 256</c>. DOS sign-extends the word, so a1 ≥ 128 is negative (<c>p3.event-gold</c> = <c>dos</c>);
    /// the remaster's default reads it unsigned (the fixed DOS bug).
    /// </summary>
    private long Gold(int a1) =>
        _session.Options.P3SignedEventGold() && a1 >= 128 ? (a1 << 8) - 0x10000 : (long)a1 << 8;

    /// <summary>The field id of byte <paramref name="a"/> (<c>a − 61</c> above 120) and the altar fix (120 → Divine Aid).</summary>
    private int Field(int a)
    {
        var f = a > 120 ? a - 61 : a;
        if (f == FieldTorso && !_session.Options.P3DosAltar())
            f = FieldDivineAid; // the altar grants Divine Aid as its text says (DOS: +0xf8, the Torso)
        return f;
    }

    private void StepBack()
    {
        var d = _session.CampaignAccess.RequireDungeon();
        DungeonTraversal.StepBack(d);
    }

    private void Teleport(int x, int y)
    {
        var d = _session.CampaignAccess.RequireDungeon();
        if (!Level.InBounds(x, y))
            return;
        d.X = d.PrevX = x;
        d.Y = d.PrevY = y;
    }

    private void SetField(int f, int value)
    {
        foreach (var m in Living.ToList())
        {
            switch (f)
            {
                case FieldDivineAid:
                    m.P3.DivineAid = true; // root 0xabbe(member, 57): +0xf0 := 57
                    break;
                case FieldGoldA or FieldGoldB:
                    Phantasie3Campaign.TakeGold(_session, Math.Min(Phantasie3Campaign.PartyGold(_session), GoldTakeCap));
                    break;
                case FieldXp:
                    if (m.Experience > XpTaken)
                        m.Experience -= XpTaken;
                    break;
                default:
                    WriteField(m, f, value);
                    break;
            }
        }
    }

    private void SetBit(int bit, int f, bool or)
    {
        if (bit is < 0 or > 15)
            return;
        foreach (var m in _session.Party.Members)
        {
            var v = GetField(m, f);
            WriteField(m, f, or ? v | (1 << bit) : v & (1 << bit));
        }
    }

    /// <summary>Op 27: <c>+8 + 2·a1</c> + 1 while below 30 (a1 1–5: STR, INT, DEX, CON, CHA).</summary>
    private static void RaiseStat(Character m, int a1)
    {
        var v = GetField(m, a1);
        if (v < StatRaiseCap)
            WriteField(m, a1, v + 1);
    }

    /// <summary>The word at <c>+8 + 2f</c> as the remaster holds it.</summary>
    public static int GetField(Character m, int f)
    {
        var p3 = m.Phantasie3;
        return f switch
        {
            1 => m.Stats.Str,
            2 => m.Stats.Int,
            3 => m.Stats.Dex,
            4 => m.Stats.Con,
            5 => m.Stats.Cha,
            6 => m.Stats.Luck,
            FieldP2Score => p3?.Phantasie2Score ?? 0,
            FieldMagic => m.MagicCur,
            FieldP1Score => p3?.Phantasie1Score ?? 0,
            FieldScore => m.Score,
            FieldQuestBits => p3?.QuestBits ?? 0,
            FieldDeedBits => p3?.DeedBits ?? 0,
            FieldDivineAid => p3?.DivineAid == true ? 57 : 0,
            FieldTorso => (int)P3BodyAreas.Of(m)[P3BodyArea.Torso],
            _ => 0,
        };
    }

    public static void WriteField(Character m, int f, int value)
    {
        switch (f)
        {
            case 1: m.Stats = m.Stats with { Str = value }; break;
            case 2: m.Stats = m.Stats with { Int = value }; break;
            case 3: m.Stats = m.Stats with { Dex = value }; break;
            case 4: m.Stats = m.Stats with { Con = value }; break;
            case 5: m.Stats = m.Stats with { Cha = value }; break;
            case 6: m.Stats = m.Stats with { Luck = value }; break;
            case FieldP2Score: m.P3.Phantasie2Score = value; break;
            case FieldMagic: m.MagicCur = value; break;
            case FieldP1Score: m.P3.Phantasie1Score = value; break;
            case FieldScore: m.Score = value; break;
            case FieldQuestBits: m.P3.QuestBits = value; break;
            case FieldDeedBits: m.P3.DeedBits = value; break;
            case FieldDivineAid: m.P3.DivineAid = value != 0; break;
            case FieldTorso:
            {
                var body = P3BodyAreas.Of(m);
                body[P3BodyArea.Torso] = (P3AreaState)Math.Clamp(value, 0, 3);
                m.BodyCondition = body.ToCondition();
                Phantasie3Campaign.ApplyBodyStatus(m, body);
                break;
            }
        }
    }
}
