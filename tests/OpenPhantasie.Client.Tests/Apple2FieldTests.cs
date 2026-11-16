using Microsoft.Xna.Framework.Input;
using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client.Tests;

/// <summary>
/// s224: Phantasie I Apple II's combat field (II's s226: the same field plus the tossed rock) (<see cref="Apple2FieldRun"/>, the pack's <c>apple2_field</c>;
/// <c>reverse-engineering/apple2/phantasie1/GRAPHICS.md</c> section 7): the places OUTDOORS / DUNGON 1480–1550 give, poses
/// that stay to the round's end, a slain monster erased at its line while a dying member stays drawn, the sparkle and the
/// spell hit, the text window's HOMEs, and the presenter's routing (the Amiga's field unchanged).
/// </summary>
public class Apple2FieldTests
{
    private static readonly Apple2FieldLayout Layout = new()
    {
        ScreenX = 20, ScreenY = 4, LoopMs = 1.0, SparkleMs = 300,
        Bodies = new Apple2FieldBodies(7, 26, [0, 2], 25, 19, [1, 4, 5], 27, 26),
    };

    private static readonly Character A = new() { Name = "A", Race = Race.Human, Class = CharacterClass.Fighter };
    private static readonly Character B = new() { Name = "B", Race = Race.Elf, Class = CharacterClass.Wizard };
    private static readonly Character C = new() { Name = "C", Race = Race.Dwarf, Class = CharacterClass.Priest };
    private static readonly Monster Orc1 = new() { Id = 8, Name = "ORC", HitPoints = 5 };
    private static readonly Monster Orc2 = new() { Id = 8, Name = "ORC", HitPoints = 0 }; // slain in the round
    private static readonly Monster Ant = new() { Id = 4, Name = "GIANT ANT", HitPoints = 5 };

    private static Apple2FieldSnapshot Snapshot(IEnumerable<Character>? dead = null, IEnumerable<Character>? parrying = null) =>
        new([A, B, C], new HashSet<Character>(dead ?? [], ReferenceEqualityComparer.Instance),
            new HashSet<Character>(parrying ?? [], ReferenceEqualityComparer.Instance), null, [[Orc1, Orc2], [Ant]]);

    [Fact]
    public void Ranks_StackByTheRankHeights_TheThirdNeverAboveTheTop()
    {
        Assert.Equal([135, 113, 83], Layout.RankYs([22, 30, 40]));   // 1480-1500
        Assert.Equal([135, 35, 0], Layout.RankYs([100, 60, 10]));    // the third: X * (X > 0)
        Assert.Equal([135, -5], Layout.RankYs([140, 10]));           // the second is not clamped
        Assert.Equal(new[] { 3, 43, 83 }, Enumerable.Range(0, 3).Select(i => Layout.MonsterX(3, i, 3))); // 1550
        Assert.Equal(63, Layout.MonsterX(3, 1, 2));
    }

    [Theory]
    [InlineData(Race.Human, CharacterClass.Fighter, 19)]
    [InlineData(Race.Human, CharacterClass.Thief, 19)]
    [InlineData(Race.Human, CharacterClass.Monk, 27)]
    [InlineData(Race.Elf, CharacterClass.Wizard, 27)]
    [InlineData(Race.Elf, CharacterClass.Priest, 27)]
    [InlineData(Race.Dwarf, CharacterClass.Wizard, 25)]
    [InlineData(Race.Sprite, CharacterClass.Ranger, 25)]
    [InlineData(Race.Ogre, CharacterClass.Fighter, 26)]
    [InlineData(Race.Undead, CharacterClass.Priest, 26)]
    public void Bodies_FollowTOWN2350(Race race, CharacterClass cls, int body) =>
        Assert.Equal(body, Layout.Bodies.For((int)race, (int)cls));

    /// <summary>A swings at Orc 2 and kills it; B casts a damage spell on the ant; Orc 1 hits C, who dies.</summary>
    private static Apple2FieldRun Round(int timeLag = 5, bool sound = true)
    {
        string[] log =
        [
            GameText.F("core.combat.x_hits", "A"), GameText.F("core.combat.x_takes_x_damage", "ORC", 9),
            GameText.F("core.combat.x_dies", "ORC"),
            GameText.F("core.combat.x_casts_a_spell", "B"), GameText.F("core.combat.x_takes_x_damage", "GIANT ANT", 4),
            GameText.F("core.combat.x_hits", "ORC"), GameText.F("core.combat.x_takes_x_damage", "C", 12),
            GameText.F("core.combat.x_dies", "C"),
        ];
        CombatLineTag[] tags =
        [
            new(0, A, null), new(0, A, null, CombatSwing.Hit), new(3, B, null), new(5, null, Orc1),
            new(5, null, Orc1, CombatSwing.Hit),
        ];
        CombatHit[] hits = [new(DamageSource.PartyMelee, A, Orc2, Line: 1), new(DamageSource.PartySpell, B, Ant, Line: 4, SpellDamage: 4)];
        return Apple2FieldRun.Plan(Layout, log, tags, hits, ["VICTORY"], Snapshot(), timeLag, sound);
    }

