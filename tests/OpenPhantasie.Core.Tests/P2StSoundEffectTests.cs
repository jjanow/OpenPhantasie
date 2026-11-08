using OpenPhantasie.Data;
using OpenPhantasie.Data.Audio;

namespace OpenPhantasie.Core.Tests;

/// <summary>
/// s135, II ST's sound effects (<c>FORMAT.md</c> 7.2): <c>0xb80e(n)</c> → Dosound of the table <c>0x24a8e</c> that
/// <c>0xb6b0</c> fills (every entry <c>0x21ada</c>, then 26 set one by one); sounds 1, 9 and 27 use a second player.
/// </summary>
public class P2StSoundEffectTests
{
    private const int Header = 0x1c; // PHANT.PRG file offset = image + 0x1c

    private static GraphicsPack Pack =>
        GraphicsPackCatalog.Load(GameDataRepository.DefaultPathFor(GameId.P2)).Find("atari-st")
        ?? throw new InvalidOperationException("II ST pack missing");

    private static StDosoundEffects Effects => Pack.StDosoundEffects!;

    private static byte[] Prg => Effects.TryLoadImage() ?? throw new InvalidOperationException("PHANT.PRG missing");

    private static int U16(byte[] b, int at) => (b[at] << 8) | b[at + 1];

    private static int U32(byte[] b, int at) => (b[at] << 24) | (b[at + 1] << 16) | (b[at + 2] << 8) | b[at + 3];

    [OriginalDataFact]
    public void Pack_StSounds_NotAmigaOrP3()
    {
        var pack = Pack;
        Assert.Null(pack.SoundEffects);
        Assert.Null(pack.DosoundEffects);
        Assert.EndsWith("unpacked/disk1/PHANT.PRG", Effects.FilePath.Replace('\\', '/'));
        Assert.Equal(2_000_000, Effects.ClockHz);
        Assert.Equal(Enumerable.Range(0, 36).Except([1, 9, 27]), Effects.Sounds.Keys.Order()); // 33 = 0xb7e6's click, 34 = 0xb7fa, 35 = 0xb9ce (s156)
        Assert.Equal([1, 9, 27], Effects.Repeats.Keys.Order()); // s136: the second player 0xb92a
        Assert.All(Enumerable.Range(0, 36), n => Assert.True(Effects.Has(n)));
        Assert.Equal(32, pack.EndingScene!.Crowd!.Sound);
        Assert.Equal((33, 33), (pack.EndingScene.IntroPage!.Sound, pack.EndingScene.ClosingPage!.Sound));
    }

    [OriginalDataFact]
    public void Pack_MatchesTheTable0xb6b0Fills()
    {
        var g = Prg;
        // 0xb6c4: adda.l #0x24a8e, a0; move.l #0x21ada, (a0) for d7 < 33.
        Assert.Equal(0x24A8E, U32(g, Header + 0xb6c6));
        Assert.Equal(0x21ADA, U32(g, Header + 0xb6cc));
        Assert.Equal(33, U16(g, Header + 0xb6d4));
        var table = Enumerable.Range(0, 33).ToDictionary(n => n, _ => 0x21ADA);
        var set = 0;
        for (var at = Header + 0xb6d8; U16(g, at) == 0x23FC; at += 10, set++) // move.l #imm, abs.l
            table[(U32(g, at + 6) - 0x24A8E) / 4] = U32(g, at + 2);
        Assert.Equal(26, set);
        foreach (var (n, image) in table)
            if (n is not (1 or 9 or 27))
                Assert.Equal(image + Header, Effects.Sounds[n]);
    }

