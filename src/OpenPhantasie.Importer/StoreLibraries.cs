using System.Text.RegularExpressions;

namespace OpenPhantasie.Importer;

/// <summary>
/// The usual Steam and GOG library folders on this machine (10-importer.md 4.2), offered as shortcuts. Only where to
/// look: what is in them is found by content (10-importer.md 4.2), never by folder names.
/// </summary>
public static partial class StoreLibraries
{
    public sealed record Library(string Store, string Path);

    public static IReadOnlyList<Library> Find()
    {
        var found = new List<Library>();
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var steamRoots = new List<string>();
        if (OperatingSystem.IsWindows())
        {
            steamRoots.Add(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam"));
            steamRoots.Add(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Steam"));
        }
        else if (OperatingSystem.IsMacOS())
            steamRoots.Add(System.IO.Path.Combine(home, "Library", "Application Support", "Steam"));
        else
        {
            steamRoots.Add(System.IO.Path.Combine(home, ".steam", "steam"));
            steamRoots.Add(System.IO.Path.Combine(home, ".local", "share", "Steam"));
            steamRoots.Add(System.IO.Path.Combine(home, ".var", "app", "com.valvesoftware.Steam", ".local", "share", "Steam"));
        }

        var libraries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var root in steamRoots.Where(Directory.Exists))
        {
            libraries.Add(root);
            var vdf = System.IO.Path.Combine(root, "steamapps", "libraryfolders.vdf");
            if (!File.Exists(vdf))
                continue;
            try
            {
                foreach (Match m in VdfPath().Matches(File.ReadAllText(vdf)))
                    libraries.Add(m.Groups[1].Value.Replace(@"\\", @"\"));
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
        foreach (var lib in libraries)
        {
            var common = System.IO.Path.Combine(lib, "steamapps", "common");
            if (Directory.Exists(common) && found.All(f => !SamePath(f.Path, common)))
                found.Add(new("Steam", common));
        }

        var gog = new List<string>();
        if (OperatingSystem.IsWindows())
        {
            gog.Add(@"C:\GOG Games");
            gog.Add(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "GOG Galaxy", "Games"));
            gog.Add(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "GOG Galaxy", "Games"));
        }
        else
        {
            gog.Add(System.IO.Path.Combine(home, "GOG Games"));
            gog.Add(System.IO.Path.Combine(home, "Games", "gog"));
        }
        foreach (var g in gog.Where(Directory.Exists))
            if (found.All(f => !SamePath(f.Path, g)))
                found.Add(new("GOG", g));
        return found;
    }

    private static bool SamePath(string a, string b) =>
        string.Equals(RealPath(a), RealPath(b),
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    /// <summary>The path with every symbolic link on the way resolved (~/.steam/steam is a link to the Steam folder).</summary>
    private static string RealPath(string path)
    {
        var full = System.IO.Path.GetFullPath(path).TrimEnd('/', '\\');
        try
        {
            var root = System.IO.Path.GetPathRoot(full) ?? "";
            var current = root;
            foreach (var part in full[root.Length..].Split('/', '\\', StringSplitOptions.RemoveEmptyEntries))
            {
                current = System.IO.Path.Combine(current, part);
                var info = new DirectoryInfo(current);
                if (info.LinkTarget is not null && info.ResolveLinkTarget(returnFinalTarget: true) is { } target)
                    current = target.FullName;
            }
            return current.TrimEnd('/', '\\');
        }
        catch (IOException) { return full; }
        catch (UnauthorizedAccessException) { return full; }
    }

    [GeneratedRegex("\"path\"\\s+\"([^\"]+)\"")]
    private static partial Regex VdfPath();
}
