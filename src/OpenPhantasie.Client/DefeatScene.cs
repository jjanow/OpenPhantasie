using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client;

/// <summary>What one slot of II's defeat-scene portrait row shows (ST <c>0x12b8e</c>, FORMAT.md §10.3).</summary>
public enum DefeatPortraitKind
{
    /// <summary>The ghost portrait (<see cref="OpenPhantasie.Data.PictureNames.PortraitGhost"/>): before the judgement.</summary>
    Ghost,

    /// <summary>The member's race portrait: RESURRECTED, or `core.defeat.x_status_undead` (race 15, the UNDEAD portrait).</summary>
    Race,

    /// <summary>DESTROYED: the slot is cleared (<c>0x12824</c>).</summary>
    Cleared,

    /// <summary>Phantasie III (Amiga): DESTROYED shows the grave (<see cref="OpenPhantasie.Data.PictureNames.PortraitGrave"/>).</summary>
    Grave,
}

public readonly record struct DefeatPortrait(DefeatPortraitKind Kind, int RaceId);

/// <summary>Where the door is and whether what stands behind it shows, for one frame of the defeat scene.</summary>
/// <param name="DoorHeight">Rows of the door drawn from its top (90 = closed, 0 = gone); its bottom stays at ST y 104.</param>
/// <param name="ShowBehindDoor">The sign (DefeatDemonSign) or the Great Demon's face (DefeatOutcome) is drawn.</param>
public readonly record struct DefeatSceneFrame(int DoorHeight, bool ShowBehindDoor)
{
    /// <summary>ST y of the door's top this frame (<c>0x12dc0</c>: y = 104 − h).</summary>
    public int DoorY => DefeatScene.DoorBottom - DoorHeight;
}

/// <summary>
/// Phantasie II's party-defeat scene on <c>DECOR.PIC</c> (ST <c>0x12bea</c>, FORMAT.md §10.3/§10.4) [C] static s95:
/// <list type="bullet">
/// <item>The portrait row: one 32×32 slot per party member (DEAD included, the summoned elemental dropped), spaced
/// <c>(320 − 32·n)/(n + 1)</c> apart (<c>0x1814e</c>). After `core.defeat.your_party_has_been_defeated` every slot shows the ghost
/// portrait (<c>0x12b5e(0)</c>); each fate then redraws its slot.</item>
/// <item>The door (80×90 at (123,14)): after `core.defeat.arrived_at_astral_plane` it sinks into the floor, 90 steps of 4
/// VBLs (<c>0x12d8c</c>): step j draws the door's top <c>91 − j</c> rows at <c>y = 104 − h</c>. Then the sign (5th
/// defeat) or, after a 3000 ms wait (<c>0x12e28</c>), the Great Demon's face.</item>
/// </list>
/// The remaster shows the scene behind its prompts: the door sinks while the first prompt after the defeat box shows. The
/// whole ST screen is drawn 1:1 over the remaster's status, help and message lines, the prompt's text one line at a time
/// on the bottom line (<see cref="DefeatScenePager"/>; s98, user: never crop scenes).
/// </summary>
public static class DefeatScene
{
    public const int DoorHeightClosed = 90;
    public const int DoorBottom = 14 + DoorHeightClosed;
    public const int VblPerStep = 4;
    public const int Steps = DoorHeightClosed;

    /// <summary>
    /// VBL rate the ST pack assumes (its colour cycle's <c>vbl_hz</c>, a US colour ST) [~]. s97 static: neither
    /// <c>START.PRG</c> nor <c>PHANT.PRG</c> sets the sync mode (no <c>$FF820A</c> write) or reads <c>palmode</c> /
    /// <c>_vbclock</c> / <c>_frclock</c> / <c>_hz_200</c>, so the door's 360 VBLs (XBIOS Vsync) take 6 s on a 60 Hz
    /// (US TOS) machine and 7.2 s on a 50 Hz (PAL TOS) one; the game itself fixes no rate. The 3000 ms waits use the AES
    /// timer and are the same on both.
    /// </summary>
    public const double VblHz = 60;

