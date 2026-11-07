using System.Runtime.CompilerServices;
using OpenPhantasie.Core;

namespace OpenPhantasie.Client.Tests;

/// <summary>
/// T1.5: Core's text and the Client's come from one table; load Phantasie I's English one before any test. Core ids
/// only II or III has (II's <c>core.p2.*</c>, III's <c>core.p3.*</c>, the messages behind III's town screens) fall back to their tables, and IV's in-game text
/// (<c>p4x.*</c>, <c>o&lt;map&gt;.*</c>) to IV's, as in the Core tests.
/// </summary>
internal static class GameTextSetup
{
    [ModuleInitializer]
    internal static void LoadPhantasieIEnglish()
    {
        UiText.Table = UiText.For(GameId.P1);
        GameText.Source = new Fallback(UiText.Table, UiText.For(GameId.P2), UiText.For(GameId.P3), UiText.For(GameId.P4));
    }

    private sealed class Fallback(params OpenPhantasie.Data.StringTable[] tables) : ITextSource
    {
        public string Get(string id) => (tables.FirstOrDefault(t => t.Contains(id)) ?? tables[^1]).Get(id);
    }
}
