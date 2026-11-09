using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Core.Tests;

/// <summary>
/// Elementals across the games (PLAN P3 QoL, user D3-040): the every-game options <c>elemental-load</c>,
/// <c>elemental-drop</c> and <c>elemental-purge</c> (I, II and III; <c>game</c> in every preset = each game's own rule),
/// and the invariant that no game and no option value removes an elemental from the game.
/// </summary>
public class ElementalGuildOptionsTests
{
    private static readonly GameId[] GuildGames = [GameId.P1, GameId.P2, GameId.P3];

    private static readonly Dictionary<GameId, GameDataRepository> Repos = [];

    private static GameDataRepository Data(GameId game)
    {
        lock (Repos)
        {
            if (!Repos.TryGetValue(game, out var d))
                Repos[game] = d = game == GameId.P1
                    ? GameDataRepository.LoadPhantasie1()
                    : GameDataRepository.Load(GameDataRepository.DefaultPathFor(game));
            return d;
        }
    }

    private static readonly CharacterClass[] Classes =
        [CharacterClass.Fighter, CharacterClass.Priest, CharacterClass.Wizard, CharacterClass.Thief, CharacterClass.Ranger,
            CharacterClass.Monk];

    /// <summary>A new game of <paramref name="game"/> in its Guild with <paramref name="members"/> characters in the party.</summary>
    private static GameSession Guild(GameId game, GameOptions options, int members)
    {
        var s = GameSessionFactory.Create(Data(game), new SeededDosRandomSource(11));
        s.StartNewGame(options);
        s.OpenGuild();
        for (var i = 0; i < members; i++)
        {
            var c = s.CreateAndEnroll("EL" + (char)('A' + i), Race.Human, Classes[i]);
            Assert.True(s.TryAddToParty(c, out var err), err);
        }
        return s;
    }

    private static GameOptions Opts(GameId game, params (string Id, string Value)[] values)
    {
        var o = GameOptions.ForPreset(game);
        foreach (var (id, value) in values)
            o = o.With(id, value);
        return o;
    }

    // ---- The descriptors ---------------------------------------------------------------------------------------------

    [Fact]
    public void Descriptors_AreInTheTabsOfIIIAndIII_NotIV_AndTheGamesOwnInEveryPreset()
    {
        RuleOptionDescriptor[] all = [RuleOptionRegistry.ElementalLoad, RuleOptionRegistry.ElementalDrop, RuleOptionRegistry.ElementalPurge];
        Assert.Equal(["elemental-load", "elemental-drop", "elemental-purge"], all.Select(d => d.Id));
        Assert.Equal(["game", "phantasie-i", "phantasie-iii"], RuleOptionRegistry.ElementalLoad.Values);
        Assert.Equal(["game", "phantasie-i", "phantasie-iii"], RuleOptionRegistry.ElementalDrop.Values);
        Assert.Equal(["game", "phantasie-i", "as-drop", "refused"], RuleOptionRegistry.ElementalPurge.Values);
        foreach (var d in all)
        {
            Assert.True(d.EveryGame);
            Assert.Equal(("game", "game", "game"), (d.Manual, d.Dos, d.Remaster)); // today's behaviour in every preset
            foreach (var game in Enum.GetValues<GameId>())
            {
                var tab = RuleOptionRegistry.For(game);
                Assert.Equal(game != GameId.P4, tab.Contains(d));
                Assert.Equal("guild-limit", tab[^1].Id); // guild-limit stays last in each tab
            }
        }
    }

    [OriginalDataFact]
    public void PhantasieIIIsRules_AreIIIsRulesJson_AndIIsHasNoBlock()
    {
        Assert.Equal(ElementalRules.PhantasieIII, GameTables.Load(GameId.P3).Rules.Elementals);
        Assert.Null(GameTables.Load(GameId.P1).Rules.Elementals); // I's rule
        Assert.Null(GameTables.Load(GameId.P2).Rules.Elementals); // II plays I's rule
    }

    // ---- Off (game) = today's rules, every preset --------------------------------------------------------------------