    [OriginalDataFact]
    public void Sound33_IsTheFormClick_0xb7e6()
    {
        var g = Prg;
        // 0xb7e6: move.l #0x21ab0, (sp); jsr 0xb8b0 (the play path after the table lookup).
        Assert.Equal(0x2EBC, U16(g, Header + 0xb7ea));
        Assert.Equal(0x21AB0, U32(g, Header + 0xb7ec));
        Assert.Equal(0xB8B0, U32(g, Header + 0xb7f2));
        Assert.Equal(0x21AB0 + Header, Effects.Sounds[33]);
        // Registers 0-13 := 0, then 0b 0e 07 fe 08 10 0d 0e | 82 06 | 08 00 | 82 00: A on the triangle envelope, 7 ticks.
        var score = Effects.Score(g, 33)!;
        Assert.Equal(7, score.Ticks);
        Assert.Contains(new PsgWrite(0, 13, 0x0E), score.Writes);
        Assert.Contains(new PsgWrite(7, 8, 0), score.Writes);
    }

    [OriginalDataFact]
    public void Sound32_IsTheCrowdsNoiseSwell()
    {
        var g = Prg;
        Assert.Equal(0x21DB8 + Header, Effects.Sounds[32]);
        var score = Effects.Score(g, 32)!;
        // 07 07 06 1f 08 10 09 10 0a 10 0c 10 0d 0d | 82 4d | 0c 90 0d 00 | 82 80 | 08 00 09 00 0a 00 | 82 00
        Assert.Equal(78 + 129, score.Ticks);
        Assert.Equal(4.14, score.DurationSeconds, 6);
        Assert.Contains(new PsgWrite(0, 7, 0x07), score.Writes);   // tones off, noise on A, B, C
        Assert.Contains(new PsgWrite(0, 13, 0x0D), score.Writes);  // envelope rise and hold
        Assert.Contains(new PsgWrite(78, 13, 0x00), score.Writes); // then the fall
        Assert.Contains(new PsgWrite(207, 8, 0), score.Writes);    // levels off at the end
    }

    [OriginalDataTheory]
    [InlineData(1, 0xb838, 0x21BC0, 10)]
    [InlineData(9, 0xb822, 0x21B54, 10)]
    [InlineData(27, 0xb84e, 0x21B66, 6)]
    public void SecondPlayer_RepeatsMatch0xb80e(int sound, int site, int body, int count)
    {
        var g = Prg;
        // move.l #body, (sp); move.w #count, -(sp); jsr 0xb92a
        Assert.Equal(0x2EBC, U16(g, Header + site));
        Assert.Equal(body, U32(g, Header + site + 2));
        Assert.Equal(0x3F3C, U16(g, Header + site + 6));
        Assert.Equal(count, U16(g, Header + site + 8));
        Assert.Equal(0xB92A, U32(g, Header + site + 12));
        var r = Effects.Repeats[sound];
        Assert.Equal((body + Header, count, 0x21A32 + Header, 30), (r.Body, r.Count, r.Tail, r.TailLength));
        // 0xb992: 30 bytes of 0x21a32 appended (cmpw #30).
        Assert.Equal(0x21A32, U32(g, Header + 0xb988));
        Assert.Equal(30, U16(g, Header + 0xb994));
    }

    [OriginalDataFact]
    public void Sound1_IsTheBodyTenTimesThenTheReset()
    {
        var g = Prg;
        var script = StDosoundEffects.BuildRepeat(g, Effects.Repeats[1])!;
        // body 07 f9 09 10 0a 10 02 01 80 06 0d 0a 81 0b 01 0a (16 bytes, the ff dropped) x 10 + 30-byte reset.
        Assert.Equal(16 * 10 + 30, script.Length);
        Assert.Equal(new byte[] { 0x07, 0xF9, 0x09, 0x10 }, script[16..20]);
        Assert.Equal(new byte[] { 0x82, 0x00 }, script[^2..]);
        var score = Effects.Score(g, 1)!;
        // Each copy sweeps register 11 from 7 to 10, one step a tick (EmuTOS's 0x81): four ticks a copy.
        Assert.Equal(40, score.Ticks);
        Assert.Equal(10, score.Writes.Count(w => w is { Register: 11, Value: 0x0A }));
        Assert.Equal(0.8, Effects.Seconds(g, 1), 6);
        Assert.True(Effects.Score(g, 9)!.Ticks > 0);
        Assert.True(Effects.Score(g, 27)!.Ticks > 0);
    }

