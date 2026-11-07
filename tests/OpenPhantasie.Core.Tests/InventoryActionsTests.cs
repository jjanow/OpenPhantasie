using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Core.Tests;

/// <summary>09 T2 inventory moves (<c>reverse-engineering/dos/logic-specs/inventory-actions.md</c>).</summary>
public class InventoryActionsTests
{
    private const int SmallShield = 4, LargeShield = 12, Robes = 22, PlateMail = 30, Club = 48, BigWeapon = 60, Potion = 101;

    private static readonly CharacterStats Strong = new(18, 10, 18, 15, 10, 10);

    private static GameSession Session(out Character fighter, out Character wizard, OptionPreset preset = OptionPreset.Manual)
    {
        var s = GameSessionFactory.Create(GameDataRepository.LoadPhantasie1(), new SeededDosRandomSource(5));
        s.StartNewGame(GameOptions.ForPreset(GameId.P1, preset));
        s.OpenGuild();
        fighter = s.CreateAndEnroll("Fighto", Race.Human, CharacterClass.Fighter);
        wizard = s.CreateAndEnroll("Wizo", Race.Human, CharacterClass.Wizard);
        Assert.True(s.TryAddToParty(fighter, out _));
        Assert.True(s.TryAddToParty(wizard, out _));
        s.EnterTown(1);
        fighter.Stats = Strong;
        wizard.Stats = Strong;
        return s;
    }

    private static int SlotOf(Character ch, int id) => ch.InventoryItemIds.IndexOf(id);

    private static void FillTo9(Character ch)
    {
        while (CarriedItems.Count(ch) < Character.InventorySlots)
            CarriedItems.Put(ch, Potion);
    }

    // --- Equip --------------------------------------------------------------------------------------------------

    [OriginalDataFact]
    public void Equip_FromCarried_MovesTheWornPointer_AndTheOldItemStaysCarried()
    {
        var s = Session(out var f, out _);
        CarriedItems.Put(f, LargeShield);
        Assert.Equal(SmallShield, f.WornShield);

        Assert.Equal(InventoryRefusal.None, s.TryEquip(f, SlotOf(f, LargeShield)));

        Assert.Equal(LargeShield, f.WornShield);
        Assert.Equal(12, f.ShieldSoak);
        Assert.Contains(SmallShield, f.InventoryItemIds);
        Assert.Equal(4, CarriedItems.Count(f));
        Assert.True(InventoryActions.IsWorn(f, SlotOf(f, LargeShield), s.Equipment));
        Assert.False(InventoryActions.IsWorn(f, SlotOf(f, SmallShield), s.Equipment));
    }

    [OriginalDataFact]
    public void Equip_CanPickAWorseItem_ThePlayersChoice()
    {
        var s = Session(out var f, out _);
        CarriedItems.Put(f, LargeShield);
        Assert.Equal(InventoryRefusal.None, s.TryEquip(f, SlotOf(f, LargeShield)));
        Assert.Equal(InventoryRefusal.None, s.TryEquip(f, SlotOf(f, SmallShield)));
        Assert.Equal(SmallShield, f.WornShield);
        Assert.Equal(4, f.ShieldSoak);
    }

    [OriginalDataFact]
    public void Equip_Refusals()
    {
        var s = Session(out var f, out _);
        CarriedItems.Put(f, Potion);
        CarriedItems.Put(f, PlateMail); // needs STR 20 (manual p.21)
        var outsider = new Character { Name = "Out", Stats = Strong };
        outsider.InventoryItemIds.Add(LargeShield);

        Assert.Equal(InventoryRefusal.NoItem, s.CanEquip(f, 40));
        Assert.Equal(InventoryRefusal.NoItem, s.CanEquip(f, -1));
        Assert.Equal(InventoryRefusal.NotEquipment, s.CanEquip(f, SlotOf(f, Potion)));
        Assert.Equal(InventoryRefusal.Unusable, s.TryEquip(f, SlotOf(f, PlateMail)));
        Assert.Equal(Robes, f.WornArmor);
        Assert.Equal(InventoryRefusal.NotInParty, s.CanEquip(outsider, 0));
    }

    [OriginalDataFact]
    public void Equip_AnEmptyCarriedSlotHole_IsNoItem()
    {
        var s = Session(out var f, out _);
        f.InventoryItemIds.Clear();
        f.InventoryItemIds.AddRange([Potion, 0, LargeShield]);
        Assert.Equal(InventoryRefusal.NoItem, s.CanEquip(f, 1));
        Assert.Equal(InventoryRefusal.None, s.CanEquip(f, 2));
    }

