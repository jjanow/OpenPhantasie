# Phantasie III — wilderness maps, planes, terrain, places, River Styx, Astral Plane

Phase 09 T4 3c.6 (s81, static; nothing run in DOSBox). Source of truth: the III manual (Wilderness PDF 7 / pr.9,
Terrain key PDF 13 / pr.21, map sections PDF 15 / pr.24, scrolls PDF 12 / pr.18–19). Code: DOS `MAIN.EXE` (ovl3 =
wilderness; addresses `../FORMAT.md` §1). File formats: `../FORMAT.md` §9. Tags: `[C]` code / data bytes, `[~]`
probable, `[?]` not decoded. No manual ↔ code conflict raised here (the manual is silent on most of it).

## 1. State `[C]`

| Global | Meaning |
|---|---|
| `0x27f3:0x0cc4` | **current plane**: 0 Scandor (Material Plane), 1 Light, 2 Dark, 4 Netherworld ("Death"). Code tests `& 3` (Light / Dark) and `& 4` |
| `FAR:0x09e6` | plane whose map is loaded (−1 / 1 at start forces a load) |
| `FAR:0x4882` / `0x4884` | party x / y on the current map (start 24, 37 = Pendragon) |
| `FAR:0x9f8`, `0x487e`, `0x97ec`, `0x9814` | copies of x / y (previous step, position to return to) `[~]` |
| `FAR:0x97c8` / `0x27f3:0x0cce` | map width / last row index |
| `FAR:0x488a` | far pointer to the current map bytes (tile ids, row-major: `map[y · width + x]`) |
| `0x27f3:0x0cbc`, `FAR:0xdd84` | place type / number found at the party's square (§4) |
| `FAR:0xdd98` | current town (1–14 Scandor, 15 Light, 16 Dark, 17 Death) |
| `FAR:0xe596` | in a town; `FAR:0x487c` = in the wilderness loop |

## 2. Maps per plane `[C]` (root `0x125d`, `load_plane_map`)

When the plane differs from the loaded one:

| Plane | Size (w × h) | Source | Start square (when arriving from another plane) |
|---|---|---|---|
| 0 Scandor | 50 × 75 | `WORLD.DAT` (3750 B, read once at start, ovl1 `0x5e0`) | — (position kept) |
| 1 Light | 9 × 9 | `PLNS.DAT` offset 0, 81 B | (4, 1) |
| 2 Dark | 9 × 9 | `PLNS.DAT` offset 0x51, 81 B | (4, 1) |
| 4 Netherworld | 31 × 16 | `PLNS.DAT` offset 0xa2, 496 B; the 2 bytes after the map are set to tile 0xf9 (River Styx) | (7, 3) |

The plane is chosen by the town the party is in (root `0x1e18`): town 15 → Light, 16 → Dark, 17 → Netherworld, 1–14
→ Scandor. So the planes are reached through their towns (Light / Dark / Death; creation in town 15 / 16 / 17 gives
origin Light / Darkness / Netherworld, `../FORMAT.md` §5.1); how a party gets from Scandor to town 15–17 is a dungeon
exit: exit destinations 29 / 31 / 41 put the party on the Light / Dark / Netherworld plane at the start square above
(dungeon 3, the crystal building: Light and Dark, once its lever is set; dungeon 11, the Castle of Dark: the
Netherworld; corrected s82, the s81 text used the wrong record ↔ dungeon map; `dungeon-events.md` §1, §7) `[C]`. Every
other exit leaves the party on its dungeon square. A dead party is sent to plane 0 (§6). The manual's "alternate
planes of good and evil" (PDF 7 / pr.8) = Light / Dark; "Other planes, dimensions, have their own labels" (PDF 15 /
pr.24) agrees (the planes are separate small maps).

## 3. Terrain `[C]` (ovl3 `0x375`)

The tile id under the party gives the terrain (name = master string 54 + t, shown when it changes):

| Tile ids | t | Name |
|---|---|---|
| 0–25 | 10 | Water (swim test, §5) |
| 26–38 | 19 | (no name: town tiles `[~]`) |
| 39–46 | 20 | (no name: dungeon tiles `[~]`) |
| 47–54 | 21 | "A Tavern" (inn tiles `[~]`) |
| 61–67 | 14 | Dense Fog |
| 68–74 | 11 | Bright Mist |
| 90–114 | 3 | Mountains |
| 115–154 | 2 | Hills |
| 155–187 | 9 | Desert |
| 188–205 | 0 | Meadows |
| 206–209, 214–224 (222 = 13 "A Bridge") | 6 | Cobblestone Path |
| 225–241 | 1 | Forest |
| ≥ 242 | 15 | The River Styx (sets the "blocked" flag `FAR:0xe594`) |
| anything else (55–60, 75–89, 210–213) | 4 | (empty name) |

On the **Netherworld** plane these override: 61–77 → 17 Black Path, 78–82 → 16 Mist, 83–87 → 18 Steam, 88–241 → 8
Lava. Standing on Lava after a move hurts the party (root `0x2426(13, 1, 3)`, the same routine as Water) `[C]` call,
`[~]` damage size. On any plane other than Scandor a blocked square shows "The River Styx". Names 5 / 7 / 12
("Netherworld", "Ocean") are not produced by this table; "Ocean" is the blocked text on Scandor (§5).

