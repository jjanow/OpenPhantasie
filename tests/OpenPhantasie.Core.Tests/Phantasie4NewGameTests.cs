using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Core.Tests;

/// <summary>
/// Phantasie IV's new game and the creation screens (s106; <c>FORMAT.md</c> §6.1, <c>ancestry.md</c> §10): Pendragon with
/// an empty party, Guild roster N, plain characters and descendants, saved ancestors and Hall counts.
/// </summary>
public class Phantasie4NewGameTests
{
    private static readonly GameOptions Manual = GameOptions.ForPreset(GameId.P4);
    private static readonly GameOptions Code = GameOptions.ForPreset(GameId.P4, OptionPreset.Dos);

    /// <summary>Returns the scripted rand results (1..n) in order, then 1 (the lowest) for every later draw; counts draws.</summary>
    private sealed class ScriptRng(params int[] results) : IRandomSource
    {
        private readonly Queue<int> _q = new(results);

        public int Draws { get; private set; }

        public int Next(int minInclusive, int maxExclusive)
        {
            Draws++;
            return _q.Count > 0 ? Math.Clamp(_q.Dequeue(), minInclusive, maxExclusive - 1) : minInclusive;
        }
    }

    private static int Find(P4TownLoop w, string id)
    {
        var text = GameText.S(id);
        for (var i = 0; i < w.Choices.Count; i++)
        {
            if (w.Choices[i].Text.Contains(text, StringComparison.Ordinal))
                return i;
        }
        throw new InvalidOperationException($"No choice {text}.");
    }

    /// <summary>A new game, into the Guild roster, and N: the "descendant of a mixed race?" question is open.</summary>
    private static Phantasie4Campaign AtQuestion(GameOptions o, IRandomSource rng)
    {
        var game = Phantasie4World.NewGame(o, rng);
        var w = game.Town;
        w.Enter(P4Building.Guild);
        w.Choose(0); // G: Guild roster
        w.Choose(1); // N: Make a character
        Assert.Equal(P4TownPrompt.Pick, w.Prompt);
        Assert.Equal(3, w.Choices.Count); // E: Finish creating, Yes, No
        return game;
    }

    /// <summary>No → Human → <paramref name="cls"/> → Okay → name: a plain character from the question.</summary>
    private static void MakePlain(P4TownLoop w, string name, int classIndex = 3)
    {
        w.Choose(2);             // No: a plain character
        w.Choose(P4Ids.Human);   // race menu
        w.Choose(classIndex);    // class menu (Human: Thief, Monk, Ranger, Fighter, Priest, Wizard)
        Assert.Equal(P4TownPrompt.Confirm, w.Prompt);
        Assert.NotNull(w.CreationSheet);
        w.Answer(true);          // Okay
        Assert.Equal(P4TownPrompt.Text, w.Prompt);
        Assert.Equal(8, w.TextLimit);
        w.EnterText(name);
        Assert.Equal(P4TownPrompt.Wait, w.Prompt); // the sheet with the skills
        w.Continue();
        Assert.Equal(3, w.Choices.Count);          // the question again
    }

    [OriginalDataFact]
    public void NewGame_StartsInPendragon_WithAnEmptyParty_AndOnlyTheGuildOpen()
    {
        var game = Phantasie4World.NewGame(Manual, new ScriptRng());
        Assert.True(game.LoopBuilt);
        Assert.Equal(new P4Location(P4Place.Town, P4TownLoop.Pendragon, 0, 0), game.Location);
        Assert.Equal(P4TownPrompt.Menu, game.Town.Prompt);
        Assert.Empty(game.Party.Members);
        Assert.All(game.Party.Roster, m => Assert.Null(m));
        Assert.Equal([P4Building.Guild], game.Town.Buildings); // _townmenu: one door and the System icon
        Assert.True(game.CanSave);
    }

