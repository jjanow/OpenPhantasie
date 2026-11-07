using System.Text.Json.Serialization;
using OpenPhantasie.Data.Audio;

namespace OpenPhantasie.Data;

/// <summary>
/// Manifest <c>reward_scene</c> (Phantasie III Amiga, the gods' reward root <c>0x1ee82</c>; <c>FORMAT.md</c> §2.4, §3.1):
/// the flashes of the intro (<see cref="Flash"/>) and the tune played from the god's picture to the scene's end
/// (<see cref="Tune"/>, <c>D/M1</c>: started at <c>0x1efe8</c>, stopped at <c>0x1f24a</c>). Either may be null.
/// </summary>
public sealed record RewardSceneLayout(RewardFlashLayout? Flash, TitleTune? Tune)
{
    internal static RewardSceneLayout FromDto(Dto dto, int? packVblHz, string manifestPath, string rootDirectory)
    {
        InvalidDataException Bad(string what) => new($"{manifestPath}: reward_scene: {what}.");
        RewardFlashLayout? flash = null;
        if (dto.Flash is { } f)
        {
            var hz = f.VblHz ?? packVblHz;
            if (f.Pictures is not { Count: > 0 } pictures || pictures.Any(string.IsNullOrWhiteSpace))
                throw Bad("flash: no pictures");
            if (f.Count <= 0 || f.XRange <= 0 || f.YRange <= 0 || f.XJitter <= 0 || f.YJitter <= 0 || f.UnitX <= 0 ||
                f.WaitMin < 0 || f.WaitMax < f.WaitMin || f.WaitStart < 0 || f.WaitStepEvery <= 0 || hz is not > 0)
                throw Bad("flash: bad numbers");
            if (f.Sound is <= 0 || f.SoundFrame < 0 || f.SoundFrame >= pictures.Count)
                throw Bad("flash: bad sound");
            flash = new RewardFlashLayout(pictures, f.Count, f.XRange, f.YRange, f.XJitter, f.XMax ?? int.MaxValue,
                f.YJitter, f.YAdd, f.UnitX, f.WaitStart, f.WaitMin, f.WaitMax, f.WaitStepEvery, f.WaitExtraVbl, hz.Value)
            {
                Sound = f.Sound,
                SoundFrame = f.SoundFrame,
            };
        }
        TitleTune? tune = null;
        if (dto.Tune is { } td)
        {
            if (td.Format is null || !TuneFormats.All.Contains(td.Format) || string.IsNullOrEmpty(td.File))
                throw Bad("tune: unknown format or no file");
            tune = new TitleTune(td.Format, Path.GetFullPath(Path.Combine(rootDirectory, td.File)), td.Offset ?? 0, td.Length,
                null, td.VblHz ?? packVblHz, td.ClockHz);
        }
        return new RewardSceneLayout(flash, tune);
    }

    internal sealed class Dto
    {
        public FlashDto? Flash { get; set; }
        public TitleSequence.TuneDto? Tune { get; set; }
    }

    internal sealed class FlashDto
    {
        public List<string>? Pictures { get; set; }
        public int Count { get; set; }
        [JsonPropertyName("x_range")] public int XRange { get; set; }
        [JsonPropertyName("y_range")] public int YRange { get; set; }
        [JsonPropertyName("x_jitter")] public int XJitter { get; set; }
        [JsonPropertyName("x_max")] public int? XMax { get; set; }
        [JsonPropertyName("y_jitter")] public int YJitter { get; set; }
        [JsonPropertyName("y_add")] public int YAdd { get; set; }
        [JsonPropertyName("unit_x")] public int UnitX { get; set; }
        [JsonPropertyName("wait_start")] public int WaitStart { get; set; }
        [JsonPropertyName("wait_min")] public int WaitMin { get; set; }
        [JsonPropertyName("wait_max")] public int WaitMax { get; set; }
        [JsonPropertyName("wait_step_every")] public int WaitStepEvery { get; set; }
        [JsonPropertyName("wait_extra_vbl")] public int WaitExtraVbl { get; set; }
        [JsonPropertyName("vbl_hz")] public int? VblHz { get; set; }
        public int? Sound { get; set; }
        [JsonPropertyName("sound_frame")] public int SoundFrame { get; set; }
    }
}

