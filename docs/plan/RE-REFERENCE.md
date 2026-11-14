# RE reference — address conventions, dead ends, tooling

> Cold file: read before static RE or emulator work (DOSBox-X, FS-UAE, Hatari, XEiJ, the Client on Xvfb).
> Open items live in `PLAN.md`; what a session did lives in `SESSION-LOG.md`.

## Tags

Used across the plan docs, logic specs and `FORMAT.md` files (full list: `reverse-engineering/dos/logic-specs/README.md`):
`[C]` confirmed (read in the code or data) · `[C] live` also seen in a running emulator · `[X]` cross-checked with the
manual · `[~]` probable, unverified · `[?]` open · `[U]` the user decides. `PLAN.md` adds work tags (`[S]` static RE,
`[D+]` DOSBox run with a state still to make, `[D✓]` state exists, `[A]` Amiga, `[I]` implementation).

Manual (I's source of truth): `docs/manuals/phantasie1/Phantasie_I_1986_SSI_manual_text.pdf`; `pdftotext -layout`
reads it. All manuals: `docs/manuals/README.md`.

## Address conventions — Phantasie I DOS (don't mix these up)

| Convention | Formula | Used by |
|---|---|---|
| **File offset** | — | capstone, logic-specs (`OV1 0x2786`), HANDOFF |
| **Old Ghidra export** (`FUN_0000_xxxx`) | = file offset | `reverse-engineering/dos/ghidra/*.functions.txt`, `*.decompiled.c` |
| **rea / Ghidra 12** (`FUN_1000_xxxx`, `0x1xxxx`) | linear = file + **0x10100**; `FUN_1000_xxxx` = file + 0x100 | rea MCP tools (`open_binary format=dos-com`) |
| **Runtime CS (OV1 loaded)** | CS = file + **0x3c26** | DOSBox debugger, string pointers in OV1 code |
| **OV1 string immediate** | imm = file + **0x5ff7** (15/15 strings hit exactly once) | `mov`/`push` of a string ptr in OV1 code → grep LE word |
| **OV2 string immediate** | imm = file + **0x433d** | wilderness inn strings |
| **Character record** | `char_index * 0x11a + 0x5bb0` in COM, OV1 and OV2; runtime `+0xNN` = disk `@NN` (name `+0x10e` = `@270`) | all party loops |
| **OV1/OV2 jump tables** | `jmp cs:[bx+T]` → file = T − **0x53dc** (lifespan `0x0ece`, training `0x12be`; OV2 action chooser `0x6f78`) | switch dispatch inside OV1 and OV2 |
| **OV1 → COM near call** | **unpacked** COM image (`re_common.unpack_phantasi_com()`) = target − **0xac24**. The older "COM file = (target & 0xffff) − **0xa934**" holds only inside one packed literal run (`e5ca`→`0x3c96` RNG, `d1c9`→`0x2895` print-at) | `call 0xffffxxxx` in OV1 |
| **COM data pointers** | format strings: file = ptr + **0x54b8**; name tables: file = ptr + **0x56c5** | sheet renderer `0x2ef4` |
| **PHANTASI.COM is EXEPACK-packed** | MZ at file `0xf0`, image `0x2f0`, `RB` header `0x86a0`. Unpacked image: DS:x = image `0xc9f0 + x` (DS = SS = `0xc9f`). The per-window deltas above (`0x56d3`/`0x56c5`/`0x54b8`) are packing artifacts, valid only inside one literal run | `tools/scripts/re_common.py` `unpack_phantasi_com()`, `com_ds()` |

Key runtime globals: found/party gold `CS:0x63f6` (Core `Sack.Gold`; `[0x63f4]` = its pair, P0.1 purse split) · active
party size `[CS:0x648e]` · monster runtime table `DS:0x1a6c` stride `0x39`.

## Do-not-redo list — Phantasie I DOS (details in `HANDOFF-2026-07-18.md`)

- The training dispatch table is at OV1 file `0x12be` (jump-table delta `0x53dc`). Reading it as `CS:0x669a` was a wrong
  delta.
- Monster runtime offsets Armor `+0x14` / CV `+0x25` / Defense `+0x2d` / AttackPower `+0x37` are right. A check once
  "disproved" them because it compared against a `monsters.json` parsed from EXEPACK-packed bytes. Never parse data from
  the raw COM file: unpack it first.
- OV1 string pointers are locatable: `file + 0x5ff7`.
- `+0xda` is the **in-party flag** (Guild Add = 1, Drop = 0), not status; a status-write grep on it is pointless.
  Status is `+0x18` (`@24`: 0 DEAD / 1 OK / 2 SLEEP).
- `PHANTASI.COM 0x659a` 6-entry table: unrelated to training.
- DOS I has **no Temple town service** (and no Temple cost): don't hunt for one. OV1 `0x2786` is Guild → Add member.
- Byte-editing `@218` to fake a dead character doesn't work (shows OK). Get a real death in DOSBox.
- rea on orphaned handler bodies: same blind spot as the old export (see "rea assessment"); use capstone.
- capstone on the raw (packed) `PHANTASI.COM` works for `0x2a80..0x3060` only: that range lies inside one EXEPACK literal run.

## Emulators and the Client: general rules

- **Virtual screen only** (`AGENTS.md`): every emulator, the Client, `xdotool` and screenshots run on a private Xvfb
  display. Pick a free number first (`/tmp/.X11-unix/X<N>` absent); never kill an Xvfb you did not start. One display,
  game copy and out dir per run, so several agents can work at once.
- **No sound on the speakers:** route audio to a PulseAudio null sink (`PULSE_SINK=<sink>`, plus `ALSOFT_DRIVERS=pulse`
  for OpenAL programs) or turn it off; start metacity with `CANBERRA_DRIVER=null`. Check `pactl list sink-inputs`
  afterwards. Per tool: the Client script and `fsuae_run.sh` handle it; Hatari's `start.sh` passes `--sound off`;
  DOSBox-X's `start.sh` turns all of this off and runs on its own null sink. `[mixer] nosound=true` alone is **not enough** (a FluidSynth
  MIDI stream still opened on the real sink): add `[midi] mpu401=none` + `mididevice=none`, `[speaker] pcspeaker=false` +
  `tandy=off`, `[sblaster] sbtype=none`, `[gus] gus=false`, start it with `PULSE_SINK=<a null sink of your own>` and check
  `pactl list sink-inputs` (the DOSBox-X pid must be on your sink or absent); for XEiJ see its section.
