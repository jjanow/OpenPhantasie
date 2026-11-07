using Microsoft.Xna.Framework;

namespace OpenPhantasie.Client;

/// <summary>Card positions for the party strip, and whether they are portrait cards or one-line rows.</summary>
public sealed record PartyCardLayout(bool Lines, IReadOnlyList<Rectangle> Cards);

/// <summary>
/// Pure layout for the party strip (P2): up to seven members (six adventurers + a summoned elemental in slot 7, DOS party
/// size <c>[0x648e]</c> = 7 while it fights) placed inside a screen region without spilling out of it. Portrait cards
/// are used when they fit (one or more rows); otherwise every member gets a one-line row "NAME hp/max". A card is as tall
/// as the pack's portraits need (<see cref="CardHeightFor"/>): III Amiga's 32×48 full figures get 70-row cards, never cut.
/// </summary>
public static class PartyStripLayout
{
    /// <summary>Six adventurers plus the elemental (party slot 7).</summary>
    public const int MaxCards = 7;

    /// <summary>A card for a 32×32 portrait (I, II): border, portrait, name and HP lines.</summary>
    public const int CardHeight = 54;
    public const int CardGap = 2;
    public const int PortraitSize = 32;

    /// <summary>
    /// Height of a card whose portrait box is <paramref name="portraitHeight"/> rows, with the name on
    /// <paramref name="nameLines"/> lines and the HP line under it.
    /// </summary>
    public static int CardHeightFor(int portraitHeight, int nameLines = 1) =>
        CardHeight + Math.Max(PortraitSize, portraitHeight) - PortraitSize + (Math.Max(1, nameLines) - 1) * LineHeight;

    /// <summary>
    /// <paramref name="name"/> as a card <paramref name="cardWidth"/> wide shows it (s99, user: no text cut off): one line
    /// when it fits, else split into the fewest even pieces that do ("ARENTINO" on a 48-pixel card: "AREN" / "TINO").
    /// </summary>
    public static IReadOnlyList<string> NameLines(string name, int cardWidth)
    {
        var fits = CharsFitting(cardWidth - 4);
        if (string.IsNullOrEmpty(name) || name.Length <= fits)
            return [name ?? ""];
        var lines = (name.Length + fits - 1) / fits;
        var per = (name.Length + lines - 1) / lines;
        return [.. Enumerable.Range(0, lines).Select(i => name.Substring(i * per, Math.Min(per, name.Length - i * per)))];
    }

    /// <summary>
    /// The HP line ("192/192") as a card <paramref name="cardWidth"/> wide shows it: one line when it fits, else broken
    /// after the "/" (seven cards in combat with three-digit HP).
    /// </summary>
    public static IReadOnlyList<string> HpLines(string hp, int cardWidth)
    {
        var slash = hp.IndexOf('/');
        return hp.Length <= CharsFitting(cardWidth - 4) || slash < 0 ? [hp] : [hp[..(slash + 1)], hp[(slash + 1)..]];
    }

    /// <summary>
    /// The most text lines past the card's usual two (name, HP) any card of <paramref name="layout"/> needs, plus one:
    /// the <c>nameLines</c> a layout is computed with. <paramref name="hps"/> may be empty (names only).
    /// </summary>
    public static int NameLinesNeeded(PartyCardLayout layout, IReadOnlyList<string> names, IReadOnlyList<string>? hps = null)
    {
        if (layout.Lines)
            return 1;
        var most = 1;
        for (var i = 0; i < layout.Cards.Count && i < names.Count; i++)
            most = Math.Max(most, NameLines(names[i], layout.Cards[i].Width).Count
                + (hps is not null && i < hps.Count ? HpLines(hps[i], layout.Cards[i].Width).Count - 1 : 0));
        return most;
    }

    /// <summary>Top of a card's name line; the HP line is one line under it.</summary>
    public static int NameTop(Rectangle card, int portraitHeight) => card.Y + 4 + Math.Max(PortraitSize, portraitHeight);

