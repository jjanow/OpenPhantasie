using System.Text.Json.Nodes;
using OpenPhantasie.Importer.Formats;
using static OpenPhantasie.Importer.Pipelines.P1Amiga.AmigaGfx;

namespace OpenPhantasie.Importer.Pipelines.P1Amiga;

/// <summary>
/// Port of <c>tools/scripts/extract_amiga_graphics.py</c>: monster sprites (<c>mstr1/2.pat</c>), race portraits
/// (<c>party1/2.pat</c>), the screen pictures (<c>*.pic</c>), bitmaps and hardware sprites cut from <c>game</c>, the
/// palette swatches, the contact sheets and <c>extraction-index.json</c>. Offsets and palettes: the script's notes and
/// <c>reverse-engineering/amiga/FORMAT.md</c>.
/// </summary>
internal static class P1AmigaGraphics
{
    // (file, width, height, output, palette offset, hw4)
    private static readonly (string File, int W, int H, string Rel, int Pal, bool Hw4)[] ScreenPics =
    [
        ("cover1.pic", 320, 200, "ui/cover1.png", PaletteAltOff, true),
        ("cover2.pic", 320, 200, "ui/cover2.png", PaletteAltOff, true),
        ("decor.pic", 320, 200, "ui/decor.png", PaletteCombatOff, true),
        ("pelnor.pic", 320, 200, "ui/pelnor-town.png", PaletteTownOff, true),
        ("final.pic", 320, 200, "ui/final.png", PaletteCombatOff, true),
        ("logo.pic", 176, 80, "ui/logo.png", PaletteMapOff, true),
    ];

    private const int Hunk69Off = 0x19428;

    private static readonly (int Off, int W, int H, string Rel)[] Hunk69Bitmaps =
    [
        (0x994, 32, 32, "ui/ending-ghost.png"),
        (0x3694, 32, 37, "ui/ending-arm.png"),
        (0x1814, 80, 90, "ui/decor-door.png"),
        (0xB94, 80, 80, "ui/decor-demon-face.png"),
        (0x32D4, 48, 40, "ui/decor-sorry-sign.png"),
    ];

    private static readonly (int Off, int W, int H, string Rel)[] GameBitmaps =
    [
        (0x167AA, 16, 19, "ui/combat-grave.png"),
    ];

    private static readonly (int Off, int H, int[] Words, string Rel)[] Hunk69Sprites =
    [
        (0x154, 16, [0xEE0, 0xE00, 0xEB0], "ui/combat-sparkle.png"),
    ];

    public sealed record Sprite(int Index, string Name, int Width, int Height, int PatOffset, string Idle, string Attacking);

    /// <summary>Writes the art and <c>extraction-index.json</c>; returns the index (the asset manifest reads it).</summary>
    public static JsonObject Run(ImportContext ctx, byte[] game, Action<string, RgbaImage> png)
    {
        var palette = LoadPalette(game, PaletteCombatOff, hw4: true);
        WritePalettes(ctx, game, png);
        var monsters = Monsters(ctx, game, palette, png);
        var races = Races(ctx, game, palette, png);
        var screens = new JsonArray();
        foreach (var (file, w, h, rel, pal, hw4) in ScreenPics)
        {
            var data = ctx.Files.Read(file);
            if (data.Length != w * h * 4 / 8)
                ctx.Warn($"{file}: size {data.Length} != {w * h * 4 / 8} for {w}x{h}x4");
            png(rel, DecodePlanar(data, 0, w, h, LoadPalette(game, pal, hw4)));
            screens.Add(new JsonObject
            {
                ["source"] = file, ["width"] = w, ["height"] = h, ["bitplanes"] = 4,
                ["palette_offset"] = $"0x{pal:x}", ["expansion"] = hw4 ? "hw4" : "amiga_to_st_nibble_lut", ["path"] = rel,
            });
        }
        foreach (var (off, w, h, rel) in Hunk69Bitmaps)
        {
            png(rel, DecodePlanar(game, Hunk69Off + off, w, h, palette));
            screens.Add(new JsonObject
            {
                ["hunk69_offset"] = $"0x{off:x}", ["width"] = w, ["height"] = h, ["path"] = rel, ["expansion"] = "hw4",
            });
        }
        foreach (var (off, w, h, rel) in GameBitmaps)
        {
            png(rel, DecodePlanar(game, off, w, h, palette));
            screens.Add(new JsonObject
            {
                ["file_offset"] = $"0x{off:x}", ["width"] = w, ["height"] = h, ["path"] = rel, ["expansion"] = "hw4",
            });
        }
        foreach (var (off, h, words, rel) in Hunk69Sprites)
        {
            png(rel, DecodeSprite(game, Hunk69Off + off, h, words.Select(Gun4).ToArray()));
            screens.Add(new JsonObject
            {
                ["hunk69_offset"] = $"0x{off:x}", ["width"] = 16, ["height"] = h, ["path"] = rel,
                ["expansion"] = "sprite: colours 1-3 from SetRGB4 (c * 17), 0 transparent",
            });
        }
        Sheets(ctx, monsters, races, png);

        var index = new JsonObject
        {
            ["_source"] = "Amiga ADF PHANT volume (phantasie-v1.0-1987-ssi.adf)",
            ["_decoder"] = "tools/scripts/extract_amiga_graphics.py",
            ["_palette"] = new JsonObject
            {
                ["combat_offset"] = $"0x{PaletteCombatOff:x}",
                ["alt_offset"] = $"0x{PaletteAltOff:x}",
                ["expansion"] = "monsters and races: hw4 (c * 17, the combat table); each screen lists its own",
            },
            ["monsters"] = SpriteArray(monsters),
            ["races"] = SpriteArray(races),
            ["screens"] = screens,
        };
        return index;
    }

