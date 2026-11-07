using OpenPhantasie.Data;

namespace OpenPhantasie.Core.Tests;

/// <summary>
/// IV's location-map view cycling (<c>_ianim</c> <c>0x175BC</c>, s120; <c>FORMAT.md</c> §6.10 "Dungeon <c>_ianim</c>"):
/// <see cref="P4DungeonAnimation"/> with the X68000 pack's <c>dungeon_view_animation</c>, on synthetic views and on the
/// three views timed on XEiJ in s119.
/// </summary>
public class P4DungeonAnimationTests
{
    private const int S = P4DungeonView.Size;

    private static P4ViewAnimationRules Rules() =>
        GraphicsPackCatalog.Load(GameDataRepository.DefaultPathFor(GameId.P4)).Default!.DungeonViewAnimation!;

    private static readonly IReadOnlyDictionary<int, P4DungeonFile> Files = Phantasie4World.LoadDungeonFiles();

    private sealed class FixedRoll(int value) : IRandomSource
    {
        public int Calls { get; private set; }

        public int Next(int minInclusive, int maxExclusive)
        {
            Calls++;
            return Math.Clamp(value, minInclusive, maxExclusive - 1);
        }
    }

    private static ushort Word(int floor, int north, int west) => (ushort)(floor | (north << 6) | (west << 11));

    /// <summary>An open 31 × 31 map of <paramref name="fill"/>, edited.</summary>
    private static P4DungeonMap Map(ushort fill, Action<ushort[]>? edit = null)
    {
        var words = new ushort[31 * 31];
        Array.Fill(words, fill);
        edit?.Invoke(words);
        return new P4DungeonMap(new P4DungeonFile(9, "test", words, [new(0x20, 0, 4, 0)], [0x22, 0x00]));
    }

    private static P4DungeonAnimation Shown(P4DungeonView view, IRandomSource? rng = null, bool levitating = false)
    {
        var a = new P4DungeonAnimation(Rules(), rng ?? new FixedRoll(1));
        a.Show(view, levitating);
        return a;
    }

    private static void Step(P4DungeonAnimation a)
    {
        for (var i = 0; i <= a.Rules.StepAfter; i++)
            a.Pass();
    }

    [OriginalDataFact]
    public void Pack_HasTheMeasuredConstants()
    {
        var r = Rules();
        Assert.Equal(50, r.StepAfter);
        Assert.Equal([new(19, 21), new(24, 26), new(27, 30)], r.FloorCycles);
        Assert.Equal([new(10, 12), new(13, 15)], r.NorthCycles);
        Assert.Equal([new P4ViewCycle(2, 4)], r.WestCycles);
        Assert.Equal(new P4ViewCycle(42, 43), r.RandomFloor);
        Assert.Equal((41, 2, 50), (r.RandomFloorBase, r.RandomFloorRange, r.RandomFloorAfter));
        var t = r.Timing;
        Assert.Equal((5.53, 0.14, 0.27, 0.49, 0.54), (t.PassMs, t.CellOutside, t.CellUnseen, t.CellSeen, t.CellSeenStepped));
        Assert.Equal((2.99, 7.8, 10.51, 7.16), (t.FloorDraw, t.PartyDraw, t.NorthDraw, t.WestDraw)); // west: s120, map 18
    }

    [OriginalDataFact]
    public void StepsOnThe51stPass_AndThePhaseRunsOn()
    {
        var a = Shown(P4DungeonView.Of(Map(Word(19, 31, 7)), 15, 15, 0, false));
        for (var i = 0; i < 50; i++)
            Assert.Equal(5.53, a.Pass());
        Assert.Empty(a.Draws);
        Assert.True(a.Pass() > 5.53);
        Assert.Equal(1, a.Steps);
        Assert.Equal(0, a.Phase);
        Assert.Equal(20, P4DungeonView.Floor(a.Cells[4 * S + 4]));
        // A redraw puts the map's pictures back; the phase is not reset.
        a.Pass();
        a.Redraw();
        Assert.Equal(1, a.Phase);
        Assert.Empty(a.Draws);
        Assert.Equal(19, P4DungeonView.Floor(a.Cells[4 * S + 4]));
    }