    [OriginalDataFact]
    public void Sound15_TheDeathMark_RunsAbout4Seconds()
    {
        var g = Prg;
        // 0x21b0c: ... 82 d0 (wait 208 = 209 ticks) ... 82 00.
        var score = Effects.Score(g, 15)!;
        Assert.InRange(score.Ticks, 209, 212);
        Assert.InRange(Effects.Seconds(g, 15), 4.1, 4.3);
    }

    [OriginalDataFact]
    public void CombatLines_LoadFromThePack()
    {
        var lines = Pack.StCombatLines!;
        Assert.Equal((5, 10, 1000), (lines.SpeedMs(1), lines.SpeedMs(2), lines.SpeedMs(3)));
        Assert.Equal((9, 18, 3), (lines.SoundWaitSpeed, lines.NoWaitAfter, lines.ShortText));
        Assert.Equal((300.0, 10.0, 9), (lines.PoseMs, lines.PoseFastMs, lines.PoseSpeed));
        Assert.Equal(new StCombatDeath(15, 500), lines.Death);
        var hits = lines.Lines["core.combat.x_hits"];
        Assert.Equal((3, 5), (hits[CombatLineSide.Member].Sound, hits[CombatLineSide.Monster].Sound));
        Assert.True(lines.Lines["core.combat.x_dies"][CombatLineSide.Member].Death);
        Assert.Equal(16, lines.Lines["core.combat.x_dies"][CombatLineSide.Monster].Sound);
        Assert.True(lines.Lines["core.combat.x_casts_a_spell"][CombatLineSide.Member].Pose);
        Assert.False(lines.Lines["core.combat.x_casts_a_spell"][CombatLineSide.Monster].Hold);
        Assert.Equal([9], lines.Lines["core.combat.x_breathes"][CombatLineSide.Monster].Pre);
        Assert.Equal(6, lines.Lines["core.combat.magic_drained"][CombatLineSide.Any].Sound);
        // Every id in the table is a string the game has.
        var ids = StringIds(GameDataRepository.DefaultPathFor(GameId.P1))
            .Concat(StringIds(Path.Combine(GameDataRepository.DefaultPathFor(GameId.P1), "..", "shared"))).ToHashSet();
        Assert.All(lines.Lines.Keys, k => Assert.Contains(k, ids));
    }

