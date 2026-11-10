using Microsoft.Xna.Framework.Input;
using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client;

/// <summary>
/// P1.4 / P2: DOS save import + export (<see cref="DosSaveConverter"/>). Modal: folder path → confirmation → warnings list.
/// Export is a town action only and never overwrites without a YES (P1.4 decisions 6 and 9); import replaces the current
/// game after a YES and loads the DOS files as-is, then lists every warning (decision 3).
/// </summary>
public sealed class DosSaveDialog : IPadYesNo
{
    public enum Step { Closed, EnterPath, ConfirmOverwrite, ConfirmReplace, ShowWarnings, ReviewCharacter }

    private const int WarningsPerPage = 9;

    private readonly ClientSettings _settings;
    private bool _export;
    /// <summary>T5: the game a transfer reads its roster from (I for I → II, II for II → I); null outside a transfer.</summary>
    private GameId? _transferFrom;

    private bool _transfer => _transferFrom is not null;

    /// <summary>T5: the transfer adds to Phantasie III's roster (D3-030), one `transfer.keep_question` each.</summary>
    private bool _intoIII;

    /// <summary>III's transfer: the source slots still to review (occupied only) and the ones kept so far.</summary>
    private IReadOnlyList<Character?> _reviewRoster = [];
    private List<int> _reviewSlots = [];
    private int _reviewAt;
    private List<int> _kept = [];
    private DosSaveSet? _pendingExport;
    private IReadOnlyList<DosSaveWarning> _pendingWarnings = [];
    private List<string> _report = [];

    public DosSaveDialog(ClientSettings settings) => _settings = settings;

    public Step Current { get; private set; } = Step.Closed;
    public bool IsOpen => Current != Step.Closed;

    /// <summary>A Y / N step is up (the overwrite / replace questions, a transfer's keep question).</summary>
    public bool AsksYesNo => Current is Step.ConfirmOverwrite or Step.ReviewCharacter or Step.ConfirmReplace;
    public string Path { get; private set; } = "";
    public string Title { get; private set; } = "";
    public int Scroll { get; private set; }

    /// <summary>Set after an import replaced the game, so the shell can reset its own state; cleared by the shell.</summary>
    public bool Imported { get; set; }

    /// <summary>
    /// T5 transfer: the source game's roster, read and waiting for the shell to start the new game with it
    /// (<see cref="GameSession.TransferFromPhantasieI"/> / <see cref="GameSession.TransferFromPhantasieII"/>); taken once
    /// with <see cref="TakeTransfer"/>.
    /// </summary>
    private IReadOnlyList<Character?>? _transferRoster;

    public IReadOnlyList<Character?>? TakeTransfer()
    {
        var roster = _transferRoster;
        _transferRoster = null;
        return roster;
    }

    /// <summary>III's transfer: the source game and the source slots the player kept, set with the roster.</summary>
    public (GameId From, IReadOnlyCollection<int> Keep)? TransferIntoIII { get; private set; }

