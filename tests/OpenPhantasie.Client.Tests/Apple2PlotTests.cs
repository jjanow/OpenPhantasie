using Microsoft.Xna.Framework.Input;
using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client.Tests;

/// <summary>
/// s228: Phantasie III Apple II's combat field (<see cref="Apple2FieldRun.PlanPlot"/>, <see cref="UiOverlays.Apple2PlotPlaces"/>,
/// the pack's <c>apple2_field</c> with <c>monster_plot</c>; <c>reverse-engineering/apple2/phantasie3/GRAPHICS.md</c> section 9):
/// MONSTER PLOT's columns and rank rows (the giant's), the party rows, the archer's bow and arrow, the cast's arm 5 and
/// sparkle, a member erased at its death, a same-named monster told apart by the hit before its death line, the parry's
/// arm from the round's start, the message box's HOMEs, and III's routing to the field.
/// </summary>
public class Apple2PlotTests
{
    private static readonly Apple2FieldPlot Plot = new()
    {
        Boxes = new Apple2FieldBoxes(4, 6, 5, 0, 1, 2, 3, 5, 4, "OIBG"),
    };

    private static readonly Apple2FieldLayout Layout = new()
    {
        ScreenX = 20, ScreenY = 4, LoopMs = 1.0, SparkleMs = 300, TextTop = 9, TextRows = 3, TextLeft = 17, Columns = 22,
        Bodies = new Apple2FieldBodies(7, 26, [0, 2], 25, 19, [1, 4, 5], 27, 0) { NobleClass = 3, NobleBody = 4 },
        Plot = Plot,
    };

    private static Character Member(string name, Race race, CharacterClass cls, P3PartyRank rank) =>
        new() { Name = name, Race = race, Class = cls, Phantasie3 = new Phantasie3CharacterData { Rank = rank } };

    private static readonly Character A = Member("A", Race.Human, CharacterClass.Fighter, P3PartyRank.Front);
    private static readonly Character B = Member("B", Race.Elf, CharacterClass.Wizard, P3PartyRank.Back);
    private static readonly Character C = Member("C", Race.Dwarf, CharacterClass.Ranger, P3PartyRank.Middle);
    private static readonly Character D = Member("D", Race.Human, CharacterClass.Thief, P3PartyRank.Middle);
    private static readonly Monster Orc1 = new() { Id = 8, Name = "ORC", HitPoints = 5 };
    private static readonly Monster Orc2 = new() { Id = 8, Name = "ORC", HitPoints = 0 }; // slain in the round
    private static readonly Monster Ant = new() { Id = 4, Name = "GIANT ANT", HitPoints = 5 };

    private static Apple2FieldSnapshot Snapshot(IEnumerable<Character>? parrying = null, bool dungeon = false,
        IReadOnlyList<IReadOnlyList<Monster>>? ranks = null) =>
        new Apple2FieldSnapshot([A, B, C, D], new HashSet<Character>(ReferenceEqualityComparer.Instance),
            new HashSet<Character>(parrying ?? [], ReferenceEqualityComparer.Instance), null, ranks ?? [[Orc1, Orc2], [Ant]])
        {
            Rows = new Dictionary<Character, int>(ReferenceEqualityComparer.Instance) { [A] = 2, [B] = 0, [C] = 1, [D] = 1 },
            Archers = new HashSet<Character>([C], ReferenceEqualityComparer.Instance),
            InDungeon = dungeon,
        };

