using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client;

/// <summary>
/// Textures of the active graphics pack (T1.5). The Client asks for named pictures (<see cref="PictureNames"/>: "town
/// backdrop", "monster 42 idle", "dungeon glyph 0xe6"…); the pack's manifest maps names to files. A name the pack lacks
/// gives null and the caller draws what that platform showed there — never another pack's art. With no pack (no
/// extracts found) every picture is null, as before T1.5. Which pack: <see cref="GraphicsPackCatalog.Default"/> (Amiga
/// for Phantasie I); the DOS CGA pack is drawn only when chosen explicitly.
/// </summary>
public sealed class AssetCatalog : IDisposable
{
    private readonly GraphicsDevice _device;
    private readonly Dictionary<string, Texture2D> _byPath = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Texture2D?> _byName = new(StringComparer.Ordinal);
    private readonly Dictionary<(string Name, int Phase), Texture2D> _cycled = new();
    private readonly Dictionary<(string Name, string Palette), Texture2D?> _repaletted = new();

    public AssetCatalog(GraphicsDevice device, GraphicsPack? pack)
    {
        _device = device;
        Pack = pack;
    }

    /// <summary>The pack in use, or null when no pack's files were found.</summary>
    public GraphicsPack? Pack { get; }

    public bool IsAvailable => Pack is not null;

    /// <summary>The pack's screen (resolution, pixel shape, background); the remaster default without a pack.</summary>
    public ScreenSpec Screen => Pack?.Screen ?? ScreenSpec.Default;

    /// <summary>
    /// Play time in seconds that drives the pack's <see cref="ColourCycle"/> (the ST overworld's water); the Client sets
    /// it each frame. Packs without a cycle ignore it.
    /// </summary>
    public double CycleSeconds { get; set; }

    /// <summary>
    /// Picture <paramref name="name"/> from the active pack, or null when the pack has none. A picture the pack's colour
    /// cycle covers comes back as it shows at <see cref="CycleSeconds"/>.
    /// </summary>
    public Texture2D? Get(string name)
    {
        if (Pack is null) return null;
        if (!_byName.TryGetValue(name, out var tex))
        {
            var path = Pack.Resolve(name);
            tex = path is null ? null : Load(path);
            _byName[name] = tex;
        }
        if (tex is not null && Pack.ColourCycle is { } cycle && cycle.Covers(name) &&
            cycle.Phase(CycleSeconds) is var phase and not 0)
            return Cycled(name, tex, cycle, phase);
        return tex;
    }

    /// <summary>
    /// <paramref name="source"/> after <paramref name="phase"/> cycle steps: each pixel stored in a cycled register's
    /// colour shows the colour that register holds now (<see cref="ColourCycle.Shown"/>); other pixels stay.
    /// </summary>
    private Texture2D Cycled(string name, Texture2D source, ColourCycle cycle, int phase)
    {
        if (_cycled.TryGetValue((name, phase), out var hit))
            return hit;
        var data = new Color[source.Width * source.Height];
        source.GetData(data);
        RecolourForCycle(data, cycle, phase);
        var tex = new Texture2D(_device, source.Width, source.Height);
        tex.SetData(data);
        return _cycled[(name, phase)] = tex;
    }

    /// <summary>
    /// Picture <paramref name="name"/> cut in pack palette <paramref name="from"/> as it shows with palette
    /// <paramref name="to"/> in force (each pixel in <c>from</c>'s colour i takes <c>to</c>'s colour i; III Amiga: the
    /// keep windows' buttons in a dungeon, s125). The picture as cut when either palette is unknown or they are the same.
    /// </summary>
    public Texture2D? GetInPalette(string name, string? from, string? to)
    {
        if (Get(name) is not { } source || from is null || to is null || from == to || Pack is null
            || !Pack.Palettes.TryGetValue(from, out var cut) || !Pack.Palettes.TryGetValue(to, out var shown))
            return Get(name);
        if (_repaletted.TryGetValue((name, to), out var hit))
            return hit;
        var data = new Color[source.Width * source.Height];
        source.GetData(data);
        Repalette(data, cut, shown);
        var tex = new Texture2D(_device, source.Width, source.Height);
        tex.SetData(data);
        return _repaletted[(name, to)] = tex;
    }

