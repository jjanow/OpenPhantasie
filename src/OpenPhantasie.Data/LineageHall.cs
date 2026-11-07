using OpenPhantasie.Core;

namespace OpenPhantasie.Data;

/// <summary>
/// T5 lineage in I–III (<c>ancestry.md</c> section 13.1): the shared, read-only Hall list, built each time Guild New asks
/// for it from what already exists. IV's shipped Hall persons with a base race (<c>rule-tables.json</c> <c>hall</c>; not
/// Nikademus, whose race is "?"), the characters on the remaster's saves of the other games (<c>savegame-p1/p2/p3.json</c>,
/// read as the transfers read them, <see cref="TransferSource"/>), and the IV save's Hall imports
/// (<see cref="P4Party.HallImports"/>). No roster file of its own; nothing is written.
/// </summary>
public static class LineageHall
{
    /// <summary>
    /// The list for a Guild New in <paramref name="game"/>: IV's tables from the shipped data, the saves from
    /// <paramref name="savePathFor"/>. A missing, old or unreadable save adds nothing.
    /// </summary>
    public static LineageSetup Load(GameId game, Func<GameId, string> savePathFor)
    {
        ArgumentNullException.ThrowIfNull(savePathFor);
        var iv = Phantasie4RuleTables.Load(Path.Combine(GameDataRepository.DefaultPathFor(GameId.P4), Phantasie4RuleTables.FileName));
        var rosters = new Dictionary<GameId, IReadOnlyList<Character?>>();
        foreach (var other in new[] { GameId.P1, GameId.P2, GameId.P3 }.Where(g => g != game))
            if (TryRead(savePathFor(other), path => TransferSource.ReadRoster(path, other)) is { } roster)
                rosters[other] = roster;
        var imports = TryRead(savePathFor(GameId.P4), path => Phantasie4SaveCodec.ReadJsonFile(path).Party.Guild?.HallImports);
        return Build(iv, game, rosters, imports ?? []);
    }

    /// <summary>
    /// The list from data already read: IV's shipped persons, then each other game's Guild in game order (I, II, III), then
    /// the IV imports by slot. <paramref name="game"/>'s own roster is left out; a person with no base race is skipped.
    /// </summary>
    public static LineageSetup Build(Phantasie4RuleTables iv, GameId game,
        IReadOnlyDictionary<GameId, IReadOnlyList<Character?>> rosters, IEnumerable<P4HallPerson?> hallImports)
    {
        ArgumentNullException.ThrowIfNull(iv);
        ArgumentNullException.ThrowIfNull(rosters);
        ArgumentNullException.ThrowIfNull(hallImports);
        string? ClassName(int cls) => cls >= 0 && cls < iv.Classes.Count ? iv.Classes[cls] : null;
        var persons = new List<LineagePerson>();
        for (var i = 0; i < iv.Hall.Count; i++)
        {
            var h = iv.Hall[i];
            if (LineageRules.FromHall(h.HallSlot ?? i, iv.HallNames[i], h.Race, h.Class, h.Abilities, ClassName(h.Class)) is { } p)
                persons.Add(p);
        }
        foreach (var source in new[] { GameId.P1, GameId.P2, GameId.P3 })
        {
            if (source == game || !rosters.TryGetValue(source, out var roster))
                continue;
            for (var slot = 0; slot < roster.Count; slot++)
                if (roster[slot] is { } c && LineageRules.FromCharacter(c, source, slot) is { } p)
                    persons.Add(p);
        }
        foreach (var h in hallImports.OfType<P4HallPerson>().Where(h => h.Slot >= P4Hall.FirstImportSlot))
            if (LineageRules.FromHall(h.Slot, h.Name, h.Race, h.Class, h.Stats, ClassName(h.Class)) is { } p)
                persons.Add(p);
        return new LineageSetup(iv.Tables.Creation, persons);
    }

    private static T? TryRead<T>(string path, Func<string, T?> read) where T : class
    {
        if (!File.Exists(path))
            return null;
        try
        {
            return read(path);
        }
        catch (Exception) // an old, damaged or other game's save adds no ancestors; it must not stop creation
        {
            return null;
        }
    }
}
