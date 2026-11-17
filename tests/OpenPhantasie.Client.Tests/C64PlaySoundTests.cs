using System.Text.Json;
using OpenPhantasie.Client.Audio;
using OpenPhantasie.Core;
using OpenPhantasie.Data;
using OpenPhantasie.Data.Audio;

namespace OpenPhantasie.Client.Tests;

/// <summary>
/// s228: the C64 packs' sounds in play (<c>sid_sounds</c>' <c>fight.*</c> / <c>town.ring</c>, I's
/// <c>reverse-engineering/c64/phantasie1/GRAPHICS.md</c> 9.8): each routine's writes as the programs make them, and which
/// moments of play make which (<see cref="C64PlaySoundCues"/>).
/// </summary>
public class C64PlaySoundTests
{
    private static SidSounds Load(GameId game)
    {
        var path = Path.Combine(GameDataRepository.DefaultPathFor(game), "graphics", "c64.json");
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        return SidSounds.FromJson(doc.RootElement.GetProperty("sid_sounds").GetRawText(), path);
    }

    private static readonly SidSounds I = Load(GameId.P1), II = Load(GameId.P2), III = Load(GameId.P3);

    private static readonly string[] Fight =
        ["alive", "awakens", "cast", "dies", "encounter", "mark", "miss", "monster_cast", "monster_hit", "wound", "hit"];

    private static double Rms(float[] stereo, double from, double to, int rate = TuneRenderer.DefaultSampleRate)
    {
        var a = (int)(from * rate);
        var b = Math.Min(stereo.Length / 2, (int)(to * rate));
        double sum = 0;
        for (var i = a; i < b; i++)
            sum += stereo[2 * i] * stereo[2 * i];
        return b > a ? Math.Sqrt(sum / (b - a)) : 0;
    }

    private static float[] Render(SidScore score) => new SidRenderer(score, Sid6581.PalClockHz).RenderAll();

    [Fact]
    public void Packs_HaveEveryTerrainsFightSounds_AndIHasTheRingTune()
    {
        foreach (var sid in new[] { I, II })
            foreach (var terrain in new[] { "outdoors", "dungeon" })
                foreach (var sound in Fight)
                    Assert.True(sid.Has($"fight.{terrain}.{sound}"), $"{terrain}.{sound}");
        Assert.True(I.Has("town.ring"));
        Assert.False(II.Has("town.ring")); // TOWN2 holds the routine, no statement calls it
        Assert.Equal(["fight.death", "fight.hit", "fight.miss", "fight.ping"], III.Names.Order(StringComparer.Ordinal));
        Assert.True(C64PlaySoundCues.HasFightSounds(I, GameId.P1) && C64PlaySoundCues.HasFightSounds(III, GameId.P3));
    }

    [Theory]
    [InlineData(1, false, 50)] // OUTDOORS 0x66c9
    [InlineData(2, false, 70)] // OUTDOORS2 0x6c1d
    [InlineData(1, true, 61)] // DUNGEON 0x6660
    [InlineData(2, true, 61)] // DUNGEON2 0x6bd1
    public void Dies_HoldsTheGateThroughThreeJiffyWaits(int game, bool dungeon, int jiffies)
    {
        var sid = game == 1 ? I : II;
        var writes = sid.Score(C64PlaySoundCues.Key(game == 1 ? GameId.P1 : GameId.P2, dungeon, "dies"), () => 0.5).Writes;
        var gates = writes.Where(w => w.Register == 4).ToList();
        Assert.Equal([21, 21, 21, 20], gates.Select(g => g.Value));
        for (var k = 1; k < 4; k++)
            Assert.Equal(jiffies / 60.0, gates[k].Seconds - gates[k - 1].Seconds, 5);
        Assert.Contains(writes, w => w is { Register: 15, Value: 4 }); // ring by voice 3 at freq hi 4
    }