    private static readonly Apple2FieldArt Art = new(
        new Dictionary<int, Apple2FieldMonster>
        {
            [8] = new(0, 1, 21, new("orc-idle.png", 0, 0), new("orc-b.png", 0, 0)) { Shape = 5 },
            [4] = new(0, 1, 21, new("ant-idle.png", 0, 0), new("ant-b.png", 0, 0)) { Shape = 7 },
            [70] = new(0, 1, 42, new("giant-idle.png", 0, 0), new("giant-b.png", 0, 0)) { Shape = 21 },
        },
        new Dictionary<(int, string), Apple2FieldPicture>
        {
            [(19, "idle")] = new("19.idle", -3, 20), [(19, "attacking")] = new("19.attacking", -1, 25),
            [(19, "parrying")] = new("19.parrying", 1, 24), [(27, "idle")] = new("27.idle", -3, 20),
            [(27, "casting")] = new("27.casting", -1, 22), [(4, "idle")] = new("4.idle", -3, 20),
        },
        new Apple2FieldPicture("sparkle.png", 5, 10),
        new Dictionary<int, Apple2FieldPicture> { [1] = new("hit.png", 1, 18) })
    {
        Effects = new Dictionary<string, Apple2FieldPicture>
        {
            ["arrow"] = new("arrow.png", -5, 9), ["bow"] = new("bow.png", 1, 22), ["bow-fired"] = new("fired.png", 1, 21),
            ["body-down"] = new("down.png", -2, 20),
        },
        Shapes = new Dictionary<int, Apple2FieldPicture> { [20] = new("arm20.png", 0, 0), [22] = new("arm22.png", 0, 0) },
        Frame = new Apple2FieldPicture("frame.png", 0, 0),
    };

    [Fact]
    public void Columns_Ranks_AndRows_FollowMonsterPlotAnd9100()
    {
        // 1550: X% = 21 + 4 * INT(5 * (Y + .5) / (GN% + 1))
        Assert.Equal([29], Enumerable.Range(0, 1).Select(i => Plot.ByteColumn(i, 1)));
        Assert.Equal([25, 33], Enumerable.Range(0, 2).Select(i => Plot.ByteColumn(i, 2)));
        Assert.Equal([21, 29, 37], Enumerable.Range(0, 3).Select(i => Plot.ByteColumn(i, 3)));
        Assert.Equal([21, 25, 33, 37], Enumerable.Range(0, 4).Select(i => Plot.ByteColumn(i, 4)));
        Assert.Equal([21, 25, 29, 33, 37], Enumerable.Range(0, 5).Select(i => Plot.ByteColumn(i, 5)));
        Assert.Equal((120, 99, 96, 96), (Plot.RankY(0, false), Plot.RankY(0, true), Plot.RankY(1, false), Plot.RankY(1, true)));
        Assert.Equal((190, 182, 174), (Plot.PartyY(0), Plot.PartyY(1), Plot.PartyY(2))); // back, middle, front
    }

    [Theory]
    [InlineData(Race.Human, CharacterClass.Fighter, 0, 19)]
    [InlineData(Race.Human, CharacterClass.Fighter, 3, 4)]   // TOWN3 2360: social class 3 → shape 4
    [InlineData(Race.Elf, CharacterClass.Priest, 3, 4)]
    [InlineData(Race.Dwarf, CharacterClass.Fighter, 3, 25)]  // but not over 25
    [InlineData(Race.Ogre, CharacterClass.Wizard, 3, 4)]
    [InlineData(Race.Human, CharacterClass.Monk, 2, 27)]
    [InlineData(Race.Human, CharacterClass.Monk, 4, 27)]     // a later social class (Darklord) is not the noble's
    public void Bodies_FollowTOWN3(Race race, CharacterClass cls, int social, int body) =>
        Assert.Equal(body, Layout.Bodies.For((int)race, (int)cls, social));

