using static OpenPhantasie.Core.Tests.Phantasie3CampaignTests;

namespace OpenPhantasie.Core.Tests;

/// <summary>III's creation age (DOS ovl2 <c>0x2691…0x2748</c>, decoded s98): f = LCK · race factor (single precision).</summary>
public class P3CreationAgeTests
{
    private static P3CreationAgeRule Rule => Campaign(NewGame()).World.CreationAge!;

    [OriginalDataTheory]
    [InlineData(Race.Human, 10, 1, 10, 510)]     // f = 10: 25 · 10 + 260
    [InlineData(Race.Gnome, 10, 7, 10, 570)]     // races 0 and 3 keep LCK
    [InlineData(Race.Dwarf, 13, 1, 32, 1072)]    // f = 32.5
    [InlineData(Race.Halfling, 8, 7, 20, 880)]   // f = 20: 31 · 20 + 260
    [InlineData(Race.Elf, 10, 7, 190, 6150)]     // f = 19 · LCK
    [InlineData(Race.Orc, 8, 1, 3, 340)]         // f = 3.2f: 25 · 3.2000000477 + 260 = 340.0000012
    [InlineData(Race.Pixie, 13, 4, 5, 405)]      // f = 5.2f: 28 · 5.2 + 260 = 405.6 (the race-13 branch is dead code)
    public void AgeAndMaximumAge_FollowDos(Race race, int lck, int roll, int maxAge, int ageClock)
    {
        Assert.Equal(maxAge, Rule.MaxAge(race, lck));
        Assert.Equal(ageClock, Rule.AgeClock(race, lck, roll));
    }

    [OriginalDataFact]
    public void NewMember_StartsAtDosAge()
    {
        var s = NewGame(options: GameOptions.ForPreset(GameId.P3).With("p3.lifespans", "dos")); // D3-041: DOS's age
        s.OpenGuild();
        var elf = s.CreateAndEnroll("ELF", Race.Elf, CharacterClass.Wizard);
        var human = s.CreateAndEnroll("MAN", Race.Human, CharacterClass.Fighter);
        Assert.InRange(elf.AgeClock, 25 * 19 * 8 + 260, 31 * 19 * 13 + 260);
        Assert.InRange(human.AgeClock, 25 * 8 + 260, 31 * 13 + 260);
        Assert.Equal(0, (elf.AgeClock - 260) % (19 * elf.Stats.Luck)); // (roll + 24) · 19 · LCK
        Assert.Equal(0, (human.AgeClock - 260) % human.Stats.Luck);
    }
}