    [OriginalDataTheory]
    [InlineData(GameId.P1, OptionPreset.Manual)]
    [InlineData(GameId.P1, OptionPreset.Dos)]
    [InlineData(GameId.P1, OptionPreset.Remaster)]
    [InlineData(GameId.P2, OptionPreset.Manual)]
    [InlineData(GameId.P2, OptionPreset.Dos)]
    [InlineData(GameId.P2, OptionPreset.Remaster)]
    [InlineData(GameId.P3, OptionPreset.Manual)]
    [InlineData(GameId.P3, OptionPreset.Dos)]
    [InlineData(GameId.P3, OptionPreset.Remaster)]
    public void EveryPreset_PlaysTheGamesOwnRules(GameId game, OptionPreset preset)
    {
        var s = Guild(game, GameOptions.ForPreset(game, preset), members: 1);
        Assert.Equal(s.Party.Rules.ElementalRulesOrDefault, s.GuildElementalRules);
        Assert.Equal(game == GameId.P3 ? ElementalRules.PhantasieIII : ElementalRules.PhantasieI, s.GuildElementalRules);
        Assert.Equal(s.Options.P3PurgeRule(), s.PurgeRule);
        Assert.Equal(
            game != GameId.P3 ? PurgePartyRule.ClearsElemental
            : preset == OptionPreset.Dos ? PurgePartyRule.Refused
            : PurgePartyRule.KeepsElemental,
            s.PurgeRule);
    }

    [OriginalDataTheory]
    [InlineData(GameId.P1)]
    [InlineData(GameId.P2)]
    public void IAndII_Own_SixFirst_NoAddAfter_DropAndPurgeUnload(GameId game)
    {
        var s = Guild(game, GameOptions.ForPreset(game), members: 5);
        Assert.False(s.TryAddElemental(0, out _)); // six characters first
        var sixth = s.CreateAndEnroll("ELZ", Race.Human, CharacterClass.Fighter);
        Assert.True(s.TryAddToParty(sixth, out var err), err);
        Assert.True(s.TryAddElemental(0, out err), err);
        Assert.True(s.TryDropFromParty(s.Party.Members[0], out err), err);
        Assert.Null(s.Party.Elemental); // OV1 0x22d1
        var back = s.Roster.Occupied.First(c => !s.Party.Members.Contains(c));
        Assert.True(s.TryAddToParty(back, out err), err);
        Assert.True(s.TryAddElemental(1, out err), err);
        Assert.False(s.TryAddToParty(s.CreateAndEnroll("ELY", Race.Human, CharacterClass.Fighter), out _)); // full anyway
        Assert.True(s.TryPurge(s.Party.Members[0], out err), err);
        Assert.Null(s.Party.Elemental); // OV1 0x2489
    }

    // ---- elemental-load ----------------------------------------------------------------------------------------------

    [OriginalDataTheory]
    [InlineData(GameId.P1)]
    [InlineData(GameId.P2)]
    public void Load_PhantasieIII_InIAndII_OneCharacter_AndMoreJoinInFrontOfIt(GameId game)
    {
        var s = Guild(game, Opts(game, ("elemental-load", "phantasie-iii")), members: 1);
        Assert.True(s.TryAddElemental(2, out var err), err);
        var c = s.CreateAndEnroll("ELZ", Race.Elf, CharacterClass.Wizard);
        Assert.True(s.TryAddToParty(c, out err), err);
        Assert.Equal(2, s.Party.Adventurers.Count());
        Assert.NotNull(s.Party.Elemental);
        // Under the game's own Drop rule (I's) a Drop still unloads it.
        Assert.True(s.TryDropFromParty(c, out err), err);
        Assert.Null(s.Party.Elemental);
    }

    [OriginalDataFact]
    public void Load_PhantasieI_InIII_SixFirst_AndNobodyAfter()
    {
        var s = Guild(GameId.P3, Opts(GameId.P3, ("elemental-load", "phantasie-i")), members: 5);
        Assert.False(s.TryAddElemental(0, out var err));
        Assert.Equal(GameText.S("core.guild.elementals_only_join_six"), err);
        Assert.True(s.TryDropFromParty(s.Party.Members[4], out err), err);
        var c = s.Roster.Occupied.First(m => !s.Party.Members.Contains(m));
        Assert.True(s.TryAddToParty(c, out err), err);
        Assert.True(s.TryAddToParty(s.CreateAndEnroll("ELZ", Race.Elf, CharacterClass.Wizard), out err), err);
        Assert.True(s.TryAddElemental(0, out err), err);
        Assert.True(s.TryDropFromParty(s.Party.Members[0], out err), err); // III's own Drop: it stays
        Assert.NotNull(s.Party.Elemental);
        Assert.False(s.TryAddToParty(s.Roster.Occupied.First(m => !s.Party.Members.Contains(m)), out err)); // blocked, I's rule
        Assert.Equal(GameText.S("core.guild.elemental_joined_no_more"), err);
    }