    /// <summary>
    /// A swings at Orc 2 and kills it; B casts at the ant; C (an archer) misses, then hits Orc 1; Orc 1 hits D, who dies.
    /// The death lines read the Apple's way: the name, then "-DEATH-" (both orcs share the name).
    /// </summary>
    private static Apple2FieldRun Round(int timeLag = 5, bool sound = true, IEnumerable<Character>? parrying = null)
    {
        string[] log =
        [
            GameText.F("core.combat.x_hits", "A"), GameText.F("core.combat.x_takes_x_damage", "ORC", 9),
            GameText.F("core.combat.x_dies", "ORC"),
            GameText.F("core.p3.x_casts_x", "B", "FIREFLASH"), GameText.F("core.combat.x_takes_x_damage", "GIANT ANT", 4),
            GameText.F("core.combat.x_misses", "C"),
            GameText.F("core.combat.x_hits", "C"), GameText.F("core.combat.x_takes_x_damage", "ORC", 2),
            GameText.F("core.combat.x_hits", "ORC"), GameText.F("core.combat.x_takes_x_damage", "D", 12),
            GameText.F("core.combat.x_dies", "D"),
        ];
        CombatLineTag[] tags =
        [
            new(0, A, null), new(0, A, null, CombatSwing.Hit), new(2, A, null, Sound: P3Sounds.Dies),
            new(3, B, null), new(3, B, null, Sound: P3Fight.CastSound),
            new(5, C, null), new(5, C, null, CombatSwing.Miss), new(6, C, null, CombatSwing.Hit),
            new(8, null, Orc1), new(8, null, Orc1, CombatSwing.Hit),
        ];
        CombatHit[] hits =
        [
            new(DamageSource.PartyMelee, null, Orc2, Line: 1), new(DamageSource.PartySpell, null, Ant, Line: 4, SpellDamage: 4),
            new(DamageSource.PartyMelee, null, Orc1, Line: 7), new(DamageSource.MonsterMelee, D, null, Line: 9),
        ];
        var dead = D.Status;
        D.Status = CharacterStatus.Dead; // dies in the round
        try
        {
            return Apple2FieldRun.PlanPlot(Layout, log, tags, hits, ["VICTORY"], Snapshot(parrying), timeLag, sound);
        }
        finally
        {
            D.Status = dead;
        }
    }

    [Fact]
    public void Lines_Pause_TheArrowsLoops_TheSpellHit_TheCastTones()
    {
        var run = Round();
        Assert.Equal(12, run.Lines.Count);
        Assert.Equal(0, run.Lines[0].EndMs - run.Lines[0].StartMs);              // "A HITS": straight to its damage
        Assert.Equal(600, run.Lines[1].EndMs - run.Lines[1].StartMs);            // PA = 120 * 5 loops
        Assert.Equal(300, run.Lines[3].EndMs - run.Lines[3].StartMs);            // the cast tones [?]
        Assert.Equal(500 + 600, run.Lines[4].EndMs - run.Lines[4].StartMs);      // 4132's 500 loops, then PA
        Assert.Equal(250 + 600, run.Lines[5].EndMs - run.Lines[5].StartMs);      // 1628: the missed arrow's 250, then PA
        Assert.Equal(500 + 600, run.Lines[7].EndMs - run.Lines[7].StartMs);      // the arrow on the orc (SP = 2)
        Assert.Equal(0, Round(sound: false).Lines[3].EndMs - Round(sound: false).Lines[3].StartMs);
    }

    [Fact]
    public void Archer_DrawsTheBow_LoosesIt_TheArrowMissesAtTheMissPoint_ThenHitsTheOrc()
    {
        var run = Round();
        var start = UiOverlays.Apple2PlotPlaces(Layout, Art, Snapshot(), run.At(-1));
        // slot 2 (C): x 147 + 38, the middle row 182; shape 2 for the figure (9200), no body
        Assert.Contains(new UiOverlays.Apple2FieldPlace("bow.png", 20 + 185 - 1, 4 + 182 - 22), start);
        var miss = run.At(run.Lines[5].StartMs + 1);
        Assert.Contains(C, miss.Fired);
        Assert.True(miss.RockMiss);
        var missPlaces = UiOverlays.Apple2PlotPlaces(Layout, Art, Snapshot(), miss);
        Assert.Contains(new UiOverlays.Apple2FieldPlace("arrow.png", 20 + 220 + 5, 4 + 104 - 9), missPlaces);
        Assert.Contains(missPlaces, p => p.File == "fired.png");
        Assert.DoesNotContain(missPlaces, p => p.File == "bow.png");
        Assert.Contains(new UiOverlays.Apple2FieldPlace("arrow.png", 20 + 220 + 5, 4 + 105 - 9),
            UiOverlays.Apple2PlotPlaces(Layout, Art, Snapshot(dungeon: true), miss)); // DUNGON3 1628
        Assert.False(run.At(run.Lines[5].StartMs + 251).RockMiss);
        var hit = run.At(run.Lines[7].StartMs + 1);
        Assert.Same(Orc1, hit.Hit);
        Assert.True(hit.HitRock);
        // Orc 1: front rank (y 120), the first of 2 living at the round's start: byte column 25, so (7 * 25 + 3, 120 + 20)
        Assert.Contains(new UiOverlays.Apple2FieldPlace("arrow.png", 20 + 178 + 5, 4 + 140 - 9),
            UiOverlays.Apple2PlotPlaces(Layout, Art, Snapshot(), hit));
    }

