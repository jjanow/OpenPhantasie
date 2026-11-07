using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Core.Tests;

/// <summary>
/// D-069 (user s57: manual, ENCORE "Continuing Saved Games"): several parties out at once; Continue asks for a Guild number
/// and calls up that character's party where it was (<c>save-file-format.md</c> §Several parties and Continue).
/// </summary>
public class SeveralPartiesTests
{
    private static readonly GameDataRepository Data = GameDataRepository.LoadPhantasie1();

    private static SessionSnapshot SaveAndRead(GameSession s) => SessionCodec.ReadJson(SessionCodec.WriteJson(s.CaptureSnapshot()));

    /// <summary>Guild number (roster slot + 1) by name: a load replaces the roster objects.</summary>
    private static int Number(GameSession s, Character c) =>
        Array.FindIndex(s.Roster.Slots, x => x?.Name == c.Name) + 1;

    private static Character Live(GameSession s, Character c) => s.Roster.Slots[Number(s, c) - 1]!;

    /// <summary>A + B out in the wilderness; C enrolled, in no party.</summary>
    private static GameSession TwoOutOneHome(out Character a, out Character b, out Character c)
    {
        var s = GameSessionFactory.Create(Data, new SeededDosRandomSource(7));
        s.StartNewGame();
        s.OpenGuild();
        a = s.CreateAndEnroll("Alpha", Race.Human, CharacterClass.Fighter);
        b = s.CreateAndEnroll("Bravo", Race.Elf, CharacterClass.Priest);
        c = s.CreateAndEnroll("Charlie", Race.Dwarf, CharacterClass.Thief);
        Assert.True(s.TryAddToParty(a, out _));
        Assert.True(s.TryAddToParty(b, out _));
        s.DepartGuildToWilderness();
        Assert.Equal(GameMode.Overworld, s.Mode);
        return s;
    }

    [OriginalDataFact]
    public void Continue_CharacterInNoParty_OpensANewEmptyPartyInItsTown_AndParksTheOther()
    {
        var s = TwoOutOneHome(out var a, out var b, out var c);
        c.CurrentTown = 3; // lives in another town
        var (region, x, y) = (s.Overworld.RegionId, s.Overworld.X, s.Overworld.Y);
        var file = SaveAndRead(s);

        Assert.True(s.Continue(file, Number(s, c), out var error), error);
        Assert.Equal(GameMode.Town, s.Mode);
        Assert.Equal(3, s.CurrentTownId);
        Assert.Empty(s.Party.Members);
        Assert.Equal(s.Towns[3].ExitPosition, (s.Overworld.RegionId, s.Overworld.X, s.Overworld.Y));
        var parked = Assert.Single(s.OtherParties);
        Assert.Equal([Number(s, a) - 1, Number(s, b) - 1], parked.PartySlotIndices);
        Assert.Equal(GameMode.Overworld, parked.Mode);
        Assert.Equal((region, x, y), (parked.OverworldRegionId, parked.OverworldX, parked.OverworldY));
        Assert.Empty(parked.RosterSlots); // shared state lives on the top level only
        Assert.Null(parked.Wilderness!.RegionTiles);
    }

    [OriginalDataFact]
    public void ParkedMembers_CannotBeAddedOrPurged_ButOthersCan()
    {
        var s = TwoOutOneHome(out var a, out _, out var c);
        s.Continue(SaveAndRead(s), Number(s, c), out _);
        s.OpenGuild();
        Assert.True(s.IsInOtherParty(Live(s, a)));
        Assert.False(s.TryAddToParty(Live(s, a), out var error));
        Assert.Equal(SeveralParties.OtherPartyRefusal, error);
        Assert.False(s.TryPurge(Live(s, a), out error));
        Assert.Equal(SeveralParties.OtherPartyRefusal, error);
        Assert.False(s.TryRename(Live(s, a), "Zulu", out error)); // D-065: still a party member
        Assert.Equal(GameSession.RenamePartyMemberRefusal, error);
        Assert.True(s.TryAddToParty(Live(s, c), out error), error);
    }