    [Fact]
    public void Lines_PauseByTimeLag_AHitsLineGoesStraightToItsDamage()
    {
        var run = Round();
        Assert.Equal(9, run.Lines.Count);
        Assert.Equal((0.0, 0.0), (run.Lines[0].StartMs, run.Lines[0].EndMs));       // "A HITS": 1710 -> 4120 at once
        Assert.Equal(0, run.Lines[5].EndMs - run.Lines[5].StartMs);                 // "ORC HITS": 1925 -> 4005 at once
        Assert.Equal(600, run.Lines[1].EndMs);                                      // PA = 120 * 5 loops of 1 ms [~]
        Assert.Equal(300, run.Lines[3].EndMs - run.Lines[3].StartMs);               // the cast's tones (sparkle) [?]
        Assert.Equal(500 + 600, run.Lines[4].EndMs - run.Lines[4].StartMs);         // the spell hit's 500 loops, then PA
        Assert.Equal(0, Round(timeLag: 0).Lines[1].EndMs);                          // TIME LAG 0: no pause (PA = 0)
        Assert.Equal(0, Round(sound: false).Lines[3].EndMs - Round(sound: false).Lines[3].StartMs); // 7570: NS returns
    }

    [Fact]
    public void Poses_StayToTheRoundsEnd_TheSlainErased_TheDyingMemberStays()
    {
        var run = Round();
        var start = run.At(0);
        Assert.Contains(A, start.Swung);                        // arm 22 from the swing (1695)
        Assert.Empty(start.Gone);
        var end = run.At(run.LengthMs + 1000);
        Assert.Contains(A, end.Swung);                          // still: only the next round's 1525 redraws it
        Assert.Contains(B, end.Cast);                           // arm 21 (1760)
        Assert.Contains(Orc1, end.Posed);                       // pose B (2006)
        Assert.Equal([Orc2], end.Gone);                         // erased at its DIES (4145)
        var places = UiOverlays.Apple2FieldPlaces(Layout, Art, Snapshot(), end);
        Assert.Equal(3, places.Count(p => p.File.StartsWith("figures/", StringComparison.Ordinal))); // C still drawn (4020)
        Assert.DoesNotContain(places, p => p.File == "orc-idle.png");
        Assert.Single(places, p => p.File == "orc-b.png");      // Orc 1 posed, Orc 2 gone
    }

    [Fact]
    public void Sparkle_OverTheCaster_DuringTheTones_TheHitOnItsMonsterAtItsScale()
    {
        var run = Round();
        var cast = run.Lines[3];
        Assert.Same(B, run.At(cast.StartMs + 1).Sparkle);
        Assert.Null(run.At(cast.EndMs + 1).Sparkle);
        var damage = run.Lines[4];
        var state = run.At(damage.StartMs + 1);
        Assert.Same(Ant, state.Hit);
        Assert.Null(run.At(damage.StartMs + 501).Hit);
        var places = UiOverlays.Apple2FieldPlaces(Layout, Art, Snapshot(), state);
        // the ant: H% 10, SCALE 2, the second rank at 135 - 22 (the orcs' YS%); its hit drawn at SCALE 2
        Assert.Contains(new UiOverlays.Apple2FieldPlace("hit-2.png", 20 + 10 - 2, 4 + 113 - 9), places);
        Assert.Contains(new UiOverlays.Apple2FieldPlace("ant-idle.png", 20 + 10 - 1, 4 + 113 - 8), places);
        var sparkle = UiOverlays.Apple2FieldPlaces(Layout, Art, Snapshot(), run.At(cast.StartMs + 1))
            .Single(p => p.File == "sparkle.png");
        Assert.Equal((20 + 19 + 19 - 5, 4 + 136 - 10), (sparkle.X, sparkle.Y)); // slot 1: (19 + 19, 136) (1762)
    }

