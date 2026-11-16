using System.Text.Json.Nodes;
using OpenPhantasie.Importer.Pipelines.P2St;
using OpenPhantasie.Importer.Profiles;

namespace OpenPhantasie.Importer.Pipelines.P1Apple2;

/// <summary>
/// s224: Phantasie I Apple II's end scenes as their Applesoft BASIC runs them (<c>CEMETARY</c>: the party wipe;
/// <c>FLASH?</c>: Zeus; <c>reverse-engineering/apple2/phantasie1/GRAPHICS.md</c> 7). The scenes are replayed on a hi-res
/// page (<c>OpenPhantasie.Data.Apple2Hires</c>, the ROM's DRAW / XDRAW / HPLOT) with the game's own shape table, so the
/// import writes two files beside the art: <see cref="ShapesFile"/> (<c>SHAPES</c> as on the disk) and
/// <see cref="Page2File"/> (hi-res page 2, memory $4000–$5FFF, which <c>FLASH?</c> 2100 flashes: <c>SHAPES</c> at $451F and
/// <c>FLASH?</c> at $5101 where the game keeps them; the party records, the variables and the rest of memory are not
/// known and stay 0 `[~]`). The steps (<see cref="Manifest"/>) are the listing's statements with the interpreter's
/// time per statement, measured by running both programs under the Apple II+ ROM in a 6502 emulator (s224;
/// <c>tools/scripts/a2_basic_trace.py</c>, <c>a2_p1_end_scenes.py</c>).
/// </summary>
public static class P1Apple2Scenes
{
    public const string ShapesFile = "scenes/shapes.bin";
    public const string Page2File = "scenes/page2.bin";

    /// <summary>The files, from side A's <c>SHAPES</c> and side B's <c>FLASH?</c>.</summary>
    internal static IEnumerable<(string Rel, byte[] Bytes)> Files(FileSet files)
    {
        var s = new P1Apple2Tables.Sides(files);
        var shapes = s.A("SHAPES");
        if (shapes.Length < 2 || shapes[0] < 22)
            throw new InvalidDataException($"SHAPES: {shapes.Length} bytes, {(shapes.Length > 0 ? shapes[0] : 0)} shapes");
        var page2 = new byte[0x2000];
        shapes.AsSpan(0, Math.Min(shapes.Length, 0x2000 - 0x51F)).CopyTo(page2.AsSpan(0x51F));
        var program = Relink(s.B("FLASH?"), 0x5101);
        program.AsSpan(0, Math.Min(program.Length, 0x2000 - 0x1101)).CopyTo(page2.AsSpan(0x1101));
        return [(ShapesFile, shapes), (Page2File, page2)];
    }

    /// <summary>An Applesoft program's lines relinked to load at <paramref name="at"/> (RDOS's <c>&amp; RUN</c> does this).</summary>
    internal static byte[] Relink(byte[] data, int at)
    {
        var o = new List<byte>();
        var p = 0;
        var addr = at;
        while (p + 4 <= data.Length && (data[p] | data[p + 1] << 8) != 0)
        {
            var q = Array.IndexOf(data, (byte)0, p + 4);
            if (q < 0)
                break;
            var line = data.AsSpan(p + 2, q + 1 - (p + 2));
            var link = addr + 2 + line.Length;
            o.Add((byte)(link & 0xFF));
            o.Add((byte)(link >> 8));
            o.AddRange(line.ToArray());
            addr = link;
            p = q + 1;
        }
        o.Add(0);
        o.Add(0);
        return [.. o];
    }

    /// <summary>The import step: the two files under <see cref="P1Apple2Art.Root"/>.</summary>
    public static void Write(ImportContext ctx)
    {
        foreach (var (rel, bytes) in Files(ctx.Files))
            ctx.WriteFile($"{P1Apple2Art.Root}/{rel}", bytes);
    }

