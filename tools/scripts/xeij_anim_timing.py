#!/usr/bin/env python3
"""Measure IV's picture-animation pass length from an XEiJ GIF recording (T6.4, FORMAT.md §6.10 "Timing").

    xeij_anim_timing.py SCENE CAPTURE.gif [CAPTURE.gif ...] [--pack PACK.json] [--vsync-ms 18.0317] [--max-cv 0.15]

XEiJ (launched by xeij_run.sh) records one GIF frame per emulated vsync. It writes each frame's delay in 2 cs steps
(its 20 ms floor; the real period of the 768 x 512 31 kHz mode is 18.03 ms) and merges identical frames, so a GIF
frame's delay / 20 ms is the number of vsyncs it stood.

For every record the scene arms, the script watches the record's rectangle on the 768 x 512 screen (_cvsx / _cvsy of
the scene's display origin + the record's offset) and logs each change and the frame image it settles on (matched
against the pack's frame sheet; colours are compared through a per-image colour mapping, so XEiJ's palette scaling
does not matter). A copy split over two vsyncs (the _ggcopy running across the beam) counts once. The record's frame
list is then aligned with the seen images (frames whose picture equals the one before change nothing on screen and are
walked through). Each frame stays count + 1 _anima passes, count = (rand(range) + base) >> 2 with rand = 1..range, so
between the first and the last seen change

    pass_ms = vsyncs x vsync_ms / (sum of the expected passes of the frames walked through)

summed over the scene's records. Frames with range 1 have a fixed life, so their gaps alone give exact passes.
"""
import argparse
import json
import pathlib
from collections import Counter

import numpy as np
from PIL import Image, ImageSequence

ROOT = pathlib.Path(__file__).resolve().parents[2]
DEFAULT_PACK = ROOT / 'src/OpenPhantasie.Data/GameData/phantasie4/graphics/x68000.json'

# Display origins (P4.X layout coordinates on 640 x 400) of the _setanim call sites, FORMAT.md §6.10.
ORIGINS = {
    'title': (160, 64), 'p4.street.0': (160, 20), 'p4.street.1': (160, 20), 'p4.street.2': (160, 20),
    'p4.guild': (160, 64), 'p4.inn': (160, 32), 'p4.bank': (160, 32), 'p4.shipwright': (160, 32),
    'p4.mystic': (160, 90), 'p4.mystic.reading': (160, 90), 'p4.ending.street': (160, 154),
    'p4.ending.banquet': (160, 32), 'p4.ending.cell': (160, 32),
}


def cvsx(x: int) -> int:
    return x * 704 // 640 + 40 if x else 0


def cvsy(y: int) -> int:
    return y * 456 // 400 + 40 if y else 0


def life(base: int, rng: int) -> tuple[float, float]:
    """Passes a frame stays up, ((rand(range) + base) >> 2) + 1 with rand = 1..range: (mean, variance)."""
    v = [((r + base) >> 2) + 1 for r in range(1, rng + 1)]
    m = sum(v) / rng
    return m, sum((x - m) ** 2 for x in v) / rng


def match(a: np.ndarray, b: np.ndarray) -> float:
    """Share of pixels consistent with a one-to-one colour mapping a <-> b (1.0 = the same picture up to palette)."""
    ka = (a.reshape(-1, 3) @ np.array([1 << 16, 1 << 8, 1])).tolist()
    kb = (b.reshape(-1, 3) @ np.array([1 << 16, 1 << 8, 1])).tolist()
    pairs = Counter(zip(ka, kb))
    fwd, bwd = {}, {}
    for (x, y), c in pairs.items():
        fwd[x] = max(fwd.get(x, 0), c)
        bwd[y] = max(bwd.get(y, 0), c)
    return min(sum(fwd.values()), sum(bwd.values())) / len(ka)


def main() -> None:
    ap = argparse.ArgumentParser()
    ap.add_argument('scene')
    ap.add_argument('gifs', nargs='+')
    ap.add_argument('--max-cv', type=float, default=0.15,
                    help='the "tight" estimate uses only gaps whose expected passes vary by at most this share (sd / mean)')
    ap.add_argument('--pack', default=str(DEFAULT_PACK))
    ap.add_argument('--vsync-ms', type=float, default=1000 / (69_551_900 / (8 * 2 * 138) / 568))
    args = ap.parse_args()
    pack = json.load(open(args.pack))
    pa = pack['picture_animations']
    scene = pa['scenes'][args.scene]
    sheet_path = pathlib.Path(args.pack).parent / '../../../../..' / pack['root'] / pack['pictures'][scene['sheet']]
    sheet = np.array(Image.open(sheet_path.resolve()).convert('RGB')).astype(np.int64)
    # ORIGINS is _setanim's own display origin _adis; a scene's 'at' is where that origin sits in the Client's picture
    # (the ending street: END1 134 rows below the street's top), so it is not added again here (s117).
    ox = cvsx(ORIGINS[args.scene][0])
    oy = cvsy(ORIGINS[args.scene][1])
    first, last = scene['records']
    frames = pa['frames']

    gaps_all = []  # (vsyncs, mean passes, variance) per seen frame change
    for path in args.gifs:
        gaps_all += measure(path, args, pa, sheet, ox, oy, first, last, frames)
    report(f'scene {args.scene}, all gaps', gaps_all, args.vsync_ms)
    report(f'scene {args.scene}, gaps with sd/mean <= {args.max_cv}',
           [g for g in gaps_all if g[2] ** 0.5 <= args.max_cv * g[1]], args.vsync_ms)


