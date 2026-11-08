using Microsoft.Xna.Framework.Input;
using OpenPhantasie.Core;
using OpenPhantasie.Data;
using OpenPhantasie.Importer;

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
/// </summary>
public sealed class ImportScreen
{
    /// <summary>Characters per line on the 320-wide screen (x 14 to 306).</summary>
    public const int Columns = (320 - 28) / PixelFont.Advance;

    /// <summary>Lines of rows (or report) shown at once; more scroll.</summary>
    public const int VisibleLines = 8;

    private readonly Func<IReadOnlyList<string>, IProgress<string>, ImportReport> _run;
    private readonly Func<IProgress<string>, ImportReport> _refresh;
    private readonly Func<IReadOnlyList<StoreLibraries.Library>> _libraries;
    private readonly List<string> _paths = [];
    private IReadOnlyList<StoreLibraries.Library> _found = [];
    private Task<ImportReport>? _task;
    private volatile string _progress = "";
    private List<MenuLine> _report = [];
    private int _scroll;
    private ImportOffer? _offer;

    /// <param name="run">Scan and import the paths (default: <see cref="ImportRunner"/> into <paramref name="importRoot"/>).</param>
    /// <param name="refresh">Re-run the versions built by an older recipe (default: <see cref="ImportRunner.Refresh"/>).</param>
    public ImportScreen(string importRoot,
        Func<IReadOnlyList<string>, IProgress<string>, ImportReport>? run = null,
        Func<IReadOnlyList<StoreLibraries.Library>>? libraries = null,
        Func<IProgress<string>, ImportReport>? refresh = null)
    {
        ImportRoot = importRoot;
        _run = run ?? ((paths, progress) =>
            new ImportRunner(importRoot, GameDataLocator.ShippedRoot).Run(ImportRunner.Scan(paths), progress));
        _refresh = refresh ?? (progress => new ImportRunner(importRoot, GameDataLocator.ShippedRoot).Refresh(progress));
        _libraries = libraries ?? StoreLibraries.Find;
    }

    public string ImportRoot { get; }
    public bool IsOpen { get; private set; }
    /// <summary>Opened at start because no game is available (the welcome text shows).</summary>
    public bool FirstRun { get; private set; }
    public bool Running => _task is { IsCompleted: false };
    public bool ShowingReport { get; private set; }
    /// <summary>Selected row (see <see cref="RowCount"/>).</summary>
    public int Cursor { get; private set; }
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

    public void Open(bool firstRun)
    {
        FirstRun = firstRun;
        IsOpen = true;
        ShowingReport = false;
        AskingForOffer = false;
        _offer = null;
        Imported = false;
        Cursor = 0;
        _scroll = 0;
        try { _found = _libraries(); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { _found = []; }
    }

    // Rows: 0 = typed path, then the added paths, then libraries not yet added, then IMPORT, then DONE.
    private IReadOnlyList<StoreLibraries.Library> Libraries => _found.Where(l => !_paths.Contains(l.Path)).ToList();
    private int PathRow(int i) => 1 + i;
    private int LibraryRow(int i) => 1 + _paths.Count + i;
    private int ImportRow => 1 + _paths.Count + Libraries.Count;
    private int DoneRow => ImportRow + 1;
    public int RowCount => AskingForOffer ? 1 : DoneRow + 1;

    /// <summary>Text typed (Window.TextInput): goes into the path row; backspace deletes.</summary>
    public void OnTextInput(char c)
    {
        if (!IsOpen || Running || ShowingReport || Cursor != 0)
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
        if (!IsOpen || Running)
            return;
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
        _task = Task.Run(() => _run(paths, new SyncProgress(m => _progress = m)));
    }

    /// <summary>Poll the background import (call once a frame).</summary>
    public void Update()
    {
        if (_task is not { IsCompleted: true } task)
            return;
        _task = null;
        ShowingReport = true;
        _scroll = 0;
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
                    case Keys.Up: _scroll = Math.Max(0, _scroll - 1); break;
                    case Keys.Down: _scroll = Math.Clamp(_scroll + 1, 0, Math.Max(0, _report.Count - VisibleLines)); break;
                    case Keys.PageUp: _scroll = Math.Max(0, _scroll - VisibleLines); break;
                    case Keys.PageDown: _scroll = Math.Clamp(_scroll + VisibleLines, 0, Math.Max(0, _report.Count - VisibleLines)); break;
                    case Keys.A when _offer is not null:
                        ShowingReport = false;
                        AskingForOffer = true;
                        Typed = "";
                        Cursor = 0;
                        _scroll = 0;
                        break;
                    case Keys.Enter or Keys.Space or Keys.Escape:
                        ShowingReport = false;
                        Cursor = MainMenuScreen.Games.Any(MainMenuScreen.IsPlayable) ? DoneRow : 0;
                        _scroll = 0;
                        break;
                }
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
                continue;
            }
            switch (key)
            {
                case Keys.Escape:
                    IsOpen = false;
                    return true;
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

    /// <summary>Mouse: the row drawn on line <paramref name="line"/> of <see cref="Lines"/>, or null.</summary>
    public int? RowOfLine(int line)
    {
        if (Running || ShowingReport)
            return null;
        var row = line - HeaderCount + _scroll;
        return line >= HeaderCount && line < HeaderCount + VisibleLines && row < RowCount ? row : null;
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

    private int HeaderCount => 2 + MainMenuScreen.Games.Count + 1;

    public string Help => Running ? UiText.S("import.help.running")
        : ShowingReport ? (_offer is null ? UiText.S("import.help.report") : UiText.S("import.help.report.offer"))
        : AskingForOffer ? UiText.S("import.help.offer")
        : Cursor == 0 ? UiText.S("import.help.typing")
        : Cursor <= _paths.Count ? UiText.S("import.help.path")
        : UiText.S("import.help");

    public IReadOnlyList<MenuLine> Lines()
    {
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
            lines.AddRange(Wrap(UiText.S("import.running"), MenuLineStyle.Header));
            lines.AddRange(Wrap(_progress, MenuLineStyle.Normal));
            return lines;
        }
        var body = ShowingReport ? _report : RowLines();
        if (!ShowingReport)
        {
            if (Cursor < _scroll) _scroll = Cursor;
            if (Cursor >= _scroll + VisibleLines) _scroll = Cursor - VisibleLines + 1;
        }
        for (var i = _scroll; i < Math.Min(body.Count, _scroll + VisibleLines); i++)
            lines.Add(body[i]);
        if (body.Count > _scroll + VisibleLines)
            lines.Add(new(" ...", MenuLineStyle.Dim));
        return lines;
    }

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
