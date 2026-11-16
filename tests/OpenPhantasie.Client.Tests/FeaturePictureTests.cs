using System.Text.Json.Nodes;
using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client.Tests;

/// <summary>
/// s221: the found-treasure / dungeon-feature pictures beside their texts (<see cref="FeaturePictureSlots"/>, the packs'
/// <c>event.*</c> slots and <c>find_pictures</c>; C64 I / II, <c>reverse-engineering/c64/phantasie1/GRAPHICS.md</c> 7).
/// A pack without them draws nothing there and the prompt panel keeps its old geometry.
/// </summary>
public class FeaturePictureTests
{
    private static GraphicsPack Pack(string path, string id) =>
        GraphicsPackCatalog.Load(path).Find(id) ?? throw new InvalidOperationException($"{id} extracts missing");

    [OriginalDataFact]
    public void C64Packs_ResolveTheEventSlots_AndFindPicturesById()
    {
        foreach (var path in new[] { GameDataRepository.DefaultPhantasie1Path, GameDataRepository.DefaultPathFor(GameId.P2) })
        {
            var pack = Pack(path, "c64");
            foreach (var name in new[] { PictureNames.EventTrap, PictureNames.EventStairs, PictureNames.EventLock, PictureNames.EventExit })
                Assert.NotNull(pack.Resolve(name));
            for (var id = 1; id <= 180; id++)
                Assert.NotNull(pack.Resolve(pack.FindPicture(id)!));
            Assert.Null(pack.FindPicture(0));
            Assert.Null(pack.FindPicture(181));
            // The C64's float colour tests: only ids 41, 61 and 81 get their group's own colour (GRAPHICS.md 7).
            Assert.Equal("event.find.weapon-yellow", pack.FindPicture(41));
            Assert.Equal("event.find.weapon", pack.FindPicture(42));
            Assert.Equal("event.find.good-weapon-light-blue", pack.FindPicture(61));
            Assert.Equal("event.find.mg-weapon-cyan", pack.FindPicture(81));
            Assert.Equal("event.find.scroll", pack.FindPicture(121));
        }
    }

    /// <summary>Every other shipped pack has no event.* picture and no find_pictures: nothing is drawn, nothing moves.</summary>
    [Fact]
    public void OtherPacks_HaveNoFeaturePictures()
    {
        var root = Path.GetDirectoryName(Path.GetDirectoryName(GameDataRepository.DefaultPhantasie1Path))!;
        var manifests = Directory.EnumerateFiles(Path.Combine(root, "GameData"), "*.json", SearchOption.AllDirectories)
            .Where(f => Path.GetFileName(Path.GetDirectoryName(f)) == "graphics" && !f.EndsWith("index.json", StringComparison.Ordinal))
            .ToList();
        Assert.NotEmpty(manifests);
        foreach (var file in manifests)
        {
            if (JsonNode.Parse(File.ReadAllText(file)) is not JsonObject pack || !pack.ContainsKey("pictures"))
                continue;
            var c64 = (string?)pack["id"] == "c64" && (string?)pack["game"] is "phantasie1" or "phantasie2";
            var hasEvent = pack["pictures"] is JsonObject pictures && pictures.Any(p => p.Key.StartsWith("event.", StringComparison.Ordinal));
            Assert.True(c64 == hasEvent, $"{file}: event.* pictures");
            Assert.True(c64 == pack.ContainsKey("find_pictures"), $"{file}: find_pictures");
        }
    }

    [Fact]
    public void Slots_FollowTheMoment()
    {
        Assert.Equal(PictureNames.EventTrap, FeaturePictureSlots.ForPrompt(SessionPromptKind.DungeonTrap, null, null));
        Assert.Equal(PictureNames.EventExit, FeaturePictureSlots.ForPrompt(SessionPromptKind.DungeonExitTakeIt, null, null));
        Assert.Null(FeaturePictureSlots.ForPrompt(SessionPromptKind.Treasure, 41, null)); // no pack: no table
        Assert.Null(FeaturePictureSlots.ForPrompt(SessionPromptKind.DungeonExitSaveMap, null, null));
        Assert.Null(FeaturePictureSlots.ForPrompt(SessionPromptKind.DungeonLock, null, null)); // the listening door: none
        Assert.Equal(PictureNames.EventStairs, FeaturePictureSlots.ForStep(FeaturePicture.Stairs));
        Assert.Equal(PictureNames.EventLock, FeaturePictureSlots.ForStep(FeaturePicture.Lock));
        Assert.Null(FeaturePictureSlots.ForStep(FeaturePicture.Trap)); // a question: in its panel
        Assert.Null(FeaturePictureSlots.ForStep(FeaturePicture.None));
    }

    /// <summary>No picture: the prompt panel's text column is the old one (x 14, 48 columns).</summary>
    [Fact]
    public void PromptColumns_WithoutAPicture_AreTheOldOnes_WithOne_WrapNarrower()
    {
        Assert.Equal((0, 14, 48), UiOverlays.PromptColumns(null));
        var (cells, x, chars) = UiOverlays.PromptColumns(56); // 7 x 7 characters
        Assert.Equal((11, 14 + 11 * PixelFont.Advance, 37), (cells, x, chars));
        Assert.True(x >= 14 + 56 + 1); // the text clears the picture
        var exit = UiOverlays.PromptColumns(64);
        Assert.True(exit.TextX >= 14 + 64 + 1 && exit.TextX + exit.MaxChars * PixelFont.Advance <= 14 + 48 * PixelFont.Advance);
        // A long line wraps, never cut: every word is kept.
        var line = "YOU FIND GOOD TREASURE NUMBER ONE HUNDRED AND SIXTY ONE. KEEP IT?";
        var lines = UiOverlays.PromptLines([line], chars);
        Assert.All(lines, l => Assert.True(l.Length <= chars));
        Assert.Equal(line.Split(' '), lines.SelectMany(l => l.Split(' ', StringSplitOptions.RemoveEmptyEntries)));
    }

    [Fact]
    public void StepPictureBox_SitsNextToItsText_OnScreen_AtFullSize()
    {
        // I's DOS band (rows 9..11 on the 8 x 8 grid): the box goes under it, centred.
        var band = FeaturePictureSlots.BoxBeside(56, 56, (0, 72, 320, 24), centred: true);
        Assert.Equal((56 + 8, 56 + 8), (band.W, band.H));
        Assert.Equal(72 + 24 + 2, band.Y);
        Assert.Equal((320 - band.W) / 2, band.X);
        Assert.True(band.Y + band.H <= UiOverlays.ContentBottom);
        // The message line at the foot: no room under it, so over it, ending at the help band, from the line's left edge.
        var foot = FeaturePictureSlots.BoxBeside(56, 56, (4, UiOverlays.MessageY, 312, 8), centred: false);
        Assert.Equal(UiOverlays.ContentBottom, foot.Y + foot.H);
        Assert.Equal(4, foot.X);
    }
}
