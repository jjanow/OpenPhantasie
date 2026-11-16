"""The `apple2_scenes` step lists of Phantasie I Apple II (P1Apple2Scenes.cs) from a2_basic_trace.py runs (s224).

    python3 a2_p1_end_scenes.py CEMETARY_RUN CEMETARY_RUN6 FLASH_RUN

CEMETARY_RUN / FLASH_RUN: runs with a party of four (AARON P(4) 5 P(54) 9 body 19, VIS P(4) 0 body 25, YORL P(54) 1
body 26, CASTER P(54) 4 body 27) and --snap-lines 550,600,665,690,710 / 20,25,40,100,110,122,135,140,160; CEMETARY_RUN6:
six members with P(54) 2 / 3 (an undead path). Each mapped statement becomes a step whose s is the time to the next
mapped one; the dance (560-580) a repeat of its steady passes; the party row, the fates and the flash their own steps.
Writes scenes.json and the compact scenes.defeat.json / scenes.ending.json / scenes.thunder.json (pasted into
P1Apple2Scenes.cs).
"""
import json
import sys

HZ = 1020484.0


def load(d):
    return json.load(open(d + '/log.json'))


def r(t):
    return round(t, 4)


def chrono(log, mapping, hcolor_stmts=None):
    """[(t, op)] for the mapped statements in order, the key waits as 'key' ops (t = when taken)."""
    out = []
    hc = None
    for e in log:
        if e['ev'] == 'stmt':
            k = (e['line'], e['ptr'])
            if hcolor_stmts and k in hcolor_stmts:
                hc = hcolor_stmts[k]
            if k in mapping:
                op = mapping[k]
                op = op(hc) if callable(op) else dict(op)
                if op is not None:
                    out.append([e['t'], op])
        elif e['ev'] == 'key-taken':
            out.append([e['t'], {'op': 'key'}])
    return out


def durations(events, end_t):
    steps = []
    for i, (t, op) in enumerate(events):
        nt = events[i + 1][0] if i + 1 < len(events) else end_t
        op['s'] = r(nt - t)
        steps.append(op)
    return steps


def stmt_times(log, key):
    return [e['t'] for e in log if e['ev'] == 'stmt' and (e['line'], e['ptr']) == key]


def first(log, key):
    return stmt_times(log, key)[0]


def D(shape, x, y, scale, rot, hc):
    return {'op': 'draw', 'shape': shape, 'x': x, 'y': y, 'scale': scale, 'rot': rot, 'hcolor': hc}


def X(shape, x, y, scale, rot):
    return {'op': 'xdraw', 'shape': shape, 'x': x, 'y': y, 'scale': scale, 'rot': rot}


