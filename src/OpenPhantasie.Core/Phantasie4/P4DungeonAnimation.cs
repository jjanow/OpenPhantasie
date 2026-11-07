namespace OpenPhantasie.Core;

/// <summary>One picture cycle of <c>_ianim</c>: a picture in <see cref="First"/>..<see cref="Last"/> steps by one, wrapping.</summary>
public sealed record P4ViewCycle(int First, int Last)
{
    public bool Contains(int picture) => picture >= First && picture <= Last;

    public int Next(int picture) => picture >= Last ? First : picture + 1;
}

/// <summary>
/// The real time of <c>_ianim</c>'s passes (measured s119 on XEiJ, <c>FORMAT.md</c> §6.10 "Dungeon <c>_ianim</c>"), all in
/// milliseconds: a plain pass, then what a step pass adds per cell visited (outside the map, unseen, seen, seen and
/// stepped; row 0's cells <see cref="Row0Less"/> less) and per draw. <see cref="WestDraw"/> measured s120 (map 18); null (a pack
/// without it) counts 0.
/// </summary>
public sealed record P4ViewAnimationTiming(
    double PassMs,
    double CellOutside,
    double CellUnseen,
    double CellSeen,
    double CellSeenStepped,
    double Row0Less,
    double FloorDraw,
    double PartyDraw,
    double NorthDraw,
    double? WestDraw);

/// <summary>
/// <c>_ianim</c>'s rules (P4.X <c>0x175BC</c>; <c>FORMAT.md</c> §6.9 / §6.10) as pack data: the step comes when the pass
/// counter <c>0x43590</c> goes past <see cref="StepAfter"/>; the floor / north / west cycles; the random floor
/// (<see cref="RandomFloor"/>, re-rolled to rand(<see cref="RandomFloorRange"/>) + <see cref="RandomFloorBase"/> when the
/// seen-cell counter <c>0x43594</c> is past <see cref="RandomFloorAfter"/>); the floors the party stands in water on.
/// </summary>
public sealed record P4ViewAnimationRules(
    int StepAfter,
    IReadOnlyList<P4ViewCycle> FloorCycles,
    IReadOnlyList<P4ViewCycle> NorthCycles,
    IReadOnlyList<P4ViewCycle> WestCycles,
    P4ViewCycle RandomFloor,
    int RandomFloorBase,
    int RandomFloorRange,
    int RandomFloorAfter,
    P4ViewCycle WaterFloors,
    P4ViewAnimationTiming Timing);

/// <summary>What a step draws over the view: a floor (<c>_fpix</c>, opaque), the party (<c>_mpix</c>), a north or west wall.</summary>
public enum P4ViewDrawKind
{
    Floor,
    Party,
    North,
    West,
}

/// <summary>One draw of a step: <see cref="Picture"/> of <see cref="Kind"/> at view cell (<see cref="Column"/>, <see cref="Row"/>).</summary>
public readonly record struct P4ViewDraw(P4ViewDrawKind Kind, int Column, int Row, int Picture);

/// <summary>
/// IV's location-map view cycling (<c>_ianim</c>, P4.X <c>0x175BC</c>; <c>FORMAT.md</c> §6.10 "Dungeon <c>_ianim</c>") [C],
/// headless. One instance for the whole game: the pass counter (<c>0x43590</c>) and the seen-cell counter (<c>0x43594</c>)
/// are never reset. <see cref="Show"/> is <c>_idraw</c>: a new view (or <see cref="Redraw"/>) puts the map's own pictures
/// back (<c>_idcopy</c>) and the screen holds only the plain view again. <see cref="Pass"/> is one call from the input loop
/// (<c>_mgetc</c> / <c>_mgetc2</c>): on every 51st it steps the seen cells of the rows from <see cref="Rows"/> on
/// (<c>_iaflag</c>) and records what it drew. <see cref="Draws"/> is what the screen holds over the plain view: the last
/// draw of each (cell, kind) in the order they were made. The floor blit is opaque, so a stepped floor covers the part of
/// the wall south of it, and the doorway marks of its own cell, that the plain view draws over it, until the next redraw
/// (the original paints <c>_ianim</c>'s pictures straight into the view's graphics page).
/// <see cref="Advance"/> turns real time into passes at the measured costs (<see cref="P4ViewAnimationTiming"/>).
/// </summary>
public sealed class P4DungeonAnimation
{
    /// <summary>At most this many passes per <see cref="Advance"/> (a long stall does not replay seconds).</summary>
    public const int MaxPassesPerAdvance = 1000;

    /// <summary><c>_iaflag</c> 9: no row steps.</summary>
    public const int NoRows = 9;

    private readonly IRandomSource _rng;
    private readonly int[] _cells = new int[P4DungeonView.Size * P4DungeonView.Size];
    private readonly List<P4ViewDraw> _draws = [];
    private P4DungeonView? _view;
    private bool _levitating;
    private double _carry;

