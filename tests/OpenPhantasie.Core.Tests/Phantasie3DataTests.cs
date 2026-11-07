using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Core.Tests;

/// <summary>
/// Phase 09 T4: <c>GameData/phantasie3/</c> (built by <c>tools/scripts/build_p3_gamedata.py</c> from the DOS extracts)
/// loads through the game-neutral <see cref="GameTables"/>. Pinned values come from <c>FORMAT.md</c>, the III logic specs
/// and the III manual.
/// </summary>
public partial class Phantasie3DataTests
{
    private static readonly GameTables P3 = GameTables.Load(GameId.P3);
    private static readonly GameTables P1 = GameTables.Load(GameId.P1);
    private static Phantasie3RuleTables T => P3.Phantasie3!;

    [OriginalDataFact]
    public void LoadsWithItsOwnCounts()
    {
        Assert.Equal(GameId.P3, P3.Game);
        Assert.Equal("dos", P3.Manifest.Platform);
        Assert.Equal(80, P3.Monsters.Count);
        Assert.Equal(181, P3.Items.Count);
        Assert.Equal(57, P3.Spells.Count);
        Assert.Equal(6, P3.Names.Classes.Count);
        Assert.Equal(18, P3.Names.Races.Count);
        Assert.Equal(4, P3.OverworldRegions.Count);
        Assert.Equal(11, P3.Dungeons.Count);
        Assert.Empty(P3.MessageAreas); // no MESS files: the texts are in the DNGS records
        Assert.Equal(19, P3.Scrolls.Count);
        Assert.Equal(17, P3.Towns.Count);
        Assert.Equal(3, P3.Elementals.Count);
        Assert.All(P3.Monsters, m => Assert.Equal(GameId.P3, m.ContentId.Game));
        Assert.All(P3.Dungeons, d => Assert.Equal(GameId.P3, d.ContentId.Game));
        Assert.Equal(Enumerable.Range(1, 11), P3.Dungeons.Select(d => d.ContentId.Local));
        Assert.NotNull(P3.Phantasie3);
        Assert.Null(P1.Phantasie3);
        Assert.Empty(P1.Places);
        Assert.Null(P1.TileTerrain);
    }

    [OriginalDataFact]
    public void Names_UseIIIsOwnNumbering()
    {
        Assert.Equal(["Fighter", "Thief", "Priest", "Ranger", "Monk", "Wizard"], P3.Names.Classes.Select(c => c.Name));
        Assert.Equal(("Random", "Elemental", "Undead"), (P3.Names.Races[5].Name, P3.Names.Races[16].Name, P3.Names.Races[17].Name));
        Assert.Equal(9, P3.Names.Skills.Count);
        Assert.Equal("Fire Bow", P3.Names.Skills[8].Name);
        Assert.Equal(["Peasant", "Labourer", "Craftsman", "Noble", "Darklord", "Superhero"], P3.Names.SocialClasses.Select(s => s.Name));
        Assert.Equal(["Gelnor", "Ferronrah", "Netherworld", "Scandor", "Light", "Darkness"], P3.Names.Origins.Select(s => s.Name));
        Assert.Equal(["Head", "Left Arm", "Right Arm", "Torso", "Left Leg", "Right Leg"], P3.Names.BodyAreas.Select(s => s.Name));
        Assert.Equal(["Okay", "Injured", "Broken", "Gone"], P3.Names.BodyStates.Select(s => s.Name));
        // Core's enums keep I's order; the map goes by name (inverse of the transfer map DS:0x4e).
        Assert.Equal("FIGHTER", T.CoreClass[0]);
        Assert.Equal(("GNOME", "HALFLING", "LIZARDMAN", "UNDEAD"), (T.CoreRace[3], T.CoreRace[4], T.CoreRace[9], T.CoreRace[17]));
        Assert.False(T.CoreRace.ContainsKey(5));
        Assert.False(T.CoreRace.ContainsKey(16));
    }