def defeat(c4, c6):
    log = load(c4)
    m = {
        (80, 4): {'op': 'home'}, (80, 6): {'op': 'clear'},
        (410, 197): D(19, 200, 140, 2, 8, 3), (415, 213): D(19, 201, 140, 2, 8, 3),
        (420, 238): D(7, 226, 140, 1, 0, 2), (420, 249): D(7, 227, 140, 1, 0, 2),
        (430, 270): D(19, 240, 0, 2, 0, 3),
        (440, 284): {'op': 'hplot', 'hcolor': 3, 'points': [0, 159, 279, 159]},
        (440, 299): {'op': 'hplot', 'hcolor': 3, 'points': [0, 0, 279, 0]},
        (450, 325): D(19, 50, 100, 1, 32, 3), (450, 336): D(21, 50, 100, 1, 32, 3),
        (460, 355): D(19, 180, 80, 1, 16, 3), (460, 366): D(22, 180, 80, 1, 16, 3),
        (470, 385): D(19, 75, 125, 1, 56, 3), (470, 396): D(20, 75, 125, 1, 56, 3),
        (480, 418): D(19, 100, 170, 2, 48, 3),
        (500, 438): D(19, 180, 100, 2, 36, 3), (500, 450): D(19, 181, 100, 2, 36, 3),
        (510, 475): D(7, 130, 136, 1, 0, 2), (520, 490): D(7, 131, 136, 1, 0, 2),
        (530, 515): D(19, 130, 0, 2, 0, 3),
        (540, 529): {'op': 'vtab', 'row': 24},
        (540, 533): {'op': 'print', 'text': 'apple2.defeat.defeated', 'newline': True},
        (540, 566): {'op': 'print', 'text': 'apple2.defeat.monsters_jump', 'newline': True},
        (550, 603): {'op': 'graphics'},
        (600, 708): {'op': 'clear'}, (600, 715): {'op': 'text'}, (600, 717): {'op': 'home'},
        (610, 723): {'op': 'hplot', 'hcolor': 3, 'points': [0, 159, 100, 100, 179, 100, 279, 159]},
        (620, 758): {'op': 'hplot', 'hcolor': 3, 'points': [100, 100, 100, 10, 179, 10, 179, 100]},
        (650, 859): D(11, 49, 100, 3, 0, 6), (650, 870): D(11, 185, 100, 3, 0, 6),
        (660, 886): D(11, 48, 100, 3, 0, 6), (660, 897): D(11, 184, 100, 3, 0, 6),
        (665, 913): {'op': 'graphics'},
        (667, 927): {'op': 'vtab', 'row': 24},
        (667, 931): {'op': 'print', 'text': 'apple2.defeat.astral', 'newline': True},
        (680, 1000): D(18, 120, 90, 4, 0, 2),
        (690, 1015): {'op': 'home'}, (690, 1017): {'op': 'vtab', 'row': 24},
        (690, 1021): {'op': 'print', 'text': 'apple2.defeat.demon', 'newline': True},
        (1560, 1482): {'op': 'hgr'},
    }
    ev = [x for x in chrono(log, m) if x[1]['op'] != 'key']
    end = [e['t'] for e in log if e['ev'] == 'ampersand'][0]
    # The dance: 70 x (560: XDRAW 6, DRAW 5 at X = 100, 130, 160; 570: XDRAW 5, DRAW 6), from the 2nd pass on.
    body_keys = [(560, 643), (560, 651), (570, 679), (570, 687)]
    seq = [e for e in log if e['ev'] == 'stmt' and (e['line'], e['ptr']) in body_keys]
    assert len(seq) == 70 * 12, len(seq)
    per = [[] for _ in range(12)]
    for it in range(1, 69):
        for j in range(12):
            per[j].append(seq[it * 12 + j + 1]['t'] - seq[it * 12 + j]['t'])
    xs = [100, 130, 160]
    body = []
    for j in range(12):
        half, k, which = j // 6, (j % 6) // 2, j % 2
        x = xs[k]
        if half == 0:
            op = X(6, x, 40, 1, 0) if which == 0 else D(5, x, 50, 1, 0, 3)
        else:
            op = X(5, x, 50, 1, 0) if which == 0 else D(6, x, 40, 1, 0, 3)
        op['s'] = r(sum(per[j]) / len(per[j]))
        body.append(op)
    t_dance = seq[0]['t']
    t_after = first(log, (600, 708))
    # The party row (630-640): figures 19 + 20 per X.
    f19 = stmt_times(log, (640, 823))
    f20 = stmt_times(log, (640, 833))
    each = [r(sum(b - a for a, b in zip(f19, f20)) / len(f19)), r(sum(b - a for a, b in zip(f20, f19[1:])) / (len(f19) - 1))]
    t_row = first(log, (630, 793))
    row_lead = r(f19[0] - t_row)
    row_tail = r(first(log, (650, 859)) - f20[-1])
    party = {'op': 'party', 'xs': [[65, 185], [65, 185], [65, 125, 185], [65, 105, 145, 185],
                                   [65, 95, 125, 155, 185], [65, 89, 113, 137, 161, 185]],
             'y': 159, 'shapes': [19, 20], 'scale': 1, 'rot': 0, 'hcolor': 3, 'lead': row_lead, 'each': each,
             'tail': row_tail}
    # Fates (710-1500): times from the PRINT at 720 to the GET, per path; after the key to the next PRINT.
    keys = [e['t'] for e in log if e['ev'] == 'key-taken']
    t720 = stmt_times(log, (720, 1094))
    waits = [e['t'] for e in log if e['ev'] == 'key-wait']
    paths = []
    for i, t in enumerate(t720):
        nxt = [e for e in log if e['ev'] == 'stmt' and e['t'] > t and e['line'] in (750, 900, 1100)][0]
        paths.append((nxt['line'], r(waits[i] - t), [e for e in log if e['ev'] == 'stmt' and e['t'] > t and e['line'] in (730, 740)]))
    log6 = load(c6)
    t720b = stmt_times(log6, (720, 1094))
    waits6 = [e['t'] for e in log6 if e['ev'] == 'key-wait']
    undead = None
    for i, t in enumerate(t720b):
        nxt = [e for e in log6 if e['ev'] == 'stmt' and e['t'] > t and e['line'] in (750, 900, 1100)][0]
        if nxt['line'] == 1100:
            undead = r(waits6[i] - t)
            break
    resurrected = [p[1] for p in paths if p[0] == 900]
    destroyed = [p[1] for p in paths if p[0] == 750]
    after_key = r(sum(t720[i + 1] - keys[i] for i in range(len(t720) - 1)) / (len(t720) - 1))
    fates = {'op': 'fates', 'lead_text': 'apple2.defeat.you_have_been',
             'destroyed': {'text': 'apple2.defeat.destroyed', 's': min(destroyed)},
             'resurrected': {'text': 'apple2.defeat.resurrected', 's': resurrected[0]},
             'undead': {'text': 'apple2.defeat.undead', 'newline': True, 's': undead},
             'after_key': after_key, 'tail': r(first(log, (1560, 1480)) - keys[-1])}
    t_fates = first(log, (710, 1081))
    t_first720 = t720[0]
    fates['first'] = r(t_first720 - t_fates)
    # Assemble: ops before the dance, the dance, ops up to the row, the row, ops up to the fates, the fates, HGR.
    before = [x for x in ev if x[0] < t_dance]
    mid1 = [x for x in ev if t_after <= x[0] < t_row]
    mid2 = [x for x in ev if t_row < x[0] < t_fates]
    hgr = [x for x in ev if x[0] >= first(log, (1560, 1480))]
    steps = durations(before, t_dance)
    steps.append({'op': 'repeat', 'count': 70, 'steps': body, 's': 0})
    steps += durations(mid1, t_row)
    steps.append(dict(party, s=0))
    steps += durations(mid2, t_fates)
    steps.append(dict(fates, s=0))
    steps += durations(hgr, end)
    return steps, paths


