using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using OpenPhantasie.Core;

namespace OpenPhantasie.Client;

/// <summary>
/// IV's location-map view with the X68000 pack (T6.4, s108): P4.X's <c>_idraw</c> — a 9 × 9 overhead window whose walls
/// stand up as slanted slabs (the manual's "from diagonally above"; the file name is the plan's, it is not a first-person
/// view). <see cref="P4DungeonView"/> says what is seen; this draws it as <c>_idraw</c> does, in pack pixels from the view's
/// top-left (<c>FORMAT.md</c> §6.9 "The location-map view"), beside the text like a town scene. Then the pictures
/// <c>_ianim</c> stepped since the view was drawn (s120, <see cref="P4DungeonAnimation.Draws"/>, oldest first: water, torches
/// and the other cycling floors and walls; a step every 51 passes of 5.53 ms plus the step's own drawing, 328–539 ms in the
/// views timed on XEiJ, §6.10 "Dungeon <c>_ianim</c>"). A stepped floor is opaque, so, as in the original, it covers the
/// part of the wall south of it and its own doorway marks until the view is drawn again (seen on XEiJ, s125: map 20's
/// water at (6, 6), pixel-equal).
/// </summary>
public sealed partial class P4DungeonScreen
{
    /// <summary>The view's size in pack pixels: 16 + 9 × 32 across, 32 + 9 × 32 down (<c>_idraw</c>'s cleared area).</summary>
    public const int ViewWidth = 16 + P4DungeonView.Size * ViewCell, ViewHeight = 32 + P4DungeonView.Size * ViewCell;

    /// <summary>One cell of the view, pack pixels.</summary>
    public const int ViewCell = 32;

    /// <summary>The floor tile of cell (c, r) sits at (16 + 32c, 32 + 32r); both wall pictures at (32c, 32r).</summary>
    private const int FloorDx = 16, FloorDy = 32;

    public static string FloorPicture(int floor) => $"p4.dungeon.floor.{floor}";

    public static string NorthWallPicture(int picture) => $"p4.dungeon.north.{picture}";

    public static string WestWallPicture(int picture) => $"p4.dungeon.west.{picture}";

    public static string PartyPicture(int marker) => $"p4.dungeon.party.{marker}";

    /// <summary>The view for the party now, or null outside a location map.</summary>
    public P4DungeonView? View => Game.Place == P4Place.Dungeon && D.HasMap
        ? P4DungeonView.Of(D.Map, D.X, D.Y, D.Dir, Game.Party.Levitating)
        : null;

    /// <summary>False when the pack has no view pictures (the placeholder squares are drawn instead).</summary>
    private bool DrawView3D(SpriteBatch batch, Texture2D pixel)
    {
        if (View is not { } view || Assets?.Get(FloorPicture(0)) is null)
            return false;
        var black = Assets.PaletteColour("pkh", 0) ?? Color.Black;
        var white = Assets.PaletteColour("pkh", 15) ?? Color.White;
        return DrawBeside(batch, pixel, ViewWidth, ViewHeight, (o, k) =>
        {
            void Box(int x, int y, int w, int h, Color c) =>
                batch.Draw(pixel, o + new Vector2(x * k, y * k), null, c, 0f, Vector2.Zero, new Vector2(w * k, h * k),
                    SpriteEffects.None, 0f);
            void Pic(string name, int x, int y)
            {
                if (Assets.Get(name) is { } t)
                    batch.Draw(t, o + new Vector2(x * k, y * k), null, Color.White, 0f, Vector2.Zero, k, SpriteEffects.None, 0f);
            }

            Box(0, 0, ViewWidth, FloorDy, black);
            for (var r = 0; r < P4DungeonView.Size; r++)
            {
                Box(0, FloorDy + r * ViewCell, FloorDx, ViewCell, black);
                for (var c = 0; c < P4DungeonView.Size; c++)
                {
                    int x = c * ViewCell, y = r * ViewCell;
                    var cell = view.Cell(c, r);
                    if (cell == P4DungeonView.Outside || !view.IsSeen(c, r))
                        Box(x + FloorDx, y + FloorDy, ViewCell, ViewCell, black);
                    else
                    {
                        if (P4DungeonView.North(cell) < P4DungeonView.NoNorth)
                            Pic(NorthWallPicture(P4DungeonView.North(cell)), x, y);
                        if (P4DungeonView.West(cell) < P4DungeonView.NoWest)
                            Pic(WestWallPicture(P4DungeonView.West(cell)), x, y);
                        Pic(FloorPicture(P4DungeonView.Floor(cell)), x + FloorDx, y + FloorDy);
                        if ((cell & P4DungeonView.EastMark) != 0)
                        {
                            Box(x + 44, y + 32, 4, 32, black);
                            Box(x + 45, y + 33, 2, 30, white);
                        }
                        if ((cell & P4DungeonView.SouthMark) != 0)
                        {
                            Box(x + 16, y + 60, 32, 4, black);
                            Box(x + 17, y + 61, 30, 2, white);
                        }
                    }
                    if (c == P4DungeonView.Centre && r == P4DungeonView.Centre)
                        Pic(PartyPicture(view.Marker), x + FloorDx, y + FloorDy);
                }
            }
            if (_viewAnimation is not { } anim)
                return;
            anim.Show(view, Game.Party.Levitating); // a key this frame may have moved the party: a new view drops the old steps
            foreach (var d in anim.Draws)
            {
                int x = d.Column * ViewCell, y = d.Row * ViewCell;
                switch (d.Kind)
                {
                    case P4ViewDrawKind.Floor:
                        Pic(FloorPicture(d.Picture), x + FloorDx, y + FloorDy);
                        break;
                    case P4ViewDrawKind.Party:
                        Pic(PartyPicture(d.Picture), x + FloorDx, y + FloorDy);
                        break;
                    case P4ViewDrawKind.North:
                        Pic(NorthWallPicture(d.Picture), x, y);
                        break;
                    case P4ViewDrawKind.West:
                        Pic(WestWallPicture(d.Picture), x, y);
                        break;
                }
            }
        });
    }
}
