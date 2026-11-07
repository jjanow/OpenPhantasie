using System.Text;

namespace OpenPhantasie.Core;

/// <summary>The <c>O_0xx</c> event interpreter (<c>_event</c>, <c>_exe_ev</c>; <c>events.md</c> §2–6).</summary>
public sealed partial class P4Dungeon
{
    /// <summary>The interpreter's registers (<c>events.md</c> §4); saved with the dungeon.</summary>
    public sealed class VmState
    {
        /// <summary>Base-relative program counter.</summary>
        public int Pc { get; set; }

        /// <summary>CALL's single saved return address.</summary>
        public int Return { get; set; }

        /// <summary><c>__tff</c>.</summary>
        public int Tff { get; set; }

        /// <summary>The signed compare register (<c>0x4A432</c>).</summary>
        public int Cmp { get; set; }

        /// <summary><c>__evint</c>: the last number typed (also the town number of TELEPORT 0, 0 and gold 0).</summary>
        public int Number { get; set; }

        /// <summary><c>_evstr</c>: the last text typed.</summary>
        public string Text { get; set; } = string.Empty;

        /// <summary><c>__answer</c>: the last menu line chosen.</summary>
        public int Answer { get; set; }

        /// <summary>The menu's text addresses (<c>_mem</c>, <c>__menup</c>).</summary>
        public List<int> Menu { get; } = [];

        public VmState Clone()
        {
            var c = new VmState
            {
                Pc = Pc, Return = Return, Tff = Tff, Cmp = Cmp, Number = Number, Text = Text, Answer = Answer,
            };
            c.Menu.AddRange(Menu);
            return c;
        }
    }

    private VmState _vm = new();

    /// <summary>The interpreter state (for a front end's debug view and saves).</summary>
    public VmState Vm => _vm;

    /// <summary>What the interpreter is waiting on inside an instruction (resumed by <see cref="RunEvent"/>).</summary>
    private enum VmWait { None, Wait, Menu, Letters, Number, Fight }

    private VmWait _vmWait;

    /// <summary>
    /// <c>_event(x, y, dir)</c>: run the first trigger entry, in file order, whose square matches and whose facing is 4 or
    /// <paramref name="dir"/>; then the random-encounter roll, whether an entry matched or not.
    /// </summary>
    private void StartEvent(int x, int y, int dir, After after)
    {
        _after = after;
        var entry = Map.File.Triggers.FirstOrDefault(t =>
            t.X == x && t.Y == y && (t.Dir == P4EventTrigger.AnyDirection || t.Dir == dir));
        if (entry is null)
        {
            _stage = Stage.RandomEncounter;
            return;
        }
        EncountersOn = true; // _exe_ev starts every event with __encf = 1
        _vm.Pc = entry.Address;
        _vmWait = VmWait.None;
        _stage = Stage.Event;
    }

    private IReadOnlyList<byte> Code => Map.File.Code;

    private int B(int at) => Code[at];

    /// <summary>A 16-bit operand: little-endian (<c>_evadr</c> / <c>_evval</c>), although the CPU is big-endian.</summary>
    private int W(int at) => Code[at] | (Code[at + 1] << 8);

    private string Text(int address) => GameText.S(Map.File.TextId(address));

    /// <summary>The raw text at <paramref name="address"/> (CMPS operands are ASCII answers such as "JILL").</summary>
    private string RawText(int address)
    {
        var end = address;
        while (end < Code.Count && Code[end] != 0)
            end++;
        return Encoding.Latin1.GetString(Code.Skip(address).Take(end - address).ToArray());
    }

