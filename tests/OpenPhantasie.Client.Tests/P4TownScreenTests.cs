using System.Buffers.Binary;
using Microsoft.Xna.Framework.Input;
using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client.Tests;

/// <summary>
/// IV's town buildings and party menus on <see cref="P4DungeonScreen"/> (s102): keys reach the building prompts, the box
/// shows the building's lines, a building or menu blocks saving, and F / P / C / U open the party menus.
/// </summary>
public class P4TownScreenTests
{
    private static readonly GameOptions Code = GameOptions.ForPreset(GameId.P4, OptionPreset.Dos);

    private static P4DungeonScreen InTown(int town = 2)
    {
        var game = P4Preview.StartInTown(town, Code);
        var s = new P4DungeonScreen(game);
        // The preview party carries no pool or pack, so the town opens at once under the code's rules.
        Assert.Equal(P4TownPrompt.Menu, game.Town.Prompt);
        return s;
    }

    [OriginalDataFact]
    public void Guild_OpensWithItsMenu_AndBlocksSaving()
    {
        var s = InTown();
        s.HandleKeys([Keys.D1]); // 1 Guild
        Assert.True(s.Game.Town.InBuilding);
        var lines = s.BoxLines().Select(l => l.Text).ToList();
        Assert.Contains(lines, l => l.Contains(GameText.S("p4x.0x3fabc"), StringComparison.Ordinal));
        Assert.Null(s.HandleKeys([Keys.F5]));
        Assert.Equal(UiText.S("p4.town.no_save_inside"), s.Note);
        s.HandleKeys([Keys.D4]); // Leave
        Assert.False(s.Game.Town.InBuilding);
        Assert.Equal(P4ScreenRequest.Save, s.HandleKeys([Keys.F5]));
    }

    [OriginalDataFact]
    public void Bank_Withdraw_TakesTypedDigits()
    {
        var s = InTown();
        var m = s.Game.Party.Members[0];
        m.Account = 500;
        foreach (var x in s.Game.Party.Members.Skip(1))
            x.Account = 0;
        s.HandleKeys([Keys.D4]); // 4 Bank
        s.HandleKeys([Keys.D2]); // W
        Assert.Equal(P4TownPrompt.Number, s.Game.Town.Prompt);
        foreach (var c in "120")
            s.OnTextInput(c);
        s.HandleKeys([Keys.Enter]);
        Assert.Equal(380, m.Account);
        Assert.Equal(120, s.Game.Party.Gold);
    }

    [OriginalDataFact]
    public void PartyMenus_ProfileOpensAndCloses()
    {
        var s = InTown();
        s.HandleKeys([Keys.F]);
        Assert.True(s.MenuOpen);
        s.HandleKeys([Keys.D1]); // the first member
        Assert.Contains(s.BoxLines(), l => l.Text.Contains(s.Game.Party.Members[0].Name, StringComparison.Ordinal));
        s.HandleKeys([Keys.Space]); // back to the member pick
        s.HandleKeys([Keys.Space]); // close
        Assert.False(s.MenuOpen);
    }

    [OriginalDataFact]
    public void Outdoors_P_ShowsPartyInfo()
    {
        var game = P4Preview.StartInTown(2, Code);
        var s = new P4DungeonScreen(game);
        s.HandleKeys([Keys.D7]); // Leave
        Assert.Equal(P4Place.Outdoors, game.Place);
        s.HandleKeys([Keys.P]);
        Assert.True(s.MenuOpen);
        Assert.Contains(s.BoxLines(), l => l.Text.Contains(GameText.S("p4x.0x433ac"), StringComparison.Ordinal));
        s.HandleKeys([Keys.Space]);
        Assert.False(s.MenuOpen);
    }

    [OriginalDataFact]
    public void Outdoors_SceneIsTheMapPicture_MTogglesTheOverview()
    {
        var game = P4Preview.StartInTown(2, Code);
        var s = new P4DungeonScreen(game);
        s.HandleKeys([Keys.D7]); // Leave
        Assert.Equal(P4Place.Outdoors, game.Place);
        Assert.Equal(P4DungeonScreen.OutdoorCloseUp, s.SceneName);
        s.HandleKeys([Keys.M]);
        Assert.Equal(P4DungeonScreen.OutdoorOverview, s.SceneName);
        s.HandleKeys([Keys.P]);
        Assert.Equal(P4DungeonScreen.OutdoorOverview, s.SceneName); // s140: the view stays under a party menu (its panel's strip)
    }

    [OriginalDataFact]
    public void Town_T_StartsTheExchange()
    {
        var s = InTown();
        s.HandleKeys([Keys.T]);
        Assert.Equal(P4TownPrompt.Pick, s.Game.Town.Prompt);
        Assert.Contains(s.BoxLines(), l => l.Text == GameText.S("p4x.0x4278d"));
    }

    // --- T6.4: IV's pictures (s107) ---------------------------------------------------------------------------------

    private static (int W, int H) PngSize(string path)
    {
        var head = File.ReadAllBytes(path).AsSpan(16, 8);
        return (BinaryPrimitives.ReadInt32BigEndian(head), BinaryPrimitives.ReadInt32BigEndian(head[4..]));
    }