    private readonly Dictionary<(string Name, Color Ink, Color Paper), Texture2D?> _silhouettes = new();

    /// <summary>
    /// s156, II ST's silhouette (<c>0x18f7a</c>, FORMAT.md 7.3): picture <paramref name="name"/>'s whole rectangle, every pixel
    /// not in register 0's colour <paramref name="zero"/> in <paramref name="ink"/>, the rest in <paramref name="paper"/>
    /// (the four planes ORed, drawn with <c>vrt_cpyfm</c> replace). Null when the pack has no such picture.
    /// </summary>
    public Texture2D? GetSilhouette(string name, Color zero, Color ink, Color paper)
    {
        if (Get(name) is not { } source)
            return null;
        if (_silhouettes.TryGetValue((name, ink, paper), out var hit))
            return hit;
        var data = new Color[source.Width * source.Height];
        source.GetData(data);
        Silhouette(data, zero, ink, paper);
        var tex = new Texture2D(_device, source.Width, source.Height);
        tex.SetData(data);
        return _silhouettes[(name, ink, paper)] = tex;
    }

    /// <summary>The pixel mapping of <see cref="GetSilhouette"/>: <paramref name="zero"/>'s RGB (or a clear pixel) → paper, else ink.</summary>
    public static void Silhouette(Span<Color> pixels, Color zero, Color ink, Color paper)
    {
        for (var i = 0; i < pixels.Length; i++)
        {
            var c = pixels[i];
            pixels[i] = c.A == 0 || c.R == zero.R && c.G == zero.G && c.B == zero.B ? paper : ink;
        }
    }

    /// <summary>The pixel mapping of <see cref="GetInPalette"/> (opaque pixels matched by exact RGB, first index wins; alpha kept).</summary>
    public static void Repalette(Span<Color> pixels, IReadOnlyList<string> from, IReadOnlyList<string> to)
    {
        var cut = from.Select(VirtualScreen.ParseColor).ToArray();
        var shown = to.Select(VirtualScreen.ParseColor).ToArray();
        for (var i = 0; i < pixels.Length; i++)
        {
            var c = pixels[i];
            if (c.A != 255) continue;
            for (var j = 0; j < Math.Min(cut.Length, shown.Length); j++)
                if (c.R == cut[j].R && c.G == cut[j].G && c.B == cut[j].B)
                {
                    pixels[i] = shown[j];
                    break;
                }
        }
    }

    /// <summary>The pixel mapping of <see cref="Cycled"/> (opaque pixels matched by exact RGB; alpha kept).</summary>
    public static void RecolourForCycle(Span<Color> pixels, ColourCycle cycle, int phase)
    {
        var stored = cycle.Colours.Select(VirtualScreen.ParseColor).ToArray();
        var shown = Enumerable.Range(0, stored.Length).Select(j => VirtualScreen.ParseColor(cycle.Shown(j, phase))).ToArray();
        for (var i = 0; i < pixels.Length; i++)
        {
            var c = pixels[i];
            if (c.A != 255) continue;
            for (var j = 0; j < stored.Length; j++)
                if (c.R == stored[j].R && c.G == stored[j].G && c.B == stored[j].B)
                {
                    pixels[i] = shown[j];
                    break;
                }
        }
    }

    /// <summary>Pack pixels per layout pixel across (1 for a 320-wide pack, 2 for IV's 640 × 400 X68000 pack).</summary>
    public float PackPixelsPerLayoutPixel => VirtualScreen.LayoutScale(Screen).X;

    /// <summary>
    /// Scene <paramref name="name"/> of the pack (<see cref="GraphicsPack.Scenes"/>): each picture's texture at its offset,
    /// and the scene's size, in pack pixels. Null when the pack has no such scene or any of its pictures is missing.
    /// </summary>
    public LoadedScene? GetScene(string name)
    {
        if (Pack?.Scenes.TryGetValue(name, out var parts) != true)
            return null;
        return ComposeScene(parts!.Select(p => (p.Picture, p.X, p.Y, 1)));
    }

