using System.Text.Json;
using System.Text.RegularExpressions;
using OpenPhantasie.Data;

namespace OpenPhantasie.Core.Tests;

/// <summary>
/// T1.5: Core's (and Data's) player-visible text comes from the string tables by <c>core.*</c> id. Every id the source
/// uses is in Phantasie I's English table, every <c>core.*</c> entry is used, each lookup passes as many arguments as its
/// template takes, and the aligned templates give the same text the old interpolated strings did.
/// </summary>
public partial class GameTextTests
{
    // II's table extends I's, so it holds every core id of both games (I's own text where II does not override it); III's
    // own ids (core.p3.*) go on top.
    private static readonly StringTable En =
        StringTable.ForGame(GameId.P2).Over(StringTable.Load(GameDataRepository.DefaultPathFor(GameId.P3)));

    private static string SourceDir()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "src");
            if (File.Exists(Path.Combine(candidate, "OpenPhantasie.Core", "GameText.cs")))
                return candidate;
        }

        throw new InvalidOperationException("src/OpenPhantasie.Core not found above the test folder.");
    }

    private static IEnumerable<string> SourceFiles() =>
        new[] { "OpenPhantasie.Core", "OpenPhantasie.Data" }
            .SelectMany(d => Directory.EnumerateFiles(Path.Combine(SourceDir(), d), "*.cs", SearchOption.AllDirectories))
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"));

    [GeneratedRegex("\"(core\\.[a-z0-9_.]+)\"")]
    private static partial Regex CoreId();

    [GeneratedRegex(@"\{(\d+)")]
    private static partial Regex Placeholder();

    private static HashSet<string> UsedIds() =>
        SourceFiles().SelectMany(f => CoreId().Matches(File.ReadAllText(f)).Select(m => m.Groups[1].Value))
            .ToHashSet(StringComparer.Ordinal);

    /// <summary>Placeholders a template takes: highest index + 1 (0 for plain text).</summary>
    private static int Arity(string template) =>
        Placeholder().Matches(template.Replace("{{", "")).Select(m => int.Parse(m.Groups[1].Value) + 1).DefaultIfEmpty(0).Max();

    /// <summary>
    /// Every <c>GameText.S(…)</c> / <c>GameText.F(…)</c> call in the source: the core ids in its first argument and the
    /// number of further arguments (top level, outside strings).
    /// </summary>
    private static IEnumerable<(string File, string Kind, List<string> Ids, int Args)> Calls()
    {
        foreach (var file in SourceFiles())
        {
            var text = File.ReadAllText(file);
            foreach (Match m in Regex.Matches(text, @"GameText\.([SF])\("))
            {
                var i = m.Index + m.Length;
                int depth = 0, commas = 0, firstEnd = -1;
                var inString = false;
                for (; i < text.Length; i++)
                {
                    var c = text[i];
                    if (inString)
                    {
                        if (c == '\\') i++;
                        else if (c == '"') inString = false;
                        continue;
                    }
                    if (c == '"') inString = true;
                    else if (c is '(' or '[' or '{') depth++;
                    else if (c is ')' or ']' or '}')
                    {
                        if (depth == 0) break;
                        depth--;
                    }
                    else if (c == ',' && depth == 0)
                    {
                        commas++;
                        if (firstEnd < 0) firstEnd = i;
                    }
                }

                var first = text[(m.Index + m.Length)..(firstEnd < 0 ? i : firstEnd)];
                var ids = CoreId().Matches(first).Select(x => x.Groups[1].Value).ToList();
                if (ids.Count > 0)
                    yield return (Path.GetFileName(file), m.Groups[1].Value, ids, commas);
            }
        }
    }

    [Fact]
    public void EveryIdCoreUses_IsInTheTable()
    {
        Assert.NotEmpty(UsedIds());
        Assert.Equal([], UsedIds().Where(id => !En.Contains(id)).Order().ToList());
    }

    [Fact]
    public void EveryCoreTableEntry_IsUsed()
    {
        var used = UsedIds();
        Assert.Equal([], En.Strings.Keys.Where(id => id.StartsWith("core.", StringComparison.Ordinal) && !used.Contains(id)).ToList());
    }

    [Fact]
    public void EveryLookup_PassesTheArgumentsItsTemplateTakes()
    {
        var calls = Calls().ToList();
        Assert.True(calls.Count > 200, $"only {calls.Count} GameText calls found");
        var bad = calls.SelectMany(c => c.Ids.Select(id => (c, id)))
            .Where(t => En.Contains(t.id) && Arity(En.Get(t.id)) != (t.c.Kind == "S" ? 0 : t.c.Args))
            .Select(t => $"{t.c.File}: {t.c.Kind} {t.id} with {t.c.Args} args, template '{En.Get(t.id)}'")
            .ToList();
        Assert.Equal([], bad);
    }

    [Fact]
    public void EveryCoreTemplate_Formats()
    {
        var args = Enumerable.Range(0, 10).Select(i => (object)i).ToArray();
        foreach (var (id, _) in En.Strings.Where(e => e.Key.StartsWith("core.", StringComparison.Ordinal)))
            En.Format(id, args); // throws on a malformed template
    }

    [Fact]
    public void StringFiles_HaveNoDuplicateIds()
    {
        foreach (var dir in new[] { GameDataRepository.SharedDataPath, GameDataRepository.DefaultPathFor(GameId.P1) })
        {
            var path = Path.Combine(dir, "strings", "en.json");
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            var ids = doc.RootElement.GetProperty("strings").EnumerateObject().Select(p => p.Name).ToList();
            Assert.Equal([], ids.GroupBy(x => x).Where(g => g.Count() > 1).Select(g => g.Key).ToList());
        }

        // The game's table and the shared one must not define the same core id twice either.
        var shared = StringTable.Load(GameDataRepository.SharedDataPath);
        var game = StringTable.Load(GameDataRepository.DefaultPathFor(GameId.P1));
        Assert.Equal([], shared.Strings.Keys.Where(k => k.StartsWith("core.", StringComparison.Ordinal) && game.Contains(k)).ToList());
    }

    [OriginalDataFact]
    public void AlignedTemplates_MatchTheOldInterpolation()
    {
        // Pre-T1.5 these were $"…" strings; the table must give byte-identical text.
        Assert.Equal($"{"ALDO",-10} IS RESTING {"IN PEACE"}".TrimEnd(), GameText.F("core.inn.x_is_resting_x", "ALDO", "IN PEACE").TrimEnd());
        Assert.Equal($"{"ALDO",-10} IS RESTING".TrimEnd(), GameText.F("core.inn.x_is_resting_x", "ALDO", "").TrimEnd());
        Assert.Equal($"STAIRS FROM LEVEL {1,-2} TO {2,-2}".TrimEnd(), GameText.F("core.dungeon.stairs_from_level_x_to_x", 1, 2).TrimEnd());
        Assert.Equal("STAIRS FROM LEVEL 1  TO 2", GameText.F("core.dungeon.stairs_from_level_x_to_x", 1, 2).TrimEnd());
        Assert.Equal($"{"PRIESTO",-10}IS CHANGED", GameText.F("core.pool.x_is_changed", "PRIESTO"));
        Assert.Equal("THE RIVER STYX", GameSession.BlockedMessage(17));
        Assert.Equal(GameText.S("core.endgame.zeus_1"), Endgame.ZeusSpeech[0]);
        Assert.Equal(["YES", "NO"], new[] { SessionPrompts.Yes, SessionPrompts.No });
        Assert.Equal("Victory — return to an inn to distribute loot.", GameText.S("core.combat.victory_return_to_inn"));
    }

    [OriginalDataFact]
    public void ITheNameFields_AreDosPercentTenPointNine_CutAtNineThenPadded()
    {
        // s156: DOS I's %-10.9s (trap, op report, swim, pool rows): a name over 9 characters (an import) is cut, then padded.
        Assert.Equal("ARENTINOS DIES", GameText.F("core.dungeon.trap_x_dies", "ARENTINOSXYZ"));
        Assert.Equal("ARENTINOS IS CHANGED", GameText.F("core.pool.x_is_changed", "ARENTINOSXYZ"));
        Assert.Equal("ARENTINOS TAKES 7   DAMAGE", GameText.F("core.script.report_x_takes_x_damage", "ARENTINOSXYZ", 7));
        Assert.Equal("ALDO      SWIMS", GameText.F("core.dungeon.x_swims", "ALDO"));
        // The precision is for text only: numbers keep their own formats.
        Assert.Equal("STAIRS FROM LEVEL 1  TO 2", GameText.F("core.dungeon.stairs_from_level_x_to_x", 1, 2).TrimEnd());
    }

    [Fact]
    public void Unset_Throws_AndSourceIsTheLoadedTable()
    {
        Assert.True(GameText.IsLoaded);
        Assert.Equal("phantasie1", GameTextSetup.PhantasieI.Game);
        Assert.Equal(GameTextSetup.PhantasieI.Get("core.defeat.x_status_undead"), GameText.S("core.defeat.x_status_undead")); // I's text first
        Assert.Throws<KeyNotFoundException>(() => GameText.S("core.no.such.id"));
    }
}
