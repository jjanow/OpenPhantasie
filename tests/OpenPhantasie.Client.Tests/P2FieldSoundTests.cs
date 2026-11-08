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
    public void FieldResurrection_LineGoesInTreeFortysBox_InTheMapsPens()
    {
        // s165: 0x92c8 -> 0xa18c -> 0xa124: OUT.RSC tree 40 at (12, 75), G_BOX border VDI 0, fill VDI 6 (register 3), text
        // VDI 0, drawn over the field's map palette; nothing on the message line.
        var pack = Pack();
        var delay = pack.FieldSounds!.Delays[FieldCues.FieldResurrected];
        Assert.Equal("map", delay.BoxPalettes!["overworld"]);
        Assert.Equal("map", delay.BoxPalettes["dungeon"]);
        var boxes = pack.StCombatLines!.EndBoxes!;
        Assert.Equal((0, 3, 0), boxes.Pens);
        var map = pack.Palettes!["map"];
        Assert.Equal((map[0], map[3], map[0]), boxes.Colours(map));
        Assert.Equal((boxes.BorderColour, boxes.FillColour, boxes.TextColour), boxes.Colours(null)); // the combat colours
        Assert.Equal("#FF6D00", boxes.FillColour);
        var layout = boxes.Layout(["ZANDOR IS RESURRECTED"]);
        Assert.Equal((12, 75, 304, 40), (layout.X, layout.Y, layout.Width, layout.Height));
        Assert.Equal((12 + 8 * (2 + 7), 75 + 16), (layout.Rows[0].X, layout.Rows[0].Y)); // (35 - 21) / 2 = 7 spaces
        Assert.Equal(1000, boxes.OneLine.HoldMs); // 0xa16a: Speed + 1000
    }

    [Fact]
    public void FieldBox_HeldUntilItsTimeOrAKey_OwnsTheFrame()
    {
        var box = new P2FieldBox();
        Assert.False(box.Update(0, pressed: true)); // nothing up: the key goes on
        box.Open(" ZANDOR IS RESURRECTED ", "overworld", 10, 1010);
        Assert.Equal(["ZANDOR IS RESURRECTED"], box.Rows);
        Assert.Equal("overworld", box.Mode);
        Assert.True(box.Update(10.5, pressed: false));
        Assert.NotNull(box.Rows);
        Assert.True(box.Update(11.01, pressed: false)); // the hold ran out this frame
        Assert.Null(box.Rows);
        Assert.False(box.Update(11.02, pressed: true));
        box.Open("X", "dungeon", 20, 1010);
        Assert.True(box.Update(20.1, pressed: true)); // a key ends it and goes no further
        Assert.Null(box.Rows);
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
