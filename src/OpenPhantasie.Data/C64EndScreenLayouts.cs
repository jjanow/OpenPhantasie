using System.Text.Json.Serialization;

namespace OpenPhantasie.Data;

/// <summary>
/// s223: one line of a C64 end screen: <see cref="Text"/> (a string id) at text column <see cref="Col"/>, row
/// <see cref="Row"/> (8 × 8 cells) in palette colour <see cref="Colour"/>, then a <c>TI</c> wait of <see cref="Seconds"/>.
/// </summary>
public sealed record C64TextLine(string Text, int Col, int Row, int Colour, double Seconds)
{
    internal static C64TextLine FromDto(Dto? dto, string what, Func<string, InvalidDataException> bad) =>
        dto is null || string.IsNullOrWhiteSpace(dto.Text) || dto.Col is < 0 or > 39 || dto.Row is < 0 or > 24
        || dto.Colour is < 0 or > 15 || dto.Seconds < 0
            ? throw bad($"{what}: text, col 0..39, row 0..24, colour 0..15, seconds >= 0")
            : new C64TextLine(dto.Text!, dto.Col, dto.Row, dto.Colour, dto.Seconds);

    internal sealed class Dto
    {
        public string? Text { get; set; }
        public int Col { get; set; }
        public int Row { get; set; }
        public int Colour { get; set; }
        public double Seconds { get; set; }
    }
}

/// <summary>
/// s223: Phantasie I C64's ending as <c>P.END</c> runs it (<c>ending_scene.c64</c>; GRAPHICS.md 9.4): the astral line;
/// a cloud per slot rising beside Zeus; Zeus wiped in from the bottom up; a strike per slot (the lightning hand, then the
/// member's figure in the cloud's place); "Zeus smiles and says" with the talking face; the speech typed a word at a time on
/// rows <see cref="SpeechRow"/>…; the grant hand. Positions are screen pixels (the VIC's sprite x − 24, y − 50); colours are
/// indices of the ending's palette. Waits: <c>TI</c> seconds as the program gives them, plus the interpreter's own time
/// (<c>…_overhead</c>, measured in VICE `[~]`); <see cref="VblHz"/> frames for the wipe and the animator (PAL 50 `[~]`).
/// </summary>
public sealed record C64EndingLayout
{
    public double VblHz { get; init; } = 50;
    public double JiffyHz { get; init; } = 60;
    public required C64TextLine Astral { get; init; }

    /// <summary>The members' cloud (a multicolour sprite whose MC1 pixels are drawn in <see cref="CloudMc1"/>).</summary>
    public required string Cloud { get; init; }
    public int CloudMc1 { get; init; } = 7;
    public int CloudLeftX { get; init; }
    public int CloudRightX { get; init; }
    public int CloudGap { get; init; }
    public int CloudFromY { get; init; }
    public int CloudToY { get; init; }
    public double CloudStepSeconds { get; init; }
    public double CloudStepOverhead { get; init; }
    /// <summary>MC1's colours while rising (P.END: yellow, red), each held <see cref="CloudFlashSteps"/> steps.</summary>
    public IReadOnlyList<int> CloudFlash { get; init; } = [];
    public int CloudFlashSteps { get; init; } = 2;
    public double AfterRiseSeconds { get; init; }

    /// <summary>Zeus's box in <see cref="PictureNames.EndingFinal"/>, revealed one pixel row per <see cref="WipeFrames"/> frames, bottom first.</summary>
    public int WipeX { get; init; }
    public int WipeY { get; init; }
    public int WipeWidth { get; init; }
    public int WipeHeight { get; init; }
    public int WipeFrames { get; init; }
    public double AfterWipeSeconds { get; init; }

    public required string StrikeHand { get; init; }
    public int StrikeX { get; init; }
    public int StrikeY { get; init; }
    /// <summary>The thunder <c>GOSUB</c> before the figure: its <c>TI</c> waits and its busy loop.</summary>
    public double ThunderSeconds { get; init; }
    public double FigureSeconds { get; init; }
    public double AfterStrikeSeconds { get; init; }
    public int OkColour { get; init; } = 7;
    public int DeadColour { get; init; } = 15;

