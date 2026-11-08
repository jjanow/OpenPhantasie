using OpenPhantasie.Data.Audio;

namespace OpenPhantasie.Client.Audio;

/// <summary>
/// Phantasie III Amiga's sound effects (s110, <see cref="P3SoundEffects"/>): a run of sounds started at known VBLs is
/// rendered as one driver score (each start cuts the one before, as <c>0x1f52</c> does) and streamed through a
/// <see cref="TunePlayer"/>. One run plays at a time: a new one, or <see cref="Stop"/>, ends the old. Silent with no sound
/// device, no program file, or no packed sound.
/// </summary>
public sealed class SoundEffectPlayer : IDisposable
{
    private TunePlayer? _player;
    private object? _imageFor;
    private byte[]? _image;

    /// <summary>
    /// s160: the Amiga sounds (I, III; on Paula) through the A500's output filters, the game's AMIGA AUDIO FILTER switch
    /// (<see cref="PaulaMixer"/>), asked when a run starts (a running one keeps its own). Null or true = on, the default.
    /// </summary>
    public Func<bool>? A500Filter { get; set; }

    private bool FilterOn => A500Filter?.Invoke() ?? true;

    /// <summary>
    /// s163, a developer aid for live checks: with <c>OPENPHANTASIE_SOUND_TRACE</c> set to a file, every sound started is
    /// appended to it as "ms kind number" (Unix ms, comparable with screenshot times), so a run on Xvfb can be timed
    /// without listening.
    /// </summary>
    private static readonly string? TracePath = Environment.GetEnvironmentVariable("OPENPHANTASIE_SOUND_TRACE");

    private void Trace(string kind, int sound)
    {
        if (string.IsNullOrEmpty(TracePath))
            return;
        try
        {
            File.AppendAllText(TracePath, $"{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()} {kind} {sound}\n");
        }
        catch (IOException)
        {
        }
    }

    /// <summary>True while a run still sounds.</summary>
    public bool Playing => _player?.Playing == true;

    /// <summary>The renderer for <paramref name="plays"/> (VBL, sound number) on <paramref name="effects"/>, or null.</summary>
    public static P3MusicRenderer? Render(P3SoundEffects effects, byte[] image, IEnumerable<(int Frame, int Sound)> plays,
        int sampleRate = TuneRenderer.DefaultSampleRate, bool a500Filter = true)
    {
        ArgumentNullException.ThrowIfNull(effects);
        return effects.Score(image, plays) is { } score
            ? new P3MusicRenderer(score, effects.ClockHz ?? TuneRenderer.PaulaNtscHz, sampleRate, a500Filter)
            : null;
    }

