using OpenPhantasie.Importer.Containers;

namespace OpenPhantasie.Importer.Profiles;

public enum DetectionStatus
{
    /// <summary>Every disk's key files found with the right hashes: can be imported.</summary>
    Ready,
    /// <summary>Some disks found, others not (e.g. Phantasie II disk 2).</summary>
    MissingDisk,
    /// <summary>The file names match a supported version but the hashes don't: another build, not imported.</summary>
    UnknownBuild,
    /// <summary>A version we know by its image hash or file names, with no importer (yet).</summary>
    Recognised,
}

/// <summary>What detection found for one version.</summary>
public sealed class Detection
{
    public required VersionProfile Profile { get; init; }
    public required DetectionStatus Status { get; init; }
    public FileSet? Files { get; init; }
    /// <summary>One line for the player.</summary>
    public required string Message { get; init; }
    /// <summary>Mutable files whose bytes differ from the pristine ones (a played copy).</summary>
    public IReadOnlyList<string> PlayedFiles { get; init; } = [];
    public IReadOnlyList<Container> Containers { get; init; } = [];
}

/// <summary>
/// Content-based detection (10-importer.md 3.2): key-file sha1s first (any container: folder, zip, image), then image
/// fingerprints and file-name markers for versions we only recognise. Never by folder names or store layouts.
/// </summary>
public static class Detector
{
    public static List<Detection> Detect(IReadOnlyList<Container> containers, IReadOnlyList<VersionProfile>? profiles = null)
    {
        profiles ??= VersionProfiles.All;
        var result = new List<Detection>();
        var claimed = new HashSet<Container>();
        var unknownBuildIn = new Dictionary<Detection, List<Container>>();

        foreach (var profile in profiles.Where(p => p.Disks.Count > 0))
        {
            var matched = new List<(VersionProfile.Disk Disk, Container Container, string Anchor)>();
            var missing = new List<string>();
            var wrongHash = new List<string>();
            var wrongHashIn = new List<Container>();
            foreach (var disk in profile.Disks)
            {
                var hit = containers.Select(c => (c, Match(c, disk.KeyFiles))).FirstOrDefault(x => x.Item2.Ok);
                if (hit.c is not null)
                {
                    matched.Add((disk, hit.c, hit.Item2.Anchor));
                    continue;
                }
                // "Another build" only where the version's own marker files are there too (MESS1 / DNG1 are on
                // every I and II disk; they alone say nothing).
                var named = containers
                    .Where(c => profile.NameMarkers.Count == 0 || matched.Any(m => m.Container == c)
                                || profile.NameMarkers.All(n => c.Files.Any(f => FileSet.TailMatches(f.Path, n))))
                    .Select(c => (c, Match(c, disk.KeyFiles))).FirstOrDefault(x => x.Item2.NamesFound);
                if (named.c is not null)
                {
                    wrongHash.AddRange(named.Item2.Mismatched.Select(m => $"{named.c.Description}: {m}"));
                    wrongHashIn.Add(named.c);
                }
                missing.Add(disk.Label);
            }
            if (matched.Count == 0 && wrongHash.Count == 0)
                continue;

            var fileSet = new FileSet(matched.Select(m => (m.Container, m.Anchor)).ToList());
            foreach (var m in matched)
                claimed.Add(m.Container);
            if (matched.Count == profile.Disks.Count)
            {
                var played = profile.MutableFiles
                    .Select(f => (f, file: fileSet.Find(f.Name)))
                    .Where(x => x.file is not null && x.file.Sha1 != x.f.Sha1)
                    .Select(x => x.f.Name)
                    .ToList();
                result.Add(new Detection
                {
                    Profile = profile,
                    Status = profile.IsSupported ? DetectionStatus.Ready : DetectionStatus.Recognised,
                    Files = fileSet,
                    PlayedFiles = played,
                    Containers = matched.Select(m => m.Container).Distinct().ToList(),
                    Message = profile.IsSupported
                        ? $"{profile.Title}: found" + (played.Count > 0 ? " (a played copy)" : "")
                        : $"{profile.Title}: recognised; {profile.Reason ?? "not supported yet"}",
                });
            }
            else if (matched.Count > 0)
            {
                result.Add(new Detection
                {
                    Profile = profile,
                    Status = DetectionStatus.MissingDisk,
                    Files = fileSet,
                    Containers = matched.Select(m => m.Container).Distinct().ToList(),
                    Message = $"{profile.Title}: {string.Join(", ", missing)} not found",
                });
            }
            else
            {
                var unknown = new Detection
                {
                    Profile = profile,
                    Status = DetectionStatus.UnknownBuild,
                    Message = $"{profile.Title}: the files are there but are another build ({string.Join("; ", wrongHash.Take(4))}); not imported",
                };
                result.Add(unknown);
                unknownBuildIn[unknown] = wrongHashIn;
            }
        }

        // "Another build" of one version is no news when another profile of the same game matched those disks by
        // their own key files (Apple II II v2.1 next to the supported v1.0).
        result.RemoveAll(d => unknownBuildIn.TryGetValue(d, out var cs) && cs.Count > 0 && cs.All(c => result.Any(o =>
            o.Status != DetectionStatus.UnknownBuild && o.Profile.Game == d.Profile.Game && o.Containers.Contains(c))));

        // Image fingerprints and name markers for the rest (recognised-only versions, planned importers, and
        // reference dumps that a key-file match above already covered).
        foreach (var c in containers.Where(c => !claimed.Contains(c)))
        {
            var byImage = c.ImageSha1 is null ? null
                : profiles.FirstOrDefault(p => p.Images.Any(i => i.Sha1 == c.ImageSha1));
            var byName = byImage ?? profiles.FirstOrDefault(p => p.Disks.Count == 0 && p.NameMarkers.Count > 0
                && p.NameMarkers.All(n => c.Files.Any(f => f.Name.Equals(n, StringComparison.OrdinalIgnoreCase))));
            var profile = byName;
            if (profile is null)
                continue;
            var existing = result.FirstOrDefault(d => d.Profile == profile);
            if (existing is not null)
                continue;
            result.Add(new Detection
            {
                Profile = profile,
                Status = profile.IsSupported ? DetectionStatus.UnknownBuild : DetectionStatus.Recognised,
                Containers = [c],
                Message = profile.IsSupported
                    ? $"{profile.Title}: the image is known but its files could not be read"
                    : $"{profile.Title}: recognised; {profile.Reason ?? "not supported yet"}",
            });
        }
        return result;
    }

