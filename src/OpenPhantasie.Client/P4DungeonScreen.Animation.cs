using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using OpenPhantasie.Core;

using OpenPhantasie.Data;

namespace OpenPhantasie.Client;

/// <summary>
/// IV's picture animations on the town pictures (T6.4, s110; <c>FORMAT.md</c> §6.10): the street, Guild, Inn, Bank,
/// Shipwright, Mystic and ending pictures run their <c>_setanim</c> records at the pass length measured in XEiJ for that
/// picture's input loop (the pack's <c>pass_ms</c>), and a building picked on the street first shows its open door
/// (<c>_animdoor</c>) for the measured hold, while P4.X busy-waits (no keys, no animation).
/// </summary>
public sealed partial class P4DungeonScreen
{
    private PictureAnimationPlayer? _animation;
    private PictureAnimationSet? _animationSet;
    private PendingDoor? _door;
    private P4MysticReading? _mystic;

    /// <summary>The Mystic scene's key: its visit plays <see cref="P4MysticReading"/> (the pack's reading scene gives the bare pass).</summary>
    public const string MysticScene = "p4.mystic", MysticReadingScene = "p4.mystic.reading",
        MysticBubble = "p4.building.mystic.bubble";

    /// <summary>The Mystic's visit while its picture is up (null elsewhere, or without the pack).</summary>
    public P4MysticReading? MysticReading => _mystic;

    /// <summary>True while the Mystic's counted loops run (<c>_mystic</c> reads no keys until <c>_spc_wait</c>).</summary>
    public bool MysticBusy => _mystic is { AcceptsKeys: false };

    /// <summary>
    /// QoL MYSTIC SKIP (<see cref="GameSettings.MysticSkip"/>, set by the shell each frame; off = P4.X): a skip key during
    /// the counted loops ends them at once (<see cref="P4MysticReading.SkipLoops"/>).
    /// </summary>
    public bool MysticSkippable { get; set; }

    /// <summary>The keys that skip the Mystic's loops under <see cref="MysticSkippable"/> (the pad's A and a click come from the shell).</summary>
    public static bool IsMysticSkipKey(Keys key) => key is Keys.Space or Keys.Enter;

    /// <summary>
    /// Ends the Mystic's counted loops when <see cref="MysticSkippable"/> is on and they run; true when it did (the shell
    /// then hands this frame's input to nothing else, so the same key does not also close the Mystic in <c>_spc_wait</c>).
    /// </summary>
    public bool SkipMystic() => MysticSkippable && MysticBusy && _mystic!.SkipLoops();

    private sealed record PendingDoor(PictureDoor Door, string Sheet, P4Building Building)
    {
        public double LeftMilliseconds { get; set; }
    }

    /// <summary>The animation player (null without a pack that has <c>picture_animations</c>).</summary>
    public PictureAnimationPlayer? Animation => _animation;

    /// <summary>True while a picked building's open door is up (the original's busy wait: keys are not read).</summary>
    public bool DoorOpen => _door is not null;

    /// <summary>One frame of real time: the location-map view's cycling; the door's hold, else the animation of the picture now up.</summary>
    public void Tick(double milliseconds)
    {
        TickCursor(milliseconds); // s136: the typed prompts' cursor bar
        TickView(milliseconds);
        TickWelcome(milliseconds); // s151: _tload's "Welcome!" while the street loads
        var set = Assets?.Pack?.PictureAnimations;
        if (!ReferenceEquals(set, _animationSet))
        {
            _animationSet = set;
            _animation = set is null ? null : new PictureAnimationPlayer(set.Table, new SystemRandomSource());
            _door = null;
            _mystic = null;
        }
        if (_door is { } door)
        {
            door.LeftMilliseconds -= milliseconds;
            if (door.LeftMilliseconds > 0)
                return;
            _door = null;
            W.Enter(door.Building);
        }
        if (_animation is null || set is null)
            return;
        if (SceneName == MysticScene && set.Scenes.TryGetValue(MysticScene, out var mystic)
            && set.Scenes.TryGetValue(MysticReadingScene, out var reading)
            && set.PassMillisecondsFor(mystic) is double wait && set.PassMillisecondsFor(reading) is double bare)
        {
            _mystic ??= new P4MysticReading(_animation, MysticScene, bare, wait);
            _mystic.Advance(milliseconds);
            return;
        }
        _mystic = null;
        if (SceneName is { } name && set.Scenes.TryGetValue(name, out var scene) && set.PassMillisecondsFor(scene) is double pass)
            _animation.Show(name, scene.First, scene.Last, pass);
        else
            _animation.Hide();
        _animation.Advance(milliseconds);
    }

