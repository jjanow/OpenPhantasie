using static OpenPhantasie.Core.Tests.Phantasie3CampaignTests;

namespace OpenPhantasie.Core.Tests;

/// <summary>
/// Phase 09 T4: III's dungeons in play — entrances on exit cells, the cell codes of the step (ovl4 <c>0xe14</c>), exits to
/// the planes, the event programs (<c>dungeon-events.md</c>) and the two DOS bugs fixed by default (the archive altar,
/// sign-extended event gold) with DOS as an option.
/// </summary>
public class Phantasie3DungeonTests
{
    /// <summary>Put the party in dungeon <paramref name="n"/> on (<paramref name="x"/>, <paramref name="y"/>), no prompts.</summary>
    internal static void Enter(GameSession s, int n, int x = -1, int y = 0)
    {
        if (s.Mode == GameMode.Town)
            Assert.True(s.LeaveTown());
        var entranceX = Campaign(s).World.DungeonEntranceX[n - 1];
        s.EnterDungeon(n, entranceX, 0);
        Assert.True(s.DungeonExitPending); // the entrance is an exit cell: "An exit. Will you take it?" first (live s82)
        s.AnswerDungeonExit(takeIt: false, saveMap: false);
        if (x >= 0)
        {
            s.Dungeon!.X = x;
            s.Dungeon.Y = y;
        }
    }

    [OriginalDataFact]
    public void SavedMaps_OnePerDungeon_DNPLetter_SurviveASave()
    {
        Assert.True(Phantasie3CampaignTests.Data.Rules.DungeonSavePerDungeon);
        var s = WithParty();
        Assert.True(s.LeaveTown());
        foreach (var n in new[] { 5, 6 })
        {
            s.EnterDungeon(n, Campaign(s).World.DungeonEntranceX[n - 1], 0); // on the exit cell: the exit question waits
            s.Dungeon!.Level!.SetBufferByte(Phantasie3Campaign.EventSquares, (byte)(0x40 + n)); // a mark in the work record
            s.AnswerDungeonExit(takeIt: true, saveMap: true);
        }
        Assert.Equal([5, 6], s.DungeonSaves.Keys.Order());
        Assert.Equal(6, s.DungeonSave!.DungeonId); // the last written

        var t = OpenPhantasie.Data.GameSessionFactory.Create(Phantasie3CampaignTests.Data, new SeededDosRandomSource(3));
        t.ApplySnapshot(OpenPhantasie.Data.SessionCodec.ReadJson(OpenPhantasie.Data.SessionCodec.WriteJson(s.CaptureSnapshot())));
        Assert.Equal([5, 6], t.DungeonSaves.Keys.Order());
        Assert.Equal(6, t.DungeonSave!.DungeonId);
        t.EnterDungeon(5, Campaign(t).World.DungeonEntranceX[4], 0);
        Assert.Equal(0x45, t.Dungeon!.Level!.GetBufferByte(Phantasie3Campaign.EventSquares)); // dungeon 5's own map

        // Leaving 5 without saving forgets only 5's map (the remaster's D-027 rule); 6's stays.
        t.AnswerDungeonExit(takeIt: true, saveMap: false);
        Assert.Equal([6], t.DungeonSaves.Keys.Order());
        Assert.Equal(6, t.DungeonSave!.DungeonId);

        // D3-038 dos (p3.dungeon-map-no): a NO keeps the old map, as DOS writes nothing.
        var u = WithParty(options: GameOptions.ForPreset(GameId.P3).With("p3.dungeon-map-no", "dos"));
        Assert.True(u.LeaveTown());
        u.EnterDungeon(5, Campaign(u).World.DungeonEntranceX[4], 0);
        u.AnswerDungeonExit(takeIt: true, saveMap: true);
        u.EnterDungeon(5, Campaign(u).World.DungeonEntranceX[4], 0);
        u.AnswerDungeonExit(takeIt: true, saveMap: false);
        Assert.Equal([5], u.DungeonSaves.Keys.Order());
    }