    [Fact]
    public void Cast_Arm5_AndTheSparkleAtTheFigurePlus14Minus23_TheSpellHitOnItsMonster()
    {
        var run = Round();
        var cast = run.At(run.Lines[3].StartMs + 1);
        Assert.Contains(B, cast.Cast);
        Assert.Same(B, cast.Sparkle);
        var places = UiOverlays.Apple2PlotPlaces(Layout, Art, Snapshot(), cast);
        // B: slot 1 at x 166, the back row at 190; an elf wizard: body 27 with arm 5
        Assert.Contains(new UiOverlays.Apple2FieldPlace("27.casting", 20 + 166 + 1, 4 + 190 - 22), places);
        Assert.Contains(new UiOverlays.Apple2FieldPlace("sparkle.png", 20 + 166 + 14 - 5, 4 + 190 - 23 - 10), places);
        Assert.Null(run.At(run.Lines[3].EndMs + 1).Sparkle);
        var spell = run.At(run.Lines[4].StartMs + 1);
        Assert.Same(Ant, spell.Hit);
        Assert.False(spell.HitRock);
        // the ant: the second rank (y 96), alone: byte column 29 → (7 * 29 + 3, 96 + 20)
        Assert.Contains(new UiOverlays.Apple2FieldPlace("hit.png", 20 + 206 - 1, 4 + 116 - 18),
            UiOverlays.Apple2PlotPlaces(Layout, Art, Snapshot(), spell));
    }

    [Fact]
    public void Deaths_TheHitOrcGoes_NotItsNamesake_TheMemberErasedAtOnce()
    {
        var run = Round();
        var afterOrc = run.At(run.Lines[2].StartMs + 1);
        Assert.Equal([Orc2], afterOrc.Gone);                       // the orc A hit, not the first "ORC"
        var places = UiOverlays.Apple2PlotPlaces(Layout, Art, Snapshot(), afterOrc);
        Assert.Single(places, p => p.File == "orc-idle.png");
        Assert.All(places.Where(p => p.File.StartsWith("orc", StringComparison.Ordinal)), p => Assert.True(p.Opaque));
        Assert.Contains(A, afterOrc.Swung);                        // arm 22
        Assert.Contains(new UiOverlays.Apple2FieldPlace("19.attacking", 20 + 147 + 1, 4 + 174 - 25), places); // the front row
        var before = run.At(run.Lines[10].StartMs - 1);
        Assert.Contains(before.Window, l => l == GameText.F("core.combat.x_takes_x_damage", "D", 12));
        Assert.Contains(UiOverlays.Apple2PlotPlaces(Layout, Art, Snapshot(), before), p => p.X == 20 + 204 + 3);
        var end = run.At(run.LengthMs + 1);
        Assert.Contains(D, end.MembersGone);                       // 4025: GOSUB 9100 at once
        Assert.Contains(Orc1, end.Posed);                          // pose B (2006)
        Assert.DoesNotContain(UiOverlays.Apple2PlotPlaces(Layout, Art, Snapshot(), end), p => p.X == 20 + 204 + 3);
    }

    [Fact]
    public void Window_HomesAtEachTag_ButTheDeathsLine_ThreeRowsOf22()
    {
        var run = Round();
        Assert.Equal([GameText.F("core.combat.x_hits", "A"), GameText.F("core.combat.x_takes_x_damage", "ORC", 9),
            GameText.F("core.combat.x_dies", "ORC")], run.At(run.Lines[2].StartMs).Window); // the death prints in the box
        Assert.Equal([GameText.F("core.p3.x_casts_x", "B", "FIREFLASH")], run.At(run.Lines[3].StartMs).Window);
        Assert.All(run.Lines.Select(l => run.At(l.StartMs)), st => Assert.True(st.Window.Count <= 3));
        Assert.All(run.Lines.Select(l => run.At(l.StartMs)), st => Assert.All(st.Window, row => Assert.True(row.Length <= 22)));
    }

