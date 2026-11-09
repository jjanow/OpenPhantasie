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

    [OriginalDataFact]
    public void GearLine_Compare_AndSheet_ShowIIIsTablePoints()
    {
        // s171 shot: "DMG 12>17" (I's formula + damage A) beside the Sword +4's "DAMAGE 13 POINTS". III: Mace 4, Sword +4 13.
        const int mace = 64, swordPlus4 = 90;
        var (_, screen, a, _) = P3Town();
        a.InventoryItemIds.Clear();
        a.InventoryItemIds.AddRange([mace, swordPlus4, GodBow]);
        a.WornShield = a.WornArmor = 0;
        a.WornWeapon = mace;
        a.WornBow = GodBow;
        a.WeaponDamageA = 5;
        var gear = screen.Gear(a);
        Assert.Equal((4, 20), (gear.WeaponDamage, gear.Bow));
        var cmp = screen.Compare(a, swordPlus4)!;
        Assert.Equal((4, 13), (cmp.Before.WeaponDamage, cmp.After.WeaponDamage));
        Assert.Equal(13, screen.ItemPoints(swordPlus4));

        var sheet = string.Join("\n", InspectDialog.SheetLines(a, P3Data));
        Assert.Contains("(4)", sheet);  // the Mace, III's table (I's formula: 12)
        Assert.Contains("(20)", sheet); // the worn bow is listed with its points
        Assert.DoesNotContain("(12)", sheet);
    }
}
