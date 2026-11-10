using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using OpenPhantasie.Client;
using OpenPhantasie.Core;
using OpenPhantasie.Data;
using Xunit;

namespace OpenPhantasie.Client.Tests;

/// <summary>"Pad coverage" (s182): the pad on modal dialogs and prompts (<see cref="DialogPad"/>, Game1.DialogPad.cs).</summary>
public sealed class DialogPadTests
{
    private static SessionSnapshot Saved(GameSession s) => SessionCodec.ReadJson(SessionCodec.WriteJson(s.CaptureSnapshot()));

    private static IReadOnlyList<Keys> Pad(DialogPad pad, object dialog, params Buttons[] buttons) =>
        pad.KeysFor(dialog, buttons, (dialog as ITypedEntry)?.TypingAlphabet, dialog is IPadYesNo { AsksYesNo: true });

    [OriginalDataFact]
    public void Continue_GuildNumberQuestion_AnswersByPad_UpStepsToOne_AEntersIt()
    {
        var s = ClientSessions.Overworld(); // Fighto (1) + Priesto (2) in the wilderness
        var file = Saved(s);
        s.EnterTown(1);
        var d = new ContinueDialog();
        d.Open(file);
        var pad = new DialogPad();
        Assert.Null(d.HandleKeys(Pad(pad, d, Buttons.DPadUp), s));
        Assert.Equal("> 1_", d.Lines(s, ClientSessions.Data)[2]);
        Assert.NotNull(d.HandleKeys(Pad(pad, d, Buttons.A), s));
        Assert.False(d.IsOpen);
        Assert.True(d.TakeLoaded());
        Assert.Equal(GameMode.Overworld, s.Mode);
    }

    [OriginalDataFact]
    public void Continue_GuildNumberQuestion_WheelTypesTheNumber_YDeletes_BCancels()
    {
        var s = ClientSessions.Overworld();
        var d = new ContinueDialog();
        d.Open(Saved(s));
        var pad = new DialogPad();
        d.HandleKeys(Pad(pad, d, Buttons.DPadRight, Buttons.X), s); // the digits wheel starts on 1: Right = 2
        Assert.Equal("> 2_", d.Lines(s, ClientSessions.Data)[2]);
        d.HandleKeys(Pad(pad, d, Buttons.X, Buttons.Y), s); // 22, then Backspace
        Assert.Equal("> 2_", d.Lines(s, ClientSessions.Data)[2]);
        Assert.Equal("Load cancelled.", d.HandleKeys(Pad(pad, d, Buttons.B), s));
        Assert.False(d.IsOpen);
    }

    [OriginalDataFact]
    public void Continue_PartyList_ByPad_BGoesBackFromTheGuildNumber()
    {
        var s = ClientSessions.Overworld();
        var file = Saved(s);
        file.OtherParties = [new SessionSnapshot { PartySlotIndices = [1], Mode = GameMode.Overworld }];
        var d = new ContinueDialog();
        d.Open(file, fromList: true);
        Assert.True(d.ShowsList);
        var pad = new DialogPad();
        Assert.Equal(ContinueDialog.Question, d.HandleKeys(Pad(pad, d, Buttons.DPadUp, Buttons.A), s)); // BY GUILD NUMBER
        Assert.Equal(ContinueDialog.ListQuestion, d.HandleKeys(Pad(pad, d, Buttons.B), s));
        Assert.True(d.ShowsList);
    }

    [Fact]
    public void Wheel_Digits_StartOnOne_WrapBothWays_XTypes_YIsBackspace()
    {
        var pad = new DialogPad();
        var dialog = new object();
        Assert.Equal([Keys.D1], pad.KeysFor(dialog, [Buttons.X]));
        Assert.Equal([Keys.D0], pad.KeysFor(dialog, [Buttons.DPadLeft, Buttons.X])); // 1 → wraps to 0
        Assert.Equal([Keys.D1, Keys.D2], pad.KeysFor(dialog, [Buttons.LeftThumbstickRight, Buttons.X, Buttons.DPadRight, Buttons.X]));
        Assert.Equal([Keys.Back], pad.KeysFor(dialog, [Buttons.Y]));
    }

