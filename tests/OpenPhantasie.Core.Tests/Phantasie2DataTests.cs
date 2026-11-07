using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Core.Tests;

/// <summary>
/// Phase 09 T3: <c>GameData/phantasie2/</c> (built by <c>tools/scripts/build_p2_gamedata.py</c> from the ST extracts)
/// loads through the game-neutral <see cref="GameTables"/>, and so does Phantasie I's folder, with the same loader.
/// II's rules profile carries the decoded training delta (D2-012).
/// </summary>
public class Phantasie2DataTests
{
    private static readonly GameTables P2 = GameTables.Load(GameId.P2);
    private static readonly GameTables P1 = GameTables.Load(GameId.P1);

    [OriginalDataFact]
    public void Phantasie2_LoadsWithItsOwnCounts()
    {
        Assert.Equal(GameId.P2, P2.Game);
        Assert.Equal("atari-st", P2.Manifest.Platform);
        Assert.Equal(80, P2.Monsters.Count);
        Assert.Equal(181, P2.Items.Count);
        Assert.Equal(56, P2.Spells.Count);
        Assert.Equal(16, P2.Names.Races.Count);
        Assert.Equal(17, P2.OverworldRegions.Count);
        Assert.Equal(8, P2.Dungeons.Count);
        Assert.Equal(8, P2.MessageAreas.Count);
        Assert.Equal(14, P2.Scrolls.Count);
        Assert.Equal(12, P2.Towns.Count);
        Assert.Equal(3, P2.Elementals.Count);
        Assert.All(P2.Monsters, m => Assert.Equal(GameId.P2, m.ContentId.Game));
        Assert.All(P2.Items, i => Assert.Equal(GameId.P2, i.ContentId.Game));
        Assert.All(P2.Dungeons, d => Assert.Equal(GameId.P2, d.ContentId.Game));
    }

    [OriginalDataFact]
    public void Phantasie2_KeyRecordsMatchTheExtracts()
    {
        Assert.Equal("PLUTO", P2.Monsters[79].Name);
        Assert.Equal("---ORB---", P2.Items[150].Name);
        Assert.Equal("FLMN WAND", P2.Items[149].Name);
        Assert.Equal("PIPPACOTT", P2.Towns.Single(t => t.Id == 1).Name);
        Assert.Equal("UNDEAD", P2.Names.Races[15].Name);
        Assert.Equal(["FIRE_ELEM", "WATER_ELEM", "EARTH_ELEM"], P2.Elementals.Select(e => e.Name));
        Assert.Contains("FERRONRAH", P2.Scrolls[0].Text.ToUpperInvariant());

        // Divine spells: local 54 = DIVINE I (slot 57, #58), local 55 = DIVINE II (slot 56, #57). Costs = (slot % 4) + 1
        // on the ST (magic-divine.md §1): DIVINE I 2 MP, DIVINE II 1 MP.
        Assert.Equal(("DIVINE I", 57, 2), (P2.Spells[54].Name, P2.Spells[54].DosSlot, P2.Spells[54].MagicPointCost));
        Assert.Equal(("DIVINE II", 56, 1), (P2.Spells[55].Name, P2.Spells[55].DosSlot, P2.Spells[55].MagicPointCost));
        Assert.All(P2.Spells, s => Assert.True(s.MagicPointCostKnown));
        Assert.All(P2.Spells, s => Assert.Equal(s.DosSlot % 4 + 1, s.MagicPointCost));
    }

    [OriginalDataFact]
    public void Phantasie2_TownsCarryPlaneAndTransportationList()
    {
        // ST logic-specs/towns.md §3–4: towns 1–6 Ferronrah, 7–10 Netherworld; TRANSPORTATION offers the other five
        // Ferronrah towns, or only the Netherworld pair (7↔8, 9↔10); STARVILLE / OLYMPIA are never offered.
        string?[] planes = [.. P2.Towns.OrderBy(t => t.Id).Select(t => t.Plane)];
        IEnumerable<string?> expected = ["ferronrah", "ferronrah", "ferronrah", "ferronrah", "ferronrah", "ferronrah",
            "netherworld", "netherworld", "netherworld", "netherworld", null, null];
        Assert.Equal(expected, planes);
        IReadOnlyList<int>? Dest(int id) => P2.Towns.Single(t => t.Id == id).TransportDestinations;
        Assert.Equal([2, 3, 4, 5, 6], Dest(1));
        Assert.Equal([1, 2, 3, 4, 5], Dest(6));
        Assert.Equal([8], Dest(7));
        Assert.Equal([7], Dest(8));
        Assert.Equal([10], Dest(9));
        Assert.Equal([9], Dest(10));
        Assert.Empty(Dest(11)!);
        Assert.All(P2.Towns, t => Assert.DoesNotContain(t.TransportDestinations!, d => d > 10 || d == t.Id));

        // Phantasie I's towns.json sets neither field: its own every-other-town rule stays in force.
        Assert.All(P1.Towns, t => Assert.Null(t.TransportDestinations));
        Assert.All(P1.Towns, t => Assert.Null(t.Plane));
    }

