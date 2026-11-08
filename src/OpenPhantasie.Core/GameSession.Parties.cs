namespace OpenPhantasie.Core;

/// <summary>D-069: several parties out at once; Continue by Guild number (<see cref="SeveralParties"/>).</summary>
public sealed partial class GameSession
{
    private readonly List<SessionSnapshot> _otherParties = new();

    /// <summary>The parked parties (party-local state only), kept in the save as <see cref="SessionSnapshot.OtherParties"/>.</summary>
    public IReadOnlyList<SessionSnapshot> OtherParties => _otherParties;

    /// <summary>True when <paramref name="member"/> belongs to a parked party (Guild A)DD and P)URGE refuse it).</summary>
    public bool IsInOtherParty(Character member)
    {
        var slot = Array.IndexOf(Roster.Slots, member);
        return slot >= 0 && _otherParties.Any(p => p.PartySlotIndices.Contains(slot));
    }

    /// <summary>
    /// D-069 <c>dos</c> (<c>p1.continue</c>): true when <paramref name="file"/>'s locked rules continue the way DOS does
    /// (no Guild number asked; <see cref="ContinueSavedParty"/>). A file without rules is Original (manual).
    /// </summary>
    public static bool ContinuesWithoutAsking(SessionSnapshot file, GameId game)
    {
        ArgumentNullException.ThrowIfNull(file);
        return (file.Options?.ToOptions(game) ?? GameOptions.ForPreset(game)).IsDos(RuleOptionRegistry.ContinueParty);
    }

    /// <summary>
    /// D-069 <c>dos</c>: `continue.continue_saved_game` as DOS (OV1 <c>0x866..0x89d</c>: every flagged record joins the party; nothing
    /// is asked): the file's own party comes back. The remaster keeps where it was saved (it saves anywhere); a file
    /// with nobody in the party opens with an empty party, as DOS opens Pelnor (<c>0x7e7</c>).
    /// </summary>
    public void ContinueSavedParty(SessionSnapshot file)
    {
        ArgumentNullException.ThrowIfNull(file);
        ApplySnapshot(file);
        if (Party.Adventurers.FirstOrDefault() is { } leader)
            Note(CurrentTownId is int town
                ? GameText.F("core.parties.x_continues_in_town_x", leader.Name, town)
                : GameText.F("core.parties.x_continues_where_it_was", leader.Name));
    }

    /// <summary>
    /// `continue.continue_saved_game` (manual, ENCORE): load <paramref name="file"/> for the character with Guild number
    /// <paramref name="guildNumber"/> — its party where it was, the file's active party parked in its place. A character
    /// in no party opens a new, empty party at its town's menu (manual silent; the party starts on the town's exit tile
    /// like a DOS import). False with <paramref name="error"/> (nothing changes) when the number names nobody.
    /// </summary>
    public bool Continue(SessionSnapshot file, int guildNumber, out string error)
    {
        if (SeveralParties.Select(file, guildNumber, Rules.GuildSlotCount, out var newParty, out error) is not { } next)
            return false;
        ApplySnapshot(next);
        if (newParty && CurrentTown?.ExitPosition is { } at)
        {
            (Overworld.RegionId, Overworld.X, Overworld.Y) = at;
            var region = _overworld.GetRegion(at.Region);
            Overworld.TerrainClass = OverworldRegion.Normalize(region.GetRaw(at.X, at.Y)) / 10;
        }
        var name = Roster.Slots[guildNumber - 1]!.Name;
        Note(CurrentTownId is int town ? GameText.F("core.parties.x_continues_in_town_x", name, town) : GameText.F("core.parties.x_continues_where_it_was", name));
        return true;
    }
}