    /// <summary>
    /// The whole sinking: 90 steps × 4 VBLs = 360 VBLs by the code, 413 VBLs = 6.87 s at 60 Hz traced on Hatari (s141
    /// <c>capture_s141_sounds.md</c>: the door's blits add 53 VBLs). s156: the remaster takes the traced total, spread evenly
    /// over the steps (which steps the blits slow is not measured `[~]`).
    /// </summary>
    public const double DoorVbl = 413;

    /// <summary>The whole sinking in seconds (<see cref="DoorVbl"/> at <see cref="VblHz"/>).</summary>
    public const double DoorSeconds = DoorVbl / VblHz;

    /// <summary>The wait between the open door and the Great Demon's face (<c>0x12e28</c>: 3000 ms).</summary>
    public const double FaceDelaySeconds = 3.0;

    public const int PortraitSize = 32;

    /// <summary>
    /// The scene is drawn full-screen in ST coordinates, as the ST shows it (s98, user: never crop scenes): DECOR's 155
    /// rows from y 0, the portrait row at y 158 (FORMAT.md §10.3), the text on the bottom line.
    /// </summary>
    public const int DecorRows = 155;

    /// <summary>ST y of the portrait row (<c>0x12b8e</c>).</summary>
    public const int RowTop = 158;

    /// <summary>The text line under the row: the ST prints each line of the scene on the screen's bottom line.</summary>
    public const int TextTop = 190;

    /// <summary>Door rows drawn <paramref name="seconds"/> after the sinking started (90 at the start, 0 once gone).</summary>
    public static int DoorHeightAt(double seconds)
    {
        if (seconds <= 0)
            return DoorHeightClosed;
        var steps = (int)Math.Floor(seconds / DoorSeconds * Steps + 1e-9);
        var h = Math.Min(DoorHeightClosed, DoorHeightClosed + 1 - steps);
        return h <= 1 ? 0 : h; // the last step draws only the door's black top row: nothing left to see
    }

    /// <summary>
    /// The outcome page's lines this frame: the ST prints the Great Demon's speech and the fates only once the face shows
    /// (<c>0x12e62</c>, then the fate loop), so until then only `core.defeat.arrived_at_astral_plane` shows and the
    /// sinking door stays in view above the box. Every other page keeps its lines.
    /// </summary>
    public static IReadOnlyList<string> LinesShown(SessionPromptKind kind, IReadOnlyList<string> lines, DefeatSceneFrame frame)
    {
        if (kind != SessionPromptKind.DefeatOutcome || frame.ShowBehindDoor)
            return lines;
        var astral = GameText.S("core.defeat.arrived_at_astral_plane");
        return lines.Where(l => l == astral).ToList();
    }

    /// <summary>Left edge of slot <paramref name="index"/> in a row of <paramref name="count"/> portraits (<c>0x1814e</c>).</summary>
    public static int SlotX(int count, int index)
    {
        var gap = (320 - PortraitSize * count) / (count + 1);
        return gap + index * (gap + PortraitSize);
    }

    /// <summary>
    /// Fates the row shows while line <paramref name="lineIndex"/> of the outcome page's <paramref name="lineCount"/> lines
    /// shows (<see cref="DefeatScenePager"/>): the fate lines are the page's last <paramref name="fateCount"/>, one per
    /// member in party order (<see cref="PartyDefeat"/>), and the ST redraws each slot as its line prints (<c>0x126bc</c>).
    /// </summary>
    public static int FatesShown(int lineCount, int lineIndex, int fateCount) =>
        Math.Clamp(lineIndex - (lineCount - fateCount) + 1, 0, fateCount);

