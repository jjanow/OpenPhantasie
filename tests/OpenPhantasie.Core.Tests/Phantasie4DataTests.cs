using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Core.Tests;

/// <summary>
/// Phase 09 T6.3: <c>GameData/phantasie4/</c> (built by <c>tools/scripts/build_p4_gamedata.py</c> from the X68000 files)
/// loads through the game-neutral <see cref="GameTables"/>. Pinned values come from <c>FORMAT.md</c>, the IV logic specs,
/// the item catalogue and the manual.
/// </summary>
public class Phantasie4DataTests
{
    private static readonly GameTables P4 = GameTables.Load(GameId.P4);
    private static Phantasie4RuleTables R => P4.Phantasie4!;

    [OriginalDataFact]
    public void LoadsWithItsOwnCounts()
    {
        Assert.Equal(GameId.P4, P4.Game);
        Assert.Equal("x68000", P4.Manifest.Platform);
        Assert.Equal(84, P4.Monsters.Count);
        Assert.Equal(157, P4.Items.Count);
        Assert.Equal(65, P4.Spells.Count);
        Assert.Equal(10, P4.Names.Classes.Count);
        Assert.Equal(35, P4.Names.Races.Count);
        Assert.Equal(24, P4.OverworldRegions.Count);
        Assert.Equal(21, P4.Dungeons.Count);
        Assert.Empty(P4.MessageAreas);
        Assert.Equal(5, P4.Scrolls.Count);
        Assert.Equal(21, P4.Towns.Count);
        Assert.Equal(3, P4.Elementals.Count);
        Assert.All(P4.Monsters, m => Assert.Equal(GameId.P4, m.ContentId.Game));
        Assert.Equal(Enumerable.Range(1, 21), P4.Dungeons.Select(d => d.ContentId.Local).Order());
        Assert.NotNull(P4.Phantasie4);
        Assert.Null(P4.Phantasie3);
        Assert.Null(GameTables.Load(GameId.P3).Phantasie4);
        Assert.Equal("x68000", P4.Rules.OriginalPlatform);
        Assert.Equal((65000L, 65000L, 10), (P4.Rules.PartyGoldCap, P4.Rules.BankGoldCap, P4.Rules.GuildSlotCount));
    }

    [OriginalDataFact]
    public void FullRepository_LoadsThroughTheGenericPath()
    {
        var repo = GameDataRepository.Load(GameDataRepository.DefaultPathFor(GameId.P4));
        Assert.Equal(GameId.P4, repo.Game);
        Assert.Equal(157, repo.Towns[0].Stock.Count); // one stock slot per IV item
    }

    [OriginalDataFact]
    public void Monsters_DecodeTheDatmRecord()
    {
        // FORMAT.md 6.4 on record 0 (Skeleton).
        var s = P4.Monsters[0];
        Assert.Equal(("Skeleton", (int?)6, 50, 7, 2, -75, 1, 77L), (s.Name, s.GroupMax, s.AttackPower, s.Damage, s.Armor, s.Defense, s.Level, s.Experience));
        Assert.Equal((3, 20L), (s.HpBase, s.Gold));
        Assert.Equal([30, 32, 31], s.TreasureItems);
        Assert.Equal(90, s.NextGroupChance);
        Assert.Equal("Arathamus", P4.Monsters[83].Name);
        Assert.All(P4.Monsters, m => Assert.All(m.TreasureItems, i => Assert.InRange(i, 1, 156)));
    }

    [OriginalDataFact]
    public void Items_KindsRangesAndTheLongSword()
    {
        var kinds = P4.Items.GroupBy(i => i.Kind).ToDictionary(g => g.Key, g => g.Count());
        Assert.Equal(8, kinds["shield"]);
        Assert.Equal(19, kinds["armor"]);
        Assert.Equal(36, kinds["weapon"]);
        Assert.Equal(12, kinds["bow"]);
        Assert.Equal(9, kinds["thrown"]);
        Assert.Equal(10, kinds["healing_potion"]);
        var sword = P4.Items[40];
        Assert.Equal(("Long Sword", "weapon", 13, 51, "str2_plus_dex", 600),
            (sword.Name, sword.Kind, sword.Points, sword.MinStr, sword.StrCheck, sword.Value));
        // Manual p. 24 figure: Small Shield 7 / 10 g, Magic Shield 14 / 2000 g.
        Assert.Equal((7, 10), (P4.Items[2].Points, P4.Items[2].Value));
        Assert.Equal((14, 2000), (P4.Items[6].Points, P4.Items[6].Value));
        Assert.Equal(14, R.Tables.Towns.ItemPointsManual[40]); // D4-018
    }

