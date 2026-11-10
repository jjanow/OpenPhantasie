using System.Text.RegularExpressions;
using OpenPhantasie.Core;
using OpenPhantasie.Data;

namespace OpenPhantasie.Client.Tests;

/// <summary>
/// T1.5 string tables: the Client's on-screen text comes from the game's table by id. Every id the Client source uses is
/// in the shared shell table (T5.0) or Phantasie I's English table over it, every entry is used, and the templates are well
/// formed.
/// </summary>
public partial class StringTableTests
{
    // II's table extends I's: every id either game's Client and Core use (I's text where II has none of its own).
    private static readonly StringTable En = UiText.For(GameId.P2);

    // III's table (T4) extends I's too and adds its own core.p3.* ids.
    private static readonly StringTable P3 = UiText.For(GameId.P3);

    private static string ClientSourceDir()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "src", "OpenPhantasie.Client");
            if (File.Exists(Path.Combine(candidate, "UiText.cs")))
                return candidate;
        }

        throw new InvalidOperationException("src/OpenPhantasie.Client not found above the test folder.");
    }

    // OptionsScreen.Described("options.x", …) shows "options.x" and describes the row with "options.x.desc".
    // s185: PadWording.S / F / Format("id", …) show "id", or its ".pad" twin while the pad is the input. s193: IV's PadS("id") likewise.
    [GeneratedRegex("""(?:UiText\.[SF]|PadWording\.(?:[SF]|Format)|Described|PadS)\("([^"]+)"[,)]""")]
    private static partial Regex IdUse();

    [GeneratedRegex("\"(core\\.[a-z0-9_.]+)\"")]
    private static partial Regex CoreIdUse();

    /// <summary>Ids the Client looks up, plus the <c>core.*</c> ids Core and Data look up (GameText, same table).</summary>
    private static HashSet<string> UsedIds()
    {
        var ids = Directory.EnumerateFiles(ClientSourceDir(), "*.cs")
            .SelectMany(f => IdUse().Matches(File.ReadAllText(f)).Select(m => m.Groups[1].Value))
            .ToHashSet(StringComparer.Ordinal);
        var src = Path.GetDirectoryName(ClientSourceDir())!;
        foreach (var dir in new[] { "OpenPhantasie.Core", "OpenPhantasie.Data" })
            foreach (var f in Directory.EnumerateFiles(Path.Combine(src, dir), "*.cs", SearchOption.AllDirectories))
                ids.UnionWith(CoreIdUse().Matches(File.ReadAllText(f)).Select(m => m.Groups[1].Value));
        return ids;
    }

    /// <summary>Ids built at run time: enum names and the town menu labels.</summary>
    private static IEnumerable<string> DynamicIds() =>
        Enum.GetValues<CharacterClass>().Select(v => $"enum.CharacterClass.{v}")
            .Concat(Enum.GetValues<Race>().Select(v => $"enum.Race.{v}"))
            .Concat(Enum.GetValues<GameMode>().Select(v => $"enum.GameMode.{v}"))
            .Concat(Enum.GetValues<DungeonFacing>().Select(v => $"enum.DungeonFacing.{v}"))
            .Concat(Enum.GetValues<P3Skill>().Select(v => $"enum.P3Skill.{v}"))
            .Concat(TownMenu.BarLabels.Concat(TownMenu.BankLabels).Concat(TownMenu.OptionLabels).Concat(TownMenu.InnLabels)
                .Concat(TownMenu.DosBankLabels).Append("Trade items")
                .Select(l => "town.label." + l.ToLowerInvariant().Replace(' ', '_')))
            // T5.0: game names and tabs, presets, rules options and their values, feature modules.
            .Concat(MainMenuScreen.Games.SelectMany(g => new[] { "menu.game." + g.Prefix(), "options.tab." + g.Prefix() }))
            .Concat(Enum.GetValues<OptionPreset>().Select(p => "options.preset." + p.Id()))
            // A game's own preset label (II: "ORIGINAL (ATARI ST)"), used when the table has one.
            .Concat(Enum.GetValues<OptionPreset>()
                .SelectMany(p => MainMenuScreen.Games.Select(g => $"options.preset.{p.Id()}.{g.Prefix()}"))
                .Where(En.Contains))
            .Concat(RuleOptionRegistry.All.SelectMany(o => o.Values.Select(v => $"option.{o.Id}.{v}").Prepend("option." + o.Id)))
            .Concat(FeatureModuleRegistry.All.Select(m => "module." + m.Id))
            // The Options description box: one ".desc" per row (OptionsScreen.Detail).
            .Concat(RuleOptionRegistry.All.Select(o => $"option.{o.Id}.desc"))
            .Concat(FeatureModuleRegistry.All.Select(m => $"module.{m.Id}.desc"))
            .Concat(En.Strings.Keys.Where(k => k.StartsWith("options.", StringComparison.Ordinal) && k.EndsWith(".desc", StringComparison.Ordinal)))
            // s183: a help text's pad wording, shown by its keyboard text's id (PadWording.Help).
            .Concat(En.Strings.Keys.Where(k => k.EndsWith(PadWording.Suffix, StringComparison.Ordinal)))
            // B8 (s189): a wheel step's own line, shown by its keyboard text's id (DialogPad.WheelTextFor).
            .Concat(En.Strings.Keys.Where(k => k.EndsWith(DialogPad.WheelSuffix, StringComparison.Ordinal)))
            // 09 T2 inventory screen: Core's refusal reasons and the paperdoll boxes of the game's equipment layout.
            .Concat(InventoryActions.ReasonKeys)
            // The Guild treasury's ledger kinds (TreasuryReport.Lines).
            .Concat(Enum.GetValues<TreasuryEntryKind>().Select(TreasuryReport.KindId))
            .Concat(EquipmentLayout.PhantasieI.Slots.Select(s => "inventory.slot." + s.Id))
            // IV town doors (P4DungeonScreen.BuildingName).
            .Concat(Enum.GetValues<P4Building>().Select(P4DungeonScreen.BuildingKey))
            // IV battle: actions, options, ends and fields (P4DungeonScreen.Battle.cs).
            .Concat(P4DungeonScreen.BattleStringKeys())
            // III Amiga's combat orders screen (UiOverlays.CombatOrders.cs).
            .Concat(UiOverlays.OrdersStringKeys())
            // I Amiga's combat screen (UiOverlays.P1CombatField.cs, s132).
            .Concat(UiOverlays.P1FieldStringKeys())
            // s154: requester 21's Amiga titles and health rows, named by the pack (P1MapReports).
            .Concat(P1MapReports.StringKeys(GraphicsPackCatalog.Load(GameDataRepository.DefaultPathFor(GameId.P1)).Find("amiga")?.MapMessage?.Window))
            // I Amiga's victory speech in its own line breaks (pack ending_scene.speech, VictoryScreen.SpeechLines).
            .Concat(GraphicsPackCatalog.Load(GameDataRepository.DefaultPathFor(GameId.P1)).Find("amiga")?.EndingScene?.SpeechKeys ?? [])
            // s155: I Amiga's map menu bar (pack map_message.menu_bar).
            .Concat(UiOverlays.P1MapBarStringKeys(GraphicsPackCatalog.Load(GameDataRepository.DefaultPathFor(GameId.P1)).Find("amiga")?.MapMessage))
            // s157: I Amiga's own requester-23 texts (pack map_message.texts).
            .Concat(GraphicsPackCatalog.Load(GameDataRepository.DefaultPathFor(GameId.P1)).Find("amiga")!.MapMessage!.Texts.Values)
            // I Amiga's requesters 0 / 22 / 14 (pack map_requesters, s154).
            .Concat(P1MapRequesters.StringKeys(GraphicsPackCatalog.Load(GameDataRepository.DefaultPathFor(GameId.P1)).Find("amiga")!.MapRequesters!))
            // III Amiga's credits window (pack keep_windows.credits.text_ids, File › Info, s140).
            .Concat(GraphicsPackCatalog.Load(GameDataRepository.DefaultPathFor(GameId.P3)).Find("amiga")?.KeepWindows?.Credits?.TextIds ?? [])
            // III Amiga's Party Status labels (pack keep_windows.party_status.text_ids, window 22, s141).
            .Concat(GraphicsPackCatalog.Load(GameDataRepository.DefaultPathFor(GameId.P3)).Find("amiga")?.KeepWindows?.PartyStatus?.TextIds ?? [])
            // s156: III Amiga's menu bars (pack keep_windows.menu_bars: the fight's, outside the towns, the town's).
            .Concat(GraphicsPackCatalog.Load(GameDataRepository.DefaultPathFor(GameId.P3)).Find("amiga")?.KeepWindows?.MenuBars.Values
                .SelectMany(b => b.Menus.SelectMany(m => m.Items.Prepend(m.Title))) ?? [])
            // s171: II's terrain key rows (GameData/phantasie2/terrain-key.json, the TERRAIN KEY switch).
            .Concat(TerrainKey.For(GameId.P2).Select(e => e.Label))
            // s160: what an import companion gives (profiles.json companions "gives", shown by ImportScreen's offer).
            .Concat(OpenPhantasie.Importer.Profiles.VersionProfiles.All.SelectMany(p => p.Companions).Select(c => c.Gives));

    [Fact]
    public void Table_IsTheGamesEnglish()
    {
        Assert.Equal("phantasie2", En.Game);
        Assert.Equal("Welcome to Pippacott. G enters the Guild.", En.Get("game.welcome_to_pelnor_g_enters_the"));
        Assert.Equal("Welcome to Pelnor. G enters the Guild.", UiText.For(GameId.P1).Get("game.welcome_to_pelnor_g_enters_the"));
        Assert.Equal("en", En.Language);
        Assert.Equal("phantasie1", UiText.Table.Game); // the default before a game is loaded
    }

    [OriginalDataFact]
    public void EveryIdTheClientUses_IsInTheTable()
    {
        Assert.Equal([], UsedIds().Concat(DynamicIds()).Where(id => !En.Contains(id) && !P3.Contains(id)).ToList());
        Assert.Equal("phantasie3", P3.Game);
        Assert.Equal("Welcome to Pendragon. G enters the Guild.", P3.Get("game.welcome_to_pelnor_g_enters_the"));
    }

    [OriginalDataFact]
    public void EveryTableEntry_IsUsed()
    {
        var used = UsedIds();
        used.UnionWith(DynamicIds());
        Assert.Equal([], En.Strings.Keys.Where(id => !used.Contains(id)).ToList());
    }

    [Fact]
    public void EveryTemplate_Formats()
    {
        var args = Enumerable.Range(0, 10).Select(i => (object)i).ToArray();
        foreach (var (id, _) in En.Strings)
            En.Format(id, args); // throws on a malformed template
    }

    [OriginalDataFact]
    public void EnumNames_MatchTheirValues_InEnglish()
    {
        // Pre-T1.5 the Client drew ToString() of these enums; the English table must give the same words.
        Assert.Equal("LizardMan", UiText.Name(Race.LizardMan));
        Assert.Equal("Fighter", UiText.Name(CharacterClass.Fighter));
        Assert.Equal("Overworld", UiText.Name(GameMode.Overworld));
        Assert.Equal("South", UiText.Name(DungeonFacing.South));
        Assert.Equal("99", UiText.Name((Race)99)); // a raw byte outside the enum shows its number, as before
        // III's skills are new screen text (Find Item, Pick Lock …), not former ToString() output.
        foreach (var id in DynamicIds().Where(id => id.StartsWith("enum.", StringComparison.Ordinal)
                                                    && !id.StartsWith("enum.P3Skill.", StringComparison.Ordinal)))
            Assert.Equal(id[(id.LastIndexOf('.') + 1)..], En.Get(id));
    }

    [Fact]
    public void MissingId_Throws()
    {
        Assert.Throws<KeyNotFoundException>(() => En.Get("no.such.id"));
    }

    [Fact]
    public void Overlay_ReplacesAndAdds()
    {
        var dir = Path.Combine(Path.GetTempPath(), "phantasie-strings-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(dir, "strings"));
        try
        {
            File.WriteAllText(Path.Combine(dir, "strings", "xx.json"),
                """{ "game": "g2", "language": "xx", "strings": { "cast.cancelled": "ANNULE.", "new.id": "NEW" } }""");
            var merged = En.Over(StringTable.Load(dir, "xx"));
            Assert.Equal("ANNULE.", merged.Get("cast.cancelled"));
            Assert.Equal("NEW", merged.Get("new.id"));
            Assert.Equal(En.Get("cast.heal_whom"), merged.Get("cast.heal_whom"));
            Assert.Equal(("g2", "xx"), (merged.Game, merged.Language));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