    /// <summary>Narrowest portrait card: the 32-pixel portrait plus a border each side.</summary>
    public const int MinCardWidth = PortraitSize + 4;

    public const int LineHeight = PixelFont.LineHeight;

    public static PartyCardLayout Compute(int count, Rectangle area, int maxCardWidth = 56, int portraitHeight = PortraitSize,
        int nameLines = 1, int gap = CardGap)
    {
        count = Math.Clamp(count, 0, MaxCards);
        if (count == 0)
            return new PartyCardLayout(false, []);
        var cardHeight = CardHeightFor(portraitHeight, nameLines);

        // Portrait cards: the fewest rows whose cards are wide enough and whose height fits.
        for (var rows = 1; rows <= count; rows++)
        {
            var cols = (count + rows - 1) / rows;
            var width = Math.Min(maxCardWidth, area.Width / cols);
            if (width < MinCardWidth)
                continue;
            if (rows * cardHeight + (rows - 1) * gap > area.Height)
                break; // more rows only get taller
            var cards = new List<Rectangle>(count);
            for (var i = 0; i < count; i++)
                cards.Add(new Rectangle(area.X + i % cols * width, area.Y + i / cols * (cardHeight + gap),
                    width - gap, cardHeight));
            return new PartyCardLayout(false, cards);
        }

        // One-line rows, in as many columns as the height needs.
        var perColumn = Math.Max(1, area.Height / LineHeight);
        var columns = (count + perColumn - 1) / perColumn;
        var colWidth = area.Width / columns;
        var lines = new List<Rectangle>(count);
        for (var i = 0; i < count; i++)
            lines.Add(new Rectangle(area.X + i / perColumn * colWidth, area.Y + i % perColumn * LineHeight, colWidth,
                LineHeight));
        return new PartyCardLayout(true, lines);
    }

    /// <summary>
    /// II's combat row the ST way (<c>0x181ca</c> [C] s98): <c>n</c> portraits spread over the 320-pixel screen with the gap
    /// <c>(320 − 32·n)/(n + 1)</c> before, between and after them (<c>0x1814e</c>, <see cref="DefeatScene.SlotX"/>), so a
    /// row rebuilt after a death (<c>0x18176</c>) spreads out again instead of packing from the left. Each card is centred
    /// on its ST portrait, at most <paramref name="maxCardWidth"/> wide and narrow enough not to touch its neighbour, and
    /// kept inside <paramref name="area"/>. Falls back to <see cref="Compute"/> when one row of cards does not fit.
    /// </summary>
    public static PartyCardLayout ComputeEvenRow(int count, Rectangle area, int maxCardWidth = 56,
        int portraitHeight = PortraitSize, int nameLines = 1)
    {
        count = Math.Clamp(count, 0, MaxCards);
        if (count == 0)
            return new PartyCardLayout(false, []);
        var cardHeight = CardHeightFor(portraitHeight, nameLines);
        var pitch = DefeatScene.SlotX(count, 1) - DefeatScene.SlotX(count, 0);
        var width = count == 1 ? Math.Min(maxCardWidth, area.Width) : Math.Min(maxCardWidth, pitch - CardGap);
        if (width < MinCardWidth || cardHeight > area.Height)
            return Compute(count, area, maxCardWidth, portraitHeight, nameLines);
        var cards = new List<Rectangle>(count);
        for (var i = 0; i < count; i++)
        {
            var x = DefeatScene.SlotX(count, i) + PortraitSize / 2 - width / 2;
            x = Math.Clamp(x, area.X, area.Right - width);
            cards.Add(new Rectangle(x, area.Y, width, cardHeight));
        }
        return new PartyCardLayout(false, cards);
    }

    /// <summary>How many characters of text fit in <paramref name="pixels"/> (PixelFont advance 6).</summary>
    public static int CharsFitting(int pixels) => Math.Max(1, (pixels + 1) / PixelFont.Advance);
}