    /// <summary>
    /// The state only the option reaches in I and II (an elemental with fewer than six characters) survives a save and the
    /// way out of the Guild; II's night watch reads it only with six members (<c>NightEncounter.Watcher</c>), and I's
    /// SUMMON still needs six (D-033).
    /// </summary>
    [OriginalDataTheory]
    [InlineData(GameId.P1)]
    [InlineData(GameId.P2)]
    public void Load_PhantasieIII_InIAndII_SmallPartyWithElemental_SavesAndLeavesTheGuild(GameId game)
    {
        var s = Guild(game, Opts(game, ("elemental-load", "phantasie-iii")), members: 2);
        Assert.True(s.TryAddElemental(1, out var err), err);
        var name = s.Party.Elemental!.Name;
        var t = GameSessionFactory.Create(Data(game), new SeededDosRandomSource(11));
        t.ApplySnapshot(SessionCodec.ReadJson(SessionCodec.WriteJson(s.CaptureSnapshot())));
        Assert.Equal(name, t.Party.Elemental!.Name);
        Assert.Equal(2, t.Party.Adventurers.Count());
        Assert.Equal("phantasie-iii", t.Options.Get("elemental-load")); // locked in the save
        t.DepartGuildToWilderness();
        Assert.Equal(GameMode.Overworld, t.Mode);
        Assert.Equal(2, t.Party.Members.Count);
    }

    // ---- elemental-drop ----------------------------------------------------------------------------------------------

    [OriginalDataTheory]
    [InlineData(GameId.P1)]
    [InlineData(GameId.P2)]
    public void Drop_PhantasieIII_InIAndII_KeepsItWhileACharacterIsLeft(GameId game)
    {
        var s = Guild(game, Opts(game, ("elemental-drop", "phantasie-iii")), members: 6);
        Assert.True(s.TryAddElemental(0, out var err), err);
        while (s.Party.Adventurers.Count() > 1)
        {
            Assert.True(s.TryDropFromParty(s.Party.Members[0], out err), err);
            Assert.NotNull(s.Party.Elemental);
        }
        Assert.True(s.TryDropFromParty(s.Party.Members[0], out err), err);
        Assert.Null(s.Party.Elemental); // nobody left: unloaded
    }

    [OriginalDataFact]
    public void Drop_PhantasieI_InIII_UnloadsItOnEveryDrop()
    {
        var s = Guild(GameId.P3, Opts(GameId.P3, ("elemental-drop", "phantasie-i")), members: 3);
        Assert.True(s.TryAddElemental(0, out var err), err);
        Assert.True(s.TryDropFromParty(s.Party.Members[0], out err), err);
        Assert.Null(s.Party.Elemental);
    }

    // ---- elemental-purge ---------------------------------------------------------------------------------------------

    [OriginalDataTheory]
    [InlineData(GameId.P1, "as-drop", "game", false)]          // I's own Drop unloads
    [InlineData(GameId.P1, "as-drop", "phantasie-iii", true)]
    [InlineData(GameId.P2, "as-drop", "phantasie-iii", true)]
    [InlineData(GameId.P3, "as-drop", "game", true)]           // III's manual (D3-040)
    [InlineData(GameId.P3, "as-drop", "phantasie-i", false)]
    [InlineData(GameId.P3, "phantasie-i", "game", false)]      // replaces p3.purge = manual
    [InlineData(GameId.P2, "phantasie-i", "phantasie-iii", false)]
    public void Purge_OfAPartyMember_UnloadsOrKeepsTheElemental(GameId game, string purge, string drop, bool keeps)
    {
        var s = Guild(game, Opts(game, ("elemental-load", "phantasie-iii"), ("elemental-drop", drop), ("elemental-purge", purge)),
            members: 2);
        Assert.True(s.TryAddElemental(0, out var err), err);
        var first = s.Party.Members[0];
        Assert.True(s.TryPurge(first, out err), err);
        Assert.DoesNotContain(first, s.Roster.Occupied);
        Assert.DoesNotContain(first, s.Party.Members);
        Assert.Equal(keeps, s.Party.Elemental is not null);
    }

