namespace OpenPhantasie.Core;

/// <summary>
/// D-069 (user s57: manual, ENCORE "Continuing Saved Games"): several parties explore at once, and Continue asks for the
/// Guild number of a character and calls up that character's party where it was. DOS keeps one party (one <c>@218</c>
/// flag). The remaster save keeps the active party plus the shared world on the top level and every other party as a
/// parked <see cref="SessionSnapshot"/> in <see cref="SessionSnapshot.OtherParties"/> holding party-local state only
/// (<c>save-file-format.md</c> §Several parties and Continue). Pure snapshot work; <see cref="GameSession.Continue"/> applies it.
/// </summary>
public static class SeveralParties
{
    public static string NoSuchMember => GameText.S("core.parties.no_such_member");

    /// <summary>Guild numbers 23..25 are the elementals (manual p.4: "Town #0, not found in the Material Plane").</summary>
    public static string ElementalRefusal => GameText.S("core.parties.elementals_live_in_town_0");

    public static string OtherPartyRefusal => GameText.S("core.parties.in_another_party");

    /// <summary>
    /// Refusal text for Guild number <paramref name="guildNumber"/> (roster slot + 1) in <paramref name="file"/>, or null
    /// when it names a character. The three elementals are the numbers just past the roster
    /// (<paramref name="guildSlotCount"/> + 1..3; Phantasie I: 23–25).
    /// </summary>
    public static string? Refusal(SessionSnapshot file, int guildNumber, int guildSlotCount)
    {
        ArgumentNullException.ThrowIfNull(file);
        var slot = guildNumber - 1;
        if ((uint)slot < (uint)file.RosterSlots.Length && file.RosterSlots[slot] is not null)
            return null;
        return guildNumber > guildSlotCount && guildNumber <= guildSlotCount + 3
            ? ElementalRefusal
            : NoSuchMember;
    }

    /// <summary>
    /// The state Continue loads for Guild number <paramref name="guildNumber"/>: the file as saved when the character
    /// is in its active party; else that character's parked party (or, in no party, a new empty one in the character's
    /// town — manual silent) over the shared world, with the file's active party parked unless it has no members.
    /// <paramref name="newParty"/> is true for the empty-party case (the caller places it at the town's exit tile).
    /// Null with <paramref name="error"/> when the number names nobody.
    /// </summary>
    public static SessionSnapshot? Select(
        SessionSnapshot file, int guildNumber, int guildSlotCount, out bool newParty, out string error)
    {
        ArgumentNullException.ThrowIfNull(file);
        newParty = false;
        if (Refusal(file, guildNumber, guildSlotCount) is { } refused)
        {
            error = refused;
            return null;
        }
        error = "";
        var slot = guildNumber - 1;
        if (file.PartySlotIndices.Contains(slot))
            return file;

        var others = file.OtherParties.ToList();
        var hit = others.FindIndex(p => p.PartySlotIndices.Contains(slot));
        SessionSnapshot chosen;
        if (hit >= 0)
        {
            chosen = others[hit];
            others.RemoveAt(hit);
        }
        else
        {
            newParty = true;
            chosen = new SessionSnapshot
            {
                Mode = GameMode.Town,
                CombatReturnMode = GameMode.Overworld,
                CurrentTownId = Math.Max(1, file.RosterSlots[slot]!.CurrentTown), // a never-moved member holds 0 = Pelnor
            };
        }
        if (file.PartySlotIndices.Length > 0)
            others.Add(Park(file));

        var next = PartyLocal(chosen);
        next.Version = file.Version;
        next.Game = file.Game; // a new snapshot starts as Phantasie I
        next.RosterSlots = file.RosterSlots;
        next.Towns = file.Towns;
        next.DungeonSave = file.DungeonSave;
        next.OtherDungeonSaves = file.OtherDungeonSaves;
        next.Wilderness = chosen.Wilderness?.ShallowCopy() ?? new WildernessSnapshot();
        next.Wilderness.RegionTiles = file.Wilderness?.RegionTiles;
        next.OtherParties = others.ToArray();
        next.Options = file.Options; // T5.0: one set of rules for the whole save
        return next;
    }

    /// <summary>
    /// The parties in <paramref name="file"/> for the Continue party list (QoL, <c>GameSettings.ContinueFromList</c>): the
    /// active party first when it has members, then each parked party in save order. Names come from the file's roster
    /// (a parked party holds none). Characters in no party aren't listed (the Guild-number question still reaches them).
    /// </summary>
    public static IReadOnlyList<SavedParty> Parties(SessionSnapshot file)
    {
        ArgumentNullException.ThrowIfNull(file);
        var list = new List<SavedParty>();
        foreach (var (party, active) in new[] { (file, true) }.Concat(file.OtherParties.Select(p => (p, false))))
        {
            var slots = party.PartySlotIndices
                .Where(i => (uint)i < (uint)file.RosterSlots.Length && file.RosterSlots[i] is not null)
                .ToArray();
            if (slots.Length == 0)
                continue;
            var mode = party.Mode == GameMode.Combat ? party.CombatReturnMode : party.Mode;
            list.Add(new SavedParty(
                slots[0] + 1,
                slots.Select(i => file.RosterSlots[i]!.Name).ToArray(),
                active,
                mode,
                mode is GameMode.Town or GameMode.Guild ? party.CurrentTownId : null,
                mode == GameMode.Dungeon ? party.Dungeon?.LevelId : null));
        }
        return list;
    }

    /// <summary>The party-local part of <paramref name="snap"/> (no roster, towns, region bytes, dungeon slot or parked parties).</summary>
    public static SessionSnapshot Park(SessionSnapshot snap)
    {
        ArgumentNullException.ThrowIfNull(snap);
        var parked = PartyLocal(snap);
        if (snap.Wilderness is { } w)
        {
            parked.Wilderness = w.ShallowCopy();
            parked.Wilderness.RegionTiles = null;
        }
        return parked;
    }

    private static SessionSnapshot PartyLocal(SessionSnapshot s) => new()
    {
        Version = s.Version,
        Game = s.Game,
        Mode = s.Mode,
        CombatReturnMode = s.CombatReturnMode,
        CurrentTownId = s.CurrentTownId,
        RosterSlots = Array.Empty<Character?>(),
        PartySlotIndices = s.PartySlotIndices,
        PartyGold = s.PartyGold,
        Elemental = s.Elemental,
        ElementalSummoned = s.ElementalSummoned,
        OverworldRegionId = s.OverworldRegionId,
        OverworldX = s.OverworldX,
        OverworldY = s.OverworldY,
        OverworldTerrainClass = s.OverworldTerrainClass,
        TravelTime = s.TravelTime,
        Wilderness = s.Wilderness,
        Dungeon = s.Dungeon,
        Sack = s.Sack,
        Combat = s.Combat,
        PendingTreasure = s.PendingTreasure,
        PendingTreasureFinders = s.PendingTreasureFinders,
        PartyDefeatPending = s.PartyDefeatPending,
    };
}

/// <summary>
/// One party in a save, as the Continue party list shows it (<see cref="SeveralParties.Parties"/>).
/// <paramref name="GuildNumber"/> is its first member's (what <see cref="GameSession.Continue"/> takes);
/// <paramref name="Mode"/> is where it stands (a fight shows the mode it returns to), with the town or dungeon level.
/// </summary>
public sealed record SavedParty(
    int GuildNumber,
    IReadOnlyList<string> Names,
    bool Active,
    GameMode Mode,
    int? TownId,
    int? DungeonLevelId);
