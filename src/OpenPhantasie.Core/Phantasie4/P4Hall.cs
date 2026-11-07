namespace OpenPhantasie.Core;

/// <summary>
/// Phase 09 T5, the Hall of Glory of one IV game (<c>_fame</c>, <c>FORMAT.md</c> section 6.3): the three shipped persons (slots
/// 0–2, <see cref="P4ServiceData.Hall"/>) and the save's imports in slots 3–10 (<see cref="P4Party.HallImports"/>), which only
/// the III transfer writes (<c>_mcopy</c>; <c>ancestry.md</c> section 8.2). Everything that lists or looks up a Hall person reads
/// <see cref="Persons"/>.
/// </summary>
public static class P4Hall
{
    /// <summary>The first slot an import takes; slots 0–2 are Lord Wood, Filmon and Nikademus.</summary>
    public const int FirstImportSlot = 3;

    /// <summary>The record's u8 fields (level, the three game results) hold at most this.</summary>
    public const int ByteCap = 255;

    /// <summary>IV's "?" race (Nikademus's): the stand-in for a III race IV has no number for.</summary>
    public const int UnknownRace = 33;

    /// <summary>III's class byte that makes Lord Wood "Dead" on the Hall sheet (<c>_dsp_hinfo</c>): 4, a Priest.</summary>
    public const int WoodDeadClass = 4;

    /// <summary>A name's length in the record (+01, 10 bytes).</summary>
    public const int NameLength = 10;

    /// <summary>The shipped persons, then this save's imports, in slot order.</summary>
    public static IReadOnlyList<P4HallPerson> Persons(IReadOnlyList<P4HallPerson> shipped, P4Party party)
    {
        ArgumentNullException.ThrowIfNull(shipped);
        ArgumentNullException.ThrowIfNull(party);
        return
        [
            .. shipped.Where(h => h.Slot < FirstImportSlot || party.HallImports[h.Slot] is null),
            .. party.HallImports.Skip(FirstImportSlot).OfType<P4HallPerson>(),
        ];
    }

    /// <summary>The import slots still empty (<c>_mtrans</c> counts the unused slots of all 11; 0–2 are always used).</summary>
    public static int FreeSlots(P4Party party)
    {
        ArgumentNullException.ThrowIfNull(party);
        return party.HallImports.Skip(FirstImportSlot).Count(h => h is null);
    }

    /// <summary>
    /// The Hall sheet's status (+1A, <c>_dsp_hinfo</c>): Lord Wood (slot 0) is recomputed on display, Alive unless an import
    /// has class byte 4 (<see cref="WoodDeadClass"/>; the code's test, kept as it is); everyone else their stored flag.
    /// </summary>
    public static bool IsAlive(P4HallPerson h, IEnumerable<P4HallPerson> persons)
    {
        ArgumentNullException.ThrowIfNull(h);
        ArgumentNullException.ThrowIfNull(persons);
        return h.Slot == 0 ? !persons.Any(p => p.Slot >= FirstImportSlot && p.Class == WoodDeadClass) : h.Alive;
    }

    /// <summary>
    /// <c>_mcopy</c>: a III character as a Hall record in <paramref name="slot"/>. IV copies III's record bytes; the remaster
    /// maps its III character by meaning (<c>ancestry.md</c> section 8.2): name (10), race (I's numbering = IV's 0–14; an Undead's
    /// living race <c>[~]</c>), class (III's order = IV's), level, social class (III's Darklord 4 / Superhero 5 are IV's 5 / 4),
    /// origin (III's order = IV's import list), the five abilities, the I / II / III results (III's I score, II score and
    /// score; capped at 255 <c>[~]</c>), descendants 0, alive unless Dead.
    /// </summary>
    public static P4HallPerson FromPhantasieIII(Character c, int slot)
    {
        ArgumentNullException.ThrowIfNull(c);
        if (slot is < FirstImportSlot or >= P4Party.HallSlots)
            throw new ArgumentOutOfRangeException(nameof(slot), $"No import slot {slot}.");
        var p3 = c.Phantasie3;
        var social = p3?.SocialClass ?? 0;
        social = social switch
        {
            Phantasie3CharacterData.Darklord => DarkLord,
            Phantasie3CharacterData.Superhero => SuperHero,
            _ => social,
        };
        var s = c.Stats;
        return new P4HallPerson(
            slot, c.Name.Length > NameLength ? c.Name[..NameLength] : c.Name, RaceOf(c), (int)c.Class,
            Math.Clamp(c.Level, 0, ByteCap), social, (int)(p3?.Origin ?? Phantasie3Origin.Scandor),
            [s.Str, s.Int, s.Dex, s.Con, s.Cha],
            [Result(p3?.Phantasie1Score ?? 0), Result(p3?.Phantasie2Score ?? 0), Result(c.Score)],
            Descendants: 0, DeedsLine: 0, Alive: c.Status != CharacterStatus.Dead);
    }

