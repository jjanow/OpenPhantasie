namespace OpenPhantasie.Core;

/// <summary>
/// One person on the shared Hall list a lineage ancestor can be picked from (T5, <c>ancestry.md</c> section 13): IV's
/// shipped Hall persons with a base race, the characters on the remaster's saves of the other games, and the IV save's Hall
/// imports. Read once, at creation; nothing is written back.
/// </summary>
/// <param name="Id">Unique on the list (<c>p4:hall-0</c>, <c>p3:guild-5</c>): two picks of one id are the same person.</param>
/// <param name="Source">The game the person comes from (P4 for every Hall person).</param>
/// <param name="Race">A base race (Human … Minotaur): IV's numbers 0–14 are I's.</param>
/// <param name="Class">The class number in the source game (I–III's order = IV's for 0–5; IV's Sage 8 for Filmon).</param>
/// <param name="ClassName">The source game's class name for a class past I's six (Filmon's Sage); null otherwise.</param>
/// <param name="Abilities">STR, INT, DEX, CON, CHA as stored.</param>
/// <param name="HallSlot">The IV Hall slot (0–10) for a Hall person, else null.</param>
public sealed record LineagePerson(
    string Id, string Name, GameId Source, Race Race, int Class, string? ClassName, IReadOnlyList<int> Abilities,
    int? HallSlot = null)
{
    /// <summary>This person as IV's creation reads an ancestor (<c>_cmbattr</c>).</summary>
    public P4Ancestor ToAncestor() => new((int)Race, Class, Abilities, HallSlot);
}

/// <summary>What a lineage creation reads: IV's creation tables (inheritance, new persons) and the shared Hall list.</summary>
public sealed record LineageSetup(P4CreationTables Tables, IReadOnlyList<LineagePerson> Persons);

/// <summary>Why a pair of ancestors cannot have a descendant (<see cref="LineageRules.Pair"/>).</summary>
public enum LineageRefusal
{
    None,

    /// <summary>The two persons are of different races (user s72: base races only, no hybrids).</summary>
    MixedRaces,

    /// <summary>The pair's race is not one the game offers at creation.</summary>
    RaceNotOffered,

    /// <summary>One person picked as both ancestors (IV's <c>_moptb</c> rule).</summary>
    SamePerson,

    /// <summary>Two of IV's shipped Hall persons (slots 0–2; IV's <c>_moptb</c> rule).</summary>
    BothShipped,
}

/// <summary>A checked pair: the descendant's race, or why there is none.</summary>
public readonly record struct LineagePair(Race Race, LineageRefusal Refusal);

/// <summary>The feature module behind <see cref="FeatureModuleRegistry.Lineage"/>: no combat hooks; Guild New reads the switch.</summary>
public sealed class LineageModule : IFeatureModule
{
    public FeatureModuleDescriptor Descriptor => FeatureModuleRegistry.Lineage;
}

/// <summary>
/// T5, IV's ancestry at creation in Phantasie I, II and III as an option, base races only (user s72;
/// <c>reverse-engineering/x68000/phantasie4/logic-specs/ancestry.md</c> section 13). The pair rules, IV's inheritance
/// (<see cref="P4Creation.Inherit"/>, unchanged) and the parent references. The game's own creation does the rest
/// (<see cref="GameSession.RollDescendant"/>).
/// </summary>
public static class LineageRules
{
    /// <summary>IV's shipped Hall persons are slots 0–2 (<see cref="P4Hall.FirstImportSlot"/>).</summary>
    private const int ShippedSlots = P4Hall.FirstImportSlot;

    /// <summary>A base race: one of the fifteen living races (Human … Minotaur). Undead and IV's hybrids / "?" are not.</summary>
    public static bool IsBaseRace(int race) => race is >= (int)Race.Human and <= (int)Race.Minotaur;

    /// <summary>
    /// The races <paramref name="game"/>'s Guild New can give: I and II pick Human … Gnome and RANDOM gives Pixie … Minotaur
    /// (<see cref="CharacterCreation.RollRandomRace"/>); III's RANDOM list holds the same ten (<c>Phantasie3Campaign</c>).
    /// So all fifteen base races in each.
    /// </summary>
    public static bool OffersRace(GameId game, Race race) =>
        game is GameId.P1 or GameId.P2 or GameId.P3 && IsBaseRace((int)race);

