namespace OpenPhantasie.Core;

/// <summary>
/// Traps, chests (<c>_dtrap</c>, <c>_dchest</c>, <c>_ctrap</c>, <c>_cspi</c>), the move buttons and locked doors
/// (<c>_dmain</c>, <c>_dmove</c>, <c>_try_ul</c>) and Examine (<c>_seainit</c>, <c>_seasel</c>); <c>events.md</c> §6.
/// </summary>
public sealed partial class P4Dungeon
{
    /// <summary>A helper in progress: which one and the step it resumes at.</summary>
    private enum Helper
    {
        None,

        /// <summary>Trap spotted: the Disarm / Turn back (Give up) menu is up.</summary>
        TrapMenu,

        /// <summary>"Who disarms the trap?"</summary>
        TrapWho,

        /// <summary>After "… succeeded.": the WAIT, then done.</summary>
        TrapDisarmed,

        /// <summary>After "… failed.": the WAIT, then the trap goes off.</summary>
        TrapFailed,

        /// <summary>After "A trap!": the WAIT, then every member's result.</summary>
        TrapSprung,

        /// <summary>After the members' results: the WAIT, then done.</summary>
        TrapDone,

        /// <summary>After "There is a chest." on a locked chest: the WAIT, then the lock roll.</summary>
        ChestLock,

        /// <summary>After "There is a chest." on a trapped chest: the WAIT, then the trap.</summary>
        ChestTrap,

        /// <summary>The chest's last WAIT, then done.</summary>
        ChestDone,

        /// <summary>A locked door's message (<c>_try_ul</c>): the WAIT, then the move goes on.</summary>
        Lock,
    }

    private Helper _helper;

    /// <summary>The trap belongs to a chest (<c>_ctrap</c>) rather than the floor (<c>_dtrap</c>).</summary>
    private bool _trapOnChest;

    private void Await(Helper next, P4DungeonPrompt prompt = P4DungeonPrompt.Wait)
    {
        _helper = next;
        _stage = Stage.Helper;
        if (prompt == P4DungeonPrompt.Wait)
            Wait();
        else
            Prompt = prompt;
    }

    /// <summary>The helper is finished: back to the event (its tff is set) or, for a lock, to the move.</summary>
    private void HelperDone()
    {
        _helper = Helper.None;
        _stage = Stage.Event;
    }

    private void RunHelper()
    {
        switch (_helper)
        {
            case Helper.TrapMenu:
                if (_answer != 1)
                {
                    _vm.Tff = 0;
                    if (_trapOnChest)
                        Await(Helper.ChestDone); // _dchest's last WAIT
                    else
                        HelperDone();
                    return;
                }
                _vm.Tff = 1;
                ClearBox();
                _box.Add(GameText.S(_trapOnChest ? "p4x.0x438ff" : "p4x.0x439db"));
                Await(Helper.TrapWho, P4DungeonPrompt.Who);
                return;
            case Helper.TrapWho:
            {
                var m = Party.Members[_answer];
                _box.Add(m.Name);
                if (m.Alive && P4Dice.Rand(_rng, 100) <= m.Skill(P4Skill.Disarm))
                {
                    _box[^1] += GameText.S(_trapOnChest ? "p4x.0x43918" : "p4x.0x439f4");
                    if (_trapOnChest)
                        Await(Helper.ChestDone, P4DungeonPrompt.Wait, openChest: true);
                    else
                        Await(Helper.TrapDisarmed);
                    return;
                }
                _box[^1] += GameText.S(_trapOnChest ? "p4x.0x43929" : "p4x.0x43a05");
                Await(Helper.TrapFailed);
                return;
            }
            case Helper.TrapDisarmed:
            case Helper.TrapDone:
                HelperDone();
                return;
            case Helper.TrapFailed:
                SpringTrap();
                return;
            case Helper.TrapSprung:
                TrapResults();
                return;
            case Helper.ChestLock:
            {
                var who = LastSuccess(P4Skill.PickLock);
                _box.Add(who is int i
                    ? Party.Members[i].Name + GameText.S("p4x.0x4399e")
                    : GameText.S("p4x.0x4398f"));
                _vm.Tff = who is null ? 0 : 1;
                Await(Helper.ChestDone, P4DungeonPrompt.Wait, openChest: who is not null);
                return;
            }
            case Helper.ChestTrap:
                BeginTrap(chest: true);
                return;
            case Helper.ChestDone:
                ClearBox(); // _idraw
                HelperDone();
                return;
            case Helper.Lock:
                _helper = Helper.None;
                _stage = Stage.Button;
                return;
        }
    }