    public required C64TextLine Smiles { get; init; }
    public IReadOnlyList<string> Face { get; init; } = [];
    public int FaceX { get; init; }
    public int FaceY { get; init; }
    /// <summary>Cells printed as spaces under the face / the hand (x, y, width, height in pixels).</summary>
    public IReadOnlyList<int> FaceBlank { get; init; } = [];
    public int AnimateVbl { get; init; } = 10;

    public int SpeechCol { get; init; }
    public int SpeechRow { get; init; }
    public int SpeechColour { get; init; }
    public double LetterSeconds { get; init; }
    public int SpaceJiffies { get; init; }
    public int RandomJiffies { get; init; }

    public IReadOnlyList<string> Hand { get; init; } = [];
    public int HandX { get; init; }
    public int HandY { get; init; }
    public IReadOnlyList<int> HandBlank { get; init; } = [];
    public double GrantSeconds { get; init; }

    /// <summary>
    /// s224: the text page after the grant (<c>P.END</c> step 10: the screen cleared, the lines from
    /// (<see cref="ClosingCol"/>, <see cref="ClosingRow"/>) one row each in <see cref="ClosingColour"/>, then
    /// <see cref="ClosingKey"/>; up until a key), as string ids; empty: the screen ends after the grant.
    /// </summary>
    public IReadOnlyList<string> ClosingLines { get; init; } = [];
    public int ClosingCol { get; init; }
    public int ClosingRow { get; init; }
    public int ClosingColour { get; init; }
    public C64TextLine? ClosingKey { get; init; }

    internal static C64EndingLayout FromDto(Dto dto, Func<string, InvalidDataException> bad)
    {
        InvalidDataException Bad(string what) => bad($"c64: {what}");
        if (dto.VblHz is <= 0 || dto.JiffyHz is <= 0 || string.IsNullOrWhiteSpace(dto.Cloud) || string.IsNullOrWhiteSpace(dto.StrikeHand)
            || dto.CloudFromY < dto.CloudToY || dto.CloudStepSeconds < 0 || dto.CloudStepOverhead < 0 || dto.CloudFlashSteps <= 0
            || dto.WipeWidth <= 0 || dto.WipeHeight <= 0 || dto.WipeFrames <= 0 || dto.AnimateVbl <= 0
            || dto.ThunderSeconds < 0 || dto.FigureSeconds < 0 || dto.AfterStrikeSeconds < 0 || dto.AfterRiseSeconds < 0
            || dto.AfterWipeSeconds < 0 || dto.LetterSeconds < 0 || dto.SpaceJiffies < 0 || dto.RandomJiffies < 0
            || dto.GrantSeconds < 0 || dto.Face is not { Count: > 0 } face || dto.Hand is not { Count: > 0 } hand
            || face.Any(string.IsNullOrWhiteSpace) || hand.Any(string.IsNullOrWhiteSpace)
            || dto.FaceBlank is not { Count: 4 } || dto.HandBlank is not { Count: 4 }
            || (dto.CloudFlash ?? []).Any(c => c is < 0 or > 15)
            || new[] { dto.CloudMc1, dto.OkColour, dto.DeadColour, dto.SpeechColour }.Any(c => c is < 0 or > 15)
            || dto.Closing is { } cl && (cl.Lines is not { Count: > 0 } || cl.Lines.Any(string.IsNullOrWhiteSpace)
                || cl.Col is < 0 or > 39 || cl.Row is < 0 or > 24 || cl.Colour is < 0 or > 15))
            throw Bad("bad numbers or pictures");
        return new C64EndingLayout
        {
            VblHz = dto.VblHz ?? 50, JiffyHz = dto.JiffyHz ?? 60,
            Astral = C64TextLine.FromDto(dto.Astral, "c64.astral", bad),
            Cloud = dto.Cloud!, CloudMc1 = dto.CloudMc1, CloudLeftX = dto.CloudLeftX, CloudRightX = dto.CloudRightX,
            CloudGap = dto.CloudGap, CloudFromY = dto.CloudFromY, CloudToY = dto.CloudToY,
            CloudStepSeconds = dto.CloudStepSeconds, CloudStepOverhead = dto.CloudStepOverhead,
            CloudFlash = [.. dto.CloudFlash ?? []], CloudFlashSteps = dto.CloudFlashSteps, AfterRiseSeconds = dto.AfterRiseSeconds,
            WipeX = dto.WipeX, WipeY = dto.WipeY, WipeWidth = dto.WipeWidth, WipeHeight = dto.WipeHeight,
            WipeFrames = dto.WipeFrames, AfterWipeSeconds = dto.AfterWipeSeconds,
            StrikeHand = dto.StrikeHand!, StrikeX = dto.StrikeX, StrikeY = dto.StrikeY, ThunderSeconds = dto.ThunderSeconds,
            FigureSeconds = dto.FigureSeconds, AfterStrikeSeconds = dto.AfterStrikeSeconds, OkColour = dto.OkColour,
            DeadColour = dto.DeadColour,
            Smiles = C64TextLine.FromDto(dto.Smiles, "c64.smiles", bad),
            Face = [.. face!], FaceX = dto.FaceX, FaceY = dto.FaceY, FaceBlank = [.. dto.FaceBlank], AnimateVbl = dto.AnimateVbl,
            SpeechCol = dto.SpeechCol, SpeechRow = dto.SpeechRow, SpeechColour = dto.SpeechColour,
            LetterSeconds = dto.LetterSeconds, SpaceJiffies = dto.SpaceJiffies, RandomJiffies = dto.RandomJiffies,
            Hand = [.. hand!], HandX = dto.HandX, HandY = dto.HandY, HandBlank = [.. dto.HandBlank], GrantSeconds = dto.GrantSeconds,
            ClosingLines = [.. dto.Closing?.Lines ?? []], ClosingCol = dto.Closing?.Col ?? 0, ClosingRow = dto.Closing?.Row ?? 0,
            ClosingColour = dto.Closing?.Colour ?? 0,
            ClosingKey = dto.Closing?.Key is { } key ? C64TextLine.FromDto(key, "c64.closing.key", bad) : null,
        };
    }

