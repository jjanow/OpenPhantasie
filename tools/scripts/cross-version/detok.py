import sys
T="END FOR NEXT DATA INPUT DEL DIM READ GR TEXT PR# IN# CALL PLOT HLIN VLIN HGR2 HGR HCOLOR= HPLOT DRAW XDRAW HTAB HOME ROT= SCALE= SHLOAD TRACE NOTRACE NORMAL INVERSE FLASH COLOR= POP VTAB HIMEM: LOMEM: ONERR RESUME RECALL STORE SPEED= LET GOTO RUN IF RESTORE & GOSUB RETURN REM STOP ON WAIT LOAD SAVE DEF POKE PRINT CONT LIST CLEAR GET NEW TAB( TO FN SPC( THEN AT NOT STEP + - * / ^ AND OR > = < SGN INT ABS USR FRE SCRN( PDL POS SQR RND LOG EXP COS SIN TAN ATN PEEK LEN STR$ VAL ASC CHR$ LEFT$ RIGHT$ MID$".split()
d=open(sys.argv[1],'rb').read()
out=[];i=0
q=False
while i<len(d):
    b=d[i]
    if b==0:
        if i+4<len(d):
            ln=d[i+3]|d[i+4]<<8
            out.append("\n@%06x %5d "%(i+5,ln)); i+=5; q=False; continue
    if b==0x22: q=not q
    if b>=0x80 and not q and b-0x80<len(T): out.append(" "+T[b-0x80]+" ")
    elif 32<=b<127: out.append(chr(b))
    else: out.append("\\x%02x"%b)
    i+=1
print("".join(out))
