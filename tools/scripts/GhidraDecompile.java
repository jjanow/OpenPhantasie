// Ghidra headless post-script (Java): dump decompiled C for every function.
// Run via analyzeHeadless -process ... -postScript GhidraDecompile.java <outDir>
// @category Phantasie
import java.io.FileWriter;
import java.io.PrintWriter;

import ghidra.app.decompiler.DecompInterface;
import ghidra.app.decompiler.DecompileResults;
import ghidra.app.script.GhidraScript;
import ghidra.program.model.listing.Function;
import ghidra.program.model.listing.FunctionIterator;

public class GhidraDecompile extends GhidraScript {
    @Override
    public void run() throws Exception {
        String[] args = getScriptArgs();
        String outDir = args.length > 0 ? args[0] : "/tmp";
        String name = currentProgram.getName();

        DecompInterface dec = new DecompInterface();
        dec.openProgram(currentProgram);

        int n = 0;
        try (PrintWriter fh = new PrintWriter(new FileWriter(outDir + "/" + name + ".decompiled.c"))) {
            fh.println("// Decompiled functions for " + name + " (Ghidra 12.1, x86 real-mode).");
            fh.println("// Auto-analysis of a raw COM/overlay: treat as a lead, not ground truth.\n");
            FunctionIterator fit = currentProgram.getFunctionManager().getFunctions(true);
            for (Function f : fit) {
                DecompileResults res = dec.decompileFunction(f, 30, monitor);
                fh.println("// ==== " + f.getName() + " @ " + f.getEntryPoint() + " ====");
                if (res != null && res.decompileCompleted()) {
                    fh.println(res.getDecompiledFunction().getC());
                } else {
                    fh.println("// (decompile failed)");
                }
                fh.println();
                n++;
            }
        }
        println("[GhidraDecompile] " + name + ": " + n + " functions -> " + outDir);
    }
}