    /// <summary>IV's social classes 4 and 5 (<c>names.json</c>).</summary>
    public const int SuperHero = 4, DarkLord = 5;

    /// <summary>
    /// The race IV gets: I's numbering 0–14 is IV's. III's Undead (I → III's level-20 Undead, D3-007) has no IV race; the
    /// remaster gives the living race (<see cref="Character.BirthRace"/>, as the magical pool does) and "?" when there is
    /// none <c>[~]</c> (which byte the Japanese III writes is not known).
    /// </summary>
    public static int RaceOf(Character c)
    {
        ArgumentNullException.ThrowIfNull(c);
        var race = c.Race == Race.Undead ? c.BirthRace : c.Race;
        return race is >= Race.Human and <= Race.Minotaur ? (int)race : UnknownRace;
    }

    private static int Result(int score) => Math.Clamp(score, 0, ByteCap);

    // ---- T5 options (s149): I / II into the Hall (section 8.4), III into the Guild (section 8.3) -----------------------

    /// <summary>A game result: 100 = completed, 1 = played, 0 = not played (<c>_dsp_hinfo</c>'s thresholds, section 8).</summary>
    public const int Completed = 100, Played = 1;

    /// <summary>The social class an I / II character gets in the Hall: Noble, what DOS III writes for every I / II arrival <c>[~]</c>.</summary>
    public const int EarlierGameSocial = 3;

    /// <summary>The deed that completes Phantasie I: slew the Black Lord (<see cref="Endgame.GrantVictory"/>).</summary>
    public static readonly Deed CompletedI = new(Deed.Slew, new ContentId(GameId.P1, Endgame.BlackLordId));

    /// <summary>The deed that completes Phantasie II: destroyed the orb (II's ending, Filmon's ceremony).</summary>
    public static readonly Deed CompletedII = new(Deed.Destroyed, new ContentId(GameId.P2, Phantasie2Campaign.OrbId));

    /// <summary>
    /// <c>ancestry.md</c> section 8.4 <c>[~]</c>: a Phantasie I or II character (the remaster's own save of
    /// <paramref name="source"/>) as a Hall record in <paramref name="slot"/>. Name, race, class, level, abilities and alive as
    /// <see cref="FromPhantasieIII"/>; social Noble; origin I Gelnor, II its own (Gelnor / Ferronrah / Netherworld = the
    /// import list's 0–2); p1 / p2 = 100 with the completing deed, 1 when the character played that game (its save, or the
    /// record it brought from the other game), else 0; p3 = 0.
    /// </summary>
    public static P4HallPerson FromEarlierGame(Character c, GameId source, int slot)
    {
        ArgumentNullException.ThrowIfNull(c);
        if (source is not (GameId.P1 or GameId.P2))
            throw new ArgumentOutOfRangeException(nameof(source), source, "Only Phantasie I or II characters come this way.");
        if (slot is < FirstImportSlot or >= P4Party.HallSlots)
            throw new ArgumentOutOfRangeException(nameof(slot), $"No import slot {slot}.");
        var deeds = c.Deeds ?? [];
        var playedI = source == GameId.P1 || c.Phantasie2?.Origin == Phantasie2Origin.Gelnor;
        var playedII = source == GameId.P2 || c.Phantasie2 is not null;
        var origin = source == GameId.P2 ? (int)(c.Phantasie2?.Origin ?? Phantasie2Origin.Ferronrah) : (int)Phantasie3Origin.Gelnor;
        var s = c.Stats;
        return new P4HallPerson(
            slot, c.Name.Length > NameLength ? c.Name[..NameLength] : c.Name, RaceOf(c), (int)c.Class,
            Math.Clamp(c.Level, 0, ByteCap), EarlierGameSocial, origin, [s.Str, s.Int, s.Dex, s.Con, s.Cha],
            [GameResult(deeds.Contains(CompletedI), playedI), GameResult(deeds.Contains(CompletedII), playedII), 0],
            Descendants: 0, DeedsLine: 0, Alive: c.Status != CharacterStatus.Dead);
    }

