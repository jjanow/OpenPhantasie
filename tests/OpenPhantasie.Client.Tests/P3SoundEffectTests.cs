using OpenPhantasie.Client.Audio;
using OpenPhantasie.Core;
using OpenPhantasie.Data;
using OpenPhantasie.Data.Audio;

namespace OpenPhantasie.Client.Tests;

/// <summary>
/// s110, III Amiga: the sound effects (<c>0xb126(n)</c> → the music driver's start <c>0x1f52</c> with three streams from
/// table <c>0x23e1a</c>; FORMAT.md §7.2), sound 15 on the reward flashes, and the flash as a fight's spell effect
/// (<c>0x1833e</c> / <c>0x17e16</c> with their flash flag; FORMAT.md §2.4) in the combat palette.
/// </summary>
public class P3SoundEffectTests
{
    private static GraphicsPack Amiga =>
        GraphicsPackCatalog.Load(GameDataRepository.DefaultPathFor(GameId.P3)).Find("amiga")
        ?? throw new InvalidOperationException("III Amiga extracts missing");

    private static P3SoundEffects Effects => Amiga.SoundEffects!;

    private static byte[] Program => Effects.TryLoadImage() ?? throw new InvalidOperationException("PhantasieIII missing");

    [OriginalDataFact]
    public void Pack_SixteenSounds_EveryThirdEntry_33IsTheStop()
    {
        var fx = Effects;
        Assert.Equal(Enumerable.Range(1, 16).Select(i => 3 * i), fx.Sounds.Keys.Order());
        Assert.True(fx.IsStop(33));
        Assert.False(fx.IsStop(15));
        Assert.Equal([0x29b8c, 0x29b9a, 0x29ba6], fx.Sounds[15].Select(v => v!.Value));
        Assert.Equal([0x29ce6, null, null], fx.Sounds[12]); // a 0 pointer: a silent voice
        Assert.EndsWith("PhantasieIII/PhantasieIII", fx.FilePath.Replace('\\', '/'));
        Assert.Equal((60, 3579545), (fx.VblHz, fx.ClockHz));
    }

    [OriginalDataFact]
    public void Sound15_IsTheSameNoiseBurst_OnAllThreeVoices()
    {
        // Flat 0x23b54 (file + 0x6038): e2 02 (tempo 2), c7 0e (row 14), e5 0e (noise), c4 00 f8, note 0x32 for 5, rest 15.
        var p = Program;
        byte[] voice0 = [0xE2, 0x02, 0xC7, 0x0E, 0xE5, 0x0E, 0xC4, 0x00, 0xF8, 0x32, 0x05, 0x00, 0x0F, 0xFF];
        Assert.Equal(voice0, p.AsSpan(0x29b8c, 14).ToArray());
        Assert.Equal(voice0[2..], p.AsSpan(0x29b9a, 12).ToArray());
        Assert.Equal(voice0[2..], p.AsSpan(0x29ba6, 12).ToArray());
    }

    [OriginalDataFact]
    public void Sound15_Score_Noise_Period1524_FullVolume_ThenRelease836_45Vbls()
    {
        var s = Effects.Score(Program, [(0, 15)])!;
        for (var ch = 0; ch < 3; ch++)
        {
            Assert.Contains(new PaulaWrite(0, ch, PaulaWriteKind.Sample, P3MusicScore.WaveNoise), s.Writes);
            Assert.Contains(new PaulaWrite(0, ch, PaulaWriteKind.Period, 1524), s.Writes); // 0x21ed0[0x32 − 9]
            // Attack and decay 0: the ceiling (row 14, 15053), then at once the sustain 16128 (above it): volume 63.
            Assert.Contains(new PaulaWrite(0, ch, PaulaWriteKind.Volume, 63), s.Writes);
        }
        // The note holds (5 + 1)·2 = 12 VBLs; release 836 a VBL (row 14, nibble 8) from 16128.
        Assert.Contains(new PaulaWrite(11, 0, PaulaWriteKind.Volume, (16128 - 836) >> 8), s.Writes);
        // The rest holds 32 VBLs (frames 12-43), 0xFF read on 44, DMA off on 45: 0.75 s at 60 Hz.
        Assert.Equal(45, s.Frames);
        Assert.DoesNotContain(s.Writes, w => w.Channel == 3); // no unknown command
    }

