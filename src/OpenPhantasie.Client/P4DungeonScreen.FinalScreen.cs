using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client;

/// <summary>
/// s162: <c>_endstory</c>'s final screen (FORMAT.md 6.9 "The final screen", <c>ending.md</c> section 3) in its own titled
/// window 32 in the picture's place: the heading "THE GAME END" (font 0, shadow, colour 1), the lines, the score and the
/// completion code (font 1, colour 8), the postcard text, and after the SPACE wait the save question (font 1, bold,
/// colour 2). The text column keeps its lines and keys (as the training hall's).
/// </summary>
public sealed partial class P4DungeonScreen
{
    /// <summary>The pack's key of the final screen's titled window.</summary>
    public const string FinalScreenWindowName = "_endstory";

    /// <summary>True while the final screen's window is up (from its lines through <c>_acgamend(1)</c>).</summary>
    private bool FinalScreenUp => Game.LoopBuilt && Game.Battle is null && Game.Place == P4Place.Town
        && W.Scene == P4TownScene.EndingFinal && W.EndingScore is not null && Extras?.FinalScreen is not null
        && Extras.Titled?.ContainsKey(FinalScreenWindowName) == true;

    /// <summary>
    /// The final screen's rows in pack pixels relative to its window: the segments with the <paramref name="score"/>, then
    /// with <paramref name="question"/> the save question, wrapped short of <paramref name="right"/>.
    /// </summary>
    public static List<(int X, int Y, string Text, int Font, int Colour, int Style)> FinalScreenText(FinalScreenWindow end,
        int score, bool question, int right, Func<TrainingHallSegment, string> text, Func<string, int, int, int> width)
    {
        ArgumentNullException.ThrowIfNull(end);
        ArgumentNullException.ThrowIfNull(text);
        IEnumerable<TrainingHallSegment> segments = end.Segments;
        if (question)
            segments = segments.Append(end.Question);
        var pieces = segments.Select(s => (s.X, s.Y,
            s.Number ? PaddedAmount(score, Math.Max(1, s.Digits)) : text(s), s.Font, s.Colour ?? end.TextColour, s.Style));
        var first = end.Segments.FirstOrDefault();
        return FlowText(pieces, first?.X ?? 0, first?.Y ?? 0, right, end.LineStep, width);
    }

    /// <summary>P4.X font <paramref name="font"/> (0 = the window font, 1 = the thin one) with <paramref name="style"/>'s
    /// passes, from the pack's roles; null without them.</summary>
    private IFontSource? PackFont(int font, int style) =>
        FontSources.ForRole(Assets?.Pack, font == 1 ? PanelLabelFontRole : MysticFontRole) is { } f
            ? X68kFontStyle.With(f, style) : null;

    private int PackWidth(string text, int font, int style, float k) =>
        PackFont(font, style) is { } f ? PixelFont.Measure(text, f) : PackWidth(text, k);

    private List<(int X, int Y, string Text, int Font, int Colour, int Style)> FinalScreenRows(FinalScreenWindow end,
        TitledWindow window, float k) =>
        FinalScreenText(end, W.EndingScore ?? 0, W.EndingStep != P4EndingStep.None,
            window.Window.Width - (end.Segments.FirstOrDefault()?.X ?? 0), s => PanelText(s.Label, s.Text),
            (t, font, style) => PackWidth(t, font, style, k));

    /// <summary>The final screen's window height in pack pixels: the original's, taller by rows wrapped English adds.</summary>
    private int FinalScreenHeight(FinalScreenWindow end, TitledWindow window, float k) =>
        Math.Max(window.Window.Height,
            FinalScreenRows(end, window, k).Select(r => r.Y + end.LineStep).DefaultIfEmpty(0).Max());

    private bool DrawFinalScreen(SpriteBatch batch, Texture2D pixel)
    {
        if (!FinalScreenUp || Assets?.PackPixelsPerLayoutPixel is not > 0)
            return false;
        var end = Extras!.FinalScreen!;
        var window = Extras.Titled![FinalScreenWindowName];
        var k = 1f / Assets.PackPixelsPerLayoutPixel;
        return DrawTitledBeside(batch, pixel, window.Window.Width, FinalScreenHeight(end, window, k),
            PanelText(window.Title, window.TitleText), (origin, scale) => DrawFinalScreenAt(batch, pixel, end, window, origin, scale));
    }