    /// <summary>
    /// The pair rule (section 13.2): both persons the same race (the descendant's); a new ancestor takes the other's race
    /// (both new: <paramref name="raceIfBothNew"/>); the race must be one the game offers; not the same person twice; not
    /// two of IV's shipped three.
    /// </summary>
    public static LineagePair Pair(GameId game, LineagePerson? first, LineagePerson? second, Race raceIfBothNew)
    {
        if (first is not null && second is not null)
        {
            if (first.Id == second.Id)
                return new(first.Race, LineageRefusal.SamePerson);
            if (first.HallSlot is < ShippedSlots && second.HallSlot is < ShippedSlots)
                return new(first.Race, LineageRefusal.BothShipped);
            if (first.Race != second.Race)
                return new(first.Race, LineageRefusal.MixedRaces);
        }
        var race = first?.Race ?? second?.Race ?? raceIfBothNew;
        return OffersRace(game, race) ? new(race, LineageRefusal.None) : new(race, LineageRefusal.RaceNotOffered);
    }

    /// <summary>
    /// The descendant's five abilities (section 13.3): a new ancestor is the target game's own plain roll for the
    /// descendant's race and class (<paramref name="rollNew"/>, ancestor 1 first; s149: not IV's <c>_newattr</c>, whose
    /// higher roll made "new + new" beat I's plain roll by about 3 per ability) `[~]`, then IV's <c>_cmbattr</c>
    /// (<see cref="P4Creation.Inherit"/>) with IV's tables. The game's own creation clamps them to its range.
    /// </summary>
    public static int[] Inherit(GameOptions options, P4CreationTables tables, IRandomSource rng, LineagePerson? first,
        LineagePerson? second, Race race, CharacterClass cls, Func<IReadOnlyList<int>> rollNew)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(tables);
        ArgumentNullException.ThrowIfNull(rng);
        ArgumentNullException.ThrowIfNull(rollNew);
        var a1 = first?.ToAncestor() ?? new P4Ancestor((int)race, (int)cls, [.. rollNew()]);
        var a2 = second?.ToAncestor() ?? new P4Ancestor((int)race, (int)cls, [.. rollNew()]);
        return P4Creation.Inherit(options, tables, rng, a1, a2, (int)race, (int)cls);
    }

    /// <summary>
    /// <see cref="Character.Lineage"/> for the pair: each person by its own game's race and class ids and its name; a new
    /// ancestor by <paramref name="game"/>'s ids for the descendant's race and class, no name.
    /// </summary>
    public static Lineage LineageOf(GameId game, LineagePerson? first, LineagePerson? second, Race race, CharacterClass cls) =>
        new([ParentOf(game, first, race, cls), ParentOf(game, second, race, cls)]);

    private static ParentRef ParentOf(GameId game, LineagePerson? p, Race race, CharacterClass cls) =>
        p is null
            ? new ParentRef(new ContentId(game, (int)race).ToString(), new ContentId(game, (int)cls).ToString())
            : new ParentRef(new ContentId(p.Source, (int)p.Race).ToString(), new ContentId(p.Source, p.Class).ToString(), p.Name);

    /// <summary>
    /// A character of <paramref name="source"/>'s save (Guild slot <paramref name="slot"/>) as a Hall-list person, or null
    /// when its race is no base race (an Undead: user s72, base races only) or its class is none of the six.
    /// </summary>
    public static LineagePerson? FromCharacter(Character c, GameId source, int slot)
    {
        ArgumentNullException.ThrowIfNull(c);
        if (!IsBaseRace((int)c.Race) || c.Class is < CharacterClass.Thief or > CharacterClass.Wizard || string.IsNullOrEmpty(c.Name))
            return null;
        var s = c.Stats;
        return new LineagePerson($"{source.Prefix()}:guild-{slot}", c.Name, source, c.Race, (int)c.Class, null,
            [s.Str, s.Int, s.Dex, s.Con, s.Cha]);
    }

    /// <summary>
    /// An IV Hall person (shipped or imported) as a Hall-list person, or null when the race is no base race (Nikademus's
    /// "?", IV's 33). <paramref name="className"/> is IV's name of the class, kept only for a class past I's six.
    /// </summary>
    public static LineagePerson? FromHall(int slot, string name, int race, int cls, IReadOnlyList<int> abilities, string? className)
    {
        ArgumentNullException.ThrowIfNull(abilities);
        if (!IsBaseRace(race) || abilities.Count != 5)
            return null;
        return new LineagePerson($"{GameId.P4.Prefix()}:hall-{slot}", name, GameId.P4, (Race)race, cls,
            cls > (int)CharacterClass.Wizard ? className : null, abilities, slot);
    }
}
