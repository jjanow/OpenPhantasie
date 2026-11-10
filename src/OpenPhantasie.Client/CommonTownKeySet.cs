using Microsoft.Xna.Framework.Input;
using OpenPhantasie.Core;

namespace OpenPhantasie.Client;

/// <summary>
/// QoL COMMON TOWN KEYS (<see cref="GameSettings.CommonTownKeys"/>, user s180, D3-074): one set of town letters for every
/// game, the Phantasie I manual's (p.3 / p.11, D-066: A Armory, B Bank, G Guild, M Mystic, N Inn, L Leave; C Cast, U Use,
/// D Distribute, I Inspect, S Save) plus III's T Trade (III PDF 13 / pr.21). I and II use exactly these already
/// (<see cref="TownMenu"/>). With the switch on, a letter of the set means the same in III (over the Amiga bar's own
/// letters) and IV (over its door digits and S System); a game's keys outside the set keep working.
/// </summary>
public static class CommonTownKeySet
{
    /// <summary>The set's letters.</summary>
    public static readonly IReadOnlyList<Keys> Letters =
        [Keys.A, Keys.B, Keys.G, Keys.M, Keys.N, Keys.L, Keys.C, Keys.U, Keys.D, Keys.I, Keys.S, Keys.T];

    public static bool Contains(Keys key) => Letters.Contains(key);

    /// <summary>IV's building a letter of the set opens, or null (the letter is not a door).</summary>
    public static P4Building? P4Door(Keys key) => key switch
    {
        Keys.A => P4Building.Armory,
        Keys.B => P4Building.Bank,
        Keys.G => P4Building.Guild,
        Keys.M => P4Building.Mystic,
        Keys.N => P4Building.Inn,
        Keys.L => P4Building.Leave,
        _ => null,
    };
}
