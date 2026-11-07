using System.Text.Json;
using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Core.Tests;

/// <summary>
/// Phantasie IV's game object (T6.3, <see cref="Phantasie4Campaign"/>): entering a location map, where the party is after
/// the dungeon hands it out, and saves — a game saved and loaded at any prompt, mid-event included, goes on exactly as
/// one that never stopped.
/// </summary>
public class Phantasie4CampaignTests
{
    private static readonly GameOptions Manual = GameOptions.ForPreset(GameId.P4);

    /// <summary>The remaster's resumable saves (<c>p4.dungeon-reload</c> = <c>saved-map</c>): the "as if never stopped" pins.</summary>
    private static readonly GameOptions Resumable = Manual.With(P4RuleOptions.DungeonReload.Id, "saved-map");

    /// <summary>Draw n is a hash of (seed, n): a copy at the same count goes on with the same numbers.</summary>
    private sealed class CountingRng(int seed, long draws = 0) : IRandomSource
    {
        public long Draws { get; private set; } = draws;

        public int Next(int minInclusive, int maxExclusive)
        {
            var h = (ulong)(seed * 0x9E3779B9L) ^ (ulong)(Draws++ * 0x632BE59BD9B4E019L);
            h ^= h >> 31;
            h *= 0xBF58476D1CE4E5B9UL;
            h ^= h >> 29;
            return minInclusive + (int)(h % (ulong)(maxExclusive - minInclusive));
        }
    }

    private static P4Party Party()
    {
        var p = new P4Party { Gold = 5000 };
        for (var i = 0; i < 3; i++)
        {
            var m = new P4Member { Name = $"M{i}", Level = 3, HpMax = 30, Hp = 30, Defence = 10 };
            Array.Fill(m.Skills, 60);
            m.Items[0] = 1 + i;
            p.Members.Add(m);
        }
        p.Pack[5] = 0x81;
        p.PackCharges[5] = 20;
        return p;
    }

    private static Phantasie4Campaign NewGame(IRandomSource rng, P4Location? start = null, GameOptions? options = null) =>
        Phantasie4World.NewCampaign(options ?? Resumable, Party(), rng, start ?? new P4Location(P4Place.Outdoors, 1, 16, 2));

    /// <summary>Save and load (a fight cannot be saved: the game goes on as it is).</summary>
    private static Phantasie4Campaign Reload(Phantasie4Campaign c, IRandomSource rng) => c.Battle is not null ? c
        : Phantasie4World.RestoreCampaign(Phantasie4SaveCodec.ReadJson(Phantasie4SaveCodec.WriteJson(c.Capture())), rng);

    /// <summary>One answer to the running 7 × 7 battle: Beg (sometimes Greet), parry, take items.</summary>
    private static void BattleStep(P4Battle b, Random pick)
    {
        switch (b.Prompt)
        {
            case P4BattlePrompt.Options:
                b.ChooseOption(pick.Next(4) == 0 ? P4EncounterOption.Greet : P4EncounterOption.Beg); // short fights: the loops are what this pins
                break;
            case P4BattlePrompt.Order: b.Order(new P4Order(P4ActionKind.Parry)); break;
            case P4BattlePrompt.Treasure: b.TakeItem(true); break;
            default: b.Continue(); break;
        }
    }

    /// <summary>One answer to whatever the game waits for, chosen by <paramref name="pick"/>; false once nothing is left to do.</summary>
    private static bool Step(Phantasie4Campaign c, Random pick)
    {
        var d = c.Dungeon;
        switch (d.Prompt)
        {
            case P4DungeonPrompt.Action:
                if (pick.Next(8) == 0)
                    d.Act(pick.Next(2) == 0 ? P4DungeonCommand.Examine : P4DungeonCommand.Other);
                else
                    d.Move(new[] { 0, 1, 2, 3, 5, 6, 7, 8, 7, 7 }[pick.Next(10)]);
                return true;
            case P4DungeonPrompt.Wait: d.Continue(); return true;
            case P4DungeonPrompt.Menu: d.Choose(1 + pick.Next(d.MenuOptions.Count)); return true;
            case P4DungeonPrompt.Who: d.Choose(pick.Next(c.Party.Members.Count)); return true;
            case P4DungeonPrompt.Letters: d.EnterText("JILL"); return true;
            case P4DungeonPrompt.Number: d.EnterNumber(pick.Next(10)); return true;
            case P4DungeonPrompt.Fight: d.FightOver(partyLost: false); return true; // the 7 × 7 field is not built
            default: return false;
        }
    }

