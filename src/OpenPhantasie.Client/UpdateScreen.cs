using Microsoft.Xna.Framework.Input;
using OpenPhantasie.Client.Updates;

namespace OpenPhantasie.Client;

/// <summary>
/// The update screen (s174, remaster only: the originals have nothing like it). Opened from Options → GENERAL → UPDATE NOW
/// (it checks at once) or by the start-up check when it found a newer release (CHECK FOR UPDATES AT START). It shows the
/// running version, the release found and its notes, and on UPDATE NOW downloads the archive for this machine, puts it
/// over the install (<see cref="UpdateInstaller"/>) and restarts the game. A shell state like the import screen.
/// </summary>
public sealed class UpdateScreen
{
    public enum State { Checking, UpToDate, Available, NoBuild, DevBuild, Installing, Installed, Failed }

    /// <summary>Lines of release notes shown at once; more scroll (PgUp / PgDn).</summary>
    public const int NoteLines = 8;

    private readonly Func<CancellationToken, Task<UpdateCheckResult>> _check;
    private readonly Func<UpdateRelease, IProgress<double>, Task> _install;
    private readonly bool _isRelease;
    private Task<UpdateCheckResult>? _checking;
    private Task? _installing;
    private volatile int _percent = -1;
    private List<MenuLine> _notes = [];
    private int _scroll;
    private string _error = "";

    /// <param name="check">Ask for the latest release.</param>
    /// <param name="install">Download and place a release (throws on failure).</param>
    /// <param name="isRelease">The running build is a release build (a local build never updates itself).</param>
    public UpdateScreen(Func<CancellationToken, Task<UpdateCheckResult>> check,
        Func<UpdateRelease, IProgress<double>, Task> install, bool isRelease)
    {
        _check = check;
        _install = install;
        _isRelease = isRelease;
    }

    public bool IsOpen { get; private set; }
    public State Current { get; private set; }
    public UpdateRelease? Release { get; private set; }
    /// <summary>The newest release seen (also when it has no build for this machine).</summary>
    public Version? Latest { get; private set; }
    /// <summary>On the Available screen: 0 = UPDATE NOW, 1 = LATER.</summary>
    public int Cursor { get; private set; }
    /// <summary>Set once the new build is in place and a key was pressed: the shell starts the program again and exits.</summary>
    public bool RestartRequested { get; private set; }

    /// <summary>UPDATE NOW from Options: check now.</summary>
    public void Open()
    {
        Reset();
        if (!_isRelease)
        {
            Current = State.DevBuild;
            return;
        }
        Current = State.Checking;
        _checking = _check(CancellationToken.None);
    }

    /// <summary>The start-up check found <paramref name="result"/>: show it without asking again.</summary>
    public void OpenWith(UpdateCheckResult result)
    {
        Reset();
        Show(result);
    }

    private void Reset()
    {
        IsOpen = true;
        RestartRequested = false;
        Release = null;
        Latest = null;
        Cursor = 0;
        _scroll = 0;
        _notes = [];
        _error = "";
        _percent = -1;
        _checking = null;
        _installing = null;
    }

    private void Show(UpdateCheckResult result)
    {
        Latest = result.Latest;
        Release = result.Release;
        Current = result.Outcome switch
        {
            UpdateCheckOutcome.Available => State.Available,
            UpdateCheckOutcome.NoBuildForPlatform => State.NoBuild,
            _ => State.UpToDate,
        };
        if (Release is not null)
            _notes = Notes(Release.Notes);
    }

    /// <summary>Release notes (GitHub Markdown) as plain wrapped lines: heading marks and emphasis dropped, nothing cut.</summary>
    public static List<MenuLine> Notes(string markdown)
    {
        var lines = new List<MenuLine>();
        foreach (var raw in markdown.Replace("\r", "").Split('\n'))
        {
            var line = raw.TrimEnd();
            var header = line.StartsWith('#');
            line = line.TrimStart('#').Trim().Replace("**", "").Replace("`", "");
            if (line.StartsWith("* ") || line.StartsWith("- "))
                line = "- " + line[2..];
            if (line.Length == 0)
            {
                if (lines.Count > 0 && lines[^1].Text.Trim().Length > 0)
                    lines.Add(new("", MenuLineStyle.Normal));
                continue;
            }
            lines.AddRange(ImportScreen.Wrap(line, header ? MenuLineStyle.Header : MenuLineStyle.Normal));
        }
        while (lines.Count > 0 && lines[^1].Text.Trim().Length == 0)
            lines.RemoveAt(lines.Count - 1);
        return lines;
    }

    /// <summary>Poll the background check / install (call once a frame).</summary>
    public void Update()
    {
        if (_checking is { IsCompleted: true } check)
        {
            _checking = null;
            if (check.IsCompletedSuccessfully)
                Show(check.Result);
            else
                Fail(check.Exception?.GetBaseException().Message ?? "?");
        }
        if (_installing is { IsCompleted: true } install)
        {
            _installing = null;
            if (install.IsCompletedSuccessfully)
                Current = State.Installed;
            else
                Fail(install.Exception?.GetBaseException().Message ?? "?");
        }
    }

    private void Fail(string message)
    {
        _error = message;
        Current = State.Failed;
    }

    private void StartInstall()
    {
        if (Release is null)
            return;
        Current = State.Installing;
        _percent = 0;
        var progress = new SyncProgress(f => _percent = (int)(f * 100));
        var release = Release;
        _installing = Task.Run(() => _install(release, progress));
    }

