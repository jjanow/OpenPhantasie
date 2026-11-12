using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client;

/// <summary>On-screen HUD, mode panels, inventory / bestiary overlays.</summary>
public static partial class UiOverlays
{
    // Virtual 320×200 regions — keep mode art out of chrome.
    public const int ContentTop = 14;
    public const int ContentBottom = 164;
    public const int HelpY = 168;
    public const int MessageY = 182;

    private static readonly Color PanelBg = new(16, 22, 36, 235);
    private static readonly Color PanelEdge = new(90, 110, 150);
    private static readonly Color TitleGold = new(220, 200, 120);
    private static readonly Color BodyText = new(190, 200, 220);
    private static readonly Color DimText = new(130, 145, 170);
    private static readonly Color Accent = new(120, 200, 140);

    /// <param name="statusLine">
    /// Null hides the top status line (the manual's wilderness / dungeon screen, D-077); empty shows the plain one
    /// (mode, gold, party, scale); text replaces the scale with it (region / position, <c>Game1.FieldStatus</c>).
    /// </param>
    /// <param name="messageKeys">The message is a command list (combat orders): its key tokens are clickable, and what the
    /// 52-column line cuts off goes on a second line so every choice can be clicked. The shell sets it only once the mouse
    /// is used, so the keyboard screen is unchanged.</param>
    public static void DrawHud(
        SpriteBatch batch,
        Texture2D pixel,
        GameSession session,
        ClientSettings settings,
        string message,
        string help,
        string? statusLine = "",
        bool messageKeys = false)
    {
        Fill(batch, pixel, new Rectangle(0, 0, 320, 12), new Color(6, 8, 14));
        Fill(batch, pixel, new Rectangle(0, 166, 320, 34), new Color(6, 8, 14));
        HLine(batch, pixel, 0, 12, 320, PanelEdge);
        HLine(batch, pixel, 0, 166, 320, PanelEdge);

        if (statusLine is not null)
            PixelFont.Draw(batch, pixel, 4, 3, StatusText(session, settings, statusLine), TitleGold);
        else if (TreasuryGoldLine(session) is { } goldLine)
            // P3 QoL (11-guild-treasury.md section 5): without the status line (STATUS LINES off, the town's list screens,
            // the Guild) the Guild treasury's gold line takes the band's empty row.
            PixelFont.Draw(batch, pixel, 4, 3, goldLine, TitleGold);

        // The key help, then the message, each whole (s99): a line longer than the 52 columns goes on, at a word, on the
        // next line, and the band's rows close up to 8 pixels when the two need four lines.
        var shownHelp = PadWording.Help(help ?? ""); // s183: the pad's wording while the pad is the input
        var helpTokens = ReferenceEquals(shownHelp, help ?? ""); // a pad word ("A") is no key to click
        var helpLines = Lines(shownHelp, 52);
        var messageLines = Lines(message ?? "", 52);
        var (helpY, messageY, pitch) = HudRows(helpLines.Count, messageLines.Count);
        for (var i = 0; i < helpLines.Count; i++)
        {
            PixelFont.Draw(batch, pixel, 4, helpY + i * pitch, helpLines[i], DimText);
            if (helpTokens)
                HitTokens(helpLines[i], 4, helpY + i * pitch, commandList: true, maxChars: 52);
        }
        for (var i = 0; i < messageLines.Count; i++)
        {
            PixelFont.Draw(batch, pixel, 4, messageY + i * pitch, messageLines[i], BodyText);
            if (messageKeys)
                HitTokens(messageLines[i], 4, messageY + i * pitch, commandList: true, maxChars: 52);
        }
    }

    /// <summary>
    /// Tops of the help and message lines in the bottom band and their pitch: <see cref="HelpY"/> / <see cref="MessageY"/>
    /// as always while each is one line; one after the other at 9 pixels for three lines, at 8 for four (rows 166..199).
    /// </summary>
    public static (int Help, int Message, int Pitch) HudRows(int helpLines, int messageLines)
    {
        if (helpLines <= 1 && messageLines <= 1)
            return (HelpY, MessageY, PixelFont.LineHeight);
        if (helpLines == 1 && messageLines == 2)
            return (HelpY, MessageY, PixelFont.LineHeight); // the message's second line at 191, as before
        var pitch = helpLines + messageLines <= 3 ? PixelFont.LineHeight : 8;
        var top = pitch == 8 ? 167 : HelpY;
        return (top, top + Math.Max(1, helpLines) * pitch, pitch);
    }

    /// <summary>
    /// HUD for the full-screen overworld map, which fills rows 15..194 as on the Amiga: only the message, at the top,
    /// on as many lines as it needs (location signs run to three). s185: then the key help (<paramref name="help"/>, in the
    /// pad's words while the pad is the input), whole, on the band's next rows; with no message it takes the first row
    /// (<see cref="MapHudLines"/>). The shell gives both only for a dialog's help (<c>Game1.MapOnlyBand</c>), so the
    /// map's own help never grows the band over the map.
    /// </summary>
    /// <param name="gold">P3 QoL: the Guild treasury's gold line, right-aligned on the band's first row; the message then
    /// keeps to the columns left of it.</param>
    public static void DrawMapHud(SpriteBatch batch, Texture2D pixel, string message, string? gold = null, string? help = null)
    {
        var shownHelp = PadWording.Help(help ?? ""); // the pad's wording while the pad is the input
        var helpTokens = ReferenceEquals(shownHelp, help ?? ""); // a pad word ("A") is no key to click
        var lines = MapHudLines(message, gold, shownHelp);
        var height = Math.Max(1, lines.Count) * PixelFont.LineHeight + 4;
        Fill(batch, pixel, new Rectangle(0, 0, 320, Math.Max(14, height)), new Color(6, 8, 14));
        for (var i = 0; i < lines.Count; i++)
        {
            var y = 3 + i * PixelFont.LineHeight;
            PixelFont.Draw(batch, pixel, 4, y, lines[i].Text, lines[i].Help ? DimText : BodyText);
            if (lines[i].Help && helpTokens)
                HitTokens(lines[i].Text, 4, y, commandList: true, maxChars: 52);
        }
        if (gold is not null)
            PixelFont.Draw(batch, pixel, 316 - PixelFont.Measure(gold), 3, gold, TitleGold);
    }

    /// <summary>
    /// The full-screen map's band rows (<see cref="DrawMapHud"/>): the message cut to the columns the gold line leaves (as
    /// before), then the help word-wrapped to the band's 52 columns (the first row's room when it is the first row). An empty
    /// message keeps its blank row only when there is no help. Nothing is dropped.
    /// </summary>
    public static IReadOnlyList<(string Text, bool Help)> MapHudLines(string? message, string? gold, string? help)
    {
        var cols = gold is null ? 52 : Math.Max(1, 52 - (PixelFont.Measure(gold) + PixelFont.Advance - 1) / PixelFont.Advance - 1);
        var lines = (message ?? "").Split('\n')
            .SelectMany(l => l.Length == 0 ? [""] : Enumerable.Range(0, (l.Length + cols - 1) / cols).Select(i => l.Substring(i * cols, Math.Min(cols, l.Length - i * cols))))
            .Select(l => (Text: l, Help: false))
            .ToList();
        if (string.IsNullOrWhiteSpace(help))
            return lines;
        if (lines.All(l => l.Text.Length == 0))
            lines.Clear();
        lines.AddRange(Lines(help, lines.Count == 0 ? cols : 52).Select(l => (l, true)));
        return lines;
    }

    /// <summary>Room the status line has at x 4 (the 52 columns of the band).</summary>
    public const int StatusWidth = 312;

    /// <summary>
    /// The top status line: mode, gold, party, then <paramref name="statusLine"/> (region / position) or, when it is empty,
    /// the scale. Under the Guild treasury (P3 QoL, <c>11-guild-treasury.md</c> section 5) the gold is
    /// <see cref="TreasuryGoldLine"/>; when that line is wider than the band it leaves out the mode word (the position
    /// names it), then the trip part.
    /// </summary>
    public static string StatusText(GameSession session, ClientSettings settings, string statusLine)
    {
        var alive = session.Party.Members.Count(m => m.HpCur > 0);
        var count = session.Party.Members.Count;
        var mode = UiText.Name(session.Mode).ToUpperInvariant();
        if (TreasuryGoldLine(session) is not { } full)
            return statusLine.Length > 0
                ? UiText.F("ui.x_gold_x_party_x_x", mode, session.Party.GoldCarried, alive, count, statusLine)
                : UiText.F("ui.x_gold_x_party_x_x_2", mode, session.Party.GoldCarried, alive, count, settings.IntegerScale);
        string Line(string gold, bool withMode) => (statusLine.Length > 0, withMode) switch
        {
            (true, true) => UiText.F("ui.x_x_party_x_x", mode, gold, alive, count, statusLine),
            (true, false) => UiText.F("ui.x_party_x_x", gold, alive, count, statusLine),
            (false, true) => UiText.F("ui.x_x_party_x_scale_x", mode, gold, alive, count, settings.IntegerScale),
            _ => UiText.F("ui.x_party_x_scale_x", gold, alive, count, settings.IntegerScale),
        };
        var plain = TreasuryReport.GoldLine(session.SpendableGold, 0);
        string[] tries = [Line(full, true), Line(full, false), Line(plain, true), Line(plain, false)];
        return tries.FirstOrDefault(t => PixelFont.Measure(t) <= StatusWidth) ?? tries[^1];
    }

    /// <summary>
    /// The Guild treasury's gold line (<c>11-guild-treasury.md</c> section 5): spendable gold, plus the trip gold away from
    /// town (Town and Guild show none); null while the option is off.
    /// </summary>
    public static string? TreasuryGoldLine(GameSession session) => session.TreasuryOn
        ? TreasuryReport.GoldLine(session.SpendableGold, session.Mode is GameMode.Town or GameMode.Guild ? 0 : session.TripGold)
        : null;

    /// <summary>
    /// The gold line for a room <paramref name="room"/> pixels wide (remaster font): the whole line, or without the trip
    /// part when that is too wide; null when the treasury is off or even the short line does not fit.
    /// </summary>
    public static string? TreasuryGoldFitting(GameSession session, int room)
    {
        if (TreasuryGoldLine(session) is not { } full)
            return null;
        if (PixelFont.Measure(full) <= room)
            return full;
        var plain = TreasuryReport.GoldLine(session.SpendableGold, 0);
        return PixelFont.Measure(plain) <= room ? plain : null;
    }

    /// <summary>
    /// A menu bar's free right end (I Amiga's map screen, III Amiga's town and outside bars): the gold line right-aligned
    /// (4 px from the edge) when it fits right of <paramref name="usedRight"/> (the last title's end).
    /// </summary>
    public static void DrawBarGold(SpriteBatch batch, Texture2D pixel, GameSession? session, int usedRight, int y, Color ink)
    {
        if (session is null)
            return;
        using var font = PixelFont.With(PixelFont.Remaster5x7);
        if (TreasuryGoldFitting(session, 316 - usedRight - 6) is { } gold)
            PixelFont.Draw(batch, pixel, 316 - PixelFont.Measure(gold), y, gold, ink);
    }

    /// <summary>
    /// The gold the town screens show (the Armory header, Distribute's SELL row): the purse, or under the Guild treasury
    /// everything the party can spend.
    /// </summary>
    public static long ShownGold(GameSession session) => session.TreasuryOn ? session.SpendableGold : session.Party.GoldCarried;

    /// <summary>The manual's wilderness command list (WILDERNESS TRAVEL, D-076/D-077).</summary>
    public static string[] FieldCommands =>
    [
        UiText.S("ui.c_ast_h_ealth"),
        UiText.S("ui.i_nspect_u_se_potion"),
        UiText.S("ui.t_ime_lag"),
        UiText.S("ui.3_n_e_s_w_move"),
    ];

    /// <summary>The dungeon's command line: the wilderness one plus L)OOK (manual DUNGEON EXPLORATION).</summary>
    public static string[] DungeonFieldCommands =>
    [
        UiText.S("ui.c_ast_h_ealth"),
        UiText.S("ui.i_nspect_u_se_potion"),
        UiText.S("ui.t_ime_lag_l_ook"),
        UiText.S("ui.3_n_e_s_w_move"),
    ];

    /// <summary>
    /// The command list for <paramref name="mode"/>; without T)IME LAG when <paramref name="timeLagCommand"/> is false (the
    /// D-073 QoL switch moved it into the Options tab).
    /// </summary>
    public static string[] FieldCommandsFor(GameMode mode, bool timeLagCommand = true)
    {
        var dungeon = mode == GameMode.Dungeon;
        var commands = dungeon ? DungeonFieldCommands : FieldCommands;
        if (timeLagCommand)
            return commands;
        if (dungeon)
        {
            commands[2] = UiText.S("ui.l_ook");
            return commands;
        }
        return [.. commands.Where((_, i) => i != 2)];
    }

    /// <summary>
    /// Where the party pictures go beside the wilderness / dungeon map: two rows of cards, three to a row 51 pixels wide
    /// (s99: an eight-letter name fits whole; the maps end by x 160).
    /// </summary>
    public static readonly Rectangle FieldPartyArea = new(162, ContentTop + 2, 156, 2 * PartyStripLayout.CardHeight + PartyStripLayout.CardGap);

    /// <summary>
    /// The sidebar's cards when they do not all fit (a summoned elemental's 32×48 portrait, nine-letter names): the wheel, the
    /// bar or <see cref="SidebarScroll"/>'s keys.
    /// </summary>
    public static ScrollBox FieldPartyScroll { get; } = new();

    /// <summary>
    /// QoL TERRAIN KEY (PLAN P3, D2-014; II's wilderness): the party keeps the first row of cards (the rest scroll in
    /// <see cref="FieldPartyScroll"/>) and the key takes the rows under it, down to where the cards end without it, so
    /// neither covers the other, the map or the command list.
    /// </summary>
    public static (Rectangle Party, Rectangle Key) TerrainKeyLayout(AssetCatalog assets, GameSession session)
    {
        // The first row of cards as the scrolling strip lays it out (room for its bar; two-line names make it taller).
        var all = FieldPartyArea;
        var narrower = new Rectangle(all.X, all.Y, all.Width - ScrollBox.BarWidth - 1, 1000);
        var cards = PlanPartyStrip(assets, session, narrower, 52, null, false, true, 1).Layout.Cards;
        var row = cards.Count == 0 ? PartyStripLayout.CardHeight
            : cards.Where(c => c.Y == cards[0].Y).Max(c => c.Bottom) - all.Y;
        var party = all with { Height = Math.Clamp(row, 1, all.Height - PixelFont.LineHeight - 2) };
        return (party, new Rectangle(all.X, party.Bottom + 1, all.Width, all.Bottom - party.Bottom - 1));
    }

    /// <summary>The terrain key's rows when they do not all fit (the wheel over the box, its bar, or <see cref="SidebarScroll"/>'s keys).</summary>
    public static ScrollBox TerrainKeyScroll { get; } = new();

    /// <summary>
    /// The terrain key in <paramref name="area"/>: per row its swatches (each one map cell of the pack picture: a fill
    /// pattern's top-left cell as the map shows it, an icon whole; framed, so the black dark voids show) and its name.
    /// Rows past the box scroll (<see cref="TerrainKeyScroll"/>); a picture the pack lacks leaves its name standing.
    /// </summary>
    public static void DrawTerrainKey(SpriteBatch batch, Texture2D pixel, AssetCatalog assets,
        IReadOnlyList<TerrainKeyEntry> entries, Rectangle area)
    {
        var cw = assets.Maps?.CellWidth ?? 12;
        var ch = assets.Maps?.CellHeight ?? 9;
        var pitch = Math.Max(ch, PixelFont.LineHeight) + 1; // frames of adjacent rows share a line
        var visible = Math.Max(1, (area.Height - 1) / pitch);
        var box = TerrainKeyScroll;
        box.Set(entries.Count, visible);
        HitScrollArea(box, area);
        var swatchColumns = Math.Max(1, entries.Max(e => e.Pictures.Count));
        var textX = area.X + 2 + swatchColumns * (cw + 1) + 3;
        for (var row = 0; row < visible && box.Offset + row < entries.Count; row++)
        {
            var entry = entries[box.Offset + row];
            var y = area.Y + 1 + row * pitch;
            for (var i = 0; i < entry.Pictures.Count; i++)
            {
                var x = area.X + 2 + i * (cw + 1);
                var cell = new Rectangle(x, y, cw, ch);
                Fill(batch, pixel, new Rectangle(x - 1, y - 1, cw + 2, ch + 2), PanelEdge);
                Fill(batch, pixel, cell, Color.Black);
                if (assets.Get(entry.Pictures[i]) is { } tex)
                {
                    var src = new Rectangle(0, 0, Math.Min(tex.Width, cw), Math.Min(tex.Height, ch));
                    batch.Draw(tex, new Rectangle(x + (cw - src.Width) / 2, y + (ch - src.Height) / 2, src.Width, src.Height),
                        src, Color.White);
                }
            }
            PixelFont.Draw(batch, pixel, textX, y + (ch - PixelFont.GlyphHeight + 1) / 2, UiText.S(entry.Label), BodyText);
        }
        DrawScrollBar(batch, pixel, box, area);
    }

    /// <summary>Top of the command list, under <see cref="FieldPartyArea"/>.</summary>
    public static int FieldCommandsTop => FieldPartyArea.Bottom + 2;

    /// <summary>
    /// D-077 (manual WILDERNESS TRAVEL): beside the map, "pictures of the party members and a list of command options".
    /// Used by the wilderness and the dungeon screen (the dungeon map replaces the wilderness map).
    /// </summary>
    public static void DrawFieldSidebar(
        SpriteBatch batch, Texture2D pixel, AssetCatalog assets, GameSession session, bool timeLagCommand = true,
        IReadOnlyList<TerrainKeyEntry>? terrainKey = null)
    {
        // QoL TERRAIN KEY (II, D2-014): the key shares the room beside the map with the party; each scrolls what does not fit.
        var (partyArea, keyArea) = terrainKey is { Count: > 0 } ? TerrainKeyLayout(assets, session) : (FieldPartyArea, Rectangle.Empty);
        // III: the wilderness / dungeon roster shows each member's body-area stick figure (manual: "In the Wilderness ...
        // shown in a stick figure diagram under the character's status"; Amiga roster 0x93e0 → 0x9694), not the portrait.
        DrawPartyStrip(batch, pixel, assets, session, partyArea, maxCardWidth: 52, bodyFigures: true, gap: 1,
            scroll: FieldPartyScroll);
        if (terrainKey is { Count: > 0 })
            DrawTerrainKey(batch, pixel, assets, terrainKey, keyArea);
        else
            TerrainKeyScroll.Set(0, 1); // no key shown: its scroll keys (SidebarScroll) move nothing
        var commands = FieldCommandsFor(session.Mode, timeLagCommand);
        for (var i = 0; i < commands.Length; i++)
        {
            // s193 (pad fix E30): the move row names the D-pad and RT + A (the letters' command list) while the pad is the
            // input; a pad word is no key to click
            var shown = PadWording.Help(commands[i]);
            PixelFont.Draw(batch, pixel, FieldPartyArea.X + 6, FieldCommandsTop + i * PixelFont.LineHeight, shown, BodyText);
            if (ReferenceEquals(shown, commands[i]))
                HitTokens(commands[i], FieldPartyArea.X + 6, FieldCommandsTop + i * PixelFont.LineHeight, commandList: false);
        }
    }