def ending(f4):
    log = load(f4)
    hcs = {(12, 12): 3, (1010, 923): 3, (1010, 932): 0, (1030, 1019): 3, (1030, 1028): 0, (35, 196): 3, (110, 351): 5}
    bolt1 = [100, 0, 130, 15, 70, 40, 120, 60, 73, 80, 115, 90, 76, 110, 110, 122, 100, 140]
    bolt2 = [200, 0, 235, 17, 172, 38, 226, 64, 167, 83, 217, 80, 172, 113, 214, 118, 200, 150]
    m = {
        (10, 4): {'op': 'home'}, (10, 6): {'op': 'hgr'},
        (12, 15): {'op': 'hplot', 'hcolor': 3, 'points': [0, 159, 279, 159]},
        (1000, 854): lambda hc: {'op': 'hplot', 'hcolor': hc, 'points': bolt1},
        (1020, 947): lambda hc: {'op': 'hplot', 'hcolor': hc, 'points': bolt2},
        (1100, 1043): {'op': 'sound', 'sound': 'thunder'},
        (40, 203): {'op': 'party'},
        (100, 292): {'op': 'vtab', 'row': 24},
        (100, 296): {'op': 'print', 'text': 'apple2.ending.olympus', 'newline': True},
        (110, 354): D(9, 101, 80, 4, 0, 5),
        (2100, 1232): {'op': 'home'},
        (2100, 1242): {'op': 'flash'},
        (122, 384): {'op': 'home'}, (122, 386): {'op': 'vtab', 'row': 24},
        (122, 390): {'op': 'print', 'text': 'apple2.ending.zeus_appears', 'pad': 3, 'newline': True},
        (130, 444): {'op': 'home'}, (130, 446): {'op': 'vtab', 'row': 23},
        (130, 450): {'op': 'print', 'text': 'apple2.ending.zeus_1', 'pad': 2, 'newline': True},
        (130, 484): {'op': 'print', 'text': 'apple2.ending.zeus_2', 'pad': 4, 'newline': True},
        (130, 514): {'op': 'print', 'text': 'apple2.ending.zeus_3', 'pad': 2, 'newline': False},
        (160, 675): {'op': 'hgr'},
    }
    end = first(log, (170, 681))
    ev = []
    for t, op in chrono(log, m, hcs):
        if t >= end:
            break
        if op['op'] == 'flash':
            if ev and ev[-1][1]['op'] == 'flash':
                continue
        ev.append([t, op])
    steps = durations(ev, end)
    # The party op: figures (20, then the member's body) at SCALE 2, per GOSUB 2000.
    d20 = stmt_times(log, (2000, 1199))
    db = stmt_times(log, (2000, 1210))
    each = [r(sum(b - a for a, b in zip(d20, db)) / len(d20)), r(sum(b - a for a, b in zip(db, d20[1:])) / (len(d20) - 1))]
    lead = r(d20[0] - first(log, (40, 203)))
    po = len(d20) - 1
    for s in steps:
        if s['op'] == 'party':
            s.update({'xs': [[120]] + [[35 + k * 180 // n for k in range(n + 1)] for n in range(1, 6)], 'y': 159,
                      'shapes': [20, 0], 'scale': 2, 'rot': 0, 'hcolor': 3, 'lead': lead, 'each': each,
                      'tail': r(first(log, (100, 292)) - db[-1])})
            s['s'] = 0
    # Flash timing: the first burst.
    tf = first(log, (2100, 1242))
    sw = [e['t'] for e in log if e['ev'] == 'sw' and e['a'] in ('0xc055', '0xc054') and e['t'] >= tf][:20]
    on = [sw[i + 1] - sw[i] for i in range(0, 20, 2)]
    off = [sw[i + 2] - sw[i + 1] for i in range(0, 18, 2)]
    for s in steps:
        if s['op'] == 'flash':
            s.update({'count': 10, 'first': r(sw[0] - tf), 'page2': r(sum(on) / len(on)), 'page1': r(sum(off) / len(off))})
    # The thunder: CALL 770 entries (x, y, pitch, duration) relative to the GOSUB 1100.
    g = [e['cyc'] for e in log if e['ev'] == 'stmt' and (e['line'], e['ptr']) == (1100, 1043)][0]
    calls = [e for e in log if e['ev'] == 'call770']
    first15 = calls[:15]
    thunder = {'x': first15[0]['x'], 'y': first15[0]['y'],
               'calls': [[round((c['cyc'] - g) / HZ, 5), c['p'], c['d']] for c in first15]}
    return steps, thunder


def main():
    d_steps, paths = defeat(sys.argv[1], sys.argv[2])
    e_steps, thunder = ending(sys.argv[3])
    out = {'defeat': d_steps, 'ending': e_steps, 'thunder': thunder}
    json.dump(out, open('scenes.json', 'w'), indent=1)
    def compact(steps):
        return '[\n' + ',\n'.join('  ' + json.dumps(st, separators=(',', ':')) for st in steps) + '\n]'
    open('scenes.defeat.json', 'w').write(compact(d_steps))
    open('scenes.ending.json', 'w').write(compact(e_steps))
    open('scenes.thunder.json', 'w').write(json.dumps(thunder, separators=(',', ':')))
    print('defeat steps', len(d_steps), 'ending steps', len(e_steps))
    print('paths', [(p[0], p[1]) for p in paths])


if __name__ == '__main__':
    main()