    private void DrawFinalScreenAt(SpriteBatch batch, Texture2D pixel, FinalScreenWindow end, TitledWindow window,
        Vector2 origin, float k)
    {
        batch.Draw(pixel, origin, null, Pal(14, Color.LightGray), 0f, Vector2.Zero,
            new Vector2(window.Window.Width * k, FinalScreenHeight(end, window, k) * k), SpriteEffects.None, 0f);
        void Text(int x, int y, string text, int font, int style, int colour)
        {
            var at = origin + new Vector2(x * k, y * k);
            if (PackFont(font, style) is { } f)
                PixelFont.DrawScaled(batch, pixel, at, text, Pal(colour, Color.Black), k, f);
            else
                PackText(batch, pixel, at, text, Pal(colour, Color.Black), k);
        }
        var h = end.Heading;
        var heading = PanelText(h.Label, h.Text);
        Text((h.X ?? 0) - (PackWidth(heading, h.Font, 0, k) >> 1), h.Y ?? 0, heading, h.Font, h.Style, h.Colour ?? end.TextColour);
        foreach (var (x, y, text, font, colour, style) in FinalScreenRows(end, window, k))
            Text(x, y, text, font, style, colour);
    }
}

/// <summary>
/// s162: a wiped-out party (<c>_deadmsg</c>, FORMAT.md 6.9 "The wiped-out party"): <c>_xblack</c> blacks the whole
/// screen, the message window is drawn at the original's place over the black while the key wait is up; after the key
/// <c>_xblack</c> again, so <c>_acgamend(1)</c>'s panel (and the remaster's text column with its keys) stand on black,
/// with no picture or view under them.
/// </summary>
public sealed partial class P4DungeonScreen
{
    /// <summary>True from the party's loss on (outside a fight), with a pack that has <c>_deadmsg</c>'s window.</summary>
    public bool BlackedOut => Game.LoopBuilt && Game.Battle is null && Game.PartyLost
        && Assets?.PackPixelsPerLayoutPixel is > 0 && Extras?.DeadMessage is not null;

    private bool DrawBlackedOut(SpriteBatch batch, Texture2D pixel)
    {
        if (!BlackedOut)
            return false;
        var dead = Extras!.DeadMessage!;
        var k = 1f / Assets!.PackPixelsPerLayoutPixel;
        Fill(batch, pixel, new Rectangle(0, 0, 320, 200), Pal(dead.ScreenColour, Color.Black));
        if (_system is not null)
        {
            // _xblack again, then _acgamend(1): the panel and the column on black.
            DrawBeside(batch, pixel, 0, 0, (_, _) => { });
            return true;
        }
        var w = dead.Window;
        var origin = new Vector2(w.X * k, w.Y * k);
        void Box(PanelRect r, Color c) =>
            batch.Draw(pixel, origin + new Vector2(r.X * k, r.Y * k), null, c, 0f, Vector2.Zero,
                new Vector2(r.Width * k, r.Height * k), SpriteEffects.None, 0f);
        var lines = dead.Lines.Select(l => (Line: l, Text: PanelText(l.Label, l.Text))).ToList();
        // The window as wide as the original's, or wider when a translation's line would pass its frames.
        var widest = lines.Select(l => PackWidth(l.Text, dead.Font, dead.Style, k)).DefaultIfEmpty(0).Max();
        var width = Math.Max(w.Width, widest + 16);
        var grow = width - w.Width;
        origin.X = Math.Max(0, origin.X - grow / 2f * k);
        Box(new PanelRect(0, 0, width, w.Height), Pal(dead.Fill, Color.Black));
        var frame = Pal(dead.FrameColour, Color.White);
        foreach (var f in dead.Frames)
        {
            var r = new PanelRect(f.X, f.Y, f.Width + grow, f.Height);
            Box(r with { Height = 1 }, frame);
            Box(r with { Y = r.Y + r.Height - 1, Height = 1 }, frame);
            Box(r with { Width = 1 }, frame);
            Box(r with { X = r.X + r.Width - 1, Width = 1 }, frame);
        }
        foreach (var (line, text) in lines)
        {
            var at = origin + new Vector2(((line.X ?? 0) + grow / 2f - (PackWidth(text, dead.Font, 0, k) >> 1)) * k,
                (line.Y ?? 0) * k);
            if (PackFont(dead.Font, dead.Style) is { } font)
                PixelFont.DrawScaled(batch, pixel, at, text, Pal(dead.TextColour, Color.White), k, font);
            else
                PackText(batch, pixel, at, text, Pal(dead.TextColour, Color.White), k);
        }
        var note = HelpLines(Note ?? Help, 51);
        var noteTop = 189 - (note.Count - 1) * PixelFont.LineHeight;
        for (var i = 0; i < note.Count; i++)
            PixelFont.Draw(batch, pixel, 6, noteTop + i * PixelFont.LineHeight, note[i], Note is null ? Dim : Title);
        return true;
    }
}

