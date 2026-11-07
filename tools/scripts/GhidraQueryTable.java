// Ghidra headless post-script (Java): dump memory map + probe an address for
// data/refs/xrefs. Used to resolve the OV1 training-class dispatch table at
// CS:0x669a (see docs/plan/HANDOFF-2026-07-18.md).
// Run via analyzeHeadless -process OV1.OVR -postScript GhidraQueryTable.java <outDir>
// @category Phantasie
import java.io.FileWriter;
import java.io.PrintWriter;

import ghidra.app.script.GhidraScript;
import ghidra.program.model.address.Address;
import ghidra.program.model.address.AddressFactory;
import ghidra.program.model.listing.Listing;
import ghidra.program.model.mem.Memory;
import ghidra.program.model.mem.MemoryBlock;
import ghidra.program.model.symbol.Reference;
import ghidra.program.model.symbol.ReferenceIterator;
import ghidra.program.model.symbol.ReferenceManager;

public class GhidraQueryTable extends GhidraScript {
    @Override
    public void run() throws Exception {
        String[] args = getScriptArgs();
        String outDir = args.length > 0 ? args[0] : "/tmp";
        String name = currentProgram.getName();

        try (PrintWriter fh = new PrintWriter(new FileWriter(outDir + "/" + name + ".tablequery.txt"))) {
            Memory mem = currentProgram.getMemory();
            fh.println("# Memory blocks in " + name);
            for (MemoryBlock b : mem.getBlocks()) {
                fh.println(b.getName() + "\tstart=" + b.getStart() + "\tend=" + b.getEnd()
                        + "\tsize=" + b.getSize() + "\tinitialized=" + b.isInitialized()
                        + "\ttype=" + b.getType());
            }

            fh.println();
            fh.println("# Probing candidate addresses for the training dispatch table");
            AddressFactory af = currentProgram.getAddressFactory();
            String[] candidates = { "0x669a", "0x2a74", "0xa2c0" };
            for (String c : candidates) {
                Address a = af.getAddress(c);
                fh.println("--- candidate " + c + " -> " + a + " ---");
                if (a == null) {
                    fh.println("  could not parse address");
                    continue;
                }
                boolean inBlock = mem.contains(a);
                fh.println("  in defined memory: " + inBlock);
                if (inBlock) {
                    try {
                        byte[] buf = new byte[16];
                        int n = mem.getBytes(a, buf);
                        StringBuilder sb = new StringBuilder();
                        for (int i = 0; i < n; i++) sb.append(String.format("%02x ", buf[i]));
                        fh.println("  bytes: " + sb.toString());
                    } catch (Exception e) {
                        fh.println("  byte read failed: " + e.getMessage());
                    }
                    ReferenceManager rm = currentProgram.getReferenceManager();
                    ReferenceIterator refs = rm.getReferencesTo(a);
                    int nrefs = 0;
                    while (refs.hasNext()) {
                        Reference r = refs.next();
                        fh.println("  xref from " + r.getFromAddress() + " type=" + r.getReferenceType());
                        nrefs++;
                    }
                    fh.println("  total xrefs to exact address: " + nrefs);
                }
            }
        }
        println("[GhidraQueryTable] " + name + " -> " + outDir);
    }
}
