using Microsoft.Xna.Framework.Input;
using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client.Tests;

/// <summary>
/// s135: III Amiga's message box (window 7, <c>0xaaac</c>) for the town / guild lines whose Amiga equivalent goes through
/// it (FORMAT.md 8.4, the caller table): the pack's text ids, the gate and the wait that takes its closing key.
/// </summary>
public class AmigaMessageBoxTests
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
             "cast.x_knows_no_spells", "useitem.x_carries_nothing"], // s158: 0x84e0 / 0xb8aa, 0xba7c
            Window.TextIds);
        // Every id is a fixed line in III's tables (a {0} template could never match a shown line).
        var p3 = StringTable.ForGame(GameId.P1).Over(StringTable.Load(GameDataRepository.DefaultPathFor(GameId.P3)));
        Assert.All(Window.TextIds, id =>
        {
            Assert.True(p3.Contains(id), id);
            Assert.DoesNotContain("{0}", p3.Get(id));
        });
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
