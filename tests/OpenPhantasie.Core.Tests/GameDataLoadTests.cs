using OpenPhantasie.Data;

namespace OpenPhantasie.Core.Tests;

/// <summary>
/// M1: every extracted Phase 3/4 table loads and validates.
/// </summary>
public class GameDataLoadTests
{
    [OriginalDataFact]
    public void LoadPhantasie1_LoadsAllExtractedTables()
    {
        var data = GameDataRepository.LoadPhantasie1();

        Assert.Equal(GameDataRepository.ExpectedMonsterCount, data.Monsters.Count);
        Assert.Equal(GameDataRepository.ExpectedItemCount, data.Items.Count);
        Assert.Equal(GameDataRepository.ExpectedSpellCount, data.Spells.Count);
        Assert.Equal(GameDataRepository.ExpectedDungeonLevelCount, data.Dungeons.Count);
        Assert.Equal(GameDataRepository.ExpectedOverworldRegionCount, data.OverworldRegions.Count);
        Assert.Equal(GameDataRepository.ExpectedMessageAreaCount, data.MessageAreas.Count);
        Assert.Equal(GameDataRepository.ExpectedScrollCount, data.Scrolls.Count);
        Assert.Equal(GameDataRepository.ExpectedClassCount, data.Names.Classes.Count);
        Assert.Equal(GameDataRepository.ExpectedRaceCount, data.Names.Races.Count);
        Assert.Equal(15, data.Names.TownLabels.Count);
        Assert.Equal(GameDataRepository.ExpectedTownCount, data.Towns.Count);
        Assert.Equal("PELNOR", data.GetTown(1).Name);
        Assert.NotEmpty(data.GuildRoster.Characters);
        Assert.Equal(CharacterSchemaNotes.GuildRecordSize, data.GuildRoster.RecordSize);
        Assert.Equal(CharacterSchemaNotes.GuildSlotCount, data.GuildRoster.SlotCount);
    }

    [OriginalDataFact]
    public void Overworld_RegionsAre26x20()
    {
        var data = GameDataRepository.LoadPhantasie1();
        var out1 = Assert.Single(data.OverworldRegions, r => r.Id == "OUT1.DAT");
        Assert.Equal(1, out1.RegionId);
        Assert.Equal(GameDataRepository.OverworldGridWidth, out1.GridWidth);
        Assert.Equal(GameDataRepository.OverworldGridHeight, out1.GridHeight);
        Assert.Equal(20, out1.Tiles.Count);
        Assert.All(out1.Tiles, row => Assert.Equal(26, row.Count));
        Assert.NotEmpty(out1.Features);
        Assert.Contains(out1.TextPages, t => t.Text.Contains("RED PONY", StringComparison.OrdinalIgnoreCase));
    }

    [OriginalDataFact]
    public void MessagesAndScrolls_HaveDecryptedText()
    {
        var data = GameDataRepository.LoadPhantasie1();

        Assert.All(data.MessageAreas, a => Assert.NotEmpty(a.Records));
        Assert.Contains(data.Scrolls, s => s.Text.Contains("Gelnor", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(1, data.Scrolls[0].Id);
    }

    [OriginalDataFact]
    public void GuildRoster_IncludesWizardoFixture()
    {
        var data = GameDataRepository.LoadPhantasie1();
        var wiz = Assert.Single(data.GuildRoster.Characters, c => c.Name == "wizardo");
        Assert.Equal("ELF", wiz.Race);
        Assert.Equal("WIZARD", wiz.Class);
        Assert.Equal(7, wiz.MagicCur);
        Assert.Equal(7, wiz.MagicMax);
        Assert.NotNull(wiz.Skills);
    }

    [OriginalDataFact]
    public void GuildRoster_SeedsS11FieldsIntoCharacters()
    {
        var data = GameDataRepository.LoadPhantasie1();
        var snap = Assert.Single(data.GuildRoster.Characters, c => c.Name == "priesto");
        var priest = GameSessionFactory.SnapshotToCharacter(snap, data.CreationTables);
        Assert.Equal(10, priest.MpCeiling);
        Assert.Equal(CharacterCreation.LifespanFor(Race.Human, 12, data.CreationTables), priest.Lifespan); // D-003 band, not DOS @252 = 12
        Assert.Equal(574, priest.AgeClock);
        Assert.Equal(new[] { 36, 52, 56, 56, 52, 56, 0, 52, 0, 64 }, priest.SkillBonuses);
    }

    [OriginalDataFact]
    public void Names_CrossCheckSpellAndMonsterTables()
    {
        var data = GameDataRepository.LoadPhantasie1();
        Assert.Equal("WIZARD", data.Names.Classes[5].Name);
        Assert.Equal("MINOTAUR", data.Names.Races[14].Name);
        Assert.Equal(data.Spells[0].Name, data.Names.Spells[0].Name);
        Assert.Equal(data.Monsters[0].Name, data.Names.Monsters[0].Name);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(3, 4)]
    [InlineData(4, 1)]
    [InlineData(40, 1)]
    [InlineData(54, 2)] // DIVINE I = DOS slot 57 (s41)
    public void SpellMagicPointCost_MatchesOv2Formula(int index, int expected) =>
        Assert.Equal(expected, GameDataRepository.SpellMagicPointCost(index));
}
