using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client;

/// <summary>
/// <c>_sklup</c>'s level-up skill window (s152, FORMAT.md 6.9 "Level-up skill picks"): window 32 over the training hall,
/// drawn from the Core's <see cref="P4SkillWindow"/> (rows, prompt, trained line, done) and the pack's
/// <see cref="SkillUpWindow"/> (places, colours, the bar's and header's string ids). It takes the picture's place (the
/// training hall shows none); the text column keeps its lines and keys. A click on a row presses the remaster's key for
/// that skill's line (a digit, past 9 the cursor there and Return).
/// </summary>
public sealed partial class P4DungeonScreen
{
    /// <summary>The Core's skill window while it is up in town (outside menus, the System menu and fights), else null.</summary>
    private P4SkillWindow? CoreSkillWindow => Game.LoopBuilt && Game.Battle is null && !MenuOpen && _system is null
        && Game.Place == P4Place.Town ? W.SkillWindow : null;

    /// <summary>
    /// The x of the now and after columns (pack pixels): the original's, moved right just enough to clear the widest skill
    /// name (<paramref name="widestSkill"/>; English names run past x 112) with <see cref="ListColumnGap"/>, after keeping
    /// its distance from now `[~]`.
    /// </summary>
    public static (int Now, int After) SkillUpColumns(SkillUpRows rows, int widestSkill)
    {
        ArgumentNullException.ThrowIfNull(rows);
        var now = Math.Max(rows.NowX, rows.SkillX + widestSkill + ListColumnGap);
        return (now, now + rows.AfterX - rows.NowX);
    }

    /// <summary><c>_trang</c>'s window while the Training Hall is open in town (the same gates as <see cref="CoreSkillWindow"/>).</summary>
    private P4TrainingWindow? CoreTrainingWindow => Game.LoopBuilt && Game.Battle is null && !MenuOpen && _system is null
        && Game.Place == P4Place.Town ? W.TrainingWindow : null;

