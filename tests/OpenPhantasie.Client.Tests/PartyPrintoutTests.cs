using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client.Tests;

/// <summary>s56: Printout lines, OV1 <c>0x46dd</c>.</summary>
public class PartyPrintoutTests
{
    [OriginalDataFact]
    public void Lines_FollowTheDosFormat()
    {
        var data = GameDataRepository.LoadPhantasie1();
        var party = new Party();
        var hero = new Character
        {
            Name = "Aldo", Level = 3, Race = Race.Elf, Class = CharacterClass.Priest, Status = CharacterStatus.Ok,
            SpellsKnown = new int[CharacterCreation.SpellCount],
        };
        hero.SpellsKnown[0] = 1;
        hero.InventoryItemIds.AddRange([49, 105]); // MACE (not listed), HEALING 5
        party.Members.Add(hero);

        var lines = PartyPrintout.Lines(party, data);

        Assert.Equal("", lines[0]);
        Assert.Equal("", lines[1]);
        Assert.Equal("      Aldo LEVEL: 3        ELF     PRIEST  OKAY", lines[2]);
        Assert.Equal("SPELLS:", lines[3]);
        Assert.Equal(data.Spells.First(sp => sp.Id == 0).Name, lines[4]);
        Assert.Equal(["POTIONS:", "HEALING 5"], lines.Skip(5));
    }
}
