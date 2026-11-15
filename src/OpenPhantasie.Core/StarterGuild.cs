namespace OpenPhantasie.Core;

/// <summary>
/// A pre-generated Guild member a new game starts with (s218): the C64 Phantasie III's six (manual: "A reasonable
/// beginning party has been pre-generated and pre-equipped and is listed as the first 6 members of the Guild"; user s217:
/// imported from the disk being imported). Data only, in Core's terms; the game's campaign builds the character
/// (<see cref="ICampaign.CreateStarterMember"/>), filling what the record does not hold by its own rules. Empty for every
/// copy without one (DOS III has none).
/// </summary>
public sealed record StarterGuildMember
{
    /// <summary>The Guild slot (0-based; the Guild number is slot + 1).</summary>
    public required int Slot { get; init; }

    public required string Name { get; init; }
    public required Race Race { get; init; }
    public required CharacterClass Class { get; init; }
    public required int Level { get; init; }
    public required CharacterStats Stats { get; init; }
    public required int HpMax { get; init; }
    public required int HpCur { get; init; }
    public required int MpCeiling { get; init; }
    public required int MagicMax { get; init; }
    public required int MagicCur { get; init; }

    /// <summary>Gold in the bank.</summary>
    public required int Gold { get; init; }

    public required long Experience { get; init; }

    /// <summary>The age clock in 1/26 year (Core's <see cref="Character.AgeClock"/>); null = Guild New's start age under
    /// the game's rules (D3-078: the C64 v1.0 six, whose stored clocks read 255-314 years).</summary>
    public required int? AgeClock { get; init; }

    public required CharacterStatus Status { get; init; }
    public required int Score { get; init; }

    /// <summary>The eight shared skills; <see cref="FireBow"/> is III's ninth.</summary>
    public required CharacterSkills Skills { get; init; }

    public int FireBow { get; init; }

    /// <summary>III's social class (0 Peasant .. 3 Noble).</summary>
    public int SocialClass { get; init; }

    /// <summary>III's origin (<see cref="Phantasie3Origin"/>), null when the record has none.</summary>
    public int? Origin { get; init; }

    /// <summary>The town the member is in (the game's town number).</summary>
    public required int Town { get; init; }

    public int WornShield { get; init; }
    public int WornArmor { get; init; }
    public int WornWeapon { get; init; }
    public int WornBow { get; init; }

    /// <summary>The carried items (worn ones included), item ids.</summary>
    public IReadOnlyList<int> Carried { get; init; } = [];

    /// <summary>Spell n at index n − 1: 0 unknown, 1 known, 2 learnable.</summary>
    public IReadOnlyList<int> SpellsKnown { get; init; } = [];
}