## 3a. Time and aging per step `[C]` (s88)

The party's "days out" counter (`FAR:0x9e2`, u32, saved; `../FORMAT.md` §8) counts **thirds of a day**: the Party
Status screen prints it ÷ 3 (root `0x1c6d`). Root `0x14a9` adds, by the terrain word `FAR:0xb61a`:

| Terrain t (§3) | Added | Time |
|---|---|---|
| 1 Forest | 2 | ⅔ day |
| 2 Hills, 3 Mountains | 3 | 1 day |
| anything else (Meadows, Path, Desert, Water, Mist, Lava, Styx, …) | 1 | ⅓ day |

- **Wilderness:** every arrow-key move (ovl3 `0x28c`, `0x2f3`) calls it right after the move and **before** the loop top
  re-reads the terrain (ovl3 `0x184` → `0x375`), so the cost is that of the square **being left**. A move pushed back
  (map edge, River Styx) has already been charged.
- **Dungeon:** `FAR:0xb61a` is set to 4 on entry (ovl4 `0x9f0`, restored on leaving, `0xd2e`), so each step that moves
  (step result ≥ 5) adds 1 (⅓ day) (ovl4 `0xc02`); a bump into a wall costs nothing.
- **Aging** happens only on **entering a town** (root `0xa911` → ovl3 `0x65a`): every party member with status ≠ Dead
  gets age clock (`+0x28`, units of 1/26 year = 2 weeks) += `⌊⌊days_out / 3⌋ / 14⌋`, i.e. one unit per 14 full days out.
  Leaving the town (ovl3 `0x88`) sets days out to 0, so the remainder under 14 days is dropped each trip. A new or
  disbanded party also zeroes it (root `0xa861`). The town inn adds 1 unit per stay (ovl2 `0x3577`).
