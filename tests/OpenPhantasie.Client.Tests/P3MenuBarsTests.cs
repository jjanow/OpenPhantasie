using Microsoft.Xna.Framework.Input;
using OpenPhantasie.Client;
using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client.Tests;

/// <summary>
/// s156: III Amiga's three menu bars as pack data (<c>keep_windows.menu_bars</c>, FORMAT.md 8.5 "The bars as pack data"):
/// the places written in the pack are the ones <c>0x12d6</c> computes from the strings at start-up, and the items are the
/// Amiga's lists (<c>0x2397c</c> fight, <c>0x23910</c> outside the towns, <c>0x238a4</c> town).
/// </summary>
public class P3MenuBarsTests
{
    private static IReadOnlyDictionary<string, CombatFieldMenuBar> Bars() =>
        GraphicsPackCatalog.Load(GameDataRepository.DefaultPathFor(GameId.P3)).Find("amiga")!.KeepWindows!.MenuBars;

    private static CombatFieldMenuBar Rule(CombatFieldMenuBar bar) =>
        UiOverlays.RuleMenuBar([.. bar.Menus.Select(m => (m.Title, m.Items))]);

    [OriginalDataTheory]
    [InlineData("fight")]
    [InlineData("outside")]
    [InlineData("town")]
    public void PackPlaces_AreThe0x12d6Rule(string name)
    {
        var bar = Bars()[name];
        var rule = Rule(bar);
        Assert.Equal((rule.Rows, rule.BarPen, rule.TextPen, rule.TextY), (bar.Rows, bar.BarPen, bar.TextPen, bar.TextY));
        Assert.Equal((rule.BoxPen, rule.ItemPen, rule.HighlightPen, rule.HighlightTextPen, rule.Frame, rule.Font),
            (bar.BoxPen, bar.ItemPen, bar.HighlightPen, bar.HighlightTextPen, bar.Frame, bar.Font));
        Assert.NotNull(bar.FontFile); // topaz 8 ships with the pack
        Assert.Equal(rule.Menus.Count, bar.Menus.Count);
        for (var m = 0; m < bar.Menus.Count; m++)
        {
            var (p, r) = (bar.Menus[m], rule.Menus[m]);
            Assert.Equal((r.TitleX, r.Highlight, r.Box, r.ItemHeight, r.ItemX, r.ItemY),
                (p.TitleX, p.Highlight, p.Box, p.ItemHeight, p.ItemX, p.ItemY));
            Assert.Equal(p.Items.Count, p.Actions.Count);
        }
    }

    [OriginalDataFact]
    public void FightAndOutsideItems_AreTheClientsLists()
    {
        var fight = Bars()["fight"];
        Assert.Equal(CombatPresenter.AmigaFieldMenus.Select(m => m.TitleId), fight.Menus.Select(m => m.Title));
        Assert.Equal(CombatPresenter.AmigaFieldMenus.Select(m => m.Items.Select(i => i.LabelId)), fight.Menus.Select(m => m.Items));
        var outside = Bars()["outside"];
        Assert.Equal(AmigaOutsideBar.Menus.Select(m => m.TitleId), outside.Menus.Select(m => m.Title));
        Assert.Equal(AmigaOutsideBar.Menus.Select(m => m.Items.Select(i => i.LabelId)), outside.Menus.Select(m => m.Items));
    }

    [OriginalDataFact]
    public void Places_AreTheMeasuredOnes()
    {
        var b = Bars();
        Assert.Equal([8, 56, 128], b["fight"].Menus.Select(m => m.TitleX));
        Assert.Equal([new Box(0, 12, 80, 16), new Box(48, 12, 144, 64), new Box(112, 12, 96, 32)], b["fight"].Menus.Select(m => m.Box));
        Assert.Equal(new Box(48, 12, 160, 40), b["outside"].Menus[1].Box); // seen s140: (48, 12)-(207, 51)
        Assert.Equal([8, 56, 112], b["town"].Menus.Select(m => m.TitleX));
        Assert.Equal(new Box(96, 12, 128, 96), b["town"].Menus[2].Box); // seen s116 / s120: (96, 12)-(223, 107)
        Assert.Equal(new Box(48, 1, 72, 10), b["fight"].Menus[1].Highlight); // Options: x 48-119, rows 1-10
    }

    [OriginalDataFact]
    public void TownItems_AreTheAmigasList0x238a4()
    {
        var town = Bars()["town"];
        Assert.Equal(["File", "Guild", "Options"], town.Menus.Select(m => UiText.S(m.Title)));
        Assert.Equal(["?) Info ", "O) Load", "S) Save"], town.Menus[0].Items.Select(UiText.S));
        Assert.Equal(["+) Add Member", "-) Drop Member", "E) Examine Member", "N) New Member", "P) Purge Member",
            "R) Rename Member", "----------------- ", "L) Learn Spells", "T) Training"], town.Menus[1].Items.Select(UiText.S));
        Assert.Equal(["A) Armoury", "B) Bank", "I) Inn", "M) Mystic", "X) Exit", "------------- ", "C) Cast Spell",
            "D) Distribute", "G) Trade Item", "U) Use Item", "V) View Party"], town.Menus[2].Items.Select(UiText.S));
        Assert.Equal("", town.Menus[1].Actions[6]); // the rule rows: key 0 in 0x2189c
        Assert.Equal("", town.Menus[2].Actions[5]);
    }

    [OriginalDataFact]
    public void TownBar_ClickClick_PressPullsDown_TakeGivesTheAction()
    {
        var bar = Bars()["town"];
        var state = new AmigaTownBar();
        state.Toggle(bar, 1);
        Assert.Equal(1, state.Open);
        Assert.Equal(-1, state.Item); // the pointer is on the title: nothing lit
        state.Hover(bar, 4);
        Assert.Equal("purge", state.Take(bar, 4));
        Assert.Null(state.Open);

        state.PullDown(2);
        Assert.Equal("", state.Take(bar, 5)); // the rule row: key 0, nothing
        Assert.Null(state.Open);
        state.Toggle(bar, 0);
        state.Toggle(bar, 0); // a second click on the open title closes it
        Assert.Null(state.Open);
    }

    [OriginalDataFact]
    public void TownBar_Keys_PullOptionsDown_StepAndTake()
    {
        var bar = Bars()["town"];
        var state = new AmigaTownBar();
        Assert.Equal((false, ""), state.HandleKey(bar, Keys.A)); // letters go on to the town's own keys (D-066)
        Assert.Equal((true, ""), state.HandleKey(bar, Keys.Left)); // the DOS row's cursor is not shown: used up
        Assert.Null(state.Open);
        Assert.Equal((true, ""), state.HandleKey(bar, Keys.Down));
        Assert.Equal((AmigaTownBar.OptionsMenu, 0), (state.Open!.Value, state.Item));
        state.HandleKey(bar, Keys.Up); // wraps to V) View Party
        Assert.Equal(10, state.Item);
        Assert.Equal((true, "view"), state.HandleKey(bar, Keys.Enter));
        Assert.Null(state.Open);
        state.HandleKey(bar, Keys.Space);
        state.HandleKey(bar, Keys.Right); // Options -> File
        Assert.Equal(0, state.Open);
        Assert.Equal((true, ""), state.HandleKey(bar, Keys.Escape));
        Assert.Null(state.Open);
        Assert.Equal((false, ""), state.HandleKey(bar, Keys.Escape)); // closed: Esc goes on (the game menu)
    }
}