    [Fact]
    public void Places_FollowTheDrawPoints_DeadAtTheStartLeftOut_AParryShowsArm21()
    {
        var places = UiOverlays.Apple2FieldPlaces(Layout, Art, Snapshot(dead: [B], parrying: [C]), Round().At(-1));
        var figures = places.Where(p => p.File.StartsWith("figures/", StringComparison.Ordinal)).ToList();
        Assert.Equal(["figures/19.idle", "figures/25.casting"], figures.Select(f => f.File)); // B's slot stays empty
        Assert.Equal((20 + 0 + 5, 4 + 158 - 20), (figures[0].X, figures[0].Y));
        Assert.Equal(20 + 38 + 5, figures[1].X);                                             // slot 2 at 19 * 2
        var orcs = places.Where(p => p.File == "orc-idle.png").ToList();
        Assert.Equal([20 + 3 - 0 + 0, 20 + 3 + 60], orcs.Select(o => o.X));                   // 3 + i * 120 / 2
        Assert.All(orcs, o => Assert.Equal(4 + 135 - 20, o.Y));
    }

    [Fact]
    public void Window_HomesAtEachTurnAndSwing_KeepsTheLastFourRows()
    {
        var run = Round();
        Assert.Equal([GameText.F("core.combat.x_hits", "A"), GameText.F("core.combat.x_takes_x_damage", "ORC", 9),
            GameText.F("core.combat.x_dies", "ORC")], run.At(run.Lines[2].StartMs).Window);
        Assert.Equal([GameText.F("core.combat.x_casts_a_spell", "B")], run.At(run.Lines[3].StartMs).Window); // 1610 HOME
        Assert.Equal(GameText.F("core.combat.x_hits", "ORC"), run.At(run.Lines[5].StartMs).Window[0]);       // 1860 HOME
        // an untagged cast line (no caster tag) is matched by the caster's name
        var untagged = Apple2FieldRun.Plan(Layout, [GameText.F("core.combat.x_casts_a_spell", "B")], [], [], [], Snapshot(), 5, true);
        Assert.Contains(B, untagged.At(1).Cast);
        Assert.Equal(["ABCD EFGH", "IJ"], Apple2FieldRun.Wrap("ABCD EFGH IJ", 9));
        Assert.Equal(["ABCDEFGHI", "JK"], Apple2FieldRun.Wrap("ABCDEFGHIJK", 9));
    }

    private static readonly Apple2FieldArt Art = new(
        new Dictionary<int, Apple2FieldMonster>
        {
            [8] = new(3, 1, 22, new("orc-idle.png", 0, 20), new("orc-b.png", 0, 19)),
            [4] = new(10, 2, 30, new("ant-idle.png", 1, 8), new("ant-b.png", 1, 8)),
        },
        new Dictionary<(int, string), Apple2FieldPicture>
        {
            [(19, "idle")] = new("figures/19.idle", -5, 20), [(19, "attacking")] = new("figures/19.attacking", -5, 23),
            [(27, "idle")] = new("figures/27.idle", -5, 20), [(27, "casting")] = new("figures/27.casting", 1, 23),
            [(25, "idle")] = new("figures/25.idle", -5, 20), [(25, "casting")] = new("figures/25.casting", -5, 20),
        },
        new Apple2FieldPicture("sparkle.png", 5, 10),
        new Dictionary<int, Apple2FieldPicture> { [1] = new("hit-1.png", 1, 18), [2] = new("hit-2.png", 2, 9) });

    [OriginalDataFact]
    public void ApplePack_HasTheField_AndItsIndexGivesEveryMonsterAndFigure()
    {
        var pack = GraphicsPackCatalog.Load(GameDataRepository.DefaultPhantasie1Path).Find("apple2")!;
        var layout = pack.Apple2Field!;
        Assert.Equal((20, 4, 138, 158, 135, 120), (layout.ScreenX, layout.ScreenY, layout.Divider, layout.PartyY, layout.RankY, layout.Spread));
        Assert.Equal(3, layout.Lines.Count);
        var art = pack.LoadApple2FieldArt()!;
        Assert.Equal(80, art.Monsters.Count);
        Assert.Equal((3, 1, 22), (art.Monsters[0].X, art.Monsters[0].Scale, art.Monsters[0].RankHeight)); // SKELETON's MN record
        foreach (var body in new[] { 19, 25, 26, 27 })
            foreach (var pose in new[] { Apple2FieldArt.Idle, Apple2FieldArt.Attacking, Apple2FieldArt.Casting })
                Assert.True(File.Exists(pack.ResolveFile(art.Figures[(body, pose)].File)));
        Assert.NotNull(art.Sparkle);
        Assert.All(art.Monsters.Values, m => Assert.True(art.Hits.ContainsKey(m.Scale), $"no spell hit at SCALE {m.Scale}"));
        Assert.All(art.Hits.Values, h => Assert.True(File.Exists(pack.ResolveFile(h.File))));
        // the other I packs have no Apple field
        Assert.Null(GraphicsPackCatalog.Load(GameDataRepository.DefaultPhantasie1Path).Find("amiga")?.Apple2Field);
    }