    [OriginalDataTheory]
    [InlineData(19, 20)]
    [InlineData(21, 19)]
    [InlineData(24, 25)]
    [InlineData(26, 24)]
    [InlineData(27, 28)]
    [InlineData(30, 27)]
    [InlineData(18, 18)]
    [InlineData(22, 22)]
    [InlineData(41, 41)]
    public void FloorCycles_WrapInTheirOwnRange(int from, int to)
    {
        var a = Shown(P4DungeonView.Of(Map(Word(from, 31, 7)), 15, 15, 0, false));
        Step(a);
        Assert.Equal(to, P4DungeonView.Floor(a.Cells[4 * S + 4]));
        Assert.Equal(from != to, a.Draws.Any(d => d.Kind == P4ViewDrawKind.Floor));
    }

    [OriginalDataTheory]
    [InlineData(10, 11)]
    [InlineData(12, 10)]
    [InlineData(13, 14)]
    [InlineData(15, 13)]
    [InlineData(9, 9)]
    [InlineData(16, 16)]
    public void NorthCycles(int from, int to)
    {
        // A wall north of the centre: the centre is seen, its own north picture steps.
        var a = Shown(P4DungeonView.Of(Map(Word(2, 31, 7), w => w[15 * 31 + 15] = Word(2, (ushort)from, 7)), 15, 15, 0, false));
        Step(a);
        Assert.Equal(to, P4DungeonView.North(a.Cells[4 * S + 4]));
        Assert.Equal(from != to, a.Draws.Contains(new P4ViewDraw(P4ViewDrawKind.North, 4, 4, to)));
    }

    [OriginalDataFact]
    public void WestCycle_2To4()
    {
        var a = Shown(P4DungeonView.Of(Map(Word(2, 31, 7), w => w[15 * 31 + 15] = Word(2, 31, 4)), 15, 15, 0, false));
        Step(a);
        Assert.Equal(2, P4DungeonView.West(a.Cells[4 * S + 4]));
        Assert.Contains(new P4ViewDraw(P4ViewDrawKind.West, 4, 4, 2), a.Draws);
    }

    [OriginalDataFact]
    public void OnlySeenCellsStep()
    {
        // A closed 1-square cell: walls north (31 → 1) of the centre and of the square below, west of the centre and of its east neighbour.
        var map = Map(Word(19, 31, 7), w =>
        {
            w[15 * 31 + 15] = Word(19, 1, 1);
            w[16 * 31 + 15] = Word(19, 1, 7);
            w[15 * 31 + 16] = Word(19, 31, 1);
        });
        var view = P4DungeonView.Of(map, 15, 15, 0, false);
        Assert.Equal(1, view.Seen.Count(s => s));
        var a = Shown(view);
        Step(a);
        Assert.Equal([new P4ViewDraw(P4ViewDrawKind.Floor, 4, 4, 20), new P4ViewDraw(P4ViewDrawKind.Party, 4, 4, 0)], a.Draws);
        Assert.Equal(19, P4DungeonView.Floor(a.Cells[3 * S + 4]));
    }

    [OriginalDataTheory]
    [InlineData(0, 9)]
    [InlineData(1, 8)]
    [InlineData(3, 6)]
    [InlineData(P4DungeonAnimation.NoRows, 0)]
    public void Rows_SkipTheRowsAboveIaflag(int rows, int steppedRows)
    {
        var a = Shown(P4DungeonView.Of(Map(Word(19, 31, 7)), 15, 15, 0, false));
        a.Rows = rows;
        Step(a);
        Assert.Equal(steppedRows * S, a.Draws.Count(d => d.Kind == P4ViewDrawKind.Floor));
        Assert.All(a.Draws, d => Assert.True(d.Row >= rows));
        Assert.Equal(1, a.Steps); // the phase runs even when no row steps
    }

    [OriginalDataFact]
    public void RandomFloor_ChangesOnlyPastTheSeenCountAndRestartsIt()
    {
        // Open floor 42 everywhere: 81 seen cells a step; the counter passes 50 on the 51st seen cell.
        var rng = new FixedRoll(2);
        var a = Shown(P4DungeonView.Of(Map(Word(42, 31, 7)), 15, 15, 0, false), rng);
        Step(a);
        var changed = a.Draws.Where(d => d.Kind == P4ViewDrawKind.Floor).ToList();
        Assert.Single(changed); // cell 51 (index 50: row 5, column 5) only; the counter then starts again and reaches 30
        Assert.Equal(new P4ViewDraw(P4ViewDrawKind.Floor, 5, 5, 43), changed[0]);
        Assert.Equal(30, a.SeenCount);
        Assert.Equal(1, rng.Calls);
        Step(a); // counted across steps: 30 + 21 = 51 at index 20
        Assert.Contains(new P4ViewDraw(P4ViewDrawKind.Floor, 2, 2, 43), a.Draws);
    }