    /// <summary>Step onto (<paramref name="x"/>, <paramref name="y"/>) from a walkable neighbour.</summary>
    internal static DungeonMoveResult StepOnto(GameSession s, int x, int y)
    {
        var level = s.Dungeon!.Level!;
        foreach (var (dx, dy, dir) in new[]
                 {
                     (0, -1, CardinalDirection.South), (0, 1, CardinalDirection.North), (-1, 0, CardinalDirection.East),
                     (1, 0, CardinalDirection.West),
                 })
        {
            var (nx, ny) = (x + dx, y + dy);
            if (level.IsWalkable(nx, ny) && level.Get(nx, ny) is < 0xdd and not (>= 0x80 and <= 0x89) and not (>= 2 and <= 6)
                and not (>= 8 and <= 17))
            {
                s.Dungeon.X = nx;
                s.Dungeon.Y = ny;
                return s.MoveDungeon(dir);
            }
        }
        throw new InvalidOperationException($"No quiet neighbour of ({x}, {y}).");
    }

    [OriginalDataFact]
    public void Dungeons_AreTheirRecords_24x30_EnteredOnTheirExitCell()
    {
        var s = WithParty();
        Enter(s, 5);
        var level = s.Dungeon!.Level!;
        Assert.Equal(24, level.GridWidth);
        Assert.Equal(30, level.GridHeight);
        Assert.Equal(4224, level.BufferLength);
        Assert.Equal((11, 0), (s.Dungeon.X, s.Dungeon.Y)); // the archives (letter A) enter at x 11
        Assert.Equal(Phantasie3Campaign.DungeonChanceOnEntry, s.Dungeon.WanderingChance);
    }

    [OriginalDataFact]
    public void Walls_AreCellZero()
    {
        var s = WithParty();
        Enter(s, 5);
        var r = s.MoveDungeon(CardinalDirection.West); // (10, 0) is 0
        Assert.Equal(DungeonMoveKind.BlockedWall, r.Kind);
        Assert.Equal((11, 0), (s.Dungeon!.X, s.Dungeon.Y));
        var down = s.MoveDungeon(CardinalDirection.South); // (11, 1) = 0x11, a random-encounter corridor
        Assert.NotEqual(DungeonMoveKind.BlockedWall, down.Kind);
    }

    [OriginalDataFact]
    public void LeavingByTheEntrance_PutsThePartyBackOnTheDungeonsSquare()
    {
        var s = WithParty();
        s.LeaveTown();
        s.Overworld.X = 25;
        s.Overworld.Y = 39;
        s.EnterDungeon(5, 11, 0);
        s.AnswerDungeonExit(takeIt: true, saveMap: false);
        Assert.Equal(GameMode.Overworld, s.Mode);
        Assert.Equal((0, 25, 39), (s.Overworld.RegionId, s.Overworld.X, s.Overworld.Y));
    }

    [OriginalDataTheory]
    [InlineData(0xd8, Phantasie3Campaign.LightPlane, 4, 1)] // crystal building exit 1: destination 29
    [InlineData(0xd9, Phantasie3Campaign.DarkPlane, 4, 1)]  // exit 2: destination 31 (its x, y bytes are ignored)
    public void CrystalBuildingExits_LeadToThePlanes(byte exitCell, int plane, int x, int y)
    {
        var s = WithParty();
        Enter(s, 3);
        var d = s.Dungeon!;
        d.Level!.Set(d.X, d.Y, exitCell); // as the lever sets it (dungeon-events.md §5)
        s.CampaignAccess.DungeonExitPending = true;
        s.AnswerDungeonExit(takeIt: true, saveMap: false);
        Assert.Equal((plane, x, y), (s.Overworld.RegionId, s.Overworld.X, s.Overworld.Y));
        Assert.Equal(9, s.CurrentOverworldRegion.GridWidth);
    }

