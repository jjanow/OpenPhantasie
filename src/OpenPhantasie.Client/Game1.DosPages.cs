using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client;

public partial class Game1
{
    // s154: I's DOS path (AMIGA SCREENS off, or a pack without the Amiga's map requesters): a step's lines paged as DOS's
    // boxes, each waiting for a key (P1DosMapPages); a fight Core started waits for the last page. The party defeat runs by
    // itself on DOS's 5.00-s line clock (P1DosDefeatClock). Data: GameData/phantasie1/dos-screens.json.
    private P1DosMapPages? _dosPages;
    private int _dosPage;
    private bool _dosPageArmed;
    private long _dosPagesFightMark;
    private IReadOnlySet<string> _dosPaged = new HashSet<string>();
    private (GameId Game, DosScreens? Screens)? _dosScreens;
    private string? _rowLineBeforeStep; // s156: the message line before a map step (DOS's row 24 under its boxes)

    /// <summary>The running game's <c>dos-screens.json</c>, or null.</summary>
    private DosScreens? DosScreensData
    {
        get
        {
            if (_session is not { } s)
                return null;
            if (_dosScreens is not { } cached || cached.Game != s.Game)
            {
                DosScreens? loaded = null;
                try
                {
                    loaded = DosScreens.TryLoad(GameDataRepository.DefaultPathFor(s.Game));
                }
                catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
                {
                }
                _dosScreens = (s.Game, loaded);
            }
            return _dosScreens.Value.Screens;
        }
    }

    /// <summary>True while a DOS page is up.</summary>
    private bool DosPagesUp => _dosPages is not null;

    /// <summary>The rows of the DOS page up now, or null.</summary>
    private IReadOnlyList<string>? DosPageRows() =>
        _dosPages is { } pages && _dosPage < pages.Pages.Count ? pages.Pages[_dosPage].Rows : null;

    /// <summary>s156: the dungeon cell under the page up now when Core has moved the party since (op 12, a jammed lock).</summary>
    private P1MapMessageRun.PartyCell? DosPagePartyAt() =>
        _dosPages is { } pages && _dosPage < pages.Pages.Count ? pages.Pages[_dosPage].PartyAt : null;

    /// <summary>s156: the page up now at DOS's places, or null (the remaster's panel then).</summary>
    private P1DosPageLayout? DosPageLayout() =>
        _dosPages is { } pages && _dosPage < pages.Pages.Count && DosScreensData?.Pages is { } places
            ? P1DosPageLayout.For(places, pages.Pages[_dosPage]) : null;

    /// <summary>
    /// After a step or an answer on I's DOS path (the Amiga run did not plan): the step's DOS boxes as pages. True when pages
    /// were planned (the caller leaves the fight's start to them).
    /// </summary>
    private bool PlanDosPages(GameSession s, long mark)
    {
        _dosPaged = new HashSet<string>();
        if (s.Game != GameId.P1 || _combatUi.IsPresenting || DosScreensData?.MapBoxes is not { } boxes)
            return false;
        if (CurrentGameSettings.OriginalScreens && _assets?.Pack?.MapMessage is not null)
            return false; // the Amiga's requesters (P1MapMessageRun) own the map's texts on that path

        if (P1DosMapPages.ForStep(boxes, s, mark, _stepMoveMark) is not { } pages)
            return false;
        _dosPages = pages;
        _dosPage = 0;
        _dosPageArmed = false;
        _dosPagesFightMark = mark;
        _dosPaged = pages.PagedLines;
        // The message line keeps the lines on no page (DOS's row 24); the pages show the rest, once. A step whose lines are
        // all paged keeps the row-24 line from before it (s156).
        SetFeedback(pages.MessageLine(_rowLineBeforeStep), UiText.S("game.event_page_any_key"));
        if (_promptNoteLines.Count > 0)
            _promptNote = string.Join(" ", _promptNoteLines.Where(n => !pages.PagedLines.Contains(n.Trim())));
        _defeatCause = new(); // the report was paged: the defeat box does not repeat it
        _walk = null;
        return true;
    }