    internal sealed class ClosingDto
    {
        public List<string>? Lines { get; set; }
        public int Col { get; set; }
        public int Row { get; set; }
        public int Colour { get; set; }
        public C64TextLine.Dto? Key { get; set; }
    }

    internal sealed class Dto
    {
        [JsonPropertyName("vbl_hz")] public double? VblHz { get; set; }
        [JsonPropertyName("jiffy_hz")] public double? JiffyHz { get; set; }
        public C64TextLine.Dto? Astral { get; set; }
        public string? Cloud { get; set; }
        [JsonPropertyName("cloud_mc1")] public int CloudMc1 { get; set; } = 7;
        [JsonPropertyName("cloud_left_x")] public int CloudLeftX { get; set; }
        [JsonPropertyName("cloud_right_x")] public int CloudRightX { get; set; }
        [JsonPropertyName("cloud_gap")] public int CloudGap { get; set; }
        [JsonPropertyName("cloud_from_y")] public int CloudFromY { get; set; }
        [JsonPropertyName("cloud_to_y")] public int CloudToY { get; set; }
        [JsonPropertyName("cloud_step_seconds")] public double CloudStepSeconds { get; set; }
        [JsonPropertyName("cloud_step_overhead")] public double CloudStepOverhead { get; set; }
        [JsonPropertyName("cloud_flash")] public List<int>? CloudFlash { get; set; }
        [JsonPropertyName("cloud_flash_steps")] public int CloudFlashSteps { get; set; } = 2;
        [JsonPropertyName("after_rise_seconds")] public double AfterRiseSeconds { get; set; }
        [JsonPropertyName("wipe_x")] public int WipeX { get; set; }
        [JsonPropertyName("wipe_y")] public int WipeY { get; set; }
        [JsonPropertyName("wipe_width")] public int WipeWidth { get; set; }
        [JsonPropertyName("wipe_height")] public int WipeHeight { get; set; }
        [JsonPropertyName("wipe_frames")] public int WipeFrames { get; set; }
        [JsonPropertyName("after_wipe_seconds")] public double AfterWipeSeconds { get; set; }
        [JsonPropertyName("strike_hand")] public string? StrikeHand { get; set; }
        [JsonPropertyName("strike_x")] public int StrikeX { get; set; }
        [JsonPropertyName("strike_y")] public int StrikeY { get; set; }
        [JsonPropertyName("thunder_seconds")] public double ThunderSeconds { get; set; }
        [JsonPropertyName("figure_seconds")] public double FigureSeconds { get; set; }
        [JsonPropertyName("after_strike_seconds")] public double AfterStrikeSeconds { get; set; }
        [JsonPropertyName("ok_colour")] public int OkColour { get; set; } = 7;
        [JsonPropertyName("dead_colour")] public int DeadColour { get; set; } = 15;
        public C64TextLine.Dto? Smiles { get; set; }
        public List<string>? Face { get; set; }
        [JsonPropertyName("face_x")] public int FaceX { get; set; }
        [JsonPropertyName("face_y")] public int FaceY { get; set; }
        [JsonPropertyName("face_blank")] public List<int>? FaceBlank { get; set; }
        [JsonPropertyName("animate_vbl")] public int AnimateVbl { get; set; } = 10;
        [JsonPropertyName("speech_col")] public int SpeechCol { get; set; }
        [JsonPropertyName("speech_row")] public int SpeechRow { get; set; }
        [JsonPropertyName("speech_colour")] public int SpeechColour { get; set; }
        [JsonPropertyName("letter_seconds")] public double LetterSeconds { get; set; }
        [JsonPropertyName("space_jiffies")] public int SpaceJiffies { get; set; }
        [JsonPropertyName("random_jiffies")] public int RandomJiffies { get; set; }
        public List<string>? Hand { get; set; }
        [JsonPropertyName("hand_x")] public int HandX { get; set; }
        [JsonPropertyName("hand_y")] public int HandY { get; set; }
        [JsonPropertyName("hand_blank")] public List<int>? HandBlank { get; set; }
        [JsonPropertyName("grant_seconds")] public double GrantSeconds { get; set; }
        public ClosingDto? Closing { get; set; }
    }
}

