using OpenPhantasie.Data;
using OpenPhantasie.Data.Audio;

namespace OpenPhantasie.Core.Tests;

/// <summary>
/// s128, I Amiga's sound effects (Amiga FORMAT.md "Sound effects"): <c>h89 +0xee(n)</c> plays entry n of the 30 longs at
/// h91 <c>+0x1f4</c>, a Dosound script in hunk 90, through the title tune's player <c>+0x304</c>.
/// </summary>
public class P1AmigaSoundEffectTests
{
    /// <summary>Hunk 89's code and hunk 90's data in <c>game</c> (file offsets of their first bytes).</summary>
    private const int H89 = 0x289f4, H90 = 0x29798;

    private static GraphicsPack Pack =>
        GraphicsPackCatalog.Load(GameDataRepository.DefaultPathFor(GameId.P1)).Find("amiga")
        ?? throw new InvalidOperationException("I Amiga pack missing");

    private static AmigaDosoundEffects Effects => Pack.DosoundEffects!;

    private static byte[] Game => Effects.TryLoadImage() ?? throw new InvalidOperationException("game missing");

    private static int U32(byte[] b, int at) => (b[at] << 24) | (b[at + 1] << 16) | (b[at + 2] << 8) | b[at + 3];

    [OriginalDataFact]
    public void Pack_ThirtySounds_InTheProgramFile_NoP3Effects()
    {
        var fx = Effects;
        Assert.Null(Pack.SoundEffects);
        Assert.Equal(Enumerable.Range(0, 33), fx.Sounds.Keys.Order()); // 0-29 the table, 30 the sparkle's h90 +0x299 (s133), 31 the spell list's tone h90 +5 (s134), 32 the wall's beep h90 +0x10 (s141)
        Assert.Equal([H90 + 0x299], fx.Sounds[30]);
        Assert.Equal([H90 + 5], fx.Sounds[31]);
        Assert.Equal([H90 + 0x10], fx.Sounds[32]); // h89 +0xe0 plays the default directly (as 1)
        Assert.EndsWith("unpacked/PHANT/game", fx.FilePath.Replace('\\', '/'));
        Assert.Equal(3579545, fx.ClockHz);
        Assert.Equal(Enumerable.Repeat(H90 + 0x24d, 6), fx.Sounds[27]); // +0x158(6, h90 +0x24d)
        Assert.Equal(new byte[] { 0, 0, 0, 0, 0xFF, 0x00, 0x2D }, Game.AsSpan(H90, 7).ToArray()); // h90 starts here
    }

    [OriginalDataFact]
    public void Pack_MatchesTheTableH89Fills()
    {
        // h89 +0x1a: every entry = h90 +0x10 (adda.l #0x1f4; move.l #0x10, (a0)), then 15 × move.l #h90+x, h91+0x1f4+4n.
        var g = Game;
        Assert.Equal(0x10, U32(g, H89 + 0x22));
        var table = Enumerable.Range(0, 30).ToDictionary(n => n, _ => 0x10);
        for (var at = H89 + 0x2a; at < H89 + 0xca; at += 10)
        {
            Assert.Equal(0x23FC, (g[at] << 8) | g[at + 1]); // move.l #imm, abs.l
            table[(U32(g, at + 6) - 0x1f4) / 4] = U32(g, at + 2);
        }
        Assert.Equal(16, table.Values.Count(v => v != 0x10));
        foreach (var (n, off) in table)
            if (n != 27) // 27 is the dispatcher's own case
                Assert.Equal([H90 + off], Effects.Sounds[n]);
        // The default beep serves these numbers.
        Assert.Equal([1, 2, 6, 7, 9, 11, 12, 13, 14, 24, 26, 27, 28, 29], table.Where(kv => kv.Value == 0x10).Select(kv => kv.Key));
    }

    [OriginalDataTheory]
    [InlineData(22, 1, 30)]   // one step: 82 19 → ⌊25·6/5⌋
    [InlineData(25, 125, 125)] // the sweep fe +0x32 … 0x68 in byte steps, one tick each
    [InlineData(1, 1, 7)]     // the default beep
    [InlineData(24, 1, 7)]
    [InlineData(21, 15, 31)]  // the decay 7, 4, 3, 3, 2, 2, 2, 1 × 8
    [InlineData(27, 12, 60)]  // h90 +0x24d six times
    [InlineData(23, 19, 22)]  // the defeat's
    public void Sound_StepsAndTicks(int sound, int steps, int ticks)
    {
        var s = Effects.Score(Game, sound)!;
        Assert.Equal(steps, s.Steps.Count);
        Assert.Equal(ticks, s.Ticks);
        Assert.Equal(ticks / 50.0, Effects.Seconds(Game, sound), 9);
    }

    [OriginalDataFact]
    public void Sound22_OneLowNote_OnA_ChannelOneOff()
    {
        // 07 fc, 08 0f, 00 70, 01 17, 82 19: A = 0x1770 at volume 15 × 4; B on but period and volume 0; reg 7 written.
        var step = Effects.Score(Game, 22)!.Steps.Single();
        Assert.Equal(new PaulaNote(true, AmigaDosoundScore.Period(0x1770), 60), step.Channels[2]);
        Assert.Equal(step.Channels[2], step.Channels[3]);
        Assert.Equal(new PaulaNote(true, 0, 0), step.Channels[0]);
        Assert.False(step.Channels[1].Sent);
        Assert.Equal(10738, AmigaDosoundScore.Period(0x1770));
    }

    [OriginalDataFact]
    public void Sound25_SweepsTheLowByte_From0x1fe()
    {
        var s = Effects.Score(Game, 25)!;
        Assert.Equal(AmigaDosoundScore.Period(0x1fe), s.Steps[0].Channels[2].Period);
        Assert.Equal(912, AmigaDosoundScore.Period(0x1fe));
        Assert.Equal(AmigaDosoundScore.Period(0x130), s.Steps[1].Channels[2].Period); // 0xfe + 0x32 wraps to 0x30
        Assert.Equal(AmigaDosoundScore.Period(0x136), s.Steps[^1].Channels[2].Period); // the last before 0x68
        Assert.All(s.Steps, st => Assert.False(st.Channels[0].Sent)); // 07 fe: B off
    }

    [OriginalDataFact]
    public void Script_JoinsRepeats_UpToEachFF()
    {
        var g = Game;
        var one = Effects.Script(g, 27)!;
        Assert.Equal(6 * 18 + 1, one.Length); // 18 bytes before the 0xFF at h90 +0x25f
        Assert.Equal(0xFF, one[^1]);
        Assert.Null(Effects.Script(g, 33));
    }
}
