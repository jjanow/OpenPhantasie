# Phase 9 T6 — Phantasie IV parentage vs party transfer (s71 analysis)

Starting point for the s72 discussion with the user. Sources are from T6.0 (s71). **s81: the Japanese PC-98 manual was
found (source M below)**; it is IV's source of truth (user, s81), and where it settles a point this doc says so. The s72
decisions (section 3) are all built (s148–s149). The facts below come from:
- **A**: the 1990 postcard advert (`docs/manuals/phantasie4/phantasie4-postcard-ad-jp.pdf`)
- **B, C**: MSX Magazine 1991-03 pp.12–13 and 1991-07 pp.18–19 (`…/phantasie4-msx-magazine-…pdf`)
- **D**: the "Old Games" fan page (homepage1.nifty.com/PC-GAMER, Wayback 2007–13)
- **E**: the Tagoo review (2006)
- **F**: the game's own interface text, read off PC-98 disk 1 (Shift-JIS strings around 0x54300–0x56500; the same strings
  are on PC-88 and X68000)
- **G**: the RPG Codex interview with Wood
- **M** (s81): the Japanese PC-9801 manual, spell list and box (`docs/manuals/phantasie4/phantasie4-pc98-manual-jp.pdf`;
  English: `phantasie4-manual-en.md`, cited by printed page)

Anything not confirmed is marked `[?]`. RE of the rules source (X68000, T6.2) settles those.

## 1. What IV does (known)

- **Two ancestors per new character.** The screens are labelled 第１祖先 / 第２祖先, "First / Second ancestor" (F); neither is
  called father or mother. "The descendant born between them is the character who adventures this time" (C).
- **Each ancestor** is either "N: decided anew" or "H: a person from the Hall" (F). Its race is "S: same race / D: different
  race / H: the Hall person's race" (F), then race and class are chosen. **Settled (s79, X68000 code `[C]`):** N/H is
  asked for the **first** ancestor; S/D/H is asked for the **second**. S and D roll the second ancestor's race (S keeps
  the first ancestor's race 50% of the time, D finds a different one 75%), and the second ancestor is then a new person
  of that race whose class the player picks. H takes a Hall person. Rules:
  `reverse-engineering/x68000/phantasie4/logic-specs/ancestry.md` §3.
- **Manual (M, s81):** p. 17 describes the two ancestors: First ancestor "N: decide anew" (races and classes limited to
  Phantasie III's) or "H: people of the Hall" (registered as is); Second ancestor "same race as the First, or a different
  race"; "the computer calculates the genes and decides the newborn's race (whether a mixed race is born also depends on
  the computer)". It does not mention the code's third option for the second ancestor (H: race of a Hall person), the
  hybrid list, or "Not possible". Agrees with the code.
- **Mixed races:** "Create the new character as a descendant of a composite race?" (F). There are 18 base races (I's 15 plus
  Hill Giant, Gargoyle, Spriggan `[~]`) and 15 hybrids (Half Dwarf, Half Elf, Half Gnome, Dwarfling, Dwarf Gnome, Gnomeling,
  Sprixie, Half Ogre, Trollgre, Gnoll-Orc, Kobold-Dwarf, Koblin, Half Orc, Gnome-Goblin, Lizataur). Only Human + Orc → Half
  Orc is confirmed (screenshot, B). Gargoyle is a race whose class is also "Gargoyle". **Settled (s79) `[C]`:** all
  15 hybrid pairs are in the code (`_combi`; ancestry.md §4): Human+Dwarf Half Dwarf, Human+Elf Half Elf, Human+Gnome Half
  Gnome, Dwarf+Halfling Dwarfling, Dwarf+Gnome Dwarf Gnome, Halfling+Gnome Gnomeling, Pixie+Sprite Sprixie, Human+Ogre Half
  Ogre, Ogre+Troll Trollgre, Gnoll+Orc Gnoll-Orc, Dwarf+Kobold Kobold-Dwarf, Kobold+Goblin Koblin, Human+Orc Half Orc,
  Gnome+Goblin Gnome-Goblin, Lizard Man+Minotaur Lizataur. Any other mixed pair is "Not possible"; same race gives the
  same race. The Gargoyle race can only be the Gargoyle class `[C]`.
- **Inheritance:** stats are inherited "to some extent" (C, D), and trying for a model child usually gives an ordinary one (C).
  The one sample: Human Ranger 18/17/16/16/17 + Orc Fighter 19/12/14/12/6 → Half Orc Fighter 19/17/18/14/13. Dex 18 is above
  both parents, so there is a random part. Formula `[?]`. **Settled (s79) `[C]`:** each ability = three dice of
  6·u^k (u = rand(100)/100) + 3, then class and race modifiers; k comes from the two ancestors' average with their own
  race/class bonuses removed (high average → small k → high rolls). The sample above fits. ancestry.md §5.