    [OriginalDataFact]
    public void CastleOfDarkExit_LeadsToTheNetherworld()
    {
        var s = WithParty();
        Enter(s, 11);
        var d = s.Dungeon!;
        d.X = 3; // the 0xd8 cell at (3, 0), destination 41
        d.Y = 0;
        s.CampaignAccess.DungeonExitPending = true;
        s.AnswerDungeonExit(takeIt: true, saveMap: false);
        Assert.Equal((Phantasie3Campaign.Netherworld, 7, 3), (s.Overworld.RegionId, s.Overworld.X, s.Overworld.Y));
    }

    private static GameSession AtAltar(GameOptions? options = null)
    {
        var s = WithParty(options: options);
        s.Party.Members[0].P3.Phantasie2Score = 200; // a Phantasie II hero (only T5's transfer writes it)
        Enter(s, 5);
        StepOnto(s, 19, 24); // the strange altar (entry 0xe3, four choices)
        var prompt = Assert.IsType<PendingDungeonEvent>(s.DungeonEventPrompt);
        Assert.Equal(4, prompt.Message.Options.Count);
        Assert.Equal("4) Pray on it", prompt.Message.Options[3]);
        return s;
    }

    [OriginalDataFact]
    public void ArchiveAltar_Fixed_GrantsDivineAid_AsItsTextSays()
    {
        var s = AtAltar();
        s.AnswerDungeonEvent(4);
        ReadPages(s);
        Assert.All(s.Party.Members, m => Assert.True(m.Phantasie3!.DivineAid));
        Assert.All(s.Party.Members, m => Assert.True(P3BodyAreas.Of(m).AllOkay));
        Assert.Contains(s.Log, l => l.Contains("minor diety", StringComparison.Ordinal));
        var gold = s.Sack.Gold;
        StepOnto(s, 19, 24);
        s.AnswerDungeonEvent(4); // someone has Divine Aid now: the altar gives gold instead
        ReadPages(s);
        Assert.Equal(gold + 1024, s.Sack.Gold);
    }

    [OriginalDataFact]
    public void ArchiveAltar_Dos_InjuresEveryTorso_AndGivesNoSpell()
    {
        var s = AtAltar(GameOptions.ForPreset(GameId.P3).With(P3RuleOptions.Altar.Id, "dos"));
        s.AnswerDungeonEvent(4);
        ReadPages(s);
        Assert.All(s.Party.Members, m => Assert.False(m.Phantasie3!.DivineAid));
        Assert.All(s.Party.Members, m => Assert.Equal(P3AreaState.Injured, P3BodyAreas.Of(m)[P3BodyArea.Torso]));
        var gold = s.Sack.Gold;
        StepOnto(s, 19, 24);
        s.AnswerDungeonEvent(4); // torsos hurt: gold (live s82: "The party finds some gold.", +1,024)
        ReadPages(s);
        Assert.Equal(gold + 1024, s.Sack.Gold);
    }

    private static GameSession AtWizard(GameOptions? options = null)
    {
        var s = WithParty(options: options);
        s.Party.GoldCarried = 50_000;
        s.Party.Members[0].Experience = 20_000;
        s.Party.Members[1].Experience = 5_000;
        Enter(s, 4);
        StepOnto(s, 16, 21); // "There is an old human wizard here. 1) Attack 2) Talk"
        s.AnswerDungeonEvent(2); // Talk → op 22 runs the offer square (three choices)
        var offer = Assert.IsType<PendingDungeonEvent>(s.DungeonEventPrompt);
        Assert.Equal(3, offer.Message.Options.Count);
        s.AnswerDungeonEvent(2); // Okay
        return s;
    }

    [OriginalDataFact]
    public void GnomeWizard_SellsDivineAid_AndEventGoldIsUnsignedByDefault()
    {
        var s = AtWizard();
        Assert.All(s.Party.Members, m => Assert.True(m.Phantasie3!.DivineAid));
        Assert.Equal(0, s.Party.GoldCarried + s.Sack.Gold); // op 9 a1 = 255 takes 65,280 more (nothing left)
        Assert.Equal(8_000, s.Party.Members[0].Experience); // −12,000 above 12,000
        Assert.Equal(5_000, s.Party.Members[1].Experience);
    }