    /// <summary>
    /// The party strip inside <paramref name="area"/>: every member incl. a summoned elemental (slot 7), laid out by
    /// <see cref="PartyStripLayout"/> as portrait cards or, where those don't fit, one-line rows. <paramref name="shown"/>
    /// leaves members out of the row (II's combat row, <see cref="CombatRowShows"/>); a card's digit stays its party number.
    /// <paramref name="evenRow"/> spaces one row of cards as the ST's combat row (<see cref="PartyStripLayout.ComputeEvenRow"/>).
    /// The cards are as tall as the pictures and names need (<see cref="PlanPartyStrip"/>), so a caller sizes
    /// <paramref name="area"/> with <see cref="PartyStripHeight"/>.
    /// </summary>
    public static void DrawPartyStrip(
        SpriteBatch batch,
        Texture2D pixel,
        AssetCatalog assets,
        GameSession session,
        Rectangle area,
        int maxCardWidth = 56,
        bool cardDigits = false,
        Func<Character, bool>? deathMark = null,
        Func<Character, bool>? shown = null,
        bool evenRow = false,
        bool bodyFigures = false,
        int gap = PartyStripLayout.CardGap,
        ScrollBox? scroll = null,
        Action<Character, Rectangle>? overPortrait = null,
        Func<Character, Texture2D?>? portraitFor = null)
    {
        var party = session.Party.Members;
        var (layout, members, portraitHeight, _) = PlanPartyStrip(assets, session, area, maxCardWidth, shown, evenRow, bodyFigures, gap);
        if (scroll is not null)
            layout = ScrollCards(batch, pixel, assets, session, area, maxCardWidth, shown, bodyFigures, gap, scroll, layout);
        for (var i = 0; i < layout.Cards.Count; i++)
        {
            if (layout.Cards[i].IsEmpty)
                continue; // scrolled out of view
            var ch = members[i];
            var number = party.IndexOf(ch) + 1;
            if (cardDigits && number <= 9)
                Hits?.Add(new HitRegion(layout.Cards[i]) { Click = [HitTest.Digit(number)] }); // the member's number key
            var r = layout.Cards[i];
            var elemental = ReferenceEquals(ch, session.Party.Elemental);
            var hpColor = HpColor(ch);
            var hp = $"{ch.HpCur}/{ch.HpMax}";

            if (layout.Lines)
            {
                var room = PartyStripLayout.CharsFitting(r.Width - 2) - hp.Length - 1;
                PixelFont.Draw(batch, pixel, r.X, r.Y, Fit(ch.Name, Math.Max(1, room)), elemental ? ElementalEdge : Color.White);
                PixelFont.Draw(batch, pixel, r.Right - PixelFont.Measure(hp) - 2, r.Y, hp, hpColor);
                continue;
            }

            DrawPanel(batch, pixel, r, PanelBg, elemental ? ElementalEdge : PanelEdge);
            var portrait = portraitFor?.Invoke(ch) // II's pose on the row (s156)
                           ?? (elemental ? assets.PortraitElemental : assets.GetPortraitIdle((int)ch.Race));
            var px = r.X + (r.Width - PartyStripLayout.PortraitSize) / 2;
            var portraitRect = new Rectangle(px, r.Y + 2, PartyStripLayout.PortraitSize, portraitHeight);
            // II: a member dying this round: 0x18a02 first clears the portrait's rectangle (0x19b28, pen 0), then the mark
            // (s163, seen live: the remaster had drawn the mark over the portrait).
            var marked = deathMark is not null && deathMark(ch) && assets.PortraitDeathMark is not null;
            if (marked)
                Fill(batch, pixel, portraitRect, Color.Black);
            else if (ShowsBodyFigure(assets, session, ch, bodyFigures) && assets.BodyFigure is { } figure)
                DrawBodyFigure(batch, pixel, assets, figure, ch, portraitRect.Location);
            else if (portrait is not null)
                DrawPortrait(batch, portrait, portraitRect);
            else
                Fill(batch, pixel, portraitRect, elemental ? new Color(90, 50, 30) : new Color(40, 50, 70));
            // II: the ST's mark on a member dying this round, at portrait + (8,13) (0x18a02; CombatPresenter.DeathMarkMs).
            if (deathMark is not null && deathMark(ch) && assets.PortraitDeathMark is { } mark)
                batch.Draw(mark, new Rectangle(portraitRect.X + 8, portraitRect.Y + 13, mark.Width, mark.Height), Color.White);
            overPortrait?.Invoke(ch, portraitRect);

            // The whole name, on a second line when the card is too narrow for it (s99); the HP under the last line.
            var nameTop = PartyStripLayout.NameTop(r, portraitHeight);
            foreach (var name in PartyStripLayout.NameLines(ch.Name, r.Width))
            {
                PixelFont.Draw(batch, pixel, r.X + Math.Max(2, (r.Width - PixelFont.Measure(name)) / 2), nameTop, name,
                    Color.White);
                nameTop += PixelFont.LineHeight;
            }
            foreach (var part in PartyStripLayout.HpLines(hp, r.Width))
            {
                PixelFont.Draw(batch, pixel, r.X + Math.Max(2, (r.Width - PixelFont.Measure(part)) / 2), nameTop, part, hpColor);
                nameTop += PixelFont.LineHeight;
            }
        }
    }

    /// <summary>The cards <see cref="DrawPartyStrip"/> lays out, with the portrait box and name lines they were sized for.</summary>
    public sealed record PartyStripPlan(PartyCardLayout Layout, List<Character> Members, int PortraitHeight, int NameLines)
    {
        /// <summary>Bottom of the lowest card (the area's top when there are none).</summary>
        public int Bottom(Rectangle area) => Layout.Cards.Count == 0 ? area.Y : Layout.Cards.Max(c => c.Bottom);
    }

    /// <summary>
    /// The party strip's layout in <paramref name="area"/>: the portrait box as tall as the tallest picture a card shows
    /// (<see cref="StripPortraitHeight"/>), then a second name line on every card when some name does not fit its card.
    /// </summary>
    public static PartyStripPlan PlanPartyStrip(AssetCatalog assets, GameSession session, Rectangle area,
        int maxCardWidth = 56, Func<Character, bool>? shown = null, bool evenRow = false, bool bodyFigures = false,
        int gap = PartyStripLayout.CardGap)
    {
        var members = shown is null ? session.Party.Members.ToList() : session.Party.Members.Where(shown).ToList();
        var portraitHeight = StripPortraitHeight(assets, session, members, bodyFigures);
        var names = members.Select(m => m.Name).ToList();
        var hps = members.Select(m => $"{m.HpCur}/{m.HpMax}").ToList();
        PartyCardLayout Lay(int lines) => evenRow
            ? PartyStripLayout.ComputeEvenRow(members.Count, area, maxCardWidth, portraitHeight, lines)
            : PartyStripLayout.Compute(members.Count, area, maxCardWidth, portraitHeight, lines, gap);
        var nameLines = 1;
        var layout = Lay(nameLines);
        for (var needed = PartyStripLayout.NameLinesNeeded(layout, names, hps); needed > nameLines;
             needed = PartyStripLayout.NameLinesNeeded(layout, names, hps))
            layout = Lay(nameLines = needed);
        return new PartyStripPlan(layout, members, portraitHeight, nameLines);
    }

    /// <summary>
    /// The cards of a strip whose whole cards do not fit <paramref name="area"/> (III's wilderness sidebar with a summoned
    /// elemental): laid out in rows with room for the bar, the rows <paramref name="scroll"/> shows moved to the top of the
    /// area, the others empty (not drawn), and the bar drawn. A strip that fits comes back as it was.
    /// </summary>
    private static PartyCardLayout ScrollCards(SpriteBatch batch, Texture2D pixel, AssetCatalog assets, GameSession session,
        Rectangle area, int maxCardWidth, Func<Character, bool>? shown, bool bodyFigures, int gap, ScrollBox scroll,
        PartyCardLayout layout)
    {
        if (!layout.Lines && layout.Cards.All(area.Contains))
        {
            scroll.Set(0, 1);
            return layout;
        }
        var narrower = new Rectangle(area.X, area.Y, area.Width - ScrollBox.BarWidth - 1, 1000);
        var cards = PlanPartyStrip(assets, session, narrower, maxCardWidth, shown, false, bodyFigures, gap).Layout.Cards;
        var rowTops = cards.Select(c => c.Y).Distinct().Order().ToList();
        var pitch = rowTops.Count > 1 ? rowTops[1] - rowTops[0] : cards[0].Height + gap;
        var visible = Math.Max(1, (area.Height + gap) / pitch);
        scroll.Set(rowTops.Count, visible);
        HitScrollArea(scroll, area);
        var shownCards = cards.Select(c =>
        {
            var row = rowTops.IndexOf(c.Y) - scroll.Offset;
            return row < 0 || row >= visible ? Rectangle.Empty : c with { Y = area.Y + row * pitch };
        }).ToList();
        DrawScrollBar(batch, pixel, scroll, area);
        return new PartyCardLayout(false, shownCards);
    }

    /// <summary>
    /// Height the party strip's cards take in an area <paramref name="width"/> wide with room for every row it needs
    /// (the fewest rows whose cards are wide enough): what a caller sizes its strip area by.
    /// </summary>
    public static int PartyStripHeight(AssetCatalog assets, GameSession session, int width, int maxCardWidth = 56,
        Func<Character, bool>? shown = null, bool evenRow = false, bool bodyFigures = false)
    {
        var area = new Rectangle(0, 0, width, 1000);
        return PlanPartyStrip(assets, session, area, maxCardWidth, shown, evenRow, bodyFigures).Bottom(area);
    }

    private static readonly Color ElementalEdge = new(220, 140, 70);

    /// <summary>
    /// Rows of the portrait box on a party card (s99, user: make the cards taller, nothing cropped): the pack's tallest
    /// portrait (<see cref="AssetCatalog.PortraitHeight"/>; III Amiga's 32×48 full figures), or 32 for III's wilderness /
    /// dungeon cards, which show the 32-row body figure instead.
    /// </summary>
    public static int StripPortraitHeight(AssetCatalog assets, GameSession session, IEnumerable<Character> members,
        bool bodyFigures = false) =>
        members.Select(m => ShowsBodyFigure(assets, session, m, bodyFigures)
                ? PartyStripLayout.PortraitSize
                : PortraitRows(ReferenceEquals(m, session.Party.Elemental) ? assets.PortraitElemental
                    : assets.GetPortraitIdle((int)m.Race)))
            .Append(PartyStripLayout.PortraitSize)
            .Max();

    /// <summary>Rows <paramref name="portrait"/> takes on a 32-pixel-wide card (32 without a picture).</summary>
    private static int PortraitRows(Texture2D? portrait) => portrait is null
        ? PartyStripLayout.PortraitSize
        : (portrait.Height * PartyStripLayout.PortraitSize + portrait.Width - 1) / portrait.Width;

    /// <summary>III's wilderness / dungeon card shows <paramref name="m"/>'s body figure rather than a portrait.</summary>
    private static bool ShowsBodyFigure(AssetCatalog assets, GameSession session, Character m, bool bodyFigures) =>
        bodyFigures && !ReferenceEquals(m, session.Party.Elemental) && m.BodyCondition is not null && assets.BodyFigure is not null;

    /// <summary>
    /// A portrait in <paramref name="box"/>, never cut or stretched out of shape (s99). A picture of the box's aspect (I,
    /// II) fills it exactly as before; a shorter one is drawn at the box's width from its top; a taller one (III's 32×48
    /// elemental on a 32×32 body-figure card) is shrunk by the smallest whole factor that fits it whole, centred.
    /// </summary>
    public static void DrawPortrait(SpriteBatch batch, Texture2D portrait, Rectangle box) =>
        batch.Draw(portrait, PortraitTarget(portrait.Width, portrait.Height, box), Color.White);

    /// <summary>Where <see cref="DrawPortrait"/> puts a <paramref name="width"/>×<paramref name="height"/> picture in <paramref name="box"/>.</summary>
    public static Rectangle PortraitTarget(int width, int height, Rectangle box)
    {
        var rows = width * box.Height / Math.Max(1, box.Width);
        if (height == rows)
            return box;
        if (height < rows)
            return new Rectangle(box.X, box.Y, box.Width, height * box.Width / Math.Max(1, width));
        var factor = Math.Max((width + box.Width - 1) / box.Width, (height + box.Height - 1) / box.Height);
        var (w, h) = (width / factor, height / factor);
        return new Rectangle(box.X + (box.Width - w) / 2, box.Y + (box.Height - h) / 2, w, h);
    }

    /// <summary>
    /// III's body-area stick figure (Amiga <c>0x9694</c>, the pack's <see cref="BodyFigureLayout"/>) with its top-left at
    /// <paramref name="at"/>: the box in the figure's background, then each piece in its area's state; a Gone area clears
    /// its rectangle. Nothing but the box for a Dead member (the roster <c>0x93e0</c> skips the figure at status 0).
    /// </summary>
    public static void DrawBodyFigure(SpriteBatch batch, Texture2D pixel, AssetCatalog assets, BodyFigureLayout figure,
        Character member, Point at)
    {
        var background = VirtualScreen.ParseColor(figure.Background);
        Fill(batch, pixel, new Rectangle(at.X, at.Y, figure.Width, figure.Height), background);
        foreach (var step in figure.Plan(member.BodyCondition?.Areas, member.Status == CharacterStatus.Dead))
        {
            if (step.Slot is not { } slot)
                Fill(batch, pixel, new Rectangle(at.X + step.X, at.Y + step.Y, step.Width, step.Height), background);
            else if (assets.GetBodyPiece(slot) is { } tex)
                batch.Draw(tex, new Vector2(at.X + step.X, at.Y + step.Y), Color.White);
        }
    }

    /// <summary>
    /// II's combat row (ST <c>0x18176</c>, FORMAT.md §10.3): only the non-DEAD members (status 0; SLEEP stays, II has no
    /// other status), in party order. A member who died this round keeps its slot until its "DIES" sequence is over
    /// (<c>0x18a02</c>: the mark, sound 15, 500 ms, the sound's end; s136 <see cref="CombatPresenter.KeepsRowSlot"/>); then
    /// <c>0x185d6(1)</c> rebuilds the row without it. Every row build filters the same way (encounter
    /// start, victory, a resurrection in combat), so the dead stay off later fights and a resurrected member is back.
    /// </summary>
    public static bool CombatRowShows(Character member, Func<Character, bool> hasDeathMark) =>
        member.Status != CharacterStatus.Dead || hasDeathMark(member);

    /// <summary>The combat strip's <c>shown</c> filter: II drops the dead (<see cref="CombatRowShows"/>); I, III and IV keep everyone.</summary>
    public static Func<Character, bool>? CombatRowFilter(GameId game, Func<Character, bool> hasDeathMark) =>
        game == GameId.P2 ? m => CombatRowShows(m, hasDeathMark) : null;

    private static Color HpColor(Character ch) =>
        ch.HpCur <= 0 ? new Color(200, 70, 70)
        : ch.HpCur * 2 < ch.HpMax ? new Color(220, 160, 60)
        : Accent;

    public static void DrawGuild(
        SpriteBatch batch,
        Texture2D pixel,
        AssetCatalog assets,
        GameSession session)
    {
        Fill(batch, pixel, new Rectangle(0, ContentTop, 320, ContentBottom - ContentTop), new Color(22, 30, 48));

        // Soft title art behind the panel (cover1 is a valid extract; logo.png is not).
        if (assets.TitleCover is not null)
            batch.Draw(assets.TitleCover,
                new Rectangle(0, ContentTop, 320, ContentBottom - ContentTop),
                Color.White * 0.18f);

        // Two columns of three so a full party fits the panel, race and class in full (s99: no "HALFL"). s206: the columns
        // sit side by side unwrapped when their widest entries fit (the panel widens to the screen's edges if it must:
        // "ARENTINO HUMAN RANGER L10" had kept its "L10" on a line of its own); only an entry that cannot fit even then goes
        // on, never cut, on the column's next line, and the list scrolls when that runs past the panel.
        const int columnChars = 24;
        var members = session.Party.Members;
        var entries = Enumerable.Range(0, 2).Select(c => members.Skip(c * 3).Take(3)
            .Select(ch => UiText.F("ui.x_x_x_l_x", ch.Name, UiText.Name(ch.Race).ToUpperInvariant(),
                UiText.Name(ch.Class).ToUpperInvariant(), ch.Level)).ToList()).ToList();
        var layout = GuildListLayout.For(
            entries[0].Select(PixelFont.Measure).DefaultIfEmpty(0).Max(), entries[1].Select(PixelFont.Measure).DefaultIfEmpty(0).Max());
        var columns = entries.Select(col => col.SelectMany(e => layout.Wraps ? Lines(e, columnChars) : [e]).ToList()).ToList();

        DrawPanel(batch, pixel, new Rectangle(layout.PanelX, 20, layout.PanelWidth, 140), new Color(14, 20, 34, 220), PanelEdge);

        // Taller cards (III's 32×48 portraits, a name on two lines) take their extra rows from the gaps of the title
        // block above them; past 16 rows the title and its subtitle share one line.
        var cardHeight = Math.Max(PartyStripLayout.CardHeight, PartyStripHeight(assets, session, 280));
        var extra = cardHeight - PartyStripLayout.CardHeight;
        // s187 (pad fix A5): the Title screen's Guild takes only A / D (no N, no G).
        var hint = session.Mode == GameMode.Title ? UiText.S("ui.title_guild_hint")
            : session.Roster.Occupied.Any() ? UiText.S("ui.a_add_to_party_n_new") : UiText.S("ui.n_creates_a_new_member");
        int hintY, stripTop;
        if (extra <= 16)
        {
            var lift = extra;
            PixelFont.Draw(batch, pixel, 100, 28 - lift / 4, UiText.S("ui.phantasie"), TitleGold, scale: 2);
            PixelFont.Draw(batch, pixel, 86, 48 - lift / 2, UiText.S("ui.adventurers_guild"), TitleGold);
            hintY = 60 - lift * 5 / 8;
            stripTop = 74 - lift;
        }
        else
        {
            var title = UiText.S("ui.phantasie");
            var subtitle = UiText.S("ui.adventurers_guild");
            var x = 160 - (PixelFont.Measure(title) * 2 + 12 + PixelFont.Measure(subtitle)) / 2;
            PixelFont.Draw(batch, pixel, x, 22, title, TitleGold, scale: 2);
            PixelFont.Draw(batch, pixel, x + PixelFont.Measure(title) * 2 + 12, 29, subtitle, TitleGold);
            hintY = 39;
            stripTop = 48;
        }
        PixelFont.Draw(batch, pixel, 160 - PixelFont.Measure(hint) / 2, hintY, hint, DimText);
        HitTokens(hint, 160 - PixelFont.Measure(hint) / 2, hintY, commandList: true);

        DrawPartyStrip(batch, pixel, assets, session, new Rectangle(20, stripTop, 280, cardHeight + 2));

        var listTop = stripTop + cardHeight + 5;
        var rows = (159 - listTop - PixelFont.GlyphHeight) / PixelFont.LineHeight + 1;
        var list = new Rectangle(layout.ListLeft - 2, listTop - 1, layout.ListRight - layout.ListLeft + 6, rows * PixelFont.LineHeight);
        GuildListScroll.Set(columns.Max(c => c.Count), rows);
        GuildListScroll.TopFor(members.Count);
        HitScrollArea(GuildListScroll, list);
        for (var c = 0; c < columns.Count; c++)
            for (var r = 0; r < rows && GuildListScroll.Offset + r < columns[c].Count; r++)
                PixelFont.Draw(batch, pixel, c == 0 ? layout.ListLeft : layout.SecondX, listTop + r * PixelFont.LineHeight,
                    columns[c][GuildListScroll.Offset + r], BodyText);
        DrawScrollBar(batch, pixel, GuildListScroll, list);
    }