    /// <summary>
    /// The row for <paramref name="kind"/>: ghosts for every member while the defeat box and the sign show, each member's
    /// fate once the outcome's Great Demon shows (<paramref name="shown"/> = <see cref="SessionPrompts.ShownDefeat"/>;
    /// <paramref name="faceShown"/> = the frame's <see cref="DefeatSceneFrame.ShowBehindDoor"/>). The ST redraws a slot
    /// only in the fate loop after the face (<c>0x126bc</c>), so the ghosts stay while the door sinks (s97 Client check);
    /// with <paramref name="fatesShown"/> only the first that many slots show their fate, the rest keep the ghost until
    /// their line prints (<see cref="FatesShown"/>, s98).
    /// </summary>
    public static IReadOnlyList<DefeatPortrait> Row(GameSession session, PartyDefeatResult? shown, SessionPromptKind kind,
        bool faceShown = true, int fatesShown = int.MaxValue)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (kind == SessionPromptKind.DefeatOutcome && shown is not null && faceShown)
            return shown.Fates.Select((f, i) => i >= fatesShown
                ? new DefeatPortrait(DefeatPortraitKind.Ghost, (int)f.Member.Race) // its fate line is still to come
                : f.Fate switch
                {
                    DefeatFate.Destroyed => new DefeatPortrait(DefeatPortraitKind.Cleared, 0),
                    _ => new DefeatPortrait(DefeatPortraitKind.Race, (int)f.Member.Race), // UNDEAD: Race is already 15
                }).ToList();
        var members = shown is not null
            ? shown.Fates.Select(f => f.Member)
            : session.Party.Members.Where(m => !ReferenceEquals(m, session.Party.Elemental));
        return members.Select(m => new DefeatPortrait(DefeatPortraitKind.Ghost, (int)m.Race)).ToList();
    }
}

/// <summary>
/// Phantasie III's party-defeat scene as the Amiga shows it (root <c>0x1dc16</c>, static s98): on a cleared screen the
/// first two sentences print on text rows 12–13 ("`core.p3.astral_1` / begin the journey…", 120 ticks,
/// then "`core.p3.astral_2` / the Astral Plane…", 120 ticks); then <c>D/W2.cmp</c> (the gates of the
/// Astral Plane, art on rows 0–140) fills the screen, the party's portraits stand under it (<c>0x1b6be</c>: 32×48 each,
/// slot i at x = 48·i, y = 143, the y of combat row 0 from <c>0x1b42e</c>), and every further line prints on text row 24
/// (y 192): `core.p3.astral_3`, then each member's name and fate, after which the row is redrawn with
/// that member's new picture (destroyed: race byte 22 = the grave, sprite 16; undead: race 17 = the Undead portrait).
/// The Amiga moves on by itself (100 / 140-tick waits); the remaster pages with Return (<see cref="DefeatScenePager"/>).
/// </summary>
public static class AstralScene
{
    /// <summary>Lines before this index print on the cleared screen; from it on the picture shows.</summary>
    public const int PictureFromLine = 2;

    /// <summary>Text rows 12–13 of the cleared screen (8-pixel rows).</summary>
    public const int IntroTextTop = 96;

    public const int RowTop = 143;
    public const int SlotWidth = 48;
    public const int PortraitWidth = 32;
    public const int PortraitHeight = 48;

    /// <summary>Text row 24.</summary>
    public const int TextTop = 192;

    /// <summary>The palette (<see cref="AssetCatalog.PaletteColour"/>) in force while the scene prints: <c>D/W2.cmp</c>'s.</summary>
    public const string Palette = "astral";

    /// <summary>The text colour of every line (colour 15).</summary>
    public const int TextColour = 15;

    /// <summary>A member's line when the fate is undead: name and fate in colour 14 (<c>0x1df0a</c>, string 23), not 15.</summary>
    public const int UndeadColour = 14;

    /// <summary>
    /// The palette colour of line <paramref name="lineIndex"/> of the outcome page (its non-blank lines numbered as
    /// <see cref="DefeatScenePager.Index"/> does, <paramref name="lineCount"/> of them): <see cref="UndeadColour"/> for a
    /// member made undead, else <see cref="TextColour"/>. The fate lines are the page's last <c>Fates.Count</c>.
    /// </summary>
    public static int LineColour(PartyDefeatResult shown, int lineCount, int lineIndex)
    {
        ArgumentNullException.ThrowIfNull(shown);
        var fate = lineIndex - (lineCount - shown.Fates.Count);
        return fate >= 0 && fate < shown.Fates.Count && shown.Fates[fate].Fate == DefeatFate.Undead ? UndeadColour : TextColour;
    }

