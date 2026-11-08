using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace OpenPhantasie.Data;

/// <summary>
/// Manifest <c>ending_scene</c>: Phantasie I Amiga's victory screen (hunk 32 <c>+0x6d0</c>, s127 static; Amiga
/// <c>FORMAT.md</c> "Defeat and ending pictures"). The picture (<see cref="PictureNames.EndingFinal"/>) is blitted on a
/// black screen, then the pens of <see cref="Palette"/> are set one at a time, <see cref="FadeVbl"/> VBLs apart
/// (<c>h77 +0x288</c>, WaitTOF at <see cref="VblHz"/>); a <c>Delay</c> of <see cref="HoldTicks"/> (1/50 s); the ghost
/// (<see cref="Ghost"/>) in each OK member's slot of the portrait row (top <see cref="RowTop"/>); Zeus' raised arm
/// (<see cref="Arm"/> at <see cref="ArmX"/>, <see cref="ArmY"/>); per OK member its portrait over its slot, then
/// <see cref="GrantTicks"/>; the speech, one line at a time centred on row <see cref="TextY"/> in pen
/// <see cref="TextPen"/> (Amiga's own line breaks: <see cref="SpeechKeys"/>), each up to <see cref="LineTicks"/> polls of
/// <see cref="LinePollSeconds"/> or a key (<c>h62 +0x576</c>); then the pens back to black from the last,
/// <see cref="FadeVbl"/> apart. s130 live: the <see cref="Disk"/> waits and keys through the input port
/// (<see cref="QueuedKeys"/>).
/// <para>Phantasie II ST (<c>0x12f2a</c>, s128; <c>atari-st/phantasie2/logic-specs/endgame.md</c> §7.1) uses the same
/// screen with the optional parts: no ghost or arm; an <see cref="IntroPage"/> before the picture and a
/// <see cref="ClosingPage"/> after the speech (each until a key); header lines before the hold; per member a title on the
/// bottom line while its portrait stands on the <see cref="StageX"/>, <see cref="StageY"/> spot, then the
/// <see cref="Crowd"/> cheering; the row filled by record (<see cref="RowByRecord"/>); bottom lines justified to
/// <see cref="JustifyWidth"/>; waits in AES milliseconds (<see cref="TickHz"/> 1000).</para>
/// </summary>
public sealed record EndingSceneLayout(
    IReadOnlyList<string> Palette, double VblHz, int FadeVbl, int HoldTicks, string? Ghost, string? Arm, int ArmX, int ArmY,
    int RowTop, int SlotSize, int GrantTicks, int TextY, int TextPen, int LineTicks)
{
    /// <summary>The sounds <c>h89 +0xee(n)</c> starts on the way (s128), or null for a silent screen without their waits.</summary>
    public EndingSceneSounds? Sounds { get; init; }

    private static readonly Regex HexColour = new("^#[0-9A-Fa-f]{6}$");

    /// <summary>Ticks a second for <see cref="HoldTicks"/>, <see cref="GrantTicks"/>, <see cref="LineTicks"/> and the
    /// crowd's flash (Amiga <c>Delay</c>: 50; ST AES timers: 1000).</summary>
    public double TickHz { get; init; } = 50;

    /// <summary>The spot each member's portrait is shown on while presented (II: Filmon's door, slot rect 6), or null.</summary>
    public int? StageX { get; init; }
    public int? StageY { get; init; }

    /// <summary>The crowd cheering after each presentation (II: <c>crowd.pic</c>), or null.</summary>
    public EndingCrowd? Crowd { get; init; }

    /// <summary>The row's portraits go to records 0…k−1 (II's loop over records) instead of the granted members (I).</summary>
    public bool RowByRecord { get; init; }

    /// <summary>Bottom lines stretched to this width, centred (II: TOS <c>v_justified</c> 310); 0 = centred single-spaced (I).</summary>
    public int JustifyWidth { get; init; }

    /// <summary>
    /// The length of one line poll (I Amiga <c>h62 +0x576</c>: one <c>Delay(1)</c> and a port check a turn, 21.87 ms
    /// live, s130 `[C]`), seconds; a line lasts <see cref="LineTicks"/> polls. Null = one tick of <see cref="TickHz"/>.
    /// </summary>
    public double? LinePollSeconds { get; init; }

    /// <summary>
    /// I Amiga's input port (s130 `[C]`): key presses and releases queue while nothing reads them; each sound drains the
    /// queue and a press or release during a sound ends it at a step; a speech line takes one event from the queue. False
    /// (II): a key ends only the line or page waiting then, other keys are dropped.
    /// </summary>
    public bool QueuedKeys { get; init; }

    /// <summary>VBLs before each blit of the row and the arm (I Amiga's h77 <c>+0xcfe</c>: one <c>WaitTOF</c>, s153 live:
    /// the ghosts one a frame); 0 = drawn at once (II).</summary>
    public int BlitVbl { get; init; }

    /// <summary>The disk waits on the way (I Amiga, s130 live), or null.</summary>
    public EndingDiskWaits? Disk { get; init; }

    /// <summary>String keys of the speech as this version prints it (I Amiga's own line breaks), or null for Core's lines.</summary>
    public IReadOnlyList<string>? SpeechKeys { get; init; }

    /// <summary>
    /// The font of the speech line (I Amiga: <c>h80 +0x84a</c> prints on the window's RastPort, whose font the boot code
    /// sets to z6font, s153), or null for the pack's own font. <see cref="FontFile"/> is its file next to the manifest.
    /// </summary>
    public string? Font { get; init; }
    public string? FontFile { get; init; }

    /// <summary>The page before the picture / after the speech (II), or null.</summary>
    public EndingPage? IntroPage { get; init; }
    public EndingPage? ClosingPage { get; init; }

    internal static EndingSceneLayout FromDto(Dto dto, int? packVblHz, string manifestPath)
    {
        InvalidDataException Bad(string what) => new($"{manifestPath}: ending_scene: {what}.");
        if (dto.Palette is not { Count: 16 } palette || palette.Any(c => !HexColour.IsMatch(c ?? "")))
            throw Bad("palette must be 16 #RRGGBB colours");
        if (palette.Distinct(StringComparer.OrdinalIgnoreCase).Count() != 16)
            throw Bad("palette colours must differ (pens are found by colour)");
        if (dto.Ghost is { } g && string.IsNullOrWhiteSpace(g) || dto.Arm is { } a && string.IsNullOrWhiteSpace(a))
            throw Bad("empty ghost or arm picture name");
        var hz = dto.VblHz ?? packVblHz;
        if (hz is not > 0 || dto.FadeVbl <= 0 || dto.HoldTicks < 0 || dto.GrantTicks < 0 || dto.LineTicks <= 0 ||
            dto.SlotSize <= 0 || dto.TextPen is < 0 or > 15 || dto.TickHz is <= 0 || dto.JustifyWidth < 0 ||
            (dto.Stage is not null && dto.Stage.Count != 2) || dto.LinePollSeconds is <= 0 || dto.BlitVbl < 0 ||
            dto.Speech is { } sk && sk.Any(string.IsNullOrWhiteSpace))
            throw Bad("bad numbers");
        EndingSceneSounds? sounds = null;
        if (dto.Sounds is { } sd)
        {
            if (sd.BeforeBlack < 0 || sd.OnBlack < 0 || sd.BeforeArm < 0 || sd.AfterPortrait < 0
                || sd.AfterSpeech is null || sd.AfterSpeech.Any(n => n < 0))
                throw Bad("bad sound numbers");
            sounds = new EndingSceneSounds(sd.BeforeBlack, sd.OnBlack, sd.BeforeArm, sd.AfterPortrait, sd.AfterSpeech);
        }
        return new EndingSceneLayout(palette!, hz.Value, dto.FadeVbl, dto.HoldTicks, dto.Ghost, dto.Arm, dto.ArmX,
            dto.ArmY, dto.RowTop, dto.SlotSize, dto.GrantTicks, dto.TextY, dto.TextPen, dto.LineTicks)
        {
            Sounds = sounds,
            TickHz = dto.TickHz ?? 50,
            StageX = dto.Stage?[0],
            StageY = dto.Stage?[1],
            Crowd = dto.Crowd is { } c ? EndingCrowd.FromDto(c, Bad) : null,
            RowByRecord = dto.RowByRecord,
            LinePollSeconds = dto.LinePollSeconds,
            QueuedKeys = dto.QueuedKeys,
            BlitVbl = dto.BlitVbl,
            Disk = dto.Disk is { } dk ? EndingDiskWaits.FromDto(dk, Bad) : null,
            SpeechKeys = dto.Speech is { } speech ? [.. speech!] : null,
            JustifyWidth = dto.JustifyWidth,
            Font = dto.Font,
            FontFile = CombatFieldLayout.FontFileFor(dto.Font, manifestPath),
            IntroPage = dto.IntroPage is { } ip ? EndingPage.FromDto(ip, Bad, manifestPath) : null,
            ClosingPage = dto.ClosingPage is { } cp ? EndingPage.FromDto(cp, Bad, manifestPath) : null,
        };
    }

    internal sealed class Dto
    {
        public List<string?>? Palette { get; set; }
        [JsonPropertyName("vbl_hz")] public int? VblHz { get; set; }
        [JsonPropertyName("fade_vbl")] public int FadeVbl { get; set; }
        [JsonPropertyName("hold_ticks")] public int HoldTicks { get; set; }
        public string? Ghost { get; set; }
        public string? Arm { get; set; }
        [JsonPropertyName("arm_x")] public int ArmX { get; set; }
        [JsonPropertyName("arm_y")] public int ArmY { get; set; }
        [JsonPropertyName("row_top")] public int RowTop { get; set; }
        [JsonPropertyName("slot_size")] public int SlotSize { get; set; }
        [JsonPropertyName("grant_ticks")] public int GrantTicks { get; set; }
        [JsonPropertyName("text_y")] public int TextY { get; set; }
        [JsonPropertyName("text_pen")] public int TextPen { get; set; }
        [JsonPropertyName("line_ticks")] public int LineTicks { get; set; }
        [JsonPropertyName("tick_hz")] public double? TickHz { get; set; }
        public List<int>? Stage { get; set; }
        public EndingCrowd.Dto? Crowd { get; set; }
        [JsonPropertyName("row_by_record")] public bool RowByRecord { get; set; }
        [JsonPropertyName("justify_width")] public int JustifyWidth { get; set; }
        [JsonPropertyName("intro_page")] public EndingPage.Dto? IntroPage { get; set; }
        [JsonPropertyName("closing_page")] public EndingPage.Dto? ClosingPage { get; set; }
        public SoundsDto? Sounds { get; set; }
        [JsonPropertyName("line_poll_seconds")] public double? LinePollSeconds { get; set; }
        [JsonPropertyName("queued_keys")] public bool QueuedKeys { get; set; }
        [JsonPropertyName("blit_vbl")] public int BlitVbl { get; set; }
        public EndingDiskWaits.Dto? Disk { get; set; }
        public List<string?>? Speech { get; set; }
        public string? Font { get; set; }
    }

    internal sealed class SoundsDto
    {
        [JsonPropertyName("before_black")] public int BeforeBlack { get; set; }
        [JsonPropertyName("on_black")] public int OnBlack { get; set; }
        [JsonPropertyName("before_arm")] public int BeforeArm { get; set; }
        [JsonPropertyName("after_portrait")] public int AfterPortrait { get; set; }
        [JsonPropertyName("after_speech")] public List<int>? AfterSpeech { get; set; }
    }
}

