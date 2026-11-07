using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client;

/// <summary>
/// s151: <c>_tload</c>'s "Welcome!" (P4.X <c>0x2BB4</c>–<c>0x2C1E</c>, FORMAT.md 6.10, the pack's
/// <see cref="PictureAnimationSet.Welcome"/>): before each street load (every arrival in a town, a load in town, the
/// ending's street) the street window is cleared to colour 1 and "Welcome!" written in font 0 italic, colour 15, centred
/// on (156, 81); the street picture, read and decoded meanwhile, then covers it. The remaster has no load to wait for,
/// so it keeps the text up for the time XEiJ measured (read + decode) `[~]`, reading no keys (the original reads none
/// while it loads), then shows the street at once (the picture's copy is not animated `[~]`).
/// </summary>
public sealed partial class P4DungeonScreen
{
    private P4TownLoop? _welcomeLoop;
    private int _welcomeLoads;
    private double _welcomeLeft;
    private double _pleaseWaitLeft;

    /// <summary>True while "Please Wait..." or "Welcome!" stands in for the street picture.</summary>
    public bool WelcomeUp => _welcomeLeft > 0 || _pleaseWaitLeft > 0;

    /// <summary>True while "Please Wait..." (s156) is up, before "Welcome!".</summary>
    public bool PleaseWaitUp => _pleaseWaitLeft > 0;

    /// <summary>
    /// s156: how long "Please Wait..." stays: the measured town load (its animation sheet read, decoded and copied, then
    /// <c>_wcls</c>), or only the <c>_wcls</c> part for the ending's <c>_tload(1)</c>, which loads no sheet `[~]`.
    /// </summary>
    public static double PleaseWaitHold(PicturePleaseWait? wait, bool ending) =>
        wait is null ? 0 : ending ? wait.HoldNoSheetMilliseconds : wait.HoldMilliseconds;

    /// <summary>
    /// Whether a frame starts the hold: a new town loop (a new game or a load) with the street already up (the original's
    /// <c>_town</c> ran <c>_tload</c>); else the loop ran <c>_tload</c> since the last frame (<see cref="P4TownLoop.Loads"/>).
    /// A new loop still at an entry prompt (shares, aging, sharing out items) waits for its own load instead.
    /// </summary>
    public static bool WelcomeStarts(bool newLoop, bool streetUp, bool loadedSinceLastFrame) =>
        newLoop ? streetUp : loadedSinceLastFrame;

    /// <summary>Starts or runs down the hold (<see cref="WelcomeStarts"/>).</summary>
    private void TickWelcome(double milliseconds)
    {
        var hold = Assets?.Pack?.PictureAnimations?.Welcome?.HoldMilliseconds ?? 0;
        var newLoop = !ReferenceEquals(W, _welcomeLoop);
        var loaded = W.Loads != _welcomeLoads;
        _welcomeLoop = W;
        _welcomeLoads = W.Loads;
        if (newLoop || loaded)
        {
            var starts = WelcomeStarts(newLoop, BannerScene, loaded);
            _welcomeLeft = starts ? hold : 0;
            _pleaseWaitLeft = starts ? PleaseWaitHold(Assets?.Pack?.PictureAnimations?.Welcome?.PleaseWait,
                W.Scene == P4TownScene.EndingStreet) : 0;
            return;
        }
        if (_pleaseWaitLeft > 0)
            _pleaseWaitLeft = Math.Max(0, _pleaseWaitLeft - milliseconds);
        else if (_welcomeLeft > 0)
            _welcomeLeft = Math.Max(0, _welcomeLeft - milliseconds);
    }

    /// <summary>
    /// s156: <c>_tload</c>'s "Please Wait..." over the street's area (<paramref name="width"/> × <paramref name="height"/>
    /// pack pixels): the window grey, <c>_whole</c>'s sunken box and the text in font 0 centred as <c>_wprcen</c> does.
    /// </summary>
    private void DrawPleaseWait(SpriteBatch batch, Texture2D pixel, Vector2 origin, float k, int width, int height,
        PicturePleaseWait p)
    {
        void Box(float x, float y, float w, float h, int colour, Color fallback) =>
            batch.Draw(pixel, origin + new Vector2(x * k, y * k), null, Assets!.PaletteColour("pkh", colour) ?? fallback, 0f,
                Vector2.Zero, new Vector2(w * k, h * k), SpriteEffects.None, 0f);
        Box(0, 0, width, height, p.Back, Color.LightGray);
        // The XEiJ shot's corners: the dark rows / columns run to the outer corners, one pixel shorter on the inner ones.
        int x0 = p.BoxX - 2, y0 = p.BoxY - 2, w = p.BoxWidth + 4, h = p.BoxHeight + 4;
        Box(x0, y0 + h - 2, w, 2, p.Light, Color.White);
        Box(x0 + w - 2, y0, 2, h, p.Light, Color.White);
        Box(x0, y0, w, 1, p.Dark, Color.DimGray);
        Box(x0, y0 + 1, w - 1, 1, p.Dark, Color.DimGray);
        Box(x0, y0, 1, h, p.Dark, Color.DimGray);
        Box(x0 + 1, y0, 1, h - 1, p.Dark, Color.DimGray);
        var text = GameText.S("p4x.0x3fa38");
        PackText(batch, pixel, origin + new Vector2((p.X - (PackWidth(text, k) >> 1)) * k, p.Y * k), text,
            Assets!.PaletteColour("pkh", p.Colour) ?? Color.Black, k);
    }

    /// <summary>
    /// The street window as <c>_tload</c> leaves it while the picture loads: <paramref name="width"/> ×
    /// <paramref name="height"/> pack pixels of colour <see cref="PictureWelcome.Back"/> with "Welcome!" (the pack's italic
    /// font 0, <see cref="ExitFontRole"/>) centred on its x by the plain width, as <c>_wprcen</c> does.
    /// </summary>
    private void DrawWelcome(SpriteBatch batch, Texture2D pixel, Vector2 origin, float k, int width, int height)
    {
        if (Assets?.Pack?.PictureAnimations?.Welcome is not { } w)
            return;
        batch.Draw(pixel, origin, null, Assets.PaletteColour("pkh", w.Back) ?? Color.DimGray, 0f, Vector2.Zero,
            new Vector2(width * k, height * k), SpriteEffects.None, 0f);
        var text = GameText.S("p4x.0x3fa47");
        var colour = Assets.PaletteColour("pkh", w.Colour) ?? Color.White;
        if (FontSources.ForRole(Assets.Pack, ExitFontRole) is { } font)
            PixelFont.DrawScaled(batch, pixel, origin + new Vector2((w.X - (PixelFont.Measure(text, font) >> 1)) * k, w.Y * k),
                text, colour, k, font);
        else
            PixelFont.Draw(batch, pixel, (int)Math.Round(origin.X + w.X * k - PixelFont.Measure(text) / 2f),
                (int)Math.Round(origin.Y + w.Y * k), text, colour);
    }
}