    [OriginalDataFact]
    public void Sound15_AgainMidway_CutsTheFirst_AndStartsAfresh()
    {
        var s = Effects.Score(Program, [(0, 15), (6, 15)])!;
        Assert.Equal(6 + 45, s.Frames);
        for (var ch = 0; ch < 3; ch++)
        {
            Assert.Contains(new PaulaWrite(6, ch, PaulaWriteKind.DmaOff, 0), s.Writes);  // 0x1f52 clears the channel
            Assert.Contains(new PaulaWrite(6, ch, PaulaWriteKind.Period, 1524), s.Writes); // and the note starts again
        }
    }

    [OriginalDataFact]
    public void Sound12_HasOneVoice_TheOtherTwoStaySilent()
    {
        var s = Effects.Score(Program, [(0, 12)])!;
        Assert.DoesNotContain(s.Writes, w => w.Channel is 1 or 2 && w.Kind == PaulaWriteKind.DmaOn);
        Assert.Contains(s.Writes, w => w.Channel == 0 && w.Kind == PaulaWriteKind.DmaOn);
    }

    [OriginalDataFact]
    public void EveryPackedSound_Decodes_WithKnownCommandsOnly()
    {
        foreach (var n in Effects.Sounds.Keys.Where(n => !Effects.IsStop(n)))
        {
            var s = Effects.Score(Program, [(0, n)])!;
            Assert.True(s.Frames is > 0 and < 600, $"sound {n}: {s.Frames} VBLs");
            Assert.False(s.Loops);
            Assert.DoesNotContain(s.Writes, w => w.Channel == 3);
        }
    }

    [OriginalDataFact]
    public void Render_Sound15_Is45VblsOfPcm_NotSilent()
    {
        var r = SoundEffectPlayer.Render(Effects, Program, [(0, 15)], 44100)!;
        Assert.Equal((long)Math.Round(45 * 44100 / 60.0), r.Length);
        var pcm = r.RenderAll();
        Assert.Contains(pcm, v => Math.Abs(v) > 0.05f);
    }

    // ---- The reward scene's sounds ---------------------------------------------------------------------------------------

    [OriginalDataFact]
    public void RewardFlash_Sound15_BeforeEachFlashsSecondFrame()
    {
        var f = Amiga.RewardScene!.Flash!;
        Assert.Equal((15, 1), (f.Sound, f.SoundFrame));
        var (rising, _) = f.Plan(_ => 0);
        var vbls = f.SoundVbls(rising);
        Assert.Equal(50, vbls.Count);
        // Flash 0 at speed 6 (7 VBLs a frame): sound on VBL 7; flash 1 starts at 21 → 28. At speed 1: every 6 VBLs.
        Assert.Equal([7, 28], vbls.Take(2));
        Assert.Equal(6, vbls[^1] - vbls[^2]);
        // As one driver run the sounds cut each other: the run ends 45 VBLs after the last start.
        var score = Effects.Score(Program, vbls.Select(v => (v, 15)))!;
        Assert.Equal(vbls[^1] + 45, score.Frames);
    }

    // ---- The flash as a combat spell effect -----------------------------------------------------------------------------

    private static CombatFlashLayout Combat => Amiga.CombatFlash!;

    [OriginalDataFact]
    public void CombatFlash_Speed6_7VblsAFrame_Sound15_InTheCombatPalette()
    {
        var c = Combat;
        Assert.Equal((6, 7, 21, 15, 1), (c.Wait, c.FrameVbl, c.FlashVbl, c.Sound, c.SoundFrame));
        var combat = Amiga.Palettes["combat"];
        // Registers 12-15 of heros.cmp's palette: red, orange, yellow, white.
        Assert.Equal(["#DB0000", "#DB9200", "#FFDB49", "#FFFFFF"], combat.Skip(12));
        var all = new HashSet<(int, int, int)>();
        var sizes = new List<(int, int)>();
        for (var i = 0; i < 3; i++)
        {
            var path = Amiga.Resolve(c.Pictures[i])!;
            Assert.EndsWith($"flash/combat/{37 + i}.png", path.Replace('\\', '/'));
            sizes.Add(TitleSplashTests.PngSize(path));
            using var png = File.OpenRead(path);
            all.UnionWith(TitleSplashTests.PngColours(png));
        }
        Assert.Equal([(16, 14), (32, 17), (32, 23)], sizes);
        Assert.Equal(new HashSet<(int, int, int)> { (0, 0, 0), (0xDB, 0, 0), (0xDB, 0x92, 0), (0xFF, 0xDB, 0x49), (0xFF, 0xFF, 0xFF) },
            all);
    }

