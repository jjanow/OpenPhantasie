# Phantasie III logic specs (3c.6)

Behaviour of DOS III where it differs from I, as rules and pseudocode (no C#). Formats and record fields:
`../FORMAT.md`; function addresses: `../FUNCTIONS.md`; manual ↔ code conflicts: `DIFFERENCES-III.md` (repo root).
Tags: `[C]` from code / data bytes, `[~]` probable, `[?]` not decoded. s79 / s81 specs are static; s81 ran the first DOSBox
checks (3c.8: transfer level, D3-018 defence, D3-019 Down targets, damage running sum, group fields); s82 a second run
(D3-002…004, D3-010…015, D3-017, D3-019 Out, D3-020, dungeon events; `../screens-s82/`).

| Work-list item (`docs/plan/03c-phantasie3-re.md`) | Spec | State |
|---|---|---|
| Body areas: injury, status, restrictions, healing, inns | `body-areas.md` | `[C]` (hit-location row 1 user; status letters in `encounters.md`) |
| Bows, Fire Bow, Arrow Flame, Accuracy | `bows.md` | `[C]` (Fire Bow growth: `training.md` §5) |
| Aim, party ranks, action menu, melee to-hit | `combat-actions-and-ranks.md` | `[C]`; defence, Down → 100, running sum and group fields verified live s81 |
| Round loop: turn order, monster turns / specials, sleep and waking, round start / end, rank swap | `combat-round.md` | `[C]` static (s87); four live checks open |
| Social class at creation, starting gold, class kit, first spell | `../FORMAT.md` §5.1 | `[C]`; endgame titles in `endgame.md` |
| Equipment slots and requirements | `../FORMAT.md` §4.1, §5.1 | `[C]` |
| Save files | `../FORMAT.md` §8 | `[C]` layout, globals partly `[?]` |
| Skill training (3 per level), training costs, HP per level | `training.md` | `[C]` (s81; D3-022, D3-023) |
| Monster status U/S/D/K, fleeing, reputation, Greet / Surrender / Mercy | `encounters.md` | `[C]` (s81; D3-021, D3-024; two flags `[?]`) |
| Spells Arrow Flame / Accuracy, Divine Aid | `bows.md` §5, `dungeon-events.md` §6 | `[C]` |
| Bank fee, Trade, Distribute, saving / autosave, Mystic, Drop | `town.md` | `[C]` (s81; D3-025…D3-028) |
| Elementals as a 7th member | `elementals.md` | `[C]` (s81; no conflict) |
| Planes, terrain, places, Ocean / River Styx, Astral Plane, scrolls | `planes.md` | `[C]` (s81; swim / lava damage `[~]`) |
| Endgame: score, final fights, Darklord / Superhero | `endgame.md` | `[C]` (s81; quest-bit writers s82 in `dungeon-events.md`) |
| Dungeon event squares, the 27 event ops, quest / deed bits, Divine Aid, exits, record ↔ dungeon map | `dungeon-events.md` | `[C]` (s82, static; live checks in 3c.8) |
| Data files WORLD / PLNS / INITARMS / SCROLLS / DNGS | `../FORMAT.md` §9 | layouts `[C]`; DNGS ops / exits in `dungeon-events.md` |
