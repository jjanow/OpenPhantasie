using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Core.Tests;

/// <summary>
/// The Continue / load path end to end (s74): a fresh save from the current build loads through the real codec and
/// <see cref="GameSession.Continue"/>; an old (pre-T1.2) save is reported as <see cref="OldSaveFormatException"/>, not a
/// parser error; <see cref="SeveralParties.Parties"/> feeds the QoL party list.
/// </summary>
public class ContinueLoadTests
{
    private static readonly GameDataRepository Data = GameDataRepository.LoadPhantasie1();

    /// <summary>As the Client's StartGame builds a session: seed 42, then a new game locked to <paramref name="rules"/>.</summary>
    private static GameSession NewGame(GameOptions rules)
    {
        var s = GameSessionFactory.Create(Data, new SeededDosRandomSource(42));
        s.StartNewGame(rules);
        return s;
    }

    private static string TempSave() =>
        Path.Combine(Path.GetTempPath(), $"phantasie-savegame-{Guid.NewGuid():N}.json");

    [OriginalDataFact]
    public void FreshSave_NewGamePartySave_LoadsInAnotherNewGame_WithTheSavesOwnRules()
    {
        var s = NewGame(GameOptions.ForPreset(GameId.P1));
        s.OpenGuild();
        var a = s.CreateAndEnroll("Alpha", Race.Human, CharacterClass.Fighter);
        var b = s.CreateAndEnroll("Bravo", Race.Elf, CharacterClass.Priest);
        Assert.True(s.TryAddToParty(a, out _));
        Assert.True(s.TryAddToParty(b, out _));
        s.LeaveGuild();
        Assert.Equal(GameMode.Town, s.Mode);

        var path = TempSave();
        try
        {
            SessionCodec.WriteJsonFile(path, s.CaptureSnapshot());

            // Main menu → Play: a new session with other new-game rules, then Continue with Guild number 1.
            var next = NewGame(GameOptions.ForPreset(GameId.P1, OptionPreset.Dos));
            var file = SessionCodec.ReadJsonFile(path);
            Assert.True(next.Continue(file, 1, out var error), error);
            Assert.Equal(GameMode.Town, next.Mode);
            Assert.Equal(1, next.CurrentTownId);
            Assert.Equal(["Alpha", "Bravo"], next.Party.Members.Select(m => m.Name));
            Assert.Equal(OptionPreset.Manual, next.Options.Preset); // the save's rules, not the new game's
            Assert.Equal(GameId.P1, next.Game);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [OriginalDataFact]
    public void FreshSave_FromTheWilderness_LoadsWhereItWas()
    {
        var s = NewGame(GameOptions.ForPreset(GameId.P1, OptionPreset.Remaster));
        s.OpenGuild();
        Assert.True(s.TryAddToParty(s.CreateAndEnroll("Alpha", Race.Human, CharacterClass.Fighter), out _));
        s.DepartGuildToWilderness();
        var at = (s.Overworld.RegionId, s.Overworld.X, s.Overworld.Y);
        var file = SessionCodec.ReadJson(SessionCodec.WriteJson(s.CaptureSnapshot()));

        var next = NewGame(GameOptions.ForPreset(GameId.P1));
        Assert.True(next.Continue(file, 1, out var error), error);
        Assert.Equal(GameMode.Overworld, next.Mode);
        Assert.Equal(at, (next.Overworld.RegionId, next.Overworld.X, next.Overworld.Y));
        Assert.Equal(OptionPreset.Remaster, next.Options.Preset);
    }

    /// <summary>What a save from before s71 looks like: version 2, no "game", bare-number ids (nested guild JSON too).</summary>
    private static string OldStyle(string json)
    {
        var root = JsonNode.Parse(json)!.AsObject();
        root.Remove("game");
        root["version"] = 2;
        // Inside the nested guild JSON a quote is written ".
        return Regex.Replace(root.ToJsonString(), @"(\\u0022|\\?"")p1:(\d+)(\\u0022|\\?"")", "$2");
    }

    [OriginalDataFact]
    public void OldSave_IsReportedAsOldSave_NotAsAParserError()
    {
        var s = NewGame(GameOptions.ForPreset(GameId.P1));
        s.OpenGuild();
        Assert.True(s.TryAddToParty(s.CreateAndEnroll("Alpha", Race.Human, CharacterClass.Fighter), out _));
        var json = SessionCodec.WriteJson(s.CaptureSnapshot());
        Assert.Contains("\"p1:", json);

        var old = OldStyle(json);
        Assert.DoesNotContain("p1:", old);
        Assert.Equal(2, Assert.Throws<OldSaveFormatException>(() => SessionCodec.ReadJson(old)).Version);

        // Version 3 claimed, but a guild member's nested ids are still bare numbers.
        var root = JsonNode.Parse(json)!.AsObject();
        var slot = root["roster"]!.AsArray().First(n => n is not null)!;
        slot["characterJson"] = Regex.Replace((string)slot["characterJson"]!, "\"p1:(\\d+)\"", "$1");
        Assert.Throws<OldSaveFormatException>(() => SessionCodec.ReadJson(root.ToJsonString()));

        // Not JSON, or not a save, is an ordinary unreadable file.
        Assert.Throws<InvalidDataException>(() => SessionCodec.ReadJson("{ not json"));
        Assert.Throws<InvalidDataException>(() => SessionCodec.ReadJson("[1, 2]"));
    }

    [OriginalDataFact]
    public void Parties_ListsTheActivePartyThenTheParkedOnes_NamesFromTheRoster()
    {
        var s = NewGame(GameOptions.ForPreset(GameId.P1));
        s.OpenGuild();
        var a = s.CreateAndEnroll("Alpha", Race.Human, CharacterClass.Fighter);
        var b = s.CreateAndEnroll("Bravo", Race.Elf, CharacterClass.Priest);
        var c = s.CreateAndEnroll("Charlie", Race.Dwarf, CharacterClass.Thief);
        s.CreateAndEnroll("Delta", Race.Human, CharacterClass.Wizard); // in no party: not listed
        Assert.True(s.TryAddToParty(a, out _));
        Assert.True(s.TryAddToParty(b, out _));
        s.DepartGuildToWilderness();

        // Call up Charlie (in no party): A + B are parked in the wilderness, Charlie's new party stands in Pelnor.
        var cNumber = Array.IndexOf(s.Roster.Slots, c) + 1;
        Assert.True(s.Continue(SessionCodec.ReadJson(SessionCodec.WriteJson(s.CaptureSnapshot())), cNumber, out var error), error);
        s.OpenGuild();
        Assert.True(s.TryAddToParty(s.Roster.Slots[cNumber - 1]!, out _));
        s.LeaveGuild();
        var file = SessionCodec.ReadJson(SessionCodec.WriteJson(s.CaptureSnapshot()));

        var parties = SeveralParties.Parties(file);
        Assert.Equal(2, parties.Count);
        Assert.Equal((cNumber, true, GameMode.Town, (int?)1), (parties[0].GuildNumber, parties[0].Active, parties[0].Mode, parties[0].TownId));
        Assert.Equal(["Charlie"], parties[0].Names);
        Assert.Equal(["Alpha", "Bravo"], parties[1].Names);
        Assert.False(parties[1].Active);
        Assert.Equal(GameMode.Overworld, parties[1].Mode);
        Assert.Null(parties[1].TownId);

        // Each entry's Guild number calls up that party.
        var next = NewGame(GameOptions.ForPreset(GameId.P1));
        Assert.True(next.Continue(file, parties[1].GuildNumber, out error), error);
        Assert.Equal(["Alpha", "Bravo"], next.Party.Members.Select(m => m.Name));
        Assert.Equal(GameMode.Overworld, next.Mode);
    }

    [Fact]
    public void Parties_SkipsAnEmptyActiveParty_AndShowsAFightsReturnMode()
    {
        var file = new SessionSnapshot
        {
            RosterSlots = [new Character { Name = "Alpha" }, null, new Character { Name = "Gamma" }],
            PartySlotIndices = [],
            Mode = GameMode.Town,
            CurrentTownId = 1,
            OtherParties =
            [
                new SessionSnapshot { PartySlotIndices = [2, 0], Mode = GameMode.Combat, CombatReturnMode = GameMode.Dungeon,
                    Dungeon = new DungeonSnapshot { LevelId = 4 } },
            ],
        };
        var p = Assert.Single(SeveralParties.Parties(file));
        Assert.Equal(3, p.GuildNumber); // the first member's (Gamma, slot 2)
        Assert.Equal(["Gamma", "Alpha"], p.Names);
        Assert.Equal((GameMode.Dungeon, (int?)4, (int?)null), (p.Mode, p.DungeonLevelId, p.TownId));
    }

    [Fact]
    public void Select_KeepsTheFilesGame_ForAParkedOrNewParty()
    {
        var file = new SessionSnapshot
        {
            Game = GameId.P3,
            RosterSlots = [new Character { Name = "Alpha" }, new Character { Name = "Bravo" }],
            PartySlotIndices = [0],
            Mode = GameMode.Overworld,
            OtherParties = [new SessionSnapshot { Game = GameId.P3, PartySlotIndices = [1], Mode = GameMode.Town, CurrentTownId = 2 }],
        };
        var next = SeveralParties.Select(file, 2, 20, out var newParty, out var error);
        Assert.NotNull(next);
        Assert.False(newParty, error);
        Assert.Equal(GameId.P3, next.Game);
        Assert.All(next.OtherParties, p => Assert.Equal(GameId.P3, p.Game));
    }
}
