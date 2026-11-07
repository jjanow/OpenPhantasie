using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Core.Tests;

/// <summary>
/// P1.3 golden check: the <c>creation-tables.json</c> tables Core reads equal the bytes of the original
/// <c>TWNS.INT</c> (offsets in <c>character-creation.md</c> / <c>data-tables.md</c>), except the documented
/// manual decisions D-001 (Halfling DEX) and D-006 (Monk SLEEP).
/// </summary>
public class CreationTablesGoldenTests
{
    private static readonly byte[] Twns = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "master", "TWNS.INT"));
    private static readonly CharacterCreationTables Tables = GameDataRepository.LoadPhantasie1().CreationTables;

    private const int Classes = 6;
    private const int Races = 15;
    private const int Levels = 15;

    [OriginalDataFact]
    public void HpFactors_AreTwns0x000()
    {
        for (var c = 0; c < Classes; c++)
            for (var l = 0; l < Levels; l++)
                Assert.Equal(Twns[c * Levels + l], Tables.HpFactorsByClass[c][l]);
    }

    [OriginalDataFact]
    public void MpCeiling_IsTwns0x1c2()
    {
        for (var c = 0; c < Classes; c++)
            for (var l = 0; l < Levels; l++)
                Assert.Equal(Twns[0x1c2 + c * Levels + l], Tables.MpCeilingByClass[c][l]);
    }

    [OriginalDataFact]
    public void SkillCoefficients_AreTwns0x21c()
    {
        for (var c = 0; c < Classes; c++)
            for (var l = 0; l < Levels; l++)
                for (var s = 0; s < 10; s++)
                {
                    var at = 0x21c + c * 0x12c + l * 0x14 + s * 2;
                    Assert.Equal(Twns[at] | Twns[at + 1] << 8, Tables.SkillCoefficientsByClassLevel[c][l][s]);
                }
    }

    [OriginalDataFact]
    public void SpellLevels_AreTwns0x05a_ExceptMonkSleep_D006()
    {
        for (var c = 0; c < Classes; c++)
            for (var s = 0; s < Tables.SpellLevelsByClass[c].Count; s++)
            {
                var dos = Twns[0x5a + c * 60 + s];
                if (c == (int)CharacterClass.Monk && s == 41)
                {
                    Assert.Equal(7, dos);
                    Assert.Equal(0, Tables.SpellLevelsByClass[c][s]); // D-006: manual has no Monk SLEEP
                    continue;
                }
                Assert.Equal(dos, Tables.SpellLevelsByClass[c][s]);
            }
    }

    [OriginalDataFact]
    public void ClassModifiers_AreTwns0x924()
    {
        for (var c = 0; c < Classes; c++)
            for (var a = 0; a < 5; a++)
                Assert.Equal(Twns[0x924 + c * 5 + a], Tables.ClassAttributeModifiers[c][a]);
    }

    [OriginalDataFact]
    public void RaceModifiers_AreTwns0x942_ExceptHalflingDex_D001()
    {
        for (var r = 0; r < Races; r++)
            for (var a = 0; a < 5; a++)
            {
                var dos = Twns[0x942 + r * 5 + a];
                if (r == (int)Race.Halfling && a == 2)
                {
                    Assert.Equal(11, dos);
                    Assert.Equal(12, Tables.RaceAttributeModifiers[r][a]); // D-001: manual ceiling 20
                    continue;
                }
                Assert.Equal(dos, Tables.RaceAttributeModifiers[r][a]);
            }
    }
}