/// <summary>
/// s224: Phantasie II C64's ending as <c>WIN</c> runs it (<c>ending_scene.c64_win</c>; II's GRAPHICS.md 8.1, I's 9.5):
/// the intro page (the ending scene's <c>intro_page</c>) while the hall loads, no key; the hall alone while the band
/// file loads; the crowd and the whole party appear with the header lines; a <c>FOR 4000</c>; a cheer; per presented
/// member a <c>FOR 4000</c>, the member in its attacking pose in <see cref="PresentColour"/> with its title on three
/// centred rows, a <c>FOR 1000</c>, a <c>FOR 4000</c>, a cheer, the member back; the wands prompt until a key; the
/// closing page (<c>closing_page</c>) until a key. A cheer: the panel cleared, <see cref="CheerLine"/>, the crowd's
/// second frame for the sound, the first again. Positions are screen pixels / text cells; colours the ending palette's.
/// The busy loops' and loads' times were measured in VICE (PAL, true drive) `[~]`.
/// </summary>
public sealed record C64WinLayout
{
    public double IntroSeconds { get; init; }
    public double HallSeconds { get; init; }
    /// <summary>The crowd's two frames (idle, cheering) at (<see cref="CrowdX"/>, <see cref="CrowdY"/>).</summary>
    public IReadOnlyList<string> Crowd { get; init; } = [];
    public int CrowdX { get; init; }
    public int CrowdY { get; init; }
    public int RowLeft { get; init; }
    public int RowTop { get; init; }
    public int SlotSize { get; init; }
    public int PresentColour { get; init; }
    public int TextColour { get; init; }
    /// <summary>Cells (col, row) of the header lines, one per line of Core's header.</summary>
    public IReadOnlyList<(int Col, int Row)> HeaderAt { get; init; } = [];
    public double ForSeconds { get; init; }
    public double TitleSeconds { get; init; }
    public int TitleRow { get; init; }
    /// <summary>A title row's column: INT(<see cref="TitleCentre"/> − its length / 2).</summary>
    public int TitleCentre { get; init; }
    public required string TitleWord { get; init; }
    public required string TitleFrom { get; init; }
    public required C64TextLine CheerLine { get; init; }
    public double CheerFrameAt { get; init; }
    public double CheerFrameSeconds { get; init; }
    public double CheerLineSeconds { get; init; }
    public double CheerSeconds { get; init; }
    /// <summary>Cells of the wands prompt's lines (Core's speech: its first line, then the wand lines; blank lines skipped).</summary>
    public IReadOnlyList<(int Col, int Row)> WandsAt { get; init; } = [];
    /// <summary>Printed after the prompt's last line (" (PRESS RET)"), a string id, or null.</summary>
    public string? WandsKey { get; init; }