    [OriginalDataFact]
    public void X68000Pack_EveryTownScene_IsWholeAndFitsBesideTheText()
    {
        // P4.X's 640 × 400 layout space; a scene's pictures stack as _xunpack drew them, 1:1 (FORMAT.md §6.9).
        var pack = GraphicsPackCatalog.Load(GameDataRepository.DefaultPathFor(GameId.P4)).Default!;
        Assert.Equal((640, 400), (pack.Screen.Width, pack.Screen.Height));
        string[] expected = ["p4.street.0", "p4.street.1", "p4.street.2", "p4.guild", "p4.inn", "p4.bank", "p4.shipwright",
            "p4.mystic", "p4.hall", .. Enumerable.Range(0, 9).Select(i => $"p4.hall.history.{i}"), "p4.ending.street",
            "p4.ending.banquet", "p4.ending.cell", "p4.scroll"];
        Assert.Equal(expected.Order(), pack.Scenes.Keys.Order());
        foreach (var (name, parts) in pack.Scenes)
        {
            var sizes = parts.Select(p => (p, PngSize(pack.Resolve(p.Picture)!))).ToList();
            var w = sizes.Max(x => x.p.X + x.Item2.W);
            var h = sizes.Max(x => x.p.Y + x.Item2.H);
            Assert.True(w <= 2 * 196 && h <= 2 * 192, $"{name} is {w} × {h}");
        }
        Assert.Equal((312, 348), PngSize(pack.Resolve("p4.street.1")!));
        Assert.Equal(new ScenePicture("p4.hall.stage", 0, 202), pack.Scenes["p4.hall"][1]);
        Assert.Equal(new ScenePicture("p4.ending.town", 0, 134), pack.Scenes["p4.ending.street"][1]); // END1 at (160, 154)
        // The outdoor map pictures (s108): one per map 1–24 (_omaps 0x42DA2: 7 / 11 / 12 → OT06, 18 / 19 / 24 → OT16).
        foreach (var map in Enumerable.Range(1, 24))
            Assert.Equal((104, 104), PngSize(pack.Resolve(P4DungeonScreen.OutdoorPicture(map))!));
        Assert.EndsWith("OT06.png", pack.Resolve(P4DungeonScreen.OutdoorPicture(11))!, StringComparison.Ordinal);
        Assert.EndsWith("OT16.png", pack.Resolve(P4DungeonScreen.OutdoorPicture(24))!, StringComparison.Ordinal);
    }

    [OriginalDataFact]
    public void SceneName_StreetByTownType_BuildingsAndNoneInMenus()
    {
        var s = InTown(2); // Pendleton: town type 1 → TOWN_2
        Assert.Equal("p4.street.1", s.SceneName);
        s.HandleKeys([Keys.D1]); // Guild
        Assert.Equal("p4.guild", s.SceneName);
        s.HandleKeys([Keys.D1]); // the roster clears it
        Assert.Null(s.SceneName);
        Assert.Equal("p4.street.0", InTown(1).SceneName); // Pendragon → TOWN_1
        var menu = InTown(2);
        menu.HandleKeys([Keys.U]);
        Assert.True(menu.MenuOpen);
        Assert.Null(menu.SceneName);
    }

    [OriginalDataFact]
    public void AncestorText_HallPerson_NewPerson_Unknown()
    {
        // _page1 → _dsp_uni: "name a Social  Race  Class  Level n" / "Social  Race  Class" / "Unknown".
        var s = InTown(1);
        Assert.Equal("Lord Wood a Royalty  Human  Ranger  Level 16", s.AncestorText(new P4AncestorRef(P4AncestorKind.Hall, 0)));
        Assert.Equal("Noble  Elf  Ranger", s.AncestorText(new P4AncestorRef(P4AncestorKind.New, 1, Social: 3, Race: 2, Class: 2)));
        Assert.Equal("Unknown", s.AncestorText(P4AncestorRef.None));
    }

    [Theory]
    [InlineData(320, 3, 960)]
    [InlineData(640, 3, 1280)] // IV's 640 × 400: its own 2× (nearest to the layout's 3×), not 1920
    [InlineData(640, 1, 640)]
    [InlineData(640, 4, 1280)]
    public void WindowSize_CountsLayoutPixels(int width, int scale, int expected) =>
        Assert.Equal(expected, VirtualScreen.PreferredSize((width, width * 5 / 8), width, scale).W);

    [Theory]
    [InlineData(P4Building.Guild, 0)]
    [InlineData(P4Building.Mystic, 1)]
    [InlineData(P4Building.Armory, 2)]
    [InlineData(P4Building.Bank, 3)]
    [InlineData(P4Building.Inn, 4)]
    [InlineData(P4Building.HallOfGlory, 5)]
    [InlineData(P4Building.Shipwright, 5)]
    [InlineData(P4Building.Leave, -1)]
    public void DoorPick_IsP4XsBuildingIndex(P4Building building, int pick) =>
        Assert.Equal(pick, P4DungeonScreen.DoorPick(building)); // _animdoor's column (FORMAT.md 6.10)

    [OriginalDataFact]
    public void WithoutAPack_ABuildingOpensAtOnce_AndTickDoesNothing()
    {
        var s = InTown();
        s.Tick(1000);
        Assert.Null(s.Animation);
        s.HandleKeys([Keys.D1]);
        Assert.False(s.DoorOpen);
        Assert.True(s.Game.Town.InBuilding);
    }

    [Fact]
    public void PlainBox_EndsAbovePartyPanel()
    {
        // s114: the box was a fixed 150 pixels and ran under the party panel (map panel 4 + 117, party at 125), so the
        // shop list's last rows sat on the border; it now ends 4 pixels above the panel and scrolls the rest.
        const int mapRight = 4 + (2 * P4DungeonScreen.ViewRadius + 1) * 9, partyTop = mapRight + 4;
        var box = P4DungeonScreen.BoxArea(mapRight, partyTop);
        Assert.Equal(partyTop - 4, box.Bottom);
        Assert.Equal(316, box.Right);
    }
}