    /// <summary>
    /// Where the Guild screen's member list goes (s206): the usual panel (x 8, 304 wide; text 16…306, the second column at
    /// 164) when both columns' widest entries fit it unwrapped, the second column moved right of the first's widest entry
    /// if need be; else the panel widened to x 2…318 (text 8…312); else the usual panel with entries wrapped at the
    /// column (<see cref="Wraps"/>), as before s206. Text right edges stay clear of the scroll bar.
    /// </summary>
    public readonly record struct GuildListLayout(int PanelX, int PanelWidth, int ListLeft, int ListRight, int SecondX, bool Wraps)
    {
        private const int Gap = 9; // about one and a half spaces, so the columns read apart

        public static GuildListLayout For(int leftWidest, int rightWidest) =>
            Fit(8, 304, 16, 306, leftWidest, rightWidest) ?? Fit(2, 316, 8, 312, leftWidest, rightWidest)
            ?? new GuildListLayout(8, 304, 16, 306, 16 + 148, Wraps: true);

        private static GuildListLayout? Fit(int panelX, int panelWidth, int left, int right, int leftWidest, int rightWidest)
        {
            var second = Math.Max(left + 148, left + leftWidest + Gap);
            return second + rightWidest <= right ? new GuildListLayout(panelX, panelWidth, left, right, second, Wraps: false) : null;
        }
    }

    /// <summary>The Guild screen's member list (<see cref="DrawGuild"/>), when its entries need more lines than fit.</summary>
    public static ScrollBox GuildListScroll { get; } = new();

    // Town screen (s98, user: never crop scenes): the whole town picture 1:1 on the whole screen, as every original shows
    // it (DOS s49 capture: bar, picture, name; the ST's PELNOR.PIC with its GEM bar and name scroll). The remaster's bar
    // goes over the picture's own bar rows and the name where the pack's original puts it (TownScreenLayout); the status
    // and help lines are not drawn, and a message shows on a one-line band for a few seconds (DrawTownHud).
    /// <summary>Source rows of the town picture the dimmed Armory backdrop shows (decoration behind the panels).</summary>
    private const int TownPictureSourceTop = 11;
    private static readonly Color DosWhite = new(255, 255, 255);
    private static readonly Color DosBlack = new(0, 0, 0);

    /// <summary>
    /// The town menu, full-screen: the town picture 1:1, the DOS bar with its highlight over the picture's top
    /// <see cref="TownScreenLayout.BarRows"/> rows, the name, and an open Bank / Option / Inn list under the bar.
    /// </summary>
    public static void DrawTownMenu(
        SpriteBatch batch,
        Texture2D pixel,
        AssetCatalog assets,
        GameSession session,
        TownMenu menu,
        string? openDoor = null,
        bool originalFont = false,
        bool dosBar = true)
    {
        var layout = assets.TownScreen;
        const int barTop = 0;
        var barHeight = Math.Max(PixelFont.LineHeight, layout.BarRows);
        var pictureTop = barTop + barHeight;
        Fill(batch, pixel, new Rectangle(0, 0, 320, ScreenBottom), DosBlack);
        if (assets.TownBackdropFor(session.CurrentTownId) is { } picture)
            batch.Draw(picture, new Rectangle(0, 0, picture.Width, picture.Height), Color.White);
        // III: the building being entered shows its open door (D/w3 piece over the same street square, 0xacc8).
        if (openDoor is not null && assets.TownDoorsFor(session.CurrentTownId) is { } doors && layout.Doors?.TryGetValue(openDoor, out var at) == true)
        {
            var square = new Rectangle(at.X, at.Y, layout.DoorSize, layout.DoorSize);
            batch.Draw(doors, square, square, Color.White);
        }
        // s156: III Amiga's own bar (File / Guild / Options, DrawTownMenuBar) goes over the picture's top rows instead.
        if (dosBar)
            Fill(batch, pixel, new Rectangle(0, barTop, 320, barHeight), DosBlack); // the picture's own bar rows
        var labelY = barTop + (barHeight - PixelFont.GlyphHeight) / 2;

        // The bar spreads the seven labels across the width; the highlight is inverse video.
        IReadOnlyList<string> labels = dosBar ? TownMenu.BarText : [];
        var textWidth = labels.Sum(PixelFont.Measure);
        var gap = (320 - textWidth) / (labels.Count + 1);
        var x = gap;
        var subX = 0;
        for (var i = 0; i < labels.Count; i++)
        {
            var w = PixelFont.Measure(labels[i]);
            var selected = i == menu.BarCursor;
            var label = new Rectangle(x - 2, labelY - 1, w + 3, PixelFont.GlyphHeight + 2);
            if (menu.Place == TownPlace.Menu)
                HitCursorRow(label, menu.BarCursor, i, Keys.Left, Keys.Right);
            else
                Hits?.Add(new HitRegion(label) { Click = [Keys.Back] }); // a list is open: the bar closes it first
            if (selected)
            {
                Fill(batch, pixel, label, DosWhite);
                subX = x - 2;
            }
            PixelFont.Draw(batch, pixel, x, labelY, labels[i], selected ? DosBlack : DosWhite);
            x += w + gap;
        }

        var name = session.CurrentTown?.Name ?? UiText.F("ui.town_x", session.CurrentTownId);
        // III Amiga: centred by 8-px cells (an odd gap half a cell left), black in the dim towns 16–17 (0x7e54).
        var nameColour = assets.IsDimTown(session.CurrentTownId) && layout.DimNameColour is { } dimInk
            ? dimInk
            : layout.NameColourFor(session.CurrentTownId, session.FreshStart); // II ST: per town (pen = town number)
        using (PixelFont.With(originalFont ? TownTextFont(layout) : null)) // I Amiga: z6font (h80:0x818, s135)
            PixelFont.Draw(batch, pixel, TownNameLeft(layout, name, session.FreshStart), layout.NameY,
                name, VirtualScreen.ParseColor(nameColour));

        var sub = openDoor is null ? menu.SubLabels : []; // the Bank list opens once the door has shown
        if (Hits is { } hits)
            hits.Wheel = sub.Count > 0 ? (Keys.Up, Keys.Down) : (Keys.Left, Keys.Right);
        if (sub.Count > 0)
        {
            var width = sub.Max(PixelFont.Measure) + 8;
            var left = Math.Min(subX, 320 - width - 2);
            var box = new Rectangle(left, pictureTop, width, sub.Count * PixelFont.LineHeight + 4);
            Fill(batch, pixel, box, DosBlack);
            for (var i = 0; i < sub.Count; i++)
            {
                var y = box.Y + 2 + i * PixelFont.LineHeight;
                var selected = i == menu.SubCursor;
                HitCursorRow(new Rectangle(box.X + 2, y - 1, width - 4, PixelFont.LineHeight), menu.SubCursor, i, Keys.Up, Keys.Down);
                if (selected)
                    Fill(batch, pixel, new Rectangle(box.X + 2, y - 1, width - 4, PixelFont.LineHeight), DosWhite);
                PixelFont.Draw(batch, pixel, box.X + 4, y, sub[i], selected ? DosBlack : DosWhite);
            }
        }
    }

    /// <summary>
    /// s154: I Amiga's "LEAVING %s" (h29 <c>+0x168</c>): the town picture, the line at the name's row centred on 320 in the
    /// pack's town font (h80 <c>+0x818</c>: the line cleared first, so no name; z6font: x 106 for PELNOR), in
    /// <paramref name="ink"/>; a click goes nowhere.
    /// </summary>
    public static void DrawTownLeaving(SpriteBatch batch, Texture2D pixel, AssetCatalog assets, TownScreenLayout layout, int townId,
        string line, Color ink)
    {
        Fill(batch, pixel, new Rectangle(0, 0, 320, ScreenBottom), DosBlack);
        if (assets.TownBackdropFor(townId) is { } picture)
            batch.Draw(picture, new Rectangle(0, 0, picture.Width, picture.Height), Color.White);
        using var font = PixelFont.With(TownTextFont(layout));
        Fill(batch, pixel, new Rectangle(0, layout.NameY, 320, PixelFont.GlyphHeight), DosBlack);
        PixelFont.Draw(batch, pixel, TownLeavingLeft(line), layout.NameY, line, ink);
        Hits?.Add(new HitRegion(new Rectangle(0, 0, 320, ScreenBottom), new BackdropTag()));
    }

    /// <summary>s154: "LEAVING %s" with the name in the pack's field (I Amiga: the record's 10 characters, blanks counted).</summary>
    public static string TownLeavingLine(TownScreenLayout layout, string name) =>
        UiText.F("p1town.leaving_x", layout.FieldName(name));

    /// <summary>s154: the leaving line's left edge in the current font: centred on 320 (h80 <c>+0x892</c>).</summary>
    public static int TownLeavingLeft(string line) => Math.Max(0, (320 - PixelFont.Measure(line)) / 2);

    /// <summary>The pack's font for the town line (<see cref="TownScreenLayout.Font"/>), or null for the pack's own.</summary>
    public static IFontSource? TownTextFont(TownScreenLayout layout) => FontSources.Named(layout.Font, layout.FontFile);

    /// <summary>How long a town message stays on its band (<see cref="DrawTownHud"/>).</summary>
    public const double TownMessageSeconds = 4.0;

    /// <summary>
    /// Left edge of the town name: centred on the pack's name x, padded to its name field first (I Amiga: 10 characters,
    /// so every name starts at 130), moved by III's cell centring (<see cref="TownScreenLayout.NameShift"/>).
    /// </summary>
    public static int TownNameLeft(TownScreenLayout layout, string name, bool freshStart) =>
        Math.Clamp(layout.NameXFor(freshStart) - PixelFont.Measure(layout.FieldName(name)) / 2 + layout.NameShift(name.Length), 0, 320);

    /// <summary>
    /// Top of the town screen's one-line band: the pack's <see cref="TownScreenLayout.MessageY"/> (the
    /// picture's own blank rows); without one the bottom line, or above the name when the name is down there.
    /// </summary>
    public static int TownBandTop(TownScreenLayout layout) =>
        layout.MessageY ??
        (layout.NameY >= ScreenBottom - 2 * BandHeight ? Math.Max(0, layout.NameY - BandHeight - 2) : ScreenBottom - BandHeight);

    /// <summary>The town band's one-line height: down to the screen's bottom (at most <see cref="BandHeight"/>).</summary>
    public static int TownBandHeight(TownScreenLayout layout) => Math.Min(BandHeight, ScreenBottom - TownBandTop(layout));

    private const int BandHeight = 10;

    /// <summary>
    /// The town screen's only chrome besides the bar (s98): no status line and no help line, so the picture shows whole.
    /// <paramref name="message"/> (when not null) shows on a one-line band for <see cref="TownMessageSeconds"/> after it
    /// changes (Game1). With <paramref name="help"/> (the mouse rests on the band's rows or below) the band shows the key
    /// help instead, its key tokens clickable as on the help line.
    /// </summary>
    /// <param name="gold">P3 QoL: the session whose Guild treasury gold line the band shows (null: none). On the pack's
    /// blank rows it sits at the band row's left, the text keeping to the columns right of it; over the picture (no blank
    /// rows: III's DOS look) it gets a small veil of its own at the band row's right, the text keeping left of it.</param>
    public static void DrawTownHud(SpriteBatch batch, Texture2D pixel, TownScreenLayout layout, string? message, string? help,
        bool originalFont = false, GameSession? gold = null)
    {
        var text = help ?? message;
        string? goldText;
        int goldW;
        using (PixelFont.With(Remaster5x7Font))
        {
            goldText = gold is null ? null : TreasuryGoldFitting(gold, 156);
            goldW = goldText is null ? 0 : PixelFont.Measure(goldText);
        }
        var onBlankRows = layout.MessageY is not null;
        var bandRow = TownBandTop(layout) + (onBlankRows ? 1 : 2);
        if (goldText is not null)
        {
            using var remaster = PixelFont.With(Remaster5x7Font);
            var gx = onBlankRows ? 4 : 316 - goldW;
            if (!onBlankRows)
                Fill(batch, pixel, new Rectangle(gx - 4, TownBandTop(layout), goldW + 8, TownBandHeight(layout)), new Color(6, 8, 14, 230));
            PixelFont.Draw(batch, pixel, gx, bandRow, goldText, TitleGold);
        }
        if (string.IsNullOrEmpty(text))
            return;
        // The text's columns: the band row less the gold line's place (and a gap) when it shows.
        var left = goldText is not null && onBlankRows ? 4 + goldW + 6 : 4;
        var right = goldText is not null && !onBlankRows ? 316 - goldW - 10 : 316;
        // A message (not the key help) in the pack's town font behind AMIGA SCREENS: I Amiga's h80:0x818 (s135).
        using var font = PixelFont.With(originalFont && help is null ? TownTextFont(layout) : null);
        // A text longer than the band's line goes on, whole, on lines above it (s99), the band growing upward. On a
        // pack's own blank rows (MessageY) the band is the picture's black and covers no art for one line (the DOS name
        // line gives way to the message while it shows); elsewhere it is a dark veil over the picture's bottom.
        var lines = Lines(text, goldText is null ? 52 : Math.Max(1, (right - left) / PixelFont.Advance));
        var top = TownBandTop(layout) - (lines.Count - 1) * PixelFont.LineHeight;
        Fill(batch, pixel, new Rectangle(left - 4, top, right + 4 - (left - 4), TownBandHeight(layout) + (lines.Count - 1) * PixelFont.LineHeight),
            onBlankRows ? DosBlack : new Color(6, 8, 14, 230));
        for (var i = 0; i < lines.Count; i++)
        {
            var y = top + (onBlankRows ? 1 : 2) + i * PixelFont.LineHeight;
            // On the pack's own line a message is centred, as the ST (0x4082) and Amiga I (h80:0x892) print it.
            var x = onBlankRows && help is null ? Math.Max(left, (320 - PixelFont.Measure(lines[i])) / 2) : left;
            PixelFont.Draw(batch, pixel, x, y, lines[i], help is not null ? DimText : BodyText);
            if (help is not null && !PadWording.Active) // s183: the pad's words are no keys to click
                HitTokens(lines[i], x, y, commandList: true, maxChars: 52);
        }
    }

    private static IFontSource Remaster5x7Font => PixelFont.Remaster5x7;

    /// <summary>Inside the Armory (town bar → Armory): the stock list, the shopper and the party.</summary>
    public static void DrawArmory(
        SpriteBatch batch,
        Texture2D pixel,
        AssetCatalog assets,
        GameSession session,
        IReadOnlyList<(int Id, string Name, int Qty, int Price)> stock,
        int cursor,
        int shopper)
    {
        if (assets.TownBackdropFor(session.CurrentTownId) is { } shopBackdrop)
            batch.Draw(shopBackdrop,
                new Rectangle(0, ContentTop, 320, ContentBottom - ContentTop),
                new Rectangle(0, TownPictureSourceTop, 320, ContentBottom - ContentTop),
                Color.White * 0.40f);
        else
            Fill(batch, pixel, new Rectangle(0, ContentTop, 320, ContentBottom - ContentTop), new Color(36, 60, 44));

        // s99 (user: never lose content to fit): the whole party as full portrait cards across the top (I had fallen back
        // to one-line rows from three members on), the stock in two columns under it, in a box that scrolls when it
        // has more rows than fit. The key help the side panel repeated is on the help line.
        const int maxCard = 52;
        var cardHeight = Math.Max(PartyStripLayout.CardHeight, PartyStripHeight(assets, session, 304, maxCard));
        var partyPanel = new Rectangle(4, 16, 312, cardHeight + 6);
        DrawPanel(batch, pixel, partyPanel, PanelBg, PanelEdge);
        DrawPartyStrip(batch, pixel, assets, session, new Rectangle(8, 19, 304, cardHeight), maxCardWidth: maxCard,
            cardDigits: true);

        var stockPanel = new Rectangle(4, partyPanel.Bottom + 2, 312, ContentBottom - partyPanel.Bottom - 2);
        DrawPanel(batch, pixel, stockPanel, PanelBg, PanelEdge);
        var town = session.CurrentTown;
        var headerY = stockPanel.Y + 4;
        PixelFont.Draw(batch, pixel, 10, headerY, UiText.F("ui.x_armory", town?.Name ?? UiText.F("ui.town_x", session.CurrentTownId)), TitleGold);
        var shopperName = session.Party.Members.Count > 0
            ? session.Party.Members[Math.Clamp(shopper, 0, session.Party.Members.Count - 1)].Name : "-";
        var gold = UiText.F("ui.gold_x_shopper_x", ShownGold(session), shopperName);
        PixelFont.Draw(batch, pixel, stockPanel.Right - 6 - PixelFont.Measure(gold), headerY, gold, DimText);

        var rowsTop = headerY + 10;
        if (stock.Count == 0)
        {
            PixelFont.Draw(batch, pixel, 10, rowsTop, UiText.S("ui.no_stock"), DimText);
            return;
        }
        // s174 (D3-073, the manual): III's list adds the shopper's point column, names padded so it lines up.
        var buyer = session.Party.Members.Count > 0
            ? session.Party.Members[Math.Clamp(shopper, 0, session.Party.Members.Count - 1)] : null;
        var points = stock.Select(s => buyer is null ? null : session.ArmoryPointsText(buyer, s.Id)).ToList();
        var nameWidth = stock.Max(s => s.Name.Length);
        var pointsWidth = points.Max(p => p?.Length ?? 0);
        string Row(int i) => points[i] is { } pts
            ? UiText.F("ui.x_x_x_x_x_x_g", i == cursor ? ">" : " ", stock[i].Name.PadRight(nameWidth), pts.PadLeft(pointsWidth),
                stock[i].Qty, stock[i].Price)
            : UiText.F("ui.x_x_x_x_x_g", i == cursor ? ">" : " ", stock[i].Name, stock[i].Qty, stock[i].Price);
        // Two columns (filled top to bottom) when every row fits half the panel, else one.
        var columnWidth = (stockPanel.Width - 12) / 2;
        var columns = Enumerable.Range(0, stock.Count).All(i => PixelFont.Measure(Row(i)) <= columnWidth - 6) ? 2 : 1;
        if (columns == 1)
            columnWidth = stockPanel.Width - 12;
        var visible = (stockPanel.Bottom - 3 - rowsTop - PixelFont.GlyphHeight) / PixelFont.LineHeight + 1;
        var total = (stock.Count + columns - 1) / columns; // rows of the list
        var box = ArmoryScroll;
        box.Set(total, visible);
        box.Follow(cursor % total);
        var area = new Rectangle(8, rowsTop - 1, stockPanel.Width - 8, visible * PixelFont.LineHeight);
        HitScrollArea(box, area);
        for (var c = 0; c < columns; c++)
            for (var r = 0; r < visible && box.Offset + r < total; r++)
            {
                var i = c * total + box.Offset + r;
                if (i >= stock.Count)
                    break;
                var x = 10 + c * columnWidth;
                var y = rowsTop + r * PixelFont.LineHeight;
                PixelFont.Draw(batch, pixel, x, y, Row(i), i == cursor ? Color.Yellow : BodyText);
                Hits?.Add(new Rectangle(x - 2, y - 1, columnWidth - 4, PixelFont.LineHeight), new ArmoryRowTag(i));
            }
        DrawScrollBar(batch, pixel, box, area);
        if (Hits is { } hits)
            hits.Wheel = (Keys.Up, Keys.Down);
    }

    /// <summary>The Armory's stock list (<see cref="DrawArmory"/>), when it has more rows than fit.</summary>
    public static ScrollBox ArmoryScroll { get; } = new();

    public static void DrawCombat(
        SpriteBatch batch,
        Texture2D pixel,
        AssetCatalog assets,
        GameSession session,
        ClientSettings settings,
        CombatPresenter presenter,
        GameDataRepository data,
        string? help = null)
    {
        DrawCombatScreen(batch, pixel, assets, session, settings, presenter, data, help);
        if (presenter.P2Run?.ActiveBox is { } box && presenter.P2Lines?.Invoke()?.EndBoxes is { } boxes)
            DrawP2EndBox(batch, pixel, boxes, box.Rows, FontSources.ForRole(assets.Pack, P2EndBoxFontRole)); // over everything, as the ST's objc_draw
        // s156: a rock's miss (0x18c6c): the rock sprite over everything; s163: the ST's screen spot moved onto the
        // remaster's monster area (the rank box) by the pack's rock_miss_area.
        if (presenter.P2Run?.Sparkle is { Target: null } && presenter.P2Lines?.Invoke()?.Sparkle is { } miss
            && assets.Get(miss.Rock) is { } rock)
        {
            var area = LastRankBox;
            var (x, y) = miss.RockMissOn(area.X, area.Y, area.Width, area.Height, rock.Width, rock.Height);
            batch.Draw(rock, new Rectangle(x, y, rock.Width, rock.Height), Color.White);
        }
    }

