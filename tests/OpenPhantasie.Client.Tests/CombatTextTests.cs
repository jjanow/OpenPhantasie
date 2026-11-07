using OpenPhantasie.Client;
using OpenPhantasie.Core;

namespace OpenPhantasie.Client.Tests;

public class CombatTextTests
{
    [Fact]
    public void RankLines_NoEncounter_IsEmpty() => Assert.Empty(CombatText.RankLines(null));

    private sealed class NoItems : IItemCatalog
    {
        public ItemCatalogEntry? Get(int itemId) => null;
        public int Count => 0;
    }

    [Theory]
    [InlineData(OptionPreset.Manual, "K S D U")]
    [InlineData(OptionPreset.Dos, "K S D O")]
    public void RankLines_III_AddsAStatusLetterPerLivingMember(OptionPreset preset, string letters)
    {
        var fight = new P3Fight
        {
            HitLocations = new P3HitLocationTable([[100, 100, 100, 100, 100, 100]]), Items = new NoItems(),
        };
        var enc = new CombatEncounter(new Party(RulesProfile.PhantasieI))
        {
            Rules = GameOptions.ForPreset(GameId.P3, preset), CampaignState = fight,
        };
        Monster Gnome(int hp = 5) => new() { Id = 1, Name = "Gnome", HitPoints = hp };
        var asleep = Gnome();
        asleep.StatusFlags = MonsterStatusFlags.Asleep;
        var down = Gnome();
        var outCold = Gnome();
        enc.AddRank(Gnome(), asleep, down, outCold, Gnome(hp: 0));
        fight.BodyOf(down)[P3BodyArea.LeftLeg] = P3AreaState.Broken;
        fight.BodyOf(outCold)[P3BodyArea.Head] = P3AreaState.Broken;

        var line = Assert.Single(CombatText.RankLines(enc));
        Assert.EndsWith(" " + letters, line);
    }
}
