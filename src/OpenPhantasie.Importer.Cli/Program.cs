using OpenPhantasie.Data;
using OpenPhantasie.Importer;
using OpenPhantasie.Importer.Profiles;

// openphantasie-import <path>... [--out DIR] [--list] [--steam]
//   Scans the paths (disk images, zips, folders), lists what it finds and (without --list) imports it into DIR
//   (default: the Client's import folder, <LocalApplicationData>/OpenPhantasie/import). Same library as the
//   Client's import screen (10-importer.md 4.1).

var paths = new List<string>();
string? outDir = null;
var listOnly = false;
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
        case "--steam":
            paths.AddRange(StoreLibraries.Find().Select(l => l.Path));
            break;
        case "-h" or "--help":
            Console.WriteLine("usage: openphantasie-import <path>... [--out DIR] [--list] [--steam]");
            return 0;
        default:
            paths.Add(args[i]);
            break;
    }
}
if (paths.Count == 0)
{
    Console.Error.WriteLine("usage: openphantasie-import <path>... [--out DIR] [--list] [--steam]");
    return 2;
}
outDir ??= Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OpenPhantasie", "import");

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

var report = new ImportRunner(outDir, GameDataLocator.ShippedRoot)
    .Run(scan, new Progress<string>(m => Console.WriteLine("  " + m)));
foreach (var v in report.Versions)
{
    Console.WriteLine(v.Ok ? $"imported {v.Title}: {v.FilesWritten} files" : $"FAILED {v.Title}: {v.Error}");
    foreach (var w in v.Warnings)
        Console.WriteLine("  warning: " + w);
}
foreach (var s in report.Skipped)
    Console.WriteLine("skipped: " + s);
Console.WriteLine(report.Committed ? $"written to {outDir}" : "nothing written");
return report.Versions.All(v => v.Ok) ? 0 : 1;
