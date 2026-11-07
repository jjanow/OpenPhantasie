using System.Text.Json.Serialization;

namespace OpenPhantasie.Data;

/// <summary>
/// Manifest <c>defeat_screen</c>: Phantasie I Amiga's party-defeat screens, which run by themselves (hunk 32 <c>+0</c> /
/// <c>+0x55c</c>, s153 static and live; <c>reverse-engineering/amiga/FORMAT.md</c> "The party defeat"):
/// <list type="number">
/// <item>The screen black while <c>decor.pic</c> loads (<see cref="PictureRead"/>, disk `[~]`), then the picture, the
/// closed door and the combat palette at once.</item>
/// <item>Each text line (<c>+0x340</c>: centred on row <see cref="TextY"/> in pen <see cref="TextPen"/>) waits
/// <see cref="LinePolls"/> polls of <see cref="PollSeconds"/> or a key or click (<c>h62 +0x576</c>).</item>
/// <item>"YOUR PARTY HAS BEEN DEFEATED"; the ghost in every slot of the row (all members, one blit a frame);
/// <see cref="GhostSound"/>; "YOU HAVE ARRIVED AT THE ASTRAL PLANE"; the door sinks (<see cref="DoorSeconds"/>, a raw
/// Paula tone under it); <c>Delay(</c><see cref="AfterDoorTicks"/><c>)</c>; <see cref="DemonSound"/>; the Great Demon's
/// face; "THE GREAT DEMON APPEARS AND SPEAKS:".</item>
/// <item>Per member: <see cref="FateSound"/>, then its fate (portrait or cleared slot) and its line.</item>
/// <item>A last wait of <see cref="EndPolls"/> polls or a key.</item>
/// </list>
/// The sounds hold the screen (h89 <c>+0x304</c> blocks) and drain the input port; keys queue as on the victory screen.
/// </summary>
public sealed record DefeatScreenLayout(double PictureRead, int LinePolls, double PollSeconds, int EndPolls, double DoorSeconds,
    int AfterDoorTicks, int GhostSound, int DemonSound, int FateSound, int TextY, int TextPen)
{
    /// <summary>VBLs before each blit of the row (one <c>WaitTOF</c>, as the victory screen).</summary>
    public int BlitVbl { get; init; } = 1;

    /// <summary>The machine's VBL rate (NTSC 60).</summary>
    public double VblHz { get; init; } = 60;

    /// <summary>Before the door's first step: h89 <c>+0x7ca</c> starts the raw tone (live 0.15 s `[~]`).</summary>
    public double DoorLeadSeconds { get; init; }

    /// <summary>After the door's last step: h89 <c>+0x7f4</c> stops the tone (live 0.17 s `[~]`).</summary>
    public double DoorTailSeconds { get; init; }

    /// <summary>
    /// s154: the text's font (h80 <c>+0x84a</c>, the window's z6font, as the victory speech), or null for the pack's own;
    /// <see cref="FontFile"/> is its file next to the manifest.
    /// </summary>
    public string? Font { get; init; }

    public string? FontFile { get; init; }

    /// <summary>
    /// s154: the raw tone under the door (h89 <c>+0x7ca</c>, static `[C]`): the effects' 16-byte ramp on channel
    /// <see cref="ToneChannel"/> at Paula period <see cref="TonePeriod"/> (not scaled), volume <see cref="ToneVolume"/>,
    /// looping until h89 <c>+0x7f4</c> stops it at once; started after line 2's wait (before the lead), stopped after the
    /// door's last step. Null period: no tone.
    /// </summary>
    public int? TonePeriod { get; init; }

    public int ToneChannel { get; init; }

    public int ToneVolume { get; init; }

    /// <summary>
    /// s154: h32 <c>+0x63c</c> counts the defeats of the program run (h33 <c>+0</c>, DATA, never saved or reset) and on the
    /// <see cref="SignDefeat"/>th exactly blits the sign (h69 <c>+0x32d4</c>) at (<see cref="SignX"/>, <see cref="SignY"/>)
    /// after the door and waits <see cref="SignPolls"/> polls or a key (h62 <c>+0x576</c>) before the <c>Delay</c>; the face
    /// covers it. Null: no sign.
    /// </summary>
    public int? SignDefeat { get; init; }

    public int SignX { get; init; }

    public int SignY { get; init; }

    public int SignPolls { get; init; }

    internal static DefeatScreenLayout FromDto(Dto dto, string manifestPath)
    {
        if (dto.PictureRead < 0 || dto.LinePolls <= 0 || dto.PollSeconds <= 0 || dto.EndPolls < 0 || dto.DoorSeconds < 0
            || dto.AfterDoorTicks < 0 || dto.BlitVbl < 0 || dto.DoorLeadSeconds < 0 || dto.DoorTailSeconds < 0
            || dto.VblHz is <= 0 || dto.TextPen is < 0 or > 15)
            throw new InvalidDataException($"{manifestPath}: defeat_screen: bad numbers.");
        return new DefeatScreenLayout(dto.PictureRead, dto.LinePolls, dto.PollSeconds, dto.EndPolls, dto.DoorSeconds,
            dto.AfterDoorTicks, dto.GhostSound, dto.DemonSound, dto.FateSound, dto.TextY, dto.TextPen)
        {
            BlitVbl = dto.BlitVbl,
            VblHz = dto.VblHz ?? 60,
            DoorLeadSeconds = dto.DoorLeadSeconds,
            DoorTailSeconds = dto.DoorTailSeconds,
            Font = string.IsNullOrWhiteSpace(dto.Font) ? null : dto.Font,
            FontFile = CombatFieldLayout.FontFileFor(dto.Font, manifestPath),
            TonePeriod = dto.RawTone is { Period: > 0 } t ? t.Period : null,
            ToneChannel = Math.Clamp(dto.RawTone?.Channel ?? 0, 0, 3),
            ToneVolume = Math.Clamp(dto.RawTone?.Volume ?? 0, 0, 64),
            SignDefeat = dto.Sign is { Defeat: > 0 } g ? g.Defeat : null,
            SignX = dto.Sign?.X ?? 0,
            SignY = dto.Sign?.Y ?? 0,
            SignPolls = Math.Max(0, dto.Sign?.Polls ?? 0),
        };
    }

    internal sealed class Dto
    {
        [JsonPropertyName("picture_read")] public double PictureRead { get; set; }
        [JsonPropertyName("line_polls")] public int LinePolls { get; set; }
        [JsonPropertyName("poll_seconds")] public double PollSeconds { get; set; }
        [JsonPropertyName("end_polls")] public int EndPolls { get; set; }
        [JsonPropertyName("door_seconds")] public double DoorSeconds { get; set; }
        [JsonPropertyName("after_door_ticks")] public int AfterDoorTicks { get; set; }
        [JsonPropertyName("ghost_sound")] public int GhostSound { get; set; }
        [JsonPropertyName("demon_sound")] public int DemonSound { get; set; }
        [JsonPropertyName("fate_sound")] public int FateSound { get; set; }
        [JsonPropertyName("text_y")] public int TextY { get; set; }
        [JsonPropertyName("text_pen")] public int TextPen { get; set; }
        [JsonPropertyName("blit_vbl")] public int BlitVbl { get; set; } = 1;
        [JsonPropertyName("vbl_hz")] public double? VblHz { get; set; }
        [JsonPropertyName("door_lead_seconds")] public double DoorLeadSeconds { get; set; }
        [JsonPropertyName("door_tail_seconds")] public double DoorTailSeconds { get; set; }
        public string? Font { get; set; }
        [JsonPropertyName("raw_tone")] public ToneDto? RawTone { get; set; }
        public SignDto? Sign { get; set; }
    }

    internal sealed class ToneDto
    {
        public int Channel { get; set; }
        public int Period { get; set; }
        public int Volume { get; set; }
    }

    internal sealed class SignDto
    {
        public int Defeat { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public int Polls { get; set; }
    }
}

/// <summary>
/// Manifest <c>cue_sounds</c> (I Amiga, s153 static, FORMAT.md "Sound effects"): sounds the game plays at moments outside
/// the combat and map-box screens. <see cref="Step"/>: h65 <c>+0x158</c> plays the tone (h89 <c>+0xd2</c>) when the map's
/// input routine returns a direction (every overworld / dungeon step; live s153: a 0.064 s tone per step click).
/// <see cref="OverworldEntry"/>: h59 <c>+0x342</c> / h41 <c>+0x320</c> before "A TOWN.  ENTER?" / "A DUNGEON.  ENTER?".
/// <see cref="BlackKnights"/>: h74 <c>+0x204</c> before the BLACK KNIGHTS menu.
/// </summary>
public sealed record CueSounds(int? Step, int? OverworldEntry, int? BlackKnights)
{
    /// <summary>
    /// s156: the sound after a Resurrection cast outside a fight raised a member (I Amiga h38 <c>+0x466</c>: 12 after the
    /// requester-23 box "%s IS RESURRECTED"; Core's <c>FieldCues.FieldResurrected</c>), or null.
    /// </summary>
    public int? Resurrected { get; init; }

    /// <summary>
    /// s158: the tone (h89 <c>+0xd2</c>) the town and the map play after a requester answer of the character sheet (h29
    /// <c>+0xcf4</c>: requester 14, any button), after each sheet view's EXIT (<c>+0xe86</c> SKILLS, <c>+0xf52</c>
    /// EQUIPMENT, <c>+0x934</c> POTIONS), after each gadget of the SPELLS view's requester 12 (h23 <c>+0x734</c>) and after
    /// Guild › New's PURGE / KEEP (h23 <c>+0xc56</c>), or null.
    /// </summary>
    public int? SheetTone { get; init; }

    /// <summary>
    /// s158: the sound before the town TRANSPORTATION's fade (h26 <c>+0xe4a</c>: 22 once a town is picked from "TRANSPORT
    /// TO WHICH TOWN?", h8 <c>+0x12d8</c> → h26 <c>+0xc72</c> → <c>+0xe30</c>), or null.
    /// </summary>
    public int? Transport { get; init; }

    /// <summary>
    /// s158: the alerts that beep before they show (I Amiga h26 <c>+0x6e</c> and <c>+0x1a8</c>: h89 <c>+0xe0</c>, then the
    /// requester), as the remaster's lines that stand for them (<see cref="FieldAlerts"/>, the shape II ST's use), or null.
    /// </summary>
    public FieldAlerts? Alerts { get; init; }

    internal static CueSounds FromDto(Dto dto, string manifestPath) => new(dto.Step, dto.OverworldEntry, dto.BlackKnights)
    {
        Resurrected = dto.Resurrected,
        SheetTone = dto.SheetTone,
        Transport = dto.Transport,
        Alerts = dto.Alerts is { } a ? FieldSounds.AlertsFromDto(a, manifestPath, "cue_sounds.alerts") : null,
    };

    internal sealed class Dto
    {
        public int? Step { get; set; }
        [JsonPropertyName("overworld_entry")] public int? OverworldEntry { get; set; }
        [JsonPropertyName("black_knights")] public int? BlackKnights { get; set; }
        public int? Resurrected { get; set; }
        [JsonPropertyName("sheet_tone")] public int? SheetTone { get; set; }
        public int? Transport { get; set; }
        public FieldSounds.AlertsDto? Alerts { get; set; }
    }
}

/// <summary>
/// Manifest <c>defeat_pictures</c> (s157): where Phantasie I DOS's party-defeat pictures (<see cref="PictureNames.DefeatTemple"/>
/// and the rest; OV1 <c>0x2af</c>, endgame.md section 4) put the member row and the line. OV1 <c>0x5fe</c> clears pixel
/// rows <see cref="ClearTop"/>..<see cref="ClearBottom"/> (text rows 19..23), then blits a 32 x 32 ghost per member at y
/// <see cref="RowY"/>: n members, gap = ((320 - 32 n) / (n + 1)) rounded down to <see cref="SlotAlign"/> pixels, slot i at
/// gap + i (gap + 32). A destroyed member's slot is cleared by text cells (OV1 <c>0x747</c>: columns x / 8 - 1 to
/// x / 8 + 4, the same rows). Each line is centred on row <see cref="TextY"/> in <see cref="TextColour"/>.
/// </summary>
public sealed record DefeatPicturesLayout(int RowY, int SlotAlign, int ClearTop, int ClearBottom, int TextY, string TextColour)
{
    /// <summary>The screen palette while these screens show (DOS: CGA palette 0, OV1 <c>0x2c7</c>), in place of the
    /// pack's; null keeps the pack's.</summary>
    public IReadOnlyList<string>? Palette { get; init; }

    /// <summary>Slot <paramref name="index"/>'s left edge among <paramref name="count"/> (OV1 <c>0x63c..0x681</c>).</summary>
    public int SlotX(int count, int index)
    {
        var gap = (320 - 32 * count) / (count + 1);
        if (SlotAlign > 1)
            gap = gap / SlotAlign * SlotAlign;
        return gap + index * (gap + 32);
    }

    internal static DefeatPicturesLayout FromDto(Dto dto, string manifestPath)
    {
        if (dto.RowY < 0 || dto.SlotAlign < 1 || dto.ClearTop < 0 || dto.ClearBottom < dto.ClearTop || dto.TextY < 0
            || string.IsNullOrWhiteSpace(dto.TextColour))
            throw new InvalidDataException($"{manifestPath}: defeat_pictures: bad numbers.");
        return new DefeatPicturesLayout(dto.RowY, dto.SlotAlign, dto.ClearTop, dto.ClearBottom, dto.TextY, dto.TextColour!)
        {
            Palette = dto.Palette is { Count: > 0 } pens ? pens : null,
        };
    }

    internal sealed class Dto
    {
        [JsonPropertyName("row_y")] public int RowY { get; set; }
        [JsonPropertyName("slot_align")] public int SlotAlign { get; set; } = 1;
        [JsonPropertyName("clear_top")] public int ClearTop { get; set; }
        [JsonPropertyName("clear_bottom")] public int ClearBottom { get; set; }
        [JsonPropertyName("text_y")] public int TextY { get; set; }
        [JsonPropertyName("text_colour")] public string? TextColour { get; set; }
        public List<string>? Palette { get; set; }
    }
}
