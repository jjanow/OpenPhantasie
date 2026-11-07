using System.Globalization;

namespace OpenPhantasie.Data.Audio;

/// <summary>
/// Manifest <c>sound_effects</c> with format <c>amiga-dosound</c> (Phantasie I Amiga, s128; <c>reverse-engineering/amiga/
/// FORMAT.md</c> "Sound effects") `[C]`: <c>h89 +0xee(n)</c> plays entry n of the table at h91 <c>+0x1f4</c>, an Atari ST
/// Dosound script in hunk 90, through the title tune's player h89 <c>+0x304</c> (<see cref="AmigaDosoundScore"/>). No
/// samples: the 16-byte ramp on Paula. <see cref="Sounds"/>: per sound number the script offsets in
/// <see cref="FilePath"/> (the program <c>game</c>); one offset = that script; several = the scripts joined before playing,
/// each copied up to its first <c>0xFF</c> byte and one <c>0xFF</c> after the last (<c>+0x158</c>, sound 27: h90
/// <c>+0x24d</c> six times). The player blocks: the game waits <see cref="Hold"/> for each sound.
/// </summary>
public sealed record AmigaDosoundEffects(string FilePath, IReadOnlyDictionary<int, IReadOnlyList<int>> Sounds, int? ClockHz)
{
    /// <summary>
    /// What each step of the player costs beyond its <c>Delay</c> (audio.device writes; s130 live capture `[~]`, fitted
    /// on sounds 25 and 21), seconds.
    /// </summary>
    public double StepOverheadSeconds { get; init; }

    /// <summary>What one call of the player costs once beyond its steps (FINISH, WaitIO; s130 `[~]`), seconds.</summary>
    public double CallOverheadSeconds { get; init; }

    /// <summary>
    /// What each channel a step sends adds to that step (s153 `[~]`, fitted live): the player writes, FINISHes and
    /// <c>WaitIO</c>s every sent channel at each step (h89 <c>+0x304</c>), so a step of the effects' A-only scripts
    /// (<c>07 fe</c>: channels 2 and 3) costs less than one of A and B (<c>07 fc</c>: 0, 2 and 3), seconds.
    /// </summary>
    public double ChannelOverheadSeconds { get; init; }

    /// <summary>How long the player spends on <paramref name="step"/>: its <c>Delay</c>, <see cref="StepOverheadSeconds"/>
    /// and <see cref="ChannelOverheadSeconds"/> per channel it sends.</summary>
    public double StepSeconds(AmigaDosoundStep step)
    {
        ArgumentNullException.ThrowIfNull(step);
        return (double)step.Ticks / AmigaDosoundScore.TickHz + StepOverheadSeconds
            + step.Channels.Count(c => c.Sent) * ChannelOverheadSeconds;
    }

    /// <summary>The program's bytes, or null when the file is missing.</summary>
    public byte[]? TryLoadImage() => File.Exists(FilePath) ? File.ReadAllBytes(FilePath) : null;

    /// <summary>The script <c>+0x304</c> reads for sound <paramref name="sound"/>, or null for a number the pack lacks.</summary>
    public byte[]? Script(byte[] image, int sound)
    {
        ArgumentNullException.ThrowIfNull(image);
        if (!Sounds.TryGetValue(sound, out var offsets) || offsets.Count == 0 || offsets.Any(o => o >= image.Length))
            return null;
        if (offsets.Count == 1)
            return image[offsets[0]..];
        var joined = new List<byte>();
        foreach (var o in offsets)
            for (var p = o; p < image.Length && image[p] != 0xFF; p++)
                joined.Add(image[p]);
        joined.Add(0xFF);
        return [.. joined];
    }

    /// <summary>Sound <paramref name="sound"/> as the player plays it, or null for a number the pack lacks.</summary>
    public AmigaDosoundScore? Score(byte[] image, int sound) =>
        Script(image, sound) is { } script ? AmigaDosoundScore.Parse(script) : null;

    /// <summary>How long sound <paramref name="sound"/> sounds (Σ ticks / 50); 0 when unknown.</summary>
    public double Seconds(byte[] image, int sound) => Score(image, sound)?.DurationSeconds ?? 0;

    /// <summary>
    /// How long the player holds the game for sound <paramref name="sound"/>: each step <see cref="StepSeconds"/>, then
    /// <see cref="CallOverheadSeconds"/> (s130, s153). A key ends it at a step's end
    /// (<see cref="SoundHold.StepEnds"/>). <see cref="SoundHold.None"/> when unknown.
    /// </summary>
    public SoundHold Hold(byte[] image, int sound)
    {
        if (Score(image, sound) is not { Steps.Count: > 0 } score)
            return SoundHold.None;
        var ends = new List<double>();
        var t = 0.0;
        foreach (var step in score.Steps)
            ends.Add(t += StepSeconds(step));
        return new SoundHold(ends, CallOverheadSeconds);
    }

    internal static AmigaDosoundEffects FromDto(P3SoundEffects.Dto dto, string manifestPath, string rootDirectory)
    {
        InvalidDataException Bad(string what) => new($"{manifestPath}: sound_effects: {what}.");
        if (dto.Format != TuneFormats.AmigaDosound || string.IsNullOrEmpty(dto.File))
            throw Bad("unknown format or no file");
        var sounds = new Dictionary<int, IReadOnlyList<int>>();
        foreach (var (key, offsets) in dto.Sounds ?? [])
        {
            if (!int.TryParse(key, NumberStyles.None, CultureInfo.InvariantCulture, out var n) || n < 0)
                throw Bad($"sound number '{key}'");
            if (offsets is not { Count: > 0 })
                throw Bad($"sound {n}: no script");
            sounds[n] = offsets.Select(v =>
                v is not null && v.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                && int.TryParse(v.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var o) && o >= 0
                    ? o
                    : throw Bad($"sound {n}: bad offset '{v}'")).ToList();
        }
        if (dto.StepOverheadSeconds is < 0 || dto.CallOverheadSeconds is < 0 || dto.ChannelOverheadSeconds is < 0)
            throw Bad("negative overhead");
        return new AmigaDosoundEffects(Path.GetFullPath(Path.Combine(rootDirectory, dto.File)), sounds, dto.ClockHz)
        {
            StepOverheadSeconds = dto.StepOverheadSeconds ?? 0,
            CallOverheadSeconds = dto.CallOverheadSeconds ?? 0,
            ChannelOverheadSeconds = dto.ChannelOverheadSeconds ?? 0,
        };
    }
}

/// <summary>
/// How a blocking sound holds the game: the player checks for a key after each step, so a key ends it at the next of
/// <see cref="StepEnds"/> (seconds from its start); <see cref="Tail"/> follows the last step played.
/// </summary>
public sealed record SoundHold(IReadOnlyList<double> StepEnds, double Tail)
{
    public static readonly SoundHold None = new([], 0);

    /// <summary>The whole hold when no key comes.</summary>
    public double Seconds => (StepEnds.Count > 0 ? StepEnds[^1] : 0) + (StepEnds.Count > 0 ? Tail : 0);

    /// <summary>A hold of one uncuttable step of <paramref name="seconds"/>.</summary>
    public static SoundHold Fixed(double seconds) => seconds > 0 ? new([seconds], 0) : None;

    /// <summary>The hold's length when a key comes <paramref name="into"/> seconds after its start.</summary>
    public double CutAt(double into)
    {
        foreach (var end in StepEnds)
            if (end > into)
                return end + Tail;
        return Seconds;
    }
}
