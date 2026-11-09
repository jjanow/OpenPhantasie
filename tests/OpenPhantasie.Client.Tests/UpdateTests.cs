using System.Formats.Tar;
using System.IO.Compression;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Microsoft.Xna.Framework.Input;
using OpenPhantasie.Client;
using OpenPhantasie.Client.Updates;

namespace OpenPhantasie.Client.Tests;

/// <summary>s174: the self-updater: the release check, the download, putting a build over the install, the screen, the options.</summary>
public sealed class UpdateTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "op-update-test-" + Guid.NewGuid().ToString("N"));

    public UpdateTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
    }

    private const string ReleaseJson = """
        {"tag_name":"v1.2.0","html_url":"https://github.com/o/r/releases/tag/v1.2.0","body":"## What's new\r\n\r\n* **Faster** saves\r\n- `fix` one",
         "assets":[
          {"name":"OpenPhantasie-1.2.0-win-x64.zip","browser_download_url":"https://dl/win","size":10},
          {"name":"OpenPhantasie-1.2.0-linux-x64.tar.gz","browser_download_url":"https://dl/linux","size":5},
          {"name":"OpenPhantasie-1.2.0-osx-arm64.tar.gz","browser_download_url":"https://dl/mac","size":7}]}
        """;

    private static JsonElement Release(string json = ReleaseJson) => JsonDocument.Parse(json).RootElement;

    [Theory]
    [InlineData("v1.2.0", 1, 2, 0)]
    [InlineData("1.2", 1, 2, 0)]
    [InlineData("V10.0.3", 10, 0, 3)]
    public void TryParse_ReadsTags(string tag, int a, int b, int c) => Assert.Equal(new Version(a, b, c), AppVersion.TryParse(tag));

    [Theory]
    [InlineData("1.2.0-beta")]
    [InlineData("nightly")]
    [InlineData("")]
    public void TryParse_RefusesPreReleasesAndJunk(string tag) => Assert.Null(AppVersion.TryParse(tag));

    [Fact]
    public void Strip_DropsTheCommitSuffix() => Assert.Equal("1.0.0", AppVersion.Strip("1.0.0+abc123"));

    [Fact]
    public void LocalBuild_IsNotARelease()
    {
        Assert.Equal("0.0.0-dev", AppVersion.Text); // the test run uses a local build
        Assert.False(AppVersion.IsRelease);
    }

    [Fact]
    public void PlatformId_CoversTheThreeSystems()
    {
        Assert.Equal("win-x64", AppVersion.ComputePlatformId("win", Architecture.X64));
        Assert.Equal("osx-arm64", AppVersion.ComputePlatformId("osx", Architecture.Arm64));
        Assert.Equal("linux-x64", AppVersion.ComputePlatformId("linux", Architecture.X64));
        Assert.Null(AppVersion.ComputePlatformId("linux", Architecture.X86));
        Assert.Null(AppVersion.ComputePlatformId(null, Architecture.X64));
    }

    [Fact]
    public void Evaluate_NewerRelease_PicksThisMachinesArchive()
    {
        var r = UpdateService.Evaluate(Release(), new Version(1, 0, 0), "linux-x64");
        Assert.Equal(UpdateCheckOutcome.Available, r.Outcome);
        Assert.Equal("OpenPhantasie-1.2.0-linux-x64.tar.gz", r.Release!.AssetName);
        Assert.Equal("https://dl/linux", r.Release.AssetUrl);
        Assert.Equal(5, r.Release.AssetSize);
        Assert.Equal(new Version(1, 2, 0), r.Release.Version);
    }

    [Fact]
    public void Evaluate_SameOrOlder_IsUpToDate()
    {
        Assert.Equal(UpdateCheckOutcome.UpToDate, UpdateService.Evaluate(Release(), new Version(1, 2, 0), "win-x64").Outcome);
        Assert.Equal(UpdateCheckOutcome.UpToDate, UpdateService.Evaluate(Release(), new Version(2, 0, 0), "win-x64").Outcome);
    }

    [Fact]
    public void Evaluate_NoArchiveForThisMachine()
    {
        var r = UpdateService.Evaluate(Release(), new Version(1, 0, 0), "osx-x64");
        Assert.Equal(UpdateCheckOutcome.NoBuildForPlatform, r.Outcome);
        Assert.Equal(new Version(1, 2, 0), r.Latest);
    }

    [Theory]
    [InlineData("OpenPhantasie-1.0.0-osx-x64.tar.gz", "osx-x64", true)]
    [InlineData("OpenPhantasie-1.0.0-osx-x64.tar.gz", "osx-arm64", false)]
    [InlineData("OpenPhantasie-1.0.0-win-x64.zip", "win-x64", true)]
    [InlineData("OpenPhantasie-1.0.0-win-x64.zip.sha256", "win-x64", false)]
    [InlineData("Other-1.0.0-win-x64.zip", "win-x64", false)]
    public void IsAssetFor_MatchesNameEnd(string name, string platform, bool expected) =>
        Assert.Equal(expected, UpdateService.IsAssetFor(name, platform));

    private sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Seen { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Seen.Add(request);
            return Task.FromResult(respond(request));
        }
    }

    [Fact]
    public async Task Check_AsksTheLatestReleaseOfTheRepository()
    {
        var handler = new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(ReleaseJson) });
        var service = new UpdateService(new HttpClient(handler), "o/r", new Version(1, 0, 0), "win-x64");
        var r = await service.CheckAsync();
        Assert.Equal("https://api.github.com/repos/o/r/releases/latest", handler.Seen.Single().RequestUri!.ToString());
        Assert.Equal(UpdateCheckOutcome.Available, r.Outcome);
    }

    [Fact]
    public async Task Check_NoReleaseYet_IsUpToDate()
    {
        var handler = new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        var service = new UpdateService(new HttpClient(handler), "o/r", new Version(1, 0, 0), "win-x64");
        Assert.Equal(UpdateCheckOutcome.UpToDate, (await service.CheckAsync()).Outcome);
    }

    [Fact]
    public async Task Check_ServerError_Throws()
    {
        var handler = new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));
        var service = new UpdateService(new HttpClient(handler), "o/r", new Version(1, 0, 0), "win-x64");
        await Assert.ThrowsAsync<HttpRequestException>(() => service.CheckAsync());
    }

    [Fact]
    public async Task Download_WritesTheFile_AndChecksTheSize()
    {
        var bytes = Encoding.ASCII.GetBytes("12345");
        var handler = new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) });
        var service = new UpdateService(new HttpClient(handler), "o/r", new Version(1, 0, 0), "linux-x64");
        var release = new UpdateRelease(new Version(1, 2, 0), "v1.2.0", "", "", "a.tar.gz", "https://dl/linux", 5);
        var path = Path.Combine(_dir, "dl", "a.tar.gz");
        var seen = new List<double>();
        await service.DownloadAsync(release, path, new Progress(seen));
        Assert.Equal(bytes, File.ReadAllBytes(path));
        Assert.Equal(1.0, seen[^1]);

        await Assert.ThrowsAsync<IOException>(() => service.DownloadAsync(release with { AssetSize = 6 }, path, null));
    }

    private sealed class Progress(List<double> seen) : IProgress<double>
    {
        public void Report(double value) => seen.Add(value);
    }

    // ---- the installer ----------------------------------------------------------------------------------------------

    private string Install(params (string Rel, string Text)[] files)
    {
        var root = Path.Combine(_dir, "install");
        foreach (var (rel, text) in files)
        {
            var p = Path.Combine(root, rel);
            Directory.CreateDirectory(Path.GetDirectoryName(p)!);
            File.WriteAllText(p, text);
        }
        return root;
    }

    private string Zip(string name, params (string Rel, string Text)[] files)
    {
        var path = Path.Combine(_dir, name);
        using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
        foreach (var (rel, text) in files)
        {
            using var w = new StreamWriter(zip.CreateEntry(rel).Open());
            w.Write(text);
        }
        return path;
    }

    private string TarGz(string name, params (string Rel, string Text)[] files)
    {
        var path = Path.Combine(_dir, name);
        using var file = File.Create(path);
        using var gz = new GZipStream(file, CompressionLevel.Fastest);
        using var tar = new TarWriter(gz, TarEntryFormat.Pax);
        foreach (var (rel, text) in files)
        {
            var entry = new PaxTarEntry(TarEntryType.RegularFile, rel)
            {
                DataStream = new MemoryStream(Encoding.UTF8.GetBytes(text)),
                Mode = rel.EndsWith("OpenPhantasie") ? (UnixFileMode)0b111_101_101 : (UnixFileMode)0b110_100_100,
            };
            tar.WriteEntry(entry);
        }
        return path;
    }

    [Fact]
    public void InstallRoot_IsTheFolder_OrTheAppBundle()
    {
        Assert.Equal(Path.GetFullPath("/games/op"), UpdateInstaller.InstallRoot("/games/op/"));
        Assert.Equal(Path.GetFullPath("/Applications/OpenPhantasie.app"),
            UpdateInstaller.InstallRoot("/Applications/OpenPhantasie.app/Contents/MacOS/"));
    }

    [Fact]
    public void Install_Zip_WithATopFolder_ReplacesAndAddsFiles_KeepsOthers()
    {
        var root = Install(("OpenPhantasie.exe", "old exe"), ("GameData/a.json", "old a"), ("mine.txt", "keep"));
        var zip = Zip("u.zip", ("OpenPhantasie/OpenPhantasie.exe", "new exe"), ("OpenPhantasie/GameData/a.json", "new a"),
            ("OpenPhantasie/GameData/b.json", "new b"));

        Assert.Equal(3, UpdateInstaller.Install(zip, root, "OpenPhantasie.exe"));

        Assert.Equal("new exe", File.ReadAllText(Path.Combine(root, "OpenPhantasie.exe")));
        Assert.Equal("new a", File.ReadAllText(Path.Combine(root, "GameData/a.json")));
        Assert.Equal("new b", File.ReadAllText(Path.Combine(root, "GameData/b.json")));
        Assert.Equal("keep", File.ReadAllText(Path.Combine(root, "mine.txt")));
        Assert.Equal("old exe", File.ReadAllText(Path.Combine(root, "OpenPhantasie.exe" + UpdateInstaller.OldSuffix)));
        Assert.False(Directory.Exists(Path.Combine(root, UpdateInstaller.WorkFolder)));

        UpdateInstaller.CleanUp(root);
        Assert.Empty(Directory.EnumerateFiles(root, "*" + UpdateInstaller.OldSuffix, SearchOption.AllDirectories));
        Assert.Equal("keep", File.ReadAllText(Path.Combine(root, "mine.txt")));
    }

    [Fact]
    public void Install_TarGz_AppBundle_KeepsTheProgramExecutable()
    {
        var root = Install(("Contents/MacOS/OpenPhantasie", "old"), ("Contents/Info.plist", "old plist"));
        var tgz = TarGz("u.tar.gz", ("OpenPhantasie.app/Contents/MacOS/OpenPhantasie", "new"),
            ("OpenPhantasie.app/Contents/Info.plist", "new plist"));

        UpdateInstaller.Install(tgz, root, "Contents/MacOS/OpenPhantasie");

        var program = Path.Combine(root, "Contents/MacOS/OpenPhantasie");
        Assert.Equal("new", File.ReadAllText(program));
        Assert.Equal("new plist", File.ReadAllText(Path.Combine(root, "Contents/Info.plist")));
        if (!OperatingSystem.IsWindows())
            Assert.True(File.GetUnixFileMode(program).HasFlag(UnixFileMode.UserExecute));
    }

    [Fact]
    public void Install_ArchiveWithoutTheProgram_ChangesNothing()
    {
        var root = Install(("OpenPhantasie", "old"));
        var zip = Zip("u.zip", ("Other/Other", "x"));
        Assert.Throws<InvalidDataException>(() => UpdateInstaller.Install(zip, root, "OpenPhantasie"));
        Assert.Equal("old", File.ReadAllText(Path.Combine(root, "OpenPhantasie")));
    }

    [Fact]
    public void Apply_FailureHalfway_PutsEveryFileBack()
    {
        var root = Install(("a.txt", "old a"), ("b.txt", "old b"));
        var payload = Path.Combine(_dir, "payload");
        Directory.CreateDirectory(Path.Combine(payload, "c.txt")); // c.txt: a folder in the payload where nothing sits…
        File.WriteAllText(Path.Combine(payload, "a.txt"), "new a");
        File.WriteAllText(Path.Combine(payload, "b.txt"), "new b");
        File.WriteAllText(Path.Combine(payload, "new.txt"), "added");
        File.WriteAllText(Path.Combine(root, "c.txt"), "a file"); // …and a file in the install: the folder can't be made
        File.WriteAllText(Path.Combine(payload, "c.txt", "inner.txt"), "x");

        Assert.ThrowsAny<IOException>(() => UpdateInstaller.Apply(payload, root));

        Assert.Equal("old a", File.ReadAllText(Path.Combine(root, "a.txt")));
        Assert.Equal("old b", File.ReadAllText(Path.Combine(root, "b.txt")));
        Assert.False(File.Exists(Path.Combine(root, "new.txt")));
        Assert.Empty(Directory.EnumerateFiles(root, "*" + UpdateInstaller.OldSuffix));
    }

    [Fact]
    public void CanWrite_TrueForAWritableFolder() => Assert.True(UpdateInstaller.CanWrite(Install(("x", "y"))));

    // ---- the screen -------------------------------------------------------------------------------------------------

    private static readonly UpdateRelease Found =
        new(new Version(1, 2, 0), "v1.2.0", "## New\n\n* one\n* two", "", "OpenPhantasie-1.2.0-linux-x64.tar.gz", "u", 1);

    private static void Settle(UpdateScreen s, UpdateScreen.State until)
    {
        for (var i = 0; i < 500 && s.Current != until; i++)
        {
            Thread.Sleep(10);
            s.Update();
        }
        Assert.Equal(until, s.Current);
    }

    [Fact]
    public void Screen_LocalBuild_SaysSo_AndDoesNotCheck()
    {
        var asked = false;
        var s = new UpdateScreen(_ => { asked = true; return Task.FromResult(new UpdateCheckResult(UpdateCheckOutcome.UpToDate, null, null)); },
            (_, _) => Task.CompletedTask, isRelease: false);
        s.Open();
        Assert.Equal(UpdateScreen.State.DevBuild, s.Current);
        Assert.False(asked);
        Assert.Contains(s.Lines(), l => l.Text.Contains("local build"));
        Assert.True(s.HandleKeys([Keys.Escape]));
    }

    [Fact]
    public void Screen_CheckUpToDate()
    {
        var s = new UpdateScreen(_ => Task.FromResult(new UpdateCheckResult(UpdateCheckOutcome.UpToDate, null, null)),
            (_, _) => Task.CompletedTask, isRelease: true);
        s.Open();
        Settle(s, UpdateScreen.State.UpToDate);
        Assert.Contains(s.Lines(), l => l.Text.Contains("latest version"));
    }

    [Fact]
    public void Screen_CheckFails_ShowsTheReason()
    {
        var s = new UpdateScreen(_ => Task.FromException<UpdateCheckResult>(new HttpRequestException("no network")),
            (_, _) => Task.CompletedTask, isRelease: true);
        s.Open();
        Settle(s, UpdateScreen.State.Failed);
        Assert.Contains(s.Lines(), l => l.Text.Contains("no network"));
    }

    [Fact]
    public void Screen_Available_UpdateNow_InstallsThenAsksForRestart()
    {
        UpdateRelease? installed = null;
        var s = new UpdateScreen(_ => throw new InvalidOperationException("not asked"),
            (r, p) => { installed = r; p.Report(1.0); return Task.CompletedTask; }, isRelease: true);
        s.OpenWith(new UpdateCheckResult(UpdateCheckOutcome.Available, Found, Found.Version));
        var lines = s.Lines().Select(l => l.Text).ToList();
        Assert.Contains(lines, t => t.Contains("Version 1.2.0 is available."));
        Assert.Contains(lines, t => t.Trim() == "New");
        Assert.Contains(lines, t => t.Trim() == "- one");
        Assert.Equal(">UPDATE NOW", lines[^2]);
        Assert.Equal(" LATER", lines[^1]);

        Assert.False(s.HandleKeys([Keys.Enter]));
        Settle(s, UpdateScreen.State.Installed);
        Assert.Same(Found, installed);
        Assert.True(s.HandleKeys([Keys.Enter]));
        Assert.True(s.RestartRequested);
    }

    [Fact]
    public void Screen_Available_Later_Closes()
    {
        var s = new UpdateScreen(_ => throw new InvalidOperationException(), (_, _) => throw new InvalidOperationException(), true);
        s.OpenWith(new UpdateCheckResult(UpdateCheckOutcome.Available, Found, Found.Version));
        s.HandleKeys([Keys.Down]);
        Assert.Equal(1, s.Cursor);
        Assert.True(s.HandleKeys([Keys.Enter]));
        Assert.False(s.RestartRequested);
        Assert.False(s.IsOpen);
    }

    [Fact]
    public void Screen_ClickOnLater_Closes()
    {
        var s = new UpdateScreen(_ => throw new InvalidOperationException(), (_, _) => throw new InvalidOperationException(), true);
        s.OpenWith(new UpdateCheckResult(UpdateCheckOutcome.Available, Found, Found.Version));
        Assert.True(s.Click(s.Lines().Count - 1));
        Assert.False(s.RestartRequested);
    }

    [Fact]
    public void Screen_InstallFails_SaysNothingChanged()
    {
        var s = new UpdateScreen(_ => throw new InvalidOperationException(),
            (_, _) => Task.FromException(new UnauthorizedAccessException("read-only")), true);
        s.OpenWith(new UpdateCheckResult(UpdateCheckOutcome.Available, Found, Found.Version));
        s.HandleKeys([Keys.Enter]);
        Settle(s, UpdateScreen.State.Failed);
        Assert.Contains(s.Lines(), l => l.Text.Contains("read-only"));
        Assert.True(s.HandleKeys([Keys.Enter]));
        Assert.False(s.RestartRequested);
    }

    [Fact]
    public void Notes_LongLinesWrap_NothingCut()
    {
        var text = string.Join(" ", Enumerable.Repeat("word", 40));
        var lines = UpdateScreen.Notes(text);
        Assert.True(lines.Count > 1);
        Assert.Equal(text, string.Join(" ", lines.Select(l => l.Text.Trim())));
    }

    // ---- the options ------------------------------------------------------------------------------------------------

    [Fact]
    public void Options_General_HasTheStartCheckSwitch_AndUpdateNow()
    {
        var settings = new ClientSettings();
        Assert.True(settings.CheckUpdatesAtStart);
        var o = new OptionsScreen(settings, _ => [], _ => false);
        o.Open();
        var rows = o.RowTexts().Select(t => t.TrimStart()).ToList();
        Assert.Contains(rows, t => t.StartsWith("CHECK UPDATES AT START") && t.EndsWith("ON"));
        Assert.Contains(rows, t => t.StartsWith("UPDATE NOW") && t.EndsWith("v0.0.0-dev"));

        var at = rows.FindIndex(t => t.StartsWith("CHECK UPDATES AT START"));
        for (var i = 0; i <= at; i++)
            o.HandleKeys([Keys.Down]);
        o.HandleKeys([Keys.Enter]);
        Assert.False(settings.CheckUpdatesAtStart);
        Assert.True(o.Changed);

        o.HandleKeys([Keys.Down, Keys.Enter]);
        Assert.True(o.UpdateRequested);
        Assert.False(o.IsOpen);
    }

    [OriginalDataFact]
    public void Options_InGame_HasNoUpdateNow()
    {
        var o = new OptionsScreen(new ClientSettings(), _ => [], _ => false);
        o.Open(ClientSessions.Overworld());
        o.HandleKeys([Keys.Left]); // the tab row: back to GENERAL
        Assert.Null(o.Tab);
        var rows = o.RowTexts().Select(t => t.TrimStart()).ToList();
        Assert.Contains(rows, t => t.StartsWith("CHECK UPDATES AT START"));
        Assert.DoesNotContain(rows, t => t.StartsWith("UPDATE NOW"));
    }

    [Fact]
    public void Settings_RoundTripTheSwitch()
    {
        var json = JsonSerializer.Serialize(new ClientSettings { CheckUpdatesAtStart = false },
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        Assert.Contains("\"checkUpdatesAtStart\":false", json);
    }
}
