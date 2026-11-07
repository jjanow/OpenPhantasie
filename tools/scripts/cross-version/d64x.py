import sys,re,os
def spt(t): return 21 if t<=17 else 19 if t<=24 else 18 if t<=30 else 17
def off(t,s): return (sum(spt(x) for x in range(1,t))+s)*256
def chain(img,t,s):
    out=bytearray(); seen=set()
    while t:
        if (t,s) in seen: break
        seen.add((t,s)); b=img[off(t,s):off(t,s)+256]
        out+= b[2:] if b[0] else b[2:b[1]+1]; t,s=b[0],b[1]
    return bytes(out)
img=open(sys.argv[1],'rb').read(); dest=sys.argv[2]; os.makedirs(dest,exist_ok=True)
t,s=18,1
while t:
    b=img[off(t,s):off(t,s)+256]
    for i in range(8):
        e=b[i*32:i*32+32]
        if e[2]&7==0 and not e[2]&0x80: continue
        n=re.sub(r'[^A-Za-z0-9.+-]','_',e[5:21].rstrip(b'\xa0').decode('latin1')) or '_'
        d=chain(img,e[3],e[4]); open(os.path.join(dest,n),'wb').write(d)
        print(n, e[2]&7, len(d), "load=%04x"%(d[0]|d[1]<<8) if len(d)>1 else "")
    t,s=b[0],b[1]
