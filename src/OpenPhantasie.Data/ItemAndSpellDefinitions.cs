using OpenPhantasie.Core;

namespace OpenPhantasie.Data;

/// <summary>
/// Equipment / consumable table entry from items.json (Phase 3).
/// Combat points and prices are formula-derived from index + value_factor V —
/// see reverse-engineering/dos/logic-specs/data-tables.md.
/// </summary>
public record ItemDefinition(
    int Id,
    string Name,
    int ValueFactor,
    string Kind,
    int? Points,
    int? MinStr,
    string? StrCheck)
{
    /// <summary>Game-scoped id (<c>p1:</c><see cref="Id"/>), T1.2.</summary>
    public ContentId ContentId { get; init; }

    /// <summary>
    /// Phantasie III: the DOS value table entry (<c>DS:0xf92</c>); the armory price is <c>value · price index / 100</c>.
    /// Null in I / II, whose price comes from <see cref="ValueFactor"/> (III's <see cref="ValueFactor"/> is 0).
    /// </summary>
    public int? Value { get; init; }
}

/// <summary>Remaster-written U)SE ITEM text for one item (<c>item-descriptions.json</c>, D-061).</summary>
public record ItemDescription(string FullName, string Text);

/// <summary>
/// Spell table entry. Names/order and MP costs are confirmed (55 spells).
/// MP cost = (DOS slot % 4) + 1 — see magic-spells.md [C]; the slot equals the index except DIVINE I (54 → slot 57).
/// </summary>
public record SpellDefinition(
    int Id,
    string Name,
    string Category,
    string? EffectSummary,
    /// <summary>
    /// [C]: (DOS slot % 4) + 1 (OV2.OVR @ 0x47da). Always 1–4.
    /// </summary>
    int MagicPointCost,
    /// <summary>Provenance of the effect rule (tags as in the logic specs).</summary>
    string? EffectCoefficientsNote,
    /// <summary>[C] DOS spell slot (name table DS 0x174d, known flag @96 + 2·slot) — index, except DIVINE I = 57.</summary>
    int DosSlot)
{
    /// <summary>Game-scoped id (<c>p1:</c><see cref="Id"/>), T1.2.</summary>
    public ContentId ContentId { get; init; }

    /// <summary>
    /// False when the data states no cost and the game has no decoded cost rule (Phantasie II's divine spells, <c>[?]</c>);
    /// <see cref="MagicPointCost"/> is then 0 and must not be used.
    /// </summary>
    public bool MagicPointCostKnown { get; init; } = true;
}