    /// <summary>
    /// The header's pieces (s156): the original's header (<paramref name="original"/>, one string at <paramref name="x0"/>)
    /// is split at its spaces into tokens whose x follow from <c>_fsize</c>'s advances (16 per full-width character, the
    /// font's own for the rest; <paramref name="width"/>); the shown text's tokens take those places one for one, those at
    /// or right of the now column (<paramref name="nowX"/>) moved with the columns by <paramref name="shift"/>, and never
    /// closer than a space to the token before. A shown text with another token count stays one string at x0 `[~]`.
    /// </summary>
    public static List<(int X, string Text)> SkillUpHeader(string original, string shown, int x0, int nowX, int shift,
        Func<string, int> width)
    {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(shown);
        ArgumentNullException.ThrowIfNull(width);
        int Advance(char c) => c > 0xFF && c is not (>= '｡' and <= 'ﾟ') ? 16 : width(c.ToString());
        var places = new List<int>();
        var x = x0;
        var inToken = false;
        foreach (var c in original)
        {
            var space = c is ' ' or '　';
            if (!space && !inToken)
                places.Add(x);
            inToken = !space;
            x += Advance(c);
        }
        var tokens = shown.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length != places.Count)
            return [(x0, shown)];
        var result = new List<(int, string)>();
        var end = int.MinValue;
        for (var i = 0; i < tokens.Length; i++)
        {
            var at = Math.Max(places[i] + (places[i] >= nowX ? shift : 0), end == int.MinValue ? x0 : end + width(" "));
            result.Add((at, tokens[i]));
            end = at + width(tokens[i]);
        }
        return result;
    }

    /// <summary>
    /// <c>_trang</c>'s lines (s156) in pack pixels relative to its window: the name, then the note's segments (at their own
    /// place or after the last), then "not enough gold" after a YES the purse cannot pay. A piece that would pass
    /// <paramref name="right"/> wraps at a space (or, with none, between characters) to the next row at its line's start;
    /// every later row moves down with it (the user's fit rule: no text is dropped). <paramref name="text"/> gives a
    /// segment's shown string; <paramref name="width"/> a string's width in a font.
    /// </summary>
    public static List<(int X, int Y, string Text, int Font, int Colour)> TrainingHallText(TrainingHallWindow hall,
        P4TrainingWindow window, int right, Func<TrainingHallSegment, string> text, Func<string, int, int> width)
    {
        ArgumentNullException.ThrowIfNull(hall);
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(width);
        var key = window.Note switch
        {
            P4TrainingNote.PoorCondition => "poor_condition",
            P4TrainingNote.NothingMoreToTeach => "nothing_more",
            P4TrainingNote.NeedsExperience => "needs_experience",
            _ => "cost",
        };
        var pieces = new List<(int? X, int? Y, string Text, int Font, int Colour)>
        {
            (hall.Name.X, hall.Name.Y, window.Member, 0, hall.TextColour),
        };
        IEnumerable<TrainingHallSegment> segments = hall.Notes.TryGetValue(key, out var note) ? note : [];
        if (window.ShortOfGold)
            segments = segments.Append(hall.ShortOfGold);
        foreach (var s in segments)
            pieces.Add((s.X, s.Y, s.Number ? window.Amount.ToString(System.Globalization.CultureInfo.InvariantCulture)
                : text(s), s.Font, s.Colour ?? hall.TextColour));
        return [.. FlowText(pieces.Select(p => (p.X, p.Y, p.Text, p.Font, p.Colour, 0)), hall.Name.X, hall.Name.Y, right,
            hall.LineStep, (t, font, _) => width(t, font)).Select(r => (r.X, r.Y, r.Text, r.Font, r.Colour))];
    }

    /// <summary>
    /// A window's text pieces in pack pixels (s156 <c>_trang</c>, s162 <c>_endstory</c>): each at its own place when it has
    /// one, else at the text cursor after the last, from (<paramref name="x0"/>, <paramref name="y0"/>). A piece that would
    /// pass <paramref name="right"/> wraps at a space (or, with none, between characters) to the next row at its line's
    /// start; every later row moves down with it (the user's fit rule: no text is dropped). <paramref name="width"/> gives
    /// a string's width in a font and style.
    /// </summary>
    public static List<(int X, int Y, string Text, int Font, int Colour, int Style)> FlowText(
        IEnumerable<(int? X, int? Y, string Text, int Font, int Colour, int Style)> pieces, int x0, int y0, int right,
        int step, Func<string, int, int, int> width)
    {
        ArgumentNullException.ThrowIfNull(pieces);
        ArgumentNullException.ThrowIfNull(width);
        var rows = new List<(int, int, string, int, int, int)>();
        int x = x0, y = y0, start = x, shift = 0;
        foreach (var (px, py, s, font, colour, style) in pieces)
        {
            if (px is int nx && py is int ny)
                (x, y, start) = (nx, ny + shift, nx);
            var rest = s;
            while (rest.Length > 0 && x + width(rest, font, style) > right)
            {
                var cut = rest.Length - 1;
                while (cut > 0 && (x + width(rest[..cut], font, style) > right || rest[cut] != ' '))
                    cut--;
                if (cut == 0 && x > start)
                    cut = -1; // nothing fits after the text before it: the whole piece starts the next row
                else if (cut == 0)
                    for (cut = 1; cut < rest.Length && x + width(rest[..(cut + 1)], font, style) <= right; cut++)
                    {
                    }
                if (cut > 0)
                    rows.Add((x, y, rest[..cut], font, colour, style));
                rest = cut > 0 ? rest[cut..].TrimStart() : rest;
                (x, y) = (start, y + step);
                shift += step;
            }
            if (rest.Length > 0)
                rows.Add((x, y, rest, font, colour, style));
            x += width(rest, font, style);
        }
        return rows;
    }

    /// <summary>The training hall's rows now, wrapped 16 short of the window's right edge (its left margin).</summary>
    private List<(int X, int Y, string Text, int Font, int Colour)> TrainingHallRows(TrainingHallWindow hall,
        P4TrainingWindow window, float k) =>
        TrainingHallText(hall, window, hall.Window.Width - hall.Name.X, s => PanelText(s.Label, s.Text),
            (t, font) => font == 1 && FontSources.ForRole(Assets?.Pack, PanelLabelFontRole) is { } f1
                ? PixelFont.Measure(t, f1) : PackWidth(t, k));

    /// <summary>
    /// Where the skill window's bar starts in the hall's window (pack pixels): the original's (window 32's two openings),
    /// moved down past the hall's last row when wrapped English rows would reach it.
    /// </summary>
    public static int SkillUpTop(TrainingHallWindow hall, SkillUpWindow sk, int textBottom)
    {
        ArgumentNullException.ThrowIfNull(hall);
        ArgumentNullException.ThrowIfNull(sk);
        return Math.Max(sk.Window.Y - hall.Window.Y + Math.Min(0, sk.Bar.Y), textBottom);
    }

    /// <summary>
    /// <c>_trang</c>'s window (s156) at <paramref name="origin"/> (its top-left, layout pixels): the window grey, the name
    /// and the note (the number in font 1 and its colour), and a level-up's <c>_sklup</c> window in its lower part. s158:
    /// the title bar ("訓練場") and frame round it are <see cref="DrawTitledBeside"/>'s.
    /// </summary>
    private void DrawTrainingWindow(SpriteBatch batch, Texture2D pixel, TrainingHallWindow hall, P4TrainingWindow window,
        P4SkillWindow? skills, SkillUpWindow? sk, Vector2 origin, float k)
    {
        var rows = TrainingHallRows(hall, window, k);
        var (w, h) = TrainingHallArea(hall, window, sk, k);
        batch.Draw(pixel, origin, null, Pal(hall.BackColour, Color.LightGray), 0f, Vector2.Zero, new Vector2(w * k, h * k),
            SpriteEffects.None, 0f);
        var labelFont = FontSources.ForRole(Assets?.Pack, PanelLabelFontRole);
        foreach (var (x, y, text, font, colour) in rows)
        {
            var at = origin + new Vector2(x * k, y * k);
            if (font == 1 && labelFont is not null)
                PixelFont.DrawScaled(batch, pixel, at, text, Pal(colour, Color.Black), k, labelFont);
            else
                PackText(batch, pixel, at, text, Pal(colour, Color.Black), k);
        }
        if (skills is not null && sk is not null)
            DrawSkillWindow(batch, pixel, sk, skills, origin + new Vector2(0,
                SkillUpTop(hall, sk, rows.Select(r => r.Y + hall.LineStep).DefaultIfEmpty(0).Max()) * k), k);
    }

    /// <summary>The hall window's size in pack pixels now: the original's, taller by the rows wrapped English text adds.</summary>
    private (int Width, int Height) TrainingHallArea(TrainingHallWindow hall, P4TrainingWindow window, SkillUpWindow? sk,
        float k)
    {
        var bottom = TrainingHallRows(hall, window, k).Select(r => r.Y + hall.LineStep).DefaultIfEmpty(0).Max();
        var extra = sk is null ? 0 : SkillUpTop(hall, sk, bottom) - SkillUpTop(hall, sk, 0);
        return (hall.Window.Width, hall.Window.Height + extra);
    }

    /// <summary>The pack pixels the drawn area takes: the window's width, its height plus the bar above it.</summary>
    public static (int Width, int Height) SkillUpArea(SkillUpWindow sk)
    {
        ArgumentNullException.ThrowIfNull(sk);
        return (sk.Window.Width, sk.Window.Height - Math.Min(0, sk.Bar.Y));
    }

    /// <summary>
    /// Draws the window with the bar's top-left at <paramref name="origin"/> (layout pixels): <c>_trang</c>'s grey under
    /// it (window 32 has no fill of its own), the bar (black with the title in colour 15; at the end grey with the done
    /// line), the header, <c>_whole</c>'s sunken frame, the rows (a row under the pointer lit, colours ^ 5, while the pick
    /// is open), the prompt and the trained line. Text in font 0.
    /// </summary>
    private void DrawSkillWindow(SpriteBatch batch, Texture2D pixel, SkillUpWindow sk, P4SkillWindow window, Vector2 origin,
        float k)
    {
        var top = Math.Min(0, sk.Bar.Y);
        Vector2 At(float x, float y) => origin + new Vector2(x * k, (y - top) * k);
        void Box(float x, float y, float w, float h, Color c) =>
            batch.Draw(pixel, At(x, y), null, c, 0f, Vector2.Zero, new Vector2(w * k, h * k), SpriteEffects.None, 0f);
        var (width, height) = SkillUpArea(sk);
        Box(0, top, width, height, Pal(sk.BackColour, Color.LightGray));
        var b = sk.Bar;
        var bar = window.Done ? sk.Done : sk.Title;
        Box(b.X, b.Y, b.Width, b.Height, Pal(window.Done ? sk.DoneBack : sk.BarColour, Color.Black));
        var barText = PanelText(bar.Label, bar.Text);
        PackText(batch, pixel, At(bar.X - (PackWidth(barText, k) >> 1), bar.Y), barText, Pal(bar.Colour, Color.White), k);
        var ink = Pal(sk.TextColour, Color.Black);
        var r = sk.Rows;
        var (nowX, afterX) = SkillUpColumns(r, window.Rows.Select(row => PackWidth(row.Skill, k)).DefaultIfEmpty(0).Max());
        foreach (var (x, text) in SkillUpHeader(sk.Header.Text, PanelText(sk.Header.Label, sk.Header.Text), sk.Header.X,
                     r.NowX, nowX - r.NowX, t => PackWidth(t, k)))
            PackText(batch, pixel, At(x, sk.Header.Y), text, ink, k);
        var f = sk.Frame.Rect;
        Color dark = Pal(sk.Frame.Dark, Color.DimGray), light = Pal(sk.Frame.Light, Color.White);
        for (var n = 1; n <= NoYesFrame; n++)
        {
            Box(f.X - n, f.Y - n, f.Width + 2 * n, 1, dark);
            Box(f.X - n, f.Y - n, 1, f.Height + 2 * n, dark);
            Box(f.X - n, f.Y + f.Height + n - 1, f.Width + 2 * n, 1, light);
            Box(f.X + f.Width + n - 1, f.Y - n, 1, f.Height + 2 * n, light);
        }
        var open = W.Prompt == P4TownPrompt.Pick && window.Left > 0;
        var button = r.Button;
        for (var i = 0; i < Math.Min(window.Rows.Count, r.Count); i++)
        {
            var row = window.Rows[i];
            var y = r.YOf(i);
            var at = At(button.X, y);
            float bw = button.Width * k, bh = button.Height * k;
            var xor = open ? BoxXorAt(at.X, at.Y, bw, bh) : 0;
            Box(button.X, y, button.Width, button.Height, Pal(r.Back ^ xor, Color.LightGray));
            var c = Pal(sk.TextColour ^ xor, Color.Black);
            PackText(batch, pixel, At(r.KeyX, y), ((char)('A' + i)).ToString(), c, k);
            PackText(batch, pixel, At(r.SkillX, y), row.Skill, c, k);
            PackText(batch, pixel, At(nowX, y), PaddedAmount(row.Now, r.Digits), c, k);
            PackText(batch, pixel, At(afterX, y), PaddedAmount(row.After, r.Digits), c, k);
            if (!open)
                continue;
            var (key, line) = ListPress(row.Choice);
            UiOverlays.Hits?.Add(new HitRegion(new Rectangle((int)at.X, (int)at.Y, (int)Math.Ceiling(bw), (int)Math.Ceiling(bh)),
                new P4PanelIconTag(P4SkillWindow.Name, i, line)) { Click = [key] });
        }
        if (window.Prompt is { } prompt)
            PackText(batch, pixel, At(sk.Prompt.X, sk.Prompt.Y), prompt, ink, k);
        if (window.Trained is { } trained)
            PackText(batch, pixel, At(sk.Trained.X, sk.Trained.Y), trained, ink, k);
    }
}
