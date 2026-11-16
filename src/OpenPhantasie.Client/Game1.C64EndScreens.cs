using Microsoft.Xna.Framework.Input;
using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client;

public partial class Game1
{
    private C64EndingScreen? _c64Ending;
    private C64EndingView? _c64EndingView;
    private double _c64EndingStart;
    private C64WinScreen? _c64Win;
    private C64WinView? _c64WinView;
    private double _c64WinStart;
    private C64DefeatScreen? _c64Defeat;
    private C64DefeatView? _c64DefeatView;
    private double _c64DefeatStart;

    /// <summary>s223: I / II's party wipe runs as the C64's <c>CEMETARY</c> (the pack's <c>c64_defeat</c> and its temple,
    /// ORIGINAL SCREENS on): it runs by itself, no key.</summary>
    private bool C64DefeatScreens => _session is { Game: GameId.P1 or GameId.P2 } && CurrentGameSettings.OriginalScreens
        && _assets?.Pack?.C64Defeat is { } layout && _assets.Get(layout.Temple) is not null;

    /// <summary>
    /// s223: the C64's own end screens, once a frame. I's victory with a pack whose <c>ending_scene</c> has <c>c64</c>
    /// (<see cref="C64EndingScreen"/>), s224 II's with <c>c64_win</c> (<see cref="C64WinScreen"/>); the party wipe under
    /// <see cref="C64DefeatScreens"/> (<see cref="C64DefeatScreen"/>). Each answers Core's prompt itself when it is over;
    /// the endings read a key only where the C64 does (I's closing page, II's wands prompt and closing page).
    /// </summary>
    private void UpdateC64EndScreens(double now)
    {
        var prompt = _session is not null && _splash is null ? CurrentPrompt : null;
        // The victory.
        if (prompt is { Kind: SessionPromptKind.VictoryScene } && _session!.PendingVictoryScene is { } scene
            && _assets?.Pack?.EndingScene is { C64: { } c64 } ending && _assets.Get(PictureNames.EndingFinal) is not null)
        {
            if (!ReferenceEquals(_c64Ending?.Scene, scene))
            {
                IReadOnlyList<string> speech = ending.SpeechKeys is { } keys && keys.Count == scene.SpeechLines.Count
                    ? [.. keys.Select(GameText.S)] : scene.SpeechLines;
                _c64Ending = new C64EndingScreen(c64, scene, speech, GameText.S(c64.Astral.Text), GameText.S(c64.Smiles.Text),
                    Random.Shared.NextDouble, [.. c64.ClosingLines.Select(GameText.S)],
                    c64.ClosingKey is { } closingKey ? GameText.S(closingKey.Text) : null);
                _c64EndingStart = now;
            }
            if (_c64Ending.At(now - _c64EndingStart).Done)
            {
                _c64Ending = null;
                AnswerPrompt(prompt, 1);
            }
        }
        else
            _c64Ending = null;
        // s224: II's victory (WIN).
        if (prompt is { Kind: SessionPromptKind.VictoryScene } && _session!.PendingVictoryScene is { } winScene
            && _assets?.Pack?.EndingScene is { C64Win: { } win } && _assets.Get(PictureNames.EndingFinal) is not null)
        {
            if (!ReferenceEquals(_c64Win?.Scene, winScene))
            {
                _c64Win = new C64WinScreen(win, winScene, GameText.S(win.TitleWord), GameText.S(win.TitleFrom),
                    GameText.S(win.CheerLine.Text), win.WandsKey is { } wandsKey ? GameText.S(wandsKey) : "");
                _c64WinStart = now;
            }
            if (_c64Win.At(now - _c64WinStart).Done)
            {
                _c64Win = null;
                AnswerPrompt(prompt, 1);
            }
        }
        else
            _c64Win = null;
        // The party wipe.
        if (prompt is null || !C64DefeatScreens
            || prompt.Kind is not (SessionPromptKind.PartyDefeat or SessionPromptKind.DefeatDemonSign or SessionPromptKind.DefeatOutcome))
        {
            _c64Defeat = null;
            return;
        }
        if (prompt.Kind == SessionPromptKind.PartyDefeat && (MapBoxesUp || MapWaitLines() is not null))
            return; // a step's boxes close first
        if (prompt.Kind is SessionPromptKind.PartyDefeat or SessionPromptKind.DefeatDemonSign)
        {
            AnswerPrompt(prompt, 1); // Core judges the members; the C64 shows no sign (II's 5th defeat)
            return;
        }
        if (_prompts.ShownDefeat is not { } defeat)
            return;
        if (!ReferenceEquals(_c64Defeat?.Defeat, defeat))
        {
            var layout = _assets!.Pack!.C64Defeat!;
            _c64Defeat = new C64DefeatScreen(layout, defeat, prompt.Lines, GameText.S(layout.Defeated.Text));
            _c64DefeatStart = now;
        }
        if (_c64Defeat.At(now - _c64DefeatStart).Done)
        {
            _c64Defeat = null;
            AnswerPrompt(prompt, 1);
        }
    }

