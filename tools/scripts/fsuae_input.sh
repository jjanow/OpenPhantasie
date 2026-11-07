#!/usr/bin/env bash
# Drive an FS-UAE window started by fsuae_run.sh on Xvfb (never the user's desktop).
#
#   fsuae_input.sh DISPLAY_NUM CMD [ARGS] [CMD [ARGS] ...]
#
#   focus           start metacity if none runs and focus the FS-UAE window (needed once: SDL ignores XSendEvent,
#                   so keys and the mouse must go to the focused window as real XTEST events)
#   home            push the Amiga pointer into the top-left corner (the Amiga mouse is relative)
#   click X Y       put the pointer's tip on window pixel (X, Y) of the 960 × 600 window and click the left button
#                   (homes first; after homing the tip sits at window (126, 15) = the screen's top-left, and 1 host px
#                   moves it about 1 window px; check with a shot)
#   rclick X Y      the same with the right button
#   move X Y        put the pointer's tip on window pixel (X, Y) without clicking
#   menu X Y X2 Y2  Amiga menu pick: hold the right button at (X, Y) on the title bar, move to (X2, Y2), release
#   key KEY         press and hold KEY for 150 ms (xdotool key names: Return, space, Escape, a, F1 …)
#   type TEXT       type TEXT one key at a time
#   wait SECONDS    sleep
#   shot FILE       PNG of the FS-UAE window
set -euo pipefail
export DISPLAY=":$1"; shift
win() { xdotool search --name "FS-UAE" | head -1; }
while (($#)); do
  cmd=$1; shift
  case $cmd in
    focus)
      pgrep -f "metacity.*" >/dev/null || { CANBERRA_DRIVER=null PULSE_SINK=agents_mute metacity --replace >/dev/null 2>&1 & sleep 2; }
      w=$(win); xdotool windowactivate "$w" 2>/dev/null || true; xdotool windowfocus --sync "$w"
      xdotool mousemove --window "$w" 480 300 ;;
    home) for _ in 1 2 3 4 5 6; do xdotool mousemove_relative -- -200 -200; sleep 0.05; done ;;
    click|rclick)
      x=$((($1 - 126) * 9 / 10)) y=$((($2 - 15) * 9 / 10)); shift 2; ((x < 0)) && x=0; ((y < 0)) && y=0
      for _ in 1 2 3 4 5 6; do xdotool mousemove_relative -- -200 -200; sleep 0.05; done
      while ((x > 0 || y > 0)); do
        dx=$((x > 50 ? 50 : x)); dy=$((y > 50 ? 50 : y)); x=$((x - dx)); y=$((y - dy))
        xdotool mousemove_relative -- "$dx" "$dy"; sleep 0.04
      done
      sleep 0.2
      b=1; [[ $cmd == rclick ]] && b=3
      xdotool mousedown "$b"; sleep 0.12; xdotool mouseup "$b"; sleep 0.3 ;;
    move|menu)
      if [[ $cmd == menu ]]; then mx=$3 my=$4 ox=$1 oy=$2; fi
      x=$((($1 - 126) * 9 / 10)) y=$((($2 - 15) * 9 / 10)); shift 2; ((x < 0)) && x=0; ((y < 0)) && y=0
      for _ in 1 2 3 4 5 6; do xdotool mousemove_relative -- -200 -200; sleep 0.05; done
      while ((x > 0 || y > 0)); do
        dx=$((x > 50 ? 50 : x)); dy=$((y > 50 ? 50 : y)); x=$((x - dx)); y=$((y - dy))
        xdotool mousemove_relative -- "$dx" "$dy"; sleep 0.04
      done
      if [[ $cmd == menu ]]; then
        shift 2; sleep 0.2; xdotool mousedown 3; sleep 0.4
        dx=$(((mx - ox) * 9 / 10)) dy=$(((my - oy) * 9 / 10))
        n=20; for ((i = 1; i <= n; i++)); do
          xdotool mousemove_relative -- $((dx * i / n - dx * (i - 1) / n)) $((dy * i / n - dy * (i - 1) / n)); sleep 0.04
        done
        sleep 0.4; xdotool mouseup 3; sleep 0.4
      fi ;;
    key) xdotool keydown "$1"; sleep 0.15; xdotool keyup "$1"; sleep 0.3; shift ;;
    type)
      t=$1; shift
      for ((i = 0; i < ${#t}; i++)); do
        c=${t:i:1}; [[ $c == " " ]] && c=space
        xdotool keydown "$c"; sleep 0.1; xdotool keyup "$c"; sleep 0.15
      done ;;
    wait) sleep "$1"; shift ;;
    shot) import -window "$(win)" "$1"; shift ;;
    *) echo "unknown command $cmd" >&2; exit 2 ;;
  esac
done
