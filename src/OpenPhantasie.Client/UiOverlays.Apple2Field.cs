using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client;

/// <summary>
/// s224 (II s226): the Apple II engine's combat field (I and II) (<see cref="Apple2FieldRun"/>, the pack's <c>apple2_field</c>;
/// <c>reverse-engineering/apple2/phantasie1/GRAPHICS.md</c> section 7), drawn while a round plays: the mixed hi-res screen
/// at the pack's place, its white lines (4810), the monster ranks and the party row at their DRAW points, the sparkle,
/// the spell hit and II's rock, and the four text rows in the Apple's 7-px cells. Right of the divider the Apple keeps the map: left
/// black here `[~]`.
/// </summary>
public static partial class UiOverlays
{
    /// <summary>Where each picture of the field goes now (screen pixels, top-left), for the draw and the tests.</summary>
    public sealed record Apple2FieldPlace(string File, int X, int Y);

    /// <summary>
    /// The pictures of the Apple field at <paramref name="state"/>, in drawing order: monsters (rank by rank, idle or pose B,
    /// the slain left out), the spell hit or II's rock on a monster, II's missed rock, the party figures (body by race and class; arm 22 after a swing, 21 after a cast
    /// or with a parry, else 20; the members dead at the round's start left out), the sparkle.
    /// </summary>
    public static IReadOnlyList<Apple2FieldPlace> Apple2FieldPlaces(Apple2FieldLayout layout, Apple2FieldArt art,
        Apple2FieldSnapshot snapshot, Apple2FieldRun.State state)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(art);
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(state);
        var places = new List<Apple2FieldPlace>();
        void Put(Apple2FieldPicture? picture, int x, int y)
        {
            if (picture is not null)
                places.Add(new Apple2FieldPlace(picture.File, layout.ScreenX + x - picture.OriginX, layout.ScreenY + y - picture.OriginY));
        }
        var heights = snapshot.Ranks.Select(r => r.Count > 0 && art.Monsters.TryGetValue(r[0].Id, out var m) ? m.RankHeight : 0).ToList();
        var ys = layout.RankYs(heights);
        (Apple2FieldPicture Picture, int X, int Y)? hit = null;
        for (var r = 0; r < snapshot.Ranks.Count; r++)
        {
            var rank = snapshot.Ranks[r];
            for (var i = 0; i < rank.Count; i++)
            {
                var monster = rank[i];
                if (!art.Monsters.TryGetValue(monster.Id, out var a))
                    continue;
                var x = layout.MonsterX(a.X, i, rank.Count);
                var shown = state.HitRock ? art.RockHits : art.Hits;
                if (ReferenceEquals(state.Hit, monster)
                    && (shown.GetValueOrDefault(a.Scale) ?? shown.GetValueOrDefault(1)) is { } h)
                    hit = (h, x, ys[r]);
                if (state.Gone.Contains(monster))
                    continue;
                Put(state.Posed.Contains(monster) ? a.Attacking : a.Idle, x, ys[r]);
            }
        }
        if (hit is { } on)
            Put(on.Picture, on.X, on.Y);
        if (state.RockMiss && layout.Rock is { } rock)
            Put(art.RockHits.GetValueOrDefault(1), rock.MissX, rock.MissY); // 1628: SCALE 1 at the miss point
        for (var k = 0; k < snapshot.Party.Count; k++)
        {
            var member = snapshot.Party[k];
            if (snapshot.DeadBefore.Contains(member))
                continue;
            var body = ReferenceEquals(member, snapshot.Elemental)
                ? layout.Bodies.ElementalBody
                : layout.Bodies.For((int)member.Race, (int)member.Class);
            var pose = state.Swung.Contains(member) ? Apple2FieldArt.Attacking
                : state.Cast.Contains(member) || snapshot.Parrying.Contains(member) ? Apple2FieldArt.Casting
                : Apple2FieldArt.Idle;
            Put(art.Figures.GetValueOrDefault((body, pose)), layout.PartyStep * k, layout.PartyY);
            if (ReferenceEquals(state.Sparkle, member))
                Put(art.Sparkle, layout.PartyStep * k + layout.SparkleDx, layout.SparkleY);
        }
        return places;
    }

    public static void DrawApple2Field(SpriteBatch batch, Texture2D pixel, AssetCatalog assets, CombatPresenter presenter)
    {
        if (presenter.AppleShown is not { } state || presenter.AppleSnapshot is not { } snapshot
            || assets.Pack?.Apple2Field is not { } layout || assets.Apple2FieldArt is not { } art)
            return;
        Fill(batch, pixel, new Rectangle(0, 0, 320, ScreenBottom), Color.Black);
        foreach (var (x0, y0, x1, y1) in layout.Lines)
            Fill(batch, pixel, new Rectangle(layout.ScreenX + Math.Min(x0, x1), layout.ScreenY + Math.Min(y0, y1),
                Math.Abs(x1 - x0) + 1, Math.Abs(y1 - y0) + 1), Color.White);
        foreach (var place in Apple2FieldPlaces(layout, art, snapshot, state))
            if (assets.GetFile(place.File) is { } tex)
                batch.Draw(tex, new Rectangle(place.X, place.Y, tex.Width, tex.Height), Color.White);
        for (var row = 0; row < state.Window.Count && row < layout.TextRows; row++)
            DrawCells(batch, pixel, layout.CellWidth, layout.ScreenX,
                layout.ScreenY + (layout.TextTop + row) * layout.CellHeight, state.Window[row], Color.White);
    }
}