    private readonly record struct DiskMatch(bool Ok, bool NamesFound, string Anchor, IReadOnlyList<string> Mismatched);

    private static DiskMatch Match(Container c, IReadOnlyList<VersionProfile.KeyFile> keys)
    {
        string? anchor = null;
        var mismatched = new List<string>();
        var namesFound = true;
        foreach (var key in keys)
        {
            var candidates = key.Name == "*"
                ? c.Files.Where(f => f.Length == key.Size).ToList()
                : c.Files.Where(f => FileSet.TailMatches(f.Path, key.Name)).ToList();
            if (candidates.Count == 0)
            {
                namesFound = false;
                continue;
            }
            var good = candidates.FirstOrDefault(f => f.Length == key.Size && f.Sha1 == key.Sha1);
            if (good is null && key.Name == "*")
            {
                namesFound = false; // any-name keys (a ROM) match by content only: a same-size file says nothing
                continue;
            }
            if (good is null)
            {
                mismatched.Add(key.Name);
                continue;
            }
            anchor ??= key.Name == "*"
                ? (good.Path.LastIndexOf('/') is var slash and >= 0 ? good.Path[..slash] : "")
                : FileSet.AnchorOf(good.Path, key.Name);
        }
        var ok = namesFound && mismatched.Count == 0;
        return new DiskMatch(ok, namesFound && mismatched.Count > 0, anchor ?? "", mismatched);
    }
}

/// <summary>
/// The files of one detected version: its matched containers, each with an anchor folder (where the key files sit;
/// empty for an image's root). Lookups are case-insensitive by path tail (<c>MESS1</c>, <c>D/m0</c>), nearest the
/// anchor first.
/// </summary>
public sealed class FileSet
{
    private readonly IReadOnlyList<(Container Container, string Anchor)> _parts;

    public FileSet(IReadOnlyList<(Container Container, string Anchor)> parts) => _parts = parts;

    public IEnumerable<Container> Containers => _parts.Select(p => p.Container).Distinct();

    /// <summary>The file at <paramref name="name"/> (a name or a path tail), or null.</summary>
    public ImportFile? Find(string name)
    {
        foreach (var (c, anchor) in _parts)
        {
            var exact = anchor.Length == 0 ? name : anchor + "/" + name;
            var hit = c.Files.FirstOrDefault(f => f.Path.Equals(exact, StringComparison.OrdinalIgnoreCase));
            if (hit is not null)
                return hit;
        }
        foreach (var (c, anchor) in _parts)
        {
            var hit = c.Files
                .Where(f => (anchor.Length == 0 || f.Path.StartsWith(anchor + "/", StringComparison.OrdinalIgnoreCase))
                            && TailMatches(f.Path, name))
                .OrderBy(f => f.Path.Length)
                .FirstOrDefault();
            if (hit is not null)
                return hit;
        }
        return null;
    }

    /// <summary>The bytes of <paramref name="name"/>; throws <see cref="FileNotFoundException"/> naming the file.</summary>
    public byte[] Read(string name) =>
        (Find(name) ?? throw new FileNotFoundException($"{name} not found in the selected copy", name)).Data;

    public bool Has(string name) => Find(name) is not null;

    /// <summary>Every file under the anchors (for pipelines that enumerate, e.g. all <c>OUT*.DAT</c>).</summary>
    public IEnumerable<ImportFile> All() =>
        _parts.SelectMany(p => p.Container.Files.Where(f =>
            p.Anchor.Length == 0 || f.Path.StartsWith(p.Anchor + "/", StringComparison.OrdinalIgnoreCase)));

    internal static bool TailMatches(string path, string tail) =>
        path.Equals(tail, StringComparison.OrdinalIgnoreCase)
        || path.EndsWith("/" + tail, StringComparison.OrdinalIgnoreCase);

    internal static string AnchorOf(string path, string tail) =>
        path.Length > tail.Length ? path[..(path.Length - tail.Length - 1)] : "";
}
