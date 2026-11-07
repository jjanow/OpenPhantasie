using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client;

/// <summary>
/// The title splash (s102): a game's start-up pictures as its original showed them (the pack's
/// <see cref="TitleSequence"/>), between the main-menu pick and the game's own first screen. A shell state like the main
/// menu. Each step stays for its <see cref="TitleStep.Seconds"/> (null = until a key or click); a key or click ends the
/// current step (as a key ends the Amiga I title tune), Esc ends the whole splash. s151: a step with a start menu (IV's
/// <c>_disk(0)</c> New Game / Saved Game panel, <see cref="TitleStep.Menu"/>) ends only on a pick (<see cref="Choose"/>);
/// Esc skips to it. Presentation switch
/// <see cref="GameSettings.TitleScreen"/> (on by default, as the originals).
/// </summary>
public sealed partial class TitleSplash
{
    private double _elapsed, _total;

    public TitleSplash(GameId game, TitleSequence sequence)
    {
        Game = game;
        Sequence = sequence ?? throw new ArgumentNullException(nameof(sequence));
    }

    public GameId Game { get; }
    public TitleSequence Sequence { get; }

    /// <summary>
    /// The pack's picture animations (IV, s110), or null: a step with an <see cref="TitleStep.Animation"/> plays that scene
    /// at its measured pass length.
    /// </summary>
    public PictureAnimationSet? Animations
    {
        get => _animations;
        init
        {
            _animations = value;
            Player = value is null ? null : new PictureAnimationPlayer(value.Table, new SystemRandomSource());
        }
    }

    private readonly PictureAnimationSet? _animations;

    /// <summary>The animation player (null without <see cref="Animations"/>).</summary>
    public PictureAnimationPlayer? Player { get; private init; }

    /// <summary>The step shown (index into <see cref="TitleSequence.Steps"/>); = their count once done.</summary>
    public int Step { get; private set; }

    public bool Done => Step >= Sequence.Steps.Count;

    public TitleStep? Current => Done ? null : Sequence.Steps[Step];

    /// <summary>
    /// One frame: <paramref name="seconds"/> passed; <paramref name="advance"/> = a key or click this frame (ends the step),
    /// <paramref name="skipAll"/> = Esc (ends the splash). Returns true once the splash is over.
    /// </summary>
    public bool Update(double seconds, bool advance, bool skipAll)
    {
        if (Done)
            return true;
        if (skipAll)
        {
            // s151: Esc skips the pictures up to a start menu (IV's _disk(0)), which waits for its pick; else the splash ends.
            if (CurrentMenu is not null)
                advance = false;
            else
            {
                var menu = Sequence.Steps.Select((t, i) => (t, i)).FirstOrDefault(p => p.i > Step && MenuOf(p.t) is not null);
                Step = menu.t is null ? Sequence.Steps.Count : menu.i;
                _elapsed = 0;
                if (Done)
                    return true;
                Animate(0);
                return false;
            }
        }
        _elapsed += Math.Max(0, seconds);
        _total += Math.Max(0, seconds);
        if (CurrentMenu is not null)
            advance = false; // only a pick ends a menu step (Choose)
        if (advance || (Current!.EndsAt is double end ? _total >= end : Current.Seconds is double hold && _elapsed >= hold))
        {
            Step++;
            _elapsed = 0;
        }
        Animate(Math.Max(0, seconds) * 1000);
        return Done;
    }

    /// <summary>The current step's animation (if any) armed and run for <paramref name="milliseconds"/>.</summary>
    private void Animate(double milliseconds)
    {
        if (Player is null || _animations is null)
            return;
        if (Current?.Animation is { } name && _animations.Scenes.TryGetValue(name, out var scene)
            && _animations.PassMillisecondsFor(scene) is double pass)
        {
            var fresh = Player.Scene != name;
            Player.Show(name, scene.First, scene.Last, pass);
            if (!fresh)
                Player.Advance(milliseconds); // the step's first frame only arms (_setanim draws nothing)
        }
        else
            Player.Hide();
    }

    /// <summary>
    /// Draw the current step on the splash canvas (canvas pixels, 1:1): the background, the picture at its own size,
    /// then the text lines centred on their x. A picture the pack lacks draws nothing (no borrowed art).
    /// </summary>
    public void Draw(SpriteBatch batch, Texture2D pixel, Func<string, Texture2D?> picture, Func<string, string> text)
    {
        if (Current is not { } step)
            return;
        batch.Draw(pixel, new Rectangle(0, 0, Sequence.Width, Sequence.Height), VirtualScreen.ParseColor(Sequence.Background));
        if (step.Picture is { } name && picture(name) is { } tex)
        {
            batch.Draw(tex, new Vector2(step.X, step.Y), Color.White);
            DrawAnimation(batch, step, picture);
        }
        foreach (var line in step.Texts)
        {
            var s = text(line.StringId);
            PixelFont.Draw(batch, pixel, line.CentreX - PixelFont.Measure(s) / 2, line.Y, s, VirtualScreen.ParseColor(line.Colour));
        }
    }
}

public sealed partial class TitleSplash
{
    /// <summary>The frames the animation copied so far, over the step's picture (the plain picture until the first).</summary>
    private void DrawAnimation(SpriteBatch batch, TitleStep step, Func<string, Texture2D?> picture)
    {
        if (Player?.Scene is not { } name || name != step.Animation || _animations is null
            || !_animations.Scenes.TryGetValue(name, out var scene) || picture(scene.Sheet) is not { } sheet)
            return;
        foreach (var (record, frame) in Player.Shown)
        {
            var r = _animations.Table.Records[record];
            var f = _animations.Table.Frames[frame];
            batch.Draw(sheet, new Vector2(step.X + scene.X + r.X, step.Y + scene.Y + r.Y),
                new Rectangle(f.SheetX, f.SheetY, r.Width, r.Height), Color.White);
        }
    }
}

/// <summary>
/// <c>--title-screen N</c> (1–4): open on game N's title splash whatever its Options switch says, then go on as if N had
/// been picked in the main menu. A developer switch for checking the
/// splash (and taking its screenshots), like <see cref="P4Preview"/>.
/// </summary>
public static class TitlePreview
{
    public const string Switch = "--title-screen";

    public static GameId? ParseArgs(IReadOnlyList<string> args, out string? error)
    {
        error = null;
        var i = args.ToList().IndexOf(Switch);
        if (i < 0)
            return null;
        if (i + 1 < args.Count && int.TryParse(args[i + 1], out var n) && n is >= 1 and <= 4)
            return (GameId)n;
        error = $"{Switch} needs a game 1-4.";
        return null;
    }
}
