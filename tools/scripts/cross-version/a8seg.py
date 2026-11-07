import sys
d=open(sys.argv[1],'rb').read(); p=0; mem=bytearray(65536); segs=[]
while p+4<=len(d):
    if d[p]==0xff and d[p+1]==0xff: p+=2
    s=d[p]|d[p+1]<<8; e=d[p+2]|d[p+3]<<8; p+=4
    mem[s:e+1]=d[p:p+e-s+1]; segs.append((s,e,p)); p+=e-s+1
for s,e,fo in segs: print("seg %04x-%04x fileoff %x"%(s,e,fo))
open(sys.argv[2],'wb').write(mem)
