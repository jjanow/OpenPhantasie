using Microsoft.Xna.Framework.Input;
using OpenPhantasie.Client;
using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client.Tests;

/// <summary>T5 lineage on Guild New (<see cref="GuildNewDialog"/>, <c>ancestry.md</c> section 13): the descendant path.</summary>
public class LineageDialogTests
{
    private static readonly Lazy<Phantasie4RuleTables> Iv = new(() =>
        Phantasie4RuleTables.Load(Path.Combine(GameDataRepository.DefaultPathFor(GameId.P4), Phantasie4RuleTables.FileName)));

    private static GameSession InGuild(GameId game = GameId.P1, bool lineage = true)
    {
        var data = game == GameId.P1 ? ClientSessions.Data : GameDataRepository.Load(GameDataRepository.DefaultPathFor(game));
        var s = GameSessionFactory.Create(data, new SeededDosRandomSource(4));
        var o = GameOptions.ForPreset(game);
        s.StartNewGame(lineage ? o.WithModule(FeatureModuleRegistry.Lineage.Id, true) : o);
        s.OpenGuild();
        return s;
    }

    private static LineagePerson Orc(int n) =>
        new($"p3:guild-{n}", $"GROK{n}", GameId.P3, Race.Orc, (int)CharacterClass.Fighter, null, [17, 8, 12, 15, 6]);

    /// <summary>The Hall list of the shipped data (Lord Wood, Filmon) plus <paramref name="extra"/>.</summary>
    private static LineageSetup Setup(params LineagePerson[] extra)
    {
        var shipped = LineageHall.Build(Iv.Value, GameId.P1, new Dictionary<GameId, IReadOnlyList<Character?>>(), []);
        return new LineageSetup(shipped.Tables, [.. shipped.Persons, .. extra]);
    }

    private static Keys[] Type(string text) => [.. text.Select(c => Keys.A + (char.ToUpperInvariant(c) - 'A'))];

    /// <summary>One key per call, as the frame loop delivers them (a step change ends a call's keys).</summary>
    private static void Press(GuildNewDialog d, GameSession s, Keys[] keys)
    {
        foreach (var k in keys)
            d.HandleKeys([k], s);
    }

    private static IReadOnlyList<string> Lines(GuildNewDialog d, GameSession s) => d.Lines(s, ClientSessions.Data);

    [OriginalDataFact]
    public void OptionOff_OpensAtTheRaceMenu_AsBefore()
    {
        var s = InGuild(lineage: false);
        var d = new GuildNewDialog(_ => Setup());
        d.Open(s);
        Assert.Equal("SELECT RACE", Lines(d, s)[0]);
    }

    [OriginalDataFact]
    public void OptionOn_AsksPlainOrDescendant_PlainIsTheUsualRoll()
    {
        var s = InGuild();
        var d = new GuildNewDialog(_ => Setup());
        d.Open(s);
        Assert.Equal(["NEW MEMBER", "1 PLAIN ROLL", "2 DESCENDANT OF TWO ANCESTORS"], Lines(d, s).Take(3));
        Press(d, s, [Keys.D1]);
        Assert.Equal("SELECT RACE", Lines(d, s)[0]);
        Press(d, s, [Keys.D3, Keys.D6, Keys.Y]);
        Press(d, s, [.. Type("plain"), Keys.Enter]);
        Assert.Null(Assert.Single(s.Roster.Occupied).Lineage);
    }

    [OriginalDataFact]
    public void Descendant_WoodAndANewAncestor_ClassRollNameEnroll()
    {
        var s = InGuild();
        var d = new GuildNewDialog(_ => Setup());
        d.Open(s);
        Press(d, s, [Keys.D2]);
        var list = Lines(d, s);
        Assert.Equal("FIRST ANCESTOR", list[0]);
        Assert.Equal("> NEW ANCESTOR (ROLLED NOW)", list[1]);
        Assert.Equal("  LORD WOOD  HUMAN RANGER  (IV HALL)", list[2]);
        Assert.Equal("  FILMON  HUMAN SAGE  (IV HALL)", list[3]);
        Assert.DoesNotContain(list, l => l.Contains("NIKADEMUS", StringComparison.Ordinal));
        Press(d, s, [Keys.Down, Keys.Enter]); // Lord Wood
        Assert.Equal("SECOND ANCESTOR", Lines(d, s)[0]);
        Assert.Contains("FIRST ANCESTOR: LORD WOOD", Lines(d, s));
        Press(d, s, [Keys.Enter]); // a new ancestor: Human, as Wood
        Assert.Equal("SELECT CLASS", Lines(d, s)[0]);
        Press(d, s, [Keys.D3]); // RANGER
        var accept = Lines(d, s);
        Assert.Equal("RANGER HUMAN", accept[0]);
        Assert.Contains("ANCESTORS: LORD WOOD, NEW", accept);
        Assert.Equal("ACCEPT? (Y/N)", accept[^1]);
        Press(d, s, [Keys.Y, .. Type("heir"), Keys.Enter]);
        var heir = Assert.Single(s.Roster.Occupied);
        Assert.Equal("HEIR", heir.Name);
        Assert.Equal(Race.Human, heir.Race);
        Assert.Equal("Lord Wood", heir.Lineage!.Parents[0].Name);
    }

