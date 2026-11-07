using System.Security.Cryptography;

namespace OpenPhantasie.Importer.Containers;

/// <summary>
/// One file found in the player's sources: a file in a folder, a zip entry, or a file inside a disk image.
/// <see cref="Path"/> is relative to its <see cref="Container"/> and uses '/' (e.g. <c>D/m0</c> on the Amiga III disk).
/// The bytes are read on first use and kept.
/// </summary>
public sealed class ImportFile
{
    private readonly Func<byte[]> _read;
    private byte[]? _data;
    private string? _sha1;

    private readonly long? _size;

    /// <param name="size">The size when known without reading (directory entry), so size checks read nothing.</param>
    public ImportFile(Container container, string path, Func<byte[]> read, long? size = null)
    {
        Container = container;
        Path = path.Replace('\\', '/').Trim('/');
        _read = read;
        _size = size;
    }

    public ImportFile(Container container, string path, byte[] data) : this(container, path, () => data, data.Length) => _data = data;

    public Container Container { get; }

    /// <summary>Path inside the container, '/'-separated.</summary>
    public string Path { get; }

    /// <summary>The last path segment (the file name as the game knows it).</summary>
    public string Name => Path[(Path.LastIndexOf('/') + 1)..];

    public byte[] Data => _data ??= _read();

    /// <summary>Byte size; from the directory entry when known (no read).</summary>
    public long Length => _data?.Length ?? _size ?? Data.Length;

    /// <summary>Lower-case hex sha1 of the bytes.</summary>
    public string Sha1 => _sha1 ??= Hashing.Sha1(Data);

    public override string ToString() => $"{Container.Description}:{Path}";
}

/// <summary>
/// A unit the player handed in, or found inside one: a folder, a zip, or a disk image. Files of one container are
/// looked up together (a game's key files must come from the same container set, see <see cref="FileSet"/>).
/// </summary>
public sealed class Container
{
    public Container(string kind, string description, string? sourcePath)
    {
        Kind = kind;
        Description = description;
        SourcePath = sourcePath;
    }

    /// <summary><c>folder</c>, <c>zip</c>, <c>fat12</c>, <c>adf</c>, <c>stx</c>, <c>dim</c>, <c>raw-image</c>.</summary>
    public string Kind { get; }

    /// <summary>For messages: the file or folder name, with the outer container for nested ones.</summary>
    public string Description { get; }

    /// <summary>The path on disk the player picked (or the zip / folder holding it), for <c>import.json</c>.</summary>
    public string? SourcePath { get; }

    /// <summary>sha1 of the whole image file, when the container is one (Appendix A fingerprints).</summary>
    public string? ImageSha1 { get; init; }

    /// <summary>Byte size of the whole image file, when the container is one.</summary>
    public long? ImageSize { get; init; }

    public List<ImportFile> Files { get; } = [];

    public override string ToString() => Description;
}

public static class Hashing
{
    public static string Sha1(ReadOnlySpan<byte> data) => Convert.ToHexStringLower(SHA1.HashData(data));
}
