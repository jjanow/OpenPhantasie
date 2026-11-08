using Microsoft.Xna.Framework.Input;
using OpenPhantasie.Client;
using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client.Tests;

/// <summary>Guild N)EW / P)URGE / R)ENAME / L)IST dialogs and the <c>--test-party</c> bootstrap.</summary>
public partial class GuildMemberDialogTests
{
    private static GameSession InGuild()
    {
        var s = GameSessionFactory.Create(ClientSessions.Data, new SeededDosRandomSource(4));
        s.StartNewGame();
        s.OpenGuild();
        return s;
    }

    private static Keys[] Type(string text) =>
        [.. text.Select(c => c == ' ' ? Keys.Space : char.IsDigit(c) ? Keys.D0 + (c - '0') : Keys.A + (char.ToUpperInvariant(c) - 'A'))];

    [OriginalDataFact]
    public void New_RaceClassAcceptName_EnrollsAndShowsSheet()
    {
        var s = InGuild();
        var d = new GuildNewDialog();
        Assert.Null(d.Open(s));
        d.HandleKeys([Keys.D3], s); // ELF
        d.HandleKeys([Keys.D6], s); // WIZARD
        Assert.Equal("ACCEPT? (Y/N)", d.Lines(s, ClientSessions.Data)[^1]);
        Assert.Empty(s.Roster.Occupied);
        d.HandleKeys([Keys.Y], s);
        d.HandleKeys(Type("zandor"), s);
        Assert.Contains("INPUT NAME: ZANDOR_", d.Lines(s, ClientSessions.Data));
        d.HandleKeys([Keys.Enter], s);
        var member = Assert.Single(s.Roster.Occupied);
        Assert.Equal("ZANDOR", member.Name);
        Assert.Equal(CharacterClass.Wizard, member.Class);
        Assert.Equal(Race.Elf, member.Race);
        Assert.Equal("ZANDOR", d.Lines(s, ClientSessions.Data)[0]); // full sheet
        d.HandleKeys([Keys.Space], s);
        Assert.False(d.IsOpen);
    }

    [OriginalDataFact]
    public void New_NoAtAccept_DropsTheRoll_AndRestartsAtRace()
    {
        var s = InGuild();
        var d = new GuildNewDialog();
        d.Open(s);
        d.HandleKeys([Keys.D1], s);
        d.HandleKeys([Keys.D4], s);
        d.HandleKeys([Keys.N], s);
        Assert.Equal("SELECT RACE", d.Lines(s, ClientSessions.Data)[0]);
        Assert.Empty(s.Roster.Occupied);
    }

    [OriginalDataFact]
    public void New_III_YesAtAccept_Plays6_NoPlaysNothing()
    {
        // s138: Amiga 0x13938, OK on the rolled character's sheet before the name field.
        var s = GameSessionFactory.Create(GameDataRepository.Load(GameDataRepository.DefaultPathFor(GameId.P3)),
            new SeededDosRandomSource(4));
        s.StartNewGame();
        s.OpenGuild();
        var d = new GuildNewDialog();
        Assert.Null(d.Open(s));
        d.HandleKeys([Keys.D1], s);
        d.HandleKeys([Keys.D1], s);
        var mark = s.SoundSequence;
        d.HandleKeys([Keys.N], s);
        Assert.Empty(s.SoundsSince(mark));
        d.HandleKeys([Keys.D1], s);
        d.HandleKeys([Keys.D1], s);
        d.HandleKeys([Keys.Y], s);
        Assert.Equal([P3Sounds.RollKept], s.SoundsSince(mark));
    }

    [OriginalDataFact]
    public void New_I_YesAtAccept_PlaysNothing()
    {
        var s = InGuild();
        var d = new GuildNewDialog();
        d.Open(s);
        d.HandleKeys([Keys.D1], s);
        d.HandleKeys([Keys.D4], s);
        var mark = s.SoundSequence;
        d.HandleKeys([Keys.Y], s);
        Assert.Empty(s.SoundsSince(mark));
    }

    [OriginalDataFact]
    public void Purge_RemovesTheMember_WithoutConfirm()
    {
        var s = InGuild();
        s.CreateAndEnroll("ONE", Race.Human, CharacterClass.Fighter);
        s.CreateAndEnroll("TWO", Race.Human, CharacterClass.Fighter);
        var d = new GuildPickDialog();
        Assert.Null(d.OpenPurge(s));
        d.HandleKeys([Keys.Down, Keys.Enter], s);
        Assert.Equal("ONE", Assert.Single(s.Roster.Occupied).Name);
        Assert.True(d.IsOpen); // DOS asks again
    }

