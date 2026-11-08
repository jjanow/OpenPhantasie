using System.Text.RegularExpressions;
using Microsoft.Xna.Framework.Input;
using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client;

/// <summary>
/// III Amiga's message box (window 7, <c>0xaaac</c>, s134 / s135) outside the orders screen: a town / guild line whose
/// Amiga equivalent goes through window 7 (the pack's <see cref="KeepWindowsLayout.MessageWindow.TextIds"/>) shows in the
/// window instead of the remaster's band or line, under AMIGA SCREENS. The original's wait (<c>0x3ba0</c> with key 0)
/// ends on any key or a click on OK and takes that key: nothing under the window sees it.
/// </summary>
public sealed class AmigaMessageBox
{
    /// <summary>The text in the window; empty when the window is not up.</summary>
    public string Text { get; private set; } = "";

    public bool Shown => Text.Length > 0;

    /// <summary>
    /// True when <paramref name="line"/> is one of the window's texts (resolved through the string tables). s162: a text
    /// with format items (<c>{0}</c>: a member's name, Monster Evaluate's level) takes any line it formats to.
    /// </summary>
    public static bool Takes(KeepWindowsLayout.MessageWindow? window, string? line) =>
        window is not null && !string.IsNullOrEmpty(line) && window.TextIds.Any(id => Matches(GameText.S(id), line));

    /// <summary>Whether <paramref name="line"/> is <paramref name="text"/> with each format item filled by some text.</summary>
    public static bool Matches(string text, string line)
    {
        if (text == line)
            return true;
        if (!text.Contains('{'))
            return false;
        var pattern = "^" + Regex.Replace(Regex.Escape(text), @"\\\{\d+(?:,-?\d+)?(?::[^}]*)?}", ".+?") + "$";
        return Regex.IsMatch(line, pattern, RegexOptions.Singleline);
    }

    /// <summary>Opens the window with <paramref name="line"/> when <paramref name="enabled"/> and the window takes it.</summary>
    public bool Offer(KeepWindowsLayout.MessageWindow? window, bool enabled, string? line)
    {
        if (!enabled || !Takes(window, line))
            return false;
        Text = line!;
        return true;
    }

    /// <summary>
    /// While the window is up it owns the frame's keys: any key (a click on OK sends O) closes it. True when the window
    /// was up (the keys are used up); false when it was not, so the keys go on.
    /// </summary>
    public bool HandleKeys(IEnumerable<Keys> newKeys)
    {
        if (!Shown)
            return false;
        if (newKeys.Any())
            Text = "";
        return true;
    }

    /// <summary>Drops the window (a load, the main menu).</summary>
    public void Close() => Text = "";
}
