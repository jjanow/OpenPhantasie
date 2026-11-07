using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Core.Tests;

public class CharacterCreationTests
{
    private static CharacterCreationTables Tables =>
        GameDataRepository.LoadPhantasie1().CreationTables;

    // Manual "Maximum Value for Attributes by Race" (STR INT DEX CON CHA). The roll
    // caps at min(18, …) + raceMod - 10, so the maximum is raceMod + 8. Halfling DEX
    // follows the manual (20), not the DOS binary (19) — DIFFERENCES.md D-001.
    [OriginalDataTheory]
    [InlineData(Race.Human, 18, 18, 18, 18, 18)]
    [InlineData(Race.Dwarf, 20, 17, 17, 19, 17)]
    [InlineData(Race.Elf, 17, 19, 19, 17, 18)]
    [InlineData(Race.Halfling, 16, 18, 20, 18, 17)]
    [InlineData(Race.Gnome, 19, 17, 18, 19, 17)]
    [InlineData(Race.Pixie, 16, 18, 21, 16, 17)]
    [InlineData(Race.Sprite, 16, 18, 22, 16, 17)]
    [InlineData(Race.Ogre, 21, 14, 16, 19, 13)]
    [InlineData(Race.Gnoll, 20, 13, 17, 21, 11)]
    [InlineData(Race.Troll, 22, 13, 15, 20, 12)]
    [InlineData(Race.Kobold, 17, 15, 19, 19, 13)]
    [InlineData(Race.Orc, 19, 16, 17, 18, 14)]
    [InlineData(Race.Goblin, 17, 14, 18, 17, 11)]
    [InlineData(Race.LizardMan, 19, 14, 17, 18, 13)]
    [InlineData(Race.Minotaur, 20, 14, 16, 19, 12)]
    public void RaceMaximums_MatchManualTable(Race race, int str, int intel, int dex, int con, int cha)
    {
        var mods = Tables.RaceAttributeModifiers[(int)race];
        Assert.Equal(new[] { str, intel, dex, con, cha }, mods.Select(m => m + 8).ToArray());
    }

    [OriginalDataFact]
    public void Skills_MatchGuildFixtureCharacters()
    {
        var tables = Tables;
        var data = GameDataRepository.LoadPhantasie1();

        foreach (var snap in data.GuildRoster.Characters)
        {
            var race = ParseRace(snap.Race);
            var cls = ParseClass(snap.Class);
            var stats = new CharacterStats(
                snap.Strength, snap.Intelligence, snap.Dexterity,
                snap.Constitution, snap.Charisma, snap.Luck);
            var skills = CharacterCreation.ComputeSkills(cls, stats, tables);

            Assert.Equal(snap.Skills!.Attack, skills.Attack);
            Assert.Equal(snap.Skills.Parry, skills.Parry);
            Assert.Equal(snap.Skills.FindItem, skills.FindItem);
            Assert.Equal(snap.Skills.SpotTrap, skills.SpotTrap);
            Assert.Equal(snap.Skills.DisarmTrap, skills.DisarmTrap);
            Assert.Equal(snap.Skills.Listen, skills.Listen);
            Assert.Equal(snap.Skills.PickLock, skills.PickLock);
            Assert.Equal(snap.Skills.Swim, skills.Swim);
        }
    }

    [OriginalDataTheory]
    [InlineData(CharacterClass.Thief, 4)]
    [InlineData(CharacterClass.Monk, 7)]
    [InlineData(CharacterClass.Ranger, 6)]
    [InlineData(CharacterClass.Fighter, 5)]
    [InlineData(CharacterClass.Priest, 7)]
    [InlineData(CharacterClass.Wizard, 7)]
    public void StartingMagic_MatchesMpCeilingFormula(CharacterClass cls, int expectedMagic)
    {
        var tables = Tables;
        var cr = tables.MpCeilingByClass[(int)cls][0];
        Assert.Equal(expectedMagic, (3 * cr) >> 2);
    }

    [OriginalDataFact]
    public void Create_Wizard_GetsFireflashAndStartingKit()
    {
        var ch = CharacterCreation.Create(
            new CharacterCreationRequest
            {
                Name = "wizardo",
                Race = Race.Elf,
                Class = CharacterClass.Wizard,
            },
            Tables,
            new SeededDosRandomSource(42));

        Assert.Equal(1, ch.Level);
        Assert.Equal(RulesProfile.PhantasieI.StartingGold, ch.GoldInBank);
        Assert.Equal(1, ch.SpellsKnown[4]); // FIREFLASH I
        Assert.Equal(0, ch.SpellsKnown[0]);
        Assert.Equal(CharacterCreation.StartingShieldId, ch.WornShield);
        Assert.Equal(CharacterCreation.StartingArmorId, ch.WornArmor);
        Assert.Equal(CharacterCreation.StartingWeaponId, ch.WornWeapon);
        Assert.InRange(ch.Stats.Luck, 8, 13);
        Assert.InRange(ch.Stats.Str, 3, 18);
        Assert.True(ch.HpMax >= 1);
        Assert.Equal(ch.HpMax, ch.HpCur);
        Assert.Equal(7, ch.MagicMax);
    }

