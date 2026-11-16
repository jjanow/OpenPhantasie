using Microsoft.Xna.Framework.Input;
using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client;

public partial class Game1
{
    private Apple2Scene? _a2Ending;
    private VictoryScene? _a2EndingFor;
    private Apple2Scene? _a2Defeat;
    private PartyDefeatResult? _a2DefeatFor;
    private Apple2SceneView? _a2View;
    private double _a2Last;
    private bool _a2Key;
    private byte[]? _a2Shapes;
    private byte[]? _a2Page2;
    private Apple2ScenesLayout? _a2ShapesFor;

    /// <summary>
    /// s224: I's Apple II end scenes run (<c>apple2_scenes</c>: <c>CEMETARY</c> / <c>FLASH?</c> replayed, GRAPHICS.md 8):
    /// I, ORIGINAL SCREENS on, the pack has the scenes and the import's shape table reads.
    /// </summary>
    private Apple2ScenesLayout? Apple2Scenes
    {
        get
        {
            if (_session is not { Game: GameId.P1 } || !CurrentGameSettings.OriginalScreens || _assets?.Pack?.Apple2Scenes is not { } layout)
                return null;
            if (!ReferenceEquals(_a2ShapesFor, layout))
            {
                _a2ShapesFor = layout;
                _a2Shapes = layout.TryLoadShapes();
                _a2Page2 = layout.TryLoadPage2();
            }
            return _a2Shapes is null ? null : layout;
        }
    }

    private bool Apple2DefeatScenes => Apple2Scenes is { Defeat.Count: > 0 };

    /// <summary>
    /// s224: the Apple's end scenes, once a frame. The victory (<see cref="SessionPromptKind.VictoryScene"/>) runs
    /// <c>FLASH?</c>'s steps with the party's own figures; the party wipe answers Core's first two prompts at once and runs
    /// <c>CEMETARY</c>'s steps on the outcome, Core's fates printed as the Apple prints its own. Each answers Core's
    /// prompt when its last statement has run. A key (or a click, or the pad's A / D-pad) is latched for the scene's GETs.
    /// </summary>
    private void UpdateApple2EndScenes(double now)
    {
        var dt = Math.Clamp(now - _a2Last, 0, 0.25);
        _a2Last = now;
        var prompt = _session is not null && _splash is null ? CurrentPrompt : null;
        var layout = prompt is null ? null : Apple2Scenes;
        // The victory.
        if (layout is { Ending.Count: > 0 } && prompt is { Kind: SessionPromptKind.VictoryScene }
            && _session!.PendingVictoryScene is { } scene)
        {
            if (!ReferenceEquals(_a2EndingFor, scene))
            {
                _a2EndingFor = scene;
                var members = _session.Party.Adventurers
                    .Select(m => new Apple2SceneMember(m.Name, layout.Bodies.For((int)m.BirthRace, (int)m.Class))).ToList();
                _a2Ending = new Apple2Scene(layout.Ending, _a2Shapes!, members, GameText.S, page2: _a2Page2);
                _a2Key = false;
                dt = 0;
            }
            RunApple2Scene(_a2Ending!, layout, dt);
            if (_a2Ending!.Done)
            {
                _a2Ending = null;
                AnswerPrompt(prompt, 1);
            }
            return;
        }
        _a2Ending = null;
        _a2EndingFor = null;
        // The party wipe.
        if (layout is not { Defeat.Count: > 0 }
            || prompt!.Kind is not (SessionPromptKind.PartyDefeat or SessionPromptKind.DefeatDemonSign or SessionPromptKind.DefeatOutcome))
        {
            _a2Defeat = null;
            _a2DefeatFor = null;
            return;
        }
        if (prompt.Kind == SessionPromptKind.PartyDefeat && (MapBoxesUp || MapWaitLines() is not null))
            return; // a step's boxes close first
        if (prompt.Kind is SessionPromptKind.PartyDefeat or SessionPromptKind.DefeatDemonSign)
        {
            AnswerPrompt(prompt, 1); // Core judges the members; CEMETARY shows no sign
            return;
        }
        if (_prompts.ShownDefeat is not { } defeat)
            return;
        if (!ReferenceEquals(_a2DefeatFor, defeat))
        {
            _a2DefeatFor = defeat;
            var members = defeat.Fates.Select(f => new Apple2SceneMember(f.Member.Name, 19)).ToList();
            var fates = defeat.Fates.Select(f => f.Fate switch
            {
                DefeatFate.Resurrected => Apple2SceneFate.Resurrected,
                DefeatFate.Undead => Apple2SceneFate.Undead,
                _ => Apple2SceneFate.Destroyed,
            }).ToList();
            _a2Defeat = new Apple2Scene(layout.Defeat, _a2Shapes!, members, GameText.S, fates, _a2Page2);
            _a2Key = false;
            dt = 0;
        }
        RunApple2Scene(_a2Defeat!, layout, dt);
        if (_a2Defeat!.Done)
        {
            _a2Defeat = null;
            AnswerPrompt(prompt, 1);
        }
    }

    private void RunApple2Scene(Apple2Scene scene, Apple2ScenesLayout layout, double dt)
    {
        scene.Advance(dt, ref _a2Key);
        foreach (var name in scene.TakeSounds())
            if (_settings.Sound && layout.Sounds.TryGetValue(name, out var sound))
                _effects.Play(sound, layout.ClockHz);
    }

    /// <summary>Draws the Apple's ending or party wipe running for <paramref name="prompt"/>; true when drawn.</summary>
    private bool DrawApple2EndScenes(SessionPrompt prompt)
    {
        if (_spriteBatch is null || _pixel is null || GraphicsDevice is null)
            return false;
        var scene = prompt.Kind == SessionPromptKind.VictoryScene ? _a2Ending
            : prompt.Kind == SessionPromptKind.DefeatOutcome ? _a2Defeat : null;
        if (scene is null)
            return false;
        _a2View ??= new Apple2SceneView(GraphicsDevice);
        return _a2View.Draw(_spriteBatch, _pixel, scene);
    }

    /// <summary>While an Apple scene runs, keys, clicks and the pad's A / D-pad are latched for its GETs, nothing else.</summary>
    private bool HandleApple2EndSceneKeys(KeyboardState keys, GamePadState pad, SessionPrompt prompt)
    {
        var running = prompt.Kind == SessionPromptKind.VictoryScene && _a2Ending is not null
                      || prompt.Kind == SessionPromptKind.DefeatOutcome && _a2Defeat is not null;
        if (!running)
            return false;
        if (_input.NewKeys(keys).Any() || _mouse.LeftPressed && _mouse.Position is not null || ScenePadEvents(pad).Presses > 0)
            _a2Key = true;
        return true;
    }
}