    [OriginalDataFact]
    public void Continue_SwitchesBackAndForth_EachPartyWhereItWas_RosterShared()
    {
        var s = TwoOutOneHome(out var a, out var b, out var c);
        var (region, x, y) = (s.Overworld.RegionId, s.Overworld.X, s.Overworld.Y);
        s.Party.GoldCarried = 123;
        s.Continue(SaveAndRead(s), Number(s, c), out _);
        s.OpenGuild();
        s.TryAddToParty(Live(s, c), out _);
        s.Party.GoldCarried = 7;
        s.Roster.Slots[Number(s, a) - 1]!.Experience = 999; // roster is one shared copy: the change must survive

        var aNumber = Number(s, a);
        Assert.True(s.Continue(SaveAndRead(s), aNumber, out var error), error);
        Assert.Equal(GameMode.Overworld, s.Mode);
        Assert.Equal((region, x, y), (s.Overworld.RegionId, s.Overworld.X, s.Overworld.Y));
        Assert.Equal(["Alpha", "Bravo"], s.Party.Members.Select(m => m.Name));
        Assert.Same(s.Roster.Slots[aNumber - 1], s.Party.Members[0]);
        Assert.Equal(123, s.Party.GoldCarried);
        Assert.Equal(999, s.Party.Members[0].Experience);
        var parked = Assert.Single(s.OtherParties);
        Assert.Equal(GameMode.Guild, parked.Mode);
        Assert.Equal(7, parked.PartyGold);

        // Calling up Charlie again lands his party in the Guild where it was saved.
        Assert.True(s.Continue(SaveAndRead(s), Number(s, c), out _));
        Assert.Equal(GameMode.Guild, s.Mode);
        Assert.Equal(["Charlie"], s.Party.Members.Select(m => m.Name));
        Assert.Equal(GameMode.Overworld, Assert.Single(s.OtherParties).Mode);
    }

    [OriginalDataFact]
    public void Continue_ActiveMember_LoadsAsSaved()
    {
        var s = TwoOutOneHome(out _, out _, out var c);
        s.Continue(SaveAndRead(s), Number(s, c), out _);
        s.OpenGuild();
        s.TryAddToParty(Live(s, c), out _);
        var file = SaveAndRead(s);
        s.StartNewGame();
        Assert.Empty(s.OtherParties);
        Assert.True(s.Continue(file, Number(s, c), out _)); // Charlie's party is the active one: the file as saved
        Assert.Equal(GameMode.Guild, s.Mode);
        Assert.Equal(["Charlie"], s.Party.Members.Select(m => m.Name));
        Assert.Single(s.OtherParties);
    }

    [OriginalDataFact]
    public void Continue_FromAnEmptyActiveParty_ParksNothing()
    {
        var s = GameSessionFactory.Create(Data, new SeededDosRandomSource(7));
        s.StartNewGame();
        s.OpenGuild();
        var x = s.CreateAndEnroll("Xray", Race.Human, CharacterClass.Wizard);
        Assert.True(s.Continue(SaveAndRead(s), Number(s, x), out _));
        Assert.Empty(s.OtherParties);
        Assert.Equal((GameMode.Town, 1), (s.Mode, s.CurrentTownId));
    }

    [OriginalDataTheory]
    [InlineData(0, "No such Guild member.")]
    [InlineData(5, "No such Guild member.")] // empty slot
    [InlineData(22, "No such Guild member.")]
    [InlineData(23, "The elementals live in Town #0.")]
    [InlineData(25, "The elementals live in Town #0.")]
    [InlineData(26, "No such Guild member.")]
    public void Continue_NumberNamingNobody_IsRefused_NothingChanges(int number, string expected)
    {
        var s = TwoOutOneHome(out _, out _, out _);
        var file = SaveAndRead(s);
        s.Continue(file, 3, out _); // Charlie: Alpha and Bravo parked
        Assert.False(s.Continue(file, number, out var error));
        Assert.Equal(expected, error);
        Assert.Equal(GameMode.Town, s.Mode);
        Assert.Single(s.OtherParties);
    }