    /// <summary>The pack's <c>apple2_scenes</c> (the same for every copy).</summary>
    public static JsonObject Manifest() => J.O(
        ("_note", "s224 (GRAPHICS.md 8): CEMETARY (defeat) and FLASH? (ending) as their BASIC runs them, a statement per " +
                  "step and s = the seconds to the next statement (the Apple II+ ROM run in a 6502 emulator, 1.020484 MHz; " +
                  "a key wait's s runs after the key). Drawn on a hi-res page with the ROM's DRAW / XDRAW (any ROT) / HPLOT " +
                  "and SHAPES (shapes.bin). Mixed mode: hi-res rows 0-159 over text rows 21-24. The party row: CEMETARY " +
                  "630 draws shape 19 + 20 at X = 65 TO 185 STEP 120 / (PO + (PO = 0)) (two figures for a lone member); " +
                  "FLASH? 40-50 the member's own body (record byte 168, bodies) after arm 20 at 35 + PM * 180 / PO (120 " +
                  "alone). The fates are Core's (CEMETARY's own rule, 725-740, is not used); their seconds are the Apple's " +
                  "per path. Not shown: DUNGON's INSERT BOOT DISK page before CEMETARY, FLASH?'s after the scene. " +
                  "Interpreter times vary with the party (names, bodies) [~]; page 2 (flash) is rebuilt from SHAPES and " +
                  "FLASH? only [~]."),
        ("shapes", ShapesFile),
        ("page2", Page2File),
        ("clock_hz", 1020484),
        ("_bodies", "TOWN 2350-2356: races 7+ shape 26, DWARF / HALFLING / GNOME / PIXIE / SPRITE 25, HUMAN / ELF 19, or 27 " +
                    "for a MONK / PRIEST / WIZARD."),
        ("bodies", J.O(("by_race", J.Ints([19, 25, 19, 25, 25, 25, 25, 26, 26, 26, 26, 26, 26, 26, 26, 26])), ("robed", 27),
            ("robed_races", J.Ints([0, 2])), ("robed_classes", J.Ints([1, 4, 5])))),
        ("_sounds", "FLASH? 1100: 15 pairs of DATA 1105 POKEd into 768 (pitch) / 769 (duration), then CALL 770 (INFO: a " +
                    "click, then DEY / DEX loops; X = 157, Y = 2 at entry); calls = [start s, pitch, duration]."),
        ("sounds", J.O(("thunder", JsonNode.Parse(Thunder)))),
        ("defeat", JsonNode.Parse(DefeatSteps)),
        ("ending", JsonNode.Parse(EndingSteps)));