    [OriginalDataFact]
    public void Spells_CarryTheManualCostsAndTheCode()
    {
        // D4-044: manual Slow 1 / 2 = 1 / 2, Arrow Flame 2 = 3, Party Arrow Flame 1 = 2, Dissolve = 1.
        Assert.Equal(("Slow 1", 1, 2), (P4.Spells[10].Name, P4.Spells[10].MagicPointCost, R.Tables.Combat.MpCostCode[10]));
        Assert.Equal((2, 3), (P4.Spells[11].MagicPointCost, R.Tables.Combat.MpCostCode[11]));
        Assert.Equal((3, 2), (P4.Spells[24].MagicPointCost, R.Tables.Combat.MpCostCode[24]));
        Assert.Equal((2, 3), (P4.Spells[25].MagicPointCost, R.Tables.Combat.MpCostCode[25]));
        Assert.Equal(("Dissolve", 1, 3), (P4.Spells[47].Name, P4.Spells[47].MagicPointCost, R.Tables.Combat.MpCostCode[47]));
        Assert.All(P4.Spells, s => Assert.InRange(s.MagicPointCost, 1, 4));
        Assert.Equal("E", P4.Spells[0].Category);
        Assert.Equal("N/T", P4.Spells[46].Category); // Transportation
    }

    [OriginalDataFact]
    public void Names_UseIVsOwnNumbering()
    {
        Assert.Equal(["Thief", "Monk", "Ranger", "Fighter", "Priest", "Wizard", "Shadow Warrior", "Gargoyle", "Sage", "Lord"],
            P4.Names.Classes.Select(c => c.Name));
        Assert.Equal(("Human", "Lizard Man", "Spriggan", "Half Orc", "?", "Duck"),
            (P4.Names.Races[0].Name, P4.Names.Races[13].Name, P4.Names.Races[17].Name, P4.Names.Races[30].Name,
                P4.Names.Races[33].Name, P4.Names.Races[34].Name));
        Assert.Equal(11, P4.Names.Skills.Count);
        Assert.Equal(["Head", "Torso", "Right Arm", "Left Arm", "Right Leg", "Left Leg"], P4.Names.BodyAreas.Select(b => b.Name));
        Assert.Equal("Super Hero", P4.Names.SocialClasses[4].Name);
    }

    [OriginalDataFact]
    public void Towns_PriceFactorsAndTypes()
    {
        var t = P4.Towns.ToDictionary(x => x.Id);
        Assert.Equal(("Pendragon", 80), (t[1].Name, t[1].PriceIndex));
        Assert.Equal(100, t[10].PriceIndex);
        Assert.Equal(120, t[13].PriceIndex);
        Assert.All(P4.Towns, x => Assert.All(x.Stock, s => Assert.Equal(0, s)));
        Assert.Equal([80, 80, 80, 80, 80, 80, 80, 80, 80, 80, 100, 100, 100, 120, 120, 120, 120, 120, 120, 120, 120, 120],
            R.Tables.Towns.PriceIndexByMap);
    }

    [OriginalDataFact]
    public void Overworld_MapsAndPlaces()
    {
        var map1 = P4.OverworldRegions.Single(r => r.RegionId == 1);
        Assert.Equal((26, 26, "map1"), (map1.GridWidth, map1.GridHeight, map1.Plane));
        Assert.Equal(140, map1.Tiles[12][17]); // a town tile: type 14, sub-index 0
        Assert.Contains(P4.Places, p => p is { Plane: "map1", Kind: "town", Number: 12, X: 17, Y: 12 });
        Assert.Contains(P4.Places, p => p is { Kind: "dungeon", Number: 21, Name: "Skull Fortress" });
        // Maps 6, 7, 11 and 12 share MOUT record 5 (all rapids).
        Assert.All(new[] { 6, 7, 11, 12 }, n =>
            Assert.All(P4.OverworldRegions.Single(r => r.RegionId == n).Tiles.SelectMany(x => x), v => Assert.Equal(1, v / 10)));
        Assert.Equal(21, P4.Places.Count(p => p.Kind == "town"));
    }

