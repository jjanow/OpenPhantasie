using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Core.Tests;

/// <summary>
/// II's training threshold in the ST's own arithmetic (<c>guild_train 0x0cf58…0x0d012</c>, s92) and the signed FFP add /
/// sub it needs. Every expected value comes from PHANT.PRG's routines run in a 68000 emulator
/// (<c>tools/scripts/st_p2_ffp_oracle.py</c>); 24 of the 40 training cases differ from a double model by 1–3 XP.
/// </summary>
public class P2TrainingFfpTests
{
    private static readonly GameTables P2 = GameTables.Load(GameId.P2);

    /// <summary>A stat set whose weighted sum for <paramref name="cls"/> is <paramref name="sum"/> (one weight-1 stat).</summary>
    private static CharacterStats StatsFor(CharacterClass cls, int sum) => cls switch
    {
        CharacterClass.Fighter => new CharacterStats(0, 0, sum, 0, 0, 0), // 2·STR + DEX
        CharacterClass.Wizard => new CharacterStats(0, 0, 0, 0, 0, sum), // 2·INT + LUCK
        _ => new CharacterStats(sum, 0, 0, 0, 0, 0), // STR has weight 1 for THIEF, MONK, RANGER, PRIEST
    };

    [OriginalDataTheory]
    [InlineData(CharacterClass.Fighter, 79, 7, 25240, 0)]
    [InlineData(CharacterClass.Priest, 44, 2, 2013, 0)]
    [InlineData(CharacterClass.Fighter, 43, 1, 767, 0)]
    [InlineData(CharacterClass.Ranger, 38, 1, 1390, 0)]
    [InlineData(CharacterClass.Wizard, 25, 9, 339005, 0)]
    [InlineData(CharacterClass.Fighter, 40, 9, 162982, -3)]
    [InlineData(CharacterClass.Priest, 28, 0, 256, -3)]
    [InlineData(CharacterClass.Monk, 50, 9, 195578, -3)]
    [InlineData(CharacterClass.Fighter, 61, 7, 32688, 0)]
    [InlineData(CharacterClass.Monk, 86, 3, 2898, 1)]
    [InlineData(CharacterClass.Ranger, 83, 0, 129, 1)]
    [InlineData(CharacterClass.Thief, 78, 10, 90430, -1)]
    [InlineData(CharacterClass.Fighter, 30, 11, 706063, -1)]
    [InlineData(CharacterClass.Ranger, 49, 8, 117881, -1)]
    [InlineData(CharacterClass.Thief, 28, 9, 139698, -3)]
    [InlineData(CharacterClass.Fighter, 33, 13, 2081473, -1)]
    [InlineData(CharacterClass.Fighter, 28, 0, 237, 1)]
    [InlineData(CharacterClass.Monk, 59, 14, 3143919, -2)]
    [InlineData(CharacterClass.Priest, 24, 14, 5667751, -3)]
    [InlineData(CharacterClass.Wizard, 25, 14, 6430313, 2)]
    [InlineData(CharacterClass.Fighter, 64, 13, 1073262, -3)]
    [InlineData(CharacterClass.Ranger, 50, 12, 1220753, -2)]
    [InlineData(CharacterClass.Wizard, 21, 12, 2361557, 0)]
    [InlineData(CharacterClass.Ranger, 53, 14, 3733156, 2)]
    [InlineData(CharacterClass.Wizard, 76, 14, 2115258, 0)]
    [InlineData(CharacterClass.Monk, 28, 14, 6624631, 0)]
    [InlineData(CharacterClass.Fighter, 72, 14, 1717519, 0)]
    [InlineData(CharacterClass.Fighter, 23, 14, 5376528, -1)]
    [InlineData(CharacterClass.Wizard, 87, 14, 1847813, 0)]
    [InlineData(CharacterClass.Wizard, 64, 14, 2511864, -2)]
    [InlineData(CharacterClass.Ranger, 60, 14, 3297621, -2)]
    [InlineData(CharacterClass.Monk, 44, 14, 4215698, -3)]
    [InlineData(CharacterClass.Priest, 65, 14, 2092728, -1)]
    [InlineData(CharacterClass.Priest, 44, 12, 953714, -3)]
    [InlineData(CharacterClass.Wizard, 25, 13, 3571783, -2)]
    [InlineData(CharacterClass.Ranger, 55, 14, 3597401, -3)]
    [InlineData(CharacterClass.Wizard, 40, 13, 2232380, 0)]
    [InlineData(CharacterClass.Monk, 29, 12, 1973190, -1)]
    [InlineData(CharacterClass.Wizard, 45, 14, 3572424, 2)]
    [InlineData(CharacterClass.Ranger, 29, 12, 2104732, -3)]
    public void Shortfall_IsTheStsFfpN(CharacterClass cls, int sum, int level, int xp, int n)
    {
        var rules = P2.Rules.Training!;
        Assert.Equal(TrainingArithmetic.MotorolaFfp, rules.Arithmetic);
        Assert.Equal(n < 0 ? -n : 0, rules.Shortfall(cls, StatsFor(cls, sum), level, xp));
    }