    [Fact]
    public void Wheel_LeavesTheMenuPadKeys_ButLeftRightStepIt()
    {
        var pad = new DialogPad();
        Assert.Equal([Keys.Up, Keys.Down, Keys.Enter, Keys.Escape, Keys.PageUp, Keys.PageDown],
            pad.KeysFor(new object(), [Buttons.DPadUp, Buttons.DPadDown, Buttons.DPadLeft, Buttons.DPadRight, Buttons.A,
                Buttons.B, Buttons.LeftShoulder, Buttons.RightShoulder, Buttons.Start]));
    }

    [Fact]
    public void Wheel_Names_StepTheLetters_AndTheSpace()
    {
        var pad = new DialogPad();
        var dialog = new object();
        Assert.Equal([Keys.A], pad.KeysFor(dialog, [Buttons.X], NameEntry.Alphabet));
        Assert.Equal([Keys.Space], pad.KeysFor(dialog, [Buttons.DPadLeft, Buttons.X], NameEntry.Alphabet)); // A → wraps to the space
        Assert.Equal([Keys.A], pad.KeysFor(dialog, [Buttons.DPadRight, Buttons.X], NameEntry.Alphabet));
        Assert.Equal([Keys.D1], pad.KeysFor(dialog, [Buttons.X])); // the step changed to digits: the wheel starts again
    }

    [Fact]
    public void Wheel_ANewDialog_StartsAgain_AndTheHelpWaitsForThePad()
    {
        var pad = new DialogPad();
        var first = new object();
        Assert.Null(pad.Help(first, null, yesNo: false, leftRightNative: false));
        pad.KeysFor(first, []);
        Assert.Null(pad.Help(first, null, false, false)); // no button yet
        pad.KeysFor(first, [Buttons.DPadDown, Buttons.A]);
        Assert.Null(pad.Help(first, null, false, false)); // a list driven by D-pad / A keeps its own help
        pad.KeysFor(first, [Buttons.DPadRight]);
        Assert.Contains("2", pad.Help(first, null, false, false));
        Assert.Equal("Pad: A yes  X no  B back", pad.Help(first, null, yesNo: true, leftRightNative: false));
        var second = new object();
        Assert.Null(pad.Help(second, null, false, false));
        Assert.Equal([Keys.D1], pad.KeysFor(second, [Buttons.X]));
    }

    [OriginalDataFact]
    public void GuildNew_EnrolledSheet_AndInspectPage_HaveNoWheel()
    {
        // s182 live pass: the name's wheel hint stayed on the enrolled member's PRESS SPACE sheet (and the WHOM step's on
        // an Inspect page); those steps type nothing, so the pad keeps Left / Right as arrows there and shows no wheel.
        var s = GameSessionFactory.Create(ClientSessions.Data, new SeededDosRandomSource(4));
        s.OpenGuild();
        Assert.True(s.TryAddToParty(s.CreateAndEnroll("Ao", Race.Human, CharacterClass.Fighter), out _));
        var d = new GuildNewDialog();
        d.Open(s);
        if (d.Lines(s, ClientSessions.Data)[0] != UiText.S("member.select_race"))
            d.HandleKeys([Keys.D1], s); // T5's first question (a plain roll)
        d.HandleKeys([Keys.D1], s); // race
        d.HandleKeys([Keys.D4], s); // class
        Assert.True(d.AsksYesNo);
        Assert.False(d.TakesLeftRight);
        d.HandleKeys([Keys.Y], s); // keep
        Assert.Equal(NameEntry.Alphabet, d.TypingAlphabet);
        Assert.False(d.TakesLeftRight); // the name: the wheel
        d.HandleKeys([Keys.A, Keys.B, Keys.Enter], s);
        Assert.True(d.IsOpen);
        Assert.True(d.TakesLeftRight); // the sheet: no wheel
        var pad = new DialogPad();
        Assert.Equal([Keys.Left], pad.KeysFor(d, [Buttons.DPadLeft], d.TypingAlphabet, leftRightNative: d.TakesLeftRight));
        Assert.Null(pad.Help(d, d.TypingAlphabet, yesNo: false, leftRightNative: d.TakesLeftRight));

        var inspect = new InspectDialog(new PromptMenu(), ClientSessions.Data);
        inspect.Open(s); // in the Guild: the who list
        Assert.False(inspect.TakesLeftRight);
        inspect.HandleKeys([Keys.D1], s);
        Assert.True(inspect.TakesLeftRight); // the sheet's page menu
        inspect.HandleKeys([Keys.Enter], s); // open a page
        Assert.True(inspect.TakesLeftRight); // a page types nothing either
    }

