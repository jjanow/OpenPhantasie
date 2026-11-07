using System.Runtime.CompilerServices;
using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Core.Tests;

/// <summary>
/// T1.5: Core's text comes from the string tables; the tests run with Phantasie I's real English table. Ids only II has
/// (<c>core.p2.*</c>) fall back to II's table (then III's and IV's, whose IV ids <c>p4x.*</c> / <c>oNN.*</c> are its own), so II's campaign tests run in the same process without changing I's text.
/// Tests that need a game's own text for an id both games have swap <see cref="GameText.Source"/> themselves.
/// </summary>
internal static class GameTextSetup
{
    internal static readonly StringTable PhantasieI = StringTable.ForGame(GameId.P1);
    internal static readonly StringTable PhantasieII = StringTable.ForGame(GameId.P2);
    internal static readonly StringTable PhantasieIII = StringTable.ForGame(GameId.P3);
    internal static readonly StringTable PhantasieIV = StringTable.ForGame(GameId.P4);

    [ModuleInitializer]
    internal static void LoadPhantasieIEnglish() => GameText.Source = new Fallback(PhantasieI, PhantasieII, PhantasieIII, PhantasieIV);

    private sealed class Fallback(params StringTable[] tables) : ITextSource
    {
        public string Get(string id) => (tables.FirstOrDefault(t => t.Contains(id)) ?? tables[^1]).Get(id);
    }
}
