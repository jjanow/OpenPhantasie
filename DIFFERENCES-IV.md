# DIFFERENCES-IV — Phantasie IV written sources vs game code

**The Japanese PC-9801 manual was found in s81** (MOCAGH; `docs/manuals/phantasie4/phantasie4-pc98-manual-jp.pdf`,
transcription and English in `docs/manuals/phantasie4/phantasie4-manual-en.md`, cited as "manual p. n" = printed page).
It is treated as IV's source of truth, as the manuals are for I–III (user s81: manual first, above the magazines and catalogue). The box also held a supplementary manual (start-up only, no
rules) and a spell list (`phantasie4-pc98-spell-list-jp.pdf`). Secondary written sources: the MSX Magazine articles, the
postcard advert and the item catalogue in `docs/manuals/phantasie4/` (summary: `docs/plan/09-trilogy-iv-parentage.md`).
Same rules as `DIFFERENCES.md` (Phantasie I): every conflict between a written source and the code goes to the user,
who decides. Keep each entry to **Source · Code · Decision · Remaster · Side effects** (plus the spec). IDs are
`D4-nnn`. Code addresses: X68000 (`reverse-engineering/x68000/phantasie4/`) and MSX2
(`reverse-engineering/msx2/phantasie4/FORMAT.md`, segment:address).

**Status:** `PENDING` = raised, no decision yet · `ON HOLD` = the user waits for more evidence · `DECIDED: <side>` +
`built` / `not built` · `NOT A CONFLICT`.

