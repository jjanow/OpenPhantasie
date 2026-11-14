using Microsoft.Xna.Framework.Input;
using OpenPhantasie.Core;
using OpenPhantasie.Data;
using OpenPhantasie.Importer;
using OpenPhantasie.Importer.Profiles;

namespace OpenPhantasie.Client;

/// <summary>
/// The first-run import screen (10-importer.md 4.1, user decisions s158): the remaster ships no game data, so it asks for
/// the player's own copies (disk images, zips, installed folders, Steam / GOG libraries), imports what it finds and
/// says what was imported, what was not and why (unsupported version, missing disk, a played copy's losses). Shown at
/// start when no game is available, and from Options (IMPORT GAMES). A shell state like the Options screen.
/// <para>Rows: a path typed in (Return adds it; files dropped on the window are added too), the paths added (Delete
/// removes one), the Steam / GOG library folders found on this machine, IMPORT, DONE. While importing a progress line
/// shows; then the report replaces the list until a key is pressed.</para>
/// <para>s160: when the report names a file the import still lacks (<see cref="ImportReport.Offers"/>: Amiga I / III
/// imported without the Kickstart 1.3 ROM, so no topaz font), A asks for that file: only the path row shows, and the
/// path typed or dropped there is imported at once, alone (the rest of the import stays).</para>
/// <para>s205: the paths are scanned first; for a played copy whose maps would come over played (DOS I; the Apple II's
/// II, D2-052: <see cref="ImportRunner.CanRestoreMaps"/>) the player is asked whether to restore them as on a fresh copy, then the
/// import runs.</para>
/// </summary>
public sealed class ImportScreen
{
    /// <summary>Characters per line on the 320-wide screen (x 14 to 306).</summary>
    public const int Columns = (320 - 28) / PixelFont.Advance;

    /// <summary>
    /// s185: the lines under the title a menu screen holds (<c>UiOverlays.DrawMenuScreen</c>: from y <see cref="FirstLineY"/>
    /// at a <see cref="LinePitch"/>-pixel pitch, the last at y 170, clear of the help line at 186). The rows (or the report,
    /// or the browser's rows) take what the header leaves; more scroll.
    /// </summary>
    public const int ScreenLines = 15;

    /// <summary>Top of the first line under the title on the menu screen, and the pitch of its lines.</summary>
    public const int FirstLineY = 30, LinePitch = 10;

    /// <summary>Lines of rows (or report, or browser rows) shown at once under the current header; more scroll.</summary>
    public int VisibleLines => Math.Max(1, ScreenLines - (HeaderCount - 1));

    /// <summary>
    /// s185: the rows' (report's, browser's) scroll: its bar shows on the screen's right when they don't fit
    /// (<see cref="UiOverlays.DrawImportScreen"/>), the wheel over them and the bar move it, and it follows the cursor.
    /// </summary>
    public ScrollBox Scroll { get; } = new();

    private readonly Func<IReadOnlyList<string>, IProgress<string>, ImportReport>? _run;
    private readonly Func<IReadOnlyList<string>, ScanResult> _scan;
    private readonly Func<ScanResult, IReadOnlySet<string>, IProgress<string>, ImportReport> _import;
    private Task<ScanResult>? _scanTask;
    private ScanResult? _pendingScan;
    private readonly List<Detection> _restoreQueue = [];
    private readonly HashSet<string> _restore = new(StringComparer.Ordinal);
    private readonly Func<IProgress<string>, ImportReport> _refresh;
    private readonly Func<IReadOnlyList<StoreLibraries.Library>> _libraries;
    private readonly List<string> _paths = [];
    private IReadOnlyList<StoreLibraries.Library> _found = [];
    private Task<ImportReport>? _task;
    private volatile string _progress = "";
    private List<MenuLine> _report = [];
    private ImportOffer? _offer;

