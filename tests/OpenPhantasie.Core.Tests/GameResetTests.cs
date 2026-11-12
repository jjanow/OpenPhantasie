using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Core.Tests;

/// <summary>P3 "Reset the game" (user s203 / s204, <c>docs/plan/16-reset-game.md</c>): Purge and Keep, I–IV.</summary>
public class GameResetTests
{
    private static GameOptions Rules(string treasury) => GameOptions.ForPreset(GameId.P1).With("treasury", treasury);

    private static SessionSnapshot SaveAndRead(SessionSnapshot s) => SessionCodec.ReadJson(SessionCodec.WriteJson(s));

    /// <summary>A played I game: Alpha (in the party, 100 purse gold, 30 sack gold) and Bravo in the Guild, a sack item.</summary>
    private static (GameDataRepository Data, SessionSnapshot Save) PlayedI(string treasury = "off")
    {
        var data = GameDataRepository.LoadPhantasie1();
        var s = GameSessionFactory.Create(data, new SeededDosRandomSource(7));
        s.StartNewGame(Rules(treasury));
        s.OpenGuild();
        var a = s.CreateAndEnroll("Alpha", Race.Human, CharacterClass.Fighter);
        s.CreateAndEnroll("Bravo", Race.Elf, CharacterClass.Priest);
        Assert.True(s.TryAddToParty(a, out _));
        s.LeaveGuild();
        a.GoldInBank = 50;
        a.Level = 7;
        s.Party.GoldCarried = 100;
        s.Sack.Gold = 30;
        Assert.True(s.Sack.TryAdd(1));
        return (data, SaveAndRead(s.CaptureSnapshot()));
    }

    // ---- DOS I's region rule (OV1 0x4914) ---------------------------------------------------------------------------

    [Fact]
    public void DosResetCells_Unfogs_RearmsX8_AndFogs()
    {
        byte[] cells = [17, 18, 120 + 18, 120 + 27, 0, 120 + 100, 119];
        GameSession.DosResetCells(cells);
        Assert.Equal(new byte[] { 137, 137, 137, 147, 120, 220, 239 }, cells);
    }

    [OriginalDataFact]
    public void DosResetFog_LeavesPristineDataAndOut1Alone()
    {
        var data = GameDataRepository.LoadPhantasie1();
        var s = GameSessionFactory.Create(data, new SeededDosRandomSource(1));
        s.StartNewGame();
        var before = s.CaptureSnapshot().Wilderness!.RegionTiles!;
        var here = s.CurrentOverworldRegion;
        Assert.Equal("OUT1.DAT", here.FileName, ignoreCase: true);
        here.Tiles[0] = 18; // a spent fight in OUT1: DOS leaves blocks 0 / 4 as they are
        s.ApplyDosResetFog();
        var after = s.CaptureSnapshot().Wilderness!.RegionTiles!;
        Assert.Equal(18, after[here.RegionId][0]);
        foreach (var (id, tiles) in before)
            if (id != here.RegionId)
                Assert.Equal(tiles, after[id]); // pristine DOS data: every other cell fogged, no x8
    }

    // ---- I–III --------------------------------------------------------------------------------------------------------

    [OriginalDataFact]
    public void Purge_IsANewGameUnderTheSavesRules()
    {
        var (data, save) = PlayedI("trip");
        var reset = GameReset.ResetSession(save, data, ResetMode.Purge, dosFog: true);
        Assert.All(reset.RosterSlots, c => Assert.Null(c));
        Assert.Empty(reset.PartySlotIndices);
        Assert.Equal(0, reset.PartyGold);
        Assert.DoesNotContain(reset.Sack.ItemIds, i => i != 0);
        Assert.Equal(1, reset.CurrentTownId); // Pelnor
        Assert.Equal("trip", reset.Options!.ToOptions(GameId.P1).Values["treasury"]);
    }

