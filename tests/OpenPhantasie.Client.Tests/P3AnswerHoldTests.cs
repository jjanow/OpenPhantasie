using Microsoft.Xna.Framework.Input;
using OpenPhantasie.Client;
using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client.Tests;

/// <summary>
/// III's Encounter-menu answers (s214, <c>encounters.md</c> section 4 "How an answer is shown"): DOS root <c>0x11376</c> /
/// the Amiga's <c>0x1a676</c> print the answer and wait before the fight ends or goes on; the presenter holds the line
/// <see cref="CombatPresenter.AnswerTicks"/> at the COMBAT SPEED instead of dropping it for the XP line.
/// </summary>
public class P3AnswerHoldTests
{
    private static readonly GameDataRepository P3Data = GameDataRepository.Load(GameDataRepository.DefaultPathFor(GameId.P3));

    private sealed class Switchable(int seed) : IDosRandomSource
    {
        private readonly SeededDosRandomSource _inner = new(seed);
        public bool Low { get; set; }
        public int Next(int minInclusive, int maxExclusive) => Low ? minInclusive : _inner.Next(minInclusive, maxExclusive);
        public int NextDosWord() => Low ? 0 : _inner.NextDosWord();
    }

    /// <summary>A III party of two meeting Skeletons (id 0, reaction −10), the presenter at the round's top.</summary>
    private static (GameSession, CombatPresenter, Switchable) AtPartyOptions(bool amigaField)
    {
        var rng = new Switchable(7);
        var s = GameSessionFactory.Create(P3Data, rng);
        s.StartNewGame(null);
        s.OpenGuild();
        foreach (var (name, cls) in new[] { ("HERO0", CharacterClass.Fighter), ("HERO1", CharacterClass.Priest) })
            Assert.True(s.TryAddToParty(s.CreateAndEnroll(name, Race.Human, cls), out var err), err);
        s.LeaveGuild();
        s.LeaveTown();
        s.BeginCombat(0, SurpriseFlags.Normal);
        var ui = new CombatPresenter(new ClientSettings()) { AmigaField = () => amigaField, AmigaOrders = () => amigaField };
        ui.Start(s);
        Assert.Equal(CombatPhase.PartyOptions, ui.Phase);
        return (s, ui, rng);
    }

    private static void Greet(GameSession s, CombatPresenter ui, bool amigaField)
    {
        if (amigaField)
        {
            ui.HandleKey(s, Keys.G);
            return;
        }
        while (CombatText.PartyOptionMenu[ui.OptionCursor].Option != PartyOption.Greetings)
            ui.HandleKey(s, Keys.Down);
        ui.HandleKey(s, Keys.Enter);
    }

    private static double AnswerMs => CombatFieldRun.TicksMs(CombatPresenter.AnswerTicks, AmigaCombatSpeed.Normal);

    [OriginalDataFact]
    public void Greetings_Accepted_TheLineStays_ThenTheFightEnds()
    {
        var (s, ui, rng) = AtPartyOptions(amigaField: false);
        rng.Low = true; // rnd(100) = 1 < reaction + 40: greeted
        Greet(s, ui, amigaField: false);
        Assert.Null(s.Combat); // Core has ended the fight
        Assert.True(ui.IsPresenting); // ... the presenter still shows the answer
        Assert.True(ui.AnswerHeld);
        var greeting = GameText.S("core.p3.greetings");
        Assert.Equal(greeting, ui.LastLine);
        Assert.False(ui.Update(s, AnswerMs - 50, fastForward: false));
        Assert.Equal(greeting, ui.LastLine);
        Assert.True(ui.Update(s, 100, fastForward: false));
        Assert.False(ui.IsPresenting);
        Assert.False(ui.AnswerHeld);
        Assert.NotEqual(greeting, ui.LastLine); // the XP line follows on the map's line, as before
    }

    [OriginalDataFact]
    public void Greetings_Accepted_FastForward_EndsTheHold()
    {
        var (s, ui, rng) = AtPartyOptions(amigaField: false);
        rng.Low = true;
        Greet(s, ui, amigaField: false);
        Assert.True(ui.Update(s, 0, fastForward: true)); // no FightToEnd on an ended fight
        Assert.False(ui.IsPresenting);
        Assert.Null(s.Combat);
    }

    [OriginalDataFact]
    public void Greetings_Accepted_AmigaField_PlaysTheLineOnTheField_ForFortyTicks()
    {
        var (s, ui, rng) = AtPartyOptions(amigaField: true);
        rng.Low = true;
        Greet(s, ui, amigaField: true);
        Assert.NotNull(ui.FieldRun);
        var line = Assert.Single(ui.FieldRun!.Lines);
        Assert.Equal(GameText.S("core.p3.greetings"), line.Text);
        Assert.Equal(AnswerMs, line.ShowMs); // 0x1bacc(40): 0.8 s at Normal
        Assert.Equal(line.Text, ui.FieldShown?.Line?.Text);
        Assert.False(ui.Update(s, AnswerMs / 2, fastForward: false));
        Assert.True(ui.Update(s, AnswerMs, fastForward: false));
        Assert.Null(ui.FieldRun);
        Assert.False(ui.IsPresenting);
    }

    [OriginalDataFact]
    public void Greetings_Refused_NoResponseStays_ThenTheOrders()
    {
        var (s, ui, _) = AtPartyOptions(amigaField: false);
        s.Combat!.MonsterDamaged = true; // FAR:0xb618: a hurt monster never greets
        Greet(s, ui, amigaField: false);
        Assert.NotNull(s.Combat);
        Assert.Equal(GameText.S("core.p3.no_response"), ui.LastLine);
        Assert.Equal(CombatPhase.Resolving, ui.Phase);
        Assert.False(ui.Update(s, AnswerMs + 1, fastForward: false));
        Assert.Equal(CombatPhase.Orders, ui.Phase);
        Assert.False(ui.AnswerHeld);
    }
}