    private static string Fingerprint(Phantasie4Campaign c) => c.Battle is { } b
        ? $"battle {b.Round} {b.Prompt} {string.Join("|", b.Log)} {string.Join(",", b.Monsters.Select(m => $"{m.Square}/{m.Hp}"))}"
            + string.Join(",", c.Party.Members.Select(m => $"{m.Hp}/{m.Condition}"))
        : !c.CanSave
        ? $"building {c.Town.Prompt} {string.Join("|", c.Town.Box)} {string.Join("|", c.Town.Choices)} "
            + Phantasie4SaveCodec.WriteJson(new P4CampaignSnapshot(GameOptionsSnapshot.From(c.Options),
                P4PartySnapshot.From(c.Party), c.Place, 0, 0, 0, 0, null))
        : Phantasie4SaveCodec.WriteJson(c.Capture());

    [OriginalDataFact]
    public void EnterDungeon_KeepsTheFacing_AndLocationFollowsTheLoop()
    {
        var c = NewGame(new CountingRng(1));
        Assert.Equal(P4Place.Outdoors, c.Location.Place);
        Assert.False(c.LoopBuilt);
        Assert.Equal(0, c.Facing);

        c.EnterDungeon(1, 16, 27);
        Assert.True(c.LoopBuilt);
        Assert.Equal(new P4Location(P4Place.Dungeon, 1, c.Dungeon.X, c.Dungeon.Y), c.Location);
    }

    [OriginalDataFact]
    public void OnlyIvRules_AreAccepted()
    {
        Assert.Throws<ArgumentException>(() => Phantasie4World.NewCampaign(GameOptions.ForPreset(GameId.P1), Party(),
            new CountingRng(1), new P4Location(P4Place.Town, 1, 0, 0)));
    }

    [OriginalDataFact]
    public void DungeonEntries_ComeFromTheOutdoorPlaces()
    {
        var e = Phantasie4World.DungeonEntries();
        Assert.Equal(("Town Hall", 16, 27), e[1]);
        Assert.Equal(("Skull Fortress", 18, 29), e[21]);
        Assert.Equal(14, e.Count); // the other maps are reached from inside another map or a town
    }

    [OriginalDataFact]
    public void WalkingOffTheMapEdge_LeavesForTheOutdoors_AndFollowRunsTheOutdoorLoop()
    {
        // O_001's edge event (0x21, TELEPORT 1, 9, 10, 10): the Town Hall's south wall is open below x = 15.
        var c = NewGame(new CountingRng(7));
        c.EnterDungeon(1, 16, 27);
        var west = true;
        for (var i = 0; i < 50 && c.Dungeon.Prompt != P4DungeonPrompt.Left; i++)
        {
            if (c.Dungeon.Prompt == P4DungeonPrompt.Action)
            {
                c.Dungeon.Move(west ? 3 : 1);
                west = false;
            }
            else
                Assert.True(Step(c, new Random(1)));
        }
        Assert.Equal(P4DungeonPrompt.Left, c.Dungeon.Prompt);
        var at = c.Location;
        Assert.Equal(new P4Location(P4Place.Outdoors, 9, 10, 10), at);

        var back = Reload(c, new CountingRng(7));
        Assert.Equal(at, back.Location);
        Assert.Equal(P4Place.Dungeon, back.Place);

        // _game's switch: the outdoor loop arrives on (10, 10) of map 9 and runs that square's step.
        c.Follow();
        Assert.Equal(P4Place.Outdoors, c.Place);
        Assert.Equal((9, 10, 10), (c.Outdoor.Map, c.Outdoor.X, c.Outdoor.Y));
        Assert.Equal(at, c.Location);
    }

