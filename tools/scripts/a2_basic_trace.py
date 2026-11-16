"""Run one of Phantasie's Applesoft programs under the Apple II+ ROM in a 6502 emulator, logging what it does (s224).

    python3 a2_basic_trace.py ROM SHAPES INFO PROGRAM OUT_DIR [--party JSON] [--po N] [--seconds S] [--snap-lines A,B]

Needs py65 (`pip install py65`, outside the repo) and a 12 KiB Apple II+ ROM image ($D000-$FFFF; not in the repo).
The machine boots to Applesoft's prompt (no cards), then is put in the state the game leaves (I's HELLO / INTRO:
TXTTAB $5101, SHAPES at $451F with $E8/$E9 on it, INFO at $302, HPAG page 1), a party poked at SA = 16385 (--party:
a list of {"name": ..., "<byte>": value}; PO at SA + 1203), the program relinked at $5101 and RUN typed. Every GET is
answered at once with a space. The run stops at the first `&` (RDOS: the next program) or after --seconds.
OUT_DIR/log.json: each statement start (line, offset; consecutive repeats once), soft switches $C050-$C057, key waits,
speaker clicks ($C030 reads) and CALL 770 entries / returns, with 6502 cycles (1.020484 MHz) and seconds; snapshots of
hi-res page 1, the text page and memory $4000-$5FFF (snapNNN.*) at every switch, key wait, --snap-lines line start and
the end. py65's DEC abs (3 cycles) is set to the 6502's 6. Used for reverse-engineering/apple2/phantasie1/GRAPHICS.md 8.
"""
import hashlib
import json
import sys

from py65.devices.mpu6502 import MPU
from py65.memory import ObservableMemory

HZ = 1020484.0
MPU.cycletime[0xCE] = 6  # py65 lists DEC abs as 3 cycles; the 6502 takes 6