- So one wilderness step costs no age by itself; a trip ages the party ⌊days / 14⌋ units on return. The manual gives no
  step time. Built s91 (`P3TravelTime`, kept in the session's travel-time word as thirds of a day).

## 4. Places `[C]` (ovl3 `0xa22`, `place_at_party`)

Places are **hard-coded coordinates** in the code, not map data. Type 1 town (name string 111 + n), 2 dungeon
(string 33 + n, = `DNGS.DAT` record n − 1), 3 independent inn (string 45 + n; 250 gold, ovl3 `0x8cc`), 4 map edge.

- **Map edge** (y = 0, y = last row, x = 0 or x = width − 1): type 4, number 12 (Scandor) / 15 (planes), blocked.
  (ovl3 `0xa38…0xa71`: y against 0 and the rows word `FAR:0xcce`, x against 0 and the column count `FAR:0x97c8` − 1; the counts: Scandor 50 × 74, Netherworld 31 × 15, Light / Dark
  9 × 8, `MAIN.EXE` file offsets `0x152b…0x1672`, Scandor's 74 rows also at start-up `0x1c472`, s125.)
  **Amiga differs** (s125, `../../../amiga/phantasie3/FORMAT.md` §4, §8.7): its test `0x9c92` compares x with the
  count itself, so the last column (Scandor 49, Netherworld 30, Light / Dark 8) can be entered there; rows as DOS. The
  remaster keeps DOS's edge (DOS wins on numbers).
- **Netherworld:** (7, 3) town 17 "Death"; (6, 10) dungeon 9 "A dark fortress".
- **Light / Dark**: (4, 1) town 15 / 16; Light (6, 7) dungeon 10 "The Castle of Light"; Dark (2, 7) dungeon 11 "The
  Castle of Dark". The Scandor list below is also checked on these planes, but none of its squares lies inside a 9 × 9
  map.
- **Scandor** (x, y → type n):

| Square | Place | Square | Place |
|---|---|---|---|
| (24, 37) | town 1 Pendragon | (46, 9) | dungeon 1 "A very small straw hut" |
| (15, 39) | town 2 Sandy Shores | (25, 8) | dungeon 2 "Many men guard a tent here" |
| (5, 35) | town 3 Pendleton | (48, 36) | dungeon 3 "A large crystal building" |
| (35, 42) | town 4 Rocky Hills | (36, 38) | dungeon 4 "A large cavern starts here" |
| (43, 52) | town 5 Deltor | (25, 39) | dungeon 5 "The Pendragon town archives" |
| (22, 65) | town 6 Tirith | (2, 35) | dungeon 6 "The Dwarven burial grounds" |
| (11, 69) | town 7 Tierrin | (28, 67) | dungeon 7 "A giant wooden building" |
| (25, 27) | town 8 Asmith | (7, 66) | dungeon 8 "A cave smelling of sulphur" |
| (5, 51) | town 9 Sierion | (24, 20) | inn 1 The Mystic Inn |
| (44, 66) | town 10 Xanador | (46, 21) | inn 2 The Portal Inn |
| (44, 5) | town 11 Flagler | (43, 34) | inn 3 The Crown Inn |
| (22, 12) | town 12 Lansing | (24, 32) | inn 4 The Jolly Inn |
| (8, 9) | town 13 Scanport | (14, 24) | inn 5 The Kanpai Inn |
| (15, 22) | town 14 Sciattica | (2, 21) | inn 6 The Desert Inn |
| | | (22, 50) | inn 7 The Royal Inn |
| | | (2, 65) | inn 8 The Deadly Inn |

  The Pendragon archives (dungeon 5, (25, 39)) lie two squares south of Pendragon (24, 37), as the manual's "Pendragon
  Archives south of town" (PDF 3 / pr.1) says. 25 sections of 10 × 15 squares give the manual's 5 × 5 map sections A–E / I–V
  (PDF 15 / pr.24) `[~]`.
- Stepping on a place square asks "<name>" / "Will you enter?" (ovl3 `0x6c8`). Town: leaves the wilderness loop and
  sets the in-town flag. Dungeon: ovl4 `0x62d` (dungeon entry / saved map). Inn: needs 250 gold in the treasury
  ("The party cannot afford the stay here."), runs the inn (`body-areas.md` §4, wilderness inn) and then takes 250.

## 5. Blocked squares: Ocean and River Styx `[C]` (ovl3 `0x92c`)

A square flagged blocked (map edge, or a tile ≥ 242) cannot be entered: the label "Ocean" (Scandor) or "River Styx"
(planes) is shown and the party is pushed one square back the way it came (the reverse of the last arrow key). There is
no swim roll and no ferry here; nothing in the code charges a toll or offers a boat `[~]` (no Charon / ferry text in
the program). The manual names "rivers/lakes/seas" and "no encounters on water" (PDF 13 / pr.21).

No random encounters start on Water, Lava or the River Styx (terrain 10 / 8 / 15 skip the encounter roll, ovl3
`0x1f9…0x213`) `[C]` — the manual's "no encounters on water" agrees.

Water (tiles 0–25), when entered on foot: root `0x2426(0, 1, …)` makes each member roll Swim ("swims." / "sinks for
… damage" / "[original text]", strings 514–516) `[~]` (routine not traced in full); the manual's swim rule (PDF 4 /
pr.3, PDF 7 / pr.9).

## 6. Party death: the Astral Plane `[C]` (root `0x1e71`)

When the whole party dies (`FAR:0xfc9a` = 1) the game does **not** end ("Game over" string 441 is never used):

1. "[original text] [original text]" / "Your party has arrived at the gates
   to [original text]" / "[original text]". The elemental is dismissed.
2. For each member, with `r = rand()`:
   - level > `(r & 3) + 2` (i.e. level above 2–5): **resurrected** — body areas healed, status Okay, HP ≥ 1, CON − 1
     ("…[original text]", 522);
   - else level > `(r' & 3)` (0–3): **made Undead** — the same healing and CON − 1, race 17 Undead, **level 20**
     ("…[original text]", 523);
   - else **destroyed** — removed from the roster ("…[original text]", 521).
3. The survivors are placed in a random town 1–14 (`rand() % 14 + 1`), plane 0, `FAR:0xfc9a` = 2. Arriving in that
   town (root `0xa911`) then **empties the found-item list and the party experience pool** (`FAR:0x9d8`, `0x9da`);
   party gold is kept `[C]`.

This is where DOS III's "Undead at level 20" comes from (also used by the I → III transfer, D3-007). Undead cannot
train or learn spells (`training.md` §1). The manual says nothing about a wiped party; not a conflict.

## 7. Scrolls `[C]` (root `0xeb`, `read_scroll`)

Using a scroll item (ids 121–140, town only) shows text from `SCROLLS.DAT` (19 records × 1050 B, `../FORMAT.md` §9.4):

| Item | Name | Records shown |
|---|---|---|
| 121 | Scroll I | 0 |
| 122 | Scroll II | 1, 2, 3 (three pages) |
| 123 | Scroll III | 4 |
| 124 | Scroll III | 3, 4, 5 |
| 125 | Scroll III | 4 |
| 126 | Scroll IV | the map picture "[original text]" (`SHAPES.CGA`, no text) |
| 127 / 128 | Scroll V / VI | 5 / 6 |
| 129 | Scroll VII | 7, 8, 9 (three pages: "The Universe" …) |
| 130 | Scroll VII | 8 (text cut at byte 869) |
| 131 | Scroll VII | 9 |
| 132–140 | Scroll VIII–XVI | 10–18 |

Record = `item − 121`, minus one more above item 126; item 123 → 4; items 122 / 124 / 129 show three consecutive
records. The duplicate III / VII items are partial copies of a three-page scroll, as read; whether 124's start at
record 3 is intended `[~]`. Text records are NUL-terminated within their 1050 bytes.

**Built (s95):** Use Item (town only) shows the item's records from `scrolls.json` `item_records` as pages, an empty line
between them; Scroll IV shows the Amiga picture `D/s6` (s97, pack `scroll.picture.126`), or names it when no pack is present.