    [Fact]
    public void LeftRightNative_KeepsTheArrows_NoWheel()
    {
        var pad = new DialogPad();
        Assert.Equal([Keys.Left, Keys.Right, Keys.Enter],
            pad.KeysFor(new object(), [Buttons.DPadLeft, Buttons.X, Buttons.Y, Buttons.DPadRight, Buttons.A], leftRightNative: true));
    }

    [Fact]
    public void YesNo_AIsY_XIsN_BIsEsc()
    {
        Assert.Equal([Keys.Y, Keys.N, Keys.Escape, Keys.Up], DialogPad.YesNoKeys([Buttons.A, Buttons.X, Buttons.B, Buttons.DPadUp]));
        Assert.Equal([Keys.Y], new DialogPad().KeysFor(new object(), [Buttons.A], yesNo: true));
    }

    [OriginalDataFact]
    public void GameMenu_QuitByPad_AAsksThenAAnswersYes()
    {
        var s = ClientSessions.Overworld();
        var d = new GameMenuDialog();
        d.Open();
        var pad = new DialogPad();
        d.HandleKeys(Pad(pad, d, Buttons.DPadUp, Buttons.A), s); // Up wraps to Quit; A asks
        Assert.True(d.AsksYesNo);
        Assert.True(d.IsOpen);
        d.HandleKeys(Pad(pad, d, Buttons.X), s); // X = N: back to the menu
        Assert.False(d.AsksYesNo);
        d.HandleKeys(Pad(pad, d, Buttons.A), s);
        d.HandleKeys(Pad(pad, d, Buttons.A), s);
        Assert.False(d.IsOpen);
        Assert.Equal(GameMenuChoice.Quit, d.TakeChoice());
    }

    [OriginalDataFact]
    public void TimeLag_ByPad_WheelPicksTheLag_AIsSoundOn()
    {
        var s = ClientSessions.Overworld();
        var d = new TimeLagDialog();
        d.Open();
        var pad = new DialogPad();
        d.HandleKeys(Pad(pad, d, Buttons.DPadRight, Buttons.DPadRight, Buttons.X), s); // 3
        Assert.True(d.AsksYesNo);
        d.HandleKeys(Pad(pad, d, Buttons.A), s);
        Assert.Equal((3, true), d.TakeChosen());
    }

    [Fact]
    public void Prompt_PadIsArrowsAndReturn_NoShouldersNoEsc()
    {
        Assert.Equal([Keys.Up, Keys.Right, Keys.Enter],
            Game1.PromptPadKeys([Buttons.DPadUp, Buttons.LeftShoulder, Buttons.RightShoulder, Buttons.B, Buttons.X,
                Buttons.LeftThumbstickRight, Buttons.A]));
    }

    [Fact]
    public void EventPage_DPadStepsThePick_ASendsItsNumber()
    {
        Assert.Equal((0, Keys.D1), Game1.EventPadStep(0, 3, [Buttons.A])); // nothing picked: 1
        Assert.Equal((1, (Keys?)null), Game1.EventPadStep(0, 3, [Buttons.DPadDown])); // the first press shows 1
        Assert.Equal((2, Keys.D2), Game1.EventPadStep(1, 3, [Buttons.DPadRight, Buttons.A]));
        Assert.Equal((3, (Keys?)null), Game1.EventPadStep(1, 3, [Buttons.DPadUp])); // wraps
        Assert.Equal((1, (Keys?)null), Game1.EventPadStep(3, 3, [Buttons.LeftThumbstickDown]));
        Assert.Equal((0, (Keys?)null), Game1.EventPadStep(0, 3, [Buttons.B, Buttons.X]));
    }