    /// <summary>s163: the combat screen's monster area (the rank box) as last drawn, for the rock's miss.</summary>
    public static Rectangle LastRankBox { get; private set; } = new(8, 92, 146, 80);

    /// <summary>
    /// s156, II ST's pose (<c>0x186de</c> / <c>0x188fc</c>, FORMAT.md 7.3): the attacking picture <paramref name="attacking"/>,
    /// or its silhouette in the pack's <see cref="StCombatLines.SilhouettePens"/> of the combat palette.
    /// </summary>
    private static Texture2D? P2PoseArt(AssetCatalog assets, CombatPresenter presenter, string attacking, bool silhouette)
    {
        if (!silhouette)
            return assets.Get(attacking);
        if (presenter.P2Lines?.Invoke()?.SilhouettePens is not { } pens || assets.Pack is not { } pack
            || !pack.Palettes.TryGetValue("combat", out var combat) || combat.Count <= Math.Max(pens[0], pens[1]))
            return assets.Get(attacking);
        return assets.GetSilhouette(attacking, VirtualScreen.ParseColor(combat[0]), VirtualScreen.ParseColor(combat[pens[0]]),
            VirtualScreen.ParseColor(combat[pens[1]]));
    }

    /// <summary>II's member pose on the row now (s156), or null (the idle portrait).</summary>
    private static Texture2D? P2MemberPose(AssetCatalog assets, CombatPresenter presenter, Character member,
        GameSession session) =>
        !ReferenceEquals(member, session.Party.Elemental) // the elemental keeps its own picture
        && presenter.P2Run?.Pose is { Member: { } posing } pose && ReferenceEquals(posing, member)
            ? P2PoseArt(assets, presenter, PictureNames.PortraitAttacking((int)member.Race), pose.Silhouette)
            : null;

    /// <summary>
    /// II ST's end-of-fight box (s138, FORMAT.md 7.3a; OUT.RSC tree 40 / 41 at (12, 75) on the 320×200 screen): the fill
    /// with its border drawn inward, each row's characters in the ST's 8-px cells from <see cref="StEndBoxes.Layout"/>
    /// (the remaster's glyphs centred in each cell: TOS's 8×8 font is not on the disks `[~]`).
    /// </summary>
    /// <remarks>s165: <paramref name="palette"/> colours the box from the registers of the palette the screen shows (field
    /// Resurrection's box over the map, <see cref="P2FieldBox"/>); null = the pack's own (combat) colours.</remarks>
    public static void DrawP2EndBox(SpriteBatch batch, Texture2D pixel, StEndBoxes boxes, IReadOnlyList<string> rows,
        IFontSource? font = null, IReadOnlyList<string>? palette = null)
    {
        var layout = boxes.Layout(rows);
        var (border, fill, textColour) = boxes.Colours(palette);
        var rect = new Rectangle(layout.X, layout.Y, layout.Width, layout.Height);
        Fill(batch, pixel, rect, VirtualScreen.ParseColor(border));
        Fill(batch, pixel, new Rectangle(rect.X + boxes.Border, rect.Y + boxes.Border, rect.Width - 2 * boxes.Border,
            rect.Height - 2 * boxes.Border), VirtualScreen.ParseColor(fill));
        var ink = VirtualScreen.ParseColor(textColour);
        foreach (var (x, y, text) in layout.Rows)
            DrawCells(batch, pixel, boxes.Cell, x, y, text, ink, font);
    }

    /// <summary>s160: the pack's font role of II ST's end-of-fight boxes (OUT.RSC trees 40 / 41: G_TEXT, font 3 = the
    /// system 8×8; FORMAT.md 7.3a): EmuTOS's 8×8.</summary>
    public const string P2EndBoxFontRole = "p2.end_box";

    /// <summary>
    /// s158: II ST's alert box (<see cref="P2AlertBox"/>, AES <c>form_alert</c>; FORMAT.md 7.4) as <see cref="StAlertLook"/>
    /// lays it out on the 320×200 screen: the outline, the box with its one-pixel border, the lines and the button(s) in
    /// <paramref name="fill"/> / <paramref name="ink"/> (VDI 0 / 1 of the palette the mode shows); the text in
    /// <paramref name="font"/> (s160: the pack's <c>p2.alert</c> role, EmuTOS's 8×8 system font; TOS's own is not on the
    /// disks), one glyph per 8-px cell. The button is drawn selected while <paramref name="held"/> is over it. A click on
    /// a button is Return; the rest of the screen takes no click.
    /// </summary>
    public static void DrawStAlert(SpriteBatch batch, Texture2D pixel, StAlertLook look, StAlert alert, Color fill,
        Color ink, Point? held = null, IFontSource? font = null)
    {
        var layout = look.Layout(alert);
        static Rectangle R((int X, int Y, int W, int H) r) => new(r.X, r.Y, r.W, r.H);
        var outline = R(layout.Outline);
        var box = R(layout.Box);
        Fill(batch, pixel, outline, fill);
        Outline(batch, pixel, outline, ink); // OUTLINED: a line 3 px outside the box
        Outline(batch, pixel, box, ink); // the root's own border, 1 px inward
        foreach (var (x, y, text) in layout.Lines)
            DrawCells(batch, pixel, look.Cell, x, y, text, ink, font);
        Hits?.Add(new HitRegion(new Rectangle(0, 0, 320, ScreenBottom), new BackdropTag()));
        foreach (var (x, y, w, h, text) in layout.Buttons)
        {
            var inner = new Rectangle(x, y, w, h);
            var frame = new Rectangle(x - look.ButtonBorder, y - look.ButtonBorder, w + 2 * look.ButtonBorder,
                h + 2 * look.ButtonBorder);
            var down = held is { } p && frame.Contains(p);
            Fill(batch, pixel, frame, ink);
            Fill(batch, pixel, inner, down ? ink : fill);
            var start = x + Math.Max(0, (w - look.Cell * text.Length) / 2);
            DrawCells(batch, pixel, look.Cell, start, y, text, down ? fill : ink, font);
            Hits?.Add(new HitRegion(frame) { Click = [Keys.Enter] });
        }
    }

    /// <summary>s160: the pack's font role of II ST's alert boxes (<see cref="DrawStAlert"/>): EmuTOS's 8×8.</summary>
    public const string P2AlertFontRole = "p2.alert";

    /// <summary>
    /// One glyph of <paramref name="font"/> (null = the current font) centred in each <paramref name="cell"/>-px cell of a
    /// text from (x, y): an 8×8 font fills its cells from their top-left as GEM draws it (the EmuTOS shots: UNUSABLE's
    /// top row 2 cells under the box's), the remaster's 5×7 sits in the middle.
    /// </summary>
    public static void DrawCells(SpriteBatch batch, Texture2D pixel, int cell, int x, int y, string text, Color ink,
        IFontSource? font = null)
    {
        using var scope = PixelFont.With(font);
        var dy = Math.Max(0, (cell - PixelFont.Font.Height) / 2);
        for (var i = 0; i < text.Length; i++)
        {
            var glyph = text[i].ToString();
            if (glyph == " ")
                continue;
            var dx = Math.Max(0, (cell - PixelFont.Measure(glyph)) / 2);
            PixelFont.Draw(batch, pixel, x + cell * i + dx, y + dy, glyph, ink);
        }
    }

    private static void DrawCombatScreen(
        SpriteBatch batch,
        Texture2D pixel,
        AssetCatalog assets,
        GameSession session,
        ClientSettings settings,
        CombatPresenter presenter,
        GameDataRepository data,
        string? help)
    {
        Fill(batch, pixel, new Rectangle(0, 0, 320, ScreenBottom), new Color(42, 18, 18));
        // Taller cards (III's 32×48 portraits, a name on two lines, s99): the title line moves up, the panel down to the
        // content's bottom, and the rank and menu rows close up a pixel each. What still does not fit scrolls: the log
        // (newest at the bottom) and the menu on the right.
        var shown = CombatRowFilter(session.Game, presenter.KeepsRowSlot);
        var evenRow = session.Game == GameId.P2; // the ST spreads the rebuilt row over the screen (0x181ca)
        var cardHeight = Math.Max(PartyStripLayout.CardHeight, PartyStripHeight(assets, session, 304, 52, shown, evenRow));
        var tall = cardHeight > PartyStripLayout.CardHeight;
        var panel = new Rectangle(4, 1, 312, ScreenBottom - 2);
        // Complete monster pictures need the upper part of the panel. Keep the party cards together below them;
        // the orders and log share the right column, leaving the left column's full height for rank blocks.
        var strip = new Rectangle(8, panel.Bottom - 2 - cardHeight, 304, cardHeight);
        const int top = 4;
        const int titleY = top;
        var menuTop = top + 28;
        DrawPanel(batch, pixel, panel, new Color(24, 10, 10, 230), new Color(160, 80, 80));

        // s136: II's round is paced by its Speed (COMBAT SPEED), not I's T)IME LAG.
        var heading = presenter.P2Lines?.Invoke() is not null
            ? UiText.F("ui.combat_speed_x", presenter.FieldSpeed switch
            {
                AmigaCombatSpeed.Fast => UiText.S("options.combat_speed.fast"),
                AmigaCombatSpeed.Slow => UiText.S("options.combat_speed.slow"),
                _ => UiText.S("options.combat_speed.normal"),
            })
            : UiText.F("ui.combat_time_lag_x", settings.TimeLag);
        var rx = tall ? 166 : 156;
        PixelFont.Draw(batch, pixel, rx, titleY, heading, TitleGold);
        PixelFont.Draw(batch, pixel, rx, titleY + 8, PadWording.Help(UiText.S("ui.shift_skip")), DimText);

        // s110 (III Amiga): a spell flash on a member is drawn over its portrait after the whole strip, so no later card
        // covers the part that runs past the portrait (the Amiga blits it over the screen).
        var flash = presenter.FlashShown;
        Point? flashAt = null;
        DrawPartyStrip(batch, pixel, assets, session, strip, maxCardWidth: 52,
            cardDigits: presenter.AsksHealTarget, deathMark: presenter.HasDeathMark, shown: shown, evenRow: evenRow,
            overPortrait: (ch, r) =>
            {
                if (flash is { } f && ReferenceEquals(f.Flash.Member, ch))
                    flashAt = r.Location;
            },
            portraitFor: presenter.P2Run is null ? null : ch => P2MemberPose(assets, presenter, ch, session));
        if (flash is { } onMember && flashAt is { } memberAt && presenter.FlashRun is { } memberRun)
            DrawCombatFlash(batch, assets, memberRun.Layout, onMember, memberAt);

        // Left column: the rank list "RANK n : k NAME(S)", each rank with its monster's picture at its own size (s99, user:
        // no art squeezed) and its line beside it, wrapped; whole picture blocks scroll by the wheel, bar or Home / End.
        const int logRows = 2;
        var rankBox = new Rectangle(8, top, rx - 10, strip.Top - top - 2);
        LastRankBox = rankBox;
        if (session.Combat is not null || presenter.P2RankRows is not null)
            DrawRankBox(batch, pixel, assets, session.Combat, presenter, rankBox);

        // The log: every line whole (wrapped to the column), the newest at the bottom; it scrolls back (PgUp / PgDn, the
        // wheel, the bar) when there are more lines than room.
        var logChars = (panel.Right - rx - 10) / PixelFont.Advance;
        var helpLines = Lines(PadWording.Help(help ?? ""), logChars);
        var helpTop = strip.Top - helpLines.Count * PixelFont.LineHeight - 2;
        for (var i = 0; i < helpLines.Count; i++)
            PixelFont.Draw(batch, pixel, rx, helpTop + i * PixelFont.LineHeight, helpLines[i], DimText);
        var logTop = helpTop - logRows * PixelFont.LineHeight - 2;
        var logLines = presenter.Log.SelectMany(l => Lines(l, logChars)).ToList();
        var logBox = presenter.LogScroll;
        var following = logBox.Offset >= logBox.MaxOffset;
        logBox.Set(logLines.Count, logRows);
        if (following)
            logBox.End();
        var logArea = new Rectangle(rx, logTop - 1, panel.Right - rx - 4, logRows * PixelFont.LineHeight);
        HitScrollArea(logBox, logArea);
        for (var i = 0; i < logRows && logBox.Offset + i < logLines.Count; i++)
            PixelFont.Draw(batch, pixel, rx + 2, logTop + i * PixelFont.LineHeight, logLines[logBox.Offset + i], BodyText);
        DrawScrollBar(batch, pixel, logBox, logArea);

        // Right column: PARTY OPTIONS menu, the spell list or the orders list, in a box that scrolls when its rows run
        // past the panel (the highlighted row always in view).
        var menuChars = (panel.Right - 4 - rx) / PixelFont.Advance;
        var menuRows = Math.Max(1, (logTop - 2 - menuTop - PixelFont.GlyphHeight) / 8 + 1);
        var menuArea = new Rectangle(rx - 1, menuTop - 1, 314 - rx, menuRows * 8);
        var menuBox = presenter.MenuScroll;
        string? title = null;
        var rows = new List<(string Text, Color Color, Action<Rectangle>? Hit)>();
        var cursorRow = -1;
        // The old bottom HUD carried the current order question. Keep it in the new right column too: the orders
        // table alone does not tell the player that a rank, spell or healing target is being requested.
        if (presenter.AwaitingOrders)
            foreach (var part in Lines(presenter.LastLine, menuChars))
                rows.Add((part, TitleGold, null));
        if (presenter.Phase == CombatPhase.PartyOptions)
        {
            title = UiText.S("ui.party_options");
            var menu = CombatText.PartyOptionMenu;
            for (var i = 0; i < menu.Count; i++)
            {
                var on = i == presenter.OptionCursor;
                var index = i;
                if (on)
                    cursorRow = rows.Count;
                foreach (var part in Lines((on ? "> " : "  ") + menu[i].Label, menuChars))
                    rows.Add((part, on ? Color.Yellow : BodyText,
                        r => HitCursorRow(r, presenter.OptionCursor, index, Keys.Up, Keys.Down)));
            }
            if (Hits is { } hits)
                hits.Wheel = (Keys.Up, Keys.Down);
        }
        else if (presenter.SpellChoices(session) is { Count: > 0 } spells)
        {
            // "WHICH SPELL?": the combat-castable spells (D-035), narrowed by the digits typed so far; every one listed.
            title = UiText.F("ui.which_spell_x", presenter.SpellDigits);
            foreach (var id in spells)
            {
                Keys[] keys = [.. (id + 1).ToString().Select(c => HitTest.Digit(c - '0')), Keys.Enter];
                foreach (var part in Lines(UiText.F("ui.x_x_x_mp", id + 1, data.GetSpell(id).Name,
                             Magic.MagicPointCost(id, SpellSlots.For(session.Options))), menuChars))
                    rows.Add((part, BodyText, presenter.SpellDigits.Length == 0 // the spell's number, then Return
                        ? r => Hits?.Add(new HitRegion(r) { Click = keys }) : null));
            }
        }
        else if (presenter.Phase is CombatPhase.Orders or CombatPhase.Resolving && session.Combat is not null)
        {
            title = UiText.S("ui.orders");
            var members = session.Party.Members;
            var nameWidth = Math.Max(8, members.Take(7).Max(m => m.Name.Length));
            for (var i = 0; i < members.Count && i < 7; i++)
            {
                if (!CombatPresenter.IsOrderable(members[i]))
                    continue; // sleepers etc. are left off the list (OV2 0x6ab9)
                var on = i == presenter.OrderMember;
                if (on)
                    cursorRow = rows.Count;
                var text = $"{(on ? ">" : " ")}{i + 1} {members[i].Name.PadRight(nameWidth)} {presenter.OrderLabelFor(session, i)}";
                foreach (var part in Lines(text, menuChars))
                    rows.Add((part, on ? Color.Yellow : BodyText, null));
            }
        }
        if (title is null)
            return;
        PixelFont.Draw(batch, pixel, rx, top + 18, title, TitleGold);
        if (presenter.AwaitingOrders)
        {
            menuBox.TopFor(presenter.LastLine.GetHashCode());
            cursorRow = 0; // keep the question visible; the complete orders / spell list still scrolls below it
        }
        menuBox.Set(rows.Count, menuRows);
        if (cursorRow >= 0)
            menuBox.Follow(cursorRow);
        HitScrollArea(menuBox, menuArea);
        for (var i = 0; i < menuRows && menuBox.Offset + i < rows.Count; i++)
        {
            var (text, color, hit) = rows[menuBox.Offset + i];
            var rowY = menuTop + i * 8;
            PixelFont.Draw(batch, pixel, rx, rowY, text, color);
            hit?.Invoke(new Rectangle(rx - 1, rowY - 1, menuArea.Width, 8));
        }
        DrawScrollBar(batch, pixel, menuBox, menuArea);
    }

    /// <summary>
    /// The combat rank list in <paramref name="box"/>: per living rank its first monster's idle picture 1:1 (a coloured
    /// block without one) and the rank line wrapped beside it. <see cref="CombatPresenter.RankScroll"/> moves by complete
    /// blocks; "WHICH RANK?" takes a click on a visible block.
    /// </summary>
    private static void DrawRankBox(SpriteBatch batch, Texture2D pixel, AssetCatalog assets, CombatEncounter? combat,
        CombatPresenter presenter, Rectangle box)
    {
        // s156: while II's round plays, the ranks as they stood before it, less the monsters erased so far (0x186de mode 0).
        List<string> lines;
        List<IReadOnlyList<Monster>> living;
        IReadOnlyList<Monster>? kinds = null;
        if (presenter.P2RankRows is { } p2Rows)
        {
            living = p2Rows.ToList();
            // s163: a rank emptied in the round keeps its row (monster slots), "RANK n : 0 …" (the ST draws no line).
            IReadOnlyList<Monster> rowKinds = presenter.P2RankKinds ?? [.. living.Select(r => r[0])];
            kinds = rowKinds;
            lines = living.Select((r, i) => UiText.F("combattext.rank_x_x_x_s", i + 1, r.Count, rowKinds[i].Name)).ToList();
        }
        else if (combat is not null)
        {
            lines = CombatText.RankLines(combat).ToList();
            living = combat.Ranks.Where(r => !r.IsWiped).Select(r => (IReadOnlyList<Monster>)r.Living.ToList()).ToList();
        }
        else
            return;
        if (presenter.P2RankSlots(combat) is { } slots && slots.Count == lines.Count)
        {
            DrawRankSlots(batch, pixel, assets, presenter, box, lines, slots, kinds);
            return;
        }
        var pose = presenter.P2Run?.Pose is { Monster: not null } p ? p : null;
        var sparkle = presenter.P2Run?.Sparkle is { Target: not null } sp ? sp : null;
        var sparkleLayout = presenter.P2Lines?.Invoke()?.Sparkle;
        var inner = new Rectangle(box.X, box.Y, box.Width - ScrollBox.BarWidth - 2, box.Height);
        var rows = new List<(Texture2D? Picture, Point Size, IReadOnlyList<string> Text, int Height)>();
        for (var i = 0; i < lines.Count && i < living.Count; i++)
        {
            var picture = assets.GetMonsterIdle(living[i][0].Id);
            // s156: II's attack pose / silhouette on the rank's picture (the ST poses the monster's own slot `[~]`).
            if (pose is { Monster: { } posing } && living[i].Contains(posing)
                && P2PoseArt(assets, presenter, PictureNames.MonsterAttacking(posing.Id), pose.Silhouette) is { } posed)
                picture = posed;
            var size = picture is null ? new Point(12, 12) : new Point(picture.Width, picture.Height);
            var text = Lines(lines[i], Math.Max(1, (inner.Width - size.X - 6) / PixelFont.Advance));
            rows.Add((picture, size, text, Math.Max(size.Y, 3 + text.Count * PixelFont.LineHeight)));
        }
        var scroll = presenter.RankScroll;
        var visible = WholePictureRows(scroll, rows.Select(r => r.Height).ToArray(), box.Height);
        HitScrollArea(scroll, box);
        Clipped(batch, box, () =>
        {
            foreach (var (r, offset) in visible)
            {
                var y = box.Y + offset;
                var (picture, size, text, height) = rows[r];
                var art = new Rectangle(box.X + 2, y, size.X, size.Y);
                if (picture is not null)
                    batch.Draw(picture, art, Color.White);
                else
                    Fill(batch, pixel, art, new Color(90, 40, 40));
                // s110 (III Amiga): a spell flash on a monster of this rank, over the rank's one picture `[~]` (the Amiga
                // draws each monster and flashes the one hit).
                if (presenter.FlashShown is { Flash.Monster: { } hit } shownFlash && presenter.FlashRun is { } run
                    && living[r].Contains(hit))
                    DrawCombatFlash(batch, assets, run.Layout, shownFlash, art.Location);
                // s156: II's sparkle (0x18b16) at its random spot inside the picture: x + rand(w − 16), y + rand(h − 16).
                if (sparkle is { Target: { } target } && sparkleLayout is { } sl && living[r].Contains(target)
                    && assets.Get(sparkle.Rock ? sl.Rock : sl.Spell) is { } sprite)
                    batch.Draw(sprite, new Rectangle(art.X + StCombatSparkle.Offset(sparkle.RollX, art.Width - sl.Size),
                        art.Y + StCombatSparkle.Offset(sparkle.RollY, art.Height - sl.Size), sprite.Width, sprite.Height),
                        Color.White);
                for (var k = 0; k < text.Count; k++)
                {
                    var lineY = y + 3 + k * PixelFont.LineHeight;
                    if (lineY >= box.Y && lineY + PixelFont.GlyphHeight <= box.Bottom) // a line shows whole or not at all
                        PixelFont.Draw(batch, pixel, art.Right + 4, lineY, text[k], Color.White);
                }
            }
        });
        if (presenter.AsksRank)
            foreach (var (i, offset) in visible)
                Hits?.Add(new HitRegion(new Rectangle(box.X, box.Y + offset, inner.Width, rows[i].Height))
                    { Click = [HitTest.Digit(i + 1)] });
        DrawScrollBar(batch, pixel, scroll, box);
    }