    [OriginalDataFact]
    public void Phantasie2_SharesIEquipmentAndCreationTables()
    {
        // Items 0–100 and every value factor are identical to I (FORMAT.md §3).
        for (var i = 0; i < 181; i++)
            Assert.Equal(P1.Items[i].ValueFactor, P2.Items[i].ValueFactor);
        for (var i = 0; i <= 100; i++)
            Assert.Equal((P1.Items[i].Name, P1.Items[i].Kind, P1.Items[i].Points), (P2.Items[i].Name, P2.Items[i].Kind, P2.Items[i].Points));
        // Starter kit points seen live on II's EQUIPMENT page (roster-live.md): SM SHIELD 4, MACE 4, LEATHER 3.
        Assert.Equal((4, 4, 3), (P2.Items[4].Points, P2.Items[49].Points, P2.Items[23].Points));

        // D2-009 (manual, as in I): the two cells I's data takes from the manual are the same in II's data.
        Assert.Equal(P1.CreationTables.RaceAttributeModifiers, P2.CreationTables.RaceAttributeModifiers);
        // D2-015 (user s79: manual): II's wizards learn RESURRECTION (slot 43) at level 13; every other cell is I's.
        var p2Levels = P2.CreationTables.SpellLevelsByClass.Select(r => r.Take(55).ToArray()).ToArray();
        Assert.Equal(13, p2Levels[(int)CharacterClass.Wizard][43]);
        p2Levels[(int)CharacterClass.Wizard][43] = 0;
        Assert.Equal(P1.CreationTables.SpellLevelsByClass.Select(r => r.Take(55)), p2Levels);
        // D2-021 (user s80: manual bands, as I's D-003): II's bands are I's.
        Assert.NotEmpty(P1.CreationTables.LifespanBandsByRace);
        Assert.Equal(P1.CreationTables.LifespanBandsByRace, P2.CreationTables.LifespanBandsByRace);
    }

    [OriginalDataFact]
    public void Phantasie1_LoadsThroughTheSameLoader_AndAgreesWithTheFullRepository()
    {
        var full = GameDataRepository.LoadPhantasie1();
        Assert.Equal(GameId.P1, P1.Game);
        Assert.Equal(full.Monsters.Count, P1.Monsters.Count);
        Assert.Equal(full.Spells.Select(s => (s.Name, s.MagicPointCost)), P1.Spells.Select(s => (s.Name, s.MagicPointCost)));
        Assert.Equal(full.OverworldRegions.Count, P1.OverworldRegions.Count);
        Assert.Equal(full.Dungeons.Count, P1.Dungeons.Count);
        Assert.Equal(full.Towns.Select(t => t.Name), P1.Towns.Select(t => t.Name));
        Assert.Equal(full.Rules, P1.Rules);
    }

    [OriginalDataFact]
    public void Phantasie2_Rules_TrainingDelta_D2012()
    {
        var t = P2.Rules.Training!;
        Assert.Equal(GameId.P2, P2.Rules.Game);
        Assert.Equal(TrainingFactorReset.LevelZero, t.Reset);
        Assert.Equal(new ClassTraining(CharacterClass.Fighter, 2, 0, 1, 0, 0, 0, 33.0, 0x84000046), t.For(CharacterClass.Fighter));
        Assert.Equal(new ClassTraining(CharacterClass.Priest, 1, 2, 0, 0, 0, 0, 36.3, 0x91333346), t.For(CharacterClass.Priest));
        foreach (var cls in new[] { CharacterClass.Thief, CharacterClass.Monk, CharacterClass.Ranger, CharacterClass.Wizard })
            Assert.Equal(TrainingRules.PhantasieI.For(cls), t.For(cls) with { DivisorFfp = null });
        Assert.Equal(22, P2.Rules.GuildSlotCount);
        Assert.Equal(22, P2.Rules.GuildByPlatform["atari-st"].SlotCount);
    }

    [OriginalDataFact]
    public void Phantasie2_Rules_CapsAndStartingGold()
    {
        // ST N)EW fills all 22 slots (0x0ebf6); the profile keeps the manual's 21 / 65,000 until D2-019 / D2-020 are decided.
        Assert.Equal(new PlatformGuildSize(22, 22), P2.Rules.GuildByPlatform["atari-st"]);
        Assert.Equal(21, P2.Rules.GuildMaxCharacters);
        Assert.Equal((65000L, 65000L), (P2.Rules.PartyGoldCap, P2.Rules.BankGoldCap));
        Assert.Equal(256, P2.Rules.StartingGold); // create_character 0x0cc2a [C]
    }