    /// <summary>One answer to whatever any loop waits for (dungeon, outdoors, town), then <see cref="Phantasie4Campaign.Follow"/>.</summary>
    private static bool StepAny(Phantasie4Campaign c, Random pick)
    {
        bool done;
        if (c.Battle is { } battle)
        {
            BattleStep(battle, pick);
            c.Follow();
            return true;
        }
        switch (c.Place)
        {
            case P4Place.Outdoors:
                var o = c.Outdoor;
                switch (o.Prompt)
                {
                    case P4OutdoorPrompt.Action:
                        if (pick.Next(20) == 0)
                            o.ToggleMap();
                        else
                            o.Move(pick.Next(8));
                        break;
                    case P4OutdoorPrompt.Wait: o.Continue(); break;
                    case P4OutdoorPrompt.Confirm: o.Answer(pick.Next(3) != 0); break;
                    case P4OutdoorPrompt.Fight: o.FightOver(pick.Next(4) == 0 ? P4FightOutcome.NotWon : P4FightOutcome.Won); break;
                    default: return false;
                }
                done = true;
                break;
            case P4Place.Town:
                var w = c.Town;
                switch (w.Prompt)
                {
                    case P4TownPrompt.Menu:
                        var b = w.Buildings[pick.Next(w.Buildings.Count)];
                        w.Enter(b == P4Building.Leave && pick.Next(2) == 0 ? P4Building.Inn : b);
                        break;
                    case P4TownPrompt.Wait: w.Continue(); break;
                    case P4TownPrompt.Confirm: w.Answer(pick.Next(2) == 0); break;
                    case P4TownPrompt.Shares: w.EnterShares([.. c.Party.Members.Select(_ => 1 + pick.Next(3))]); break;
                    case P4TownPrompt.Pick:
                        var open = Enumerable.Range(0, w.Choices.Count).Where(i => w.Choices[i].Enabled).ToList();
                        w.Choose(w.CanBack && (open.Count == 0 || pick.Next(3) == 0) ? -1 : open[pick.Next(open.Count)]);
                        break;
                    case P4TownPrompt.Who:
                        var who = Enumerable.Range(0, w.WhoEnabled.Count).Where(i => w.WhoEnabled[i]).ToList();
                        w.Choose(w.CanBack && (who.Count == 0 || pick.Next(2) == 0) ? -1 : who[pick.Next(who.Count)]);
                        break;
                    case P4TownPrompt.Number: w.EnterNumber(pick.Next(1000)); break;
                    case P4TownPrompt.Text: w.EnterText("N" + pick.Next(100)); break;
                    default: return false;
                }
                done = true;
                break;
            default:
                done = Step(c, pick);
                break;
        }
        c.Follow();
        return done;
    }

    /// <summary>
    /// The pinning test for the outdoor and town loops: a game started outdoors or in a town, wandering between map kinds,
    /// saved and loaded at any prompt, goes on as if it never stopped.
    /// </summary>
    [OriginalDataTheory]
    [InlineData(11, P4Place.Outdoors, 9, 9, 10, 1, false)] // Pendragon's exit: mostly in and out of town
    [InlineData(12, P4Place.Outdoors, 8, 17, 5, 3, true)] // Pendleton's ship square
    [InlineData(13, P4Place.Town, 2, 0, 0, 2, true)]
    [InlineData(17, P4Place.Outdoors, 9, 9, 24, 5, true)] // Tiris's exit
    public void SaveAndLoad_OutdoorsAndInTowns_GoesOnAsIfNeverStopped(int seed, P4Place place, int map, int x, int y, int every,
        bool fights)
    {
        P4Party Sailing()
        {
            var p = Party();
            foreach (var m in p.Members)
                m.AgingFactor = 100;
            (p.Ship, p.ShipMap, p.ShipX, p.ShipY) = (P4ShipState.Moored, 8, 17, 5);
            p.ExperiencePool = 77;
            return p;
        }
        var start = new P4Location(place, map, x, y);
        var straightRng = new CountingRng(seed);
        var straight = Phantasie4World.NewCampaign(Resumable, Sailing(), straightRng, start);
        straight.Start();
        var loadedRng = new CountingRng(seed);
        var loaded = Phantasie4World.NewCampaign(Resumable, Sailing(), loadedRng, start);
        loaded.Start();
        var pickA = new Random(seed);
        var pickB = new Random(seed);
        var places = new HashSet<P4Place>();
        var outdoorPrompts = new HashSet<P4OutdoorPrompt>();

        for (var i = 0; i < 1500; i++)
        {
            places.Add(loaded.Place);
            if (loaded.Place == P4Place.Outdoors)
                outdoorPrompts.Add(loaded.Outdoor.Prompt);
            if (i % every == 0 && loaded.CanSave) // a fight or an open building cannot be saved
            {
                loadedRng = new CountingRng(seed, loadedRng.Draws);
                loaded = Reload(loaded, loadedRng);
            }
            var a = StepAny(straight, pickA);
            var b = StepAny(loaded, pickB);
            Assert.Equal(a, b);
            Assert.Equal(straightRng.Draws, loadedRng.Draws);
            Assert.Equal(Fingerprint(straight), Fingerprint(loaded));
            if (!a)
                break;
        }
        Assert.Contains(P4Place.Outdoors, places);
        Assert.Contains(P4OutdoorPrompt.Confirm, outdoorPrompts);
        // Saved while a fight waits for its key, and with the fight itself pending.
        if (fights)
        {
            Assert.Contains(P4OutdoorPrompt.Wait, outdoorPrompts);
            Assert.Contains(P4OutdoorPrompt.Fight, outdoorPrompts);
        }
        Assert.Contains(P4Place.Town, places);
    }

