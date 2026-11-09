using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client;

/// <summary>
/// QoL SAVE SLOTS (<c>docs/plan/12-save-slots.md</c>, <see cref="GameSettings.SaveSlots"/>): the shell's side of the slot
/// picker (<see cref="SaveSlotScreen"/>). Save and Load in every game open it when the game's switch is on; picking a game
/// on the main menu opens it on NEW GAME and the game's saves when any slot holds one. The autosave on leaving town keeps
/// writing the game's one save (the AUTO slot) and never asks.
/// </summary>
public partial class Game1
{
    private readonly SaveSlotScreen _slotScreen = new();

    /// <summary>Opens the picker on <paramref name="game"/>'s slots (Save: the numbered ones; Load / Start: all).</summary>
    private void OpenSaveSlots(GameId game, SaveSlotMode mode)
    {
        var dir = ClientSettings.DataDirectory;
        var slots = mode == SaveSlotMode.Save ? SaveSlots.Writable(dir, game) : SaveSlots.All(dir, game);
        _slotScreen.Open(game, mode, SaveSlots.Describe(slots), PlaceNamer(game));
    }

    /// <summary>
    /// A game picked on the main menu (after its title splash): with SAVE SLOTS on and a save in any slot, the picker on
    /// NEW GAME and the saves; else a new game, as before.
    /// </summary>
    private void StartOrOfferSaves(GameId game)
    {
        if (_settings.Game(game).SaveSlots
            && SaveSlots.All(ClientSettings.DataDirectory, game).Any(s => File.Exists(s.Path)))
        {
            OpenSaveSlots(game, SaveSlotMode.Start);
            return;
        }
        StartNew(game);
    }

    private void StartNew(GameId game)
    {
        if (game == GameId.P4)
            StartNewPhantasie4();
        else
            StartGame(game);
    }

    /// <summary>IV's save of title slot <paramref name="slot"/>: with SAVE SLOTS that slot's file, else the one save.</summary>
    private string P4SlotPath(int slot) =>
        _settings.Game(GameId.P4).SaveSlots && slot >= 1 && slot <= SaveSlots.NumberedCount(GameId.P4)
            ? SaveSlots.PathOf(ClientSettings.DataDirectory, GameId.P4, slot)
            : ClientSettings.SavePathFor(GameId.P4);

    /// <summary>The picker closed: runs its pick (Esc on Save says nothing was saved).</summary>
    private void RunSaveSlotPick()
    {
        var game = _slotScreen.Game;
        var mode = _slotScreen.Mode;
        if (_slotScreen.TakePick() is not { } pick)
        {
            if (mode != SaveSlotMode.Save)
                return;
            if (_p4 is not null)
                _p4.Note = UiText.S("saveslots.not_saved");
            else if (_session is not null)
                SetFeedback(UiText.S("saveslots.not_saved"), HelpForMode(_session.Mode));
            return;
        }
        switch (pick)
        {
            case { Mode: SaveSlotMode.Save, Slot: { } slot } when game == GameId.P4:
                WriteP4Save(slot.Path, slot);
                break;
            case { Mode: SaveSlotMode.Save, Slot: { } slot }:
                WriteSave(slot.Path, slot);
                break;
            case { Mode: SaveSlotMode.Load, Slot: { } slot } when game == GameId.P4:
                LoadP4From(slot.Path);
                break;
            case { Mode: SaveSlotMode.Load, Slot: { } slot }:
                LoadFrom(slot.Path);
                break;
            case { Mode: SaveSlotMode.Start, Slot: null }:
                StartNew(game);
                break;
            case { Mode: SaveSlotMode.Start, Slot: { } slot } when game == GameId.P4:
                LoadSavedPhantasie4(slot.Number);
                break;
            case { Mode: SaveSlotMode.Start, Slot: { } slot }:
                StartGame(game); // as F9 after a new game: the save's own rules replace the new game's
                LoadFrom(slot.Path);
                break;
        }
    }

    /// <summary>Writes IV's campaign to <paramref name="path"/> (<paramref name="slot"/>: the slot it is, for the note).</summary>
    private void WriteP4Save(string path, SaveSlot? slot)
    {
        try
        {
            Directory.CreateDirectory(ClientSettings.DataDirectory);
            Phantasie4SaveCodec.WriteJsonFile(path, _p4!.Game.Capture());
            _p4.Note = slot is null ? UiText.S("p4.dungeon.saved") : UiText.F("saveslots.saved_x", SaveSlotScreen.Label(slot));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _p4!.Note = UiText.F("saveslots.save_failed_x", ex.Message); // IV's table has no game.* ids
        }
    }

    /// <summary>F9's load of IV's save at <paramref name="path"/> onto the running IV screen.</summary>
    private void LoadP4From(string path)
    {
        try
        {
            _p4!.Replace(Phantasie4World.RestoreCampaign(Phantasie4SaveCodec.ReadJsonFile(path),
                new SeededRandomSource(Environment.TickCount)));
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or ArgumentException
                                       or KeyNotFoundException or InvalidOperationException)
        {
            _p4!.Note = UiText.F("p4.dungeon.load_failed_x", ex.Message);
        }
    }

    /// <summary>
    /// Names a slot's town for the picker when the game's names are at hand: I–III's towns from the running game's data
    /// (or, from the main menu, the game's data read for it), IV's from the running campaign's tables. Null = its number.
    /// </summary>
    private Func<SavePlace, int?, string?> PlaceNamer(GameId game)
    {
        if (game == GameId.P4)
        {
            var ids = _p4?.Game.Tables.Service.TownNameIds;
            return (place, n) => place == SavePlace.Town && ids is not null && n is int i && i > 0 && i < ids.Count
                ? GameText.S(ids[i]) : null;
        }
        var data = _session?.Game == game ? _data : TryLoadData(game);
        return (place, n) => place == SavePlace.Town && data is not null && n is int id && data.Towns.Any(t => t.Id == id)
            ? data.GetTown(id).Name : null;
    }

    private static GameDataRepository? TryLoadData(GameId game)
    {
        try
        {
            return GameDataRepository.Load(GameDataRepository.DefaultPathFor(game));
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException
                                       or System.Text.Json.JsonException or KeyNotFoundException)
        {
            return null;
        }
    }
}