    [OriginalDataFact]
    public void Presenter_PlaysEachRoundOnTheAppleField_ToTheFightsEnd()
    {
        var s = ClientSessions.Overworld();
        s.BeginWildernessCombat(ClientSessions.Data.Monsters.First(m => m.HpBase <= 8).Id, SurpriseFlags.MonstersSurprised);
        var ui = new CombatPresenter(new ClientSettings()) { AppleField = () => Layout };
        ui.Start(s);
        ui.Update(s, 1, fastForward: false);                     // round 1 at once (no Party Options)
        Assert.NotNull(ui.AppleSnapshot);
        Assert.NotNull(ui.AppleShown);
        Assert.Null(ui.P1Run);
        Assert.Null(ui.FieldRun);
        Apple2FieldRun? last = null;
        var ended = false;
        for (var step = 0; step < 400 && !ended; step++)
        {
            if (ui.AppleRun is { } run)
            {
                last = run;
                ended = ui.Update(s, run.LengthMs + 1, fastForward: false) && s.Mode != GameMode.Combat;
                Assert.Null(ui.AppleRun);                        // the round's run ends with its last line
                if (s.Mode == GameMode.Combat)
                    Assert.Equal(CombatPhase.PartyOptions, ui.Phase);
            }
            else if (ui.Phase == CombatPhase.PartyOptions)
                ui.HandleKey(s, Keys.Enter);                     // FIGHT
            else if (ui.AsksRank)
                ui.HandleKey(s, Keys.D1);
            else if (ui.AwaitingOrders)
                ui.HandleKey(s, Keys.D2);                        // ATTACK
            else
                ended |= ui.Update(s, 1, fastForward: false) && s.Mode != GameMode.Combat;
        }
        Assert.True(ended);
        Assert.NotEqual(GameMode.Combat, s.Mode);
        Assert.NotNull(last);                                     // (a fight can also end at the morale check: MONSTERS RUN)
        Assert.Null(ui.AppleRun);
        Assert.False(ui.IsPresenting);
    }

    [OriginalDataFact]
    public void Presenter_WithoutTheSwitch_OrOnTheAmigaField_NoAppleRun()
    {
        var s = ClientSessions.Overworld();
        s.BeginWildernessCombat(ClientSessions.Data.Monsters.First(m => m.HpBase <= 8).Id, SurpriseFlags.MonstersSurprised);
        var off = new CombatPresenter(new ClientSettings()) { AppleField = () => null };
        off.Start(s);
        off.Update(s, 1, fastForward: false);
        Assert.Null(off.AppleRun);

        var t = ClientSessions.Overworld();
        t.BeginWildernessCombat(ClientSessions.Data.Monsters.First(m => m.HpBase <= 8).Id, SurpriseFlags.MonstersSurprised);
        var amiga = GraphicsPackCatalog.Load(GameDataRepository.DefaultPathFor(GameId.P1)).Find("amiga")!.CombatField!;
        var both = new CombatPresenter(new ClientSettings())
        {
            P1Layout = amiga, AmigaP1Field = () => true, P1SoundSeconds = _ => 0, AppleField = () => Layout,
        };
        both.Start(t);
        both.Update(t, 1, fastForward: false);
        Assert.NotNull(both.P1Run);                              // the Amiga's field keeps its route
        Assert.Null(both.AppleRun);
    }

    // ---- II (s226): the same field, plus the tossed rock (OUTDOORS2 / DUNGON2 1623-1628, 4132) -------------------------

    private static readonly Apple2FieldLayout P2Layout = Layout with { Rock = new Apple2FieldRock(70, 20, 250, 500) };

    private static readonly Apple2FieldArt P2Art = Art with
    {
        RockHits = new Dictionary<int, Apple2FieldPicture> { [1] = new("rock-1.png", 1, 3), [2] = new("rock-2.png", 2, 6) },
    };

