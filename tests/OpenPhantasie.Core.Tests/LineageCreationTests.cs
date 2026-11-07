using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Core.Tests;

/// <summary>
/// T5 lineage in I–III (<c>ancestry.md</c> section 13): the option, the pair rule, IV's inheritance through the target
/// game's creation and its clamp, the Hall list and <see cref="Character.Lineage"/> in each game's save.
/// </summary>
public class LineageCreationTests
{
    private static readonly Lazy<Phantasie4RuleTables> Iv = new(() =>
        Phantasie4RuleTables.Load(Path.Combine(GameDataRepository.DefaultPathFor(GameId.P4), Phantasie4RuleTables.FileName)));

    private static P4CreationTables Tables => Iv.Value.Tables.Creation;

    private static GameSession InGuild(GameId game, int seed = 4, bool lineage = true, IDosRandomSource? rng = null)
    {
        var s = GameSessionFactory.Create(GameDataRepository.Load(GameDataRepository.DefaultPathFor(game)),
            rng ?? new SeededDosRandomSource(seed));
        var options = GameOptions.ForPreset(game);
        s.StartNewGame(lineage ? options.WithModule(FeatureModuleRegistry.Lineage.Id, true) : options);
        s.OpenGuild();
        return s;
    }

    private static LineageSetup Setup(params LineagePerson[] persons) => new(Tables, persons);

    private static LineagePerson Person(string id, Race race, CharacterClass cls, params int[] abilities) =>
        new(id, id.ToUpperInvariant(), GameId.P3, race, (int)cls, null, abilities.Length == 5 ? abilities : [14, 14, 14, 14, 14]);

    private static LineagePerson Wood => Shipped(0);
    private static LineagePerson Filmon => Shipped(1);

    private static LineagePerson Shipped(int slot)
    {
        var h = Iv.Value.Hall[slot];
        return LineageRules.FromHall(slot, Iv.Value.HallNames[slot], h.Race, h.Class, h.Abilities, Iv.Value.Classes[h.Class])!;
    }

    // --- the option ---------------------------------------------------------------------------------------------------

    [Fact]
    public void Module_IsAnOptionInIToIII_NeverAModuleInIV_OffInEveryPreset()
    {
        var d = FeatureModuleRegistry.Lineage;
        foreach (var g in new[] { GameId.P1, GameId.P2, GameId.P3 })
        {
            Assert.True(d.IsOptionalIn(g));
            Assert.False(d.IsDefaultIn(g));
            foreach (var preset in new[] { OptionPreset.Manual, OptionPreset.Dos, OptionPreset.Remaster })
                Assert.DoesNotContain(d.Id, GameOptions.ForPreset(g, preset).Modules);
            var on = GameOptions.ForPreset(g).WithModule(d.Id, true);
            Assert.Equal(OptionPreset.Custom, on.Preset);
            Assert.Contains(d.Id, on.CreateModules().Modules.Select(m => m.Descriptor.Id));
        }
        // IV has the rule in its own creation: no switch there, and its module list is what it was.
        Assert.False(d.IsOptionalIn(GameId.P4));
        Assert.False(d.IsDefaultIn(GameId.P4));
        Assert.DoesNotContain(d.Id, GameOptions.ForPreset(GameId.P4).Modules);
        Assert.Throws<NotSupportedException>(() => GameOptions.ForPreset(GameId.P4).WithModule(d.Id, true));
    }

    [OriginalDataTheory]
    [InlineData(GameId.P1)]
    [InlineData(GameId.P2)]
    [InlineData(GameId.P3)]
    public void OptionOff_NotOffered_AndPlainCreationIsUnchanged(GameId game)
    {
        var off = InGuild(game, lineage: false);
        var on = InGuild(game, lineage: true);
        Assert.False(off.LineageOffered);
        Assert.True(on.LineageOffered);
        // The same seed rolls the same character whether the option is on (plain roll picked) or off.
        var a = off.RollNewMember(Race.Dwarf, CharacterClass.Priest);
        var b = on.RollNewMember(Race.Dwarf, CharacterClass.Priest);
        Assert.Null(a.Lineage);
        Assert.Null(b.Lineage);
        Assert.Equal(GuildCodec.WriteJson([a], game), GuildCodec.WriteJson([b], game));
    }

