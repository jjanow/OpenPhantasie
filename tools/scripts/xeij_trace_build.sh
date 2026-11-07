#!/usr/bin/env bash
# Build a copy of XEiJ with two env-gated test hooks (s117; FORMAT.md §6.10 / §6.11.1, RE-REFERENCE "X68000 full-system runs"):
#
#   xeij_trace_build.sh OUTDIR
#   XEIJ_HOME=OUTDIR XEIJ_OPMTRACE=WORK/opm.trace [XEIJ_PCHOOK=FROM,TO XEIJ_POKE=A=B,...] xeij_run.sh WORK ...
#
# XEIJ_OPMTRACE=FILE  one line per event, each starting with the emulated time in ps (XEiJ.mpuClockTime, from reset):
#                       "W rr dd pc=PC"  an OPM register write (rr = register, dd = data; PC = the writing instruction)
#                       "A"              an OPM timer A overflow
#                       "I d0=.. d1=.. d2=.. a1=.. pc=PC"  a trap #15 IOCS call with d0 = $F0 (OPMDRV: d1 = 0 _m_init,
#                                        1 alloc, 2 assign, 4 vset, 6 trk, 8 play, ...), $68 (_OPMSET) or $6A (_OPMINTST)
#                       "RESET"          the OPM reset
#                     The Sound > OPM log window does not open on Xvfb (s110); this replaces it.
# XEIJ_PCHOOK=FROM,TO the first time the MPU is about to run FROM (hex runtime address), write the bytes of
#                     XEIJ_POKE=ADDR=BYTE,... (hex) to main memory and go on at TO instead (stack untouched, so TO must take
#                     FROM's arguments).
# XEIJ_PCTRACE=ADDR   log "P" (with the time) every time the MPU is about to run ADDR (hex runtime address), e.g. IV's
#                     _anima (image 0x2e7f8 + 0x4d4e0 = 0x7bcd8): the intervals are the input loop's pass length.
#                     P4.X loads at 0x4d4e0 in the two-disk boot (runtime = image address + 0x4d4e0;
#                     _m_init's trap at image 0x3b542 is logged at pc 0x088a22). Used to reach the Shipwright and the
#                     endings without playing there: FROM = _disk (0x1c7e + base), TO = _shipyard / _ending.
#                     A comma list traces several addresses (s119); each line is then "P ADDR" (one address: plain "P").
# XEIJ_PCHOOK_ARM=FILE the hook waits until FILE exists (s119): FROM can then be a routine that runs all the time
#                     (e.g. _anima, FROM = TO for a poke with no jump), and `touch FILE` fires it at the moment wanted.
# XEIJ_CMDFILE=FILE + XEIJ_CMDPC=ADDR (s120): every 32nd time the MPU reaches ADDR (hex runtime address, e.g. _anima) the
#                     hook looks for FILE; when it exists it runs its lines and deletes it: "poke ADDR BYTE ..." (hex),
#                     "dump ADDR LEN" (hex; logs "D ADDR hexbytes" to the XEIJ_OPMTRACE file), "jump ADDR" (go on at ADDR).
#                     Pokes and reads at any moment of a run, several times (XEIJ_PCHOOK fires once).
# Needs Java 26 (XEIJ_JDK, default ~/emulators/jdk-26.0.2.1+1). XEIJ_SRC = the XEiJ folder (default ~/emulators/XEiJ-0.26.10.08).
set -euo pipefail
out=$(realpath -m "$1")
src=${XEIJ_SRC:-$HOME/emulators/XEiJ-0.26.10.08}
jdk=${XEIJ_JDK:-$HOME/emulators/jdk-26.0.2.1+1}
mkdir -p "$out"
rm -rf "$out/xeij" "$out/data" "$out/class"
cp -r "$src/xeij" "$src/data" "$src/jSerialComm-2.11.4.jar" "$out/"
cd "$out"

cat > xeij/OpmTrace.java <<'EOF'
package xeij;
import java.io.*;
public class OpmTrace {
  static PrintWriter out;
  static {
    String f = System.getenv ("XEIJ_OPMTRACE");
    if (f != null) {
      try { out = new PrintWriter (new BufferedWriter (new FileWriter (f), 1 << 16), false); } catch (IOException e) { out = null; }
      Runtime.getRuntime ().addShutdownHook (new Thread (() -> { if (out != null) { synchronized (OpmTrace.class) { out.flush (); } } }));
    }
  }
  public static synchronized void line (String s) {
    if (out != null) { out.println (XEiJ.mpuClockTime + " " + s); }
  }
  public static synchronized void flush () { if (out != null) out.flush (); }
}
EOF