    [OriginalDataFact]
    public void RandomFloor_SameValueStillDrawn()
    {
        var a = Shown(P4DungeonView.Of(Map(Word(43, 31, 7)), 15, 15, 0, false), new FixedRoll(2));
        Step(a);
        Assert.Contains(new P4ViewDraw(P4ViewDrawKind.Floor, 5, 5, 43), a.Draws);
    }

    [OriginalDataTheory]
    [InlineData(false, 2 + 4)]
    [InlineData(true, 2)]
    public void Party_RedrawnOverASteppedCentreFloor_InWaterWithoutLevitation(bool levitating, int marker)
    {
        var a = Shown(P4DungeonView.Of(Map(Word(27, 31, 7)), 15, 15, 2, levitating), levitating: levitating);
        Step(a);
        var i = a.Draws.ToList().IndexOf(new P4ViewDraw(P4ViewDrawKind.Floor, 4, 4, 28));
        Assert.True(i >= 0);
        Assert.Equal(new P4ViewDraw(P4ViewDrawKind.Party, 4, 4, marker), a.Draws[i + 1]);
        Assert.Single(a.Draws, d => d.Kind == P4ViewDrawKind.Party);
    }

    [OriginalDataFact]
    public void Party_NotRedrawnWhenTheCentreFloorDoesNotStep()
    {
        var a = Shown(P4DungeonView.Of(Map(Word(19, 31, 7), w => w[15 * 31 + 15] = Word(2, 31, 7)), 15, 15, 0, false));
        Step(a);
        Assert.DoesNotContain(a.Draws, d => d.Kind == P4ViewDrawKind.Party);
    }

    [OriginalDataFact]
    public void Draws_KeepTheLastPerCellInDrawingOrder()
    {
        var view = P4DungeonView.Of(Map(Word(19, 31, 7), w => w[15 * 31 + 15] = Word(19, 11, 7)), 15, 15, 0, false);
        var a = Shown(view);
        Step(a);
        Step(a);
        var seen = view.Seen.Count(s => s);
        Assert.Equal(seen + 2, a.Draws.Count); // a floor per seen cell, the party and the centre's north wall, the second step's
        Assert.All(a.Draws.Where(d => d.Kind == P4ViewDrawKind.Floor), d => Assert.Equal(21, d.Picture));
        Assert.Equal(new P4ViewDraw(P4ViewDrawKind.Floor, 0, 0, 21), a.Draws[0]);
        var centre = a.Draws.ToList().IndexOf(new P4ViewDraw(P4ViewDrawKind.Floor, 4, 4, 21));
        Assert.Equal(new P4ViewDraw(P4ViewDrawKind.Party, 4, 4, 0), a.Draws[centre + 1]);
        Assert.Equal(new P4ViewDraw(P4ViewDrawKind.North, 4, 4, 10), a.Draws[centre + 2]);
    }

    [OriginalDataFact]
    public void Show_SameViewKeepsTheSteps_NewViewRedraws()
    {
        var map = Map(Word(19, 31, 7), w => w[15 * 31 + 15] = Word(2, 31, 7));
        var a = Shown(P4DungeonView.Of(map, 15, 15, 0, false));
        Step(a);
        a.Show(P4DungeonView.Of(map, 15, 15, 0, false), false);
        Assert.NotEmpty(a.Draws);
        a.Show(P4DungeonView.Of(map, 15, 16, 0, false), false);
        Assert.Empty(a.Draws);
        a.Show(P4DungeonView.Of(map, 15, 16, 1, false), false); // a turn: the marker changes, _idraw again
        Assert.Empty(a.Draws);
    }

    [OriginalDataFact]
    public void Advance_TurnsTimeIntoPasses_StepPassCostsItsDrawing()
    {
        var map = Map(Word(2, 31, 7), w => w[15 * 31 + 15] = Word(2, 10, 7));
        var a = Shown(P4DungeonView.Of(map, 15, 15, 0, false));
        Assert.Equal(50, a.Advance(50 * 5.53 + 0.001));
        Assert.Equal(0, a.Steps);
        Assert.Equal(1, a.Advance(5.53));
        Assert.Equal(1, a.Steps);
        // The step pass took far longer than 5.53 ms: the next passes wait until that time has gone by.
        var owed = a.LastStepMilliseconds - 5.53;
        Assert.Equal(0, a.Advance(owed - 0.01));
        Assert.Equal(1, a.Advance(5.54));
    }