/// <summary>
/// s162: the Town Gate window (<c>_townent</c>, FORMAT.md 6.9 "The Town Gate"): titled, in the picture's place while the
/// aging wait is up, each aged member's line at the member's own row.
/// </summary>
public sealed partial class P4DungeonScreen
{
    /// <summary>The pack's key of the Town Gate's titled window.</summary>
    public const string TownGateWindowName = "_townent";

    /// <summary>The Town Gate's rows in pack pixels relative to its window, wrapped short of <paramref name="right"/>.</summary>
    public static List<(int X, int Y, string Text, int Font, int Colour, int Style)> TownGateText(TownGateWindow gate,
        IReadOnlyList<P4GateRow> rows, int right, Func<TrainingHallSegment, string> text, Func<string, int, int, int> width)
    {
        ArgumentNullException.ThrowIfNull(gate);
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(text);
        var pieces = rows.Select(r => ((int?)gate.X, (int?)(gate.Y + gate.Step * r.Member),
            r.Name + text(r.Line == gate.Died.Label ? gate.Died : gate.GrownOlder), 0, gate.TextColour, 0));
        return FlowText(pieces, gate.X, gate.Y, right, gate.Step, width);
    }

    private bool DrawTownGate(SpriteBatch batch, Texture2D pixel)
    {
        if (!Game.LoopBuilt || Game.Battle is not null || Game.Place != P4Place.Town || W.GateRows is not { Count: > 0 } rows
            || Extras?.TownGate is not { } gate || Extras.Titled?.GetValueOrDefault(TownGateWindowName) is not { } window
            || Assets?.PackPixelsPerLayoutPixel is not > 0)
            return false;
        var k = 1f / Assets.PackPixelsPerLayoutPixel;
        var text = TownGateText(gate, rows, window.Window.Width - gate.X, s => PanelText(s.Label, s.Text),
            (t, font, style) => PackWidth(t, font, style, k));
        var height = Math.Max(window.Window.Height, text.Select(r => r.Y + gate.Step).DefaultIfEmpty(0).Max());
        return DrawTitledBeside(batch, pixel, window.Window.Width, height, PanelText(window.Title, window.TitleText),
            (origin, scale) =>
            {
                batch.Draw(pixel, origin, null, Pal(14, Color.LightGray), 0f, Vector2.Zero,
                    new Vector2(window.Window.Width * scale, height * scale), SpriteEffects.None, 0f);
                foreach (var (x, y, t, font, colour, style) in text)
                {
                    var at = origin + new Vector2(x * scale, y * scale);
                    if (PackFont(font, style) is { } f)
                        PixelFont.DrawScaled(batch, pixel, at, t, Pal(colour, Color.Black), scale, f);
                    else
                        PackText(batch, pixel, at, t, Pal(colour, Color.Black), scale);
                }
            });
    }
}
