import sys,os,re
d=open(sys.argv[1],'rb').read(); dest=sys.argv[2]; os.makedirs(dest,exist_ok=True)
ss=d[4]|d[5]<<8; img=d[16:]
def sec(n):
    if n<=3: return img[(n-1)*128:n*128]
    return img[3*128+(n-4)*ss:3*128+(n-3)*ss]
for s in range(361,369):
    b=sec(s)
    for e in range(8):
        en=b[e*16:e*16+16]
        fl=en[0]
        if fl==0: continue
        if fl&0x80: continue
        cnt=en[1]|en[2]<<8; start=en[3]|en[4]<<8
        name=(en[5:13].decode('latin1').strip()+"."+en[13:16].decode('latin1').strip())
        fileno=(s-361)*8+e
        out=bytearray(); n=start; seen=set()
        while n and n not in seen and len(seen)<2000:
            seen.add(n); x=sec(n)
            nb=x[ss-1]&0x7f; nxt=((x[ss-3]&3)<<8)|x[ss-2]
            out+=x[:nb]; n=nxt
        nm=re.sub(r'[^A-Za-z0-9.]','_',name)
        open(os.path.join(dest,nm),'wb').write(out); print(nm,hex(fl),cnt,len(out))