    private const string DefeatSteps = """
        [
          {"op":"home","s":0.0156},
          {"op":"clear","s":1.5439},
          {"op":"draw","shape":19,"x":200,"y":140,"scale":2,"rot":8,"hcolor":3,"s":0.0332},
          {"op":"draw","shape":19,"x":201,"y":140,"scale":2,"rot":8,"hcolor":3,"s":0.0384},
          {"op":"draw","shape":7,"x":226,"y":140,"scale":1,"rot":0,"hcolor":2,"s":0.0281},
          {"op":"draw","shape":7,"x":227,"y":140,"scale":1,"rot":0,"hcolor":2,"s":0.0314},
          {"op":"draw","shape":19,"x":240,"y":0,"scale":2,"rot":0,"hcolor":3,"s":0.0318},
          {"op":"hplot","hcolor":3,"points":[0,159,279,159],"s":0.0351},
          {"op":"hplot","hcolor":3,"points":[0,0,279,0],"s":0.0351},
          {"op":"draw","shape":19,"x":50,"y":100,"scale":1,"rot":32,"hcolor":3,"s":0.0218},
          {"op":"draw","shape":21,"x":50,"y":100,"scale":1,"rot":32,"hcolor":3,"s":0.027},
          {"op":"draw","shape":19,"x":180,"y":80,"scale":1,"rot":16,"hcolor":3,"s":0.0218},
          {"op":"draw","shape":22,"x":180,"y":80,"scale":1,"rot":16,"hcolor":3,"s":0.0198},
          {"op":"draw","shape":19,"x":75,"y":125,"scale":1,"rot":56,"hcolor":3,"s":0.0297},
          {"op":"draw","shape":20,"x":75,"y":125,"scale":1,"rot":56,"hcolor":3,"s":0.0273},
          {"op":"draw","shape":19,"x":100,"y":170,"scale":2,"rot":48,"hcolor":3,"s":0.036},
          {"op":"draw","shape":19,"x":180,"y":100,"scale":2,"rot":36,"hcolor":3,"s":0.0407},
          {"op":"draw","shape":19,"x":181,"y":100,"scale":2,"rot":36,"hcolor":3,"s":0.0461},
          {"op":"draw","shape":7,"x":130,"y":136,"scale":1,"rot":0,"hcolor":2,"s":0.0279},
          {"op":"draw","shape":7,"x":131,"y":136,"scale":1,"rot":0,"hcolor":2,"s":0.0318},
          {"op":"draw","shape":19,"x":130,"y":0,"scale":2,"rot":0,"hcolor":3,"s":0.0319},
          {"op":"vtab","row":24,"s":0.0025},
          {"op":"print","text":"apple2.defeat.defeated","newline":true,"s":0.0235},
          {"op":"print","text":"apple2.defeat.monsters_jump","newline":true,"s":0.0247},
          {"op":"graphics","s":0.0201},
          {"op":"repeat","count":70,"steps":[{"op":"xdraw","shape":6,"x":100,"y":40,"scale":1,"rot":0,"s":0.0233},{"op":"draw","shape":5,"x":100,"y":50,"scale":1,"rot":0,"hcolor":3,"s":0.0242},{"op":"xdraw","shape":6,"x":130,"y":40,"scale":1,"rot":0,"s":0.0232},{"op":"draw","shape":5,"x":130,"y":50,"scale":1,"rot":0,"hcolor":3,"s":0.0242},{"op":"xdraw","shape":6,"x":160,"y":40,"scale":1,"rot":0,"s":0.0233},{"op":"draw","shape":5,"x":160,"y":50,"scale":1,"rot":0,"hcolor":3,"s":0.033},{"op":"xdraw","shape":5,"x":100,"y":50,"scale":1,"rot":0,"s":0.0232},{"op":"draw","shape":6,"x":100,"y":40,"scale":1,"rot":0,"hcolor":3,"s":0.0243},{"op":"xdraw","shape":5,"x":130,"y":50,"scale":1,"rot":0,"s":0.0232},{"op":"draw","shape":6,"x":130,"y":40,"scale":1,"rot":0,"hcolor":3,"s":0.0243},{"op":"xdraw","shape":5,"x":160,"y":50,"scale":1,"rot":0,"s":0.0232},{"op":"draw","shape":6,"x":160,"y":40,"scale":1,"rot":0,"hcolor":3,"s":0.0346}],"s":0},
          {"op":"clear","s":0.2789},
          {"op":"text","s":0.0003},
          {"op":"home","s":0.0157},
          {"op":"hplot","hcolor":3,"points":[0,159,100,100,179,100,279,159],"s":0.0582},
          {"op":"hplot","hcolor":3,"points":[100,100,100,10,179,10,179,100],"s":0.0467},
          {"op":"party","xs":[[65,185],[65,185],[65,125,185],[65,105,145,185],[65,95,125,155,185],[65,89,113,137,161,185]],"y":159,"shapes":[19,20],"scale":1,"rot":0,"hcolor":3,"lead":0.0154,"each":[0.0209,0.0177],"tail":0.021,"s":0},
          {"op":"draw","shape":11,"x":49,"y":100,"scale":3,"rot":0,"hcolor":6,"s":0.0621},
          {"op":"draw","shape":11,"x":185,"y":100,"scale":3,"rot":0,"hcolor":6,"s":0.0635},
          {"op":"draw","shape":11,"x":48,"y":100,"scale":3,"rot":0,"hcolor":6,"s":0.0629},
          {"op":"draw","shape":11,"x":184,"y":100,"scale":3,"rot":0,"hcolor":6,"s":0.0643},
          {"op":"graphics","s":0.0072},
          {"op":"vtab","row":24,"s":0.0025},
          {"op":"print","text":"apple2.defeat.astral","newline":true,"s":8.9646},
          {"op":"draw","shape":18,"x":120,"y":90,"scale":4,"rot":0,"hcolor":2,"s":0.0767},
          {"op":"home","s":0.0156},
          {"op":"vtab","row":24,"s":0.0025},
          {"op":"print","text":"apple2.defeat.demon","newline":true,"s":6.9825},
          {"op":"fates","lead_text":"apple2.defeat.you_have_been","destroyed":{"text":"apple2.defeat.destroyed","s":0.6662},"resurrected":{"text":"apple2.defeat.resurrected","s":0.1003},"undead":{"text":"apple2.defeat.undead","newline":true,"s":0.1504},"after_key":0.0202,"tail":0.3613,"first":0.0032,"s":0},
          {"op":"hgr","s":0.0001}
        ]
        """;