    /// <summary>P4.X's building index on the street (<c>_bvs</c>'s pick, the door table's column), or −1.</summary>
    public static int DoorPick(P4Building building) => building switch
    {
        P4Building.Guild or P4Building.Mystic or P4Building.Armory or P4Building.Bank or P4Building.Inn => (int)building,
        P4Building.HallOfGlory or P4Building.Shipwright => 5,
        _ => -1,
    };

    /// <summary>A building picked on the street: its open door first when the pack has it, then the building.</summary>
    private void EnterFromStreet(P4Building building)
    {
        var set = Assets?.Pack?.PictureAnimations;
        var pick = DoorPick(building);
        if (set is { DoorHoldMilliseconds: double hold } && W.Scene == P4TownScene.Street && W.Site is { } site && pick >= 0
            && site.Type < set.DoorsByTownType.Count && pick < set.DoorsByTownType[site.Type].Count
            && set.DoorsByTownType[site.Type][pick] is { } door
            && set.Scenes.TryGetValue($"p4.street.{site.Type}", out var street) && Assets!.Get(street.Sheet) is not null)
        {
            _door = new PendingDoor(door, street.Sheet, building) { LeftMilliseconds = hold };
            return;
        }
        W.Enter(building);
    }

    /// <summary>
    /// Draws the animation frames now up, and the open door, over the scene drawn at <paramref name="origin"/> with
    /// <paramref name="k"/> layout pixels per pack pixel.
    /// </summary>
    private void DrawAnimation(SpriteBatch batch, Vector2 origin, float k, Texture2D pixel)
    {
        DrawFrames(batch, origin, k);
        DrawStreetBanner(batch, pixel, origin, k); // s136: _tload's town name (no animation record reaches its rows)
        DrawMysticBubble(batch, pixel, origin, k);
    }

    /// <summary>
    /// The Mystic's speech bubble (<c>MYSTLK</c> at the picture's origin, <c>0x6C68</c>) once the reading is done, with the
    /// score and verdict in it [C], seen on XEiJ s120: the score line <c>_wlocpr</c> left-aligned from window (64, 16), the
    /// score's digits in colour 2 straight after the text (<c>_wdouti</c>'s unpadded format), the verdict <c>_wprcen</c>
    /// centred on x 156 at y 40; black on the bubble, in the pack's <see cref="MysticFontRole"/> font (P4.X font 0, s135)
    /// when it has one, else the remaster's.
    /// </summary>
    private void DrawMysticBubble(SpriteBatch batch, Texture2D pixel, Vector2 origin, float k)
    {
        if (_mystic is not { BubbleUp: true } || SceneName != MysticScene || Assets?.Get(MysticBubble) is not { } bubble)
            return;
        batch.Draw(bubble, origin, null, Color.White, 0f, Vector2.Zero, k, SpriteEffects.None, 0f);
        var red = Assets.PaletteColour("pkh", 2) ?? Color.Red;
        var boxLines = W.Box.Where(l => l.Length > 0).ToList();
        if (boxLines.Count == 0)
            return;
        if (FontSources.ForRole(Assets.Pack, MysticFontRole) is { } font)
        {
            DrawMysticBubbleText(batch, pixel, origin, k, font, boxLines, red);
            return;
        }
        // Line 1 from x 64 (wrapped to the bubble's right edge), the verdict centred; a wrapped score line pushes it down.
        var scoreLines = UiOverlays.Wrap(boxLines[0], (int)((BubbleRight - BubbleScoreX) * k) / PixelFont.Advance).ToList();
        var y = BubbleScoreY * k;
        var prefix = GameText.S("p4x.0x40237");
        foreach (var line in scoreLines)
        {
            var x = (int)Math.Round(origin.X + BubbleScoreX * k);
            var digits = line.StartsWith(prefix, StringComparison.Ordinal)
                ? line[prefix.Length..].TakeWhile(char.IsAsciiDigit).Count() : 0;
            if (digits > 0)
            {
                PixelFont.Draw(batch, pixel, x, (int)Math.Round(origin.Y + y), prefix, Color.Black);
                x += PixelFont.Measure(prefix);
                PixelFont.Draw(batch, pixel, x, (int)Math.Round(origin.Y + y), line.Substring(prefix.Length, digits), red);
                x += PixelFont.Measure(line.Substring(prefix.Length, digits));
                PixelFont.Draw(batch, pixel, x, (int)Math.Round(origin.Y + y), line[(prefix.Length + digits)..], Color.Black);
            }
            else
                PixelFont.Draw(batch, pixel, x, (int)Math.Round(origin.Y + y), line, Color.Black);
            y += PixelFont.LineHeight;
        }
        y = Math.Max(y, BubbleVerdictY * k);
        var cols = (int)(BubbleTextWidth * k) / PixelFont.Advance;
        foreach (var line in boxLines.Skip(1).SelectMany(l => UiOverlays.Wrap(l, cols)))
        {
            var w = PixelFont.Measure(line);
            PixelFont.Draw(batch, pixel, (int)Math.Round(origin.X + BubbleCentreX * k - w / 2f), (int)Math.Round(origin.Y + y),
                line, Color.Black);
            y += PixelFont.LineHeight;
        }
    }

