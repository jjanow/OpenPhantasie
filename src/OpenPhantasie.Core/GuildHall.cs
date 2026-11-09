namespace OpenPhantasie.Core;

/// <summary>
/// Guild hall party/roster management (OV1.OVR) — create/add/drop/purge/rename.
/// Training and spell-learning live in <see cref="TownMenus"/>.
/// </summary>
public static class GuildHall
{
    /// <summary>[C] (s49) COM menu 9 title, labels 46..48 (options 49/50 = YES/NO): the Add gate for a member from another town.</summary>
    public static IReadOnlyList<string> AddMoveConfirmText =>
        [GameText.S("core.guild.move_not_in_town"), GameText.S("core.guild.move_takes_2_months"), GameText.S("core.guild.move_to_get_here")];

    /// <summary>[C] OV1 <c>0x284c</c>: age-clock units (<c>@264</c>, 2 weeks each) an Add from another town costs.</summary>
    public const int AddAgeCost = 4;

    /// <summary>
    /// Guild → Add, roster rows (OV1 <c>0x2786</c>) [C] (s49 re-read of <c>0x2835..0x2851</c>). DOS asks for confirmation
    /// (<c>0x3b08</c>, see <see cref="AddNeedsMoveConfirm"/>) only when the member's <c>@254</c> differs from this Guild's
    /// town; on every confirmed Add — same town too — it then sets <c>@254</c> := this town, <c>@264</c> += 4 and
    /// <c>@218</c> := 1. D-067 (user s53, manual silent): only a member from another town ages (the gate says
    /// `core.guild.move_takes_2_months`); DOS quirk Q8 aged a same-town Add too. A caller must ask first and only call this on a yes.
    /// <paramref name="guildTownId"/> is the 1-based Core town id of the Guild.
    /// </summary>
    public static bool TryAddToParty(GuildRoster roster, Party party, Character member, int guildTownId, out string error) =>
        TryAddToParty(roster, party, member, guildTownId, agesEveryAdd: false, out error);

    /// <summary>
    /// <paramref name="agesEveryAdd"/> = DOS (<c>p1.add-aging</c>, D-067, Q8): every Add ages the member 2 months.
    /// <paramref name="elementals"/> = the save's elemental rules (<see cref="ElementalGuildRules.Elementals"/>); null = the
    /// party's rules.json block.
    /// </summary>
    public static bool TryAddToParty(
        GuildRoster roster, Party party, Character member, int guildTownId, bool agesEveryAdd, out string error,
        ElementalRules? elementals = null)
    {
        ArgumentNullException.ThrowIfNull(roster);
        ArgumentNullException.ThrowIfNull(party);
        ArgumentNullException.ThrowIfNull(member);

        // [C] OV1 0x1fe8 / 0x278d: Add is refused while an elemental is loaded ([0x62a8] ≠ 0). III (ovl2 0x3a0a): not refused,
        // the elemental stays in its own slot after the characters (ElementalRules.BlocksAdd).
        if (party.Elemental is not null && (elementals ?? party.Rules.ElementalRulesOrDefault).BlocksAdd)
        {
            error = GameText.S("core.guild.elemental_joined_no_more");
            return false;
        }

        if (party.Members.Count >= Party.MaxMembers)
        {
            error = GameText.S("core.guild.party_is_full");
            return false;
        }

        if (party.Members.Contains(member))
        {
            error = GameText.S("core.guild.already_in_party");
            return false;
        }

        if (!roster.Occupied.Contains(member))
        {
            error = GameText.S("core.guild.not_in_roster");
            return false;
        }

        var travelled = AddNeedsMoveConfirm(member, guildTownId);
        party.Members.Add(member);
        member.CurrentTown = guildTownId; // @254 := town (0x2848)
        if (travelled || agesEveryAdd)
            member.AgeClock += AddAgeCost; // @264 += 4 (0x284c); DOS does it on every Add (Q8, D-067)
        member.InParty = true;            // @218 := 1 (0x2851)
        error = "";
        return true;
    }

