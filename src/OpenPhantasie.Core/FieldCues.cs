namespace OpenPhantasie.Core;

/// <summary>
/// s139: named moments outside combat that a platform may sound (<see cref="GameSession.Cue"/>). Core only says that the
/// moment happened; which sound plays (or none) is the platform pack's data (II Atari ST: <c>field_sounds</c> in
/// <c>phantasie2/graphics/atari-st.json</c>, FORMAT.md 7.4). The names are game-neutral, so a pack of another game or
/// platform can map the same moments; a moment no pack maps is silent. The II ST mapping is read from PHANT.PRG's call
/// sites and traced in Hatari (s140, FORMAT.md 7.4, <c>parity/capture_s140_sounds.md</c>): every moment below plays its
/// sound, the ST's box following 0–11 VBL later. The remaster notes a moment in the frame its line shows, which matches
/// those; field Resurrection's "box, then sound" is delayed by the pack (<c>field_sounds.cue_delays</c>, s141: the box's
/// Speed + 1000 ms hold and its 355 ms redraw).
/// </summary>
public static class FieldCues
{
    /// <summary>The overworld's "A TOWN. ENTER ?" is asked (II ST <c>0x63e2</c>: sound 19 before tree 37).</summary>
    public const string TownPrompt = "overworld.town_prompt";

    /// <summary>The overworld's "A DUNGEON. ENTER ?" is asked (II ST <c>0x106a0</c>: sound 19 before tree 30).</summary>
    public const string DungeonPrompt = "overworld.dungeon_prompt";

    /// <summary>A dungeon step into a wall or off the map, `core.dungeon.that_is_the_wall` (II ST <c>0x1046c</c>: <c>0xb7fa</c>, the
    /// default script, for a cell 0 and for a step off the map alike).</summary>
    public const string DungeonWall = "dungeon.wall";

    /// <summary>A disarm roll fails, before "%s FAILS" and the spring (II ST <c>0x10e9c</c>: sound 29).</summary>
    public const string DisarmFails = "dungeon.disarm_fails";

    /// <summary>Dungeon event op 3, "A SPELL IS CAST" (II ST <c>0x11d02</c>: sound 28 before the box).</summary>
    public const string EventSpellCast = "dungeon.event.spell_cast";

    /// <summary>Dungeon event op 12, `core.script.the_party_is_moved` (II ST <c>0x11dfc</c>: sound 16; op 13 is silent).</summary>
    public const string EventPartyMoved = "dungeon.event.party_moved";

    /// <summary>Dungeon event op 14, `core.script.you_hear_a_noise` (II ST <c>0x11e88</c>: sound 26; op 15 is silent).</summary>
    public const string EventNoise = "dungeon.event.noise";

    /// <summary>TRANSPORTATION: a town was picked (II ST <c>0x262c</c>: sound 22, then the palette fade <c>0x260a</c>, the
    /// pack's <c>transport_fade</c>, drawn by the front end since s140).</summary>
    public const string Transport = "town.transport";

    /// <summary>A field or dungeon Resurrection raised a member, after its line (II ST <c>0x92d8</c>: sound 12).</summary>
    public const string FieldResurrected = "field.resurrected";

    /// <summary>A town cast of a Healing on a living member or a Resurrection that raised one, after its line (II ST
    /// <c>0xe5ec</c> / <c>0xe7f2</c>: the "D O N E !" alert, which beeps the default script first). The remaster draws no
    /// such alert, so the beep sounds alone `[~]`.</summary>
    public const string TownCastDone = "town.cast_done";
}
