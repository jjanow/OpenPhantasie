using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Core.Tests;

/// <summary>
/// Phase 09 T5, III → IV: the Hall of Glory's T (<c>_htran</c> → <c>_trans</c> → <c>_mtrans</c> → <c>_mcopy</c>,
/// <c>ancestry.md</c> section 8.2; <see cref="P4Hall"/>, <see cref="P4TownLoop.HallTransferSource"/>).
/// </summary>
public class TransferToIVTests
{
    private static readonly GameOptions Code = GameOptions.ForPreset(GameId.P4, OptionPreset.Dos);
    private static readonly P4Tables T = GameTables.Load(GameId.P4).Phantasie4!.Tables;
    private static readonly P4Atlas Atlas = Phantasie4World.LoadAtlas();
    private static readonly P4ServiceData Data = Phantasie4World.LoadServiceData();

    private static Character Third(string name, CharacterClass cls = CharacterClass.Fighter, int social = 3,
        int score = 0, CharacterStatus status = CharacterStatus.Ok) => new()
    {
        Name = name, Race = Race.Elf, BirthRace = Race.Elf, Class = cls, Level = 7, Status = status, Score = score,
        Stats = new CharacterStats(16, 12, 15, 14, 11, 10),
        Phantasie3 = new Phantasie3CharacterData
        {
            SocialClass = social, Origin = Phantasie3Origin.Gelnor, Phantasie1Score = 120, Phantasie2Score = 0,
        },
    };

    private static P4Party Party()
    {
        var p = new P4Party { Gold = 100 };
        var m = new P4Member
        {
            Name = "M0", Class = P4Ids.Fighter, Race = P4Ids.Human, Level = 1, HpMax = 20, Hp = 20,
            Abilities = [15, 15, 15, 15, 12, 10], AgingFactor = 200, Social = 3, MoveBase = 7, GuildSlot = 0,
        };
        p.Roster[0] = m.Clone();
        p.Members.Add(m);
        return p;
    }

    /// <summary>Pendragon's Hall, T, "Transfer?" answered Okay: the III list is open (or the no-save note).</summary>
    private static P4TownLoop AtList(P4Party party, IReadOnlyList<Character?>? roster)
    {
        var w = new P4TownLoop(Code, T, Atlas, party, new SeededRandomSource(1), Data) { HallTransferSource = _ => roster };
        w.Arrive(1);
        w.Enter(P4Building.HallOfGlory);
        w.Choose(2); // T: Character transfer
        Assert.Equal((P4TownPrompt.Confirm, "_htran"), (w.Prompt, w.IconMenu));
        w.Answer(true);
        return w;
    }

    [Fact]
    public void FromPhantasieIII_MapsByMeaning()
    {
        var c = Third("ARWENIELLO", social: Phantasie3CharacterData.Superhero, score: 260);
        c.P3.Origin = Phantasie3Origin.Ferronrah;
        var h = P4Hall.FromPhantasieIII(c, 3);
        Assert.Equal("ARWENIELLO", h.Name);
        Assert.Equal((3, P4Ids.Elf, (int)CharacterClass.Fighter, 7), (h.Slot, h.Race, h.Class, h.Level));
        Assert.Equal(P4Hall.SuperHero, h.Social); // III's Superhero 5 is IV's Super Hero 4
        Assert.Equal(1, h.Origin);               // the import list: Ferronrah
        Assert.Equal([16, 12, 15, 14, 11], h.Stats);
        Assert.Equal([120, 0, 255], h.Results);  // the score capped at the byte
        Assert.Equal((0, true), (h.Descendants, h.Alive));
        Assert.Equal("p4x.0x41ce1", P4TownLoop.HallDeeds(h).First); // saved Scandor

        var dark = P4Hall.FromPhantasieIII(Third("DARK", social: Phantasie3CharacterData.Darklord, score: 200), 4);
        Assert.Equal(P4Hall.DarkLord, dark.Social);
        Assert.Equal("p4x.0x41caa", P4TownLoop.HallDeeds(dark).First); // aided Nikademus

        var undead = Third("BONES", status: CharacterStatus.Dead);
        undead.Race = Race.Undead;
        undead.BirthRace = Race.Orc;
        var u = P4Hall.FromPhantasieIII(undead, 5);
        Assert.Equal(((int)Race.Orc, false), (u.Race, u.Alive)); // the living race, never IV's 15 (Hill Giant)
    }