    /// <summary>The views timed on XEiJ in s119 / s120 (map, x, y) with their measured step pass and step period.</summary>
    public static TheoryData<int, int, int, double, double> Measured => new()
    {
        { 1, 16, 27, 51.4, 328.1 },   // Town Hall entry: 2 north torches (10)
        { 5, 15, 29, 79.7, 356.3 },   // Lord Keogh's Castle entry: 16 water (27)
        { 9, 15, 7, 261.9, 538.6 },   // the Inn square (poked): 68 water and the party's floor 19
        { 18, 19, 8, 55.2, 331.9 },   // Fishing Village (s120): 3 west walls 2, nothing else steps
    };

    [OriginalDataTheory]
    [MemberData(nameof(Measured))]
    public void StepPeriod_MatchesTheXEiJMeasurements(int map, int x, int y, double stepPass, double period)
    {
        var view = P4DungeonView.Of(new P4DungeonMap(Files[map]), x, y, 0, false);
        // The timed views hold no 42-43 floor (random); only map 18's steps west walls.
        var rules = Rules();
        var west = 0;
        for (var i = 0; i < S * S; i++)
        {
            if (!view.Seen[i] || view.Cells[i] == P4DungeonView.Outside)
                continue;
            if (rules.WestCycles.Any(c => c.Contains(P4DungeonView.West(view.Cells[i]))))
                west++;
            Assert.False(rules.RandomFloor.Contains(P4DungeonView.Floor(view.Cells[i])));
        }
        Assert.Equal(map == 18 ? 3 : 0, west);
        var a = Shown(view);
        double total = 0;
        for (var i = 0; i <= rules.StepAfter; i++)
            total += a.Pass();
        Assert.Equal(1, a.Steps);
        // Core gives 51.26 / 79.52 / 261.70 / 54.99 ms step passes and 327.76 / 356.02 / 538.20 / 331.49 ms periods (maps 1,
        // 5, 9, 18): 0.2-0.4 ms short each (the per-cell costs are rounded means).
        Assert.True(Math.Abs(a.LastStepMilliseconds - stepPass) <= 1 && Math.Abs(total - period) <= 1, $"{a.LastStepMilliseconds:F2} {total:F2}");
        Assert.InRange(total, period - 1, period + 1);
    }

    [OriginalDataFact]
    public void MonasteryWater_StepCoversTheWallsUnderIt_AsXEiJShows()
    {
        // s125, XEiJ (parity/shots/p4-x68k-dungeon-water-wall-drawn.png / -stepped.png, the GIF): Monastery of the Shadow
        // Priests (20) at (6, 6). _idraw stands row 3's north walls (0) and grates (7) over row 2's water; the first step
        // redraws that water opaque and only the water, so the walls are gone until the next _idraw. The remaster's
        // capture of the same view is pixel-equal in both states.
        var view = P4DungeonView.Of(new P4DungeonMap(Files[20]), 6, 6, 0, false);
        var walls = Enumerable.Range(0, S).Where(c => view.IsSeen(c, 3) && P4DungeonView.North(view.Cell(c, 3)) != 31).ToList();
        Assert.Equal([2, 3, 4, 5, 6, 7], walls);
        Assert.All(walls, c => Assert.True(view.IsSeen(c, 2) && P4DungeonView.Floor(view.Cell(c, 2)) is >= 27 and <= 30));
        var a = Shown(view);
        Step(a);
        Assert.All(walls, c => Assert.Contains(a.Draws, d => d is { Kind: P4ViewDrawKind.Floor, Row: 2 } && d.Column == c));
        Assert.DoesNotContain(a.Draws, d => d.Kind is P4ViewDrawKind.North or P4ViewDrawKind.West);

        // (5, 29): column 3's west walls stand over column 2's water (rows 0-3); the step redraws the water, not the walls.
        view = P4DungeonView.Of(new P4DungeonMap(Files[20]), 5, 29, 0, false);
        Assert.All(Enumerable.Range(0, 4), r =>
        {
            Assert.Equal(1, P4DungeonView.West(view.Cell(3, r)));
            Assert.True(view.IsSeen(2, r) && P4DungeonView.Floor(view.Cell(2, r)) is >= 27 and <= 30);
        });
        a = Shown(view);
        Step(a);
        Assert.All(Enumerable.Range(0, 4), r => Assert.Contains(new P4ViewDraw(P4ViewDrawKind.Floor, 2, r, 28), a.Draws));
        Assert.DoesNotContain(a.Draws, d => d.Kind is P4ViewDrawKind.North or P4ViewDrawKind.West);
    }