    internal static C64WinLayout FromDto(Dto dto, Func<string, InvalidDataException> bad)
    {
        InvalidDataException Bad(string what) => bad($"c64_win: {what}");
        static bool Cell(List<int>? c) => c is { Count: 2 } && c[0] is >= 0 and <= 39 && c[1] is >= 0 and <= 24;
        if (dto.Crowd is not { Count: 2 } crowd || crowd.Any(string.IsNullOrWhiteSpace)
            || string.IsNullOrWhiteSpace(dto.TitleWord) || string.IsNullOrWhiteSpace(dto.TitleFrom)
            || dto.HeaderAt is not { } header || !header.All(Cell) || dto.WandsAt is not { } wands || !wands.All(Cell)
            || new[] { dto.IntroSeconds, dto.HallSeconds, dto.ForSeconds, dto.TitleSeconds, dto.CheerFrameAt,
                dto.CheerFrameSeconds, dto.CheerLineSeconds, dto.CheerSeconds }.Any(s => s < 0)
            || dto.SlotSize <= 0 || dto.TitleRow is < 0 or > 22 || dto.PresentColour is < 0 or > 15
            || dto.TextColour is < 0 or > 15)
            throw Bad("bad numbers or pictures");
        return new C64WinLayout
        {
            IntroSeconds = dto.IntroSeconds, HallSeconds = dto.HallSeconds, Crowd = [.. crowd!], CrowdX = dto.CrowdX,
            CrowdY = dto.CrowdY, RowLeft = dto.RowLeft, RowTop = dto.RowTop, SlotSize = dto.SlotSize,
            PresentColour = dto.PresentColour, TextColour = dto.TextColour,
            HeaderAt = [.. header.Select(c => (c[0], c[1]))], ForSeconds = dto.ForSeconds, TitleSeconds = dto.TitleSeconds,
            TitleRow = dto.TitleRow, TitleCentre = dto.TitleCentre, TitleWord = dto.TitleWord!, TitleFrom = dto.TitleFrom!,
            CheerLine = C64TextLine.FromDto(dto.CheerLine, "c64_win.cheer_line", bad), CheerFrameAt = dto.CheerFrameAt,
            CheerFrameSeconds = dto.CheerFrameSeconds, CheerLineSeconds = dto.CheerLineSeconds, CheerSeconds = dto.CheerSeconds,
            WandsAt = [.. wands.Select(c => (c[0], c[1]))], WandsKey = dto.WandsKey,
        };
    }

    internal sealed class Dto
    {
        [JsonPropertyName("intro_seconds")] public double IntroSeconds { get; set; }
        [JsonPropertyName("hall_seconds")] public double HallSeconds { get; set; }
        public List<string>? Crowd { get; set; }
        [JsonPropertyName("crowd_x")] public int CrowdX { get; set; }
        [JsonPropertyName("crowd_y")] public int CrowdY { get; set; }
        [JsonPropertyName("row_left")] public int RowLeft { get; set; }
        [JsonPropertyName("row_top")] public int RowTop { get; set; }
        [JsonPropertyName("slot_size")] public int SlotSize { get; set; }
        [JsonPropertyName("present_colour")] public int PresentColour { get; set; }
        [JsonPropertyName("text_colour")] public int TextColour { get; set; }
        [JsonPropertyName("header_at")] public List<List<int>>? HeaderAt { get; set; }
        [JsonPropertyName("for_seconds")] public double ForSeconds { get; set; }
        [JsonPropertyName("title_seconds")] public double TitleSeconds { get; set; }
        [JsonPropertyName("title_row")] public int TitleRow { get; set; }
        [JsonPropertyName("title_centre")] public int TitleCentre { get; set; }
        [JsonPropertyName("title_word")] public string? TitleWord { get; set; }
        [JsonPropertyName("title_from")] public string? TitleFrom { get; set; }
        [JsonPropertyName("cheer_line")] public C64TextLine.Dto? CheerLine { get; set; }
        [JsonPropertyName("cheer_frame_at")] public double CheerFrameAt { get; set; }
        [JsonPropertyName("cheer_frame_seconds")] public double CheerFrameSeconds { get; set; }
        [JsonPropertyName("cheer_line_seconds")] public double CheerLineSeconds { get; set; }
        [JsonPropertyName("cheer_seconds")] public double CheerSeconds { get; set; }
        [JsonPropertyName("wands_at")] public List<List<int>>? WandsAt { get; set; }
        [JsonPropertyName("wands_key")] public string? WandsKey { get; set; }
    }
}

