using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Core.Tests;

public class MonsterDataRoundTripTests
{
    [OriginalDataFact]
    public void LoadPhantasie1_Monsters_RoundTripFromPhase3Json()
    {
        var data = GameDataRepository.LoadPhantasie1();

        Assert.Equal(GameDataRepository.ExpectedMonsterCount, data.Monsters.Count);

        // Unpacked PHANTASI.COM DS:0x1a6c (s22) — equals the live DOSBox capture.
        var skeleton = data.GetMonster(0);
        Assert.Equal("SKELETON", skeleton.Name);
        Assert.Equal(2, skeleton.Armor);
        Assert.Equal(1, skeleton.CombatRating);
        Assert.Equal(-75, skeleton.Defense);
        Assert.Equal(50, skeleton.AttackPower);
        Assert.Equal(3, skeleton.HpBase);
        Assert.Equal(77, skeleton.Experience);
        Assert.Equal(new[] { 58, 59 }, skeleton.TreasureItems);

        var viper = data.GetMonster(6);
        Assert.Equal("VIPER", viper.Name);
        Assert.Equal(12, viper.CombatRating);
        Assert.Equal(20, viper.HpBase);
        Assert.Equal(14, viper.Damage);

        var zeus = data.GetMonster(74);
        Assert.Equal("ZEUS", zeus.Name);
        Assert.Equal(999, zeus.Defense);
        Assert.Equal(666, zeus.HpBase);
        Assert.Equal(36, zeus.Level);

        // Phase 4 asset-manifest cross-ref lands on the definition.
        Assert.False(string.IsNullOrWhiteSpace(viper.SpriteIdlePath));
        Assert.Contains("viper", viper.SpriteIdlePath!, StringComparison.OrdinalIgnoreCase);
    }

    [OriginalDataFact]
    public void MonsterDefinition_ToCombatant_IsUsableFromCore()
    {
        var data = GameDataRepository.LoadPhantasie1();
        Monster combatant = data.GetMonster(6).ToCombatant(new SeededRandomSource(7));

        Assert.Equal(6, combatant.Id);
        Assert.Equal("VIPER", combatant.Name);
        Assert.InRange(combatant.HitPoints!.Value, 10, 30);
        Assert.Equal(14, combatant.DamageField);

        // Party + resolver seams exist for Phase 6 combat work.
        var party = new Party();
        party.Members.Add(new Character
        {
            Name = "wizardo",
            Class = CharacterClass.Wizard,
            Race = Race.Elf,
            Stats = new CharacterStats(3, 17, 5, 14, 6, 9),
            HpCur = 4,
            HpMax = 4,
        });

        Assert.Single(party.Members);
        Assert.IsAssignableFrom<ICombatResolver>(new CombatResolver());

        var a = new SeededRandomSource(12345);
        var b = new SeededRandomSource(12345);
        Assert.Equal(a.Next(0, 1000), b.Next(0, 1000));
    }

    [OriginalDataFact]
    public void Spells_HaveConfirmedMpCosts()
    {
        var data = GameDataRepository.LoadPhantasie1();

        Assert.Equal(GameDataRepository.ExpectedSpellCount, data.Spells.Count);
        Assert.All(data.Spells, s =>
            Assert.Equal(GameDataRepository.SpellMagicPointCost(s.Id), s.MagicPointCost));

        Assert.Equal(1, data.GetSpell(0).MagicPointCost); // HEALING I
        Assert.Equal(4, data.GetSpell(3).MagicPointCost); // HEALING IV
        Assert.Equal(1, data.GetSpell(40).MagicPointCost); // CHARM
        Assert.Equal(2, data.GetSpell(54).MagicPointCost); // DIVINE I = DOS slot 57 (s41)
        Assert.Equal(57, data.GetSpell(54).DosSlot);
        Assert.All(data.Spells, s => Assert.Equal(SpellId.DosSlot(s.Id), s.DosSlot));
        Assert.All(data.Spells, s => Assert.DoesNotContain("[?]", s.EffectCoefficientsNote ?? ""));
        Assert.Equal("HEALING I", data.Spells[0].Name);
        Assert.Equal("E", data.Spells[0].Category);
    }

    [OriginalDataFact]
    public void Dungeons_ExposeDecoded33x38Grid()
    {
        var data = GameDataRepository.LoadPhantasie1();

        Assert.Equal(GameDataRepository.ExpectedDungeonLevelCount, data.Dungeons.Count);
        var dng1 = Assert.Single(data.Dungeons, d => d.Id == "DNG1");
        Assert.NotNull(dng1.Cells);
        Assert.Equal(33, dng1.Cells!.Width);
        Assert.Equal(38, dng1.Cells.Height);
        Assert.Equal(0xd7, dng1.Cells[20, 0].FeatureId);
        Assert.Equal(GameDataRepository.DungeonGridByteLength, dng1.GridByteLength);
        Assert.Equal(GameDataRepository.DungeonGridByteLength, dng1.GetGridBytes().Length);
        Assert.True(dng1.RawFeatureBytes.Length >= dng1.GridByteLength);
        Assert.Contains(dng1.EmbeddedTexts, t => t.Text.Contains("PURPLE GAS", StringComparison.Ordinal));

        Assert.Equal(282, CharacterSchemaNotes.GuildRecordSize);
        Assert.Equal(22, CharacterSchemaNotes.GuildSlotCount);
    }
}
