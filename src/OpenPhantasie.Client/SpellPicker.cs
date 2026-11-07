using Microsoft.Xna.Framework.Input;
using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client;

/// <summary>
/// The DOS scrolling spell picker (COM <c>0x139c</c>) [C] (s58 static, save-file-format.md §member view): spell names only,
/// the highlight bar fixed on one row with up to <see cref="RowsAbove"/> known spells above it and <see cref="RowsBelow"/>
/// below; Up/Down move the cursor. DOS keeps one global cursor for every spell picker (starts at 0), so one instance is
/// shared; the remaster's cast dialogs still use numbered lists, so only Inspect → SPELLS uses it for now. Spells costing
/// more than current MP look the same as the rest (s58 DOSBox: one white for every name), so there is no colour change.
/// s58 DOSBox frames <c>b6</c>/<c>b7_c</c> confirm the fixed bar: after Down the names move up under the same bar row.
/// </summary>
public sealed class SpellPicker
{
    public const int RowsAbove = 3;
    public const int RowsBelow = 2;

    /// <summary>The shared cursor: an index into the member's known spells.</summary>
    public int Cursor { get; private set; }

    /// <summary>The member's known spells in spell order.</summary>
    public static IReadOnlyList<int> Known(Character ch) =>
        Enumerable.Range(0, ch.SpellsKnown.Length).Where(id => Magic.KnowsSpell(ch, id)).ToList(); // II: 56 (DIVINE II)

    /// <summary>Clamp the cursor to the member's list (DOS behaviour past the end <c>[~]</c>).</summary>
    public void Fit(Character ch) => Cursor = Math.Clamp(Cursor, 0, Math.Max(0, Known(ch).Count - 1));

    /// <summary>Up/Down move the cursor (stays put at either end <c>[~]</c>); returns true if the key was one of them.</summary>
    public bool Move(Character ch, Keys key)
    {
        if (key is not (Keys.Up or Keys.Down))
            return false;
        var count = Known(ch).Count;
        Cursor = Math.Clamp(Cursor + (key == Keys.Down ? 1 : -1), 0, Math.Max(0, count - 1));
        return true;
    }

    /// <summary>
    /// The window: <see cref="RowsAbove"/> rows, the bar row (marked <c>&gt;</c>), <see cref="RowsBelow"/> rows; a row with
    /// no spell is blank.
    /// </summary>
    public IReadOnlyList<string> Rows(Character ch, GameDataRepository data)
    {
        var known = Known(ch);
        var rows = new List<string>();
        for (var i = Cursor - RowsAbove; i <= Cursor + RowsBelow; i++)
        {
            var name = i >= 0 && i < known.Count ? data.GetSpell(known[i]).Name : "";
            rows.Add((i == Cursor && known.Count > 0 ? ">" : " ") + name);
        }
        return rows;
    }
}
