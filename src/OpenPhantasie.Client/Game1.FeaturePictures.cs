using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using OpenPhantasie.Core;

namespace OpenPhantasie.Client;

/// <summary>
/// s221: the art pack's picture beside a moment's text (<see cref="FeaturePictureSlots"/>; C64 I / II's found-treasure and
/// dungeon-feature pictures). A question's picture is drawn in its panel (<see cref="UiOverlays.DrawPrompt"/>); a dungeon
/// step's own line (the stairs, a locked door) gets a box next to the line while that line is up: I's DOS band (a page)
/// or the message line. A pack without the pictures draws nothing and nothing moves.
/// </summary>
public partial class Game1
{
    /// <summary>The last dungeon step's line and its picture name, or null.</summary>
    private (string Line, string Picture)? _stepPicture;

    /// <summary>After a dungeon step: remember its line's picture, if the moment has one (else forget the last one).</summary>
    private void NoteStepPicture(DungeonMoveResult result) =>
        _stepPicture = FeaturePictureSlots.ForStep(result.Picture) is { } name && result.Message is { } line
            ? (line.Trim(), name) : null;

    /// <summary>The picture for <paramref name="prompt"/> in the running pack, or null.</summary>
    private Texture2D? PromptPicture(SessionPrompt prompt) =>
        _assets?.Pack is { } pack
        && FeaturePictureSlots.ForPrompt(prompt.Kind, _session?.PendingTreasure.Count > 0 ? _session.PendingTreasure[0] : null,
            pack) is { } name
            ? _assets.Get(name) : null;

    /// <summary>The step line's picture when its line is the one on <paramref name="rows"/>, or null.</summary>
    private Texture2D? StepPictureOn(IEnumerable<string> rows) =>
        _session is { Mode: GameMode.Dungeon } && _stepPicture is { } step && rows.Any(r => r.Trim() == step.Line)
            ? _assets?.Get(step.Picture) : null;

    /// <summary>A step line's picture next to the DOS band <paramref name="layout"/> (8 × 8 cells).</summary>
    private void DrawStepPictureBeside(P1DosPageLayout layout, IReadOnlyList<string> rows)
    {
        if (StepPictureOn(rows) is not { } picture)
            return;
        var box = FeaturePictureSlots.BoxBeside(picture.Width, picture.Height,
            (layout.Left * 8, layout.Top * 8, (layout.Right - layout.Left + 1) * 8, (layout.Bottom - layout.Top + 1) * 8), centred: true);
        UiOverlays.DrawPictureBox(_spriteBatch!, _pixel!, picture, box);
    }

    /// <summary>
    /// A step line's picture over the help band while the line is on the message line and nothing covers the map (no
    /// question, page, dialog or sheet).
    /// </summary>
    private void DrawStepPictureOnMessageLine()
    {
        if (CurrentPrompt is not null || DosPagesUp || OpenModal is not null || _dosDialog.IsOpen || _showInventory || _showBestiary
            || StepPictureOn([BandMessage]) is not { } picture)
            return;
        var box = FeaturePictureSlots.BoxBeside(picture.Width, picture.Height, (4, UiOverlays.MessageY, 312, 8), centred: false);
        UiOverlays.DrawPictureBox(_spriteBatch!, _pixel!, picture, box);
    }
}