    /// <summary>Handle this frame's keys; true on the frame the screen closed.</summary>
    public bool HandleKeys(IEnumerable<Keys> newKeys)
    {
        if (!IsOpen)
            return false;
        foreach (var key in newKeys)
        {
            switch (Current)
            {
                case State.Checking or State.Installing:
                    continue; // runs to its end (the install puts every file back on a failure)
                case State.Installed:
                    if (key is Keys.Enter or Keys.Space or Keys.Escape)
                    {
                        RestartRequested = true;
                        IsOpen = false;
                        return true;
                    }
                    continue;
                case State.Available:
                    switch (key)
                    {
                        case Keys.Up or Keys.Down or Keys.Left or Keys.Right:
                            Cursor = 1 - Cursor;
                            break;
                        case Keys.PageUp:
                            _scroll = Math.Max(0, _scroll - NoteLines);
                            break;
                        case Keys.PageDown:
                            _scroll = Math.Clamp(_scroll + NoteLines, 0, Math.Max(0, _notes.Count - NoteLines));
                            break;
                        case Keys.Enter or Keys.Space:
                            if (Cursor == 0)
                                StartInstall();
                            else
                                return Close();
                            break;
                        case Keys.Escape:
                            return Close();
                    }
                    continue;
                default:
                    if (key is Keys.Enter or Keys.Space or Keys.Escape)
                        return Close();
                    continue;
            }
        }
        return false;
    }

    /// <summary>A click on line <paramref name="line"/> of <see cref="Lines"/>: on a choice row, that choice; elsewhere as Return.</summary>
    public bool Click(int line)
    {
        if (Current == State.Available && RowOfLine(line) is int row)
            Cursor = row;
        return HandleKeys([Keys.Enter]);
    }

    /// <summary>The choice row (0 UPDATE NOW, 1 LATER) drawn on line <paramref name="line"/>, or null.</summary>
    public int? RowOfLine(int line)
    {
        if (Current != State.Available)
            return null;
        var first = Lines().Count - 2;
        return line == first ? 0 : line == first + 1 ? 1 : null;
    }

    private bool Close()
    {
        IsOpen = false;
        return true;
    }

    public string Help => Current switch
    {
        State.Checking or State.Installing => UiText.S("import.help.running"),
        State.Available => UiText.S("options.update.help.available"),
        State.Installed => UiText.S("options.update.help.restart"),
        _ => UiText.S("options.update.help.back"),
    };

    public IReadOnlyList<MenuLine> Lines()
    {
        var lines = new List<MenuLine>
        {
            new(UiText.S("options.update.title"), MenuLineStyle.Title),
            new(" " + UiText.F("options.update.this_version", AppVersion.Text), MenuLineStyle.Normal),
            new("", MenuLineStyle.Normal),
        };
        switch (Current)
        {
            case State.Checking:
                lines.AddRange(ImportScreen.Wrap(UiText.S("options.update.checking"), MenuLineStyle.Normal));
                break;
            case State.DevBuild:
                lines.AddRange(ImportScreen.Wrap(UiText.F("options.update.dev_build", AppVersion.ReleasesPage), MenuLineStyle.Normal));
                break;
            case State.UpToDate:
                lines.AddRange(ImportScreen.Wrap(UiText.S("options.update.up_to_date"), MenuLineStyle.Normal));
                break;
            case State.NoBuild:
                lines.AddRange(ImportScreen.Wrap(
                    UiText.F("options.update.no_build", Latest?.ToString(3) ?? "?", AppVersion.ReleasesPage), MenuLineStyle.Normal));
                break;
            case State.Available:
                lines.AddRange(ImportScreen.Wrap(UiText.F("options.update.available", Release!.Version.ToString(3)), MenuLineStyle.Header));
                for (var i = _scroll; i < Math.Min(_notes.Count, _scroll + NoteLines); i++)
                    lines.Add(_notes[i]);
                if (_notes.Count > _scroll + NoteLines)
                    lines.Add(new(" ...", MenuLineStyle.Dim));
                lines.Add(new("", MenuLineStyle.Normal));
                lines.Add(Choice(0, UiText.S("options.update.now")));
                lines.Add(Choice(1, UiText.S("options.update.later")));
                break;
            case State.Installing:
                lines.AddRange(ImportScreen.Wrap(_percent < 100
                    ? UiText.F("options.update.downloading", Release?.Version.ToString(3) ?? "?", Math.Max(0, _percent))
                    : UiText.S("options.update.installing"), MenuLineStyle.Normal));
                break;
            case State.Installed:
                lines.AddRange(ImportScreen.Wrap(PadWording.F("options.update.installed", Release?.Version.ToString(3) ?? "?"), MenuLineStyle.Header));
                break;
            case State.Failed:
                lines.AddRange(ImportScreen.Wrap(UiText.F("options.update.failed", _error, AppVersion.ReleasesPage), MenuLineStyle.Normal));
                break;
        }
        return lines;
    }

    private MenuLine Choice(int row, string text) =>
        new((Cursor == row ? ">" : " ") + text, Cursor == row ? MenuLineStyle.Selected : MenuLineStyle.Normal);

    private sealed class SyncProgress(Action<double> report) : IProgress<double>
    {
        public void Report(double value) => report(value);
    }
}