    [OriginalDataFact]
    public void Monsters_FromCreatureDat()
    {
        var skel = P3.Monsters[0];
        Assert.Equal(("Skeleton", 50, 7, 2, -75, 5), (skel.Name, skel.AttackPower, skel.Damage, skel.Armor, skel.Defense, skel.GroupMax));
        Assert.Equal((20L, 77L, 1), (skel.Gold, skel.Experience, skel.Byte33));
        Assert.Equal(("Nikademus", "Nikademus", "Lord Wood"), (P3.Monsters[77].Name, P3.Monsters[78].Name, P3.Monsters[79].Name));
        Assert.All(P3.Monsters, m => Assert.Equal(0, m.GroupSizeFactor)); // I's field is not filled with III's byte 14
        Assert.Equal(1, P3.Monsters[79].GroupMax);
    }

    [OriginalDataFact]
    public void Items_UseTheDosNames_D3005_AndTheManualPoints()
    {
        Assert.Equal(("Dagger +1", 2, 3), (P3.Items[81].Name, P3.Items[81].Points, P3.Items[81].MinStr));
        Assert.Equal(("Dagger +2", 3, 6), (P3.Items[82].Name, P3.Items[82].Points, P3.Items[82].MinStr));
        Assert.Equal(("Compound Bow +2", 12, 22), (P3.Items[52].Name, P3.Items[52].Points, P3.Items[52].MinStr));
        Assert.Equal("God Armour", P3.Items[40].Name);
        Assert.Contains(P3.Items, i => i.Name == "Halberd");
        Assert.Equal("Wand of Nikademus", P3.Items[180].Name);
        Assert.Equal(13006, P3.Items[180].Value);
        Assert.Equal(("shield", "str"), (P3.Items[4].Kind, P3.Items[4].StrCheck));
        Assert.Equal(("bow", "str_plus_dex"), (P3.Items[41].Kind, P3.Items[41].StrCheck));
        Assert.Equal(("weapon", "str2_plus_dex"), (P3.Items[64].Kind, P3.Items[64].StrCheck));
        Assert.All(P3.Items, i => Assert.Equal(0, i.ValueFactor));
        Assert.All(P3.Items, i => Assert.NotNull(i.Value));
        Assert.All(P1.Items, i => Assert.Null(i.Value));
    }

    [OriginalDataFact]
    public void Spells_ManualCostsAndTheEmptySlot53()
    {
        Assert.Equal(("Arrow Flame I", "C/P", 1), (P3.Spells[12].Name, P3.Spells[12].Category, P3.Spells[12].MagicPointCost));
        Assert.Equal(("P.Arrow Flame II", 4), (P3.Spells[15].Name, P3.Spells[15].MagicPointCost));
        Assert.Equal(("Transportation", "N/T", 2), (P3.Spells[53].Name, P3.Spells[53].Category, P3.Spells[53].MagicPointCost));
        Assert.Equal(("Accuracy", 3), (P3.Spells[54].Name, P3.Spells[54].MagicPointCost));
        Assert.Equal(("Party Accuracy", 4), (P3.Spells[55].Name, P3.Spells[55].MagicPointCost));
        Assert.Equal("", P3.Spells[52].Name);
        Assert.False(P3.Spells[52].MagicPointCostKnown);
        Assert.Equal("Divine Aid", P3.Spells[56].Name);
        Assert.False(P3.Spells[56].MagicPointCostKnown);
    }