    [OriginalDataFact]
    public void Descendant_MixedPair_IsRefusedWithAMessage_AndAsksAgain()
    {
        var s = InGuild();
        var d = new GuildNewDialog(_ => Setup(Orc(1)));
        d.Open(s);
        Press(d, s, [Keys.D2, Keys.Down, Keys.Enter]); // Lord Wood
        Press(d, s, [Keys.Down, Keys.Down, Keys.Down, Keys.Enter]); // GROK1, an Orc
        var lines = Lines(d, s);
        Assert.Equal("SECOND ANCESTOR", lines[0]);
        Assert.Equal("NOT POSSIBLE: THE RACES DIFFER.", lines[^1]);
        Press(d, s, [Keys.Up, Keys.Enter]); // Filmon: two of the Hall's own heroes
        Assert.Equal("NOT POSSIBLE: TWO OF THE HALL'S OWN HEROES.", Lines(d, s)[^1]);
        Press(d, s, [Keys.Up, Keys.Enter]); // Lord Wood again
        Assert.Equal("NOT POSSIBLE: THE SAME PERSON TWICE.", Lines(d, s)[^1]);
        Assert.Empty(s.Roster.Occupied);
    }

    [OriginalDataFact]
    public void Descendant_OrcPersonAndNew_IsAnOrc_InIII()
    {
        var s = InGuild(GameId.P3);
        var d = new GuildNewDialog(_ => Setup(Orc(1)));
        d.Open(s);
        Press(d, s, [Keys.D2, Keys.Enter]); // a new ancestor first
        Press(d, s, [Keys.Down, Keys.Down, Keys.Down, Keys.Enter]); // GROK1
        Assert.Equal("SELECT CLASS", Lines(d, s)[0]);
        Press(d, s, [Keys.D4]);
        Assert.StartsWith("FIGHTER ORC", Lines(d, s)[0], StringComparison.Ordinal);
        Assert.Contains("ANCESTORS: NEW, GROK1", Lines(d, s));
    }

    [OriginalDataFact]
    public void Descendant_BothNew_AsksTheRace_BaseFiveOnly()
    {
        var s = InGuild(GameId.P2);
        var d = new GuildNewDialog(_ => Setup());
        d.Open(s);
        Press(d, s, [Keys.D2, Keys.Enter, Keys.Enter]);
        var race = Lines(d, s);
        Assert.Equal("SELECT RACE", race[0]);
        Assert.DoesNotContain(race, l => l.Contains("RANDOM", StringComparison.Ordinal));
        Press(d, s, [Keys.D6]); // no RANDOM here
        Assert.Equal("SELECT RACE", Lines(d, s)[0]);
        Press(d, s, [Keys.D5, Keys.D2]); // GNOME MONK
        Assert.StartsWith("MONK GNOME", Lines(d, s)[0], StringComparison.Ordinal);
        Press(d, s, [Keys.N]); // N: back to the first question, both ancestors dropped
        Assert.Equal("NEW MEMBER", Lines(d, s)[0]);
    }

    [OriginalDataFact]
    public void LongHallList_Pages_AndKeepsEveryRow()
    {
        var s = InGuild();
        var extra = Enumerable.Range(1, 20).Select(Orc).ToArray();
        var d = new GuildNewDialog(_ => Setup(extra));
        d.Open(s);
        Press(d, s, [Keys.D2]);
        Lines(d, s);
        Assert.Equal(new ListPage(1, GuildNewDialog.AncestorRows, 0, 23), d.Page);
        for (var i = 0; i < 22; i++)
            Press(d, s, [Keys.Down]);
        var lines = Lines(d, s);
        Assert.Equal(new ListPage(1, GuildNewDialog.AncestorRows, 23 - GuildNewDialog.AncestorRows, 23), d.Page);
        Assert.Equal("> GROK20  ORC FIGHTER  (III)", lines[GuildNewDialog.AncestorRows]);
    }

    [OriginalDataFact]
    public void NoIVData_StaysOnTheQuestion_WithAMessage()
    {
        var s = InGuild();
        var d = new GuildNewDialog(_ => throw new FileNotFoundException("rule-tables.json"));
        d.Open(s);
        Press(d, s, [Keys.D2]);
        Assert.Equal("NEW MEMBER", Lines(d, s)[0]);
        Assert.Equal("IV'S DATA IS MISSING: NO DESCENDANTS.", Lines(d, s)[^1]);
        Press(d, s, [Keys.D1]);
        Assert.Equal("SELECT RACE", Lines(d, s)[0]);
    }
}
