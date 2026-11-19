import os
import sys
import tempfile

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

from docx import Document

SRC = r"C:\Users\kuang\Documents\FYP\ProjectProposal(6).docx"
OUT = os.path.join(tempfile.gettempdir(), "proposal_dump.txt")

doc = Document(SRC)

lines = [f"PARAGRAPHS={len(doc.paragraphs)} TABLES={len(doc.tables)}"]

for i, para in enumerate(doc.paragraphs):
    text = para.text.strip()
    if text:
        lines.append(f"[P{i}] ({para.style.name}) {text}")

for ti, table in enumerate(doc.tables):
    lines.append("")
    lines.append(f"===== TABLE {ti} rows={len(table.rows)} =====")
    for ri, row in enumerate(table.rows):
        cells = []
        seen = set()
        for cell in row.cells:
            if id(cell._tc) in seen:
                continue
            seen.add(id(cell._tc))
            cells.append(cell.text.replace("\n", " / ").strip())
        lines.append(f"R{ri}: " + " | ".join(cells))

with open(OUT, "w", encoding="utf-8") as f:
    f.write("\n".join(lines))

print(OUT, len(lines))