    /// <summary>Scroll by complete entries. Text and its picture stay together; the last entry is always reachable.</summary>
    public static IReadOnlyList<(int Index, int Y)> WholePictureRows(ScrollBox scroll, IReadOnlyList<int> heights, int room)
    {
        scroll.Set(heights.Count, 1);
        var rows = new List<(int, int)>();
        var y = 0;
        for (var i = scroll.Offset; i < heights.Count && y + heights[i] <= room; i++)
        {
            rows.Add((i, y));
            y += heights[i] + SlotGap;
        }
        return rows;
    }

    /// <summary>Pixels between two monster slots of a rank, across and down (s162).</summary>
    public const int SlotGap = 2;

    /// <summary>
    /// s162: where each monster slot of a rank goes (II ST, <see cref="StCombatLines.MonsterSlots"/>): left to right from
    /// (<paramref name="left"/>, <paramref name="top"/>), <see cref="SlotGap"/> apart, each at its own size; a slot that
    /// would run past <paramref name="width"/> starts a new line (the first of a line always stays, so nothing is cut).
    /// </summary>
    public static List<Rectangle> SlotRects(IReadOnlyList<Point> sizes, int left, int top, int width)
    {
        ArgumentNullException.ThrowIfNull(sizes);
        var rects = new List<Rectangle>();
        int x = left, y = top, lineHeight = 0;
        foreach (var size in sizes)
        {
            if (x > left && x + size.X > left + width)
            {
                x = left;
                y += lineHeight + SlotGap;
                lineHeight = 0;
            }
            rects.Add(new Rectangle(x, y, size.X, size.Y));
            x += size.X + SlotGap;
            lineHeight = Math.Max(lineHeight, size.Y);
        }
        return rects;
    }

    /// <summary>
    /// s162, II ST's ranks with a picture per monster (<c>0x18288</c>: each group a row of its own monsters): per rank its
    /// line (wrapped), then each monster's idle picture 1:1 in its slot (<see cref="SlotRects"/>); the pose, the sparkle and
    /// the erase on the monster's own slot (<c>0x186de</c> / <c>0x18b16</c>), an erased slot left empty (a rank with every
    /// monster erased an empty row, sized by its kind in <paramref name="kinds"/>, until the re-rank). Scrolls as
    /// <see cref="DrawRankBox"/>.
    /// </summary>
    private static void DrawRankSlots(SpriteBatch batch, Texture2D pixel, AssetCatalog assets, CombatPresenter presenter,
        Rectangle box, IReadOnlyList<string> lines, IReadOnlyList<IReadOnlyList<Monster?>> slots,
        IReadOnlyList<Monster>? kinds)
    {
        var pose = presenter.P2Run?.Pose is { Monster: not null } p ? p : null;
        var sparkle = presenter.P2Run?.Sparkle is { Target: not null } sp ? sp : null;
        var sparkleLayout = presenter.P2Lines?.Invoke()?.Sparkle;
        var inner = new Rectangle(box.X, box.Y, box.Width - ScrollBox.BarWidth - 2, box.Height);
        var rows = new List<(int Rank, IReadOnlyList<string> Text, List<(Monster? Monster, Texture2D? Picture, Rectangle At)> Slots, int Height)>();
        for (var i = 0; i < lines.Count; i++)
        {
            // The rank's own kind sizes its slots, so a row whose monsters are all erased keeps its height (s163).
            var kind = kinds is not null && i < kinds.Count ? kinds[i] : slots[i].FirstOrDefault(m => m is not null);
            var idle = kind is null ? null : assets.GetMonsterIdle(kind.Id);
            var size = idle is null ? new Point(12, 12) : new Point(idle.Width, idle.Height);
            // A visual row of slots is one scroll block, with its rank line beside it (repeated after a wrap).
            var across = Math.Max(1, (inner.Width - 8 - 8 * PixelFont.Advance) / (size.X + SlotGap));
            for (var first = 0; first < Math.Max(1, slots[i].Count); first += across)
            {
                var count = Math.Min(across, slots[i].Count - first);
                var pictureWidth = Math.Max(size.X, count * (size.X + SlotGap) - SlotGap);
                var text = Lines(lines[i], Math.Max(1, (inner.Width - pictureWidth - 8) / PixelFont.Advance));
                var cells = new List<(Monster?, Texture2D?, Rectangle)>();
                for (var k = 0; k < count; k++)
                {
                    var m = slots[i][first + k];
                    var picture = m is null ? null : idle;
                    if (m is not null && pose is { Monster: { } posing } && ReferenceEquals(posing, m)
                        && P2PoseArt(assets, presenter, PictureNames.MonsterAttacking(m.Id), pose.Silhouette) is { } posed)
                        picture = posed;
                    cells.Add((m, picture, new Rectangle(2 + k * (size.X + SlotGap), 0, size.X, size.Y)));
                }
                rows.Add((i, text, cells, Math.Max(size.Y, 3 + text.Count * PixelFont.LineHeight)));
            }
        }
        var scroll = presenter.RankScroll;
        var visible = WholePictureRows(scroll, rows.Select(r => r.Height).ToArray(), box.Height);
        HitScrollArea(scroll, box);
        Clipped(batch, box, () =>
        {
            foreach (var (index, offset) in visible)
            {
                var y = box.Y + offset;
                var (_, text, cells, _) = rows[index];
                var textX = box.X + (cells.Count == 0 ? 2 : cells.Max(c => c.At.Right)) + 4;
                for (var k = 0; k < text.Count; k++)
                {
                    var lineY = y + 3 + k * PixelFont.LineHeight;
                    if (lineY >= box.Y && lineY + PixelFont.GlyphHeight <= box.Bottom) // a line shows whole or not at all
                        PixelFont.Draw(batch, pixel, textX, lineY, text[k], Color.White);
                }
                foreach (var (monster, picture, at) in cells)
                {
                    var art = new Rectangle(box.X + at.X, y + at.Y, at.Width, at.Height);
                    if (picture is not null)
                        batch.Draw(picture, art, Color.White);
                    else if (monster is not null)
                        Fill(batch, pixel, art, new Color(90, 40, 40));
                    // 0x18b16: the sparkle at its random spot inside the monster's own slot.
                    if (monster is not null && sparkle is { Target: { } target } && ReferenceEquals(target, monster)
                        && sparkleLayout is { } sl && assets.Get(sparkle.Rock ? sl.Rock : sl.Spell) is { } sprite)
                        batch.Draw(sprite, new Rectangle(art.X + StCombatSparkle.Offset(sparkle.RollX, art.Width - sl.Size),
                            art.Y + StCombatSparkle.Offset(sparkle.RollY, art.Height - sl.Size), sprite.Width, sprite.Height),
                            Color.White);
                }
            }
        });
        if (presenter.AsksRank)
            foreach (var (i, offset) in visible)
                Hits?.Add(new HitRegion(new Rectangle(box.X, box.Y + offset, inner.Width, rows[i].Height))
                    { Click = [HitTest.Digit(rows[i].Rank + 1)] });
        DrawScrollBar(batch, pixel, scroll, box);
    }

    /// <summary>
    /// A combat spell flash (III Amiga <c>0x1b814</c>, <see cref="CombatFlashRun"/>): its first frames drawn over each other
    /// at <paramref name="picture"/> + the flash's offset (the Amiga's masked blits: colour 0 transparent; s170: III DOS
    /// ORs them onto the screen, the layout's <see cref="CombatFlashLayout.Blend"/>).
    /// </summary>
    private static void DrawCombatFlash(SpriteBatch batch, AssetCatalog assets, CombatFlashLayout layout,
        (CombatFlashRun.Flash Flash, int Frames) shown, Point picture) =>
        Blended(batch, layout.Blend, () =>
        {
            for (var i = 0; i < shown.Frames && i < layout.Pictures.Count; i++)
                if (assets.Get(layout.Pictures[i]) is { } sparkle)
                    batch.Draw(sparkle, new Rectangle(picture.X + shown.Flash.X, picture.Y + shown.Flash.Y, sparkle.Width,
                        sparkle.Height), Color.White);
        });

    /// <summary>
    /// A prompt's text lines as the panel draws them: blank lines dropped, each wrapped to <paramref name="maxChars"/>
    /// columns at word breaks; all of them (s99: a panel with more lines than fit scrolls, <see cref="PromptScroll"/>).
    /// </summary>
    public static List<string> PromptLines(IEnumerable<string> lines, int maxChars) =>
        lines.Where(l => !string.IsNullOrWhiteSpace(l)).SelectMany(l => Wrap(l, maxChars)).ToList();

    /// <summary>A prompt panel's text when it has more lines than the screen holds (PgUp / PgDn, the wheel, the bar).</summary>
    public static ScrollBox PromptScroll { get; } = new();

    /// <summary>Rows (text + answers) a centred prompt panel holds between the top and the help band.</summary>
    public const int PromptPanelRows = 12;

    /// <summary>
    /// The side-by-side answers ("&gt;YES  NO") in rows of at most <paramref name="maxChars"/> columns: each answer's row
    /// and its columns. One row as always when they fit; else they go on, whole, on the next row.
    /// </summary>
    public static (List<string> Rows, List<(int Row, int Start, int End)> Spots) SideBySide(IReadOnlyList<string> options,
        int cursor, int maxChars)
    {
        var rows = new List<string> { "" };
        var spots = new List<(int, int, int)>();
        for (var i = 0; i < options.Count; i++)
        {
            var token = (i == cursor ? ">" : " ") + options[i];
            if (rows[^1].Length > 0 && rows[^1].Length + 2 + token.Length > maxChars)
                rows.Add("");
            if (rows[^1].Length > 0)
                rows[^1] += "  ";
            spots.Add((rows.Count - 1, rows[^1].Length, rows[^1].Length + token.Length));
            rows[^1] += token;
        }
        return (rows, spots);
    }

    public static void DrawPrompt(SpriteBatch batch, Texture2D pixel, SessionPrompt prompt, int cursor)
    {
        const int maxChars = 48;
        // A line longer than the panel wraps at a word (III's DOS sentences run to 80 chars), never cut (s98).
        var lines = PromptLines(prompt.Lines, maxChars);
        var single = prompt.Options.Count == 1;
        var sideBySide = prompt.IsYesNo || prompt.IsShares || prompt.Hotkeys is not null;
        if (!single && !sideBySide && prompt.Options.Count > PromptListRows)
        {
            DrawPromptGrid(batch, pixel, prompt, cursor, lines, maxChars);
            return;
        }
        var (pairRows, pairSpots) = SideBySide(prompt.Options, cursor, maxChars);
        var answers = single ? [PadWording.S("ui.press_enter")] // s193 (pad fix E30): "PRESS A" while the pad is the input
            : sideBySide ? pairRows
            : prompt.Options.Select((o, i) => $"{(i == cursor ? ">" : " ")}{i + 1} {o}").SelectMany(a => Lines(a, maxChars)).ToList();
        var shown = ScrollPromptText(lines, answers.Count, atEnd: prompt.Kind == SessionPromptKind.PartyDefeat);
        var height = 16 + (shown + answers.Count) * 10 + 4;
        var top = PromptPanelTop(shown, height);
        var panel = new Rectangle(8, top, 304, height);
        DrawPanel(batch, pixel, panel, PanelBg, PanelEdge);
        var y = top + 8 + shown * 10 + 4;
        HitPrompt(prompt, cursor, single, sideBySide, y, pairSpots); // under the text's scroll bar
        DrawPromptText(batch, pixel, lines, shown, top);
        for (var i = 0; i < answers.Count; i++)
        {
            var on = !single && !sideBySide && i == cursor;
            PixelFont.Draw(batch, pixel, 14, y, answers[i], on ? Color.Yellow : BodyText);
            y += 10;
        }
    }

    /// <summary>
    /// How many of <paramref name="lines"/> a prompt panel shows above <paramref name="answerRows"/> answer rows: all of
    /// them when they fit <see cref="PromptPanelRows"/>, else the rest of the panel, scrolled by <see cref="PromptScroll"/>
    /// (back to the top for each new text).
    /// </summary>
    private static int ScrollPromptText(IReadOnlyList<string> lines, int answerRows, bool atEnd = false)
    {
        var shown = Math.Min(lines.Count, Math.Max(2, PromptPanelRows - answerRows));
        PromptScroll.Set(lines.Count, Math.Max(1, shown));
        if (PromptScroll.TopFor(string.Join("\n", lines).GetHashCode()) && atEnd)
            PromptScroll.End(); // the defeat report opens on its last blows and the banner; Up / PgUp go back
        return shown;
    }

    /// <summary>A prompt panel's top: centred on its text as always, moved up when its foot would reach the help band.</summary>
    private static int PromptPanelTop(int textRows, int height) =>
        Math.Max(ContentTop + 2, Math.Min(TextPanelTop(textRows), ContentBottom - height));

    /// <summary>The scrolled prompt text from the panel's first line (and its bar); the y under the last line shown.</summary>
    private static int DrawPromptText(SpriteBatch batch, Texture2D pixel, IReadOnlyList<string> lines, int shown, int top)
    {
        var y = top + 8;
        var area = new Rectangle(12, y - 1, 296, shown * 10);
        HitScrollArea(PromptScroll, area);
        for (var i = 0; i < shown && PromptScroll.Offset + i < lines.Count; i++)
        {
            PixelFont.Draw(batch, pixel, 14, y, lines[PromptScroll.Offset + i], TitleGold);
            y += 10;
        }
        DrawScrollBar(batch, pixel, PromptScroll, area);
        return top + 8 + shown * 10;
    }

    /// <summary>
    /// Phantasie II's party-defeat scene (ST <c>0x12bea</c>, FORMAT.md §10.3/§10.4; <see cref="DefeatScene"/>) behind the
    /// defeat prompts: <c>DECOR.PIC</c> (the temple) with the door at (123,14) — closed under the banner, sinking into the
    /// floor (<paramref name="frame"/>) — then the "Sorry, we've run out of Demons" sign at (139,39) on the 5th defeat of
    /// the run, or the Great Demon's face at (123,19); and the portrait row of <paramref name="row"/> (ghosts, then each
    /// member's fate). Drawn full-screen 1:1 in ST coordinates, over the status, help and message lines: the whole of DECOR
    /// (155 rows, stairs and the guards' platform included), the row at y 158 and <paramref name="line"/> on the bottom line
    /// (<see cref="DefeatScenePager"/>), as the ST screen shows them (s98, user: never crop scenes; s97 had cut DECOR at the
    /// door's floor line to keep the remaster's status line). A click anywhere is Return. False (nothing drawn) when the
    /// pack has no DECOR picture.
    /// </summary>
    public static bool DrawDefeatScene(SpriteBatch batch, Texture2D pixel, AssetCatalog assets, SessionPromptKind kind,
        DefeatSceneFrame frame, IReadOnlyList<DefeatPortrait> row, string? line = null, bool awaitsReturn = true,
        Color? lineColour = null, int? centredTop = null, bool clickIsReturn = true)
    {
        if (assets.Decor is not { } decor)
            return false;
        Fill(batch, pixel, new Rectangle(0, 0, 320, ScreenBottom), DosBlack);
        var h = Math.Min(decor.Height, DefeatScene.DecorRows);
        batch.Draw(decor, new Rectangle(0, 0, decor.Width, h), new Rectangle(0, 0, decor.Width, h), Color.White);
        if (frame.ShowBehindDoor && kind != SessionPromptKind.PartyDefeat)
        {
            var (behind, bx, by) = kind == SessionPromptKind.DefeatDemonSign
                ? (assets.DecorSorrySign, 139, 39)
                : (assets.DecorDemonFace, 123, 19);
            DrawDecorPart(batch, behind, bx, by, behind?.Height ?? 0);
        }
        if (frame.DoorHeight > 0)
            DrawDecorPart(batch, assets.DecorDoor, 123, frame.DoorY, frame.DoorHeight);
        DrawDefeatRow(batch, assets, row);
        if (line is not null && centredTop is int textTop)
        {
            // I Amiga (s153): h80 +0x84a centres the line on 320 at its TopEdge in the layout's pen; it fits one row.
            var text = line.Trim();
            PixelFont.Draw(batch, pixel, (320 - PixelFont.Measure(text)) / 2, textTop, text, lineColour ?? DefeatTextGreen);
        }
        else if (line is not null)
        {
            // A line too long for the bottom line wraps upward over the row's foot, never cut (s98).
            var parts = Wrap(line, 50).ToList();
            var top = DefeatScene.TextTop + 1 - (parts.Count - 1) * 10;
            if (parts.Count > 1)
                Fill(batch, pixel, new Rectangle(0, top - 1, 320, DefeatScene.TextTop + 1 - top), DosBlack);
            for (var i = 0; i < parts.Count; i++)
                PixelFont.Draw(batch, pixel, 4, top + i * 10, parts[i], DefeatTextGreen);
            if (awaitsReturn) // Return shows the next line or, on the last, goes on (the ST went on by itself)
                PixelFont.Draw(batch, pixel, 310, DefeatScene.TextTop + 1, ">", DimText);
        }
        if (clickIsReturn) // I Amiga's screens take the button's press and release themselves (s153)
            Hits?.Add(new HitRegion(new Rectangle(0, 0, 320, ScreenBottom), new BackdropTag()) { Click = [Keys.Enter] });
        return true;
    }

