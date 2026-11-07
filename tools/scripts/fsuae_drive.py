#!/usr/bin/env python3
"""Drive FS-UAE (started by fsuae_run.sh with "zoom = 640x400") on an Xvfb display, in Amiga screen pixels.

    fsuae_drive.py :N CMD [ARGS] [CMD [ARGS] ...]

  focus             start metacity (silenced) if none runs on :N and focus the FS-UAE window
  home              push the Amiga pointer into the screen's top-left corner (the Amiga mouse is relative)
  at X Y            home, then move to Amiga screen pixel (X, Y): 2 host px per Amiga px at zoom 640x400
                    (env HX / HY override), in steps of at most 20 host px
  rel DX DY         relative host move (steps of at most 20)
  down B / up B     hold / release mouse button B (1 left, 3 right)
  click [B]         press and release button B (default 1), held HOLD s (env, default 0.15)
  menu X Y X2 Y2    Intuition menu pick: hold the right button over the title (X, Y), move straight to the
                    item (X2, Y2) in Amiga px, wait, release
  key K [SECS]      hold key K (xdotool name) SECS (default HOLD)
  type TEXT         type TEXT one key at a time
  wait SECS         sleep
  shot FILE         window PNG (scaled)
  ashot FILE        exact Amiga pixels: FS-UAE's own screenshot (F12+S), its 640x400 "crop" image halved to
                    320x200 and written to FILE. Needs "screenshots_output_dir" in the config (fsuae_run.sh sets
                    WORKDIR/screenshots, given here by env SHOTS, default the newest file there). The crop starts
                    5 rows above the Amiga screen's row 0 on NTSC I (screen row = FILE row - 5; s128, town picture
                    matched at 99.65 %).

Why (s128, I's Guild menu picks): the old fsuae_input.sh scaled moves by 0.9 for the default zoom; at zoom 640x400 one
Amiga pixel is 2 host px, so with that scale its pointer fell short of the menu items. Moving with the right button held
straight to the item and pausing before the release picks every Guild item. In I's own windows a pick after a requester
(e.g. "NOT IN THE TOWN ... YES") needs two clicks ~0.4 s apart: the first only activates the window again.
"""
import glob
import os
import subprocess
import sys
import time

from PIL import Image


def main(argv: list[str]) -> None:
    disp = argv[0]
    if not disp.startswith(':'):
        sys.exit('first argument: the Xvfb display, e.g. :71')
    if disp == ':0':
        sys.exit('never the desktop display')
    os.environ['DISPLAY'] = disp
    hx = float(os.environ.get('HX', '2'))
    hy = float(os.environ.get('HY', '2'))
    hold = float(os.environ.get('HOLD', '0.15'))

    def x(*a):
        subprocess.run(['xdotool', *map(str, a)], check=True)

    def win() -> str:
        out = subprocess.run(['xdotool', 'search', '--name', 'FS-UAE'], capture_output=True, text=True).stdout.split()
        if not out:
            sys.exit('no FS-UAE window on ' + disp)
        return out[0]

    def rel(dx: int, dy: int, step: int = 20) -> None:
        while dx or dy:
            sx = max(-step, min(step, dx))
            sy = max(-step, min(step, dy))
            x('mousemove_relative', '--', sx, sy)
            dx -= sx
            dy -= sy
            time.sleep(0.03)

    def home() -> None:
        for _ in range(8):
            x('mousemove_relative', '--', -200, -200)
            time.sleep(0.04)
        time.sleep(0.1)

    def at(ax: float, ay: float) -> None:
        home()
        rel(round(ax * hx), round(ay * hy))
        time.sleep(0.15)

    a = list(argv[1:])
    while a:
        c = a.pop(0)
        if c == 'home':
            home()
        elif c == 'at':
            at(float(a.pop(0)), float(a.pop(0)))
        elif c == 'rel':
            rel(int(a.pop(0)), int(a.pop(0)))
        elif c in ('down', 'up'):
            x('mouse' + c, a.pop(0))
        elif c == 'click':
            b = a.pop(0) if a and a[0].isdigit() else '1'
            x('mousedown', b)
            time.sleep(hold)
            x('mouseup', b)
            time.sleep(0.2)
        elif c == 'menu':
            mx, my, ix, iy = (float(a.pop(0)) for _ in range(4))
            at(mx, my)
            x('mousedown', 3)
            time.sleep(0.5)
            rel(round((ix - mx) * hx), round((iy - my) * hy))
            time.sleep(0.5)
            x('mouseup', 3)
            time.sleep(0.3)
        elif c == 'key':
            k = a.pop(0)
            h = float(a.pop(0)) if a and a[0].replace('.', '', 1).isdigit() else hold
            x('keydown', k)
            time.sleep(h)
            x('keyup', k)
            time.sleep(0.2)
        elif c == 'type':
            for ch in a.pop(0):
                k = {' ': 'space', '-': 'minus', '.': 'period'}.get(ch, ch)
                x('keydown', k)
                time.sleep(0.1)
                x('keyup', k)
                time.sleep(0.15)
        elif c == 'wait':
            time.sleep(float(a.pop(0)))
        elif c == 'shot':
            subprocess.run(['import', '-window', win(), a.pop(0)], check=True)
        elif c == 'ashot':
            out = a.pop(0)
            shots = os.environ.get('SHOTS')
            if not shots:
                sys.exit('ashot needs env SHOTS = the screenshots_output_dir of the run')
            before = set(glob.glob(os.path.join(shots, '*-crop-*.png')))
            x('keydown', 'F12')
            time.sleep(0.2)
            x('key', 's')
            time.sleep(0.2)
            x('keyup', 'F12')
            new: set[str] = set()
            for _ in range(40):
                time.sleep(0.2)
                new = set(glob.glob(os.path.join(shots, '*-crop-*.png'))) - before
                if new:
                    break
            if not new:
                sys.exit('no FS-UAE screenshot appeared in ' + shots)
            time.sleep(0.3)
            im = Image.open(max(new, key=os.path.getmtime)).convert('RGB')
            im.resize((im.width // 2, im.height // 2), Image.NEAREST).save(out)
        elif c == 'focus':
            wm = subprocess.run(['xprop', '-root', '_NET_SUPPORTING_WM_CHECK'], capture_output=True, text=True).stdout
            if 'window id' not in wm:  # no window manager on this display yet
                env = dict(os.environ, CANBERRA_DRIVER='null', PULSE_SINK='agents_mute')
                subprocess.Popen(['metacity', '--replace', f'--display={disp}'], env=env,
                                 stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, start_new_session=True)
                time.sleep(2)
            w = win()
            x('windowactivate', w)
            x('windowfocus', '--sync', w)
        else:
            sys.exit('unknown command ' + c)


if __name__ == '__main__':
    main(sys.argv[1:])