    [OriginalDataFact]
    public void PlainCharacter_GoesIntoTheFirstFreeSlot_WithNoAncestors()
    {
        var game = AtQuestion(Manual, new SeededRandomSource(7));
        var w = game.Town;
        MakePlain(w, "BRAND");
        var m = game.Party.Roster[0]!;
        Assert.Equal("BRAND", m.Name);
        Assert.Equal((P4Ids.Human, P4Ids.Fighter, 1), (m.Race, m.Class, m.Level));
        Assert.Equal((P4TownLoop.Pendragon, 0), (m.Town, m.GuildSlot));
        Assert.All(m.Ancestors, a => Assert.Equal(P4AncestorKind.None, a.Kind));
        Assert.Equal(P4Creation.Origin(P4TownLoop.Pendragon), m.Origin);
        Assert.InRange(m.Abilities[P4Member.Luck], 9, 14);
        Assert.True(m.HpMax > 0);
        // The loop goes on: a second character takes slot 1, then E leaves to the roster.
        MakePlain(w, "ODO", classIndex: 4);
        Assert.Equal(P4Ids.Priest, game.Party.Roster[1]!.Class);
        w.Choose(0); // E: Finish creating
        Assert.Equal(2, w.RosterList.Count);
        Assert.Null(w.CreationSheet);
        Assert.Empty(w.Box); // the question is gone from the roster screen
    }

    [OriginalDataFact]
    public void Cansel_KeepsNothing_AndBackOnTheRaceMenuLeavesTheRollsUnmade()
    {
        var rng = new ScriptRng();
        var game = AtQuestion(Manual, rng);
        var w = game.Town;
        w.Choose(2);
        w.Choose(P4Ids.BaseRaces + 1); // 7: Back
        Assert.Equal(3, w.Choices.Count);
        Assert.Equal(0, rng.Draws);
        w.Choose(2);
        w.Choose(P4Ids.Elf);
        w.Choose(5); // Wizard
        Assert.Equal(15, rng.Draws); // five abilities × three dice
        w.Answer(false); // Cansel: back to the question, nothing made (choosing again is the re-roll, manual p. 9)
        Assert.Equal(3, w.Choices.Count);
        Assert.All(game.Party.Roster, m => Assert.Null(m));
    }

    [OriginalDataFact]
    public void RandomCreature_OffersTheRolledList()
    {
        var rng = new ScriptRng(50); // 50 ≥ 26: the common list (Pixie … Minotaur)
        var w = AtQuestion(Manual, rng).Town;
        w.Choose(2);
        w.Choose(P4Ids.BaseRaces); // 6: Random Creature
        Assert.Equal(8, w.Choices.Count);
        w.Choose(6); // the seventh: Lizard Man
        Assert.Equal(2, w.Choices.Count); // Thief, Fighter
    }

    [OriginalDataFact]
    public void Descendant_OfLordWood_AndANewHuman_CountsForTheHall()
    {
        // H: Lord Wood (Hall slot 0, Human). S: rand(100) = 30 ≤ 50, so ancestor 2 is a new Human; every later draw lowest.
        var rng = new ScriptRng(30);
        var game = AtQuestion(Manual, rng);
        var w = game.Town;
        var wood = Phantasie4World.LoadServiceData().Hall[0];
        Assert.Equal(wood.Descendants, w.HallDescendants(wood));
        w.Choose(1);            // Yes: a descendant
        w.Choose(1);            // H: a person from the Hall
        w.Choose(0);            // A: Lord Wood
        Assert.Equal(wood.Name, w.FirstAncestorSheet!.Name);
        w.Answer(true);         // Okay
        w.Choose(0);            // S: same race
        Assert.Equal(P4TownPrompt.Wait, w.Prompt); // "Race: Human"
        Assert.Equal(P4Ids.Human, w.SecondAncestorSheet!.Race);
        w.Continue();
        w.Choose(3);            // ancestor 2: Fighter
        w.Choose(3);            // the descendant: Fighter
        Assert.Equal(P4TownPrompt.Confirm, w.Prompt);
        Assert.Equal(P4Ids.Human, w.CreationSheet!.Race);
        w.Answer(true);
        w.EnterText("WOODSON");
        w.Continue();
        var m = game.Party.Roster[0]!;
        Assert.Equal(new P4AncestorRef(P4AncestorKind.Hall, 0), m.Ancestors[0]);
        Assert.Equal(P4AncestorKind.New, m.Ancestors[1].Kind);
        Assert.Equal((1, P4Ids.Human, P4Ids.Fighter), (m.Ancestors[1].Index, m.Ancestors[1].Race, m.Ancestors[1].Class));
        Assert.Equal(wood.Descendants + 1, w.HallDescendants(wood));
    }