    [OriginalDataFact]
    public void Towns_PriceIndexByTownNumber_AndInitarmsStock()
    {
        var pen = P3.Towns.Single(t => t.Id == 1);
        Assert.Equal(("Pendragon", 150, "scandor"), (pen.Name, pen.PriceIndex, pen.Plane));
        Assert.Equal(181, pen.Stock.Count);
        Assert.Equal((0, 5, 5, 1, 200, 200), (pen.Stock[0], pen.Stock[21], pen.Stock[22], pen.Stock[23], pen.Stock[126], pen.Stock[132]));
        Assert.Equal(10, pen.Stock.Count(s => s > 0));
        Assert.Equal(123, P3.Towns.Single(t => t.Id == 14).PriceIndex); // Sciattica (s82: the 18-entry table)
        Assert.Equal(["light", "dark", "netherworld"], P3.Towns.Where(t => t.Id >= 15).Select(t => t.Plane));
        Assert.Equal(["Light", "Dark", "Death"], P3.Towns.Where(t => t.Id >= 15).Select(t => t.Name));
        Assert.All(P3.Towns.Where(t => t.Id >= 15), t => Assert.All(t.Stock, s => Assert.Equal(0, s)));
    }

    [OriginalDataFact]
    public void World_PlaneMaps_Places_AndTerrain()
    {
        var byPlane = P3.OverworldRegions.ToDictionary(r => r.Plane!);
        Assert.Equal((50, 75), (byPlane["scandor"].GridWidth, byPlane["scandor"].GridHeight));
        Assert.Equal((9, 9), (byPlane["light"].GridWidth, byPlane["light"].GridHeight));
        Assert.Equal((31, 16), (byPlane["netherworld"].GridWidth, byPlane["netherworld"].GridHeight));
        Assert.Equal((7, 3), byPlane["netherworld"].Arrival);
        Assert.Equal([0, 1, 2, 4], P3.OverworldRegions.Select(r => r.RegionId).Order());
        Assert.All(byPlane["scandor"].Tiles[0], t => Assert.Equal(1, t)); // row 0 is Water

        Assert.Equal((17, 11, 8), (P3.Places.Count(p => p.Kind == "town"), P3.Places.Count(p => p.Kind == "dungeon"),
            P3.Places.Count(p => p.Kind == "inn")));
        var pendragon = P3.Places.Single(p => p.Kind == "town" && p.Number == 1);
        Assert.Equal(("scandor", 24, 37), (pendragon.Plane, pendragon.X, pendragon.Y));
        var archives = P3.Places.Single(p => p.Kind == "dungeon" && p.Number == 5);
        Assert.Equal((25, 39, "The Pendragon town archives"), (archives.X, archives.Y, archives.Name));
        var fortress = P3.Places.Single(p => p.Kind == "dungeon" && p.Number == 9);
        Assert.Equal(("netherworld", 6, 10), (fortress.Plane, fortress.X, fortress.Y));
        Assert.Equal(57, byPlane["netherworld"].Tiles[3][7]); // town 17 "Death"

        var terrain = P3.TileTerrain!;
        Assert.Equal("Water", terrain.Names[terrain.TerrainOf(0, "scandor")]);
        Assert.Equal("A Bridge", terrain.Names[terrain.TerrainOf(222, "scandor")]);
        Assert.Equal("Forest", terrain.Names[terrain.TerrainOf(230, "scandor")]);
        Assert.Equal("Lava", terrain.Names[terrain.TerrainOf(100, "netherworld")]);
        Assert.Equal("Mountains", terrain.Names[terrain.TerrainOf(100, "scandor")]);
        Assert.True(terrain.IsBlocked(249));
        Assert.False(terrain.IsBlocked(241));
    }