| ID | Topic | Status |
|---|---|---|
| D4-001 | Nikademus as an ancestor: magazine B "very strong descendant" vs "Not possible" in both codes; manual silent | DECIDED: manual, Nikademus allowed (counts as Human; code "Not possible" as option) · built (`p4.nikademus`) |
| D4-002 | Lord Wood / Filmon ancestor: magazine C "noble birth, levelling costs no money" vs nothing passed on; manual silent on ancestry, confirms level-up gold by class | NOT A CONFLICT (manual silent; code built s83) |
| D4-010 | Social class: "Elves and Dwarves tend to be high class" vs one roll for all five base races | DECIDED: manual, Elves / Dwarves roll +10 (user s82; code flat roll as option) · built (`p4.social-bias`) |
| D4-011 | (merged into D4-031: gold caps) | — |
| D4-012 | Base stride: Halfling 5 vs 6; Orc "about as fast as humans" (8) vs 7 | DECIDED: manual, Halfling 5 / Orc 8 (code 6 / 7 as option) · built (`p4.strides`) |
| D4-013 | Gargoyle race: may be Thief / Fighter by the manual's wording vs Gargoyle class only | DECIDED: manual, Gargoyles may be Thief / Fighter (code Gargoyle-only as option) · built (`p4.gargoyle-classes`) |
| D4-014 | Training price by race (humans cheapest, guild favourites, others much dearer) vs Charisma only | NOT A CONFLICT (Charisma carries the race effect; code) |
| D4-015 | Priests "reach higher levels than other classes and at the top can cast every spell" vs one level gate for all; 35 of 65 spells | DECIDED: manual, Priest cap 35, +6 spells per level 31–35 (user s82; code as option) · built (`p4.priest-levels`) |
| D4-016 | Fighter spells: "3–4 minor spells even at high level" vs 7 in the class table | DECIDED: manual, Fighters 4 minor spells (code 7 as option) · built (`p4.fighter-spells`) |
| D4-017 | Box back: imported characters adventure *together with* their descendants vs manual p. 8 and code (Hall only) | NOT A CONFLICT (box copy; Hall only) · option `p4.transfer-join` built s149 (off in every preset) |
| D4-018 | Long Sword: 14 points (manual p. 57) vs 13 (code and catalogue) | DECIDED: manual 14 (code 13 as option) · built (`p4.long-sword`) |
| D4-019 | Thieves have fewer HP than other classes (manual p. 17) vs Wizards' HP table lower than Thieves' | DECIDED: manual, Thief HP = lowest other class − 1 (user s82; code table as option) · built (`p4.thief-hp`) |
| D4-020 | Shop resale "at a slightly higher price" vs sold for price / 4 + 1 and resold at full price | DECIDED: manual, resale at paid × 1.25 (user s82; code full-price resale as option) · built (`p4.resale`) |
| D4-030 | Town entry: manual auto-deposits party gold into accounts; code shares out party experience, gold untouched | DECIDED: manual auto-deposit + XP share-out (code XP-only as option) · built (`p4.town-entry`) |
| D4-031 | Gold caps: manual 65,000 for party gold and accounts; code caps 65,000 only in the bank (sell 65,535, loot 999,999,999, level-up 65,535) | DECIDED: manual 65,000 on every path, withdraw bug fixed (option: uncapped) · built (`p4.gold-cap`) |
| D4-032 | Town inn: manual "stay at least two weeks"; code passes no time | DECIDED: manual, town inn stay takes two weeks (code no-time as option) · built (`p4.inn-time`) |
| D4-033 | Lifespans: manual Elf ~2000, Dwarf/Gnome 200, Human 80, others 40–50; code Elf never dies, Halfling 215–345, Pixie/Sprite/Human 89–138, races 7+ and hybrids 30–49 | DECIDED: manual lifespans (X68000 code as option; QoL: aging normalised across I–IV, `shared-aging`, `docs/plan/13-aging-normalised.md`) · built (`p4.lifespans`) |
| D4-034 | Aging effect: manual "abilities decline"; X68000 sets all abilities and Luck to 0 (MSX2 × 0.8) | DECIDED: abilities × 0.8 once, as MSX2 (X68000 zeroing as option) · built (`p4.aging`) |
| D4-035 | Healing potions: wounds healed (manual Tables 2–3: 1–3 / 4–6 / 7–9 / 10 = Healing 1–4) vs code one step lower (potion n heals trunc((n + 1) / 3) wound points); Healing 6 HP 35 vs 36 | DECIDED: manual (X68000 code as option) · built (`p4.potion-wounds`) |
| D4-036 | Spot Item: manual finds items after combat (high value also identifies them); treasure automatic outdoors when the enemy is wiped out vs code never reads Spot Item, flat 45 % (dungeon) / level + 18 % (outdoors) | DECIDED: manual (X68000 code as option) · built (`p4.spot-item`) |
| D4-037 | Throwing skill: manual a separate skill (Wizards start missile training at 7) vs code never reads it; thrown weapons use Fire Bow | DECIDED: manual (X68000 code as option) · built (`p4.throwing`) |
| D4-038 | Ships: manual "wrecked in combat or wear out" vs no combat damage or wear in the code (lost only by running aground, the shining sea, or buying another) | DECIDED: manual, 5 % on a lost / fled sea fight, 4000 steps (user s84; X68000 code as option) · built (`p4.ship-wear`, `p4.ship-life`) |
| D4-039 | Levitation outside combat: manual lasts 5 × the slowest stride vs code until the end of the next fight, any distance, no effect outdoors | DECIDED: manual (X68000 code as option) · built (`p4.levitation`) |
| D4-040 | Navigation: manual "do not use it below 50 %" vs code has no use action or threshold (best navigator steers automatically) | DECIDED: advice with a risk: below 50 an extra (50 − skill) % drift per step (user s84; X68000 code as option) · built (`p4.navigation`) |
| D4-041 | Dungeon Examine (E): manual party searches, finds traps and treasure more efficiently vs code an automatic walk to the least-visited squares, no search roll | DECIDED: manual (X68000 code as option) · built (`p4.examine`) |
| D4-042 | Attack strikes twice, Slash 3–4 times (manual) vs one hit roll and one damage roll per action | DECIDED: manual (X68000 code as option) · built (`p4.strikes`) |
| D4-043 | Losing the head or torso kills regardless of HP (manual) vs party member only "Out" and healable | DECIDED: manual (X68000 code as option) · built (`p4.head-torso`) |
| D4-044 | MP costs: Slow 1/2, Arrow Flame 2, Party Arrow Flame 1, Dissolve differ between manual, spell list and code | DECIDED: manual (X68000 code as option) · built (`p4.mp-costs`) |
| D4-045 | Party Arrow Flame: manual raises bow hit chance and damage vs code damage only | DECIDED: manual (X68000 code as option) · built (`p4.party-arrow-flame`) |
| D4-046 | Auto-combat spell sets (manual p. 32) vs the code's lists (extra spells; never Sleep or Dissolve) | DECIDED: manual (X68000 code as option) · built (`p4.auto-spells`) |
| D4-047 | Flameball: manual around the caster, no effect on strong armour, radius 1/2/2.5/3 vs code centred on the target, min 1 damage, ranks 3 and 4 the same area | DECIDED: manual (X68000 code as option) · built (`p4.flameball`) |
| D4-048 | Night watch: manual a good Listen wakes the whole party vs code only cancels the monsters' free round | DECIDED: manual (X68000 code as option) · built (`p4.night-watch`) |
| D4-049 | Beg for mercy: manual hand over gold and goods vs code gold → gold mod 256, no items taken | DECIDED: manual (X68000 code as option) · built (`p4.beg-mercy`) |
| D4-050 | Weakness: manual the enemy's attacks do less damage vs code lowers their defence against party hits | DECIDED: manual (X68000 code as option) · built (`p4.weakness`) |
| D4-051 | Spell damage: Fire Flash 1–N and Mind Blast 1–N (manual) vs rand(N) × Int / 15 and 1…N−1 with an XP-based immunity; effect by caster level vs Int / MP | DECIDED: manual (X68000 code as option) · built (`p4.spell-damage`) |
| D4-052 | Spell wording: Slow 2 ¾ vs ¼ stride, Dissolve "slowly" vs at once, Fear "flees" vs level −6, Teleportation "another place" vs previous square | DECIDED: manual, Dissolve over 3 rounds; Teleportation wording (previous square, option random / entrance) (user s84; X68000 code as option) · built (`p4.spell-wording`, `p4.teleport`) |
| D4-053 | Identify: manual "does not always succeed" vs always succeeds | DECIDED: manual (X68000 code as option) · built (`p4.identify`) |
| D4-054 | Monster Evaluation: manual "sometimes reports wrongly" vs always the true level | DECIDED: manual (X68000 code as option) · built (`p4.monster-eval`) |
| D4-055 | Monster HP (manual silent): X68000 gives 80 % of monsters 1.8 × base HP (probable precedence slip), MSX2 0.5–1.4 × base | DECIDED: MSX2 formula (user s82; X68000 as option) · built (`p4.monster-hp`) |
| D4-056 | Very weak ancestors (bonus-free average below 5.0) give maximal abilities: the exponent lookup runs out of table (CPU-checked s82); manual silent | DECIDED: bug fixed, last table value k = 5.635 (user s84; code's 18s as option) · built (`p4.weak-ancestors`) |
| D4-057 | Outdoor "M Map": manual a view switch (3 × 3 overview, can move on it); code also runs the square's step (hours, tile, encounter roll) on each toggle | DECIDED (s98): the manual; code as option `p4.map-step` |
| D4-058 | Aim Blow: manual "aimed at the monster's head or torso"; code only −15 to hit, the hit location is the ordinary melee roll (head ≈ 3 %, torso ≈ 8 %) | DECIDED: manual (head 25 % / torso 75 %) · built (`p4.aim-blow`) |
| D4-060 | Training past level 15: manual levels rise (Priests higher); X68000 writes (L + 1) mod 15, so level 15 trains to level 1; MSX2 goes on to 30 | DECIDED (user s103): MSX2 default, X68000 wrap as option · built (`p4.level-wrap`) |
| D4-061 | Spells outside combat by place: manual pp. 54–55 (Levitation wilds / dungeons only, Divine Aid there too) vs code (Levitation also in town, Divine Aid dungeons only); pp. 32 / 35 shorter lists | DECIDED (user s103): manual default, code's lists as option · built (`p4.cast-places`) |
| D4-062 | Party info: manual "the gold, experience and new items gained since leaving town"; code prints the stored pool (one tenth of the experience; the town gate shows × 10) and the party's total gold | DECIDED (user s103): manual default (full XP, gold since town), code's display as option · built s104 (`p4.party-info`) |
| D4-070 | Teleportation in a location map: manual "to another place"; D4-052 was decided as "previous square (the code)", but the code's step-back (`_enc_end` → `_cox`) is outdoor-only: in a dungeon the party stays on the fight's square | DECIDED (user s103): previous square stays default; add the code's stay as a `p4.teleport` value · built s104 (`stay`) |
| D4-071 | Spell 64 ("????????", the game's Annihilate Enemy): the manual lists it among the combat spells as "a spell concerning the works of the gods" (effect not described); the code lists it in combat, says its name and only empties the caster's MP | DECIDED: code (s105) — castable, no effect, MP 0; QoL option `p4.annihilate` = `divine` (user s117, off in every preset) adds a remaster effect |
| D4-072 | Outdoor "M Map" overview: manual "the 3 × 3 overall map around the current area"; code shows one of three fixed 3 × 3 blocks (the party's map is often not in the middle, e.g. map 1 in a corner), and in the Netherworld only the party's own map (the rest show filler) | DECIDED (user s109: C) · the code's blocks in every preset; option `p4.overview` = `centred` for the manual's reading |
| D4-073 | Rename takes a name of spaces only (manual silent): `_re_name` refuses only an empty entry (first byte 0); creation's `_mkname` takes even an empty name | NOT A CONFLICT (manual silent; remaster as the code) |

D4-030 … D4-034 come from the towns spec (`reverse-engineering/x68000/phantasie4/logic-specs/towns.md`), which also
read the manual's town pages. Manual checks that found **no conflict** are listed at the end (§ Checked, no conflict). Contradictions inside the
printed material (not involving the code) are in `phantasie4-manual-en.md`, section "Inconsistencies inside the printed
material".

---

## D4-001 — Nikademus as an ancestor

- **Source:**
  - MSX Magazine article B describes a "very strong descendant of Nikademus" made through the Hall of Glory.
  - **Manual (s81): silent on Nikademus as an ancestor.** It names no exception, no "Not possible" result and no
    failure case. What it does say:
    - p. 8: 「このシリーズの3人の主人公「ウッド卿」、「フィルモン」、「ニカデモス」、それにあなたのキャラクタの転送が済んでいたら、
      これら8人を足して計11名のキャラクタに関するプロフィールを見ることが出来ます。」 — "You can see the profiles of the series'
      three protagonists, Lord Wood, Filmon and Nikademus, plus, if you have transferred your characters, those 8: 11
      characters in all." (Hall menu "2 Character information"; the text is about viewing profiles.)
    - p. 11: 「その際、栄光の殿堂に祭られたキャラクタ（すでにあなたの昔のキャラクタが転送されていればそれらのキャラクタも対象と
      なります。）を祖先とするキャラクタを作ることも出来ます。」 — "You can then also make a character whose ancestor is a
      character enshrined in the Hall of Glory (including your old characters, if you have already transferred them)."
    - p. 17: 「「H」で選んだ過去のキャラクタはそのまま第1祖先として登録されます。」 — "A past character chosen with H is
      registered as the First ancestor as is." 「以上の手続き2人の祖先が決まると、コンピュータが遺伝子を計算した結果、新しく生まれる
      キャラクタの種族を決めてくれます。（「複合種族」が生まれてくるかどうかも、コンピュータの計算次第です。）」 — "When the two
      ancestors have been decided, the computer calculates the genes and decides the newborn's race. (Whether a mixed race
      is born also depends on the computer's calculation.)"
  - Reading: the manual neither allows nor forbids a Nikademus descendant. Its general wording ("a character enshrined in
    the Hall") lists no exception, but it also does not promise that every Hall person or pair works. It does not support
    magazine B's "very strong descendant" and does not describe the code's "Not possible".
- **Code (X68000 and MSX2 agree) `[C]` static:**
  - With Nikademus (shipped Hall slot 2, race 33 `  ?`, class Lord, social Black Lord) as the first ancestor, every
    second-ancestor path ends in "不可能です" / "ふかのうです" (Not possible) and creation restarts. MSX2: slot stored at
    `(0x8002)` (`1001:0x1223`); S / D check at `1001:0x1621`, H check first at `1001:0x1822`.
  - As the second ancestor his race forms no key in the 5×5 race-combination table (X68000 `combine`; MSX2 `1001:0x171E`,
    table `0x16EC`), which also gives Not possible.
  - The shipped Hall is byte-identical on both platforms (MSX2 segment `8200` vs X68000 `FAME`). Only one MSX2 code build
    is known.
  - "Sorry, the ancestor you found was …" is the S / D race-roll message, not a Nikademus case.
- **Decision (user s81):** manual (any Hall of Glory character may be an ancestor, no exception): Nikademus can be chosen. For the race combination he counts as **Human** (user). The code's "Not possible" as a rules option.
- **Remaster:** built s83: `p4.nikademus` (`manual`: Nikademus may be either ancestor and combines as Human, `P4Creation.CombiningRace`; `x68000`: every path "Not possible"). The other H rules (same person twice, both of the shipped three) stay as the code.
- **Side effects:** none beyond the rule.
- **Spec:** `reverse-engineering/x68000/phantasie4/logic-specs/ancestry.md` §3.2, §9, §11.

## D4-002 — Lord Wood / Filmon as an ancestor: noble birth

- **Source:**
  - MSX Magazine article C (1991-07): with Lord Wood or Filmon as an ancestor you start of noble birth, so levelling up
    costs no money.
  - **Manual (s81): silent on ancestry and social class.** It never says that an ancestor passes on social class, and it
    never says levelling can cost nothing. What it says about social class and money:
    - p. 22: 「主な階級は次の4つです　Noble 貴族　Craftsman 職人　Laborer 労働者　Peasant 農民　エルフとドワーフには、概して高い
      階級の者が多いといえます。階級の違いは、ゲーム・スタート時の所持金が異なるなど、高い階級の者ほどお金に余裕が出て来ます。
      （レベル上昇の際にも、高い階級の者ほど貰える金額が多い。）」 — "The main classes are these four: Noble, Craftsman,
      Laborer, Peasant. Elves and Dwarves, on the whole, tend to be of high class. Differences of class mean, for example,
      different starting money: the higher the class, the better off. (At each level-up, too, the higher the class, the
      more money the character receives.)"
    - p. 25: 「（最初の預金額は、貴族は768ゴールド、農夫は0です。）」 — "(The opening balance is 768 gold for a Noble and 0 for a
      Peasant.)"
    - p. 11: Charisma 「トレーニングを受ける時や呪文を教わる時の値段に影響があります。」 — "affects the price of training and of
      learning spells"; p. 12: humans train cheaper at the human-run guilds (D4-014). No social-class or ancestry term
      for the training price is mentioned.
  - Reading: the manual **confirms the code's money rules** (starting gold = social × 256 gives exactly Noble 768,
    Peasant 0; higher class → more gold at each level-up = `_calpow`'s social^social × level × 30) and **does not
    support** magazine C's "Lord Wood / Filmon ancestor → noble birth". Its "the higher the class, the more money at
    level-up" is the code path behind "levelling pays for itself" for noble-born characters.
- **Code (X68000 and MSX2 agree) `[C]` static:**
  - Social class is rolled by race (MSX2 `1001:0x1A5A`, X68000 thresholds). Per Hall ancestor only social 4 (Super Hero)
    makes the descendant Royalty and 5 (Dark Lord) an Outcast (MSX2 `1001:0x1C11`–`0x1CF3`). Lord Wood (Royalty) and
    Filmon (Noble) pass nothing on.
  - Training cost has no ancestry or social-class term: level^2.2 × (20 − Cha)² × town factor (80 / 100 / 120 %), cap
    25,000 (X68000; MSX2 segment `100C` the same).
  - Each level-up **pays** the character social^social × level × 30 gold (cap 65,535): 810 × level for a Noble, the cap
    for Royalty. `[~]` This is probably what "levelling costs no money" refers to; it holds for any noble-born character,
    but no code makes a Wood / Filmon descendant noble-born.
  - Starting gold = social × 256 (Noble 768, Peasant 0), as manual p. 25.
- **Code, s81 (towns spec) `[C]`:** the level-up gold goes into record +26, which is the character's **bank account**
    (the bank's "口座/残高"; manual p. 29 "銀行預金", excluding party gold). Training and magic training are paid from
    **party gold**, so a noble-born character's level-up bonus pays for training only after a bank withdrawal, and
    withdrawals stop at 65,000 party gold (`logic-specs/towns.md` §5). The PC-98 manual p. 22 says only that higher
    social classes start with more gold and get more at each level-up (matches the code); the manual pages read for the
    towns spec (pp. 22–32) say nothing about Hall ancestors giving noble birth.
- **Decision (user s81):** NOT A CONFLICT: the manual is silent on ancestors giving noble birth and agrees with the code on money (Noble 768 opening balance, more gold per level-up for higher classes). Magazine C was loose; the code is built.
- **Remaster:** built s83 (code): `P4Creation.HallSocial` passes only Super Hero / Dark Lord on; starting gold social × 256; level-up gold `P4Training.LevelUpGold`.
- **Side effects:** none beyond the rule.
- **Spec:** `ancestry.md` §6, §9, §11; `reverse-engineering/x68000/phantasie4/logic-specs/training.md`.

## D4-010 — Social class: Elves and Dwarves tend to be high class

- **Source:** manual p. 22: 「エルフとドワーフには、概して高い階級の者が多いといえます。」 — "Elves and Dwarves, on the whole,
  tend to be of high class."
- **Code `[C]`** (`_get_social`, MSX2 `1001:0x1A5A`): Human, Dwarf, Elf, Halfling and Gnome all roll `rand(100)` with the
  same thresholds (Peasant ≤ 29, Laborer ≤ 59, Craftsman ≤ 89, Noble 90–100, i.e. 11% Noble); other races roll
  `rand(75)` (never Noble). No Elf or Dwarf bias.
- **Decision (user s81):** manual: Elves and Dwarves roll higher social classes. The size of the bias: see Amount. The code's single roll as a rules option.
- **Amount, s82 evidence search:** none found. PC-98 `_get_social` (P4.EXE `0xAB99`) is identical to X68000 (rand(100) for races 0–4, thresholds 29 / 59 / 89; rand(75) otherwise); MSX2 the same; the manual, magazines, handbook and catalogue give no number. Asked the user s82 (see Amount).
- **Amount (user s82):** Elves and Dwarves roll `rand(100) + 10`, capped at 100, against the same thresholds: Noble 11 % → 21 %, Peasant 30 % → 20 %.
- **Remaster:** built s83: `p4.social-bias` (`P4Creation.RollSocial`; Elves / Dwarves rand(100) + 10, cap 100; `x68000` = the flat roll).
- **Side effects:** an Elf/Dwarf bias would raise their starting gold (social × 256) and level-up gold (D4-002).
- **Spec:** `ancestry.md` §6.

## D4-011 — merged into D4-031

Raised from the manual read (p. 25: 65,000 per account) at the same time as the towns spec's D4-031, which covers it with
more code detail (level-up gold capped at 65,535 is one of D4-031's cases). Use D4-031.

## D4-012 — Base stride (movement) by race

- **Source:** manual pp. 12–16: Human 8, Dwarf 5, Elf as fast as humans, Gnome 4, **Halfling 5**, Gargoyle 12, Spriggan
  9, Pixie 10 (flying), Sprite faster than pixies, Ogre 7, Troll as fast as humans, Kobold 5, **Orc 「足の速さは人間と同じ
  程度です」 ("about as fast as humans")**, Goblin 5, Lizard Man 6. Thief and Monk +1 (pp. 17–18).
- **Code `[C]`** (`_moverate`, race table `0x41472`, class table `0x414f6`): Human 8, Dwarf 5, Elf 8, **Halfling 6**,
  Gnome 4, Pixie 10, Sprite 11, Ogre 7, Gnoll 6, Troll 8, Kobold 5, **Orc 7**, Goblin 5, Lizard Man 6, Minotaur 8, Hill
  Giant 8, Gargoyle 12, Spriggan 9. Class: Thief +1, Monk +1, Ranger 0, Fighter 0, Priest −1, Wizard −1, Shadow
  Warrior +2, Gargoyle 0.
- **Differences:** Halfling 5 vs 6 (clear); Orc "about human" (8) vs 7 (the manual's 程度 makes this loose). All other
  stated values match, as do Thief/Monk +1.
- **Decision (user s81):** manual: Halfling stride 5, Orc 8. The code's 6 / 7 as a rules option.
- **Remaster:** built s83: `p4.strides` (`P4Creation.Stride`; rule-tables.json `stride_manual_overrides` Halfling 5, Orc 8).
- **Side effects:** combat movement (MV) and Levitation range (5 × slowest stride, manual p. 55).
- **Spec:** `ancestry.md` §5.4; `extracted/creation-tables.json` `moverate_*`.

## D4-013 — Gargoyle race: which classes

- **Source:** manual p. 17 (Thief): 「ヒル・ジャイアント以外なら、どんな種族の者でも「盗賊」になれます。」 — "Any race except the Hill
  Giant can become a Thief." p. 18 (Fighter): 「スプリガン以外の種族の者は誰でも良い「戦士」になれます」 — "Any race except the
  Spriggan can make a good Fighter." p. 19: the Gargoyle class is 「ガーゴイルだけがなれる職業です」 ("a class only Gargoyles
  can take"); p. 13: random characters 「「盗賊」と「戦士」だけにしか使えません」 ("can only be Thieves and Fighters").
- **Code `[C]`** (`class_mask_by_race`): Gargoyle race = Gargoyle class only (mask `00000001`). Hill Giant = Fighter
  only, Spriggan = Thief only (as the manual).
- **Decision (user s81):** manual: a Gargoyle may take the Thief or Fighter class (as well as the Gargoyle class). The code's Gargoyle-only as a rules option.
- **Remaster:** built s83: `p4.gargoyle-classes` (`P4Creation.ClassAllowed`; manual mask Thief, Fighter, Gargoyle).
- **Side effects:** none beyond creation menus.
- **Spec:** `ancestry.md` §2 item 3.

## D4-014 — Training price by race

- **Source:** manual p. 12 (Human): 「それぞれの町のギルドは人間が経営している為、トレーニングや魔法の学習は他の種族より安く受けることが
  出来ます。ギルドで優先的な待遇を受ける資格のある種族はエルフ、ドワーフ、ノーム、スプライト、ピクシー等です。だが他の種族はギルドで歓迎
  されず、トレーニングを受けるのにかなり高いお金を払わされます。」 — "Each town's guild is run by humans, so humans get training
  and magic lessons more cheaply than other races. Races entitled to preferential treatment at the guild are Elves,
  Dwarves, Gnomes, Sprites, Pixies etc. Other races are not welcome and must pay a good deal more for training."
  But p. 11 explains monsters' high prices through Charisma: 「10種類のモンスターがトレーニング等で高いお金を払わなくては
  ならないのは、彼等の見た目が悪い、つまりこのカリスマ値が低いせいなのです。」
- **Code `[C]`:** training cost = L^2.2 × (20 − Cha)² × town factor (cap 25,000) and spell price =
  trunc(9000 × T / 100) / Cha / Int × L / (9 − class) — **no race term**. Race acts only through the race's Charisma
  modifier: Human 0, Elf 0, Dwarf / Gnome / Pixie / Sprite / Halfling −1, monsters −4 to −7.
- **Two readings (no side taken):** (a) p. 11's explanation: the race effect *is* the Charisma modifier, so code and
  manual agree except that Elves pay the same as Humans and Halflings count as favoured; (b) p. 12 literally: a separate
  race price factor, humans cheapest — which the code does not have.
- **Decision (user s81):** NOT A CONFLICT: Charisma (with the race modifiers) carries the race effect, as the manual itself explains for monsters (p. 11). The code is built.
- **Remaster:** built s83 (code): training and spell prices use Charisma only (`P4Training.TrainingCost`, `SpellPrice`).
- **Side effects:** training and spell prices.
- **Spec:** `training.md` §3, §5.

## D4-015 — Priests: higher levels and every spell

- **Source:** manual p. 19: 「彼等は他の職業の者より高いレベルに達することが出来、最高のレベルに達すれば、すべての呪文を唱えることが出来る
  ようになります。」 — "They can reach higher levels than other classes, and at the highest level can cast every spell."
- **Code `[C]`:** the training hall's gate is the same for every class (record +09 "Score" ≥ 125 or level ≥ 31 →
  "nothing more I can teach"; `_calpow` stops at level 30). The learnable-spell table (`TABL` 0x6EA, 15 levels × 3)
  grants a Priest **35 of the 65 spells** (Wizard 33); no later level adds the rest `[~]` (only 15 level rows exist).
  Experience overflow near level 24–26 is still open (`training.md` §4).
- **Decision (user s81):** manual: Priests reach a higher level cap than other classes and can cast all 65 spells at the top. The cap and the spell schedule: see Amount. The code's single gate and 35 spells as a rules option.
- **Amount, s82 evidence search:** none found. PC-98 `TABL` is byte-identical to X68000 (same spell schedule, same 15 level rows); no written source gives the Priest level cap or schedule. Asked the user s82 (see Amount).
- **Amount (user s82):** Priests train to level 35 (others stop at 30); levels 31–35 each grant 6 of the 30 spells missing from the Priest's `TABL` schedule, in spell-number order, so a level-35 Priest has all 65.
- **Remaster:** built s83: `p4.priest-levels` (`P4Training.MaxLevel` 35 / 30, `Gate`, `SpellsGranted`: levels 31–35 each add 6 of the 30 missing spells in number order).
- **Side effects:** class balance; Priest spell list.
- **Spec:** `training.md` §1, §4; `extracted/creation-tables.json` `spells_granted_by_class_level`.

## D4-016 — Fighter (and Thief) spell counts

- **Source:** manual p. 56 (hedged: 「だいたい次のように分けられると思います」 "roughly, we think"): 「盗賊は1～2の心理的な呪文、それに
  かなりレベルが高くなってから1～2の攻撃的な呪文を覚えるにとどまります。戦士も同様です。かなりレベルが高くなっても、あまり重要でない呪文を
  3～4覚える程度です。」 — "Thieves learn only 1–2 psychological spells, plus 1–2 offensive spells at quite high level.
  Fighters likewise: even at quite a high level they learn only 3–4 minor spells."
- **Code `[C]`** (`TABL` 0x6EA): **Fighter 7 spells** — *Strength 1 (L3), Monster Eval. (L5), *Strength 2 (L7),
  Accuracy (L9), *Strength 3 (L11), *Strength 4 (L13), Arrow Flame 1 (L15). Thief 4 — Magic Bag (L4), Sleep (L7),
  *Protection 2 (L10), Fire Flash 4 (L13): close to the manual (Sleep psychological, Fire Flash 4 offensive), with two
  extra utility spells.
- **Decision (user s81):** manual: Fighters learn only their first 4 minor spells. The code's 7 as a rules option.
- **Remaster:** built s83: `p4.fighter-spells` (`P4Training.SpellsGranted`: the first 4 of the Fighter schedule — *Strength 1, Monster Eval., *Strength 2, Accuracy).
- **Side effects:** class spell lists.
- **Spec:** `extracted/creation-tables.json` `spells_granted_by_class_level`.

## D4-017 — Box back: old characters adventure together with their descendants

- **Source:**
  - Box back (`docs/manuals/phantasie4/phantasie4-pc98-box-back.jpg`): 「過去のシリーズで、あなたが育て上げた愛着あるキャラクタ
    たちを移し、その子孫たちとともに、冒険を共にすることができる。」 — "You can transfer the beloved characters you raised in
    the earlier series and share the adventure together with their descendants."
  - Manual p. 8: 「「ファンタジーⅢ」で活躍させたキャラクタで、特に愛着のキャラクタを8名まで転送することが出来ます。…ただし、このキャラ
    クタは子孫を作るためのものにすぎません。」 — "You can transfer up to 8 favourite characters from Phantasie III … However,
    these characters serve only to make descendants." Manual p. 1 and p. 7 speak of your characters' *descendants*
    appearing.
- **Code `[C]`:** III transfers write only Hall slots 3–10 (`_mtrans`, `_mcopy`); Hall persons are never party members
  (`ancestry.md` §8, `FORMAT.md` §6.3).
- **Note:** the box blurb may just be loose ad copy ("together with" = through), and the manual and the code agree. The
  user's s72 decision (III → IV: faithful Hall-only by default, a direct-join option off by default →
  `09-trilogy-iv-parentage.md` §3) already covers both readings; listed so the user can confirm.
- **Built (s149):** the option `p4.transfer-join` (`hall` in every preset = manual and code; `guild` = a III character
  can join IV's Guild, `ancestry.md` section 8.3).
- **Decision (user s81):** NOT A CONFLICT: box copy; manual p. 8 and the code agree that imported characters go to the Hall (direct join stays the s72 option).
- **Remaster:** built (T5): the III → IV transfer writes the Hall (`P4Hall`); the s72 direct join is the option
  `p4.transfer-join` (Built, above).
- **Side effects:** none.
- **Spec:** `09-trilogy-iv-parentage.md` §1–§3.

## D4-018 — Long Sword: 14 or 13 points

- **Source:** manual p. 57: 「例えば14ポイントの切れ味を持つロング・スワードはかなり強い戦士でなければ、扱うことは出来ません。」 — "e.g.
  a Long Sword with an edge of 14 points can only be used by a fairly strong fighter."
- **Code `[C]`:** `_idat` Long Sword (0x28) attack value **13**, requirement b2 = 51 (2 × Str + Dex ≥ 51), price 600.
  The item catalogue also says 13.
- **Decision (user s81):** manual: the Long Sword has 14 points. The code / catalogue 13 as a rules option.
- **Remaster:** built s83: `p4.long-sword` (`P4Town.ItemPoints`; items.json keeps the code's 13, rule-tables.json `item_points_manual` 14).
- **Side effects:** none beyond the item.
- **Spec:** `extracted/items.json`; `FORMAT.md` §6.5.

## D4-019 — Thieves' hit points

- **Source:** manual p. 17: 「盗賊のヒット・ポイントとマジック・ポイントは同じレベルの他の職業の者より低くなりますが、他の職業の者より少ない
  経験値でそのレベルに達することが出来ます。」 — "Thieves have fewer hit points and magic points than other classes at the
  same level, but reach each level with less experience."
- **Code `[C]`** (`TABL` 0x000, HP growth by class and level; levels 1 / 5 / 10 / 15): Thief 3 / 16 / 35 / 55,
  **Wizard 3 / 11 / 25 / 40**, Shadow Warrior 2 / 9 / 34 / 62, Priest 4 / 20 / 45 / 65, Fighter 5 / 28 / 60 / 85.
  Wizards (and Shadow Warriors before level 11) have fewer HP than Thieves. The experience half matches (`training.md`
  §2: Thieves need the least). Magic points: not compared (MP comes from the magic-level table `TABL` 0x14A; not checked
  against this claim).
- **Decision (user s81):** manual: Thieves have the fewest HP of all classes. The HP values: see Amount. The code's table as a rules option.
- **Amount, s82 evidence search:** none found. PC-98 `TABL` (HP table) is byte-identical to X68000; no written source gives HP numbers. Asked the user s82 (see Amount).
- **Amount (user s82):** at each level, Thief HP = the lowest value of any other class at that level − 1, floor 1 (L5 8, L10 24, L15 39).
- **Remaster:** built s83: `p4.thief-hp` (`P4Training.HpGrowthRow`: L1 1, L5 8, L10 24, L15 39).
- **Side effects:** none.
- **Spec:** `extracted/creation-tables.json` `hp_growth_by_class_level`.

## D4-020 — Shop resale price

- **Source:** manual p. 23: 「買い取られた物は店に置かれ、少し高い値段で売られます。」 — "Items bought [from the party] are put on
  sale in the shop at a slightly higher price."
- **Code `[C]`** (`_sell`, `_buyitem`; `logic-specs/towns.md` §6): the shop pays trunc(price / 4) + 1 and the item
  goes into the saved stock of the town where it was sold; the shop sells at trunc(price × T / 100) with the town factor
  T = 80 / 100 / 120 % — so a sold item comes back at about 3.2–4.8 times what the shop paid.
- **Decision (user s81):** manual: an item you sold is resold at a slight markup over what the shop paid. The markup: see Amount. The code's full-price resale (80–120 % of base) as a rules option.
- **Amount, s82 evidence search:** none found. PC-98 `_sell` (P4.EXE `0xD19B`) is identical (price / 4 + 1, purse cap 65 535, stock +1 in the current town) and no platform has a separate resale price; the manual gives no amount. Asked the user s82 (see Amount).
- **Amount (user s82):** a sold item is resold at trunc(paid × 1.25), no town factor (a 100-gold item: shop pays 26, resells at 32).
- **Remaster:** built s83: `p4.resale` (`P4Town.ResalePrice`: trunc(paid × 1.25); the caller keeps the paid price with the sold stock).
- **Side effects:** economy.
- **Spec:** `logic-specs/towns.md` §6.1–§6.2; `FORMAT.md` §6.5.

## D4-030 — Entering a town: party gold into the accounts, or experience?

- **Source:** PC-98 manual p. 25 (Bank): "When you enter a town after wandering the wilds, the gold the party carries is
  divided in the ratio you choose and paid into each member's account. So to buy things or train in town you must first
  withdraw gold at the bank."
- **Code (X68000 and MSX2 agree) `[C]` static:**
  - On town entry `_townent` (`0x2CF2`) calls `_gatebank` (`0x4EF0`), window "[original text]" (Share out experience). It
    divides the **party experience pool** (`_par`+18, filled at combat end with XP ÷ 10) by shares (1–3; 0 allowed only for a member not Okay) into each member's
    **experience** (+04, cap 9,999,999). MSX2 seg `1000`: the same window ("けいけんちのぶんぱい", `0x0ABB`).
  - Party gold (`_par`+1C) is not written by `_townent`, `_welcome` or `_gatebank`; gold reaches the accounts only
    through the bank's own **D: Deposit** (`_deposit` `0x4992`, called only from `_bank`), which uses the same 1–3
    share screen. So party gold stays spendable on entry and no withdrawal is needed to shop or train.
  - PC-98 (the manual's platform): code not read; its program holds the same strings (Disk 1, e.g. [original text] at offset 352,122, 預金入金 352,069), so no sign of a different build.
- **Decision (user s81):** manual: on town entry the party gold is paid into the members' accounts by the chosen 1–3 shares, and the party experience pool is still shared out (both screens run; the manual names only the gold, the code only the XP). The code's XP-only entry (gold stays in the purse) as a rules option.
- **Remaster:** built s83: `p4.town-entry` (`P4Town.DepositsOnEntry`, `Deposit`, `ShareExperience`). s97: runs in the
  town loop (`P4TownLoop`): gold shares first, then the experience shares; an Okay member's share is 1–3, only a member
  who is not Okay may take 0 `[~]` (the "0" button's enable field).
- **Side effects:** both screens run on entry: the experience share-out is still needed
  (no other code moves the pool into characters).
- **Spec:** `reverse-engineering/x68000/phantasie4/logic-specs/towns.md` §2.1, §5.

## D4-031 — Gold caps

- **Source:** PC-98 manual p. 25: each character's account holds at most 65,000 gold; the party's purse holds at most
  65,000 gold.
- **Code (X68000; MSX2 has the same 65,000 constants in the bank) `[C]` static:**
  - Bank: deposit caps each account at 65,000 (`_deposit`), withdraw caps party gold at 65,000 (`_withdraw`). Matches.
  - Selling an item caps party gold at **65,535** (`_sell` `0xA236`); the bank then disables Withdraw only at exactly
    65,535.
  - Gold from a fight is added with a cap of **999,999,999** (`_esub01` `0x248AC`), so the purse can far exceed 65,000.
  - Level-up gold goes into the account with a cap of **65,535** (`_calpow`, `training.md` §4).
  - One sale with the purse above 65,535 cuts it to 65,535. With the purse above 65,000, Withdraw's limit
    (65,000 − purse, unsigned) wraps: any amount is accepted, the account wraps below zero (u16) and the purse is reset
    to 65,000 (`_withdraw` `0x5456`, `0x554E`, `0x559A`).
  - PC-98 (the manual's platform): code not read; its program holds the same strings (Disk 1, e.g. [original text] at offset 352,122, 預金入金 352,069), so no sign of a different build.
- **Decision (user s81):** manual: party gold and every account are capped at 65,000 on every path (bank, selling, fight loot, level-up gold); the withdraw wrap bug is not reproduced. Option: uncapped. (The user named no option for the code's mixed caps.)
- **Remaster:** built s83: `p4.gold-cap` (`manual` 65,000 on every path, the withdraw wrap not reproduced; `uncapped` no cap — also the Original (X68000) preset, as the code's mixed caps have no option of their own; `P4Town.Cap`, `Withdraw`, `P4Training.LevelUpGold`).
- **Side effects:** none beyond the rule.
- **Spec:** `towns.md` §5, §6.2.

## D4-032 — Town inn: does a stay take time?

- **Source:** PC-98 manual p. 27 (Inn): the town inn is free "and you stay at least two weeks". The outdoor inns
  cost 250 gold plus tax per stay.
- **Code (X68000 and MSX2 agree) `[C]` static:**
  - Town inn `_inn` (`0x2188`) refills HP/MP, heals up to 2 wound points, recharges rings; it takes no gold (matches)
    and calls no `_dayout` and changes no age, so **no time passes**. MSX2 seg `1000` inn (`0x1FA3`–`0x21F8`) has no
    day-counter write either (the only one in the segment is the reset at `0x0396`).
  - Outdoor inn `_oinn` (`0x14F98`): 256 gold (= "250 plus tax", matches) and `_dayout(1000)`.
  - The only `_dayout` callers are `_oinn` and the outdoor move (`_omain`); dungeons pass no time either.
  - PC-98 (the manual's platform): code not read; its program holds the same strings (Disk 1, e.g. [original text] at offset 352,122, 預金入金 352,069), so no sign of a different build.
- **Decision (user s81):** manual: a town inn stay takes two weeks and ages the party (as I's D-004 and II's D2-032). The code's no-time stay as a rules option.
- **Remaster:** built s83: `p4.inn-time` (`P4Town.InnStayDays`: 14 days into the party's day counter). s97: the town
  loop's Inn adds them (`P4TownLoop`); they age the party at the next town entry.
- **Side effects:** time feeds aging (`towns.md` §2.2), so a two-week stay ages characters (very little: one age unit per 0x700 days).
- **Spec:** `towns.md` §4.

## D4-033 — Lifespans by race

- **Source:** PC-98 manual p. 30 (Age): near the end of its race's natural lifespan a character's abilities decline,
  then it dies. Lifespans: Elf about 2,000 years, Dwarf and Gnome 200, Human 80, the others 40–50.
- **Code (X68000; MSX2 the same threshold rule) `[C]` static:** at each town entry a character with
  trunc(age / 256) ≥ 0.7 F is aged once (abilities × 0.8 on MSX2, see `towns.md` §2.2 for X68000) and dies at
  trunc(age / 256) ≥ F, i.e. at about 9.85 × F years. F (record +20, `ancestry.md` §7 item 10) = Luck (9–14) × 19 for
  Elf (cap 255), × 2.5 for Dwarf, **Halfling** and Gnome, × 1 for Human, **Pixie and Sprite**, ÷ 2.5 for every race
  from 7 up (including the hybrids, e.g. Half Elf). Death ages: Elf **never** (A = age / 256 is a signed shift on both platforms and turns negative at age 32,768 ≈ 1,260
  years, before any Elf F of 171–255 is reached; Elves with F > 181 never even decline); Dwarf / Gnome ≈ 215–345 (manual 200);
  Human ≈ 89–138 (manual 80); Halfling ≈ 215–345 (manual "others 40–50"); Pixie / Sprite ≈ 89–138 (manual 40–50); races
  7+ and hybrids ≈ 30–49 (manual 40–50; Half Elf and the other part-Elf/Dwarf hybrids fall here too).
  Also an original bug: the race tested for F is the race of **guild slot 0**, not of the new character
  (`ancestry.md` §7), so in play every character's lifespan follows slot 0's race.
  - PC-98 (the manual's platform): code not read; its program holds the same strings (Disk 1, e.g. [original text] at offset 352,122, 預金入金 352,069), so no sign of a different build.
- **Decision (user s81):** manual: lifespans by race as the manual gives them (Elf about 2,000, Dwarf and Gnome 200, Human 80, the others 40–50 years; per-race exact values and spread are a manual-silent detail set when built, keeping the code's 70 % "grown older" point). The X68000 lifespan rule as a rules option. QoL option (user): normalise aging against all four games, PLAN P3: built as
  `shared-aging` (`docs/plan/13-aging-normalised.md`).
- **Remaster:** built s83: `p4.lifespans` (`P4Aging.ManualLifespanYears`, `Check`). Values set at build (manual-silent detail, user s81): death age = base × (0.9 + 0.04 × (Luck − 9)), Luck clamped 9–14 (±10 %), bases Elf 2,000, Dwarf and Gnome 200, Human 80, every other race 45 (Halflings, the monster races and the mixed races: the manual's "others 40–50"); grown older at 70 %. `x68000`: the aging-factor rule (the caller may pass guild slot 0's race for the code's creation bug). s97: runs at every
  town entry (`P4TownLoop`); dead members age but are not checked `[~]`; the messages wait for a key `[~]`.
- **Confirmed (user s84):** these build values stand.
- **QoL `shared-aging`** (every game, `docs/plan/13-aging-normalised.md`; off in both Original presets, **on in Remaster**):
  one remaster aging model for I–IV, I's D-003 model. It overrides `p4.lifespans` and `p4.aging` while on:
  - Elf 2,700–3,300 years: the I / II / III manuals' "about 3000", over IV's 2,000;
  - Dwarf / Gnome 180–220, Human 72–88, every other race 40–50;
  - picked by Luck 9–14 and stored at creation (`P4Member.Lifespan`), so a decline does not shorten it;
  - grown older past 70 % (× 0.8 once), dead past 100 %;
  - the age cap of 65,000 lifted;
  - a III → IV Guild join keeps III's age, lifespan and "has aged".
  
  With it off, the values above apply unchanged.
- **Side effects:** the starting age (`ancestry.md` §7 item 11) comes from F. With `p4.lifespans` = `x68000` the
  creation screens (s106) keep the code's guild-slot-0 race for F; with the manual's lifespans F uses the character's own
  race (the slot-0 test is an original bug, and F only sets the starting age there).
- **Spec:** `towns.md` §2.2; `ancestry.md` §7.

## D4-034 — What "growing older" does to the abilities

- **Source:** PC-98 manual p. 30 (Age): as a character nears its race's lifespan "its abilities decline, and in the end
  it dies".
- **Code `[C]` static — the two code platforms differ:**
  - MSX2 seg `1000` (`0x0472`, `0x0534`–`0x06C2`): once, at 70 % of the lifespan (`towns.md` §2.2), Str, Int, Dex, Con,
    Cha, Luck and record +35 are each set to trunc(value × 80 / 100). Matches the manual.
  - X68000 (`_townent` `0x2EDC`, `0x301A`–`0x31BC`): the same seven bytes are multiplied by the integer conversion of the
    float 0.8. The conversion is FLOAT2's `__CFTOL` (`SYS/FLOAT2.X` on disk 1: `$FEE9` → `0x6AA` → `0x2B6E`), which
    truncates and returns 0 for |x| < 1, so the multiplier is 0: **all abilities and Luck become 0** at the first
    "has grown older" message. Original bug (the decompile shows `byte × (char)0.8`).
  - Both platforms: a character already marked "grown older" and still alive is scaled again on later town entries with
    a factor left over from an earlier party member (or uninitialised) `[~]`.
  - PC-98 (the manual's platform): code not read; same message strings on Disk 1 (は歳をとった at offset 356,736).
- **Decision (user s81):** "abilities decline" read as MSX2's rule: at the 70 % point every ability and Luck are multiplied by 0.8, once. The X68000's zeroing (0.8 truncated to 0) as a rules option.
- **Remaster:** built s83: `p4.aging` (`P4Aging.ScaleOnAging`: `msx2` × 0.8, `x68000` 0). The code's re-scaling of an already-aged member with a left-over factor is not reproduced `[~]`. s97: runs at every town entry (`P4TownLoop`).
- **Side effects:** none beyond the rule.
- **Spec:** `towns.md` §2.2.


## D4-035 — Healing potions: how many wounds they heal

- **Source:**
  - Manual p. 45, Table 2 (effects of healing potions and spells): potions **1–3** = Healing 1, one light wound;
    **4–6** = Healing 2, one serious wound or two light; **7–9** = Healing 3, one lost part or one serious + one light;
    **10** = Healing 4, one lost part or more. 「呪文・薬の効果や負傷の度合は全て数式化されています。」 and the formulas 「失う（もぎ取られる）＝軽傷×3 もしくは
    重傷＋1」, 「重傷＝軽傷×2」 — "all effects of spells and potions and all degrees of wounds are formulas: lost = light × 3 or
    serious + 1; serious = light × 2."
  - Manual p. 58, Table 3: HP restored 1, 4, 9, 16, 25, **35**, 49, 64, 81, 100; wounds as Table 2 (7–9 printed as "one
    each of lost, serious, light", an inconsistency inside the printed material). The item catalogue (secondary)
    repeats Table 3, including 35.
- **Code (X68000) `[C]` static** (`_use_hp` `0xEDC4`, `_heal` `0x1FFA`; `logic-specs/items.md` §3):
  - HP += n² for Healing n (cap HP max): **Healing 6 gives 36**, the others match the table.
  - Wounds: `_heal(member, trunc((n + 1) / 3))`, a budget of wound points (light 1, serious 2, lost 3; highest level
    first). So potion **1 heals no wound**, 2–4 heal 1 point (one light), 5–7 heal 2 points (one serious or two
    light), 8–10 heal 3 points (one lost, or serious + light). Potions 1, 4, 7 and 10 are one step below the manual;
    potion 10 never heals "more than one lost part".
  - The inn calls `_heal(·, 2)`, which matches the manual's "an inn stay heals like Healing 2" (p. 46).
  - 10 % of potions "have gone bad" (`_spoile`: no effect, 1–8 damage); the manual does not mention it (no conflict,
    manual silent).
  - MSX2 / PC-98: not read.
- **Decision (user s82):** manual by default, the X68000 code's behaviour as a rules option (blanket call for D4-035…054). A later PC-98 read may turn this into a platform difference (not a conflict).
- **Remaster:** built s83: `p4.potion-wounds` (`P4Body.HealingPotion`: manual HP 1, 4, 9, 16, 25, 35, 49, 64, 81, 100 and heal points 1, 1, 1, 2, 2, 2, 3, 3, 3, 4).
- **Side effects:** none beyond potions. The Healing spells' own budgets are in `logic-specs/combat.md` (spells).
- **Spec:** `reverse-engineering/x68000/phantasie4/logic-specs/items.md` §3.


## D4-036 — Spot Item and treasure after a fight

- **Source:** manual p. 21: 「C Spot（アイテムの発見）戦闘の後、落ちているアイテムを見つけ出す能力です。…またこの技能が高いと、見つけた
  アイテムの性質まで見抜けるので、役に立つ物かどうかを判別することも可能です。」 — "Spot Item: the ability to find items lying around
  after combat … a high value also reveals the nature of found items, so you can tell whether they are useful." Manual
  p. 44: treasure in dungeons is found after a fight by members with a high skill at finding treasure; 「アウトドアで敵を全滅させた
  時、…相手が財宝を持っていれば、自動的に手に入ります。」 — "outdoors, when you wipe out the enemy, any treasure they carry is yours
  automatically."
- **Code (X68000) `[C]` static:** no instruction in P4.X reads record +40 (Spot Item) — neither absolute `_pmb` / `_gmb`
  addresses nor record-pointer displacements. Post-combat treasure (`_esub02` `0x24774`) is a flat chance: 45 % in
  dungeons, `_emlevel` + 18 % outdoors (not automatic); the monsters' own treasure ids (`DATM` +3A/3C/3E) are loaded only
  outdoors. Item appraisal (`_ident`) does not use the skill either. MSX2 / PC-98 not read.
- **Also:** hidden dungeon items (event `CHEST 3`, `_cspi` `0x1B1FC`) are found with record +42 (Spot Trap), not +40 (the
  manual names no skill for them, so this alone is no conflict; `events.md`).
- **Also (combat spec §12 item 7):** surrender in a dungeon gets the same roll (manual: automatic); the extra random item
  after a successful roll comes outdoors always, in a dungeon with 64 %, and depends on an uninitialised local (bug `[~]`).
- **Decision (user s82):** manual by default, the X68000 code's behaviour as a rules option (blanket call for D4-035…054). A later PC-98 read may turn this into a platform difference (not a conflict).
- **Remaster:** built s83: `p4.spot-item` (`P4Combat.TreasureFound`: automatic outdoors when wiped out and in a dungeon on surrender; otherwise the party-wide Spot Item roll, rand(100) ≤ skill for each living member, one success finds — the game's own skill roll, `skills.md` §4; confirmed user s84).
- **Amount (user s84):** a finder with Spot Item ≥ 50 also identifies the found items (`P4Combat.SpotIdentifies`; built s85).
  What "identifies" shows (s110): the code has no identified state (names always show), so the remaster prints the
  game's own appraisal line (`_ident_mi`, the Identify text) under each found item while the best living member's Spot
  Item is ≥ 50 `[~]` (`P4Battle` `Treasure` / `NextTreasure`).
- **Side effects:** treasure rates; the Spot Item skill is otherwise dead.
- **Spec:** `logic-specs/skills.md` §4–§6.

## D4-038 — Ships wrecked in combat or worn out

- **Source:** manual p. 28: 「ただし、この船は戦闘で壊れたり、使い古されたりします。」 — "But this ship can be wrecked in combat or
  wear out."
- **Code (X68000) `[C]` static:** no fight damages the ship (a fight at sea leaves a moored ship on the square,
  `_oencgo`), and there is no use counter. A ship is lost only by running aground during a forced drift (`_save_ship`
  `0x14BF4`), by the shining-sea force (`_obarrier` `0x13FDE`), or by buying another (`_shipyard`).
- **Platforms:** X68000 code only; MSX2 / PC-98 (the manual's platform) code for this rule not read (PC-98 `TABL`,
  `_get_social` and `_sell` were compared s82 and are identical).
- **Decision (user s82):** manual by default, the X68000 code's behaviour as a rules option (blanket call for D4-035…054). A later PC-98 read may turn this into a platform difference (not a conflict).
- **Remaster:** built s83: `p4.ship-wear` (`P4Outdoor.ShipWreckedInFight`, `ShipWornOut`). 
- **Amounts (user s84):** a 5 % wreck chance after a sea fight the party loses or flees (a won fight never wrecks);
  a ship lasts 4000 sea steps, configurable (an option for the step count). Built s85: `P4Outdoor.ShipWreckedInFight`
  (`partyWon`), `ShipWornOut` reads `p4.ship-life` (4000 / 2000 / 8000, 4000 in every preset).
- **Wired s97 (`P4OutdoorLoop`), readings `[~]`:** the wreck roll is made only when the party had its own ship (a party
  that was swimming gets the code's moored ship after a sea fight); a wreck loses the ship ("The ship was wrecked in the
  fight!") and also skips the code's mooring after the fight. Sea steps are counted while aboard; **what happens when a
  ship reaches `p4.ship-life` is not decided `[?]`** (nothing happens yet).
- **Side effects:** none beyond the rule.
- **Spec:** `logic-specs/outdoor.md` §4.4.

## D4-039 — Levitation outside combat: how long it lasts

- **Source:** manual p. 55: 「パーティ全員が空中を飛べるようになります。足の速さは変化しませんが、パーティ全体の歩幅の5倍の距離だけ
  飛んでいられます。注：パーティ全体の歩幅とは一番遅いメンバーの歩幅です。」 — "The whole party can fly. Speed does not change,
  but they stay aloft for 5 × the party's stride in distance (the slowest member's stride)."
- **Code (X68000) `[C]` static:** casting Levitation outside combat sets `_par`+15 (`_pmagic3` `0x2D3EC`); only the end
  of a fight (`_encount`) clears it. No distance or step count. Its only effect is in dungeons (no water damage, `_dmain`
  / `_dswim`); outdoors the flag is never read, so it does not stop swimming damage there (`_owater`).
- **Platforms:** X68000 code only; MSX2 / PC-98 (the manual's platform) code for this rule not read (PC-98 `TABL`,
  `_get_social` and `_sell` were compared s82 and are identical).
- **Decision (user s82):** manual by default, the X68000 code's behaviour as a rules option (blanket call for D4-035…054). A later PC-98 read may turn this into a platform difference (not a conflict).
- **Remaster:** built s83: `p4.levitation` (`P4Outdoor.LevitationSteps` = 5 × the slowest stride; `LevitationWorksOutdoors`).
  Wired s97 (`P4OutdoorLoop`): a levitating party does not swim outdoors and the steps run down per outdoor move. Reading
  `[~]`: every fight still ends Levitation under the manual rule too (the code's `_encount` does; the manual gives a
  distance, not an end on fighting); the outdoor and dungeon loops both clear it when a fight ends.
- **Side effects:** water travel.
- **Spec:** `logic-specs/outdoor.md` §6; `skills.md` §4.4.

## D4-040 — Navigation below 50 %

- **Source:** manual p. 21: 「但しキャラクタの技能が50%以上になるまでは、使わないで下さい。」 — "But do not use it until the
  character's skill is 50 % or more." (May be advice rather than a rule.)
- **Code (X68000) `[C]` static:** Navigation cannot be "used": the party member with the highest Navigation steers the
  ship automatically (`_oship_mark` `0x14478`, `_owater`); bad navigation means drift and storms. No 50 % threshold.
- **Platforms:** X68000 code only; MSX2 / PC-98 (the manual's platform) code for this rule not read (PC-98 `TABL`,
  `_get_social` and `_sell` were compared s82 and are identical).
- **Decision (user s82):** manual by default, the X68000 code's behaviour as a rules option (blanket call for D4-035…054). A later PC-98 read may turn this into a platform difference (not a conflict).
- **Remaster:** built s83: `p4.navigation` (`P4Outdoor.Navigator`): built as a hard rule (only a member with Navigation ≥ 50 steers).
- **Reading (user s84):** advice with a risk, not a hard rule: the best navigator always steers, and below 50 the ship
  drifts off course on a (50 − skill) % chance per step. Built s85 (replaces the hard rule): `P4Outdoor.Navigator` is the
  best navigator under both rules; `Drifts` keeps the code's own roll (rand(100) > Navigation) and, under the manual below
  50, adds a second roll rand(100) ≤ 50 − Navigation. **User s86:** the extra risk is on top of the code's drift
  (Navigation 30: 70 % → 76 % a step), not a replacement for it.
- **Side effects:** none.
- **Spec:** `logic-specs/outdoor.md` §4.2, §8.

## D4-041 — Dungeon Examine

- **Source:** manual p. 38: 「とくに部屋の中などで、パーティの周囲を全員で調べます。その結果効率良くワナを見つけたり、財宝を見つけることが
  出来るようになります。」 — "The whole party searches its surroundings, especially in rooms, and so finds traps and treasure
  more efficiently."
- **Code (X68000) `[C]` static:** the E button (`_dmain` → `_seainit` `0x180CA` / `_seasel` `0x18110`) starts an
  automatic walk of up to 63 steps, each to the least-visited neighbouring square, with random encounters at a quarter of
  the rate (out of 400 instead of 100). No spot, trap or chest roll depends on it. (Its visit-count clearing loop is
  bugged: it zeroes one byte 900 times.)
- **Platforms:** X68000 code only; MSX2 / PC-98 (the manual's platform) code for this rule not read (PC-98 `TABL`,
  `_get_social` and `_sell` were compared s82 and are identical).
- **Decision (user s82):** manual by default, the X68000 code's behaviour as a rules option (blanket call for D4-035…054). A later PC-98 read may turn this into a platform difference (not a conflict).
- **Remaster:** built s83: `p4.examine` (`P4Outdoor.ExamineWalks`, `ExamineSearchBonus`). 
- **Amount (user s84):** Examine gives +20 to the trap-finding and treasure-finding rolls. Built s85.
- **Side effects:** none beyond the rule.
- **Spec:** `logic-specs/events.md` §6.4.

## D4-037 — Throwing: a skill the code never reads

- **Source:** manual p. 21: 「K Throwing（発射）…「弓術」の技能とまったく同じ技能」 — Throwing is listed as its own skill, "practically
  the same skill as Fire Bow"; p. 19: Wizards begin missile training at level 7 (the Throwing table: 1 at levels 1–6, 50
  from 7).
- **Code (X68000) `[C]` static:** no instruction reads record +50 (Throwing). Thrown weapons use the Fire Bow value
  (+48 → +71) for the hit chance and the bow's damage path (`_chatkp` `0x27E18`, `_chatkd` `0x27920`); the earlier note
  that `_pmagic1` reads +50 was wrong (it reads +48). So the Throwing value that training raises has no effect.
- **Two readings (no side taken):** (a) "treated exactly like Fire Bow" = same rules, separate skill value → the code
  should use +50 for thrown weapons; (b) the code's single Fire Bow value is what the manual means → no conflict
  (combat spec's reading).
- **Platforms:** X68000 code only; MSX2 / PC-98 (the manual's platform) code for this rule not read (PC-98 `TABL`,
  `_get_social` and `_sell` were compared s82 and are identical).
- **Decision (user s82):** manual by default, the X68000 code's behaviour as a rules option (blanket call for D4-035…054). A later PC-98 read may turn this into a platform difference (not a conflict).
- **Remaster:** built s83: `p4.throwing` (`P4Combat.MissileSkill`: throws use Throwing +50 under the manual).
- **Side effects:** none beyond the rule.
- **Spec:** `logic-specs/skills.md` §4, §6; `combat.md` §3.1, §12.

## D4-042 — Attack and Slash: number of strikes

- **Source:** manual p. 43: Attack 「敵を2度攻撃します」 — "attacks the enemy twice"; Slash 「敵に3～4度素早い攻撃をしかけます」 —
  "makes 3–4 quick attacks" (for 1–2 less damage than Attack).
- **Code (X68000) `[C]` static:** one hit roll and one damage roll per action for every weapon action (`_chrexe`
  `0x28322`, `_chatkp` `0x27E18`, `_chatkd` `0x27920`; the animation `_f2f` repeats nothing). Slash's n = 3 or 4
  (trunc(Hm / 75) + 1, offered from Hm 150) divides the hit chance and is subtracted from the damage instead. The damage
  offsets match (Thrust − Attack = +2; Slash 2–3 below Attack).
- **Platforms:** X68000 code only; MSX2 / PC-98 (the manual's platform) code for this rule not read (PC-98 `TABL`,
  `_get_social` and `_sell` were compared s82 and are identical).
- **Decision (user s82):** manual by default, the X68000 code's behaviour as a rules option (blanket call for D4-035…054). A later PC-98 read may turn this into a platform difference (not a conflict).
- **Remaster:** built s83: `p4.strikes` (`P4Combat.Strikes`: Attack 2, Slash n = 3–4; each strike is the code's own hit and damage roll).
- **Reading (user s84):** the developers meant lower accuracy for more damage: every strike that lands does full
  damage, with a to-hit penalty per strike, Attack's smaller than Slash's. Numbers left to Claude: Attack −10 and Slash
  −20 to hit on every strike. Built s85: `MeleeHitChance` Hm − 10 (Attack) / Hm − 20 (Slash, not divided by n);
  `MeleeDamage` drops the Attack −1 offset; Thrust keeps +1.
- **Slash damage (user s86: obey the manual):** Slash does 1–2 less damage than Attack (p. 43): offset −(n − 2), i.e.
  −1 with 3 strikes, −2 with 4. Built s86.
- **Side effects:** none beyond the rule.
- **Spec:** `logic-specs/combat.md` §2, §3, §12 item 1.

## D4-043 — Losing the head or torso

- **Source:** manual p. 46: 「頭部や胴体を失ってしまえば、ヒット・ポイントには関係なしに死んでしまいます」 — "if the head or torso
  is lost, the character dies regardless of hit points."
- **Code (X68000) `[C]` static:** a party member with head or torso at level 3 becomes "Out" (`_pc` 4) but stays alive
  (condition 1, HP kept; `_setpc` `0x1D3C`, `_pdamage` `0x1E940`) and can be healed back (`_heal` budget 3). Monsters do
  die.
- **Platforms:** X68000 code only; MSX2 / PC-98 (the manual's platform) code for this rule not read (PC-98 `TABL`,
  `_get_social` and `_sell` were compared s82 and are identical).
- **Decision (user s82):** manual by default, the X68000 code's behaviour as a rules option (blanket call for D4-035…054). A later PC-98 read may turn this into a platform difference (not a conflict).
- **Remaster:** built s83: `p4.head-torso` (`P4Body.WoundsKill`).
- **Side effects:** none beyond the rule.
- **Spec:** `logic-specs/combat.md` §5, §6, §12 item 2.

## D4-044 — Spell MP costs

- **Source:** manual pp. 50–53 and the spell list (`phantasie4-pc98-spell-list-jp.pdf`): Slow 1 / 2 cost 1 / 2 (manual
  p. 53; spell list 2 / 3); Arrow Flame 2 costs 3 and Party Arrow Flame 1 costs 2 (manual p. 50 and the list); Dissolve
  1 (manual p. 51; list 3).
- **Code (X68000) `[C]` static** (MP table `0x44B7C`, `_getmp`): Slow 1 / 2 = **2 / 3** (as the list), Arrow Flame 2 =
  **2**, Party Arrow Flame 1 = **3** (swapped against both), Dissolve = **3** (as the list).
- **Platforms:** X68000 code only; MSX2 / PC-98 (the manual's platform) code for this rule not read (PC-98 `TABL`,
  `_get_social` and `_sell` were compared s82 and are identical).
- **Decision (user s82):** manual by default, the X68000 code's behaviour as a rules option (blanket call for D4-035…054). A later PC-98 read may turn this into a platform difference (not a conflict).
- **Remaster:** built s83: `p4.mp-costs` (`P4Combat.MpCost`; spells.json `magic_point_cost` = manual, `code_magic_point_cost` = the table).
- **Side effects:** none beyond the rule.
- **Spec:** `logic-specs/combat.md` §9, §12 item 3.

## D4-045 — Party Arrow Flame: hit chance

- **Source:** manual p. 50: 「パーティ全員の弓の命中率を高め、相手に与えるダメージの量を増やします」 — "raises the whole party's
  bow hit chance and the damage done" (50 % / 100 %).
- **Code (X68000) `[C]` static:** damage +15 / +25 only, no hit bonus (`_pmagic1` `0x2BCC4`); the single-target Arrow
  Flame does add 50 / 100 to the hit chance.
- **Platforms:** X68000 code only; MSX2 / PC-98 (the manual's platform) code for this rule not read (PC-98 `TABL`,
  `_get_social` and `_sell` were compared s82 and are identical).
- **Decision (user s82):** manual by default, the X68000 code's behaviour as a rules option (blanket call for D4-035…054). A later PC-98 read may turn this into a platform difference (not a conflict).
- **Remaster:** built s83: `p4.party-arrow-flame` (`P4Combat.ArrowFlame`: +50 / +100 hit for the party forms too).
- **Side effects:** none beyond the rule.
- **Spec:** `logic-specs/combat.md` §9, §12 item 4.

## D4-046 — Auto-combat spell sets

- **Source:** manual p. 32: auto combat / auto round use only attack spells 5–8, 11–12, 14, 20, 38, 43, 48, 59–62 and
  defence spells 1–4, 9–10, 13, 16–19, 40.
- **Code (X68000) `[C]` static** (`_try_cast` `0x1EFE2`, tables `0x44146`, `0x44162`): also casts Confusion 2–4 (21–23),
  Party Arrow Flame 1 / 2 (26, 27), Party Accuracy (29), Mind Blast 1–4 (34–37) and Awaken (46); never casts Sleep (38)
  or Dissolve (48).
- **Platforms:** X68000 code only; MSX2 / PC-98 (the manual's platform) code for this rule not read (PC-98 `TABL`,
  `_get_social` and `_sell` were compared s82 and are identical).
- **Decision (user s82):** manual by default, the X68000 code's behaviour as a rules option (blanket call for D4-035…054). A later PC-98 read may turn this into a platform difference (not a conflict).
- **Remaster:** built s83: `p4.auto-spells` (`P4Combat.AutoMayCast`, `AutoAttackWalk`: the code's walk kept for the manual's spells, then the manual's others (11, 14, 38, 43, 48) in number order; confirmed user s84).
- **Side effects:** none beyond the rule.
- **Spec:** `logic-specs/combat.md` §9, §12 item 5.

## D4-047 — Flameball

- **Source:** manual p. 52: 「これを唱える者の周りにいる者は、敵・味方を問わず火の玉の被害を受けます。強力な鎧を着ている者には通じません」 —
  "everyone around the caster, friend or foe, is hit … it does not affect those wearing strong armour"; radius 1 / 2 /
  2.5 / 3 squares.
- **Code (X68000) `[C]` static** (`_pmagic2` `0x2A5A0`, count table `0x4494F`): centred on the chosen target square;
  armour is subtracted but every creature hit takes at least 1; ranks 3 and 4 cover the same 21 squares. Squares off the
  field are dropped, with no row wrap (s110: `_fmark` box of radius r around the target, `combat.md` §9.1).
- **Platforms:** X68000 code only; MSX2 / PC-98 (the manual's platform) code for this rule not read (PC-98 `TABL`,
  `_get_social` and `_sell` were compared s82 and are identical).
- **Decision (user s82):** manual by default, the X68000 code's behaviour as a rules option (blanket call for D4-035…054). A later PC-98 read may turn this into a platform difference (not a conflict).
- **Remaster:** built s83: `p4.flameball` (`P4Combat.FlameballSquares`: Euclidean radius 1 / 2 / 2.5 / 3 around the caster = 4 / 12 / 20 / 28 squares; `FlameballDamage`). 
- **Amount (user s84):** "strong armour" = body armour of 10 points or more (Plate Mail, Elvin Chain Mail, Plate Mail +1,
  Mythral Mail). Built s85 (`FlameballDamage` takes the worn body armour's points; monsters 0).
- **Side effects:** none beyond the rule.
- **Spec:** `logic-specs/combat.md` §9, §12 item 6.

## D4-048 — Night watch and Listen

- **Source:** manual p. 44: 「見張り役の聴覚（Listen）が良ければ、遭遇する前にパーティ全員を起こすことも出来ます」 — "if the
  watch's Listen is good, he can wake the whole party before the encounter."
- **Code (X68000) `[C]` static** (`_oencchk` `0x1668C`): a successful Listen roll only removes the monsters' free first
  round; the other members stay asleep.
- **Platforms:** X68000 code only; MSX2 / PC-98 (the manual's platform) code for this rule not read (PC-98 `TABL`,
  `_get_social` and `_sell` were compared s82 and are identical).
- **Decision (user s82):** manual by default, the X68000 code's behaviour as a rules option (blanket call for D4-035…054). A later PC-98 read may turn this into a platform difference (not a conflict).
- **Remaster:** built s83: `p4.night-watch` (`P4Combat.WatchWakesParty`). Wired s97 (`P4OutdoorLoop`); reading `[~]`: the
  watch wakes the members this night attack put to sleep (no one else outside combat is asleep).
- **Side effects:** none beyond the rule.
- **Spec:** `logic-specs/combat.md` §10, §12 item 8; `outdoor.md` §5.

## D4-049 — Beg for mercy

- **Source:** manual p. 40: 「パーティの所持金や品物を相手に渡して立ち去ることになります」 — "you hand over the party's gold and
  goods and leave."
- **Code (X68000) `[C]` static** (`_enc_opt` `0x1CE02`, `_enc_end` `0x24AE2`): on success party gold becomes gold mod
  256; no items are taken.
- **Platforms:** X68000 code only; MSX2 / PC-98 (the manual's platform) code for this rule not read (PC-98 `TABL`,
  `_get_social` and `_sell` were compared s82 and are identical).
- **Decision (user s82):** manual by default, the X68000 code's behaviour as a rules option (blanket call for D4-035…054). A later PC-98 read may turn this into a platform difference (not a conflict).
- **Remaster:** built s83: `p4.beg-mercy` (`P4Combat.BegForMercy`: all party gold). 
- **Amount (user s84):** all party gold plus one random unequipped item from each member. Built s85 (`BegItemsTaken`).
- **Side effects:** none beyond the rule.
- **Spec:** `logic-specs/combat.md` §8, §12 item 9.

## D4-050 — Weakness

- **Source:** manual p. 50: 「敵の強さを奪うので、攻撃を受けてもダメージが少なくてすみます」 — "it saps the enemy's strength, so its
  attacks do less damage."
- **Code (X68000) `[C]` static** (`_pmagic1`): lowers the monsters' +1C (their penalty to the party's hit chance) to
  80 / 60 / 40 / 20 % and cancels a monster Strength bonus; the monsters' damage value (+18) is untouched.
- **Platforms:** X68000 code only; MSX2 / PC-98 (the manual's platform) code for this rule not read (PC-98 `TABL`,
  `_get_social` and `_sell` were compared s82 and are identical).
- **Decision (user s82):** manual by default, the X68000 code's behaviour as a rules option (blanket call for D4-035…054). A later PC-98 read may turn this into a platform difference (not a conflict).
- **Remaster:** built s83: `p4.weakness` (`P4Combat.Weakness`: the code's per-rank factors 0.8 / 0.6 / 0.4 / 0.2 applied to the monsters' damage instead of their defence; the Strength bonus is cancelled either way; confirmed user s84).
- **Side effects:** none beyond the rule.
- **Spec:** `logic-specs/combat.md` §9, §12 item 10.

## D4-051 — Spell damage ranges

- **Source:** manual pp. 49–50 and the spell list: Fire Flash 1–10 / 30 / 60 / 100, Mind Blast 1–8 / 18 / 30 / 44; p. 48:
  offensive spell effects depend on 「となえる者のレベルや呪文のランク」 — "the caster's level and the spell's rank".
- **Code (X68000) `[C]` static** (`_pmagic1` `0x2BCC4`): Fire Flash = rand(N) × Int / 15 (Int 18 → up to 1.2 N; Int < 15
  never reaches N); Mind Blast 1 … N − 1, and no effect on monsters whose experience is a multiple of 7 below 2000. The
  code uses Int (Fire Flash), max MP (the resistance roll) and the encounter level (the groups' mean level, s98; Sleep, Fear, Dissolve,
  Dispel Undead), and the caster's level only for Fear, Dispel Undead, Teleportation and Resurrection.
  The "× 5 + 50" success rolls (resistance R, Fear, Resurrection) compare unsigned (s110, `combat.md` §9): a limit below
  0 (a gap of 11+ against the caster) makes the spell always work, not never. The manual gives no success chance for
  these, so this is not a new conflict. Remaster (s110, `P4Combat.SuccessRoll`): unsigned under the code option
  (`p4.spell-damage` = x68000), signed under the manual option. User s111: Fear's roll stays under `p4.spell-damage`
  with the other seven (not moved to `p4.spell-wording`).
  Healing r restores rand(6 r² + 4) − 1 = **0**–9 / 27 / 57 / 99 HP, in and out of combat (manual 1–9 / 27 / 57 / 99, p. 55;
  `spells-noncombat.md`).
- **Platforms:** X68000 code only; MSX2 / PC-98 (the manual's platform) code for this rule not read (PC-98 `TABL`,
  `_get_social` and `_sell` were compared s82 and are identical).
- **Decision (user s82):** manual by default, the X68000 code's behaviour as a rules option (blanket call for D4-035…054). A later PC-98 read may turn this into a platform difference (not a conflict).
- **Remaster:** built s83: `p4.spell-damage` (`P4Combat.FireFlash`, `MindBlast`, `HealingSpell`: 1–N, no XP-7 immunity). 
- **Decided (user s84):** flat ranges only; the caster's level does not change spell damage.
- **Side effects:** none beyond the rule.
- **Spec:** `logic-specs/combat.md` §9, §12 item 11.

## D4-052 — Smaller spell-wording mismatches

- **Source:** manual p. 53 Slow table: Slow 1 enemy stride 1/2, Slow 2 enemy stride 3/4 (as printed; transcription line 1836); p. 51 Dissolve 「敵をゆっくりと溶かしてしまいます。」 "slowly dissolves the enemy", Fear "the
  enemy flees in terror", Teleportation "to another place".
- **Code (X68000) `[C]` / `[~]` static:** Slow 2 stride ¼ (`_mcalmove`); Dissolve hits all monsters at once on one roll;
  Fear only lowers the encounter level by 6 `[~]`; Teleportation moves back to the previous square.
- **Note:** the user may judge some of these to be wording only.
- **Platforms:** X68000 code only; MSX2 / PC-98 (the manual's platform) code for this rule not read (PC-98 `TABL`,
  `_get_social` and `_sell` were compared s82 and are identical).
- **Decision (user s82):** manual by default, the X68000 code's behaviour as a rules option (blanket call for D4-035…054). A later PC-98 read may turn this into a platform difference (not a conflict).
- **Remaster:** built s83: `p4.spell-wording` (`P4Combat.SlowStrideFactor` ½ / ¾, `Fear` → the monsters flee on the code's roll, confirmed user s84; `Teleport`, `DissolveRounds`).
- **Amounts (user s84):** Dissolve spreads the same total damage evenly over the cast round and the next two.
  Teleportation is wording: back to the previous square (the code) by default, with an option for a random open square
  on the same level / outdoor area or the dungeon entrance. Built s85: `DissolveDamageByRound` (remainder in the earliest
  rounds); option `p4.teleport` (`previous` in every preset / `random` / `entrance`), no longer part of `p4.spell-wording`.
- **Side effects:** none beyond the rule.
- **Spec:** `logic-specs/combat.md` §9, §12 item 12.


## D4-053 — Identify always succeeds

- **Source:** manual p. 54 (Identify #15): 「アイテムの特性を教えてくれますので、魔法の力を持っているかどうかを、知るのに効果があります。
  但し、いつでも識別出来るとは限りません。」 — "Tells an item's properties … **but it does not always succeed.**"
- **Code (X68000) `[C]` static:** spell 15 outside combat opens the Item Appraisal screen (`_pmagic3` `0x2D3EC` →
  `_ident` `0x2DDBA` → `_ident_mi` `0x2DA08`); the chosen item's description is always shown, no roll.
- **Platforms:** X68000 code only; MSX2 / PC-98 (the manual's platform) code for this rule not read (PC-98 `TABL`,
  `_get_social` and `_sell` were compared s82 and are identical).
- **Decision (user s82):** manual by default, the X68000 code's behaviour as a rules option (blanket call for D4-035…054). A later PC-98 read may turn this into a platform difference (not a conflict).
- **Remaster:** built s83: `p4.identify` (`P4Outdoor.IdentifySucceeds`). 
- **Amount (user s84):** success chance 50 % + 5 % per caster level, up to 100 %. Built s85.
- **Side effects:** none beyond the rule.
- **Spec:** `logic-specs/spells-noncombat.md`.

## D4-054 — Monster Evaluation never misreports

- **Source:** manual p. 55 (Monster Evaluation #49): 「その地域にいるモンスターのだいたいのレベルを知ることが出来ます。時々、間違った
  報告をすることもありますが…」 — "Tells the rough level of the monsters in the area. **Sometimes it reports wrongly** …"
- **Code (X68000) `[C]` static** (`_pmagic3` `0x2D3EC`): in a dungeon it shows the map's fixed level (`_dmlv`),
  outdoors the current encounter level clamped to 1–12; no random error.
- **Platforms:** X68000 code only; MSX2 / PC-98 (the manual's platform) code for this rule not read (PC-98 `TABL`,
  `_get_social` and `_sell` were compared s82 and are identical).
- **Decision (user s82):** manual by default, the X68000 code's behaviour as a rules option (blanket call for D4-035…054). A later PC-98 read may turn this into a platform difference (not a conflict).
- **Remaster:** built s83: `p4.monster-eval` (`P4Outdoor.MonsterEvaluation`: a wrong report is off by N up or down). 
- **Amounts (user s84):** wrong (50 % − 5 % per caster level, minimum 10 %) of the time, off by exactly 1 level up
  or down, kept within 1–12. Built s85 (down on rand(2) = 1).
- **Side effects:** none beyond the rule.
- **Spec:** `logic-specs/spells-noncombat.md`.

## D4-055 — Monster hit points per platform

- **Source:** the manual gives no monster HP rule.
- **Code `[C]`:** X68000 rolls 1.8 × base HP for about 80 % of monsters, a probable operator-precedence slip; MSX2 rolls
  0.5–1.4 × base (`FORMAT.md` §6.4, checked at CPU level s82). Not a manual conflict: a platform difference inside the rules
  source, the same kind as D4-034.
- **Decision (user s82):** MSX2's 0.5–1.4 × base by default; X68000's formula as a rules option.
- **Remaster:** built s83: `p4.monster-hp` (`P4Combat.MonsterHp`: `msx2` default, `x68000` option).
- **Side effects:** fights are easier than on X68000.

## D4-056 — Very weak ancestors give maximal abilities

- **Source:** the manual is silent (p. 17: "the computer calculates the genes"; no rule for weak ancestors).
- **Code `[C]`** (CPU check s82, `ancestry.md` §5.2): `_get_modifer` steps from 18.0 down to 5.0; when the ancestors'
  bonus-free average is below 5.0 the loop runs out and returns the double left in d0:d1 (≈ 5.7 × 10⁻³¹³), so every die
  rolls 6 and the descendant gets 18 (before race modifiers) in that ability. 400 real `_mdice` calls at average 4.5 gave
  18 every time. MSX2 not checked. Reachable with weak "new" ancestors (rolled 3–4 abilities) or Hall persons whose class
  rows subtract a lot (Filmon's Str and Con come out at 6).
- **Decision (user s84):** a bug. Below 5.0 the exponent is the table's last value (k = 5.635: weak dice); the code's
  run-off (all 6s) as a rules option.
- **Remaster:** built s85: `P4Creation.InheritanceExponent` returns k = 5.635 below 5.0; `p4.weak-ancestors` = `x68000`
  keeps the run-off (all 6s).
- **Side effects:** inheritance only.
- **Spec:** `logic-specs/ancestry.md` §5.2.

## D4-057 — Outdoor "M Map": a view switch, or a step?

- **Source:** manual p. 35 (M 地図): 「あなたがもっと大きな視野で地図を見たい場合は、「M 地図」を指定して下さい。現在いる地区の周囲、3×3の
  全体図を見ることが出来ますし、この大きな地図の上で移動することも出来ます。そして再び「M 地図」を指定すると、元の部分地図に戻ります。」 —
  "To see the map with a wider view, choose M Map: you see the 3 × 3 block around the current area and can move on it;
  M Map again returns to the local map." Nothing about time or encounters.
- **Code (X68000) `[C]` static** (`_omain` `0x15E0A`–`0x15E38`): M flips the overview flag (`0x43002`), reloads the map
  picture and sets the loop's "moved" flag, so `_omain` runs the whole step on the same square again: the terrain's hours
  pass (`_dayout`), water / lava damage and the tile's question repeat, and the encounter roll is made again.
- **Platforms:** X68000 only; MSX2 / PC-98 not read.
- **Decision:** DECIDED (user, s98): **obey the manual** — M is a pure view switch (no step).
- **Remaster:** `P4OutdoorLoop.ToggleMap` only flips the view; option `p4.map-step` (`manual` default in the Original (manual)
  and Remaster presets, `x68000` in the Original (X68000) preset) runs the code's step on each toggle.
- **Side effects:** time (aging) and encounter frequency for a player who toggles often.
- **Spec:** `logic-specs/outdoor.md` §9.

---

## D4-058 — Aim Blow: aimed at the head or torso, or just a harder hit?

- **Source:** manual p. 43 (command table): M 狙い討ち Aim Blow — 「敵の頭や胴体を狙って1回攻撃します。攻撃よりも命中率は下がります。」 —
  "One attack aimed at the monster's head or torso; lower hit chance than Attack."
- **Code (X68000) `[C]` static** (s98): Aim Blow is action 0x0B. `_chatkp` subtracts 15 from the hit chance and `_chatkd`
  adds no damage offset (`combat.md` §3.1–3.2). The hit location comes from `_mdamage` → `_mlget(type)`, and the type
  comes from `_chdtyp` (`0x28266`), which reads only the monster's +43 flag and whether attacker and target fly. It never
  reads the action. So an Aim Blow lands where any melee blow lands: u^0.5 over the six parts, head about 3 % and torso
  about 8 % of hits (type 5 monsters: never head or torso).
- **Platforms `[C]` s99 static:** MSX2 (player disk segment `4001`; actions counted from 0x0B, Aim Blow = 0: hit chance
  `0x251f` −15, damage offset `0x29c6` none, `_mdamage` twin `0x2b43`, `_mlget` twin `0x2de0`) and PC-98 (`P4.EXE`
  unpacked: hit chance `0x1a985`, `_mdamage` twin `0x1a1ac`, `_mlget` twin `0x1a033`) do the same as X68000: only −15
  to hit; no version reads the action when it picks the body part, so no binary has an Aim Blow split of its own. The
  X68000 re-check found no other reader of 0x0B in the hit / damage / wound path (`_chatkf`, `_getmd`, `_setmc`,
  `_chdtyp` never read it). PC-88 not checked.
- **Decision (user s99):** the manual: an Aim Blow that hits lands on the head or the torso. The user asked for the
  binaries' percentages; since none has its own, the split is the code's melee roll cut to those two parts: u =
  (rand(100) − 1) / 100, part = trunc(6·u^0.5): u 0.00–0.02 head, 0.03–0.11 torso → **head 3 in 12 (25 %), torso 9 in
  12 (75 %)**.
- **Remaster:** rules option `p4.aim-blow` (`P4Body.AimedLocation` / `MonsterHitLocation`, `P4Battle` melee): `manual`
  (Original (manual) and Remaster presets) · `x68000` (the code: the ordinary melee roll). Manual-silent detail handled
  automatically: a type-5 target (big monsters, fliers hit from the ground; manual p. 46: blows can't reach the head)
  keeps the code's arm / leg roll. Body shapes 2 and 3 still turn the hit into "body" / torso as for any blow.
- **Side effects:** under the manual, Aim Blow becomes the way to kill or stun outright: a lost head or torso kills a
  monster, and a serious wound there makes it helpless (hit chance 200).
- **Spec:** `logic-specs/combat.md` §3, §14.

## D4-060 — Training past level 15: the X68000 wraps the level back to 1

- **Source:** manual p. 27 (training hall: "if he has the points for the next level, it shows the cost…"), p. 29
  ("as experience points grow, the character can go up levels by training at the guild"), p. 17 / D4-015 ("Priests can
  reach higher levels than other classes"). The manual never names a level cap and never says levels restart.
- **Code `[C]` static (s102):**
  - X68000 `_calpow` (`0xC320`): x = L + 1; when x > 15, x = x mod 15 (`__CLMOD` at `0xC868`); the learnable-spell row is
    x − 1 and **the level byte +08 is written with x** (`0xC910`). So a level-15 character who trains becomes **level 1**
    (16 mod 15), and no X68000 character ever passes level 15. The training gate itself allows up to level 30.
  - MSX2 (segment `100C`, `0x0E43`–`0x0ED7`): the same mod-15 row for the spells, but the level is raised with
    `ld a,1; add a,(hl)` — levels 16 … 30 work.
  - Both platforms re-read the spell schedule from row 1 at levels 16+ (nothing new for most classes); at L = 29 the row
    index is −1, which reads the previous class's level-15 row (Thieves: three bytes before the table).
  - PC-98 not checked.
- **Decision:** DECIDED (user, s103): **MSX2 as default** (levels go on to 30; manual + D4-015); the X68000 wrap stays an option.
- **Remaster (built s102, both sides):** rules option `p4.level-wrap` (`P4Training.LevelAfter`): `msx2` (Original (manual)
  and Remaster presets; matches the manual's higher levels and D4-015's decided Priest levels 31–35) · `x68000` (Original
  (X68000) preset: level 15 trains to 1). The mod-15 spell row is the same under both (`P4Training.SpellRow`).
- **Side effects:** under `x68000` D4-015's Priest levels 31–35 are unreachable.
- **Spec:** `logic-specs/training.md` §4.1.

## D4-061 — Which spells can be cast outside combat, by place

- **Source:** manual pp. 54–55: usable anywhere outside combat Identify (#15), Magic Bag (#50); only in town
  Transportation (#47), #65; in the wilds or dungeons Levitation (#13), Monster Evaluation (#49), #63; anywhere (also in
  combat) Healing (#1–4), Resurrection. pp. 32 / 35 (summaries) say only Healing, Resurrection, Transportation in town and
  only Healing, Monster Evaluation, Resurrection outdoors — they contradict pp. 54–55 inside the manual.
- **Code `[C]` static (s102):** `_getmagic` places (table `0x44AC8` / `0x44AE2`): outdoors 1–4, 13, 40, 15, 49, 50; dungeon
  the same + 63; town 1–4, 13, 40, 15, 47, 50, 65. So the code also offers **Levitation in town** (manual: wilds or
  dungeons) and **no Divine Aid (#63) outdoors** (manual: wilds or dungeons). Everything else matches pp. 54–55.
- **Decision:** DECIDED (user, s103): **manual pp. 54–55 as default**; the code's lists stay the `x68000` value.
- **Remaster (built s102, both sides):** rules option `p4.cast-places` (`P4TownRules.CastList`, `rule-tables.json`
  `services.spell_places`): `manual` (pp. 54–55: no Levitation in town, Divine Aid also outdoors; Original (manual) and
  Remaster presets) · `x68000` (the code's lists).
- **Side effects:** none beyond the two spells (Divine Aid outdoors gives map 1–24's hint by `_cmapn`, as in a dungeon).
- **Spec:** `logic-specs/spells-noncombat.md` ("Which spells each place offers").
## D4-062 — Party info: experience shown as a tenth, gold as the total

- **Source:** manual p. 36: 「P [original text] … 町を出てから獲得した所持金、経験値、新しいアイテム」 — "shows the gold, experience and
  new items the whole party has gained since leaving town".
- **Code `[C]` static (s102):** `_party_info` / `_p_info1menu` (`0x16290`) prints `_par`+1C (party gold, not reset on
  leaving town) and `_par`+18, the experience pool, which `_esub01` stores as trunc(XP / 10) per fight; the town gate shows
  pool × 10 (`towns.md` §2.1). So the screen shows **a tenth of the experience gained**, and the party's whole purse rather
  than the gold gained since town (under D4-030's manual entry the purse is paid into the accounts, so it is close to
  "since town"; under the code it is not). "Items found" counts the pack, which `_welcome` empties at each town entry,
  so that part matches.
- **Decision:** DECIDED (user, s103): **manual as default** — full experience (pool × 10) and the gold gained since
  leaving town; a `p4` option keeps the code's display (pool as stored, whole purse). Option to build.
- **Remaster (s104):** `p4.party-info`: `manual` (default) shows the gold gained since the party last left a town
  (`P4Party.GoldSinceTown`: fights and gold events add, `_exittown` clears; saved) and the pool × 10; `x68000` the purse
  and the pool as stored.
- **Side effects:** none (display only).
- **Spec:** `logic-specs/towns.md` §3 (Party strategy and profile page 2).

## D4-070 — Teleportation (and flight / Beg) in a location map: the code does not move the party

- **Source:** manual p. 51 (spell table): Teleportation 「戦場からパーティ全員を別の場所へ移してしまいます。」 — "moves the
  whole party from the battlefield to another place". p. 40: L Flee 「ダンジョンでは、部屋のドアのところまで…逃げます。そして次の
  ターンで戦場から離脱します。」 — "in a dungeon you flee to the room's door … and leave the battlefield on the next turn" (the
  battlefield, not the map square); B Beg 「…渡して立ち去ることになります」 "hand over … and leave".
- **Code (X68000) `[C]` static (s102):** `_enc_end` (`0x24AE2`) copies `_cox` / `_coy` into `_ccx` / `_ccy` for ends 2
  (Teleportation), 3 (Beg) and 4 (fled) (`0x24C28`, `0x24C4A`); Greet (106) and every win skip it. `_ccx` / `_cox` are the
  **outdoor** square: `_cox` is written only by `_omove` / `_save_ship` / `_oload`, and `_ccx` is read only by outdoor code
  (`_oencgo`, `_omain`, `_oship_mark`, `_getxy`, `_e_terrain`, `_save`). Outdoors `_oencgo` reloads the map at `_ccx`, so
  the party is back on the square it came from (a sea fight's ship is moored there). On a location map the party's square
  is `_cmapx` / `_cmapy`; `_denc` / `_rndenc` / `_exe_ev` do nothing with the end code, so **the party stays on the fight's
  square** and the event or pass goes on (the write to the outdoor `_ccx` is dead: `_oload` overwrites it on the way out).
- **Why it is raised:** D4-052 (user s84) kept "back to the previous square (the code)" as the default destination of
  Teleportation, on the reading that the code steps back everywhere. In a location map it does not: there the code's
  Teleportation leaves the party where it fought, which the manual's "to another place" does not describe. Flight and Beg
  in a location map are not conflicts (the manual says nothing about the map square).
- **Platforms:** X68000 only; MSX2 / PC-98 not read.
- **Decision:** DECIDED (user, s103): **(a)** — previous square stays the default in location maps too; the code's
  "stay on the fight's square" is added as a new `p4.teleport` value (to build).
- **Remaster (s104):** `P4FightReturn.For` sends Teleportation to the previous square on both map kinds by default
  (`p4.teleport` = `previous`, every preset); `random` picks a square with an open side that is not water `[~]`,
  `entrance` the square where the party came onto the map `[~]` (outdoors there is no entrance: previous); `stay` is the
  code: previous square outdoors, the fight's square in a location map. Flight and Beg follow the code.
- **Side effects:** none beyond where the party stands (in a location map the square's event does not run again).
- **Spec:** `logic-specs/combat.md` §8, §11, §14.

## D4-071 — Spell 64 in combat: listed, but it does nothing

- **Source:** manual spell table (pp. 52–53), combat spells: 「???????? (#64)」 — 「神の御業にかかわる呪文です。説明は差し控えさせ
  ていただきます。」 "A spell concerning the works of the gods; we refrain from describing it." No MP cost or effect is
  printed. The spell list leaves #64 blank.
- **Code (X68000) `[C]` static (s102):** the combat spell pages of `_getmagic(member, 0)` (page table `0x44AC8`, spell
  lists `0x44AE2`) list 64 (page 4, with 20–23, 30–33, 59–62); 63 (Divine Aid) is only on the dungeon page and 65
  (Super Transport) only on the town page. `_chrexe` says the name and calls `_pmagic2`, which has no branch for 64
  (its spell compares stop at 62); afterwards MP is set to 0 for 64 (`combat.md` §9). So in combat it costs all MP and
  has no effect.
- **Why it is raised:** the manual presents #64 as a combat spell with a (secret) divine effect; the code gives it none.
  The manual states no rule to implement, so this may be judged "not a conflict" (manual silent on the effect).
- **Platforms (s104, static `[C]`, `combat.md` §9.1 "MSX2 / PC-98"):** the same on all three. MSX2: combat page 4 lists
  64 (tables identical to X68000), the cast prints its name, the second spell handler (seg `4004`) has no branch for it
  (returns 0 at `0x1034`), MP set to 0 at `0x1599`; name `Annihilate E.` PC-98 (`P4.EXE`, EXEPACK unpacked): the same
  (handler `0x289E8` falls through at `0x29568`, MP 0 at `0x1B3AB`); name `Annihilate Enemy`. No platform gives #64 an
  effect.
- **Decision:** DECIDED (user, s105): keep the code. Giving #64 an effect is wanted as a QoL option (off = the code);
  user s117: "make up an effect proportional to what the spell should be".
- **Remaster:** the code — castable in combat when known with MP ≥ its cost, no effect, MP 0
  (`P4Battle.CombatSpellBuilt`, `Cast`). QoL option `p4.annihilate` (s117; `x68000` in every preset, `divine` on
  request): with M = the caster's MP at the cast, every monster on the field takes 2M + rand(2M) − 1 (2M … 4M − 1; table
  `combat.annihilate` in `rule-tables.json`), no resistance roll, no immunity, no armour, no wound, never the party; MP
  then 0 as in the code (`P4Battle.Annihilate`). Not an original rule: invented to fit the strongest combat spells (about
  3 damage per MP, unblockable, the whole pool at once).
- **Side effects:** none.
- **Spec:** `logic-specs/combat.md` §9.1.

---

## D4-072 — Outdoor "M Map": a block around the party, or a fixed block?

- **Source:** manual p. 35 (M 地図): 「現在いる地区の周囲、3×3の全体図を見ることが出来ます」 — "you can see the 3 × 3 overall map
  around the area you are in." (Same passage as D4-057.)
- **Code (X68000) `[C]` static** (`_omap_load` `0x157F6`, `_omove` `0x149DC`, `FORMAT.md` §6.9 "Outdoor map"): the overview
  draws one of three **fixed** blocks of 3 × 3 maps: block 0 = maps 1 2 3 / 6 7 8 / 11 12 13, block 1 = 3 4 5 / 8 9 10 /
  13 14 15 (Scandor is 5 maps wide, so the two blocks overlap in the middle column), block 2 = the Netherworld 16–24. The
  block changes only when the party walks out of it, so the party's map can sit in any of the nine cells (map 1 always in
  the top-left corner). In the Netherworld (maps ≥ 16) every cell except the party's own map shows map 16's picture.
- **Reading:** "around" can mean "the block that contains it" (the code) or "centred on it" (a block of the 3 × 3
  neighbours, which would show off-grid edges for maps on Scandor's rim). The manual says nothing about the Netherworld.
- **Platforms:** X68000 read; MSX2 / PC-98 not read.
- **Decision:** DECIDED (user s109, option C): the code's fixed blocks by default, the centred reading as an option.
- **Remaster:** option `p4.overview` (OUTDOOR OVERVIEW): `x68000` (FIXED BLOCKS) in every preset — the code's blocks
  (`P4OutdoorLoop.OverviewBlock` / `OverviewMaps`, data `rule-tables.json` `outdoor_terrain.overview`); `centred` (CENTRED
  ON PARTY) — the nine maps around the party's map (`P4OverviewTable.CentredOn`, grid laid out from the blocks), cells past
  the world's edge black; in the Netherworld the other cells still show the hidden map (the code's rule; the manual is
  silent there).
- **Side effects:** display only (no rule changes either way).

---

## D4-073 — Rename (and naming) accepts a name of spaces only

- **Source:** the manual's Guild pages ("R Rename", "give it a name") say nothing about which names are allowed. Raised
  by the s183 pad pass ("Rename takes a blank name"): the pad's name wheel has a space (one Left from A).
- **Code (X68000) `[C]` static** (s185): `_re_name` (`0x7520`) clears a 10-byte buffer, reads up to 8 characters with
  `_wscan(0x20, buf, 8)` (`0x755A`), and goes back to the roster pick with no question only when the **first byte is 0**
  (`0x7564`–`0x756E`: an empty entry, Return at once). `_wscan` (`0x34550`) takes every byte 0x20–0x7E and 0xA0–0xDF as a
  character (the space included) and trims nothing, so " " or "   " passes the test, is asked Okay / Cansel and becomes the
  name. Creation: `_mkname` (`0x13624`) calls the same `_wscan` with no test, its callers `_make2` (`0x136DA`) and
  `_make5` (`0x13E4C`) go straight on to `_mkdetail`, which copies the 10 bytes into the record (`0x12B42`): even an
  empty name is kept.
- **Decision:** NOT A CONFLICT (s185): the manual is silent, so the remaster follows the code. Refusing a name of only
  spaces would be a QoL option (not built).
- **Remaster:** as the code. Rename refuses only the empty name and takes a name of spaces
  (`P4TownLoop.Rename`, test `Rename_AnEmptyNameChangesNothing_ANameOfSpacesIsTaken`); creation takes whatever is typed,
  empty included (`NameAndRecord`).
- **Side effects:** a member named only with spaces shows as an empty name in the roster and party lists.
- **Spec:** `logic-specs/towns.md` (Roster, R Rename).

---

## Checked, no conflict (s81)

- (s102) Manual p. 27, Magic Training Hall: "when he can learn no more, the message 'This training is over' appears" —
  in the code that message (今回の訓練は終了です) closes the level-up skill picks (`_sklup`); the Magic Hall just returns
  to the member pick when no open spell is left. Wording / placement only; not raised.

Manual statements compared with decoded code or data that agree:

| Manual | Code / data |
|---|---|
| p. 25: opening balance Noble 768, Peasant 0 | gold = social × 256 (`_mkdetail`) |
| p. 22: higher class → more gold at each level-up | `_calpow`: social^social × level × 30 |
| p. 8: Hall = Lord Wood, Filmon, Nikademus + up to 8 III transfers = 11; transfers only make descendants | `FAME` 3 shipped + 8 import slots; Hall persons never join the party |
| p. 10: up to 10 characters registered; party of 6; create in any town's guild | 10 guild slots; `pmemb` 6 + elemental slot; origin from the creation map |
| p. 17: First ancestor N/H; N limited to Phantasie III races and classes; Second ancestor same / different race; the computer decides the race and whether a mixed race results | `_make1` N/H; N's race lists are I–III's 15 (+ Ogre/Troll list), III's six classes; `_mopta` S/D rolls (code also offers "H: race of a Hall person" for the second ancestor, which the manual does not mention) |
| p. 9: 5 races + 13 random characters | `_racemenu` 5 + `_rrace` lists 8 + 5 = 13 |
| (s106) p. 6: "New Game" opens the starting town, Pendragon, where characters are made and the party formed | `_load(0)`: town, map 1 (Pendragon), empty guild; the town menu offers only the Guild until a party exists (`FORMAT.md` §6.1) |
| (s106) p. 9: if the abilities shown don't please, choose again as often as you like; then name the character | Okay / Cansel after the roll; Cansel returns to the question, so race and class are picked and rolled again; Okay → name (`ancestry.md` §10) |
| pp. 13–19: random characters Thief/Fighter only; Hill Giant Fighter only; Spriggan Thief only; Priest = the 5 races + some mixed races; Gargoyle class only for Gargoyles | `class_mask_by_race` (Priest also for Half Ogre, Half Orc, Kobold-Dwarf, Gnome-Goblin, and the five human-type hybrids) — except D4-013 |
| pp. 17–18: Thief and Monk +1 stride | `moverate_class` Thief +1, Monk +1 |
| p. 17–18: Thieves need least experience, then Fighters; Shadow Warriors need a great deal | `_trang` ratios (Thief r ≈ 2.7, Fighter ≈ 1.6; Shadow Warrior Int+Dex+Luck / 75 ≈ 0.7) |
| p. 19: Wizards begin missile training at level 7 | Throwing skill table, Wizard: 1 at levels 1–6, 50 from level 7 |
| p. 19: Shadow Warrior → Shadow Priest at 5 (spells; Defence, Find traps, Listen, Swim), Shadow Lord at 11; low HP before 5 | Shadow Warrior skill tables jump at level 5 (Parry 40→80, Spot Trap 5→50, Disarm 10→40, Listen 7→30, Swim 0→15) and 11 (Attack 130→190, Navigation 80→99, Listen 48→70 at 12); HP 2,3,3,3 then 9 at 5 |
| p. 19: Gargoyles start with more HP | Gargoyle HP 8 at level 1 (Fighter 5) |
| p. 11, p. 27: Charisma affects training and spell prices; low Intelligence may fail a spell lesson | `_trang` (20 − Cha)²; `_mage` price / Cha, success rand(100) < 5 × Int |
| p. 27: "This training is over" when no more can be learnt | string 今回の訓練は終了です (`_mage`) |
| p. 24, p. 30: 9 items per character | record +52–5A (9 bytes) |
| p. 24 Figure 4: Small Shield 7 / 10 g, Medium Shield 25 g, Magic Shield 14 / 2000 g, Clothing 1 / 5 g, Robes 2 / 10 g, Hard Leather 4 / 25 g | `_idat` values and base prices identical (the shop multiplies by the town factor, `towns.md` §6.1) |
| p. 58 Tables 3–4: healing potion 1, 4, 9, 16, 25, 35, 49, 64, 81, 100; magic potion 3 … 30 | item catalogue identical (effects in code not decoded) |
| Spell list numbers 1–65 | `_magic` index + 1; #63–65 = Divine Aid, Annihilate Enemy, Super Transport (the manual's undisclosed "????????" spells, in the wilds/dungeon, combat and town sections) |
| p. 29 Figure 6: two "Ancestor: Unknown" lines for a plain character; Luck as sixth ability | `_page1`, `_dsp_uni` ("Unknown" for kind none) |
| p. 29: Score = overall rating of the character's achievements | record +09 "Score" (`_page1`) — supports the `[~]` field name |
| p. 6, p. 33: save in 3 slots | `_diskmenu` `*1:Save`–`*3:Save` |
| p. 35: outdoor area 26 × 26 squares | `MOUT` 700-byte records (26 × 26 = 676 + header `[~]`) |
| Supplement: Program + Player disks, saves on the Player disk in drive 2 | X68000 saves on drive 1 = disk 2, the player disk (`FORMAT.md` §6.1) |