    /// <summary>
    /// A scene put together at run time (IV's outdoor map, s108): each picture at its offset, drawn <c>Scale</c> times its
    /// size (P4.X's <c>_scdisd2</c> doubles a picture). Null when a picture is missing. <paramref name="minWidth"/> /
    /// <paramref name="minHeight"/> keep the scene at least that size when edge parts are left out (D4-072's black cells).
    /// </summary>
    public LoadedScene? ComposeScene(IEnumerable<(string Picture, int X, int Y, int Scale)> parts, int minWidth = 0, int minHeight = 0)
    {
        var loaded = new List<(Texture2D Texture, int X, int Y, int Scale)>();
        foreach (var (picture, x, y, scale) in parts)
        {
            if (Get(picture) is not { } tex)
                return null;
            loaded.Add((tex, x, y, scale));
        }
        return loaded.Count == 0 ? null : new LoadedScene(loaded,
            Math.Max(minWidth, loaded.Max(p => p.X + p.Texture.Width * p.Scale)),
            Math.Max(minHeight, loaded.Max(p => p.Y + p.Texture.Height * p.Scale)));
    }

    public Texture2D? TitleCover => Get(PictureNames.TitleCover);
    /// <summary>Town picture (Amiga <c>pelnor.pic</c>, every town: art on rows 11..177, the Amiga menu text on rows 0..8).</summary>
    public Texture2D? TownBackdrop => Get(PictureNames.TownBackdrop);
    /// <summary>The open doors of the town street, each at its own place (III Amiga <c>D/w3</c>; <see cref="TownScreenLayout.Doors"/>).</summary>
    public Texture2D? TownDoors => Get(PictureNames.TownDoors);

    /// <summary>
    /// <see cref="TownBackdrop"/> as town <paramref name="townId"/> shows it: in the pack's dim palette for a
    /// <see cref="TownScreenLayout.DimTowns"/> town (III Amiga towns 16–17, <c>0x23050</c>), else as it is.
    /// </summary>
    public Texture2D? TownBackdropFor(int? townId) => DimFor(PictureNames.TownBackdrop, townId);

    /// <summary><see cref="TownDoors"/> in the town's palette (<see cref="TownBackdropFor"/>).</summary>
    public Texture2D? TownDoorsFor(int? townId) => DimFor(PictureNames.TownDoors, townId);

    /// <summary>Whether town <paramref name="townId"/> shows in the pack's dim palette.</summary>
    public bool IsDimTown(int? townId) => townId is { } t && TownScreen.DimTowns.Contains(t);

    private Texture2D? DimFor(string name, int? townId)
    {
        var tex = Get(name);
        var layout = TownScreen;
        if (tex is null || !IsDimTown(townId) || Pack?.Palettes.GetValueOrDefault(layout.DimFrom ?? "") is not { } from
            || Pack.Palettes.GetValueOrDefault(layout.DimPalette ?? "") is not { } to)
            return tex;
        if (_dimmed.TryGetValue(name, out var hit))
            return hit;
        var data = new Color[tex.Width * tex.Height];
        tex.GetData(data);
        Recolour(data, from, to);
        var dimmed = new Texture2D(_device, tex.Width, tex.Height);
        dimmed.SetData(data);
        return _dimmed[name] = dimmed;
    }

    private readonly Dictionary<string, Texture2D> _dimmed = new();

    /// <summary>Opaque pixels of colour <paramref name="from"/>[i] (exact RGB) become <paramref name="to"/>[i]; alpha kept.</summary>
    public static void Recolour(Span<Color> pixels, IReadOnlyList<string> from, IReadOnlyList<string> to)
    {
        var a = from.Select(VirtualScreen.ParseColor).ToArray();
        var b = to.Select(VirtualScreen.ParseColor).ToArray();
        for (var i = 0; i < pixels.Length; i++)
        {
            var c = pixels[i];
            if (c.A != 255) continue;
            for (var j = 0; j < Math.Min(a.Length, b.Length); j++)
                if (c.R == a[j].R && c.G == a[j].G && c.B == a[j].B)
                {
                    pixels[i] = b[j];
                    break;
                }
        }
    }
    public Texture2D? Decor => Get(PictureNames.Decor);
    public Texture2D? DecorDoor => Get(PictureNames.DecorDoor);
    public Texture2D? DecorDemonFace => Get(PictureNames.DecorDemonFace);
    public Texture2D? DecorSorrySign => Get(PictureNames.DecorSorrySign);
    public Texture2D? PortraitGhost => Get(PictureNames.PortraitGhost);
    public Texture2D? PortraitDeathMark => Get(PictureNames.PortraitDeathMark);