    [OriginalDataFact]
    public void Transfer_MarksUntilTheFreeSlotsAreFull_ThenCopiesInListOrder()
    {
        var party = Party();
        for (var s = 3; s < 9; s++) // six imports already: two slots left
            party.HallImports[s] = P4Hall.FromPhantasieIII(Third($"OLD{s}"), s);
        var w = AtList(party, [Third("ALPHA"), null, Third("BETA"), Third("GAMMA")]);
        Assert.Equal((P4TownPrompt.Pick, "_show_gm"), (w.Prompt, w.IconMenu));
        Assert.Equal(3 + 2, w.Choices.Count); // the three, then O and X
        Assert.Contains("2" + GameText.S("p4x.0x42879"), w.Box);
        w.Choose(2); // GAMMA
        w.Choose(0); // ALPHA: the marks fill the two slots
        Assert.Equal(P4TownPrompt.Confirm, w.Prompt);
        Assert.Contains(GameText.S("p4x.0x4285a"), w.Box);
        w.Answer(true);
        Assert.Equal(["ALPHA", "GAMMA"], new[] { party.HallImports[9]!.Name, party.HallImports[10]!.Name });
        Assert.Equal((P4TownPrompt.Pick, "_hall"), (w.Prompt, w.IconMenu));
        Assert.Equal(11, w.HallPersons.Count);
    }

    [OriginalDataFact]
    public void Transfer_UnmarkOkayCancelAndAbort()
    {
        var party = Party();
        var w = AtList(party, [Third("ALPHA"), Third("BETA")]);
        w.Choose(0);
        w.Choose(0); // unmarked again
        w.Choose(1);
        Assert.StartsWith("*", w.Choices[1].Text);
        w.Choose(2); // O: Okay early
        w.Answer(false); // Cansel
        Assert.All(party.HallImports, Assert.Null);

        w.Choose(2);
        w.Answer(true);
        w.Choose(1);
        w.Choose(3); // X: Abort
        Assert.All(party.HallImports, Assert.Null);

        w.Choose(2);
        w.Answer(true);
        w.Choose(1);
        w.Choose(2);
        w.Answer(true);
        Assert.Equal("BETA", party.HallImports[3]!.Name);
        Assert.Equal(4, w.HallPersons.Count);
    }

    [OriginalDataFact]
    public void Transfer_FullHall_OrNoSave_SaysSoAndReturns()
    {
        var party = Party();
        for (var s = 3; s < P4Party.HallSlots; s++)
            party.HallImports[s] = P4Hall.FromPhantasieIII(Third($"OLD{s}"), s);
        var w = AtList(party, [Third("ALPHA")]);
        Assert.Equal(P4TownPrompt.Wait, w.Prompt);
        Assert.Contains(GameText.S("p4x.0x4285a"), w.Box);
        w.Continue();
        Assert.Equal(P4TownScene.Hall, w.Scene);

        var none = AtList(Party(), null);
        Assert.Equal(P4TownPrompt.Wait, none.Prompt);
        Assert.Contains(GameText.S("core.p4.hall_no_iii_save"), none.Box);
    }

    [OriginalDataFact]
    public void Imports_AreAncestors_SaveWithTheGame_AndAPriestMakesLordWoodDead()
    {
        var game = Phantasie4World.NewGame(Code, new SeededRandomSource(3));
        game.Party.HallImports[3] = P4Hall.FromPhantasieIII(Third("HERO", score: 150), 3);
        var w = game.Town;
        Assert.Equal(["Lord Wood", "Filmon", "Nikademus", "HERO"], w.HallPersons.Select(h => h.Name));
        Assert.True(P4Hall.IsAlive(w.HallPersons[0], w.HallPersons));

        // N → Yes → H: the Hall list offers the import.
        w.Enter(P4Building.Guild);
        w.Choose(0);
        w.Choose(1);
        w.Choose(1);
        w.Choose(1);
        Assert.Equal(4, w.Choices.Count);
        Assert.Contains("HERO", w.Choices[3].Text);

        var back = Phantasie4World.RestoreCampaign(Phantasie4SaveCodec.ReadJson(Phantasie4SaveCodec.WriteJson(Capture(game))),
            new SeededRandomSource(1));
        var h = back.Party.HallImports[3]!;
        Assert.Equal(("HERO", 3, 7, true), (h.Name, h.Slot, h.Level, h.Alive));
        Assert.Equal([16, 12, 15, 14, 11], h.Stats);
        Assert.Equal([120, 0, 150], h.Results);

        game.Party.HallImports[4] = P4Hall.FromPhantasieIII(Third("CLERIC", CharacterClass.Priest), 4);
        Assert.False(P4Hall.IsAlive(w.HallPersons[0], w.HallPersons));
    }

    /// <summary>A capture outside a building: a fresh game on Pendragon's menu.</summary>
    private static P4CampaignSnapshot Capture(Phantasie4Campaign game)
    {
        var fresh = Phantasie4World.NewGame(Code, new SeededRandomSource(3));
        fresh.Party.HallImports[3] = game.Party.HallImports[3];
        return fresh.Capture();
    }
}
