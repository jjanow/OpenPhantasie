using Microsoft.Xna.Framework.Input;
using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client;

/// <summary>A key-driven modal panel (cast, inspect, use potion, health): it owns every key while open.</summary>
public interface IModalDialog
{
    bool IsOpen { get; }

    string Help { get; }

    /// <summary>Handle this frame's new keys; returns a HUD line when something happened or the dialog closed.</summary>
    string? HandleKeys(IEnumerable<Keys> newKeys, GameSession session);

    IReadOnlyList<string> Lines(GameSession session, GameDataRepository data);
}

/// <summary>
/// A page of a longer list a dialog shows in its <see cref="IModalDialog.Lines"/> (s99: the panel draws a bar beside it):
/// panel lines <see cref="FirstLine"/> .. + <see cref="Shown"/> hold list rows <see cref="Offset"/> .. of <see cref="Total"/>.
/// </summary>
public sealed record ListPage(int FirstLine, int Shown, int Offset, int Total)
{
    /// <summary>The page, or null when the whole list shows (no bar).</summary>
    public static ListPage? Of(int firstLine, int shown, int offset, int total) =>
        total > shown ? new ListPage(firstLine, shown, offset, total) : null;
}

/// <summary>A dialog that pages a long list itself (arrows / wheel); the panel shows where the page is.</summary>
public interface IPagedDialog
{
    /// <summary>The page the last <see cref="IModalDialog.Lines"/> call showed, or null.</summary>
    ListPage? Page { get; }
}