    [OriginalDataFact]
    public void EquipFromStash_WithRoom_CarriesAndWearsIt_LeavingAHole()
    {
        var s = Session(out var f, out _);
        s.Sack.ItemIds.AddRange([LargeShield, Potion]);

        Assert.Equal(InventoryRefusal.None, s.TryEquipFromStash(0, f));

        Assert.Equal([0, Potion], s.Sack.ItemIds); // the sack keeps its holes ([D✓] s65)
        Assert.Contains(LargeShield, f.InventoryItemIds);
        Assert.Contains(SmallShield, f.InventoryItemIds);
        Assert.Equal(LargeShield, f.WornShield);
        Assert.Equal(12, f.ShieldSoak);
    }

    [OriginalDataFact]
    public void EquipFromStash_With9Carried_SwapsTheWornItemIntoTheStash()
    {
        var s = Session(out var f, out _);
        FillTo9(f);
        s.Sack.ItemIds.AddRange([Potion, LargeShield]);
        var shieldSlot = SlotOf(f, SmallShield);

        Assert.Equal(InventoryRefusal.None, s.TryEquipFromStash(1, f));

        Assert.Equal([Potion, SmallShield], s.Sack.ItemIds);
        Assert.Equal(LargeShield, f.InventoryItemIds[shieldSlot]);
        Assert.Equal(9, CarriedItems.Count(f));
        Assert.Equal(LargeShield, f.WornShield);
    }

    [OriginalDataFact]
    public void EquipFromStash_With9Carried_AndNothingWornThere_IsCarryFull()
    {
        var s = Session(out var f, out _);
        Assert.Equal(InventoryRefusal.None, s.TryUnequip(f, WornItems.Shield));
        FillTo9(f);
        s.Sack.ItemIds.Add(LargeShield);
        Assert.Equal(InventoryRefusal.CarryFull, s.TryEquipFromStash(0, f));
        Assert.Equal([LargeShield], s.Sack.ItemIds);
    }

    [OriginalDataFact]
    public void EquipFromStash_Refusals()
    {
        var s = Session(out var f, out _);
        s.Sack.ItemIds.AddRange([0, Potion, PlateMail]);
        Assert.Equal(InventoryRefusal.NoItem, s.CanEquipFromStash(0, f));
        Assert.Equal(InventoryRefusal.NoItem, s.CanEquipFromStash(9, f));
        Assert.Equal(InventoryRefusal.NotEquipment, s.CanEquipFromStash(1, f));
        Assert.Equal(InventoryRefusal.Unusable, s.CanEquipFromStash(2, f));
    }

    // --- Unequip ------------------------------------------------------------------------------------------------

    [OriginalDataFact]
    public void Unequip_ClearsTheWornPointer_TheItemStaysCarried()
    {
        var s = Session(out var f, out _);
        Assert.Equal(InventoryRefusal.None, s.TryUnequip(f, WornItems.Weapon));
        Assert.Equal(0, f.WornWeapon);
        Assert.Equal(0, f.WeaponDamageB);
        Assert.Contains(Club, f.InventoryItemIds);
        Assert.Equal(InventoryRefusal.NothingWorn, s.TryUnequip(f, WornItems.Weapon));
        Assert.Equal(InventoryRefusal.NotEquipment, s.CanUnequip(f, "bow"));
    }

    // --- Transfer -----------------------------------------------------------------------------------------------

    [OriginalDataFact]
    public void Transfer_AWornItem_StopsItBeingWorn_AndTheReceiverDoesNotWearIt()
    {
        var s = Session(out var f, out var w);
        CarriedItems.Put(f, LargeShield);
        Assert.Equal(InventoryRefusal.None, s.TryEquip(f, SlotOf(f, LargeShield)));
        var wizardShield = w.WornShield;

        Assert.Equal(InventoryRefusal.None, s.TryTransferItem(f, SlotOf(f, LargeShield), w));

        Assert.DoesNotContain(LargeShield, f.InventoryItemIds);
        Assert.Equal(0, f.WornShield);
        Assert.Equal(0, f.ShieldSoak);
        Assert.Contains(LargeShield, w.InventoryItemIds);
        Assert.Equal(wizardShield, w.WornShield);
    }

    [OriginalDataFact]
    public void Transfer_OneOfTwoCopies_KeepsTheOtherWorn()
    {
        var s = Session(out var f, out var w);
        CarriedItems.Put(f, Club);
        Assert.Equal(Club, f.WornWeapon);
        Assert.Equal(InventoryRefusal.None, s.TryTransferItem(f, SlotOf(f, Club), w));
        Assert.Contains(Club, f.InventoryItemIds);
        Assert.Equal(Club, f.WornWeapon);
        Assert.Equal(ItemRules.CombatGate(Club).Points, f.WeaponDamageB);
    }

