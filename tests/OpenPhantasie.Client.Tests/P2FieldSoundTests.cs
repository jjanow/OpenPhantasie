using OpenPhantasie.Client.Audio;
using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client.Tests;

/// <summary>
/// s139, II ST: the moments outside combat (<see cref="FieldCues"/>) played from the pack's <c>field_sounds</c> (FORMAT.md
/// 7.4): moment → sound number, the <c>0xb8f2</c> pacing per COMBAT SPEED, and each mapped sound rendering on the YM.
/// </summary>
public class P2FieldSoundTests
{
    private static GraphicsPack Pack() =>
        GraphicsPackCatalog.Load(GameDataRepository.DefaultPathFor(GameId.P2)).Find("atari-st")!;

    [OriginalDataFact]
    public void Cues_MapToTheStsNumbers_InOrder_UnknownSkipped()
    {
        var field = Pack().FieldSounds!;
        Assert.Equal([19, 34, 29, 28, 16, 26, 22, 12, 34, 19],
            Game1.FieldSoundsFor(field, [FieldCues.TownPrompt, FieldCues.DungeonWall, FieldCues.DisarmFails,
                FieldCues.EventSpellCast, FieldCues.EventPartyMoved, "no.such.moment", FieldCues.EventNoise,
                FieldCues.Transport, FieldCues.FieldResurrected, FieldCues.TownCastDone, FieldCues.DungeonPrompt]));
    }

    [OriginalDataTheory]
    [InlineData(AmigaCombatSpeed.Fast, false)] // 5 ms: the new sound cuts the old one off
    [InlineData(AmigaCombatSpeed.Normal, true)] // 10 ms: it waits for it
    [InlineData(AmigaCombatSpeed.Slow, true)] // 1000 ms
    public void Pacing_FollowsTheSpeedWord(AmigaCombatSpeed speed, bool waits)
    {
        var pack = Pack();
        Assert.Equal(waits, Game1.FieldSoundsWait(pack.FieldSounds!, pack.StCombatLines!.SpeedMs((int)speed)));
    }

    [OriginalDataTheory]
    [InlineData(AmigaCombatSpeed.Fast, 1360)] // 5 + 1000 + 355
    [InlineData(AmigaCombatSpeed.Normal, 1365)] // s141 Hatari: box shown -> 12 in 1360 ms
    [InlineData(AmigaCombatSpeed.Slow, 2355)] // s141 Hatari: 2360 ms
    public void FieldResurrection_SoundsAfterTheBoxsHoldAndRedraw(AmigaCombatSpeed speed, int ms)
    {
        // 0x92ce 0xa18c: "%s IS RESURRECTED" held Speed + 1000 ms (a key ends it), 0x17210 redraws, then 0x92d8 plays 12.
        var pack = Pack();
        var delay = pack.FieldSounds!.Delays[FieldCues.FieldResurrected];
        Assert.Equal(ms, delay.Ms(pack.StCombatLines!.SpeedMs((int)speed)));
        Assert.Equal(355, delay.AfterMs); // what is left once a key ends the hold
        Assert.False(pack.FieldSounds.Delays.ContainsKey(FieldCues.TownPrompt)); // the others sound before their box
    }

    [OriginalDataFact]
    public void Pacing_NoSpeedTable_PlaysInTurn() =>
        Assert.True(Game1.FieldSoundsWait(Pack().FieldSounds!, null));

    [OriginalDataFact]
    public void EveryMappedSound_RendersOnTheYm()
    {
        var pack = Pack();
        var fx = pack.StDosoundEffects!;
        var image = fx.TryLoadImage()!;
        Assert.All(pack.FieldSounds!.Cues.Values.Distinct(),
            n => Assert.True(SoundEffectPlayer.Render(fx, image, n, 8000)?.RenderAll().Length > 0, $"sound {n}"));
    }

    [Fact]
    public void OtherGamesPacks_HaveNoFieldSounds()
    {
        Assert.Null(GraphicsPackCatalog.Load(GameDataRepository.DefaultPathFor(GameId.P1)).Find("amiga")?.FieldSounds);
        Assert.Null(GraphicsPackCatalog.Load(GameDataRepository.DefaultPathFor(GameId.P3)).Find("amiga")?.FieldSounds);
    }
}
