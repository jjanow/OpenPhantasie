namespace OpenPhantasie.Core;

public sealed partial class Phantasie3Campaign
{
    /// <summary>
    /// s218: a pre-generated Guild member (the C64 III's six, <c>reverse-engineering/c64/phantasie3/FORMAT.md</c> 3.5) as a
    /// III character. The record's values are kept as they are (attributes, HP, magic, gold, experience, age clock, the nine
    /// skills, social class, origin, town, the worn and carried items, the spells); what it does not hold takes Guild New's
    /// rules, with no draw: skill bonuses from the attributes, the worn items' points, every body area Okay, rank Front,
    /// saved option Thrust, and (D3-041, the manual's lifespans) I's lifespan for the race and LCK. D3-078 (user s218): a
    /// member without an age clock (the C64 v1.0 six) gets Guild New's start age, as <see cref="RollNewMember"/> gives it:
    /// DOS's ⌊(rnd(7) + 24) · f + 260⌋, then under the manual's lifespans I's start age for its lifespan.
    /// </summary>
    public Character? CreateStarterMember(GameSession session, StarterGuildMember member)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(member);
        var w = World;
        var creation = session.CreationTables;
        var c = new Character
        {
            Name = member.Name,
            Race = member.Race,
            BirthRace = member.Race,
            Class = member.Class,
            Status = member.Status,
            Level = member.Level,
            Experience = member.Experience,
            Score = member.Score,
            Stats = member.Stats,
            HpMax = member.HpMax,
            HpCur = member.HpCur,
            MpCeiling = member.MpCeiling,
            MagicMax = member.MagicMax,
            MagicCur = member.MagicCur,
            CurrentTown = member.Town,
            Skills = member.Skills,
        };
        c.AddBankGold(member.Gold, session.Party.BankGoldCap);
        c.SkillBonuses = CharacterCreation.SkillBonusSeeds(c.Stats);

        var p3 = c.P3;
        p3.SocialClass = member.SocialClass;
        p3.Rank = P3PartyRank.Front;
        p3.Origin = member.Origin is { } o && Enum.IsDefined((Phantasie3Origin)o) ? (Phantasie3Origin)o : Phantasie3Origin.Scandor;
        p3.FireBow = member.FireBow;

        var lifespans = !session.Options.P3NoLifespans() && creation.LifespanBandsByRace.Count > 0;
        if (member.AgeClock is { } age)
            c.AgeClock = age;
        else
        {
            var rng = session.CampaignAccess.Rng;
            c.AgeClock = w.CreationAge?.AgeClock(member.Race, c.Stats.Luck, P3Dice.Rnd(rng, 7)) ?? NoLifespanStartAge;
        }
        if (lifespans)
        {
            c.Lifespan = CharacterCreation.LifespanFor(member.Race, Math.Clamp(c.Stats.Luck, 8, 13), creation);
            if (member.AgeClock is null)
                c.AgeClock = CharacterCreation.StartingAgeClock(c.Lifespan, session.CampaignAccess.Rng);
        }

        c.SpellsKnown = new int[w.SpellCount];
        for (var s = 0; s < c.SpellsKnown.Length && s < member.SpellsKnown.Count; s++)
            c.SpellsKnown[s] = member.SpellsKnown[s];

        c.InventoryItemIds.AddRange(member.Carried);
        c.WornShield = member.WornShield;
        c.WornArmor = member.WornArmor;
        c.WornWeapon = member.WornWeapon;
        c.WornBow = member.WornBow;
        CombatGear.ApplyWornItemPoints(c, id => ItemPoints(session, id));
        c.BodyCondition = new P3BodyAreas().ToCondition();
        return c;
    }
}