    [OriginalDataFact]
    public void Transfer_Refusals()
    {
        var s = Session(out var f, out var w);
        Assert.Equal(InventoryRefusal.NoItem, s.CanTransferItem(f, 8, w));
        Assert.Equal(InventoryRefusal.SameMember, s.CanTransferItem(f, 0, f));
        Assert.Equal(InventoryRefusal.NotInParty, s.CanTransferItem(f, 0, new Character()));
        FillTo9(w);
        Assert.Equal(InventoryRefusal.CarryFull, s.TryTransferItem(f, 0, w));
        Assert.Equal(3, CarriedItems.Count(f));
    }

    [OriginalDataFact]
    public void Transfer_ToADeadMember_IsAllowed_AsDosDistributeDoes()
    {
        var s = Session(out var f, out var w);
        w.Status = CharacterStatus.Dead;
        Assert.Equal(InventoryRefusal.None, s.TryTransferItem(f, SlotOf(f, Club), w));
        Assert.Equal(2, w.InventoryItemIds.Count(id => id == Club));
    }

    // --- Stash --------------------------------------------------------------------------------------------------

    [OriginalDataFact]
    public void ToStash_FillsTheFirstHole_AndUnwearsTheItem()
    {
        var s = Session(out var f, out _);
        s.Sack.ItemIds.AddRange([Potion, 0, Potion]);
        Assert.Equal(InventoryRefusal.None, s.TryPutInStash(f, SlotOf(f, Robes)));
        Assert.Equal([Potion, Robes, Potion], s.Sack.ItemIds);
        Assert.Equal(0, f.WornArmor);
        Assert.Equal(0, f.ArmorSoak);
        Assert.DoesNotContain(Robes, f.InventoryItemIds);
    }

    [OriginalDataFact]
    public void ToStash_Refusals()
    {
        var s = Session(out var f, out _);
        Assert.Equal(InventoryRefusal.NoItem, s.CanPutInStash(f, 5));
        while (!s.Sack.IsFull)
            s.Sack.TryAdd(Potion);
        Assert.Equal(LootSack.MaxItems, s.Sack.ItemCount);
        Assert.Equal(InventoryRefusal.StashFull, s.TryPutInStash(f, 0));
        Assert.Equal(3, CarriedItems.Count(f));
    }

    [OriginalDataFact]
    public void FromStash_LeavesAHole_TrimsTrailingHoles_AndDoesNotWear()
    {
        var s = Session(out var f, out _);
        s.Sack.ItemIds.AddRange([LargeShield, Potion]);
        Assert.Equal(InventoryRefusal.None, s.TryTakeFromStash(0, f));
        Assert.Equal([0, Potion], s.Sack.ItemIds);
        Assert.Equal(SmallShield, f.WornShield);
        Assert.Equal(InventoryRefusal.None, s.TryTakeFromStash(1, f));
        Assert.Empty(s.Sack.ItemIds);
        Assert.Equal([SmallShield, Club, Robes, LargeShield, Potion], f.InventoryItemIds);
    }

    [OriginalDataFact]
    public void FromStash_Refusals()
    {
        var s = Session(out var f, out _);
        s.Sack.ItemIds.AddRange([0, Potion]);
        Assert.Equal(InventoryRefusal.NoItem, s.CanTakeFromStash(0, f));
        Assert.Equal(InventoryRefusal.NoItem, s.CanTakeFromStash(7, f));
        FillTo9(f);
        Assert.Equal(InventoryRefusal.CarryFull, s.TryTakeFromStash(1, f));
        Assert.Equal([0, Potion], s.Sack.ItemIds);
    }

    [Fact]
    public void StashItems_ListsSlotsWithoutHoles()
    {
        var sack = new LootSack();
        sack.ItemIds.AddRange([0, Potion, 0, Club]);
        Assert.Equal([(1, Potion), (3, Club)], InventoryActions.StashItems(sack));
    }

    // --- Compare ------------------------------------------------------------------------------------------------

