using OpenPhantasie.Core;

namespace OpenPhantasie.Data;

/// <summary>
/// Phase 09 T1.2: game-prefixed ids. Every item, spell, monster and dungeon level in a game's data folder carries an
/// <c>id</c> like <c>p1:42</c>; the loader checks it against the record's own number and that one folder holds one
/// game. Cross-references inside a folder (monster companions and treasure, town stock, pair tables…) stay bare local
/// numbers: a game's data never points into another game's.
/// </summary>
public sealed partial class GameDataRepository
{
    /// <summary>The game this data folder holds (from its ids; every id in the folder names the same game).</summary>
    public GameId Game { get; private init; } = GameId.P1;

    /// <summary>Lookup by game-scoped id; an id from another game is not found (no cross-game collision).</summary>
    public MonsterDefinition GetMonster(ContentId id) => GetMonster(LocalOf(id, "Monster"));

    /// <inheritdoc cref="GetMonster(ContentId)"/>
    public SpellDefinition GetSpell(ContentId id) => GetSpell(LocalOf(id, "Spell"));

    /// <inheritdoc cref="GetMonster(ContentId)"/>
    public ItemDefinition GetItem(ContentId id) => GetItem(LocalOf(id, "Item"));

    /// <inheritdoc cref="GetMonster(ContentId)"/>
    public DungeonLevelDefinition GetDungeon(ContentId id) =>
        Dungeons.FirstOrDefault(d => d.ContentId == id)
        ?? throw new KeyNotFoundException($"Dungeon id {id} not found.");

    /// <summary>This game's id for a local number (<c>p1:42</c> for 42 in Phantasie I data).</summary>
    public ContentId IdOf(int local) => new(Game, local);

    private int LocalOf(ContentId id, string what) =>
        id.Game == Game ? id.Local : throw new KeyNotFoundException($"{what} id {id} not found in {Game.Prefix()} data.");

    /// <summary>Parses a record's <c>id</c> and checks its local part equals the record's own number.</summary>
    private static ContentId ParseRecordId(string? id, int expectedLocal, string what, string path)
    {
        if (!ContentId.TryParse(id, out var parsed))
            throw new InvalidDataException($"{path}: {what} {expectedLocal} needs an id like p1:{expectedLocal}, got '{id}'.");
        if (parsed.Local != expectedLocal)
            throw new InvalidDataException($"{path}: {what} id {parsed} does not match its number {expectedLocal}.");
        return parsed;
    }

    /// <summary>Level number from a <c>DNG%d</c> key (the original loader's file name).</summary>
    private static int DungeonLevelNumber(string key)
    {
        var digits = new string(key.Where(char.IsDigit).ToArray());
        return int.TryParse(digits, out var n) ? n : throw new InvalidDataException($"Bad dungeon key: {key}");
    }

    /// <summary>The single game every loaded id names.</summary>
    private static GameId SingleGame(IEnumerable<ContentId> ids)
    {
        var games = ids.Select(i => i.Game).Distinct().ToList();
        return games.Count == 1
            ? games[0]
            : throw new InvalidDataException(
                $"One game data folder must hold one game; its ids name {string.Join(", ", games.Select(g => g.Prefix()))}.");
    }
}