    public P4DungeonAnimation(P4ViewAnimationRules rules, IRandomSource rng)
    {
        Rules = rules ?? throw new ArgumentNullException(nameof(rules));
        _rng = rng ?? throw new ArgumentNullException(nameof(rng));
    }

    public P4ViewAnimationRules Rules { get; }

    /// <summary><c>0x43590</c>: passes since the last step (the step runs on the pass that takes it past <see cref="P4ViewAnimationRules.StepAfter"/>).</summary>
    public int Phase { get; private set; }

    /// <summary><c>0x43594</c>: seen cells visited by steps since the random floor last changed.</summary>
    public int SeenCount { get; private set; }

    /// <summary><c>_iaflag</c>: the first view row that steps (0 all, 1 during an event, 3 under a window, 9 none).</summary>
    public int Rows { get; set; }

    /// <summary>The view now up (the plain <c>_idraw</c>), or null.</summary>
    public P4DungeonView? View => _view;

    /// <summary><c>_pibuf</c> as the steps left it (the view's cells until the first step).</summary>
    public IReadOnlyList<int> Cells => _cells;

    /// <summary>What the steps drew since the view was drawn: the last draw per (kind, cell), oldest first.</summary>
    public IReadOnlyList<P4ViewDraw> Draws => _draws;

    /// <summary>How long the last step pass took (ms), or 0 before the first step.</summary>
    public double LastStepMilliseconds { get; private set; }

    /// <summary>Steps run so far.</summary>
    public int Steps { get; private set; }

    /// <summary>
    /// The view <paramref name="view"/> is up: when its cells, sight or party marker differ from the last one shown, it is
    /// drawn plain again (<c>_idraw</c>: <c>_idcopy</c>, the steps' pictures gone). The counters run on.
    /// </summary>
    public void Show(P4DungeonView view, bool levitating)
    {
        ArgumentNullException.ThrowIfNull(view);
        _levitating = levitating;
        if (_view is { } old && old.Marker == view.Marker && old.Cells.SequenceEqual(view.Cells) && old.Seen.SequenceEqual(view.Seen))
            return;
        _view = view;
        Redraw();
    }

    /// <summary><c>_idraw</c> of the same view (a window closed): the map's own pictures again.</summary>
    public void Redraw()
    {
        _draws.Clear();
        if (_view is { } v)
            for (var i = 0; i < _cells.Length; i++)
                _cells[i] = v.Cells[i];
    }

    /// <summary>No view is up (a fight, a town): nothing steps until the next <see cref="Show"/>.</summary>
    public void Hide()
    {
        _view = null;
        _draws.Clear();
        _carry = 0;
    }

    /// <summary><paramref name="milliseconds"/> of real time passed: run the passes it holds; returns how many ran.</summary>
    public int Advance(double milliseconds)
    {
        if (_view is null || !(milliseconds > 0))
            return 0;
        _carry += milliseconds;
        var passes = 0;
        while (_carry >= Rules.Timing.PassMs && passes < MaxPassesPerAdvance)
        {
            _carry -= Pass();
            passes++;
        }
        if (passes == MaxPassesPerAdvance)
            _carry = 0;
        return passes;
    }

    /// <summary>One <c>_ianim</c> call; returns how long its pass takes (ms): a plain pass, or the step pass with its drawing.</summary>
    public double Pass()
    {
        Phase++;
        if (Phase <= Rules.StepAfter)
            return Rules.Timing.PassMs;
        Phase = 0;
        Steps++;
        LastStepMilliseconds = Rules.Timing.PassMs + Step();
        return LastStepMilliseconds;
    }