    /// <summary>
    /// The pinning test: one game played straight through and the same game saved, written to JSON, read back and loaded
    /// at many points (whatever prompt it was at) end in the same state.
    /// </summary>
    [OriginalDataTheory]
    [InlineData(1, 1, 16, 27, 1)]
    [InlineData(2, 2, 8, 17, 5)] // O_002's lever room: waits, then a menu
    [InlineData(3, 2, 8, 17, 3)]
    [InlineData(4, 5, 15, 29, 7)]
    public void SaveAndLoad_AnywhereGoesOnAsIfNeverStopped(int seed, int map, int x, int y, int every)
    {
        var straightRng = new CountingRng(seed);
        var straight = NewGame(straightRng);
        straight.EnterDungeon(map, x, y);
        var loadedRng = new CountingRng(seed);
        var loaded = NewGame(loadedRng);
        loaded.EnterDungeon(map, x, y);
        var pickA = new Random(seed);
        var pickB = new Random(seed);
        var prompts = new HashSet<P4DungeonPrompt>();

        for (var i = 0; i < 1500; i++)
        {
            prompts.Add(loaded.Dungeon.Prompt);
            if (i % every == 0 && loaded.CanSave) // a fight or an open building cannot be saved
            {
                loadedRng = new CountingRng(seed, loadedRng.Draws);
                loaded = Reload(loaded, loadedRng);
            }
            var a = Step(straight, pickA);
            var b = Step(loaded, pickB);
            Assert.Equal(a, b);
            Assert.Equal(straightRng.Draws, loadedRng.Draws); // loading draws nothing
            Assert.Equal(Fingerprint(straight), Fingerprint(loaded));
            if (!a)
                break;
        }
        Assert.Contains(P4DungeonPrompt.Wait, prompts);
        if (map == 2)
            Assert.Contains(P4DungeonPrompt.Menu, prompts);
    }

    /// <summary>Answers the dungeon's prompts until it waits at its command prompt.</summary>
    private static void ToCommandPrompt(Phantasie4Campaign c, Random pick)
    {
        for (var i = 0; i < 50 && c.Dungeon.Prompt != P4DungeonPrompt.Action; i++)
            Assert.True(Step(c, pick));
        Assert.Equal(P4DungeonPrompt.Action, c.Dungeon.Prompt);
    }

    [OriginalDataFact]
    public void DungeonReload_Default_ReadsTheMapFresh_RotatesTheFlags_RecomputesTheMarks()
    {
        Assert.True(Phantasie4Campaign.ReloadsDungeon(Manual));
        var c = NewGame(new CountingRng(5), options: Manual);
        c.EnterDungeon(2, 8, 17);
        ToCommandPrompt(c, new Random(5));
        var (x, y) = (c.Dungeon.X, c.Dungeon.Y);
        var file = c.Dungeon.Map.File;
        // An event's changes (as _setfl / _setyoko make them): an opened chest and a new north picture.
        c.Dungeon.Map.SetFloor(x, y, P4DungeonMap.OpenedChest);
        c.Dungeon.Map.SetNorthPicture(x, y, 2);
        var counter = c.Party.StoryFlags[0];
        c.Party.LocationFlag(counter, 3) = 1;
        c.Party.LocationFlag(2, 4) = 1;

        var back = Reload(c, new CountingRng(5));

        Assert.Equal(P4Place.Dungeon, back.Place);
        Assert.Equal((2, x, y, c.Facing), (back.Dungeon.Map.Map, back.Dungeon.X, back.Dungeon.Y, back.Facing));
        Assert.Equal(file.Words, back.Dungeon.Map.Words); // _dload: O_002 read again, the event changes gone
        Assert.Equal(new P4DungeonMap(file).EdgeMarks, back.Dungeon.Map.EdgeMarks); // _prex on the fresh pictures
        Assert.Equal(counter - 1 < 1 ? 21 : counter - 1, back.Party.StoryFlags[0]); // _dreset: one more step
        Assert.Equal(0, back.Party.LocationFlag(counter, 3)); // and that map's 20 flags cleared
        Assert.True(back.Dungeon.NoStepSinceLoad);
    }

    [OriginalDataFact]
    public void DungeonReload_SavedMap_KeepsTheEventChanges_AndTheFlags()
    {
        var c = NewGame(new CountingRng(5));
        c.EnterDungeon(2, 8, 17);
        ToCommandPrompt(c, new Random(5));
        var (x, y) = (c.Dungeon.X, c.Dungeon.Y);
        c.Dungeon.Map.SetFloor(x, y, P4DungeonMap.OpenedChest);
        var counter = c.Party.StoryFlags[0];

        var back = Reload(c, new CountingRng(5));

        Assert.Equal(P4DungeonMap.OpenedChest, back.Dungeon.Map.Floor(x, y));
        Assert.Equal(counter, back.Party.StoryFlags[0]);
    }

