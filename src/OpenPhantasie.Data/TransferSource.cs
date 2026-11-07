using OpenPhantasie.Core;

namespace OpenPhantasie.Data;

/// <summary>
/// Phase 09 T5, character transfer: reads the source game's roster file for <see cref="GameSession.TransferFromPhantasieI"/>
/// (I → II), <see cref="GameSession.TransferFromPhantasieII"/> (II → I) and <see cref="GameSession.TransferIntoPhantasieIII"/>
/// (I / II → III). The ST reads the other game's own
/// <c>guild.dat</c> from its data disk (<c>atari-st/phantasie2/logic-specs/transfer.md</c> sections 7, 8); the remaster's
/// equivalent is its own save of that game (<c>savegame-p1.json</c> / <c>savegame-p2.json</c>), whose top-level roster holds
/// every character (parked parties hold none).
/// </summary>
public static class TransferSource
{
    /// <summary>
    /// The Guild slots of the <paramref name="game"/> save at <paramref name="path"/>. A save of another game throws
    /// <see cref="InvalidDataException"/>; an old-format save throws <see cref="OldSaveFormatException"/>.
    /// </summary>
    public static IReadOnlyList<Character?> ReadRoster(string path, GameId game) =>
        RosterOf(SessionCodec.ReadJsonFile(path), game);

    /// <summary>The Guild slots of a <paramref name="game"/> save (see <see cref="ReadRoster"/>).</summary>
    public static IReadOnlyList<Character?> RosterOf(SessionSnapshot save, GameId game)
    {
        ArgumentNullException.ThrowIfNull(save);
        if (save.Game != game)
            throw new InvalidDataException($"This save belongs to {save.Game.Prefix()}, not {game.Prefix()}.");
        return save.RosterSlots;
    }
}
