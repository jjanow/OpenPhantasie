using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using OpenPhantasie.Core;

namespace OpenPhantasie.Client;

/// <summary>
/// IV's 7 × 7 combat field with the X68000 pack (T6.4, s108): P4.X's <c>_opn_field</c> / <c>_obj</c> (<c>FORMAT.md</c> §6.9
/// "The combat field"). Squares 48 × 40 under a 20-row strip of colour 14 (<c>_terr</c>); every square shows the field's
/// ground picture; figures (48 × 48, <c>p4mon.pix</c>) and obstacles stand 16 rows higher, so a figure overhangs the
/// square above. The figure is the record's frame for its facing (<see cref="P4BattleFacing.Frame"/>, standing pose); a
/// flier is drawn over the obstacle on its square, others under it. In a walled fight the exit square carries the "Exit"
/// label (<c>_dsp_exit</c>: no frame, colour 15, seen s120; its layer over a figure <c>[~]</c>). Missile and spell flights (<c>fly.pix</c>) and the
/// walking pose are animations whose timing is <c>[?]</c>, not drawn. Remaster aids on top: each member's number, each
/// monster's letter (the order keys name them), the member giving orders and the target cursor outlined.
/// </summary>
public sealed partial class P4DungeonScreen
{
    /// <summary>One square of the field, pack pixels (<c>_bkpix</c>: 48 across, 40 down).</summary>
    public const int FieldCellWidth = 48, FieldCellHeight = 40;

    /// <summary>The strip above the field (<c>_terr</c>: colour 14, 336 × 20) and how far figures stand up (<c>_ofs2</c> = 16 rows).</summary>
    public const int FieldStrip = 20, FigureLift = 16, FigureSize = 48;

    public const int FieldWidth = P4Battle.Size * FieldCellWidth, FieldHeight = FieldStrip + P4Battle.Size * FieldCellHeight;

    /// <summary>The ground picture of <paramref name="b"/>'s field (<c>_e_finit</c>'s bback record; the pack maps it).</summary>
    public static string GroundPicture(P4Battle b) =>
        b.Outdoors ? $"p4.combat.ground.{b.Field}" : $"p4.combat.ground.location.{b.Terrain}";

    public static string ObstaclePicture(int kind) => $"p4.combat.obstacle.{kind}";

    public static string SpritePicture(int record) => $"p4.sprite.{record}";

    /// <summary>
    /// The <c>p4mon.pix</c> record of a party member (<c>_enc_char</c>'s loader <c>0x2078C</c>): race + 84, race 34 → 81; party
    /// slot 6 (the elemental) adds the summoned kind (<c>_par</c>+14) when it is below 3.
    /// </summary>
    public static int PersonRecord(int race, int slot, int? elemental)
    {
        var record = race == 34 ? 81 : race + 84;
        if (slot == 6 && elemental is >= 0 and < 3)
            record += elemental.Value;
        return record;
    }

    /// <summary>Whether the exit square shows its "Exit" window: fights on a location map or on the ship (<c>_dsp_exit</c>).</summary>
    public static bool ShowsExitWindow(P4Battle b) => !b.Outdoors || b.Field == "ship";

    /// <summary>The pack's font role for the exit square's label (P4.X font 0, style 2 = italic).</summary>
    public const string ExitFontRole = "p4.combat.exit";

    /// <summary>The label's row in its window (<c>_wprcen(win, width / 2, 2, …)</c>).</summary>
    public const int ExitLabelY = 2;

    /// <summary>
    /// Where <c>_dsp_exit</c>'s label starts in its square: the window is <c>_fsize</c> + 16 wide and <c>_wprcen</c> centres
    /// the text on width / 2 (integer halves), so "Exit" (25 pixels in font 0) starts at x 8 and its ink at 9 (s120 shot).
    /// </summary>
    public static int ExitLabelX(int labelWidth) => (labelWidth + 16) / 2 - labelWidth / 2;

