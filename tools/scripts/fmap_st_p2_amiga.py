"""Phase 09 T3, task 3b.6: function map of Phantasie II `PHANT.PRG` (Atari ST) against Phantasie I `game` (Amiga).

Parses both binaries (PRG fixups via st_prg.py; Amiga hunks + relocs), disassembles them with m68k-linux-gnu-objdump,
extracts per-function features (strings, floats, constants, callees), matches functions and diffs the pairs.
Writes reverse-engineering/atari-st/phantasie2/logic-specs/00-function-map.json; `--table` prints the markdown tables
used in 00-function-map.md. Needs binutils-m68k-linux-gnu.
"""
import struct, subprocess, re, sys, os, json, math, tempfile
from collections import defaultdict, Counter
import difflib, bisect
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from st_prg import load_prg

REPO = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..'))
ST_PRG = REPO + '/reverse-engineering/atari-st/phantasie2/unpacked/disk1/PHANT.PRG'
AMIGA = REPO + '/reverse-engineering/amiga/unpacked/PHANT/game'
TMP = tempfile.mkdtemp(prefix='fmap_st_p2_')
import atexit, shutil
atexit.register(shutil.rmtree, TMP, True)

LINE = re.compile(r'^\s*([0-9a-f]+):\t([0-9a-f ]+?)\s*(?:\t(\S+)\s*(.*))?$')


