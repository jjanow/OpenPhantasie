using Microsoft.Xna.Framework.Input;
using OpenPhantasie.Client;
using OpenPhantasie.Importer;
using OpenPhantasie.Importer.Profiles;

namespace OpenPhantasie.Client.Tests;

/// <summary>s205 (user): a played DOS I copy is asked about before the import: restore its maps or keep them as played.</summary>
public class ImportRestoreMapsTests
{
    private const string Dos = "p1-dos-1987";

    private static ScanResult PlayedScan(params string[] played) => new()
    {
        Paths = [], Containers = [], Notes = [],
        Detections = [new Detection { Profile = VersionProfiles.Get(Dos), Status = DetectionStatus.Ready, Message = "found", PlayedFiles = played }],
    };

    private static (ImportScreen Screen, List<IReadOnlySet<string>> Runs) Screen(ScanResult scan)
    {
        var runs = new List<IReadOnlySet<string>>();
        var s = new ImportScreen(Path.Combine(Path.GetTempPath(), "phantasie-import-restore-test"), libraries: () => [],
            scan: _ => scan, import: (_, restore, _) => { runs.Add(restore); return new ImportReport(); });
        s.Open(firstRun: false);
        s.AddPaths([Path.GetTempPath()]);
        s.HandleKeys([Keys.Down, Keys.Down, Keys.Enter]); // IMPORT
        return (s, runs);
    }

    private static void Until(ImportScreen s, Func<bool> done)
    {
        for (var i = 0; i < 500 && !done(); i++)
        {
            Thread.Sleep(10);
            s.Update();
        }
        Assert.True(done());
    }

    [Fact]
    public void APlayedCopy_IsAsked_AndYesRestoresIt()
    {
        var (s, runs) = Screen(PlayedScan("OUT2.DAT", "GUILD.DAT"));
        Until(s, () => s.AskingRestore);
        Assert.Empty(runs);
        var lines = s.Lines().Select(l => l.Text).ToList();
        Assert.Equal("A PLAYED COPY", lines[0]);
        Assert.Contains(lines, l => l.Contains("been played"));
        Assert.Contains(lines, l => l.Contains("RESTORE THE MAPS"));
        Assert.Contains(lines, l => l.Contains("KEEP THEM AS PLAYED"));
        Assert.Contains("Y restore", s.Help);
        Assert.Equal(2, s.RowCount);
        s.HandleKeys([Keys.Enter]); // cursor on RESTORE
        Until(s, () => s.ShowingReport);
        Assert.Equal([Dos], Assert.Single(runs));
    }

    [Fact]
    public void KeepAndN_ImportAsPlayed_EscImportsNothing()
    {
        var (s, runs) = Screen(PlayedScan("OUT2.DAT"));
        Until(s, () => s.AskingRestore);
        s.HandleKeys([Keys.Down, Keys.Enter]);
        Until(s, () => s.ShowingReport);
        Assert.Empty(Assert.Single(runs));

        (s, runs) = Screen(PlayedScan("OUT2.DAT"));
        Until(s, () => s.AskingRestore);
        s.HandleKeys([Keys.N]);
        Until(s, () => s.ShowingReport);
        Assert.Empty(Assert.Single(runs));

        (s, runs) = Screen(PlayedScan("OUT2.DAT"));
        Until(s, () => s.AskingRestore);
        s.HandleKeys([Keys.Escape]);
        Assert.False(s.AskingRestore);
        Assert.False(s.Running);
        Assert.True(s.IsOpen);
        Assert.Single(s.Paths); // the paths stay
        Thread.Sleep(50);
        s.Update();
        Assert.Empty(runs);
    }

    [Fact]
    public void AFreshCopy_OrOnlySaveFilesChanged_IsNotAsked()
    {
        foreach (var played in new[] { Array.Empty<string>(), ["GUILD.DAT", "SACK.DAT"] })
        {
            var (s, runs) = Screen(PlayedScan(played));
            Until(s, () => s.ShowingReport);
            Assert.Empty(Assert.Single(runs));
        }
    }

    [Fact]
    public void Pad_AnswersWithAAndB()
    {
        var (s, runs) = Screen(PlayedScan("OUT3.DAT"));
        Until(s, () => s.AskingRestore);
        s.HandleKeys(s.PadKeys([Microsoft.Xna.Framework.Input.Buttons.DPadDown]));
        s.HandleKeys(s.PadKeys([Microsoft.Xna.Framework.Input.Buttons.A]));
        Until(s, () => s.ShowingReport);
        Assert.Empty(Assert.Single(runs));
    }
}
