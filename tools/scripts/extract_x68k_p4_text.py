#!/usr/bin/env python3
"""Extract Phantasie IV (X68000) in-game text into a string table with an English column.

Phase 09 T6.1 / T6.2 (docs/plan/03d-phantasie4-re.md §T6.1). Static only.

Sources (Shift-JIS / cp932):
  P4.X  data segment: NUL-terminated strings. A string whose first byte is the target of a relocated pointer is
        `ref_confirmed` [C]; `refs` names the function (text symbol) or data table holding each pointer.
  O_001..O_021  location/event files: bytecode with inline NUL-terminated text, `$` = line break [~].
  SCR1..SCR5    quest scrolls: fixed 35-byte lines (17 full-width characters + NUL) [C].
Text inside pictures (*.PKH) is not covered here (T6.1 "art text", needs the picture decoder).

Translations live in reverse-engineering/x68000/phantasie4/translation/en.json: {"<exact Japanese>": "<English>"}.
They are keyed by the Japanese text, so one entry covers every copy, and re-running this script never loses them.
Where one Japanese fragment needs different English in different places (word order, a line that reads differently in
its own context), the special key "@rows" maps a row id ("P4.X:0x41a06", "SCR1:0x0302") to its own English (s201).
Rows whose text has no Japanese characters get status "source-english" (the binary already has English).

Output: reverse-engineering/x68000/phantasie4/extracted/text/strings.json (+ untranslated.txt work list)
"""
import json
import os
import re
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from x68k_x import XFile  # noqa: E402

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..'))
P4 = os.path.join(ROOT, 'reverse-engineering', 'x68000', 'phantasie4')
UNP = os.path.join(P4, 'unpacked')
OUT = os.path.join(P4, 'extracted', 'text')
TRANS = os.path.join(P4, 'translation', 'en.json')

JP = re.compile(r'[　-ヿ一-鿿！-ﾟ]')
BAD = re.compile(r'[-\x00-\x08\x0b\x0c\x0e-\x1a\x1c-\x1f\x7f]')
KANJI_ONLY = re.compile(r'^[一-鿿]+$')


def decode(seg):
    try:
        return seg.decode('cp932')
    except UnicodeDecodeError:
        return None


def plausible(t, confirmed):
    if t is None or not t.strip() or BAD.search(t):
        return False
    if confirmed:
        return True
    if JP.search(t):
        # short all-kanji runs and lone half-width kana are usually data bytes that happen to decode
        return len(JP.findall(t)) >= 2 and not (len(t) <= 3 and KANJI_ONLY.match(t))
    # ASCII-only: keep words, drop fragments
    return len(t) >= 3 and re.search(r'[A-Za-z]{2}', t) is not None


def segments(blob, start, end):
    i = start
    while i < end:
        j = blob.find(b'\0', i, end)
        if j < 0:
            j = end
        if j > i:
            yield i, blob[i:j]
        i = j + 1


def p4x_rows(x):
    im = x.image
    pm = x.pointer_map()
    data_syms = sorted((s['value'], s['name']) for s in x.symbols if s['kind'] == 'data')

    def where(site):
        if site < x.text:
            return x.func_at(site) or 'text?'
        name = None
        for v, n in data_syms:
            if v <= site:
                name = n
            else:
                break
        return name or 'data?'

    starts = sorted(t for t in pm if x.text <= t < x.text + x.data)
    rows = []
    for off, seg in segments(im, x.text, x.text + x.data):
        # split a segment where a relocated pointer points into its middle
        cuts = [off] + [t for t in starts if off < t < off + len(seg)]
        for k, c in enumerate(cuts):
            e = cuts[k + 1] if k + 1 < len(cuts) else off + len(seg)
            t = decode(im[c:e])
            conf = c in pm
            if not plausible(t, conf):
                continue
            refs = sorted({where(s) for s in pm.get(c, [])})
            rows.append(dict(src='P4.X', off=f'0x{c:05x}', ref_confirmed=conf, refs=refs, ja=t))
    return rows