    /// <summary>
    /// [C] OV1 <c>0x2835..0x2843</c>: Add shows the <c>0x3b08</c> gate only when the member lives in another town
    /// (<c>@254</c> ≠ the Guild's town; creation writes the creating Guild's town, OV1 <c>0xdfb</c>). The gate is COM menu 9
    /// (<see cref="AddMoveConfirmText"/>, YES = 0 / NO = 1); <c>0x3b08</c> returns 1 only for YES.
    /// </summary>
    public static bool AddNeedsMoveConfirm(Character member, int guildTownId)
    {
        ArgumentNullException.ThrowIfNull(member);
        return Math.Max(0, member.CurrentTown - 1) != Math.Max(0, guildTownId - 1);
    }

    /// <summary>
    /// Guild → Add, elemental rows (OV1 <c>0x2786</c>) [C]: the three elementals (roster 22–24) are listed only when
    /// the party has six members (<c>[0x618e]</c> = slot 5's <c>@92</c>), and only while none is loaded. The pick is a
    /// fresh copy of the template in party slot 7 (<see cref="Party.Elemental"/>): no in-party flag, no town or age
    /// change, party size stays 6. III (<see cref="ElementalRules"/>, ovl2 <c>0x3a0a</c>): one character is enough ("The
    /// party is not assembled." with none).
    /// </summary>
    public static bool TryAddElemental(Party party, Character template, out string error, ElementalRules? elementals = null)
    {
        ArgumentNullException.ThrowIfNull(party);
        ArgumentNullException.ThrowIfNull(template);

        if (party.Elemental is not null)
        {
            error = GameText.S("core.guild.elemental_already_joined");
            return false;
        }

        var min = (elementals ?? party.Rules.ElementalRulesOrDefault).MinMembers;
        if (party.Members.Count < min)
        {
            error = GameText.S(min >= Party.MaxMembers ? "core.guild.elementals_only_join_six" : "core.guild.party_not_assembled");
            return false;
        }

        party.Elemental = template.Clone();
        error = "";
        return true;
    }

    /// <summary>
    /// Guild ▸ Drop on the loaded elemental itself (<see cref="ElementalRules.DropsElemental"/>): II's Drop list holds the
    /// seven party records (ST <c>0xd57c</c>: record 6's in-party word, roster number and status cleared, nothing copied
    /// back); III's Drop on it clears the elemental flag (DOS ovl2 <c>0x3c92</c>). Only the party copy goes; the Guild
    /// record stays, so it can be added again.
    /// </summary>
    public static bool TryDropElemental(Party party, out string error, ElementalRules? elementals = null)
    {
        ArgumentNullException.ThrowIfNull(party);
        if (party.Elemental is null || !(elementals ?? party.Rules.ElementalRulesOrDefault).DropsElemental)
        {
            error = GameText.S("core.guild.not_in_party");
            return false;
        }
        party.DismissElemental();
        party.Elemental = null;
        error = "";
        return true;
    }

    /// <summary>
    /// Guild ▸ Drop. <paramref name="found"/> = the session's found-gold purse, which III counts as party gold for the Drop
    /// share (<see cref="DropGoldShare"/>; DOS has one treasury). <paramref name="elementals"/> = the save's elemental rules
    /// (<see cref="ElementalGuildRules.Elementals"/>); null = the party's rules.json block. Only the party copy of a loaded
    /// elemental is ever unloaded; its Guild record stays.
    /// </summary>
    public static bool TryDropFromParty(Party party, Character member, out string error, LootSack? found = null,
        ElementalRules? elementals = null)
    {
        ArgumentNullException.ThrowIfNull(party);
        ArgumentNullException.ThrowIfNull(member);

        if (!party.Members.Contains(member))
        {
            error = GameText.S("core.guild.not_in_party");
            return false;
        }

        if (party.Rules.DropGoldShare)
            DropGoldShare(party, member, found); // III ovl2 0x3d1c: before the count goes down (0x3e06)
        party.Members.Remove(member);
        member.InParty = false; // [C] OV1 0x225c: @218 := 0
        // [C] OV1 0x22d1: every Drop also clears party slot 7. III (ovl2 0x3c92 → 0x3e0b): only when no character is left.
        // II (ST 0xd57c / 0xc14a, s172): never; the elemental stays even alone (KeepsAlone).
        var rules = elementals ?? party.Rules.ElementalRulesOrDefault;
        if (!rules.DropKeeps || (!rules.KeepsAlone && !party.Adventurers.Any()))
            party.Elemental = null;
        error = "";
        return true;
    }

