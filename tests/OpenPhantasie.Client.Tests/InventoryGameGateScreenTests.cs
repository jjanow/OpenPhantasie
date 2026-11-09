using Microsoft.Xna.Framework.Input;
using OpenPhantasie.Client;
using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client.Tests;

/// <summary>
/// The inventory screen on Phantasie III asks III's own gate (<see cref="GameSession.CanWear"/>: tables, bows STR + DEX),
/// not I's id formulas: the greyed rows, wearing, and give-and-wear on another member's box. Plus the member tabs (the
/// screen's party strip) as drop targets for a stash item by mouse and by pad.
/// </summary>
public class InventoryGameGateScreenTests
{
    private const int SelfBowPlus1 = 42, GodBow = 60;
    private static readonly GameDataRepository P3Data = GameDataRepository.Load(GameDataRepository.DefaultPathFor(GameId.P3));

    private static (GameSession S, InventoryScreen Screen, Character A, Character B) P3Town()
    {
        var s = GameSessionFactory.Create(P3Data, new SeededDosRandomSource(7));
        s.StartNewGame();
        s.OpenGuild();
        var a = s.CreateAndEnroll("ARCHER", Race.Human, CharacterClass.Fighter);
        var b = s.CreateAndEnroll("BRUTE", Race.Human, CharacterClass.Fighter);
        Assert.True(s.TryAddToParty(a, out _));
        Assert.True(s.TryAddToParty(b, out _));
        s.LeaveGuild();
        Assert.Equal(GameMode.Town, s.Mode);
        a.Stats = new CharacterStats(1, 10, 1, 10, 10, 10);  // Self Bow +1: III 1 + 1 ≥ 2; I 2·1 + 1 < 4
        b.Stats = new CharacterStats(18, 10, 5, 10, 10, 10); // God Bow: III 23 < 38; I 41 ≥ 40
        var screen = new InventoryScreen();
        screen.Open(s, P3Data);
        return (s, screen, a, b);
    }

    [OriginalDataFact]
    public void GreyedRows_FollowIIIsGate()
    {
        var (_, screen, a, b) = P3Town();
        Assert.Null(screen.UnusableReason(a, SelfBowPlus1));
        Assert.NotNull(screen.UnusableReason(b, GodBow));
        Assert.False(ItemRules.IsUsable(SelfBowPlus1, a.Stats)); // what the screen used to ask
    }

    [OriginalDataFact]
    public void StashBowOnTheBowBox_IsWornByIIIsRule()
    {
        var (s, screen, a, _) = P3Town();
        s.Sack.ItemIds.Add(SelfBowPlus1);
        var stash = new StashSpot(s.Sack.ItemIds.IndexOf(SelfBowPlus1));
        Assert.Contains("SELF BOW +1", screen.Drop(stash, new SlotSpot(0, "bow")).ToUpperInvariant());
        Assert.Equal(SelfBowPlus1, WornItems.Get(a, "bow"));
    }

    [OriginalDataFact]
    public void GiveAndWear_OnAnotherMembersBox_RefusesByIIIsRule()
    {
        var (s, screen, a, b) = P3Town();
        CarriedItems.Put(a, GodBow);
        var line = screen.Drop(new CarriedSpot(0, a.InventoryItemIds.IndexOf(GodBow)), new SlotSpot(1, "bow"));
        Assert.Equal(screen.Reason(InventoryRefusal.Unusable, b), line);
        Assert.Contains(GodBow, a.InventoryItemIds); // never half moved
        Assert.DoesNotContain(GodBow, b.InventoryItemIds);
    }

    [OriginalDataFact]
    public void StashItem_DroppedOnAMemberTab_GoesToThatMember_ByMouseAndByPad()
    {
        var (s, screen, a, b) = P3Town();
        s.Sack.ItemIds.Add(GodBow);
        Assert.Equal(InventoryMoveKind.FromStash, screen.Plan(new StashSpot(0), new MemberTabSpot(1)).Kind);
        screen.Drop(new StashSpot(0), new MemberTabSpot(1)); // a mouse drop on the tab: given, not worn
        Assert.Contains(GodBow, b.InventoryItemIds);
        Assert.Equal(0, s.Sack.ItemCount);
        Assert.Equal(1, screen.Member); // the drop shows the member it went to

        s.Sack.ItemIds.Add(SelfBowPlus1);
        screen.HandleButton(Buttons.DPadRight);    // the stash column
        screen.HandleButton(Buttons.Start);        // G: give…
        Assert.True(screen.Giving);
        screen.HandleButton(Buttons.RightShoulder); // …to the next member (wraps to the first)
        screen.HandleButton(Buttons.A);             // A gives
        Assert.Contains(SelfBowPlus1, a.InventoryItemIds);
        Assert.Equal(0, s.Sack.ItemCount);
    }
}