/// <summary>
/// The crowd cheering on II's ending (<c>crowd.pic</c>, <c>0x131a0</c>): <see cref="Flashes"/> blits of
/// <see cref="Frames"/>[0], [1], [0], … at (<see cref="X"/>, <see cref="Y"/>), <see cref="FlashTicks"/> apart, then the
/// last frame stays. <see cref="Sound"/>: the sound started with the cheer (II: <c>0xb80e(32)</c>, s135; the pack's
/// <c>sound_effects</c>), or null; it does not hold the screen (Dosound returns at once, the frames run over it).
/// </summary>
public sealed record EndingCrowd(IReadOnlyList<string> Frames, int X, int Y, int Flashes, int FlashTicks)
{
    public int? Sound { get; init; }

    internal static EndingCrowd FromDto(Dto dto, Func<string, InvalidDataException> bad)
    {
        if (dto.Frames is not { Count: >= 2 } frames || frames.Any(string.IsNullOrWhiteSpace) || dto.Flashes <= 0 ||
            dto.FlashTicks <= 0)
            throw bad("crowd: two or more frames, flashes and flash_ticks > 0");
        if (dto.Sound is < 0)
            throw bad("crowd: sound >= 0");
        return new EndingCrowd([.. frames!], dto.X, dto.Y, dto.Flashes, dto.FlashTicks) { Sound = dto.Sound };
    }

