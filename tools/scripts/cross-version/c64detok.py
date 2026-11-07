import sys
T="END FOR NEXT DATA INPUT# INPUT DIM READ LET GOTO RUN IF RESTORE GOSUB RETURN REM STOP ON WAIT LOAD SAVE VERIFY DEF POKE PRINT# PRINT CONT LIST CLR CMD SYS OPEN CLOSE GET NEW TAB( TO FN SPC( THEN NOT STEP + - * / ^ AND OR > = < SGN INT ABS USR FRE POS SQR RND LOG EXP COS SIN TAN ATN PEEK LEN STR$ VAL ASC CHR$ LEFT$ RIGHT$ MID$ GO".split()
d=open(sys.argv[1],'rb').read()[2:]
base=int(sys.argv[2],16) if len(sys.argv)>2 else 0x801
p=0
while p+4<=len(d):
    nxt=d[p]|d[p+1]<<8
    if nxt==0: break
    ln=d[p+2]|d[p+3]<<8
    q=p+4; s=[]; inq=False
    while q<len(d) and d[q]!=0:
        b=d[q]
        if b==0x22: inq=not inq
        if b>=0x80 and not inq and b-0x80<len(T): s.append(T[b-0x80])
        elif 32<=b<127: s.append(chr(b))
        else: s.append("{%02x}"%b)
        q+=1
    print("%5d %s"%(ln,"".join(s)))
    np=nxt-base
    if np<=p: p=q+1
    else: p=np
