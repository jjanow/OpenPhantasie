using OpenPhantasie.Importer.Pipelines;
using OpenPhantasie.Importer.Profiles;

namespace OpenPhantasie.Importer.Tests;

/// <summary>
/// <see cref="ImportContext.TryOptional"/> (s222): an art step that meets a missing or malformed file warns and takes back
/// its own writes, the tables written before it stay, and any other exception still fails the version.
/// </summary>
public class OptionalStepTests
{
    private static ImportContext NewContext()
    {
        var detection = new Detection
        {
            Profile = VersionProfiles.Get("p1-c64"), Status = DetectionStatus.Ready, Message = "test",
        };
        var session = new ImportSession
        {
            ShippedGameDataRoot = Path.GetTempPath(), StagingRoot = Path.Combine(Path.GetTempPath(), "op-none"),
            Detections = [detection],
        };
        return new ImportContext(session, detection);
    }

    [Theory]
    [InlineData(typeof(InvalidDataException))]
    [InlineData(typeof(FileNotFoundException))]
    [InlineData(typeof(EndOfStreamException))]
    [InlineData(typeof(IndexOutOfRangeException))]
    public void A_failed_step_warns_and_leaves_no_half_pack(Type exception)
    {
        var ctx = NewContext();
        ctx.WriteGameDataBytes("phantasie1", "monsters.json", [1]);
        ctx.WriteFile("art/kept.png", [2]);

        var done = ctx.TryOptional("No C64 art", () =>
        {
            ctx.WriteFile("art/new.png", [3]);
            ctx.WriteFile("art/kept.png", [4]);
            throw (Exception)Activator.CreateInstance(exception, "MN: expected at least 1 bytes, got 0")!;
        });

        Assert.False(done);
        Assert.Equal(["GameData/phantasie1/monsters.json", "files/art/kept.png"], ctx.Pending.Keys.Order(StringComparer.Ordinal));
        Assert.Equal([2], ctx.Pending["files/art/kept.png"]);
        Assert.Equal("No C64 art: MN: expected at least 1 bytes, got 0.", Assert.Single(ctx.Warnings));
    }

    [Fact]
    public void A_finished_step_keeps_its_writes()
    {
        var ctx = NewContext();
        Assert.True(ctx.TryOptional("No C64 art", () => ctx.WriteFile("art/new.png", [3])));
        Assert.True(ctx.Pending.ContainsKey("files/art/new.png"));
        Assert.Empty(ctx.Warnings);
    }

    [Fact]
    public void Other_exceptions_still_fail_the_version()
    {
        var ctx = NewContext();
        Assert.Throws<InvalidOperationException>(() => ctx.TryOptional("No C64 art", () => throw new InvalidOperationException()));
    }
}