    internal sealed class Dto
    {
        public List<string>? Frames { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public int Flashes { get; set; }
        [JsonPropertyName("flash_ticks")] public int FlashTicks { get; set; }
        public int? Sound { get; set; }
    }
}

/// <summary>
/// A text page of II's ending (TOS <c>v_gtext</c> lines on a black screen, then the OK form until a key): line i at
/// (<see cref="X"/>, <see cref="Y"/> + i · <see cref="LineGap"/>) in <see cref="TextPen"/>, the OK button in
/// <see cref="ButtonPen"/>, both pens of <see cref="Palette"/> (null = the ending's palette, so the fade-out dims it).
/// </summary>
public sealed record EndingPage(IReadOnlyList<string>? Palette, int X, int Y, int LineGap, int TextPen, int ButtonPen)
{
    /// <summary>The sound started when OK closes the page (II: <c>0xb7e6</c>'s click after the form, s135), or null; it
    /// does not hold the screen.</summary>
    public int? Sound { get; init; }

    /// <summary>
    /// s160: the page's font (II ST: <c>v_gtext</c> in the 8×8 system font, italic: EmuTOS's, the pack's
    /// <c>fonts/&lt;id&gt;.json</c>), or null for the pack's own; <see cref="FontFile"/> is its file next to the manifest.
    /// </summary>
    public string? Font { get; init; }
    public string? FontFile { get; init; }