    [OriginalDataFact]
    public void Compare_GivesBeforeAndAfter_WithoutTouchingTheMember()
    {
        var s = Session(out var f, out _);
        var word12 = f.Word12;
        var cmp = s.CompareGear(f, LargeShield)!;

        Assert.Equal(WornItems.Shield, cmp.Slot.Id);
        Assert.Equal(SmallShield, cmp.ReplacedItemId);
        Assert.Equal(4, cmp.Before.ShieldSoak);
        Assert.Equal(12, cmp.After.ShieldSoak);
        Assert.Equal(cmp.Before.ArmorSoak, cmp.After.ArmorSoak);
        Assert.Equal(
            CombatMath.ComputeDefenseRating(f.Stats.Dex, f.ArmorSoak, f.Skills.Parry, 12), cmp.After.DefenseRating);
        Assert.Equal(f.WeaponDamageA + f.WeaponDamageB, cmp.Before.WeaponDamage);
        Assert.Equal(SmallShield, f.WornShield);
        Assert.Equal(4, f.ShieldSoak);
        Assert.Equal(word12, f.Word12);

        var weapon = s.CompareGear(f, BigWeapon)!;
        Assert.Equal(
            f.WeaponDamageA + ItemRules.CombatGate(BigWeapon).Points, weapon.After.WeaponDamage);
        Assert.Null(s.CompareGear(f, Potion));
    }

    // --- Slots come from data -----------------------------------------------------------------------------------

    [Fact]
    public void TheSlotLayout_DecidesWhatCanBeWorn()
    {
        var party = new Party();
        var ch = new Character { Stats = Strong };
        party.Members.Add(ch);
        ch.InventoryItemIds.AddRange([BigWeapon, 70]);
        var shortWeapons = new EquipmentLayout([new EquipmentSlot(WornItems.Weapon, 41, 65)]).Validate();

        Assert.Equal(InventoryRefusal.None, InventoryActions.TryEquip(party, ch, 0, shortWeapons));
        Assert.Equal(InventoryRefusal.NotEquipment, InventoryActions.CanEquip(party, ch, 1, shortWeapons));
        Assert.Equal(InventoryRefusal.NotEquipment, InventoryActions.CanUnequip(party, ch, WornItems.Shield, shortWeapons));
    }

    [Fact]
    public void ALayoutSlotWithoutACharacterField_IsRejected()
    {
        var ring = new EquipmentLayout([new EquipmentSlot("ring", 141, 150)]);
        Assert.Throws<ArgumentException>(() => ring.Validate());
        // T4: Phantasie III's bow slot has its field (Character.WornBow).
        new EquipmentLayout([new EquipmentSlot("bow", 41, 60)]).Validate();
    }

    // --- Where: the rules option --------------------------------------------------------------------------------

    [Fact]
    public void StashAccess_Option_TownForBothOriginalPresets_AnywhereForRemaster()
    {
        var d = RuleOptionRegistry.StashAccess;
        Assert.Equal("p1.stash-access", d.Id);
        Assert.Null(d.Difference);
        Assert.Equal(StashAccessRule.Town, GameOptions.ForPreset(GameId.P1).StashAccess);
        Assert.Equal(StashAccessRule.Town, GameOptions.ForPreset(GameId.P1, OptionPreset.Dos).StashAccess);
        Assert.Equal(StashAccessRule.Anywhere, GameOptions.ForPreset(GameId.P1, OptionPreset.Remaster).StashAccess);
        Assert.Equal(StashAccessRule.Anywhere, GameOptions.ForPreset(GameId.P1).With(d.Id, "anywhere").StashAccess);
    }

    [Theory]
    [InlineData(GameMode.Town, StashAccessRule.Town, true)]
    [InlineData(GameMode.Guild, StashAccessRule.Town, true)] // user, s75: inventory edits in the Guild too
    [InlineData(GameMode.Overworld, StashAccessRule.Town, false)]
    [InlineData(GameMode.Dungeon, StashAccessRule.Town, false)]
    [InlineData(GameMode.Combat, StashAccessRule.Town, false)]
    [InlineData(GameMode.Town, StashAccessRule.Anywhere, true)]
    [InlineData(GameMode.Guild, StashAccessRule.Anywhere, true)]
    [InlineData(GameMode.Overworld, StashAccessRule.Anywhere, true)]
    [InlineData(GameMode.Dungeon, StashAccessRule.Anywhere, true)]
    [InlineData(GameMode.Combat, StashAccessRule.Anywhere, false)]
    [InlineData(GameMode.Title, StashAccessRule.Anywhere, false)]
    public void StashAccessible_ByModeAndRule(GameMode mode, StashAccessRule rule, bool expected) =>
        Assert.Equal(expected, InventoryActions.StashAccessible(mode, rule));