    [Fact]
    public void PressTracker_CountsPressesAndLaterReleases()
    {
        var t = new PadPressTracker();
        var aDown = new GamePadState(new GamePadThumbSticks(), new GamePadTriggers(), new GamePadButtons(Buttons.A), new GamePadDPad());
        var up = new GamePadState(new GamePadThumbSticks(), new GamePadTriggers(), new GamePadButtons(), new GamePadDPad());
        Assert.Equal((0, 0), t.Events([], up));
        Assert.Equal((1, 0), t.Events([Buttons.A], aDown));
        Assert.Equal((0, 0), t.Events([], aDown));
        Assert.Equal((0, 1), t.Events([], up));
        Assert.Equal((0, 0), t.Events([], up));
    }

    [Fact]
    public void PadCommands_TheGuildsLetters_WithTheBoundKeys()
    {
        var settings = new ClientSettings();
        var guild = PadCommandDialog.EntriesFor(GameMode.Guild, settings.GetKey, examine: false, timeLag: false);
        Assert.Equal([settings.GetKey(GameAction.GuildAdd), settings.GetKey(GameAction.GuildDrop), settings.GetKey(GameAction.GuildNew),
            settings.GetKey(GameAction.GuildList), settings.GetKey(GameAction.GuildRename), settings.GetKey(GameAction.GuildPurge),
            Keys.S, Keys.T], guild.Select(e => e.Key));
        Assert.Contains(PadCommandDialog.EntriesFor(GameMode.Guild, settings.GetKey, examine: true, timeLag: false),
            e => e.Key == settings.GetKey(GameAction.GuildExamine));
        Assert.Equal([settings.GetKey(GameAction.GuildAdd), settings.GetKey(GameAction.GuildDrop)],
            PadCommandDialog.EntriesFor(GameMode.Title, settings.GetKey, false, false).Select(e => e.Key));
        Assert.Empty(PadCommandDialog.EntriesFor(GameMode.Town, settings.GetKey, false, true));
    }

    [Fact]
    public void PadCommands_TheFieldsLetters_LookInTheDungeonOnly_TimeLagWhenACommand()
    {
        var settings = new ClientSettings();
        var field = PadCommandDialog.EntriesFor(GameMode.Overworld, settings.GetKey, false, timeLag: true).Select(e => e.Key).ToList();
        Assert.Equal([settings.GetKey(GameAction.Fight), settings.GetKey(GameAction.CastSpell), settings.GetKey(GameAction.Inspect),
            settings.GetKey(GameAction.UsePotion), settings.GetKey(GameAction.Health), settings.GetKey(GameAction.TimeLag)], field);
        var dungeon = PadCommandDialog.EntriesFor(GameMode.Dungeon, settings.GetKey, false, timeLag: false).Select(e => e.Key).ToList();
        Assert.Contains(settings.GetKey(GameAction.Look), dungeon);
        Assert.DoesNotContain(settings.GetKey(GameAction.TimeLag), dungeon);
    }

    [Fact]
    public void PadCommands_DPadMoves_APicksOnce_BCloses()
    {
        var d = new PadCommandDialog();
        d.Open([new("one", Keys.A), new("two", Keys.D), new("three", Keys.N)]);
        var pad = new DialogPad();
        Assert.Equal("", d.HandleKeys(pad.KeysFor(d, [Buttons.DPadUp, Buttons.A]), null!)); // Up wraps to the last
        Assert.False(d.IsOpen);
        Assert.Equal(Keys.N, d.TakeChoice());
        Assert.Null(d.TakeChoice());
        d.Open([new("one", Keys.A)]);
        Assert.Equal("", d.HandleKeys(pad.KeysFor(d, [Buttons.B]), null!));
        Assert.False(d.IsOpen);
        Assert.Null(d.TakeChoice());
    }
}
