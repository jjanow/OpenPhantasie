using OpenPhantasie.Core;

namespace OpenPhantasie.Client;

/// <summary>Which pictures I's party defeat draws on DOS's line clock (s157: the pictures follow the art pack).</summary>
public enum DosDefeatArt
{
    /// <summary>No pictures resolve: the timed text panel (s154).</summary>
    None,

    /// <summary>The pack's own DOS pictures (<c>defeat.temple</c> and the rest, the CGA pack).</summary>
    Dos,

    /// <summary>The Amiga's defeat pictures (<c>decor.pic</c>, door, face, ghost, portraits) on DOS's beats.</summary>
    Amiga,
}

/// <summary>What the outcome shows for one line of DOS's clock.</summary>
/// <param name="Face">The Great Demon's face shows (DOS: <c>defeat.demon</c>; Amiga art: the door gone, the face drawn).</param>
/// <param name="Fates">Members whose fate shows (from slot 0); the rest still show their ghosts.</param>
/// <param name="Line">The one line on the bottom row (DOS clears row 24 before each), or null.</param>
public readonly record struct DosDefeatFrame(bool Face, int Fates, string? Line);

/// <summary>
/// s157: Phantasie I DOS's party-defeat screens line by line (OV1 <c>0x2af</c> / <c>0x3bc</c>, endgame.md section 4):
/// `core.defeat.your_party_has_been_defeated` on black (the defeat prompt), then on the outcome prompt, one line per
/// <see cref="P1DosDefeatClock"/> step: line 0 `core.defeat.arrived_at_astral_plane` over the temple with an empty
/// doorway and a ghost per member; line 1 `core.defeat.great_demon_speaks` over the temple with the face; line
/// 2 + i member i's fate, drawn before its line (a figure or a cleared slot). No MonoGame types.
/// </summary>
public static class P1DosDefeatScene
{
    /// <summary>The outcome's frame at clock step <paramref name="index"/> of <paramref name="lines"/> (Core's outcome
    /// lines: the two scene lines, then a fate line per member); the step past the last keeps the last frame.</summary>
    public static DosDefeatFrame At(int index, IReadOnlyList<string> lines, int members)
    {
        ArgumentNullException.ThrowIfNull(lines);
        var i = Math.Clamp(index, 0, Math.Max(0, lines.Count - 1));
        return new DosDefeatFrame(i >= 1, Math.Clamp(i - 1, 0, members), i < lines.Count ? lines[i] : null);
    }

    /// <summary>The row: the first <paramref name="fates"/> members show their fate (UNDEAD's race is already 15; a
    /// destroyed member's slot is cleared), the others their ghost.</summary>
    public static IReadOnlyList<DefeatPortrait> Row(PartyDefeatResult defeat, int fates)
    {
        ArgumentNullException.ThrowIfNull(defeat);
        var row = new List<DefeatPortrait>(defeat.Fates.Count);
        for (var i = 0; i < defeat.Fates.Count; i++)
        {
            var (member, fate) = defeat.Fates[i];
            row.Add(i >= fates ? new DefeatPortrait(DefeatPortraitKind.Ghost, (int)member.Race)
                : fate == DefeatFate.Destroyed ? new DefeatPortrait(DefeatPortraitKind.Cleared, 0)
                : new DefeatPortrait(DefeatPortraitKind.Race, (int)member.Race));
        }
        return row;
    }

    /// <summary>The Amiga art's door on DOS's beats `[~]`: shut on the Astral Plane line, gone with the face from the
    /// demon's line on (the Amiga's 7-s sink has no place on a 5-s line).</summary>
    public static DefeatSceneFrame AmigaDoor(DosDefeatFrame frame) =>
        new(frame.Face ? 0 : DefeatScene.DoorHeightClosed, frame.Face);

    /// <summary>
    /// Which art to draw: the pack's DOS pictures when it has both whole screens and the ghost, else the Amiga's when it
    /// has <c>decor.pic</c>, the door, the face and the ghost, else none. A pack never borrows another's pictures.
    /// </summary>
    public static DosDefeatArt ArtFor(bool dosTemple, bool dosDemon, bool dosGhost, bool decor, bool door, bool face, bool ghost) =>
        dosTemple && dosDemon && dosGhost ? DosDefeatArt.Dos
        : decor && door && face && ghost ? DosDefeatArt.Amiga
        : DosDefeatArt.None;
}
