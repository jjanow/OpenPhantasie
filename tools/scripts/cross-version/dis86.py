import sys,capstone
d=open(sys.argv[1],'rb').read(); a=int(sys.argv[2],16); b=int(sys.argv[3],16)
md=capstone.Cs(capstone.CS_ARCH_X86, capstone.CS_MODE_16)
p=a
while p<b:
    n=0
    for i in md.disasm(d[p:b],p):
        print("%05x  %-16s %s %s"%(i.address,i.bytes.hex(),i.mnemonic,i.op_str)); p=i.address+i.size; n+=1
    if not n: print("%05x  db %02x"%(p,d[p])); p+=1