    /// <param name="run">Scan and import the paths in one step, asking nothing (tests). Default: the paths are scanned
    /// (<paramref name="scan"/>), a played copy's maps asked about, then imported (<paramref name="import"/>).</param>
    /// <param name="refresh">Re-run the versions built by an older recipe (default: <see cref="ImportRunner.Refresh"/>).</param>
    /// <param name="browseHome">Where the folder browser starts (default: the user's home folder).</param>
    public ImportScreen(string importRoot,
        Func<IReadOnlyList<string>, IProgress<string>, ImportReport>? run = null,
        Func<IReadOnlyList<StoreLibraries.Library>>? libraries = null,
        Func<IProgress<string>, ImportReport>? refresh = null,
        string? browseHome = null,
        Func<IReadOnlyList<string>, ScanResult>? scan = null,
        Func<ScanResult, IReadOnlySet<string>, IProgress<string>, ImportReport>? import = null)
    {
        ImportRoot = importRoot;
        _browser = new FileBrowser(browseHome);
        _run = run;
        _scan = scan ?? (paths => ImportRunner.Scan(paths));
        _import = import ?? ((found, restore, progress) =>
            new ImportRunner(importRoot, GameDataLocator.ShippedRoot).Run(found, progress, restoreMaps: restore));
        _refresh = refresh ?? (progress => new ImportRunner(importRoot, GameDataLocator.ShippedRoot).Refresh(progress));
        _libraries = libraries ?? StoreLibraries.Find;
    }

    public string ImportRoot { get; }
    public bool IsOpen { get; private set; }
    /// <summary>Opened at start because no game is available (the welcome text shows).</summary>
    public bool FirstRun { get; private set; }
    public bool Running => _task is { IsCompleted: false } || _scanTask is not null;

    /// <summary>s205: asking whether a played copy's maps are restored (<see cref="RestoreTitle"/>); rows RESTORE / KEEP.</summary>
    public bool AskingRestore => _restoreQueue.Count > 0;

    /// <summary>The version asked about while <see cref="AskingRestore"/>.</summary>
    public string? RestoreTitle => AskingRestore ? _restoreQueue[0].Profile.Title : null;
    public bool ShowingReport { get; private set; }
    /// <summary>Selected row (see <see cref="RowCount"/>); while browsing, the browser's row.</summary>
    public int Cursor
    {
        get => Browsing ? _browser.Cursor : _cursor;
        private set => _cursor = value;
    }

    private int _cursor;
    /// <summary>Set when an import committed something; the shell reloads data and art.</summary>
    public bool Imported { get; private set; }
    public IReadOnlyList<string> Paths => _paths;
    public string Typed { get; private set; } = "";

    /// <summary>The missing file the last report offered to add (null: none).</summary>
    public ImportOffer? Offer => _offer;

    /// <summary>Asking for <see cref="Offer"/>'s file: the next path given is imported alone.</summary>
    public bool AskingForOffer { get; private set; }

    /// <summary>Run when an import committed (the shell re-reads the import: <see cref="GameDataLocator.UseImport"/>).</summary>
    public Action? OnImported { get; set; }

    // --- The folder browser (s183) ------------------------------------------------------------------------------------
    // A pad can't type a path, so Tab (pad Y) on the path row, or on the missing file's row, opens a browser of the
    // folders and files on this machine (<see cref="FileBrowser"/>, shared with the DOS save dialog since s190). The
    // keyboard can use it too.

    private readonly FileBrowser _browser;

    /// <summary>The folder browser is open (it takes the keys and the rows).</summary>
    public bool Browsing { get; private set; }

    /// <summary>The folder the browser shows (null: the drive list).</summary>
    public string? BrowseFolder => _browser.Folder;

    /// <summary>The browser's rows (<see cref="Cursor"/> picks one while browsing).</summary>
    public IReadOnlyList<FileBrowser.Entry> BrowseEntries => _browser.Entries;

    /// <summary>Open the browser where the typed path points (its folder), else where it was last, else at home.</summary>
    private void OpenBrowser()
    {
        Browsing = true;
        Scroll.Reset();
        _browser.Open(Checked(Typed));
    }

    /// <summary>The browser's keys.</summary>
    private void BrowseKey(Keys key)
    {
        var folder = _browser.Folder;
        switch (_browser.Key(key, VisibleLines))
        {
            case FileBrowser.Outcome.Closed:
                Browsing = false;
                Scroll.Reset();
                break;
            case FileBrowser.Outcome.Picked:
                BrowsePick(_browser.Picked);
                break;
            default:
                if (_browser.Folder != folder)
                    Scroll.Reset();
                break;
        }
    }

    /// <summary>The browser's pick: the missing file is imported at once; else the path is added and the cursor goes to IMPORT.</summary>
    private void BrowsePick(string path)
    {
        Browsing = false;
        Scroll.Reset();
        if (AskingForOffer)
        {
            if (Checked(path) is { } file)
                Start([file]);
            return;
        }
        if (AddPath(path))
            Cursor = ImportRow;
    }