- **Killing:** `pkill -f PATTERN` also matches the calling shell's own command line and kills it. Kill by PID or with
  `pkill -x NAME`. Likewise `pgrep -f "Xvfb :78"` in a wait loop matches the loop itself and never ends (one loop spun
  2 h 39 min): use `pgrep -x Xvfb` plus a check of its args, or the pattern `'[X]vfb :78'`.
- **Keeping an emulator up across Bash calls:** processes started by a foreground Bash call die when that call ends.
  Either start, wait and screenshot in **one** call, or start the emulator from a `run_in_background` call that then
  waits (on its PID or a long `sleep`, with a hard timeout) and drive it from other calls.
- Before finishing, stop everything you started (emulators, Xvfb, metacity, the Client, build servers).

## DOSBox-X (I and III DOS)

- Scripts: `reverse-engineering/dos/working/xvfb/{start,key,shot,stop}.sh` (Xvfb + `output=surface` + `import`,
  `xwd|ffmpeg` fallback; also `step.sh`, `walk.sh`, `round.sh`). Every script takes `XDISP=:N` (default `:99`) and
  `OUT=` / `GAME=` (a scratch game copy). `reverse-engineering/dos/working/run.sh` is the interactive launcher.
- **Stopping:** only with `stop.sh` (SIGKILL; closing the window pops a quit dialog; `start.sh` also sets
  `quit warning=false`). DOSBox-X ignores SIGTERM, and a second `start.sh` runs beside the old instance: check
  `pgrep -x dosbox-x` first. `stop.sh` finds its instance by `DISPLAY=` in `/proc/<pid>/environ`, which can be
  unreadable here, so it may silently kill nothing: then match the conf path in `/proc/<pid>/cmdline` or kill by PID,
  and check `pgrep -x dosbox-x` again.