    [OriginalDataFact]
    public void MeasuredViews_HoldTheAnimatedCellsS119Counted()
    {
        int Count(int map, int x, int y, Func<int, bool> pick)
        {
            var v = P4DungeonView.Of(new P4DungeonMap(Files[map]), x, y, 0, false);
            return Enumerable.Range(0, S * S).Count(i => v.Seen[i] && v.Cells[i] != P4DungeonView.Outside && pick(v.Cells[i]));
        }
        Assert.Equal(2, Count(1, 16, 27, w => P4DungeonView.North(w) is >= 10 and <= 15));
        Assert.Equal(16, Count(5, 15, 29, w => P4DungeonView.Floor(w) is >= 27 and <= 30));
        // s119's "69 water" at the Inn square are its 69 stepping floors: 68 water and the party's own floor 19 (19-21),
        // which also redraws the party every step.
        Assert.Equal(68, Count(9, 15, 7, w => P4DungeonView.Floor(w) is >= 27 and <= 30));
        Assert.Equal(69, Count(9, 15, 7, w => P4DungeonView.Floor(w) is >= 19 and <= 21 or >= 27 and <= 30));
        Assert.Equal(19, P4DungeonView.Floor(P4DungeonView.Of(new P4DungeonMap(Files[9]), 15, 7, 0, false).Cell(4, 4)));
    }

    [Theory]
    [InlineData(P4DungeonPrompt.Action, P4DungeonPrompt.Action, false, 0, false)]
    [InlineData(P4DungeonPrompt.Action, P4DungeonPrompt.Wait, true, 3, false)]
    [InlineData(P4DungeonPrompt.Wait, P4DungeonPrompt.Action, false, 0, true)]
    [InlineData(P4DungeonPrompt.Action, P4DungeonPrompt.Menu, true, 9, false)]
    [InlineData(P4DungeonPrompt.Menu, P4DungeonPrompt.Wait, true, 3, true)]
    // _ctrap / _dtrap set 3 just before _getwho (s135).
    [InlineData(P4DungeonPrompt.Action, P4DungeonPrompt.Who, true, 3, false)]
    [InlineData(P4DungeonPrompt.Wait, P4DungeonPrompt.Who, true, 3, false)]
    // INPUTS / INPUTN: _wscan / _wiscan never reach _mgetc (no _ianim), and nothing redraws after them (s135).
    [InlineData(P4DungeonPrompt.Action, P4DungeonPrompt.Letters, true, 9, false)]
    [InlineData(P4DungeonPrompt.Wait, P4DungeonPrompt.Number, true, 9, false)]
    [InlineData(P4DungeonPrompt.Letters, P4DungeonPrompt.Wait, true, 3, false)]
    [InlineData(P4DungeonPrompt.Number, P4DungeonPrompt.Who, true, 3, false)]
    [InlineData(P4DungeonPrompt.Letters, P4DungeonPrompt.Action, true, 0, true)]
    [InlineData(P4DungeonPrompt.Menu, P4DungeonPrompt.Fight, true, 0, true)]
    public void NextRows_FollowsWhoWrites0x4358C(P4DungeonPrompt previous, P4DungeonPrompt prompt, bool inEvent, int rows, bool redraw)
    {
        var before = previous switch { P4DungeonPrompt.Wait => 3, P4DungeonPrompt.Menu or P4DungeonPrompt.Letters or P4DungeonPrompt.Number => 9, P4DungeonPrompt.Action => 0, _ => 1 };
        Assert.Equal(new P4DungeonAnimation.RowsChange(rows, redraw),
            P4DungeonAnimation.NextRows(before, previous, prompt, inEvent, command: false));
    }

    [Fact]
    public void NextRows_ACommandScreenStopsEveryRow()
    {
        Assert.Equal(new P4DungeonAnimation.RowsChange(9, false),
            P4DungeonAnimation.NextRows(0, P4DungeonPrompt.Action, P4DungeonPrompt.Action, false, command: true));
    }
}