    /// <summary>The sound as the demon appears (<c>0x1dde6</c>, before `core.p3.astral_3`).</summary>
    public const int DemonSound = 45;

    /// <summary>A member's fate sound (<c>0x1dfe6</c>): 48 when destroyed, 9 otherwise (resurrected or undead).</summary>
    public const int DestroyedSound = 48;
    public const int FateSound = 9;

    /// <summary>
    /// The sound line <paramref name="lineIndex"/> of the outcome page starts with (s117; numbered as
    /// <see cref="LineColour"/>), or null: <see cref="DemonSound"/> on the demon's line (the one before the fates),
    /// <see cref="DestroyedSound"/> / <see cref="FateSound"/> on each member's fate line.
    /// </summary>
    public static int? SoundFor(PartyDefeatResult shown, int lineCount, int lineIndex)
    {
        ArgumentNullException.ThrowIfNull(shown);
        var fate = lineIndex - (lineCount - shown.Fates.Count);
        if (fate == -1 && lineIndex >= PictureFromLine)
            return DemonSound;
        if (fate < 0 || fate >= shown.Fates.Count)
            return null;
        return shown.Fates[fate].Fate == DefeatFate.Destroyed ? DestroyedSound : FateSound;
    }

    /// <summary>
    /// The row: each member's living race before its fate line shows, then the grave (destroyed), the Undead portrait or
    /// the race (resurrected) once <paramref name="fatesShown"/> passes it (<see cref="DefeatScene.FatesShown"/>).
    /// </summary>
    public static IReadOnlyList<DefeatPortrait> Row(PartyDefeatResult shown, int fatesShown)
    {
        ArgumentNullException.ThrowIfNull(shown);
        return shown.Fates.Select((f, i) =>
        {
            var living = f.Fate == DefeatFate.Undead && f.Member.BirthRace != Race.Undead ? f.Member.BirthRace : f.Member.Race;
            if (i >= fatesShown)
                return new DefeatPortrait(DefeatPortraitKind.Race, (int)living);
            return f.Fate switch
            {
                DefeatFate.Destroyed => new DefeatPortrait(DefeatPortraitKind.Grave, 0),
                DefeatFate.Undead => new DefeatPortrait(DefeatPortraitKind.Race, (int)Race.Undead),
                _ => new DefeatPortrait(DefeatPortraitKind.Race, (int)living),
            };
        }).ToList();
    }
}

/// <summary>
/// Phantasie III's gods' reward as the Amiga shows it (root <c>0x1ee82</c>, static s102; DOS ovl4 <c>0x0000</c> has the
/// same text): on a cleared screen "`core.p3.reward_noises` lights" (text row 11, colour 15, then the flashing
/// <c>0x1f30a</c>), "When the noises and lights ..." (row 9); then <c>D/W1.cmp</c> (Zeus, a good win) or <c>D/W0.cmp</c>
/// (Pluto, an evil win; art on rows 0-120) with the party's portraits under it (<c>0x1b6be</c>: slot i at x = 48 i,
/// y = 123, the y of combat row 3: every record's row word is set to 3 at <c>0x1ef96</c>; slot 6, the elemental's, is
/// empty) and "Press a key" at text column 14 row 23, while the reward tune <c>D/M1</c> plays (<c>0x1fa5c</c>). After a
/// key the god's speech is a text page on the parchment (<c>0x1e2c2</c> with the frame, shown in the dungeon palette,
/// colour 2 on 4). The remaster pages with Return through <see cref="Pages"/>. The intro's flashes (<c>0x1f30a</c> /
/// <c>0x1b814</c>, the pack's <see cref="RewardFlashLayout"/>) run on pages 0 and 1, which move on by themselves when
/// their run ends (the Amiga reads no key there; Return skips the run); <c>D/M1</c> plays from the picture page to the
/// scene's end (<see cref="RewardSceneClock"/>, s109). Sound 15 starts before each flash's second frame (s110, the
/// pack's <see cref="RewardFlashLayout.Sound"/>; the Sound switch silences it).
/// </summary>
public static class GodsRewardScene
{
    /// <summary>Pages 0 and 1: the intro lines on the cleared screen, from these y (text rows 11 and 9).</summary>
    public static readonly int[] IntroTop = [88, 72];