def objdump(code: bytes, vma: int, tag: str):
    fn = os.path.join(TMP, f'_{tag}.bin')
    open(fn, 'wb').write(code)
    out = subprocess.run(['m68k-linux-gnu-objdump', '-D', '-b', 'binary', '-m', 'm68k',
                          f'--adjust-vma={vma:#x}', fn], capture_output=True, text=True).stdout
    ins = []
    for ln in out.splitlines():
        m = LINE.match(ln)
        if not m:
            continue
        a = int(m.group(1), 16)
        hx = m.group(2).replace(' ', '')
        if m.group(3) is None:  # continuation line
            if ins:
                ins[-1][1] += len(hx) // 2
            continue
        ins.append([a, len(hx) // 2, m.group(3), m.group(4) or ''])
    return ins


class Bin:
    """Common view: mem(addr)->bytes, relocs (positions), code ranges, data ranges, symbols."""
    def u32(self, a):
        b = self.read(a, 4)
        return struct.unpack('>I', b)[0] if len(b) == 4 else None

    def cstr(self, a, limit=200):
        b = self.read(a, limit)
        s = b.split(b'\0', 1)[0]
        return s

    def is_data(self, a):
        return any(lo <= a < hi for lo, hi in self.data_ranges)


class ST(Bin):
    name = 'st'
    def __init__(self):
        p = load_prg(ST_PRG)
        self.p = p
        self.img = bytearray(p.image)
        self.relocs = set(p.relocs)
        self.text = (0, p.text_size)
        self.code_ranges = [self.text]
        self.data_ranges = [(p.text_size, p.text_size + p.data_size)]
        self.bss = (p.text_size + p.data_size, p.text_size + p.data_size + p.bss_size)
        self.syms = {0x19076: 'xbios', 0x19086: 'bios', 0x19096: 'gemdos'}

    def read(self, a, n):
        return bytes(self.img[a:a + n])

    def disasm(self):
        return objdump(bytes(self.img[:self.text[1]]), 0, 'st')

    @staticmethod
    def is_os(t):
        return 0x19076 <= t < 0x19d42 or 0x19e7c <= t <= 0x1a5f0 or 0x1a6f8 <= t <= 0x1ac2e

    def fmt(self, a):
        return f'0x{a:05x}'


class Amiga(Bin):
    name = 'amiga'
    def __init__(self):
        d = open(AMIGA, 'rb').read()
        self.raw = d
        p = 0
        def L():
            nonlocal p
            v = struct.unpack_from('>I', d, p)[0]; p += 4; return v
        assert L() == 0x3f3
        while L():
            pass
        L(); f = L(); l = L(); [L() for _ in range(l - f + 1)]
        self.hunks = []; self.syms = {}; self.relocs = set()
        cur = -1; relist = []
        while p < len(d):
            t = L() & 0x3fffffff
            if t in (0x3e9, 0x3ea):
                n = L(); self.hunks.append({'type': 'CODE' if t == 0x3e9 else 'DATA', 'data': bytearray(d[p:p + n * 4]), 'fileoff': p}); p += n * 4
                cur = len(self.hunks) - 1
            elif t == 0x3eb:
                n = L(); self.hunks.append({'type': 'BSS', 'data': bytearray(n * 4), 'fileoff': None}); cur = len(self.hunks) - 1
            elif t == 0x3ec:
                while True:
                    c = L()
                    if not c: break
                    th = L()
                    for _ in range(c):
                        relist.append((cur, L(), th))
            elif t == 0x3f2:
                pass
            elif t == 0x3f0:
                while True:
                    c = L()
                    if not c: break
                    nm = d[p:p + c * 4].rstrip(b'\0').decode(); p += c * 4; v = L()
                    if not nm.startswith('.'):
                        self.syms[(cur << 16) | v] = nm
            else:
                raise ValueError(hex(t))
        for h, off, th in relist:
            data = self.hunks[h]['data']
            v = struct.unpack_from('>I', data, off)[0]
            struct.pack_into('>I', data, off, v + (th << 16))
            self.relocs.add((h << 16) | off)
        self.code_ranges = [(i << 16, (i << 16) + len(x['data'])) for i, x in enumerate(self.hunks) if x['type'] == 'CODE' and x['data']]
        self.data_ranges = [(i << 16, (i << 16) + len(x['data'])) for i, x in enumerate(self.hunks) if x['type'] == 'DATA' and x['data']]

    def read(self, a, n):
        h, o = a >> 16, a & 0xffff
        if h >= len(self.hunks):
            return b''
        return bytes(self.hunks[h]['data'][o:o + n])

    def disasm(self):
        ins = []
        for lo, hi in self.code_ranges:
            ins += objdump(bytes(self.hunks[lo >> 16]['data']), lo, f'am{lo>>16}')
        return ins

    def fmt(self, a):
        return f'h{a>>16}:0x{a & 0xffff:04x}'

    @staticmethod
    def is_os(t):
        return (t >> 16) >= 227 or (t >> 16) in (77, 80)

    def fileoff(self, a):
        h = self.hunks[a >> 16]
        return None if h['fileoff'] is None else h['fileoff'] + (a & 0xffff)


def printable(s):
    return len(s) >= 2 and all(32 <= c < 127 or c in (10, 13) for c in s)


TARGET = re.compile(r'^(?:0x)?([0-9a-f]+)$')


def ffp(v):
    m = (v >> 8) & 0xffffff
    e = v & 0x7f
    s = -1 if v & 0x80 else 1
    if not (m & 0x800000):
        return None
    return s * m / (1 << 24) * 2.0 ** (e - 64)


def features(b: Bin):
    ins = b.disasm()
    addr_idx = {x[0]: i for i, x in enumerate(ins)}
    code_lo = lambda a: any(lo <= a < hi for lo, hi in b.code_ranges)
    starts = set()
    for a, n, mn, op in ins:
        if mn == 'linkw' and op.startswith('%fp,'):
            starts.add(a)
    for a, n, mn, op in ins:
        if mn in ('jsr', 'bsrw', 'bsrs', 'bsrl', 'bsr'):
            m = TARGET.match(op)
            if m:
                t = int(m.group(1), 16)
                if code_lo(t) and t in addr_idx:
                    starts.add(t)
    for lo, hi in b.code_ranges:
        if lo in addr_idx:
            starts.add(lo)
    starts.update(k for k in b.syms if code_lo(k))
    starts = sorted(starts)
    # function bounds: next start or end of code range
    funcs = {}
    ends = {}
    for i, s in enumerate(starts):
        hi = next(h for l, h in b.code_ranges if l <= s < h)
        e = starts[i + 1] if i + 1 < len(starts) and starts[i + 1] < hi else hi
        funcs[s] = {'addr': s, 'end': e, 'calls': [], 'strings': [], 'datarefs': [], 'imm': [], 'floats': [],
                    'traps': [], 'tablestrings': [], 'ninsn': 0, 'os': []}
    fs = sorted(funcs)
    import bisect
    def owner(a):
        i = bisect.bisect_right(fs, a) - 1
        return fs[i] if i >= 0 and funcs[fs[i]]['end'] > a else None
    prev = []
    for k, (a, n, mn, op) in enumerate(ins):
        o = owner(a)
        if o is None:
            continue
        F = funcs[o]
        F['ninsn'] += 1
        F.setdefault('mn', []).append(mn)
        if mn in ('jsr', 'bsrw', 'bsrs', 'bsrl', 'jmp') :
            m = TARGET.match(op)
            if m:
                t = int(m.group(1), 16)
                F['calls'].append(t)
                if b.is_os(t):  # arguments of OS / GEM / Amiga-library calls are platform noise
                    while F.get('imm_at') and F['imm_at'][-1] >= k - 5:
                        F['imm_at'].pop(); F['imm'].pop()
                if b.name == 'st' and t in (0x19076, 0x19086, 0x19096):
                    # find last movew #n,%sp@-
                    for j in range(k - 1, max(k - 8, 0), -1):
                        mm = re.match(r'#(\d+),%sp@-', ins[j][3])
                        if ins[j][2] == 'movew' and mm:
                            F['os'].append((b.syms[t], int(mm.group(1)))); break
        if mn.startswith('trap'):
            F['traps'].append(op)
        # relocated operands
        for pos in range(a, a + n):
            if pos in b.relocs:
                v = b.u32(pos)
                if v is None: continue
                if b.is_data(v):
                    F['datarefs'].append(v)
                    s = b.cstr(v)
                    if printable(s):
                        F['strings'].append(s.decode('latin-1'))
                    elif (v in b.relocs):
                        # pointer table: collect strings it points to
                        q = v; cnt = 0
                        while q in b.relocs and cnt < 200:
                            t = b.u32(q); s2 = b.cstr(t) if t is not None else b''
                            if printable(s2):
                                F['tablestrings'].append(s2.decode('latin-1'))
                                break
                            q += 4; cnt += 1
        # immediates
        for mm in re.finditer(r'#(-?\d+)', op):
            v = int(mm.group(1))
            if mn.startswith(('add', 'sub', 'lea')) and ('%sp' in op or '%fp' in op):
                continue
            if mn.startswith('link'):
                continue
            # skip relocated immediates (addresses)
            if any(pos in b.relocs for pos in range(a, a + n)) and abs(v) > 0x400:
                continue
            F['imm'].append(v)
            F.setdefault('imm_at', []).append(k)
        # floats
        if b.name == 'st':
            for mm in re.finditer(r'#(-?\d+),(%sp@-|%sp@$|%fp@\(-?\d+\)$)', op):
                v = int(mm.group(1)) & 0xffffffff
                if mn == 'movel' and v >> 24 != 0xff and 0x30 <= (v & 0x7f) <= 0x50 and (v & 0x80000000):
                    f = ffp(v)
                    if f is not None: F['floats'].append(round(f, 6))
        else:
            mm = re.match(r'#(-?\d+),%d([0-7])$', op)
            if mn == 'movel' and mm and k + 1 < len(ins):
                hi = int(mm.group(1)) & 0xffffffff
                nx = ins[k + 1]
                m2 = re.match(r'#(-?\d+),%d([0-7])$', nx[3])
                if m2 and int(m2.group(2)) == int(mm.group(2)) + 1 and nx[2] in ('movel', 'moveq') and (hi >> 20) & 0x7ff in range(0x3e0, 0x420):
                    lo = int(m2.group(1)) & 0xffffffff
                    f = struct.unpack('>d', struct.pack('>II', hi, lo))[0]
                    F['floats'].append(round(f, 6))
    return funcs, ins



WORD = re.compile(r"[A-Z0-9%'][A-Z0-9%'.\-]*")
STOP = {'THE', 'AND', 'YOU', 'OK', 'O.K.', 'YES', 'NO', 'SORRY', 'ABORT'}


def toks(strings):
    out = set()
    for s in strings:
        u = s.upper()
        u = re.sub(r'^\[\d\]', ' ', u)
        for w in WORD.findall(u):
            w = w.strip(".'-")
            if len(w) >= 3 and w not in STOP:
                out.add(w)
            elif w.startswith('%') and len(w) >= 2:
                out.add(re.sub(r'[\d.\-]', '', w))  # %-11.9s -> %s
    return out


def load():
    s = ST(); fs, _ = features(s)
    a = Amiga(); fa, _ = features(a)
    return s, fs, a, fa


def st_game(k):
    return 0x13c <= k < 0x19076 or k == 0x19d42  # 0x0-0x13b: DRI crt0 + asm helpers


def am_game(k):
    return (k >> 16) <= 89




def fmt_s(k):
    return f'0x{k:05x}'

DISP = re.compile(r'%a[0-5]@\((-?\d+)')
IGN_IMM = {-1, 0, 1, 2, 3, 4}


def prep(F, is_st):
    for k, f in F.items():
        f['tok'] = toks(f['strings'])
        f['ttok'] = toks(f['tablestrings'])
        f['fl'] = set(round(x, 3) for x in f['floats'] if x not in (0.0, 1.0))
        im = Counter()
        for v in f['imm']:
            if v in IGN_IMM:
                continue
            if v == 318: v = 282
            if v == 7950: v = 7050
            im[v] += 1
        f['im'] = im


def add_disp(b, F):
    ins = b.disasm()
    import bisect
    ks = sorted(F)
    for a_, n, mn, op in ins:
        i = bisect.bisect_right(ks, a_) - 1
        if i < 0: continue
        f = F[ks[i]]
        if a_ >= f['end']: continue
        for mm in DISP.finditer(op):
            f.setdefault('disp', set()).add(int(mm.group(1)))


def jacc_w(A, B, w=None):
    if not A and not B:
        return None
    if w is None:
        return len(A & B) / len(A | B)
    u = sum(w.get(t, 1) for t in A | B)
    return sum(w.get(t, 1) for t in A & B) / u if u else None


def ms_j(A, B):
    if not A and not B:
        return None
    inter = sum((A & B).values()); uni = sum((A | B).values())
    return inter / uni


class Ctx:
    def __init__(self):
        self.s, self.fs, self.a, self.fa = load()
        prep(self.fs, True); prep(self.fa, False)
        add_disp(self.s, self.fs); add_disp(self.a, self.fa)
        df = Counter()
        for F in (self.fs, self.fa):
            for f in F.values():
                for t in f['tok'] | f['ttok']:
                    df[t] += 1
        N = len(self.fs) + len(self.fa)
        self.idf = {t: math.log(N / c) for t, c in df.items()}
        self.gs = sorted(k for k in self.fs if st_game(k))
        self.ga = sorted(k for k in self.fa if am_game(k))
        self.m = {}; self.rm = {}; self.how = {}
        self.cs = {k: self.callees(self.fs, k, st_game) for k in self.fs}
        self.ca = {k: self.callees(self.fa, k, am_game) for k in self.fa}
        self.callers_s = defaultdict(list); self.callers_a = defaultdict(list)
        for k, l in self.cs.items():
            for c in l: self.callers_s[c].append(k)
        for k, l in self.ca.items():
            for c in l: self.callers_a[c].append(k)

    @staticmethod
    def callees(F, k, game):
        seen = []
        for c in F[k]['calls']:
            if c in F and game(c) and c not in seen and c != k:
                seen.append(c)
        return seen

    def sim(self, x, y, detail=False):
        X, Y = self.fs[x], self.fa[y]
        parts = []
        t = jacc_w(X['tok'] | X['ttok'], Y['tok'] | Y['ttok'], self.idf)
        if t is not None: parts.append((3.0, t))
        i = ms_j(X['im'], Y['im'])
        if i is not None: parts.append((2.0, i))
        d = jacc_w(X.get('disp', set()), Y.get('disp', set()))
        if d is not None: parts.append((1.5, d))
        if X['fl'] or Y['fl']:
            parts.append((2.0, jacc_w(X['fl'], Y['fl'])))
        nx, ny = X['ninsn'], Y['ninsn']
        parts.append((1.0, min(nx, ny) / max(nx, ny)))
        cx, cy = len(self.cs[x]), len(self.ca[y])
        parts.append((0.7, (min(cx, cy) + 1) / (max(cx, cy) + 1)))
        # matched callees agreement
        mc = [c for c in self.cs[x] if c in self.m]
        if mc:
            agree = sum(1 for c in mc if self.m[c] in self.ca[y]) / len(mc)
            parts.append((2.0, agree))
        w = sum(p[0] for p in parts)
        v = sum(p[0] * p[1] for p in parts) / w
        return (v, parts) if detail else v

    def add(self, x, y, why):
        if x in self.m or y in self.rm or x in getattr(self, 'blocked', ()):
            return False
        self.m[x] = y; self.rm[y] = x; self.how[x] = why
        return True

    def anchors(self):
        cands = []
        for x in self.gs:
            X = self.fs[x]
            if not (X['tok'] or X['ttok']):
                continue
            for y in self.ga:
                Y = self.fa[y]
                A = X['tok'] | X['ttok']; B = Y['tok'] | Y['ttok']
                sh = A & B
                if not sh: continue
                w = sum(self.idf[t] for t in sh)
                r = w / sum(self.idf[t] for t in A | B)
                nx, ny = X['ninsn'], Y['ninsn']
                sane = 0.33 <= nx / ny <= 3.0 or w * r > 30
                if w >= 4 and r >= 0.2 and sane:
                    cands.append((w * r, x, y))
        for x in self.gs:
            if not self.fs[x]['fl']: continue
            for y in self.ga:
                if not self.fa[y]['fl']: continue
                A, B = self.fs[x]['fl'], self.fa[y]['fl']
                sh = A & B
                if len(sh) >= 2 and len(sh) / len(A | B) >= 0.3:
                    cands.append((3 * len(sh) ** 2 / len(A | B), x, y))
        cands.sort(reverse=True)
        for sc, x, y in cands:
            self.add(x, y, f'strings/floats {sc:.1f}')

    def propagate(self, thr=0.45):
        changed = True; rounds = 0
        while changed and rounds < 30:
            changed = False; rounds += 1
            # 1: callee / caller sets of matched pairs
            for x, y in list(self.m.items()):
                for rel, us, ua in (('callee', self.cs[x], self.ca[y]), ('caller', self.callers_s[x], self.callers_a[y])):
                    us = [c for c in us if c not in self.m]
                    ua = [c for c in ua if c not in self.rm]
                    if not us or not ua: continue
                    pairs = sorted(((self.sim(i, j), i, j) for i in us for j in ua), reverse=True)
                    for v, i, j in pairs:
                        lim = thr if (len(us) > 1 or len(ua) > 1) else thr - 0.1
                        if v >= lim and self.add(i, j, f'{rel} of {fmt_s(x)} (sim {v:.2f})'):
                            changed = True
            # 2: module windows
            anc = [k for k in self.gs if k in self.m]
            for p, q in zip(anc, anc[1:]):
                ya, yb = self.m[p], self.m[q]
                if ya >> 16 != yb >> 16 or not ya < yb: continue
                gs = [k for k in self.gs if p < k < q and k not in self.m]
                ga = [k for k in self.ga if ya < k < yb and k not in self.rm]
                if not gs or not ga: continue
                # DP alignment
                n, mm = len(gs), len(ga)
                S = [[self.sim(gs[i], ga[j]) for j in range(mm)] for i in range(n)]
                D = [[0.0] * (mm + 1) for _ in range(n + 1)]
                B = [[None] * (mm + 1) for _ in range(n + 1)]
                for i in range(1, n + 1):
                    for j in range(1, mm + 1):
                        opts = [(D[i - 1][j], 'u'), (D[i][j - 1], 'l')]
                        if S[i - 1][j - 1] >= thr - 0.05:
                            opts.append((D[i - 1][j - 1] + S[i - 1][j - 1], 'd'))
                        D[i][j], B[i][j] = max(opts)
                i, j = n, mm
                while i > 0 and j > 0:
                    if B[i][j] == 'd':
                        if self.add(gs[i - 1], ga[j - 1], f'module order {fmt_s(p)}..{fmt_s(q)} (sim {S[i-1][j-1]:.2f})'): changed = True
                        i -= 1; j -= 1
                    elif B[i][j] == 'u': i -= 1
                    else: j -= 1


NOISE = {0, 1, 2, 3, 4, 282, 318, 65535, 7050, 7950}


def consts(f):
    """Signature constants: |immediates| plus struct displacements, minus compiler noise (set, not multiset)."""
    v = {abs(k) for k in f['im']} | {abs(k) for k in f.get('disp', set())}
    return {k for k in v if k <= 0xffff} - NOISE  # 32-bit immediates are float bit patterns: compared as 'float'


def diff(c, x, y):
    """Normalised signature diff of a matched pair (see 00-function-map.md, 'How status is decided')."""
    X, Y = c.fs[x], c.fa[y]
    d = {}
    if X['fl'] != Y['fl']:
        d['float'] = (sorted(X['fl'] - Y['fl']), sorted(Y['fl'] - X['fl']))
    t1 = X['tok'] | X['ttok']; t2 = Y['tok'] | Y['ttok']
    if t1 != t2:
        d['text'] = (sorted(t1 - t2)[:12], sorted(t2 - t1)[:12])
    k1, k2 = consts(X), consts(Y)
    if k1 != k2:
        d['const'] = (sorted(k1 - k2)[:12], sorted(k2 - k1)[:12])
    cs = c.cs[x]; ca = c.ca[y]
    bad = [fmt_s(k) for k in cs if c.m.get(k) is not None and c.m[k] not in ca]
    un_s = [fmt_s(k) for k in cs if c.m.get(k) is None]
    un_a = [c.a.fmt(k) for k in ca if k not in c.rm]
    if bad:
        d['calls'] = bad
    if un_s or un_a:
        d['info_unmatched_callees'] = dict(st=un_s, amiga=un_a)
    return d


# ---------------------------------------------------------------------------------------------
# Pairs settled by reading both functions (s76). They override the automatic matcher.
OVERRIDES = {
    0x0a72e: (0x2c0998, 'read: monster-group builder (I: one function; II splits the id check into 0x186de)'),
    0x12f2a: (0x2006d0, 'read: final ending (I: Zeus "WELL DONE, MY SMALL FRIENDS", final.pic; II: Filmon, final2.pic)'),
    0x00974: (0x1703e8, 'read: spell picker titled TEMPLE, called from Learn (both)'),
    0x0cdde: (0x0513b0, 'read: Guild training, same 6-way class switch'),
    0x0c71a: (0x051748, 'read: level-up'),
    0x07610: (0x230000, 'read: combat round'),
    0x0f298: (0x470446, 'read: combat action chooser'),
    0x0fac2: (0x470e88, 'read: swim handler, II adds lava (arg 0)'),
    0x06022: (0x3b0000, 'read: overworld step + terrain switch'),
    0x06ecc: (0x3207c4, 'read: cast spell outside combat'),
    0x03a50: (0x1d10ba, 'read: character sheet'),
    0x07344: (0x020000, 'read: main loop, 13-mode dispatch'),
    0x09bf4: (0x2c0000, 'read: mode-11 handler (encounter start) in both main loops'),
    0x0a290: (0x2c0614, 'read: mode-12 handler (combat victory) in both main loops'),
}
NO_MATCH = {0x186de, 0x00938}
READ_NEW = {0x19d42, 0x186de, 0x00938, 0x137c0, 0x137e6, 0x0fe18}  # II-only functions read in full (s76)  # II-only helpers split out of a matched I function

ST_NAMES = {
    0x00000: 'crt0 _start (DRI)', 0x0006e: 'aes_trap (trap #2, d0=200)', 0x00096: '__BDOS (trap #2 + stack check)',
    0x000c4: 'blkfill (ptr, byte, n)', 0x00352: 'guild_menu', 0x00580: 'bank_menu', 0x005da: 'pick_member',
    0x00938: 'spell_picker_title "TEMPLE"', 0x00974: 'spell_picker (TEMPLE)', 0x00a1a: 'list_available_spells',
    0x00f64: 'score_rank_text (endgame lines)', 0x017e4: 'confirm_cost "IT WILL COST"', 0x01586: 'buy_ninth_item_alert',
    0x01d54: 'armory', 0x02080: 'inn_town', 0x023ee: 'transport_menu', 0x0292a: 'read_scroll',
    0x03192: 'party_line (LEVEL %3d ... origin)', 0x03a50: 'character_sheet (ORIGIN, LUCK, SCORE, TOSS)',
    0x04382: 'load_phant_rsc', 0x04b88: 'load_out_region', 0x04c8c: 'save_out_region', 0x05af2: 'dungeon_map_load',
    0x05d06: 'dungeon_save (DNG.SAV/DNGX.SAV)', 0x06022: 'overworld_step (terrain switch)', 0x06528: 'event_lookup',
    0x067c6: 'use_potion', 0x06ad6: 'heal_member', 0x06cac: 'party_status', 0x06ecc: 'cast_spell_field (DIVINE I/II)',
    0x07344: 'main_loop (13 modes)', 0x07610: 'combat_round (incl. Toss Rock)', 0x08f0a: 'monster_spell_sleep',
    0x092f8: 'monster_breath_special', 0x09188: 'resurrect_member', 0x09794: 'member_takes_damage',
    0x099e0: 'monster_takes_damage', 0x09bf4: 'encounter_start', 0x09dac: 'encounter_reaction',
    0x0a290: 'combat_victory (loot, amulets)', 0x0a72e: 'build_monster_groups', 0x0ad9a: 'pluto_voice (endgame text)',
    0x0ae86: 'find_items', 0x0b2a4: 'show_ranks', 0x0b4e4: 'field_command_loop', 0x0ba9a: 'town_aging',
    0x0c1ea: 'shop_menu', 0x0c71a: 'level_up (TOSS bonus @62)', 0x0c90c: 'create_character (origin @284)',
    0x0cdde: 'guild_train', 0x0d0d2: 'guild_learn_spell', 0x0d442: 'guild_add', 0x0d57c: 'guild_drop',
    0x0d68a: 'guild_purge', 0x0d798: 'score_check (orb/amulets, @282)', 0x0d9a4: 'name_unique_check',
    0x0da82: 'guild_rename', 0x0ded4: 'bank_withdraw', 0x0dfcc: 'use_item (amulets/wand/orb)', 0x0e3f6: 'town_cast_spell',
    0x0e4d4: 'town_heal', 0x0e69e: 'guild_inspect', 0x0efc0: 'combat_orders (FIGHT)', 0x0f0dc: 'combat_member_line',
    0x0f14c: 'pick_rank', 0x0f298: 'combat_choose_action (TOSS)', 0x0f864: 'wilderness_inn',
    0x0fac2: 'lava_or_swim (mode 8 / 10)', 0x0fd40: 'rnd100 = rand()/327', 0x0fe38: 'night_watch_encounter',
    0x10290: 'dungeon_alloc', 0x10320: 'dungeon_load (DNG%d/MESS%d)', 0x1046c: 'wall_message',
    0x10698: 'dungeon_main', 0x1079c: 'dungeon_exit (save map?)', 0x10db0: 'trap_disarm', 0x1119a: 'stairs',
    0x111d6: 'door_lock', 0x116aa: 'dungeon_message', 0x11ab6: 'dungeon_event', 0x12078: 'field_id_map (record word by id, DOS OV2 0x1ba5)',
    0x12678: 'party_death_screen', 0x127fc: 'destroyed', 0x128a2: 'resurrected', 0x128f0: 'granted_undead',
    0x1298e: 'hero_title "%s - the %s from %s"', 0x12bea: 'defeat / astral plane', 0x12f2a: 'endgame_filmon',
    0x13608: 'scrolls_load', 0x13758: 'disk_error_alert', 0x1697c: 'party_hp_mp_window', 0x17536: 'printout',
    0x186de: 'check_monster_id "Wrong monster"', 0x18856: 'debug_alert_bad_row', 0x18b16: 'combat_hit_display (Polish debug alert)',
    0x18e2a: 'alloc_shapes', 0x18eae: 'load_shapes (MSTR*/PARTY*.PAT)', 0x19d42: 'rulebook_quiz',
    0x137c0: 'vbl_install (colour cycling)', 0x137e6: 'vbl_cycle_off (+ data after 0x13862)', 0x151ee: '(data, not code)',
    0x0fe18: 'party_wiped_out (mode 6)', 0x02ab0: '"YOU ARE CHANGED" (sets @302) [~]', 0x0ec36: 'town_main (mode 1)',
    0x136a2: 'printer_line (Cprnout)', 0x13526: 'roster_save', 0x0ed50: 'load_twns_int', 0x0b7e6: 'sound_fx',
    0x0b7fa: 'sound_fx', 0x0b9ce: 'sound_fx', 0x0b92a: 'sound_play',
    # s95
    0x075e2: 'reset_repeat_orders (D-078)',
    0x09894: 'kill_member ("%s DIES", ghost mark)',
    0x095e8: 'party_wide_special (field ids)',
    0x0b16e: 'compact_monsters (state > 1 → 3)',
    0x0bda6: 'town_entry_payout (shares)',
    0x0bf5a: 'start_or_continue (@218 load)',
    0x0c0a8: 'armory_restock',
    0x12a8a: 'defeat_portrait_row',
    0x12b5e: 'defeat_ghost_row',
    0x12b8e: 'defeat_slot (ghost or race portrait)',
    0x16cbe: 'wait_ms (evnt_multi timer; a key ends it)',
    0x188fc: 'portrait_action (0 ghost mark, 1 attack, 2 cast)',
    0x0afb6: 'combat_prep (@246/@248/@250)',
    0x17d9c: 'combat_spell_picker',
}
AM_NAMES = {}


def st_runtime_names(b, F, ins):
    """Name the ST runtime: OS traps, AES / VDI bindings by opcode, libc and FFP helpers."""
    AES = {10: 'appl_init', 19: 'appl_exit', 20: 'evnt_keybd', 21: 'evnt_button', 22: 'evnt_mouse', 23: 'evnt_mesag',
           24: 'evnt_timer', 25: 'evnt_multi', 30: 'menu_bar', 31: 'menu_icheck', 32: 'menu_ienable', 33: 'menu_tnormal',
           42: 'objc_draw', 43: 'objc_find', 44: 'objc_offset', 47: 'objc_change', 50: 'form_do', 51: 'form_dial',
           52: 'form_alert', 54: 'form_center', 77: 'graf_handle', 78: 'graf_mouse', 79: 'graf_mkstate',
           107: 'wind_update', 110: 'rsrc_load', 111: 'rsrc_free', 112: 'rsrc_gaddr'}
    VDI = {6: 'v_pline', 8: 'v_gtext', 9: 'v_fillarea', 11: 'v_gdp (v_bar…)', 12: 'vst_height', 16: 'vsl_width',
           17: 'vsl_color', 22: 'vst_color', 23: 'vsf_interior', 25: 'vsf_color', 32: 'vswr_mode', 39: 'vst_alignment',
           100: 'v_opnvwk', 101: 'v_clsvwk', 102: 'vq_extnd', 104: 'vsf_perimeter', 106: 'vst_effects',
           109: 'vro_cpyfm', 112: 'vsf_udpat', 114: 'vr_recfl', 116: 'vqt_extent', 121: 'vrt_cpyfm',
           122: 'v_show_c', 129: 'vs_clip'}
    names = {0x19076: 'xbios (trap #14)', 0x19086: 'bios (trap #13)', 0x19096: 'gemdos (trap #1)',
             0x1a5f0: 'vdi_trap (trap #2, d0=115)', 0x1a6f8: 'aes_call (crys_if)', 0x1a606: 'lmul (32-bit multiply)',
             0x1a65a: 'ldiv (32-bit divide) [~]', 0x1ad22: 'rand (0..32767) [~]', 0x1ac48: '_main (DRI, CON: setup)',
             0x1b7cc: '_main argv parser (C runtime)', 0x1bf3c: 'sprintf', 0x1bf94: '_doprt (printf engine)',
             0x1db48: 'fpadd', 0x1db68: 'fpcmp', 0x1db86: 'fpdiv', 0x1dba6: 'fppow', 0x1dbc6: 'fpltof (long → FFP)',
             0x1dc56: 'fpftol (FFP → long)', 0x1dcde: 'fpmul', 0x1dcfe: 'fpneg', 0x1dd1a: 'fpsub',
             0x1dd3a: 'ffppwr (asm)', 0x1dd86: 'ffpadd (asm)', 0x1de58: 'ffpcmp (asm)', 0x1de88: 'ffpdiv (asm)',
             0x1df20: 'ffpexp (asm)', 0x1dffa: 'ffplog (asm)', 0x1e0ca: 'ffpmul (asm)'}
    ks = sorted(F)
    for i, (a, n, mn, op) in enumerate(ins):
        k = ks[bisect.bisect_right(ks, a) - 1]
        if mn == 'jsr' and op == '0x1a6f8':
            for j in range(i - 1, i - 4, -1):
                mm = re.match(r'#(\d+),%sp@$', ins[j][3])
                if mm:
                    v = int(mm.group(1)); names.setdefault(k, f'{AES.get(v, "aes")} (AES {v})'); break
        mm = re.match(r'#(\d+),0x25f3e$', op)
        if mn == 'movew' and mm and k >= 0x19e7c:
            v = int(mm.group(1)); names.setdefault(k, f'{VDI.get(v, "vdi")} (VDI {v})')
    return names


def auto_name(f):
    for s in f['strings'] + f['tablestrings']:
        t = re.sub(r'^\[\d\]\[', '', s).replace('|', ' ').strip()
        t = re.sub(r'\s+', ' ', t)
        if re.search(r'[A-Za-z]{3}', t):
            return 'str: "' + t[:34] + ('…' if len(t) > 34 else '') + '"'
    return ''


def platform_hint(f, is_st):
    if is_st:
        aes = [c for c in f['calls'] if 0x190a6 <= c < 0x19d42 or 0x19e7c <= c < 0x1a606 or 0x1a6f8 <= c <= 0x1ac2e]
        return 'GEM/VDI UI' if aes else ''
    return ''


def main():
    out_dir = os.path.join(REPO, 'reverse-engineering', 'atari-st', 'phantasie2', 'logic-specs')
    c = Ctx()
    for x, (y, why) in OVERRIDES.items():
        c.m[x] = y; c.rm[y] = x; c.how[x] = why
    c.blocked = NO_MATCH
    c.anchors()
    c.propagate()
    ins_s = c.s.disasm()
    names = dict(st_runtime_names(c.s, c.fs, ins_s)); names.update(ST_NAMES)
    rows = []
    for x in sorted(c.fs):
        f = c.fs[x]
        game = st_game(x)
        y = c.m.get(x)
        d = diff(c, x, y) if y is not None else None
        if not game:
            status = 'runtime' if y is None else 'runtime (matched)'
        elif y is None:
            status = 'new'
        elif set(d or {}) - {'info_unmatched_callees'}:
            status = 'changed'
        else:
            status = 'same'
        rows.append(dict(st=x, st_end=f['end'], ninsn=f['ninsn'], name=names.get(x) or auto_name(f), gem=bool(platform_hint(f, True)),
                         amiga=c.a.fmt(y) if y is not None else None, amiga_ninsn=c.fa[y]['ninsn'] if y is not None else None,
                         status=status, how=c.how.get(x, ''), diff={k: str(v) for k, v in (d or {}).items()},
                         sim=round(c.sim(x, y), 2) if y is not None else None,
                         os=sorted(set(f'{a}:{n}' for a, n in f['os'])),
                         strings=f['strings'][:4], callees=[fmt_s(k) for k in c.cs[x]][:20]))
    removed = []
    for y in c.ga:
        if y not in c.rm:
            removed.append(dict(amiga=c.a.fmt(y), ninsn=c.fa[y]['ninsn'], name=auto_name(c.fa[y]),
                                calls_amiga_lib=sorted({c.a.syms[t] for t in c.fa[y]['calls'] if t in c.a.syms})[:6]))
    json.dump(dict(st_game_functions=len(c.gs), amiga_game_functions=len(c.ga), rows=rows, amiga_unmatched=removed),
              open(os.path.join(out_dir, '00-function-map.json'), 'w'), indent=1)
    cnt = Counter(r['status'] for r in rows)
    print('ST functions', len(rows), dict(cnt), 'amiga game', len(c.ga), 'amiga unmatched', len(removed))


def evidence(r):
    how = r['how']
    if how.startswith('strings/floats'):
        how = 'strings ' + ', '.join(f'"{x[:28]}"' for x in r['strings'][:2]) if r['strings'] else 'floats'
    how = re.sub(r' \(sim [0-9.]+\)', '', how)
    parts = [how] if how else []
    if r.get('gem'):
        parts.append('calls GEM/VDI')
    if r['sim'] is not None:
        parts.append(f"sim {r['sim']:.2f}")
    d = {k: v for k, v in r['diff'].items() if k != 'info_unmatched_callees'}
    if d:
        parts.append('Δ ' + '; '.join(f'{k} {v}' for k, v in d.items()))
    elif r['status'] == 'new' and r['strings']:
        parts.append('strings ' + ', '.join(f'"{x[:30]}"' for x in r['strings'][:2]))
    return ' · '.join(parts).replace('|', '/')


def table():
    """Print the markdown table rows of the game functions (00-function-map.md §8)."""
    out_dir = os.path.join(REPO, 'reverse-engineering', 'atari-st', 'phantasie2', 'logic-specs')
    d = json.load(open(os.path.join(out_dir, '00-function-map.json')))
    print('| II `PHANT.PRG` | Name | Amiga I `game` | Status | Evidence |')
    print('|---|---|---|---|---|')
    for r in d['rows']:
        if r['status'].startswith('runtime'):
            continue
        tag = '[C]' if r['how'].startswith('read:') else ('[~]' if r['status'] != 'new' and (r['sim'] or 0) >= 0.6 or r['how'].startswith('strings') else '[?]')
        if r['status'] == 'new':
            tag = '[C]' if r['st'] in READ_NEW else '[~]'
        am = f"`{r['amiga']}`" if r['amiga'] else '—'
        name = r['name'].replace('|', '/')
        print(f"| `0x{r['st']:05x}` | {name} | {am} | {r['status']} {tag} | {evidence(r)[:230]} |")
    print()
    print('Amiga I functions without a II partner:')
    print()
    print('| Amiga I `game` | Size (insns) | Name / strings |')
    print('|---|---|---|')
    for r in d['amiga_unmatched']:
        print(f"| `{r['amiga']}` | {r['ninsn']} | {r['name'].replace('|', '/')} |")


if __name__ == '__main__':
    table() if '--table' in sys.argv else main()