    /// <summary>The pack's font role for the Mystic's bubble (P4.X font 0, style 0: <c>_mystic</c>'s <c>_font(0, 0, 0, 16)</c>).</summary>
    public const string MysticFontRole = "p4.mystic.bubble";

    /// <summary>
    /// The bubble's text in the original's window font (s135), in pack pixels like the picture: the score line from
    /// window (64, 16) (<c>_wlocpr</c>; the digits in colour 2 straight after, <c>_wdouti</c>'s unpadded format), the verdict
    /// from <c>_wprcen(win, 156, 40, …)</c> = x 156 − width / 2 (integer half, <c>asr</c>); black (<c>_fgcol</c> 0). Lines
    /// longer than the bubble wrap (the translation's), never cut; a wrapped line steps the font's 16 rows.
    /// </summary>
    private static void DrawMysticBubbleText(SpriteBatch batch, Texture2D pixel, Vector2 origin, float k, IFontSource font,
        IReadOnlyList<string> boxLines, Color red)
    {
        void Text(float x, float y, string text, Color c) =>
            PixelFont.DrawScaled(batch, pixel, origin + new Vector2(x * k, y * k), text, c, k, font);
        var prefix = GameText.S("p4x.0x40237");
        var y = BubbleScoreY;
        foreach (var line in UiOverlays.Wrap(boxLines[0], (int)(BubbleRight - BubbleScoreX) / PixelFont.Advance, font))
        {
            var digits = line.StartsWith(prefix, StringComparison.Ordinal)
                ? line[prefix.Length..].TakeWhile(char.IsAsciiDigit).Count() : 0;
            if (digits > 0)
            {
                var x = BubbleScoreX;
                Text(x, y, prefix, Color.Black);
                x += PixelFont.Measure(prefix, font);
                Text(x, y, line.Substring(prefix.Length, digits), red);
                x += PixelFont.Measure(line.Substring(prefix.Length, digits), font);
                Text(x, y, line[(prefix.Length + digits)..], Color.Black);
            }
            else
                Text(BubbleScoreX, y, line, Color.Black);
            y += font.LineHeight;
        }
        y = Math.Max(y, BubbleVerdictY);
        foreach (var line in boxLines.Skip(1).SelectMany(l => UiOverlays.Wrap(l, (int)BubbleTextWidth / PixelFont.Advance, font)))
        {
            Text(BubbleCentreX - (PixelFont.Measure(line, font) >> 1), y, line, Color.Black);
            y += font.LineHeight;
        }
    }

    /// <summary>The bubble's text: the score line from window (64, 16), the verdict at y 40 centred on x 156 (pack pixels);
    /// the bubble spans window x 40–262 (<c>MYSTLK.PKH</c>, XEiJ s120).</summary>
    private const float BubbleCentreX = 156, BubbleScoreX = 64, BubbleScoreY = 16, BubbleVerdictY = 40, BubbleRight = 258,
        BubbleTextWidth = 216;

    private void DrawFrames(SpriteBatch batch, Vector2 origin, float k)
    {
        if (_animationSet is not { } set || _animation?.Scene is not { } name || name != SceneName
            || !set.Scenes.TryGetValue(name, out var scene) || Assets?.Get(scene.Sheet) is not { } sheet)
        {
            DrawDoor(batch, origin, k);
            return;
        }
        foreach (var (record, frame) in _animation.Shown)
        {
            var r = set.Table.Records[record];
            var f = set.Table.Frames[frame];
            batch.Draw(sheet, origin + new Vector2((scene.X + r.X) * k, (scene.Y + r.Y) * k),
                new Rectangle(f.SheetX, f.SheetY, r.Width, r.Height), Color.White, 0f, Vector2.Zero, k, SpriteEffects.None, 0f);
        }
        DrawDoor(batch, origin, k);
    }

    private void DrawDoor(SpriteBatch batch, Vector2 origin, float k)
    {
        if (_door is not { } door || _animationSet is not { } set || Assets?.Get(door.Sheet) is not { } sheet)
            return;
        batch.Draw(sheet, origin + new Vector2(door.Door.X * k, door.Door.Y * k),
            new Rectangle(door.Door.SheetX, door.Door.SheetY, set.DoorWidth, set.DoorHeight), Color.White, 0f, Vector2.Zero, k,
            SpriteEffects.None, 0f);
    }
}
