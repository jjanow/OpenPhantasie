using Microsoft.Xna.Framework.Input;

namespace OpenPhantasie.Client;

/// <summary>
/// s158: III Amiga's confirm question in window 1 (<c>0xaae2</c>: the text as window 1's title, OK / CANCEL, sound 36
/// first) for the town bar's File › Load ("Are you sure you want to load an old\rgame over this current one?",
/// <c>0xb1f0</c>) and File › Save ("… save this\rgame over your previously saved one?", <c>0xba9a</c>), FORMAT.md 8.4 / 8.9.
/// OK runs the item (<see cref="Action"/>), CANCEL drops it; the window owns every key while up (the keys of
/// <see cref="Game1.KeepWindowChoice"/>, Esc = CANCEL; other keys are read and dropped, as <c>0x3ba0</c>).
/// </summary>
public sealed class AmigaConfirmWindow
{
    /// <summary>The question (rows split at "\r"); empty when the window is not up.</summary>
    public string Text { get; private set; } = "";

    /// <summary>The bar item OK runs.</summary>
    public string Action { get; private set; } = "";

    public bool Shown => Text.Length > 0;

    /// <summary>The question's rows, as <c>0x854</c> breaks a title at "\r".</summary>
    public IReadOnlyList<string> Lines => Text.Split('\r', '\n');

    public void Open(string text, string action)
    {
        Text = text ?? "";
        Action = action ?? "";
    }

    public void Close()
    {
        Text = "";
        Action = "";
    }

    /// <summary>
    /// The frame's keys while the window is up: OK (Return, Space, O, Y; a click on OK sends O) closes it and returns its
    /// action, CANCEL (C, N, Esc; a click sends C) closes it and returns ""; other keys are dropped. Used = the window was
    /// up (the keys go nowhere else).
    /// </summary>
    public (bool Used, string Action) HandleKeys(IEnumerable<Keys> newKeys)
    {
        if (!Shown)
            return (false, "");
        foreach (var key in newKeys)
        {
            var choice = key == Keys.Escape ? 2 : Game1.KeepWindowChoice(key);
            if (choice is null)
                continue;
            var action = choice == 1 ? Action : "";
            Close();
            return (true, action);
        }
        return (true, "");
    }
}