    private static IEnumerable<string> StringIds(string gameDir)
    {
        using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(gameDir, "strings", "en.json")));
        return doc.RootElement.GetProperty("strings").EnumerateObject().Select(p => p.Name).ToList();
    }

    // ==== s139: the moments outside combat (FORMAT.md 7.4, field_sounds) =================================================

    [OriginalDataFact]
    public void FieldSounds_EveryCoreMoment_MapsToAPackedSound()
    {
        var field = Pack.FieldSounds ?? throw new InvalidOperationException("no field_sounds");
        var moments = typeof(FieldCues).GetFields().Where(f => f.IsLiteral).Select(f => (string)f.GetRawConstantValue()!).ToList();
        Assert.Equal(moments.Order(), field.Cues.Keys.Order());
        var image = Prg;
        Assert.All(field.Cues, kv =>
        {
            Assert.True(Effects.Has(kv.Value), kv.Key);
            Assert.True(Effects.Score(image, kv.Value)!.Ticks > 0, kv.Key);
        });
        Assert.Equal(9, field.WaitSpeedMs); // 0xb8f2: [0x1fefa] >= 9
        Assert.Null(field.SoundFor("no.such.moment"));
    }

    [OriginalDataFact]
    public void FieldSounds_34_IsTheDefaultScript_0xb7faPlaysStraight()
    {
        var g = Prg;
        Assert.Equal(Effects.Sounds[6], Effects.Sounds[34]);
        Assert.Equal(0x21ADA + Header, Effects.Sounds[34]);
        // 0xb7fa: link; move.l #0x21ada, (sp); jsr 0xb8b0
        Assert.Equal(0x21ADA, U32(g, Header + 0xb800));
        Assert.Equal(0xB8B0, U32(g, Header + 0xb806));
    }

    [OriginalDataFact]
    public void Sound35_IsTheSparkleScript_0xb9cePlays()
    {
        var g = Prg;
        // s156 (FORMAT.md 7.3): 0xb9ce: link; move.l #0x21c48, (sp); jsr 0xb8b0 (the wait, the silence, Dosound).
        Assert.Equal(0x21C48 + Header, Effects.Sounds[35]);
        Assert.Equal(0x21C48, U32(g, Header + 0xb9d4));
        Assert.Equal(0xB8B0, U32(g, Header + 0xb9da));
    }

    /// <summary><c>move.w #n,(sp)</c> (<c>3ebc</c>) or <c>move.w #n,-(sp)</c> (<c>3f3c</c>) then <c>jsr 0xb80e</c> at
    /// <paramref name="site"/>.</summary>
    [OriginalDataTheory]
    [InlineData(0x63e2, FieldCues.TownPrompt)]
    [InlineData(0x106a0, FieldCues.DungeonPrompt)]
    [InlineData(0x10e9c, FieldCues.DisarmFails)]
    [InlineData(0x11d02, FieldCues.EventSpellCast)]
    [InlineData(0x11dfc, FieldCues.EventPartyMoved)]
    [InlineData(0x11e88, FieldCues.EventNoise)]
    [InlineData(0x262c, FieldCues.Transport)]
    [InlineData(0x92d8, FieldCues.FieldResurrected)]
    public void FieldSounds_MatchTheirCallSites(int site, string moment)
    {
        var g = Prg;
        Assert.Contains(U16(g, Header + site - 4), new[] { 0x3EBC, 0x3F3C });
        Assert.Equal(Pack.FieldSounds!.SoundFor(moment), U16(g, Header + site - 2));
        Assert.Equal(0x4EB9, U16(g, Header + site));
        Assert.Equal(0xB80E, U32(g, Header + site + 2));
    }

    [OriginalDataFact]
    public void FieldSounds_WallAndDone_Call0xb7fa()
    {
        var g = Prg;
        Assert.Equal(0xB7FA, U32(g, Header + 0x10472)); // 0x1046c "THAT IS A WALL"
        foreach (var site in new[] { 0xe5ec, 0xe7f2 }) // the town casts' "D O N E !": 0x15ce → 0x1628(9) → 0xb7fa
            Assert.Equal(0x15CE, U32(g, Header + site + 2));
        Assert.Equal(9, U16(g, Header + 0x15d4));
        Assert.Equal(0x1628, U32(g, Header + 0x15d8));
        Assert.Equal(0xB7FA, U32(g, Header + 0x1646));
        Assert.Equal(34, Pack.FieldSounds!.SoundFor(FieldCues.DungeonWall));
        Assert.Equal(34, Pack.FieldSounds.SoundFor(FieldCues.TownCastDone));
    }

    // ==== s140: the defeat scene, the alerts, the clicks, TRANSPORTATION's fade (FORMAT.md 7.4) ========================

    /// <summary><c>move.w #n,(sp)</c> then <c>jsr target</c> at <paramref name="site"/>.</summary>
    private static void AssertCall(byte[] g, int site, int n, int target)
    {
        Assert.Equal(0x3EBC, U16(g, Header + site - 4));
        Assert.Equal(n, U16(g, Header + site - 2));
        Assert.Equal(0x4EB9, U16(g, Header + site));
        Assert.Equal(target, U32(g, Header + site + 2));
    }

    [OriginalDataFact]
    public void Defeat_SoundsMatchTheirCallSites_AtSpeed20()
    {
        var g = Prg;
        var d = Pack.FieldSounds!.Defeat!;
        AssertCall(g, 0x12d5e, d.Ghosts, 0xB80E); // after the ghost row 0x12b5e(0)
        AssertCall(g, 0x12d86, d.Door, 0xB80E); // then the door loop (d6 = 14, 90 steps)
        AssertCall(g, 0x12e36, d.Demon, 0xB80E); // then the face 0x13864 at (123, 19)
        AssertCall(g, 0x126d2, d.Fate, 0xB80E); // in the fate loop, before the branch
        // 0x1269a: move.w #20, 0x1fefa (the Speed word) for the whole scene.
        Assert.Equal(0x33FC, U16(g, Header + 0x1269a));
        Assert.Equal(d.SpeedMs, U16(g, Header + 0x1269c));
        Assert.Equal(0x1FEFA, U32(g, Header + 0x1269e));
    }

    [OriginalDataTheory]
    [InlineData(0x2356, 0)] // There is no room (move.w #0 is clr.w here)
    [InlineData(0x2366, 6)] // Not in the town ... YES / NO
    [InlineData(0x15e0, 1)] // You can not afford it
    [InlineData(0x15f2, 5)] // No one is wise enough
    [InlineData(0x1604, 10)] // UNUSABLE
    [InlineData(0x1616, 2)] // You are improved
    [InlineData(0x28cc, 3)] // You must assemble a group
    [InlineData(0x15ce, 9)] // D O N E !
    public void Alerts_WrappersCall0x1628(int wrapper, int alert)
    {
        var g = Prg;
        Assert.Equal(0x4E56, U16(g, Header + wrapper)); // link
        if (alert == 0)
            Assert.Equal(0x4257, U16(g, Header + wrapper + 4)); // clr.w (sp)
        else
        {
            Assert.Equal(0x3EBC, U16(g, Header + wrapper + 4));
            Assert.Equal(alert, U16(g, Header + wrapper + 6));
        }
        var jsr = alert == 0 ? wrapper + 6 : wrapper + 8;
        Assert.Equal(0x4EB9, U16(g, Header + jsr));
        Assert.Equal(0x1628, U32(g, Header + jsr + 2));
    }

    [OriginalDataFact]
    public void Alerts_BeepFirst_AndTheCallersAreTheListedOnes()
    {
        var g = Prg;
        // 0x1628: rsrc_gaddr(5, n) (0x1abfa), then 0xb7fa, then form_alert; 0x1668: 0xb7fa first.
        Assert.Equal(0xB7FA, U32(g, Header + 0x1646));
        Assert.Equal(0xB7FA, U32(g, Header + 0x166e));
        Assert.Equal(0xB7FA, U32(g, Header + 0x1816)); // training's "You need %ld more experience points"
        // Every jsr 0x1628 / 0x1668 / 0xb7fa in the text: the wrappers, the direct sites and 0x1046c (the wall).
        var sites = new List<(int Site, int Target)>();
        for (var at = 0; at + 6 <= 0x1e298; at += 2)
            if (U16(g, Header + at) == 0x4EB9 && U32(g, Header + at + 2) is 0x1628 or 0x1668 or 0xB7FA && at != 0x1644)
                sites.Add((at, U32(g, Header + at + 2)));
        Assert.Equal([
            (0x98a, 0x1628), (0x9d6, 0x1628), (0x9f6, 0x1628), (0x15b2, 0x1628), (0x15c4, 0x1628), (0x15d6, 0x1628),
            (0x15e8, 0x1628), (0x15fa, 0x1628), (0x160c, 0x1628), (0x161e, 0x1628), (0x166c, 0xB7FA), (0x1814, 0xB7FA),
            (0x188a, 0x1628), (0x235c, 0x1628), (0x236e, 0x1628), (0x28d4, 0x1628), (0x28e6, 0x1628), (0x290c, 0x1628),
            (0x6fb8, 0x1628), (0xbc14, 0x1668), (0xbc4e, 0x1668), (0x10470, 0xB7FA),
        ], sites);
        Assert.Equal(34, Pack.FieldSounds!.Alerts!.Sound);
    }

    [OriginalDataFact]
    public void Clicks_AreThe24Sites_AndThePackWiresTheMenuBarAndThePicker()
    {
        var g = Prg;
        var sites = new List<int>();
        for (var at = 0; at + 6 <= 0x1e298; at += 2)
            if (U16(g, Header + at) == 0x4EB9 && U32(g, Header + at + 2) == 0xB7E6)
                sites.Add(at);
        Assert.Equal(24, sites.Count);
        Assert.Contains(0x294, sites); // the menu bar's MN_SELECTED
        Assert.Contains(0x25b4, sites); // the transport picker, before its cancel check (0x25c8)
        Assert.Equal(33, Pack.FieldSounds!.Click);
    }

    [OriginalDataFact]
    public void TransportFade_MatchesTheLoopsOf0x260a()
    {
        var g = Prg;
        var fade = Pack.TransportFade!;
        AssertCall(g, 0x262c, 22, 0xB80E);
        Assert.Equal(0x7C00 | fade.OutVbl, U16(g, Header + 0x2648)); // moveq #10, d6: Vsyncs per register out
        Assert.Equal(0x7C00 | fade.HoldVbl, U16(g, Header + 0x2666)); // moveq #100, d6
        Assert.Equal(0x7C00 | fade.InVbl, U16(g, Header + 0x269e)); // moveq #7, d6: per register back
        Assert.Equal(fade.Registers, U16(g, Header + 0x2662)); // cmp.w #16, d7
        Assert.Equal(0x23A62, U32(g, Header + 0x2688)); // the restored colours
        Assert.Equal(0xB9E2, U32(g, Header + 0x26be)); // then the silence
        Assert.Equal(0x21A32, U32(g, Header + 0xb9e8)); // Dosound(0x21a32)
        var town = Enumerable.Range(0, 16).Select(i =>
        {
            var w = U16(g, Header + 0x23A62 + 2 * i); // 0x0RGB, 3 bits each
            return (w >> 8 & 7, w >> 4 & 7, w & 7);
        }).ToList();
        Assert.Equal(16, fade.Palette.Count);
        for (var i = 0; i < 16; i++)
        {
            var hex = fade.Palette[i];
            var (r, gg, b) = town[i];
            Assert.Equal((r, gg, b), (Level(hex, 1), Level(hex, 3), Level(hex, 5)));
        }
        static int Level(string hex, int at) => (int)Math.Round(Convert.ToInt32(hex.Substring(at, 2), 16) / 255.0 * 7);
    }

    /// <summary>s162 (atari-st FORMAT.md 7.1): no II sound writes a fixed level 1–3 to registers 8–10, so the ST circuit
    /// model's excess at those levels (10–23 % over the measured STF table) is never reached by a fixed level; the title
    /// script uses 10–15, the effects 7–15, besides 0 and the envelope (bit 4).</summary>
    [OriginalDataFact]
    public void NoSound_WritesAFixedLevelOneToThree()
    {
        var tune = Pack.TitleSequence!.Tune!;
        var title = Assert.IsType<DosoundScore>(tune.TryLoad());
        var image = Prg;
        var scores = Enumerable.Range(0, 36).Select(n => Effects.Score(image, n)).Append(title).ToList();
        Assert.All(scores, Assert.NotNull);
        var fixedLevels = scores.SelectMany(sc => sc!.Writes)
            .Where(w => w.Register is >= 8 and <= 10 && (w.Value & 0x10) == 0 && (w.Value & 0x0F) != 0)
            .Select(w => w.Value & 0x0F).Distinct().Order().ToList();
        Assert.NotEmpty(fixedLevels);
        Assert.DoesNotContain(fixedLevels, v => v <= 3);
        Assert.Equal([10, 11, 12, 13, 14, 15], title.Writes.Where(w => w.Register == 8 && w.Value is > 0 and < 16)
            .Select(w => w.Value).Distinct().Order());
    }
}