    [OriginalDataTheory]
    [InlineData(GameId.P1)]
    [InlineData(GameId.P2)]
    [InlineData(GameId.P3)]
    public void Purge_Refused_InEveryGame_RefusesAPartyMember(GameId game)
    {
        var s = Guild(game, Opts(game, ("elemental-purge", "refused")), members: 1);
        var member = s.Party.Members[0];
        var refusal = s.PurgeRefusal(member);
        Assert.Equal(GameText.S("core.guild.cannot_purge_party_member"), refusal);
        Assert.DoesNotContain("core.", refusal); // every game has the line
        Assert.False(s.TryPurge(member, out _));
        Assert.Contains(member, s.Roster.Occupied);
        Assert.True(s.TryDropFromParty(member, out var err), err);
        Assert.True(s.TryPurge(member, out err), err); // Drop first, then Purge
    }

    [OriginalDataFact]
    public void Purge_NotGame_ReplacesIIIsPurgeOption()
    {
        var dos = GameOptions.ForPreset(GameId.P3, OptionPreset.Dos);
        var s = Guild(GameId.P3, dos.With("elemental-purge", "as-drop"), members: 2);
        Assert.Null(s.PurgeRefusal(s.Party.Members[0])); // p3.purge = dos no longer refuses
        var t = Guild(GameId.P3, GameOptions.ForPreset(GameId.P3).With("elemental-purge", "refused"), members: 2);
        Assert.NotNull(t.PurgeRefusal(t.Party.Members[0])); // p3.purge = manual no longer purges
    }

    // ---- The invariant: an elemental is never removed from the game -------------------------------------------------

    public static TheoryData<GameId, string, string, string, string> EveryCombination()
    {
        var data = new TheoryData<GameId, string, string, string, string>();
        foreach (var game in GuildGames)
            foreach (var load in RuleOptionRegistry.ElementalLoad.Values)
                foreach (var drop in RuleOptionRegistry.ElementalDrop.Values)
                    foreach (var purge in RuleOptionRegistry.ElementalPurge.Values)
                        foreach (var p3 in game == GameId.P3 ? P3RuleOptions.Purge.Values : ["-"])
                            data.Add(game, load, drop, purge, p3);
        return data;
    }

    [OriginalDataTheory]
    [MemberData(nameof(EveryCombination))]
    public void NoGameOrOption_RemovesAnElementalFromTheGame(GameId game, string load, string drop, string purge, string p3)
    {
        var o = Opts(game, ("elemental-load", load), ("elemental-drop", drop), ("elemental-purge", purge));
        if (game == GameId.P3)
            o = o.With(P3RuleOptions.Purge.Id, p3);
        var s = Guild(game, o, members: 6);
        var templates = s.ElementalTemplates.Select(t => t.Name).ToArray();
        Assert.Equal(3, templates.Length);

        for (var e = 0; e < templates.Length; e++)
        {
            if (s.Party.Adventurers.Count() < 6)
                RefillParty(s);
            Assert.True(s.TryAddElemental(e, out var err), err);
            var loaded = s.Party.Elemental!;

            // An elemental itself is never purged: neither the party copy nor the Guild record.
            Assert.Equal(GameText.S("core.guild.cannot_purge_elemental"), s.PurgeRefusal(loaded));
            Assert.DoesNotContain("core.", s.PurgeRefusal(loaded));
            Assert.False(s.TryPurge(loaded, out _));
            Assert.Equal(GameText.S("core.guild.cannot_purge_elemental"), s.PurgeRefusal(s.ElementalTemplates[e]));
            Assert.False(s.TryPurge(s.ElementalTemplates[e], out _));
            Assert.Same(loaded, s.Party.Elemental);

            // Purge (or, when refused, Drop) every character: at most the party copy is unloaded.
            while (s.Party.Adventurers.Any())
            {
                var m = s.Party.Members[0];
                if (!s.TryPurge(m, out _))
                    Assert.True(s.TryDropFromParty(m, out err), err);
            }
            Assert.Null(s.Party.Elemental); // no character left: unloaded in every rule
            Assert.Equal(templates, s.ElementalTemplates.Select(t => t.Name));
        }

        // A record with an elemental's race that somehow sits in the roster can't be purged either.
        var stray = s.ElementalTemplates[0].Clone();
        var free = Array.IndexOf(s.Roster.Slots, null);
        Assert.True(free >= 0);
        s.Roster.Slots[free] = stray;
        Assert.False(s.TryPurge(stray, out _));
        Assert.Same(stray, s.Roster.Slots[free]);

        // Every elemental can still be loaded.
        RefillParty(s);
        for (var e = 0; e < templates.Length; e++)
        {
            Assert.True(s.TryAddElemental(e, out var err), err);
            Assert.Equal(templates[e], s.Party.Elemental!.Name);
            Assert.True(s.TryDropFromParty(s.Party.Members[0], out err), err);
            if (s.Party.Elemental is not null) // a Drop that keeps it: drop the rest to unload it
                while (s.Party.Adventurers.Any())
                    Assert.True(s.TryDropFromParty(s.Party.Members[0], out err), err);
            Assert.Null(s.Party.Elemental);
            RefillParty(s);
        }
    }

