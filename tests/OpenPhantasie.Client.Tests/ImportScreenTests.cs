using Microsoft.Xna.Framework.Input;
using OpenPhantasie.Client;
using OpenPhantasie.Importer;

namespace OpenPhantasie.Client.Tests;

/// <summary>s158: the first-run import screen (10-importer.md 4.1), with the import itself faked.</summary>
public class ImportScreenTests
{
    private static ImportScreen Screen(Func<IReadOnlyList<string>, IProgress<string>, ImportReport> run,
        params StoreLibraries.Library[] libraries) =>
        new(Path.Combine(Path.GetTempPath(), "phantasie-import-screen-test"), run, () => libraries);

    private static void Type(ImportScreen s, string text)
    {
        foreach (var c in text)
            s.OnTextInput(c);
    }

    private static void WaitForReport(ImportScreen s)
    {
        for (var i = 0; i < 500 && !s.ShowingReport; i++)
        {
            Thread.Sleep(10);
            s.Update();
        }
        Assert.True(s.ShowingReport);
    }

    [Fact]
    public void TypedPath_IsAdded_AndDeleteRemovesIt()
    {
        var s = Screen((_, _) => new ImportReport());
        s.Open(firstRun: true);
        Assert.Equal("WELCOME", s.Lines()[0].Text);
        Type(s, "/no/such/folder");
        s.HandleKeys([Keys.Enter]);
        Assert.Empty(s.Paths); // not found: stays typed
        Assert.Equal("/no/such/folder", s.Typed);
        for (var i = 0; i < 15; i++)
            s.OnTextInput('\b');
        Type(s, Path.GetTempPath());
        s.HandleKeys([Keys.Enter]);
        Assert.Single(s.Paths);
        Assert.Equal("", s.Typed);
        s.HandleKeys([Keys.Down, Keys.Delete]);
        Assert.Empty(s.Paths);
    }

    [Fact]
    public void Import_RunsInTheBackground_ShowsTheReport_AndCallsBack()
    {
        IReadOnlyList<string>? asked = null;
        var report = new ImportReport { Committed = true };
        report.Versions.Add(new("p1-dos-1987", "Phantasie I (DOS, SSI 1987)", true, 12,
            ["This copy of Phantasie I (DOS, SSI 1987) has been played (OUT3.DAT changed). It was imported as it is: on the saved map regions (OUT3.DAT) the fog is already lifted and fixed fights already won stay gone."], null));
        report.Skipped.Add("Phantasie II (C64): recognised; no importer for this version yet");
        var s = Screen((paths, _) => { asked = paths; return report; });
        var called = false;
        s.OnImported = () => called = true;
        s.Open(firstRun: false);
        s.AddPaths([Path.GetTempPath()]);
        s.HandleKeys([Keys.Down, Keys.Down, Keys.Enter]); // path row, IMPORT
        WaitForReport(s);
        Assert.True(called);
        Assert.True(s.Imported);
        Assert.Equal([Path.GetFullPath(Path.GetTempPath())], asked);
        var all = ImportScreen.ReportLines(report);
        Assert.All(all, l => Assert.True(l.Text.Length <= ImportScreen.Columns, l.Text));
        var text = string.Join(" ", all.Select(l => l.Text.Trim()));
        Assert.Contains("fog is already lifted", text);
        Assert.Contains("C64", text);
        s.HandleKeys([Keys.Enter]);
        Assert.False(s.ShowingReport);
    }

    [Fact]
    public void LibraryShortcut_AddsTheFolder_EscCloses()
    {
        var s = Screen((_, _) => new ImportReport(), new StoreLibraries.Library("Steam", "/steam/common"));
        s.Open(firstRun: false);
        Assert.Contains(s.Lines(), l => l.Text.Contains("SEARCH Steam: /steam/common"));
        s.HandleKeys([Keys.Down, Keys.Enter]);
        Assert.Equal(["/steam/common"], s.Paths);
        Assert.True(s.HandleKeys([Keys.Escape]));
        Assert.False(s.IsOpen);
    }

    [Fact]
    public void Wrap_KeepsEveryWord_WithinTheScreen()
    {
        var long1 = new string('x', 120);
        var lines = ImportScreen.Wrap("a short start " + long1 + " end", MenuLineStyle.Normal);
        Assert.All(lines, l => Assert.True(l.Text.Length <= ImportScreen.Columns));
        Assert.Equal(("ashortstart" + long1 + "end").Length, string.Concat(lines.Select(l => l.Text.Replace(" ", ""))).Length);
    }
}
