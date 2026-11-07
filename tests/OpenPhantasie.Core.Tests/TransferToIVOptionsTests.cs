using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Core.Tests;

/// <summary>
/// Phase 09 T5, the two IV transfer options (user s72, built s149): <c>p4.transfer-join</c> (a III character joins IV's Guild,
/// <c>ancestry.md</c> section 8.3) and <c>p4.transfer-games</c> (I and II characters into the Hall, section 8.4). Both off in
/// every preset: the Hall's T stays the original's (<see cref="TransferToIVTests"/>).
/// </summary>
public class TransferToIVOptionsTests
{
    private static readonly GameOptions Code = GameOptions.ForPreset(GameId.P4, OptionPreset.Dos);
    private static readonly GameOptions Join = Code.With(P4RuleOptions.TransferJoin.Id, "guild");
    private static readonly GameOptions AllGames = Code.With(P4RuleOptions.TransferGames.Id, "all");
    private static readonly P4Tables T = GameTables.Load(GameId.P4).Phantasie4!.Tables;
    private static readonly P4Atlas Atlas = Phantasie4World.LoadAtlas();
    private static readonly P4ServiceData Data = Phantasie4World.LoadServiceData();

    private static readonly Deed BlackLord = new(Deed.Slew, new ContentId(GameId.P1, Endgame.BlackLordId));
    private static readonly Deed Orb = new(Deed.Destroyed, new ContentId(GameId.P2, Phantasie2Campaign.OrbId));

    private static Character Third(string name, Race race = Race.Elf, CharacterClass cls = CharacterClass.Wizard,
        int social = 3, CharacterStatus status = CharacterStatus.Ok) => new()
    {
        Name = name, Race = race, BirthRace = race, Class = cls, Level = 7, Status = status, Score = 150,
        Stats = new CharacterStats(16, 12, 15, 14, 11, 10), GoldInBank = 5000,
        Phantasie3 = new Phantasie3CharacterData { SocialClass = social, Origin = Phantasie3Origin.Netherworld },
    };

