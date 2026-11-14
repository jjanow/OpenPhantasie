# Phase 9 T6 — Phantasie IV parentage vs party transfer (s71 analysis)

What IV does with ancestors and transfers, and how the remaster handles it. The decisions (section 3) are all built.
Sources (T6.0):
- **A**: the 1990 postcard advert (`docs/manuals/phantasie4/phantasie4-postcard-ad-jp.pdf`)
- **B, C**: MSX Magazine 1991-03 pp.12–13 and 1991-07 pp.18–19 (`…/phantasie4-msx-magazine-…pdf`)
- **D**: the "Old Games" fan page (homepage1.nifty.com/PC-GAMER, Wayback 2007–13)
- **E**: the Tagoo review (2006)
- **F**: the game's own interface text, read off PC-98 disk 1 (Shift-JIS strings around 0x54300–0x56500; the same strings
  are on PC-88 and X68000)
- **G**: the RPG Codex interview with Wood
- **M**: the Japanese PC-9801 manual, spell list and box (`docs/manuals/phantasie4/phantasie4-pc98-manual-jp.pdf`;
  English: `phantasie4-manual-en.md`, cited by printed page). **IV's source of truth** (user).

`[C]` = read in the X68000 code (the rules source); the MSX2 code (B's platform) was checked to do the same where noted.
`[?]` = still unconfirmed. Rules: `reverse-engineering/x68000/phantasie4/logic-specs/ancestry.md` (cited below as
ancestry.md).

## 1. What IV does (known)

- **Two ancestors per new character.** The screens are labelled 第１祖先 / 第２祖先, "First / Second ancestor" (F); neither is
  called father or mother. "The descendant born between them is the character who adventures this time" (C).
- **Each ancestor's choices** (F): "N: decided anew" or "H: a person from the Hall"; race "S: same race / D: different race /
  H: the Hall person's race"; then race and class. **Code `[C]`:** N/H is asked for the **first** ancestor; S/D/H for the
  **second**. S and D roll the second ancestor's race (S keeps the first ancestor's race 50% of the time, D finds a
  different one 75%); the second ancestor is then a new person of that race whose class the player picks. H takes a Hall
  person. ancestry.md section 3.
  **Manual (M) p. 17:** First ancestor "N: decide anew" (races and classes limited to Phantasie III's) or "H: people of the
  Hall" (registered as is); Second ancestor "same race as the First, or a different race"; "the computer calculates the
  genes and decides the newborn's race (whether a mixed race is born also depends on the computer)". It does not mention
  the code's H option for the second ancestor, the hybrid list or "Not possible". Agrees with the code.
- **Mixed races:** "Create the new character as a descendant of a composite race?" (F). 18 base races (I's 15 plus Hill
  Giant, Gargoyle, Spriggan `[~]`) and 15 hybrids. **Code `[C]`** (`_combi`; ancestry.md section 4): Human+Dwarf Half
  Dwarf, Human+Elf Half Elf, Human+Gnome Half Gnome, Dwarf+Halfling Dwarfling, Dwarf+Gnome Dwarf Gnome, Halfling+Gnome
  Gnomeling, Pixie+Sprite Sprixie, Human+Ogre Half Ogre, Ogre+Troll Trollgre, Gnoll+Orc Gnoll-Orc, Dwarf+Kobold
  Kobold-Dwarf, Kobold+Goblin Koblin, Human+Orc Half Orc (also in B's screenshot), Gnome+Goblin Gnome-Goblin, Lizard
  Man+Minotaur Lizataur. Any other mixed pair is "Not possible"; same race gives the same race. The Gargoyle race can only
  be the Gargoyle class `[C]`.
- **Inheritance:** stats are inherited "to some extent" (C, D), and trying for a model child usually gives an ordinary one
  (C). **Code `[C]`:** each ability = three dice of 6·u^k (u = rand(100)/100) + 3, then class and race modifiers; k comes
  from the two ancestors' average with their own race/class bonuses removed (high average → small k → high rolls).
  ancestry.md section 5. It fits the one sample: Human Ranger 18/17/16/16/17 + Orc Fighter 19/12/14/12/6 → Half Orc
  Fighter 19/17/18/14/13 (Dex 18 is above both parents: the random part).
- **Social class:** magazine C says that with Lord Wood or Filmon as an ancestor "you start of noble birth, so levelling up
  costs no money" `[~]`. **Code `[C]`, MSX2 too:** social class is random by race; only a Hall ancestor who is a Super Hero
  makes the descendant Royalty, and a Dark Lord makes it an Outcast. Lord Wood (Royalty) and Filmon (Noble) pass nothing
  on, so C does not match the code (`DIFFERENCES-IV.md` D4-002: NOT A CONFLICT, the manual is silent, the code is built).
  Training cost has no ancestry or social-class term, but each level-up pays social^social × level × 30 gold, so a
  noble-born character levels for free `[~]` (`reverse-engineering/x68000/phantasie4/logic-specs/training.md`). Class
  strings include III's four plus Super Hero, Dark Lord, Black Lord, Royalty, Outcast, Lord.
  **Manual (M):** p. 22 lists four main classes (Noble, Craftsman, Laborer, Peasant) and says Elves and Dwarves tend to be
  high class (the code has no such bias: D4-010, decided as the manual, the code's flat roll as option `p4.social-bias`);
  higher classes start with more money (p. 25: Noble 768, Peasant 0 = the code's social × 256) and get more at each
  level-up (= `_calpow`). It says nothing about ancestors passing on social class.
- **Hall of Glory (栄光の殿堂):** holds Nikademus, Filmon and Lord Wood from the start (B), plus imported characters. It keeps
  a "number of descendants" per person and generated biography lines ("fought …", "defended / failed to defend …", "freed …
  from the curse", "attempted conquest", "nothing of note") (F). **Code `[C]`:** only III transfers write Hall slots (3
  shipped + up to 8 imports); IV's own characters never enter it. **Manual (M) p. 8 agrees:** the three protagonists plus
  up to 8 III transfers, "11 characters in all"; the Hall is only in Pendragon. A person's deeds line is built from three
  stored results (Phantasie I / II / III: not played / played / completed) and the social class; the latest game played
  wins. Using a Hall person as an ancestor adds 1 to their descendants (max 99). A Hall ancestor who completed any game
  gives the descendant the spell Divine Aid. ancestry.md sections 7–8; record layout `FORMAT.md` section 6.3.
- **Transfer is only from III, and only into the Hall** (D; F: "Transfer III's … data?", "N more can be transferred", "No
  more can be transferred"). Imported characters are possible ancestors, **not** party members. **Manual (M) p. 8:**
  「ただし、このキャラクタは子孫を作るためのものにすぎません。」 ("However, these characters serve only to make descendants.")
  The PC-98 box back says you can move your old characters in "and share the adventure together with their
  descendants": loose ad copy (`DIFFERENCES-IV.md` D4-017, NOT A CONFLICT; the join is the option `p4.transfer-join`).
  The PC-98 and X68000 screens are confirmed; MSX2 is unchecked `[?]`. **Code `[C]`:** IV reads III's player disk as raw
  256-byte sectors (a 2D-format disk), checks for the text "Pendragon" at sector 0x120, then reads 21 III character
  records of 160 bytes. Which III release writes that format (presumably the Japanese III on the same platform) is `[?]`:
  the III disk is not in the repo.
- **Not found anywhere:** parents affecting appearance, or any "affinity" system (the old T6 wording "shapes stats and
  affinities" has no source).
- **Nikademus as an ancestor.** B says a descendant of Nikademus can be made and is very strong; D says choosing him makes
  creation fail ("just a joke"). **Code `[C]`, MSX2 too (ancestry.md section 12):** agrees with D. With Nikademus as first
  ancestor every option ends in "Not possible"; as second ancestor his race (`  ?`) combines with nothing. "Sorry, the
  ancestor you found was …" is the ordinary message when the S/D race roll gives a race the player did not ask for.
  **Manual (M): silent.** It names Nikademus among the Hall persons whose profiles can be viewed (p. 8) and says a Hall
  person can be an ancestor (p. 11) with no exception. `DIFFERENCES-IV.md` D4-001, DECIDED: the manual, Nikademus allowed
  (the code's "Not possible" as option `p4.nikademus`).

## 2. What it means for the remaster

The analysis behind the decisions. How each point was built: ancestry.md sections 8.2 (III → Hall), 8.3 (Guild join),
8.4 (I / II into the Hall) and 13 (lineage in I–III).

1. **How a III character enters IV.** Faithful default: into the Hall as a possible ancestor, not the party. The cross-game
   option `p4.transfer-join` (off by default) lets it join IV's party directly, as I→II and I/II→III do. It maps III's
   fields to IV's (IV adds Luck, the Swim/Navigation/Throwing skills and more social classes) and gives level 1 with 100
   XP per old level, not II/III's level 0 (ancestry.md section 8.3).
2. **Do imported characters get parents?** No: in IV, Hall persons have no ancestors of their own; only characters created
   in IV do. The Hall's biography lines need a deeds record, so the shared model has `Character.Deeds`; a character
   without a record gets "nothing of note".
3. **Feeding the Hall from I or II.** The original accepts only III. Option `p4.transfer-games` (off = III only) lets any
   earlier game's characters enter IV's Hall. No conflict: the Hall only stores ancestors.
4. **Lineage as a cross-game option** (an ancestry step at creation in I–III), module `p4.lineage`. Hybrid races would need
   race data I–III never had (maxima, modifiers, spell and skill rules; the same kind of `[U]` as "missing monster data for
   cross-game modules"), so it uses base races only (user). There is no shared Hall: I–III read a list rebuilt from the
   other games' saves and IV's Hall imports.
5. **Effect on the T1.1 model.** IV's record keeps, per ancestor, only a kind (none / Hall / new) and an index: a Hall
   ancestor is the Hall slot; a new ancestor keeps social class, race and class (no abilities). Abilities are read only
   once, at creation, from the Hall record or the freshly rolled new ancestor, so 1:1 needs no stats snapshot
   (`reverse-engineering/x68000/phantasie4/FORMAT.md` section 6.2). **As built for lineage in I–III:**
   `Character.Lineage` holds two or more `ParentRef(RaceId, ClassId, Name)` (empty without lineage): a Hall person by its own game's
   ids and name, a new ancestor with no name; no social class or Hall slot is kept (ancestry.md section 13.4).
6. **A transfer chain for one party.** I→II→III works as in the originals (all lost except the Wand, level 0 with training
   money). III→IV ends the character's adventuring life: it becomes an ancestor of IV's party, unless the direct-join
   option is on.

## 3. Decisions (user, s72, 2026-10-26)

- III → IV: faithful Hall-only by default, with a direct-join option, off by default (`p4.transfer-join`).
- I and II characters into the Hall: yes, as an option, off = III only (`p4.transfer-games`).
- Lineage at creation in I–III: yes, as a cross-game option, base races only, no hybrids (module `p4.lineage`).
- Deeds record: in the shared model (`Character.Deeds`). Mapping to IV's Hall results (ancestry.md section 8.1): I and II
  built for `p4.transfer-games`; III's transfer copies III's own scores; the rest is a proposal.
