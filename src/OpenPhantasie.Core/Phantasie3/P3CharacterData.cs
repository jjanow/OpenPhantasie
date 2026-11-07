namespace OpenPhantasie.Core;

/// <summary>Where a Phantasie III character comes from (record <c>+0x58</c>, sheet string 129 + n, <c>FORMAT.md</c> §5).</summary>
public enum Phantasie3Origin
{
    /// <summary>0: transferred from Phantasie I.</summary>
    Gelnor = 0,

    /// <summary>1: transferred from Phantasie II.</summary>
    Ferronrah = 1,

    /// <summary>2: made in town 17, Death (the Netherworld).</summary>
    Netherworld = 2,

    /// <summary>3: made in a Scandor town (1–14).</summary>
    Scandor = 3,

    /// <summary>4: made in town 15, Light.</summary>
    Light = 4,

    /// <summary>5: made in town 16, Dark.</summary>
    Darkness = 5,
}

/// <summary>
/// The fields Phantasie III's 262-byte record has beyond the shared character (<c>FORMAT.md</c> §5,
/// <c>dungeon-events.md</c> §4): social class, combat rank, origin, the quest and deed bit words the dungeon programs
/// write, the Phantasie I / II scores a transfer brings, the Fire Bow skill and Divine Aid. Null on a character that never
/// played III. The six body areas live in <see cref="Character.BodyCondition"/> (<see cref="P3BodyAreas"/>).
/// </summary>
public sealed class Phantasie3CharacterData
{
    /// <summary>Darklord: Lord Wood killed (<c>endgame.md</c> §3).</summary>
    public const int Darklord = 4;

    /// <summary>Superhero: Nikademus killed.</summary>
    public const int Superhero = 5;

    /// <summary><c>+0x8e</c>: Peasant 0, Labourer 1, Craftsman 2, Noble 3, Darklord 4, Superhero 5.</summary>
    public int SocialClass { get; set; }

    /// <summary><c>+0x08</c>: Back 0, Middle 1, Front 2 (new characters start Front).</summary>
    public P3PartyRank Rank { get; set; } = P3PartyRank.Front;

    /// <summary><c>+0x58</c>.</summary>
    public Phantasie3Origin Origin { get; set; } = Phantasie3Origin.Scandor;

    /// <summary><c>+0x8a</c>: quest bits 0–7, written only by dungeon programs (op 24); the score weighs them.</summary>
    public int QuestBits { get; set; }

    /// <summary><c>+0x8c</c>: once-only flags and evil deeds (soups, statues, the Gem, the Fairy), compared as a number.</summary>
    public int DeedBits { get; set; }

    /// <summary><c>+0x5c</c>: the Phantasie I score a transfer brought (Filmon: "who defeated the Black Lord" at ≥ 100).</summary>
    public int Phantasie1Score { get; set; }

    /// <summary><c>+0x18</c>: the Phantasie II score (Filmon at ≥ 200, the archive altar). DOS's own transfer never writes it.</summary>
    public int Phantasie2Score { get; set; }

    /// <summary><c>+0xf0</c> = 57: Divine Aid learned (the wizard in the Gnome catacombs). Not castable; dungeon tests only.</summary>
    public bool DivineAid { get; set; }

    /// <summary><c>+0x48</c>: the Fire Bow skill (III's ninth skill; <see cref="CharacterSkills"/> holds the other eight).</summary>
    public int FireBow { get; set; }

    /// <summary>
    /// <c>+0x6e</c>: the saved option, the member's last order of the previous fight as a DOS action code (0 Attack …
    /// 7 Cast, 11–16 the rank-2 forms; <see cref="P3CombatModule.ActionCode"/>). Written at fight end (root <c>0xd4ff</c>),
    /// re-checked and loaded as the first order at fight start (root <c>0xd55b</c>). Creation writes 3, Thrust (ovl2
    /// <c>0x255e</c>).
    /// </summary>
    public int SavedAction { get; set; } = P3CombatModule.NewCharacterAction;

    /// <summary>
    /// <c>+0x70</c>: the spell of a saved Cast (DOS keeps the spell number); the remaster keeps the whole packed cast order
    /// (<see cref="CombatAction"/>), null when none.
    /// </summary>
    public int? SavedCast { get; set; }

    /// <summary>
    /// <c>+0x6e</c> = 5: the saved option is Parry. DOS's Parry (<c>p3.parry</c> = <c>dos</c>, D3-036) takes the shield off
    /// monster melee all fight while it is set.
    /// </summary>
    public bool ParrySaved => SavedAction == (int)P3Action.Parry;

    /// <summary>
    /// <c>+0x54</c>: the experience shares last typed at a town entry's "Shares for NAME :" (D3-071, DOS ovl2 <c>0x0000</c>(1)
    /// writes the answer back; creation writes 1). The prompt lights it for a living member (0 or less → 1 first); a dead
    /// member's prompt starts at 0.
    /// </summary>
    public int ExperienceShares { get; set; } = 1;

    public Phantasie3CharacterData Clone() => new()
    {
        SocialClass = SocialClass,
        Rank = Rank,
        Origin = Origin,
        QuestBits = QuestBits,
        DeedBits = DeedBits,
        Phantasie1Score = Phantasie1Score,
        Phantasie2Score = Phantasie2Score,
        DivineAid = DivineAid,
        FireBow = FireBow,
        SavedAction = SavedAction,
        SavedCast = SavedCast,
        ExperienceShares = ExperienceShares,
    };
}