    /// <summary>False when the pack lacks the field's ground (the placeholder squares are drawn instead).</summary>
    private bool DrawBattleArt(SpriteBatch batch, Texture2D pixel, P4Battle b)
    {
        if (Assets?.Get(GroundPicture(b)) is not { } ground)
            return false;
        var strip = Assets.PaletteColour("pkh", 14) ?? Color.Gray;
        var black = Assets.PaletteColour("pkh", 0) ?? Color.Black;
        var white = Assets.PaletteColour("pkh", 15) ?? Color.White;
        return DrawBeside(batch, pixel, FieldWidth, FieldHeight, (o, k) =>
        {
            void Box(float x, float y, float w, float h, Color c) =>
                batch.Draw(pixel, o + new Vector2(x * k, y * k), null, c, 0f, Vector2.Zero, new Vector2(w * k, h * k),
                    SpriteEffects.None, 0f);
            void Frame(float x, float y, float w, float h, Color c)
            {
                Box(x, y, w, 2, c);
                Box(x, y + h - 2, w, 2, c);
                Box(x, y, 2, h, c);
                Box(x + w - 2, y, 2, h, c);
            }
            void Pic(Texture2D t, int x, int y, Rectangle? source = null, bool mirrored = false) =>
                batch.Draw(t, o + new Vector2(x * k, y * k), source, Color.White, 0f, Vector2.Zero, k,
                    mirrored ? SpriteEffects.FlipHorizontally : SpriteEffects.None, 0f);
            void Figure(int record, int facing, int x, int y)
            {
                if (Assets.Get(SpritePicture(record)) is not { } sheet)
                    return;
                var (frame, mirrored) = P4BattleFacing.Frame(facing);
                Pic(sheet, x, y, new Rectangle(frame * FigureSize, 0, FigureSize, FigureSize), mirrored);
            }

            Box(0, 0, FieldWidth, FieldStrip, strip);
            for (var sq = 0; sq < P4Battle.Squares; sq++)
                Pic(ground, P4Battle.Col(sq) * FieldCellWidth, FieldStrip + P4Battle.Row(sq) * FieldCellHeight);
            for (var sq = 0; sq < P4Battle.Squares; sq++)
            {
                int x = P4Battle.Col(sq) * FieldCellWidth, y = FieldStrip + P4Battle.Row(sq) * FieldCellHeight - FigureLift;
                var obstacle = b.Obstacle(sq) >= 0 ? Assets.Get(ObstaclePicture(b.Obstacle(sq))) : null;
                var member = b.MemberAt(sq);
                var monster = member is null ? b.MonsterAt(sq) : null;
                var flier = member?.Flying ?? monster?.Group.Flying ?? false;
                if (obstacle is not null && flier)
                    Pic(obstacle, x, y);
                if (member is not null)
                    Figure(PersonRecord(member.Member.Race, member.Slot, b.Party.Elemental), member.Facing, x, y);
                else if (monster is not null)
                    Figure(monster.Group.Type.Index, monster.Facing, x, y);
                if (obstacle is not null && !flier)
                    Pic(obstacle, x, y);
            }
            // _dsp_exit (XEiJ s120 [C]): no box or frame, "Exit" in colour 15 straight on the floor, in P4.X's font 0 with
            // style 2 (italic; the pack's "p4.combat.exit" font, s135: pixel-identical to the shot); the window is the
            // label's width + 16 and centres it (ExitLabelX), text at window y 2. Drawn after the figures; over a
            // figure on the exit square not seen [~]. A pack without that font: the remaster's upright one.
            if (ShowsExitWindow(b))
            {
                var label = GameText.S("p4x.0x442b8");
                var exitFont = FontSources.ForRole(Assets.Pack, ExitFontRole);
                foreach (var e in b.Exits)
                {
                    int ex = P4Battle.Col(e) * FieldCellWidth, ey = FieldStrip + P4Battle.Row(e) * FieldCellHeight;
                    if (exitFont is not null)
                        PixelFont.DrawScaled(batch, pixel, o + new Vector2((ex + ExitLabelX(PixelFont.Measure(label, exitFont))) * k,
                            (ey + ExitLabelY) * k), label, white, k, exitFont);
                    else
                        PixelFont.Draw(batch, pixel, (int)Math.Round(o.X + (ex + 8) * k),
                            (int)Math.Round(o.Y + (ey + 8) * k - PixelFont.GlyphHeight / 2f), label, white);
                }
            }
            // Remaster aids: who is who for the order keys, the member giving orders and the target cursor.
            var current = b.Current;
            for (var sq = 0; sq < P4Battle.Squares; sq++)
            {
                int x = P4Battle.Col(sq) * FieldCellWidth, y = FieldStrip + P4Battle.Row(sq) * FieldCellHeight;
                var tag = b.MemberAt(sq) is { } m ? (m.Slot + 1).ToString(System.Globalization.CultureInfo.InvariantCulture)
                    : b.MonsterAt(sq) is { } mon ? mon.Letter.ToString() : null;
                if (tag is not null)
                {
                    Box(x + 2, y + FieldCellHeight - 2 - (PixelFont.GlyphHeight + 2) / k, (PixelFont.Advance * tag.Length + 2) / k,
                        (PixelFont.GlyphHeight + 2) / k, black);
                    PixelFont.Draw(batch, pixel, (int)(o.X + (x + 2) * k + 1), (int)(o.Y + (y + FieldCellHeight - 2) * k) - PixelFont.GlyphHeight - 1,
                        tag, b.MemberAt(sq) is not null ? Party : Foe);
                }
                if (current is not null && current.Square == sq)
                    Frame(x, y, FieldCellWidth, FieldCellHeight, Color.White);
                if (TargetCursor == sq)
                    Frame(x + 2, y + 2, FieldCellWidth - 4, FieldCellHeight - 4, Cursor1);
            }
            if (b.TreasureLines is { Count: > 0 } found)
                DrawMessageWindow(batch, pixel, found, o + new Vector2(0, FieldStrip * k), k);
        });
    }