/// <summary>
/// s223: Phantasie I / II C64's party wipe as <c>CEMETARY</c> / <c>CEMETARY2</c> run it (<c>c64_defeat</c>; I's
/// GRAPHICS.md 9.2 / 9.4): it runs by itself, no key. "Your party has been defeated"; the temple with the door closed;
/// the astral line; a ghost per member, one each <see cref="GhostSeconds"/>; a wait; the door rises behind the temple
/// (<see cref="DoorSteps"/> pixels, one each <see cref="DoorStepSeconds"/> + overhead); the Great Demon's face; its line;
/// per member its fate line in the fate's colour, a sound, the ghost turned into its figure in that colour (a destroyed
/// member's switched off); a last wait. Screen pixels, palette indices of the pack's palette.
/// </summary>
public sealed record C64DefeatLayout
{
    public required string Temple { get; init; }
    public required string Ghost { get; init; }
    public required string Door { get; init; }
    public required string Face { get; init; }
    public required C64TextLine Defeated { get; init; }
    public int TextCol { get; init; }
    public int TextRow { get; init; }
    public int AstralColour { get; init; }
    public int DemonColour { get; init; }
    /// <summary>Ghost k at x = floor((<see cref="RowX2"/> − <see cref="RowGap"/> · (n − 1)) / 2) + <see cref="RowGap"/> · k.</summary>
    public int RowX2 { get; init; }
    public int RowGap { get; init; }
    public int RowY { get; init; }
    public double GhostSeconds { get; init; }
    public double AfterGhostsSeconds { get; init; }
    public int DoorX { get; init; }
    public int DoorY { get; init; }
    public int DoorSteps { get; init; }
    public double DoorStepSeconds { get; init; }
    public double DoorStepOverhead { get; init; }
    public int FaceX { get; init; }
    public int FaceY { get; init; }
    public double DemonSeconds { get; init; }
    public double FateSoundSeconds { get; init; }
    public double FateCopySeconds { get; init; }
    public double FateSeconds { get; init; }
    public int DestroyedColour { get; init; } = 11;
    public int ResurrectedColour { get; init; } = 7;
    public int UndeadColour { get; init; } = 3;
    public double EndSeconds { get; init; }

    /// <summary>s224: an UNDEAD fate's member name, printed on its own row (<c>CEMETARY</c> <c>0x58e</c>: column 1, row 21,
    /// cyan) before its line, or null.</summary>
    public int? UndeadNameRow { get; init; }
    public int UndeadNameCol { get; init; }

