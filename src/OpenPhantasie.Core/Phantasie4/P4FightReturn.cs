namespace OpenPhantasie.Core;

/// <summary>Where the party goes on its map after a fight (<see cref="P4FightReturn.For"/>).</summary>
public enum P4FightReturnKind
{
    /// <summary>The party stays on the fight's square.</summary>
    Stay,

    /// <summary>Back to the square the last step came from (<c>_cox</c> / <c>_coy</c> outdoors, <c>_oldcx</c> in a dungeon).</summary>
    Previous,

    /// <summary>D4-052 option <c>random</c>: a random open square of the same level / outdoor area <c>[~]</c>.</summary>
    Random,

    /// <summary>D4-052 option <c>entrance</c>: the square where the party came onto the location map <c>[~]</c>.</summary>
    Entrance,
}

/// <summary>A square the loops put the party on after a fight; <see cref="MoorHere"/>: a sea fight's ship is moored there.</summary>
public sealed record P4FightMove(int X, int Y, bool MoorHere = true);

/// <summary>
/// After a fight (<c>combat.md</c> §8, §11): <c>_enc_end</c> copies the old square <c>_cox</c> / <c>_coy</c> into the current
/// one for ends 2 (Teleportation), 3 (Beg for mercy) and 4 (fled), and <c>_oencgo</c> reloads the outdoor map there (a sea
/// fight moors the ship on that square) `[C]`. <c>_cox</c> is only written by the outdoor loop (<c>_omove</c>,
/// <c>_oload</c>), so on a location map the code leaves the party on the fight's square. Greet (106) and every win stay.
/// Teleportation goes where <c>p4.teleport</c> says (D4-052, D4-070): previous square (also in a location map, the user's
/// default), a random open square, the location map's entrance, or <c>stay</c> (the code: like a flight).
/// </summary>
public static class P4FightReturn
{
    public static P4FightReturnKind For(GameOptions o, P4BattleEnd end, bool outdoors)
    {
        ArgumentNullException.ThrowIfNull(o);
        switch (end)
        {
            case P4BattleEnd.Teleported:
                return P4Combat.Teleport(o) switch
                {
                    P4TeleportDestination.RandomSquare => P4FightReturnKind.Random,
                    // Outdoors there is no entrance: the code's previous square [~].
                    P4TeleportDestination.Entrance => outdoors ? P4FightReturnKind.Previous : P4FightReturnKind.Entrance,
                    // D4-070: the code's _cox copy only moves the party outdoors.
                    P4TeleportDestination.Stay => outdoors ? P4FightReturnKind.Previous : P4FightReturnKind.Stay,
                    _ => P4FightReturnKind.Previous,
                };
            case P4BattleEnd.Begged or P4BattleEnd.Fled:
                return outdoors ? P4FightReturnKind.Previous : P4FightReturnKind.Stay;
            default:
                return P4FightReturnKind.Stay;
        }
    }

    /// <summary>
    /// D4-052 <c>random</c> outdoors <c>[~]</c>: a square of the same 26 × 26 map whose terrain type has a land battlefield
    /// (<c>_e_terrain</c>: desert, hills, forest, grassland, mist, Netherworld; not water, ship, towns or places), picked by
    /// rand over the candidates in map order; null when none.
    /// </summary>
    public static (int X, int Y)? RandomOutdoorSquare(P4OutdoorMap map, P4BattleTables tables, IRandomSource rng)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(tables);
        var open = new List<(int, int)>();
        for (var y = 0; y < P4OutdoorMap.Size; y++)
            for (var x = 0; x < P4OutdoorMap.Size; x++)
                if (tables.FieldFor(P4OutdoorMap.TypeOf(map.Tile(x, y))) is { } f && f.Name != "ship")
                    open.Add((x, y));
        return open.Count == 0 ? null : open[P4Dice.Rand(rng, open.Count) - 1];
    }

    /// <summary>
    /// D4-052 <c>random</c> in a location map <c>[~]</c>: a square with at least one open side (move code 0) that is not
    /// water (where no fight can start), picked by rand over the candidates in map order; null when none.
    /// </summary>
    public static (int X, int Y)? RandomDungeonSquare(P4DungeonMap map, IRandomSource rng)
    {
        ArgumentNullException.ThrowIfNull(map);
        var open = new List<(int, int)>();
        for (var y = 0; y < P4DungeonFile.Size; y++)
            for (var x = 0; x < P4DungeonFile.Size; x++)
            {
                if (map.FightRefused(x, y))
                    continue;
                for (var d = 0; d < 4; d++)
                {
                    if (map.Move(x, y, d) != P4DungeonMap.Open)
                        continue;
                    open.Add((x, y));
                    break;
                }
            }
        return open.Count == 0 ? null : open[P4Dice.Rand(rng, open.Count) - 1];
    }
}