    /// <summary>The browser's lines: the folder it shows (wrapped, nothing cut), then a note when it can't be read.</summary>
    private List<MenuLine> BrowseHeader()
    {
        var lines = new List<MenuLine>
        {
            new(AskingForOffer ? UiText.S("import.browse.title.offer") : UiText.S("import.browse.title"), MenuLineStyle.Title),
        };
        lines.AddRange(Wrap(_browser.Folder ?? UiText.S("import.browse.drives"), MenuLineStyle.Header));
        if (_browser.Unreadable)
            lines.AddRange(Wrap(UiText.S("import.browse.unreadable"), MenuLineStyle.Dim));
        return lines;
    }

    private List<MenuLine> BrowseRows() =>
        [.. _browser.Entries.Select((e, i) => new MenuLine((i == _browser.Cursor ? ">" : " ") + Tail(e.Name, Columns - 1),
            i == _browser.Cursor ? MenuLineStyle.Selected : e.Kind == FileBrowser.EntryKind.UseFolder ? MenuLineStyle.Header : MenuLineStyle.Normal))];

    public void Open(bool firstRun)
    {
        FirstRun = firstRun;
        IsOpen = true;
        ShowingReport = false;
        AskingForOffer = false;
        Browsing = false;
        _offer = null;
        _restoreQueue.Clear();
        _pendingScan = null;
        Imported = false;
        Cursor = 0;
        Scroll.Reset();
        try { _found = _libraries(); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { _found = []; }
    }

    // Rows: 0 = typed path, then the added paths, then libraries not yet added, then IMPORT, then DONE.
    private IReadOnlyList<StoreLibraries.Library> Libraries => _found.Where(l => !_paths.Contains(l.Path)).ToList();
    private int PathRow(int i) => 1 + i;
    private int LibraryRow(int i) => 1 + _paths.Count + i;
    private int ImportRow => 1 + _paths.Count + Libraries.Count;
    private int DoneRow => ImportRow + 1;
    public int RowCount => AskingRestore ? 2 : AskingForOffer ? 1 : DoneRow + 1;

    /// <summary>Text typed (Window.TextInput): goes into the path row; backspace deletes.</summary>
    public void OnTextInput(char c)
    {
        if (!IsOpen || Running || ShowingReport || Browsing || AskingRestore || Cursor != 0)
            return;
        if (c == '\b')
        {
            if (Typed.Length > 0)
                Typed = Typed[..^1];
        }
        else if (!char.IsControl(c) && Typed.Length < 1024)
            Typed += c;
    }

    /// <summary>Files or folders dropped on the window.</summary>
    public void AddPaths(IEnumerable<string> paths)
    {
        if (!IsOpen || Running || AskingRestore)
            return;
        Browsing = false; // a drop answers as the browser's pick would
        if (AskingForOffer)
        {
            var given = paths.Select(Checked).OfType<string>().ToList();
            if (given.Count > 0)
                Start(given);
            return;
        }
        foreach (var p in paths)
            AddPath(p);
        ShowingReport = false;
    }

    private bool AddPath(string raw)
    {
        if (Checked(raw) is not { } p)
            return false;
        if (!_paths.Contains(p))
            _paths.Add(p);
        return true;
    }

    /// <summary>The full path of an existing file or folder typed or dropped (<c>~</c> = home), or null.</summary>
    private static string? Checked(string raw)
    {
        var p = raw.Trim().Trim('"');
        if (p.StartsWith('~'))
            p = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + p[1..];
        if (p.Length == 0 || !(File.Exists(p) || Directory.Exists(p)))
            return null;
        return Path.GetFullPath(p);
    }

    /// <summary>
    /// s163 (10-importer.md 4.4): the import holds versions built by an older recipe of this importer; re-run them from
    /// the files they were imported from, on a background task, and show the report (a version whose files are gone
    /// is named there, with a request to import it again).
    /// </summary>
    public void OpenRefresh()
    {
        Open(firstRun: false);
        _progress = UiText.S("import.refreshing");
        _task = Task.Run(() => _refresh(new SyncProgress(m => _progress = m)));
    }

    /// <summary>Import <paramref name="paths"/> on a background task.</summary>
    private void Start(IReadOnlyList<string> paths)
    {
        AskingForOffer = false;
        Typed = "";
        _progress = UiText.S("import.scanning");
        if (_run is { } run)
            _task = Task.Run(() => run(paths, new SyncProgress(m => _progress = m)));
        else
            _scanTask = Task.Run(() => _scan(paths));
    }

    /// <summary>Run the import of the scanned paths with the answers given.</summary>
    private void StartImport(ScanResult scan)
    {
        _pendingScan = null;
        var restore = new HashSet<string>(_restore, StringComparer.Ordinal);
        _task = Task.Run(() => _import(scan, restore, new SyncProgress(m => _progress = m)));
    }

    /// <summary>The answer for the version asked about: the next one is asked, or the import runs.</summary>
    private void AnswerRestore(bool restore)
    {
        if (restore)
            _restore.Add(_restoreQueue[0].Profile.Id);
        _restoreQueue.RemoveAt(0);
        Cursor = 0;
        Scroll.Reset();
        if (_restoreQueue.Count == 0 && _pendingScan is { } scan)
            StartImport(scan);
    }

    /// <summary>Poll the background scan and import (call once a frame).</summary>
    public void Update()
    {
        if (_scanTask is { IsCompleted: true } scanned)
        {
            _scanTask = null;
            if (scanned.IsFaulted)
            {
                ShowingReport = true;
                Scroll.Reset();
                _offer = null;
                _report = Wrap(UiText.F("import.failed", scanned.Exception!.GetBaseException().Message), MenuLineStyle.Normal);
                return;
            }
            _restore.Clear();
            _restoreQueue.Clear();
            _restoreQueue.AddRange(ImportRunner.RestorableMaps(scanned.Result));
            _pendingScan = scanned.Result;
            Cursor = 0;
            Scroll.Reset();
            if (_restoreQueue.Count == 0)
                StartImport(scanned.Result);
            return;
        }
        if (_task is not { IsCompleted: true } task)
            return;
        _task = null;
        ShowingReport = true;
        Scroll.Reset();
        _offer = null;
        if (task.IsFaulted)
        {
            _report = Wrap(UiText.F("import.failed", task.Exception!.GetBaseException().Message), MenuLineStyle.Normal);
            return;
        }
        var report = task.Result;
        _offer = report.Offers.FirstOrDefault();
        if (report.Committed)
        {
            Imported = true;
            _paths.Clear();
            OnImported?.Invoke();
        }
        _report = ReportLines(report);
    }

    /// <summary>The report as screen lines (wrapped; nothing is cut).</summary>
    public static List<MenuLine> ReportLines(ImportReport report)
    {
        var lines = new List<MenuLine>();
        foreach (var v in report.Versions)
        {
            lines.AddRange(Wrap(v.Ok ? UiText.F("import.ok", v.Title, v.FilesWritten) : UiText.F("import.error", v.Title, v.Error ?? ""),
                v.Ok ? MenuLineStyle.Header : MenuLineStyle.Normal));
            foreach (var w in v.Warnings)
                lines.AddRange(Wrap("- " + w, MenuLineStyle.Normal));
        }
        foreach (var s in report.Skipped)
            lines.AddRange(Wrap(UiText.F("import.skipped", s), MenuLineStyle.Dim));
        foreach (var n in report.Notes)
            lines.AddRange(Wrap(n, MenuLineStyle.Dim));
        foreach (var o in report.Offers)
            lines.AddRange(Wrap(UiText.F("import.offer", string.Join(", ", o.ForTitles), UiText.S(o.GivesId), o.Title),
                MenuLineStyle.Header));
        if (lines.Count == 0)
            lines.AddRange(Wrap(UiText.S("import.nothing"), MenuLineStyle.Normal));
        return lines;
    }

    /// <summary>Handle this frame's keys; true on the frame the screen closed.</summary>
    public bool HandleKeys(IEnumerable<Keys> newKeys)
    {
        if (!IsOpen)
            return false;
        foreach (var key in newKeys)
        {
            if (Running)
                continue; // the import runs to its end; its files are written to a side folder until then
            if (ShowingReport)
            {
                switch (key)
                {
                    case Keys.Up or Keys.Down or Keys.PageUp or Keys.PageDown:
                        Scroll.Set(_report.Count, VisibleLines);
                        Scroll.HandleKey(key);
                        break;
                    case Keys.A when _offer is not null:
                        ShowingReport = false;
                        AskingForOffer = true;
                        Typed = "";
                        Cursor = 0;
                        Scroll.Reset();
                        break;
                    case Keys.Enter or Keys.Space or Keys.Escape:
                        ShowingReport = false;
                        Cursor = MainMenuScreen.Games.Any(MainMenuScreen.IsPlayable) ? DoneRow : 0;
                        Scroll.Reset();
                        break;
                }
                continue;
            }
            if (AskingRestore)
            {
                switch (key)
                {
                    case Keys.Up or Keys.Down:
                        Cursor = 1 - Cursor;
                        break;
                    case Keys.Enter or Keys.Space:
                        AnswerRestore(Cursor == 0);
                        break;
                    case Keys.Y:
                        AnswerRestore(true);
                        break;
                    case Keys.N:
                        AnswerRestore(false);
                        break;
                    case Keys.Escape: // nothing is imported; the paths stay
                        _restoreQueue.Clear();
                        _pendingScan = null;
                        Cursor = ImportRow;
                        Scroll.Reset();
                        break;
                }
                continue;
            }
            if (Browsing)
            {
                BrowseKey(key);
                continue;
            }
            if (AskingForOffer)
            {
                if (key == Keys.Escape)
                {
                    AskingForOffer = false;
                    Typed = "";
                    Cursor = 0;
                }
                else if (key == Keys.Enter && Checked(Typed) is { } file)
                    Start([file]);
                else if (key == Keys.Tab)
                    OpenBrowser();
                continue;
            }
            switch (key)
            {
                case Keys.Escape:
                    IsOpen = false;
                    return true;
                case Keys.Tab:
                    Cursor = 0;
                    OpenBrowser();
                    break;
                case Keys.Up:
                    Cursor = (Cursor + RowCount - 1) % RowCount;
                    break;
                case Keys.Down:
                    Cursor = (Cursor + 1) % RowCount;
                    break;
                case Keys.Delete when Cursor >= 1 && Cursor <= _paths.Count:
                    _paths.RemoveAt(Cursor - 1);
                    Cursor = Math.Min(Cursor, RowCount - 1);
                    break;
                case Keys.Enter:
                    if (Pick())
                    {
                        IsOpen = false;
                        return true;
                    }
                    break;
            }
        }
        return false;
    }

    /// <summary>
    /// "Pad coverage" (s182): a pad on this screen. The shared map (<see cref="InputMap.MenuPadKey"/>: D-pad / stick =
    /// arrows, A = Return, B = Esc, LB / RB = PgUp / PgDn) plus X for Delete (removes the added path under the cursor).
    /// s183: Y opens the folder browser (Tab) on the rows and on the missing file's row, and takes the report's offer (A);
    /// in the browser A opens (Return), B closes it (Esc), X picks (Space) and Y goes up a folder (Backspace). s185: the
    /// help names the pad's buttons by <see cref="PadWording"/> (the shell notes the last input), as every other screen.
    /// </summary>
    public IEnumerable<Keys> PadKeys(IEnumerable<Buttons> buttons)
    {
        ArgumentNullException.ThrowIfNull(buttons);
        var keys = new List<Keys>();
        foreach (var b in buttons)
        {
            var k = AskingRestore
                ? InputMap.MenuPadKey(b)
                : Browsing
                ? FileBrowser.PadKey(b)
                : ShowingReport
                    ? b == Buttons.Y && _offer is not null ? Keys.A : InputMap.MenuPadKey(b)
                    : b switch
                    {
                        Buttons.X => Keys.Delete,
                        Buttons.Y => Keys.Tab,
                        _ => InputMap.MenuPadKey(b),
                    };
            if (k != Keys.None)
                keys.Add(k);
        }
        return keys;
    }

    /// <summary>Mouse: the row drawn on line <paramref name="line"/> of <see cref="Lines"/>, or null.</summary>
    public int? RowOfLine(int line)
    {
        if (Running || ShowingReport)
            return null;
        var row = line - HeaderCount + Scroll.Offset;
        var rows = Browsing ? _browser.Entries.Count : RowCount;
        return line >= HeaderCount && line < HeaderCount + VisibleLines && row < rows ? row : null;
    }

    /// <summary>Return on the cursor's row; true when it closes the screen.</summary>
    private bool Pick()
    {
        if (Cursor == 0)
        {
            if (AddPath(Typed))
                Typed = "";
            return false;
        }
        if (Cursor >= LibraryRow(0) && Cursor < ImportRow)
        {
            _paths.Add(Libraries[Cursor - LibraryRow(0)].Path);
            return false;
        }
        if (Cursor == ImportRow)
        {
            if (Typed.Length > 0 && AddPath(Typed))
                Typed = "";
            if (_paths.Count == 0)
                return false;
            Start(_paths.ToList());
            return false;
        }
        return Cursor == DoneRow;
    }

    private sealed class SyncProgress(Action<string> report) : IProgress<string>
    {
        public void Report(string value) => report(value);
    }

    private int HeaderCount => Browsing && !Running && !ShowingReport && !AskingRestore ? BrowseHeader().Count
        : AskingRestore ? RestoreHeader().Count
        : 2 + MainMenuScreen.Games.Count + 1;

    /// <summary>The question about a played copy's maps (s205), over its two rows.</summary>
    private List<MenuLine> RestoreHeader()
    {
        var lines = new List<MenuLine> { new(UiText.S("import.restore.title"), MenuLineStyle.Title) };
        // D2-052: the Apple's II keeps only the fog in its regions, DOS I spent fixed fights too.
        var question = AskingRestore && !ImportRunner.MapsHoldFights(_restoreQueue[0])
            ? UiText.F("import.restore.question.fog", RestoreTitle ?? "")
            : UiText.F("import.restore.question", RestoreTitle ?? "");
        lines.AddRange(Wrap(question, MenuLineStyle.Normal));
        lines.Add(new("", MenuLineStyle.Normal));
        return lines;
    }

    /// <summary>
    /// The help line, by keys. s185: the menu screen shows its <c>.pad</c> twin while the pad is the input
    /// (<see cref="PadWording.Help"/>, applied where it is drawn), as every other screen; no flag of its own.
    /// </summary>
    public string Help => Running ? UiText.S("import.help.running")
        : AskingRestore ? UiText.S("import.help.restore")
        : ShowingReport ? (_offer is null ? UiText.S("import.help.report") : UiText.S("import.help.report.offer"))
        : Browsing ? UiText.S("import.help.browse")
        : AskingForOffer ? UiText.S("import.help.offer")
        : Cursor == 0 ? UiText.S("import.help.typing")
        : Cursor <= _paths.Count ? UiText.S("import.help.path")
        : UiText.S("import.help");

    public IReadOnlyList<MenuLine> Lines()
    {
        if (AskingRestore)
            return Scrolled(RestoreHeader(), [Row(0, UiText.S("import.row.restore")), Row(1, UiText.S("import.row.keep_maps"))]);
        if (Browsing && !Running && !ShowingReport)
            return Scrolled(BrowseHeader(), BrowseRows());
        var lines = new List<MenuLine>
        {
            new(FirstRun ? UiText.S("import.title.first") : UiText.S("import.title"), MenuLineStyle.Title),
            new(UiText.S("import.games"), MenuLineStyle.Header),
        };
        foreach (var game in MainMenuScreen.Games)
            lines.Add(GameLine(game));
        lines.Add(new("", MenuLineStyle.Normal));

        if (Running)
        {
            Scroll.Set(0, 1); // nothing scrolls: no bar
            lines.AddRange(Wrap(UiText.S("import.running"), MenuLineStyle.Header));
            lines.AddRange(Wrap(_progress, MenuLineStyle.Normal));
            return lines;
        }
        return Scrolled(lines, ShowingReport ? _report : RowLines());
    }

    /// <summary>
    /// <paramref name="lines"/> (the header), then as much of <paramref name="body"/> as fits under it from the scroll (kept
    /// on the cursor's row, but for the report). s185: the rows fill the screen down to the help line, and the scroll's bar
    /// (<see cref="Scroll"/>) marks a longer list; no line is dropped.
    /// </summary>
    private List<MenuLine> Scrolled(List<MenuLine> lines, List<MenuLine> body)
    {
        var room = Math.Max(1, ScreenLines - (lines.Count - 1));
        Scroll.Set(body.Count, room);
        if (!ShowingReport)
            Scroll.Follow(Cursor);
        for (var i = Scroll.Offset; i < Math.Min(body.Count, Scroll.Offset + room); i++)
            lines.Add(body[i]);
        return lines;
    }

    /// <summary>The screen line the rows (report, browser rows) start on (the title is line 0).</summary>
    public int BodyLine => HeaderCount;

    private static MenuLine GameLine(GameId game)
    {
        var name = UiText.S("menu.game." + game.Prefix());
        var versions = GameDataLocator.ImportedVersions(game);
        string state;
        if (GameDataLocator.IsImported(game) || versions.Count > 0)
            state = string.Join(", ", versions.Select(v => VersionLabel(v)));
        else if (GameDataLocator.IsAvailable(game))
            state = UiText.S("import.state.builtin");
        else
            state = UiText.S("import.state.missing");
        var text = $" {name,-14}{state}";
        return new(text.Length > Columns ? text[..(Columns - 3)] + "..." : text,
            GameDataLocator.IsAvailable(game) ? MenuLineStyle.Normal : MenuLineStyle.Dim);
    }

    /// <summary>A version id as a short label (<c>p1-dos-1987</c> → <c>DOS 1987</c>).</summary>
    public static string VersionLabel(string profileId)
    {
        var parts = profileId.Split('-').Skip(1).ToList();
        return string.Join(" ", parts.Select(p => p switch
        {
            "dos" => "DOS", "amiga" => "AMIGA", "st" => "ATARI ST", "x68000" => "X68000", "apple2" => "APPLE II",
            _ => p.ToUpperInvariant(),
        }));
    }

    private List<MenuLine> RowLines()
    {
        var rows = new List<MenuLine>();
        var label = AskingForOffer ? UiText.S("import.row.offer") : UiText.S("import.row.path");
        var room = Columns - 1 - label.Length - 1;
        var typed = Typed.Length > room ? "..." + Typed[^(room - 3)..] : Typed;
        rows.Add(Row(0, label + typed + (Cursor == 0 ? "_" : "")));
        if (AskingForOffer && _offer is { } offer)
        {
            // Only the path row is a row; the prompt sits under it, so the mouse rows stay where they are.
            rows.AddRange(Wrap(UiText.F("import.offer.prompt", offer.Title, UiText.S(offer.GivesId)), MenuLineStyle.Dim));
            return rows;
        }
        for (var i = 0; i < _paths.Count; i++)
            rows.Add(Row(PathRow(i), "  " + Tail(_paths[i], Columns - 3)));
        var libs = Libraries;
        for (var i = 0; i < libs.Count; i++)
            {
            var lib = UiText.F("import.row.library", libs[i].Store);
            rows.Add(Row(LibraryRow(i), lib + Tail(libs[i].Path, Columns - 1 - lib.Length)));
        }
        rows.Add(_paths.Count > 0 || Typed.Length > 0
            ? Row(ImportRow, UiText.S("import.row.import"))
            : new(Mark(ImportRow) + UiText.S("import.row.import"), Cursor == ImportRow ? MenuLineStyle.Selected : MenuLineStyle.Dim));
        rows.Add(Row(DoneRow, UiText.S("import.row.done")));
        return rows;
    }

    private string Mark(int row) => row == Cursor ? ">" : " ";

    private MenuLine Row(int row, string text) =>
        new(Mark(row) + text, row == Cursor ? MenuLineStyle.Selected : MenuLineStyle.Normal);

    /// <summary>The end of a long path (the part that names the game), marked with "..." where cut for the row.</summary>
    public static string Tail(string path, int room) => path.Length <= room ? path : "..." + path[^(room - 3)..];

    /// <summary>Word-wrap <paramref name="text"/> to <see cref="Columns"/> (long words are split); no line is dropped.</summary>
    public static List<MenuLine> Wrap(string text, MenuLineStyle style)
    {
        var result = new List<MenuLine>();
        var width = Columns - 1;
        foreach (var paragraph in text.Split('\n'))
        {
            var line = "";
            foreach (var word in paragraph.Split(' '))
            {
                var w = word;
                while (w.Length > width)
                {
                    if (line.Length > 0)
                    {
                        result.Add(new(" " + line, style));
                        line = "";
                    }
                    result.Add(new(" " + w[..width], style));
                    w = w[width..];
                }
                if (line.Length == 0)
                    line = w;
                else if (line.Length + 1 + w.Length <= width)
                    line += " " + w;
                else
                {
                    result.Add(new(" " + line, style));
                    line = w;
                }
            }
            result.Add(new(" " + line, style));
        }
        return result;
    }
}
