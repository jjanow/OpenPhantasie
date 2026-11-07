// Ghidra headless post-script for Phantasie IV P4.X (see x68k_p4_ghidra_prep.py): symbols, BSS block, FPACK/DOS comments,
// then decompiles every text function into <dir>/decomp.c. Script argument: the prep output directory.
import ghidra.app.script.GhidraScript;
import ghidra.app.decompiler.*;
import ghidra.program.model.address.*;
import ghidra.program.model.listing.*;
import ghidra.program.model.symbol.*;
import ghidra.program.model.mem.*;
import java.io.*;
import java.nio.file.*;
import java.util.*;
import java.util.regex.*;

public class GhidraP4Setup extends GhidraScript {
    public void run() throws Exception {
        String sp = getScriptArgs()[0] + "/";
        Map<String,String> fpack = new HashMap<>();
        String[][] fp = {{"fe00","__LMUL"},{"fe01","__LDIV"},{"fe02","__LMOD"},{"fe0c","__RANDOMIZE"},{"fe0d","__SRAND"},{"fe0e","__RAND"},
          {"fe10","__STOL"},{"fe24","__ECVT"},{"fe25","__FCVT"},{"fe42","?"},
          {"fee0","__CLMUL"},{"fee1","__CLDIV"},{"fee2","__CLMOD"},{"fee3","__CUMUL"},{"fee4","__CUDIV"},{"fee5","__CUMOD"},
          {"fee6","__CLTOD"},{"fee7","__CDTOL"},{"fee8","__CLTOF"},{"fee9","__CFTOL"},{"feea","__CFTOD"},{"feeb","__CDTOF"},
          {"feec","__CDCMP"},{"feed","__CDADD"},{"feee","__CDSUB"},{"feef","__CDMUL"},{"fef0","__CDDIV"},{"fef8","__CDTST"}};
        for (String[] e : fp) fpack.put(e[0], e[1]);
        AddressSpace as = currentProgram.getAddressFactory().getDefaultAddressSpace();
        Memory mem = currentProgram.getMemory();
        if (mem.getBlock(as.getAddress(0x4a3c6)) == null) {
            mem.createUninitializedBlock("bss", as.getAddress(0x4a3c6), 0x463a6 + 0x2000, false);
        }
        String lf = new String(Files.readAllBytes(Paths.get(sp + "linef.json")));
        Matcher m = Pattern.compile("\"([0-9a-f]+)\": \"([0-9a-f]+)\"").matcher(lf);
        Listing L = currentProgram.getListing();
        while (m.find()) {
            Address a = as.getAddress(Long.parseLong(m.group(1), 16));
            String w = m.group(2);
            String c = w.startsWith("fe") ? ("FPACK $" + w + " " + fpack.getOrDefault(w, "?")) : ("DOS $" + w);
            L.setComment(a, CodeUnit.PRE_COMMENT, c);
        }
        String sy = new String(Files.readAllBytes(Paths.get(sp + "syms.json")));
        Matcher s = Pattern.compile("\\[\"([^\"]+)\", (\\d+), \"(\\w+)\"\\]").matcher(sy);
        List<Address> funcs = new ArrayList<>();
        SymbolTable st = currentProgram.getSymbolTable();
        while (s.find()) {
            Address a = as.getAddress(Long.parseLong(s.group(2)));
            st.createLabel(a, s.group(1), SourceType.USER_DEFINED);
            if (s.group(3).equals("text")) funcs.add(a);
        }
        for (Address a : funcs) disassemble(a);
        for (Address a : funcs) {
            Symbol sym = st.getPrimarySymbol(a);
            Function f = getFunctionAt(a);
            if (f == null) f = createFunction(a, sym.getName());
        }
        DecompInterface di = new DecompInterface();
        di.openProgram(currentProgram);
        PrintWriter pw = new PrintWriter(new FileWriter(sp + "decomp.c"));
        for (Address a : funcs) {
            Function f = getFunctionAt(a);
            if (f == null) { pw.println("// no function at " + a); continue; }
            DecompileResults r = di.decompileFunction(f, 120, monitor);
            pw.println("// ==== " + f.getName() + " @ " + a);
            if (r != null && r.getDecompiledFunction() != null) pw.println(r.getDecompiledFunction().getC());
            else pw.println("// decompile failed: " + (r == null ? "" : r.getErrorMessage()));
        }
        pw.close();
    }
}
