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
    public void Refresh_RunsOnOpen_ShowsTheReport_AndCallsBack()
    {
        // s163 (10-importer.md 4.4): a version built by an older recipe is re-run from its recorded files at start.
        var report = new ImportReport { Committed = true };
        report.Versions.Add(new("p3-dos-1988", "Phantasie III (DOS, SSI 1988)", true, 900, [], null));
        report.Notes.Add("Phantasie I (DOS, SSI 1987) was imported by an older version of the importer, and its files were not found again");
        var ran = 0;
        var s = new ImportScreen(Path.Combine(Path.GetTempPath(), "phantasie-import-screen-test"), (_, _) => new ImportReport(),
            () => [], _ => { ran++; return report; });
        var called = false;
        s.OnImported = () => called = true;
        s.OpenRefresh();
        Assert.True(s.IsOpen);
        Assert.False(s.FirstRun);
        WaitForReport(s);
        Assert.Equal(1, ran);
        Assert.True(called);
        var text = string.Join(" ", ImportScreen.ReportLines(report).Select(l => l.Text.Trim()));
        Assert.Contains("Phantasie III", text);
        Assert.Contains("not found again", text);
    }

    [Fact]
    public void MissingTopaz_TheReportOffersIt_AImportsTheGivenFileAlone()
    {
        // s160: Amiga III imported without the Kickstart 1.3 ROM.
        var calls = new List<IReadOnlyList<string>>();
        var first = new ImportReport { Committed = true };
        first.Versions.Add(new("p3-amiga-1.0", "Phantasie III (Amiga v1.0, 1987)", true, 600, [], null));
        first.Offers.Add(new ImportOffer("kickstart-1.3", "Amiga Kickstart 1.3 ROM (34.5)", ["Phantasie III (Amiga v1.0, 1987)"],
            "import.gives.topaz8"));
        var second = new ImportReport { Committed = true };
        second.Versions.Add(new("kickstart-1.3", "Amiga Kickstart 1.3 ROM (34.5)", true, 3, [], null));
        var s = Screen((paths, _) => { calls.Add(paths); return calls.Count == 1 ? first : second; });
        s.Open(firstRun: false);
        s.AddPaths([Path.GetTempPath()]);
        s.HandleKeys([Keys.Down, Keys.Down, Keys.Enter]);
        WaitForReport(s);
        Assert.NotNull(s.Offer);
        var text = string.Join(" ", ImportScreen.ReportLines(first).Select(l => l.Text.Trim()));
        Assert.Contains("topaz 8", text);
        Assert.Contains("Kickstart 1.3", text);
        Assert.Contains("Press A", text);
        Assert.Contains("A add", s.Help);

        s.HandleKeys([Keys.A]);
        Assert.False(s.ShowingReport);
        Assert.True(s.AskingForOffer);
        Assert.Equal(1, s.RowCount); // only the file row
        Assert.Contains(s.Lines(), l => l.Text.Contains("FILE:", StringComparison.Ordinal));
        Assert.Contains(s.Lines(), l => l.Text.Contains("Kickstart", StringComparison.Ordinal));
        var rom = Path.Combine(Path.GetTempPath(), "phantasie-import-screen-kick.rom");
        File.WriteAllBytes(rom, [0]);
        try
        {
            Type(s, rom);
            s.HandleKeys([Keys.Enter]);
            WaitForReport(s);
            Assert.Equal(2, calls.Count);
            Assert.Equal([Path.GetFullPath(rom)], calls[1]); // the ROM alone, not the earlier paths
            Assert.False(s.AskingForOffer);
            Assert.Null(s.Offer);
        }
        finally
        {
            File.Delete(rom);
        }
    }

    private static (ImportScreen Screen, List<IReadOnlyList<string>> Calls) OfferShown()
    {
        var calls = new List<IReadOnlyList<string>>();
        var first = new ImportReport { Committed = true };
        first.Offers.Add(new ImportOffer("kickstart-1.3", "Amiga Kickstart 1.3 ROM (34.5)", ["Phantasie I (Amiga v1.0, 1987)"],
            "import.gives.topaz8"));
        var s = Screen((paths, _) => { calls.Add(paths); return calls.Count == 1 ? first : new ImportReport(); });
        s.Open(firstRun: false);
        s.AddPaths([Path.GetTempPath()]);
        s.HandleKeys([Keys.Down, Keys.Down, Keys.Enter]);
        WaitForReport(s);
        return (s, calls);
    }

    [Fact]
    public void Offer_EscBacksOut_TheScreenStaysOpen()
    {
        var (s, calls) = OfferShown();
        s.HandleKeys([Keys.A, Keys.Escape]);
        Assert.False(s.AskingForOffer);
        Assert.True(s.IsOpen);
        s.AddPaths([Path.GetTempPath()]); // a drop now only adds a path again
        Assert.Single(calls);
        Assert.Single(s.Paths);
    }

    [Fact]
    public void Offer_ADroppedFile_IsImportedAlone()
    {
        var (s, calls) = OfferShown();
        s.HandleKeys([Keys.A]);
        s.AddPaths([Path.GetTempPath()]);
        WaitForReport(s);
        Assert.Equal(2, calls.Count);
        Assert.Equal([Path.GetFullPath(Path.GetTempPath())], calls[1]);
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
