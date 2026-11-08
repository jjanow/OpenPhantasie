using System.IO.Compression;

namespace OpenPhantasie.Importer.Containers;

/// <summary>
/// Turns what the player picked (files, folders, zips, disk images, in any mix) into <see cref="Container"/>s. Folders
/// are searched recursively; zips are opened in memory; disk images are opened read-only and their file systems read
/// (FAT12, Human68k, AmigaDOS, Pasti, Apple II RDOS from a <c>.dsk</c> or a <c>.woz</c>). Nothing is written next to the
/// originals. Images we can't read a file system from (Apple II DOS 3.3, C64, PC-88 …) still become containers with their image sha1, so detection can name them.
/// </summary>
public static class SourceScanner
{
    /// <summary>Largest single file considered (a Steam library folder holds much else).</summary>
    public const long MaxFileBytes = 64L * 1024 * 1024;

    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".adf", ".ima", ".img", ".st", ".stx", ".dim", ".xdf", ".hdm", ".dsk", ".d64", ".d88", ".woz", ".atr", ".zip",
        ".msa", ".fdi", ".do", ".po", ".nib", ".2mg",
    };

    public static List<Container> Scan(IEnumerable<string> paths, ICollection<string> notes, CancellationToken cancel = default)
    {
        var result = new List<Container>();
        foreach (var raw in paths)
        {
            cancel.ThrowIfCancellationRequested();
            var path = Path.GetFullPath(raw);
            if (Directory.Exists(path))
                ScanFolder(path, result, notes, cancel);
            else if (File.Exists(path))
            {
                var info = new FileInfo(path);
                if (info.Length > MaxFileBytes)
                {
                    notes.Add($"{path}: too large to be a game disk, skipped");
                    continue;
                }
                var data = File.ReadAllBytes(path);
                if (!TryOpenImage(info.Name, data, path, info.Name, result, notes, depth: 0))
                {
                    // A single loose file: its own folder-like container.
                    var c = new Container("folder", info.Name, path);
                    c.Files.Add(new ImportFile(c, info.Name, data));
                    result.Add(c);
                }
            }
            else
                notes.Add($"{raw}: not found");
        }
        return result;
    }

    private static void ScanFolder(string root, List<Container> result, ICollection<string> notes, CancellationToken cancel)
    {
        var folder = new Container("folder", Path.GetFileName(Path.TrimEndingDirectorySeparator(root)) is { Length: > 0 } n ? n : root, root);
        IEnumerable<string> files;
        try
        {
            files = Directory.EnumerateFiles(root, "*", new EnumerationOptions
            {
                RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.ReparsePoint,
                MaxRecursionDepth = 12,
            });
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            notes.Add($"{root}: {e.Message}");
            return;
        }
        foreach (var file in files)
        {
            cancel.ThrowIfCancellationRequested();
            FileInfo info;
            try { info = new FileInfo(file); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { continue; }
            if (info.Length > MaxFileBytes)
                continue;
            var rel = Path.GetRelativePath(root, file);
            if (info.Extension.Equals(".zip", StringComparison.OrdinalIgnoreCase))
            {
                // Zips on disk are read entry by entry, never held whole (a Steam library has many).
                if (TryOpenZipFile(file, $"{folder.Description}/{rel.Replace('\\', '/')}", result, notes))
                    continue;
            }
            if (ImageExtensions.Contains(info.Extension) || LooksLikeImageSize(info.Length))
            {
                byte[] data;
                try { data = File.ReadAllBytes(file); }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException) { notes.Add($"{file}: {e.Message}"); continue; }
                if (TryOpenImage(info.Name, data, file, $"{folder.Description}/{rel.Replace('\\', '/')}", result, notes, depth: 0))
                    continue;
                folder.Files.Add(new ImportFile(folder, rel, data));
                continue;
            }
            var captured = file;
            folder.Files.Add(new ImportFile(folder, rel, () => File.ReadAllBytes(captured), info.Length));
        }
        if (folder.Files.Count > 0)
            result.Add(folder);
    }

    private static bool TryOpenZipFile(string path, string description, List<Container> result, ICollection<string> notes)
    {
        try
        {
            string sha1;
            using (var fs = File.OpenRead(path))
                sha1 = Convert.ToHexStringLower(System.Security.Cryptography.SHA1.HashData(fs));
            using var zip = ZipFile.OpenRead(path);
            var folder = new Container("zip", description, path) { ImageSha1 = sha1, ImageSize = new FileInfo(path).Length };
            foreach (var entry in zip.Entries)
            {
                if (entry.FullName.EndsWith('/') || entry.Length > MaxFileBytes)
                    continue;
                var name = entry.FullName;
                if (ImageExtensions.Contains(Path.GetExtension(entry.Name)) || LooksLikeImageSize(entry.Length))
                {
                    var bytes = ReadEntry(path, name);
                    if (TryOpenImage(entry.Name, bytes, path, $"{description}/{name}", result, notes, depth: 1))
                        continue;
                }
                folder.Files.Add(new ImportFile(folder, name, () => ReadEntry(path, name), entry.Length));
            }
            if (folder.Files.Count > 0)
                result.Add(folder);
            return true;
        }
        catch (Exception e) when (e is InvalidDataException or IOException or UnauthorizedAccessException)
        {
            notes.Add($"{description}: unreadable zip ({e.Message})");
            return true;
        }
    }

    private static byte[] ReadEntry(string zipPath, string fullName)
    {
        using var zip = ZipFile.OpenRead(zipPath);
        using var s = zip.GetEntry(fullName)!.Open();
        using var ms = new MemoryStream();
        s.CopyTo(ms);
        return ms.ToArray();
    }

    private static bool LooksLikeImageSize(long size) =>
        size is 368_640 or 737_280 or 819_200 or 901_120 or 1_261_824 or 1_261_568 or 174_848 or 143_360;

    /// <summary>Open <paramref name="data"/> as a zip or a disk image; false when it is neither.</summary>
    internal static bool TryOpenImage(string name, byte[] data, string sourcePath, string description,
        List<Container> result, ICollection<string> notes, int depth)
    {
        if (depth > 3)
            return false;
        var ext = Path.GetExtension(name);
        var sha1 = Hashing.Sha1(data);

        if (data.Length > 4 && data[0] == 'P' && data[1] == 'K' && data[2] == 3 && data[3] == 4)
        {
            try
            {
                using var zip = new ZipArchive(new MemoryStream(data), ZipArchiveMode.Read);
                var folder = new Container("zip", description, sourcePath) { ImageSha1 = sha1, ImageSize = data.Length };
                foreach (var entry in zip.Entries)
                {
                    if (entry.FullName.EndsWith('/') || entry.Length > MaxFileBytes)
                        continue;
                    var entryName = entry.FullName;
                    if (ImageExtensions.Contains(Path.GetExtension(entry.Name)) || LooksLikeImageSize(entry.Length))
                    {
                        var bytes = ReadEntry(data, entryName);
                        if (TryOpenImage(entry.Name, bytes, sourcePath, $"{description}/{entryName}", result, notes, depth + 1))
                            continue;
                    }
                    // Other entries are read only when detection or a pipeline asks (a Steam library has many zips).
                    folder.Files.Add(new ImportFile(folder, entryName, () => ReadEntry(data, entryName), entry.Length));
                }
                if (folder.Files.Count > 0)
                    result.Add(folder);
                return true;
            }
            catch (InvalidDataException e)
            {
                notes.Add($"{description}: unreadable zip ({e.Message})");
                return true;
            }
        }

        if (StxImage.IsStx(data))
        {
            try
            {
                var st = StxImage.ToSt(data);
                var vol = Fat12Volume.TryOpenDos(st);
                var c = new Container("stx", description, sourcePath) { ImageSha1 = sha1, ImageSize = data.Length };
                if (vol is not null)
                    AddFat(c, vol);
                result.Add(c);
                return true;
            }
            catch (InvalidDataException e)
            {
                notes.Add($"{description}: {e.Message}");
                return true;
            }
        }

        if (DimImage.IsDim(data))
        {
            var c = new Container("dim", description, sourcePath) { ImageSha1 = sha1, ImageSize = data.Length };
            var vol = Fat12Volume.TryOpenHuman68k(DimImage.ToRaw(data));
            if (vol is not null)
                AddFat(c, vol);
            result.Add(c);
            return true;
        }

        if (data.Length == 1_261_568 && Fat12Volume.TryOpenHuman68k(data) is { } xdf)
        {
            var c = new Container("xdf", description, sourcePath) { ImageSha1 = sha1, ImageSize = data.Length };
            AddFat(c, xdf);
            result.Add(c);
            return true;
        }

        if (AdfVolume.TryOpen(data) is { } adf)
        {
            var c = new Container("adf", description, sourcePath) { ImageSha1 = sha1, ImageSize = data.Length };
            foreach (var e in adf.Files())
            {
                var entry = e;
                c.Files.Add(new ImportFile(c, entry.Path, () => adf.Read(entry), entry.Size));
            }
            result.Add(c);
            return true;
        }

        // Apple II .woz flux image (the original SSI disks): decoded to the 13-sector image RdosVolume reads. The
        // container keeps the .woz's own sha1, so image fingerprints still name it.
        if (WozImage.IsWoz(data))
        {
            if (WozImage.TryDecode(data) is { } woz && RdosVolume.TryOpen(woz.Image) is { } wozRdos)
            {
                // Track 0 is RDOS's boot / DOS track (on SSI's disks its sector 12 has no data field: the 16-sector
                // boot sector sits there); a sector missing elsewhere may be inside a game file.
                var lost = woz.Missing.Where(m => m.Track > 0).ToList();
                if (lost.Count > 0)
                    notes.Add($"{description}: {lost.Count} sector(s) of the .woz did not decode (track.sector "
                        + string.Join(", ", lost.Take(8).Select(m => $"{m.Track}.{m.Sector}")) + "); they read as zeros");
                result.Add(RdosContainer("woz", description, sourcePath, sha1, data.Length, wozRdos));
            }
            else
                result.Add(new Container("raw-image", description, sourcePath) { ImageSha1 = sha1, ImageSize = data.Length });
            return true;
        }

        if (!ImageExtensions.Contains(ext) && !LooksLikeImageSize(data.Length))
            return false;

        // Apple II RDOS (SSI's 13-sector DOS in a 140K .dsk: Phantasie II's 4am crack).
        if (data.Length == RdosVolume.ImageSize && RdosVolume.TryOpen(data) is { } rdos)
        {
            result.Add(RdosContainer("rdos", description, sourcePath, sha1, data.Length, rdos));
            return true;
        }

        if (Fat12Volume.TryOpenDos(data) is { } fat)
        {
            var c = new Container("fat12", description, sourcePath) { ImageSha1 = sha1, ImageSize = data.Length };
            try
            {
                AddFat(c, fat);
            }
            catch (Exception e) when (e is ArgumentException or IndexOutOfRangeException)
            {
                c.Files.Clear();
            }
            result.Add(c);
            return true;
        }

        if (ImageExtensions.Contains(ext))
        {
            // An image whose file system we don't read (Apple II, C64, Atari 8-bit, PC-88/98 …): fingerprint only.
            result.Add(new Container("raw-image", description, sourcePath) { ImageSha1 = sha1, ImageSize = data.Length });
            return true;
        }
        return false;
    }

    private static Container RdosContainer(string kind, string description, string? sourcePath, string sha1, int size, RdosVolume rdos)
    {
        var c = new Container(kind, description, sourcePath) { ImageSha1 = sha1, ImageSize = size };
        foreach (var e in rdos.Files)
        {
            var entry = e;
            c.Files.Add(new ImportFile(c, entry.Name, () => rdos.Read(entry), RdosVolume.SizeOf(entry)));
        }
        return c;
    }

    private static void AddFat(Container c, Fat12Volume vol)
    {
        foreach (var e in vol.Files())
        {
            var entry = e;
            c.Files.Add(new ImportFile(c, entry.Path, () => vol.Read(entry), entry.Size));
        }
    }

    private static byte[] ReadEntry(byte[] zipBytes, string fullName)
    {
        using var zip = new ZipArchive(new MemoryStream(zipBytes), ZipArchiveMode.Read);
        using var s = zip.GetEntry(fullName)!.Open();
        using var ms = new MemoryStream();
        s.CopyTo(ms);
        return ms.ToArray();
    }
}
