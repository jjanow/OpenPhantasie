using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client.Tests;

/// <summary>
/// "Pad coverage" (s182, <c>P4DungeonScreen.Pad.cs</c>): IV's screens take the pad as the keys each prompt reads: A / B
/// follow the prompt (Return, Y, Space; Esc, N, Space, Backspace), LB / RB are F5 / F9, Y / X pick the clickable buttons
/// and A clicks the picked one, and in a fight's orders Y / X pick the order A gives.
/// </summary>
public class P4PadTests
{
    private static readonly GameOptions Code = GameOptions.ForPreset(GameId.P4, OptionPreset.Dos);
    private static readonly GameOptions Manual = GameOptions.ForPreset(GameId.P4);
    private static readonly P4Tables T = GameTables.Load(GameId.P4).Phantasie4!.Tables;

    private static Keys KeyOf(P4ActionKind kind) => kind switch
    {
        P4ActionKind.AimBlow => Keys.M, P4ActionKind.Lunge => Keys.L, P4ActionKind.Attack => Keys.A,
        P4ActionKind.Thrust => Keys.T, P4ActionKind.Slash => Keys.S, P4ActionKind.FireBow => Keys.F,
        P4ActionKind.Throw => Keys.H, P4ActionKind.Parry => Keys.P, P4ActionKind.Cast => Keys.C,
        _ => Keys.None,
    };

    private static IReadOnlyList<Keys> Pad(P4DungeonScreen s, params Buttons[] pressed) =>
        s.PadKeys(pressed, [], [], out _);

    /// <summary>The pad's keys for <paramref name="pressed"/>, then those keys into the screen.</summary>
    private static P4ScreenRequest? Press(P4DungeonScreen s, params Buttons[] pressed) => s.HandleKeys(Pad(s, pressed));

    private static P4Party Party()
    {
        var p = new P4Party();
        foreach (var name in new[] { "ANN", "BOB" })
        {
            var m = new P4Member { Name = name, Level = 3, HpMax = 20, Hp = 20, Defence = 10 };
            Array.Fill(m.Skills, 50);
            p.Members.Add(m);
        }
        return p;
    }

    /// <summary>An open map whose load event is <paramref name="code"/> (as <c>P4DungeonScreenTests.Synthetic</c>).</summary>
    private static Phantasie4Campaign Synthetic(bool battle, params byte[] code)
    {
        var words = new ushort[31 * 31];
        Array.Fill(words, (ushort)((31 << 6) | (7 << 11)));
        var file = new P4DungeonFile(1, "test", words, [new P4EventTrigger(0x20, 0, 4, 0)], code);
        var c = new Phantasie4Campaign(Manual, T, Phantasie4World.LoadAtlas(), Party(), new SeededRandomSource(1), _ => file,
            i => $"item{i}", new P4Location(P4Place.Outdoors, 1, 0, 0), battle ? Phantasie4World.LoadBattleData() : null);
        c.EnterDungeon(1, 10, 10);
        return c;
    }

    [OriginalDataFact]
    public void Shoulders_AreSaveAndLoad_NeverPageKeys()
    {
        var s = new P4DungeonScreen(P4Preview.StartInTown(2, Code));
        Assert.Equal([Keys.F5], Pad(s, Buttons.LeftShoulder));
        Assert.Equal([Keys.F9], Pad(s, Buttons.RightShoulder));
        Assert.Equal(P4ScreenRequest.Save, Press(s, Buttons.LeftShoulder)); // SAVE SLOTS on: the shell's picker
        Assert.Equal(P4ScreenRequest.Load, Press(s, Buttons.RightShoulder));

        // Outdoors PgUp / PgDn are moves (ButtonOf): the shoulders must not walk the party.
        s.HandleKeys([Keys.D7]); // Leave
        Assert.Equal(P4Place.Outdoors, s.Game.Place);
        Assert.Equal([Keys.F5], Pad(s, Buttons.LeftShoulder));
        Assert.Equal([Keys.F9], Pad(s, Buttons.RightShoulder));
    }

    [OriginalDataFact]
    public void OriginalSlots_LbOpensIvsOwnSavePage_AndBGoesBack()
    {
        var s = new P4DungeonScreen(P4Preview.StartInTown(2, Code)) { OriginalSlots = true };
        Assert.Null(Press(s, Buttons.LeftShoulder));
        Assert.Equal(P4SystemPage.Save, s.SystemPage);
        Assert.Equal([Keys.Space], Pad(s, Buttons.B));
        Press(s, Buttons.B);
        Assert.Null(s.SystemPage);
    }