    /// <summary>
    /// s157: Phantasie I DOS's party-defeat outcome with the pack's DOS pictures (<see cref="P1DosDefeatScene"/>; OV1
    /// <c>0x2af</c>): the whole temple screen (the face once <see cref="DosDefeatFrame.Face"/>), the row cleared
    /// (<c>0x5fe</c>), each slot's ghost or figure at the layout's places, a destroyed member's text cells cleared
    /// (<c>0x747</c>), and the line centred on the bottom row. False when the pictures are missing.
    /// </summary>
    public static bool DrawDosDefeatScene(SpriteBatch batch, Texture2D pixel, AssetCatalog assets, DefeatPicturesLayout layout,
        DosDefeatFrame frame, IReadOnlyList<DefeatPortrait> row)
    {
        var screen = assets.Get(frame.Face ? PictureNames.DefeatDemon : PictureNames.DefeatTemple);
        if (screen is null)
            return false;
        Fill(batch, pixel, new Rectangle(0, 0, 320, ScreenBottom), DosBlack);
        batch.Draw(screen, new Rectangle(0, 0, screen.Width, screen.Height), Color.White);
        var rowRect = new Rectangle(0, layout.ClearTop, 320, layout.ClearBottom - layout.ClearTop + 1);
        Fill(batch, pixel, rowRect, DosBlack);
        var ghost = assets.Get(PictureNames.DefeatGhost);
        for (var i = 0; i < row.Count; i++)
        {
            var x = layout.SlotX(row.Count, i);
            var picture = row[i].Kind switch
            {
                DefeatPortraitKind.Ghost => ghost,
                DefeatPortraitKind.Race => assets.Get(PictureNames.DefeatFigure(row[i].RaceId)),
                _ => null,
            };
            if (picture is not null)
                batch.Draw(picture, new Rectangle(x, layout.RowY, picture.Width, picture.Height), Color.White);
            else if (row[i].Kind == DefeatPortraitKind.Cleared)
                Fill(batch, pixel, new Rectangle(Math.Max(0, (x / 8 - 1) * 8), layout.ClearTop, 48, rowRect.Height), DosBlack);
        }
        Fill(batch, pixel, new Rectangle(0, layout.TextY, 320, ScreenBottom - layout.TextY), DosBlack);
        if (frame.Line is { } line)
        {
            var text = line.Trim();
            PixelFont.Draw(batch, pixel, (320 - PixelFont.Measure(text)) / 2, layout.TextY, text,
                VirtualScreen.ParseColor(layout.TextColour));
        }
        return true;
    }

    /// <summary>
    /// Phantasie III's party-defeat scene (Amiga root <c>0x1dc16</c>, <see cref="AstralScene"/>), full-screen 1:1 over the
    /// status, help and message lines (s98, user: never crop scenes): line <paramref name="lineIndex"/> of the outcome
    /// page. The first two lines print on the cleared screen at text rows 12–13, wrapped and centred; from the third the
    /// whole Astral Plane picture shows with the party's portraits <paramref name="row"/> under it and the line on text row
    /// 24 (a line too long for one row wraps upward from there, never cut). A click anywhere is Return. False when the
    /// pack has no Astral Plane picture.
    /// </summary>
    public static bool DrawAstralScene(SpriteBatch batch, Texture2D pixel, AssetCatalog assets, int lineIndex, string? line,
        IReadOnlyList<DefeatPortrait> row, bool awaitsReturn = true, Color? lineColour = null)
    {
        if (assets.SceneAstralPlane is not { } picture)
            return false;
        Fill(batch, pixel, new Rectangle(0, 0, 320, ScreenBottom), DosBlack);
        var parts = line is null ? [] : Wrap(line, 52).ToList();
        if (lineIndex < AstralScene.PictureFromLine)
        {
            for (var i = 0; i < parts.Count; i++)
                PixelFont.Draw(batch, pixel, (320 - PixelFont.Measure(parts[i])) / 2, AstralScene.IntroTextTop + 1 + i * 10,
                    parts[i], DosWhite);
        }
        else
        {
            batch.Draw(picture, new Rectangle(0, 0, picture.Width, picture.Height), Color.White);
            for (var i = 0; i < row.Count; i++)
            {
                var portrait = row[i].Kind switch
                {
                    DefeatPortraitKind.Grave => assets.PortraitGrave,
                    DefeatPortraitKind.Race => assets.GetPortraitIdle(row[i].RaceId),
                    _ => null,
                };
                if (portrait is not null)
                    batch.Draw(portrait, new Rectangle(i * AstralScene.SlotWidth, AstralScene.RowTop, portrait.Width,
                        portrait.Height), Color.White);
            }
            var top = AstralScene.TextTop - (parts.Count - 1) * 10;
            if (parts.Count > 1) // the rows above text row 24 hold the portraits' feet: give the extra lines a backing
                Fill(batch, pixel, new Rectangle(0, top - 1, 320, AstralScene.TextTop - top), DosBlack);
            for (var i = 0; i < parts.Count; i++)
                PixelFont.Draw(batch, pixel, 4, top + i * 10, parts[i], lineColour ?? DosWhite); // undead: colour 14
        }
        if (awaitsReturn)
            PixelFont.Draw(batch, pixel, 310, lineIndex < AstralScene.PictureFromLine ? 190 : AstralScene.TextTop, ">", DimText);
        Hits?.Add(new HitRegion(new Rectangle(0, 0, 320, ScreenBottom), new BackdropTag()) { Click = [Keys.Enter] });
        return true;
    }

    /// <summary>
    /// Phantasie III's gods' reward (Amiga root <c>0x1ee82</c>, <see cref="GodsRewardScene"/>), full-screen 1:1: page
    /// <paramref name="page"/>. Pages 0-1 are the intro lines on the cleared screen (wrapped, never cut); page 2 the whole
    /// god's picture with the party's portraits under it and "Press a key"; page 3 the speech on the parchment (rows that
    /// run past its foot scroll, <see cref="DrawScrollFrame"/>). A click anywhere is Return. False when the pack has no
    /// picture for this outcome or no parchment. <paramref name="flash"/>: the intro's sparkle shown now
    /// (<see cref="RewardSceneClock.FlashAt"/>), its first frames drawn over each other on top of the text (Amiga
    /// <c>0x1b814</c>: masked blits, colour 0 transparent).
    /// </summary>
    public static bool DrawRewardScene(SpriteBatch batch, Texture2D pixel, AssetCatalog assets, RewardScene scene, int page,
        IReadOnlyList<string> pages, IReadOnlyList<ScrollFrameLayout.Edge>? edges, string pressKey,
        (RewardFlashLayout.Flash Flash, int Frames)? flash = null)
    {
        if (assets.SceneReward(scene.Good) is not { } picture || assets.ScrollFrame is not { } frame || edges is null)
            return false;
        Fill(batch, pixel, new Rectangle(0, 0, 320, ScreenBottom), DosBlack);
        Hits?.Add(new HitRegion(new Rectangle(0, 0, 320, ScreenBottom), new BackdropTag()) { Click = [Keys.Enter] });
        if (page < GodsRewardScene.PicturePage)
        {
            var parts = Wrap(pages[page], 52).ToList();
            var top = Math.Min(GodsRewardScene.IntroTop[page], 190 - parts.Count * 10);
            for (var i = 0; i < parts.Count; i++)
                PixelFont.Draw(batch, pixel, 4, top + i * 10, parts[i], DosWhite);
            PixelFont.Draw(batch, pixel, 310, 190, ">", DimText);
            if (flash is { } shown && assets.RewardScene?.Flash is { } layout)
                Blended(batch, layout.Blend, () => // s170: III DOS ORs the frames (0x1537), the Amiga masks them
                {
                    for (var i = 0; i < shown.Frames && i < layout.Pictures.Count; i++)
                        if (assets.Get(layout.Pictures[i]) is { } sparkle)
                            batch.Draw(sparkle, new Rectangle(shown.Flash.X, shown.Flash.Y, sparkle.Width, sparkle.Height),
                                Color.White);
                });
        }
        else if (page == GodsRewardScene.PicturePage)
        {
            batch.Draw(picture, new Rectangle(0, 0, picture.Width, picture.Height), Color.White);
            for (var i = 0; i < scene.PartyRaces.Count; i++)
                if (assets.GetPortraitIdle((int)scene.PartyRaces[i]) is { } portrait)
                    batch.Draw(portrait, new Rectangle(i * GodsRewardScene.SlotWidth, GodsRewardScene.RowTop, portrait.Width,
                        portrait.Height), Color.White);
            PixelFont.Draw(batch, pixel, GodsRewardScene.PressKeyX, GodsRewardScene.PressKeyY, pressKey, DosWhite);
        }
        else
            DrawScrollFrame(batch, pixel, assets, frame, edges, GodsRewardScene.SpeechLines(scene, pressKey));
        return true;
    }

    /// <summary>
    /// A keep question in the pack's own window (III Amiga, <see cref="KeepWindowsLayout"/>, FORMAT.md §2.3): the box
    /// filled, the frame (<c>0x31e6</c>: the outer colour from the top-left to x1 − 1 / y1 − 1, the inner one pixel in and
    /// down to x1 / y1, drawn top, bottom, left, right), the lines at their origins (a line too wide for the box goes on
    /// at a word on the next row; never cut), and the OK / CANCEL buttons, the second state while
    /// <paramref name="held"/> is over one. A click on OK presses O, on CANCEL C; elsewhere does nothing. False (nothing
    /// drawn) when the pack lacks the buttons. <paramref name="inDungeon"/>: the buttons show in the pack's
    /// <see cref="KeepWindowsLayout.DungeonPalette"/> (the Amiga's dungeon palette stays in force, s125 seen).
    /// </summary>
    public static bool DrawKeepWindow(SpriteBatch batch, Texture2D pixel, AssetCatalog assets, KeepWindowsLayout layout,
        KeepWindowsLayout.Window window, IReadOnlyList<string> lines, Point? held, bool inDungeon = false)
    {
        var palette = inDungeon ? layout.DungeonPalette : null;
        if (assets.GetInPalette(PictureNames.Button("ok"), layout.ButtonPalette, palette) is not { } ok
            || assets.GetInPalette(PictureNames.Button("cancel"), layout.ButtonPalette, palette) is not { } cancel)
            return false;
        var (x0, y0, x1, y1) = (window.Box[0], window.Box[1], window.Box[2], window.Box[3]);
        using var font = PixelFont.With(FontSources.Named(window.Font, window.FontFile)); // s162: window 1 in topaz 8
        var pitch = KeepWindowRowPitch(window);
        var rows = KeepWindowRows(window, lines);
        var buttonsTop = Math.Min(window.Ok.Y, window.Cancel.Y);
        var grow = rows.Count == 0 ? 0 : Math.Max(0, rows[^1].Y + pitch + 2 - buttonsTop);
        var lift = Math.Max(0, y1 + grow - (ScreenBottom - 1));
        y0 -= lift;
        y1 += grow - lift;
        var (ok0, cancel0) = ((window.Ok.X, window.Ok.Y + grow - lift), (window.Cancel.X, window.Cancel.Y + grow - lift));
        DrawWindowFrame(batch, pixel, layout, x0, y0, x1, y1);
        var ink = VirtualScreen.ParseColor(layout.TextColour);
        foreach (var (x, y, text) in rows)
            PixelFont.Draw(batch, pixel, x, y - lift, text, ink);
        DrawWindowButtons(batch, assets, layout, ok, cancel, ok0, cancel0, held, palette);
        return true;
    }

    /// <summary>
    /// s162: the row pitch of a keep window's text: the font's line height when the pack names a font for the window (III
    /// Amiga's window 1: topaz 8, <c>0x854</c> moves one 8-px row per "\r"), else the remaster's 10 px.
    /// </summary>
    public static int KeepWindowRowPitch(KeepWindowsLayout.Window window) =>
        FontSources.Named(window.Font, window.FontFile) is { } f ? f.LineHeight : 10;

    /// <summary>
    /// A keep window's text rows (each line whole, s99: a line too wide for the box goes on at a word on the next row and
    /// pushes the next line down; never cut), <see cref="KeepWindowRowPitch"/> apart, measured in the window's font.
    /// </summary>
    public static IReadOnlyList<(int X, int Y, string Text)> KeepWindowRows(KeepWindowsLayout.Window window,
        IReadOnlyList<string> lines)
    {
        using var font = PixelFont.With(FontSources.Named(window.Font, window.FontFile));
        var pitch = KeepWindowRowPitch(window);
        var x1 = window.Box[2];
        var rows = new List<(int X, int Y, string Text)>();
        var (tx, ty) = window.Text[0];
        for (var i = 0; i < lines.Count; i++)
        {
            var next = rows.Count == 0 ? ty : rows[^1].Y + pitch;
            (tx, ty) = i < window.Text.Count ? (window.Text[i].X, Math.Max(window.Text[i].Y, next)) : (tx, next);
            foreach (var row in Lines(lines[i], Math.Max(1, (x1 - 3 - tx) / PixelFont.Advance)))
            {
                rows.Add((tx, ty, row));
                ty += pitch;
            }
        }
        return rows;
    }

    /// <summary>
    /// An Amiga window's box and frame (<c>0x3904</c> / <c>0x31e6</c>: filled, the outer colour from the top-left to
    /// x1 − 1 / y1 − 1, the inner one pixel in and down to x1 / y1); clicks outside the buttons do nothing.
    /// </summary>
    private static void DrawWindowFrame(SpriteBatch batch, Texture2D pixel, KeepWindowsLayout layout, int x0, int y0, int x1, int y1) =>
        DrawWindowFrame(batch, pixel, VirtualScreen.ParseColor(layout.FillColour), VirtualScreen.ParseColor(layout.FrameColours[0]),
            VirtualScreen.ParseColor(layout.FrameColours[1]), x0, y0, x1, y1);

    private static void DrawWindowFrame(SpriteBatch batch, Texture2D pixel, Color fill, Color outer, Color inner,
        int x0, int y0, int x1, int y1)
    {
        Fill(batch, pixel, new Rectangle(x0, y0, x1 - x0 + 1, y1 - y0 + 1), fill);
        Fill(batch, pixel, new Rectangle(x0, y0, x1 - x0, 1), outer);
        Fill(batch, pixel, new Rectangle(x0 + 1, y0 + 1, x1 - x0 - 1, 1), inner);
        Fill(batch, pixel, new Rectangle(x0, y1 - 1, x1 - x0, 1), outer);
        Fill(batch, pixel, new Rectangle(x0 + 1, y1, x1 - x0, 1), inner);
        Fill(batch, pixel, new Rectangle(x0, y0, 1, y1 - y0), outer);
        Fill(batch, pixel, new Rectangle(x0 + 1, y0 + 1, 1, y1 - y0 - 2), inner);
        Fill(batch, pixel, new Rectangle(x1 - 1, y0, 1, y1 - y0), outer);
        Fill(batch, pixel, new Rectangle(x1, y0 + 1, 1, y1 - y0), inner);
        Hits?.Add(new HitRegion(new Rectangle(0, 0, 320, ScreenBottom), new BackdropTag()));
    }

    /// <summary>The OK / CANCEL buttons (the second state while <paramref name="held"/> is over one); a click is O / C.</summary>
    private static void DrawWindowButtons(SpriteBatch batch, AssetCatalog assets, KeepWindowsLayout layout, Texture2D ok,
        Texture2D cancel, (int X, int Y) okAt, (int X, int Y) cancelAt, Point? held, string? palette = null)
    {
        void Button(Texture2D up, string name, (int X, int Y) at, Keys key)
        {
            var area = new Rectangle(at.X, at.Y, layout.ButtonWidth, layout.ButtonHeight);
            var picture = held is { } p && area.Contains(p)
                && assets.GetInPalette(PictureNames.Button(name, chosen: true), layout.ButtonPalette, palette) is { } down
                ? down : up;
            batch.Draw(picture, new Rectangle(at.X, at.Y, picture.Width, picture.Height), Color.White);
            Hits?.Add(new HitRegion(area) { Click = [key] });
        }
        Button(ok, "ok", okAt, Keys.O);
        Button(cancel, "cancel", cancelAt, Keys.C);
    }

    /// <summary>
    /// A list window of the original's (III Amiga Bank / Trade, s117, <see cref="AmigaWindowView"/>): the box and frame, each
    /// row at its place in the pack's list colours (yellow = a value or the pick; a row too wide for the box is cut at the
    /// frame, never wrapped into the next row), a click on a pickable row presses its key, and OK / CANCEL. False when the
    /// pack lacks the buttons.
    /// </summary>
    public static bool DrawAmigaWindow(SpriteBatch batch, Texture2D pixel, AssetCatalog assets, KeepWindowsLayout layout,
        AmigaWindowView view, Point? held, bool inDungeon = false, ListPage? page = null)
    {
        // s163: in a dungeon the window shows in the dungeon palette (0x3904 keeps it, 8.7): the buttons and colour 8.
        var palette = inDungeon ? layout.DungeonPalette : null;
        if (assets.GetInPalette(PictureNames.Button("ok"), layout.ButtonPalette, palette) is not { } ok
            || assets.GetInPalette(PictureNames.Button("cancel"), layout.ButtonPalette, palette) is not { } cancel)
            return false;
        var w = view.Window;
        var (x0, y0, x1, y1) = (w.Box[0], w.Box[1], w.Box[2], w.Box[3]);
        // A row wider than the box widens it to the right (up to the screen's edge), never cut (s99).
        var widest = view.Rows.Count == 0 ? 0 : view.Rows.Max(r => r.X + PixelFont.Measure(r.Text) + 3);
        x1 = Math.Max(x1, Math.Min(319, widest));
        // Rows that would reach the buttons grow the box (and push the buttons) down, never cut (s99).
        var lastRow = view.Rows.Count == 0 ? 0 : view.Rows.Max(r => r.Y);
        var grow = Math.Max(0, lastRow + 10 - Math.Min(w.Ok.Y, w.Cancel.Y));
        var lift = Math.Max(0, y1 + grow - (ScreenBottom - 1));
        DrawWindowFrame(batch, pixel, layout, x0, y0 - lift, x1, y1 + grow - lift);
        var normal = VirtualScreen.ParseColor(layout.ListColours[0]);
        var bright = VirtualScreen.ParseColor(layout.ListColours[1]);
        if (inDungeon)
            (normal, bright) = (InPalette(assets, normal, MapPalette, DungeonPalette), InPalette(assets, bright, MapPalette, DungeonPalette));
        for (var i = 0; i < view.Rows.Count; i++)
        {
            var row = view.Rows[i];
            PixelFont.Draw(batch, pixel, row.X, row.Y - lift, row.Text, row.Highlight ? bright : normal);
        }
        foreach (var (index, key) in view.Picks)
        {
            var row = view.Rows[index];
            Hits?.Add(new HitRegion(new Rectangle(row.X - 1, row.Y - lift - 1, x1 - 2 - row.X, 8)) { Click = [key] });
        }
        foreach (var (index, cursor) in view.CursorRows ?? [])
        {
            var row = view.Rows[index];
            Hits?.Add(new HitRegion(new Rectangle(row.X - 1, row.Y - lift - 1, x1 - 2 - row.X, 8), new AmigaPromptRowTag(cursor)));
        }
        // A dialog's page of a longer list (the Guild treasury's ledger): a bar beside its rows shows where the page is.
        if (page is not null && page.FirstLine < view.Rows.Count)
        {
            var top = view.Rows[page.FirstLine].Y - lift - 1;
            var rows = Math.Min(page.Shown, view.Rows.Count - page.FirstLine);
            var step = rows > 1 ? view.Rows[page.FirstLine + 1].Y - view.Rows[page.FirstLine].Y : 8; // the rows' own spacing
            var pageBox = new ScrollBox();
            pageBox.Set(page.Total, page.Shown);
            pageBox.ScrollTo(page.Offset);
            var track = ScrollBox.Track(new Rectangle(x0 + 4, top, x1 - 4 - (x0 + 4), rows * step));
            Fill(batch, pixel, track, ScrollTrack);
            Fill(batch, pixel, pageBox.Thumb(track), ScrollThumb);
        }
        DrawWindowButtons(batch, assets, layout, ok, cancel, (w.Ok.X, w.Ok.Y + grow - lift), (w.Cancel.X, w.Cancel.Y + grow - lift),
            held, palette);
        return true;
    }

