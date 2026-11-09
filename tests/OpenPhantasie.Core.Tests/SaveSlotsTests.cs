using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Core.Tests;

/// <summary>
/// QoL SAVE SLOTS (<c>docs/plan/12-save-slots.md</c>): the slot files of each game (<see cref="SaveSlots"/>; the single save
/// is the first slot), what a slot's row tells (<see cref="SaveSlotPreview"/>), the newest save other games read, and a
/// bad file described without throwing.
/// </summary>
public class SaveSlotsTests
{
    private static string TempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"phantasie-slots-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static Character Member(string name) => new() { Name = name };

    /// <summary>ALPHA + BRAVO active in town 3, CHARLIE parked in dungeon level 5.</summary>
    private static SessionSnapshot TwoParties() => new()
    {
        Game = GameId.P2,
        Mode = GameMode.Town,
        CurrentTownId = 3,
        RosterSlots = [Member("ALPHA"), Member("BRAVO"), Member("CHARLIE")],
        PartySlotIndices = [0, 1],
        OtherParties =
        [
            new SessionSnapshot { Mode = GameMode.Dungeon, PartySlotIndices = [2], Dungeon = new DungeonSnapshot { LevelId = 5 } },
        ],
    };

    [Fact]
    public void Paths_TheSingleSaveIsTheFirstSlot_AutoInIToIII_SlotOneInIV()
    {
        const string dir = "/saves";
        Assert.Equal(Path.Combine(dir, "savegame-p1.json"), SaveSlots.SinglePath(dir, GameId.P1));
        Assert.Equal(SaveSlots.SinglePath(dir, GameId.P3), SaveSlots.PathOf(dir, GameId.P3, SaveSlots.AutoNumber));
        Assert.Equal(Path.Combine(dir, "savegame-p3-4.json"), SaveSlots.PathOf(dir, GameId.P3, 4));
        Assert.Equal(SaveSlots.SinglePath(dir, GameId.P4), SaveSlots.PathOf(dir, GameId.P4, 1));
        Assert.Equal(Path.Combine(dir, "savegame-p4-3.json"), SaveSlots.PathOf(dir, GameId.P4, 3));
        Assert.Throws<ArgumentOutOfRangeException>(() => SaveSlots.PathOf(dir, GameId.P4, 0)); // IV has no AUTO
        Assert.Throws<ArgumentOutOfRangeException>(() => SaveSlots.PathOf(dir, GameId.P4, 4)); // the original's three
        Assert.Throws<ArgumentOutOfRangeException>(() => SaveSlots.PathOf(dir, GameId.P1, 10));
    }

    [Fact]
    public void Lists_LoadHasAutoAndNine_SaveOnlyTheNumbered_IVItsThree()
    {
        const string dir = "/saves";
        var all = SaveSlots.All(dir, GameId.P1);
        Assert.Equal(10, all.Count);
        Assert.True(all[0].IsAuto);
        Assert.Equal(Enumerable.Range(1, 9), SaveSlots.Writable(dir, GameId.P1).Select(s => s.Number));
        Assert.Equal([1, 2, 3], SaveSlots.All(dir, GameId.P4).Select(s => s.Number));
        Assert.Equal([1, 2, 3], SaveSlots.Writable(dir, GameId.P4).Select(s => s.Number)); // slot 1 is IV's one save
        Assert.DoesNotContain(SaveSlots.All(dir, GameId.P4), s => s.IsAuto);
    }

    [Fact]
    public void Preview_IToIII_ActivePartyFirst_PlaceAndOtherParties()
    {
        var p = SaveSlotPreview.Of(TwoParties());
        Assert.Equal(["ALPHA", "BRAVO"], p.Names);
        Assert.Equal(SavePlace.Town, p.Place);
        Assert.Equal(3, p.Number);
        Assert.Equal(1, p.OtherParties);
    }