    /// <summary>
    /// III's Drop share (<see cref="RulesProfile.DropGoldShare"/>; DOS ovl2 <c>0x3d1c…0x3d8e</c>, Amiga <c>0xd4ae…0xd4fe</c>
    /// [C] s98): <c>share = party gold / characters</c> (a 32-bit divide, the count still holding <paramref name="member"/>,
    /// the elemental not counted; a negative result is 0); the account gets the share and the party gold loses it (not
    /// below 0). The remainder stays with the party. No status test, so a Dead member gets his share too. DOS has no cap on
    /// either; under the manual's 65,000 account cap (D3-026) only what fits leaves the purse (the remaster's choice: no
    /// gold is lost). The party gold is <see cref="Party.GoldCarried"/> plus <paramref name="found"/>'s gold (III's found gold
    /// is party gold too, <c>Phantasie3Campaign.PartyGold</c>); the share comes out of the found gold first, then the
    /// carried gold, as III's other payments. Returns the gold moved.
    /// </summary>
    public static long DropGoldShare(Party party, Character member, LootSack? found = null)
    {
        ArgumentNullException.ThrowIfNull(party);
        ArgumentNullException.ThrowIfNull(member);
        var characters = party.Members.Count;
        if (characters <= 0)
            return 0;
        var foundGold = Math.Max(0, found?.Gold ?? 0);
        var share = Math.Max(0, (party.GoldCarried + foundGold) / characters);
        var moved = share - member.AddBankGold(share, party.BankGoldCap);
        var fromFound = Math.Min(foundGold, moved);
        if (found is not null)
            found.Gold -= fromFound;
        party.GoldCarried = Math.Max(0, party.GoldCarried - (moved - fromFound));
        return moved;
    }

    /// <summary>
    /// Why Guild ▸ Purge would refuse <paramref name="member"/>, or null when it would purge: an elemental (every game and
    /// rule: an elemental is never removed from the game; DOS III string 406), not in the roster, or a party member under
    /// DOS III's refusal (<see cref="PurgePartyRule.Refused"/>, string 448). s160: DOS III tests these before its "Are you
    /// sure …?" question (guild_menu <c>0xafe3…0xb075</c>), so the front end asks only when this is null.
    /// </summary>
    public static string? PurgeRefusal(GuildRoster roster, Party party, Character member,
        PurgePartyRule partyRule = PurgePartyRule.ClearsElemental)
    {
        ArgumentNullException.ThrowIfNull(roster);
        ArgumentNullException.ThrowIfNull(party);
        ArgumentNullException.ThrowIfNull(member);
        if (ReferenceEquals(member, party.Elemental) || IsElementalRecord(member))
            return GameText.S("core.guild.cannot_purge_elemental"); // DOS III refuses too (string 406)
        if (Array.IndexOf(roster.Slots, member) < 0)
            return GameText.S("core.guild.not_in_roster");
        if (party.Members.Contains(member) && partyRule == PurgePartyRule.Refused)
            return GameText.S("core.guild.cannot_purge_party_member"); // DOS III string 448
        return null;
    }

    /// <summary>
    /// True for an elemental's record: race or birth race 16 (I / II's records: race 15 shown, birth race 16; III's: 16).
    /// No creatable race has that number, so a roster that somehow held an elemental record still could not purge it.
    /// </summary>
    public static bool IsElementalRecord(Character c)
    {
        ArgumentNullException.ThrowIfNull(c);
        return (int)c.Race == ElementalRace || (int)c.BirthRace == ElementalRace;
    }

    /// <summary>The elementals' race number in every game's records (I / II birth race 16, III race 16).</summary>
    public const int ElementalRace = 16;