    private static int _refill;

    private static void RefillParty(GameSession s)
    {
        while (s.Party.Adventurers.Count() < 6)
        {
            var c = s.Roster.Occupied.FirstOrDefault(m => !s.Party.Members.Contains(m) && !GuildHall.IsElementalRecord(m))
                    ?? s.CreateAndEnroll("R" + (char)('A' + Interlocked.Increment(ref _refill) % 26)
                        + (char)('A' + _refill / 26 % 26), Race.Human, CharacterClass.Fighter);
            Assert.True(s.TryAddToParty(c, out var err), err);
        }
    }

    /// <summary>Returns 1 (the lowest) for every draw.</summary>
    private sealed class LowRng : IRandomSource
    {
        public int Next(int minInclusive, int maxExclusive) => minInclusive;
    }

    [OriginalDataFact]
    public void IV_DeletingRosterMembers_KeepsThePartysElemental_AndEveryElementalCanStillBePicked()
    {
        // IV picks its elemental (an index into the game's three) when the party is formed and has no Guild Add, Drop or
        // Purge of one, so the cross-game options are not in its tab; its roster Delete never touches the elemental.
        Assert.DoesNotContain(RuleOptionRegistry.For(GameId.P4), d => d.Id.StartsWith("elemental-", StringComparison.Ordinal));
        var battle = Phantasie4World.LoadBattleData();
        var names = battle.Elementals!.Select(e => e.Name).ToArray();
        Assert.Equal(3, names.Length);
        var party = new P4Party { Elemental = 2 };
        for (var i = 0; i < 4; i++)
        {
            var m = new P4Member
            {
                Name = "M" + i, Class = P4Ids.Fighter, Race = P4Ids.Human, Level = 1, HpMax = 20, Hp = 20,
                Abilities = [15, 15, 15, 15, 12, 10], AgingFactor = 200, Social = 3, MoveBase = 7, GuildSlot = i, Town = 2,
            };
            party.Roster[i] = m.Clone();
            party.Members.Add(m);
        }
        var w = new P4TownLoop(GameOptions.ForPreset(GameId.P4), GameTables.Load(GameId.P4).Phantasie4!.Tables,
            Phantasie4World.LoadAtlas(), party, new LowRng(), Phantasie4World.LoadServiceData());
        w.Arrive(2);
        while (w.Prompt == P4TownPrompt.Shares)
            w.EnterShares([.. party.Members.Select(_ => 1)]);
        w.Enter(P4Building.Guild);
        w.Choose(0); // G: roster
        for (var i = 0; i < 3; i++)
        {
            w.Choose(3); // D: delete
            w.Choose(0);
            w.Answer(true);
            w.Choose(-1);
        }
        Assert.Equal(2, party.Elemental);
        Assert.Equal(names, battle.Elementals!.Select(e => e.Name));

        for (var e = 0; e < names.Length; e++)
        {
            w.Choose(0); // P: form party
            w.Choose(0); // the one member left
            w.Choose(-1);
            w.Answer(true);
            w.Choose(e); // the elemental
            Assert.Equal(e, party.Elemental);
        }
    }
}
