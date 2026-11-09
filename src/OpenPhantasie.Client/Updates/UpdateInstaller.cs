using System.Diagnostics;
using System.Formats.Tar;
using System.IO.Compression;

namespace OpenPhantasie.Client.Updates;

/// <summary>
/// Puts a downloaded release over the running install (s174). Every file of the new build is moved in place; a file it
/// replaces is first renamed to <c>name.update-old</c> (a running program's files can be renamed on Windows too, not
/// deleted), and those are deleted at the next start (<see cref="CleanUp"/>). If a move fails, every file is put back
/// as it was. Settings, saves and the import live in the user data folder, outside the install, and are not touched.
/// <para>The install root is the folder holding the program, or on macOS the <c>.app</c> bundle around it. The archive
/// may wrap the build in a top folder: the folder whose layout matches the install (it holds the program at the same
/// relative path) is the one copied.</para>
/// </summary>
public static class UpdateInstaller
{
    public const string OldSuffix = ".update-old";

    /// <summary>The work folder inside the install (same volume, so moves are renames).</summary>
    public const string WorkFolder = ".update";

    /// <summary>The running install's root: <see cref="AppContext.BaseDirectory"/>, or the <c>.app</c> bundle around it.</summary>
    public static string InstallRoot(string baseDirectory)
    {
        var dir = new DirectoryInfo(baseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        if (dir.Name == "MacOS" && dir.Parent is { Name: "Contents" } contents
            && contents.Parent is { } app && app.Name.EndsWith(".app", StringComparison.OrdinalIgnoreCase))
            return app.FullName;
        return dir.FullName;
    }

    /// <summary>Whether the install folder can be written (an install under Program Files or /Applications may not be).</summary>
    public static bool CanWrite(string installRoot)
    {
        try
        {
            var probe = Path.Combine(installRoot, WorkFolder, ".probe");
            Directory.CreateDirectory(Path.GetDirectoryName(probe)!);
            File.WriteAllText(probe, "");
            File.Delete(probe);
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>Unpack <paramref name="archive"/> (.zip or .tar.gz) into an empty <paramref name="target"/>.</summary>
    public static void Extract(string archive, string target)
    {
        if (Directory.Exists(target))
            Directory.Delete(target, recursive: true);
        Directory.CreateDirectory(target);
        if (archive.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            ZipFile.ExtractToDirectory(archive, target);
        else
        {
            using var file = File.OpenRead(archive);
            using var gz = new GZipStream(file, CompressionMode.Decompress);
            TarFile.ExtractToDirectory(gz, target, overwriteFiles: true); // keeps the files' Unix modes (the program's x bit)
        }
    }

    /// <summary>
    /// The folder in <paramref name="extracted"/> laid out as the install: the one holding the program at
    /// <paramref name="programRelative"/> (its path from the install root). Null if the archive has no such program.
    /// </summary>
    public static string? FindPayload(string extracted, string programRelative)
    {
        var rel = programRelative.Replace('\\', '/');
        foreach (var file in Directory.EnumerateFiles(extracted, Path.GetFileName(rel), SearchOption.AllDirectories))
        {
            var norm = file.Replace('\\', '/');
            if (!norm.EndsWith("/" + rel, StringComparison.Ordinal))
                continue;
            return file[..^(rel.Length + 1)];
        }
        return null;
    }

    /// <summary>
    /// Move every file of <paramref name="payload"/> into <paramref name="installRoot"/>, renaming the ones replaced to
    /// <c>.update-old</c>. All or nothing: on a failure the files moved so far are taken back out and the old ones restored,
    /// then the error is thrown.
    /// </summary>
    public static int Apply(string payload, string installRoot)
    {
        var moved = new List<(string Dest, string? Old)>();
        try
        {
            foreach (var src in Directory.EnumerateFiles(payload, "*", SearchOption.AllDirectories).ToList())
            {
                var rel = Path.GetRelativePath(payload, src);
                var dest = Path.Combine(installRoot, rel);
                Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                string? old = null;
                if (File.Exists(dest))
                {
                    old = dest + OldSuffix;
                    if (File.Exists(old))
                        File.Delete(old);
                    File.Move(dest, old);
                }
                moved.Add((dest, old));
                File.Move(src, dest);
            }
        }
        catch
        {
            for (var i = moved.Count - 1; i >= 0; i--)
            {
                var (dest, old) = moved[i];
                try
                {
                    if (old is not null && File.Exists(old))
                    {
                        if (File.Exists(dest))
                            File.Delete(dest);
                        File.Move(old, dest);
                    }
                    else if (old is null && File.Exists(dest))
                        File.Delete(dest); // a new file this update added
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                {
                    // keep restoring the others; the next start's CleanUp leaves a .update-old it cannot place alone
                }
            }
            throw;
        }
        return moved.Count;
    }

    /// <summary>
    /// The whole install step: unpack <paramref name="archive"/> into the work folder, find the build in it, move it over
    /// the install. Returns the number of files placed.
    /// </summary>
    public static int Install(string archive, string installRoot, string programRelative)
    {
        var extracted = Path.Combine(installRoot, WorkFolder, "new");
        Extract(archive, extracted);
        var payload = FindPayload(extracted, programRelative)
            ?? throw new InvalidDataException($"the archive has no {programRelative}");
        var count = Apply(payload, installRoot);
        TryDeleteDirectory(Path.Combine(installRoot, WorkFolder));
        return count;
    }

    /// <summary>
    /// At start: delete the <c>.update-old</c> files a finished update left and its work folder. Only those names, only
    /// inside the install.
    /// </summary>
    public static void CleanUp(string installRoot)
    {
        try
        {
            foreach (var f in Directory.EnumerateFiles(installRoot, "*" + OldSuffix, SearchOption.AllDirectories))
                try { File.Delete(f); }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
            TryDeleteDirectory(Path.Combine(installRoot, WorkFolder));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
    }

    private static void TryDeleteDirectory(string dir)
    {
        try
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
    }

    /// <summary>Start the program again (the updated files) as a new process.</summary>
    public static void Relaunch(string programPath)
    {
        var info = new ProcessStartInfo(programPath) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(programPath)! };
        Process.Start(info);
    }
}
