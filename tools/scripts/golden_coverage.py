"""P1.3 golden-coverage audit: list Core members whose doc comment marks a decoded rule ([C]/[X]) but that no test names.

Run from the repo root: ``python3 tools/scripts/golden_coverage.py``. A hit is a candidate gap, not proof — the member
may be pinned through a caller. Rules that live only in a logic-spec or a data JSON file are not seen by this scan.
"""
import re,glob,sys
tests="".join(open(f).read() for f in glob.glob("tests/OpenPhantasie.Core.Tests/*.cs"))
out=[]
for f in sorted(glob.glob("src/OpenPhantasie.Core/**/*.cs",recursive=True)):
    lines=open(f).read().split("\n")
    doc=[]
    for i,l in enumerate(lines):
        s=l.strip()
        if s.startswith("///"): doc.append(s); continue
        if doc and re.search(r"\[C\]|\[C |\[X\]|\[C/",(" ".join(doc))):
            m=re.match(r"(?:public|internal)\s+(?:static\s+)?(?:readonly\s+)?(?:const\s+)?(?:[\w<>\[\],?. ()]+?\s+)?(\w+)\s*(\(|=>|\{|=|;)",s)
            if m and m.group(1) not in ("class","record","struct","enum","get","set"):
                name=m.group(1)
                if not re.search(r"\b"+name+r"\b",tests):
                    out.append(f"{f}:{i+1}: {name}")
        if not s.startswith("["): doc=[]
print("\n".join(out))
