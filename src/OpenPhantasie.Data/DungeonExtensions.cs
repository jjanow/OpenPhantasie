using OpenPhantasie.Core;

namespace OpenPhantasie.Data;

public static class DungeonExtensions
{
    public static DungeonLevel ToRuntime(this DungeonLevelDefinition def)
    {
        var levelId = ParseLevelId(def.Id);
        if (def.GridWidth != DungeonLevel.Width || def.GridHeight != DungeonLevel.Height)
        {
            // T4: a level with its own grid (III: 24x30) keeps its whole record as the buffer (III: 4224 B, DNP<letter>).
            var n = def.GridWidth * def.GridHeight;
            var record = def.RawFeatureBytes;
            return new DungeonLevel(levelId, def.Id, record[..n], record[n..],
                def.EmbeddedTexts.Select(t => t.Text).ToList(), def.GridWidth, def.GridHeight, record.Length)
            {
                Messages = def.Messages.ToDictionary(kv => kv.Key, kv => SplitMenu(kv.Value)),
            };
        }

        var grid = def.GetGridBytes();
        if (grid.Length < DungeonLevel.GridByteLength)
        {
            throw new InvalidDataException(
                $"{def.Id}: grid is {grid.Length} bytes; expected {DungeonLevel.GridByteLength}.");
        }

        if (grid.Length > DungeonLevel.GridByteLength)
        {
            var trimmed = new byte[DungeonLevel.GridByteLength];
            Buffer.BlockCopy(grid, 0, trimmed, 0, trimmed.Length);
            grid = trimmed;
        }

        var sideLen = Math.Max(0, def.RawFeatureBytes.Length - DungeonLevel.GridByteLength);
        var side = new byte[sideLen];
        if (sideLen > 0)
            Buffer.BlockCopy(def.RawFeatureBytes, DungeonLevel.GridByteLength, side, 0, sideLen);

        return new DungeonLevel(
            levelId,
            def.Id,
            grid,
            side,
            def.EmbeddedTexts.Select(t => t.Text).ToList())
        {
            Messages = def.Messages,
        };
    }

    /// <summary>
    /// A III text whose menu choices are inline after 0xf0 bytes ("…which do you choose?ð1)Strengthð2)…", read as
    /// U+00F0): the part before the first one is the text, each later part a choice. The 38-character cut lines are
    /// joined first (the cut kept every byte) and the text is cut again.
    /// </summary>
    private static DungeonMessage SplitMenu(DungeonMessage message)
    {
        const char Break = '\u00f0';
        var whole = string.Concat(message.Lines);
        if (!whole.Contains(Break))
            return message;
        var parts = whole.Split(Break);
        var lines = new List<string>();
        for (var at = 0; at < parts[0].Length; at += 38)
            lines.Add(parts[0].Substring(at, Math.Min(38, parts[0].Length - at)));
        return message with { Lines = lines, Options = parts.Skip(1).Where(p => p.Length > 0).ToList() };
    }

    public static DungeonTraversal CreateDungeonTraversal(this GameDataRepository data) =>
        new(data.Dungeons.Select(d => d.ToRuntime())) { Encounters = data.Rules.EncounterRulesOrDefault };

    private static int ParseLevelId(string id)
    {
        var digits = new string(id.Where(char.IsDigit).ToArray());
        return int.TryParse(digits, out var n) ? n : throw new InvalidDataException($"Bad dungeon id: {id}");
    }
}
