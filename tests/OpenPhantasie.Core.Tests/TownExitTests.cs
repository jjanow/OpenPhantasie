using OpenPhantasie.Core;

namespace OpenPhantasie.Core.Tests;

public class TownExitTests
{
    // GUILD.DAT fixtures (level 1, after leaving town): fighto/thiefo/crusho/gobbo/priesto/wizardo.
    [Theory]
    [InlineData(CharacterClass.Fighter, 15, 2)]
    [InlineData(CharacterClass.Thief, 11, 2)]
    [InlineData(CharacterClass.Fighter, 10, 1)]
    [InlineData(CharacterClass.Priest, 3, 0)]
    [InlineData(CharacterClass.Wizard, 3, 0)]
    public void WeaponDamageA_MatchesFixtures(CharacterClass cls, int str, int expected) =>
        Assert.Equal(expected, TownExit.WeaponDamageA(1, cls, str));

    [Fact]
    public void WeaponDamageA_ScalesWithLevelAndClassFactor() =>
        // Thief factor 7: (3·10·7 + 11·18) / 66 = 408 / 66 = 6
        Assert.Equal(6, TownExit.WeaponDamageA(10, CharacterClass.Thief, 18));

    [Fact]
    public void Apply_WritesDamageAAndOrderStampForEverySlotIncludingDead()
    {
        var p = new Party();
        p.Members.Add(new Character { Name = "a", Class = CharacterClass.Fighter, Stats = new CharacterStats(15, 0, 0, 0, 0, 0) });
        p.Members.Add(new Character { Name = "b", Class = CharacterClass.Thief, Status = CharacterStatus.Dead, Stats = new CharacterStats(11, 0, 0, 0, 0, 0) });

        TownExit.Apply(p);

        Assert.Equal(2, p.Members[0].WeaponDamageA);
        Assert.Equal(12, p.Members[0].Phantasie1.PartyOrderStamp);
        Assert.Equal(2, p.Members[1].WeaponDamageA);
        Assert.Equal(14, p.Members[1].Phantasie1.PartyOrderStamp);
    }
}
