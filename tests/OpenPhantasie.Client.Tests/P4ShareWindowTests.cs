using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client.Tests;

/// <summary>
/// s165: IV's (X68000) last presentation windows (FORMAT.md 6.9): <c>_mage</c>'s window, <c>_gatebank</c>'s share-out
/// window, Share out's equipment table (<c>_init_eq</c> / <c>_disp_eq</c>), the shares asked member by member, the outdoor
/// inn's window.
/// </summary>
public class P4ShareWindowTests
{
    private static PanelExtras Extras() =>
        GraphicsPackCatalog.Load(GameDataRepository.DefaultPathFor(GameId.P4)).Find("x68000")!.CommandPanels!.Extras;

    private static int Width(string t) => 8 * t.Length;

    private static P4Member Member(string name, int condition = P4Member.Okay) => new() { Name = name, Condition = condition };

    [OriginalDataFact]
    public void Pack_HoldsMagesWindow_AtItsCallSites()
    {
        var mage = Extras().MagicHall!;
        Assert.Equal((new PanelRect(160, 32, 312, 352), 14, 0, 16, (16, 16)),
            (mage.Window, mage.BackColour, mage.TextColour, mage.LineStep, mage.Name));
        Assert.Equal(("p4x.0x3fc1c", "魔法訓練場"), (mage.Title, mage.TitleText));
        var offer = mage.Notes["offer"];
        Assert.Equal([(120, 16), (16, 32)], offer.Take(2).Select(s => (s.X ?? 0, s.Y ?? 0)));
        Assert.True(offer[2].Number);
        Assert.Equal((1, 8), (offer[2].Font, offer[2].Colour));
        Assert.Equal((16, 48), (offer[4].X, offer[4].Y));
        Assert.Equal((16, 32), (mage.Notes["not_worthy"][0].X, mage.Notes["not_worthy"][0].Y));
        Assert.Equal((32, 32), (mage.Notes["poor_condition"][1].X, mage.Notes["poor_condition"][1].Y));
        Assert.All(mage.Results!.Values.Append(mage.ShortOfGold), s => Assert.Equal((16, 64, 2), (s.X, s.Y, s.Colour)));
        Assert.Equal(new PanelRect(0, 64, 311, 16), mage.ResultClear);
    }

    [OriginalDataFact]
    public void MagesWindow_OfferOnTheNamesRow_TheResultUnderIt()
    {
        var mage = Extras().MagicHall!;
        var w = new P4TrainingWindow("ELWYN", P4TrainingNote.Offer, 450, false, P4TrainingWindow.MagicHall, P4LessonResult.Learned);
        var rows = P4DungeonScreen.TrainingHallText(mage, w, 296, s => s.Label switch
        {
            "p4x.0x3fc6b" => "Any spell ",
            "p4x.0x3fc76" => "for ",
            "p4x.0x3fc81" => " gold each,",
            "p4x.0x3fc97" => "which?",
            "p4x.0x3fcd2" => " learned the spell.",
            _ => s.Text,
        }, (t, _) => Width(t));
        Assert.Contains((16, 16, "ELWYN", 0, 0), rows);
        Assert.Contains((120, 16, "Any spell ", 0, 0), rows);
        Assert.Contains((16, 32, "for ", 0, 0), rows);
        Assert.Contains((48, 32, "450", 1, 8), rows); // the price after "for " in font 1, colour 8
        Assert.Contains((16, 48, "which?", 0, 0), rows);
        Assert.Contains((16, 64, "ELWYN learned the spell.", 0, 2), rows); // English needs the subject [~]
        rows = P4DungeonScreen.TrainingHallText(mage, w with { Note = P4TrainingNote.NotWorthy, Result = null }, 296,
            s => s.Text, (t, _) => Width(t));
        Assert.Contains((16, 32, mage.Notes["not_worthy"][0].Text, 0, 0), rows);
    }

    [OriginalDataFact]
    public void Pack_HoldsGatebanksWindow()
    {
        var x = Extras();
        var gate = x.GateBank!;
        Assert.Equal(new PanelRect(160, 132, 312, 160), x.Titled!["_gatebank"].Window);
        Assert.Equal((68, 136, 8, "p4x.0x3fecc"), (gate.Pool.X, gate.Pool.Y, gate.PoolDigits, gate.PoolUnit.Label));
        Assert.Equal(new PanelRect(264, 32, 40, 92), gate.Frame);
        Assert.Equal((272, 40, 20, 4, 4, "  "), (gate.Buttons.X, gate.Buttons.Y, gate.Buttons.Step, gate.Buttons.Count,
            gate.Buttons.Colour, gate.Buttons.Pad));
        Assert.Equal((16, 16, 32, 164, 8), (gate.RowY, gate.RowStep, gate.NameX, gate.ShareX, gate.GainDigits));
    }