    [OriginalDataFact]
    public void Street_AIsReturn_BAsksForTheMainMenu_AAnswersYes()
    {
        var s = new P4DungeonScreen(P4Preview.StartInTown(2, Code));
        Assert.Equal(P4TownPrompt.Menu, s.Game.Town.Prompt);
        Assert.Equal([Keys.Enter], Pad(s, Buttons.A));
        Assert.Equal([Keys.Escape], Pad(s, Buttons.B));
        Assert.Equal([Keys.Escape], Pad(s, Buttons.Start));
        Press(s, Buttons.B);
        Assert.True(s.MainMenuAsked);
        Assert.Equal([Keys.Y], Pad(s, Buttons.A));
        Assert.Equal([Keys.N], Pad(s, Buttons.B));
        Press(s, Buttons.B); // N: stay
        Assert.False(s.MainMenuAsked);
        Press(s, Buttons.Start);
        Assert.Equal(P4ScreenRequest.MainMenu, Press(s, Buttons.A));
    }

    [OriginalDataFact]
    public void Street_DPadMovesTheCursor_AEntersTheBuilding_BLeavesItsList()
    {
        var s = new P4DungeonScreen(P4Preview.StartInTown(2, Code));
        Assert.Equal([Keys.Down], Pad(s, Buttons.DPadDown));
        Assert.Equal([Keys.Down], Pad(s, Buttons.LeftThumbstickDown));
        // No diagonals on the street: the stick's down-right is two cursor keys, not PgDn.
        Assert.Equal([Keys.Down, Keys.Right], s.PadKeys([Buttons.DPadDown, Buttons.DPadRight], [], [], out _));
        var at = s.Game.Town.Buildings.ToList().IndexOf(P4Building.Bank);
        for (var i = 0; i < at; i++)
            Press(s, Buttons.DPadDown);
        Assert.Equal(at, s.Cursor);
        Press(s, Buttons.A);
        Assert.True(s.Game.Town.InBuilding);
        Assert.Equal(P4TownPrompt.Pick, s.Game.Town.Prompt);
        Assert.Equal([Keys.Enter], Pad(s, Buttons.A));
        Assert.Equal(s.Game.Town.CanBack ? Keys.Space : Keys.Escape, Assert.Single(Pad(s, Buttons.B)));
    }

    [OriginalDataFact]
    public void Shipwright_WaitTakesA_QuestionTakesAAsYesAndBAsNo()
    {
        var game = P4Preview.StartInTown(2, Code);
        var s = new P4DungeonScreen(game);
        game.Town.Enter(P4Building.Shipwright);
        Assert.Equal(P4TownPrompt.Wait, game.Town.Prompt);
        Press(s, Buttons.A);
        Assert.Equal(P4TownPrompt.Menu, game.Town.Prompt);
        game.Party.Gold = 5000;
        game.Town.Enter(P4Building.Shipwright);
        Assert.Equal(P4TownPrompt.Confirm, game.Town.Prompt);
        Assert.Equal([Keys.Y], Pad(s, Buttons.A));
        Assert.Equal([Keys.N], Pad(s, Buttons.B));
        Press(s, Buttons.B);
        Assert.Equal(P4TownPrompt.Wait, game.Town.Prompt); // "Come again."
        Press(s, Buttons.A);
        Assert.Equal(P4TownPrompt.Menu, game.Town.Prompt);
    }

    [OriginalDataFact]
    public void Mystic_ItsSpaceWait_EndsWithA()
    {
        // s181: a pad player was stuck at the Mystic's SPACE wait (IV read keys only). Without a pack the counted loops
        // do not run, so the visit goes straight to its wait.
        var game = P4Preview.StartInTown(2, Code);
        var s = new P4DungeonScreen(game);
        game.Town.Enter(P4Building.Mystic);
        Assert.False(s.MysticBusy);
        Assert.Equal(P4TownPrompt.Wait, game.Town.Prompt);
        Assert.Equal([Keys.Enter], Pad(s, Buttons.A));
        Press(s, Buttons.A);
        Assert.NotEqual(P4TownPrompt.Wait, game.Town.Prompt);
    }

