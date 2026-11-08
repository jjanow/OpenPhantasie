using System.Text.Json.Serialization;

namespace OpenPhantasie.Data;

/// <summary>
/// s140: Phantasie II ST's TRANSPORTATION fade (<c>PHANT.PRG 0x260a</c>, FORMAT.md 7.4) [C] static, not traced live
/// `[~]` (s162: I Amiga's h26 <c>+0xe30</c> is the same loop, <see cref="AfterSound"/>): after sound 22, hardware registers 0..15 go black one by one (each followed by <see cref="OutVbl"/> Vsyncs),
/// <see cref="HoldVbl"/> Vsyncs, then each takes its colour of <see cref="Palette"/> back (each followed by
/// <see cref="InVbl"/> Vsyncs), and <c>0xb9e2</c> silences the sound (<see cref="SilencesSound"/>). No key is read.
/// </summary>
/// <param name="Palette">The colours restored, register order (the town palette <c>0x23a62</c>).</param>
public sealed record StTransportFade(IReadOnlyList<string> Palette, double VblHz, int OutVbl, int HoldVbl, int InVbl,
    bool SilencesSound)
{
    /// <summary>
    /// s162: the fade starts only once the sounds before it have ended (I Amiga h26 <c>+0xe30</c>: the list's tone and 22
    /// block in h89's player before the first SetRGB4), not with them (II ST <c>0x260a</c> runs the fade under sound 22).
    /// </summary>
    public bool AfterSound { get; init; }

    /// <summary>s162: the fade is drawn only under AMIGA SCREENS (I Amiga: the DOS path has none), as the pack's other cues.</summary>
    public bool OriginalScreensOnly { get; init; }

    /// <summary>
    /// s163 (I Amiga, filmed): the three <c>DisplayBeep</c>s after the fade (h26 <c>+0xe30</c>) flash register 0 to
    /// <see cref="FlashColour"/> for this many Vsyncs (KS 1.3: colour 0's complement, <c>#FFFFFF</c>, 2–3 film frames, no
    /// sound); the new town is drawn under it. 0: no flash (II ST).
    /// </summary>
    public int FlashVbl { get; init; }

    /// <summary>s163: register 0's colour during the flash (<see cref="FlashVbl"/>).</summary>
    public string FlashColour { get; init; } = "#FFFFFF";

    /// <summary>The flash's end, seconds after the fade began (<see cref="TotalSeconds"/> without a flash).</summary>
    public double EndSeconds => (TotalVbl + FlashVbl) / VblHz;

    /// <summary>s163: whether the DisplayBeep flash is up <paramref name="seconds"/> after the fade began.</summary>
    public bool IsFlash(double seconds) => FlashVbl > 0 && seconds >= TotalSeconds && seconds < EndSeconds;

    /// <summary>Registers faded (16 on the ST).</summary>
    public int Registers => Palette.Count;

    /// <summary>Vsyncs from the first register's blackout to the silence: n·out + hold + n·in (372 on the ST).</summary>
    public int TotalVbl => Registers * OutVbl + HoldVbl + Registers * InVbl;

    /// <summary>The whole fade, seconds (6.2 s at 60 Hz).</summary>
    public double TotalSeconds => TotalVbl / VblHz;

    /// <summary>
    /// Whether register <paramref name="register"/> is black <paramref name="seconds"/> after the fade began: register k
    /// goes black at Vsync k·out and is back at n·out + hold + k·in; from <see cref="TotalSeconds"/> on all are back.
    /// </summary>
    public bool IsBlack(int register, double seconds)
    {
        if ((uint)register >= (uint)Registers || seconds < 0 || seconds >= TotalSeconds)
            return false;
        var vbl = seconds * VblHz + 1e-6; // a whole number of Vsyncs in seconds lands on its Vsync
        var back = Registers * OutVbl + HoldVbl + register * InVbl;
        return vbl >= register * OutVbl && vbl < back;
    }

    internal static StTransportFade FromDto(Dto dto, IReadOnlyDictionary<string, List<string>>? palettes, string manifestPath)
    {
        if (dto.Palette is null || palettes?.GetValueOrDefault(dto.Palette) is not { Count: > 0 } colours)
            throw new InvalidDataException($"{manifestPath}: transport_fade: palette '{dto.Palette}' not found.");
        if (dto.VblHz <= 0 || dto.OutVbl < 0 || dto.HoldVbl < 0 || dto.InVbl < 0 || dto.FlashVbl < 0)
            throw new InvalidDataException($"{manifestPath}: transport_fade: bad timing.");
        return new StTransportFade(colours, dto.VblHz, dto.OutVbl, dto.HoldVbl, dto.InVbl, dto.SilencesSound)
        {
            AfterSound = dto.AfterSound,
            OriginalScreensOnly = dto.OriginalScreensOnly,
            FlashVbl = dto.FlashVbl,
            FlashColour = dto.FlashColour ?? "#FFFFFF",
        };
    }

    public sealed class Dto
    {
        [JsonPropertyName("_note")] public string? Note { get; set; }
        public string? Palette { get; set; }
        [JsonPropertyName("vbl_hz")] public double VblHz { get; set; }
        [JsonPropertyName("out_vbl")] public int OutVbl { get; set; }
        [JsonPropertyName("hold_vbl")] public int HoldVbl { get; set; }
        [JsonPropertyName("in_vbl")] public int InVbl { get; set; }
        [JsonPropertyName("silences_sound")] public bool SilencesSound { get; set; }
        [JsonPropertyName("after_sound")] public bool AfterSound { get; set; }
        [JsonPropertyName("original_screens_only")] public bool OriginalScreensOnly { get; set; }
        [JsonPropertyName("flash_vbl")] public int FlashVbl { get; set; }
        [JsonPropertyName("flash_colour")] public string? FlashColour { get; set; }
    }
}
