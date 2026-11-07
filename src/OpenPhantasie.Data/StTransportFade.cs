using System.Text.Json.Serialization;

namespace OpenPhantasie.Data;

/// <summary>
/// s140: Phantasie II ST's TRANSPORTATION fade (<c>PHANT.PRG 0x260a</c>, FORMAT.md 7.4) [C] static, not traced live
/// `[~]`: after sound 22, hardware registers 0..15 go black one by one (each followed by <see cref="OutVbl"/> Vsyncs),
/// <see cref="HoldVbl"/> Vsyncs, then each takes its colour of <see cref="Palette"/> back (each followed by
/// <see cref="InVbl"/> Vsyncs), and <c>0xb9e2</c> silences the sound (<see cref="SilencesSound"/>). No key is read.
/// </summary>
/// <param name="Palette">The colours restored, register order (the town palette <c>0x23a62</c>).</param>
public sealed record StTransportFade(IReadOnlyList<string> Palette, double VblHz, int OutVbl, int HoldVbl, int InVbl,
    bool SilencesSound)
{
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
        if (dto.VblHz <= 0 || dto.OutVbl < 0 || dto.HoldVbl < 0 || dto.InVbl < 0)
            throw new InvalidDataException($"{manifestPath}: transport_fade: bad timing.");
        return new StTransportFade(colours, dto.VblHz, dto.OutVbl, dto.HoldVbl, dto.InVbl, dto.SilencesSound);
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
    }
}