    [OriginalDataFact]
    public void Phantasie2_MonsterSpriteSizes_FromTheSizeTable()
    {
        // PHANT.PRG 0x2112e (w, h) [C]: SKELETON 32x34, ORANGE DRAGON 80x73, the Pluto set 80x80 from 74 on.
        Assert.Equal((32, 34), (P2.Monsters[0].SpriteWidth, P2.Monsters[0].SpriteHeight));
        Assert.Equal((80, 73), (P2.Monsters[54].SpriteWidth, P2.Monsters[54].SpriteHeight));
        Assert.All(P2.Monsters.Skip(74), m => Assert.Equal((80, 80), (m.SpriteWidth, m.SpriteHeight)));
        Assert.All(P2.Monsters, m =>
        {
            Assert.Contains(m.SpriteWidth, new[] { 32, 48, 64, 80 });
            Assert.InRange(m.SpriteHeight, 21, 80);
        });
    }

    [OriginalDataTheory]
    // Live UTILIT2 runs (transfer-live.md): FIGHTER STR 5 DEX 3 → 507, PRIEST STR 3 INT 4 → 659, THIEF sum 15 → 263.
    [InlineData(CharacterClass.Fighter, 5, 1, 3, 1, 1, 1, 507)]
    [InlineData(CharacterClass.Priest, 3, 4, 1, 1, 1, 1, 659)]
    [InlineData(CharacterClass.Thief, 5, 1, 5, 1, 1, 1, 263)]
    public void Phantasie2_LevelZeroTarget_IsTheLiveTransferMinimum(
        CharacterClass cls, int str, int intel, int dex, int con, int cha, int luck, long expected)
    {
        var stats = new CharacterStats(str, intel, dex, con, cha, luck);
        Assert.Equal(expected, P2.Rules.Training!.XpRequired(cls, stats, level: 0, experience: 1000));
        var c = new Character { Class = cls, Stats = stats };
        Assert.Equal(TransferIToII.LevelZeroMinimum(c), P2.Rules.Training.XpRequired(cls, stats, 0, 1000));
    }

    [OriginalDataFact]
    public void Phantasie2_ZennoTrains_AtXp1024_LevelZero()
    {
        // roster-live.md: ZENNO (FIGHTER STR 26 DEX 3, level 0, XP 1024) trained live. II's level-0 target is 119.
        var stats = new CharacterStats(26, 5, 3, 20, 8, 9);
        Assert.Equal(119, P2.Rules.Training!.XpRequired(CharacterClass.Fighter, stats, 0, 1024));
        // Under I's rules a level-0 character with XP > 0 uses F = 1.8^0 = 1 too, but I's FIGHTER sum differs.
        Assert.NotEqual(119, TrainingRules.PhantasieI.XpRequired(CharacterClass.Fighter, stats, 0, 1024));
    }

    [OriginalDataFact]
    public void Phantasie1_TrainingProfile_MatchesTheDosChain()
    {
        Assert.Equal(TrainingRules.PhantasieI, P1.Rules.Training);
        var samples = new[]
        {
            new CharacterStats(17, 15, 16, 14, 12, 11), new CharacterStats(8, 18, 9, 10, 6, 13),
            new CharacterStats(3, 3, 3, 3, 3, 8), new CharacterStats(20, 20, 20, 20, 20, 13),
        };
        foreach (var cls in Enum.GetValues<CharacterClass>())
        foreach (var s in samples)
        foreach (var level in new[] { 1, 2, 5, 9, 14 })
        foreach (var xp in new long[] { 0, 1, 50_000 })
        {
            var dos = TownMenus.TrainingTarget((int)cls, s, level, xp);
            var generic = TrainingRules.PhantasieI.Target(cls, s, level, xp);
            Assert.True(Math.Abs(dos - generic) <= 1e-5 * Math.Abs(dos) + 1e-3, $"{cls} L{level} XP{xp}: {dos} vs {generic}");
        }
    }

    [Fact]
    public void Manifest_RejectsAMissingCount()
    {
        var dir = Path.Combine(Path.GetTempPath(), "p2-manifest-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var path = Path.Combine(dir, GameManifest.FileName);
            File.WriteAllText(path, """{ "game": "p2", "platform": "atari-st", "counts": { "monsters": 80 } }""");
            var ex = Assert.Throws<InvalidDataException>(() => GameManifest.Load(path));
            Assert.Contains("counts.items", ex.Message);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
