using Microsoft.Xna.Framework.Input;

namespace OpenPhantasie.Client;

/// <summary>
/// A browser of the folders and files on this machine, for screens a pad can't type a path into (s183's Import browser,
/// shared with the DOS save / transfer dialog since s190, pad fix list C10). Up / Down move, PgUp / PgDn page, Return /
/// Right opens a folder (Return also picks a file or "use this folder"), Backspace / Left goes up, Space picks the row (a
/// folder or a file), Esc closes. The owner draws the rows and turns the pad's buttons into these keys.
/// </summary>
public sealed class FileBrowser
{
    /// <summary>One row: up one folder, use this folder, or a folder / file in it.</summary>
    public sealed record Entry(EntryKind Kind, string Name, string Path);

    public enum EntryKind { Up, UseFolder, Folder, File }

    /// <summary>What a key did: nothing for the owner, the browser closed (Esc), or a row was picked (<see cref="Picked"/>).</summary>
    public enum Outcome { None, Closed, Picked }

    private readonly string _home;
    private string? _last;
    private List<Entry> _entries = [];

    public FileBrowser(string? home = null) =>
        _home = home ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    /// <summary>The folder shown (null: the drive list, Windows, above a drive's root).</summary>
    public string? Folder { get; private set; }

    public IReadOnlyList<Entry> Entries => _entries;

    /// <summary>The row under the cursor.</summary>
    public int Cursor { get; private set; }

    /// <summary>The folder shown could not be read (only its Up / use rows are listed).</summary>
    public bool Unreadable { get; private set; }

    /// <summary>The path the last <see cref="Outcome.Picked"/> chose.</summary>
    public string Picked { get; private set; } = "";

    /// <summary>
    /// Open at <paramref name="near"/>: the folder itself, else a file's (or a missing path's) folder when that exists,
    /// else where the browser was last, else at home. The cursor lands on <paramref name="near"/> when it is listed.
    /// </summary>
    public void Open(string? near)
    {
        var path = string.IsNullOrWhiteSpace(near) ? null : near.Trim();
        var dir = path is not null && Directory.Exists(path) ? path
            : path is not null && System.IO.Path.GetDirectoryName(path) is { } parent && Directory.Exists(parent) ? parent
            : _last is not null && Directory.Exists(_last) ? _last
            : _home;
        BrowseTo(dir, path);
    }

    /// <summary>Show <paramref name="dir"/> (null: the drive list) with the cursor on <paramref name="select"/>, if listed.</summary>
    public void BrowseTo(string? dir, string? select = null)
    {
        Folder = dir;
        Unreadable = false;
        var entries = new List<Entry>();
        if (dir is null)
        {
            foreach (var d in DriveInfo.GetDrives().Where(d => d.IsReady))
                entries.Add(new Entry(EntryKind.Folder, d.Name, d.RootDirectory.FullName));
        }
        else
        {
            _last = dir;
            if (System.IO.Path.GetDirectoryName(dir) is not null || OperatingSystem.IsWindows())
                entries.Add(new Entry(EntryKind.Up, UiText.S("import.browse.up"), ""));
            entries.Add(new Entry(EntryKind.UseFolder, UiText.S("import.browse.use"), dir));
            try
            {
                var info = new DirectoryInfo(dir);
                // Visible names first, then hidden ones (a Steam library sits under ~/.local), each A-Z.
                static IEnumerable<T> Sorted<T>(IEnumerable<T> items) where T : FileSystemInfo =>
                    items.OrderBy(i => i.Name.StartsWith('.')).ThenBy(i => i.Name, StringComparer.OrdinalIgnoreCase);
                entries.AddRange(Sorted(info.EnumerateDirectories()).Select(d =>
                    new Entry(EntryKind.Folder, d.Name + System.IO.Path.DirectorySeparatorChar, d.FullName)));
                entries.AddRange(Sorted(info.EnumerateFiles()).Select(f => new Entry(EntryKind.File, f.Name, f.FullName)));
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Security.SecurityException)
            {
                Unreadable = true;
            }
        }
        _entries = entries;
        var at = select is null ? -1 : entries.FindIndex(e => e.Path == select);
        Cursor = at >= 0 ? at : entries.FindIndex(e => e.Kind is EntryKind.Folder or EntryKind.File) is var first and >= 0
            ? first : 0;
    }

    /// <summary>Up one folder (the drive list above a Windows drive's root); the cursor lands on the folder left.</summary>
    public void Up()
    {
        if (Folder is null)
            return;
        var parent = System.IO.Path.GetDirectoryName(System.IO.Path.TrimEndingDirectorySeparator(Folder));
        if (parent is null && !OperatingSystem.IsWindows())
            return; // the root
        BrowseTo(parent, Folder);
    }

    /// <summary>One key; <paramref name="page"/> is how many rows PgUp / PgDn move.</summary>
    public Outcome Key(Keys key, int page)
    {
        var count = _entries.Count;
        var entry = Cursor < count ? _entries[Cursor] : null;
        switch (key)
        {
            case Keys.Up when count > 0:
                Cursor = (Cursor + count - 1) % count;
                break;
            case Keys.Down when count > 0:
                Cursor = (Cursor + 1) % count;
                break;
            case Keys.PageUp:
                Cursor = Math.Max(0, Cursor - page);
                break;
            case Keys.PageDown:
                Cursor = Math.Max(0, Math.Min(count - 1, Cursor + page));
                break;
            case Keys.Back or Keys.Left:
                Up();
                break;
            case Keys.Escape:
                return Outcome.Closed;
            case Keys.Enter or Keys.Right when entry is not null:
                switch (entry.Kind)
                {
                    case EntryKind.Up:
                        Up();
                        break;
                    case EntryKind.Folder:
                        BrowseTo(entry.Path);
                        break;
                    case EntryKind.UseFolder or EntryKind.File when key == Keys.Enter:
                        return Pick(entry.Path);
                }
                break;
            case Keys.Space when entry is { Kind: not EntryKind.Up }:
                return Pick(entry.Path);
        }
        return Outcome.None;
    }

    private Outcome Pick(string path)
    {
        Picked = path;
        return Outcome.Picked;
    }

    /// <summary>The browser's pad: A opens (Return), B closes (Esc), X picks (Space), Y goes up (Backspace), else the menu map.</summary>
    public static Keys PadKey(Microsoft.Xna.Framework.Input.Buttons button) => button switch
    {
        Microsoft.Xna.Framework.Input.Buttons.X => Keys.Space,
        Microsoft.Xna.Framework.Input.Buttons.Y => Keys.Back,
        _ => InputMap.MenuPadKey(button),
    };
}