    [OriginalDataFact]
    public void OriginalRules_RefuseEveryMoveOutsideTown()
    {
        var s = Session(out var f, out var w);
        s.Sack.ItemIds.Add(LargeShield);
        Assert.True(s.StashAccessible);
        s.OpenGuild();
        s.DepartGuildToWilderness();
        Assert.False(s.StashAccessible);
        Assert.Equal(InventoryRefusal.NotHere, s.CanEquip(f, 0));
        Assert.Equal(InventoryRefusal.NotHere, s.TryEquipFromStash(0, f));
        Assert.Equal(InventoryRefusal.NotHere, s.TryUnequip(f, WornItems.Shield));
        Assert.Equal(InventoryRefusal.NotHere, s.TryTransferItem(f, 0, w));
        Assert.Equal(InventoryRefusal.NotHere, s.TryPutInStash(f, 0));
        Assert.Equal(InventoryRefusal.NotHere, s.TryTakeFromStash(0, f));
        Assert.Equal(SmallShield, f.WornShield);
        Assert.Equal([LargeShield], s.Sack.ItemIds);
        Assert.NotNull(s.CompareGear(f, LargeShield)); // looking is always allowed
    }

    [OriginalDataFact]
    public void OriginalRules_AllowEveryMoveInTheGuild()
    {
        // user, s75: "let me adjust inventory anywhere" — the Guild counts as town under p1.stash-access = town.
        var s = Session(out var f, out var w);
        s.Sack.ItemIds.Add(LargeShield);
        s.OpenGuild();
        Assert.Equal(GameMode.Guild, s.Mode);
        Assert.Equal(StashAccessRule.Town, s.Options.StashAccess);
        Assert.Equal(InventoryRefusal.None, s.InventoryAccess);
        Assert.Equal(InventoryRefusal.None, s.TryEquipFromStash(0, f));
        Assert.Equal(LargeShield, f.WornShield);
        Assert.Equal(InventoryRefusal.None, s.TryTransferItem(f, SlotOf(f, SmallShield), w));
        Assert.Equal(InventoryRefusal.None, s.TryPutInStash(w, SlotOf(w, SmallShield)));
        Assert.Equal([SmallShield], s.Sack.ItemIds.Where(id => id != 0));

        // Leaving the Guild for the wilderness is outside town again.
        s.DepartGuildToWilderness();
        Assert.Equal(InventoryRefusal.NotHere, s.InventoryAccess);
    }

    [OriginalDataFact]
    public void RemasterRules_AllowTheStashInTheWilderness_ButNotInCombat()
    {
        var s = Session(out var f, out _, OptionPreset.Remaster);
        s.Sack.ItemIds.Add(LargeShield);
        s.OpenGuild();
        s.DepartGuildToWilderness();
        Assert.True(s.StashAccessible);
        Assert.Equal(InventoryRefusal.None, s.TryEquipFromStash(0, f));
        Assert.Equal(LargeShield, f.WornShield);

        s.BeginWildernessCombat();
        Assert.Equal(GameMode.Combat, s.Mode);
        Assert.Equal(InventoryRefusal.NotHere, s.TryPutInStash(f, 0));
    }

    [OriginalDataFact]
    public void AnOpenDistributeScreen_BlocksTheMoves()
    {
        var s = Session(out var f, out _);
        s.BeginItemDistribution();
        Assert.NotNull(s.ItemDistribution);
        Assert.Equal(InventoryRefusal.NotHere, s.CanTakeFromStash(0, f));
    }

    [OriginalDataFact]
    public void StashAccess_IsLockedIntoTheSave()
    {
        var s = Session(out _, out _, OptionPreset.Remaster);
        var json = SessionCodec.WriteJson(s.CaptureSnapshot());
        var other = GameSessionFactory.Create(GameDataRepository.LoadPhantasie1(), new SeededDosRandomSource(5));
        other.ApplySnapshot(SessionCodec.ReadJson(json));
        Assert.Equal(StashAccessRule.Anywhere, other.Options.StashAccess);
    }

    [Fact]
    public void ReasonKeys_AreDistinct_AndNamespaced()
    {
        var keys = InventoryActions.ReasonKeys;
        Assert.Equal(Enum.GetValues<InventoryRefusal>().Length - 1, keys.Count);
        Assert.Equal(keys.Count, keys.Distinct().Count());
        Assert.All(keys, k => Assert.StartsWith("inventory.refuse.", k));
        Assert.Equal("", InventoryActions.ReasonKey(InventoryRefusal.None));
        Assert.Equal("inventory.refuse.carry_full", InventoryActions.ReasonKey(InventoryRefusal.CarryFull));
    }
}