    /// <summary>The ST scene's text colour (green on black, <c>p2-defeat-great-demon.png</c>).</summary>
    private static readonly Color DefeatTextGreen = new(96, 224, 96);

    /// <summary>The top <paramref name="rows"/> rows of a DECOR part at ST (x, y), clipped to DECOR's rows.</summary>
    private static void DrawDecorPart(SpriteBatch batch, Texture2D? part, int x, int y, int rows)
    {
        if (part is null)
            return;
        var ph = Math.Min(Math.Min(rows, part.Height), DefeatScene.DecorRows - y);
        if (ph > 0)
            batch.Draw(part, new Rectangle(x, y, part.Width, ph), new Rectangle(0, 0, part.Width, ph), Color.White);
    }

    /// <summary>Bottom of the virtual 320×200 screen.</summary>
    public const int ScreenBottom = 200;

    /// <summary>Top of the defeat scene's portrait row: the ST's y 158 (FORMAT.md §10.3), under the whole of DECOR.</summary>
    public const int DefeatRowTop = DefeatScene.RowTop;

    private static void DrawDefeatRow(SpriteBatch batch, AssetCatalog assets, IReadOnlyList<DefeatPortrait> row)
    {
        for (var i = 0; i < row.Count; i++)
        {
            var picture = row[i].Kind switch
            {
                DefeatPortraitKind.Ghost => assets.PortraitGhost,
                DefeatPortraitKind.Race => assets.GetPortraitIdle(row[i].RaceId),
                _ => null,
            };
            if (picture is not null)
                DrawPortrait(batch, picture, new Rectangle(DefeatScene.SlotX(row.Count, i), DefeatRowTop,
                    DefeatScene.PortraitSize, DefeatScene.PortraitSize));
        }
    }

    /// <summary>A numbered prompt menu shows one answer per line up to this many (I's menus never have more).</summary>
    private const int PromptListRows = 6;
    private const int PromptGridColumns = 3;
    private const int PromptGridCellChars = 16;

    /// <summary>
    /// A menu with more answers than <see cref="PromptListRows"/> (II's DNG8 door: "PUSH BUTTON 1 … 9", message 71) in
    /// rows of <see cref="PromptGridColumns"/>, numbered, the highlight on <paramref name="cursor"/>; every answer shows
    /// and can be clicked.
    /// </summary>
    private static void DrawPromptGrid(SpriteBatch batch, Texture2D pixel, SessionPrompt prompt, int cursor,
        IReadOnlyList<string> lines, int maxChars)
    {
        // Three columns while every answer fits its cell, else one answer per line (s99: none is cut).
        var columns = prompt.Options.Select((o, i) => $" {i + 1} {o}").All(t => t.Length <= PromptGridCellChars - 1)
            ? PromptGridColumns : 1;
        var cellChars = columns == 1 ? maxChars : PromptGridCellChars;
        var rows = (prompt.Options.Count + columns - 1) / columns;
        var shown = ScrollPromptText(lines, rows);
        var height = 16 + (shown + rows) * 10 + 4;
        var top = PromptPanelTop(shown, height);
        DrawPanel(batch, pixel, new Rectangle(8, top, 304, height), PanelBg, PanelEdge);
        if (Hits is { } hits)
            hits.Add(new HitRegion(ModalArea, new BackdropTag()) { Click = [] });
        var y = DrawPromptText(batch, pixel, lines, shown, top);
        y += 4;
        for (var i = 0; i < prompt.Options.Count; i++)
        {
            var cellY = y + i / columns * 10;
            var column = i % columns * cellChars;
            var text = $"{(i == cursor ? ">" : " ")}{i + 1} {prompt.Options[i]}";
            PixelFont.Draw(batch, pixel, 14 + column * PixelFont.Advance, cellY, text, i == cursor ? Color.Yellow : BodyText);
            HitCursorRow(TextSpan(14, cellY, column, column + cellChars - 1), cursor, i, Keys.Up, Keys.Down);
        }
        if (Hits is { } wheel)
            wheel.Wheel = (Keys.Up, Keys.Down);
    }

    /// <summary>
    /// The prompt's answers as mouse targets, laid out as <see cref="DrawPrompt"/> draws them: hover moves the highlight
    /// the way the arrows do (<see cref="PromptMenu"/>), a click adds Return; a one-answer box takes a click anywhere.
    /// </summary>
    private static void HitPrompt(SessionPrompt prompt, int cursor, bool single, bool sideBySide, int answersY,
        IReadOnlyList<(int Row, int Start, int End)> pairSpots)
    {
        if (Hits is not { } hits)
            return;
        hits.Add(new HitRegion(ModalArea, new BackdropTag()) { Click = single ? [Keys.Enter] : [] });
        if (single)
            return;
        if (sideBySide)
        {
            for (var i = 0; i < pairSpots.Count; i++)
            {
                var (row, start, end) = pairSpots[i];
                HitCursorRow(TextSpan(14, answersY + row * 10, start, Math.Min(end, 47)), cursor, i, Keys.Left, Keys.Right);
            }
            return;
        }
        for (var i = 0; i < Math.Min(6, prompt.Options.Count); i++)
            HitCursorRow(TextSpan(14, answersY + i * 10, 0, 48), cursor, i, Keys.Up, Keys.Down);
        hits.Wheel = (Keys.Up, Keys.Down);
    }

    /// <summary>Top of a <see cref="DrawTextPanel"/> panel of <paramref name="lineCount"/> lines (its line i is at top + 8 + 10·i).</summary>
    public static int TextPanelTop(int lineCount) =>
        Math.Max(ContentTop + 2, (ContentTop + ContentBottom - (16 + lineCount * 10)) / 2);

    /// <summary>
    /// A text panel's box and text: <see cref="Normal"/> (8 .. 312, text from x 14, 48 columns) or, s183, <see cref="Wide"/>
    /// (the screen's whole width, 51 columns) for a list whose rows run long (Continue's WHICH PARTY?, <see cref="IWidePanel"/>).
    /// </summary>
    public readonly record struct TextPanelFrame(int Left, int Width, int TextX, int MaxChars)
    {
        public static readonly TextPanelFrame Normal = new(8, 304, 14, 48);
        public static readonly TextPanelFrame Wide = new(1, 318, 5, 51);

        public static TextPanelFrame Of(bool wide) => wide ? Wide : Normal;
    }

    /// <summary>A centred modal text panel (DOS save dialog): first line in gold, the rest in body text.</summary>
    public static void DrawTextPanel(SpriteBatch batch, Texture2D pixel, IReadOnlyList<string> lines) =>
        DrawTextPanel(batch, pixel, lines, typing: null);

    /// <summary>
    /// <see cref="DrawTextPanel(SpriteBatch, Texture2D, IReadOnlyList{string})"/> with a <paramref name="typing"/> grid
    /// under the text (one panel line per grid row); null draws exactly the plain panel. <paramref name="wide"/>: the
    /// <see cref="TextPanelFrame.Wide"/> box.
    /// </summary>
    public static void DrawTextPanel(SpriteBatch batch, Texture2D pixel, IReadOnlyList<string> lines, TypingGrid? typing,
        ListPage? page = null, bool wide = false)
    {
        var frame = TextPanelFrame.Of(wide);
        // Each line whole (s99): a line longer than the panel goes on, at a word, on the next row; a panel with more rows
        // than the screen holds scrolls (PgUp / PgDn where the dialog leaves them free, the wheel, the bar).
        var gridRows = typing?.Rows.Count ?? 0;
        var (rows, shown, top, height) = TextPanelLayout(lines, gridRows, wide);
        DrawPanel(batch, pixel, new Rectangle(frame.Left, top, frame.Width, height), PanelBg, PanelEdge);
        int? LineTop(int line) => TextPanelLineTop(lines, line, gridRows, wide);
        HitPanel(lines, frame.TextX, top + 8, 10, frame.MaxChars, LineTop);
        var area = new Rectangle(frame.Left + 4, top + 7, frame.Width - 8, shown * 10);
        HitScrollArea(TextPanelScroll, area);
        var y = top + 8;
        for (var i = 0; i < shown && TextPanelScroll.Offset + i < rows.Count; i++)
        {
            var (text, line) = rows[TextPanelScroll.Offset + i];
            PixelFont.Draw(batch, pixel, frame.TextX, y, text, line == 0 ? TitleGold : BodyText);
            y += 10;
        }
        DrawScrollBar(batch, pixel, TextPanelScroll, area);
        // A dialog's own page of a longer list: a bar beside its rows shows where the page is (the arrows and the wheel
        // page it, as the help line says).
        if (page is not null && !TextPanelScroll.CanScroll && LineTop(page.FirstLine) is int pageTop)
        {
            var pageBox = new ScrollBox();
            pageBox.Set(page.Total, page.Shown);
            pageBox.ScrollTo(page.Offset);
            var track = ScrollBox.Track(new Rectangle(frame.Left + 4, pageTop - 1, frame.Width - 8,
                Math.Min(page.Shown, lines.Count - page.FirstLine) * 10));
            Fill(batch, pixel, track, ScrollTrack);
            Fill(batch, pixel, pageBox.Thumb(track), ScrollThumb);
        }
        if (typing is not null)
            DrawTypingGrid(batch, pixel, typing, frame.TextX, y);
    }

    /// <summary>
    /// A text panel's rows (each line wrapped, with the line it belongs to), how many show, the panel's top and height;
    /// sets <see cref="TextPanelScroll"/> for them (a new panel starts at its top).
    /// </summary>
    public static (List<(string Text, int Line)> Rows, int Shown, int Top, int Height) TextPanelLayout(
        IReadOnlyList<string> lines, int gridRows = 0, bool wide = false)
    {
        var maxChars = TextPanelFrame.Of(wide).MaxChars;
        var rows = lines.SelectMany((l, i) => Lines(l, maxChars).Select(r => (Text: r, Line: i))).ToList();
        var shown = Math.Min(rows.Count, Math.Max(1, PromptPanelRows + 1 - gridRows));
        TextPanelScroll.Set(rows.Count, shown);
        TextPanelScroll.TopFor(HashCode.Combine(lines.Count > 0 ? lines[0] : "", lines.Count));
        var height = 16 + (shown + gridRows) * 10;
        var top = Math.Max(ContentTop + 2, (ContentTop + ContentBottom - height) / 2);
        return (rows, shown, top, height);
    }

    /// <summary>Where a text panel draws line <paramref name="line"/>'s first row, or null when it is scrolled out of view.</summary>
    public static int? TextPanelLineTop(IReadOnlyList<string> lines, int line, int gridRows = 0, bool wide = false)
    {
        var (rows, shown, top, _) = TextPanelLayout(lines, gridRows, wide);
        var row = rows.FindIndex(r => r.Line == line) - TextPanelScroll.Offset;
        return row >= 0 && row < shown ? top + 8 + row * 10 : null;
    }

    /// <summary>A modal text panel's rows (<see cref="DrawTextPanel(SpriteBatch, Texture2D, IReadOnlyList{string}, TypingGrid?, ListPage?)"/>) when they run past the screen.</summary>
    public static ScrollBox TextPanelScroll { get; } = new();

    /// <summary>
    /// A text scroll on the pack's parchment (III Amiga <c>0x1e2c2</c>): the whole screen, the frame pieces at
    /// <paramref name="edges"/> (picked once per reading, <see cref="ScrollFrameLayout.Pick"/>), the inside filled, the
    /// lines from the inside's top-left in the frame's text colour (the first line, the scroll's name, too). A
    /// <paramref name="footer"/> (III's event page: "Press a key" / "Press a number", Amiga <c>0x1d9dc</c>) sits at text
    /// cell (14, 23) and the rows scroll above it; such a page opens on its newest rows (the Amiga scrolls the inside so
    /// the latest text shows), and with <paramref name="anyKey"/> a click anywhere is Return ("Press a key").
    /// </summary>
    public static void DrawScrollFrame(SpriteBatch batch, Texture2D pixel, AssetCatalog assets, ScrollFrameLayout frame,
        IReadOnlyList<ScrollFrameLayout.Edge> edges, IReadOnlyList<string> lines, string? footer = null, bool anyKey = false)
    {
        Fill(batch, pixel, new Rectangle(0, 0, 320, 200), Color.Black);
        foreach (var e in edges)
            if (assets.GetScrollFramePiece(e.Slot) is { } piece)
                batch.Draw(piece, new Vector2(e.Column * frame.Cell, e.Row * frame.Cell), Color.White);
        var inside = new Rectangle(frame.Fill[0], frame.Fill[1], frame.Fill[2], frame.Fill[3]);
        Fill(batch, pixel, inside, VirtualScreen.ParseColor(frame.FillColour));
        var ink = VirtualScreen.ParseColor(frame.TextColour);
        var maxChars = Math.Max(1, (inside.Width - 4) / PixelFont.Advance);
        // Each line whole, wrapped at a word when wider than the parchment, and the rows scroll past its foot (s99).
        var rows = lines.SelectMany((l, i) => Lines(l, maxChars).Select(r => (Text: r, Line: i))).ToList();
        var shown = Math.Max(1, Math.Min(rows.Count, (inside.Height - 2 - (footer is null ? 0 : FooterRoom)) / 10));
        ScrollFrameScroll.Set(rows.Count, shown);
        if (ScrollFrameScroll.TopFor(string.Join("\n", lines).GetHashCode()) && footer is not null)
            ScrollFrameScroll.End(); // the event page follows its newest rows
        int? LineTop(int line)
        {
            var row = rows.FindIndex(r => r.Line == line) - ScrollFrameScroll.Offset;
            return row >= 0 && row < shown ? inside.Y + 2 + row * 10 : null;
        }
        HitPanel(lines, inside.X + 2, inside.Y + 2, 10, maxChars, LineTop);
        if (anyKey)
            Hits?.Add(new HitRegion(new Rectangle(0, 0, 320, ScreenBottom), new BackdropTag()) { Click = [Keys.Enter] });
        var area = new Rectangle(inside.X, inside.Y + 1, inside.Width, shown * 10);
        HitScrollArea(ScrollFrameScroll, area);
        for (var i = 0; i < shown && ScrollFrameScroll.Offset + i < rows.Count; i++)
            PixelFont.Draw(batch, pixel, inside.X + 2, inside.Y + 2 + i * 10, rows[ScrollFrameScroll.Offset + i].Text, ink);
        DrawScrollBar(batch, pixel, ScrollFrameScroll, area);
        if (footer is not null) // s192 (pad fix C18): a longer footer (the pad's pick) moves left to stay on the parchment
            PixelFont.Draw(batch, pixel, Math.Max(inside.X + 2, Math.Min(FooterColumn * frame.Cell,
                inside.Right - 2 - PixelFont.Measure(footer))), FooterRow * frame.Cell, footer, ink);
    }

    /// <summary>III's event-page footer at text cell (14, 23) (Amiga <c>0x1d9dc</c>), and the room kept free for it.</summary>
    private const int FooterColumn = 14, FooterRow = 23, FooterRoom = 12;

    /// <summary>The parchment's rows (<see cref="DrawScrollFrame"/>) when they run past its foot.</summary>
    public static ScrollBox ScrollFrameScroll { get; } = new();

    /// <summary>The typing grid's rows from (x, y), 10 pixels a row; each cell is a mouse target that presses its key.</summary>
    private static void DrawTypingGrid(SpriteBatch batch, Texture2D pixel, TypingGrid grid, int x, int y)
    {
        // Over the panel's own targets: a click between cells does nothing (an "any key" panel would take it as Return).
        Hits?.Add(new Rectangle(x - 2, y - 1, TypingGrid.RowColumns * PixelFont.Advance + 4, grid.Rows.Count * 10), new BackdropTag());
        foreach (var row in grid.Rows)
        {
            foreach (var cell in row)
            {
                PixelFont.Draw(batch, pixel, x + cell.X, y, cell.Label, Accent);
                Hits?.Add(new HitRegion(new Rectangle(x + cell.X - 1, y - 1, cell.Width, 10)) { Click = [cell.Key] });
            }
            y += 10;
        }
    }

    /// <summary>
    /// A full-screen remaster menu (T5.0 main menu and Options): the first line is the title (double size, centred), the
    /// rest one per 10 rows, and <paramref name="help"/> on the bottom row. Drawn over everything.
    /// </summary>
    public static void DrawMenuScreen(SpriteBatch batch, Texture2D pixel, IReadOnlyList<MenuLine> lines, string help) =>
        DrawMenuScreen(batch, pixel, lines, help, top: 30, helpY: 186);

    /// <summary>
    /// The Options screen: its list (a little higher than a plain menu screen) and below it a box describing the row under
    /// the cursor, wrapped to the box; a longer text scrolls ([ / ], the wheel over the box, its bar) and starts at its top
    /// for each row.
    /// </summary>
    public static void DrawOptionsScreen(SpriteBatch batch, Texture2D pixel, OptionsScreen options)
    {
        DrawMenuScreen(batch, pixel, options.Lines(), options.Help, top: OptionsLayout.ListTop, helpY: OptionsLayout.HelpY);
        var area = new Rectangle(OptionsLayout.DetailX, OptionsLayout.DetailTop, OptionsLayout.DetailWidth, OptionsLayout.DetailHeight);
        DrawPanel(batch, pixel, area, PanelBg, PanelEdge);
        var lines = new List<(string Text, Color Color)>();
        foreach (var paragraph in options.Detail())
        {
            var color = paragraph.Style switch
            {
                MenuLineStyle.Header => TitleGold,
                MenuLineStyle.Dim => DimText,
                _ => BodyText,
            };
            foreach (var part in Wrap(paragraph.Text, OptionsLayout.DetailColumns))
                lines.Add((part, color));
        }
        var box = options.DetailBox;
        box.Set(lines.Count, OptionsLayout.DetailLines);
        box.TopFor(options.DetailKey);
        HitScrollArea(box, area);
        for (var i = 0; i < OptionsLayout.DetailLines && box.Offset + i < lines.Count; i++)
            PixelFont.Draw(batch, pixel, area.X + 4, area.Y + 3 + i * PixelFont.LineHeight, lines[box.Offset + i].Text,
                lines[box.Offset + i].Color);
        DrawScrollBar(batch, pixel, box, new Rectangle(area.X, area.Y + 1, area.Width - 1, area.Height - 2));
    }

