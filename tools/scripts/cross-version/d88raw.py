import sys,struct
d=open(sys.argv[1],'rb').read()
name=d[:16]; size=struct.unpack_from('<I',d,0x1c)[0]; mtype=d[0x1b]
tracks=[struct.unpack_from('<I',d,0x20+4*i)[0] for i in range(164)]
secs=[]
for t in tracks:
    if not t: continue
    p=t
    n=struct.unpack_from('<H',d,p+4)[0]
    for k in range(n):
        c,h,r,N=d[p],d[p+1],d[p+2],d[p+3]; ln=struct.unpack_from('<H',d,p+14)[0]
        secs.append((c,h,r,N,d[p+16:p+16+ln])); p+=16+ln
secs.sort(key=lambda s:(s[0],s[1],s[2]))
geo={}
for c,h,r,N,b in secs: geo.setdefault(len(b),0); geo[len(b)]+=1
print(sys.argv[1].split('/')[-1], 'media',hex(mtype),'sectors',len(secs),'sizes',geo, 'tracks used',sum(1 for t in tracks if t))
# per track info
spt={}
for c,h,r,N,b in secs: spt.setdefault((c,h),[]).append((r,len(b)))
ks=sorted(spt)
print('first tracks:',[(k,len(spt[k]),spt[k][0][1]) for k in ks[:3]], 'last',ks[-1])
open(sys.argv[2],'wb').write(b''.join(s[4] for s in secs))