- **Social class follows ancestry `[~]`:** with Lord Wood or Filmon as an ancestor "you start of noble birth, so levelling up
  costs no money" (C). **Code (s79) `[C]`:** social class is random by race; only a Hall ancestor who is a Super Hero makes
  the descendant Royalty, and a Dark Lord makes it an Outcast. Lord Wood (Royalty) and Filmon (Noble) pass nothing on, so
  magazine C's "noble birth" does not match the code. `DIFFERENCES-IV.md` D4-002: NOT A CONFLICT (the manual is silent;
  the code is built). **Manual (M, s81):** p. 22 lists four main classes (Noble, Craftsman, Laborer, Peasant), says
  Elves and Dwarves tend to be high class (the code has no such bias: D4-010, decided as the manual, the code's flat roll
  as option `p4.social-bias`), and that higher classes start with more money (p. 25: Noble
  768, Peasant 0 = the code's social × 256) and get more money at each level-up (= `_calpow`). It says nothing about
  ancestors passing on social class, so it neither confirms nor refutes magazine C. **s80:** the
  MSX2 code does the same `[C]`; training cost has no ancestry or social-class term, but each level-up pays
  social^social × level × 30 gold, so a noble-born character levels for free `[~]` (`reverse-engineering/x68000/phantasie4/logic-specs/training.md`). Class strings include III's four plus Super Hero, Dark Lord, Black Lord, Royalty, Outcast, Lord.
- **Hall of Glory (栄光の殿堂):** holds Nikademus, Filmon and Lord Wood from the start (B), plus imported characters. It keeps
  a "number of descendants" per person and generated biography lines ("fought …", "defended / failed to defend …", "freed …
  from the curse", "attempted conquest", "nothing of note") (F). **Settled (s79) `[C]`:** only III transfers write Hall
  slots (3 shipped + up to 8 imports); IV's own characters never enter it. **Manual (M) p. 8 agrees:** the three protagonists Lord Wood, Filmon and
  Nikademus plus up to 8 III transfers, "11 characters in all"; the Hall is only in Pendragon. A person's deeds line is built from three stored
  results (Phantasie I / II / III: not played / played / completed) and the social class; the latest game played wins.
  Using a Hall person as an ancestor adds 1 to their descendants (max 99). A Hall ancestor who completed any game gives
  the descendant the spell Divine Aid. ancestry.md §7–§8; record layout `FORMAT.md` §6.3.
- **Transfer is only from III, and only into the Hall** (D; F: "Transfer III's … data?", "N more can be transferred", "No
  more can be transferred"). Imported characters are **not** party members in IV: they are possible ancestors. **Settled by the manual (M) p. 8:**
  「ただし、このキャラクタは子孫を作るためのものにすぎません。」 ("However, these characters serve only to make descendants.")
  The PC-98 box back says you can move your old characters in "and share the adventure together with their
  descendants": loose ad copy (`DIFFERENCES-IV.md` D4-017, NOT A CONFLICT; the join is the option
  `p4.transfer-join`). The PC-98 and
  X68000 screens are confirmed; MSX2 is unchecked `[?]`. Which III version is accepted (presumably the Japanese III on the
  same platform) is `[?]`. **X68000 (s79) `[C]`:** IV reads III's player disk as raw 256-byte sectors (a 2D-format disk) and
  checks for the text "Pendragon" at sector 0x120; then 21 III character records of 160 bytes. The III disk itself is not
  in the repo, so which III release writes that format stays `[?]`.
- **Not found anywhere:** parents affecting appearance, or any "affinity" system. The old T6 wording ("shapes stats and
  affinities") is not backed by the sources found so far.
- **Conflict between sources:** B says a descendant of Nikademus can be made and is very strong. D says choosing him makes
  creation fail ("just a joke"). Leave both until RE; F's "Sorry, the ancestor you found was …" may be the joke path.
  **X68000 code (s79) `[C]`:** agrees with D. With Nikademus as first ancestor every option ends in "Not possible";
  as second ancestor his race (`  ?`) combines with nothing. "Sorry, the ancestor you found was …" is the ordinary
  message when the S/D race roll gives a race the player did not ask for. **s80:** the MSX2 code (B's platform) does the same `[C]` (`reverse-engineering/x68000/phantasie4/logic-specs/ancestry.md` §12).
  **Manual (M, s81): silent.** It names Nikademus only among the Hall persons whose profiles can be viewed (p. 8) and
  says a Hall person can be an ancestor (p. 11) with no exception and no failure case. `DIFFERENCES-IV.md` D4-001,
  DECIDED: the manual, Nikademus allowed (the code's "Not possible" as option `p4.nikademus`).

## 2. What it means for the remaster

The s71 analysis behind the decisions. How each point was built: `reverse-engineering/x68000/phantasie4/logic-specs/
ancestry.md` sections 8.2 (III → Hall), 8.3 (Guild join), 8.4 (I / II into the Hall) and 13 (lineage in I–III).

1. **How a III character enters IV.** Faithful default: it goes into the Hall as a possible ancestor, not into the party. A
   cross-game option (off by default) could let it join IV's party directly, as I→II and I/II→III do. That option needs IV's
   numbers for a "fresh" import (level 0 + training money in II/III's style) and a mapping of III fields to IV's. IV adds Luck,
   the Swim/Navigation/Throwing skills and more social classes. **Built (s149, `p4.transfer-join`)** with level 1 and 100
   XP per old level, not level 0 (`ancestry.md` section 8.3).
2. **Do imported characters get parents?** In IV, Hall persons have no ancestors of their own. Only characters created in IV
   do. So the faithful answer is no. The Hall also stores biography lines built from deeds in III. For the remaster to
   generate them from its own play, the shared model needs a light deeds record (added s72: `Character.Deeds`). A
   fallback is "nothing of note" for characters without a record.
3. **Feeding the Hall from I or II.** The original accepts only III. The unified game can offer "any earlier game's
   characters may enter IV's Hall" as an option (off = III only). It doesn't conflict with anything, because the Hall only
   stores ancestors. **Built (s149, `p4.transfer-games`).**
4. **Lineage as a cross-game option** (an ancestry step at creation in I–III). This is possible once IV's inheritance and
   hybrid rules are decoded. Costs:
   - hybrid races need race data in I–III (maxima, modifiers, spell and skill rules) that those games never had. This is the
     same kind of `[U]` as "missing monster data for cross-game modules";
   - the Hall must exist outside IV, as a shared roster of retired heroes.
   - Recommendation: plan it as a T5 option, gated on T6.2.
   - **Built (s149, module `p4.lineage`)** with base races only (user s72), so no hybrid data was needed. There is no
     shared Hall: I–III read a list rebuilt from the other games' saves and IV's Hall imports.
5. **Effect on the T1.1 model.** `Character.Lineage` (two or more `ParentRef(RaceId, ClassId)`, empty in I) fits a "decided
   anew" ancestor. A Hall ancestor needs more: a reference to the Hall person (and probably their stats at the time, because
   inheritance reads them). Extend `ParentRef` in T6.3, once the record layout is known. No change is needed now.
   **Layout known (s79) `[C]`:** IV's record keeps, per ancestor, only a kind (none / Hall / new) and an index: a Hall
   ancestor is the Hall slot; a new ancestor keeps social class, race and class (no abilities). Abilities are read only
   once, at creation, from the Hall record or the freshly rolled new ancestor. So `ParentRef` needs RaceId, ClassId, an
   optional social class and an optional Hall-person reference; no stats snapshot is needed for 1:1
   (`reverse-engineering/x68000/phantasie4/FORMAT.md` §6.2). **As built for lineage in I–III (s149):** `ParentRef(RaceId, ClassId, Name)`,
   a Hall person by its own game's ids and name, a new ancestor with no name; no social class or Hall slot is kept
   (`ancestry.md` section 13.4).
6. **A transfer chain for one party.** I→II→III works as in the originals (all lost except the Wand, level 0 with training
   money). III→IV ends the character's adventuring life: it becomes an ancestor. A player who wants "one party through all
   four" sees the old characters live on as parents of IV's party, unless the direct-join option above is on.

## 3. Decisions (user, s72, 2026-10-26)

- III → IV: faithful Hall-only by default (built s148), with a direct-join option, off by default (`p4.transfer-join`,
  s149).
- I and II characters into the Hall: yes, as an option, off = III only (`p4.transfer-games`, s149).
- Lineage at creation in I–III: yes, as a cross-game option, base races only, no hybrids (module `p4.lineage`, s149).
- Deeds record: added to the shared model (s72, `Character.Deeds`). Mapping to IV's Hall results (`ancestry.md` section
  8.1): I and II built for `p4.transfer-games` (s149); III's transfer copies III's own scores; the rest is a proposal.