    [OriginalDataFact]
    public void Code_Nikademus_RollsTheRaceFirst_ThenSaysNotPossible()
    {
        var rng = new ScriptRng();
        var w = AtQuestion(Code, rng).Town;
        w.Choose(1);
        w.Choose(1);
        w.Choose(2);            // C: Nikademus
        w.Answer(true);
        w.Choose(0);            // S
        Assert.Equal(P4TownPrompt.Wait, w.Prompt);
        Assert.Contains(GameText.S("p4x.0x42cf2"), w.Box);
        Assert.Equal(1, rng.Draws); // _mopta's rand(100) comes before the test
        w.Continue();
        Assert.Equal(2, w.Choices.Count); // back to N / H for ancestor 1
        w.Choose(1);
        w.Choose(2);
        w.Answer(true);
        w.Choose(2);            // H: refused before the Hall list
        Assert.Equal(P4TownPrompt.Wait, w.Prompt);
        Assert.Contains(GameText.S("p4x.0x42cf2"), w.Box);
    }

    [OriginalDataFact]
    public void FullRoster_SaysSo_AndReturns()
    {
        var game = Phantasie4World.NewGame(Manual, new ScriptRng());
        for (var i = 0; i < P4Party.RosterSlots; i++)
            game.Party.Roster[i] = new P4Member { Name = $"M{i}", GuildSlot = i };
        var w = game.Town;
        w.Enter(P4Building.Guild);
        w.Choose(0);
        w.Choose(1);
        Assert.Equal(P4TownPrompt.Wait, w.Prompt);
        Assert.Contains(GameText.S("p4x.0x42935"), w.Box);
        w.Continue();
        Assert.Equal(5, w.Choices.Count); // the roster's buttons again
    }

    [OriginalDataFact]
    public void NewGame_ToAPartyOutsideTheGate_AndTheSaveKeepsAncestorsAndHallCounts()
    {
        var game = AtQuestion(Manual, new SeededRandomSource(11));
        var w = game.Town;
        MakePlain(w, "BRAND");
        // A descendant of Filmon (Hall slot 1) and a new person.
        w.Choose(1);
        w.Choose(1);
        w.Choose(1);            // B: Filmon
        w.Answer(true);
        w.Choose(0);            // S
        w.Continue();
        w.Choose(0);
        w.Choose(0);
        w.Answer(true);
        w.EnterText("PIP");
        w.Continue();
        w.Choose(0);            // E: Finish creating
        Assert.Equal(2, w.RosterList.Count);
        var filmon = game.Services!.Hall[1];
        Assert.Equal(filmon.Descendants + 1, w.HallDescendants(filmon));

        // P: form the party of both, no elemental; leave the Guild; the town's doors open; out of the gate.
        w.Choose(0);
        w.Choose(0);
        w.Choose(1);
        w.Choose(-1);
        w.Answer(true);
        w.Choose(3);
        w.Choose(-1);
        w.Choose(Find(w, "p4x.0x3fa9c")); // L: leave the Guild
        Assert.Equal(2, game.Party.Members.Count);
        Assert.Contains(P4Building.Leave, w.Buildings);
        // _tload restocks only with a party (_igen8 after the party-size test), and the party was formed inside the
        // Guild: the first party of a new game finds the shop empty until the next town entry, as in the original.
        Assert.All(game.Party.ShopStock, n => Assert.Equal(0, n));

        // Saved from the town menu (not inside a building).
        var json = Phantasie4SaveCodec.WriteJson(game.Capture());
        var back = Phantasie4World.RestoreCampaign(Phantasie4SaveCodec.ReadJson(json), new ScriptRng());
        Assert.Equal(game.Party.Roster[1]!.Ancestors, back.Party.Roster[1]!.Ancestors);
        Assert.Equal(filmon.Descendants + 1, back.Town.HallDescendants(filmon));

        w.Enter(P4Building.Leave);
        game.Follow();
        Assert.Equal(P4Place.Outdoors, game.Place);
    }
}