    [OriginalDataFact]
    public void PartyMenu_BGoesBackWithSpace()
    {
        var s = new P4DungeonScreen(P4Preview.StartInTown(2, Code));
        s.HandleKeys([Keys.F]); // the member pick (_inspect)
        Assert.True(s.MenuOpen);
        Assert.True(s.Game.Menus!.CanBack);
        Assert.Equal([Keys.Space], Pad(s, Buttons.B));
        Press(s, Buttons.B);
        Assert.False(s.MenuOpen);
    }

    [OriginalDataFact]
    public void System_BClosesIt_AboutTakesAAsSpace_EndGameTakesAAsYes()
    {
        var s = new P4DungeonScreen(P4Preview.StartInTown(2, Code));
        s.HandleKeys([Keys.S]);
        Assert.Equal(P4SystemPage.Menu, s.SystemPage);
        Assert.Equal([Keys.Space], Pad(s, Buttons.B));
        s.HandleKeys([Keys.A]);
        Assert.Equal(P4SystemPage.About, s.SystemPage);
        Assert.Equal([Keys.Space], Pad(s, Buttons.A));
        Press(s, Buttons.A);
        Assert.Equal(P4SystemPage.Menu, s.SystemPage);
        s.HandleKeys([Keys.E, Keys.E]); // End game ▸ End game?
        Assert.Equal(P4SystemPage.EndGame, s.SystemPage);
        Assert.Equal([Keys.N], Pad(s, Buttons.B));
        Assert.Equal(P4ScreenRequest.MainMenu, Press(s, Buttons.A));
        Press(s, Buttons.B);
        Assert.Null(s.SystemPage);
    }

    [OriginalDataFact]
    public void Outdoors_TheHeldPerpendicularGivesTheDiagonal()
    {
        var s = new P4DungeonScreen(P4Preview.StartInTown(2, Code));
        s.HandleKeys([Keys.D7]); // Leave
        Assert.Equal(P4OutdoorPrompt.Action, s.Game.Outdoor.Prompt);
        Assert.Equal([Keys.Up], s.PadKeys([Buttons.DPadUp], [Buttons.DPadUp], [], out _));
        Assert.Equal([Keys.PageUp], s.PadKeys([Buttons.DPadRight], [Buttons.DPadUp, Buttons.DPadRight], [], out _));
        Assert.Equal([Keys.Home], s.PadKeys([Buttons.LeftThumbstickUp, Buttons.LeftThumbstickLeft], [], [], out _));
        Assert.Equal([Keys.End], s.PadKeys([Buttons.DPadLeft], [Buttons.DPadDown], [], out _));
        Assert.Equal([Keys.PageDown], s.PadKeys([Buttons.DPadDown], [Buttons.DPadRight], [], out _));
        // The D-pad and the stick are two controls: one's held direction does not bend the other's.
        Assert.Equal([Keys.Right], s.PadKeys([Buttons.DPadRight], [Buttons.LeftThumbstickUp], [], out _));
        Assert.Equal(P4DungeonScreen.DirectionOf(Keys.PageUp), P4DungeonScreen.DirectionOf(Keys.NumPad9));
    }

    [Fact]
    public void PadMoves_WithoutDiagonals_AreTheArrows() =>
        Assert.Equal([Keys.Up, Keys.Right],
            P4DungeonScreen.PadMoves([Buttons.DPadUp, Buttons.DPadRight], [Buttons.DPadUp, Buttons.DPadRight], false));

    [Fact]
    public void StepFocus_Wraps_AndStartsAtAnEnd()
    {
        Assert.Null(P4DungeonScreen.StepFocus(null, 0, 1));
        Assert.Equal(0, P4DungeonScreen.StepFocus(null, 3, 1));
        Assert.Equal(2, P4DungeonScreen.StepFocus(null, 3, -1));
        Assert.Equal(1, P4DungeonScreen.StepFocus(0, 3, 1));
        Assert.Equal(0, P4DungeonScreen.StepFocus(2, 3, 1));
        Assert.Equal(2, P4DungeonScreen.StepFocus(0, 3, -1));
        Assert.Equal(0, P4DungeonScreen.StepFocus(5, 3, 1)); // a stale pick starts again
    }