    /// <summary>
    /// s160: pixels the font's cell starts right of <see cref="X"/> (negative = left). II ST: top-aligned italic text
    /// starts left_offset + right_offset = 4 px left of the <c>v_gtext</c> point (EmuTOS <c>vdi_text.c</c>, vertical
    /// alignment 5), so the slanted top row sits at x; matches the s80 page shots pixel for pixel.
    /// </summary>
    public int FontDx { get; init; }

    /// <summary>
    /// s162: the OK button label's font (II ST: the form's G_BOXTEXT has <c>te_font</c> 5, the 6×6 small system font:
    /// EmuTOS's, the pack's <c>fonts/&lt;id&gt;.json</c>), or null for the pack's own; <see cref="ButtonFontFile"/> is its
    /// file.
    /// </summary>
    public string? ButtonFont { get; init; }
    public string? ButtonFontFile { get; init; }

    internal static EndingPage FromDto(Dto dto, Func<string, InvalidDataException> bad, string manifestPath)
    {
        if (dto.Palette is { } p && (p.Count != 16 || p.Any(c => !HexColourOk(c))) || dto.LineGap <= 0 ||
            dto.TextPen is < 0 or > 15 || dto.ButtonPen is < 0 or > 15 || dto.Sound is < 0)
            throw bad("page: 16 #RRGGBB colours, line_gap > 0, pens 0..15, sound >= 0");
        return new EndingPage(dto.Palette is { } pal ? [.. pal!] : null, dto.X, dto.Y, dto.LineGap, dto.TextPen, dto.ButtonPen)
        {
            Sound = dto.Sound,
            Font = dto.Font,
            FontFile = CombatFieldLayout.FontFileFor(dto.Font, manifestPath),
            FontDx = dto.FontDx,
            ButtonFont = dto.ButtonFont,
            ButtonFontFile = CombatFieldLayout.FontFileFor(dto.ButtonFont, manifestPath),
        };
    }

    private static bool HexColourOk(string? c) => c is { Length: 7 } && c[0] == '#' && c[1..].All(Uri.IsHexDigit);

    internal sealed class Dto
    {
        public List<string?>? Palette { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        [JsonPropertyName("line_gap")] public int LineGap { get; set; }
        [JsonPropertyName("text_pen")] public int TextPen { get; set; }
        [JsonPropertyName("button_pen")] public int ButtonPen { get; set; }
        public int? Sound { get; set; }
        public string? Font { get; set; }
        [JsonPropertyName("font_dx")] public int FontDx { get; set; }
        [JsonPropertyName("button_font")] public string? ButtonFont { get; set; }
    }
}

/// <summary>
/// The victory screen's sounds (h32 <c>+0x6d0</c>, s128): <see cref="BeforeBlack"/> before the black table,
/// <see cref="OnBlack"/> once <c>final.pic</c> is blitted (still black), <see cref="BeforeArm"/> between the ghosts and
/// Zeus' arm, <see cref="AfterPortrait"/> after each granted member's portrait (before its <c>Delay</c>),
/// <see cref="AfterSpeech"/> between the last line and the fade-out. Each holds the screen while it plays (h89
/// <c>+0x304</c> blocks).
/// </summary>
public sealed record EndingSceneSounds(int BeforeBlack, int OnBlack, int BeforeArm, int AfterPortrait, IReadOnlyList<int> AfterSpeech);

/// <summary>
/// The victory screen's disk waits (I Amiga, s130 live `[~]`: they depend on the head position): <see cref="PictureRead"/>
/// (<c>final.pic</c>, black screen) after the first sound; <see cref="Save"/> (<c>guild.dat</c> written) after the first
/// <see cref="SaveAfterSounds"/> sounds past the speech; <see cref="TownLoad"/> (black, the town reloaded) after the
/// fade-out. Seconds.
/// </summary>
public sealed record EndingDiskWaits(double PictureRead, double Save, int SaveAfterSounds, double TownLoad)
{
    internal static EndingDiskWaits FromDto(Dto dto, Func<string, InvalidDataException> bad)
    {
        if (dto.PictureRead < 0 || dto.Save < 0 || dto.SaveAfterSounds < 0 || dto.TownLoad < 0)
            throw bad("disk: negative wait");
        return new EndingDiskWaits(dto.PictureRead, dto.Save, dto.SaveAfterSounds, dto.TownLoad);
    }

    internal sealed class Dto
    {
        [JsonPropertyName("picture_read")] public double PictureRead { get; set; }
        public double Save { get; set; }
        [JsonPropertyName("save_after_sounds")] public int SaveAfterSounds { get; set; }
        [JsonPropertyName("town_load")] public double TownLoad { get; set; }
    }
}