    // --- the pair rule ------------------------------------------------------------------------------------------------

    [Fact]
    public void Pair_SameRace_TakesThatRace_NewTakesTheOthers_BothNewTheMenuRace()
    {
        var orcA = Person("orc-a", Race.Orc, CharacterClass.Fighter);
        var orcB = Person("orc-b", Race.Orc, CharacterClass.Thief);
        Assert.Equal(new LineagePair(Race.Orc, LineageRefusal.None), LineageRules.Pair(GameId.P1, orcA, orcB, Race.Human));
        Assert.Equal(new LineagePair(Race.Orc, LineageRefusal.None), LineageRules.Pair(GameId.P2, null, orcB, Race.Human));
        Assert.Equal(new LineagePair(Race.Orc, LineageRefusal.None), LineageRules.Pair(GameId.P3, orcA, null, Race.Elf));
        Assert.Equal(new LineagePair(Race.Elf, LineageRefusal.None), LineageRules.Pair(GameId.P1, null, null, Race.Elf));
    }

    [OriginalDataFact]
    public void Pair_Refusals_Mixed_SamePerson_ShippedPair_NoBaseRace()
    {
        var orc = Person("orc", Race.Orc, CharacterClass.Fighter);
        var human = Person("human", Race.Human, CharacterClass.Fighter);
        Assert.Equal(LineageRefusal.MixedRaces, LineageRules.Pair(GameId.P1, orc, human, Race.Human).Refusal);
        Assert.Equal(LineageRefusal.MixedRaces, LineageRules.Pair(GameId.P3, Wood, orc, Race.Human).Refusal);
        Assert.Equal(LineageRefusal.SamePerson, LineageRules.Pair(GameId.P1, orc, orc, Race.Human).Refusal);
        Assert.Equal(LineageRefusal.BothShipped, LineageRules.Pair(GameId.P2, Wood, Filmon, Race.Human).Refusal);
        Assert.Equal(LineageRefusal.None, LineageRules.Pair(GameId.P2, Wood, human, Race.Human).Refusal);
        Assert.Equal(LineageRefusal.RaceNotOffered, LineageRules.Pair(GameId.P1, null, null, Race.Undead).Refusal);
        Assert.Equal(LineageRefusal.RaceNotOffered, LineageRules.Pair(GameId.P4, null, null, Race.Human).Refusal);
    }

    [OriginalDataFact]
    public void MixedPair_RollDescendantRefuses_AndEnrollsNothing()
    {
        var s = InGuild(GameId.P1);
        var orc = Person("orc", Race.Orc, CharacterClass.Fighter);
        Assert.Throws<InvalidOperationException>(() =>
            s.RollDescendant(Setup(orc, Wood), Wood, orc, Race.Human, CharacterClass.Fighter));
        Assert.Empty(s.Roster.Occupied);
    }

    [OriginalDataFact]
    public void HallList_BaseRacesOnly_NoNikademus_NoUndead_NotTheGamesOwnSave()
    {
        var undead = new Character { Name = "GHOUL", Race = Race.Undead, BirthRace = Race.Elf, Class = CharacterClass.Monk };
        var elf = new Character
        {
            Name = "ELWYN", Race = Race.Elf, Class = CharacterClass.Wizard, Stats = new CharacterStats(9, 17, 15, 11, 12, 10),
        };
        var import = new P4HallPerson(3, "Bruno", (int)Race.Dwarf, (int)CharacterClass.Fighter, 9, 2, 0, [17, 10, 12, 16, 9],
            [0, 0, 100], 0, 0, true);
        var unknownImport = import with { Slot = 4, Name = "Odd", Race = P4Hall.UnknownRace };
        var rosters = new Dictionary<GameId, IReadOnlyList<Character?>>
        {
            [GameId.P1] = [undead, null, elf],
            [GameId.P3] = [elf],
        };
        var forII = LineageHall.Build(Iv.Value, GameId.P2, rosters, [null, null, null, import, unknownImport]);
        Assert.Equal(["Lord Wood", "Filmon", "ELWYN", "ELWYN", "Bruno"], forII.Persons.Select(p => p.Name));
        Assert.Equal(["p4:hall-0", "p4:hall-1", "p1:guild-2", "p3:guild-0", "p4:hall-3"], forII.Persons.Select(p => p.Id));
        Assert.Equal("Sage", forII.Persons[1].ClassName); // Filmon: IV's class 8
        Assert.Equal(Race.Dwarf, forII.Persons[4].Race);
        Assert.Equal(3, forII.Persons[4].HallSlot);

        var forI = LineageHall.Build(Iv.Value, GameId.P1, rosters, []);
        Assert.DoesNotContain(forI.Persons, p => p.Source == GameId.P1); // the game's own Guild is not on its list
        Assert.Equal(3, forI.Persons.Count);
    }