    [OriginalDataFact]
    public void Gatebank_RowsComeUpMemberByMember_ThenTheGainsReplaceTheShares()
    {
        var gate = Extras().GateBank!;
        P4Member[] members = [Member("A"), Member("B"), Member("C")];
        var text = P4DungeonScreen.GateBankText(gate, new P4GateShare(900, null), members, [2], s => s.Text);
        Assert.Contains((68, 136, gate.Pool.Text + P4DungeonScreen.PaddedAmount(900, 8) + gate.PoolUnit.Text), text);
        Assert.Contains((32, 16, "A"), text);
        Assert.Contains((164, 16, "2"), text);
        Assert.Contains((32, 32, "B"), text); // his turn: the name, no share yet
        Assert.DoesNotContain(text, t => t.Y == 32 && t.X == 164);
        Assert.DoesNotContain(text, t => t.Text == "C");
        text = P4DungeonScreen.GateBankText(gate, new P4GateShare(900, [449, 224, 224]), members, null, s => s.Text);
        Assert.Contains((164, 16, P4DungeonScreen.PaddedAmount(449, 8)), text);
        Assert.Contains((164, 48, P4DungeonScreen.PaddedAmount(224, 8)), text);
    }

    [Fact]
    public void Shares_AreGivenMemberByMember_ZeroOnlyForAMemberNotOkay()
    {
        P4Member[] members = [Member("A"), Member("B", condition: 2)];
        var (list, done) = P4DungeonScreen.GiveShare([], members, 0);
        Assert.Empty(list); // the "0" button is off for a member in good health
        Assert.False(done);
        (list, done) = P4DungeonScreen.GiveShare(list, members, 3);
        Assert.Equal([3], list);
        Assert.False(done);
        (list, done) = P4DungeonScreen.GiveShare(list, members, 0);
        Assert.Equal([3, 0], list);
        Assert.True(done);
        (list, _) = P4DungeonScreen.GiveShare([], members, 4);
        Assert.Empty(list);
    }

    [OriginalDataFact]
    public void Deposit_RowsComeUpMemberByMember_TheShareAtX196()
    {
        var bank = Extras().Bank!;
        P4Member[] members = [Member("A"), Member("B"), Member("C")];
        var view = new P4BankWindow(P4BankView.Deposit, [], null);
        var text = P4DungeonScreen.BankText(bank, view, members, 100, [1], null, s => s.Text, (_, t) => t);
        Assert.Contains((64, 228, "A", false, 0), text);
        Assert.Contains((196, 228, "1", false, 0), text);
        Assert.Contains((64, 244, "B", false, 0), text);
        Assert.DoesNotContain(text, t => t.Text == "C");
        Assert.DoesNotContain(text, t => t.X == 196 && t.Y == 244);
    }

    [OriginalDataFact]
    public void Pack_HoldsShareOutsEquipmentTable()
    {
        var eq = Extras().ItemWindows!.Windows["_distribute"].Equipment!;
        Assert.Equal([16, 32, 136, 184, 208, 232, 256, 280], eq.Headers.Select(h => h.X ?? 0));
        Assert.All(eq.Headers, h => Assert.Equal(224, h.Y));
        Assert.Equal((new PanelRect(16, 239, 280, 1), 0), (eq.Line, eq.LineColour));
        Assert.Equal((240, 16, 16, 2, 32, 136, 32, 2), (eq.RowY, eq.RowStep, eq.NumberX, eq.NumberColour, eq.NameX, eq.ClassX,
            eq.ClassWidth, eq.ValueDigits));
        Assert.Equal([("weapon", 184), ("armour", 208), ("shield", 232), ("bow", 256), ("thrown", 280)], eq.Values);
        Assert.Equal((224, 16, 15, 15), (eq.KindBoxY, eq.KindBoxWidth, eq.KindBoxHeight, eq.KindBoxXor));
        Assert.Equal((16, 280, 16), (eq.Button.X, eq.Button.Width, eq.Button.Height));
    }

    [OriginalDataFact]
    public void EquipmentTable_RowsAndTheLitSlot()
    {
        var eq = Extras().ItemWindows!.Windows["_distribute"].Equipment!;
        var rows = new List<P4DungeonScreen.EquipmentRow>
        {
            new("BRAND", "Fighter", new Dictionary<string, int> { ["weapon"] = 12, ["armour"] = 4, ["thrown"] = 3 }),
            new("ELWYN", "Shadow Warrior", new Dictionary<string, int>()),
        };
        var text = P4DungeonScreen.EquipmentText(eq, 0, rows, "armour", s => s.Text, Width);
        Assert.Contains((16, 240, "1", 2), text);
        Assert.Contains((32, 240, "BRAND", 0), text);
        Assert.Contains((136, 240, "Figh", 0), text); // _flen: characters while the width so far is below 32
        Assert.Contains((184, 240, "12", 0), text);
        Assert.Contains((208, 240, "  4", 0), text);
        Assert.Contains((232, 240, "  0", 0), text);
        Assert.Contains((136, 256, "Shad", 0), text);
        Assert.Contains((208, 224, eq.Headers[4].Text, 15), text); // the placed item's slot: XOR 15
        Assert.Contains((184, 224, eq.Headers[3].Text, 0), text);
        // English headers wider than the 24-pixel step: the step grows, the columns move left to end inside the window.
        Assert.Equal([172, 199, 226, 253, 280], P4DungeonScreen.EquipmentColumns(eq, 25, 304).Select(c => c.X));
        Assert.Equal([184, 208, 232, 256, 280], P4DungeonScreen.EquipmentColumns(eq, 16, 304).Select(c => c.X));
        Assert.Equal("weapon", P4DungeonScreen.SlotName(P4ItemKind.Weapon));
        Assert.Null(P4DungeonScreen.SlotName(P4ItemKind.Other));
    }
}