    /// <summary>The step (<c>0x175C0</c>…): every seen cell of rows <see cref="Rows"/>..8 steps; returns the time its cells and draws add.</summary>
    private double Step()
    {
        var t = Rules.Timing;
        double ms = 0;
        if (_view is not { } view)
            return 0;
        const int n = P4DungeonView.Size;
        for (var r = Math.Max(Rows, 0); r < n; r++)
            for (var c = 0; c < n; c++)
            {
                if (r == 0)
                    ms -= t.Row0Less;
                var word = _cells[r * n + c];
                if (word == P4DungeonView.Outside)
                {
                    ms += t.CellOutside;
                    continue;
                }
                if (!view.IsSeen(c, r))
                {
                    ms += t.CellUnseen;
                    continue;
                }
                int floor = P4DungeonView.Floor(word), north = P4DungeonView.North(word), west = P4DungeonView.West(word);
                var stepped = false;
                var floorStepped = false;
                foreach (var cycle in Rules.FloorCycles)
                    if (cycle.Contains(floor))
                    {
                        floor = cycle.Next(floor);
                        floorStepped = true;
                    }
                SeenCount++;
                if (Rules.RandomFloor.Contains(floor) && SeenCount > Rules.RandomFloorAfter)
                {
                    SeenCount = 0;
                    floor = P4Dice.Rand(_rng, Rules.RandomFloorRange) + Rules.RandomFloorBase;
                    floorStepped = true;
                }
                if (floorStepped)
                {
                    stepped = true;
                    ms += t.FloorDraw;
                    Draw(new P4ViewDraw(P4ViewDrawKind.Floor, c, r, floor));
                    word = (word & 0xffc0) | floor;
                    if (c == P4DungeonView.Centre && r == P4DungeonView.Centre)
                    {
                        ms += t.PartyDraw;
                        var marker = (view.Marker & 3) + (Rules.WaterFloors.Contains(floor) && !_levitating ? 4 : 0);
                        Draw(new P4ViewDraw(P4ViewDrawKind.Party, c, r, marker));
                    }
                }
                var northStepped = false;
                foreach (var cycle in Rules.NorthCycles)
                    if (cycle.Contains(north))
                    {
                        north = cycle.Next(north);
                        northStepped = true;
                    }
                if (northStepped)
                {
                    stepped = true;
                    ms += t.NorthDraw;
                    Draw(new P4ViewDraw(P4ViewDrawKind.North, c, r, north));
                    word = (word & 0xf83f) | (north << 6);
                }
                var westStepped = false;
                foreach (var cycle in Rules.WestCycles)
                    if (cycle.Contains(west))
                    {
                        west = cycle.Next(west);
                        westStepped = true;
                    }
                if (westStepped)
                {
                    stepped = true;
                    ms += t.WestDraw ?? 0; // a pack without the measurement counts 0
                    Draw(new P4ViewDraw(P4ViewDrawKind.West, c, r, west));
                    word = (word & 0xc7ff) | (west << 11);
                }
                ms += stepped ? t.CellSeenStepped : t.CellSeen;
                _cells[r * n + c] = word;
            }
        return ms;
    }

    private void Draw(P4ViewDraw draw)
    {
        var i = _draws.FindIndex(d => d.Kind == draw.Kind && d.Column == draw.Column && d.Row == draw.Row);
        if (i >= 0)
            _draws.RemoveAt(i);
        _draws.Add(draw);
    }

    /// <summary>
    /// <c>_iaflag</c> for the dungeon's prompt (<c>FORMAT.md</c> §6.9; who writes <c>0x4358C</c>, s135 re-read): the command
    /// menu 0 (<c>_dmain</c> sets 9 only after <c>_mgetc2</c> returns); a message wait 3 (<c>_ewait</c>); the member pick
    /// of a trap 3 (<c>_ctrap</c> / <c>_dtrap</c> set it just before <c>_getwho</c>); a menu 9 (<c>_xmenu</c>, <c>_denc</c>);
    /// a party menu or command screen 9 (<c>_dmain</c> carrying out a command). Typed input ("Letter&gt;" / "Number&gt;",
    /// INPUTS / INPUTN) steps nothing: <c>_wscan</c> / <c>_wiscan</c> read keys through <c>_wgetc</c> / <c>_inkey2</c>
    /// (IOCS only), never <c>_mgetc</c>, the only caller of <c>_ianim</c>; nor do they redraw the view afterwards, so
    /// whatever the last step left stays [C]. Other prompts keep what was set before them (TITLE writes 1, but every
    /// key wait inside an event sets its own value first, so that 1 is never what <c>_ianim</c> reads).
    /// <see cref="RowsChange.Redraw"/>: the original redraws the view here (<c>_idraw</c> then <c>_iaflag</c> = 0, as every
    /// clear but <c>_dmain</c>'s first does): back at the command menu from any other prompt, or a menu closed
    /// (<c>_xmenu</c> ends with <c>_idraw</c> + clear).
    /// </summary>
    public static RowsChange NextRows(int rows, P4DungeonPrompt previous, P4DungeonPrompt prompt, bool inEvent, bool command)
    {
        if (command)
            return new RowsChange(NoRows, false);
        var closed = previous == P4DungeonPrompt.Menu && prompt != P4DungeonPrompt.Menu;
        switch (prompt)
        {
            case P4DungeonPrompt.Action:
                return new RowsChange(0, previous != P4DungeonPrompt.Action);
            case P4DungeonPrompt.Wait or P4DungeonPrompt.Who:
                return new RowsChange(3, closed);
            case P4DungeonPrompt.Menu or P4DungeonPrompt.Letters or P4DungeonPrompt.Number:
                return new RowsChange(NoRows, false);
        }
        if (closed)
            return new RowsChange(0, true);
        if (previous == P4DungeonPrompt.Action)
            return new RowsChange(inEvent ? 1 : NoRows, false);
        return new RowsChange(rows, false);
    }

    /// <summary>The new <c>_iaflag</c> and whether the view is drawn plain again.</summary>
    public readonly record struct RowsChange(int Rows, bool Redraw);
}
