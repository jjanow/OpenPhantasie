import sys
pat=bytes.fromhex(sys.argv[1])
for f in sys.argv[2:]:
    d=open(f,'rb').read(); i=d.find(pat)
    while i>=0:
        ctx=d[i:i+24]
        print(f, hex(i), ctx.hex(' '), ''.join(chr(c) if 32<=c<127 else '.' for c in d[i:i+40]))
        i=d.find(pat,i+1)