    internal static C64DefeatLayout FromDto(Dto dto, string manifestPath)
    {
        InvalidDataException Bad(string what) => new($"{manifestPath}: c64_defeat: {what}.");
        if (new[] { dto.Temple, dto.Ghost, dto.Door, dto.Face }.Any(string.IsNullOrWhiteSpace)
            || new[] { dto.GhostSeconds, dto.AfterGhostsSeconds, dto.DoorStepSeconds, dto.DoorStepOverhead, dto.DemonSeconds,
                dto.FateSoundSeconds, dto.FateCopySeconds, dto.FateSeconds, dto.EndSeconds }.Any(s => s < 0)
            || dto.DoorSteps < 0 || dto.TextCol is < 0 or > 39 || dto.TextRow is < 0 or > 24
            || dto.UndeadNameRow is < 0 or > 24 || dto.UndeadNameCol is < 0 or > 39
            || new[] { dto.AstralColour, dto.DemonColour, dto.DestroyedColour, dto.ResurrectedColour, dto.UndeadColour }
                .Any(c => c is < 0 or > 15))
            throw Bad("bad numbers or pictures");
        return new C64DefeatLayout
        {
            Temple = dto.Temple!, Ghost = dto.Ghost!, Door = dto.Door!, Face = dto.Face!,
            Defeated = C64TextLine.FromDto(dto.Defeated, "defeated", w => Bad(w)),
            TextCol = dto.TextCol, TextRow = dto.TextRow, AstralColour = dto.AstralColour, DemonColour = dto.DemonColour,
            RowX2 = dto.RowX2, RowGap = dto.RowGap, RowY = dto.RowY, GhostSeconds = dto.GhostSeconds,
            AfterGhostsSeconds = dto.AfterGhostsSeconds, DoorX = dto.DoorX, DoorY = dto.DoorY, DoorSteps = dto.DoorSteps,
            DoorStepSeconds = dto.DoorStepSeconds, DoorStepOverhead = dto.DoorStepOverhead, FaceX = dto.FaceX,
            FaceY = dto.FaceY, DemonSeconds = dto.DemonSeconds, FateSoundSeconds = dto.FateSoundSeconds,
            FateCopySeconds = dto.FateCopySeconds, FateSeconds = dto.FateSeconds, DestroyedColour = dto.DestroyedColour,
            ResurrectedColour = dto.ResurrectedColour, UndeadColour = dto.UndeadColour, EndSeconds = dto.EndSeconds,
            UndeadNameRow = dto.UndeadNameRow, UndeadNameCol = dto.UndeadNameCol,
        };
    }

    internal sealed class Dto
    {
        public string? Temple { get; set; }
        public string? Ghost { get; set; }
        public string? Door { get; set; }
        public string? Face { get; set; }
        public C64TextLine.Dto? Defeated { get; set; }
        [JsonPropertyName("text_col")] public int TextCol { get; set; }
        [JsonPropertyName("text_row")] public int TextRow { get; set; }
        [JsonPropertyName("astral_colour")] public int AstralColour { get; set; }
        [JsonPropertyName("demon_colour")] public int DemonColour { get; set; }
        [JsonPropertyName("row_x2")] public int RowX2 { get; set; }
        [JsonPropertyName("row_gap")] public int RowGap { get; set; }
        [JsonPropertyName("row_y")] public int RowY { get; set; }
        [JsonPropertyName("ghost_seconds")] public double GhostSeconds { get; set; }
        [JsonPropertyName("after_ghosts_seconds")] public double AfterGhostsSeconds { get; set; }
        [JsonPropertyName("door_x")] public int DoorX { get; set; }
        [JsonPropertyName("door_y")] public int DoorY { get; set; }
        [JsonPropertyName("door_steps")] public int DoorSteps { get; set; }
        [JsonPropertyName("door_step_seconds")] public double DoorStepSeconds { get; set; }
        [JsonPropertyName("door_step_overhead")] public double DoorStepOverhead { get; set; }
        [JsonPropertyName("face_x")] public int FaceX { get; set; }
        [JsonPropertyName("face_y")] public int FaceY { get; set; }
        [JsonPropertyName("demon_seconds")] public double DemonSeconds { get; set; }
        [JsonPropertyName("fate_sound_seconds")] public double FateSoundSeconds { get; set; }
        [JsonPropertyName("fate_copy_seconds")] public double FateCopySeconds { get; set; }
        [JsonPropertyName("fate_seconds")] public double FateSeconds { get; set; }
        [JsonPropertyName("destroyed_colour")] public int DestroyedColour { get; set; } = 11;
        [JsonPropertyName("resurrected_colour")] public int ResurrectedColour { get; set; } = 7;
        [JsonPropertyName("undead_colour")] public int UndeadColour { get; set; } = 3;
        [JsonPropertyName("end_seconds")] public double EndSeconds { get; set; }
        [JsonPropertyName("undead_name_row")] public int? UndeadNameRow { get; set; }
        [JsonPropertyName("undead_name_col")] public int UndeadNameCol { get; set; }
    }
}
