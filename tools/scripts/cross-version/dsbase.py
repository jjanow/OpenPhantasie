import sys,re,struct,collections
d=open(sys.argv[1],'rb').read(); lo=int(sys.argv[2],16); hi=int(sys.argv[3],16)
strs=[m.start()+lo for m in re.finditer(rb'(?<=\x00)[\x20-\x7e]{5,}', d[lo:hi])]
imms=collections.Counter()
for op in (0xb8,0x68,0xbe,0xba,0xbf,0xbb,0xb9):
    for m in re.finditer(bytes([op]),d):
        p=m.start()
        if p+3<=len(d): imms[d[p+1]|d[p+2]<<8]+=1
res=[]
for B in range(0, hi, 16):
    h=sum(1 for s in strs if 0x100<=s-B<=0xffff and imms[s-B])
    res.append((h,B))
res.sort(reverse=True); print(len(strs), [(h,hex(b)) for h,b in res[:6]])