    [OriginalDataFact]
    public void Create_Priest_GetsHealingI()
    {
        var ch = CharacterCreation.Create(
            new CharacterCreationRequest
            {
                Name = "priesto",
                Race = Race.Human,
                Class = CharacterClass.Priest,
            },
            Tables,
            new SeededDosRandomSource(1));

        Assert.Equal(1, ch.SpellsKnown[0]);
        Assert.Equal(0, ch.SpellsKnown[4]);
    }

    [OriginalDataFact]
    public void Create_IsDeterministicWithSameSeed()
    {
        var req = new CharacterCreationRequest
        {
            Name = "same",
            Race = Race.Dwarf,
            Class = CharacterClass.Fighter,
        };
        var a = CharacterCreation.Create(req, Tables, new SeededDosRandomSource(99));
        var b = CharacterCreation.Create(req, Tables, new SeededDosRandomSource(99));
        Assert.Equal(a.Stats, b.Stats);
        Assert.Equal(a.HpMax, b.HpMax);
        Assert.Equal(a.Skills, b.Skills);
    }

    [Fact]
    public void NameUniqueness_IsCaseSensitive_LikeDos()
    {
        var roster = new[]
        {
            new Character { Name = "Wizardo" },
        };
        Assert.False(CharacterCreation.IsNameUnique("Wizardo", roster));
        Assert.True(CharacterCreation.IsNameUnique("wizardo", roster)); // [D✓] s65: DOS keeps "PRIESTO" beside "priesto"
        Assert.True(CharacterCreation.IsNameUnique("newbie", roster));
    }

    [Fact]
    public void RandomRace_AvoidsDuplicateStrongRace()
    {
        // Seed chosen so first roll is a monster race that already exists → fallback.
        var existing = new[] { Race.Minotaur, Race.Ogre, Race.Troll, Race.Gnoll,
            Race.Kobold, Race.Orc, Race.Goblin, Race.LizardMan, Race.Pixie, Race.Sprite };
        // Brute: many seeds should only produce HUMAN..GNOME when all strong races present.
        for (var seed = 0; seed < 50; seed++)
        {
            var race = CharacterCreation.RollRandomRace(new SeededDosRandomSource(seed), existing);
            Assert.True((int)race <= (int)Race.Gnome, $"seed {seed} produced {race}");
        }
    }

    private static Race ParseRace(string? name) => name switch
    {
        "LIZ.MAN" => Race.LizardMan,
        _ => Enum.Parse<Race>(name!.Replace(".", ""), ignoreCase: true),
    };

    [OriginalDataFact]
    public void SpellLevels_MonkLevel7_IsManualRow_D006()
    {
        // Manual: Monk L7 = spells 13 and 48 (1-based). DOS adds 42 SLEEP; remaster drops it.
        var monk = Tables.SpellLevelsByClass[(int)CharacterClass.Monk];
        var l7 = Enumerable.Range(0, monk.Count).Where(s => monk[s] == 7).Select(s => s + 1);
        Assert.Equal(new[] { 13, 48 }, l7);
        Assert.Equal(0, monk[41]);
    }

    [OriginalDataFact]
    public void SpellLevels_Level1Cells_MatchFixtureLearnableFlags()
    {
        // DOS fixtures: level-1 spells are flagged 2 ("can learn") except the starting spell (1).
        var slots = GuildCodec.ReadBinaryFile(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "GUILD.pass4.dat"), Tables);
        foreach (var ch in slots.OfType<Character>().Where(c => c.Level == 1))
        {
            var row = Tables.SpellLevelsByClass[(int)ch.Class];
            for (var s = 0; s < CharacterCreation.SpellCount; s++)
                Assert.Equal(row[s] == 1, ch.SpellsKnown[s] != 0);
        }
    }

    [OriginalDataFact]
    public void SkillCoefficients_Have15LevelsPerClass()
    {
        Assert.All(Tables.SkillCoefficientsByClassLevel, rows =>
        {
            Assert.Equal(15, rows.Count);
            Assert.All(rows, r => Assert.Equal(10, r.Count));
        });
    }

    private static CharacterClass ParseClass(string? name) =>
        Enum.Parse<CharacterClass>(name!, ignoreCase: true);
}