    [Fact]
    public void PadTargets_AreIvsOwnButtons()
    {
        HitRegion[] regions =
        [
            new(new Rectangle(0, 0, 4, 4)) { Click = [Keys.Enter] }, // someone else's (no tag)
            new(new Rectangle(0, 0, 4, 4), new P4PanelIconTag("_townmenu", 0)) { Click = [Keys.F] },
            new(new Rectangle(0, 0, 4, 4), new P4PanelIconTag("_townmenu", 1)), // nothing to press
            new(new Rectangle(0, 0, 4, 4), new P4KeypadTag('1')),
            new(new Rectangle(0, 0, 4, 4), new P4KeypadTag(null)) { Click = [Keys.Back] },
            new(new Rectangle(0, 0, 4, 4), new P4KeypadTag(null)),
        ];
        Assert.Equal([regions[1], regions[3], regions[4]], P4DungeonScreen.PadTargets(regions));
    }

    [OriginalDataFact]
    public void YAndX_PickAButton_AClicksIt_BLetsGo_AChangedSetForgetsIt()
    {
        var s = new P4DungeonScreen(P4Preview.StartInTown(2, Code));
        HitRegion f = new(new Rectangle(10, 10, 16, 16), new P4PanelIconTag("_townmenu", 0)) { Click = [Keys.F] };
        HitRegion c = new(new Rectangle(10, 40, 16, 16), new P4PanelIconTag("_townmenu", 1)) { Click = [Keys.C] };
        HitRegion[] panel = [f, c];

        Assert.Empty(s.PadKeys([Buttons.Y], [], panel, out var press));
        Assert.Null(press);
        Assert.Equal(f.Bounds.Center, s.PadPointer); // lit as under the pointer
        s.PadKeys([Buttons.Y], [], panel, out _);
        Assert.Equal(c.Bounds.Center, s.PadPointer);
        s.PadKeys([Buttons.X], [], panel, out _);
        Assert.Equal(f.Bounds.Center, s.PadPointer);
        Assert.Empty(s.PadKeys([Buttons.A], [], panel, out press)); // A clicks the pick (the shell's Act), sends no key
        Assert.Same(f, press);

        Assert.Empty(s.PadKeys([Buttons.B], [], panel, out press)); // B lets go first ...
        Assert.Null(press);
        Assert.Null(s.PadPointer);
        Assert.Equal([Keys.Escape], s.PadKeys([Buttons.B], [], panel, out _)); // ... then is B again

        s.PadKeys([Buttons.Y], [], panel, out _);
        Assert.NotNull(s.PadPointer);
        s.PadKeys([], [], [c], out _); // another set of buttons: the pick is gone
        Assert.Null(s.PadPointer);
        Assert.Equal([Keys.Enter], s.PadKeys([Buttons.A], [], [c], out press));
        Assert.Null(press);

        s.PadKeys([Buttons.Y], [], panel, out _);
        s.ClearPadFocus(); // the mouse moved
        Assert.Null(s.PadPointer);
    }

    [OriginalDataFact]
    public void MainMenuQuestion_APressesYes_NotThePickedButton()
    {
        var s = new P4DungeonScreen(P4Preview.StartInTown(2, Code));
        HitRegion f = new(new Rectangle(10, 10, 16, 16), new P4PanelIconTag("_townmenu", 0)) { Click = [Keys.F] };
        s.PadKeys([Buttons.Y], [], [f], out _);
        s.HandleKeys([Keys.Escape]);
        Assert.Equal([Keys.Y], s.PadKeys([Buttons.A], [], [f], out var press));
        Assert.Null(press);
    }