    // --- abilities: IV's _cmbattr, then the target game's range -----------------------------------------------------

    [OriginalDataFact]
    public void Inherit_IsIVsCmbattr_NewAncestorsRolledByTheGamesOwnRollFirst()
    {
        var o = GameOptions.ForPreset(GameId.P1);
        var orc = Person("orc", Race.Orc, CharacterClass.Fighter, 19, 12, 14, 12, 6);
        var got = LineageRules.Inherit(o, Tables, new SeededRandomSource(9), Wood with { Race = Race.Orc }, orc, Race.Orc,
            CharacterClass.Fighter, () => throw new InvalidOperationException("no new ancestor"));
        var expected = P4Creation.Inherit(o, Tables, new SeededRandomSource(9), (Wood with { Race = Race.Orc }).ToAncestor(),
            orc.ToAncestor(), (int)Race.Orc, (int)CharacterClass.Fighter);
        Assert.Equal(expected, got);

        // A new ancestor: the game's own plain roll (here a fixed one) for the descendant's race and class.
        int[] plain = [12, 14, 13, 11, 10];
        var newOne = new P4Ancestor((int)Race.Elf, (int)CharacterClass.Ranger, plain);
        var elf = Person("elf", Race.Elf, CharacterClass.Wizard, 9, 17, 15, 11, 12);
        var calls = 0;
        Assert.Equal(
            P4Creation.Inherit(o, Tables, new SeededRandomSource(11), newOne, elf.ToAncestor(), (int)Race.Elf, (int)CharacterClass.Ranger),
            LineageRules.Inherit(o, Tables, new SeededRandomSource(11), null, elf, Race.Elf, CharacterClass.Ranger, () =>
            {
                calls++;
                return plain;
            }));
        Assert.Equal(1, calls);
    }

    [OriginalDataTheory]
    [InlineData(GameId.P1)]
    [InlineData(GameId.P3)]
    public void NewPlusNew_StaysNearTheGamesPlainRoll(GameId game)
    {
        // Section 13.3 point 1 (s149): a new ancestor is the game's own plain roll, so "new + new" is not far above a plain
        // roll (IV's _newattr gave I +16 on the five-sum; the game's own roll +4.2 in I, +4.7 in III).
        static int Sum(Character c) => c.Stats.Str + c.Stats.Int + c.Stats.Dex + c.Stats.Con + c.Stats.Cha;
        var s = InGuild(game, seed: 5);
        var setup = Setup();
        const int n = 300;
        double plain = 0, descendant = 0;
        for (var i = 0; i < n; i++)
        {
            plain += Sum(s.RollNewMember(Race.Human, CharacterClass.Fighter));
            descendant += Sum(s.RollDescendant(setup, null, null, Race.Human, CharacterClass.Fighter));
        }
        var gap = (descendant - plain) / n;
        Assert.InRange(gap, -2.0, 7.0);
    }

