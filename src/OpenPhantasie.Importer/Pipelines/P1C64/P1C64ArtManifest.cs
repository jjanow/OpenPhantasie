using System.Text.Json.Nodes;
using OpenPhantasie.Importer.Formats;
using OpenPhantasie.Importer.Pipelines.P2St;

namespace OpenPhantasie.Importer.Pipelines.P1C64;

/// <summary>
/// <c>GameData/phantasie1/graphics/c64.json</c>, the C64 pack's manifest (s218). Everything in it is the layout the C64
/// code gives (GRAPHICS.md) and the file names <see cref="P1C64Art"/> writes; nothing depends on the copy, so the shipped
/// file equals what an import writes.
/// </summary>
public static class P1C64ArtManifest
{
    public static JsonObject Pack() => C64PhantasieArtManifest.Pack(new(
        P1C64Art.GameId, P1C64Art.RootPath,
        Note: "Commodore 64 Phantasie I art (v1.4; the p1-c64 import, P1C64Art; reverse-engineering/c64/phantasie1/" +
            "GRAPHICS.md). Monsters are the MX n sprite bands (one figure: the band's first sprites, expanded as the " +
            "VIC does); portraits the HSPRIT pair of each race (none for UNDEAD); the title PIC.* (multicolour " +
            "bitmap, doubled pixels); town.backdrop the town screen's six buildings at their places (FONT+HER's town " +
            "characters); the map from FONT's characters as UTIL-3.O's routine prints them. Colours: Colodore " +
            "(VICE colodore.vpl). Text: the remaster 5x7 font (the C64's sets are in font/). Not drawn: the item / " +
            "feature pictures (items/; the Client has no such slot).",
        TileMapsNote: "UTIL-3.O $C5C3 (SYS 49152 + 63): map byte b >= 120 prints a blank in black (fog); else class " +
            "b / 10 prints PETSCII $C695[class] OR (text column AND 1) in colour $C688[class], one 8 x 8 " +
            "character a cell, the region's 26 x 20 cells whole (text columns 1-26, rows 5-24): tile b is the " +
            "even map column's character, b + 256 the odd one's. Bytes 40, 41 and 50-59 also place a yellow " +
            "sprite (town 93, inn 94, dungeon 95) 4 pixels left of the cell (overlays, drawn at their size); " +
            "the party is white sprite 92 the same way. The Client's map box holds 19 x 18 of the cells " +
            "around the party (the C64 shows all 26 x 20). Dungeon: FONT characters $E6-$EC in grey (12) on " +
            "black by DOS's cell ranges (as on screen, live s218); a cell not yet seen is the solid $20; the " +
            "party is yellow sprite 91 over its cell.",
        TitleNote: "B lines 200-420: the title picture (PIC.*) stays up while UTIL-3.O and the two fonts load (no key " +
            "wait; its time is the disk's [?]), then the '1 - Start in Pelnor / 2 - Continue' text screen. " +
            "Shown until a key, as the Apple packs do.",
        PortraitsNote: "portrait.{race}: HSPRIT blocks 2 race (idle) and 2 race + 1 (attacking), the pair the fight copies " +
            "for a member of that race (live s218); yellow, the combat row's colour [~]. No UNDEAD pair.",
        Town: (40, 40), Inn: (41, 41), Dungeon: (50, 59)));
}