    /// <summary>Runs bytecode until END, a wait, a fight or the end of the party.</summary>
    private void RunEvent()
    {
        if (_vmWait != VmWait.None)
        {
            var wait = _vmWait;
            _vmWait = VmWait.None;
            if (wait == VmWait.Menu)
            {
                _vm.Answer = _answer;
                ClearBox(); // _xmenu ends with _idraw
            }
        }

        while (Prompt == P4DungeonPrompt.Action && _stage == Stage.Event)
        {
            var at = _vm.Pc;
            var op = B(at);
            _vm.Pc = at + 1;
            switch (op)
            {
                case 0x00: // END
                    _stage = Stage.RandomEncounter;
                    return;
                case >= 0x01 and <= 0x07: // JMP JT JF JGT JEQ JLT CALL
                    Jump(op, at);
                    break;
                case 0x08: // RET
                    _vm.Pc = _vm.Return;
                    break;
                case 0x09: // PRINT
                    Print(Text(W(at + 1)));
                    _vm.Pc = at + 3;
                    break;
                case 0x0A: // TITLE
                    ClearBox();
                    BoxTitle = Text(W(at + 1));
                    _vm.Pc = at + 3;
                    break;
                case 0x0B: // WAIT
                    _vmWait = VmWait.Wait;
                    Wait();
                    break;
                case 0x0C: // INPUTS
                    Party.Sounds.Note(P4Sounds.EventInput); // 0x196EE: _sdbeep once the band is drawn
                    _vmWait = VmWait.Letters;
                    ExamineSteps = 0;
                    _inext = NoButton;
                    Prompt = P4DungeonPrompt.Letters;
                    break;
                case 0x0D: // CMPS
                    _vm.Cmp = string.Compare(_vm.Text, RawText(W(at + 1)), StringComparison.OrdinalIgnoreCase);
                    _vm.Tff = _vm.Cmp == 0 ? 1 : 0;
                    _vm.Pc = at + 3;
                    break;
                case 0x0E: // INPUTN
                    Party.Sounds.Note(P4Sounds.EventInput); // 0x197B4
                    _vmWait = VmWait.Number;
                    ExamineSteps = 0;
                    _inext = NoButton;
                    Prompt = P4DungeonPrompt.Number;
                    break;
                case 0x0F: // CMPN
                    _vm.Cmp = _vm.Number - W(at + 1);
                    _vm.Pc = at + 3;
                    break;
                case 0x10: // HASITEM
                    _vm.Tff = Party.HasItem(B(at + 1)) ? 1 : 0;
                    _vm.Pc = at + 2;
                    break;
                case 0x11: // GIVEITEM
                    GiveItem(B(at + 1));
                    _vm.Pc = at + 2;
                    break;
                case 0x12: // CMPGOLD
                {
                    var w = W(at + 1);
                    if (w == 0)
                        w = _vm.Number;
                    _vm.Cmp = Party.Gold <= 30000 ? (int)Party.Gold - w : 30000;
                    _vm.Pc = at + 3;
                    break;
                }
                case 0x13: // GIVEGOLD
                    GiveGold(W(at + 1));
                    _vm.Pc = at + 3;
                    break;
                case 0x14: // TESTLF
                    _vm.Tff = Party.LocationFlag(Map.Map, B(at + 1));
                    _vm.Pc = at + 2;
                    break;
                case 0x15: // SETLF
                    Party.LocationFlag(Map.Map, B(at + 1)) = 1;
                    _vm.Pc = at + 2;
                    break;
                case 0x16: // CLRLF
                    Party.LocationFlag(Map.Map, B(at + 1)) = 0;
                    _vm.Pc = at + 2;
                    break;
                case 0x17: // TESTSF
                    _vm.Tff = Party.StoryFlags[B(at + 1)];
                    _vm.Pc = at + 2;
                    break;
                case 0x18: // SETSF
                    Party.StoryFlags[B(at + 1)] = 1;
                    _vm.Pc = at + 2;
                    break;
                case 0x19: // CLRSF
                    Party.StoryFlags[B(at + 1)] = 0;
                    _vm.Pc = at + 2;
                    break;
                case 0x1A: // DAMAGE
                    _vm.Pc = at + 2;
                    for (var i = 0; i < Party.Members.Count; i++)
                        if (Damage(i, P4Dice.Rand(_rng, B(at + 1))))
                            return;
                    break;
                case 0x1B: // WALL2
                    Map.SetMove(B(at + 1), B(at + 2), B(at + 3), P4DungeonMap.LockedDoor);
                    _vm.Pc = at + 4;
                    break;
                case 0x1C: // WALL0
                    Map.SetMove(B(at + 1), B(at + 2), B(at + 3), P4DungeonMap.Open);
                    _vm.Pc = at + 4;
                    break;
                case 0x1D: // OPENGATE
                    if (Map.OpenGate(B(at + 1), B(at + 2)))
                        Party.Sounds.Note(P4Sounds.GateOpens); // _openg 0x18D5E: _sdnoi after the third picture
                    _vm.Pc = at + 3;
                    break;
                case 0x1E: // TELEPORT
                    Dir = B(at + 5);
                    _vm.Pc = at + 6;
                    Teleport(B(at + 1), B(at + 2), B(at + 3), B(at + 4));
                    break;
                case 0x1F: // NOP
                    _vm.Pc = at + 2;
                    break;
                case 0x20: // SOUND (_sdnoi; 0x19C4A: its byte is passed but _sdnoi plays effect 3 whatever it is)
                    Party.Sounds.Note(P4Sounds.EventSound);
                    _vm.Pc = at + 2;
                    break;
                case 0x21: // REDRAW
                    break;
                case 0x22: // NOENC
                    EncountersOn = false;
                    break;
                case 0x23: // FIGHT
                    _vm.Pc = at + 2;
                    if (StartFight(B(at + 1), fromEvent: true))
                        _vmWait = VmWait.Fight;
                    break;
                case 0x24: // CHEST
                    _vm.Pc = at + 2;
                    BeginChest(B(at + 1));
                    break;
                case 0x25: // CMPLVL
                    _vm.Cmp = Party.AverageLevel - B(at + 1);
                    _vm.Pc = at + 2;
                    break;
                case 0x26: // CHANCE: tff = 1 when rand(100) < b, a (b − 1) % chance
                    _vm.Tff = P4Dice.Rand(_rng, 100) < B(at + 1) ? 1 : 0;
                    _vm.Pc = at + 2;
                    break;
                case 0x27: // MCLEAR
                    _vm.Menu.Clear();
                    break;
                case 0x28: // MADD
                    _vm.Menu.Add(W(at + 1));
                    _vm.Pc = at + 3;
                    break;
                case 0x29: // MENU
                    _vmWait = VmWait.Menu;
                    ShowMenu(_vm.Menu.Select(Text));
                    break;
                case 0x2A: // MJMP
                    _vm.Pc = _vm.Answer == B(at + 1) ? W(at + 2) : at + 4;
                    break;
                case 0x2B: // POOLADD (every shipped map-load event clears, then adds 10; past 24 the original writes beyond _mons)
                    _pool[Math.Min(_poolCount, PoolSize - 1)] = B(at + 1);
                    _poolCount = Math.Min(_poolCount + 1, PoolSize);
                    _vm.Pc = at + 2;
                    break;
                case 0x2C: // ENCPCT
                    EncounterRate = B(at + 1);
                    _vm.Pc = at + 2;
                    break;
                case 0x2D: // POOLCLR
                    _poolCount = 0;
                    break;
                case 0x2E: // WALL3
                    Map.SetMove(B(at + 1), B(at + 2), B(at + 3), P4DungeonMap.SealedDoor);
                    _vm.Pc = at + 4;
                    break;
                case 0x2F: // LISTEN
                    Listen();
                    break;
                case 0x30: // TRAP (operand unused)
                    _vm.Pc = at + 2;
                    BeginTrap(chest: false);
                    break;
                case 0x31: // STEPBACK
                    X = OldX;
                    Y = OldY;
                    break;
                case 0x32: // TAKEITEM
                    Party.RemoveItem(B(at + 1));
                    _vm.Pc = at + 2;
                    break;
                case 0x33: // TAKEGOLD (no check: scripts test with CMPGOLD first)
                {
                    var w = W(at + 1);
                    Party.Gold -= w == 0 ? _vm.Number : w;
                    _vm.Pc = at + 3;
                    break;
                }
                case 0x34: // RACEIS
                    _vm.Tff = Party.Members.Any(m => m.Race == B(at + 1)) ? 1 : 0;
                    _vm.Pc = at + 2;
                    break;
                case 0x35: // ENCOPT: written, never read
                    _vm.Pc = at + 2;
                    break;
                case 0x36: // TGLSF
                    Party.StoryFlags[B(at + 1)] ^= 1;
                    _vm.Pc = at + 2;
                    break;
                case 0x37: // WALLPIC5
                    Map.SetNorthPicture(B(at + 1), B(at + 2), 5);
                    _vm.Pc = at + 3;
                    break;
                case 0x38: // WALLPIC4
                    Map.SetNorthPicture(B(at + 1), B(at + 2), 4);
                    _vm.Pc = at + 3;
                    break;
                case 0x39: // RESTORE (_refle → _healall)
                    foreach (var m in Party.Members)
                    {
                        m.Hp = m.HpMax;
                        m.Mp = m.MpMax;
                        if (m.Alive)
                            m.Condition = P4Member.Okay;
                    }
                    break;
                case 0x3A: // SETFLOOR
                    Map.SetFloor(B(at + 1), B(at + 2), B(at + 3));
                    _vm.Pc = at + 4;
                    break;
                default: // unknown opcodes are skipped as one byte (none occur)
                    break;
            }
        }
    }

