using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using OpenPhantasie.Core;

namespace OpenPhantasie.Client;

/// <summary>
/// The Hall of Glory's stage figures (s151; P4.X <c>_hpix</c> <c>0xC96A</c>, FORMAT.md 6.9 "p4mon.pix"): <c>_hall</c> draws
/// HALL_A, HALL_B 202 rows under it, then for each used Hall slot 3–10 (the imports: III's transfer, and the I / II
/// characters the remaster also lets in) frame 0 of <c>p4mon.pix</c> record race + 84 (no race-34 exception; the "?" race
/// 33 gives record 117), unmirrored, with <c>_chpix</c> (slots 3–9) or <c>_mopix</c> (slot 10). Slots 0–2 (Lord Wood,
/// Filmon, Nikademus) are painted into HALL_B and get no sprite.
/// </summary>
public sealed partial class P4DungeonScreen
{
    public const string HallScene = "p4.hall";

    /// <summary>The <c>p4mon.pix</c> record of a Hall person: race + 84, the "?" race 33 included (<c>_hpix</c> <c>0xCA5C</c>).</summary>
    public static int HallRecord(int race) => race + 84;

    /// <summary>
    /// <c>_chpix</c> / <c>_mopix</c>'s grid origin on the 768 × 512 screen: GVRAM byte 123184 + 98414 = row 108, byte 414 of
    /// the 2048-byte row = x 207; a grid cell is x · 96 bytes (48 pixels) and y · 81920 bytes (40 rows).
    /// </summary>
    public const int GridScreenX = 207, GridScreenY = 108;

    /// <summary>HALL_A's place on the screen: <c>_cvsx(160)</c> = 216, <c>_cvsy(52)</c> = 99 (the scene's top-left).</summary>
    public const int HallScreenX = 216, HallScreenY = 99;

    /// <summary>
    /// Where Hall slot <paramref name="slot"/> (3–10) stands, in pack pixels from HALL_A's top-left, or null for slots
    /// 0–2 and past 10. <c>_hpix(0, slot)</c>, p = slot − 3: p 0–2 at grid (p + 3, 5) with draw offset 32 bytes (16
    /// pixels); p 3–7 at grid (p − 3, 6) with offset 0x5040 bytes (10 rows, 32 pixels). So the first row follows the three
    /// painted figures (151, 199, 247 at y 209) and the second runs from x 23 at y 259.
    /// </summary>
    public static (int X, int Y)? HallFigureAt(int slot)
    {
        var p = slot - P4Hall.FirstImportSlot;
        if (p is < 0 or >= P4Party.HallSlots - P4Hall.FirstImportSlot)
            return null;
        var (cx, cy, ox, oy) = p < 3 ? (p + 3, 5, 16, 0) : (p - 3, 6, 32, 10);
        return (GridScreenX + cx * FieldCellWidth + ox - HallScreenX, GridScreenY + cy * FieldCellHeight + oy - HallScreenY);
    }

    /// <summary>The imports' figures on the Hall scene at <paramref name="origin"/> (HALL_A's top-left), frame 0 of each record.</summary>
    private void DrawHallFigures(SpriteBatch batch, Vector2 origin, float k)
    {
        if (Assets is null)
            return;
        var imports = Game.Party.HallImports;
        for (var slot = P4Hall.FirstImportSlot; slot < imports.Length; slot++)
        {
            if (imports[slot] is not { } person || HallFigureAt(slot) is not { } at
                || Assets.Get(SpritePicture(HallRecord(person.Race))) is not { } sprite)
                continue;
            batch.Draw(sprite, origin + new Vector2(at.X * k, at.Y * k), new Rectangle(0, 0, FigureSize, FigureSize),
                Color.White, 0f, Vector2.Zero, k, SpriteEffects.None, 0f);
        }
    }
}
