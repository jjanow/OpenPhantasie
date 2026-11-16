using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client;

/// <summary>
/// s221: which art pack picture goes beside a moment's text (C64 I / II's found-treasure and dungeon-feature pictures,
/// <c>reverse-engineering/c64/phantasie1/GRAPHICS.md</c> 7). Only the names: a pack that lacks them (every pack but the C64
/// ones) resolves to nothing, and the Client then draws its text as always. It follows the art pack, never the layout
/// switch. No MonoGame types.
/// </summary>
public static class FeaturePictureSlots
{
    /// <summary>
    /// The picture for a question: a found item's (<see cref="SessionPromptKind.Treasure"/>, by the pack's
    /// <c>find_pictures</c> for <paramref name="pendingItem"/>), a spotted trap's, an exit's; null for any other.
    /// </summary>
    public static string? ForPrompt(SessionPromptKind kind, int? pendingItem, GraphicsPack? pack) => kind switch
    {
        SessionPromptKind.Treasure when pendingItem is int id => pack?.FindPicture(id),
        SessionPromptKind.DungeonTrap => PictureNames.EventTrap,
        SessionPromptKind.DungeonExitTakeIt => PictureNames.EventExit,
        _ => null,
    };

    /// <summary>
    /// The picture for a dungeon step's own line (no question follows it): the stairs, a locked door's pick. The trap and
    /// the exit are questions (<see cref="ForPrompt"/>).
    /// </summary>
    public static string? ForStep(FeaturePicture picture) => picture switch
    {
        FeaturePicture.Stairs => PictureNames.EventStairs,
        FeaturePicture.Lock => PictureNames.EventLock,
        _ => null,
    };

    /// <summary>
    /// Where a step line's picture box goes (virtual 320 × 200): <paramref name="width"/> × <paramref name="height"/> plus
    /// <see cref="Pad"/> round it, next to <paramref name="text"/> (the band or line the text is on): under it when it fits
    /// above the help band (<see cref="UiOverlays.ContentBottom"/>), else over it, ending at the help band; centred on the
    /// text when <paramref name="centred"/>, else from its left edge; kept on the screen. Nothing is scaled or cut.
    /// </summary>
    public static (int X, int Y, int W, int H) BoxBeside(int width, int height, (int X, int Y, int W, int H) text, bool centred)
    {
        int w = width + 2 * Pad, h = height + 2 * Pad;
        var below = text.Y + text.H + 2;
        var y = below + h <= UiOverlays.ContentBottom ? below : Math.Min(text.Y - 2, UiOverlays.ContentBottom) - h;
        y = Math.Max(0, y);
        var x = centred ? text.X + (text.W - w) / 2 : text.X;
        x = Math.Clamp(x, 0, Math.Max(0, 320 - w));
        return (x, y, w, h);
    }

    /// <summary>The black margin round a step line's picture in its box.</summary>
    public const int Pad = 4;
}
