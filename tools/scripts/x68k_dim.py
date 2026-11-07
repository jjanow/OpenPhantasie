#!/usr/bin/env python3
"""Read Sharp X68000 .DIM floppy images (DIFC header) holding a Human68k FAT12 file system.

Phase 09 T6.2 (03d-phantasie4-re.md 3d.1). Static, read-only on the master images.

.DIM layout (as used by DIFC.X / XM6 / the TOSEC X68000 set):
  0x000      media type (0 = 2HD, 77 cyl x 2 heads x 8 sectors x 1024 B)
  0x001-0x0AA one flag byte per track (1 = present)
  0x0AB      "DIFC HEADER  " signature
  0x100      track data, sector order, unused tracks omitted (flag 0)

Human68k FAT12: boot sector = "Hudson soft 2.00" + a big-endian BPB (unlike MS-DOS's little-endian one);
the FAT itself and the directory entries use the MS-DOS (little-endian) layout, except that bytes 12-21 of
an entry hold up to 10 more name characters (names up to 18+3).

Usage:
  x68k_dim.py IMAGE.dim --list
  x68k_dim.py IMAGE.dim --extract OUTDIR [--raw RAW.img] [--undelete DIR]
"""
import argparse
import datetime
import hashlib
import json
import os
import struct
import sys

SECTOR = 1024
SECTORS_PER_TRACK = 8
TRACKS = 154  # 77 cylinders x 2 heads


def dim_to_raw(data: bytes) -> bytes:
    if data[0xAB:0xB6] != b"DIFC HEADER":
        raise ValueError("not a DIFC .dim image")
    if data[0] != 0:
        raise ValueError(f"unsupported .dim media type {data[0]}")
    track_size = SECTOR * SECTORS_PER_TRACK
    out = bytearray()
    pos = 0x100
    for t in range(TRACKS):
        if data[1 + t]:
            out += data[pos:pos + track_size]
            pos += track_size
        else:
            out += bytes(track_size)
    return bytes(out)


class Human68kFat:
    def __init__(self, raw: bytes):
        self.raw = raw
        boot = raw[:SECTOR]
        self.oem = boot[2:18].decode("ascii", "replace")
        (self.bps, self.spc, self.nfats, self.reserved, self.root_entries, self.total,
         self.media, self.spf) = struct.unpack(">HBBHHHBB", boot[0x12:0x1E])
        self.fat_start = self.reserved
        self.root_start = self.fat_start + self.nfats * self.spf
        self.root_sectors = self.root_entries * 32 // self.bps
        self.data_start = self.root_start + self.root_sectors
        self.fat = raw[self.fat_start * self.bps:(self.fat_start + self.spf) * self.bps]

    def bpb(self):
        return dict(oem=self.oem, bytes_per_sector=self.bps, sectors_per_cluster=self.spc, fats=self.nfats,
                    reserved_sectors=self.reserved, root_entries=self.root_entries, total_sectors=self.total,
                    media=f"0x{self.media:02x}", sectors_per_fat=self.spf, data_start_sector=self.data_start)

    def fat_entry(self, n):
        off = n * 3 // 2
        v = self.fat[off] | (self.fat[off + 1] << 8)
        return (v >> 4) if n & 1 else (v & 0xFFF)

    def chain(self, first):
        seen, c = [], first
        while 2 <= c < 0xFF0:
            if c in seen:
                raise ValueError("FAT loop")
            seen.append(c)
            c = self.fat_entry(c)
        return seen

    def cluster_bytes(self, c):
        s = self.data_start + (c - 2) * self.spc
        return self.raw[s * self.bps:(s + self.spc) * self.bps]

    def read_chain(self, first, size=None):
        b = b"".join(self.cluster_bytes(c) for c in self.chain(first))
        return b if size is None else b[:size]

    @staticmethod
    def parse_entries(buf):
        for i in range(0, len(buf), 32):
            e = buf[i:i + 32]
            if e[0] == 0:
                break
            if e[0] == 0xE5:
                continue
            name = (e[0:8] + e[12:22]).rstrip(b"\x00").rstrip(b" ")
            ext = e[8:11].rstrip(b" ")
            attr = e[11]
            tm, dt, clus, size = struct.unpack("<HHHI", e[22:32])
            yield dict(name=name, ext=ext, attr=attr, time=tm, date=dt, cluster=clus, size=size)

    def walk(self, entries=None, path=""):
        if entries is None:
            root = self.raw[self.root_start * self.bps:(self.root_start + self.root_sectors) * self.bps]
            entries = list(self.parse_entries(root))
        for e in entries:
            nm = e["name"].decode("shift_jis", "replace")
            if e["ext"]:
                nm += "." + e["ext"].decode("shift_jis", "replace")
            if nm in (".", ".."):
                continue
            if e["attr"] & 0x08:  # volume label
                yield dict(e, path=nm, kind="volume")
                continue
            full = f"{path}/{nm}" if path else nm
            if e["attr"] & 0x10:
                yield dict(e, path=full, kind="dir")
                sub = list(self.parse_entries(self.read_chain(e["cluster"])))
                yield from self.walk(sub, full)
            else:
                yield dict(e, path=full, kind="file")

    def used_clusters(self):
        n = (self.total - self.data_start) // self.spc + 2
        return sum(1 for c in range(2, n) if self.fat_entry(c) != 0)