    /// <summary>
    /// While DOS pages are up they own every key, click and pad button: <paramref name="pressed"/> (one that came after the
    /// page opened) turns the page; after the last, a fight Core already started gets its screen. True while they own the frame.
    /// </summary>
    private bool UpdateDosPages(GameSession s, bool pressed)
    {
        if (_dosPages is not { } pages)
            return false;
        if (!_dosPageArmed)
        {
            _dosPageArmed = true; // the press that turns the page must come after it opened
            return true;
        }
        if (!pressed)
            return true;
        if (++_dosPage < pages.Pages.Count)
            return true;
        _dosPages = null;
        if (_help == UiText.S("game.event_page_any_key"))
            SetFeedback(_message, HelpForMode(s.Mode)); // the pages' help leaves with them
        if (s.Mode == GameMode.Combat && !_combatUi.IsPresenting)
        {
            NoteEncounterMonsters(s);
            _fightMark = _dosPagesFightMark;
            _combatUi.Start(s);
        }
        return true;
    }

    /// <summary>Drops the pages (a load, the main menu).</summary>
    private void CloseDosPages()
    {
        _dosPages = null;
        _dosPaged = new HashSet<string>();
        _p1DosDefeat = null;
    }

    /// <summary>
    /// s154: I DOS's "LEAVING %s" (OV1 <c>0x483e</c>, called by <c>0x1e15</c> when the town loop returns: row 24 cleared,
    /// the town record's name (<c>[0x6368] + 0x117</c>) printed at column 11, then the auto-save when it is on). The line
    /// for I's DOS path (not the Amiga's screens, which draw their own), or null.
    /// </summary>
    private string? P1DosLeavingLine(GameSession s, string town) =>
        s.Game == GameId.P1 && !(CurrentGameSettings.OriginalScreens && _assets?.Pack?.MapMessage is not null)
            ? UiText.F("game.dos_leaving_x", town) : null;

    // --- The party defeat (endgame.md 4 "Timing") ---

    private P1DosDefeatClock? _p1DosDefeat;
    private ScreenPalette? _dosDefeatPalette;
    private IReadOnlyList<string>? _dosDefeatPaletteFor;

    /// <summary>Whether I's defeat runs by DOS's clock: I, not the Amiga's screens, and the data names the timing.</summary>
    private bool P1DosDefeatTimed => _session is { Game: GameId.P1 } && !P1DefeatScreens && DosScreensData?.Defeat is not null;

    /// <summary>
    /// s154: once a frame on I's DOS path: the defeat box and the outcome's lines each hold the data's line time (5.00 s),
    /// then the prompt is answered by itself; a key ends the current line (<see cref="HandleP1DosDefeatKeys"/>).
    /// </summary>
    private void UpdateP1DosDefeat(double now)
    {
        var prompt = _session is not null && _splash is null ? CurrentPrompt : null;
        if (prompt is null || !P1DosDefeatTimed || DosPagesUp
            || prompt.Kind is not (SessionPromptKind.PartyDefeat or SessionPromptKind.DefeatOutcome))
        {
            _p1DosDefeat = null;
            return;
        }
        var lines = Shown(prompt).Lines;
        if (_p1DosDefeat is null || _p1DosDefeat.Kind != prompt.Kind)
            _p1DosDefeat = new P1DosDefeatClock(prompt.Kind, StepsFor(prompt, lines), DosScreensData!.Defeat!, now);
        if (_p1DosDefeat.Done(now))
        {
            _p1DosDefeat = null;
            AnswerPrompt(prompt, 1);
        }
    }

    /// <summary>
    /// The timed steps of a defeat prompt: the defeat box is one line on DOS (YOUR PARTY HAS BEEN DEFEATED; the remaster's
    /// report above it shows with it); the outcome is one step per line (ASTRAL PLANE, the demon, each fate).
    /// </summary>
    private static int StepsFor(SessionPrompt prompt, IReadOnlyList<string> lines) =>
        prompt.Kind == SessionPromptKind.PartyDefeat ? 1 : Math.Max(1, lines.Count);

    /// <summary>A key, click or pad button ends the current defeat line when DOS lets it (<c>0x3bf8</c>); true when taken.</summary>
    private bool HandleP1DosDefeatKeys(KeyboardState keys, SessionPrompt prompt)
    {
        if (_p1DosDefeat is not { } clock || clock.Kind != prompt.Kind)
            return false;
        if (_input.NewKeys(keys).Any() || _mouse.AnyPressed)
            clock.Key(_updateSeconds);
        return true;
    }