    /// <summary>The page with the god's picture and the party.</summary>
    public const int PicturePage = 2;

    /// <summary>The speech page (the last).</summary>
    public const int SpeechPage = 3;

    public const int RowTop = 123;
    public const int SlotWidth = 48;

    /// <summary>"Press a key" on the picture page: text column 14, row 23.</summary>
    public const int PressKeyX = 14 * 8;
    public const int PressKeyY = 23 * 8;

    /// <summary>
    /// One label per page for <see cref="DefeatScenePager"/> (Return moves to the next page, the last answers the
    /// prompt): the two intro lines, the picture page ("Press a key"), the speech.
    /// </summary>
    public static IReadOnlyList<string> Pages(RewardScene scene, string pressKey)
    {
        ArgumentNullException.ThrowIfNull(scene);
        var intro = scene.IntroLines.Concat(["", ""]).Take(2).Select(l => string.IsNullOrWhiteSpace(l) ? "-" : l);
        return [.. intro, pressKey, string.Join(" ", scene.SpeechLines)];
    }

    /// <summary>The speech page's lines: the god's words, a blank line, then "Press a key" (as the Amiga's last text ends).</summary>
    public static IReadOnlyList<string> SpeechLines(RewardScene scene, string pressKey)
    {
        ArgumentNullException.ThrowIfNull(scene);
        return [.. scene.SpeechLines, "", pressKey];
    }
}

/// <summary>
/// The gods' reward scene's timing (s109): which page shows since when, the flashes of the intro pages (rolled once per
/// scene, as the Amiga rolls them as it goes) and when a run is over. Page 0 runs the rising flashes, page 1 the fading
/// ones (<see cref="RewardFlashLayout.Plan"/>); the tune belongs to <see cref="GodsRewardScene.PicturePage"/> and after.
/// No MonoGame types: tests drive it with plain seconds.
/// </summary>
public sealed class RewardSceneClock
{
    private RewardScene? _scene;
    private RewardFlashLayout? _layout;
    private IReadOnlyList<RewardFlashLayout.Flash>? _rising, _fading;
    private int _page = -1;
    private double _since;

    /// <summary>The page shown last.</summary>
    public int Page => _page;

    /// <summary>
    /// The scene <paramref name="scene"/> shows page <paramref name="page"/> at <paramref name="now"/> seconds: a new scene
    /// rolls its flashes from <paramref name="next"/> (<c>next(n)</c> = 0..n − 1); a new page starts its clock.
    /// </summary>
    public void Observe(RewardScene scene, int page, double now, RewardFlashLayout? layout, Func<int, int> next)
    {
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(next);
        if (!ReferenceEquals(scene, _scene) || !ReferenceEquals(layout, _layout))
        {
            _scene = scene;
            _layout = layout;
            if (layout?.Plan(next) is var (rising, fading))
                (_rising, _fading) = (rising, fading);
            else
                (_rising, _fading) = (null, null);
            _page = -1;
        }
        if (page != _page)
        {
            _page = page;
            _since = now;
        }
    }

    /// <summary>No reward scene shows: the next one starts afresh.</summary>
    public void Reset()
    {
        _scene = null;
        _layout = null;
        _page = -1;
    }

    /// <summary>The flash run of the shown page (null on the picture and speech pages, or with no flashes).</summary>
    public IReadOnlyList<RewardFlashLayout.Flash>? Run => _page switch
    {
        0 => _rising,
        1 => _fading,
        _ => null,
    };

    /// <summary>The flash shown at <paramref name="now"/> and how many of its frames are drawn, or null.</summary>
    public (RewardFlashLayout.Flash Flash, int Frames)? FlashAt(double now) =>
        _layout is { } layout && Run is { } run ? layout.At(run, Vbl(layout, now)) : null;