    /// <summary><c>_evj</c>: JMP, JT, JF, JGT (cmp &gt; 0), JEQ, JLT (cmp &lt; 0), CALL (return = after the operand).</summary>
    private void Jump(int op, int at)
    {
        var take = op switch
        {
            0x02 => _vm.Tff != 0,
            0x03 => _vm.Tff == 0,
            0x04 => _vm.Cmp > 0,
            0x05 => _vm.Cmp == 0,
            0x06 => _vm.Cmp < 0,
            _ => true,
        };
        if (op == 0x07)
            _vm.Return = at + 3;
        _vm.Pc = take ? W(at + 1) : at + 3;
    }

    /// <summary><c>_setitm</c>: "&lt;item&gt; obtained." + WAIT.</summary>
    private void GiveItem(int item)
    {
        Party.AddItem(item, _rng);
        ClearBox();
        _box.Add(_itemName(item));
        _box.Add(GameText.S("p4x.0x4379c").TrimStart());
        _vmWait = VmWait.Wait;
        Wait();
    }

    /// <summary>
    /// <c>_setgp</c>: party gold += w; "w GP obtained." + WAIT. The code's "cap" adds another 990,000 when the gold passes
    /// 990,000 (original bug, kept under <c>p4.gold-cap</c> = uncapped); the manual's 65,000 cap (D4-031) applies otherwise.
    /// </summary>
    private void GiveGold(int w)
    {
        var gold = Party.Gold + w;
        if (gold > 990000)
            gold += 990000;
        var before = Party.Gold;
        Party.Gold = P4Town.Cap(_options, _tables.Towns, gold, P4Town.GoldPath.Loot);
        Party.GoldSinceTown += Math.Max(0, Party.Gold - before);
        ClearBox();
        _box.Add(w.ToString(System.Globalization.CultureInfo.CurrentCulture) + GameText.S("p4x.0x437ab"));
        _box.Add(GameText.S("p4x.0x437af").TrimStart());
        _vmWait = VmWait.Wait;
        Wait();
    }

    /// <summary>
    /// <c>_tele</c>: encounters off and Examine stopped; town 0 = the number typed; the same dungeon map = a move inside it;
    /// anything else leaves the map once the event and the pass are over.
    /// </summary>
    private void Teleport(int kind, int map, int x, int y)
    {
        EncountersOn = false;
        ExamineSteps = 0;
        if (kind == P4Transfer.Town && map == 0)
            map = _vm.Number;
        if (kind == P4Transfer.Dungeon && map == Map.Map)
        {
            X = x;
            Y = y;
            return;
        }
        Transfer = new P4Transfer(kind, map, x, y, Dir);
    }

    /// <summary><c>_listen</c>: the last member whose rand(100) ≤ Listen: "&lt;name&gt; sensed monsters nearby." + WAIT. Nothing else.</summary>
    private void Listen()
    {
        if (LastSuccess(P4Skill.Listen) is not int who)
            return;
        ClearBox();
        _box.Add(Party.Members[who].Name + GameText.S("p4x.0x437c0"));
        _vmWait = VmWait.Wait;
        Wait();
    }
}