    [OriginalDataFact]
    public void DungeonReload_Default_SavesOnlyAtTheCommandPrompt_SavedMapAnywhere()
    {
        foreach (var (options, midEventSaves) in new[] { (Manual, false), (Resumable, true) })
        {
            var c = NewGame(new CountingRng(2), options: options);
            c.EnterDungeon(2, 8, 17); // O_002's lever room: waits, then a menu
            var pick = new Random(2);
            var seen = false;
            for (var i = 0; i < 400 && !seen; i++)
            {
                if (c.Battle is null && c.Dungeon.Prompt is P4DungeonPrompt.Wait or P4DungeonPrompt.Menu)
                {
                    seen = true;
                    Assert.Equal(midEventSaves, c.CanSave);
                    Assert.Equal(!midEventSaves, c.SaveRefusedMidEvent);
                    if (!midEventSaves)
                        Assert.Throws<InvalidOperationException>(() => c.Capture());
                }
                if (c.Dungeon.Prompt == P4DungeonPrompt.Action)
                    Assert.True(c.CanSave);
                if (!Step(c, pick))
                    break;
            }
            Assert.True(seen);
        }
    }

    [OriginalDataFact]
    public void ASaveOutsideADungeon_KeepsThePlaceAndFacing()
    {
        var c = NewGame(new CountingRng(1), new P4Location(P4Place.Town, 3, 5, 6));
        var back = Reload(c, new CountingRng(1));
        Assert.Equal(new P4Location(P4Place.Town, 3, 5, 6), back.Location);
        Assert.False(back.Dungeon.HasMap);
        Assert.Equal(0, back.Facing);
    }

    [OriginalDataFact]
    public void WriteJsonFile_MakesTheSaveFolder()
    {
        // s135: F5 on a fresh install (no data folder yet) threw DirectoryNotFoundException.
        var root = Path.Combine(Path.GetTempPath(), "p4save-" + Guid.NewGuid().ToString("N"));
        var path = Path.Combine(root, "OpenPhantasie", "savegame-p4.json");
        try
        {
            Phantasie4SaveCodec.WriteJsonFile(path, NewGame(new CountingRng(1)).Capture());
            Assert.True(Phantasie4SaveCodec.IsPhantasie4Save(File.ReadAllText(path)));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [OriginalDataFact]
    public void TheSaveFile_NamesIv_AndPrefixesItemIds()
    {
        var c = NewGame(new CountingRng(1));
        var json = Phantasie4SaveCodec.WriteJson(c.Capture());
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.Equal("p4", root.GetProperty("game").GetString());
        var party = root.GetProperty("party");
        Assert.Equal("p4:1", party.GetProperty("members")[0].GetProperty("items")[0].GetString());
        Assert.Equal(JsonValueKind.Null, party.GetProperty("members")[0].GetProperty("items")[1].ValueKind);
        Assert.Equal("p4:129", party.GetProperty("pack")[5].GetString());
        Assert.True(Phantasie4SaveCodec.IsPhantasie4Save(json));

        // Dungeon map and encounter (monster) numbers are game ids too.
        c.EnterDungeon(2, 8, 17);
        using var inside = JsonDocument.Parse(Phantasie4SaveCodec.WriteJson(c.Capture()));
        var dungeon = inside.RootElement.GetProperty("dungeon");
        Assert.Equal("p4:2", dungeon.GetProperty("map").GetString());
        Assert.All(dungeon.GetProperty("pool").EnumerateArray(), m => Assert.StartsWith("p4:", m.GetString()));
        Assert.NotEqual(0, dungeon.GetProperty("pool").GetArrayLength());

        var back = Phantasie4SaveCodec.ReadJson(json).Party.ToParty();
        Assert.Equal(2, back.Members[1].Items[0]);
        Assert.Equal((0x81, 20), (back.Pack[5], back.PackCharges[5]));
    }

    [Fact]
    public void AnotherGamesSave_IsNotReadAsIv()
    {
        Assert.False(Phantasie4SaveCodec.IsPhantasie4Save("""{"version":3,"game":"p1"}"""));
        Assert.False(Phantasie4SaveCodec.IsPhantasie4Save("not json"));
        Assert.Throws<InvalidDataException>(() => Phantasie4SaveCodec.ReadJson("""{"version":3,"game":"p1"}"""));
    }
}