    [Fact]
    public void Miss_HoldsItsNoise_250ToINT250PlusRndSpan_EmptyPasses()
    {
        static double Hold(SidSounds sid, string key, double r)
        {
            var w = sid.Score(key, () => r).Writes.Where(x => x.Register == 4).ToList();
            return w[1].Seconds - w[0].Seconds;
        }
        var shortest = Hold(I, "fight.outdoors.miss", 0);
        Assert.InRange(shortest, 250 * 0.00093, 250 * 0.00093 + 0.002);
        Assert.InRange(Hold(I, "fight.outdoors.miss", 0.999) - shortest, 299 * 0.00093 - 1e-6, 299 * 0.00093 + 1e-6);
        Assert.InRange(Hold(I, "fight.dungeon.alive", 0.999) - Hold(I, "fight.dungeon.alive", 0), 399 * 0.00093 - 1e-6, 399 * 0.00093 + 1e-6);
        var miss = I.Score("fight.outdoors.miss", () => 0.5).Writes;
        Assert.Contains(miss, w => w is { Register: 1, Value: 247 }); // 240 + INT(RND * 14)
        Assert.Contains(miss, w => w is { Register: 4, Value: 129 }); // noise, gate
    }

    [Fact]
    public void Awakens_PlaysElevenNotes_DrawnFromSUZeroToNine()
    {
        var r = new Random(3);
        var writes = I.Score("fight.outdoors.awakens", r.NextDouble).Writes;
        var his = writes.Where(w => w.Register == 1).ToList();
        var los = writes.Where(w => w.Register == 0).ToList();
        Assert.Equal(11, his.Count); // FOR O = 0 TO 10
        var drawn = his.Zip(los, (h, l) => h.Value * 256 + l.Value).ToList();
        // SU(0…9): C6 E6 G6 C7 D6 F6 A6 B6 D7 E7 (F7, SU(10), is never drawn)
        int[] su = [17167, 21629, 25721, 34334, 19269, 22915, 28871, 32407, 38539, 43258];
        Assert.All(drawn, n => Assert.Contains(n, su));
        Assert.Equal(22, writes.Count(w => w.Register == 4));
        // the first note: 17167 = $430F (C6), SU(0) at RND = 0
        var first = I.Score("fight.outdoors.awakens", () => 0).Writes;
        Assert.Contains(first, w => w is { Register: 1, Value: 0x43 });
        Assert.Contains(first, w => w is { Register: 0, Value: 0x0F });
    }

    [Fact]
    public void MonsterHit_FadesTheVolume15To0_InStepsOfATenth()
    {
        var volumes = II.Score("fight.dungeon.monster_hit", () => 0.5).Writes.Where(w => w.Register == 24).Select(w => w.Value).ToList();
        Assert.Equal(9 + 151, volumes.Count); // nine RND passes at 15, then FOR V = 15 TO 0 STEP -.1
        Assert.Equal([15, .. Enumerable.Range(0, 15).Reverse().SelectMany(v => Enumerable.Repeat(v, 10))], volumes.Skip(9));
    }

    [Fact]
    public void Cast_SweepsTheLoopValueInTheDungeons_AndZeroOutdoors_AndIsDungeonSkipsTheClear()
    {
        var dungeon = II.Score("fight.dungeon.cast", () => 0.5).Writes;
        var outdoors = II.Score("fight.outdoors.cast", () => 0.5).Writes;
        // 15 passes of six RND notes (50 + INT(RND * 6 * O)), then the sweep
        Assert.Equal(90, dungeon.Count(w => w.Register == 1 && w.Value >= 50));
        var sweep = dungeon.SkipWhile(w => !(w.Register == 4 && w.Value == 129)).TakeWhile(w => !(w.Register == 4 && w.Value == 128))
            .Where(w => w.Register == 1).Select(w => w.Value);
        Assert.Equal(Enumerable.Range(10, 33).Reverse(), sweep);
        Assert.All(outdoors.SkipWhile(w => !(w.Register == 4 && w.Value == 129)).TakeWhile(w => !(w.Register == 4 && w.Value == 128))
            .Where(w => w.Register == 1), w => Assert.Equal(0, w.Value));
        Assert.Equal((0, 0), (dungeon[0].Register, dungeon[0].Value)); // DUNGEON2 clears all 25
        var iDungeon = I.Score("fight.dungeon.cast", () => 0.5).Writes;
        Assert.Equal((20, 1), (iDungeon[0].Register, iDungeon[0].Value)); // DUNGEON pokes S + M, M unknown: left out
    }