def deleted_entries(fs):
    """Deleted root entries (first byte 0xE5) with their data, assuming contiguous clusters (the FAT chain is gone).

    Returns dicts with name (first character unknown, shown as '?'), size, first cluster, cluster list, whether all
    those clusters are free in FAT 1, and the clusters shared with another deleted entry (= overwritten, unreliable).
    """
    root = fs.raw[fs.root_start * fs.bps:(fs.root_start + fs.root_sectors) * fs.bps]
    out = []
    for i in range(0, len(root), 32):
        e = root[i:i + 32]
        if e[0] == 0:
            break
        if e[0] != 0xE5:
            continue
        nm = ('?' + (e[1:8] + e[12:22]).rstrip(b'\0').rstrip(b' ').decode('shift_jis', 'replace'))
        ext = e[8:11].rstrip(b' ').decode('shift_jis', 'replace')
        tm, dt, clus, size = struct.unpack('<HHHI', e[22:32])
        n = -(-size // (fs.bps * fs.spc))
        cl = list(range(clus, clus + n)) if clus >= 2 else []
        out.append(dict(name=nm + ('.' + ext if ext else ''), size=size, cluster=clus, clusters=cl,
                        free=all(fs.fat_entry(c) == 0 for c in cl), time=tm, date=dt,
                        data=b''.join(fs.cluster_bytes(c) for c in cl)[:size]))
    for a in out:
        a['overlaps'] = sorted({c for b in out if b is not a for c in b['clusters'] if c in a['clusters']})
    return out


def fat_datetime(date, time):
    try:
        return datetime.datetime(1980 + (date >> 9), (date >> 5) & 15, date & 31,
                                 time >> 11, (time >> 5) & 63, (time & 31) * 2)
    except ValueError:
        return None


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("image")
    ap.add_argument("--list", action="store_true")
    ap.add_argument("--extract")
    ap.add_argument("--raw")
    ap.add_argument("--json")
    ap.add_argument("--undelete", help="write deleted root files (contiguous-cluster guess) into this folder")
    a = ap.parse_args()
    data = open(a.image, "rb").read()
    raw = dim_to_raw(data)
    if a.raw:
        open(a.raw, "wb").write(raw)
    fs = Human68kFat(raw)
    rows = []
    for e in fs.walk():
        dtm = fat_datetime(e["date"], e["time"])
        row = dict(path=e["path"], kind=e["kind"], attr=f"0x{e['attr']:02x}", size=e["size"],
                   cluster=e["cluster"], datetime=dtm.isoformat(sep=" ") if dtm else None)
        if e["kind"] == "file":
            body = fs.read_chain(e["cluster"], e["size"]) if e["size"] else b""
            row["sha1"] = hashlib.sha1(body).hexdigest()
            row["clusters"] = len(fs.chain(e["cluster"])) if e["cluster"] else 0
            if a.extract:
                out = os.path.join(a.extract, e["path"])
                os.makedirs(os.path.dirname(out) or a.extract, exist_ok=True)
                open(out, "wb").write(body)
                if dtm:
                    ts = dtm.timestamp()
                    os.utime(out, (ts, ts))
        elif e["kind"] == "dir" and a.extract:
            os.makedirs(os.path.join(a.extract, e["path"]), exist_ok=True)
        rows.append(row)
    deleted = []
    for e in deleted_entries(fs):
        dtm = fat_datetime(e["date"], e["time"])
        deleted.append(dict(path=e["name"], size=e["size"], cluster=e["cluster"], clusters_free=e["free"],
                            overlaps=e["overlaps"], datetime=dtm.isoformat(sep=" ") if dtm else None,
                            sha1=hashlib.sha1(e["data"]).hexdigest() if e["size"] else None))
        if a.undelete and e["size"] and e["free"]:
            os.makedirs(a.undelete, exist_ok=True)
            out = os.path.join(a.undelete, e["name"].replace("?", "_"))
            open(out, "wb").write(e["data"])
            if dtm:
                os.utime(out, (dtm.timestamp(), dtm.timestamp()))
    info = dict(image=os.path.basename(a.image), tracks_present=sum(data[1:1 + TRACKS]),
                bpb=fs.bpb(), used_clusters=fs.used_clusters(), entries=rows, deleted=deleted)
    if a.json:
        open(a.json, "w").write(json.dumps(info, indent=1, ensure_ascii=False))
    if a.list or not a.extract:
        print(json.dumps(info["bpb"], ensure_ascii=False))
        print("tracks present", info["tracks_present"], "used clusters", info["used_clusters"])
        for r in rows:
            print(f"{r['kind']:6} {r['attr']} {r['size']:8} {r['datetime']} {r.get('sha1', '')[:12]} {r['path']}")


if __name__ == "__main__":
    sys.exit(main())
