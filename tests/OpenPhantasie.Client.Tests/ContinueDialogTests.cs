using Microsoft.Xna.Framework.Input;
using OpenPhantasie.Client;
using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client.Tests;

/// <summary>`continue.continue_saved_game` asks for a Guild number (manual ENCORE, D-069).</summary>
public class ContinueDialogTests
{
    private static SessionSnapshot Saved(GameSession s) => SessionCodec.ReadJson(SessionCodec.WriteJson(s.CaptureSnapshot()));

    [OriginalDataFact]
    public void TypedNumber_CallsUpThatCharactersParty()
    {
        var s = ClientSessions.Overworld(); // Fighto (1) + Priesto (2) in the wilderness
        var file = Saved(s);
        s.EnterTown(1);
        var d = new ContinueDialog();
        d.Open(file);
        Assert.Equal([ContinueDialog.Title, ContinueDialog.Question, "> _"], d.Lines(s, ClientSessions.Data));
        d.HandleKeys([Keys.D2], s);
        Assert.Equal("> 2_", d.Lines(s, ClientSessions.Data)[2]);
        Assert.NotNull(d.HandleKeys([Keys.Enter], s));
        Assert.False(d.IsOpen);
        Assert.True(d.TakeLoaded());
        Assert.False(d.TakeLoaded());
        Assert.Equal(GameMode.Overworld, s.Mode);
        Assert.Equal(2, s.Party.Members.Count);
    }

    [OriginalDataFact]
    public void NumberNamingNobody_IsRefused_AndAskedAgain_EscCancels()
    {
        var s = ClientSessions.Overworld();
        var d = new ContinueDialog();
        d.Open(Saved(s));
        d.HandleKeys([Keys.D2, Keys.D3], s);
        Assert.Equal(SeveralParties.ElementalRefusal, d.HandleKeys([Keys.Enter], s));
        Assert.True(d.IsOpen);
        Assert.Equal(SeveralParties.ElementalRefusal, d.Lines(s, ClientSessions.Data)[^1]);
        d.HandleKeys([Keys.D9], s);
        Assert.Equal(SeveralParties.NoSuchMember, d.HandleKeys([Keys.Enter], s));
        Assert.Equal("Load cancelled.", d.HandleKeys([Keys.Escape], s));
        Assert.False(d.IsOpen);
        Assert.False(d.TakeLoaded());
    }

    [Fact]
    public void ContinueFromList_IsOff_ByDefault_AndRoundTrips()
    {
        Assert.False(new ClientSettings().Game(GameId.P1).ContinueFromList); // off = manual
        var on = ClientSettings.FromJson("""{ "games": { "p1": { "continueFromList": true } } }""")!;
        Assert.True(on.Game(GameId.P1).ContinueFromList);
        Assert.False(on.Game(GameId.P2).ContinueFromList);
    }

    /// <summary>Fighto + Priesto parked in Pelnor; Xray's party (alone) active there too.</summary>
    private static (GameSession Session, SessionSnapshot File) TwoParties()
    {
        var s = ClientSessions.Overworld();
        s.EnterTown(1);
        s.OpenGuild();
        var x = s.CreateAndEnroll("Xray", Race.Human, CharacterClass.Wizard);
        var number = Array.IndexOf(s.Roster.Slots, x) + 1;
        Assert.True(s.Continue(Saved(s), number, out var error), error);
        s.OpenGuild();
        Assert.True(s.TryAddToParty(s.Roster.Slots[number - 1]!, out _));
        s.LeaveGuild();
        return (s, Saved(s));
    }

    [OriginalDataFact]
    public void List_ShowsEachParty_OneRowPerLine_EnterCallsItUp()
    {
        var (s, file) = TwoParties();
        var d = new ContinueDialog();
        d.Open(file, fromList: true);
        Assert.True(d.ShowsList);
        var lines = d.Lines(s, ClientSessions.Data);
        Assert.Equal(ContinueDialog.Title, lines[0]);
        Assert.Equal("WHICH PARTY?", lines[1]);
        Assert.Equal(">" + $"{"XRAY",-16}" + "PELNOR", lines[ContinueDialog.FirstRowLine].ToUpperInvariant());
        Assert.Equal(" " + $"{"Fighto Priesto",-16}" + "PELNOR", lines[ContinueDialog.FirstRowLine + 1]); // s99: every name
        Assert.Equal(" BY GUILD NUMBER", lines[ContinueDialog.FirstRowLine + 2]);
        Assert.Equal("Xray", lines[^1]); // the selected party's names
        Assert.All(lines, l => Assert.True(l.Length <= 48, l)); // these fit; a longer one wraps in the panel (s99)

        d.HandleKeys([Keys.Down], s);
        Assert.Equal("Fighto Priesto", d.Lines(s, ClientSessions.Data)[^1]);
        Assert.NotNull(d.HandleKeys([Keys.Enter], s));
        Assert.False(d.IsOpen);
        Assert.True(d.TakeLoaded());
        Assert.Equal(GameMode.Guild, s.Mode); // saved in the Guild, where Xray was called up
        Assert.Equal(["Fighto", "Priesto"], s.Party.Members.Select(m => m.Name));
    }

    [OriginalDataFact]
    public void List_ByGuildNumber_AsksTheManualsQuestion_EscGoesBack()
    {
        var (s, file) = TwoParties();
        var d = new ContinueDialog();
        d.Open(file, fromList: true);
        Assert.Equal(ContinueDialog.Question, d.HandleKeys([Keys.Up, Keys.Enter], s)); // Up wraps to BY GUILD NUMBER
        Assert.False(d.ShowsList);
        Assert.Equal([ContinueDialog.Title, ContinueDialog.Question, "> _"], d.Lines(s, ClientSessions.Data));
        Assert.Equal(ContinueDialog.ListQuestion, d.HandleKeys([Keys.Escape], s));
        Assert.True(d.ShowsList);
        Assert.Equal("Load cancelled.", d.HandleKeys([Keys.Escape], s));
        Assert.False(d.IsOpen);
    }

