using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client.Tests;

/// <summary>
/// s98: Phantasie III's party-defeat scene as the Amiga draws it (root <c>0x1dc16</c>): the whole Astral Plane picture,
/// the portraits under it, the text one line at a time; and the prompt box wraps long lines instead of cutting them.
/// </summary>
public class AstralSceneTests
{
    [Fact]
    public void Layout_KeepsThePictureWhole_PortraitsUnderIt_TextOnRow24()
    {
        Assert.True(AstralScene.RowTop > 140); // w2's art ends on row 140
        Assert.True(AstralScene.RowTop + AstralScene.PortraitHeight <= AstralScene.TextTop);
        Assert.True(AstralScene.TextTop + PixelFont.GlyphHeight <= UiOverlays.ScreenBottom);
        Assert.True(6 * AstralScene.SlotWidth + AstralScene.PortraitWidth <= 320); // seven slots fit the width
    }

    [Fact]
    public void Row_ShowsTheLivingRace_ThenEachFateAsItsLinePrints()
    {
        var a = new Character { Name = "A", Race = Race.Dwarf, BirthRace = Race.Dwarf };
        var b = new Character { Name = "B", Race = Race.Undead, BirthRace = Race.Elf }; // Core already made B undead
        var c = new Character { Name = "C", Race = Race.Human, BirthRace = Race.Human };
        var result = new PartyDefeatResult
        {
            Fates = [(a, DefeatFate.Resurrected), (b, DefeatFate.Undead), (c, DefeatFate.Destroyed)],
        };

        var before = AstralScene.Row(result, fatesShown: 0);
        Assert.Equal([(int)Race.Dwarf, (int)Race.Elf, (int)Race.Human], before.Select(p => p.RaceId));
        Assert.All(before, p => Assert.Equal(DefeatPortraitKind.Race, p.Kind));

        var after = AstralScene.Row(result, fatesShown: 3);
        Assert.Equal(new DefeatPortrait(DefeatPortraitKind.Race, (int)Race.Dwarf), after[0]);
        Assert.Equal(new DefeatPortrait(DefeatPortraitKind.Race, (int)Race.Undead), after[1]);
        Assert.Equal(DefeatPortraitKind.Grave, after[2].Kind);

        var mid = AstralScene.Row(result, fatesShown: 1);
        Assert.Equal((int)Race.Elf, mid[1].RaceId); // B's line has not printed yet
    }

    [Fact]
    public void LineColour_UndeadFateIsColour14_OthersAndTheIntro15()
    {
        var a = new Character { Name = "A" };
        var b = new Character { Name = "B" };
        var result = new PartyDefeatResult { Fates = [(a, DefeatFate.Resurrected), (b, DefeatFate.Undead)] };
        // Outcome page: 3 intro lines, then one line per member (0x1df0a: string 23 → colour 14 for name and fate).
        Assert.Equal([15, 15, 15, 15, 14], Enumerable.Range(0, 5).Select(i => AstralScene.LineColour(result, 5, i)));

        var amiga = GraphicsPackCatalog.Load(GameDataRepository.DefaultPathFor(GameId.P3)).Find("amiga");
        if (amiga is null)
            return;
        var w2 = amiga.Palettes[AstralScene.Palette];
        Assert.Equal("#FFDB49", w2[AstralScene.UndeadColour]); // D/W2.cmp word 0x762
        Assert.Equal("#FFFFFF", w2[AstralScene.TextColour]);
    }

    [Fact]
    public void GodsReward_FourPages_PictureWhole_PortraitsUnderIt_SpeechEndsWithPressAKey()
    {
        var scene = new RewardScene(true, [Race.Human, Race.Dwarf], ["noises", "intensity"], ["god", "cause", "farewell", "away"]);
        var pages = GodsRewardScene.Pages(scene, "Press a key");
        Assert.Equal(["noises", "intensity", "Press a key", "god cause farewell away"], pages);
        Assert.Equal(GodsRewardScene.SpeechPage, pages.Count - 1);
        Assert.Equal(["god", "cause", "farewell", "away", "", "Press a key"], GodsRewardScene.SpeechLines(scene, "Press a key"));
        Assert.True(GodsRewardScene.RowTop > 121); // w0 / w1 art ends on row 120
        Assert.True(GodsRewardScene.RowTop + AstralScene.PortraitHeight <= GodsRewardScene.PressKeyY);
        Assert.True(GodsRewardScene.PressKeyY + PixelFont.GlyphHeight <= UiOverlays.ScreenBottom);
        Assert.True(5 * GodsRewardScene.SlotWidth + AstralScene.PortraitWidth <= 320); // six members fit

        var amiga = GraphicsPackCatalog.Load(GameDataRepository.DefaultPathFor(GameId.P3)).Find("amiga");
        if (amiga is null)
            return;
        Assert.EndsWith("screens/w1.png", amiga.Resolve(PictureNames.SceneRewardGood)!.Replace('\\', '/'));
        Assert.EndsWith("screens/w0.png", amiga.Resolve(PictureNames.SceneRewardEvil)!.Replace('\\', '/'));
    }

    [Fact]
    public void AmigaPack_HasTheGrave()
    {
        var amiga = GraphicsPackCatalog.Load(GameDataRepository.DefaultPathFor(GameId.P3)).Find("amiga");
        if (amiga is null)
            return;
        Assert.EndsWith("portraits/grave.png", amiga.Resolve(PictureNames.PortraitGrave)!.Replace('\\', '/'));
    }

    [Fact]
    public void PromptBox_WrapsLongLines_NeverCutsThem()
    {
        const string souls = "The souls of your defeated warriors begin the journey to the Astral Plane.";
        var lines = UiOverlays.PromptLines([souls, "", "A Great Demon appears and speaks..."], 48);
        Assert.Equal(3, lines.Count);
        Assert.Equal(souls, string.Join(" ", lines.Take(2)));
        Assert.All(lines, l => Assert.True(PixelFont.Measure(l) <= 48 * PixelFont.Advance));
    }

    [Fact]
    public void Sounds_TheDemon45_ThenEachFate48Or9()
    {
        var a = new Character { Name = "A" };
        var b = new Character { Name = "B" };
        var result = new PartyDefeatResult { Fates = [(a, DefeatFate.Destroyed), (b, DefeatFate.Resurrected)] };
        // 5 lines: two intro lines, the demon, then one fate line per member (0x1dde6, 0x1dfe6).
        Assert.Equal([null, null, 45, 48, 9], Enumerable.Range(0, 5).Select(i => AstralScene.SoundFor(result, 5, i)));
    }
}
