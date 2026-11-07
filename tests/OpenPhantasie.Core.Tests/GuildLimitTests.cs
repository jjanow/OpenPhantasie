using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Core.Tests;

/// <summary>
/// QoL rules option <c>guild-limit</c> (PLAN P3, user s79 from D3-003): one descriptor in every game's tab. <c>game</c> =
/// the game's own cap from its <c>rules.json</c> (Original presets); <c>none</c> = no Guild roster limit (Remaster). A DOS
/// export of a grown roster writes GUILD.DAT's 22 records and names the rest.
/// </summary>
public class GuildLimitTests
{
    private static readonly GameDataRepository Data = GameDataRepository.LoadPhantasie1();

    private static GameSession NewGame(GameOptions options)
    {
        var s = GameSessionFactory.Create(Data, new SeededDosRandomSource(5));
        s.StartNewGame(options);
        s.OpenGuild();
        return s;
    }

    private static GameOptions Limit(string value) => GameOptions.ForPreset(GameId.P1).With("guild-limit", value);

    /// <summary>Distinct letter-only names: AAx, ABx, …</summary>
    private static string Name(int i) => $"{(char)('A' + i / 26)}{(char)('A' + i % 26)}x";

    [Fact]
    public void Descriptor_IsInEveryGamesTab_OffInBothOriginalPresets()
    {
        var d = RuleOptionRegistry.GuildLimit;
        Assert.True(d.EveryGame);
        Assert.Equal(["game", "none"], d.Values);
        Assert.Equal(("game", "game", "none"), (d.Manual, d.Dos, d.Remaster));
        Assert.Null(d.Difference); // QoL, no D-entry
        foreach (var game in Enum.GetValues<GameId>())
        {
            Assert.Single(RuleOptionRegistry.For(game), o => o.Id == "guild-limit");
            Assert.Equal("guild-limit", RuleOptionRegistry.For(game)[^1].Id); // last in each tab
            Assert.False(GameOptions.ForPreset(game).GuildUnlimited);
            Assert.False(GameOptions.ForPreset(game, OptionPreset.Dos).GuildUnlimited);
            Assert.True(GameOptions.ForPreset(game, OptionPreset.Remaster).GuildUnlimited);
        }
    }

    [OriginalDataTheory]
    [InlineData("manual", 21)] // D-002: the manual's 21
    [InlineData("dos", 22)]    // DOS's 22 (rules.json guild_by_platform.dos)
    public void Off_KeepsTheGamesOwnCap(string guildSize, int cap)
    {
        Assert.Equal(cap, guildSize == "dos" ? Data.Rules.ForPlatform("dos").GuildMaxCharacters : Data.Rules.GuildMaxCharacters);
        var s = NewGame(Limit("game").With("p1.guild-size", guildSize));
        for (var i = 0; i < cap; i++)
            s.CreateAndEnroll(Name(i), Race.Human, CharacterClass.Fighter);
        Assert.False(s.GuildHasRoom);
        Assert.Throws<InvalidOperationException>(() => s.CreateAndEnroll(Name(cap), Race.Human, CharacterClass.Fighter));
        Assert.Equal(Data.Rules.GuildSlotCount, s.Roster.Slots.Length);
    }

    [OriginalDataFact]
    public void Off_EveryGamesCapComesFromItsRulesJson()
    {
        foreach (var game in new[] { GameId.P1, GameId.P2 })
        {
            var rules = GameTables.Load(game).Rules;
            var roster = new GuildRoster(rules);
            Assert.False(roster.Unlimited);
            Assert.Equal(rules.GuildSlotCount, roster.Slots.Length);
            Assert.Equal(rules.GuildMaxCharacters, roster.MaxCharacters);
        }
    }

    [OriginalDataFact]
    public void None_GrowsTheRosterPastTheGamesSlots_HolesFirst()
    {
        var s = NewGame(Limit("none").With("p1.guild-size", "manual"));
        var made = Enumerable.Range(0, 30).Select(i => s.CreateAndEnroll(Name(i), Race.Elf, CharacterClass.Wizard)).ToList();
        Assert.True(s.GuildHasRoom);
        Assert.Equal(30, s.Roster.Slots.Length); // one slot at a time: no trailing holes
        Assert.Equal(made, s.Roster.Slots);

        // A purged character's hole is filled before the roster grows again.
        Assert.True(s.TryPurge(made[3], out _));
        var next = s.CreateAndEnroll(Name(40), Race.Dwarf, CharacterClass.Priest);
        Assert.Same(next, s.Roster.Slots[3]);
        Assert.Equal(30, s.Roster.Slots.Length);
    }