    /// <summary>A throws a rock and misses; B throws one that hits the ant (SCALE 2); C swings and misses.</summary>
    private static Apple2FieldRun RockRound(Apple2FieldLayout layout)
    {
        string[] log =
        [
            GameText.F("core.combat.x_throws_a_rock", "A"), GameText.F("core.combat.x_misses", "A"),
            GameText.F("core.combat.x_throws_a_rock", "B"), GameText.F("core.combat.x_takes_x_damage", "GIANT ANT", 3),
            GameText.F("core.combat.x_misses", "C"),
        ];
        CombatLineTag[] tags = [new(4, C, null), new(4, C, null, CombatSwing.Miss)];
        CombatHit[] hits = [new(DamageSource.PartyRock, B, Ant, Line: 3, SpellDamage: 3)];
        return Apple2FieldRun.Plan(layout, log, tags, hits, [], Snapshot(), 5, true);
    }

    [OriginalDataFact]
    public void P2_Rock_ThrowIsATurnWithArm21_AMissFliesAtTheMissPoint_AHitOnTheMonsterAtItsScale()
    {
        var run = RockRound(P2Layout);
        Assert.Equal(0, run.Lines[0].EndMs - run.Lines[0].StartMs);        // 1623: no pause before the roll
        Assert.Equal(250 + 600, run.Lines[1].EndMs - run.Lines[1].StartMs); // 1628: 250 loops, then 4150's PA
        Assert.Equal(0, run.Lines[2].EndMs - run.Lines[2].StartMs);
        Assert.Equal(500 + 600, run.Lines[3].EndMs - run.Lines[3].StartMs); // 4132: 500 loops (SP = 2), then PA
        Assert.Equal(600, run.Lines[4].EndMs - run.Lines[4].StartMs);       // C's melee miss: only PA, no rock

        var thrown = run.At(run.Lines[0].StartMs);
        Assert.Contains(A, thrown.Cast);                                    // GOSUB 1760: arm 21
        Assert.Null(thrown.Sparkle);                                        // 1760 RETURNs: no sparkle for a rock
        Assert.Equal(GameText.F("core.combat.x_throws_a_rock", "A"), thrown.Window[0]); // 1610 HOME (its MISSES follows at once)

        var miss = run.At(run.Lines[1].StartMs + 1);
        Assert.True(miss.RockMiss);
        Assert.Contains(new UiOverlays.Apple2FieldPlace("rock-1.png", 20 + 70 - 1, 4 + 20 - 3),
            UiOverlays.Apple2FieldPlaces(P2Layout, P2Art, Snapshot(), miss));
        Assert.False(run.At(run.Lines[1].StartMs + 251).RockMiss);

        var hit = run.At(run.Lines[3].StartMs + 1);
        Assert.Same(Ant, hit.Hit);
        Assert.True(hit.HitRock);
        var places = UiOverlays.Apple2FieldPlaces(P2Layout, P2Art, Snapshot(), hit);
        Assert.Contains(new UiOverlays.Apple2FieldPlace("rock-2.png", 20 + 10 - 2, 4 + 113 - 6), places); // ant: H% 10, rank 2
        Assert.DoesNotContain(places, p => p.File.StartsWith("hit-", StringComparison.Ordinal));
        Assert.Contains(B, run.At(run.LengthMs + 1).Cast);                  // arm 21 to the round's end
        Assert.False(run.At(run.Lines[4].StartMs + 1).RockMiss);            // a melee MISSES is not a rock
    }

    [OriginalDataFact]
    public void I_HasNoRock_TheThrowLineIsPlainText()
    {
        var run = RockRound(Layout); // I's layout: no rock block
        Assert.All(run.Lines.Select(l => run.At(l.StartMs + 1)), st => Assert.False(st.RockMiss || st.HitRock));
        Assert.DoesNotContain(A, run.At(run.LengthMs + 1).Cast);
    }