    [Fact]
    public void Parry_Arm21FromTheRoundsStart_Kneeling_TheFrameFirst_GiantRow()
    {
        var run = Round(parrying: [A]);
        var places = UiOverlays.Apple2PlotPlaces(Layout, Art, Snapshot(parrying: [A]), run.At(-1));
        Assert.Equal("frame.png", places[0].File);
        Assert.Contains(places, p => p.File == "19.parrying");
        var kneel = Snapshot() with { Kneeling = new HashSet<Character>([A], ReferenceEqualityComparer.Instance) };
        var down = UiOverlays.Apple2PlotPlaces(Layout, Art, kneel, run.At(-1));
        Assert.Contains(new UiOverlays.Apple2FieldPlace("down.png", 20 + 147 + 2, 4 + 174 - 20), down);
        Assert.Contains(new UiOverlays.Apple2FieldPlace("arm20.png", 20 + 147, 4 + 174), down); // 9205 → 9220
        var giant = new Monster { Id = 70, Name = "GIANT", HitPoints = 50 };
        var giantPlaces = UiOverlays.Apple2PlotPlaces(Layout, Art, Snapshot(ranks: [[giant], [Ant]]), run.At(-1));
        Assert.Contains(new UiOverlays.Apple2FieldPlace("giant-idle.png", 20 + 203, 4 + 99, true), giantPlaces); // 1540
    }

    [Fact]
    public void Boxes_FollowOUTDOORS3_600To635()
    {
        var box = new Apple2FieldMemberBox(5, 8, 123, 140, P3Status.Okay, [0, 1, 2, 3, 0, 2]);
        var snapshot = Snapshot() with { Boxes = [box] };
        var cells = UiOverlays.Apple2PlotBoxCells(Plot.Boxes!, snapshot);
        Assert.Contains((4, 0, "A", true), cells);                    // the name, inverse (HTAB 5: column 4)
        Assert.Contains((5, 2, "5:8", false), cells);                 // a maximum below 10: one column right (605)
        Assert.Contains((5, 3, "123", false), cells);                 // hits of 100 or more: two rows (615)
        Assert.Contains((4, 4, ":140", false), cells);
        Assert.Contains((5, 5, "O", false), cells);                   // head
        Assert.Contains((4, 6, "I", false), cells);                   // left arm
        Assert.Contains((6, 6, "B", false), cells);                   // right arm
        Assert.Contains((5, 7, "G", false), cells);                   // torso
        Assert.Contains((6, 8, "B", false), cells);                   // right leg
        var status = UiText.S("p3combat.status.1").Trim();
        Assert.Contains((8, 5, status[0].ToString(), false), cells);  // the status word down column + 4
    }

    // ---- The pack and the presenter -----------------------------------------------------------------------------------

    private static GraphicsPack? P3ApplePack() =>
        GraphicsPackCatalog.Load(GameDataRepository.DefaultPathFor(GameId.P3)).Find("apple2");