    /// <summary>The shown page's flash run has ended (false on a page without one: Return moves on there).</summary>
    public bool RunOver(double now) =>
        _layout is { } layout && Run is { } run && Vbl(layout, now) >= layout.RunVbl(run);

    /// <summary>VBLs since the page showed (a hair added so a whole number of VBLs in seconds lands on its VBL).</summary>
    private double Vbl(RewardFlashLayout layout, double now) => (now - _since) * layout.VblHz + 1e-6;
}

/// <summary>
/// The scene's text, one line at a time on the bottom line as the ST prints it (s98): the whole picture and the portrait
/// row stay in view, no box covers them. Return shows the next line of the page; on the last line it answers the prompt.
/// A new page (another prompt kind) starts at its first line.
/// </summary>
public sealed class DefeatScenePager
{
    private SessionPromptKind? _kind;
    private int _index;

    /// <summary>The line to show of <paramref name="lines"/> on the page <paramref name="kind"/> (null when it has none).</summary>
    public string? Current(SessionPromptKind kind, IReadOnlyList<string> lines)
    {
        Sync(kind);
        var shown = lines.Where(l => !string.IsNullOrWhiteSpace(l)).ToList();
        return shown.Count == 0 ? null : shown[Math.Min(_index, shown.Count - 1)];
    }

    /// <summary>Index of the shown line among the page's non-blank <paramref name="lines"/>.</summary>
    public int Index(SessionPromptKind kind, IReadOnlyList<string> lines)
    {
        Sync(kind);
        return Math.Min(_index, Math.Max(0, lines.Count(l => !string.IsNullOrWhiteSpace(l)) - 1));
    }

    /// <summary>More lines follow the one shown.</summary>
    public bool HasMore(SessionPromptKind kind, IReadOnlyList<string> lines)
    {
        Sync(kind);
        return _index < lines.Count(l => !string.IsNullOrWhiteSpace(l)) - 1;
    }

    /// <summary>
    /// Return on the page: true when it moved to the next line (the prompt stays), false when the last line shows and
    /// the answer goes through.
    /// </summary>
    public bool Advance(SessionPromptKind kind, IReadOnlyList<string> lines)
    {
        if (!HasMore(kind, lines))
            return false;
        _index++;
        return true;
    }

    /// <summary>No scene page shows: the next one starts at its first line.</summary>
    public void Reset() => _kind = null;

    private void Sync(SessionPromptKind kind)
    {
        if (_kind == kind)
            return;
        _kind = kind;
        _index = 0;
    }
}

/// <summary>
/// Times the door of <see cref="DefeatScene"/> from the prompts the front end shows: the sinking starts when the defeat box
/// gives way to the sign or the outcome; an outcome after the sign (or a scene met mid-way, after a load) finds it open.
/// </summary>
public sealed class DefeatSceneClock
{
    private SessionPromptKind? _last;
    private double _start = double.NaN;

    /// <summary>The frame for the prompt <paramref name="kind"/> shown at <paramref name="now"/> seconds.</summary>
    public DefeatSceneFrame Observe(SessionPromptKind kind, double now)
    {
        var previous = _last;
        _last = kind;
        switch (kind)
        {
            case SessionPromptKind.PartyDefeat:
                _start = double.NaN;
                return new DefeatSceneFrame(DefeatScene.DoorHeightClosed, false);
            case SessionPromptKind.DefeatDemonSign or SessionPromptKind.DefeatOutcome:
                if (previous == SessionPromptKind.PartyDefeat)
                    _start = now;
                else if (previous != kind)
                    _start = double.NaN; // sign → outcome: the door is open and the face shows at once
                if (double.IsNaN(_start))
                    return new DefeatSceneFrame(0, true);
                var t = now - _start;
                var h = DefeatScene.DoorHeightAt(t);
                var behind = h == 0 && (kind == SessionPromptKind.DefeatDemonSign
                                        || t >= DefeatScene.DoorSeconds + DefeatScene.FaceDelaySeconds);
                return new DefeatSceneFrame(h, behind);
            default:
                _start = double.NaN;
                return new DefeatSceneFrame(0, true);
        }
    }
}