    private void Await(Helper next, P4DungeonPrompt prompt, bool openChest)
    {
        if (openChest)
            Map.SetFloor(X, Y, P4DungeonMap.OpenedChest);
        Await(next, prompt);
    }

    /// <summary>
    /// <c>_dtrap</c> / <c>_ctrap</c>: the last living member whose rand(100) ≤ Spot Trap (record +42) finds it and the
    /// player chooses Disarm or Turn back (Give up on a chest); nobody → it goes off at once.
    /// </summary>
    private void BeginTrap(bool chest)
    {
        _trapOnChest = chest;
        if (LastSuccess(P4Skill.SpotTrap, SearchBonus) is int spotter)
        {
            ClearBox();
            _box.Add(Party.Members[spotter].Name + GameText.S(chest ? "p4x.0x438ca" : "p4x.0x439b2"));
            _menu.Clear();
            _menu.Add(GameText.S(chest ? "p4x.0x438e7" : "p4x.0x439c5"));
            _menu.Add(GameText.S(chest ? "p4x.0x438f4" : "p4x.0x439d2"));
            ExamineSteps = 0;
            _inext = NoButton;
            Party.Sounds.Note(P4Sounds.EventMenu); // _ctrap 0x1AE8E / _dtrap 0x1B742 run _xmenu: _sdbeep
            Await(Helper.TrapMenu, P4DungeonPrompt.Menu);
            return;
        }
        SpringTrap();
    }

    /// <summary>The trap goes off: tff = 1, "A trap!" + WAIT.</summary>
    private void SpringTrap()
    {
        _vm.Tff = 1;
        ClearBox();
        _box.Add(GameText.S(_trapOnChest ? "p4x.0x43936" : "p4x.0x43a12"));
        Await(Helper.TrapSprung);
    }

    /// <summary>
    /// Each member in order: an Okay member with rand(100) &lt; defence (+35) dodges; anyone else takes rand(5) ×
    /// (trunc(map / 5) + 1). Then WAIT (a chest's opens: tff is 1).
    /// </summary>
    private void TrapResults()
    {
        ClearBox();
        for (var i = 0; i < Party.Members.Count; i++)
        {
            var m = Party.Members[i];
            if (m.Condition == P4Member.Okay && P4Dice.Rand(_rng, 100) < m.Defence)
            {
                _box.Add(m.Name + GameText.S(_trapOnChest ? "p4x.0x4393f" : "p4x.0x43a1b"));
                continue;
            }
            var d = P4Dice.Rand(_rng, 5) * (Map.Map / 5 + 1);
            _box.Add(m.Name + GameText.S(_trapOnChest ? "p4x.0x43950" : "p4x.0x43a2c") + d
                     + GameText.S(_trapOnChest ? "p4x.0x43954" : "p4x.0x43a30"));
            if (Damage(i, d))
                return;
        }
        if (_trapOnChest)
            Await(Helper.ChestDone, P4DungeonPrompt.Wait, openChest: true);
        else
            Await(Helper.TrapDone);
    }

    /// <summary>
    /// <c>_dchest(k)</c>: nothing on an opened chest (floor 6, tff = 0). Else "There is a chest." for k = 0 plain, 1 locked
    /// (Pick Lock), 2 trapped; k = 3 is a hidden item (<c>_cspi</c>). A chest whose contents the party gets becomes floor 6.
    /// </summary>
    private void BeginChest(int kind)
    {
        if (Map.Floor(X, Y) == P4DungeonMap.OpenedChest)
        {
            _vm.Tff = 0;
            return;
        }
        if (kind == 3)
        {
            HiddenItem();
            return;
        }
        ClearBox();
        _box.Add(GameText.S("p4x.0x4397e"));
        switch (kind)
        {
            case 0:
                _vm.Tff = 1;
                Await(Helper.ChestDone, P4DungeonPrompt.Wait, openChest: true);
                break;
            case 1:
                Await(Helper.ChestLock);
                break;
            case 2:
                Await(Helper.ChestTrap);
                break;
            default: // no other kind occurs: _dchest leaves tff as it was
                Await(Helper.ChestDone);
                break;
        }
    }