    [OriginalDataFact]
    public void Dungeons_FloorGridMessagesAndTriggers()
    {
        var d1 = P4.Dungeons.Single(d => d.ContentId.Local == 1);
        Assert.Equal((30, 30), (d1.GridWidth, d1.GridHeight));
        Assert.Equal(2, d1.Cells![0, 0].FeatureId); // floor type 2 on the border squares
        Assert.Equal("This chest is empty.", d1.Messages[14].Lines[0]);
        var raw = System.IO.File.ReadAllText(Path.Combine(GameDataRepository.DefaultPathFor(GameId.P4), "dungeons.json"));
        Assert.Contains("\"words_hex\"", raw);
    }

    [OriginalDataFact]
    public void Scrolls_AndTheirItems()
    {
        Assert.Equal("The Orc Scroll", P4.Scrolls[0].Lines[0].Trim());
        Assert.Equal([0], P4.ScrollItemRecords[140]);
        Assert.Equal([4], P4.ScrollItemRecords[144]);
    }

    [OriginalDataFact]
    public void Elementals_AreCharacterRecords()
    {
        Assert.Equal(["Mud El.", "Mist El.", "Air El."], P4.Elementals.Select(e => e.Name));
        Assert.Equal([250, 200, 150], P4.Elementals.Select(e => e.HpMax));
        Assert.Contains(4, P4.Elementals[2].KnownSpells); // Air knows Fire Flash 1
        var ch = P4.Elementals[0].ToCharacter();
        Assert.Equal((CharacterClass.Fighter, 20), (ch.Class, ch.Level));
    }

    [OriginalDataFact]
    public void CreationTables_FillTheSharedShape()
    {
        Assert.Equal(3, P4.CreationTables.HpFactorsByClass[(int)CharacterClass.Thief][0]);
        Assert.Equal(65, P4.CreationTables.SpellLevelsByClass[(int)CharacterClass.Priest].Count);
        Assert.Empty(P4.CreationTables.LifespanBandsByRace);
    }

    [OriginalDataFact]
    public void RuleTables_HallAndAncestry()
    {
        Assert.Equal(["Lord Wood", "Filmon", "Nikademus"], R.HallNames);
        Assert.Equal([18, 17, 16, 16, 17], R.Hall[0].Abilities);
        Assert.Equal((33, 9, 6), (R.Hall[2].Race, R.Hall[2].Class, R.Hall[2].Social));
        var c = R.Tables.Creation;
        Assert.Equal(30, P4Creation.Combine(c, 0, 11)); // Human + Orc = Half Orc
        Assert.Equal(19, P4Creation.Combine(c, 2, 0));  // Elf + Human = Half Elf
        Assert.Null(P4Creation.Combine(c, 2, 3));       // Elf + Halfling
        Assert.Equal(34, c.RaceAbilityMod.Count);
        Assert.Equal(8, c.ClassAbilityMod.Count);
        Assert.Equal(27, c.InheritanceExponent.Count);
    }

    [OriginalDataFact]
    public void Strings_EnglishAndJapanese()
    {
        var dir = GameDataRepository.DefaultPathFor(GameId.P4);
        var en = StringTable.Load(dir);
        var ja = StringTable.Load(dir, "ja");
        Assert.Equal("This chest is empty.", en.Get("o01.14"));
        Assert.Equal("このチェストは空っぽだ。", ja.Get("o01.14"));
        Assert.Equal("Pendragon", en.Get("p4x.0x3e12c"));
        Assert.Equal(en.Strings.Count, ja.Strings.Count);
    }

    [OriginalDataFact]
    public void ThrownSlot_IsAWornItem()
    {
        Assert.Equal(["shield", "armor", "weapon", "bow", "thrown"], P4.Rules.Equipment.Slots.Select(s => s.Id));
        var ch = new Character();
        WornItems.Set(ch, WornItems.Thrown, 79);
        Assert.Equal(79, WornItems.Get(ch, WornItems.Thrown));
        Assert.Equal(79, ch.Clone().WornThrown);
        Assert.Equal("thrown", P4.Rules.Equipment.SlotFor(79)!.Id);
    }
}