    [OriginalDataFact]
    public void AFight_PadPicksTheOrder_AGivesIt_BLeavesTheSpellList()
    {
        // NOENC; FIGHT 1 (Undead Goblin); END (as P4DungeonScreenTests.AFight_RunsOnTheBattleField_AndKeysPlayIt).
        var s = new P4DungeonScreen(Synthetic(true, 0x22, 0x23, 0x01, 0x00));
        var b = Assert.IsType<P4Battle>(s.Game.Battle);
        Assert.Equal(P4BattlePrompt.Options, b.Prompt);
        Assert.Equal([Keys.Enter], Pad(s, Buttons.A)); // the options list: Return on the cursor line (Fight)
        Press(s, Buttons.A);
        Assert.Equal(P4BattlePrompt.Order, b.Prompt);

        var offered = b.Offered();
        var first = P4DungeonScreen.PadOrderDefaults.First(offered.Contains); // a blow in reach, else Parry
        Assert.Null(s.PadAction);
        Assert.Equal([KeyOf(first)], Pad(s, Buttons.A)); // the default until the pad picks another
        Assert.Equal(first, s.PadAction);
        Assert.Contains(s.BoxLines(), l => l.Text.Contains($"[{UiText.S("p4.battle.action." + first.ToString().ToLowerInvariant())}]",
            StringComparison.Ordinal));
        Pad(s, Buttons.Y);
        Assert.Equal(offered[(offered.ToList().IndexOf(first) + 1) % offered.Count], s.PadAction);
        Pad(s, Buttons.X);
        Assert.Equal(first, s.PadAction);

        Assert.Equal([Keys.PageUp], s.PadKeys([Buttons.DPadUp], [Buttons.DPadRight], [], out _)); // a step's diagonal
        Assert.Equal([Keys.Escape], Pad(s, Buttons.B));
        while (s.PadAction != P4ActionKind.Cast)
            Pad(s, Buttons.Y);
        Assert.Equal([Keys.C], Pad(s, Buttons.A));
        Press(s, Buttons.A); // the combat spell list
        Assert.Equal([Keys.Enter], Pad(s, Buttons.A));
        Assert.Equal([Keys.Back], Pad(s, Buttons.B)); // back to the orders
        Press(s, Buttons.B);
        Assert.Equal([Keys.C], Pad(s, Buttons.A));
        Pad(s, Buttons.Y);
        Assert.NotEqual(P4ActionKind.Cast, s.PadAction);

        // The pad alone ends the fight: A on the options, the orders, the targets; A answers the treasure and the end.
        for (var i = 0; i < 600 && s.Game.Battle is not null; i++)
            Press(s, Buttons.A);
        Assert.Null(s.Game.Battle);
    }

    // --- s183 "Pad leftovers": the typed prompts' wheel, the pad help line, the stick quantiser -------------------------

    [OriginalDataFact]
    public void NewGame_ThePadNamesTheNewCharacter_WithTheWheel()
    {
        // A pad-only player's new game: the Guild's roster, Make a character, a plain Human Fighter, then the name by wheel.
        var game = Phantasie4World.NewGame(Manual, new SeededRandomSource(7));
        var s = new P4DungeonScreen(game);
        var w = game.Town;
        w.Enter(P4Building.Guild);
        w.Choose(0); // G: Guild roster
        w.Choose(1); // N: Make a character
        w.Choose(2); // No: a plain character
        w.Choose(P4Ids.Human);
        w.Choose(3); // Fighter
        Press(s, Buttons.A); // Okay (A = Y at the question)
        Assert.Equal(P4TownPrompt.Text, w.Prompt);
        Assert.Equal(NameEntry.Alphabet, s.PadTypingAlphabet);

        Press(s, Buttons.X); // A
        Press(s, Buttons.DPadDown); // jump ten: K
        Press(s, Buttons.X);
        Press(s, Buttons.DPadLeft); // J
        Press(s, Buttons.X);
        Assert.Equal("AKJ", s.Typed);
        Press(s, Buttons.Y); // delete
        Assert.Equal("AK", s.Typed);
        Press(s, Buttons.B); // delete too (IV's B in a typed prompt)
        Assert.Equal("A", s.Typed);
        Press(s, Buttons.DPadUp); // jump back ten from J: wraps to the space's side (37 characters)
        Assert.Equal(NameEntry.Alphabet[(9 - 10 + NameEntry.Alphabet.Length) % NameEntry.Alphabet.Length], s.PadWheelChar);
        Press(s, Buttons.A); // done
        Assert.Equal(P4TownPrompt.Wait, w.Prompt); // the sheet with the skills
        Assert.Contains(game.Party.Roster, m => m?.Name == "A");
    }