def main():
    rom, shapes, info, prog, out = sys.argv[1:6]
    opts = sys.argv[6:]
    party = json.loads(opts[opts.index('--party') + 1]) if '--party' in opts else []
    po = int(opts[opts.index('--po') + 1]) if '--po' in opts else len(party) - 1
    snap_lines = set(int(v) for v in opts[opts.index('--snap-lines') + 1].split(',')) if '--snap-lines' in opts else set()
    limit = float(opts[opts.index('--seconds') + 1]) if '--seconds' in opts else 120.0
    mem = ObservableMemory()
    romb = open(rom, 'rb').read()
    for i, b in enumerate(romb):
        mem[0xD000 + i] = b
    log = []
    state = {'key_pending': False, 'waiting': False, 'snap': 0}
    mpu = MPU(memory=mem)

    def now():
        return mpu.processorCycles

    def snap(tag):
        page = bytes(mem[0x2000:0x4000])
        text = bytes(mem[0x400:0x800])
        p2 = bytes(v & 0xFF for v in mem[0x4000:0x6000])
        n = state['snap']
        state['snap'] += 1
        open(f'{out}/snap{n:03d}.page1', 'wb').write(page)
        open(f'{out}/snap{n:03d}.text', 'wb').write(text)
        open(f'{out}/snap{n:03d}.page2', 'wb').write(p2)
        log.append({'t': now() / HZ, 'cyc': now(), 'ev': 'snap', 'n': n, 'tag': tag,
                    'sha1': hashlib.sha1(page).hexdigest()})

    keys = []

    def io_read(addr):
        if addr == 0xC000 and keys:
            return keys[0] | 0x80
        if addr == 0xC010 and keys:
            keys.pop(0)
            if not keys and state.get('typing'):
                state['typing'] = False
                mpu.processorCycles = 0
                log.clear()
                log.append({'t': 0, 'cyc': 0, 'ev': 'run'})
            return 0
        if addr == 0xC000 and not (0xFD1B <= mpu.pc <= 0xFD2E):
            return 0
        if addr == 0xC000:
            if not state['waiting']:
                state['waiting'] = True
                snap('key-wait')
                log.append({'t': now() / HZ, 'cyc': now(), 'ev': 'key-wait', 'pc': mpu.pc})
                state['key_pending'] = True
            return 0xA0 if state['key_pending'] else 0x20
        if addr == 0xC010:
            if state['key_pending'] and state['waiting']:
                state['key_pending'] = False
                state['waiting'] = False
                log.append({'t': now() / HZ, 'cyc': now(), 'ev': 'key-taken'})
            return 0
        if addr == 0xC030:
            log.append({'cyc': now(), 'ev': 'spk'})
            return 0
        if 0xC050 <= addr <= 0xC057:
            log.append({'t': now() / HZ, 'cyc': now(), 'ev': 'sw', 'a': hex(addr)})
            snap('sw-' + hex(addr))
            return 0
        return 0

    def io_write(addr, value):
        io_read(addr)

    mem.subscribe_to_read(range(0xC000, 0xC100), io_read)
    mem.subscribe_to_write(range(0xC000, 0xC100), io_write)
    # ROM is read-only.
    mem.subscribe_to_write(range(0xD000, 0x10000), lambda a, v: romb[a - 0xD000])
    # Slot ROM space reads 0 (no cards).
    mem.subscribe_to_read(range(0xC100, 0xD000), lambda a: 0)

    # Boot: monitor RESET -> no disk card -> Applesoft cold start -> ']' prompt waiting for a key.
    mpu.reset()
    mpu.pc = mem[0xFFFC] | mem[0xFFFD] << 8
    mem[0x3F4] = 0  # power-up byte invalid: cold start
    steps = 0
    while not state['waiting'] and steps < 5_000_000:
        mpu.step()
        steps += 1
    if not state['waiting']:
        raise SystemExit('no prompt')
    log.clear()
    state['snap'] = 0
    # Machine state the game leaves: SHAPES at $451F ($E8/$E9), INFO at $302, program at TXTTAB $5101 (HELLO 10-11).
    for i, b in enumerate(open(shapes, 'rb').read()):
        mem[0x451F + i] = b
    mem[0xE8], mem[0xE9] = 0x1F, 0x45
    mem[0xE6] = 0x20  # HPAG: page 1 (HGR run earlier by the game)
    for i, b in enumerate(open(info, 'rb').read()):
        mem[0x302 + i] = b
    # & -> a trap at $3F5 (RTS stub; the run stops there).
    mem[0x3F5], mem[0x3F6], mem[0x3F7] = 0x4C, 0xF5, 0x03
    sa = 16385
    for pm, rec in enumerate(party):
        base = sa + 170 * pm
        for k, v in rec.items():
            if k == 'name':
                for j, ch in enumerate(v.encode('ascii') + b'\r'):
                    mem[base + 131 + j] = ch
            else:
                mem[base + int(k)] = v
    mem[sa + 1203] = max(0, po)
    data = open(prog, 'rb').read()
    # Relink at $5101.
    base = 0x5101
    mem[0x5100] = 0
    p = 0
    addr = base
    body = bytearray()
    while True:
        nxt = data[p] | data[p + 1] << 8 if p + 4 <= len(data) else 0
        if nxt == 0:
            body += b'\x00\x00'
            break
        q = p + 4
        while data[q] != 0:
            q += 1
        line = data[p + 2:q + 1]
        link = addr + 2 + len(line)
        body += bytes([link & 0xFF, link >> 8]) + line
        addr = link
        p = q + 1
    for i, b in enumerate(body):
        mem[base + i] = b
    end = base + len(body)
    for zp, v in ((0x67, base), (0x69, end), (0x6B, end), (0x6D, end), (0x73, 0x9600), (0x6F, 0x9600)):
        mem[zp], mem[zp + 1] = v & 0xFF, v >> 8
    keys.extend(b'RUN\r')
    state['waiting'] = False
    state['key_pending'] = False
    state['typing'] = True
    mpu.processorCycles = 0
    cyc_limit = limit * HZ
    while True:
        if mpu.pc == 0x3F5:
            log.append({'t': now() / HZ, 'cyc': now(), 'ev': 'ampersand'})
            break
        if mpu.pc == 0xD43C and not state.get('typing'):  # RESTART: back at the prompt (an error)
            log.append({'t': now() / HZ, 'cyc': now(), 'ev': 'prompt-or-error', 'pc': mpu.pc})
            break
        if now() > cyc_limit:
            log.append({'t': now() / HZ, 'cyc': now(), 'ev': 'limit'})
            break
        if mpu.pc == 0x302 and state.get('incall') is None:
            state['incall'] = 1
            log.append({'cyc': now(), 'ev': 'call770', 'x': mpu.x, 'y': mpu.y, 'p': mem[0x300], 'd': mem[0x301]})
        if mpu.pc == 0x316:
            state['incall'] = None
            log.append({'cyc': now(), 'ev': 'ret770'})
        if mpu.pc == 0xD820:
            key = (mem[0x75] | mem[0x76] << 8, mem[0xB8] | mem[0xB9] << 8)
            if key != state.get('last'):
                state['last'] = key
                log.append({'t': now() / HZ, 'cyc': now(), 'ev': 'stmt', 'line': key[0], 'ptr': key[1] - 0x5101})
                if key[0] in snap_lines and key[0] not in state.setdefault('snapped', set()):
                    state['snapped'].add(key[0])
                    snap('line-%d' % key[0])
        mpu.step()
    print('end pc', hex(mpu.pc), 'line', mem[0x75] | mem[0x76] << 8)
    snap('end')
    json.dump(log, open(f'{out}/log.json', 'w'), indent=0)


if __name__ == '__main__':
    main()