    [OriginalDataFact]
    public void XpRequired_IsTheLeastXpWithNoShortfall()
    {
        var rules = P2.Rules.Training!;
        var stats = StatsFor(CharacterClass.Wizard, 25);
        var need = rules.XpRequired(CharacterClass.Wizard, stats, 14, 0);
        Assert.Equal(0, rules.Shortfall(CharacterClass.Wizard, stats, 14, need));
        Assert.True(rules.Shortfall(CharacterClass.Wizard, stats, 14, need - 1) > 0);
    }

    /// <summary>a, b, a + b, a − b from the ST's 0x1db48 / 0x1dd1a (random signed operands, exponents 34–93).</summary>
    private static readonly uint[][] AddSub =
    [
        [0xDBC8FB54, 0xBFC1EA4B, 0xDC28DC54, 0xDB691A54],
        [0xF814E859, 0x9A1AFE59, 0xC917F35A, 0xBBF3D458],
        [0xC78C28AD, 0x926A1EAD, 0xACFB23AE, 0xD48828AB],
        [0x00000000, 0xB59EEE53, 0xB59EEE53, 0xB59EEED3],
        [0xAA9EBAB4, 0xB46C6E2D, 0xA935E1B4, 0xAC0793B4],
        [0xCC7D6DA3, 0xA54CB832, 0xA54B1F32, 0xA54E51B2],
        [0x80DDB7C8, 0xDAF84ED6, 0xDAFA51D6, 0xDAF64B56],
        [0xFB312040, 0x85DA84D1, 0x85DA06D1, 0x85DB0251],
        [0x00000000, 0xDDBE443A, 0xDDBE443A, 0xDDBE44BA],
        [0xB24F3E29, 0xF65317B8, 0xF651B2B8, 0xF6547C38],
        [0xF67D52A8, 0x8960AF28, 0xDA3946A7, 0xBFEF00A9],
        [0xDCD5065C, 0x97921EB5, 0xDCD5065C, 0xDCD5065C],
        [0xA61C37D0, 0xA959FE50, 0xCF71C04A, 0xA7BB1AD1],
        [0xE7E84724, 0xD8056ED6, 0xD8056ED6, 0xD8056E56],
        [0x8E42D45C, 0xD58D2A5C, 0xB1E7FF5D, 0x8E94ACDB],
        [0xA1FDACCA, 0xEF3F024A, 0x9A82AC49, 0xC89E57CB],
        [0xA425D73F, 0xF41B32C1, 0xCB11BCC1, 0x8E925442],
        [0xE75EBE5B, 0xADECE9CA, 0xE75E675B, 0xE75F155B],
        [0xFDEAABB3, 0xDB0F134E, 0xDB0F134E, 0xDB0F13CE],
        [0xC7E5EBD7, 0xCB39E337, 0xC7E5EBD7, 0xC7E5EBD7],
        [0x00000000, 0xC02D5EB6, 0xC02D5EB6, 0xC02D5E36],
        [0xDB4836B8, 0xE8916D38, 0xD4937034, 0xE1ECD1B9],
        [0xDD411B37, 0xAA8A49AE, 0xDCEBD637, 0xDD966037],
        [0x942EA4D0, 0xEBB906B5, 0x942EA4D0, 0x942EA4D0],
        [0x8AB77240, 0xA22ED1A9, 0x8AB77140, 0x8AB77340],
        [0x8920DABC, 0x926D153C, 0x94C3B038, 0x8DC6F7BD],
        [0x802B4BB8, 0xE8F88859, 0xE8F88859, 0xE8F888D9],
        [0x893B5DC5, 0xD6E12BC7, 0xF93002C7, 0xB4925447],
        [0xEB4A43BC, 0xA8226FC8, 0xA83124C8, 0xA813BA48],
        [0xA20B2D48, 0x803E183A, 0xA20D2E48, 0xA2092C48],
        [0xA404F2B6, 0xB9F125C1, 0xBA05A6C1, 0xB9DCA441],
        [0xE246B758, 0xC283DED1, 0xE0C1AF58, 0xE3CBBF58],
        [0xD1B3FBB5, 0xA4AEBF35, 0xB414F0B3, 0xBB315DB6],
        [0xF18718B1, 0x00000031, 0xE78E802D, 0xF18718B1],
        [0xFAF4B7C4, 0xF96A4844, 0xC53780BD, 0xFA2F7FC5],
        [0xA79EB8D0, 0xF9817150, 0xA3C5724F, 0xD09014D1],
        [0xC847ECC4, 0x86D04BBF, 0xCC7E6EC4, 0xC4116AC4],
        [0xA1EA2F24, 0xF4A3194D, 0xF4A3194D, 0xF4A319CD],
        [0x83BA3CBD, 0xFB5C6DD8, 0xFB5C6DD8, 0xFB5C6D58],
        [0xB24BBCDB, 0x90E8EE35, 0xB24BBCDB, 0xB24BBCDB],
        [0xC94EF5CB, 0xA486F3AC, 0xC94EF5CB, 0xC94EF5CB],
        [0xD4AB2E44, 0x8C6E0A44, 0xB08C9C45, 0x907A4843],
        [0x901B5D4C, 0xEDE8F9A6, 0x901B5D4C, 0x901B5D4C],
        [0x00000000, 0xB81F9849, 0xB81F9849, 0xB81F98C9],
        [0xCAE0D0CF, 0xAF6EE84F, 0xDB8F40CC, 0xBD27DCD0],
        [0x9BB3F5CC, 0xC7A59625, 0x9BB3F5CC, 0x9BB3F5CC],
        [0xA263A3A2, 0xFD4C2FC1, 0xFD4C2FC1, 0xFD4C2F41],
        [0x8E45B6AA, 0xCA4CF12A, 0xF01CEC28, 0xAC4953AB],
        [0xC1129CD7, 0xA23F7655, 0x9882BFD7, 0xE9A27AD7],
        [0xE38F302D, 0xD773602D, 0xDD81482E, 0xC1BD0029],
        [0xF418A9B1, 0xFAFFF236, 0xF35F2D36, 0x81505BB7],
        [0x00000000, 0x8DBC9B58, 0x8DBC9B58, 0x8DBC9BD8],
        [0xB91C4B51, 0xFDCF6251, 0xDB75D652, 0x89662ED0],
        [0xDAB34A57, 0xD9428DD4, 0xBF8AF857, 0xF5DB9C57],
        [0xDBBA5BCB, 0xEEF2F1D8, 0xEEF9CFD8, 0xEEEC1358],
        [0xB7026E51, 0x98D4E951, 0xA7EBAB52, 0xF16C284E],
        [0xFD728CDA, 0xD93873A2, 0xFD728CDA, 0xFD728CDA],
        [0xEDBBABB6, 0xB10CD236, 0xF2BB64B4, 0xCF643EB7],
        [0xF83BA4AB, 0xA95A85D2, 0xA95A85D2, 0xA95A8552],
        [0x00000000, 0xD823F1DD, 0xD823F1DD, 0xD823F15D],
    ];

    [OriginalDataFact]
    public void SignedAddAndSub_MatchThePrograms_Routines()
    {
        foreach (var v in AddSub)
        {
            Assert.Equal(v[2], MotorolaFfp.Add(v[0], v[1]));
            Assert.Equal(v[3], MotorolaFfp.Sub(v[0], v[1]));
        }
    }
}