    [OriginalDataFact]
    public void List_WithNoParty_AsksTheManualsQuestion()
    {
        var s = ClientSessions.Overworld();
        var file = Saved(s);
        file.PartySlotIndices = [];
        var d = new ContinueDialog();
        d.Open(file, fromList: true);
        Assert.False(d.ShowsList);
        Assert.Equal(ContinueDialog.Question, d.Lines(s, ClientSessions.Data)[1]);
    }

    [OriginalDataFact]
    public void List_ScrollsPastEightRows()
    {
        var s = ClientSessions.Overworld();
        var file = Saved(s);
        // Ten one-member parked parties (the roster has room for them in the test file).
        file.RosterSlots = Enumerable.Range(0, 12).Select(i => (Character?)new Character { Name = "M" + i }).ToArray();
        file.PartySlotIndices = [0];
        file.OtherParties = Enumerable.Range(1, 10).Select(i => new SessionSnapshot { PartySlotIndices = [i], Mode = GameMode.Overworld }).ToArray();
        var d = new ContinueDialog();
        d.Open(file, fromList: true);
        Assert.Equal(11, d.Parties.Count);
        var lines = d.Lines(s, ClientSessions.Data);
        Assert.Equal(" ...", lines[ContinueDialog.FirstRowLine + ContinueDialog.VisibleRows]);
        d.HandleKeys([Keys.Up], s); // wraps to BY GUILD NUMBER, the 12th row
        lines = d.Lines(s, ClientSessions.Data);
        Assert.Equal(">BY GUILD NUMBER", lines[ContinueDialog.FirstRowLine + ContinueDialog.VisibleRows - 1]);
        Assert.Equal(" " + $"{"M4",-16}" + "WILDERNESS", lines[ContinueDialog.FirstRowLine]);
        Assert.True(lines.Count <= 13); // fits the panel
    }

    [OriginalDataFact]
    public void List_Mouse_RowsAtLines_WheelStopsAtTheEnds()
    {
        var s = ClientSessions.Overworld();
        var file = Saved(s);
        file.RosterSlots = Enumerable.Range(0, 12).Select(i => (Character?)new Character { Name = "M" + i }).ToArray();
        file.PartySlotIndices = [0];
        file.OtherParties = Enumerable.Range(1, 10).Select(i => new SessionSnapshot { PartySlotIndices = [i], Mode = GameMode.Overworld }).ToArray();
        var d = new ContinueDialog();
        d.Open(file, fromList: true);
        var lines = d.Lines(s, ClientSessions.Data);
        Assert.Null(d.RowAtLine(0)); // title
        Assert.Null(d.RowAtLine(1)); // question
        Assert.Equal(0, d.RowAtLine(ContinueDialog.FirstRowLine));
        Assert.Equal(7, d.RowAtLine(ContinueDialog.FirstRowLine + 7));
        Assert.Equal(" ...", lines[ContinueDialog.FirstRowLine + ContinueDialog.VisibleRows]);
        Assert.Null(d.RowAtLine(ContinueDialog.FirstRowLine + ContinueDialog.VisibleRows)); // "..." is not a row

        d.HandleKeys([Keys.PageUp], s); // the wheel does not wrap to BY GUILD NUMBER
        Assert.Equal(0, d.Cursor);
        d.HandleKeys(Enumerable.Repeat(Keys.PageDown, 20), s);
        Assert.Equal(d.Parties.Count, d.Cursor); // stops on the last row
        Assert.Equal(d.Parties.Count, d.RowAtLine(ContinueDialog.FirstRowLine + ContinueDialog.VisibleRows - 1));

        d.MoveTo(3); // hover
        Assert.Equal(3, d.Cursor);
        Assert.Null(new ContinueDialog().RowAtLine(ContinueDialog.FirstRowLine)); // closed: no rows
    }

    [OriginalDataFact]
    public void Notice_ShowsItsLines_AnyKeyCloses()
    {
        var s = ClientSessions.Overworld();
        var d = new ContinueDialog();
        d.OpenNotice(UiText.S("continue.old_save_lines").Split('\n'));
        Assert.True(d.IsOpen);
        var lines = d.Lines(s, ClientSessions.Data);
        Assert.Equal(ContinueDialog.Title, lines[0]);
        Assert.Contains("OLDER VERSION", string.Join(" ", lines));
        Assert.All(lines, l => Assert.True(l.Length <= 48, l));
        Assert.Null(d.HandleKeys([], s));
        Assert.Equal("", d.HandleKeys([Keys.A], s));
        Assert.False(d.IsOpen);
        Assert.False(d.TakeLoaded());
    }

    [OriginalDataFact]
    public void GuildAdd_DoesNotOfferMembersOfAParkedParty()
    {
        var s = ClientSessions.Overworld();
        s.EnterTown(1);
        s.OpenGuild();
        var x = s.CreateAndEnroll("Xray", Race.Human, CharacterClass.Wizard);
        Assert.True(s.Continue(Saved(s), Array.IndexOf(s.Roster.Slots, x) + 1, out var error), error);
        s.OpenGuild();
        Assert.Equal(["Xray"], GuildAddDialog.Rows(s, ClientSessions.Data).Select(r => r.Member!.Name));
    }
}
