namespace OpenPhantasie.Core;

/// <summary>
/// Which zero-damage line III's Damage Report prints (Amiga <c>0x1d24a</c>'s third argument picks the pair from the dungeon
/// message table <c>0x23a62</c>, s165 <c>[C]</c> static): 0 → messages 12 / 13 `core.p3.report_avoids_the_trap` / "takes " (a sprung
/// trap <c>0x1c7b6</c>, event op 16 <c>0x1cc78</c>), 2 or 3 → 16 / 17 `core.p3.report_escapes_unscathed` / "takes " (event ops 1–4
/// <c>0x1cb00</c>, lava <c>0xa676</c>). 1 → 14 / 15 "swims." / "sinks for " is the swim (<c>0xa6b8</c>), which Core's
/// own swim does not report through here.
/// </summary>
public enum P3DamageReportKind
{
    Trap,
    Escape,
}

/// <summary>A member's line in III's Damage Report.</summary>
public enum P3DamageOutcome
{
    /// <summary>Damage 0: the kind's own line (`core.p3.report_avoids_the_trap` / `core.p3.report_escapes_unscathed`), colour 15.</summary>
    Unhurt,

    /// <summary>"takes n damage.", colour 15.</summary>
    Damaged,

    /// <summary>`core.p3.report_takes_x_damage_and_dies` (the routine's +500 marker), colour 9.</summary>
    Died,

    /// <summary>HP 0 before the damage: "is dead.", colour 10 (Amiga <c>0x1d6b8</c>).</summary>
    AlreadyDead,
}

/// <summary>One member's row: name at text column 2, the line at column 10, on row 9 + the member's slot.</summary>
public sealed record P3DamageReportRow(string Name, P3DamageOutcome Outcome, int Damage)
{
    /// <summary>The text printed after the name (Amiga <c>0x1d54a</c>…<c>0x1d6ce</c>), in III's words.</summary>
    public string Text(P3DamageReportKind kind) => Outcome switch
    {
        P3DamageOutcome.Unhurt => GameText.S(kind == P3DamageReportKind.Trap ? "core.p3.report_avoids_the_trap"
            : "core.p3.report_escapes_unscathed"),
        P3DamageOutcome.Damaged => GameText.F("core.p3.report_takes_x_damage", Damage),
        P3DamageOutcome.Died => GameText.F("core.p3.report_takes_x_damage_and_dies", Damage),
        _ => GameText.S("core.p3.report_is_dead"),
    };
}

/// <summary>
/// s165: III's Damage Report window (Amiga <c>0x1d4ea</c>, called by every party-damage call <c>0x1d24a</c>, <c>[C]</c>
/// static): window 6 (struct <c>0x23360</c>, box (8, 44)–(312, 160)) with its button count set to 1 (OK only, put back to
/// 2 after), sound 42, "Damage Report" at cell (13, 7) in colour 9, then one row per party member in party order from
/// row 9 (<see cref="P3DamageReportRow"/>), and a wait for any key or OK (<c>0x3ba0</c> with key 0). The window comes up
/// before a wiped party's defeat (<c>0x1d4b4</c> sets the defeat flag after it closes).
/// <see cref="Notes"/> are the lines Core also noted for the same damage (the remaster's band / log), so a front end that
/// draws the window can leave them off its line; <see cref="Lead"/> is the line noted just before (e.g. "A trap!!!").
/// </summary>
public sealed record P3DamageReport(P3DamageReportKind Kind, IReadOnlyList<P3DamageReportRow> Rows,
    IReadOnlyList<string> Notes, string Lead)
{
    /// <summary>"Damage Report" (Amiga <c>0x1d74c</c>).</summary>
    public static string Title => GameText.S("core.p3.damage_report");
}
