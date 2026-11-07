namespace OpenPhantasie.Tests;

/// <summary>
/// Whether the games' own data is next to the tests: the full GameData (text, maps, names) that the private reference
/// repo commits and the public repo leaves to the player's import (10-importer.md 5.3). Tests that read it carry
/// <see cref="OriginalDataFactAttribute"/> / <see cref="OriginalDataTheoryAttribute"/> and are skipped without it, so
/// <c>dotnet test</c> passes on a fresh clone of the public repo.
/// </summary>
public static class OriginalData
{
    /// <summary>One table of each game that only the original copy can give (class (b) files).</summary>
    private static readonly string[] Markers =
    [
        "phantasie1/messages.json", "phantasie2/messages.json", "phantasie3/dungeons.json", "phantasie4/strings/ja.json",
    ];

    public static bool Present { get; } =
        Markers.All(m => File.Exists(Path.Combine(AppContext.BaseDirectory, "GameData", m.Replace('/', Path.DirectorySeparatorChar))));

    public const string SkipReason = "needs the original games' data (the full GameData of the private reference repo)";
}

/// <summary>A fact that needs the original games' data (<see cref="OriginalData"/>); skipped without it.</summary>
public sealed class OriginalDataFactAttribute : FactAttribute
{
    public OriginalDataFactAttribute()
    {
        if (!OriginalData.Present)
            Skip = OriginalData.SkipReason;
    }
}

/// <summary>A theory that needs the original games' data (<see cref="OriginalData"/>); skipped without it.</summary>
public sealed class OriginalDataTheoryAttribute : TheoryAttribute
{
    public OriginalDataTheoryAttribute()
    {
        if (!OriginalData.Present)
            Skip = OriginalData.SkipReason;
    }
}
