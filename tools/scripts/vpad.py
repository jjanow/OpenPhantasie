#!/usr/bin/env python3
"""Virtual Xbox 360 pad for testing the Client's pad input on Xvfb (s181; the user has no pad).
Usage: python3 vpad.py <fifo>  (needs /dev/uinput, writable here; the device is system-wide, so quit it when done).
Send commands with `echo "press A" > <fifo>`. Reads commands from the FIFO, one per line:
  press BTN [hold_s]     BTN: A B X Y LB RB BACK START GUIDE LS RS
  dpad up|down|left|right [hold_s]
  trig LT|RT value(0-255) [hold_s]   (value then back to 0)
  trighold LT|RT value   (stays)
  axes AX1 v1 AX2 v2 [hold_s]   two axes in one report (a stick diagonal at once), then both back to 0
  axis LX|LY|RX|RY value [hold_s]  (then back to 0)
  quit
"""
import os, sys, time
from evdev import UInput, ecodes as e, AbsInfo
fifo = sys.argv[1]
caps = {
  e.EV_KEY: [e.BTN_A, e.BTN_B, e.BTN_X, e.BTN_Y, e.BTN_TL, e.BTN_TR, e.BTN_SELECT, e.BTN_START, e.BTN_MODE, e.BTN_THUMBL, e.BTN_THUMBR],
  e.EV_ABS: [
    (e.ABS_X, AbsInfo(0, -32768, 32767, 16, 128, 0)),
    (e.ABS_Y, AbsInfo(0, -32768, 32767, 16, 128, 0)),
    (e.ABS_Z, AbsInfo(0, 0, 255, 0, 0, 0)),
    (e.ABS_RX, AbsInfo(0, -32768, 32767, 16, 128, 0)),
    (e.ABS_RY, AbsInfo(0, -32768, 32767, 16, 128, 0)),
    (e.ABS_RZ, AbsInfo(0, 0, 255, 0, 0, 0)),
    (e.ABS_HAT0X, AbsInfo(0, -1, 1, 0, 0, 0)),
    (e.ABS_HAT0Y, AbsInfo(0, -1, 1, 0, 0, 0)),
  ]}
ui = UInput(caps, name='Microsoft X-Box 360 pad', vendor=0x045e, product=0x028e, version=0x0110, bustype=e.BUS_USB)
print('device', ui.device.path, flush=True)
B = dict(A=e.BTN_A, B=e.BTN_B, X=e.BTN_X, Y=e.BTN_Y, LB=e.BTN_TL, RB=e.BTN_TR, BACK=e.BTN_SELECT, START=e.BTN_START, GUIDE=e.BTN_MODE, LS=e.BTN_THUMBL, RS=e.BTN_THUMBR)
AX = dict(LX=e.ABS_X, LY=e.ABS_Y, RX=e.ABS_RX, RY=e.ABS_RY, LT=e.ABS_Z, RT=e.ABS_RZ)
def w(t, c, v): ui.write(t, c, v); ui.syn()
if not os.path.exists(fifo): os.mkfifo(fifo)
while True:
  with open(fifo) as f:
    for line in f:
      p = line.split()
      if not p: continue
      cmd = p[0]; hold = 0.15
      try:
        if cmd == 'quit': ui.close(); sys.exit(0)
        if cmd == 'press':
          if len(p) > 2: hold = float(p[2])
          w(e.EV_KEY, B[p[1]], 1); time.sleep(hold); w(e.EV_KEY, B[p[1]], 0)
        elif cmd == 'dpad':
          if len(p) > 2: hold = float(p[2])
          code, v = {'up':(e.ABS_HAT0Y,-1),'down':(e.ABS_HAT0Y,1),'left':(e.ABS_HAT0X,-1),'right':(e.ABS_HAT0X,1)}[p[1]]
          w(e.EV_ABS, code, v); time.sleep(hold); w(e.EV_ABS, code, 0)
        elif cmd in ('trig', 'axis'):
          if len(p) > 3: hold = float(p[3])
          w(e.EV_ABS, AX[p[1]], int(p[2])); time.sleep(hold); w(e.EV_ABS, AX[p[1]], 0)
        elif cmd == 'axes':
          if len(p) > 5: hold = float(p[5])
          a1, a2 = AX[p[1]], AX[p[3]]
          ui.write(e.EV_ABS, a1, int(p[2])); ui.write(e.EV_ABS, a2, int(p[4])); ui.syn(); time.sleep(hold)
          ui.write(e.EV_ABS, a1, 0); ui.write(e.EV_ABS, a2, 0); ui.syn()
        elif cmd in ('trighold', 'axishold'):
          w(e.EV_ABS, AX[p[1]], int(p[2]))
        elif cmd == 'down': w(e.EV_KEY, B[p[1]], 1)
        elif cmd == 'up': w(e.EV_KEY, B[p[1]], 0)
        time.sleep(0.12)
        print('ok', line.strip(), flush=True)
      except Exception as ex:
        print('err', line.strip(), ex, flush=True)
