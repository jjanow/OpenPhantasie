using OpenPhantasie.Client;
using OpenPhantasie.Core;

namespace OpenPhantasie.Client.Tests;

public class DefeatReportTests
{
    [Fact]
    public void ShortReport_IsKeptWhole()
    {
        var lines = DefeatReport.Fit(["THE TRAP IS GAS", "", " Hero DIES "]);
        Assert.Equal(["THE TRAP IS GAS", "Hero DIES"], lines);
    }

    [Fact]
    public void LongFightReport_KeepsEveryLine_s99()
    {
        var notes = new List<string> { GameSession.EncounterBanner, "Combat vs 4 ORC!" };
        for (var i = 0; i < 20; i++)
            notes.Add($"ORC HITS Hero {i}");
        notes.Add("Hero DIES");
        notes.Add("The party has been wiped out.");

        var lines = DefeatReport.Fit(notes);

        // s99 (user: never lose content to fit): no middle lines dropped; the prompt box scrolls instead.
        Assert.Equal(notes.Count, lines.Count);
        Assert.Equal([GameSession.EncounterBanner, "Combat vs 4 ORC!", "ORC HITS Hero 0"], lines.Take(3));
        Assert.Equal(["Hero DIES", "The party has been wiped out."], lines.TakeLast(2));
    }

    /// <summary>
    /// The s62 report: a lone level-1 fighter died to DNG1 wandering fights with no text. The round that wipes the
    /// party opens the defeat box at once, so the report comes from the session log since the step that started the fight.
    /// </summary>
    [OriginalDataFact]
    public void DungeonFightThatWipesTheParty_LogsWhatTheDefeatBoxShows()
    {
        var s = ClientSessions.Overworld();
        var hero = s.Party.Members[0];
        hero.HpCur = 1;
        s.Party.Members[1].Status = CharacterStatus.Dead;
        s.EnterDungeon(1, 20, 0);
        var mark = s.LogSequence;
        s.BeginDungeonCombat(ClientSessions.Data.Monsters.First(m => m.Name == "ORC").Id); // a DNG1 wandering fight
        s.FightToEnd();          // CombatPresenter's fast-forward path

        Assert.True(s.PartyDefeatPending);
        var lines = DefeatReport.Fit(s.LogSince(mark));
        Assert.Equal(GameSession.EncounterBanner, lines[0]);
        Assert.Contains($"{hero.Name} DIES", lines);
    }
}