    [Fact]
    public void Preview_EmptyActiveParty_ShowsTheParkedOne_AFightCountsAsWhereItIs()
    {
        var file = TwoParties();
        file.PartySlotIndices = [];
        var p = SaveSlotPreview.Of(file);
        Assert.Equal(["CHARLIE"], p.Names);
        Assert.Equal(SavePlace.Dungeon, p.Place);
        Assert.Equal(5, p.Number);
        Assert.Equal(0, p.OtherParties);

        var fight = new SessionSnapshot
        {
            Mode = GameMode.Combat, CombatReturnMode = GameMode.Overworld, RosterSlots = [Member("DELTA")], PartySlotIndices = [0],
        };
        Assert.Equal(SavePlace.Wilderness, SaveSlotPreview.Of(fight).Place);
    }

    [Fact]
    public void Preview_NoPartyAtAll_NamesNobody_AtTheFilesPlace()
    {
        var p = SaveSlotPreview.Of(new SessionSnapshot { Mode = GameMode.Town, CurrentTownId = 1 });
        Assert.Empty(p.Names);
        Assert.Equal(SavePlace.Town, p.Place);
        Assert.Equal(1, p.Number);
    }

    [Fact]
    public void Preview_IV_NamesAndPlace()
    {
        var party = new P4Party();
        party.Members.Add(new P4Member { Name = "Jaela" });
        party.Members.Add(new P4Member { Name = "Bort" });
        var save = new P4CampaignSnapshot(GameOptionsSnapshot.From(GameOptions.ForPreset(GameId.P4)), P4PartySnapshot.From(party),
            P4Place.Dungeon, 12, 3, 4, 0, null);
        var p = SaveSlotPreview.Of(save);
        Assert.Equal(["Jaela", "Bort"], p.Names);
        Assert.Equal(SavePlace.Dungeon, p.Place);
        Assert.Equal(12, p.Number);
        Assert.Equal(SavePlace.Wilderness, SaveSlotPreview.Of(save with { Place = P4Place.Outdoors }).Place);
    }

    [Fact]
    public void Describe_EmptyReadableBadAndOld_NeverThrows()
    {
        var dir = TempDir();
        try
        {
            var slots = SaveSlots.All(dir, GameId.P2);
            SessionCodec.WriteJsonFile(slots[1].Path, TwoParties());
            File.WriteAllText(slots[2].Path, "{ not json");
            File.WriteAllText(slots[3].Path, """{ "version": 2, "partySlotIndices": [] }"""); // before game-prefixed ids
            var info = SaveSlots.Describe(slots);
            Assert.True(info[0].Empty); // AUTO: nothing written yet
            Assert.False(info[0].Unreadable);
            Assert.Equal(["ALPHA", "BRAVO"], info[1].Preview!.Names);
            Assert.NotNull(info[1].Written);
            Assert.True(info[2].Unreadable);
            Assert.False(info[2].Old);
            Assert.True(info[3].Unreadable);
            Assert.True(info[3].Old);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Describe_IVSave_ReadsItsOwnFormat()
    {
        var dir = TempDir();
        try
        {
            var party = new P4Party();
            party.Members.Add(new P4Member { Name = "Jaela" });
            var save = new P4CampaignSnapshot(GameOptionsSnapshot.From(GameOptions.ForPreset(GameId.P4)),
                P4PartySnapshot.From(party), P4Place.Outdoors, 1, 3, 4, 0, null);
            var slot = SaveSlots.All(dir, GameId.P4)[2];
            Phantasie4SaveCodec.WriteJsonFile(slot.Path, save);
            var info = SaveSlots.Describe(slot);
            Assert.Equal(["Jaela"], info.Preview!.Names);
            Assert.Equal(SavePlace.Wilderness, info.Preview.Place);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void ReadPath_Off_TheSingleSave_On_TheNewestSlot()
    {
        var dir = TempDir();
        try
        {
            var single = SaveSlots.SinglePath(dir, GameId.P1);
            var three = SaveSlots.PathOf(dir, GameId.P1, 3);
            Assert.Equal(single, SaveSlots.ReadPath(dir, GameId.P1, slotsOn: true)); // nothing saved: the single save's name
            File.WriteAllText(single, "{}");
            File.WriteAllText(three, "{}");
            File.SetLastWriteTimeUtc(single, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
            File.SetLastWriteTimeUtc(three, new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc));
            Assert.Equal(three, SaveSlots.ReadPath(dir, GameId.P1, slotsOn: true));
            Assert.Equal(single, SaveSlots.ReadPath(dir, GameId.P1, slotsOn: false)); // off = exactly as before
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
