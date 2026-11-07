using Microsoft.Xna.Framework.Input;
using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client.Tests;

/// <summary>
/// Mouse leftovers (PLAN P3, after s74): typed entry gets a clickable character grid, and mouse walks in a dungeon go
/// through the 0x01 corridor (drawn with the wall glyph) by the cell's real passability.
/// </summary>
public class MouseLeftoversTests
{
    // --- typing grid -------------------------------------------------------------------

    [OriginalDataFact]
    public void NameGrid_HasEveryNameKey_AndSpaceDelOk_InThreeRows_ThatFitThePanel()
    {
        var grid = TypingGrid.For(NameEntry.Alphabet);
        Assert.Equal(3, grid.Rows.Count);
        var keys = grid.Rows.SelectMany(r => r).Select(c => c.Key).ToList();
        Assert.Equal(Enumerable.Range(0, 26).Select(i => Keys.A + i), keys.Take(26));
        Assert.Equal(Enumerable.Range(0, 10).Select(i => Keys.D0 + i), keys.Skip(26).Take(10));
        Assert.Equal([Keys.Space, Keys.Back, Keys.Enter], keys.Skip(36));
        Assert.Equal(["SPACE", "DEL", "OK"], grid.Rows[^1].TakeLast(3).Select(c => c.Label));
        foreach (var row in grid.Rows)
        {
            Assert.True(row[^1].X + row[^1].Width <= TypingGrid.RowColumns * PixelFont.Advance);
            Assert.All(row.Zip(row.Skip(1)), p => Assert.Equal(p.First.X + p.First.Width, p.Second.X)); // no overlap
        }
    }

    [Fact]
    public void DigitGrid_IsOneRow_WithoutSpace()
    {
        var grid = TypingGrid.For("0123456789");
        Assert.Single(grid.Rows);
        Assert.Equal([.. Enumerable.Range(0, 10).Select(i => Keys.D0 + i), Keys.Back, Keys.Enter],
            grid.Rows[0].Select(c => c.Key));
    }

    [OriginalDataFact]
    public void GuildNew_NameStep_IsTypedByTheGridsKeys_AndFitsThePanel()
    {
        var session = GameSessionFactory.Create(ClientSessions.Data, new SeededDosRandomSource(3));
        session.OpenGuild();
        var d = new GuildNewDialog();
        Assert.Null(d.Open(session));
        Assert.Null(d.TypingAlphabet); // the race menu takes no typing
        d.HandleKeys([Keys.D1], session);
        d.HandleKeys([Keys.D1], session);
        Assert.Null(d.TypingAlphabet);
        d.HandleKeys([Keys.Y], session);
        Assert.Equal(NameEntry.Alphabet, d.TypingAlphabet);

        var grid = TypingGrid.For(d.TypingAlphabet!);
        Assert.True(d.Lines(session, ClientSessions.Data).Count + grid.Rows.Count <= InspectDialog.MaxLines);

        Keys Cell(string label) => grid.Rows.SelectMany(r => r).Single(c => c.Label == label).Key;
        d.HandleKeys([Cell("Z"), Cell("A"), Cell("P"), Cell("SPACE"), Cell("7"), Cell("X"), Cell("DEL")], session);
        Assert.Contains(d.Lines(session, ClientSessions.Data), l => l.EndsWith("ZAP 7_"));
        d.HandleKeys([Cell("OK")], session);
        Assert.Null(d.TypingAlphabet); // accepted: the sheet
        Assert.Contains(session.Roster.Occupied, c => c.Name == "ZAP 7");
    }

    [OriginalDataFact]
    public void GuildRename_TakesTypingOnlyWhileAskingTheName()
    {
        var session = GameSessionFactory.Create(ClientSessions.Data, new SeededDosRandomSource(3));
        session.OpenGuild();
        session.CreateAndEnroll("OLDNAME", Race.Human, CharacterClass.Fighter);
        var d = new GuildPickDialog();
        Assert.Null(d.OpenRename(session));
        Assert.Null(d.TypingAlphabet);
        d.HandleKeys([Keys.Enter], session);
        Assert.Equal(NameEntry.Alphabet, d.TypingAlphabet);
        d.HandleKeys([Keys.Escape], session);
        Assert.Null(d.TypingAlphabet);
    }

    [OriginalDataFact]
    public void Continue_GuildNumberQuestion_TakesDigits()
    {
        var d = new ContinueDialog();
        Assert.Null(d.TypingAlphabet);
        var s = ClientSessions.Overworld();
        d.Open(s.CaptureSnapshot());
        Assert.Equal("0123456789", d.TypingAlphabet);
        Assert.Single(TypingGrid.For(d.TypingAlphabet!).Rows);
    }

    // --- dungeon walk over the 0x01 corridor --------------------------------------------------

    [Fact]
    public void DungeonWalk_GoesThroughTheWallGlyphCorridor_ButNotWallsOrUndrawnCells()
    {
        var cells = new byte[DungeonLevel.GridByteLength];
        for (var x = 1; x <= 8; x++)
            cells[3 * DungeonLevel.Width + x] = 7; // floor on row 3
        cells[3 * DungeonLevel.Width + 5] = 1;      // (5,3): the 0x01 corridor, drawn like a wall
        var level = new DungeonLevel(1, "TEST", cells);
        var drawn = Enumerable.Range(0, 10).Select(x => (x, 3)).ToList();
        (int, int)[] stood = [(5, 3)];

        var path = Game1.DungeonWalkPath(level, drawn, stood, (1, 3), (8, 3));
        Assert.NotNull(path);
        Assert.Equal(Enumerable.Repeat(CardinalDirection.East, 7), path);
        Assert.Equal(Enumerable.Repeat(CardinalDirection.East, 4), Game1.DungeonWalkPath(level, drawn, stood, (1, 3), (5, 3)));

        Assert.Null(Game1.DungeonWalkPath(level, drawn.Where(c => c != (5, 3)), stood, (1, 3), (8, 3))); // not drawn yet
        Assert.Null(Game1.DungeonWalkPath(level, drawn, stood, (1, 3), (9, 3)));                       // a real wall
        // User s77: drawn but never stood on, the 0x01 cell looks like a wall, so the walk won't take it.
        Assert.Null(Game1.DungeonWalkPath(level, drawn, [], (1, 3), (8, 3)));
        Assert.Null(Game1.DungeonWalkPath(level, drawn, [(4, 3)], (1, 3), (5, 3)));
    }

    [Fact]
    public void QoLProgress_StoodOnCells_ArePerLevel_AndRoundTripThroughJson()
    {
        var qol = new QoLProgress();
        qol.MarkStoodOn(3, 5, 7);
        qol.MarkStoodOn(3, 5, 7); // once
        qol.MarkStoodOn(4, 1, 1);
        Assert.Equal([(5, 7)], qol.StoodOnCells(3));
        Assert.True(qol.IsStoodOn(4, 1, 1));
        Assert.False(qol.IsStoodOn(3, 1, 1));
        Assert.False(qol.IsVisited(3, 5, 7)); // a separate set from the drawn cells

        var json = System.Text.Json.JsonSerializer.Serialize(qol,
            new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase });
        Assert.Contains("\"stoodOnByLevel\"", json);
        var back = System.Text.Json.JsonSerializer.Deserialize<QoLProgress>(json,
            new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase })!;
        Assert.True(back.IsStoodOn(3, 5, 7));
    }
}
