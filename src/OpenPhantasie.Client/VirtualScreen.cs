using System.Globalization;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client;

/// <summary>
/// Integer-scale virtual framebuffer with letterboxing into a resizable window. Its size, pixel shape, background and
/// palette come from the active graphics pack (<see cref="ScreenSpec"/>, T1.5; the remaster default is 320×200, square
/// pixels, no palette). A non-square pixel aspect stretches the picture vertically (or horizontally) when it is
/// presented. Everything is drawn in the one shared remaster layout (<see cref="LayoutWidth"/> × <see cref="LayoutHeight"/>,
/// user decision: no per-platform layouts); <see cref="LayoutTransform"/> maps it onto a pack of another resolution, and
/// <see cref="WindowToVirtual(Point,int,int)"/> maps the mouse back into layout coordinates.
/// </summary>
public sealed class VirtualScreen : IDisposable
{
    private readonly GraphicsDevice _device;
    private RenderTarget2D _target;
    private int _scale;
    private readonly ScreenPalette? _palette;
    private Texture2D? _paletted;
    private Color[]? _frame;

    /// <summary>The shared remaster layout every screen is authored in (layout pixels).</summary>
    public const int LayoutWidth = ClientSettings.VirtualWidth;
    public const int LayoutHeight = ClientSettings.VirtualHeight;

    public int VirtualWidth { get; }
    public int VirtualHeight { get; }
    /// <summary>Width : height of one virtual pixel (1:1 = square).</summary>
    public int PixelAspectX { get; }
    public int PixelAspectY { get; }
    public Color Background { get; }
    public int Scale => _scale;

    /// <summary>
    /// User s152: present at the largest fractional scale that fits (aspect kept, centred, point sampled) instead of the
    /// largest integer one. Set for the title splash's canvas only (IV's 506 × 347 would drop to 2× on 1032 rows);
    /// in-game screens keep integer scaling.
    /// </summary>
    public bool FractionalFit { get; init; }
    public RenderTarget2D Target => _target;
    /// <summary>The pack's palette applied to each frame, or null (the frame is presented as drawn).</summary>
    public ScreenPalette? Palette => _palette;

    /// <summary>
    /// s157: a palette the current screen sets in place of <see cref="Palette"/> for the frames it shows (CGA's palette 0
    /// on I DOS's defeat screens, OV1 <c>0x2c7</c>), or null. Only replaces a pack palette, never adds one.
    /// </summary>
    public ScreenPalette? PaletteOverride { get; set; }

    private ScreenPalette? FramePalette => _palette is null ? null : PaletteOverride ?? _palette;

    /// <summary>
    /// The colorblind palette (QoL <see cref="GameSettings.ColorblindPalette"/>), applied after <see cref="Palette"/>; null
    /// = off, and the frame is presented as drawn.
    /// </summary>
    public ColorblindFilter? Colorblind { get; set; }

    /// <summary>s140: II ST's TRANSPORTATION fade while it runs (applied before <see cref="Colorblind"/>), else null.</summary>
    public TransportFadeFilter? Fade { get; set; }

    private Color[]? _frozen;

    /// <summary>
    /// s158: true while a frozen frame (<see cref="Freeze"/>) is shown in place of what is drawn now: II ST's
    /// TRANSPORTATION fades the old town's screen, picker box included (<c>0x25b4..0x25da</c>: the picker stays drawn,
    /// <c>0x19158</c> only clears the button's SELECTED bit in memory), while Core has already moved the party.
    /// </summary>
    public bool Frozen => _frozen is not null;

    /// <summary>
    /// s158: keep the frame on screen now (the last one drawn: call it from Update, before this frame's Draw clears the
    /// target) and present it, through the palette / fade / colorblind filters, until <see cref="Unfreeze"/>.
    /// </summary>
    public void Freeze()
    {
        _frozen ??= new Color[VirtualWidth * VirtualHeight];
        _target.GetData(_frozen);
    }

    /// <summary>s158: present what is drawn again.</summary>
    public void Unfreeze() => _frozen = null;

    /// <summary>
    /// Layout → screen transform for <c>SpriteBatch.Begin</c>: null when the pack's resolution is the layout's (the
    /// remaster default, drawn exactly as before), else a scale by <see cref="LayoutScale(ScreenSpec)"/>.
    /// </summary>
    public Matrix? LayoutTransform { get; }

    /// <summary>Virtual pixels per layout pixel, horizontally and vertically (1, 1 for a 320×200 pack).</summary>
    public static (float X, float Y) LayoutScale(ScreenSpec spec) =>
        ((float)spec.Width / LayoutWidth, (float)spec.Height / LayoutHeight);

