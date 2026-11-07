// Ghidra headless post-script (Java): export a function map + defined strings.
// Run via analyzeHeadless -process ... -postScript GhidraExport.java <outDir>
// @category Phantasie
import java.io.FileWriter;
import java.io.PrintWriter;

import ghidra.app.script.GhidraScript;
import ghidra.program.model.data.DataType;
import ghidra.program.model.listing.Data;
import ghidra.program.model.listing.DataIterator;
import ghidra.program.model.listing.Function;
import ghidra.program.model.listing.FunctionIterator;
import ghidra.program.model.listing.Listing;

public class GhidraExport extends GhidraScript {
    @Override
    public void run() throws Exception {
        String[] args = getScriptArgs();
        String outDir = args.length > 0 ? args[0] : "/tmp";
        String name = currentProgram.getName();

        Listing listing = currentProgram.getListing();

        int nf = 0;
        try (PrintWriter fh = new PrintWriter(new FileWriter(outDir + "/" + name + ".functions.txt"))) {
            fh.println("# Functions in " + name);
            fh.println("# entry\tsize\tname");
            FunctionIterator fit = currentProgram.getFunctionManager().getFunctions(true);
            for (Function f : fit) {
                fh.println(f.getEntryPoint() + "\t" + f.getBody().getNumAddresses() + "\t" + f.getName());
                nf++;
            }
        }

        int ns = 0;
        try (PrintWriter fh = new PrintWriter(new FileWriter(outDir + "/" + name + ".strings.txt"))) {
            fh.println("# Defined strings in " + name + " (addr\tvalue)");
            DataIterator dit = listing.getDefinedData(true);
            for (Data d : dit) {
                DataType dt = d.getDataType();
                String tn = dt.getName().toLowerCase();
                if (tn.contains("string") || tn.contains("char") || tn.contains("unicode")) {
                    String val = d.getDefaultValueRepresentation();
                    fh.println(d.getAddress() + "\t" + val);
                    ns++;
                }
            }
        }
        println("[GhidraExport] " + name + ": " + nf + " functions, " + ns + " strings -> " + outDir);
    }
}