    private static Character Earlier(string name, Race race = Race.Dwarf, CharacterClass cls = CharacterClass.Fighter) => new()
    {
        Name = name, Race = race, BirthRace = race, Class = cls, Level = 9, Stats = new CharacterStats(17, 9, 12, 16, 10, 11),
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

    /// <summary>Pendragon's Hall, T answered Okay, with each game's roster from <paramref name="rosters"/>; the games asked are logged.</summary>
    private static P4TownLoop AfterOkay(GameOptions o, P4Party party, Dictionary<GameId, IReadOnlyList<Character?>?> rosters,
        List<GameId>? asked = null)
    {
        var w = new P4TownLoop(o, T, Atlas, party, new SeededRandomSource(1), Data)
        {
            HallTransferSource = g =>
            {
                asked?.Add(g);
                return rosters.GetValueOrDefault(g);
            },
        };
        w.Arrive(1);
        w.Enter(P4Building.HallOfGlory);
        w.Choose(2); // T
        w.Answer(true);
        return w;
    }

    [OriginalDataFact]
    public void Options_AreOffInEveryPreset()
    {
        foreach (var preset in new[] { OptionPreset.Manual, OptionPreset.Dos, OptionPreset.Remaster })
        {
            var o = GameOptions.ForPreset(GameId.P4, preset);
            Assert.False(o.P4TransferJoinsGuild());
            Assert.False(o.P4TransferFromAllGames());
        }
        Assert.True(Join.P4TransferJoinsGuild());
        Assert.True(AllGames.P4TransferFromAllGames());
    }

    [OriginalDataFact]
    public void BothOff_TheTransferAsksNothingMore_AndReadsOnlyIII()
    {
        var asked = new List<GameId>();
        var w = AfterOkay(Code, Party(), new() { [GameId.P3] = [Third("ALPHA")] }, asked);
        Assert.Equal((P4TownPrompt.Pick, "_show_gm"), (w.Prompt, w.IconMenu));
        Assert.Equal([GameId.P3], asked);
        Assert.Contains(GameText.S("p4x.0x4281a").Trim(), w.Box);
    }

    // ---- p4.transfer-games: I and II into the Hall (section 8.4) ---------------------------------------------------

    [OriginalDataFact]
    public void FromEarlierGame_ResultsFromDeedsAndSaves()
    {
        var hero = Earlier("SIEGFRIEDXX");
        hero.Deeds = [BlackLord];
        var h = P4Hall.FromEarlierGame(hero, GameId.P1, 3);
        Assert.Equal(("SIEGFRIEDX", P4Ids.Dwarf, (int)CharacterClass.Fighter, 9), (h.Name, h.Race, h.Class, h.Level));
        Assert.Equal((P4Hall.EarlierGameSocial, 0), (h.Social, h.Origin)); // Noble; Gelnor
        Assert.Equal([17, 9, 12, 16, 10], h.Stats);
        Assert.Equal([100, 0, 0], h.Results);
        Assert.Equal("p4x.0x41c35", P4TownLoop.HallDeeds(h).First); // defeated the Black Lord

        Assert.Equal([1, 0, 0], P4Hall.FromEarlierGame(Earlier("PLAIN"), GameId.P1, 4).Results);
        Assert.Equal("p4x.0x41c54", P4TownLoop.HallDeeds(P4Hall.FromEarlierGame(Earlier("PLAIN"), GameId.P1, 4)).First);

        // A II character who came from I and finished both; one made in the Netherworld who finished nothing.
        var veteran = Earlier("VET");
        veteran.Deeds = [BlackLord, Orb];
        veteran.P2.Origin = Phantasie2Origin.Gelnor;
        var v = P4Hall.FromEarlierGame(veteran, GameId.P2, 5);
        Assert.Equal([100, 100, 0], v.Results);
        Assert.Equal(0, v.Origin);
        Assert.Equal(("p4x.0x41c6a", "p4x.0x41c81"), P4TownLoop.HallDeeds(v)); // freed Ferronrah

        var shade = Earlier("SHADE");
        shade.P2.Origin = Phantasie2Origin.Netherworld;
        var s = P4Hall.FromEarlierGame(shade, GameId.P2, 6);
        Assert.Equal([0, 1, 0], s.Results);
        Assert.Equal(2, s.Origin);
        Assert.Equal(("p4x.0x41c6a", "p4x.0x41c91"), P4TownLoop.HallDeeds(s)); // failed to free Ferronrah

        // An I character who came over from II (II → I) played II too; an Undead keeps its living race; a dead one is Dead.
        var back = Earlier("BACK");
        back.P2.Origin = Phantasie2Origin.Ferronrah;
        back.Race = Race.Undead;
        back.Status = CharacterStatus.Dead;
        var b = P4Hall.FromEarlierGame(back, GameId.P1, 7);
        Assert.Equal([1, 1, 0], b.Results);
        Assert.Equal((P4Ids.Dwarf, false), (b.Race, b.Alive));

        // Completed → its descendants know Divine Aid (_chkwon); played only → not.
        Assert.True(new P4Ancestor(h.Race, h.Class, h.Stats, h.Slot, h.Social, h.Results).CompletedAGame);
        Assert.False(new P4Ancestor(s.Race, s.Class, s.Stats, s.Slot, s.Social, s.Results).CompletedAGame);
        Assert.Throws<ArgumentOutOfRangeException>(() => P4Hall.FromEarlierGame(hero, GameId.P3, 3));
    }

    [OriginalDataFact]
    public void AllGames_AsksTheGame_IAndIIGoToTheHallOnly()
    {
        var hero = Earlier("HERO");
        hero.Deeds = [BlackLord];
        var party = Party();
        var rosters = new Dictionary<GameId, IReadOnlyList<Character?>?>
        {
            [GameId.P1] = [hero, null, Earlier("SQUIRE")], [GameId.P2] = [Earlier("FERRO")], [GameId.P3] = [Third("ALPHA")],
        };
        // The join option on too: I and II still go straight to the Hall list (no "where" question).
        var w = AfterOkay(AllGames.With(P4RuleOptions.TransferJoin.Id, "guild"), party, rosters);
        Assert.Equal((P4TownPrompt.Pick, ""), (w.Prompt, w.IconMenu));
        Assert.Contains(GameText.S("core.p4.hall_from_which_game"), w.Box);
        Assert.Equal(3, w.Choices.Count);
        w.Choose(0); // Phantasie I
        Assert.Equal((P4TownPrompt.Pick, "_show_gm"), (w.Prompt, w.IconMenu));
        Assert.Contains(GameText.S("core.p4.hall_list_i").Trim(), w.Box);
        Assert.Equal(2 + 2, w.Choices.Count);
        Assert.Contains("8" + GameText.S("p4x.0x42879"), w.Box); // the Hall's free slots
        w.Choose(0);
        w.Choose(2); // O
        w.Answer(true);
        Assert.Equal(("HERO", 100), (party.HallImports[3]!.Name, party.HallImports[3]!.Results[0]));
        Assert.Null(party.Roster[1]);

        w.Choose(2);
        w.Answer(true);
        w.Choose(1); // Phantasie II
        Assert.Contains(GameText.S("core.p4.hall_list_ii").Trim(), w.Box);
        w.Choose(0);
        w.Choose(1);
        w.Answer(true);
        Assert.Equal(("FERRO", 1), (party.HallImports[4]!.Name, party.HallImports[4]!.Results[1]));
        Assert.Equal(5, w.HallPersons.Count);

        // SPACE at the game question leaves; a game with no save says so.
        w.Choose(2);
        w.Answer(true);
        w.Choose(-1);
        Assert.Equal((P4TownPrompt.Pick, "_hall"), (w.Prompt, w.IconMenu));
        var none = AfterOkay(AllGames, Party(), new() { [GameId.P3] = [Third("ALPHA")] });
        none.Choose(1);
        Assert.Equal(P4TownPrompt.Wait, none.Prompt);
        Assert.Contains(GameText.S("core.p4.hall_no_ii_save"), none.Box);
    }

    // ---- p4.transfer-join: III into the Guild (section 8.3) --------------------------------------------------------

    [OriginalDataFact]
    public void JoinFromPhantasieIII_BuildsAnIVRecord()
    {
        var c = Third("ELBERETHIEL", social: Phantasie3CharacterData.Superhero);
        c.P3.DivineAid = true;
        var m = P4Hall.JoinFromPhantasieIII(Code, T, new SeededRandomSource(4), c, 2, 1);
        Assert.Equal(("ELBERETH", P4Ids.Elf, P4Ids.Wizard, 1), (m.Name, m.Race, m.Class, m.Level));
        Assert.Equal([16, 12, 15, 14, 11], m.Abilities.Take(5));
        Assert.InRange(m.Abilities[P4Member.Luck], 9, 14); // IV's own Luck roll
        Assert.Equal(700, m.Experience);                     // 100 × III's level 7
        Assert.Equal((P4Hall.SuperHero, 4L * 256), (m.Social, m.Account));
        Assert.True(m.Knows(T.Creation.DivineAidSpell));
        Assert.Equal(P4Creation.Origin(1), m.Origin);        // III's Netherworld: no IV origin of that name
        Assert.Equal((2, 1, 0, P4Member.Okay), (m.GuildSlot, m.Town, m.Score, m.Condition));
        Assert.True(m.HpMax > 0 && m.Hp == m.HpMax);
        Assert.Contains(m.Skills, v => v > 0);
        Assert.All(m.Ancestors, a => Assert.Equal(P4AncestorKind.None, a.Kind));
        var wizard = P4CreationRecord.Create(Code, T, new SeededRandomSource(4), "X", P4Ids.Elf, P4Ids.Wizard,
            [16, 12, 15, 14, 11], [], 1);
        Assert.Equal(wizard.MagicLevel, m.MagicLevel);       // _mkdetail's own level-0 _calpow
        Assert.Equal(wizard.Item, m.Items[0]);

        // III's Ferronrah keeps its name; a III Orc Priest keeps a class IV's creation would not offer the Orc.
        var orc = Third("GRUK", Race.Orc, CharacterClass.Priest);
        orc.P3.Origin = Phantasie3Origin.Ferronrah;
        Assert.False(P4Creation.ClassAllowed(Code, T.Creation, P4Ids.Orc, P4Ids.Priest));
        var o = P4Hall.JoinFromPhantasieIII(Code, T, new SeededRandomSource(4), orc, 3, 1);
        Assert.Equal((P4Ids.Orc, P4Ids.Priest, 1), (o.Race, o.Class, o.Origin));

        var dead = P4Hall.JoinFromPhantasieIII(Code, T, new SeededRandomSource(4), Third("GONE", status: CharacterStatus.Dead), 4, 1);
        Assert.Equal((P4Member.Dead, 0), (dead.Condition, dead.Hp));

        var lost = Third("LOST");
        lost.Race = Race.Undead;
        lost.BirthRace = Race.Undead;
        Assert.False(P4Hall.CanJoin(lost));
        Assert.Throws<ArgumentException>(() => P4Hall.JoinFromPhantasieIII(Code, T, new SeededRandomSource(4), lost, 4, 1));
    }

    [OriginalDataFact]
    public void JoinOn_AsksHallOrGuild_GuildFillsTheEmptyGuildSlots()
    {
        var party = Party();
        var lost = Third("LOST");
        lost.Race = Race.Undead;
        lost.BirthRace = Race.Undead;
        var w = AfterOkay(Join, party, new() { [GameId.P3] = [Third("ALPHA"), lost, Third("BETA")] });
        Assert.Equal((P4TownPrompt.Pick, ""), (w.Prompt, w.IconMenu));
        Assert.Contains(GameText.S("core.p4.hall_where"), w.Box);
        w.Choose(1); // Guild
        Assert.Equal((P4TownPrompt.Pick, "_show_gm"), (w.Prompt, w.IconMenu));
        Assert.Equal(2 + 2, w.Choices.Count); // the raceless Undead is not listed
        Assert.Contains("9" + GameText.S("p4x.0x42879"), w.Box); // nine empty Guild slots
        w.Choose(1); // BETA
        w.Choose(2); // O
        w.Answer(true);
        Assert.Equal("BETA", party.Roster[1]!.Name);
        Assert.Equal(1, party.Roster[1]!.GuildSlot);
        Assert.Single(party.Members); // joins the Guild, not the party
        Assert.All(party.HallImports, Assert.Null);

        // Hall: section 8.2 unchanged.
        w.Choose(2);
        w.Answer(true);
        w.Choose(0);
        Assert.Equal(3 + 2, w.Choices.Count);
        w.Choose(0);
        w.Choose(3);
        w.Answer(true);
        Assert.Equal("ALPHA", party.HallImports[3]!.Name);

        // A full Guild: "No more can be transferred."
        for (var g = 2; g < P4Party.RosterSlots; g++)
            party.Roster[g] = party.Roster[0]!.Clone();
        w.Choose(2);
        w.Answer(true);
        w.Choose(1);
        Assert.Equal(P4TownPrompt.Wait, w.Prompt);
        Assert.Contains(GameText.S("p4x.0x4285a"), w.Box);
    }

    [OriginalDataFact]
    public void JoinedMember_SavesWithTheGame()
    {
        var game = Phantasie4World.NewGame(Join, new SeededRandomSource(3));
        game.Party.Roster[1] = P4Hall.JoinFromPhantasieIII(Join, T, new SeededRandomSource(4), Third("ALPHA"), 1, 1);
        var back = Phantasie4World.RestoreCampaign(Phantasie4SaveCodec.ReadJson(Phantasie4SaveCodec.WriteJson(game.Capture())),
            new SeededRandomSource(1));
        var m = back.Party.Roster[1]!;
        Assert.Equal(("ALPHA", 700L, 3, true), (m.Name, m.Experience, m.Social, back.Options.P4TransferJoinsGuild()));
    }
}
