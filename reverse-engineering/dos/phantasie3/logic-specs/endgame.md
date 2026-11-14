# Phantasie III — score, the final fights, the gods' reward (Darklord / Superhero)

Phase 09 T4 3c.6 (static). Source of truth: the III manual — it does **not** describe the
win condition (manual question Q17; the quest text PDF 3 / pr.1, PDF 15 / pr.25 only names Nikademus, Filmon and the
scrolls), so nothing here is a conflict. Code: DOS `MAIN.EXE` (addresses `../FORMAT.md` section 1). Tags: `[C]` code / data
bytes, `[~]` probable, `[?]` not decoded. Settles D3-006's "how 4/5 are reached".

## 1. The final fights `[C]`

Monsters 77 and 78 (both "Nikademus") and 79 ("Lord Wood") are fought only through dungeon event programs (fight op
type 5, `../FORMAT.md` section 9.5); the four such ops in `DNGS.DAT` are all in **dungeon 9, "A dark fortress"** (record 8,
program offsets 0x3d9 / 0x40d → 77, 0x46f → 78, 0x3f7 → 79), which lies in the **Netherworld** at (6, 10)
(`planes.md` section 4). The Netherworld is reached by the exit with destination 41 in dungeon 11, the Castle of Dark (its
cell 0xd8, next to which "break the wand" teleports the party); dungeon 3 (the crystal building) has the exits to the
Light and Dark planes (the whole chain is in `dungeon-events.md` section 5). Who meets Nikademus at all: only a
party with no member at score ≥ 200 (`dungeon-events.md` section 5).

After a fight (combat_main root `0x104c0`, end of round root `0xdb42…0xdb8e`), if the rank-1 group is monster 77 or 78
and its first member is dead → outcome **2**; if it is 79 and dead → outcome **3** (the fight also ends). These ids
cannot be fled from, greeted, begged or made to surrender (`encounters.md` section 4). The dungeon fight routine (ovl4
`0x1d30`) pays the normal rewards and then calls the reward scene with the outcome (ovl4 `0x1e64…0x1e78`).

## 2. Score `[C]` (root `0x9884`, `compute_scores`)

```
for each party member:
    s = sum of W[b] for each set bit b (0..7) of record +0x8a     # W = DS:0x1c50 = 0, 4, 8, 12, 17, 20, 25, 30
    score (+0x82) = level / 6 + s                                 # recomputed from scratch
    if social class == 4 (Darklord):  score = clamp(score, 200, 249)
    if social class == 5 (Superhero): score = max(score, 250)
return the highest score in the party
```

The loop zeroes `+0x82` and then adds `level / 6 + s` `[C]`. Bits of `+0x8a` are quest steps (max 116 from bits +
level / 6); they are written only by dungeon event programs (`dungeon-events.md` section 4: Kilmor found, funeral, wand, Old Bow, Gnome Bow,
Lord Wood, Castle of Light, Castle of Darkness) `[C]`. Called at the Mystic (`town.md` section 5) and on leaving a
dungeon (ovl4 `0x95b`).

Uses of the score: the Mystic's rating (< 30 novice … ≥ 90 expert) and the **XP needed to train: halved from 200,
divided by 6 from 250** (`training.md` section 2). So only the two titles reach those thresholds in practice (bits give at
most 116, plus level / 6).

## 3. The reward scene `[C]` (ovl4 `0x0000`)

Outcome 2 (Nikademus killed) → the good side; outcome 3 (Lord Wood killed) → the evil side:

| | Nikademus killed | Lord Wood killed |
|---|---|---|
| God (string 536 / 537) | **Zeus** | **Pluto** |
| Cause (538 / 539) | good | evil |
| Title (544 / 545) | **Superheroes** | **Darklords** |
| Social class written | **5 Superhero** | **4 Darklord** |
| Score written | **250** | **200** |

Sequence: "[original text] lights" / "[original text] such intensity …",
every party member's status set to Okay, a picture from `PICS.CMP`, then "[original text] [Zeus. |
Pluto.] [original text] [original text] truly incredible … You have done a
great service for the cause of [good | evil] [original text] [original text] status a mortal
can ever attain. You are now [Superheroes. | Darklords.]' … '[original text] adventuring and use
your great wisdom [original text] [original text] [original text]"

For each party member (not the elemental): body areas healed and status Okay (root `0x1dbb`), social class := 5 / 4,
score := 250 / 200, rank := Front. The code computes a clamp to `base … base + 49` into a local but then stores `base`
itself, so every member ends at exactly 250 / 200 `[C]` (a dead store; the Mystic's recompute later lifts a
Superhero above 250 again).

Then: current town := 1 (Pendragon), plane := Scandor, `FAR:0xfc9a` := 3 (cleared on arrival, keeping found items
and party XP, root `0xa911`), and **the game goes on** — there is no end screen or credits; the party is back in Pendragon with its new titles. Effects that last: the Mystic adds "and heroes of
the Netherworld." (class 4) / "[original text]" (class 5); training XP ÷ 2 (Darklord) or ÷ 6 (Superhero);
the sheet shows the social class name.

## 4. Remaster notes

- The scene is shown: Core keeps `GameSession.PendingRewardScene` (the intro lines, the speech, good / evil and
  the party's races; not saved) and `SessionPrompts` asks it as `RewardScene` after the fight's own keep questions (ovl4
  `0x1d30` pays the fight's rewards first); the Client draws the Amiga's layout (`reverse-engineering/amiga/phantasie3/
  FORMAT.md` section 3.1). The intro's second line is DOS's full text (root DS `0x2edc9`).
- One game can award both titles to different characters only by fighting both bosses with different parties `[~]`;
  a character's title is overwritten by the later scene.
- The two Nikademus records (77 / 78) differ in stats (`extracted/monsters.json`); either kill counts as the good win.
- The manual's App. II social-class text and the copy-protection quiz ("How many different social classes are
  there? (page 23)") stay as they are (D3-006, decided: not a conflict).