- **Input:** overworld moves need the arrow keys (letters and keypad don't work). Guild selection lists **keep the last
  cursor row**, and the field Options menu **remembers its last row and wraps**: screenshot before Return. Lists
  **scroll** (Add / Inspect show 3 summon slots past the 22 roster records).
- **Setting up states (I):** patching `DNGX.SAV` next to the DNG1 entrance (20, 0) puts any event one step away.
  Forced-fight cells `0x80..0x89` take the monster from DNGX `0x4e8 + (cell − 0x80)` (0-based id; DNG1 `[1..9, 0]`):
  patch that byte, not `0x468..0x471` (map cells). Level-1 parties rarely win a DNG1 fight; single RANGERs can.
- **Memory:** this DOSBox-X build has no debugger (`debugbox` = Bad command). Read memory from save states
  (`autosave=4 1`, `-savedir`; III tool `tools/scripts/p3_dosbox_state.py`, notes in `03c-phantasie3-re.md` 3c.8).
  Writing works too: F12+S saves a state, `tools/scripts/p3_dosbox_poke.py` edits the zip's `Memory`, F12+L loads it
  back (slot = `[dosbox] saveslot`). Set `saveremark=false` (else F12+S starts `kdialog` and the emulator waits; unset
  `WAYLAND_DISPLAY` so nothing can open on the real desktop). A partial `mapperfile` unbinds every key: keep the default
  mapper. The DOSBox-X window is 640 px wide on Xvfb (menu clicks use those coordinates). III scripts:
  `reverse-engineering/dos/phantasie3/screens-s110/`.
- Other static tools: capstone via `pip`; Ghidra headless project `reverse-engineering/dos/ghidra/PhantasieDOS.rep`; rea MCP.

## The remaster Client on Xvfb

Use `tools/scripts/client_xvfb.sh` (required by `AGENTS.md`):
- `start :N [game args]`: Xvfb + metacity + `play.sh`, a throwaway XDG home (real settings and saves stay untouched),
  sound to its own null sink `phantasie_xvfb_N`. Waits up to 3 minutes for the window (`play.sh` may build first).
- `key :N space 3 sleep:1 …` (focus + held key each), `shot :N out.png`, `frames :N dir COUNT [s]` (bursts for
  animations), `audio :N out.wav SECONDS` (records the sink; run it in the background, then press keys), `stop :N`
  (also unloads the sink).
- A key that "does nothing" is usually a screen that doesn't take it (e.g. IV shop lists have no Space-back).
- III fight recipe: `--test-party --test-level 10 --test-game 3` (Amiga look): by pad (vpad below) B leaves the
  Guild for the street, Start pulls Options down, D-pad down ×4 and A take Exit, A outdoors starts a fight, A opens
  its Options, A takes Fight: the orders screen. No key pressed, so the help stays in the pad's words. Parry all and
  cast a weak spell (Fireflash I) so the monsters outlive the round and its flash shows (a last-round flash is never drawn).
- Pad testing (the user owns no pad): `tools/scripts/vpad.py <fifo>` makes a virtual Xbox 360 pad through
  `/dev/uinput` (writable for this user; SDL maps it with no setup). Start it before the Client, send `press A`,
  `dpad down`, `trig LT 255`, `axis RY 30000`… into the FIFO, then `quit` (the device is system-wide).

## rea assessment (2026-10-10)

Ran the rea MCP tools (Ghidra 12.1.2 backend) over the three DOS binaries:

| Binary | rea procedures | Old-export funcs | Bytes inside a func (old export) |
|---|---|---|---|
| `PHANTASI.COM` (34,816 B) | 217 | 214 | 64 % |
| `OV1.OVR` (25,703 B) | 89 | 88 | 58 % |
| `OV2.OVR` (32,963 B) | 107 | 111 | 75 % |

- rea's auto-analysis has the **same blind spot** as the old export. Every runtime-dispatched facility handler
  (Training `0x1160`, Mystic `0x20bb`, Guild Add `0x2786`, Inn `0x2a0d`) resolves to `not_in_procedure`, and
  `inspect_native_instruction` at Mystic returns `undecodable` (never disassembled).
- So rea is good for inventory, strings, xrefs, and decompiling and comparing *recognized* procedures. For orphaned
  handler bodies use **capstone `CS_MODE_16` on raw file offsets**. rea cannot create a function at an orphaned entry
  either (`annotate_native_function 0x121bb` → "No procedure exists"), so there is no rea path to decompile them.
- Byte coverage includes data tables and strings, so it is **not** a parity measure: it only says ~⅓ of each binary
  has no function boundary.
- rea keeps no state between sessions (its Ghidra project is deleted on close, its unknowns store was empty): track
  open questions in `PLAN.md` and the specs, not in rea.

## C64 cross-check
Images: `reverse-engineering/c64/original-images/phantasie1/de/Phantasie_I_1986_SSI_de_Side_{A,B}.d64` and `en/`
(+ `SOURCES.txt`). The `de` set is the **German** release, a cracked copy: search German text (e.g.
"AUFERSTANDEN"/"WIEDERBELEBT"), not the DOS English strings. 6502 code; capstone 5.0.9 has `CS_ARCH_MOS65XX`. Files:
`tools/scripts/extract_c64_d64.py` (standard D64 layout, directory at track 18 sector 1). Load address = first 2 bytes
of each PRG.

## Amiga full-system runs (FS-UAE)

- **Emulator:** FS-UAE 3.2.35 (apt `fs-uae`). Kickstart 1.3 rev 34.5 (A500) in the repo at
  `reverse-engineering/amiga/original-images/kickstart/kick34005.A500` (SHA-1 `891e9a54…cb785`; supplied by the user,
  `SOURCES.txt` there). FS-UAE's built-in AROS stops on a grey screen with III.
- **Start:** `tools/scripts/fsuae_run.sh WORKDIR DISPLAY_NUM ADF ["key = value" ...]` (A500, 512 K chip + 512 K slow,
  PAL; extra config lines e.g. `"ntsc_mode = 1"`). Copies the ADF into WORKDIR as a writable `df0.adf` on every launch
  (`cp -f`): copy `WORKDIR/df0.adf` away first to keep a party. Sound is off unless `PULSE_SINK` names an existing sink;
  record FS-UAE alone with `parec --monitor-stream=<its sink input>`. It silently reuses any Xvfb already on the display
  number given. III's disk reaches its title in ~30 s, then its "Welcome to the World of Phantasie!" menu.
- **Zoom:** use `"zoom = 640x400"` with `"texture_filter = nearest"` (no scale lines): the whole 320 × 200 screen shows at
  3× in the 960 × 600 window, and the Amiga pointer moves 1 px per 2 host px from the homed corner. This is what
  `tools/scripts/fsuae_drive.py` assumes. The default zoom crops the screen to x 40–279. Other set-ups seen:
  `"scale_x = 2.0" "scale_y = 2.0"` shows the screen at 4× (picture x 40–279 / y 25–174 inside screen rect
  (480, 240)–(1440, 840) of the 1920 × 1080 root shot); `"scale_x = 1.0" "scale_y = 1.0"` shows all 320 × 200 at 2×,
  screen origin at window (160, 100).
- **Input:** `tools/scripts/fsuae_drive.py :N …` (Amiga screen pixels at zoom 640x400; see its header for every
  command). SDL ignores `xdotool --window` (XSendEvent), so `focus` starts metacity and focuses FS-UAE once, then keys
  and the mouse go as real XTEST events. Keep FS-UAE's default input grab (do not pass `automatic_input_grab = 0`) and
  click into the window once to grab: without it the host pointer leaves the window and the Amiga mouse stops. The Amiga
  mouse is relative: `home` pushes the pointer into the top-left corner first. `menu X Y X2 Y2` holds the right button
  over the title, moves straight to the item, waits 0.5 s and releases (Guild › Add member, Exit Guild, town › Save game,
  Party Options › Fight all all picked this way). Most menu letters also work as keys. `ashot FILE` saves exact
  320 × 200 pixels from FS-UAE's own screenshot (F12+S into WORKDIR/screenshots; the crop starts 5 rows above screen
  row 0). The older `tools/scripts/fsuae_input.sh` (`click` homes the pointer tip to window (126, 15), then moves 0.9 × the
  distance and lands within ~15 px: check a shot before clicking small targets) misses menu items at this zoom (at 2×
  it fell short onto the ghosted Spells / Training rows; likely cause `[~]`).