    [OriginalDataFact]
    public void Dungeon_LetterAndNumber_ByThePad_TheWheelStartsAgainPerPrompt()
    {
        // NOENC; INPUTS; INPUTN; FIGHT 3; END (as P4DungeonScreenTests.Typing_FillsLetterAndNumber…).
        var s = new P4DungeonScreen(Synthetic(false, 0x22, 0x0C, 0x0E, 0x23, 0x03, 0x00));
        Assert.Equal(P4DungeonPrompt.Letters, s.Game.Dungeon.Prompt);
        Assert.Empty(Pad(s, Buttons.DPadRight)); // B: the wheel steps; no arrow goes to the prompt
        Pad(s, Buttons.X);
        Pad(s, Buttons.LeftThumbstickRight); // the stick steps too
        Pad(s, Buttons.X);
        Assert.Equal("BC", s.Typed);
        Assert.Equal([Keys.Back], Pad(s, Buttons.Y));
        Assert.Equal([Keys.Enter], Pad(s, Buttons.A));
        Press(s, Buttons.A);

        Assert.Equal(P4DungeonPrompt.Number, s.Game.Dungeon.Prompt);
        Assert.Equal(DialogPad.Digits, s.PadTypingAlphabet);
        Assert.Equal('1', s.PadWheelChar); // a new prompt: the wheel starts again, on 1
        Pad(s, Buttons.X);
        Pad(s, Buttons.DPadDown); // among the digits Up / Down step one
        Pad(s, Buttons.DPadRight);
        Pad(s, Buttons.X);
        Assert.Equal("13", s.Typed);
        Press(s, Buttons.A);
        Assert.Equal(P4DungeonPrompt.Fight, s.Game.Dungeon.Prompt);
        Assert.Null(s.PadTypingAlphabet);
    }

    [OriginalDataFact]
    public void TypedPrompt_YAndXAreTheWheel_NotTheButtonPick()
    {
        var game = P4Preview.StartInTown(2, Code);
        var s = new P4DungeonScreen(game);
        game.Town.Enter(P4Building.Bank);
        game.Town.Choose(1); // W: withdraw, an amount per member
        Assert.Equal(P4TownPrompt.Number, game.Town.Prompt);
        var key = new HitRegion(new Rectangle(0, 0, 10, 10), new P4KeypadTag('7'));
        s.PadKeys([Buttons.Y], [], [key], out var press);
        Assert.Null(press);
        Assert.Null(s.PadPointer); // Y deleted (nothing typed yet); no keypad key was picked
        s.PadKeys([Buttons.X], [], [key], out _);
        Assert.Equal("1", s.Typed);
    }

    [OriginalDataFact]
    public void Help_NamesThePadsButtons_WhileThePadIsInUse()
    {
        var s = new P4DungeonScreen(Synthetic(false, 0x22, 0x0C, 0x00));
        Assert.Equal(UiText.S("p4.dungeon.help_type"), s.Help);
        Pad(s, Buttons.DPadRight);
        Assert.True(s.PadInput);
        Assert.Equal(UiText.F("p4.pad.help_type_x", "B"), s.Help);
        s.PadInput = false; // a key or the mouse (the shell)
        Assert.Equal(UiText.S("p4.dungeon.help_type"), s.Help);

        var town = new P4DungeonScreen(P4Preview.StartInTown(2, Code));
        Pad(town, Buttons.DPadDown);
        Assert.Equal(UiText.S("p4.pad.help_street"), town.Help);
        HitRegion f = new(new Rectangle(10, 10, 16, 16), new P4PanelIconTag("_townmenu", 0)) { Click = [Keys.F] };
        town.PadKeys([Buttons.Y], [], [f], out _); // a button picked: A presses it
        Assert.Equal(UiText.S("p4.pad.help_focus"), town.Help);
        town.ClearPadFocus();
        town.HandleKeys([Keys.D7]); // Leave
        Pad(town, Buttons.DPadUp);
        Assert.Equal(UiText.S("p4.pad.help_move"), town.Help);
    }

    [OriginalDataFact]
    public void AFight_PadHelp_NamesTheOrderAGives()
    {
        var s = new P4DungeonScreen(Synthetic(true, 0x22, 0x23, 0x01, 0x00));
        Press(s, Buttons.A); // Fight
        var b = Assert.IsType<P4Battle>(s.Game.Battle);
        Assert.Equal(P4BattlePrompt.Order, b.Prompt);
        var first = P4DungeonScreen.PadOrderDefaults.First(b.Offered().Contains);
        Assert.Equal(UiText.F("p4.pad.help_orders_x", UiText.S("p4.battle.action." + first.ToString().ToLowerInvariant())), s.Help);
    }