    /// <summary>
    /// s157: the art the outcome draws on DOS's clock, by the pack (user: the pictures follow the art pack, not the
    /// layout): the CGA pack's own DOS pictures, the Amiga pack's defeat pictures, or none (the text panel).
    /// </summary>
    private DosDefeatArt P1DosDefeatArt => _assets is not { } a ? DosDefeatArt.None
        : P1DosDefeatScene.ArtFor(a.Pack?.DefeatPictures is not null && a.Get(PictureNames.DefeatTemple) is not null,
            a.Get(PictureNames.DefeatDemon) is not null, a.Get(PictureNames.DefeatGhost) is not null,
            a.Decor is not null, a.DecorDoor is not null, a.DecorDemonFace is not null, a.PortraitGhost is not null);

    /// <summary>
    /// s157: the outcome with pictures on DOS's clock (<see cref="P1DosDefeatScene"/>): one line at a time on the bottom
    /// row over the temple, the face from the demon's line on, each fate before its line. The Amiga pack's door is shut
    /// on the Astral Plane line and gone with the face on the demon's line (DOS's beats; the Amiga's 7-s sink has no
    /// place on a 5-s line `[~]`). False when the pack has neither set (the text panel then).
    /// </summary>
    private bool DrawP1DosDefeatPictures(SessionPrompt prompt, P1DosDefeatClock clock, IReadOnlyList<string> lines)
    {
        if (prompt.Kind != SessionPromptKind.DefeatOutcome || _prompts.ShownDefeat is not { } defeat || _assets is null)
            return false;
        var art = P1DosDefeatArt;
        if (art == DosDefeatArt.None)
            return false;
        var frame = P1DosDefeatScene.At(clock.Index(_updateSeconds), lines, defeat.Fates.Count);
        var row = P1DosDefeatScene.Row(defeat, frame.Fates);
        if (art == DosDefeatArt.Dos)
        {
            var pictures = _assets.Pack!.DefeatPictures!;
            if (_screen is not null && pictures.Palette is { } screenPens)
            {
                if (!ReferenceEquals(_dosDefeatPaletteFor, screenPens))
                    (_dosDefeatPalette, _dosDefeatPaletteFor) = (ScreenPalette.From(screenPens), screenPens);
                _screen.PaletteOverride = _dosDefeatPalette; // OV1 0x2c7: CGA palette 0 for these screens
            }
            return UiOverlays.DrawDosDefeatScene(_spriteBatch, _pixel, _assets, pictures, frame, row);
        }
        var layout = _assets.Pack?.DefeatScreen;
        Color? ink = layout is not null && _assets.Pack!.EndingScene?.Palette is { Count: 16 } pens
            ? VirtualScreen.ParseColor(pens[layout.TextPen]!) : null;
        return UiOverlays.DrawDefeatScene(_spriteBatch, _pixel, _assets, SessionPromptKind.DefeatOutcome,
            P1DosDefeatScene.AmigaDoor(frame), row, frame.Line,
            awaitsReturn: false, lineColour: ink, centredTop: layout?.TextY ?? DefeatScene.TextTop + 1, clickIsReturn: false);
    }

    /// <summary>Draws the defeat prompt's lines shown so far (no "press Enter": DOS asks for no key); true when drawn.</summary>
    private bool DrawP1DosDefeat(SessionPrompt prompt)
    {
        if (_p1DosDefeat is not { } clock || clock.Kind != prompt.Kind)
            return false;
        var lines = Shown(prompt).Lines;
        if (DrawP1DosDefeatPictures(prompt, clock, lines))
        {
            UiOverlays.Hits?.Clear(); // a click ends the line (HandleP1DosDefeatKeys), it picks nothing
            return true;
        }
        var shown = prompt.Kind == SessionPromptKind.PartyDefeat ? lines.Count : clock.LinesShown(_updateSeconds, lines.Count);
        UiOverlays.DrawTextPanel(_spriteBatch, _pixel, [.. lines.Take(shown)]);
        UiOverlays.Hits?.Clear(); // a click ends the line (HandleP1DosDefeatKeys), it picks nothing
        return true;
    }
}
