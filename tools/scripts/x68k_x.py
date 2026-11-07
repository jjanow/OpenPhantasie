#!/usr/bin/env python3
"""Parse a Human68k `.X` executable (HU header, relocation table, symbol table).

Phase 09 T6.2 (03d-phantasie4-re.md 3d.2). Image addresses below are offsets into text+data as loaded at base 0
(file offset = image address + 0x40).

Header (big-endian):  0x00 'HU', 0x03 load mode, 0x04 base, 0x08 entry, 0x0c text, 0x10 data, 0x14 bss,
                      0x18 relocation table size, 0x1c symbol table size, (0x20.. debug sizes), 0x40 text.
Relocation table:     16-bit deltas from the previous site (start 0); delta 0x0001 = a 32-bit delta follows.
                      An odd delta marks a 16-bit (word) site (bit 0 cleared to get the delta) `[~]`.
Symbol table:         {u16 type, u32 value, NUL-terminated name, pad to even}; type 0x0201 text, 0x0202 data,
                      0x0203 bss, 0x0003 common (bss), 0x0200/0x0204 absolute / linker.

Usage: x68k_x.py P4.X --json OUT.json        (writes header, symbols, relocation sites)
"""
import argparse
import bisect
import json
import struct
import sys

SYM_KINDS = {0x0201: 'text', 0x0202: 'data', 0x0203: 'bss', 0x0003: 'common', 0x0200: 'abs', 0x0204: 'linker'}


class XFile:
    def __init__(self, blob: bytes):
        if blob[:2] != b'HU':
            raise ValueError('not a Human68k .X file')
        self.blob = blob
        (self.base, self.entry, self.text, self.data, self.bss, self.rsize,
         self.ssize) = struct.unpack('>IIIIIII', blob[4:32])
        self.image = blob[0x40:0x40 + self.text + self.data]
        roff = 0x40 + self.text + self.data
        self.relocs = self._relocs(blob[roff:roff + self.rsize])
        soff = roff + self.rsize
        self.symbols = self._symbols(blob[soff:soff + self.ssize])
        self._text_syms = sorted((s['value'], s['name']) for s in self.symbols if s['kind'] == 'text')
        self._tv = [v for v, _ in self._text_syms]

    @staticmethod
    def _relocs(r):
        sites, pos, i = [], 0, 0
        while i + 2 <= len(r):
            (w,) = struct.unpack('>H', r[i:i + 2])
            i += 2
            if w == 1:
                (w,) = struct.unpack('>I', r[i:i + 4])
                i += 4
            size = 4
            if w & 1:
                w &= ~1
                size = 2
            pos += w
            sites.append((pos, size))
        return sites

    @staticmethod
    def _symbols(s):
        out, i = [], 0
        while i + 6 <= len(s):
            typ, val = struct.unpack('>HI', s[i:i + 6])
            i += 6
            j = s.index(b'\0', i)
            name = s[i:j].decode('latin1')
            i = j + 1
            if i & 1:
                i += 1
            out.append(dict(name=name, value=val, type=f'0x{typ:04x}', kind=SYM_KINDS.get(typ, '?')))
        out.sort(key=lambda x: x['value'])
        return out

    def long(self, a):
        return struct.unpack('>I', self.image[a:a + 4])[0]

    def func_at(self, addr):
        """Name of the text symbol containing addr (None outside text)."""
        if addr >= self.text:
            return None
        k = bisect.bisect_right(self._tv, addr) - 1
        return self._text_syms[k][1] if k >= 0 else None

    def pointer_map(self):
        """{target: [site, ...]} for every 32-bit relocated longword."""
        m = {}
        for site, size in self.relocs:
            if size == 4:
                m.setdefault(self.long(site), []).append(site)
        return m

    def header(self):
        return dict(base=self.base, entry=f'0x{self.entry:x}', text=self.text, data=self.data, bss=self.bss,
                    reloc_bytes=self.rsize, symbol_bytes=self.ssize, reloc_sites=len(self.relocs),
                    data_start=f'0x{self.text:x}', bss_start=f'0x{self.text + self.data:x}')


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('xfile')
    ap.add_argument('--json')
    a = ap.parse_args()
    x = XFile(open(a.xfile, 'rb').read())
    print(json.dumps(x.header()))
    if a.json:
        syms = [dict(s, value=f"0x{s['value']:06x}") for s in x.symbols]
        open(a.json, 'w').write(json.dumps(dict(header=x.header(), symbols=syms), indent=1))


if __name__ == '__main__':
    sys.exit(main())