    [OriginalDataTheory]
    [InlineData(0, 0, 16, 3)]   // slot 0: x = max(0, −1) + 2 bytes; y 146 on a Back portrait at 143
    [InlineData(2, 2, 8, 13)]   // slot 2: 11 + 2 = 13 bytes = 104 px, portrait at 96; Front portrait at 133
    [InlineData(1, 1, 8, 8)]    // Middle: 138
    [InlineData(6, 0, 0, 3)]    // slot 6: 35 + 2 = 37 → capped at 36 bytes = 288 px = the portrait's x
    public void CombatFlash_OnAMember_IsPlacedFromItsPortrait(int slot, int rank, int x, int y) =>
        Assert.Equal((x, y), Combat.PartyOffset(slot, rank, _ => 0));

    [OriginalDataFact]
    public void CombatFlash_OnAMonster_JittersFromItsPicture_GiantsTwoBytesRight()
    {
        Assert.Equal((16, 6 + 21), Combat.MonsterOffset(false, n => n - 1));
        Assert.Equal((16, 6), Combat.MonsterOffset(true, _ => 0)); // + 2 bytes
    }

    [OriginalDataFact]
    public void CombatFlashRun_FlashesSpellsAndTouches_NotSwings_OneAfterAnother()
    {
        var a = new Character { Name = "A" };
        var b = new Character { Name = "B" };
        var orc = new Monster { Id = 1, Name = "Orc" };
        CombatHit[] hits =
        [
            new(DamageSource.PartyMelee, null, orc),     // a swing: "hits!", no flash
            new(DamageSource.PartySpell, null, orc),     // 0x1833e flag set
            new(DamageSource.MonsterMelee, a, null),     // a plain swing: no flash
            new(DamageSource.MonsterMelee, b, null, Touch: true), // a stunning touch: flash
            new(DamageSource.MonsterSpecial, a, null),   // breath, a spell, Sleep: flash
        ];
        var party = new List<Character> { a, b };
        var run = CombatFlashRun.Plan(hits, Combat, m => party.IndexOf(m), _ => 0, _ => false, _ => 0)!;
        Assert.Equal([(null, orc), (b, null), (a, null)], run.Items.Select(f => (f.Member, f.Monster)));
        Assert.Equal([0, 21, 42], run.Items.Select(f => f.StartVbl));
        Assert.Equal(63, run.LengthVbl);
        Assert.Equal([(7, 15), (28, 15), (49, 15)], run.SoundPlays);
        Assert.Equal((run.Items[1], 1), run.At(21));
        Assert.Equal((run.Items[1], 3), run.At(41.5));
        Assert.Null(run.At(63));
        Assert.Null(CombatFlashRun.Plan([hits[0], hits[2]], Combat, m => party.IndexOf(m), _ => 0, _ => false, _ => 0));
    }

    [OriginalDataFact]
    public void GameSounds_PlayInTurn_EachAfterTheOneBefore_UnknownTakesNoTime()
    {
        var fx = Effects;
        var p = Program;
        var len3 = fx.Score(p, [(0, 3)])!.Frames;
        var len42 = fx.Score(p, [(0, 42)])!.Frames;
        Assert.True(len3 > 0 && len42 > 0);
        Assert.Equal([(0, 42), (len42, 3), (len42 + len3, 42)], SoundEffectPlayer.InTurn(fx, p, [42, 4, 3, 42]));
        Assert.Empty(SoundEffectPlayer.InTurn(fx, p, [1, 2]));
    }

    [Theory]
    [InlineData(GameId.P3, true, true, true)]
    [InlineData(GameId.P3, false, true, false)] // the Sound switch off
    [InlineData(GameId.P3, true, false, false)] // a pack without sounds (DOS art)
    [InlineData(GameId.P1, true, true, false)]
    public void GameSounds_OnlyIII_WithTheSwitchAndThePack(GameId game, bool on, bool pack, bool plays) =>
        Assert.Equal(plays, Game1.PlaysGameSounds(game, on, pack));
}
