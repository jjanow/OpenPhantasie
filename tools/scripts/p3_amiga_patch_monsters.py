"""Patch monsinfo.dat (38-B records): Wild Cat (4) <- Wolf (31), Gremlin (32) <- Mud Elemental (53),
Giant Bee (5): special chance 100, specials 1,1,1 (Fireflash I), magic (byte 24) 9."""
import sys
src, dst = sys.argv[1], sys.argv[2]
d = bytearray(open(src, 'rb').read())
R = 38
rec = lambda i: d[i * R:(i + 1) * R]
def put(i, b):
    d[i * R:(i + 1) * R] = b
put(4, bytes(rec(31)))
put(32, bytes(rec(53)))
b = 5 * R
d[b + 20] = 100
d[b + 21:b + 24] = bytes([1, 1, 1])
d[b + 24] = 9
open(dst, 'wb').write(d)
for i in (4, 5, 31, 32, 53):
    print(i, bytes(rec(i)).hex())
