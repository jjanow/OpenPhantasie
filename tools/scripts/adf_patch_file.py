"""Replace an OFS file's contents in an ADF in place (same size): find each 488-B data chunk of OLD in
the ADF's data blocks, write NEW's chunk, fix the block checksum.  adf_patch_file.py ADF OLD NEW"""
import struct, sys
adf, old, new = sys.argv[1], open(sys.argv[2], 'rb').read(), open(sys.argv[3], 'rb').read()
assert len(old) == len(new)
d = bytearray(open(adf, 'rb').read())
n = 0
for c in range(0, len(old), 488):
    o, w = old[c:c + 488], new[c:c + 488]
    hits = [b for b in range(len(d) // 512)
            if struct.unpack('>I', d[b * 512:b * 512 + 4])[0] == 8 and d[b * 512 + 24:b * 512 + 24 + len(o)] == o
            and struct.unpack('>I', d[b * 512 + 8:b * 512 + 12])[0] == c // 488 + 1]
    assert len(hits) == 1, (c, hits)
    b = hits[0] * 512
    if o == w:
        continue
    d[b + 24:b + 24 + len(w)] = w
    d[b + 20:b + 24] = b'\0\0\0\0'
    s = sum(struct.unpack('>128I', d[b:b + 512])) & 0xffffffff
    d[b + 20:b + 24] = struct.pack('>I', (-s) & 0xffffffff)
    n += 1
open(adf, 'wb').write(d)
print('blocks patched', n)
