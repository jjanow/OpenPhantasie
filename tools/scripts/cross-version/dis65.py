import sys,capstone
m=open(sys.argv[1],'rb').read(); a=int(sys.argv[2],16); b=int(sys.argv[3],16)
md=capstone.Cs(capstone.CS_ARCH_MOS65XX, capstone.CS_MODE_MOS65XX_6502)
p=a
while p<b:
    got=False
    for i in md.disasm(m[p:b], p):
        txt=''
        print("%04x  %-9s %-6s %s"%(i.address, i.bytes.hex(), i.mnemonic, i.op_str))
        p=i.address+i.size; got=True
        # stop at string after JSR $2a38? just continue
    if not got:
        print("%04x  %02x  db '%s'"%(p,m[p],chr(m[p]) if 32<=m[p]<127 else '.')); p+=1
