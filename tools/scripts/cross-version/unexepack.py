import sys,struct
f=open(sys.argv[1],'rb').read()
cparhdr=struct.unpack_from('<H',f,8)[0]; cs=struct.unpack_from('<H',f,0x16)[0]
img=cparhdr*16; hdr=img+cs*16
rip,rcs,ms,esz,rsp,rss,dest_len,sig=struct.unpack_from('<HHHHHHH2s',f,hdr)
print('real CS:IP %04x:%04x SS:SP %04x:%04x destlen %x sig %s'%(rcs,rip,rss,rsp,dest_len,sig))
out=bytearray(dest_len*16); out[:hdr-img]=f[img:hdr]; src=hdr-img
while out[src-1]==0xff: src-=1
dst=len(out)
while True:
    op=out[src-1]; cnt=out[src-3]|out[src-2]<<8; src-=3
    if op&0xfe==0xb0:
        fill=out[src-1]; src-=1; out[dst-cnt:dst]=bytes([fill])*cnt
    elif op&0xfe==0xb2:
        out[dst-cnt:dst]=out[src-cnt:src]; src-=cnt
    else: raise SystemExit('bad op %x'%op)
    dst-=cnt
    if op&1: break
open(sys.argv[2],'wb').write(out); print('wrote',len(out))