    private static JsonArray SpriteArray(List<Sprite> list) => new(list.Select(s => (JsonNode)new JsonObject
    {
        ["index"] = s.Index, ["name"] = s.Name, ["width"] = s.Width, ["height"] = s.Height,
        ["pat_offset"] = s.PatOffset, ["idle"] = s.Idle, ["attacking"] = s.Attacking,
    }).ToArray());

    private static List<Sprite> Monsters(ImportContext ctx, byte[] game, Rgba[] palette, Action<string, RgbaImage> png)
    {
        var mstr1 = ctx.Files.Read("mstr1.pat");
        var mstr2 = ctx.Files.Read("mstr2.pat");
        var list = new List<Sprite>();
        for (var i = 0; i < MonsterCount; i++)
        {
            var addr = (int)Long(game, MonsterAddrOff + i * 4);
            var w = Word(game, MonsterDimOff + i * 4);
            var h = Word(game, MonsterDimOff + i * 4 + 2);
            var name = CString(game, MonsterNameOff + i * MonsterNameStride);
            var b = $"{i:00}-{Slug(name)}";
            var s = new Sprite(i, name, w, h, addr, $"monsters/idle/{b}.png", $"monsters/attacking/{b}.png");
            png(s.Idle, DecodePlanar(mstr1, addr, w, h, palette));
            png(s.Attacking, DecodePlanar(mstr2, addr, w, h, palette));
            list.Add(s);
        }
        return list;
    }

    private static List<Sprite> Races(ImportContext ctx, byte[] game, Rgba[] palette, Action<string, RgbaImage> png)
    {
        var party1 = ctx.Files.Read("party1.pat");
        var party2 = ctx.Files.Read("party2.pat");
        var list = new List<Sprite>();
        var nameOff = RaceNameOff;
        for (var i = 0; i < RaceCount; i++)
        {
            var name = CString(game, nameOff);
            nameOff += name.Length + 1;
            var addr = RaceBytes * i;
            var b = $"{i:00}-{Slug(name)}";
            var s = new Sprite(i, name, 32, 32, addr, $"portraits/idle/{b}.png", $"portraits/attacking/{b}.png");
            png(s.Idle, DecodePlanar(party1, addr, 32, 32, palette));
            png(s.Attacking, DecodePlanar(party2, addr, 32, 32, palette));
            list.Add(s);
        }
        return list;
    }

    private static void WritePalettes(ImportContext ctx, byte[] game, Action<string, RgbaImage> png)
    {
        foreach (var (label, off) in new[] { ("combat", PaletteCombatOff), ("alt", PaletteAltOff) })
        {
            var colours = LoadPalette(game, off);
            var obj = new JsonObject
            {
                ["source"] = "game",
                ["offset"] = $"0x{off:x}",
                ["amiga_0rgb"] = new JsonArray(PaletteRawWords(game, off).Select(w => (JsonNode)w!).ToArray()),
                ["rgba"] = new JsonArray(colours.Select(c => (JsonNode)new JsonObject
                {
                    ["r"] = c.R, ["g"] = c.G, ["b"] = c.B, ["a"] = c.A, ["hex"] = $"#{c.R:x2}{c.G:x2}{c.B:x2}",
                }).ToArray()),
                ["expansion"] = "amiga_to_st_nibble_lut",
            };
            ctx.WriteJsonFile(P1AmigaPipeline.Root + $"palettes/{label}.json", obj, ensureAscii: true);
            var sw = new RgbaImage(16 * 16, 16);
            for (var i = 0; i < 16; i++)
                AmigaGfx.Paste(sw, Filled(16, 16, colours[i]), i * 16, 0);
            png($"palettes/{label}.png", sw);
        }
    }

    /// <summary>The contact sheets (visual QA): each sprite centred in its cell, Python's floor division, clipped.</summary>
    private static void Sheets(ImportContext ctx, List<Sprite> monsters, List<Sprite> races, Action<string, RgbaImage> png)
    {
        // The sheets paste the PNGs just written; the decoded images are rebuilt from the same files.
        var game = ctx.Files.Read("game");
        var pal = LoadPalette(game, PaletteCombatOff, hw4: true);
        var m1 = ctx.Files.Read("mstr1.pat");
        var m2 = ctx.Files.Read("mstr2.pat");
        var p1 = ctx.Files.Read("party1.pat");
        var p2 = ctx.Files.Read("party2.pat");

        void Sheet(List<Sprite> recs, byte[] pat, int cols, string dest, int cell)
        {
            var rows = (recs.Count + cols - 1) / cols;
            var img = Filled(cols * cell, rows * cell, new Rgba(0, 0, 0, 255));
            for (var i = 0; i < recs.Count; i++)
            {
                var r = recs[i];
                var sprite = DecodePlanar(pat, r.PatOffset, r.Width, r.Height, pal);
                var x = i % cols * cell + FloorDiv(cell - sprite.Width, 2);
                var y = i / cols * cell + FloorDiv(cell - sprite.Height, 2);
                AmigaGfx.Paste(img, sprite, x, y);
            }
            png(dest, img);
        }

        Sheet(monsters, m1, 10, "sheets/monsters-idle.png", 40);
        Sheet(monsters, m2, 10, "sheets/monsters-attacking.png", 40);
        Sheet(races, p1, 8, "sheets/portraits-idle.png", 36);
        Sheet(races, p2, 8, "sheets/portraits-attacking.png", 36);
    }
}