    [Fact]
    public void RingTune_PlaysSixPassesOfEightNotes_TwoJiffiesApart()
    {
        var writes = I.Score("town.ring", () => 0.5).Writes;
        var notes = writes.Skip(25).Where(w => w.Register == 1).ToList(); // after the 25-register clear
        Assert.Equal(48, notes.Count);
        for (var k = 1; k < 48; k++)
            Assert.Equal(2 / 60.0, notes[k].Seconds - notes[k - 1].Seconds, 5);
        // I = 1: 4050 / 2 = 2025 = 7 * 256 + 233; I = 6: 2861 * 3 = 8583 = 33 * 256 + 135
        Assert.Equal(7, notes[0].Value);
        Assert.Equal(233, writes.First(w => w.Register == 15 && w.Seconds >= notes[0].Seconds).Value);
        Assert.Equal(33, notes[^1].Value);
        Assert.Equal((24, 0), (writes[^1].Register, writes[^1].Value));
        Assert.Equal(33 / 60.0, writes[^1].Seconds - notes[^1].Seconds, 5);
    }

    [Fact]
    public void P3Ping_GatesForSixCycles_AndTheDeathRingsTwoNotes()
    {
        var ping = III.Score("fight.ping", () => 0.5).Writes;
        var gates = ping.Where(w => w.Register == 4).ToList();
        Assert.Equal(9, gates.Count); // three calls: the off value, on, off
        Assert.Equal(6 / 985248.0, gates[2].Seconds - gates[1].Seconds, 9);
        Assert.Equal([0x90, 0xB0, 0x90], ping.Where(w => w.Register == 1).Select(w => w.Value));
        var death = III.Score("fight.death", () => 0.5).Writes.Where(w => w.Register == 0).Select(w => w.Value);
        Assert.Equal([0xB4, 0x30], death);
        var miss = III.Score("fight.miss", () => 0.5).Writes;
        Assert.Equal((4, 0x81), (miss[^1].Register, miss[^1].Value)); // the gate left on
        Assert.True(Rms(Render(III.Score("fight.miss", () => 0.5)), 0, 0.15) > 0.005);
    }

    [Fact]
    public void FightSounds_Sound()
    {
        foreach (var sound in new[] { "hit", "monster_hit", "miss", "encounter", "cast", "awakens", "wound", "dies" })
        {
            var score = I.Score("fight.outdoors." + sound, () => 0.5);
            var rms = Rms(Render(score), 0, Math.Max(0.2, score.Seconds - 2));
            Assert.True(rms > 0.002, $"{sound} {rms}");
        }
    }

    private static readonly Character Ann = new() { Name = "ANN" }, Bob = new() { Name = "BOB", Status = CharacterStatus.Dead },
        Cid = new() { Name = "CID", Status = CharacterStatus.Dead };
    private static readonly Monster Orc = new() { Name = "ORC", Id = 45 }, Rat = new() { Name = "RAT", Id = 0 };

    [OriginalDataFact] // the lines are the string tables' (the public tree has placeholders)
    public void Round_PlaysEachLinesSound_InTheProgramsOrder()
    {
        string[] log =
        [
            GameText.F("core.combat.x_hits", "ANN"), GameText.F("core.combat.x_takes_x_damage", "ORC", 3),
            GameText.F("core.combat.x_dies", "ORC"),
            GameText.F("core.combat.x_casts_a_spell", "ANN"), GameText.F("core.combat.x_takes_x_damage", "RAT", 2),
            GameText.F("core.combat.x_misses", "RAT"),
            GameText.F("core.combat.x_hits", "RAT"), GameText.F("core.combat.x_takes_x_damage", "ANN", 1),
            GameText.F("core.combat.x_breathes", "RAT"), GameText.F("core.combat.x_takes_x_damage", "BOB", 9),
            GameText.F("core.combat.x_dies", "BOB"),
        ];
        CombatLineTag[] tags =
        [
            new(0, Ann, null, CombatSwing.Hit), new(3, Ann, null), new(5, null, Rat, CombatSwing.Miss),
            new(6, null, Rat, CombatSwing.Hit), new(8, null, Rat),
        ];
        CombatHit[] hits =
        [
            new(DamageSource.PartyMelee, null, Orc, Line: 1), new(DamageSource.PartySpell, null, Rat, Line: 4, SpellDamage: 2),
            new(DamageSource.MonsterMelee, Ann, null, Line: 7), new(DamageSource.MonsterSpecial, Bob, null, Line: 9),
        ];
        var sounds = C64PlaySoundCues.ForRound(GameId.P1, log, tags, hits, [Ann, Bob], id => id == 0 ? 2 : 1);
        Assert.Equal(["hit", "encounter", "cast", "mark", "mark", "miss", "monster_hit", "wound", "monster_cast", "dies", "dies", "dies"],
            sounds);
    }

