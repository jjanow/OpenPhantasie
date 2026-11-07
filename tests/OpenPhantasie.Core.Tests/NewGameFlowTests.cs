using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Core.Tests;

/// <summary>
/// New game (OV1 <c>0x7dd(1)</c>): empty party at the Pelnor town menu; the Guild is entered from town and left back to it;
/// Guild N)EW rolls before the name (Y names + enrolls, N drops the roll); Purge and Rename.
/// </summary>
public class NewGameFlowTests
{
    private static readonly GameDataRepository Data = GameDataRepository.LoadPhantasie1();

    private static GameSession NewGame(int seed = 3)
    {
        var s = GameSessionFactory.Create(Data, new SeededDosRandomSource(seed));
        s.StartNewGame();
        return s;
    }

    [OriginalDataFact]
    public void StartNewGame_EmptyPartyAtPelnorTownMenu()
    {
        var s = NewGame();
        Assert.Equal(GameMode.Town, s.Mode);
        Assert.Equal(1, s.CurrentTownId);
        Assert.Empty(s.Party.Members);
        Assert.Empty(s.Roster.Occupied);
        Assert.False(s.LeaveTown()); // [C] OV1 0x3250: no Exit with an empty party
        Assert.Equal(GameMode.Town, s.Mode);
    }

    [OriginalDataFact]
    public void Guild_IsEnteredFromTown_AndLeftBackToTown()
    {
        var s = NewGame();
        s.OpenGuild();
        Assert.Equal(GameMode.Guild, s.Mode);
        var hero = s.CreateAndEnroll("Hero", Race.Human, CharacterClass.Fighter);
        Assert.True(s.TryAddToParty(hero, out _));
        s.LeaveGuild();
        Assert.Equal(GameMode.Town, s.Mode);
        Assert.Equal(1, s.CurrentTownId);
        Assert.True(s.LeaveTown());
        Assert.Equal(GameMode.Overworld, s.Mode);
    }

    [OriginalDataFact]
    public void RollNewMember_IsNotEnrolledUntilNamed()
    {
        var s = NewGame();
        s.OpenGuild();
        var rolled = s.RollNewMember(Race.Elf, CharacterClass.Wizard);
        Assert.Equal("", rolled.Name);
        Assert.Equal(Race.Elf, rolled.Race);
        Assert.Empty(s.Roster.Occupied); // an N simply drops this roll

        Assert.True(s.TryEnrollNewMember(rolled, "Zandor", out _));
        Assert.Equal("Zandor", Assert.Single(s.Roster.Occupied).Name);
    }

    [OriginalDataFact]
    public void TryEnrollNewMember_RefusesDuplicateName_AndKeepsTheRoll()
    {
        var s = NewGame();
        s.OpenGuild();
        s.CreateAndEnroll("Lefty", Race.Human, CharacterClass.Thief);
        var rolled = s.RollNewMember(Race.Dwarf, CharacterClass.Fighter);
        Assert.False(s.TryEnrollNewMember(rolled, "Lefty", out var error));
        Assert.Equal("Name is not unique!", error);
        Assert.False(s.TryEnrollNewMember(rolled, "FIRE ELEM", out error)); // [D✓] s65: elemental records count
        Assert.False(s.TryEnrollNewMember(rolled, "ABCDEFGHIJ", out _)); // DOS keeps 9 characters
        Assert.Single(s.Roster.Occupied);
        Assert.True(s.TryEnrollNewMember(rolled, "Swat", out _));
        Assert.Equal(2, s.Roster.Occupied.Count());
    }

    [OriginalDataFact]
    public void RollNewMember_SameRngAsCreateAndEnroll()
    {
        var a = NewGame(9);
        var b = NewGame(9);
        var viaCreate = a.CreateAndEnroll("Same", Race.Gnome, CharacterClass.Monk);
        var rolled = b.RollNewMember(Race.Gnome, CharacterClass.Monk);
        Assert.True(b.TryEnrollNewMember(rolled, "Same", out _));
        Assert.Equal(viaCreate.Stats, rolled.Stats);
        Assert.Equal(viaCreate.HpMax, rolled.HpMax);
        Assert.Equal(viaCreate.AgeClock, rolled.AgeClock);
    }

    [OriginalDataFact]
    public void GuildHasRoom_FalseAtTheManualCap()
    {
        var s = NewGame();
        s.OpenGuild();
        for (var i = 0; i < RulesProfile.PhantasieI.GuildMaxCharacters; i++)
            s.CreateAndEnroll($"M{i}", Race.Human, CharacterClass.Fighter);
        Assert.False(s.GuildHasRoom);
    }

    [OriginalDataFact]
    public void PurgeAndRename_ThroughTheSession()
    {
        var s = NewGame();
        s.OpenGuild();
        var a = s.CreateAndEnroll("Abbott", Race.Human, CharacterClass.Monk);
        var b = s.CreateAndEnroll("Sanara", Race.Elf, CharacterClass.Priest);
        Assert.True(s.TryRename(a, "Ali", out _));
        Assert.Equal("Ali", a.Name);
        Assert.False(s.TryRename(a, "Sanara", out var dup));
        Assert.Equal("Name is not unique!", dup);
        Assert.True(s.TryPurge(b, out _));
        Assert.Equal("Ali", Assert.Single(s.Roster.Occupied).Name);
    }

    [OriginalDataFact]
    public void Rename_RefusesAPartyMember_D065()
    {
        var s = NewGame();
        s.OpenGuild();
        var a = s.CreateAndEnroll("Abbott", Race.Human, CharacterClass.Monk);
        Assert.True(s.TryAddToParty(a, out _));
        Assert.False(s.TryRename(a, "Ali", out var error));
        Assert.Equal(GameSession.RenamePartyMemberRefusal, error);
        Assert.Equal("Abbott", a.Name);
    }
}
