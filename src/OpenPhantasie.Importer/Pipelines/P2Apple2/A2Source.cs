using OpenPhantasie.Importer.Containers;
using OpenPhantasie.Importer.Profiles;

namespace OpenPhantasie.Importer.Pipelines.P2Apple2;

/// <summary>
/// The two RDOS sides of Phantasie II's Apple II disk, each read on its own (both sides carry files of the same name:
/// <c>PHANTASIE II</c>, <c>MN20</c>...). Side A holds <c>TOWN2</c>, side B <c>DUNGON2</c>.
/// </summary>
internal sealed class A2Source
{
    private readonly Container _a, _b;

    /// <summary>The version's name in the built files' <c>_source</c> lines (v1.0's: <see cref="A2Tables.Source"/>).</summary>
    public string SourceName { get; }

    public A2Source(FileSet files, string sourceName = A2Tables.Source)
    {
        SourceName = sourceName;
        _a = files.Containers.FirstOrDefault(c => Has(c, "TOWN2")) ?? throw new FileNotFoundException("side A (TOWN2) not found");
        _b = files.Containers.FirstOrDefault(c => Has(c, "DUNGON2")) ?? throw new FileNotFoundException("side B (DUNGON2) not found");
    }

    private static bool Has(Container c, string name) => c.Files.Any(f => f.Path == name);

    public byte[] A(string name) => Read(_a, name);

    public byte[] B(string name) => Read(_b, name);

    /// <summary>The live files of side A, in catalog order.</summary>
    public IEnumerable<ImportFile> FilesA => _a.Files;

    private static byte[] Read(Container c, string name) =>
        (c.Files.FirstOrDefault(f => f.Path == name) ?? throw new FileNotFoundException($"{name} not found on {c.Description}", name)).Data;
}
