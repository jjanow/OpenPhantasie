using OpenPhantasie.Data;
using OpenPhantasie.Data.Audio;

namespace OpenPhantasie.Core.Tests;

/// <summary>
/// The title tunes' decoders (s108): Phantasie I Amiga <c>tamb.dat</c> (h89 <c>+0x304</c>), II's Dosound script in
/// <c>START.PRG</c> (TOS's 50 Hz interpreter), III Amiga <c>D/m0</c> (the VBL driver <c>0x2192</c>). Formats in the
/// platforms' FORMAT.md; numbers here are what the static reads give.
/// </summary>
public class TitleTuneDecoderTests
{
    private static string Repo(string relative)
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var p = Path.Combine(dir.FullName, relative);
            if (File.Exists(p))
                return p;
        }
        throw new FileNotFoundException(relative);
    }

    private static byte[] Tamb => File.ReadAllBytes(Repo("reverse-engineering/amiga/unpacked/PHANT/tamb.dat"));
    private static byte[] StartPrg => File.ReadAllBytes(Repo("reverse-engineering/atari-st/phantasie2/unpacked/disk1/START.PRG"));
    private static byte[] M0 => File.ReadAllBytes(Repo("reverse-engineering/amiga/phantasie3/unpacked/PhantasieIII/D/m0"));
    private static byte[] M1 => File.ReadAllBytes(Repo("reverse-engineering/amiga/phantasie3/unpacked/PhantasieIII/D/m1"));

    [OriginalDataFact]
    public void PhantasieI_Tamb_Is338Steps_Of2678DosTicks()
    {
        var s = AmigaDosoundScore.Parse(Tamb);
        Assert.Equal(338, s.Steps.Count);
        Assert.Equal(2678, s.Ticks);                 // Σ ⌊n·6/5⌋, Delay() ticks of 1/50 s
        Assert.Equal(53.56, s.DurationSeconds, 6);
        // Step 1: 07 d8, 08 10, 00 53, 01 01, 0d 00, 82 06 → A = 0x153 at volume 16 → 15 × 4; B never written yet.
        var first = s.Steps[0];
        Assert.Equal(7, first.Ticks);                 // ⌊6·6/5⌋
        Assert.Equal(AmigaDosoundScore.Period(0x153), first.Channels[2].Period);
        Assert.Equal(606, AmigaDosoundScore.Period(0x153)); // ⌊339 × 1.7897732⌋
        Assert.Equal(new PaulaNote(true, 606, 60), first.Channels[2]);
        Assert.Equal(first.Channels[2], first.Channels[3]);
        Assert.Equal(new PaulaNote(true, 0, 0), first.Channels[0]);
        Assert.False(first.Channels[1].Sent);        // the reg 7 write turned channel 1 off for good
        Assert.All(s.Steps, step => Assert.False(step.Channels[1].Sent));
    }

    [Fact]
    public void PhantasieI_Player_Constants()
    {
        Assert.Equal(3_579_545.0 / 2_000_000.0, AmigaDosoundScore.PeriodFactor, 5); // single-float 0x3FE5174A
        Assert.Equal([127, 119, 111, 103, 95, 87, 79, 71, 63, 55, 47, 39, 31, 23, 15, 7],
            AmigaDosoundScore.Waveform().Select(b => (int)b));
    }

    [Fact]
    public void PhantasieI_Player_ExpandsSweeps_AndEndsOnAWaitOfZero()
    {
        // 0x80 start, 0x81 reg 8 step 4 end 12: h89 +0x9bc emits volume 4 and 8 (the end value is not played), each
        // with a wait of 1; then a wait of 0 ends the tune before the step after it.
        byte[] script = [0x07, 0xFE, 0x01, 0x01, 0x80, 0x04, 0x81, 0x08, 0x04, 0x0C, 0x82, 0x05, 0x82, 0x00, 0x82, 0x09, 0xFF];
        var s = AmigaDosoundScore.Parse(script);
        Assert.Equal([1, 1, 6], s.Steps.Select(x => x.Ticks));
        Assert.Equal([16, 32, 32], s.Steps.Select(x => x.Channels[2].Volume));
        Assert.All(s.Steps, x => Assert.False(x.Channels[0].Sent)); // reg 7 0xFE: B off
    }

    [OriginalDataFact]
    public void PhantasieII_StartPrg_Dosound_Is3231TicksOf50Hz()
    {
        // XBIOS 32 Dosound(0x1b3) = file offset 0x1cf (28-byte PRG header).
        var s = DosoundScore.Parse(StartPrg, 0x1cf);
        Assert.Equal(2064, s.ScriptBytes);
        Assert.Equal(3231, s.Ticks);                  // 285 waits Σ(n + 1)
        Assert.Equal(64.62, s.DurationSeconds, 6);
        // It first silences all 14 registers, then: mixer 0xd8, B level 16 (envelope), envelope period 0x14xx, B = 0x2fc.
        Assert.Equal(Enumerable.Range(0, 14), s.Writes.Take(14).Select(w => w.Register));
        Assert.All(s.Writes.Take(14), w => Assert.Equal((0, 0), (w.Tick, w.Value)));
        Assert.Equal([(7, 0xd8), (9, 0x10), (12, 0x14), (3, 0x01), (2, 0xfc), (13, 0)],
            s.Writes.Skip(14).Take(6).Select(w => (w.Register, w.Value)));
        var last = s.Writes[^1];
        Assert.Equal((10, 0, 3231), (last.Register, last.Value, last.Tick)); // ends by zeroing the levels itself
    }

    [Fact]
    public void Dosound_Sweep_RepeatsOncePerTick_AndAWaitOfNHoldsNPlusOneTicks()
    {
        // 0x80 0, 0x81 reg 8 step 5 end 15: 5, 10, 15 on ticks 0, 1, 2; then a wait of 2 → the end is read on tick 5.
        byte[] script = [0x80, 0x00, 0x81, 0x08, 0x05, 0x0F, 0x82, 0x02, 0xFF, 0x00];
        var s = DosoundScore.Parse(script);
        Assert.Equal([(0, 5), (1, 10), (2, 15)], s.Writes.Select(w => (w.Tick, w.Value)));
        Assert.Equal(6, s.Ticks);
    }

    [OriginalDataFact]
    public void PhantasieIII_M0_ThreeVoices_End_Together_At2897Vbls()
    {
        var s = P3MusicScore.Parse(M0, 830, 60);
        Assert.Equal([0, 299, 624], s.VoiceStarts);   // split at the first two 0xFF bytes (298 + 324 + 205 bytes)
        Assert.Equal([141, 155, 98], s.NoteCounts);
        Assert.False(s.Loops);                        // no 0xE7: the tune plays once
        Assert.Equal(2897, s.Frames);                 // all three read 0xFF on frame 2896, DMA off on 2897
        Assert.Equal(2897 / 60.0, s.DurationSeconds, 9);
        // Voice 1 opens e2 07 (tempo 7), e1, c2 20 (triangle), c4 00 b4, db, then note 0x3c for (1 + 1) × 7 frames:
        // period table [0x3c − 9] = 856 (C), triangle waveform.
        var v0 = s.Writes.Where(w => w.Channel == 0 && w.Frame == 0).ToList();
        Assert.Contains(new PaulaWrite(0, 0, PaulaWriteKind.Period, 856), v0);
        Assert.Contains(new PaulaWrite(0, 0, PaulaWriteKind.Sample, P3MusicScore.WaveTriangle), v0);
        Assert.Contains(new PaulaWrite(14, 0, PaulaWriteKind.Period, 906), s.Writes); // note 0x3b on frame 14
        // Every voice switches its DMA off after its 0xFF.
        Assert.Equal(3, s.Writes.Count(w => w.Kind == PaulaWriteKind.DmaOff && w.Frame > 0));
    }

    [OriginalDataFact]
    public void PhantasieIII_M1_TheRewardTune_AlsoDecodes()
    {
        var s = P3MusicScore.Parse(M1, 659, 60);
        Assert.Equal([0, 253, 455], s.VoiceStarts);
        Assert.False(s.Loops);
        Assert.True(s.Frames > 0);
    }

    [OriginalDataFact]
    public void PhantasieIII_Streams_UseOnlyKnownCommands_AndNotesInsideThePeriodTable()
    {
        var bytes = M0.AsSpan(0, 830).ToArray();
        var s = P3MusicScore.Parse(bytes, 830, 60);
        Assert.DoesNotContain(s.Writes, w => w.Channel == 3); // no unknown command's click
        // Vibrato rewrites a voice's period every frame; before its first note (voice 3 opens on rests) that is 0, on a
        // channel whose DMA is still off.
        Assert.All(s.Writes.Where(w => w.Kind == PaulaWriteKind.Period && w.Value != 0), w => Assert.InRange(w.Value, 120, 16256));
        var firstSound = s.Writes.First(w => w.Channel == 2 && w.Kind == PaulaWriteKind.DmaOn).Frame;
        Assert.DoesNotContain(s.Writes, w => w.Channel == 2 && w.Kind == PaulaWriteKind.Period && w.Value == 0 && w.Frame >= firstSound);
        Assert.All(s.Writes.Where(w => w.Kind == PaulaWriteKind.Volume), w => Assert.InRange(w.Value, 0, 63));
    }

    [Fact]
    public void PhantasieIII_NoiseBuffer_IsTheLcgFromSeedZero()
    {
        var noise = P3MusicScore.Waveforms()[P3MusicScore.WaveNoise];
        Assert.Equal(4000, noise.Length);
        // seed 1: 13849 = 0x3619 → bytes 00 00 36 19; seed 2: (13849·25173 + 13849) mod 65536.
        Assert.Equal([0, 0, 0x36, 0x19], noise.Take(4).Select(b => (int)(byte)b));
        var seed2 = (13849 * 25173 + 13849) % 65536;
        Assert.Equal([0, 0, seed2 >> 8, seed2 & 0xFF], noise.Skip(4).Take(4).Select(b => (int)(byte)b));
    }

    [OriginalDataTheory]
    [InlineData(GameId.P1, TuneFormats.AmigaDosound, 53.56)]
    [InlineData(GameId.P2, TuneFormats.AtariStDosound, 64.62)]
    [InlineData(GameId.P3, TuneFormats.AmigaP3Music, 2897 / 60.0)]
    [InlineData(GameId.P4, TuneFormats.X68kOpmDrv, (491521 * 41472.0 + 1793 * 10368) / 4e6)]
    public void Packs_NameTheirTitleTune(GameId game, string format, double seconds)
    {
        var pack = GraphicsPackCatalog.Load(GameDataRepository.DefaultPathFor(game)).Default!;
        var tune = pack.TitleSequence!.Tune!;
        Assert.Equal(format, tune.Format);
        Assert.Equal(seconds, tune.TryLoad()!.DurationSeconds, 6);
    }

    [OriginalDataFact]
    public void PhantasieI_Cover1_LastsTheTune_AndTheTuneEndsWithIt()
    {
        var s = GraphicsPackCatalog.Load(GameDataRepository.DefaultPathFor(GameId.P1)).Default!.TitleSequence!;
        Assert.Equal(53.56, s.Steps[0].Seconds!.Value, 6);
        Assert.Equal(5.0, s.Steps[1].Seconds);
        Assert.Equal(1, s.Tune!.UntilStep);
    }

    private static OpmDrvScore Song0 =>
        (OpmDrvScore)GraphicsPackCatalog.Load(GameDataRepository.DefaultPathFor(GameId.P4)).Default!.TitleSequence!.Tune!.TryLoad()!;

    private static byte[] P4Snd => File.ReadAllBytes(Repo("reverse-engineering/x68000/phantasie4/unpacked/disk1/P4.SND"));

    /// <summary>The (code, length, gate) events of a compiled stream that are notes (code below 0x7F), or rests too.</summary>
    private static List<(int Code, int Length, int Gate)> Notes(byte[] events, bool rests = false)
    {
        var list = new List<(int, int, int)>();
        for (var i = 0; i + 2 < events.Length; i += 3)
            if (events[i] < 0x7F || rests && events[i] == 0x7F)
                list.Add((events[i], events[i + 1], events[i + 2]));
        return list;
    }

    [OriginalDataFact]
    public void PhantasieIV_Song0_FiveTracks_CompileToTheDriversEvents()
    {
        var s = Song0;
        Assert.Equal([199, 196, 535, 361, 46], s.Tracks.Take(5).Select(t => t!.Length));
        Assert.All(s.Tracks.Skip(5), Assert.Null);
        // Track 1: v9, |:256 (count 0 = 256 rounds), T120, r2 (96, gate ⌊96·7/8⌋ − 1), @114, r8, p3, v10, g#4. (KC 0x46).
        Assert.Equal(Convert.FromHexString("801200" + "C00000" + "907800" + "7F6053" + "A07200" + "7F1814" + "E00300" + "801000" + "46483E"),
            s.Tracks[0]!.Take(27));
        Assert.Equal(0xFF, s.Tracks[0]![^1]);
        // Notes, rests and voices per track as the MML has them (& adds no event).
        Assert.Equal([38, 29, 160, 106, 0], s.Tracks.Take(5).Select(t => Notes(t!).Count));
        Assert.Equal([12, 3, 0, 2, 10], s.Tracks.Take(5).Select(t => Notes(t!, rests: true).Count - Notes(t!).Count));
        // Every playing track's round is 10 bars of 192 ticks; track 5's ten whole rests loop twice, then it ends.
        Assert.All(s.Tracks.Take(5), t => Assert.Equal(1920, Notes(t!, rests: true).Sum(n => n.Length)));
        // Track 3 (piano): o3 c / c# sixteenths alternate, q7 → gate 9 of 12.
        Assert.Equal([(0x2C, 12, 9), (0x2D, 12, 9)], Notes(s.Tracks[2]!).Take(2));
    }

    [OriginalDataFact]
    public void PhantasieIV_Song0_Plays256Rounds_OfTenBarsAt120()
    {
        var s = Song0;
        Assert.Equal(41472, OpmDrvSequencer.PassCycles(120, 120)); // 4 × 64 × ⌊19531 / 120⌋ clocks = 10.368 ms at 4 MHz
        Assert.Equal(256 * 1920 + 1, s.Passes);                   // + the pass that reads the end
        // s156: each round's 7 voice-load passes (0: @83 + @195, 48: @74, 96: @114, 384 / 768 / 1152 / 1536: @195) lose
        // one timer A interrupt (64 · 162 = 10368 clocks), the song's first pass two: 256 · 7 + 1 = 1793 (s117 XEiJ trace).
        Assert.Equal(s.Passes * 41472L + 1793 * 10368L, s.TotalCycles);
        Assert.Equal(5100.74, s.DurationSeconds, 2);              // 85 min: the splash stops it long before
        Assert.Equal((0, 1, 2), (OpmDrvSequencer.LostInterrupts(true, false), OpmDrvSequencer.LostInterrupts(false, true),
            OpmDrvSequencer.LostInterrupts(true, true)));
    }

    [OriginalDataFact]
    public void PhantasieIV_Song0_ARoundLasts7687TimerPeriods()
    {
        // FORMAT.md 6.11.3: 7680 + 7 interrupts a round = 19.925 s (XEiJ measured 19.931 s, its own timer re-arm).
        var seq = Song0.Start();
        seq.InitialWrites([]);
        var sink = new List<OpmWrite>();
        long cycles = 0;
        for (var p = 0; p < 2 * 1920; p++)
        {
            seq.Pass(sink);
            if (p >= 1920)
                cycles += seq.CyclesToNextPass;
        }
        Assert.Equal(7687 * 10368L, cycles);
        Assert.Equal(19.925, cycles / 4e6, 3);
    }

    [OriginalDataFact]
    public void PhantasieIV_Sequencer_KeysOn_WithTheVoiceAndVolume_AndOffAtTheGate()
    {
        var seq = Song0.Start();
        var w = new List<OpmWrite>();
        seq.Pass(w); // pass 0: track 3 loads @83 (PIANO 1, CON 2: only OP4 = C2 is a carrier) and plays o3 c
        Assert.Contains(new OpmWrite(0x20 + 2, 0xC0 | 7 << 3 | 2), w);           // pan 3, FL 7, CON 2
        Assert.Contains(new OpmWrite(0x78 + 2, 1 + 13), w);                       // C2 TL 1 + v11 (13)
        Assert.Contains(new OpmWrite(0x60 + 2, 36), w);                           // M1 TL 36, not a carrier
        Assert.Contains(new OpmWrite(0x28 + 2, 0x2C), w);
        Assert.True(w.IndexOf(new OpmWrite(0x08, 0x0F << 3 | 2)) > w.IndexOf(new OpmWrite(0x28 + 2, 0x2C))); // then all four slots on
        var keyOffAt = -1;
        for (var pass = 1; pass < 12 && keyOffAt < 0; pass++)
        {
            w.Clear();
            seq.Pass(w);
            if (w.Contains(new OpmWrite(0x08, 2)))
                keyOffAt = pass;
        }
        Assert.Equal(9, keyOffAt); // gate 9 of a 12-tick sixteenth
    }

    [Theory]
    [InlineData("o4a", 0x48, 48, 47)]   // index 4·12 + 6 = 54 → KC 0x48 (G at 3.58 MHz, A at the X68000's 4 MHz); l4, q8
    [InlineData("o0d#", 0x00, 48, 47)]  // the table's first note
    [InlineData("l16q7c", 0x3C, 12, 9)] // o4 c = index 45 → KC 0x3C; ⌊12·7/8⌋ − 1
    [InlineData("q1l32e", 0x41, 6, 0)]  // ⌊6/8⌋ = 0 → gate 0, not −1
    public void OpmDrv_NoteCodes_Lengths_Gates(string mml, int code, int length, int gate)
    {
        var c = new OpmDrvCompiler();
        c.Compile(mml);
        Assert.Equal([code, length, gate, 0xFF], c.Events().Select(b => (int)b));
    }

    [Fact]
    public void OpmDrv_Dots_Ties_Octaves_Loops_AndCommands()
    {
        var c = new OpmDrvCompiler();
        c.Compile("l8 c4.. & <d >>e1 |:3 r16 :| v15 @v100 t200 @12 p1");
        var e = c.Events();
        Assert.Equal([0x3C, 84, 84], e.Take(3).Select(b => (int)b));          // 48 + 24 + 12; & → gate = length
        Assert.Equal([0x4E, 24, 23], e.Skip(3).Take(3).Select(b => (int)b));  // < = octave up: o5 d (index 59)
        Assert.Equal([0x31, 192, 191], e.Skip(6).Take(3).Select(b => (int)b)); // o3 e (index 37)
        Assert.Equal([0xC0, 0, 3, 0x7F, 12, 11, 0xC0, 1, 0], e.Skip(9).Take(9).Select(b => (int)b));
        Assert.Equal([0x80, 0x02, 0, 0x80, 27, 0, 0x90, 200, 0, 0xA0, 12, 0, 0xE0, 1, 0, 0xFF], e.Skip(18).Select(b => (int)b));
        Assert.Throws<InvalidDataException>(() => new OpmDrvCompiler().Compile("o8b"));
        Assert.Throws<InvalidDataException>(() => new OpmDrvCompiler().Compile("v1&c"));
    }

    [OriginalDataFact]
    public void P4Snd_Voices_AsTheGameConvertsThem()
    {
        var snd = P4Snd;
        Assert.Equal(16000, snd.Length);
        Assert.Equal("A.PIANO", OpmDrvScore.VoiceName(snd));
        Assert.Equal("VIBE", OpmDrvScore.VoiceName(snd.AsSpan(113 * 80)));
        Assert.Equal("S.DR_o4b", OpmDrvScore.VoiceName(snd.AsSpan(194 * 80)));
        var v = OpmDrvScore.ReadSnd(snd);
        var vibe = v[113];
        Assert.Equal(55, vibe.Length);
        // FL 0 / CON 7; slot mask 15, triangle LFO, no sync, LFRQ 197, PMD 0, AMD 33, PMS 4, AMS 1, pan 3, 'V'.
        Assert.Equal([7, 15, 2, 0, 197, 0, 33, 4, 1, 3, 'V'], vibe.Take(11).Select(b => (int)b));
        // Operator 1 (M1): AR 31, D1R 20, D2R 7, RR 7, D1L 2, TL 3, KS 0, MUL 1, DT1 3, DT2 0, AME 1.
        Assert.Equal([31, 20, 7, 7, 2, 3, 0, 1, 3, 0, 1], vibe.Skip(11).Take(11).Select(b => (int)b));
        Assert.Equal([1, 4, 14, 1], Enumerable.Range(0, 4).Select(k => (int)vibe[11 + 11 * k + 7])); // MUL per operator
        var brass = v[73]; // BRASS 3: FL 7, CON 5, PMD 50, PMS 3
        Assert.Equal(7 * 8 + 5, brass[0]);
        Assert.Equal(50, brass[5]);
        Assert.Equal(3, brass[7]);
    }
}
