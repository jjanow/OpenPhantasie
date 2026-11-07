#!/usr/bin/env python3
"""Answer FS-UAE debugger stops on Phantasie III's sound start 0xb126 (s138; RE-REFERENCE.md, FS-UAE section).

    fsuae_bp_watch.py WORKDIR CODE_BASE_HEX [LABEL_FLAT_HEX ...]

WORKDIR holds fsuae.log (FS-UAE's stdout, the console debugger's output) and dbg.in (the FIFO FS-UAE reads as stdin;
`fsuae_run.sh` with DEBUG_FIFO makes it); set the breakpoints first (`f <CODE_BASE + 0xb126>`, `f <CODE_BASE + label>`
for each label). At a stop on 0xb126 it sends `m <A7> 1`, writes "<time> sound <n> caller flat <jsr address>" to
WORKDIR/sounds.txt and sends `g`. s140: a stop on a LABEL address (flat, e.g. 0x187ce for the party's Fireflash) writes
"<time> at flat <label>" and sends `g`, so a sound played through a shared routine (39 from `0x18610`, the dungeon
loop's event-word play) can be told apart by the label hit before it. Times are host seconds with milliseconds. A stop
anywhere else (F12+D) is left alone. Runs until WORKDIR/stop or WORKDIR/bpstop exists.
"""
import os
import re
import sys
import time

W = sys.argv[1]
BASE = int(sys.argv[2], 16)
BP = BASE + 0xb126
LABELS = {BASE + int(a, 16): int(a, 16) for a in sys.argv[3:]}
log = os.path.join(W, 'fsuae.log')
fifo = os.path.join(W, 'dbg.in')
out = open(os.path.join(W, 'sounds.txt'), 'a', buffering=1)
PC = re.compile(r'\n([0-9A-F]{8}) [0-9a-f]{4} ')


def send(cmd):
    with open(fifo, 'w') as f:
        f.write(cmd + '\n')


def stamp():
    t = time.time()
    return time.strftime('%H:%M:%S', time.localtime(t)) + '.%03d' % int(t % 1 * 1000)


pos = os.path.getsize(log)
buf = ''
state = 'run'
while not (os.path.exists(os.path.join(W, 'stop')) or os.path.exists(os.path.join(W, 'bpstop'))):
    time.sleep(0.05)
    with open(log, 'rb') as f:
        f.seek(pos)
        chunk = f.read().decode('latin-1')
        pos += len(chunk.encode('latin-1'))
    buf += chunk
    if state == 'run' and buf.rstrip().endswith('>'):
        pcs = PC.findall(buf)
        pc = int(pcs[-1], 16) if pcs else None
        if pc == BP:
            regs = re.findall(r'A7 ([0-9A-F]{8})', buf)
            sp = regs[-1]
            buf = ''
            state = 'mem'
            send('m %s 1' % sp)
        elif pc in LABELS:
            out.write('%s at flat %#x\n' % (stamp(), LABELS[pc]))
            buf = ''
            send('g')
    elif state == 'mem':
        m = re.search(r'([0-9A-F]{8}) ([0-9A-F]{4}) ([0-9A-F]{4}) ([0-9A-F]{4}) ', buf)
        if m and buf.rstrip().endswith('>'):
            ret = int(m.group(2) + m.group(3), 16)
            n = int(m.group(4), 16)
            line = '%s sound %d caller flat %#x' % (stamp(), n, ret - BASE - 6)
            out.write(line + '\n')
            buf = ''
            state = 'run'
            send('g')
    if len(buf) > 200000:
        buf = buf[-20000:]
