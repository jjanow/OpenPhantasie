namespace OpenPhantasie.Data.Audio;

/// <summary>
/// A decoded tune of one of the originals (the title tunes, s108): what the original's player sends to its sound chip,
/// over time, with no sound rendered yet. Headless: the Client turns a score into PCM with a model of the chip
/// (<c>OpenPhantasie.Client.Audio</c>). Each kind keeps the timing unit its player used (50 Hz system ticks, VBLs).
/// Formats and players: <c>reverse-engineering/amiga/FORMAT.md</c> "Title tune",
/// <c>reverse-engineering/atari-st/phantasie2/FORMAT.md</c> §7.1, <c>reverse-engineering/amiga/phantasie3/FORMAT.md</c> §7.1,
/// <c>reverse-engineering/x68000/phantasie4/FORMAT.md</c> §6.11.
/// </summary>
public abstract class TuneScore
{
    /// <summary>How long the player runs, start to its end marker, in seconds.</summary>
    public abstract double DurationSeconds { get; }
}

/// <summary>The tune formats a pack's <c>title_sequence.tune.format</c> may name.</summary>
public static class TuneFormats
{
    /// <summary>Phantasie I Amiga <c>tamb.dat</c>: an ST Dosound script read by the Amiga player h89 <c>+0x304</c>.</summary>
    public const string AmigaDosound = "amiga-dosound";

    /// <summary>An Atari ST XBIOS 32 Dosound script (Phantasie II <c>START.PRG</c>), played by TOS on the YM2149.</summary>
    public const string AtariStDosound = "atari-st-dosound";

    /// <summary>Phantasie III Amiga <c>D/m0</c> / <c>D/m1</c>: three voice streams for the VBL driver <c>0x2192</c>.</summary>
    public const string AmigaP3Music = "amiga-p3-music";

    /// <summary>
    /// Phantasie IV X68000: MML strings in <c>P4.X</c> for Sharp's OPMDRV.X, with the FM voices of <c>P4.SND</c>
    /// (<see cref="OpmDrvScore"/>; the tune's <c>voices</c> and <c>tracks</c>).
    /// </summary>
    public const string X68kOpmDrv = "x68k-opmdrv";

    public static readonly IReadOnlyList<string> All = [AmigaDosound, AtariStDosound, AmigaP3Music, X68kOpmDrv];
}