    [OriginalDataFact] // the lines are the string tables' (the public tree has placeholders)
    public void Round_P3_PingsBeforeTheAttackSpellsHits_AndRingsAMembersDeath()
    {
        string[] log = [GameText.F("core.combat.x_misses", "ANN"), "x", "y", GameText.F("core.combat.x_dies", "CID")];
        CombatLineTag[] tags = [new(0, Ann, null, CombatSwing.Miss)];
        CombatHit[] hits =
        [
            new(DamageSource.PartySpell, null, Orc, Line: 1, SpellDamage: 5, Spell: SpellId.FlameboltI),
            new(DamageSource.PartyMelee, null, Rat, Line: 2), new(DamageSource.MonsterMelee, Cid, null, Line: 3),
        ];
        Assert.Equal(["miss", "ping", "hit", "hit", "hit", "death"], C64PlaySoundCues.ForRound(GameId.P3, log, tags, hits, [Ann, Cid], _ => 1));
    }

    [OriginalDataFact] // Core's real rounds (I's data and string tables)
    public void RealRounds_SoundTheirSwingsHitsWoundsAndDeaths()
    {
        var seen = new HashSet<string>();
        for (var seed = 1; seed <= 25; seed++)
        {
            var session = ClientSessions.Fight(seed);
            for (var round = 0; round < 12 && session.Mode == GameMode.Combat; round++)
            {
                var before = session.Party.Members.ToList();
                for (var i = 0; i < session.Combat!.Prep.Length; i++)
                    session.Combat.Prep[i].PackedAction = CombatAction.Attack();
                var result = session.ResolveCombatRound();
                var sounds = C64PlaySoundCues.ForRound(GameId.P1, result, before, I.MarksFor);
                seen.UnionWith(sounds);
                for (var k = 0; k < sounds.Count; k++)
                    if (sounds[k] == "monster_hit") // OUTDOORS 0x2394, then 0x2e11: the wound's sound or DIES
                        Assert.True(k + 1 < sounds.Count && sounds[k + 1] is "wound" or "dies", string.Join(",", sounds));
                Assert.Equal(result.LineTags.Count(t => t.Swing == CombatSwing.Miss), sounds.Count(x => x == "miss"));
            }
        }
        Assert.Superset(new HashSet<string> { "hit", "miss", "monster_hit", "wound", "encounter" }, seen);
    }

    [OriginalDataFact] // the lines are the string tables' (the public tree has placeholders)
    public void Log_SwimsScriptsAndResurrections_OnTheMapOnly()
    {
        string[] lines =
        [
            GameText.F("core.dungeon.x_sinks_and_takes_x_damage", "ANN", 2), GameText.F("core.dungeon.swim_x_dies", "BOB"),
            GameText.F("core.magic.x_is_resurrected", "BOB"),
        ];
        Assert.Equal(["hit", "hit", "dies", "alive"], C64PlaySoundCues.ForLog(GameId.P1, GameMode.Overworld, lines));
        Assert.Empty(C64PlaySoundCues.ForLog(GameId.P1, GameMode.Town, lines)); // TOWNR: none
        Assert.Equal(["monster_cast", "monster_cast"], C64PlaySoundCues.ForLog(GameId.P2, GameMode.Dungeon,
            [GameText.S("core.script.smoke_fills_the_air"), GameText.S("core.script.the_party_is_moved")]));
    }

    [Fact]
    public void InTurn_StartsEachSoundAtTheLastWriteOfTheOneBefore()
    {
        var cues = C64PlaySoundCues.InTurn(I, ["fight.outdoors.hit", "fight.outdoors.dies", "nope", "fight.outdoors.hit"]);
        Assert.Equal(3, cues.Count);
        Assert.Equal(I.Length("fight.outdoors.hit"), cues[1].At, 9);
        Assert.Equal(I.Length("fight.outdoors.hit") + I.Length("fight.outdoors.dies"), cues[2].At, 9);
        Assert.Equal(40 / 60.0, I.Length("fight.outdoors.hit") - I.Score("fight.outdoors.hit", () => 0.5).Writes[^2].Seconds, 5);
        Assert.Equal(1, I.MarksFor(0));
        Assert.Equal(4, I.MarksFor(45));
        Assert.Equal(1, I.MarksFor(500));
    }
}