    /// <summary>Where the town screen's bar and name sit on <see cref="TownBackdrop"/> (pack <c>town_screen</c>).</summary>
    public TownScreenLayout TownScreen => Pack?.TownScreen ?? TownScreenLayout.Default;

    /// <summary>The pack's map geometry (Amiga <c>maps.json</c>), or null when the pack has none.</summary>
    public AmigaMapManifest? Maps => Pack?.MapLayout;

    /// <summary>Pre-rendered picture of a whole overworld region (Amiga: 26×20 cells of 12×9), or null.</summary>
    public Texture2D? GetRegionPicture(int regionId) => Get(PictureNames.OverworldRegion(regionId));

    /// <summary>Town / inn / dungeon icon, or null.</summary>
    public Texture2D? GetOverworldIcon(string kind) => Get(PictureNames.OverworldIcon(kind));

    /// <summary>The overworld party sprite, or null.</summary>
    public Texture2D? GetOverworldParty() => Get(PictureNames.OverworldParty);

    /// <summary>Dungeon map glyph for glyph id <c>0xe6..0xed</c>, or null.</summary>
    public Texture2D? GetDungeonGlyph(int glyph) => Get(PictureNames.DungeonGlyph(glyph));

    /// <summary>The dungeon compass, or null.</summary>
    public Texture2D? GetDungeonCompass() => Get(PictureNames.DungeonCompass);

    /// <summary>The pack's tile-drawn map geometry (Phantasie III Amiga), or null when the pack has none.</summary>
    public TileMapLayout? TileMaps => Pack?.TileMaps;

    /// <summary>Overworld tile for map byte <paramref name="tile"/> (tile-drawn packs), or null.</summary>
    public Texture2D? GetOverworldTile(int tile) => Get(PictureNames.OverworldTile(tile));

    /// <summary>Dungeon map glyph <paramref name="glyph"/> of a tile-drawn pack (<see cref="TileMapLayout.DungeonGlyphFor"/>), or null.</summary>
    public Texture2D? GetDungeonCell(int glyph) => Get(PictureNames.DungeonCell(glyph));

    /// <summary>The picture scroll item <paramref name="itemId"/> shows (III's Scroll IV), or null.</summary>
    public Texture2D? GetScrollPicture(int itemId) => Get(PictureNames.ScrollPicture(itemId));

    public Texture2D? GetMonsterIdle(int monsterId) => Get(PictureNames.MonsterIdle(monsterId));

    public Texture2D? GetPortraitIdle(int raceId) => Get(PictureNames.PortraitIdle(raceId));

    public Texture2D? GetMonsterAttacking(int monsterId) => Get(PictureNames.MonsterAttacking(monsterId));

    public Texture2D? GetPortraitAttacking(int raceId) => Get(PictureNames.PortraitAttacking(raceId));

    public Texture2D? PortraitElementalAttacking => Get(PictureNames.PortraitElementalAttacking);

    /// <summary>III's party-defeat scene, the gates of the Astral Plane (Amiga <c>D/W2.cmp</c>), or null.</summary>
    public Texture2D? SceneAstralPlane => Get(PictureNames.SceneAstralPlane);
    public Texture2D? PortraitGrave => Get(PictureNames.PortraitGrave);

    /// <summary>III's gods' reward picture (Amiga <c>D/W1.cmp</c> Zeus when <paramref name="good"/>, else <c>D/W0.cmp</c> Pluto), or null.</summary>
    public Texture2D? SceneReward(bool good) => Get(good ? PictureNames.SceneRewardGood : PictureNames.SceneRewardEvil);

    /// <summary>The gods' reward scene's flashes and tune (III Amiga), or null.</summary>
    public RewardSceneLayout? RewardScene => Pack?.RewardScene;

    /// <summary>The fight's spell flash (III Amiga), or null.</summary>
    public CombatFlashLayout? CombatFlash => Pack?.CombatFlash;

    /// <summary>The original's sound effects (III Amiga), or null.</summary>
    public OpenPhantasie.Data.Audio.P3SoundEffects? SoundEffects => Pack?.SoundEffects;

