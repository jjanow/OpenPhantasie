namespace OpenPhantasie.Core;

/// <summary>Where a saved party stands, for a save slot's row (<see cref="SaveSlotPreview"/>).</summary>
public enum SavePlace
{
    /// <summary>In a town or its Guild; <see cref="SaveSlotPreview.Number"/> is the town id (I–III) or IV's town map.</summary>
    Town,

    /// <summary>The wilderness (IV: the outdoor map <see cref="SaveSlotPreview.Number"/>).</summary>
    Wilderness,

    /// <summary>A dungeon; <see cref="SaveSlotPreview.Number"/> is the level id (I–III) or IV's dungeon map.</summary>
    Dungeon,

    /// <summary>Anywhere else (a new game's empty start, the end screens).</summary>
    Other,
}

/// <summary>
/// QoL SAVE SLOTS (<c>docs/plan/12-save-slots.md</c>): what a save slot's row tells about the save in it, read from the save
/// itself: the party's names, where it is and how many other parties the save keeps (I–III, D-069). Pure snapshot work:
/// the Client turns it into text (town names, the file's date).
/// </summary>
public sealed record SaveSlotPreview(IReadOnlyList<string> Names, SavePlace Place, int? Number, int OtherParties)
{
    /// <summary>
    /// An I–III save: its active party (or, when that is empty, the first parked one; <see cref="SeveralParties.Parties"/>)
    /// and where it is (a fight counts as the place it is fought in). A save with no party at all names nobody and gives the
    /// file's own place.
    /// </summary>
    public static SaveSlotPreview Of(SessionSnapshot file)
    {
        ArgumentNullException.ThrowIfNull(file);
        var parties = SeveralParties.Parties(file);
        if (parties.Count == 0)
        {
            var mode = file.Mode == GameMode.Combat ? file.CombatReturnMode : file.Mode;
            return new([], PlaceOf(mode), mode is GameMode.Town or GameMode.Guild ? file.CurrentTownId : null, 0);
        }
        var p = parties[0];
        var place = PlaceOf(p.Mode);
        return new(p.Names, place, place == SavePlace.Dungeon ? p.DungeonLevelId : p.TownId, parties.Count - 1);
    }

    /// <summary>An IV save: its party's names and <c>_cmapf</c> / <c>_cmapn</c> (in a town, the town loop's number; IV keeps one party).</summary>
    public static SaveSlotPreview Of(P4CampaignSnapshot save)
    {
        ArgumentNullException.ThrowIfNull(save);
        var place = save.Place switch
        {
            P4Place.Town => SavePlace.Town,
            P4Place.Outdoors => SavePlace.Wilderness,
            P4Place.Dungeon => SavePlace.Dungeon,
            _ => SavePlace.Other,
        };
        var number = place == SavePlace.Town && save.Town is { Town: > 0 } t ? t.Town : save.Map; // the town loop's own number
        return new(save.Party.Members.Select(m => m.Name).ToArray(), place, number, 0);
    }

    private static SavePlace PlaceOf(GameMode mode) => mode switch
    {
        GameMode.Town or GameMode.Guild => SavePlace.Town,
        GameMode.Overworld => SavePlace.Wilderness,
        GameMode.Dungeon => SavePlace.Dungeon,
        _ => SavePlace.Other,
    };
}