- **I's own windows:** after a requester (e.g. "NOT IN THE TOWN / IT TAKES 2 MONTH TO GET HERE", YES) the next pick needs
  two clicks ~0.4 s apart (the first only re-activates the window). From START THIS TURN IN PELNOR one click on
  the Guild's door at screen (288, 112) enters it; Add / Exit Guild are menu picks; coordinates, walks and debugger
  patches (defeat counter, HP / status, a dungeon cell) in `reverse-engineering/amiga/working/capture_s158_filming.md`.
  Copy `df0.adf` only after a save's write has finished (an early copy was a broken disk). The overworld moves by a double click on the next
  cell (cell (x, y) centre = (4 + 12x + 6, 15 + 9y + 4)). The master ADF's guild has ~16 characters in other towns;
  Add moves them to Pelnor.
- **I's title:** `tools/scripts/p1_amiga_patch_title.py ADF_COPY FIRST SECOND PAL` swaps cover1 / cover2 for other
  `.pic` files and the title's LoadRGB4 table; FIRST shows ~85–125 s after boot, SECOND for 5 s.
  `tools/scripts/p1_amiga_sprite_sheets.py build ADF OUT` writes two sheets of sprite frames over `decor.pic` /
  `final.pic` (after `p1_amiga_patch_title.py ADF decor.pic final.pic 0x94`); `compare OUT A.png B.png` counts matches
  with the PNGs.
