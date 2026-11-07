namespace OpenPhantasie.Core;

/// <summary>
/// s136: III's sound numbers outside combat (<see cref="P3Fight.CastSound"/> is the cast line's), as the game starts them
/// (<see cref="GameSession.PlaySound"/>). The Amiga plays <c>0xb126(n)</c> (FORMAT.md 7.2); DOS writes the same numbers to
/// its event word <c>FAR:0xde58</c> (ovl4 <c>0xf6c</c> / <c>0xf92</c> / <c>0x1550</c> / <c>0x1609</c> / <c>0x1672</c> /
/// <c>0x1693</c>), so the numbers are the game's, not a platform's.
/// </summary>
public static class P3Sounds
{
    /// <summary>A member's dungeon line: "hears monsters. Continue?", "picks the lock.", "spots a trap. Disarm it?",
    /// "disarms the trap." (Amiga <c>0x1f572</c>); the guild's " It is learned successfully! " (<c>0x10902</c>).</summary>
    public const int MemberLine = 6;

    /// <summary>s138: Guild New character, OK on the rolled character's sheet, before the name field (Amiga <c>0x13938</c> in
    /// <c>0x12bfc</c>; the Phantasie I transfer, which runs the same routine, skips it).</summary>
    public const int RollKept = 6;

    /// <summary>The wall bump, with "That is a wall" (event word 12: Amiga <c>0x1c4be</c>, DOS ovl4 <c>0xf6c</c>).</summary>
    public const int Wall = 12;

    /// <summary>s138: the "Disk Error! A save may have been attempted on a protected disk. …" box (Amiga <c>0x20308</c> in
    /// <c>0x20258</c>; the save writer <c>0x1faf0</c> shows it when gamesave cannot be opened). The remaster plays it on a
    /// failed save `[~]`. "Insert your Phantasie III disk." (<c>0x1f93c</c>, also 12) has no remaster moment: no disks.</summary>
    public const int DiskError = 12;

    /// <summary>A wilderness encounter starts (Amiga <c>0x149d4</c>: after the 8 % roll and the monster pick, before the
    /// night / surprise rolls; DOS root <c>0xde54</c> <c>[~]</c> whether DOS sounds it).</summary>
    public const int Encounter = 24;

    /// <summary>Event op 7 "The party finds some gold." (op 8 on its own: left in the event word).</summary>
    public const int GoldFound = 3;

    /// <summary>Event op 12 "The party is teleported." (op 13 writes nothing).</summary>
    public const int Teleported = 9;

    /// <summary>Event op 14 "A noise is heard in the distance." (op 15 writes nothing).</summary>
    public const int Noise = 27;

    /// <summary>
    /// 42: the Damage Report window (Amiga <c>0x1d4ea</c>, every party-damage call), event op 9 "Some gold was taken."
    /// (op 10 on its own and ops 1–4: left in the event word), and the special doors 0xc7 / 0xd6 ("No one can pick the
    /// lock").
    /// </summary>
    public const int Loss = 42;

    /// <summary>The Armory's refusals "He cannot carry anymore!" / "The party cannot afford it!" (s137: Amiga <c>0x111e0</c> /
    /// <c>0x11386</c> in <c>0x10fa2</c>).</summary>
    public const int ArmoryRefusal = 36;

    /// <summary>s141: window 1's confirm questions (Amiga <c>0xaae2</c> plays 36 before it draws the question; FORMAT.md 7.2).
    /// The remaster's one such question is the guild Add's "two months" (<c>0xd24c</c>).</summary>
    public const int ConfirmQuestion = 36;

    /// <summary>
    /// s158: the driver's stop (all three voice pointers 0, FORMAT.md 7.2): played before "The monsters flee!" (Amiga
    /// <c>0x15990</c>) and "The party gets away!" (<c>0x19c66</c>). It silences whatever plays, a carried title tune
    /// included.
    /// </summary>
    public const int Stop = 33;

    /// <summary>A monster's combat " dies!" line (Amiga <c>0x18610</c> in <c>damage_monster</c> <c>0x1833e</c>); a member's death has no 39.</summary>
    public const int Dies = 39;
}