    /// <summary>
    /// Colour <paramref name="index"/> of the pack's named palette <paramref name="palette"/> (<see cref="GraphicsPack.Palettes"/>:
    /// a screen's own colours, e.g. III's <c>astral</c> = <c>D/W2.cmp</c>'s), or null when the pack has no such entry.
    /// </summary>
    public Color? PaletteColour(string palette, int index) =>
        Pack?.Palettes.TryGetValue(palette, out var colours) == true && index >= 0 && index < colours.Count
            ? VirtualScreen.ParseColor(colours[index])
            : null;

    /// <summary>A summoned elemental's portrait (III Amiga picture 22), or null.</summary>
    public Texture2D? PortraitElemental => Get(PictureNames.PortraitElemental);

    private int? _portraitHeight;

    /// <summary>
    /// Rows of the tallest idle portrait (every race and the elemental) at the party cards' 32-pixel width: 48 for III
    /// Amiga's full figures, 32 for I and II (and without a pack). Card and box heights follow it, so no portrait is cut.
    /// </summary>
    public int PortraitHeight => _portraitHeight ??= Enum.GetValues<OpenPhantasie.Core.Race>()
        .Select(r => GetPortraitIdle((int)r))
        .Append(PortraitElemental)
        .OfType<Texture2D>()
        .Select(t => (t.Height * PartyStripLayout.PortraitSize + t.Width - 1) / t.Width)
        .Append(PartyStripLayout.PortraitSize)
        .Max();

    /// <summary>The pack's body-area figure (III Amiga stick figure), or null when the pack has none.</summary>
    public BodyFigureLayout? BodyFigure => Pack?.BodyFigure;

    /// <summary>Body-area figure piece <paramref name="slot"/>, or null.</summary>
    public Texture2D? GetBodyPiece(int slot) => Get(PictureNames.BodyPiece(slot));

    /// <summary>The pack's text-scroll frame (III Amiga parchment), or null when the pack has none.</summary>
    public ScrollFrameLayout? ScrollFrame => Pack?.ScrollFrame;

    /// <summary>The pack's own keep-question windows (III Amiga OK / CANCEL), or null when the pack has none.</summary>
    public KeepWindowsLayout? KeepWindows => Pack?.KeepWindows;

    /// <summary>Text-scroll frame piece <paramref name="slot"/>, or null.</summary>
    public Texture2D? GetScrollFramePiece(int slot) => Get(PictureNames.ScrollFramePiece(slot));

    public void Dispose()
    {
        foreach (var tex in _byPath.Values)
            tex.Dispose();
        foreach (var tex in _cycled.Values)
            tex.Dispose();
        _cycled.Clear();
        foreach (var tex in _repaletted.Values)
            tex?.Dispose();
        _repaletted.Clear();
        foreach (var tex in _silhouettes.Values)
            tex?.Dispose();
        _silhouettes.Clear();
        _byPath.Clear();
        _byName.Clear();
    }

    private Texture2D Load(string path)
    {
        if (_byPath.TryGetValue(path, out var existing))
            return existing;

        using var stream = File.OpenRead(path);
        var tex = Texture2D.FromStream(_device, stream);
        PremultiplyAlpha(tex);
        _byPath[path] = tex;
        return tex;
    }

    /// <summary>
    /// FromStream yields straight alpha; SpriteBatch defaults to premultiplied AlphaBlend.
    /// </summary>
    private static void PremultiplyAlpha(Texture2D texture)
    {
        var data = new Color[texture.Width * texture.Height];
        texture.GetData(data);
        for (var i = 0; i < data.Length; i++)
        {
            var c = data[i];
            data[i] = new Color(
                (byte)(c.R * c.A / 255),
                (byte)(c.G * c.A / 255),
                (byte)(c.B * c.A / 255),
                c.A);
        }

        texture.SetData(data);
    }
}

/// <summary>A pack scene ready to draw (<see cref="AssetCatalog.GetScene"/>): pictures at offsets, size in pack pixels.</summary>
public sealed record LoadedScene(IReadOnlyList<(Texture2D Texture, int X, int Y, int Scale)> Parts, int Width, int Height);