- **III:** the master ADF (`phantasie3-v1.0-1987-ssi.adf`) holds a played save: six level-21 characters in Pendragon
  ("Continue a previously saved game"). `tools/scripts/p3_amiga_patch_monsters.py` (monsinfo.dat: Wild Cat → Wolf,
  Gremlin → Mud Elemental, Giant Bee casts Fireflash I) + `tools/scripts/adf_patch_file.py` (OFS in place, checksums)
  make a test disk. III moves by clicking the map cell next to the party; in dungeons the number pad moves it. III save:
  `gamesave.dat` + 8518 = the 36-B block `0x24564` (centre x / y, view x / y, party x / y, …;
  `reverse-engineering/amiga/phantasie3/FORMAT.md` section 4); patch only the changed 488-B chunk (`adf_patch_file.py`
  stops on gamesave's second chunk, which another file repeats).
- **I, patching:** `adf_patch_file.py` fails on `OUT1.DAT`: its first block is also on the disk under `backup/` (blocks
  916 and 954), so patch both by hand (`capture_s163_filming.md`). STYX without walking there: the debugger nop
  `W 23240 4e 71`.
- **Tracing calls with the UAE debugger (III sounds):** `"console_debugger = 1"`; F12+D or a breakpoint enters the
  debugger, which reads stdin and prints to stdout. Start with `DEBUG_FIFO=SECONDS fsuae_run.sh …`: it makes the FIFO
  `WORKDIR/dbg.in`, holds it open that long and logs unbuffered to `fsuae.log`. Find the program's load base from the
  DATA hunk: Aztec's A4 = DATA + 0x7ffe, and DATA's first longs are the relocated `jmp` table (III: CODE `0xC07988`, DATA
  `0xC29218` = CODE + `0x21890`). `f ADDR` sets a breakpoint; at each stop `m SP 1` gives the return address and the
  argument, then `g`. `tools/scripts/fsuae_bp_watch.py WORKDIR BASE [ADDR …]` answers the stops on III's `0xb126`, logs
  each sound and caller, and treats extra flat addresses as label stops (logged and resumed) to tell apart sounds played
  through a shared routine. Commands sent while the emulation runs wait for the next stop (F12+D first, then `f` / `W` /
  `r D0 n`, then `g`). The `s` search does not match strings or multi-byte values; `S file addr len` (short relative
  name, written to FS-UAE's cwd) and searching the dump on the host works; `W addr b b b b` patches bytes. Dungeon cells:
  write the level buffer (pointer `[0x241ec]`, cell (x, y) at + 24y + x) rather than the step's cell word, which events
  do not read.

## Atari ST full-system runs (Hatari)

- Harness: `reverse-engineering/atari-st/working/` (Hatari 2.6.1, EmuTOS 1.4, GEMDOS hard-disk folder as `C:`, sound
  off, own Xvfb; scripts and usage in its `README.md`). II's live logs: `reverse-engineering/atari-st/phantasie2/parity/`.
- **Tracing calls (II sounds):** `tools/scripts/hatari_bp_trace.py arm OUT [+ADDR …]` adds trace breakpoints
  (`b pc = <base + addr> :trace :file f`, the run goes on) on PHANT.PRG's sound players, message line, box, dungeon
  member lines, the beeping alerts and AES `form_alert` / `form_do`; `+ADDR` adds label stops at any image address.
  Every hit also reads `_hz_200` (`$4ba`), printed in ms. `parse OUT` prints the hits with Hatari's `VBL` counter
  (60 Hz, EmuTOS US), callers and texts in order. Debugger commands go through the harness fifo (`hatari-debug …`,
  one line per open; `working/cmd.sh`); `b all` clears; `r d0=$n` in a `:once` breakpoint file forces a roll. The tool
  sandbox refuses commands with `$4ba`-style words or variables in them: put debugger lines in a file and feed them to
  the fifo with a small script, one line per open. Set-ups: `reverse-engineering/atari-st/phantasie2/parity/make_p2_s140.py`.

## X68000 full-system runs (XEiJ)
For what the X68000 binaries can't tell statically (CPU-bound timing, screen layout, the OPM's run-time writes).
`FORMAT.md` below = `reverse-engineering/x68000/phantasie4/FORMAT.md`.
- **Emulator:** `~/emulators/XEiJ-0.26.10.08/` (Java; needs **Java 26**: an Adoptium JDK 26 is unpacked at
  `~/emulators/jdk-26.0.2.1+1/`, the system Java 17 / 25 refuse the jar). Ships Sharp's free IPL ROMs and a substitute
  CG ROM. Docs: `parameter.htm` (every `-key=value`), `menu.htm`.
- **Start:** `tools/scripts/xeij_run.sh WORKDIR DISPLAY_NUM DISK1 [DISK2]` (e.g. IV's two `.dim` from
  `reverse-engineering/x68000/original-images/phantasie4/`). Starts `Xvfb :N` when needed; copies the disks into
  WORKDIR (so a party made in an earlier run is gone), keeps `XEiJ.ini` there (`-ini=`, `saveonexit=off`), model
  **Shodai** = the original X68000 (MC68000 10 MHz, IPL ROM 1.0, wait cycles on). It passes `-iplrom16=off` (XEiJ's log
  still prints an "IPLROM 1.6" table at start; MPU menu › "Use IPLROM 1.6" must be unticked) and
  `-flrenabled=off -flrinformed=on` (flashing-lights filter off, as it alters the picture, and its dialog skipped).
  IV boots to its title in ~25 s.
- **Sound:** `xeij_run.sh` routes no audio. Pass `-sound=off` **and** start it with `PULSE_SINK=` / `PIPEWIRE_NODE=` set
  to a null sink (even with `-sound=off` Java opened a stream, which landed in the null sink).
- **Input:** host keys sent with xdotool do not reach XEiJ on Xvfb; X68000 keys go through XEiJ's control pipe
  (`-pastepipe=on`): `echo "typekey n" > WORKDIR/tmp/XEiJControl` (names in `xeij/CONDevice.java` `CON_KEY_BASE`:
  letters, digits, `space`, `return`, `esc`, `tenkey8` …). Pipe writes block when XEiJ is gone: wrap them in
  `timeout 5 sh -c '…'`. The mouse follows the host pointer over the emulated screen
  (`tools/scripts/xeij_keys.py :N click:X,Y`; XEiJ's window at 0,0, screen at window (4, 58), 768 × 512 1:1); park the
  pointer on XEiJ's title bar afterwards. The X68000 cursor's position changes P4.X's loop timing (on IV's street:
  5.74 ms a pass with it on a door, 6.64 off the picture by GIF timing, 6.69 timed directly), so park it in an empty spot
  first (move the pointer to window (100, 480), then (100, 500), then the title bar) and screenshot to check before
  recording.
- **IV keys:** `n` New Game; in town `i` Inn, `b` Bank, `m` Mystic, `s` System; the Guild by clicking its door (no
  building but the Guild opens without a party: Guild › `g` roster › `n` create, then `p` + member letter + `o` to form a
  party). In location maps the party steps with the number pad (`typekey tenkey8` etc.), not the arrow keys.
- **Recording:** `xeij_keys.py :N gif` = Display › Start recording (10 s; `-gifrecordingtime` up to 30):
  `WORKDIR/capture/N.gif`, one frame per **emulated** vsync (identical frames merged; XEiJ writes 2 cs per frame for the
  18.03 ms of mode 16, so delay / 20 ms = vsyncs). Timing follows the emulated clock, not the host's speed. Each 30 s
  recording ends in a JVM crash in XEiJ's GIF compressor (the GIF is complete): boot again for the next one.
- **IV animation timing:** `tools/scripts/xeij_anim_timing.py SCENE GIF...` (scene names of the pack's
  `picture_animations`) → ms per `_anima` pass; results in `FORMAT.md` section 6.10 "Measured".
- **OPM trace, pokes and jumps:** `tools/scripts/xeij_trace_build.sh OUT` builds a patched XEiJ (run it with
  `XEIJ_HOME=OUT`):
  - `XEIJ_OPMTRACE=FILE` logs every OPM register write, timer A overflow and OPM / OPMDRV IOCS call with its emulated
    time (stands in for Sound › OPM log, which does not open on Xvfb).
  - `XEIJ_PCTRACE=ADDR[,ADDR…]` logs each time an address runs (lines `P ADDR`); IV `_anima` = `0x7bcd8`, where the mean
    interval is the pass length (more direct than the GIF method).
  - `XEIJ_PCHOOK=FROM,TO` + `XEIJ_POKE=ADDR=BYTE,…` pokes memory and jumps once when FROM is reached; FROM = TO pokes
    without a jump. `XEIJ_PCHOOK_ARM=FILE` holds the hook until FILE exists. IV addresses: P4.X at `0x4d4e0`, `_disk`
    `0x4f15e` → `_shipyard` / `_ending` (Shipwright and endings, `FORMAT.md` section 6.10);
    `XEIJ_PCHOOK=4f15e,4f15e XEIJ_POKE=8cbef=1` puts Pendragon on the type-1 street, `=2` type 2.
  - `XEIJ_CMDFILE=FILE XEIJ_CMDPC=7bcd8` (`_anima`): write FILE with lines `poke ADDR BYTE …`, `dump ADDR LEN` (→ `D`
    line in the trace file), `jump ADDR` (hex); the hook runs them at the next 32nd pass and deletes FILE, so any number
    of pokes and memory reads can happen during a run. IV: `_encper` low byte `0xCE4C1` = 0x64 + `_encf` `0xCE4C9` = 1
    gives a fight on the next step in a location map.
- **Into a dungeon with a party:** `XEIJ_PCHOOK=7bcd8,7bcd8` (`_anima`) with the arm file set and
  `XEIJ_POKE=c7d78=1,c7d7a=2,c7d7c=MAP,c7d7e=X,c7d80=Y,c7d84=1` (`_cmovf`, `_cnewf` = 2, `_cnewn` / x / y, hex). Make the
  party (Guild door at window (248, 394), `g` `n` `n` `1` `1` `o`, a name + `return`, `space` `e` `p` `a` `space` `o`
  `space` `space` `l`), `touch` the arm file on the street, then `f` `space` (any command that returns to `_town`'s loop):
  `_game` runs `_dungeon` at that square (`FORMAT.md` section 6.10 "Dungeon `_ianim`"). The remaster's same view:
  `--p4-dungeon MAP X,Y` (decimal). Event-prompt recipe: pokes `c7d7c=1,c7d7e=9,c7d80=f` (Town Hall (9, 15)), then
  `typekey tenkey8`, `space`: INPUTS "文字＞" (`reverse-engineering/x68000/phantasie4/logic-specs/events.md` section 4.1).

## Hardware documents already read (s162, for the audio output models)
- archive.org `CZ134X68000ServiceManual` (X68000 CZ-634C / 644C) and `CZ147X68030ServiceManual`: OPM clock and the
  analogue stage after the YM3012 (x68000 `FORMAT.md` 6.11.3), the ADPCM filter's values (6.13). Their `_text.pdf`
  has a text layer for finding pages; the sheets render readably at 300 dpi (`pdftoppm -x/-y/-W/-H` crops).
- archive.org `inside-x68000` (*Inside X68000*, 1992): OPM at 4 MHz, p. 280.
- archive.org `atari-520-st-schematics-jan-feb-1985` (520ST C070112 rev. B) and `SAMSComputerfactsCSCS12Atari520ST`:
  the 520ST's YM2149 output (no low-pass capacitor; atari-st `FORMAT.md` 7.1). `Atari_Mega_ST_Schematic_1986_Atari` is
  the Mega ST (other designators).
- archive.org `Atari_1040ST_Schematic_Rev_C_C070787_Dec_26_1986` (1040STF / STFM C070787 rev. C, Dec 1986;
  plain PDF, render sheet 1 at 600 dpi): the STF's YM2149 output stage, R8 / C10 (atari-st `FORMAT.md` 7.1). Also on
  archive.org: `Atari_1040STf_Schematic_1986_Atari`, `Atari_1040STe_Schematic_1986_Atari`, `atari520st1040stservicemanual`
  (not needed). The elektrotanya.com copy sits behind a captcha.
- archive.org `OutsideX680001993` (*Outside X68000*, 1993): file `Outside_X68000_schematic` is the book's
  schematic appendix (the `.pdf` gave HTTP 500; `_jp2.zip` works, scan NNNN = djvu page NNNN + 1): CZ-600CE (first
  X68000) メイン (1) / (2), サブ, コントロール (scan 0003: the sound section, x68000 `FORMAT.md` 6.11.3), then the
  CZ-634C main / control sheets and the option boards. No Sharp CZ-600C service manual is on archive.org.
