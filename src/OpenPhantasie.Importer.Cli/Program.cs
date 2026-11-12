using OpenPhantasie.Data;
using OpenPhantasie.Importer;
using OpenPhantasie.Importer.Profiles;

// openphantasie-import <path>... [--out DIR] [--list] [--check] [--steam] [--restore-maps | --keep-maps]
// openphantasie-import --refresh [--out DIR]
//   Scans the paths (disk images, zips, folders), lists what it finds and (without --list) imports it into DIR
//   (default: the Client's import folder, <LocalApplicationData>/OpenPhantasie/import). Same library as the
//   Client's import screen (10-importer.md 4.1).
//   --check: import into a scratch copy and compare every output with the sha1 DIR's import.json recorded for that
//   version; DIR is not changed. Exit 0 when all match, 1 otherwise.
//   --restore-maps / --keep-maps: a played DOS I copy's maps (fog lifted, fixed fights spent) are restored as on a
//   fresh copy, or imported as they are. Without either, the importer asks on a terminal and keeps them otherwise (s205).
//   --refresh: re-run the versions DIR's import.json records with an older recipe, from their recorded sources (4.4).

var paths = new List<string>();
string? outDir = null;
var listOnly = false;
var check = false;
var refresh = false;
bool? restoreMaps = null;
const string Usage = "usage: openphantasie-import <path>... [--out DIR] [--list] [--check] [--steam] [--restore-maps | --keep-maps] | --refresh [--out DIR]";
for (var i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--out" when i + 1 < args.Length:
            outDir = args[++i];
            break;
        case "--list":
            listOnly = true;
            break;
        case "--check":
            check = true;
            break;
        case "--refresh":
            refresh = true;
            break;
        case "--restore-maps":
            restoreMaps = true;
            break;
        case "--keep-maps":
            restoreMaps = false;
            break;
        case "--steam":
            paths.AddRange(StoreLibraries.Find().Select(l => l.Path));
            break;
        case "-h" or "--help":
            Console.WriteLine(Usage);
            return 0;
        default:
            paths.Add(args[i]);
            break;
    }
}
outDir ??= Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OpenPhantasie", "import");
if (refresh)
{
    var stale = ImportRunner.StaleVersions(outDir);
    foreach (var v in stale)
        Console.WriteLine($"older recipe: {v.ProfileId} (recipe {v.Recipe}, now {v.Current})");
    if (stale.Count == 0)
    {
        Console.WriteLine("every version is up to date");
        return 0;
    }
    var refreshed = new ImportRunner(outDir, GameDataLocator.ShippedRoot).Refresh(new Progress<string>(m => Console.WriteLine("  " + m)));
    return Print(refreshed);
}
if (paths.Count == 0)
{
    Console.Error.WriteLine(Usage);
    return 2;
}

var scan = ImportRunner.Scan(paths);
foreach (var note in scan.Notes)
    Console.WriteLine("note: " + note);
foreach (var d in scan.Detections)
    Console.WriteLine($"{d.Status,-12} {d.Profile.Id,-18} {d.Message}");
if (listOnly)
    return 0;
if (!scan.Importable.Any())
{
    Console.WriteLine("nothing to import");
    return 1;
}

if (check)
{
    var results = new ImportRunner(outDir, GameDataLocator.ShippedRoot).Check(scan);
    foreach (var v in results)
    {
        if (!v.Ok)
            Console.WriteLine($"FAILED {v.Title}: {v.Error}");
        else if (!v.InImport)
            Console.WriteLine($"not in the import: {v.Title}");
        else
            Console.WriteLine(v.Same ? $"same: {v.Title}"
                : $"differs: {v.Title}: {v.Changed.Count} changed, {v.Added.Count} new, {v.Missing.Count} no longer written");
        foreach (var k in v.Changed)
            Console.WriteLine("  changed: " + k);
        foreach (var k in v.Added)
            Console.WriteLine("  new: " + k);
        foreach (var k in v.Missing)
            Console.WriteLine("  gone: " + k);
    }
    return results.All(v => v.Same) ? 0 : 1;
}

var restore = new HashSet<string>(StringComparer.Ordinal);
foreach (var d in ImportRunner.RestorableMaps(scan))
{
    var yes = restoreMaps;
    if (yes is null && !Console.IsInputRedirected)
    {
        Console.Write($"{d.Profile.Title} has been played: its maps have the fog lifted and fixed fights already won. " +
                      "Restore them as on a fresh copy? [y/N] ");
        yes = Console.ReadLine()?.Trim().StartsWith("y", StringComparison.OrdinalIgnoreCase) == true;
    }
    if (yes == true)
        restore.Add(d.Profile.Id);
}
return Print(new ImportRunner(outDir, GameDataLocator.ShippedRoot)
    .Run(scan, new Progress<string>(m => Console.WriteLine("  " + m)), restoreMaps: restore));

int Print(ImportReport report)
{
    foreach (var v in report.Versions)
    {
        Console.WriteLine(v.Ok ? $"imported {v.Title}: {v.FilesWritten} files" : $"FAILED {v.Title}: {v.Error}");
        foreach (var w in v.Warnings)
            Console.WriteLine("  warning: " + w);
    }
    foreach (var s in report.Skipped)
        Console.WriteLine("skipped: " + s);
    foreach (var n in report.Notes)
        Console.WriteLine("note: " + n);
    // s160: a companion file the import still lacks (the Amiga games' screen font needs the Kickstart 1.3 ROM).
    foreach (var o in report.Offers)
        Console.WriteLine($"missing: {string.Join(", ", o.ForTitles)} want a file from {o.Title} ({o.GivesId}); " +
                          $"add it with: openphantasie-import <file> --out \"{outDir}\"");
    Console.WriteLine(report.Committed ? $"written to {outDir}" : "nothing written");
    return report.Versions.All(v => v.Ok) ? 0 : 1;
}