    [OriginalDataFact]
    public void GnomeWizard_DosSign_GivesTheLast256GoldBack()
    {
        var s = AtWizard(GameOptions.ForPreset(GameId.P3).With(P3RuleOptions.EventGold.Id, "dos"));
        Assert.Equal(256, s.Party.GoldCarried + s.Sack.Gold); // live s82: 50,000 → 0 → 256
    }

    [OriginalDataFact]
    public void ForcedFightCell_StartsItsFight_AndWinningLeavesPlainFloor()
    {
        var s = WithParty();
        Enter(s, 5);
        var level = s.Dungeon!.Level!;
        // (8, 0) is 0x80: forced fight with record[0x2d1].
        var expected = level.GetBufferByte(Phantasie3Campaign.ForcedFightTable);
        s.Dungeon.X = 9; // a random-encounter corridor (0x0b); chance 0 keeps it quiet
        s.Dungeon.Y = 0;
        s.Dungeon.WanderingChance = 0;
        var r = s.MoveDungeon(CardinalDirection.West);
        Assert.Equal(DungeonMoveKind.TriggerCombat, r.Kind);
        Assert.Equal(expected, r.MonsterId);
        Assert.Equal(GameMode.Combat, s.Mode);
        Assert.Equal(expected, s.Combat!.FirstMonsterId);
        Assert.InRange(s.Combat.Ranks.Count, 1, 2);
    }

    [OriginalDataFact]
    public void DungeonChance_TenOnEntry_OnlyRandomSquaresLowerIt()
    {
        var s = WithParty();
        Enter(s, 5);
        var d = s.Dungeon!;
        d.X = 9;
        d.Y = 0;
        d.WanderingChance = 0; // quiet corridor on the way
        s.MoveDungeon(CardinalDirection.West); // (8, 0): forced fight
        Assert.Equal(GameMode.Combat, s.Mode);
        Assert.Equal(0, d.WanderingChance); // the forced fight's build leaves the chance alone
        s.FightToEnd();
        Settle(s);

        d.WanderingChance = Phantasie3Campaign.DungeonChanceOnEntry;
        d.X = 11;
        d.Y = 2;
        for (var i = 0; i < 40 && s.Mode == GameMode.Dungeon && d.WanderingChance == 10; i++)
        {
            s.MoveDungeon(i % 2 == 0 ? CardinalDirection.South : CardinalDirection.North); // the 0x11 corridor
            if (s.Mode == GameMode.Combat)
                break;
        }
        Assert.Equal(GameMode.Combat, s.Mode);
        Assert.Equal(9, d.WanderingChance); // a random square's fight: −1
    }

    private static void Settle(GameSession s)
    {
        while (s.PendingTreasure.Count > 0)
            s.AnswerTreasure(false);
    }

    /// <summary>Press a key on every event-text page that waits (III's "Press a key").</summary>
    internal static void ReadPages(GameSession s)
    {
        for (var i = 0; i < 20 && s.DungeonPage is { WaitsForKey: true }; i++)
            s.AcknowledgeDungeonPage();
    }

    [OriginalDataFact]
    public void EventFlags_SetQuestBitsOnEveryMember_AndTheScoreWeighsThem()
    {
        var s = WithParty();
        foreach (var m in s.Party.Members)
            m.P3.QuestBits = 0b1000_0011; // Kilmor found, funeral, Castle of Darkness
        var best = Campaign(s).ComputeScores(s.Party);
        Assert.Equal(0 + 4 + 30 + 1 / 6, best);
        s.Party.Members[0].P3.SocialClass = Phantasie3CharacterData.Superhero;
        Assert.Equal(250, Campaign(s).ComputeScores(s.Party));
    }
}
