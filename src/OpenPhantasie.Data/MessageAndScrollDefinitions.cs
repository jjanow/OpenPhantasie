namespace OpenPhantasie.Data;

/// <summary>
/// One decrypted MESS* area (cipher cracked — see message-and-text.md).
/// </summary>
public sealed class MessageAreaDefinition
{
    public required string Id { get; init; }
    public string? Source { get; init; }
    public int Size { get; init; }
    public int SlotCount { get; init; }
    public int RecordCount { get; init; }
    public IReadOnlyList<string> Records { get; init; } = Array.Empty<string>();
}

/// <summary>
/// One decrypted SCROLLS.DTX entry.
/// </summary>
public sealed class ScrollDefinition
{
    public int Id { get; init; }
    public int Step { get; init; }
    public string Text { get; init; } = "";
    public IReadOnlyList<string> Lines { get; init; } = Array.Empty<string>();
}
