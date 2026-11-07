using Microsoft.Xna.Framework.Input;
using OpenPhantasie.Client;
using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client.Tests;

/// <summary>
/// Phase 09 T5: II's "Transfer from Phantasie I" (the ST utility's flow, <c>transfer.md</c> §7) on the DOS save dialog:
/// the erase question, then I's save file; the shell takes the roster once.
/// </summary>
public class TransferDialogTests
{
    private static readonly GameDataRepository P2Data = GameDataRepository.Load(GameDataRepository.DefaultPathFor(GameId.P2));

    private static GameSession NewIi()
    {
        var s = GameSessionFactory.Create(P2Data, new SeededDosRandomSource(3));
        s.StartNewGame();
        return s;
    }

    private static void Type(DosSaveDialog d, string text)
    {
        foreach (var c in text)
            d.OnTextInput(c);
    }

    [Fact]
    public void IIsGameMenu_OffersTheTransfer_InTheDosImportRow()
    {
        Assert.Equal("Transfer from I  F7", UiText.For(GameId.P2).Get("gamemenu.import_dos_save_f7"));
        Assert.Equal("Import DOS save  F7", UiText.For(GameId.P1).Get("gamemenu.import_dos_save_f7"));
    }

    [OriginalDataFact]
    public void Transfer_AsksTheErase_ThenReadsIsSave_AndHandsTheRosterOverOnce()
    {
        var dir = Directory.CreateTempSubdirectory("p2-transfer-ui-");
        try
        {
            var i = GameSessionFactory.Create(ClientSessions.Data, new SeededDosRandomSource(1));
            i.StartNewGame();
            i.OpenGuild();
            var rolled = i.RollNewMember(Race.Elf, CharacterClass.Wizard);
            Assert.True(i.TryEnrollNewMember(rolled, "MERLA", out var err), err);
            var path = Path.Combine(dir.FullName, "savegame-p1.json");
            SessionCodec.WriteJsonFile(path, i.CaptureSnapshot());

            var ii = NewIi();
            var d = new DosSaveDialog(new ClientSettings());
            d.OpenTransfer(path, GameId.P1);
            Assert.Equal(DosSaveDialog.Step.ConfirmReplace, d.Current); // the ST's question comes first
            Assert.Contains(UiText.S("transfer.erase_question"), d.Lines);
            d.HandleKeys([Keys.Y], ii, P2Data);
            Assert.Equal(DosSaveDialog.Step.EnterPath, d.Current);
            Assert.Equal(path, d.Path); // default: the remaster's I save
            Assert.Equal(UiText.S("transfer.type_file_del_clear_enter_ok"), d.Help);
            d.HandleKeys([Keys.Enter], ii, P2Data);
            Assert.False(d.IsOpen);
            var roster = d.TakeTransfer();
            Assert.NotNull(roster);
            Assert.Equal(["MERLA"], roster!.OfType<Character>().Select(c => c.Name));
            Assert.Null(d.TakeTransfer());

            // What the shell then does: a new II game with the roster, and the count shown.
            var result = ii.TransferFromPhantasieI(roster);
            d.ReportTransfer(result, GameId.P2);
            Assert.Equal(DosSaveDialog.Step.ShowWarnings, d.Current);
            Assert.Equal(UiText.F("transfer.x_characters_transferred", 1), d.Lines[0]);
            Assert.Equal("MERLA", ii.Roster.Slots[0]!.Name);
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [OriginalDataFact]
    public void Transfer_NoAtTheQuestion_Cancels()
    {
        var d = new DosSaveDialog(new ClientSettings());
        d.OpenTransfer("unused.json", GameId.P1);
        Assert.Equal(UiText.S("transfer.cancelled"), d.HandleKeys([Keys.N], NewIi(), P2Data));
        Assert.False(d.IsOpen);
        Assert.Null(d.TakeTransfer());
    }

    [OriginalDataFact]
    public void Transfer_RefusesAMissingFile_AndAnotherGamesSave()
    {
        var dir = Directory.CreateTempSubdirectory("p2-transfer-ui-");
        try
        {
            var ii = NewIi();
            var d = new DosSaveDialog(new ClientSettings());
            d.OpenTransfer(Path.Combine(dir.FullName, "missing.json"), GameId.P1);
            d.HandleKeys([Keys.Y], ii, P2Data);
            d.HandleKeys([Keys.Enter], ii, P2Data);
            Assert.Equal(DosSaveDialog.Step.ShowWarnings, d.Current);
            Assert.Equal(UiText.S("transfer.failed_game_unchanged"), d.Lines[0]);
            Assert.Null(d.TakeTransfer());

            var p2Save = Path.Combine(dir.FullName, "savegame-p2.json");
            SessionCodec.WriteJsonFile(p2Save, ii.CaptureSnapshot());
            d.OpenTransfer("", GameId.P1);
            d.HandleKeys([Keys.Y], ii, P2Data);
            Type(d, p2Save);
            d.HandleKeys([Keys.Enter], ii, P2Data);
            Assert.Equal(DosSaveDialog.Step.ShowWarnings, d.Current);
            Assert.Contains(UiText.S("transfer.not_a_phantasie_i_save"), d.Lines);
            Assert.Null(d.TakeTransfer());
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Fact]
    public void IsGameMenu_HasTheIIToIRow_OtherGamesDoNot()
    {
        Assert.Contains(GameMenuDialog.EntriesFor(GameId.P1), e => e.Choice == GameMenuChoice.TransferFromII);
        Assert.Equal("Transfer from II", UiText.For(GameId.P1).Get("gamemenu.transfer_from_ii"));
        foreach (var g in new[] { GameId.P2, GameId.P4 })
            Assert.DoesNotContain(GameMenuDialog.EntriesFor(g), e => e.Choice is GameMenuChoice.TransferFromII or GameMenuChoice.TransferFromI);
    }

    [Fact]
    public void IIIsGameMenu_TransfersFromIAndII_OrIOnlyUnderDos()
    {
        Assert.Equal([GameMenuChoice.TransferFromI, GameMenuChoice.TransferFromII],
            GameMenuDialog.EntriesFor(GameId.P3, GameOptions.ForPreset(GameId.P3))
                .Select(e => e.Choice).Where(c => c is GameMenuChoice.TransferFromI or GameMenuChoice.TransferFromII));
        Assert.Equal([GameMenuChoice.TransferFromI],
            GameMenuDialog.EntriesFor(GameId.P3, GameOptions.ForPreset(GameId.P3, OptionPreset.Dos))
                .Select(e => e.Choice).Where(c => c is GameMenuChoice.TransferFromI or GameMenuChoice.TransferFromII));
        Assert.Equal("Transfer from I", UiText.For(GameId.P3).Get("gamemenu.transfer_from_i"));
    }

    [OriginalDataFact]
    public void IntoIII_NoEraseQuestion_ReviewsEachCharacter_AndAddsTheKeptOnes()
    {
        var dir = Directory.CreateTempSubdirectory("p3-transfer-ui-");
        try
        {
            var i = GameSessionFactory.Create(ClientSessions.Data, new SeededDosRandomSource(1));
            i.StartNewGame();
            i.OpenGuild();
            foreach (var name in new[] { "KEEPA", "SKIPB", "KEEPC" })
                Assert.True(i.TryEnrollNewMember(i.RollNewMember(Race.Elf, CharacterClass.Wizard), name, out var err), err);
            var path = Path.Combine(dir.FullName, "savegame-p1.json");
            SessionCodec.WriteJsonFile(path, i.CaptureSnapshot());

            var p3Data = GameDataRepository.Load(GameDataRepository.DefaultPathFor(GameId.P3));
            var iii = GameSessionFactory.Create(p3Data, new SeededDosRandomSource(4));
            iii.StartNewGame();
            var d = new DosSaveDialog(new ClientSettings());
            d.OpenTransferIntoIII(path, GameId.P1, eraseFirst: false);
            Assert.Equal(DosSaveDialog.Step.EnterPath, d.Current); // adding (D3-003): nothing to confirm
            d.HandleKeys([Keys.Enter], iii, p3Data);
            Assert.Equal(DosSaveDialog.Step.ReviewCharacter, d.Current);
            Assert.Equal("KEEPA", d.Lines[2]);
            Assert.Equal(UiText.S("transfer.keep_question"), d.Lines[^1]);
            d.HandleKeys([Keys.Y], iii, p3Data);
            Assert.Equal("SKIPB", d.Lines[2]);
            d.HandleKeys([Keys.N], iii, p3Data);
            d.HandleKeys([Keys.Y], iii, p3Data);
            Assert.False(d.IsOpen);

            var roster = d.TakeTransfer();
            Assert.Equal(GameId.P1, d.TransferIntoIII!.Value.From);
            Assert.Equal([0, 2], d.TransferIntoIII.Value.Keep);
            d.ReportTransfer(iii.TransferIntoPhantasieIII(roster!, GameId.P1, d.TransferIntoIII.Value.Keep), GameId.P3);
            Assert.Equal(UiText.F("transfer.x_characters_added_to_iii", 2), d.Lines[0]);
            Assert.Equal(["KEEPA", "KEEPC"], iii.Roster.Occupied.Select(c => c.Name).Order());

            d.OpenTransferIntoIII(path, GameId.P1, eraseFirst: true);
            Assert.Equal(DosSaveDialog.Step.ConfirmReplace, d.Current);
            Assert.Contains(UiText.S("transfer.erase_question_iii"), d.Lines);
            d.HandleKeys([Keys.Y], iii, p3Data);
            d.HandleKeys([Keys.Enter], iii, p3Data);
            Assert.Equal(UiText.S("transfer.cancelled"), d.HandleKeys([Keys.Escape], iii, p3Data));
            Assert.Null(d.TakeTransfer());
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [OriginalDataFact]
    public void TransferFromII_AsksThePhantasie1Erase_ReadsIIsSave_AndRefusesIsOwn()
    {
        var dir = Directory.CreateTempSubdirectory("p1-transfer-ui-");
        try
        {
            var ii = NewIi();
            ii.OpenGuild();
            var rolled = ii.RollNewMember(Race.Dwarf, CharacterClass.Fighter);
            Assert.True(ii.TryEnrollNewMember(rolled, "GLADE", out var err), err);
            var path = Path.Combine(dir.FullName, "savegame-p2.json");
            SessionCodec.WriteJsonFile(path, ii.CaptureSnapshot());

            var i = GameSessionFactory.Create(ClientSessions.Data, new SeededDosRandomSource(1));
            i.StartNewGame();
            var d = new DosSaveDialog(new ClientSettings());
            d.OpenTransfer(path, GameId.P2);
            Assert.Equal([UiText.S("transfer.title_ii_to_i"), UiText.S("transfer.erase_question_ii_to_i"),
                UiText.S("dossave.unsaved_progress_is_lost")], d.Lines);
            d.HandleKeys([Keys.Y], i, ClientSessions.Data);
            Assert.Equal(UiText.S("transfer.phantasie_ii_save_file"), d.Title);
            d.HandleKeys([Keys.Enter], i, ClientSessions.Data);
            var roster = d.TakeTransfer();
            Assert.Equal(["GLADE"], roster!.OfType<Character>().Select(c => c.Name));

            d.ReportTransfer(i.TransferFromPhantasieII(roster), GameId.P1);
            Assert.Equal(UiText.F("transfer.x_characters_transferred_to_i", 1), d.Lines[0]);

            var p1Save = Path.Combine(dir.FullName, "savegame-p1.json");
            SessionCodec.WriteJsonFile(p1Save, i.CaptureSnapshot());
            d.OpenTransfer(p1Save, GameId.P2);
            d.HandleKeys([Keys.Y], i, ClientSessions.Data);
            d.HandleKeys([Keys.Enter], i, ClientSessions.Data);
            Assert.Contains(UiText.S("transfer.not_a_phantasie_ii_save"), d.Lines);
            Assert.Null(d.TakeTransfer());
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }
}