    [OriginalDataFact]
    public void Map_AStickDiagonal_IsOneMove_NotNorthThenNorthEast()
    {
        var s = new P4DungeonScreen(Synthetic(false, 0x22, 0x00));
        Assert.Equal(P4DungeonPrompt.Action, s.Game.Dungeon.Prompt);
        // The stick's buttons would give Up first, then PgUp; by position the push settles on NE and sends it once.
        var keys = new List<Keys>();
        foreach (var at in new[] { new Vector2(0, 0.55f), new Vector2(0.3f, 0.6f), new Vector2(0.6f, 0.65f), new Vector2(0.7f, 0.7f),
            new Vector2(0.7f, 0.7f), new Vector2(0.7f, 0.7f) })
            keys.AddRange(s.PadKeys([], [], [], at, 16.7, out _));
        Assert.Equal([Keys.PageUp], keys);
        // Back to rest, then a push north: Up once.
        keys.Clear();
        foreach (var at in new[] { Vector2.Zero, new Vector2(0, 0.9f), new Vector2(0, 0.95f), new Vector2(0, 0.95f),
            new Vector2(0, 0.95f), new Vector2(0, 0.95f) })
            keys.AddRange(s.PadKeys([], [], [], at, 16.7, out _));
        Assert.Equal([Keys.Up], keys);
        // The stick's own buttons are ignored on that path (the quantiser speaks for the stick).
        Assert.Empty(s.PadKeys([Buttons.LeftThumbstickUp], [Buttons.LeftThumbstickUp], [], new Vector2(0, 0.95f), 16.7, out _));
    }

    [Fact]
    public void StickQuantizer_OnePushOneDirection()
    {
        var q = new StickQuantizer();
        Assert.Null(q.Update(new Vector2(0.2f, 0), 16, true)); // under the threshold
        Assert.Null(q.Update(new Vector2(0.6f, 0), 16, true)); // the push starts: east so far
        Assert.Null(q.Update(new Vector2(0.6f, 0.3f), 16, true));
        Assert.Null(q.Update(new Vector2(0.6f, 0.6f), 16, true));
        Assert.Equal((1, -1), q.Update(new Vector2(0.6f, 0.6f), 30, true)); // settled: NE (Y up = Dy −1)
        Assert.Null(q.Update(new Vector2(0.7f, 0.7f), 16, true)); // held: nothing more
        Assert.Null(q.Update(new Vector2(0.4f, 0.1f), 16, true)); // not back at rest yet
        Assert.Null(q.Update(new Vector2(0.1f, 0), 16, true)); // rest
        Assert.Null(q.Update(new Vector2(0, -0.8f), 16, true)); // a second push, south
        Assert.Equal((0, 1), q.Update(new Vector2(0, -0.8f), StickQuantizer.SettleMs, true));
    }

    [Fact]
    public void StickQuantizer_AQuickFlick_CountsAtItsStrongestSample()
    {
        var q = new StickQuantizer();
        Assert.Null(q.Update(new Vector2(-0.6f, 0), 16, true));
        Assert.Null(q.Update(new Vector2(-0.9f, 0.05f), 16, true));
        Assert.Equal((-1, 0), q.Update(new Vector2(-0.1f, 0), 16, true)); // let go inside the window: west
        Assert.Null(q.Update(Vector2.Zero, 16, true));
    }

    [Theory]
    [InlineData(1f, 0f, true, 1, 0)]
    [InlineData(0.9f, 0.4f, true, 1, -1)] // 24°: past the E / NE edge (22.5°)
    [InlineData(0.93f, 0.36f, true, 1, 0)] // 21.2°: still east
    [InlineData(-0.7f, -0.7f, true, -1, 1)] // SW
    [InlineData(-0.7f, 0.7f, true, -1, -1)] // NW
    [InlineData(0.7f, 0.72f, false, 0, -1)] // four ways: the nearer axis (north)
    [InlineData(0.72f, -0.7f, false, 1, 0)] // four ways: east
    [InlineData(-1f, 0.01f, true, -1, 0)] // west, either side of the ±180° seam
    [InlineData(-1f, -0.01f, true, -1, 0)]
    public void StickQuantizer_Quantise_NearestCompassPoint(float x, float y, bool eightWay, int dx, int dy) =>
        Assert.Equal((dx, dy), StickQuantizer.Quantise(new Vector2(x, y), eightWay));

    [Fact]
    public void StickQuantizer_ButtonsOf_ADiagonalIsBothButtons()
    {
        Assert.Equal([Buttons.LeftThumbstickUp, Buttons.LeftThumbstickRight], StickQuantizer.ButtonsOf((1, -1)));
        Assert.Equal([Buttons.LeftThumbstickDown], StickQuantizer.ButtonsOf((0, 1)));
    }
}
