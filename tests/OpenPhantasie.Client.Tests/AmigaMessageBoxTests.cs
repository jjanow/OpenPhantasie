using Microsoft.Xna.Framework.Input;
using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client.Tests;

/// <summary>
/// s135: III Amiga's message box (window 7, <c>0xaaac</c>) for the town / guild lines whose Amiga equivalent goes through
/// it (FORMAT.md 8.4, the caller table): the pack's text ids, the gate and the wait that takes its closing key.
/// </summary>
public partial class AmigaMessageBoxTests
{
    private static KeepWindowsLayout.MessageWindow Window =>
        GraphicsPackCatalog.Load(GameDataRepository.DefaultPathFor(GameId.P3)).Find("amiga")!.KeepWindows!.Message!;

    [OriginalDataFact]
    public void Pack_ListsTheTownAndGuildCallersWithARemasterEquivalent()
    {
        Assert.Equal(
            ["core.guild.party_not_assembled", "core.guild.elemental_already_joined", "core.guild.already_in_party",
             "core.guild.party_is_full", "core.guild.cannot_purge_party_member", "core.guild.roster_full",
             "core.p3.cannot_afford_stay", "core.p3.cant_carry_anymore", "core.guild.learn_no_more", // s156: Learn 0x1058a
             "guild.roster_empty", "core.p3.cannot_train", "core.guild.learn_cant_afford", // s158: 0xb274, 0xb976, 0x1068a
             "cast.x_knows_no_spells", "useitem.x_carries_nothing", // s158: 0x84e0 / 0xb8aa, 0xba7c
             // s162: Monster Evaluate 0xff2e, the out-of-fight caster's 0xf99a / 0xfa4c / 0xfb02 / 0xfb1e, the dungeon
             // step's 0x1c674 / 0x1c75a
             "core.magic.monsters_at_level_x", "core.magic.not_enough_mp", "core.magic.cant_cast_here",
             "core.magic.only_in_combat", "core.p3.x_picks_the_lock", "core.p3.x_disarms_the_trap",
             "core.p3.character_health_improves"], // s163: the out-of-fight heal's 0xfd42
            Window.TextIds);
        // Every id is a line in III's tables (s162: a {0} template takes the lines it formats to).
        var p3 = StringTable.ForGame(GameId.P1).Over(StringTable.Load(GameDataRepository.DefaultPathFor(GameId.P3)));
        Assert.All(Window.TextIds, id => Assert.True(p3.Contains(id), id));
        Assert.Equal("amiga-topaz-8", Window.Font); // s162: 0x854 prints the title in topaz 8 (the s134 shot)
    }

    [Theory]
    [InlineData("{0} picks the lock.", "SWAT picks the lock.", true)]
    [InlineData("{0} picks the lock.", "SWAT picks the lock", false)]
    [InlineData("{0} picks the lock.", " picks the lock.", false)]
    [InlineData("The approximate level of the monsters within this region is {0}",
        "The approximate level of the monsters within this region is 6", true)]
    [InlineData("The ferry is not available.", "The ferry is not available.", true)]
    [InlineData("The ferry is not available.", "The ferry is not available", false)]
    [InlineData("A (b) [c]? {0,-10}", "A (b) [c]? x", true)]
    public void Matches_FillsEachFormatItem(string text, string line, bool expected) =>
        Assert.Equal(expected, AmigaMessageBox.Matches(text, line));

    [OriginalDataFact]
    public void MonsterEvaluate_Row2_KeepsItsSixBlanks_TheBandOneLine()
    {
        var line = UiText.For(GameId.P3).Format("core.magic.monsters_at_level_x", 12);
        // 0x854 prints the title with each "\r" one 8-px row down at the title column: row 2 starts with six blank cells.
        Assert.Equal(["The approximate level of the monsters", "      within this region is 12"],
            UiOverlays.MessageRows(Window, line));
        // The remaster's band is one line: the break and its blanks are one space there.
        Assert.Equal("The approximate level of the monsters within this region is 12", Game1.OneLine(line));
        Assert.Equal("A B\nC", Game1.OneLine("A B\nC")); // other line breaks are left alone
    }

    [OriginalDataFact]
    public void Takes_OnlyTheListedLines()
    {
        Assert.True(AmigaMessageBox.Takes(Window, GameText.S("core.guild.party_not_assembled")));
        Assert.True(AmigaMessageBox.Takes(Window, GameText.S("core.p3.cannot_afford_stay")));
        Assert.False(AmigaMessageBox.Takes(Window, GameText.S("game.sound_on"))); // a town toggle stays on the band
        Assert.False(AmigaMessageBox.Takes(Window, ""));
        Assert.False(AmigaMessageBox.Takes(null, GameText.S("core.guild.party_not_assembled"))); // pack without window 7
    }

    [OriginalDataFact]
    public void Offer_NeedsTheSwitch()
    {
        var box = new AmigaMessageBox();
        var line = GameText.S("core.guild.cannot_purge_party_member");
        Assert.False(box.Offer(Window, enabled: false, line)); // AMIGA SCREENS off: the remaster's band / line
        Assert.False(box.Shown);
        Assert.True(box.Offer(Window, enabled: true, line));
        Assert.Equal(line, box.Text);
    }

    [OriginalDataFact]
    public void AnyKey_ClosesIt_AndIsTaken()
    {
        var box = new AmigaMessageBox();
        Assert.False(box.HandleKeys([Keys.Enter])); // not up: the keys go on
        box.Offer(Window, enabled: true, GameText.S("core.guild.roster_full"));
        Assert.True(box.HandleKeys([])); // up, no key: it waits and still owns the frame
        Assert.True(box.Shown);
        Assert.True(box.HandleKeys([Keys.Enter])); // 0x3ba0 with key 0: any key ends the wait; the Enter goes no further
        Assert.False(box.Shown);
        box.Offer(Window, enabled: true, GameText.S("core.guild.roster_full"));
        Assert.True(box.HandleKeys([Keys.O])); // a click on OK sends O
        Assert.False(box.Shown);
    }
}
