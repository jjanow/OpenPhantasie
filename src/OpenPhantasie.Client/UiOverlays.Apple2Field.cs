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
    /// <summary>
    /// Where each picture of the field goes now (screen pixels, top-left), for the draw and the tests. <paramref name="Opaque"/>:
    /// III's <c>MONSTER PLOT</c> sprites store whole bytes, so their box is black where they have no pixels.
    /// </summary>
    public sealed record Apple2FieldPlace(string File, int X, int Y, bool Opaque = false);

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

    /// <summary>
    /// III (s228; OUTDOORS3 / DUNGON3, <c>reverse-engineering/apple2/phantasie3/GRAPHICS.md</c> section 9): the pictures of
    /// the Apple's screen at <paramref name="state"/>, in drawing order: the game's screen (<c>SCREEN</c>), the monsters
    /// (sprites with their top-left at the rank's y and byte column 21 + 4·INT(5·(i + .5) / n); pose B once posed; the slain
    /// left out), the party (slot k at (147 + 19k, 198 − 8·(row + 1)): an archer's bow, drawn or loosed; else the body by
    /// race, class and social class with arm 20 / 22 (a swing) / 5 (a cast) / 21 (a parry that took), kneeling with shape
    /// 6; the dead at the start, the members erased at their death and the elemental left out), the sparkle, then the
    /// spell hit or the arrow on a monster (4132) and the missed arrow (1628).
    /// </summary>
    public static IReadOnlyList<Apple2FieldPlace> Apple2PlotPlaces(Apple2FieldLayout layout, Apple2FieldArt art,
        Apple2FieldSnapshot snapshot, Apple2FieldRun.State state)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(art);
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(state);
        var plot = layout.Plot ?? throw new ArgumentException("not III's field (no monster_plot)", nameof(layout));
        var places = new List<Apple2FieldPlace>();
        void Put(Apple2FieldPicture? picture, int x, int y, bool opaque = false)
        {
            if (picture is not null)
                places.Add(new Apple2FieldPlace(picture.File, layout.ScreenX + x - picture.OriginX,
                    layout.ScreenY + y - picture.OriginY, opaque));
        }
        if (plot.Frame)
            Put(art.Frame, 0, 0);
        (int X, int Y)? hitAt = null;
        for (var r = 0; r < snapshot.Ranks.Count; r++)
        {
            var rank = snapshot.Ranks[r];
            var giant = rank.Count > 0 && art.Monsters.TryGetValue(rank[0].Id, out var lead) && lead.Shape == plot.GiantShape;
            var y = plot.RankY(r, giant);
            for (var i = 0; i < rank.Count; i++)
            {
                var monster = rank[i];
                var x = plot.BytePixels * plot.ByteColumn(i, rank.Count);
                if (ReferenceEquals(state.Hit, monster))
                    hitAt = (x + plot.HitDx, y + plot.HitDy);
                if (state.Gone.Contains(monster) || !art.Monsters.TryGetValue(monster.Id, out var a))
                    continue;
                Put(state.Posed.Contains(monster) ? a.Attacking : a.Idle, x, y, opaque: true);
            }
        }
        for (var k = 0; k < snapshot.Party.Count; k++)
        {
            var member = snapshot.Party[k];
            if (snapshot.DeadBefore.Contains(member) || state.MembersGone.Contains(member)
                || ReferenceEquals(member, snapshot.Elemental))
                continue; // the elemental's body (slot 6's byte 168) is not known [?]
            var x = plot.PartyX + layout.PartyStep * k;
            var y = plot.PartyY(snapshot.Rows.GetValueOrDefault(member, 2));
            if (snapshot.Archers.Contains(member))
                Put(art.Effects.GetValueOrDefault(state.Fired.Contains(member) ? "bow-fired" : "bow"), x, y); // 9200 / 1624
            else
            {
                var pose = state.Swung.Contains(member) ? Apple2FieldArt.Attacking
                    : state.Cast.Contains(member) ? Apple2FieldArt.Casting
                    : snapshot.Parrying.Contains(member) ? Apple2FieldArt.Parrying
                    : Apple2FieldArt.Idle;
                if (snapshot.Kneeling.Contains(member))
                {
                    Put(art.Effects.GetValueOrDefault("body-down"), x, y); // 9205: shape 6, then the arm
                    Put(art.Shapes.GetValueOrDefault(pose switch
                    {
                        Apple2FieldArt.Attacking => 22,
                        Apple2FieldArt.Casting => 5,
                        Apple2FieldArt.Parrying => 21,
                        _ => 20,
                    }), x, y);
                }
                else
                {
                    var body = layout.Bodies.For((int)member.Race, (int)member.Class, member.Phantasie3?.SocialClass ?? 0);
                    Put(art.Figures.GetValueOrDefault((body, pose)), x, y);
                }
            }
            if (ReferenceEquals(state.Sparkle, member))
                Put(art.Sparkle, x + plot.SparkleDx, y + plot.SparkleDy);
        }
        if (hitAt is { } h)
            Put(state.HitRock ? art.Effects.GetValueOrDefault("arrow") : art.Hits.GetValueOrDefault(1), h.X, h.Y);
        if (state.RockMiss)
        {
            var (mx, my) = snapshot.InDungeon ? plot.ArrowMissDungeon : plot.ArrowMiss;
            Put(art.Effects.GetValueOrDefault("arrow"), mx, my);
        }
        return places;
    }

    /// <summary>
    /// III's party columns (OUTDOORS3 600–635) in text cells (column, row) of the Apple's 40 × 24 grid: what goes where,
    /// for the draw and the tests; <c>Inverse</c> for the name (STARTER 620, <c>IMPREVERSE</c>).
    /// </summary>
    public static IReadOnlyList<(int Column, int Row, string Text, bool Inverse)> Apple2PlotBoxCells(Apple2FieldBoxes boxes,
        Apple2FieldSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(boxes);
        ArgumentNullException.ThrowIfNull(snapshot);
        var cells = new List<(int, int, string, bool)>();
        string Cut(string text) => text.Length > boxes.Width ? text[..boxes.Width] : text;
        var classes = InspectDialog.ClassNames;
        for (var k = 0; k < snapshot.Party.Count && k < snapshot.Boxes.Count; k++)
        {
            var member = snapshot.Party[k];
            if (ReferenceEquals(member, snapshot.Elemental))
                continue; // 602: slot 6 has no column
            var box = snapshot.Boxes[k];
            var c = boxes.Column + boxes.Step * k;
            cells.Add((c, boxes.NameRow, Cut(member.Name.ToUpperInvariant()), true));
            if ((int)member.Class is var cls && cls >= 0 && cls < classes.Length)
                cells.Add((c, boxes.ClassRow, Cut(classes[cls].ToUpperInvariant()), false));
            cells.Add((c + (box.MagicMax < 10 ? 1 : 0), boxes.MagicRow, $"{box.MagicCur}:{box.MagicMax}", false)); // 605-607
            if (box.HpMax < 100)
                cells.Add((c, boxes.HitsRow, $"{box.HpCur}:{box.HpMax}", false)); // 610
            else
            {
                cells.Add((c + 1, boxes.HitsRow, box.HpCur.ToString(System.Globalization.CultureInfo.InvariantCulture), false));
                cells.Add((c, boxes.HitsRow + 1, $":{box.HpMax}", false)); // 615
            }
            string Mark(P3BodyArea area) =>
                boxes.Marks[Math.Clamp(box.Areas.Count > (int)area ? box.Areas[(int)area] : 0, 0, boxes.Marks.Length - 1)].ToString();
            cells.Add((c + 1, boxes.BodyRow, Mark(P3BodyArea.Head), false)); // 630-635
            cells.Add((c, boxes.BodyRow + 1, Mark(P3BodyArea.LeftArm), false));
            cells.Add((c + 2, boxes.BodyRow + 1, Mark(P3BodyArea.RightArm), false));
            cells.Add((c + 1, boxes.BodyRow + 2, Mark(P3BodyArea.Torso), false));
            cells.Add((c, boxes.BodyRow + 3, Mark(P3BodyArea.LeftLeg), false));
            cells.Add((c + 2, boxes.BodyRow + 3, Mark(P3BodyArea.RightLeg), false));
            var status = UiText.S($"p3combat.status.{(int)box.Status}").Trim(); // 620: S$ down the column
            for (var r = 0; r < 4 && r < status.Length; r++)
                cells.Add((c + boxes.StatusDx, boxes.BodyRow + r, status[r].ToString(), false));
        }
        return cells;
    }

    public static void DrawApple2Field(SpriteBatch batch, Texture2D pixel, AssetCatalog assets, CombatPresenter presenter)
    {
        if (presenter.AppleShown is not { } state || presenter.AppleSnapshot is not { } snapshot
            || assets.Pack?.Apple2Field is not { } layout || assets.Apple2FieldArt is not { } art)
            return;
        Fill(batch, pixel, new Rectangle(0, 0, 320, ScreenBottom), Color.Black);
        if (layout.Plot is { } plot)
        {
            DrawApple2Plot(batch, pixel, assets, layout, plot, art, snapshot, state);
            return;
        }
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

    /// <summary>III's screen (s228): the pictures of <see cref="Apple2PlotPlaces"/>, the party columns and the message box.</summary>
    private static void DrawApple2Plot(SpriteBatch batch, Texture2D pixel, AssetCatalog assets, Apple2FieldLayout layout,
        Apple2FieldPlot plot, Apple2FieldArt art, Apple2FieldSnapshot snapshot, Apple2FieldRun.State state)
    {
        foreach (var place in Apple2PlotPlaces(layout, art, snapshot, state))
            if (assets.GetFile(place.File) is { } tex)
            {
                var at = new Rectangle(place.X, place.Y, tex.Width, tex.Height);
                if (place.Opaque)
                    Fill(batch, pixel, at, Color.Black);
                batch.Draw(tex, at, Color.White);
            }
        if (plot.Boxes is { } boxes)
            foreach (var (column, row, text, inverse) in Apple2PlotBoxCells(boxes, snapshot))
            {
                var x = layout.ScreenX + column * layout.CellWidth;
                var y = layout.ScreenY + row * layout.CellHeight;
                if (inverse)
                    Fill(batch, pixel, new Rectangle(x, y, text.Length * layout.CellWidth, layout.CellHeight), Color.White);
                DrawCells(batch, pixel, layout.CellWidth, x, y, text, inverse ? Color.Black : Color.White);
            }
        for (var row = 0; row < state.Window.Count && row < layout.TextRows; row++)
            DrawCells(batch, pixel, layout.CellWidth, layout.ScreenX + layout.TextLeft * layout.CellWidth,
                layout.ScreenY + (layout.TextTop + row) * layout.CellHeight, state.Window[row], Color.White);
    }
}