    /// <summary>
    /// <c>_msg</c>'s window over the field (s141, FORMAT.md 6.9 <c>_findit</c>): untitled, at (32, 112) of the field's
    /// squares, 272 × 48, its text area at (48, 120), 240 wide, 16-dot lines.
    /// </summary>
    public const int MessageX = 32, MessageY = 112, MessageWidth = 272, MessageHeight = 48, MessageTextX = 48,
        MessageTextY = 120, MessageTextWidth = 240, MessageLine = 16;

    /// <summary>The field's squares below the strip (<c>_cut_buf</c>'s 336 × 280).</summary>
    public const int FieldSquaresHeight = P4Battle.Size * FieldCellHeight;

    /// <summary>
    /// The message window's place and height (field-square pixels) for <paramref name="lines"/> wrapped lines of
    /// <paramref name="lineStep"/> pixels: the original's 272 × 48 at (32, 112) for two lines; more lines make it taller (the
    /// user's rule: no lines dropped) and move it up when it would run past the field's bottom (frame and shadow kept on).
    /// </summary>
    public static (int Y, int Height) MessageWindow(int lines, int lineStep)
    {
        var height = Math.Max(MessageHeight, 2 * (MessageTextY - MessageY) + lines * lineStep);
        var y = Math.Max(1, Math.Min(MessageY, FieldSquaresHeight - 2 - height));
        return (y, height);
    }

    /// <summary>
    /// Draws the found-item lines in <c>_msg</c>'s window over the field (s141): <c>_wwind</c> flag 1 untitled = white
    /// (colour 15) with a black frame and a 1-pixel black shadow right and below, the text in colour 0 in the pack's font
    /// 0 (16-dot lines; without it the remaster's font). <paramref name="squares"/> is the squares' top-left (layout pixels).
    /// </summary>
    private void DrawMessageWindow(SpriteBatch batch, Texture2D pixel, IReadOnlyList<string> text, Vector2 squares, float k)
    {
        var font = FontSources.ForRole(Assets?.Pack, MysticFontRole);
        var step = font is not null ? MessageLine : (int)Math.Ceiling(PixelFont.LineHeight / k);
        // s142: with the found-item panel over the field the window moves left of it and narrows (more lines, none cut).
        var (x, w) = FindItMessageSpan(FindItPanelLeft(k));
        var textX = x + MessageTextX - MessageX;
        var textWidth = w - (MessageWidth - MessageTextWidth);
        var columns = font is not null ? textWidth / PixelFont.Advance : (int)(textWidth * k) / PixelFont.Advance;
        var lines = text.SelectMany(l => font is not null ? UiOverlays.Wrap(l, columns, font) : UiOverlays.Wrap(l, columns))
            .ToList();
        var (y, h) = MessageWindow(lines.Count, step);
        var black = Assets?.PaletteColour("pkh", 0) ?? Color.Black;
        var white = Assets?.PaletteColour("pkh", 15) ?? Color.White;
        void Box(float bx, float by, float w, float bh, Color c) =>
            batch.Draw(pixel, squares + new Vector2(bx * k, by * k), null, c, 0f, Vector2.Zero, new Vector2(w * k, bh * k),
                SpriteEffects.None, 0f);
        Box(x - 1, y - 1, w + 2, h + 2, black);
        Box(x, y, w, h, white);
        Box(x + w + 1, y + 1, 1, h + 1, black);
        Box(x + 1, y + h + 1, w + 1, 1, black);
        for (var i = 0; i < lines.Count; i++)
        {
            var at = squares + new Vector2(textX * k, (y + MessageTextY - MessageY + i * step) * k);
            if (font is not null)
                PixelFont.DrawScaled(batch, pixel, at, lines[i], black, k, font);
            else
                PixelFont.Draw(batch, pixel, (int)Math.Round(at.X), (int)Math.Round(at.Y), lines[i], black);
        }
    }
}