def report(what: str, gaps: list, vsync_ms: float) -> None:
    if not gaps:
        print(f'{what}: none')
        return
    v = sum(g[0] for g in gaps)
    p = sum(g[1] for g in gaps)
    ms = v * vsync_ms / p
    # spread: the passes' own variance plus about +-1 vsync of reading error per gap
    sd_v = (sum(g[2] for g in gaps) * (ms / vsync_ms) ** 2 + len(gaps) / 3) ** 0.5
    print(f'{what}: {len(gaps)} gaps, {v} vsyncs / {p:.1f} passes -> pass = {ms:.3f} ms (+- {sd_v * vsync_ms / p:.3f})')


def measure(path, args, pa, sheet, ox, oy, first, last, frames) -> list:
    gif = []
    t = 0
    for f in ImageSequence.Iterator(Image.open(path)):
        gif.append((t, np.array(f.convert('RGB')).astype(np.int64)))
        t += max(1, round(f.info['duration'] / 20))
    print(f'{path}: {t} vsyncs = {t * args.vsync_ms / 1000:.2f} s at {args.vsync_ms:.4f} ms')
    out = []
    for n in range(first, last + 1):
        rec = pa['records'][n]
        (x0, y0), (w, h) = (ox + rec['at'][0], oy + rec['at'][1]), rec['size']
        ids = rec['frames']
        crops = [sheet[frames[i][1]:frames[i][1] + h, frames[i][0]:frames[i][0] + w] for i in ids]
        # image group of each position: positions with an identical picture share a group
        group = []
        for k, c in enumerate(crops):
            g = next((group[j] for j in range(k) if np.array_equal(crops[j], c)), k)
            group.append(g)
        # changes: (vsync, settled region)
        changes = []
        for (ta, a), (tb, b) in zip(gif, gif[1:]):
            ra, rb = a[y0:y0 + h, x0:x0 + w], b[y0:y0 + h, x0:x0 + w]
            if (ra != rb).any():
                if changes and changes[-1][0] == ta and tb - ta == 1 and changes[-1][2]:
                    changes[-1] = (tb, rb, False)  # second half of a split copy: same change, settled later
                else:
                    changes.append((tb, rb, True))
        obs = []
        for tv, region, _ in changes:
            scores = [match(c, region) for c in crops]
            k = int(np.argmax(scores))
            obs.append((tv, group[k], scores[k]))
        # a copy cut by the recording's end, or split over more than one vsync (an OPM interrupt in the middle of the
        # _ggcopy), leaves a picture that is no frame: drop it, so the copy counts once, at the vsync it settled (s117)
        obs = [o for i, o in enumerate(obs)
               if o[2] >= 0.999 or (i + 1 < len(obs) and obs[i + 1][0] - o[0] > 3)]
        if len(obs) < 2:
            print(f'  record {n}: {len(obs)} change(s), skipped')
            continue
        # align: a start position whose walk shows exactly the seen sequence of pictures
        m = len(ids)
        fit = None
        for k0 in range(m):
            if group[k0] != obs[0][1]:
                continue
            k, walk, ok = k0, [], True
            for _, g_seen, _ in obs[1:]:
                passes = var = 0.0
                cur = group[k]
                while True:
                    lm, lv = life(frames[ids[k]][2], frames[ids[k]][3])
                    passes += lm
                    var += lv
                    k = (k + 1) % m
                    if group[k] != cur:
                        break
                    if k == k0 and passes > 1e9:
                        break
                if group[k] != g_seen:
                    ok = False
                    break
                walk.append((passes, var))
            if ok:
                # several start positions can show the same picture sequence (repeating frame patterns): keep the
                # one whose gaps are most proportional to the expected passes
                ratios = np.log(np.diff([o[0] for o in obs]) / np.array([w[0] for w in walk]))
                score = float(np.var(ratios))
                if fit is None or score < fit[2]:
                    fit = (k0, walk, score)
        if fit is None:
            print(f'  record {n}: {len(obs)} changes, frame order not matched (scores {[round(o[2], 3) for o in obs]})')
            continue
        v = obs[-1][0] - obs[0][0]
        p = sum(w[0] for w in fit[1])
        gaps = np.diff([o[0] for o in obs])
        out += [(int(g), w[0], w[1]) for g, w in zip(gaps, fit[1])]
        print(f'  record {n}: {len(obs)} changes over {v} vsyncs, {p:.1f} expected passes -> {v * args.vsync_ms / p:.3f} ms'
              f' (min match {min(o[2] for o in obs):.3f}; gaps {gaps.min()}..{gaps.max()})')
    return out


if __name__ == '__main__':
    main()