    /// <summary>Start <paramref name="plays"/> now (VBL 0 = this call), ending whatever ran.</summary>
    public void Play(P3SoundEffects? effects, IEnumerable<(int Frame, int Sound)> plays)
    {
        Stop();
        if (effects is null)
            return;
        try
        {
            if (!ReferenceEquals(_imageFor, effects))
            {
                _image = effects.TryLoadImage();
                _imageFor = effects;
            }
            if (_image is not null && Render(effects, _image, plays, a500Filter: FilterOn) is { } renderer)
                _player = TunePlayer.TryStart(renderer);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
        {
        }
    }

    /// <summary>
    /// s136: the game's sounds started in one go (<see cref="OpenPhantasie.Core.GameSession.SoundsSince"/>) as one run, each
    /// start after the one before has ended. The Amiga shows each of these lines in its own window and waits for a key
    /// between them, so no sound cuts the one before; the remaster shows the lines at once, so it plays them in turn.
    /// A sound the pack lacks or that does not end takes no time.
    /// </summary>
    public static IReadOnlyList<(int Frame, int Sound)> InTurn(P3SoundEffects effects, byte[] image, IEnumerable<int> sounds)
    {
        ArgumentNullException.ThrowIfNull(effects);
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(sounds);
        var plays = new List<(int, int)>();
        var at = 0;
        foreach (var sound in sounds)
        {
            if (!effects.Sounds.ContainsKey(sound))
                continue;
            plays.Add((at, sound));
            if (effects.Score(image, [(0, sound)]) is { Loops: false } score)
                at += score.Frames;
        }
        return plays;
    }

    /// <summary>Start <paramref name="sounds"/> in turn now (<see cref="InTurn"/>), ending whatever ran.</summary>
    public void PlayInTurn(P3SoundEffects? effects, IReadOnlyList<int> sounds)
    {
        if (effects is null || sounds.Count == 0)
            return;
        try
        {
            if (!ReferenceEquals(_imageFor, effects))
            {
                _image = effects.TryLoadImage();
                _imageFor = effects;
            }
            if (_image is not null)
                Play(effects, InTurn(effects, _image, sounds));
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
        {
        }
    }

    /// <summary>
    /// s128, Phantasie I Amiga: start sound <paramref name="sound"/> (h89 <c>+0xee</c>) now, ending whatever ran: its
    /// Dosound script through the title tune's renderer (<see cref="AmigaDosoundRenderer"/>).
    /// </summary>
    public void Play(AmigaDosoundEffects? effects, int sound)
    {
        Stop();
        if (effects is null)
            return;
        try
        {
            if (!ReferenceEquals(_imageFor, effects))
            {
                _image = effects.TryLoadImage();
                _imageFor = effects;
            }
            if (_image is not null && Render(effects, _image, sound, a500Filter: FilterOn) is { } renderer)
                _player = TunePlayer.TryStart(renderer);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
        {
        }
    }

    /// <summary>
    /// s154, Phantasie I Amiga: a raw Paula tone now (h89 <c>+0x7ca</c>; <see cref="AmigaDosoundScore.Tone"/>) for
    /// <paramref name="seconds"/>, ending whatever ran; <see cref="Stop"/> ends it at once (<c>+0x7f4</c>).
    /// </summary>
    public void PlayTone(AmigaDosoundEffects? effects, int channel, int period, int volume, double seconds)
    {
        Stop();
        var ticks = (int)Math.Round(seconds * AmigaDosoundScore.TickHz);
        if (effects is null || AmigaDosoundScore.Tone(channel, period, volume, ticks) is not { Steps.Count: > 0 } score)
            return;
        _player = TunePlayer.TryStart(new AmigaDosoundRenderer(score, effects.ClockHz ?? TuneRenderer.PaulaNtscHz,
            TuneRenderer.DefaultSampleRate, a500Filter: FilterOn));
    }

    /// <summary>The renderer for I's sound <paramref name="sound"/> (each step its ticks plus the player's step overhead),
    /// or null when the pack lacks it or it is silent.</summary>
    public static AmigaDosoundRenderer? Render(AmigaDosoundEffects effects, byte[] image, int sound,
        int sampleRate = TuneRenderer.DefaultSampleRate, bool a500Filter = true)
    {
        ArgumentNullException.ThrowIfNull(effects);
        return effects.Score(image, sound) is { Steps.Count: > 0 } score
            ? new AmigaDosoundRenderer(score, effects.ClockHz ?? TuneRenderer.PaulaNtscHz, sampleRate,
                effects.StepOverheadSeconds, effects.ChannelOverheadSeconds, a500Filter) // s130 / s153: each step sounds as long as the player holds it
            : null;
    }

    /// <summary>
    /// s135, Phantasie II ST: start sound <paramref name="sound"/> (<c>0xb80e</c>) now, ending whatever ran (the ST silences
    /// the chip first, <c>0xba02</c>): its Dosound script on the YM2149 (<see cref="DosoundRenderer"/>, as the title tune).
    /// </summary>
    public void Play(StDosoundEffects? effects, int sound)
    {
        Trace("st", sound);
        Stop();
        if (effects is null)
            return;
        try
        {
            if (!ReferenceEquals(_imageFor, effects))
            {
                _image = effects.TryLoadImage();
                _imageFor = effects;
            }
            if (_image is not null && Render(effects, _image, sound) is { } renderer)
                _player = TunePlayer.TryStart(renderer);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
        {
        }
    }

    /// <summary>The renderer for II's sound <paramref name="sound"/>, or null when the pack lacks it or it is silent.</summary>
    public static DosoundRenderer? Render(StDosoundEffects effects, byte[] image, int sound,
        int sampleRate = TuneRenderer.DefaultSampleRate)
    {
        ArgumentNullException.ThrowIfNull(effects);
        return effects.Score(image, sound) is { Ticks: > 0 } score
            ? new DosoundRenderer(score, effects.ClockHz ?? TuneRenderer.StPsgHz, sampleRate)
            : null;
    }

    /// <summary>
    /// s152, Phantasie IV X68000: start ADPCM effects <paramref name="effects"/> in turn now (<see cref="AdpcmRenderer"/>),
    /// ending whatever ran. Silent when the pack's <c>VDATA</c> is missing or no number decodes.
    /// </summary>
    public void Play(X68kAdpcmEffects? pack, IReadOnlyList<int> effects)
    {
        Stop();
        if (pack is null || effects.Count == 0)
            return;
        try
        {
            if (!ReferenceEquals(_imageFor, pack))
            {
                _image = pack.TryLoadImage();
                _imageFor = pack;
            }
            if (_image is not null && AdpcmRenderer.For(pack, _image, effects) is { } renderer)
                _player = TunePlayer.TryStart(renderer);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
        {
        }
    }

    /// <summary>Keep the stream fed (once a frame).</summary>
    public void Update() => _player?.Update();

    public void Stop()
    {
        _player?.Dispose();
        _player = null;
    }

    public void Dispose() => Stop();
}