    /// <summary>
    /// s158: III's Guild › Examine lists every roster member (party members and others alike, Amiga 0xbabc / DOS guild_menu
    /// item 6); a pick opens that member's sheet and the list stays to ask again; Esc on the sheet closes only the sheet.
    /// </summary>
    [OriginalDataFact]
    public void Examine_ListsTheRoster_AndOpensThePickedSheet()
    {
        var s = InGuild();
        var d = new GuildPickDialog();
        Assert.Equal(UiText.S("guild.roster_empty"), d.OpenExamine(s, _ => { }));
        var one = s.CreateAndEnroll("ONE", Race.Human, CharacterClass.Fighter);
        var two = s.CreateAndEnroll("TWO", Race.Human, CharacterClass.Fighter);
        Assert.True(s.TryAddToParty(two, out _));
        var inspect = new InspectDialog(new PromptMenu(), ClientSessions.Data);
        Assert.Null(d.OpenExamine(s, inspect.OpenSheet));
        var lines = d.Lines(s, ClientSessions.Data);
        Assert.Equal(GuildPickDialog.ExamineTitle, lines[0]);
        Assert.Equal(3, lines.Count); // both: the party member too
        d.HandleKeys([Keys.Down, Keys.Enter], s);
        Assert.Same(two, inspect.SheetMember);
        Assert.True(d.IsOpen);
        inspect.HandleKeys([Keys.Escape], s);
        Assert.False(inspect.IsOpen); // the sheet only; the roster list asks again
        Assert.True(d.IsOpen);
        Assert.Equal(1, s.Roster.Occupied.Count(c => c == one)); // nothing purged
        d.HandleKeys([Keys.Escape], s);
        Assert.False(d.IsOpen);
        Assert.Null(d.OpenPurge(s)); // the next Purge is a purge again
        Assert.Equal(GuildPickDialog.PurgeTitle, d.Lines(s, ClientSessions.Data)[0]);
    }

    [OriginalDataFact]
    public void Rename_AsksForTheNewName()
    {
        var s = InGuild();
        var m = s.CreateAndEnroll("OLD", Race.Human, CharacterClass.Fighter);
        var d = new GuildPickDialog();
        d.OpenRename(s);
        d.HandleKeys([Keys.Enter], s);
        d.HandleKeys([.. Type("new"), Keys.Enter], s);
        Assert.Equal("NEW", m.Name);
    }

    [OriginalDataFact]
    public void List_ShowsGuildNumberNameClassTown()
    {
        var s = InGuild();
        s.CreateAndEnroll("LEFTY", Race.Human, CharacterClass.Thief);
        var row = Assert.Single(GuildListDialog.Rows(s));
        Assert.Equal(" 1 LEFTY        TH TOWN 1", row);
    }

    [OriginalDataFact]
    public void Dialogs_RefuseOutsideTheGuild()
    {
        var s = GameSessionFactory.Create(ClientSessions.Data, new SeededDosRandomSource(4));
        s.StartNewGame();
        Assert.Equal("Only in the Guild.", new GuildNewDialog().Open(s));
        Assert.Equal("Only in the Guild.", new GuildListDialog().Open(s));
    }

    [OriginalDataFact]
    public void TestParty_IsSixMembers_OnePerClass_InTheGuild()
    {
        var s = GameSessionFactory.Create(ClientSessions.Data, new SeededDosRandomSource(42));
        s.StartNewGame();
        TestParty.Bootstrap(s, ClientSessions.Data.CreateItemCatalog());
        Assert.Equal(GameMode.Guild, s.Mode);
        Assert.Equal(Party.MaxMembers, s.Party.Members.Count);
        Assert.Equal(Enum.GetValues<CharacterClass>().Order(), s.Party.Members.Select(m => m.Class).Order());
        Assert.All(s.Party.Members, m => Assert.Equal(0, m.GoldInBank));
        s.LeaveGuild();
        Assert.True(s.LeaveTown());
    }