    /// <summary>
    /// <c>_cspi</c>: the last living member whose rand(100) ≤ record +42 (the Spot Trap byte) "found an item." + WAIT,
    /// tff = 1; nobody → tff = 0 silently. The square is not marked.
    /// </summary>
    private void HiddenItem()
    {
        if (LastSuccess(P4Skill.SpotTrap, SearchBonus) is not int who)
        {
            _vm.Tff = 0;
            return;
        }
        ClearBox();
        _box.Add(Party.Members[who].Name + GameText.S("p4x.0x43967"));
        _vm.Tff = 1;
        _vmWait = VmWait.Wait;
        Wait();
    }

    // --- Move buttons (_dmain) and doors (_dmove, _try_ul) -------------------------------------------------------------

    /// <summary>Button → (queued button, direction, queued button if blocked, second direction) for the four diagonals.</summary>
    private static readonly IReadOnlyDictionary<int, (int Queue, int Dir, int FailQueue, int SecondDir)> Diagonals =
        new Dictionary<int, (int, int, int, int)>
        {
            [0] = (1, 3, 3, 2), // SW: W, then S
            [2] = (1, 1, 5, 2), // SE: E, then S
            [6] = (7, 3, 3, 0), // NW: W, then N
            [8] = (7, 1, 5, 0), // NE: E, then N
        };

    /// <summary>The orthogonal buttons' directions: 1 S, 3 W, 5 E, 7 N.</summary>
    private static int ButtonDir(int button) => button switch
    {
        1 => 2,
        3 => 3,
        5 => 1,
        7 => 0,
        _ => throw new ArgumentOutOfRangeException(nameof(button)),
    };

    private int _button, _buttonStep, _moveResult;

    /// <summary>A pending door: the side's code was 2 or 3 and <c>_try_ul</c> showed its message.</summary>
    private int _lockDir, _lockResult;

    private void BeginButton(int button)
    {
        _button = button;
        _buttonStep = 0;
        _stage = Stage.Button;
    }

    /// <summary>
    /// One button (<c>_dmain</c>'s switch): an orthogonal step, or a diagonal's first step with the second queued, and the
    /// other order when the first is blocked. A step off the map edge (code 7) runs the edge event (trigger x = 0x21).
    /// </summary>
    private void RunButton()
    {
        if (_buttonStep == 1 || _buttonStep == 3)
        {
            // Back from a door's message.
            _moveResult = FinishMove(_lockDir, _lockResult);
            _buttonStep++;
        }
        if (_buttonStep == 0)
        {
            if (Diagonals.TryGetValue(_button, out var d))
            {
                _inext = d.Queue;
                if (StartMove(d.Dir) is not int r)
                {
                    _buttonStep = 1;
                    return;
                }
                _moveResult = r;
            }
            else
            {
                if (StartMove(ButtonDir(_button)) is not int r)
                {
                    _buttonStep = 1;
                    return;
                }
                _moveResult = r;
            }
            _buttonStep = 2;
        }
        if (_buttonStep == 2)
        {
            if (Diagonals.TryGetValue(_button, out var d) && _moveResult != P4DungeonMap.Open)
            {
                _inext = d.FailQueue;
                if (StartMove(d.SecondDir) is not int r)
                {
                    _buttonStep = 3;
                    return;
                }
                _moveResult = r;
            }
            _buttonStep = 4;
        }
        if (_moveResult == P4DungeonMap.Edge)
            StartEvent(P4EventTrigger.OnEdge, 0, Dir, After.LoopEnd);
        else
            _stage = Stage.LoopEnd;
    }