    private static int GameResult(bool completed, bool played) => completed ? Completed : played ? Played : 0;

    /// <summary>A Guild member's name length (<c>_mkname</c>, section 1).</summary>
    public const int MemberNameLength = 8;

    /// <summary>Experience a III character brings per level: DOS III's own transfer rule (D3-072), section 8.3 <c>[~]</c>.</summary>
    public const int JoinXpPerLevel = 100;

    /// <summary>Whether a III character can join IV's Guild: it needs one of IV's races 0–14 (section 8.3).</summary>
    public static bool CanJoin(Character c) => RaceOf(c) != UnknownRace;

    /// <summary>
    /// <c>ancestry.md</c> section 8.3 <c>[~]</c>: a Phantasie III character as a new IV Guild member in <paramref name="slot"/>.
    /// <c>_mkdetail</c> builds the record (<see cref="P4CreationRecord.Build"/>: Luck, skills, HP, magic level, MP, starting
    /// kit, aging, level 1) from III's race, class and five abilities, made in <paramref name="town"/>; then III's name (8),
    /// social class (as the Hall maps it) and its gold, experience 100 × III's level, Divine Aid when III's record has it,
    /// the origin of the same name, Dead when III's character is dead. <paramref name="agingRace"/>: as creation passes it.
    /// </summary>
    public static P4Member JoinFromPhantasieIII(GameOptions o, P4Tables t, IRandomSource rng, Character c, int slot, int town,
        int? agingRace = null)
    {
        ArgumentNullException.ThrowIfNull(t);
        ArgumentNullException.ThrowIfNull(c);
        if (!CanJoin(c))
            throw new ArgumentException($"{c.Name} has no Phantasie IV race.", nameof(c));
        if (slot is < 0 or >= P4Party.RosterSlots)
            throw new ArgumentOutOfRangeException(nameof(slot), $"No Guild slot {slot}.");
        var race = RaceOf(c);
        var st = c.Stats;
        var name = c.Name.Length > MemberNameLength ? c.Name[..MemberNameLength] : c.Name;
        var r = P4CreationRecord.Build(o, t, rng, name, race, (int)c.Class,
            [.. new[] { st.Str, st.Int, st.Dex, st.Con, st.Cha }.Select(v => Math.Clamp(v, 0, ByteCap))], [], town, agingRace);
        var m = P4Member.From(r);
        var hall = FromPhantasieIII(c, FirstImportSlot);
        m.Social = hall.Social;
        m.Account = P4Creation.StartingGold(t.Creation, m.Social);
        m.Experience = (long)JoinXpPerLevel * Math.Max(0, c.Level);
        if (c.Phantasie3?.DivineAid == true)
            m.Spells[t.Creation.DivineAidSpell - 1] = 1;
        m.Origin = SameNamedOrigin(c.Phantasie3?.Origin) ?? m.Origin;
        if (c.Status == CharacterStatus.Dead)
        {
            m.Condition = P4Member.Dead;
            m.Hp = 0;
        }
        m.Town = town;
        m.GuildSlot = slot;
        return m;
    }

    /// <summary>IV's <c>_origin</c> entry named as III's origin (Gelnor 0, Ferronrah 1, Scandor 2, Plane of Darkness 6); null for the rest.</summary>
    public static int? SameNamedOrigin(Phantasie3Origin? origin) => origin switch
    {
        Phantasie3Origin.Gelnor => 0,
        Phantasie3Origin.Ferronrah => 1,
        Phantasie3Origin.Scandor => 2,
        Phantasie3Origin.Darkness => 6,
        _ => null,
    };
}