    [OriginalDataFact]
    public void Keep_TreasuryOff_DepositsTheTripGold_AndDissolvesTheParty()
    {
        var (data, save) = PlayedI();
        var reset = GameReset.ResetSession(save, data, ResetMode.Keep, dosFog: true);
        var kept = reset.RosterSlots.OfType<Character>().ToList();
        Assert.Equal(["Alpha", "Bravo"], kept.Select(c => c.Name));
        var alpha = kept[0];
        Assert.Equal(7, alpha.Level);
        Assert.Equal(50 + 130, alpha.GoldInBank); // purse 100 + sack gold 30, one share (the only member)
        Assert.All(kept, c => Assert.False(c.InParty));
        Assert.All(kept, c => Assert.Equal(1, c.CurrentTown));
        Assert.Empty(reset.PartySlotIndices);
        Assert.Equal(0, reset.PartyGold);
        Assert.DoesNotContain(reset.Sack.ItemIds, i => i != 0); // the sack resets, as DOS zeroes SACK.DAT
        Assert.Equal(save.RosterSlots.Length, reset.RosterSlots.Length); // slot for slot (Guild numbers kept)
    }

    [OriginalDataFact]
    public void Keep_TreasuryOn_SharesTheTreasury_ThenTheNewGameCollectsIt()
    {
        var (data, save) = PlayedI("trip");
        var balance = (save.Treasury?.Balance ?? 0) + 130; // the trip gold joins the treasury
        var banks = save.RosterSlots.OfType<Character>().Sum(c => (long)c.GoldInBank); // not swept yet in the save
        var reset = GameReset.ResetSession(save, data, ResetMode.Keep, dosFog: true);
        var kept = reset.RosterSlots.OfType<Character>().ToList();
        Assert.Equal(2, kept.Count);
        Assert.All(kept, c => Assert.Equal(0, c.GoldInBank)); // swept into the new game's treasury
        Assert.Equal(balance / 2 * 2 + banks, reset.Treasury!.Balance); // the treasury's odd remainder is dropped, as for transfers
    }

