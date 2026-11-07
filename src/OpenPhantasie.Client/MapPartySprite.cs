using OpenPhantasie.Data;

namespace OpenPhantasie.Client;

/// <summary>
/// s153: when the overworld's party sprite is left off for a question over the map (Amiga <c>FORMAT.md</c> "Overworld /
/// dungeon map graphics"). I Amiga's party is a hardware sprite; every question on the map is an h86 requester (the
/// <c>+0x32a</c> menus "A TOWN.  ENTER?", "A DUNGEON.  ENTER?", "AN INN.  ENTER?", the HEARS and SLEEPING MONSTERS menus,
/// "KEEP IT?"), and h86 <c>+0</c> and its wait <c>+0x132</c> MoveSprite the party to y 200 (below the screen) before
/// <c>Request</c> and back after <c>EndRequest</c>. Requester 23's boxes free it instead (s143,
/// <see cref="P1MapMessageRun.PartySpriteShown"/>). The pack says so (<see cref="GraphicsPack.PartyHiddenUnderPrompts"/>),
/// drawn behind AMIGA SCREENS. The dungeon's party glyph is a blit and stays.
/// </summary>
public static class MapPartySprite
{
    /// <summary>True when the party is not drawn: the pack hides it, AMIGA SCREENS is on and a question is up.</summary>
    public static bool HiddenUnderPrompt(GraphicsPack? pack, bool originalScreens, bool promptOpen) =>
        pack is { PartyHiddenUnderPrompts: true } && originalScreens && promptOpen;
}