    /// <summary>
    /// Guild ▸ Purge. A party member is purged and leaves the party; what happens to a loaded elemental (or whether DOS III
    /// refuses) is <paramref name="partyRule"/> (D3-040). The elemental records are never in the roster, so no rule can
    /// remove one from the game; only its party copy is unloaded.
    /// </summary>
    public static bool TryPurge(GuildRoster roster, Party party, Character member, out string error,
        PurgePartyRule partyRule = PurgePartyRule.ClearsElemental)
    {
        if (PurgeRefusal(roster, party, member, partyRule) is { } refusal)
        {
            error = refusal;
            return false;
        }
        var idx = Array.IndexOf(roster.Slots, member);
        if (party.Members.Contains(member))
        {
            party.Members.Remove(member);
            // I [C] OV1 0x2489: purging a party member also clears party slot 7. III (D3-040, manual): as III's Drop, the
            // elemental stays while a character is left. II (ST 0xd68a, s172): party records 0-5 only, so it always stays.
            if (partyRule == PurgePartyRule.ClearsElemental
                || (partyRule == PurgePartyRule.KeepsElemental && !party.Adventurers.Any()))
                party.Elemental = null;
        }

        roster.Slots[idx] = null;
        error = "";
        return true;
    }

    /// <summary>
    /// INPUT NAME rules [D✓] s65 (OV1 <c>0x1072..0x10e2</c>; manual silent): at most <see cref="MaxNameLength"/> characters,
    /// unique by exact (case-sensitive) compare against the roster and the elemental records 22–24
    /// (<paramref name="reservedNames"/>).
    /// </summary>
    /// <summary>[D✓] s65: DOS INPUT NAME keeps 9 characters.</summary>
    public const int MaxNameLength = 9;

    public static bool TryRename(Character member, string newName, GuildRoster roster, out string error,
        IEnumerable<string>? reservedNames = null)
    {
        ArgumentNullException.ThrowIfNull(member);
        ArgumentNullException.ThrowIfNull(roster);

        if (string.IsNullOrWhiteSpace(newName))
        {
            error = GameText.S("core.guild.name_required");
            return false;
        }

        newName = newName.Trim();
        if (newName.Length > MaxNameLength)
        {
            error = GameText.F("core.guild.name_at_most_x", MaxNameLength);
            return false;
        }

        if (!CharacterCreation.IsNameUnique(newName, roster.Occupied.Where(c => !ReferenceEquals(c, member)))
            || (reservedNames?.Contains(newName, StringComparer.Ordinal) ?? false))
        {
            error = GameText.S("core.guild.name_not_unique");
            return false;
        }

        member.Name = newName;
        error = "";
        return true;
    }

    public static bool TryEnroll(
        GuildRoster roster,
        Character created,
        out int slot,
        out string error)
    {
        ArgumentNullException.ThrowIfNull(roster);
        ArgumentNullException.ThrowIfNull(created);

        if (!CharacterCreation.IsNameUnique(created.Name, roster.Occupied))
        {
            slot = -1;
            error = GameText.S("core.guild.name_not_unique");
            return false;
        }

        // Phantasie I: the manual limit (21), not the DOS 22 — DIFFERENCES.md D-002 (RulesProfile.GuildMaxCharacters).
        if (!roster.HasRoom)
        {
            slot = -1;
            error = GameText.S("core.guild.roster_full");
            return false;
        }

        // The first hole; under the no-limit QoL option (guild-limit = none) a new slot when there is none.
        if (roster.FreeSlot() is var free and >= 0)
        {
            roster.Slots[free] = created;
            slot = free;
            error = "";
            return true;
        }

        slot = -1;
        error = GameText.S("core.guild.roster_full");
        return false;
    }

    public static IReadOnlyList<(int Slot, string Name, CharacterClass Class, Race Race)> ListMembers(
        GuildRoster roster) =>
        roster.Slots
            .Select((c, i) => (Slot: i, Char: c))
            .Where(x => x.Char is not null)
            .Select(x => (x.Slot, x.Char!.Name, x.Char.Class, x.Char.Race))
            .ToList();
}