    [OriginalDataFact]
    public void ApplePack_HasIIIsField_AndItsIndexGivesEveryPicture()
    {
        var pack = P3ApplePack()!;
        var layout = pack.Apple2Field!;
        var plot = layout.Plot!;
        Assert.Equal((20, 4, 9, 3, 17, 22), (layout.ScreenX, layout.ScreenY, layout.TextTop, layout.TextRows, layout.TextLeft, layout.Columns));
        Assert.Equal((147, 198, 8, 99, 21), (plot.PartyX, plot.PartyBaseY, plot.RankStep, plot.GiantY, plot.GiantShape));
        Assert.Equal([120, 96], plot.RankYs);
        Assert.Equal(((220, 104), (220, 105)), (plot.ArrowMiss, plot.ArrowMissDungeon));
        Assert.Null(layout.Rock);
        var art = pack.LoadApple2FieldArt()!;
        Assert.Equal(80, art.Monsters.Count);
        Assert.Contains(art.Monsters.Values, m => m.Shape == 21 && m.RankHeight == 42); // a giant
        foreach (var body in new[] { 19, 25, 26, 27, 4 })
            foreach (var pose in new[] { Apple2FieldArt.Idle, Apple2FieldArt.Attacking, Apple2FieldArt.Casting, Apple2FieldArt.Parrying })
                Assert.True(File.Exists(pack.ResolveFile(art.Figures[(body, pose)].File)), $"{body}.{pose}");
        foreach (var use in new[] { "arrow", "bow", "bow-fired", "body-down" })
            Assert.True(File.Exists(pack.ResolveFile(art.Effects[use].File)), use);
        foreach (var arm in new[] { 5, 20, 21, 22 })
            Assert.True(File.Exists(pack.ResolveFile(art.Shapes[arm].File)));
        Assert.True(File.Exists(pack.ResolveFile(art.Frame!.File)));
        Assert.True(File.Exists(pack.ResolveFile(art.Hits[1].File)));
        Assert.NotNull(art.Sparkle);
        Assert.All(art.Monsters.Values, m => Assert.True(File.Exists(pack.ResolveFile(m.Idle.File))
                                                         && File.Exists(pack.ResolveFile(m.Attacking.File))));
        Assert.Null(GraphicsPackCatalog.Load(GameDataRepository.DefaultPathFor(GameId.P3)).Find("amiga")?.Apple2Field);
    }

    [OriginalDataFact]
    public void Presenter_PlaysIIIsRoundsOnTheAppleField_ToTheFightsEnd()
    {
        var layout = P3ApplePack()!.Apple2Field!;
        var data = GameDataRepository.Load(GameDataRepository.DefaultPathFor(GameId.P3));
        var s = GameSessionFactory.Create(data, new SeededDosRandomSource(7));
        s.StartNewGame(null);
        s.OpenGuild();
        foreach (var (name, cls) in new[] { ("HERO0", CharacterClass.Fighter), ("HERO1", CharacterClass.Fighter) })
            Assert.True(s.TryAddToParty(s.CreateAndEnroll(name, Race.Human, cls), out var err), err);
        s.LeaveGuild();
        s.LeaveTown();
        s.BeginCombat(0, SurpriseFlags.Normal);
        var ui = new CombatPresenter(new ClientSettings()) { AppleField = () => layout };
        ui.Start(s);
        var parry = CombatPresenter.MenuChoices(s).ToList().IndexOf(CombatChoice.Parry) + 1;
        Assert.True(parry > 0);
        var rounds = 0;
        var ended = false;
        var member = 0;
        for (var step = 0; step < 3000 && !ended; step++)
        {
            if (ui.AppleRun is { } run)
            {
                rounds++;
                var snapshot = ui.AppleSnapshot!;
                Assert.Equal(snapshot.Party.Count, snapshot.Boxes.Count);   // III's snapshot (OfP3)
                if (run.Lines.Any(l => l.Text == GameText.F("core.p3.x_parries", "HERO1")))
                {
                    Assert.Contains(snapshot.Party[1], snapshot.Parrying); // arm 21 from the round's start
                }
                ended = ui.Update(s, run.LengthMs + 1, fastForward: false) && s.Mode != GameMode.Combat;
                Assert.Null(ui.AppleRun);
                member = 0;
            }
            else if (ui.Phase == CombatPhase.PartyOptions)
                ui.HandleKey(s, Keys.Enter);                     // FIGHT
            else if (ui.AsksRank)
                ui.HandleKey(s, Keys.D1);
            else if (ui.AwaitingOrders)
                ui.HandleKey(s, member++ % 2 == 0 ? Keys.D2 : Keys.D0 + parry);
            else
                ended |= ui.Update(s, 1, fastForward: false) && s.Mode != GameMode.Combat;
        }
        Assert.True(rounds > 0);
        Assert.True(ended);
        Assert.NotEqual(GameMode.Combat, s.Mode);
        Assert.Null(ui.FieldRun);                                 // the Amiga's field takes nothing
    }
}
