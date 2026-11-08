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
    [GeneratedRegex("""(?:UiText\.[SF]|Described)\("([^"]+)"[,)]""")]
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
            // 09 T2 inventory screen: Core's refusal reasons and the paperdoll boxes of the game's equipment layout.
            .Concat(InventoryActions.ReasonKeys)
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

    /// <summary>
    /// s156 (Watch list "III guild / town lines"): where Core's refusal is DOS III's own condition, III's table says it in
    /// DOS III's words (MAIN.EXE strings 411, 412, 409, 429; the Amiga's window 7 callers say the same, FORMAT.md 8.4).
    /// </summary>
    [OriginalDataTheory]
    [InlineData("core.guild.party_is_full", "The party has 6 characters.", "Party is full (max 6).")]
    [InlineData("core.guild.already_in_party", "This character is in the party.", "Already in the party.")]
    [InlineData("core.guild.roster_full", "The character roster is full.", "Guild roster is full.")]
    [InlineData("core.guild.learn_no_more", "You can learn no more at this time.", "You can't learn any more!")]
    // s158: the town dispatch read key by key (Amiga 0xb1cc, DOS guild_menu 0xac21 / ovl2 learn 0x1d12, FORMAT.md 8.4).
    [InlineData("core.guild.learn_cant_afford", "The party cannot afford anymore spells for this character.", "You can't afford it!")]
    [InlineData("guild.no_one_to_drop", "The party is not assembled.", "No one to drop.")]
    [InlineData("guild.add_members_to_a_party_first", "The party is not assembled.", "Add members to a party first.")]
    [InlineData("guild.roster_empty", "The character roster is empty.", "No one to add.")]
    [InlineData("cast.x_knows_no_spells", "This character knows no spells.", "{0} knows no spell that can be cast here.")]
    [InlineData("useitem.x_carries_nothing", "This character is not carrying anything.", "{0} carries nothing.")]
    public void P3GuildLines_AreDosIIIsOwn(string id, string p3, string p1)
    {
        Assert.Equal(p3, P3.Get(id));
        Assert.Equal(p1, UiText.For(GameId.P1).Get(id));
    }

    /// <summary>s158: Core's III training refusal (Dead / Out, Undead, level 21) is DOS guild_menu item 1's S417.</summary>
    [OriginalDataFact]
    public void P3TrainingRefusal_IsS417()
    {
        Assert.Equal("This character is unable to do this.", P3.Get("core.p3.cannot_train"));
        Assert.Equal(P3.Get("core.guild.party_not_assembled"), P3.Get("guild.no_one_to_drop")); // window 7 matches by text
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

    [OriginalDataFact]
    public void Format_WorksLikeInterpolation()
    {
        Assert.Equal("How much to withdraw? 00042", UiText.F("towndlg.how_much_to_withdraw_x", 42));
        Assert.Equal($"{"SWAT",-10}", UiText.F("health.x_x_hp_x_x_mp", "SWAT", "OKAY", 1, 2, 3, 4)[..10]);
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