    /// <summary>The layout transform for <paramref name="spec"/> (null = identity: no transform at all).</summary>
    public static Matrix? LayoutTransformFor(ScreenSpec spec) =>
        spec.Width == LayoutWidth && spec.Height == LayoutHeight
            ? null
            : Matrix.CreateScale(LayoutScale(spec).X, LayoutScale(spec).Y, 1f);

    /// <summary>Begin a layout-space batch on this screen (point sampling, the pack's layout transform).</summary>
    public void BeginLayout(SpriteBatch spriteBatch) =>
        spriteBatch.Begin(samplerState: SamplerState.PointClamp, transformMatrix: LayoutTransform);

    private static readonly RasterizerState Scissor = new() { CullMode = CullMode.None, ScissorTestEnable = true };

    /// <summary>
    /// <see cref="BeginLayout"/> drawing only inside <paramref name="clip"/> (layout pixels): a scrolling box's content
    /// shows up to its edges and no further.
    /// </summary>
    public void BeginLayoutClipped(SpriteBatch spriteBatch, Rectangle clip)
    {
        var (sx, sy) = ((float)VirtualWidth / LayoutWidth, (float)VirtualHeight / LayoutHeight);
        _device.ScissorRectangle = Rectangle.Intersect(
            new Rectangle((int)(clip.X * sx), (int)(clip.Y * sy), (int)Math.Ceiling(clip.Width * sx), (int)Math.Ceiling(clip.Height * sy)),
            new Rectangle(0, 0, VirtualWidth, VirtualHeight));
        spriteBatch.Begin(samplerState: SamplerState.PointClamp, rasterizerState: Scissor, transformMatrix: LayoutTransform);
    }

    public VirtualScreen(GraphicsDevice device, ScreenSpec spec, int scale, ScreenPalette? palette = null)
    {
        _device = device;
        VirtualWidth = spec.Width;
        VirtualHeight = spec.Height;
        PixelAspectX = spec.PixelAspectX;
        PixelAspectY = spec.PixelAspectY;
        Background = ParseColor(spec.Background);
        LayoutTransform = LayoutTransformFor(spec);
        _palette = palette;
        _scale = Math.Clamp(scale, 1, 8);
        _target = CreateTarget();
    }

    public void SetScale(int scale)
    {
        scale = Math.Clamp(scale, 1, 8);
        if (scale == _scale) return;
        _scale = scale;
        _target.Dispose();
        _target = CreateTarget();
    }

    public void CycleScale() => SetScale(_scale >= 6 ? 1 : _scale + 1);

    /// <summary>The virtual screen's shown size at scale 1 (pixel aspect applied).</summary>
    public (int W, int H) DisplaySize => PixelAspectX >= PixelAspectY
        ? (VirtualWidth * PixelAspectX / PixelAspectY, VirtualHeight)
        : (VirtualWidth, VirtualHeight * PixelAspectY / PixelAspectX);

    /// <summary>
    /// Preferred back-buffer size for the current integer scale. The scale counts layout pixels, so a pack with more pixels
    /// than the layout (IV's 640 × 400) keeps about the same window: its own integer multiple nearest to the scale (at least 1).
    /// </summary>
    public (int W, int H) PreferredBackBufferSize => PreferredSize(DisplaySize, VirtualWidth, _scale);

    public static (int W, int H) PreferredSize((int W, int H) display, int virtualWidth, int scale)
    {
        var per = Math.Max(1, (int)Math.Round(scale * (double)LayoutWidth / Math.Max(LayoutWidth, virtualWidth),
            MidpointRounding.AwayFromZero));
        return (display.W * per, display.H * per);
    }

    public void Begin()
    {
        _device.SetRenderTarget(_target);
        _device.Clear(Background);
    }

    public void EndAndPresent(SpriteBatch spriteBatch, Texture2D? /* unused */ _)
    {
        _device.SetRenderTarget(null);
        _device.Clear(Color.Black);

        var present = PresentRect(_device.PresentationParameters.BackBufferWidth,
            _device.PresentationParameters.BackBufferHeight);

        spriteBatch.Begin(samplerState: SamplerState.PointClamp);
        spriteBatch.Draw(ApplyPalette(), present, Color.White);
        spriteBatch.End();
    }

    /// <summary>
    /// The frame to present: the render target itself, or (with a palette or the colorblind palette) a copy mapped through
    /// them (<see cref="FiltersFrame"/>, <see cref="FilterFrame"/>).
    /// </summary>
    private Texture2D ApplyPalette()
    {
        if (!FiltersFrame(FramePalette, Colorblind, Fade) && _frozen is null)
            return _target;
        _frame ??= new Color[VirtualWidth * VirtualHeight];
        if (_frozen is not null)
            _frozen.CopyTo(_frame, 0);
        else
            _target.GetData(_frame);
        FilterFrame(_frame, FramePalette, Colorblind, Fade);
        _paletted ??= new Texture2D(_device, VirtualWidth, VirtualHeight, false, SurfaceFormat.Color);
        _paletted.SetData(_frame);
        return _paletted;
    }