def o_rows():
    rows = []
    d1 = os.path.join(UNP, 'disk1')
    for fn in sorted(f for f in os.listdir(d1) if re.fullmatch(r'O_\d{3}', f)):
        b = open(os.path.join(d1, fn), 'rb').read()
        for off, seg in segments(b, 0, len(b)):
            t = decode(seg)
            if t and JP.search(t) and plausible(t, False):
                # a leading ASCII byte glued to the text is usually an opcode operand; keep it visible
                m = re.match(r'^([!-~]{1,2})(?=[^\x00-\x7f])', t)
                if m and m.group(1) not in ('A', 'B', 'C', 'D', 'E', 'M', 'N'):
                    off += len(m.group(1))
                    t = t[len(m.group(1)):]
                rows.append(dict(src=fn, off=f'0x{off:04x}', ja=t))
    return rows


def scr_rows():
    rows = []
    d2 = os.path.join(UNP, 'disk2')
    for k in range(1, 6):
        fn = f'SCR{k}'
        b = open(os.path.join(d2, fn), 'rb').read()
        for line in range(len(b) // 35 + 1):
            seg = b[line * 35:(line + 1) * 35].split(b'\0')[0]
            t = decode(seg)
            if t and t.strip():
                rows.append(dict(src=fn, off=f'0x{line * 35:04x}', line=line, ja=t))
    return rows


def main():
    x = XFile(open(os.path.join(UNP, 'disk1', 'P4.X'), 'rb').read())
    rows = p4x_rows(x) + o_rows() + scr_rows()
    trans = json.load(open(TRANS, encoding='utf-8')) if os.path.exists(TRANS) else {}
    by_row = trans.pop('@rows', {})
    stats = {}
    for n, r in enumerate(rows):
        r['id'] = f"{r['src']}:{r['off']}"
        if not JP.search(r['ja']):
            r['en'], r['status'] = r['ja'], 'source-english'
        elif r['ja'] in trans and trans[r['ja']] is None:
            r['en'], r['status'] = '', 'not-text'  # data bytes that decode as Shift-JIS (marked null by hand)
        elif r['id'] in by_row:
            r['en'], r['status'] = by_row[r['id']], 'done'
        elif r['ja'] in trans:
            r['en'], r['status'] = trans[r['ja']], 'done'
        else:
            r['en'], r['status'] = '', 'todo'
        stats[r['status']] = stats.get(r['status'], 0) + 1
    order = ['id', 'src', 'off', 'line', 'ref_confirmed', 'refs', 'status', 'ja', 'en']
    rows = [{k: r[k] for k in order if k in r} for r in rows]
    uniq_todo = sorted({r['ja'] for r in rows if r['status'] == 'todo'}, key=lambda s: [r['ja'] for r in rows].index(s))
    os.makedirs(OUT, exist_ok=True)
    meta = dict(
        source='extract_x68k_p4_text.py over unpacked/disk1/P4.X (data segment), O_001-O_021, disk2/SCR1-SCR5',
        encoding='Shift-JIS (cp932). `$` in O_ files = line break; `%s` / `%d` = printf fields; `\\n` = newline',
        translations='translation/en.json keyed by exact Japanese; notes in docs/manuals/phantasie4/translation-notes.md',
        counts=dict(rows=len(rows), **stats, unique_japanese=len({r['ja'] for r in rows if JP.search(r['ja'])}),
                    unique_todo=len(uniq_todo)))
    with open(os.path.join(OUT, 'strings.json'), 'w', encoding='utf-8') as f:
        json.dump(dict(meta=meta, strings=rows), f, ensure_ascii=False, indent=1)
        f.write('\n')
    with open(os.path.join(OUT, 'untranslated.txt'), 'w', encoding='utf-8') as f:
        for s in uniq_todo:
            f.write(json.dumps(s, ensure_ascii=False) + '\n')
    print(json.dumps(meta['counts']))
    stale_rows = [k for k in by_row if k not in {r['id'] for r in rows}]
    if stale_rows:
        print(f'{len(stale_rows)} @rows ids match no row:', stale_rows[:5])
    unused = [k for k in trans if k not in {r['ja'] for r in rows}]
    if unused:
        print(f'{len(unused)} translation keys match no string (check for typos):', unused[:5])


if __name__ == '__main__':
    main()