    [OriginalDataTheory]
    [InlineData(GameId.P1)]
    [InlineData(GameId.P2)]
    [InlineData(GameId.P3)]
    public void RollDescendant_GoesThroughTheGamesCreation_WithInheritedAbilitiesClamped(GameId game)
    {
        var a = InGuild(game, seed: 21);
        var twinRng = new SeededDosRandomSource(21);
        var b = InGuild(game, rng: twinRng);
        var orcA = Person("orc-a", Race.Orc, CharacterClass.Fighter, 18, 9, 13, 15, 7);
        var orcB = Person("orc-b", Race.Orc, CharacterClass.Thief, 15, 10, 17, 12, 8);
        var setup = Setup(orcA, orcB);

        var c = a.RollDescendant(setup, orcA, orcB, Race.Human, CharacterClass.Wizard);
        Assert.Equal(Race.Orc, c.Race);
        Assert.Equal(CharacterClass.Wizard, c.Class);
        Assert.True(c.HpMax > 0); // the game's own creation ran (HP, skills, kit)
        Assert.Equal("", c.Name);

        // Twin session, same seed and steps: IV's inheritance with the same draws, then the game's own range.
        var raw = LineageRules.Inherit(b.Options, Tables, twinRng, orcA, orcB, Race.Orc, CharacterClass.Wizard,
            () => throw new InvalidOperationException("no new ancestor"));
        int[] got = [c.Stats.Str, c.Stats.Int, c.Stats.Dex, c.Stats.Con, c.Stats.Cha];
        var creation = a.CreationTables;
        for (var i = 0; i < 5; i++)
        {
            var (lo, hi) = game == GameId.P3
                ? (Phantasie3Campaign.ClampAttribute(int.MinValue / 2, Mod(creation, CharacterClass.Wizard, i), RaceMod(creation, Race.Orc, i)),
                    Phantasie3Campaign.ClampAttribute(int.MaxValue / 2, Mod(creation, CharacterClass.Wizard, i), RaceMod(creation, Race.Orc, i)))
                : CharacterCreation.AttributeRange(Race.Orc, CharacterClass.Wizard, creation, i);
            Assert.Equal(Math.Clamp(raw[i], lo, hi), got[i]);
        }

        // Character.Lineage: both persons by their own game's ids and names.
        Assert.Equal(
            [new ParentRef("p3:11", "p3:3", "ORC-A"), new ParentRef("p3:11", "p3:0", "ORC-B")],
            c.Lineage!.Parents);
    }

    private static int Mod(CharacterCreationTables t, CharacterClass cls, int i) => t.ClassAttributeModifiers[(int)cls][i];

    private static int RaceMod(CharacterCreationTables t, Race race, int i) => t.RaceAttributeModifiers[(int)race][i];

    [OriginalDataFact]
    public void AttributeRange_I_IsTheRollsOwnFormulaAtBothEnds()
    {
        var t = InGuild(GameId.P1).CreationTables;
        // Human Fighter STR: q + 12 − 7 capped 18, + 0 → 5 … 18. Troll Wizard STR: q + 6 − 7 → −1 … 16, + 4 → 3 … 20.
        Assert.Equal((5, 18), CharacterCreation.AttributeRange(Race.Human, CharacterClass.Fighter, t, 0));
        Assert.Equal((3, 20), CharacterCreation.AttributeRange(Race.Troll, CharacterClass.Wizard, t, 0));
    }

    [OriginalDataFact]
    public void NewAndNew_UsesTheMenuRace_AndStoresTheGamesOwnIds()
    {
        var s = InGuild(GameId.P2);
        var c = s.RollDescendant(Setup(), null, null, Race.Gnome, CharacterClass.Monk);
        Assert.Equal(Race.Gnome, c.Race);
        Assert.Equal([new ParentRef("p2:4", "p2:1"), new ParentRef("p2:4", "p2:1")], c.Lineage!.Parents);
    }

    [OriginalDataFact]
    public void HallAncestor_Wood_IsStoredAsIVsPerson()
    {
        var s = InGuild(GameId.P3);
        var c = s.RollDescendant(Setup(Wood), Wood, null, Race.Elf, CharacterClass.Ranger);
        Assert.Equal(Race.Human, c.Race);
        Assert.Equal(new ParentRef("p4:0", "p4:2", "Lord Wood"), c.Lineage!.Parents[0]);
        Assert.Equal(new ParentRef("p3:0", "p3:2"), c.Lineage.Parents[1]);
    }