    /// <summary>True when a frame is copied and mapped before it is shown; false = the render target is presented untouched.</summary>
    public static bool FiltersFrame(ScreenPalette? palette, ColorblindFilter? colorblind, TransportFadeFilter? fade = null) =>
        palette is not null || colorblind is not null || fade is { Active: true };

    /// <summary>The frame mapping: the pack's palette (T1.5), II ST's TRANSPORTATION fade (s140), then the colorblind
    /// palette (QoL); any may be null.</summary>
    public static void FilterFrame(Span<Color> frame, ScreenPalette? palette, ColorblindFilter? colorblind,
        TransportFadeFilter? fade = null)
    {
        palette?.Apply(frame);
        fade?.Apply(frame);
        colorblind?.Apply(frame);
    }

    /// <summary>Where the picture lands in a <paramref name="backW"/> × <paramref name="backH"/> back buffer: the largest
    /// integer multiple of <see cref="DisplaySize"/> that fits, centred (letterboxed); with <see cref="FractionalFit"/> the
    /// largest fractional one (<see cref="FractionalPresentRect"/>).</summary>
    public Rectangle PresentRect(int backW, int backH) => FractionalFit
        ? FractionalPresentRect(DisplaySize.W, DisplaySize.H, backW, backH)
        : PresentRect(DisplaySize.W, DisplaySize.H, backW, backH);

    /// <summary>The largest rectangle of <paramref name="shownW"/> × <paramref name="shownH"/>'s aspect that fits the back
    /// buffer (any real scale, up or down), centred; the other side rounds to the nearest pixel.</summary>
    public static Rectangle FractionalPresentRect(int shownW, int shownH, int backW, int backH)
    {
        if (shownW <= 0 || shownH <= 0 || backW <= 0 || backH <= 0)
            return new Rectangle(0, 0, Math.Max(0, backW), Math.Max(0, backH));
        int drawW, drawH;
        if ((long)backW * shownH <= (long)backH * shownW)
        {
            drawW = backW;
            drawH = (int)Math.Round((double)backW * shownH / shownW, MidpointRounding.AwayFromZero);
        }
        else
        {
            drawH = backH;
            drawW = (int)Math.Round((double)backH * shownW / shownH, MidpointRounding.AwayFromZero);
        }
        return new Rectangle((backW - drawW) / 2, (backH - drawH) / 2, drawW, drawH);
    }

    public static Rectangle PresentRect(int shownW, int shownH, int backW, int backH)
    {
        var fitScale = Math.Max(1, Math.Min(backW / shownW, backH / shownH));
        var drawW = shownW * fitScale;
        var drawH = shownH * fitScale;
        return new Rectangle((backW - drawW) / 2, (backH - drawH) / 2, drawW, drawH);
    }

    /// <summary>A back-buffer pixel → the layout pixel under it (pixel aspect and pack resolution included), or null in
    /// the letterbox. Hit regions are registered in layout pixels, so this is what the mouse code compares.</summary>
    public Point? WindowToVirtual(Point backBufferPoint, int backW, int backH) =>
        WindowToVirtual(PresentRect(backW, backH), LayoutWidth, LayoutHeight, backBufferPoint);

    public static Point? WindowToVirtual(Rectangle present, int virtualWidth, int virtualHeight, Point point)
    {
        if (!present.Contains(point) || present.Width <= 0 || present.Height <= 0)
            return null;
        return new Point((point.X - present.X) * virtualWidth / present.Width,
            (point.Y - present.Y) * virtualHeight / present.Height);
    }

    private RenderTarget2D CreateTarget() =>
        new(_device, VirtualWidth, VirtualHeight, false, SurfaceFormat.Color,
            DepthFormat.None, 0, RenderTargetUsage.PreserveContents);

    /// <summary><c>#RRGGBB</c> → colour (opaque).</summary>
    public static Color ParseColor(string hex)
    {
        var s = hex.TrimStart('#');
        if (s.Length != 6 || !int.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var rgb))
            throw new FormatException($"Colour '{hex}' is not #RRGGBB.");
        return new Color((rgb >> 16) & 0xff, (rgb >> 8) & 0xff, rgb & 0xff);
    }

    public void Dispose()
    {
        _target.Dispose();
        _paletted?.Dispose();
    }
}