    [OriginalDataTheory]
    [InlineData(5)]
    [InlineData(LevelUp.MaxLevel)]
    public void TestLevel_TrainsEveryMember_ThroughTheGuild_GoldUntouched(int level)
    {
        var plain = GameSessionFactory.Create(ClientSessions.Data, new SeededDosRandomSource(42));
        plain.StartNewGame();
        TestParty.Bootstrap(plain, ClientSessions.Data.CreateItemCatalog());
        var s = GameSessionFactory.Create(ClientSessions.Data, new SeededDosRandomSource(42));
        s.StartNewGame();
        TestParty.Bootstrap(s, ClientSessions.Data.CreateItemCatalog(), level);
        Assert.Equal(GameMode.Guild, s.Mode);
        Assert.All(s.Party.Members, m => Assert.Equal(level, m.Level));
        Assert.Equal(plain.Party.GoldCarried, s.Party.GoldCarried);
        Assert.All(s.Party.Members.Zip(plain.Party.Members), p => Assert.True(p.First.HpMax > p.Second.HpMax));
        Assert.All(s.Party.Members, m =>
        {
            Assert.Equal(m.HpMax, m.HpCur);
            Assert.Equal(m.MpCeiling, m.MagicMax);
            Assert.Equal(m.MagicMax, m.MagicCur);
            Assert.DoesNotContain(2, m.SpellsKnown); // every spell the level opened is learned
            Assert.Contains(m.WornWeapon, m.InventoryItemIds);
            Assert.Contains(m.WornArmor, m.InventoryItemIds);
            Assert.True(ItemRules.IsUsable(m.WornWeapon, m.Stats));
        });
        var wizard = s.Party.Members.Single(m => m.Class == CharacterClass.Wizard);
        Assert.True(wizard.SpellsKnown.Count(k => k == 1) > plain.Party.Members.Single(m => m.Class == CharacterClass.Wizard).SpellsKnown.Count(k => k == 1));
        var points = (IEnumerable<Character> p) => p.Sum(m => ItemRules.CombatGate(m.WornWeapon).Points + ItemRules.CombatGate(m.WornArmor).Points);
        Assert.True(points(s.Party.Members) > points(plain.Party.Members));
    }

    [OriginalDataFact]
    public void TestParty_LevelOne_StartsHealed_WithItsStartingGear()
    {
        var s = GameSessionFactory.Create(ClientSessions.Data, new SeededDosRandomSource(42));
        s.StartNewGame();
        TestParty.Bootstrap(s, ClientSessions.Data.CreateItemCatalog());
        Assert.All(s.Party.Members, m =>
        {
            Assert.Equal(1, m.Level);
            Assert.Equal(m.HpMax, m.HpCur);
            Assert.Equal((48, 22, 4), (m.WornWeapon, m.WornArmor, m.WornShield)); // club, robes, small shield
        });
    }

    [Theory]
    [InlineData(new string[0], null)]
    [InlineData(new[] { "--test-party" }, 1)]
    [InlineData(new[] { "--test-level", "7" }, 7)]
    [InlineData(new[] { "--test-level=15" }, 15)]
    [InlineData(new[] { "--test-party", "--test-level", "3" }, 3)]
    public void TestLevel_Args(string[] args, int? expected)
    {
        Assert.Equal(expected, TestParty.ParseArgs(args, out var error));
        Assert.Null(error);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("16")]
    [InlineData("x")]
    public void TestLevel_OutOfRange_IsAnError(string value)
    {
        Assert.Null(TestParty.ParseArgs(["--test-level", value], out var error));
        Assert.NotNull(error);
        Assert.Null(TestParty.ParseArgs(["--test-level"], out error));
        Assert.NotNull(error);
    }

    [OriginalDataFact]
    public void TestGame_Arg_PicksTheGame()
    {
        Assert.Equal(OpenPhantasie.Core.GameId.P1, TestParty.ParseGame(["--test-party"], out var error));
        Assert.Null(error);
        Assert.Equal(OpenPhantasie.Core.GameId.P2, TestParty.ParseGame(["--test-party", "--test-game", "2"], out error));
        Assert.Null(error);
        Assert.Equal(1, TestParty.ParseArgs(["--test-party", "--test-game", "2"], out error));
        Assert.Null(error);
        TestParty.ParseGame(["--test-game", "9"], out error);
        Assert.NotNull(error);
        if (!MainMenuScreen.IsPlayable(OpenPhantasie.Core.GameId.P4)) // IV is not built: no test party there
        {
            TestParty.ParseGame(["--test-game", "4"], out error);
            Assert.NotNull(error);
        }
        TestParty.ParseGame(["--test-game"], out error);
        Assert.NotNull(error);
    }
}
