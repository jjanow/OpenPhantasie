# Build the s41/s43 DNG1 test layouts (T traps, L locks, B wandering) from the s41 setup DNGX.SAV.
# usage: python3 s43_dngx_layouts.py <setup DNGX.SAV> <out dir>
import sys
src, out = sys.argv[1], sys.argv[2]
base = bytearray(open(src,'rb').read())
assert base[9*33+19]==0xca and base[10*33+20]==0x02
def put(b, x, y, v): b[y*33+x] = v
T = bytearray(base)
for y in range(1,9): put(T,20,y,0x02)
L = bytearray(base)
for y in range(1,9): put(L,20,y,0x01)
for y in (1,2,5,6,7,8): put(L,19,y,0xd5); put(L,21,y,0xca)
B = bytearray(base)
for y in range(1,10): put(B,20,y,0x0f)
for n,b in (('T',T),('L',L),('B',B)): open(f'{out}/DNGX_{n}.SAV','wb').write(b)