    public static string DefaultFolder =>
        System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "OpenPhantasie",
            "dos");

    /// <summary>Open for export. Refused outside a town (manual: "S)AVE GAME is always accomplished in a town").</summary>
    public string? OpenExport(GameSession session)
    {
        if (session.Mode != GameMode.Town)
            return UiText.S("dossave.dos_export_is_a_town_action");
        Open(export: true);
        return null;
    }

    public void OpenImport() => Open(export: false);

    private void Open(bool export)
    {
        _export = export;
        _transferFrom = null;
        _intoIII = false;
        Path = string.IsNullOrWhiteSpace(_settings.DosSaveFolder) ? DefaultFolder : _settings.DosSaveFolder;
        Title = export ? UiText.S("dossave.export_dos_save_to_folder") : UiText.S("dossave.import_dos_save_from_folder");
        Current = Step.EnterPath;
        Scroll = 0;
    }

    /// <summary>
    /// T5 (II's utility, <c>transfer.md</c> sections 7 / 8): the ST's erase question first, then the source game's roster file
    /// (default: the remaster's save of <paramref name="from"/>, <paramref name="savePath"/>). <paramref name="from"/> is
    /// Phantasie I (I → II, opened in II) or Phantasie II (II → I, opened in I).
    /// </summary>
    public void OpenTransfer(string savePath, GameId from)
    {
        _export = false;
        _intoIII = false;
        TransferIntoIII = null;
        _transferFrom = from;
        _transferRoster = null;
        Path = savePath;
        Title = from == GameId.P2 ? UiText.S("transfer.phantasie_ii_save_file") : UiText.S("transfer.phantasie_i_save_file");
        Current = Step.ConfirmReplace;
        Scroll = 0;
    }

    /// <summary>
    /// T5, Phantasie III's transfer from I or II (D3-030: a menu action, any time): the erase question only when
    /// <paramref name="eraseFirst"/> (<c>p3.transfer-roster</c> = <c>erase</c>, D3-003), then the source save, then DOS's
    /// review: each character's name, level, class, race, status, age and attributes and `transfer.keep_question`
    /// (<c>FORMAT.md</c> section 6). The shell takes the roster (<see cref="TakeTransfer"/>) with
    /// <see cref="TransferIntoIII"/>.
    /// </summary>
    public void OpenTransferIntoIII(string savePath, GameId from, bool eraseFirst)
    {
        OpenTransfer(savePath, from);
        _intoIII = true;
        TransferIntoIII = null;
        Current = eraseFirst ? Step.ConfirmReplace : Step.EnterPath;
    }

    /// <summary>Show what a transfer did, once the shell has started the new game (II, or I for II → I).</summary>
    public void ReportTransfer(CharacterTransferResult result, GameId target)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (target == GameId.P3)
        {
            Report(UiText.F("transfer.x_characters_added_to_iii", result.Transferred),
                result.LeftBehind.Count == 0 ? [] : [UiText.F("transfer.not_added_x", string.Join(", ", result.LeftBehind))]);
            return;
        }
        Report(target == GameId.P1
                ? UiText.F("transfer.x_characters_transferred_to_i", result.Transferred)
                : UiText.F("transfer.x_characters_transferred", result.Transferred),
            result.LeftBehind.Count == 0
                ? []
                : [UiText.F("transfer.left_behind_x", string.Join(", ", result.LeftBehind))]);
    }

    /// <summary>Typed characters (MonoGame <c>Window.TextInput</c>) while the path is being edited.</summary>
    public void OnTextInput(char c)
    {
        if (Current != Step.EnterPath)
            return;
        if (c == '\b')
        {
            // DesktopGL sends Backspace here too, with key repeat; the per-frame key edge would drop fast presses.
            _textBackspace = true;
            if (Path.Length > 0)
                Path = Path[..^1];
        }
        else if (c is '\r' or '\n')
            _textEnter = true; // handled in HandleKeys (needs the session)
        else if (!char.IsControl(c))
            Path += c;
    }

    private bool _textEnter;

    private bool _textBackspace;

    /// <summary>Lines the overlay shows for the current step.</summary>
    /// <summary>The warnings page when the report is longer than one (the panel's bar).</summary>
    public ListPage? Page => Current == Step.ShowWarnings ? ListPage.Of(0, WarningsPerPage, Scroll, _report.Count) : null;

    public IReadOnlyList<string> Lines => Current switch
    {
        Step.EnterPath => [Title, Tail(Path + "_", 48)],
        Step.ConfirmOverwrite => [UiText.S("dossave.the_folder_already_holds_dos_save"), Tail(Path, 48), UiText.S("dossave.overwrite_them")],
        Step.ConfirmReplace when _intoIII => [TitleIntoIII, UiText.S("transfer.erase_question_iii"), UiText.S("dossave.unsaved_progress_is_lost")],
        Step.ReviewCharacter => ReviewLines(),
        Step.ConfirmReplace when _transferFrom == GameId.P2 =>
            [UiText.S("transfer.title_ii_to_i"), UiText.S("transfer.erase_question_ii_to_i"), UiText.S("dossave.unsaved_progress_is_lost")],
        Step.ConfirmReplace when _transfer => [UiText.S("transfer.title"), UiText.S("transfer.erase_question"), UiText.S("dossave.unsaved_progress_is_lost")],
        Step.ConfirmReplace => [UiText.S("dossave.import_replaces_the_current_game"), UiText.S("dossave.unsaved_progress_is_lost"), UiText.S("dossave.continue")],
        Step.ShowWarnings => _report.Skip(Scroll).Take(WarningsPerPage).ToList(),
        _ => [],
    };

    public string Help => Current switch
    {
        Step.EnterPath when _transfer => UiText.S("transfer.type_file_del_clear_enter_ok"),
        Step.EnterPath => UiText.S("dossave.type_folder_del_clear_enter_ok"),
        Step.ConfirmOverwrite or Step.ConfirmReplace => UiText.S("dossave.y_yes_n_no"),
        Step.ReviewCharacter => UiText.S("transfer.y_keep_n_skip_esc_cancel"),
        Step.ShowWarnings => _report.Count > WarningsPerPage ? UiText.S("dossave.up_down_scroll_enter_close") : UiText.S("dossave.enter_close"),
        _ => "",
    };

    /// <summary>Handle this frame's new keys. Returns a one-line status for the HUD when the dialog closes or reports.</summary>
    public string? HandleKeys(IEnumerable<Keys> newKeys, GameSession session, GameDataRepository data)
    {
        if (_textEnter)
        {
            _textEnter = false;
            if (Current == Step.EnterPath)
                return ConfirmPath(session, data);
        }
        foreach (var key in newKeys)
        {
            switch (Current)
            {
                case Step.EnterPath:
                    if (key == Keys.Escape) { Current = Step.Closed; return UiText.S("dossave.cancelled"); }
                    if (key == Keys.Delete) { Path = ""; continue; }
                    if (key == Keys.Back && !_textBackspace && Path.Length > 0) { Path = Path[..^1]; continue; }
                    if (key == Keys.Enter) return ConfirmPath(session, data);
                    continue;

                case Step.ConfirmOverwrite:
                    if (key == Keys.Y) return WriteExport();
                    if (key is Keys.N or Keys.Escape) { Current = Step.Closed; return UiText.S("dossave.export_cancelled_nothing_written"); }
                    continue;

                case Step.ReviewCharacter:
                    if (key == Keys.Escape) { Current = Step.Closed; return UiText.S("transfer.cancelled"); }
                    if (key is not (Keys.Y or Keys.N)) continue;
                    if (key == Keys.Y) _kept.Add(_reviewSlots[_reviewAt]);
                    if (++_reviewAt < _reviewSlots.Count) continue;
                    _transferRoster = _reviewRoster;
                    TransferIntoIII = (_transferFrom!.Value, _kept.ToArray());
                    Current = Step.Closed;
                    return null;

                case Step.ConfirmReplace when _transfer:
                    if (key == Keys.Y) { Current = Step.EnterPath; return null; }
                    if (key is Keys.N or Keys.Escape) { Current = Step.Closed; return UiText.S("transfer.cancelled"); }
                    continue;

                case Step.ConfirmReplace:
                    if (key == Keys.Y) return RunImport(session, data);
                    if (key is Keys.N or Keys.Escape) { Current = Step.Closed; return UiText.S("dossave.import_cancelled"); }
                    continue;

                case Step.ShowWarnings:
                    if (key is Keys.Up or Keys.W) Scroll = Math.Max(0, Scroll - 1);
                    else if (key is Keys.Down or Keys.S)
                        Scroll = Math.Min(Math.Max(0, _report.Count - WarningsPerPage), Scroll + 1);
                    else if (key is Keys.Enter or Keys.Escape or Keys.Space) Current = Step.Closed;
                    continue;
            }
        }
        return null;
    }

    private string? ConfirmPath(GameSession session, GameDataRepository data)
    {
        var folder = Path.Trim();
        if (folder.StartsWith('~'))
            folder = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + folder[1..];
        Path = folder;
        if (_transfer)
            return ReadTransferSource(folder);
        _settings.DosSaveFolder = folder;
        _settings.Save();

        if (!_export)
        {
            if (!Directory.Exists(folder) || DosSaveSet.ExistingFiles(folder).Count == 0)
                return Report(UiText.S("dossave.no_dos_save_files_found"), [UiText.F("dossave.x_holds_none_of_x", folder, string.Join(", ", DosSaveSet.FileNames))]);
            Current = Step.ConfirmReplace;
            return null;
        }

        var result = DosSaveConverter.ExportFrom(session, data);
        if (result.Blocked)
            return Report(UiText.S("dossave.export_blocked_nothing_written"), result.Warnings);
        _pendingExport = result.Files;
        _pendingWarnings = result.Warnings;
        if (DosSaveSet.ExistingFiles(folder).Count > 0)
        {
            Current = Step.ConfirmOverwrite;
            return null;
        }
        return WriteExport();
    }

    private string WriteExport()
    {
        try
        {
            _pendingExport!.WriteFolder(Path, overwrite: true);
            return Report(UiText.F("dossave.exported_x_dos_files", DosSaveSet.FileNames.Count), _pendingWarnings);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Report(UiText.S("dossave.export_failed"), [ex.Message]);
        }
        finally
        {
            _pendingExport = null;
        }
    }

    private string RunImport(GameSession session, GameDataRepository data)
    {
        try
        {
            var warnings = DosSaveConverter.ImportInto(session, DosSaveSet.ReadFolder(Path), data);
            Imported = true;
            return Report(UiText.S("dossave.dos_save_imported"), warnings);
        }
        catch (Exception ex) // a malformed set must not take the shell down; the snapshot is applied only on success
        {
            return Report(UiText.S("dossave.import_failed_game_unchanged"), [ex.Message]);
        }
    }

    /// <summary>T5: read the source game's roster from the save at <paramref name="file"/>; the shell takes it (<see cref="TakeTransfer"/>).</summary>
    private string? ReadTransferSource(string file)
    {
        try
        {
            if (!File.Exists(file))
                return Report(UiText.S("transfer.failed_game_unchanged"), [UiText.F("transfer.no_file_x", file)]);
            var roster = TransferSource.ReadRoster(file, _transferFrom!.Value);
            if (_intoIII)
                return StartReview(roster);
            _transferRoster = roster;
            Current = Step.Closed;
            return null;
        }
        catch (OldSaveFormatException)
        {
            return Report(UiText.S("transfer.failed_game_unchanged"), [UiText.S("continue.old_save")]);
        }
        catch (Exception ex) // another game's save or an unreadable file must not take the shell down
        {
            return Report(UiText.S("transfer.failed_game_unchanged"),
                [ex is InvalidDataException
                    ? _transferFrom == GameId.P2 ? UiText.S("transfer.not_a_phantasie_ii_save") : UiText.S("transfer.not_a_phantasie_i_save")
                    : ex.Message]);
        }
    }

    private string? StartReview(IReadOnlyList<Character?> roster)
    {
        _reviewRoster = roster;
        _reviewSlots = Enumerable.Range(0, roster.Count).Where(i => roster[i] is not null).ToList();
        _reviewAt = 0;
        _kept = [];
        if (_reviewSlots.Count == 0)
            return Report(UiText.S("transfer.failed_game_unchanged"), [UiText.S("transfer.no_characters")]);
        Current = Step.ReviewCharacter;
        return null;
    }

    private string TitleIntoIII => _transferFrom == GameId.P2
        ? UiText.S("transfer.title_into_iii_from_ii")
        : UiText.S("transfer.title_into_iii_from_i");

    /// <summary>DOS III's review screen (ovl1): the character, then the question.</summary>
    private IReadOnlyList<string> ReviewLines()
    {
        var c = _reviewRoster[_reviewSlots[_reviewAt]]!;
        var s = c.Stats;
        return
        [
            TitleIntoIII,
            "",
            c.Name,
            UiText.F("transfer.level_x_x_x_x", c.Level, InspectDialog.Named(InspectDialog.ClassNames, (int)c.Class),
                InspectDialog.Named(InspectDialog.RaceNames, (int)c.Race), InspectDialog.Named(InspectDialog.StatusNames, (int)c.Status)),
            UiText.F("transfer.age_x", c.AgeClock / 26),
            UiText.F("transfer.stats_x_x_x_x_x_x", s.Str, s.Int, s.Dex, s.Con, s.Cha, s.Luck),
            "",
            UiText.S("transfer.keep_question"),
        ];
    }

    private string Report(string heading, IReadOnlyList<DosSaveWarning> warnings) =>
        Report(heading, warnings.Select(w => w.ToString()).ToList());

    private string Report(string heading, IReadOnlyList<string> lines)
    {
        _report = [heading, .. (lines.Count == 0 ? [UiText.S("dossave.no_warnings")] : lines).SelectMany(l => Wrap(l, 48))];
        Scroll = 0;
        Current = Step.ShowWarnings;
        return lines.Count == 0 ? heading : UiText.F("dossave.x_x_note_s", heading, lines.Count);
    }

    private static IEnumerable<string> Wrap(string text, int width)
    {
        var line = "";
        foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (line.Length > 0 && PixelFont.Measure(line + " " + word) > width * PixelFont.Advance)
            {
                yield return line;
                line = "  ";
            }
            line = line.Trim().Length == 0 ? line + word : line + " " + word;
        }
        if (line.Trim().Length > 0)
            yield return line;
    }

    /// <summary><paramref name="s"/> in <paramref name="max"/> layout columns (the font's widths), its end kept behind "...".</summary>
    private static string Tail(string s, int max) => PixelFont.Tail(s, max * PixelFont.Advance);
}