    /// <param name="scroll">s185: a scrolling box among the lines (the import screen's rows): the wheel over its area
    /// scrolls it, under the lines' own hover and clicks (added before them).</param>
    private static void DrawMenuScreen(
        SpriteBatch batch, Texture2D pixel, IReadOnlyList<MenuLine> lines, string help, int top, int helpY,
        (ScrollBox Box, Rectangle Area)? scroll = null)
    {
        Fill(batch, pixel, new Rectangle(0, 0, 320, 200), new Color(10, 14, 24));
        DrawPanel(batch, pixel, new Rectangle(6, 4, 308, 192), PanelBg, PanelEdge);
        if (Hits is { } hits)
        {
            hits.Add(new Rectangle(0, 0, 320, 200), new BackdropTag());
            hits.Wheel = (Keys.Up, Keys.Down);
        }
        if (scroll is var (scrollBox, scrollArea))
            HitScrollArea(scrollBox, scrollArea);
        var y = top;
        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            if (i == 0 && line.Style == MenuLineStyle.Title)
            {
                PixelFont.Draw(batch, pixel, 160 - PixelFont.Measure(line.Text), 10, line.Text, TitleGold, scale: 2); // centred at scale 2
                continue;
            }
            var color = line.Style switch
            {
                MenuLineStyle.Title or MenuLineStyle.Header => TitleGold,
                MenuLineStyle.Selected => Accent,
                MenuLineStyle.Dim => DimText,
                _ => BodyText,
            };
            PixelFont.Draw(batch, pixel, 14, y, line.Text, color);
            Hits?.Add(TextSpan(14, y, line.Text, 0, 49), new MenuLineTag(i, line.Text));
            y += 10;
        }
        PixelFont.Draw(batch, pixel, 14, helpY, PadWording.Help(help), DimText); // s183: the pad's wording, pad in use
    }

    public static void DrawInventory(
        SpriteBatch batch,
        Texture2D pixel,
        GameSession session,
        GameDataRepository data,
        int selectedIndex,
        bool compareMode)
    {
        DrawPanel(batch, pixel, new Rectangle(8, 18, 240, 142), PanelBg, PanelEdge);
        if (Hits is { } hits)
        {
            hits.Add(ModalArea, new BackdropTag());
            hits.Wheel = (Keys.Up, Keys.Down);
        }
        PixelFont.Draw(batch, pixel, 14, 24, PadWording.S("ui.inventory_v"), TitleGold); // s193 (E30): "(X)" for the pad
        PixelFont.Draw(batch, pixel, 14, 34, PadWording.Help(UiText.S("ui.arrows_select_enter_compare")), DimText); // s185

        // Every member's lines and items in one list that scrolls with the cursor (s99: it used to stop at the panel's
        // foot, leaving later members out). Rows keep their pitches: 10 for a member's two lines, 9 for an item.
        var rows = new List<(string Text, int X, int Height, Color Color, int Index, Character Owner, int ItemId)>();
        var idx = 0;
        foreach (var ch in session.Party.Members)
        {
            rows.Add((UiText.F("ui.x_hp_x_x", ch.Name, ch.HpCur, ch.HpMax), 14, 10, Color.White, -1, ch, -1));
            rows.Add((UiText.F("ui.eq_w_x_a_x_s", ch.WornWeapon, ch.WornArmor, ch.WornShield), 18, 10, DimText, -1, ch, -1));
            foreach (var (id, name) in ch.InventoryItemIds
                         .Where(id => id != 0) // empty slot (s58)
                         .Select(id => (id, name: ItemName(data, id)))
                         .OrderBy(t => t.name, StringComparer.OrdinalIgnoreCase))
            {
                var selected = idx == selectedIndex;
                rows.Add(($"{(selected ? ">" : " ")}{name}", 20, 9, selected ? Color.Yellow : BodyText, idx, ch, id));
                idx++;
            }
        }
        var compare = compareMode && rows.FirstOrDefault(r => r.Index == selectedIndex) is { ItemId: >= 0 } picked
            ? picked : default;
        const int listTop = 48;
        const int lastRowTop = 145; // as before: no row starts below it
        int FitsFrom(int first)
        {
            var (n, y) = (0, listTop);
            for (var i = first; i < rows.Count && y <= lastRowTop; i++, n++)
                y += rows[i].Height;
            return Math.Max(1, n);
        }
        var box = ItemListScroll;
        box.Set(rows.Count, FitsFrom(box.Offset));
        box.Follow(Math.Max(0, rows.FindIndex(r => r.Index == selectedIndex)));
        box.Set(rows.Count, FitsFrom(box.Offset));
        var area = new Rectangle(12, listTop - 1, 234, lastRowTop + 9 - listTop);
        HitScrollArea(box, area);
        var rowY = listTop;
        for (var i = box.Offset; i < box.Offset + box.Visible && i < rows.Count; i++)
        {
            var row = rows[i];
            PixelFont.Draw(batch, pixel, row.X, rowY, row.Text, row.Color);
            if (row.Index >= 0)
                Hits?.Add(new Rectangle(18, rowY - 1, 190, 9), new InventoryRowTag(row.Index));
            rowY += row.Height;
        }
        DrawScrollBar(batch, pixel, box, area);

        if (compare.Owner is not null)
        {
            var entry = data.Items.FirstOrDefault(i => i.Id == compare.ItemId);
            PixelFont.Draw(batch, pixel, 14, 148,
                UiText.F("ui.cmp_pts_x_vs_worn_w", entry?.Points?.ToString() ?? "?", compare.Owner.WornWeapon),
                Accent);
        }
    }

    /// <summary>The item overlay's list (<see cref="DrawInventory"/>).</summary>
    public static ScrollBox ItemListScroll { get; } = new();

    /// <summary>The bestiary's list (<see cref="DrawBestiary"/>): the arrows, the wheel and its bar scroll it.</summary>
    public static ScrollBox BestiaryScroll { get; } = new();

    public static void DrawBestiary(
        SpriteBatch batch,
        Texture2D pixel,
        AssetCatalog assets,
        GameDataRepository data,
        QoLProgress qol)
    {
        DrawPanel(batch, pixel, new Rectangle(8, 18, 280, 142), new Color(28, 16, 16, 240), new Color(160, 90, 90));
        if (Hits is { } hits)
        {
            hits.Add(ModalArea, new BackdropTag());
            hits.Wheel = (Keys.Up, Keys.Down);
        }
        PixelFont.Draw(batch, pixel, 14, 24, PadWording.S("ui.bestiary_b"), new Color(220, 140, 140)); // s193 (E30): "(Y)"
        PixelFont.Draw(batch, pixel, 14, 34, UiText.F("ui.x_recorded", qol.SeenMonsterIds.Count), DimText);

        var seen = data.Monsters.Where(m => qol.SeenMonsterIds.Contains(m.Id)).OrderBy(m => m.Name).ToList();
        const int listTop = 48;
        const int listBottom = 157;
        if (seen.Count == 0)
        {
            PixelFont.Draw(batch, pixel, 14, listTop, UiText.S("ui.no_encounters_yet_fight_to_unlock"), DimText);
            return;
        }

        // Each monster's picture 1:1 (s99: no art scaled down or out of shape), its row as tall as the picture needs;
        // as many rows as fit from the scroll offset.
        int RowHeight(int i) => Math.Max(38, (assets.GetMonsterIdle(seen[i].Id)?.Height ?? 34) + 4);
        var box = BestiaryScroll;
        box.Set(seen.Count, 1);
        var fits = 0;
        for (var h = 0; box.Offset + fits < seen.Count && h + RowHeight(box.Offset + fits) <= listBottom - listTop; fits++)
            h += RowHeight(box.Offset + fits);
        box.Set(seen.Count, Math.Max(1, fits));
        var area = new Rectangle(12, listTop, 272, listBottom - listTop);
        HitScrollArea(box, area);
        var y = listTop;
        for (var i = box.Offset; i < box.Offset + Math.Max(1, fits) && i < seen.Count; i++)
        {
            var m = seen[i];
            var sprite = assets.GetMonsterIdle(m.Id);
            var pictureWidth = sprite?.Width ?? 32;
            if (sprite is not null)
                batch.Draw(sprite, new Rectangle(14, y, sprite.Width, sprite.Height), Color.White);
            else
                Fill(batch, pixel, new Rectangle(14, y, 32, 34), new Color(60, 30, 30));
            var textX = Math.Max(52, 14 + pictureWidth + 6);
            PixelFont.Draw(batch, pixel, textX, y + 4, m.Name, Color.White);
            PixelFont.Draw(batch, pixel, textX, y + 16,
                UiText.F("ui.cv_x_hp_x_lv_x", m.CombatRating, m.HpBase, m.Level),
                new Color(180, 150, 150));
            y += RowHeight(i);
        }
        DrawScrollBar(batch, pixel, box, area);
    }

    /// <summary>The rows <see cref="DrawTooltip"/> clears: the help line (167…<see cref="MessageY"/> − 2) for one line, up to the message for two.</summary>
    public static Rectangle TooltipBand(int lines) =>
        lines > 1 ? new Rectangle(0, 167, 320, MessageY - 167) : new Rectangle(0, 167, 320, MessageY - 1 - 167);

    public static void DrawTooltip(SpriteBatch batch, Texture2D pixel, string text)
    {
        if (string.IsNullOrEmpty(text)) return;
        // Tooltip shares the help strip during combat; a longer one takes a second line, closed up above the message.
        var lines = Lines(text, 52);
        var pitch = lines.Count > 1 ? 7 : 0;
        var top = lines.Count > 1 ? HelpY - 1 : HelpY;
        Hits?.Add(new Rectangle(0, top - 1, 320, 10 + pitch * (lines.Count - 1))); // covers the help line's keys
        // Clear the help line under it (s114: a one-line tooltip was drawn over the round's key help, both unreadable).
        Fill(batch, pixel, TooltipBand(lines.Count), new Color(6, 8, 14));
        for (var i = 0; i < lines.Count; i++)
            PixelFont.Draw(batch, pixel, 4, top + i * (pitch + 1), lines[i], new Color(200, 220, 255));
    }

    /// <summary>Where <see cref="DrawDungeonOverview"/> puts the whole-level box: its frame, the cell grid inside it and one cell's size.</summary>
    public readonly record struct TileOverviewLayout(Rectangle Box, Rectangle Grid, int CellWidth, int CellHeight);

    /// <summary>
    /// s171 (PLAN P3, QoL): the whole-level overview over a dungeon view: III's tile dungeon (24 × 30 level, 20 columns of
    /// it shown), s172 also I / II's glyph view (33 × 38 level, 15 × 18 of it shown at 10 × 8 px) and the plain fallback
    /// grid. It sits inside <paramref name="view"/> left of the member cards (<see cref="FieldPartyArea"/>), so it covers
    /// no card and no command line, in the view's corner farthest from <paramref name="partyCell"/> (the party's glyph on
    /// screen), never over it. Cells are as large as fit in half the view each way (III DOS / Amiga: 3 × 2 px, a 76 × 64
    /// box; I Amiga: 2 × 1 px, a 70 × 42 box), so the whole level shows and a corner always clears the party. (s170 and
    /// before, I / II drew a see-through box at (222, 16), over member cards 2, 3 and 5.)
    /// </summary>
    public static TileOverviewLayout TileOverviewPlacement(Rectangle view, Rectangle partyCell, int gridWidth, int gridHeight)
    {
        var area = new Rectangle(view.X, view.Y, Math.Min(view.Width, FieldPartyArea.X - 1 - view.X), view.Height);
        var cw = Math.Max(1, (area.Width / 2 - 4) / Math.Max(1, gridWidth));
        var ch = Math.Max(1, (area.Height / 2 - 4) / Math.Max(1, gridHeight));
        var w = gridWidth * cw + 4;
        var h = gridHeight * ch + 4;
        var centre = partyCell.Center;
        var right = centre.X < area.Center.X;
        var bottom = centre.Y < area.Center.Y;
        Rectangle At(bool r, bool b) =>
            new(r ? area.Right - w : area.X, b ? area.Bottom - h : area.Y, w, h);
        // Farthest corner first; then the one across the other way, should the party's glyph straddle the middle.
        var box = new[] { At(right, bottom), At(!right, bottom), At(right, !bottom), At(!right, !bottom) }
            .FirstOrDefault(r => !r.Intersects(partyCell), At(right, bottom));
        return new TileOverviewLayout(box, new Rectangle(box.X + 2, box.Y + 2, gridWidth * cw, gridHeight * ch), cw, ch);
    }

    /// <summary>The colour a glyph shows most (transparent pixels left out): its cell's colour in the overview box.</summary>
    public static Color DominantColour(ReadOnlySpan<Color> pixels, Color fallback)
    {
        var counts = new Dictionary<Color, int>();
        foreach (var c in pixels)
            if (c.A != 0)
                counts[c] = counts.GetValueOrDefault(c) + 1;
        return counts.Count == 0 ? fallback : counts.MaxBy(kv => kv.Value).Key;
    }

    /// <summary>The overview's party cell: cyan, which every pack's palette keeps apart from its glyph colours (CGA: index 1).</summary>
    public static readonly Color OverviewParty = Color.Cyan;

    /// <summary>The overview's fill (cells not yet seen): opaque black, CGA's colour 0.</summary>
    public static readonly Color OverviewFill = Color.Black;

    /// <summary>The overview's frame (<see cref="PanelEdge"/>).</summary>
    public static Color OverviewEdge => PanelEdge;

    /// <summary>
    /// s171: the dungeon's whole-level overview (<see cref="TileOverviewPlacement"/>), the auto-map switch
    /// (<see cref="GameSettings.AutoMap"/>, the Options row and the Tab key). A seen cell takes <paramref name="cellColour"/>
    /// of its map byte (the colour its glyph shows most, so it follows the pack's palette: III DOS CGA stone white, floor
    /// magenta), a cell not seen stays the fill, the party is <see cref="OverviewParty"/>. The box is opaque. A click on a
    /// cell walks there, as on the view; a click on the frame does nothing (not the view's cell under it).
    /// </summary>
    public static void DrawDungeonOverview(SpriteBatch batch, Texture2D pixel, DungeonLevel level, DungeonState state,
        QoLProgress qol, Func<int, Color> cellColour, TileOverviewLayout layout)
    {
        DrawPanel(batch, pixel, layout.Box, OverviewFill, OverviewEdge);
        foreach (var (x, y) in qol.VisitedCells(state.LevelId))
        {
            if (!level.InBounds(x, y)) continue;
            batch.Draw(pixel, new Rectangle(layout.Grid.X + x * layout.CellWidth, layout.Grid.Y + y * layout.CellHeight,
                layout.CellWidth, layout.CellHeight), cellColour(level.Get(x, y)));
        }
        batch.Draw(pixel, new Rectangle(layout.Grid.X + state.X * layout.CellWidth, layout.Grid.Y + state.Y * layout.CellHeight,
            layout.CellWidth, layout.CellHeight), OverviewParty);
        Hits?.Add(layout.Box); // the frame takes a click, so none walks to the view's cell under it
        Hits?.Add(layout.Grid, new MapViewTag(0, 0, layout.CellWidth, layout.CellHeight));
    }

    public static List<(int Id, string Name, int Qty, int Price)> BuildTownStock(
        GameSession session,
        GameDataRepository data)
    {
        var town = session.CurrentTown;
        var list = new List<(int, string, int, int)>();
        if (town is null) return list;

        for (var id = TownState.FirstListedItemId; id < town.Stock.Length; id++)
        {
            var qty = town.Stock[id];
            if (qty <= 0) continue;
            var item = data.Items.FirstOrDefault(i => i.Id == id);
            if (item is null) continue;
            var price = TownMenus.ListBuyPrice(
                new ItemCatalogEntry { Id = id, Name = item.Name, ValueFactor = item.ValueFactor, Value = item.Value },
                town);
            list.Add((id, item.Name, qty, price));
        }

        return list;
    }

    private static string ItemName(GameDataRepository data, int id)
    {
        var item = data.Items.FirstOrDefault(i => i.Id == id);
        return item?.Name ?? $"#{id}";
    }

    /// <summary><paramref name="s"/> cut with "." to <paramref name="maxChars"/> layout columns (the font's widths; one character stays).</summary>
    private static string Fit(string s, int maxChars) =>
        string.IsNullOrEmpty(s) ? "" : PixelFont.Fit(s, maxChars * PixelFont.Advance, minChars: 1);


    /// <summary>The screen the current Draw batches on (set by the shell), or null (tests, headless): no clipping then.</summary>
    public static VirtualScreen? Screen { get; set; }

    /// <summary>
    /// Run <paramref name="draw"/> with everything it draws cut off at <paramref name="clip"/>'s edges (a box scrolled by
    /// pixels, its content running past top and bottom). The batch is ended and begun again around it.
    /// </summary>
    public static void Clipped(SpriteBatch batch, Rectangle clip, Action draw)
    {
        if (Screen is not { } screen)
        {
            draw();
            return;
        }
        batch.End();
        screen.BeginLayoutClipped(batch, clip);
        var outer = _clip;
        _clip = clip;
        try
        {
            draw();
        }
        finally
        {
            _clip = outer;
        }
        batch.End();
        if (outer is { } o)
            screen.BeginLayoutClipped(batch, o);
        else
            screen.BeginLayout(batch);
    }

    /// <summary>The clip of the <see cref="Clipped"/> draw running now, or null.</summary>
    private static Rectangle? _clip;

    /// <summary>
    /// s170: III DOS's OR blit (root <c>0x284:0x19c</c>): the screen's colour ORed with the frame's. In CGA's palettes every
    /// channel is 0x00, 0x55 or 0xFF, so the OR is the per-channel maximum (<see cref="FlashBlends.Or"/>): colour 0 leaves the
    /// screen as it is, green 1 over red 2 gives yellow 3, as the index OR.
    /// </summary>
    public static readonly BlendState OrBlit = new()
    {
        Name = "OrBlit",
        ColorBlendFunction = BlendFunction.Max,
        AlphaBlendFunction = BlendFunction.Max,
        ColorSourceBlend = Blend.One,
        ColorDestinationBlend = Blend.One,
        AlphaSourceBlend = Blend.One,
        AlphaDestinationBlend = Blend.One,
    };

    /// <summary>
    /// Run <paramref name="draw"/> with <paramref name="blend"/>'s blend state (<see cref="FlashBlend.Or"/>:
    /// <see cref="OrBlit"/>; a masked blit draws as everything else does), keeping the clip of a <see cref="Clipped"/>
    /// draw around it. The batch is ended and begun again around it.
    /// </summary>
    public static void Blended(SpriteBatch batch, FlashBlend blend, Action draw)
    {
        if (blend != FlashBlend.Or || Screen is not { } screen)
        {
            draw();
            return;
        }
        batch.End();
        if (_clip is { } c)
            screen.BeginLayoutClipped(batch, c, OrBlit);
        else
            screen.BeginLayout(batch, OrBlit);
        draw();
        batch.End();
        if (_clip is { } back)
            screen.BeginLayoutClipped(batch, back);
        else
            screen.BeginLayout(batch);
    }

    private static readonly Color ScrollTrack = new(40, 48, 66);
    private static readonly Color ScrollThumb = new(150, 160, 190);

    /// <summary>
    /// <paramref name="box"/> as the wheel's target over <paramref name="area"/>; register it before the box's rows so their
    /// own clicks stay on top.
    /// </summary>
    public static void HitScrollArea(ScrollBox box, Rectangle area)
    {
        if (box.CanScroll)
            Hits?.Add(area, new ScrollAreaTag(box));
    }

    /// <summary>
    /// The bar of <paramref name="box"/> on the right edge of <paramref name="area"/> (<see cref="ScrollBox.Track"/>) when it
    /// has more rows than fit: the track, the thumb, and a mouse target over the track (a click jumps, a drag follows).
    /// Nothing is drawn when everything fits.
    /// </summary>
    public static void DrawScrollBar(SpriteBatch batch, Texture2D pixel, ScrollBox box, Rectangle area)
    {
        if (!box.CanScroll)
            return;
        var track = ScrollBox.Track(area);
        Fill(batch, pixel, track, ScrollTrack);
        Fill(batch, pixel, box.Thumb(track), ScrollThumb);
        // A slightly wider target than the 3-pixel bar, so it can be hit.
        Hits?.Add(new Rectangle(track.X - 2, track.Y, track.Width + 3, track.Height), new ScrollBarTag(box, track));
    }

    private static void DrawPanel(SpriteBatch batch, Texture2D pixel, Rectangle r, Color fill, Color edge)
    {
        Fill(batch, pixel, r, fill);
        HLine(batch, pixel, r.X, r.Y, r.Width, edge);
        HLine(batch, pixel, r.X, r.Bottom - 1, r.Width, edge);
        VLine(batch, pixel, r.X, r.Y, r.Height, edge);
        VLine(batch, pixel, r.Right - 1, r.Y, r.Height, edge);
    }

    private static void Fill(SpriteBatch batch, Texture2D pixel, Rectangle r, Color c) =>
        batch.Draw(pixel, r, c);

    private static void HLine(SpriteBatch batch, Texture2D pixel, int x, int y, int w, Color c) =>
        batch.Draw(pixel, new Rectangle(x, y, w, 1), c);

    private static void VLine(SpriteBatch batch, Texture2D pixel, int x, int y, int h, Color c) =>
        batch.Draw(pixel, new Rectangle(x, y, 1, h), c);
}