    [OriginalDataFact]
    public void ParkedParty_KeepsItsWildernessMenuAndSack_ThroughJson()
    {
        var s = TwoOutOneHome(out var a, out _, out var c);
        var snap = s.CaptureSnapshot();
        snap.Wilderness!.PromptMenu = WildernessMenu.BlackKnights;
        snap.Wilderness.PromptMonsterId = WildernessEncounter.BlackKnightMonsterId;
        snap.Wilderness.PromptTitle = WildernessEncounter.BlackKnightsTitle;
        snap.Sack.ItemIds = [5, 6];
        s.ApplySnapshot(snap);
        s.Continue(SaveAndRead(s), Number(s, c), out _);
        Assert.Null(s.WildernessPrompt);
        Assert.Empty(s.Sack.ItemIds); // the sack is party-local

        s.Continue(SaveAndRead(s), Number(s, a), out _);
        Assert.Equal(WildernessMenu.BlackKnights, s.WildernessPrompt!.Menu);
        Assert.Equal([5, 6], s.Sack.ItemIds);
    }

    [OriginalDataFact]
    public void DosExport_WritesTheParkedMembersWithoutTheInPartyFlag()
    {
        var s = TwoOutOneHome(out var a, out var b, out var c);
        s.Continue(SaveAndRead(s), Number(s, c), out _);
        s.OpenGuild();
        s.TryAddToParty(Live(s, c), out _);
        s.LeaveGuild();

        var result = DosSaveConverter.ExportFrom(s, Data);
        Assert.NotNull(result.Files);
        var slots = GuildCodec.ReadBinary(result.Files!.Guild!, Data.CreationTables);
        Assert.Equal(["Charlie"], slots.Where(ch => ch is { InParty: true }).Select(ch => ch!.Name));
        Assert.Contains(result.Warnings, w => w.Message.Contains("a DOS save holds one party"));
        Assert.True(Live(s, a).InParty && Live(s, b).InParty); // the live game keeps its marks

        // Import is one party: the exported set has no parked parties.
        var back = DosSaveConverter.Import(result.Files, Data).Snapshot;
        Assert.Empty(back.OtherParties);
        Assert.Equal([Number(s, c) - 1], back.PartySlotIndices);
    }

    [OriginalDataFact]
    public void ParkedParty_InADungeon_ComesBackWhereItWas_WithItsTrapPromptAndLevelBuffer()
    {
        var s = TwoOutOneHome(out var a, out _, out var c);
        s.EnterDungeon(1, 20, 0);
        s.Dungeon!.Level!.Set(20, 1, 0x04);
        var snap = s.CaptureSnapshot();
        snap.Dungeon!.TrapPrompt = new DungeonTrapPromptSnapshot
            { TrapKind = 1, X = 20, Y = 1, SpotterIndex = 0, Message = "ALPHA SPOTS A TRAP", Disarmers = [0, 1] };
        s.ApplySnapshot(snap);
        var (x, y, facing) = (s.Dungeon!.X, s.Dungeon.Y, s.Dungeon.Facing);

        s.Continue(SaveAndRead(s), Number(s, c), out _);
        Assert.Null(s.Dungeon);
        Assert.Null(s.DungeonTrapPrompt);
        Assert.Equal(GameMode.Dungeon, Assert.Single(s.OtherParties).Mode);

        Assert.True(s.Continue(SaveAndRead(s), Number(s, a), out var error), error);
        Assert.Equal(GameMode.Dungeon, s.Mode);
        Assert.Equal((1, x, y, facing), (s.Dungeon!.LevelId, s.Dungeon.X, s.Dungeon.Y, s.Dungeon.Facing));
        Assert.Equal(0x04, s.Dungeon.Level!.Get(20, 1)); // the party's live level buffer
        Assert.Equal("ALPHA SPOTS A TRAP", s.DungeonTrapPrompt!.Message);
        Assert.Equal([0, 1], s.DungeonTrapPrompt.Disarmers);
    }
}