    /// <summary>Draws the C64 ending or party wipe running for <paramref name="prompt"/>; true when drawn.</summary>
    private bool DrawC64EndScreens(SessionPrompt prompt)
    {
        if (_assets is null || _spriteBatch is null || _pixel is null || GraphicsDevice is null)
            return false;
        if (prompt.Kind == SessionPromptKind.VictoryScene && _c64Ending is { } ending
            && _assets.Pack?.EndingScene is { C64: not null } endingLayout)
        {
            _c64EndingView ??= new C64EndingView(GraphicsDevice, _assets, endingLayout);
            return _c64EndingView.Draw(_spriteBatch, _pixel, ending, ending.At(_updateSeconds - _c64EndingStart));
        }
        if (prompt.Kind == SessionPromptKind.VictoryScene && _c64Win is { } win
            && _assets.Pack?.EndingScene is { C64Win: not null } winLayout)
        {
            _c64WinView ??= new C64WinView(GraphicsDevice, _assets, winLayout);
            return _c64WinView.Draw(_spriteBatch, _pixel, win, win.At(_updateSeconds - _c64WinStart));
        }
        if (prompt.Kind == SessionPromptKind.DefeatOutcome && _c64Defeat is { } defeat
            && _assets.Pack?.C64Defeat is { } defeatLayout && _assets.Pack.Palette is { Count: 16 } palette)
        {
            _c64DefeatView ??= new C64DefeatView(GraphicsDevice, _assets, defeatLayout, palette);
            return _c64DefeatView.Draw(_spriteBatch, _pixel, defeat, defeat.At(_updateSeconds - _c64DefeatStart));
        }
        return false;
    }

    /// <summary>
    /// The C64's end screens read no key but where the C64 does (s224: I's closing page, II's wands prompt and closing page,
    /// a GET each): a key, a click or the pad's A there ends it; everything else is taken while one runs.
    /// </summary>
    private bool HandleC64EndScreenKeys(KeyboardState keys, GamePadState pad, SessionPrompt prompt)
    {
        if (prompt.Kind == SessionPromptKind.VictoryScene && (_c64Ending is not null || _c64Win is not null))
        {
            var t = _updateSeconds - (_c64Ending is not null ? _c64EndingStart : _c64WinStart);
            var waits = _c64Ending?.WaitsForKey(t) ?? _c64Win!.WaitsForKey(t);
            if (waits && (_input.NewKeys(keys).Any() || ScenePadEvents(pad).Presses > 0 || _mouse.LeftPressed))
            {
                _c64Ending?.Key(t);
                _c64Win?.Key(t);
            }
            return true;
        }
        return prompt.Kind == SessionPromptKind.DefeatOutcome && _c64Defeat is not null;
    }
}