    [OriginalDataFact]
    public void CreationTables_ManualTables_AndDosSpellLevels()
    {
        var c = P3.CreationTables;
        // Maximum Magic Points (III PDF 10 / pr.14) = DS:0xafb: Wizard 10 .. 20, Thief 6 .. 15.
        Assert.Equal([10, 11, 11, 12, 13, 14, 14, 15, 16, 16, 17, 17, 18, 19, 20], c.MpCeilingByClass[(int)CharacterClass.Wizard]);
        Assert.Equal(6, c.MpCeilingByClass[(int)CharacterClass.Thief][0]);
        // Spells Available (III PDF 10 / pr.14) = DS:0x1208: index = spell number - 1.
        int Lv(CharacterClass k, int spell) => c.SpellLevelsByClass[(int)k][spell - 1];
        Assert.Equal((4, 10, 9, 15), (Lv(CharacterClass.Fighter, 55), Lv(CharacterClass.Fighter, 56),
            Lv(CharacterClass.Fighter, 13), Lv(CharacterClass.Fighter, 15)));
        Assert.Equal((2, 7, 0), (Lv(CharacterClass.Wizard, 55), Lv(CharacterClass.Wizard, 56), Lv(CharacterClass.Wizard, 53)));
        Assert.Equal((1, 1, 3, 9), (Lv(CharacterClass.Priest, 1), Lv(CharacterClass.Priest, 51), Lv(CharacterClass.Priest, 49),
            Lv(CharacterClass.Priest, 44)));
        Assert.Equal((4, 7, 10, 13), (Lv(CharacterClass.Thief, 41), Lv(CharacterClass.Thief, 42), Lv(CharacterClass.Thief, 18),
            Lv(CharacterClass.Thief, 8)));
        Assert.Equal((1, 3, 5), (Lv(CharacterClass.Monk, 55), Lv(CharacterClass.Monk, 54), Lv(CharacterClass.Monk, 56)));
        // HP dice = I's (and twice them = the manual's CON-10 chart).
        Assert.Equal(P1.CreationTables.HpFactorsByClass, c.HpFactorsByClass);
        // Attribute tables: the III manual's = I's manual's.
        Assert.Equal(P1.CreationTables.RaceAttributeModifiers, c.RaceAttributeModifiers);
        Assert.Equal(P1.CreationTables.ClassAttributeModifiers, c.ClassAttributeModifiers);
        Assert.Equal(P1.CreationTables.LifespanBandsByRace, c.LifespanBandsByRace); // D3-041: I's bands (manual text = I's)
        // Skill tables: I's slot layout; Fire Bow's slot 6 has no class table.
        Assert.Equal(100, c.SkillCoefficientsByClassLevel[(int)CharacterClass.Fighter][0][0]);
        Assert.All(c.SkillCoefficientsByClassLevel.SelectMany(k => k), row => Assert.Equal(0, row[6]));
        Assert.Equal(35, c.SkillCoefficientsByClassLevel[(int)CharacterClass.Thief][2][7]); // III's Pick Lock L3 (I: 55)
    }

    [OriginalDataFact]
    public void RuleTables_TrainingBodyKitAndLimits()
    {
        Assert.Equal([5, 10, 16, 22, 28, 35, 41, 47, 53, 60, 65, 70, 75, 80, 85], T.HpDice["Fighter"].Take(15));
        Assert.Equal(85, T.HpDice["Fighter"][21]);
        Assert.Equal((180L, 22946825L), (T.TrainingXpCurve[1], T.TrainingXpCurve[21]));
        Assert.Equal([1, 5, 11, 21, 34, 52, 72, 97, 126, 158, 195, 237, 282, 332, 387], T.TrainingGoldCurve.Skip(1).Take(15));
        Assert.Equal(21, T.MaxLevel);
        Assert.Equal(255, T.SkillTables["attack"]["Fighter"][14]);
        Assert.Equal(25, T.SkillTables["attack"]["Ranger"][14]); // read as written (training.md section 5)
        Assert.Equal(["attack", "parry", "find_item", "find_trap", "disarm_trap", "listen", "fire_bow", "pick_lock", "swim"],
            T.Skills.Select(s => s.Id));
        Assert.Equal((10, 2, 4, 1), T.FireBowSeed);
        Assert.Equal((30, 15), (T.FireBowClassBonus["Thief"], T.FireBowRaceBonus[2]));

        Assert.Equal(["head", "left_arm", "right_arm", "torso", "left_leg", "right_leg"], T.BodyAreas);
        Assert.Equal([69, 80, 84, 91, 91, 100], T.HitLocationRows[0].Cumulative);
        Assert.Equal([3, 11, 35, 49, 80, 100], T.HitLocationRows[2].Cumulative);
        Assert.Equal([0, 3, 5, 2, 4, 1], T.CharacterAreaForColumn);

        Assert.Equal((4, 22, 64), T.Kit);
        Assert.Equal((44, 41), (T.KitBowFor("Thief"), T.KitBowFor("Wizard")));
        Assert.Equal((1, 5, 0), (T.FirstSpell["Priest"], T.FirstSpell["Monk"], T.FirstSpell["Fighter"]));
        Assert.Equal([30, 60, 90], T.SocialClassThresholds);
        Assert.Equal(256, T.SocialClassGoldPerClass);
        Assert.Equal([0, 4, 8, 12, 17, 20, 25, 30], T.ScoreWeights);
        Assert.Equal([77, 78, 79], T.FinalBossIds);
        Assert.Equal((90, 60), T.FoundItemLimit);
        Assert.Equal((20, 17), T.DosRoster);
    }