    [OriginalDataFact]
    public void ResetSession_WritesOverTheSameFile()
    {
        var (_, save) = PlayedI();
        var path = Path.Combine(Path.GetTempPath(), $"reset-{Guid.NewGuid():N}.json");
        try
        {
            SessionCodec.WriteJsonFile(path, save);
            GameReset.ResetSession(path, GameDataRepository.DefaultPathFor(GameId.P1), ResetMode.Purge, dosFog: false);
            Assert.All(SessionCodec.ReadJsonFile(path).RosterSlots, c => Assert.Null(c));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [OriginalDataFact]
    public void Keep_PaysThePendingXpPool_OneShareEach()
    {
        var (data, save) = PlayedI();
        save.Sack.Experience = 101;
        var before = save.RosterSlots.OfType<Character>().First().Experience;
        var reset = GameReset.ResetSession(save, data, ResetMode.Keep, dosFog: true);
        var kept = reset.RosterSlots.OfType<Character>().ToList();
        Assert.Equal(before + 101, kept[0].Experience); // Alpha is the only member
        Assert.Equal(0, reset.Sack.Experience);
    }

    [OriginalDataFact]
    public void Keep_WithNoParty_SharesTheTripGoldOverTheRoster()
    {
        var (data, save) = PlayedI();
        save.PartySlotIndices = [];
        var banks = save.RosterSlots.OfType<Character>().Select(c => c.GoldInBank).ToList();
        var kept = GameReset.ResetSession(save, data, ResetMode.Keep, dosFog: true).RosterSlots.OfType<Character>().ToList();
        Assert.Equal(banks[0] + 65, kept[0].GoldInBank); // 130 over two characters
        Assert.Equal(banks[1] + 65, kept[1].GoldInBank);
    }

    [OriginalDataTheory]
    [InlineData(GameId.P2)]
    [InlineData(GameId.P3)]
    public void Keep_IIAndIII_RoundTripAndLoad(GameId game)
    {
        var data = GameDataRepository.Load(GameDataRepository.DefaultPathFor(game));
        var s = GameSessionFactory.Create(data, new SeededDosRandomSource(7));
        s.StartNewGame(GameOptions.ForPreset(game));
        s.OpenGuild();
        var a = s.CreateAndEnroll("ALPHA", Race.Human, CharacterClass.Fighter);
        Assert.True(s.TryAddToParty(a, out _));
        s.LeaveGuild();
        s.Party.GoldCarried = 40;
        var save = SaveAndRead(s.CaptureSnapshot());

        var reset = SaveAndRead(GameReset.ResetSession(save, data, ResetMode.Keep, dosFog: false));
        var kept = Assert.Single(reset.RosterSlots.OfType<Character>());
        Assert.Equal("ALPHA", kept.Name);
        Assert.False(kept.InParty);
        Assert.Empty(reset.PartySlotIndices);

        var loaded = GameSessionFactory.Create(data, new SeededDosRandomSource(8));
        loaded.StartNewGame(GameOptions.ForPreset(game));
        loaded.ContinueSavedParty(reset); // the Client's load path (D-069 dos: the saved party)
        Assert.Contains(loaded.Roster.Occupied, c => c.Name == "ALPHA");
    }

    // ---- IV -------------------------------------------------------------------------------------------------------------

    private static P4CampaignSnapshot PlayedIV(GameOptions o, out P4Party fresh)
    {
        var game = Phantasie4World.NewGame(o, new SeededRandomSource(3));
        var p = game.Party;
        p.Roster[2] = new P4Member { Name = "STAY", Town = 4, GuildSlot = 2, Account = 10 };
        p.Roster[0] = new P4Member { Name = "OLD", Town = 4, GuildSlot = 0, Account = 1, Level = 1 };
        var member = p.Roster[0]!.Clone();
        member.Level = 9; // the party copy is the newer record
        p.Members.Add(member);
        p.Gold = 40;
        p.Pack[0] = 5;
        p.StoryFlags[3] = 1;
        p.HallDescendants[1] = 7;
        var save = game.Capture();
        fresh = Phantasie4World.NewGame(o, new SeededRandomSource(4)).Party;
        return save;
    }

    [OriginalDataFact]
    public void IV_Keep_WritesMembersBack_DepositsThePurse_ResetsTheRest()
    {
        var o = GameOptions.ForPreset(GameId.P4);
        var save = PlayedIV(o, out var party);
        GameReset.ResetPhantasie4(save, party, o, Phantasie4World.NewGame(o, new SeededRandomSource(5)).Tables.Towns,
            ResetMode.Keep);
        Assert.Empty(party.Members);
        Assert.Equal(9, party.Roster[0]!.Level);
        Assert.Equal(40, party.Roster[0]!.Account); // 1 + 39: IV's ShareOut truncates 40 / 1.0001 (P4Town.ShareOut)
        Assert.Equal(10, party.Roster[2]!.Account);
        Assert.All(party.Roster.OfType<P4Member>(), m => Assert.Equal(P4TownLoop.Pendragon, m.Town));
        Assert.Equal(0, party.Gold);
        Assert.Equal(5, party.Pack[0]); // the pack is party storage (Distribute's P): it comes over
        Assert.Equal(0, party.StoryFlags[3]);
        Assert.Equal(7, party.HallDescendants[1]); // the Hall stays
    }

    [OriginalDataFact]
    public void IV_ResetFile_LoadsAgain()
    {
        var o = GameOptions.ForPreset(GameId.P4);
        var save = PlayedIV(o, out _);
        var path = Path.Combine(Path.GetTempPath(), $"reset-p4-{Guid.NewGuid():N}.json");
        try
        {
            Phantasie4SaveCodec.WriteJsonFile(path, save);
            GameReset.ResetPhantasie4(path, ResetMode.Keep);
            var game = Phantasie4World.RestoreCampaign(Phantasie4SaveCodec.ReadJsonFile(path), new SeededRandomSource(9));
            Assert.Equal(P4Place.Town, game.Place);
            Assert.Equal(9, game.Party.Roster[0]!.Level);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [OriginalDataFact]
    public void IV_Purge_KeepsOnlyTheHall()
    {
        var o = GameOptions.ForPreset(GameId.P4);
        var save = PlayedIV(o, out var party);
        GameReset.ResetPhantasie4(save, party, o, Phantasie4World.NewGame(o, new SeededRandomSource(5)).Tables.Towns,
            ResetMode.Purge);
        Assert.All(party.Roster, m => Assert.Null(m));
        Assert.Equal(7, party.HallDescendants[1]);
    }
}