cat > xeij/PcHook.java <<'EOF'
package xeij;
public class PcHook {
  public static int from = -1, to = -1;
  public static boolean on = false, many = false;
  public static byte[] mark = new byte[1 << 24];
  public static String arm = System.getenv ("XEIJ_PCHOOK_ARM");
  public static int[] pokeA = new int[0], pokeD = new int[0];
  static {
    String tr = System.getenv ("XEIJ_PCTRACE");
    if (tr != null && !tr.isBlank ()) {
      String[] t = tr.split (",");
      for (String a : t) { mark[Integer.parseInt (a.trim (), 16) & 0xffffff] = 1; }
      on = true; many = t.length > 1;
    }
    String h = System.getenv ("XEIJ_PCHOOK");
    if (h != null) {
      String[] p = h.split (",");
      from = Integer.parseInt (p[0].trim (), 16);
      to = Integer.parseInt (p[1].trim (), 16);
      String q = System.getenv ("XEIJ_POKE");
      if (q != null && !q.isBlank ()) {
        String[] r = q.split (",");
        pokeA = new int[r.length]; pokeD = new int[r.length];
        for (int i = 0; i < r.length; i++) {
          String[] kv = r[i].split ("=");
          pokeA[i] = Integer.parseInt (kv[0].trim (), 16);
          pokeD[i] = Integer.parseInt (kv[1].trim (), 16);
        }
      }
    }
  }
  public static String cmdFile = System.getenv ("XEIJ_CMDFILE");
  public static int cmdPc = -1, cmdCount = 0;
  static {
    String c = System.getenv ("XEIJ_CMDPC");
    if (cmdFile != null && c != null) { cmdPc = Integer.parseInt (c.trim (), 16); mark[cmdPc & 0xffffff] |= 2; on = true; }
  }
  public static void cmd () {
    if ((++cmdCount & 31) != 0) return;
    java.io.File f = new java.io.File (cmdFile);
    if (!f.exists ()) return;
    try {
      for (String l : java.nio.file.Files.readAllLines (f.toPath ())) {
        String[] w = l.trim ().split ("\\s+");
        if (w[0].equals ("poke")) {
          int a = Integer.parseInt (w[1], 16);
          for (int i = 2; i < w.length; i++) MainMemory.mmrM8[a + i - 2] = (byte) Integer.parseInt (w[i], 16);
          OpmTrace.line ("POKE " + l.trim ());
        } else if (w[0].equals ("dump")) {
          int a = Integer.parseInt (w[1], 16), n = Integer.parseInt (w[2], 16);
          StringBuilder b = new StringBuilder (String.format ("D %06x ", a));
          for (int i = 0; i < n; i++) b.append (String.format ("%02x", MainMemory.mmrM8[a + i] & 255));
          OpmTrace.line (b.toString ());
        } else if (w[0].equals ("jump")) {
          XEiJ.regPC = Integer.parseInt (w[1], 16);
          OpmTrace.line ("JUMP " + w[1]);
        }
      }
      OpmTrace.flush ();
      f.delete ();
    } catch (Exception e) { OpmTrace.line ("CMD error " + e); f.delete (); }
  }
  public static void hit () {
    if (on && (mark[XEiJ.regPC & 0xffffff] & 2) != 0) { cmd (); }
    if (on && (mark[XEiJ.regPC & 0xffffff] & 1) != 0) {
      OpmTrace.line (many ? String.format ("P %06x", XEiJ.regPC) : "P");
      OpmTrace.flush ();
    }
    if (XEiJ.regPC == from && (arm == null || new java.io.File (arm).exists ())) {
      fire ();
    }
  }
  public static void fire () {
    for (int i = 0; i < pokeA.length; i++) {
      MainMemory.mmrM8[pokeA[i]] = (byte) pokeD[i];
    }
    OpmTrace.line (String.format ("HOOK %06x -> %06x", from, to));
    OpmTrace.flush ();
    XEiJ.regPC = to;
    from = -1;
  }
}
EOF

python3 - <<'EOF'
def patch(path, old, new):
    s = open(path, encoding='utf-8').read()
    assert s.count(old) == 1, (path, old)
    open(path, 'w', encoding='utf-8').write(s.replace(old, new))

patch('xeij/OPM.java', """      @Override public void written (int pointer, int address, int data) {
""", """      @Override public void written (int pointer, int address, int data) {
        OpmTrace.line (String.format ("W %02x %02x pc=%06x", address, data, XEiJ.regPC0));
""")
patch('xeij/OPM.java', """      @Override protected void tick () {
        opmYM2151.timerAExpired ();""", """      @Override protected void tick () {
        OpmTrace.line ("A");
        opmYM2151.timerAExpired ();""")
patch('xeij/OPM.java', """  public static void opmReset () {
""", """  public static void opmReset () {
    OpmTrace.line ("RESET");
""")
patch('xeij/MC68000.java', """  public static void irpTrap15 () throws M68kException {
""", """  public static void irpTrap15 () throws M68kException {
    { int c = XEiJ.regRn[0] & 255; if (c == 0xf0 || c == 0x68 || c == 0x6a) {
      OpmTrace.line (String.format ("I d0=%02x d1=%08x d2=%08x a1=%08x pc=%06x", c, XEiJ.regRn[1], XEiJ.regRn[2], XEiJ.regRn[9], XEiJ.regPC0)); OpmTrace.flush (); } }
""")
patch('xeij/MC68000.java', """          XEiJ.regPC0 = t = XEiJ.regPC;  //命令の先頭アドレス
          XEiJ.regPC = t + 2;
          XEiJ.regOC = (InstructionBreakPoint.IBP_ON""", """          if (XEiJ.regPC == PcHook.from || PcHook.on && PcHook.mark[XEiJ.regPC & 0xffffff] != 0) { PcHook.hit (); }
          XEiJ.regPC0 = t = XEiJ.regPC;  //命令の先頭アドレス
          XEiJ.regPC = t + 2;
          XEiJ.regOC = (InstructionBreakPoint.IBP_ON""")
EOF

mkdir -p class
"$jdk/bin/javac" --release 26 -encoding UTF-8 -cp "xeij:jSerialComm-2.11.4.jar" -d class xeij/*.java -nowarn 2>&1 | grep -E "error" || true
printf "Manifest-Version: 1.0\nClass-Path: . jSerialComm-2.11.4.jar\nMain-Class: xeij/XEiJ\nEnable-Native-Access: ALL-UNNAMED\n" > manifest.txt
rm -f XEiJ.jar
"$jdk/bin/jar" cfm XEiJ.jar manifest.txt -C class . -C . data
echo "built $out/XEiJ.jar (run with XEIJ_HOME=$out)"
