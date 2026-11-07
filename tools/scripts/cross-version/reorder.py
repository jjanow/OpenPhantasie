import sys
d=open(sys.argv[1],'rb').read()
maps={
 'dos':list(range(16)),
 'dos2prodos':[0,0xd,0xb,9,7,5,3,1,0xe,0xc,0xa,8,6,4,2,0xf],
 'prodos2dos':[0,7,0xe,6,0xd,5,0xc,4,0xb,3,0xa,2,9,1,8,0xf],
 'phys':[0,7,14,6,13,5,12,4,11,3,10,2,9,1,8,15],
 'physinv':[0,13,11,9,7,5,3,1,14,12,10,8,6,4,2,15],
}
def build(m):
    out=bytearray()
    for t in range(35):
        for s in range(16): out+=d[(t*16+m[s])*256:(t*16+m[s])*256+256]
    return bytes(out)
def score(b):
    # count positions p where b[p]=0 and a link at p+1 points such that link - (p+1) small and b[link-delta]...
    sc=0
    for p in range(len(b)-5):
        if b[p]!=0: continue
        lk=b[p+1]|b[p+2]<<8
        # next line starts at q=p+1 ; its link lk should point to start of following line: find next zero after p+5
        q=b.find(b'\0',p+5)
        if q<0 or q-p>250: continue
        nl=b[q+1]|b[q+2]<<8
        if nl-lk==0 and lk!=0: pass
        if lk!=0 and nl-lk>0 and nl-lk<256 and (nl-lk)==(b.find(b'\0',q+5)-q): sc+=1
    return sc
for k,m in maps.items():
    b=build(m); print(k,score(b)); open(sys.argv[2]+'.'+k,'wb').write(b)