    /// <summary>
    /// The scale check behind section 13.3's note: descendants of two ordinary plain-rolled characters of the target game,
    /// against that game's plain roll, over many seeds. IV's dice run a little above I's and III's plain rolls (IV's
    /// plain roll is k = 0.6, mean raw ≈ 14), so a descendant of ordinary ancestors is never worse on average.
    /// </summary>
    [OriginalDataTheory]
    [InlineData(GameId.P1)]
    [InlineData(GameId.P3)]
    public void OrdinaryAncestors_GiveAtLeastAPlainRoll_OnAverage(GameId game)
    {
        var s = InGuild(game, seed: 1);
        double plain = 0, descendant = 0, newNew = 0;
        const int runs = 300;
        for (var i = 0; i < runs; i++)
        {
            var p1 = s.RollNewMember(Race.Human, CharacterClass.Fighter);
            var p2 = s.RollNewMember(Race.Human, CharacterClass.Fighter);
            p1.Name = "A";
            p2.Name = "B";
            var a1 = LineageRules.FromCharacter(p1, game, 0)!;
            var a2 = LineageRules.FromCharacter(p2, game, 1)!;
            plain += Sum(s.RollNewMember(Race.Human, CharacterClass.Fighter));
            descendant += Sum(s.RollDescendant(Setup(a1, a2), a1, a2, Race.Human, CharacterClass.Fighter));
            newNew += Sum(s.RollDescendant(Setup(), null, null, Race.Human, CharacterClass.Fighter));
        }
        plain /= runs;
        descendant /= runs;
        newNew /= runs;
        Assert.True(descendant >= plain - 1, $"{game}: plain {plain:F2}, descendant {descendant:F2}, new+new {newNew:F2}");
        Assert.True(newNew >= plain - 1, $"{game}: plain {plain:F2}, descendant {descendant:F2}, new+new {newNew:F2}");
        Console.WriteLine($"lineage scale {game}: plain {plain:F2}, ordinary pair {descendant:F2}, new+new {newNew:F2} (sum of 5)");
    }

    private static int Sum(Character c) => c.Stats.Str + c.Stats.Int + c.Stats.Dex + c.Stats.Con + c.Stats.Cha;

    [OriginalDataFact]
    public void Lineage_SurvivesTheIAndIITransfers()
    {
        var s = InGuild(GameId.P1);
        var c = s.RollDescendant(Setup(Wood), Wood, null, Race.Human, CharacterClass.Fighter);
        c.Name = "HEIR";
        var inII = TransferIToII.Convert(c, TransferIToIIRules.St);
        Assert.Equal(c.Lineage!.Parents, inII.Lineage!.Parents);
        Assert.Equal(c.Lineage.Parents, TransferIIToI.Convert(inII).Lineage!.Parents);
    }

    // --- Character.Lineage in each game's remaster save ---------------------------------------------------------------

    [OriginalDataTheory]
    [InlineData(GameId.P1)]
    [InlineData(GameId.P2)]
    [InlineData(GameId.P3)]
    public void Lineage_RoundTripsThroughTheGamesSave(GameId game)
    {
        var data = GameDataRepository.Load(GameDataRepository.DefaultPathFor(game));
        var s = InGuild(game);
        var c = s.RollDescendant(Setup(Wood), Wood, null, Race.Human, CharacterClass.Priest);
        Assert.True(s.TryEnrollNewMember(c, "HEIR", out var err), err);
        var snap = SessionCodec.ReadJson(SessionCodec.WriteJson(s.CaptureSnapshot()));
        var loaded = GameSessionFactory.Create(data, new SeededDosRandomSource(1));
        loaded.ApplySnapshot(snap);
        var back = Assert.Single(loaded.Roster.Occupied);
        Assert.Equal(c.Lineage!.Parents, back.Lineage!.Parents);
        Assert.Equal("Lord Wood", back.Lineage.Parents[0].Name);
        Assert.Null(back.Lineage.Parents[1].Name);
    }
}
