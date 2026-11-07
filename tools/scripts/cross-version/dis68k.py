import sys,capstone
raw=open(sys.argv[1],'rb').read(); img=raw[0x1c:]
a=int(sys.argv[2],16); b=int(sys.argv[3],16)
md=capstone.Cs(capstone.CS_ARCH_M68K, capstone.CS_MODE_M68K_000)
p=a
while p<b:
    n=0
    for i in md.disasm(img[p:b],p):
        print("%05x  %-20s %s %s"%(i.address,i.bytes.hex(),i.mnemonic,i.op_str)); p=i.address+i.size; n+=1
    if not n: print("%05x  dw %04x"%(p,int.from_bytes(img[p:p+2],'big'))); p+=2