    private const string EndingSteps = """
        [
          {"op":"home","s":0.0156},
          {"op":"hgr","s":0.2752},
          {"op":"hplot","hcolor":3,"points":[0,159,279,159],"s":0.526},
          {"op":"hplot","hcolor":3,"points":[100,0,130,15,70,40,120,60,73,80,115,90,76,110,110,122,100,140],"s":0.0919},
          {"op":"hplot","hcolor":0,"points":[100,0,130,15,70,40,120,60,73,80,115,90,76,110,110,122,100,140],"s":0.0912},
          {"op":"sound","sound":"thunder","s":0.5258},
          {"op":"hplot","hcolor":3,"points":[200,0,235,17,172,38,226,64,167,83,217,80,172,113,214,118,200,150],"s":0.1024},
          {"op":"hplot","hcolor":0,"points":[200,0,235,17,172,38,226,64,167,83,217,80,172,113,214,118,200,150],"s":0.1014},
          {"op":"sound","sound":"thunder","s":1.9807},
          {"op":"hplot","hcolor":3,"points":[200,0,235,17,172,38,226,64,167,83,217,80,172,113,214,118,200,150],"s":0.1024},
          {"op":"hplot","hcolor":0,"points":[200,0,235,17,172,38,226,64,167,83,217,80,172,113,214,118,200,150],"s":0.102},
          {"op":"sound","sound":"thunder","s":1.4941},
          {"op":"hplot","hcolor":3,"points":[100,0,130,15,70,40,120,60,73,80,115,90,76,110,110,122,100,140],"s":0.0919},
          {"op":"hplot","hcolor":0,"points":[100,0,130,15,70,40,120,60,73,80,115,90,76,110,110,122,100,140],"s":0.0912},
          {"op":"sound","sound":"thunder","s":1.4987},
          {"op":"party","s":0,"xs":[[120],[35,215],[35,125,215],[35,95,155,215],[35,80,125,170,215],[35,71,107,143,179,215]],"y":159,"shapes":[20,0],"scale":2,"rot":0,"hcolor":3,"lead":0.0315,"each":[0.0246,0.0563],"tail":0.0439},
          {"op":"vtab","row":24,"s":0.0025},
          {"op":"print","text":"apple2.ending.olympus","newline":true,"s":0.0439},
          {"op":"key","s":0.0039},
          {"op":"draw","shape":9,"x":101,"y":80,"scale":4,"rot":0,"hcolor":5,"s":0.0622},
          {"op":"home","s":0.0198},
          {"op":"flash","s":0.1537,"count":10,"first":0.0073,"page2":0.0069,"page1":0.0082},
          {"op":"sound","sound":"thunder","s":0.5211},
          {"op":"home","s":0.0198},
          {"op":"flash","s":0.1537,"count":10,"first":0.0073,"page2":0.0069,"page1":0.0082},
          {"op":"sound","sound":"thunder","s":0.5191},
          {"op":"home","s":0.0156},
          {"op":"vtab","row":24,"s":0.0025},
          {"op":"print","text":"apple2.ending.zeus_appears","pad":3,"newline":true,"s":4.8963},
          {"op":"home","s":0.0156},
          {"op":"vtab","row":23,"s":0.0026},
          {"op":"print","text":"apple2.ending.zeus_1","pad":2,"newline":true,"s":0.0063},
          {"op":"print","text":"apple2.ending.zeus_2","pad":4,"newline":true,"s":0.0229},
          {"op":"print","text":"apple2.ending.zeus_3","pad":2,"newline":false,"s":0.0071},
          {"op":"key","s":0.0068},
          {"op":"hplot","hcolor":3,"points":[100,0,130,15,70,40,120,60,73,80,115,90,76,110,110,122,100,140],"s":0.0919},
          {"op":"hplot","hcolor":0,"points":[100,0,130,15,70,40,120,60,73,80,115,90,76,110,110,122,100,140],"s":0.0906},
          {"op":"sound","sound":"thunder","s":0.5246},
          {"op":"hplot","hcolor":3,"points":[200,0,235,17,172,38,226,64,167,83,217,80,172,113,214,118,200,150],"s":0.1024},
          {"op":"hplot","hcolor":0,"points":[200,0,235,17,172,38,226,64,167,83,217,80,172,113,214,118,200,150],"s":0.1008},
          {"op":"sound","sound":"thunder","s":0.7105},
          {"op":"hgr","s":0.2736}
        ]
        """;

    private const string Thunder = """
        {"x":157,"y":2,"calls":[[0.04781,1,1],[0.06669,250,4],[0.09185,1,1],[0.10945,1,1],[0.1287,255,7],[0.16309,255,7],[0.19572,1,4],[0.22836,230,4],[0.25347,1,5],[0.28957,1,6],[0.33243,210,3],[0.35501,1,7],[0.40109,1,9],[0.45888,160,2],[0.47889,1,9]]}
        """;
}
