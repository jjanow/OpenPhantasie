#!/usr/bin/env python3
"""Send host keys to an XEiJ window on a virtual X screen (Xvfb, no window manager), take screenshots.

    xeij_keys.py :61 F12 sleep:12 shot:/path/a.png type:abc Return

Steps run in order: a key name (xdotool syntax, e.g. F12, Return, n, Down), "type:TEXT", "sleep:SECONDS",
"shot:PATH" (the whole virtual screen), "click:X,Y", "gif" (start a GIF recording through XEiJ's Display menu:
synthetic host keys such as F12 do not reach XEiJ's window on Xvfb). Keys for the X68000 itself go through XEiJ's
control pipe instead: echo "typekey n" > WORKDIR/tmp/XEiJControl (xeij_run.sh turns the pipe on). With no window manager the keyboard goes to the window under the pointer, so
the pointer is parked on XEiJ's title bar (never over the emulated screen, where it would move the X68000 mouse).
"""
import os
import subprocess
import sys
import time


def main() -> None:
    env = dict(os.environ, DISPLAY=sys.argv[1])
    found = subprocess.run(['xdotool', 'search', '--onlyvisible', '--name', 'XEiJ'],
                           env=env, capture_output=True, text=True).stdout.split()
    if not found:
        sys.exit('no visible XEiJ window')
    subprocess.run(['xdotool', 'mousemove', '300', '16'], env=env, check=True)
    subprocess.run(['xdotool', 'windowfocus', found[0]], env=env)
    for step in sys.argv[2:]:
        if step.startswith('sleep:'):
            time.sleep(float(step[6:]))
        elif step.startswith('shot:'):
            subprocess.run(['import', '-window', 'root', step[5:]], env=env, check=True)
        elif step.startswith('click:'):
            x, y = step[6:].split(',')
            # move first and hold the button: the X68000 mouse is polled, an instant move + click can be missed
            subprocess.run(['xdotool', 'mousemove', x, y], env=env, check=True)
            time.sleep(0.3)
            subprocess.run(['xdotool', 'mousedown', '1'], env=env, check=True)
            time.sleep(0.2)
            subprocess.run(['xdotool', 'mouseup', '1'], env=env, check=True)
            time.sleep(0.5)
        elif step == 'gif':  # Display menu > Start recording (needs XEiJ at 0,0 in English, as xeij_run.sh starts it)
            subprocess.run(['xdotool', 'mousemove', '112', '44', 'click', '1'], env=env, check=True)
            time.sleep(1)
            subprocess.run(['xdotool', 'mousemove', '153', '518', 'click', '1', 'mousemove', '300', '16'],
                           env=env, check=True)
        elif step.startswith('type:'):
            subprocess.run(['xdotool', 'type', '--delay', '150', step[5:]], env=env, check=True)
        else:
            subprocess.run(['xdotool', 'key', '--delay', '100', step], env=env, check=True)


if __name__ == '__main__':
    main()