    [OriginalDataFact]
    public void None_SaveAndLoad_KeepsTheWholeRoster_AndTheParty()
    {
        var s = NewGame(Limit("none"));
        for (var i = 0; i < 26; i++)
            s.CreateAndEnroll(Name(i), Race.Human, CharacterClass.Thief);
        var late = s.Roster.Slots[25]!;
        Assert.True(s.TryAddToParty(late, out _));
        var json = SessionCodec.WriteJson(s.CaptureSnapshot());

        var loaded = GameSessionFactory.Create(Data, new SeededDosRandomSource(9));
        loaded.ApplySnapshot(SessionCodec.ReadJson(json));
        Assert.True(loaded.Options.GuildUnlimited);
        Assert.Equal(26, loaded.Roster.Occupied.Count());
        Assert.Equal(late.Name, loaded.Party.Members.Single().Name);
        Assert.True(loaded.GuildHasRoom);

        // A limited save loaded after it shrinks the roster back to the game's slots.
        var limited = GameSessionFactory.Create(Data, new SeededDosRandomSource(9));
        limited.StartNewGame();
        loaded.ApplySnapshot(limited.CaptureSnapshot());
        Assert.Equal(Data.Rules.GuildSlotCount, loaded.Roster.Slots.Length);
        Assert.False(loaded.Roster.Unlimited);
    }

    [OriginalDataFact]
    public void None_Continue_ReachesAGuildNumberPastTheGamesSlots()
    {
        var s = NewGame(Limit("none"));
        for (var i = 0; i < 30; i++)
            s.CreateAndEnroll(Name(i), Race.Human, CharacterClass.Fighter);
        Assert.True(s.TryAddToParty(s.Roster.Slots[24]!, out _));
        var file = SessionCodec.ReadJson(SessionCodec.WriteJson(s.CaptureSnapshot()));

        var loaded = GameSessionFactory.Create(Data, new SeededDosRandomSource(9));
        Assert.True(loaded.Continue(file, 25, out var error), error); // slot 24: past I's 22 slots, where elementals would number
        Assert.Equal(Name(24), loaded.Party.Members.Single().Name);
    }

    [OriginalDataFact]
    public void DosExport_GrownRoster_WritesThePartyAndTheFirstOthers_NamesTheRest()
    {
        var s = NewGame(Limit("none"));
        for (var i = 0; i < 25; i++)
            s.CreateAndEnroll(Name(i), Race.Human, CharacterClass.Fighter);
        var party = new[] { s.Roster.Slots[24]!, s.Roster.Slots[0]! };
        foreach (var m in party)
            Assert.True(s.TryAddToParty(m, out _));
        s.EnterTown(1);

        var export = DosSaveConverter.ExportFrom(s, Data);
        Assert.False(export.Blocked, string.Join("; ", export.Warnings));
        var clamp = Assert.Single(export.Warnings, w => w.Kind == DosSaveWarningKind.Clamp && w.File == DosSaveSet.GuildFile);
        // 25 characters, 22 records: the party (slots 24 and 0) plus slots 1..20; slots 21..23 are left out.
        Assert.Contains($"{Name(21)}, {Name(22)}, {Name(23)}", clamp.Message);
        Assert.Contains(export.Warnings, w => w.Kind == DosSaveWarningKind.Note && w.Message.Contains("Guild numbers past 22"));

        var import = DosSaveConverter.Import(export.Files!, Data);
        var names = import.Snapshot.RosterSlots.Where(c => c is not null).Select(c => c!.Name).ToList();
        Assert.Equal(22, names.Count);
        Assert.Contains(Name(24), names);
        Assert.Equal([Name(0), Name(24)],
            import.Snapshot.PartySlotIndices.Select(i => import.Snapshot.RosterSlots[i]!.Name).Order());
        Assert.Equal(25, s.Roster.Occupied.Count()); // the live game keeps everyone
    }

    [Fact]
    public void FitGuild_RosterThatFits_IsWrittenAsBefore()
    {
        var warnings = new List<DosSaveWarning>();
        var ch = new Character { Name = "Solo" };
        var exact = new Character?[GuildCodec.SlotCount];
        exact[5] = ch;
        Assert.Same(exact, DosSaveConverter.FitGuild(exact, [5], warnings));

        // Grown but with nobody past the file's slots: cut to 22, nothing moves, nothing to say.
        var grown = new Character?[GuildCodec.SlotCount + 3];
        grown[5] = ch;
        var fitted = DosSaveConverter.FitGuild(grown, [5], warnings);
        Assert.Equal(GuildCodec.SlotCount, fitted.Length);
        Assert.Same(ch, fitted[5]);
        Assert.Empty(warnings);
    }

    [OriginalDataFact]
    public void Import_UnderNone_HasNoGuildSizeNote()
    {
        // 22 characters, as DOS fills (D-002), imported under the manual's 21 or under no limit.
        var s = NewGame(Limit("game").With("p1.guild-size", "dos"));
        for (var i = 0; i < 22; i++)
            s.CreateAndEnroll(Name(i), Race.Human, CharacterClass.Fighter);
        Assert.True(s.TryAddToParty(s.Roster.Slots[0]!, out _));
        s.EnterTown(1);
        var files = DosSaveConverter.ExportFrom(s, Data).Files!;

        bool Noted(GameOptions o) => DosSaveConverter.Import(files, Data, o).Warnings.Any(w => w.Message.Contains("(D-002)"));
        Assert.True(Noted(Limit("game")));
        Assert.False(Noted(Limit("none")));
    }
}