    [OriginalDataFact]
    public void Rules_BowSlot_CapsAndGuild()
    {
        var r = P3.Rules;
        Assert.Equal(GameId.P3, r.Game);
        Assert.Equal(["shield", "armor", "bow", "weapon"], r.Equipment.Slots.Select(s => s.Id));
        Assert.Equal("bow", r.Equipment.SlotFor(52)!.Id);
        Assert.Equal("weapon", r.Equipment.SlotFor(61)!.Id);
        Assert.Equal((65000L, 65000L), (r.PartyGoldCap, r.BankGoldCap)); // D3-026 manual
        Assert.Equal(new PlatformGuildSize(17, 17), r.GuildByPlatform["dos"]);
        Assert.Equal(0, r.StartingGold); // social class * 256 comes from rule-tables.json
        Assert.Null(r.Training);
    }

    [OriginalDataFact]
    public void Elementals_AirMistMud_AsCharacters()
    {
        Assert.Equal(["Air", "Mist", "Mud"], P3.Elementals.Select(e => e.Name));
        var air = P3.Elementals[0].ToCharacter();
        Assert.Equal((CharacterClass.Fighter, 15, 150, 30, 150, 32), (air.Class, air.Level, air.HpMax, air.MagicMax, air.Skills.Attack, air.Skills.Parry));
        Assert.Equal(new CharacterStats(25, 25, 25, 25, 25, 25), air.Stats);
        Assert.Equal((4, 22, 64, 41), (air.WornShield, air.WornArmor, air.WornWeapon, air.WornBow));
        Assert.Equal(1, air.SpellsKnown[54]); // #55 Accuracy
        Assert.Equal(1, air.SpellsKnown[55]); // #56 Party Accuracy
        Assert.Equal(31, air.SpellsKnown.Count(v => v == 1));
        var mist = P3.Elementals[1].ToCharacter();
        Assert.Equal(Enumerable.Range(24, 8), Enumerable.Range(0, mist.SpellsKnown.Length).Where(i => mist.SpellsKnown[i] == 1));
        Assert.DoesNotContain(1, P3.Elementals[2].ToCharacter().SpellsKnown);
    }

    [Fact]
    public void WornBow_IsASlot_CopiedAndSaved()
    {
        var ch = new Character { Name = "ARCHER" };
        WornItems.Set(ch, WornItems.Bow, 44);
        Assert.Equal(44, WornItems.Get(ch, "bow"));
        Assert.Equal(44, ch.Clone().WornBow);
        var back = GuildCodec.ReadJson(GuildCodec.WriteJson([ch], GameId.P3), GameId.P3).Single();
        Assert.Equal(44, back.WornBow);
        // An empty bow slot writes nothing, so I / II saves are unchanged.
        Assert.DoesNotContain("WornBow", GuildCodec.WriteJson([new Character { Name = "X" }]), StringComparison.OrdinalIgnoreCase);
    }
}
