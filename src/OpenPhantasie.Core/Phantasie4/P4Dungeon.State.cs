namespace OpenPhantasie.Core;

/// <summary>
/// Everything a <see cref="P4Dungeon"/> needs to carry on exactly where it stopped, mid-event included: the map's live
/// words and movement codes, position, the loop's globals, the interpreter registers and the helper step. The party is
/// saved on its own.
/// </summary>
public sealed record P4DungeonSnapshot(
    int Map, ushort[] Words, ushort[] Moves, int X, int Y, int Dir, int OldX, int OldY,
    P4DungeonPrompt Prompt, string? BoxTitle, string[] Box, string[] Menu, P4Fight? PendingFight, P4Transfer? Transfer,
    bool EncountersOn, int EncounterRate, int[] Pool, int PoolCount, int QueuedButton, int ExamineSteps, byte[] Visits,
    int SearchBonus, bool Searching, string Stage, string After, string VmWait, string Helper, bool TrapOnChest,
    int Button, int ButtonStep, int MoveResult, int LockDir, int LockResult, int Answer,
    int Pc, int Return, int Tff, int Cmp, int Number, string Text, int MenuAnswer, int[] MenuTexts, int EntryX = 0,
    int EntryY = 0, bool NoStepSinceLoad = false);

public sealed partial class P4Dungeon
{
    /// <summary>The whole loop state (a save).</summary>
    public P4DungeonSnapshot Capture() => new(
        Map.Map, [.. Map.Words], [.. Map.Moves], X, Y, Dir, OldX, OldY,
        Prompt, BoxTitle, [.. _box], [.. _menu], PendingFight, Transfer,
        EncountersOn, EncounterRate, [.. _pool], _poolCount, _inext, ExamineSteps, [.. _visits],
        _searchBonus, _searching, _stage.ToString(), _after.ToString(), _vmWait.ToString(), _helper.ToString(), _trapOnChest,
        _button, _buttonStep, _moveResult, _lockDir, _lockResult, _answer,
        _vm.Pc, _vm.Return, _vm.Tff, _vm.Cmp, _vm.Number, _vm.Text, _vm.Answer, [.. _vm.Menu], EntryX, EntryY, NoStepSinceLoad);

    /// <summary>Puts the loop back as <paramref name="s"/> captured it (the party must be the one saved with it).</summary>
    public void Restore(P4DungeonSnapshot s)
    {
        ArgumentNullException.ThrowIfNull(s);
        _map = new P4DungeonMap(_maps(s.Map), [.. s.Words], [.. s.Moves]);
        (X, Y, Dir, OldX, OldY, EntryX, EntryY) = (s.X, s.Y, s.Dir, s.OldX, s.OldY, s.EntryX, s.EntryY);
        NoStepSinceLoad = s.NoStepSinceLoad;
        Prompt = s.Prompt;
        BoxTitle = s.BoxTitle;
        _box.Clear();
        _box.AddRange(s.Box);
        _menu.Clear();
        _menu.AddRange(s.Menu);
        PendingFight = s.PendingFight;
        Transfer = s.Transfer;
        EncountersOn = s.EncountersOn;
        EncounterRate = s.EncounterRate;
        s.Pool.CopyTo(_pool, 0);
        _poolCount = s.PoolCount;
        _inext = s.QueuedButton;
        ExamineSteps = s.ExamineSteps;
        s.Visits.CopyTo(_visits, 0);
        _searchBonus = s.SearchBonus;
        _searching = s.Searching;
        _stage = Enum.Parse<Stage>(s.Stage);
        _after = Enum.Parse<After>(s.After);
        _vmWait = Enum.Parse<VmWait>(s.VmWait);
        _helper = Enum.Parse<Helper>(s.Helper);
        _trapOnChest = s.TrapOnChest;
        (_button, _buttonStep, _moveResult, _lockDir, _lockResult, _answer) =
            (s.Button, s.ButtonStep, s.MoveResult, s.LockDir, s.LockResult, s.Answer);
        _vm = new VmState
        {
            Pc = s.Pc, Return = s.Return, Tff = s.Tff, Cmp = s.Cmp, Number = s.Number, Text = s.Text, Answer = s.MenuAnswer,
        };
        _vm.Menu.AddRange(s.MenuTexts);
    }
}
