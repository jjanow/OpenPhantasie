using System.Text.RegularExpressions;
using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Core.Tests;

/// <summary>
/// s216 (watch list, s214 Apple III live): the status lines name towns by name ("Entered Asmith.", not "Entered town #8.")
/// and the encounter line words each rank as the games' own monster lists do: the name with "(s)" whatever the count
/// (I's Show names "RANK %1d : %1d  %s(S)", III DOS <c>0x110f5</c> / Amiga <c>0x1a4ea</c>, III manual pr.24 "2 GNOME(S)").
/// </summary>
public class StatusLineWordingTests
{
    private static readonly GameDataRepository Data = GameDataRepository.LoadPhantasie1();

    [OriginalDataFact]
    public void EnterTown_NamesTheTown()
    {
        var s = GameSessionFactory.Create(Data, new SeededDosRandomSource(7));
        s.StartNewGame();
        Assert.Equal("Entered PELNOR.", s.LastMessage);
        s.EnterTown(2);
        Assert.Contains($"Entered {Data.GetTown(2).Name}.", s.Log);
        Assert.DoesNotContain(s.Log, l => l.Contains("town #", StringComparison.Ordinal));
    }

    [OriginalDataFact]
    public void CombatLine_EachRankWithS_WhateverTheCount()
    {
        var s = GameSessionFactory.Create(Data, new SeededDosRandomSource(3));
        s.StartNewGame();
        s.OpenGuild();
        Assert.True(s.TryAddToParty(s.CreateAndEnroll("Alpha", Race.Human, CharacterClass.Fighter), out _));
        s.DepartGuildToWilderness();
        s.BeginWildernessCombat(0, SurpriseFlags.Normal);
        var expected = "Combat vs " + string.Join(", ", s.Combat!.Ranks.Select(r => $"{r.Members.Count} {r.Members[0].Name}(s)")) + "!";
        Assert.Contains(expected, s.Log);
        Assert.Matches(new Regex(@"^Combat vs \d+ [^,]+\(s\)(, \d+ [^,]+\(s\))*!$"), expected);
    }
}