    [OriginalDataFact]
    public void P2_ApplePack_HasTheFieldWithTheRock_AndItsIndexCoversEveryMonsterAndScale()
    {
        var packs = GraphicsPackCatalog.Load(GameDataRepository.DefaultPathFor(GameId.P2));
        var pack = packs.Find("apple2")!;
        var layout = pack.Apple2Field!;
        var i = GraphicsPackCatalog.Load(GameDataRepository.DefaultPhantasie1Path).Find("apple2")!.Apple2Field!;
        Assert.Equal(new Apple2FieldRock(70, 20, 250, 500), layout.Rock);
        Assert.Null(i.Rock);
        Assert.Equal((i.ScreenX, i.ScreenY, i.Divider, i.PartyStep, i.PartyY, i.RankY, i.Spread, i.SparkleDx, i.SparkleY, i.HitLoops),
            (layout.ScreenX, layout.ScreenY, layout.Divider, layout.PartyStep, layout.PartyY, layout.RankY, layout.Spread,
                layout.SparkleDx, layout.SparkleY, layout.HitLoops));                   // I's numbers but the rock
        Assert.Equal(i.Lines, layout.Lines);
        Assert.Equal(i.Bodies.RobedClasses, layout.Bodies.RobedClasses);
        Assert.Equal(i.Bodies.ElementalBody, layout.Bodies.ElementalBody);             // CHAR2 37-39: 26
        var art = pack.LoadApple2FieldArt()!;
        Assert.Equal(80, art.Monsters.Count);
        var data = GameDataRepository.Load(GameDataRepository.DefaultPathFor(GameId.P2));
        Assert.All(data.Monsters, m => Assert.True(art.Monsters.ContainsKey(m.Id), $"monster {m.Id} has no field art"));
        Assert.Equal(("SKELETON", 22), (data.Monsters[0].Name, art.Monsters[0].RankHeight)); // MN20 record 0: YS% 22
        foreach (var body in new[] { 19, 25, 26, 27 })
            foreach (var pose in new[] { Apple2FieldArt.Idle, Apple2FieldArt.Attacking, Apple2FieldArt.Casting })
                Assert.True(File.Exists(pack.ResolveFile(art.Figures[(body, pose)].File)));
        Assert.NotNull(art.Sparkle);
        Assert.All(art.Monsters.Values, m => Assert.True(art.Hits.ContainsKey(m.Scale) && art.RockHits.ContainsKey(m.Scale),
            $"no hit / rock at SCALE {m.Scale}"));
        Assert.All(art.Hits.Values.Concat(art.RockHits.Values), h => Assert.True(File.Exists(pack.ResolveFile(h.File))));
        Assert.Null(packs.Find("atari-st")?.Apple2Field);
    }

    [OriginalDataFact]
    public void P2_Presenter_PlaysTossRoundsOnTheAppleField_ToTheFightsEnd()
    {
        var data = GameDataRepository.Load(GameDataRepository.DefaultPathFor(GameId.P2));
        var s = GameSessionFactory.Create(data, new SeededDosRandomSource(7));
        s.StartNewGame(null);
        s.OpenGuild();
        foreach (var (name, cls) in new[] { ("HERO0", CharacterClass.Fighter), ("HERO1", CharacterClass.Thief) })
            Assert.True(s.TryAddToParty(s.CreateAndEnroll(name, Race.Human, cls), out var err), err);
        s.LeaveGuild();
        s.LeaveTown();
        s.BeginCombat(0, SurpriseFlags.Normal);
        var ui = new CombatPresenter(new ClientSettings()) { AppleField = () => P2Layout };
        ui.Start(s);
        var toss = CombatPresenter.MenuChoices(s).ToList().IndexOf(CombatChoice.Toss) + 1;
        Assert.True(toss > 0);
        var thrown = false;
        var ended = false;
        var rounds = 0;
        for (var step = 0; step < 2000 && !ended; step++)
        {
            if (ui.AppleRun is { } run)
            {
                rounds++;
                thrown |= run.Lines.Any(l => l.Text.EndsWith("THROWS A ROCK", StringComparison.Ordinal))
                          && run.Lines.Any(l => run.At(l.StartMs + 1) is { } st && (st.RockMiss || st.HitRock));
                ended = ui.Update(s, run.LengthMs + 1, fastForward: false) && s.Mode != GameMode.Combat;
            }
            else if (ui.Phase == CombatPhase.PartyOptions)
                ui.HandleKey(s, Keys.Enter);                     // FIGHT
            else if (ui.AsksRank)
                ui.HandleKey(s, Keys.D1);
            else if (ui.AwaitingOrders)
                ui.HandleKey(s, Keys.D0 + toss);                 // TOSS
            else
                ended |= ui.Update(s, 1, fastForward: false) && s.Mode != GameMode.Combat;
        }
        Assert.True(rounds > 0);
        Assert.True(thrown);
        Assert.True(ended);
        Assert.NotEqual(GameMode.Combat, s.Mode);
        Assert.Null(ui.P2Run);                                   // II's ST line run does not take the Apple's rounds
    }
}
