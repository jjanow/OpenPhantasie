"""Make a Phantasie I Amiga test disk whose title shows other pictures (s124): cover1.pic -> FIRST, cover2.pic -> SECOND
(names of at most 10 characters), and the title's LoadRGB4 table (h77 +0x342, hunk 69 +0xb4) -> hunk 69 + PAL.

    p1_amiga_patch_title.py ADF_COPY FIRST SECOND PAL     e.g.  p1_amiga_patch_title.py /tmp/x/p1.adf decor.pic final.pic 0x94

Works on a copy of the master ADF (never the original): patches `game` in memory, then writes it back in place with
adf_patch_file.py (same size, OFS checksums). cover1 shows while the title tune plays (~53 s), cover2 for 5 s after it,
both in the patched palette. Capture with fsuae_run.sh ... "texture_filter = nearest" for exact pixels."""
import struct
import subprocess
import sys
import tempfile
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from extract_amiga_p3_common import hunks  # noqa: E402

adf, first, second, pal = sys.argv[1], sys.argv[2].encode(), sys.argv[3].encode(), int(sys.argv[4], 0)
assert len(first) <= 10 and len(second) <= 10
game_path = Path(__file__).resolve().parents[2] / "reverse-engineering/amiga/unpacked/PHANT/game"
game = bytearray(game_path.read_bytes())
for old, new in ((b"cover1.pic\0", first), (b"cover2.pic\0", second)):
    new = new.ljust(len(old), b"\0")
    count = game.count(old)
    game[:] = game.replace(old, new)
    print(old.decode().rstrip("\0"), "->", new.decode().rstrip("\0"), count, "time(s)")
off = hunks(bytes(game))[77]["file_off"] + 0x342
assert struct.unpack(">I", game[off:off + 4])[0] == 0xB4
game[off:off + 4] = struct.pack(">I", pal)
with tempfile.NamedTemporaryFile(suffix=".game", delete=False) as f:
    f.write(game)
subprocess.run([sys.executable, str(Path(__file__).with_name("adf_patch_file.py")), adf, str(game_path), f.name], check=True)
Path(f.name).unlink()
