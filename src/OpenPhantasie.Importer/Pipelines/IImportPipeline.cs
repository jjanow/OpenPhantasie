using System.Reflection;

namespace OpenPhantasie.Importer.Pipelines;

/// <summary>
/// Builds what one version profile feeds (tables, art, raw audio files) from the player's files. Implementations are
/// found by reflection (public, parameterless constructor), so a new version plugs in by adding a class and a profile
/// in <c>Profiles/profiles.json</c> (e.g. the Apple II II importer under <c>p2-apple2</c>).
/// <para>Rules for implementations: pure (read only <see cref="ImportContext.Files"/> and the context's readers, write
/// only through the context); no repo paths; output must equal what the Python pipeline in <c>tools/scripts/</c> wrote
/// on the reference images (the golden tests).</para>
/// </summary>
public interface IImportPipeline
{
    /// <summary>The <c>profiles.json</c> id this pipeline imports (e.g. <c>p1-dos-1987</c>).</summary>
    string ProfileId { get; }

    /// <summary>Bumped when the output changes; a newer recipe than <c>import.json</c>'s re-runs the import.</summary>
    int RecipeVersion { get; }

    /// <summary>Profiles whose output this one reads when they are imported in the same run (they run first).</summary>
    IReadOnlyList<string> RunsAfter => [];

    void Run(ImportContext context);
}

public static class PipelineRegistry
{
    private static IReadOnlyList<IImportPipeline>? _all;

    public static IReadOnlyList<IImportPipeline> All => _all ??= typeof(IImportPipeline).Assembly.GetTypes()
        .Where(t => t is { IsClass: true, IsAbstract: false } && typeof(IImportPipeline).IsAssignableFrom(t)
                    && t.GetConstructor(Type.EmptyTypes) is not null)
        .Select(t => (IImportPipeline)Activator.CreateInstance(t)!)
        .OrderBy(p => p.ProfileId, StringComparer.Ordinal)
        .ToList();

    public static IImportPipeline? For(string profileId) => All.FirstOrDefault(p => p.ProfileId == profileId);
}

/// <summary>
/// A pipeline that recognises a version whose importer is still being written. It reports and imports nothing.
/// </summary>
public abstract class PlannedPipeline : IImportPipeline
{
    public abstract string ProfileId { get; }
    public int RecipeVersion => 0;
    public bool IsPlanned => true;

    public void Run(ImportContext context) =>
        throw new NotSupportedException($"{context.Profile.Title}: recognised, but its importer is not built yet");
}