    /// <summary>
    /// <c>_dmove(dir)</c>, first half: the facing turns only when no step is queued; a door (code 2 or 3) shows
    /// <c>_try_ul</c>'s message and waits (null). Otherwise the result code.
    /// </summary>
    private int? StartMove(int dir)
    {
        if (_inext == NoButton)
            Dir = dir;
        var code = Map.Move(X, Y, dir);
        if (code is not (P4DungeonMap.LockedDoor or P4DungeonMap.SealedDoor))
            return FinishMove(dir, code);

        // _try_ul: every living member rolls Pick Lock (a sealed door too); the last success forces a locked door.
        var who = LastSuccess(P4Skill.PickLock);
        ClearBox();
        if (who is null || code == P4DungeonMap.SealedDoor)
        {
            _box.Add(GameText.S("p4x.0x43a44"));
            _lockResult = P4DungeonMap.SealedDoor;
        }
        else
        {
            _box.Add(Party.Members[who.Value].Name + GameText.S("p4x.0x43a53"));
            _lockResult = P4DungeonMap.Open;
        }
        // Either way the side the party FACES becomes 3 (_plock / _setmove(… __dire, 3)): a forced door cannot be passed
        // again from that side until the map reloads.
        Map.SetMove(X, Y, Dir, P4DungeonMap.SealedDoor);
        _lockDir = dir;
        Await(Helper.Lock);
        return null;
    }

    /// <summary><c>_dmove</c>, second half: code 0 moves the party (and notes where it came from); returns the code.</summary>
    private int FinishMove(int dir, int code)
    {
        if (code != P4DungeonMap.Open)
            return code;
        var (nx, ny) = dir switch
        {
            0 => (X, Y - 1),
            1 => (X + 1, Y),
            2 => (X, Y + 1),
            _ => (X - 1, Y),
        };
        if (nx != X || ny != Y)
        {
            OldX = X;
            OldY = Y;
            X = nx;
            Y = ny;
            NoStepSinceLoad = false;
            StepLevitation();
            Party.Sounds.Note(P4Sounds.DungeonStep); // _dmove 0x1ABDA: _sdwlk once _cmapx / _cmapy changed
        }
        return code;
    }

    /// <summary>D4-039 manual: Levitation lasts a number of steps (<see cref="P4Party.LevitationSteps"/>).</summary>
    private void StepLevitation()
    {
        if (Party.LevitationSteps is not int left)
            return;
        left--;
        Party.LevitationSteps = left > 0 ? left : null;
        if (left <= 0)
            Party.Levitating = false;
    }

    // --- Examine (D4-041 code: the automatic walk) ---------------------------------------------------------------------

    public const int ExamineWalkSteps = 63;

    /// <summary>
    /// <c>_seainit</c>: 63 steps. Its clearing loop zeroes byte 14 of the movement map 900 times instead of the visit
    /// counts: the S and W codes of square (7, 0) become 0 (open) until the map reloads.
    /// </summary>
    private void StartExamineWalk()
    {
        Map.Moves[7] &= 0x00ff;
        ExamineSteps = ExamineWalkSteps;
    }

    /// <summary>
    /// <c>_seasel</c>: one step of the walk: count the visit, then queue the side with the lowest count, in the order N, E,
    /// S, W (ties keep the first). A side counts 90 when its movement code is not 0, its wall is not the plain opening
    /// (north / south picture 31, west / east 7) or it is the map edge. Nothing is below 99 only if all four are 90 — then
    /// N is queued anyway.
    /// </summary>
    private void ExamineStep()
    {
        ExamineSteps--;
        const int size = P4DungeonFile.Size, closed = 90;
        _visits[Y * size + X]++;
        int Visits(int x, int y) => x is >= 0 and < size && y is >= 0 and < size ? _visits[y * size + x] : closed;
        var counts = new[] { Visits(X, Y - 1), Visits(X + 1, Y), Visits(X, Y + 1), Visits(X - 1, Y) };
        for (var d = 0; d < 4; d++)
            if (Map.Move(X, Y, d) != 0)
                counts[d] = closed;
        if (Map.NorthPicture(X, Y) != 31 || Y == 0)
            counts[0] = closed;
        if (Map.WestPicture(X + 1, Y) != 7 || X == size - 1)
            counts[1] = closed;
        if (Map.NorthPicture(X, Y + 1) != 31 || Y == size - 1)
            counts[2] = closed;
        if (Map.WestPicture(X, Y) != 7 || X == 0)
            counts[3] = closed;
        int[] buttons = [7, 5, 1, 3];
        var best = 99;
        for (var d = 0; d < 4; d++)
            if (counts[d] < best)
            {
                best = counts[d];
                _inext = buttons[d];
            }
    }
}