/// <summary>
/// The reward scene's flashes (Amiga <c>0x1f30a</c> calling the sparkle <c>0x1b814</c>; <c>FORMAT.md</c> §2.4):
/// <see cref="Count"/> flashes per run, each at x = (rnd mod <see cref="XRange"/> + rnd mod <see cref="XJitter"/>, at most
/// <see cref="XMax"/>) · <see cref="UnitX"/> px and y = rnd mod <see cref="YRange"/> + rnd mod <see cref="YJitter"/> +
/// <see cref="YAdd"/>, showing <see cref="Pictures"/> one after another, each frame drawn over the ones before (no
/// restore between frames; the rectangle is restored after the last). Every frame waits the speed word w for
/// w + <see cref="WaitExtraVbl"/> VBLs (<c>0x2af6</c> loops until the counter passes start + w). The rising run starts
/// the word at <see cref="WaitStart"/> and lowers it by 1 every <see cref="WaitStepEvery"/> flashes (first at flash 0) down
/// to <see cref="WaitMin"/>; the fading run carries on from there, raising it the same way up to <see cref="WaitMax"/>.
/// </summary>
public sealed record RewardFlashLayout(
    IReadOnlyList<string> Pictures, int Count, int XRange, int YRange, int XJitter, int XMax, int YJitter, int YAdd,
    int UnitX, int WaitStart, int WaitMin, int WaitMax, int WaitStepEvery, int WaitExtraVbl, int VblHz)
{
    /// <summary>One flash: its top-left in pixels and the speed word of its frames.</summary>
    public readonly record struct Flash(int X, int Y, int Wait);

    /// <summary>
    /// The sound each flash starts (<c>0xb126(15)</c> in <c>0x1b814</c>; a <see cref="Audio.P3SoundEffects"/> number), or
    /// null for none.
    /// </summary>
    public int? Sound { get; init; }

    /// <summary>The frame the sound starts with: <c>0x1b814</c> calls it before drawing frame 1 (the second), after frame 0's wait.</summary>
    public int SoundFrame { get; init; }

    /// <summary>VBLs one frame of <paramref name="flash"/> lasts.</summary>
    public int FrameVbl(Flash flash) => flash.Wait + WaitExtraVbl;

    /// <summary>
    /// The VBL (from the run's start) each flash of <paramref name="run"/> starts <see cref="Sound"/> on: the flash's start
    /// plus <see cref="SoundFrame"/> frames. Empty without a sound.
    /// </summary>
    public IReadOnlyList<int> SoundVbls(IReadOnlyList<Flash> run)
    {
        ArgumentNullException.ThrowIfNull(run);
        var vbls = new List<int>();
        if (Sound is null)
            return vbls;
        var at = 0;
        foreach (var f in run)
        {
            vbls.Add(at + SoundFrame * FrameVbl(f));
            at += Pictures.Count * FrameVbl(f);
        }
        return vbls;
    }

    /// <summary>VBLs a whole run of flashes lasts.</summary>
    public int RunVbl(IReadOnlyList<Flash> run)
    {
        ArgumentNullException.ThrowIfNull(run);
        return run.Sum(f => Pictures.Count * FrameVbl(f));
    }

    /// <summary>
    /// The two runs of the scene (the noise growing, then fading), positions drawn from <paramref name="next"/>
    /// (<c>next(n)</c> = 0..n − 1, called in the original's order: the caller's x then y, then the sparkle's x then y).
    /// </summary>
    public (IReadOnlyList<Flash> Rising, IReadOnlyList<Flash> Fading) Plan(Func<int, int> next)
    {
        ArgumentNullException.ThrowIfNull(next);
        var wait = WaitStart;
        var rising = Run(true, ref wait, next);
        var fading = Run(false, ref wait, next);
        return (rising, fading);
    }

    private List<Flash> Run(bool rising, ref int wait, Func<int, int> next)
    {
        var run = new List<Flash>(Count);
        for (var i = 0; i < Count; i++)
        {
            if (i % WaitStepEvery == 0)
            {
                if (rising && wait > WaitMin)
                    wait--;
                else if (!rising && wait < WaitMax)
                    wait++;
            }
            var x = next(XRange);
            var y = next(YRange);
            x = Math.Min(x + next(XJitter), XMax);
            y += next(YJitter) + YAdd;
            run.Add(new Flash(x * UnitX, y, wait));
        }
        return run;
    }

    /// <summary>
    /// Where <paramref name="run"/> stands <paramref name="vbl"/> VBLs after it started: the flash shown and how many of
    /// its frames are drawn (1..<see cref="Pictures"/>.Count), or null once the run is over.
    /// </summary>
    public (Flash Flash, int Frames)? At(IReadOnlyList<Flash> run, double vbl)
    {
        ArgumentNullException.ThrowIfNull(run);
        if (vbl < 0)
            return null;
        foreach (var f in run)
        {
            var frame = FrameVbl(f);
            var length = Pictures.Count * frame;
            if (vbl < length)
                return (f, Math.Min(Pictures.Count, (int)(vbl / frame) + 1));
            vbl -= length;
        }
        return null;
    }
}
